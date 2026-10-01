using System.Collections;
using System.Linq;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Fighting on foot with the player's own swing (<see cref="PlayerCharacter.Attack"/>, the LMB path): a
    /// revolver fires one round per pull, hurts a raider, empties, reloads six from the pack; a stoppage fires nothing
    /// and [R] clears it; a machete cuts at arm's length; a raider in a kevlar vest loses less to the same shot; a beaten
    /// raider throws down the weapon and is let go through the talk page (standing with the gang rises); a dead raider's
    /// body can be searched.</summary>
    class PeopleCombat : Scenario
    {
        public override string Id => "people.combat";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            if (!TestWorld.Pad(8f, 30f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            yield return PH.Go(c, pad, PH.Yaw(dir), "an open pad");
            if (g.Stats.health < g.Stats.MaxHealth) { g.Stats.health = g.Stats.MaxHealth; c.Fixture("player at full health"); }

            // ---- the revolver
            PH.Grant(c, g, "tool_revolver", 1);
            PH.Grant(c, g, "ammo_cartridge", 12);
            g.UseItem("tool_revolver");
            yield return PH.Until(() => g.Player.Tool is RangedTool r && r.id == "tool_revolver" && !r.Reloading && g.Rounds(r.id) > 0, 6f);
            var gun = g.Player.Tool as RangedTool;
            if (!c.Check(gun && gun.id == "tool_revolver" && g.Rounds(gun.id) == gun.magazine, $"revolver drawn and loaded ({(gun ? g.Rounds(gun.id) : 0)} rounds)")) yield break;
            c.Check(gun.Reserve(g) == 12 - gun.magazine, $"loading took {gun.magazine} from the pack ({gun.Reserve(g)} left)");
            var target = PH.Raider(g, "test:combat:a", 9001, pad + dir * 5f, PH.Yaw(-dir), false, false);
            c.Fixture("an unarmed, unarmoured raider spawned 5 m ahead");
            yield return new WaitForSeconds(0.3f);
            float hp = target.Health;
            int hits = 0, fired = 0;
            for (int i = 0; i < gun.magazine && target.Alive; i++)
            {
                if (gun.Jammed) { gun.ReloadKey(g); yield return PH.Until(() => !gun.Reloading, 3f); }
                Face(g, target);
                int before = g.Rounds(gun.id); float h = target.Health;
                g.Player.Attack(false);
                yield return PH.Until(() => !g.Player.Swinging, 2f);
                if (g.Rounds(gun.id) == before - 1) fired++;
                if (target.Health < h) hits++;
                yield return new WaitForSeconds(0.1f);
            }
            c.Metric("shots", fired, ""); c.Metric("hits", hits, "");
            c.Check(fired >= 4 && hits >= 1, $"each pull fires one round ({fired} fired), and they land ({hits} hits, {Mathf.Max(0f, target.Health):0}/{hp:0} health)");
            if (target.Alive)
            {
                target.ApplyHit(target.transform.position + Vector3.up, dir, 20f, 0.2f, g.Player.gameObject);
                c.Fixture("the raider finished off");
            }
            yield return null;
            var body = target.GetComponentsInChildren<Lootable>(true).FirstOrDefault();
            c.Check(!target.Alive && body && body.table == "raider", "the dead raider's body can be searched (raider loot)");

            // ---- empty, reload, jam
            if (gun.Jammed) gun.ReloadKey(g);
            yield return PH.Until(() => !gun.Reloading, 3f);
            g.SetRounds(gun.id, 0);
            c.Fixture("the cylinder emptied");
            int reserve = gun.Reserve(g);
            gun.Strike(g.Player);                                                                    // an empty pull starts the reload
            c.Check(gun.Reloading, "an empty pull starts the reload");
            yield return PH.Until(() => !gun.Reloading && g.Rounds(gun.id) > 0, 6f);
            c.Check(g.Rounds(gun.id) == Mathf.Min(gun.magazine, reserve) && gun.Reserve(g) == reserve - g.Rounds(gun.id), $"reloaded {g.Rounds(gun.id)} from the pack ({gun.Reserve(g)} left)");
            gun.ForceJam();
            c.Fixture("a bad round (forced stoppage)");
            int r0 = g.Rounds(gun.id);
            g.Player.Attack(false);
            yield return PH.Until(() => !g.Player.Swinging, 2f);
            c.Check(gun.Jammed && g.Rounds(gun.id) == r0, "a stoppage fires nothing");
            gun.ReloadKey(g);
            c.Check(!gun.Jammed, "[R] clears it");
            yield return PH.Until(() => !gun.Reloading, 3f);

            // ---- armour soaks
            var bare = PH.Raider(g, "test:combat:bare", 777, pad + dir * 8f - Vector3.Cross(Vector3.up, dir) * 3f, PH.Yaw(-dir), false, false, false);
            var clad = PH.Raider(g, "test:combat:clad", 777, pad + dir * 8f + Vector3.Cross(Vector3.up, dir) * 3f, PH.Yaw(-dir), false, false, false, "vest_kevlar");
            yield return null;
            c.Fixture("two raiders alike but for a kevlar vest (calm, side by side)");
            float hb = bare.Health, hc = clad.Health;
            bare.ApplyHit(bare.transform.position + Vector3.up * 1.2f, dir, 0.65f, 0.1f, g.Player.gameObject);
            clad.ApplyHit(clad.transform.position + Vector3.up * 1.2f, dir, 0.65f, 0.1f, g.Player.gameObject);
            float lostBare = hb - bare.Health, lostClad = hc - clad.Health;
            c.Metric("shot_damage_bare", lostBare, "hp"); c.Metric("shot_damage_kevlar", lostClad, "hp");
            c.Check(lostClad < lostBare * 0.9f, $"the vest soaks a revolver round: {lostClad:0.0} vs {lostBare:0.0} hp");

            // ---- the machete
            PH.Grant(c, g, "tool_machete", 1);
            g.UseItem("tool_machete");
            yield return PH.Until(() => g.Player.Tool && g.Player.Tool.id == "tool_machete", 3f);
            if (!c.Check(g.Player.Tool && g.Player.Tool.id == "tool_machete", "machete in hand")) yield break;
            var me = g.Player.transform.position;
            var foe = PH.Raider(g, "test:combat:melee", 31337, me + g.Player.transform.forward * 1.1f, g.Player.transform.eulerAngles.y + 180f, false, false);
            c.Fixture("a raider at arm's length");
            yield return null;
            float hm = foe.Health;
            for (int i = 0; i < 3 && foe.Alive && foe.Health >= hm; i++)
            {
                Face(g, foe);
                g.Player.Attack(false);
                yield return PH.Until(() => !g.Player.Swinging, 2f);
            }
            c.Check(foe.Health < hm, $"the machete cuts ({Mathf.Max(0f, foe.Health):0}/{hm:0})");

            // ---- beaten, they give up; mercy
            if (foe.Alive && foe.Health > foe.MaxHealth * 0.23f) foe.ApplyHit(foe.transform.position + Vector3.up, dir, (foe.Health - foe.MaxHealth * 0.23f) / 28f, 0.1f, g.Player.gameObject);
            for (int i = 0; i < 20 && foe.Alive && !foe.Surrendered; i++) foe.ApplyHit(foe.transform.position + Vector3.up, dir, 0.04f, 0.1f, g.Player.gameObject);
            c.Fixture("blows until they break (light hits below a quarter health)");
            if (!c.Check(foe.Alive && foe.Surrendered && !foe.Hostile, $"the beaten raider throws down the weapon ({foe.Health:0} hp)")) yield break;
            yield return new WaitForSeconds(0.5f);
            var gangSide = Factions.OfGang(foe.Profile.gang);
            int rep = Factions.Rep(gangSide);
            yield return PH.Face(c, foe, 1.6f);
            if (!c.Check(PH.Talk(g, foe) && foe.Prompt(g).Contains("SURRENDERED"), "[E] on the one who gave up: " + PH.Line(g))) yield break;
            if (!c.Check(g.Menus.Pick("GET OUT OF HERE."), "let them go: " + PH.Rows(g))) yield break;
            c.Check(foe.leaving && Factions.Rep(gangSide) == Mathf.Clamp(rep + 5, -100, 100), $"mercy: they walk away; the {foe.Profile.gang} remember ({rep} -> {Factions.Rep(gangSide)})");
            if (g.Menus.IsOpen) g.Menus.Close();
            c.Screenshot("combat");
            yield return null;
        }

        static void Face(WastelandGame g, MadMax.Npc.Npc n)
        {
            var d = n.transform.position - g.Player.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) g.Player.transform.rotation = Quaternion.LookRotation(d);
        }

    }
}
