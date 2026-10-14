using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Melee guard and directional reactions (roadmap 13).</summary>
    public static class GuardScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MeleeGuard();
        }
    }

    /// <summary>A raider's blows against a machete held up: an early guard blocks (no harm, stamina spent), a guard raised
    /// just before the blow parries (the raider staggers), a blow from behind gets through and the player gives way
    /// forward (the reaction knows the side).</summary>
    class MeleeGuard : Scenario
    {
        public override string Id => "combat.guard";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            if (!TestWorld.Pad(8f, 30f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            yield return PH.Go(c, pad, PH.Yaw(dir), "an open pad");
            PH.Grant(c, g, MadMax.Items.ItemIds.Machete, 1);
            g.Player.Equip(ToolLibrary.Create(MadMax.Items.ItemIds.Machete, g.propMaterial));
            var P = g.Player;
            var me = P.transform.position;
            var foe = PH.Raider(g, "test:guard", 5151, me + P.transform.forward * 1.3f, P.transform.eulerAngles.y + 180f, true, false, false);
            c.Fixture("an armed, calm raider 1.3 m ahead, made to swing at the player");
            void Pin(Vector3 at)
            {                                                                                   // stands its ground 1.3 m off, facing the player
                at.y = foe.transform.position.y;
                var face = Vector3.ProjectOnPlane(P.transform.position - at, Vector3.up);
                float yaw = Quaternion.LookRotation(face).eulerAngles.y;
                foe.mode = MadMax.Npc.Npc.Mode.Stand; foe.home = at; foe.homeYaw = yaw;
                foe.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            }
            Pin(me + P.transform.forward * 1.3f);
            IEnumerator Hold(float seconds, Vector3 at)
            {                                                                                   // its routine would walk it off: pinned each frame
                float until = Time.time + seconds;
                while (Time.time < until) { Pin(at); yield return null; }
            }
            yield return new WaitForSeconds(0.6f);

            // ---- block: the guard has been up a while
            g.Stats.stamina = g.Stats.MaxStamina;
            float hp = g.Stats.health, st = g.Stats.stamina;
            g.ForceGuard = true;
            yield return new WaitForSeconds(0.5f);
            var front = P.transform.position + P.transform.forward * 1.3f;
            Pin(front);
            c.Check(g.Guarding, "the guard comes up (melee tool, forced)");
            foe.BeginSwing();
            yield return Hold(foe.StrikeDelay + 0.2f, front);
            var fp = foe.transform.position; var pp = P.transform.position;
            string why = $"d {Vector3.Distance(pp, fp):0.00} m, facing {Vector3.Dot(Vector3.ProjectOnPlane(pp - fp, Vector3.up).normalized, foe.transform.forward):0.00}, car {(g.Current ? g.Current.name : "-")}, foe {foe.DebugFoe}, mode {foe.DebugMode}";
            c.Check(g.LastGuard == "block" && g.Stats.health >= hp && g.Stats.stamina < st,
                $"a late-held guard blocks ({g.LastGuard}; health {g.Stats.health:0}/{hp:0}, stamina {g.Stats.stamina:0}/{st:0}; {why})");
            c.Screenshot("guard_block");
            yield return null;

            // ---- parry: raised just before the blow
            g.ForceGuard = false;
            yield return Hold(0.8f, front);
            foe.BeginSwing();
            float hitAt = Time.time + foe.StrikeDelay;
            while (Time.time < hitAt - 0.2f) { Pin(front); yield return null; }                  // frame by frame: up just before the blow
            g.ForceGuard = true;
            float waitTo = Time.time + 1f;
            while (g.LastGuard != "parry" && Time.time < waitTo) { Pin(front); yield return null; }
            c.Check(g.LastGuard == "parry" && (foe.Staggering || foe.Reacting == "stagger"), $"a guard raised as the blow comes parries it ({g.LastGuard}; raider staggering {foe.Staggering})");
            g.ForceGuard = false;

            // ---- from behind: through the guard, the body gives way forward
            yield return new WaitForSeconds(2f);
            P.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(P.transform.position - foe.transform.position, Vector3.up));
            g.ForceGuard = true;
            yield return new WaitForSeconds(0.5f);
            hp = g.Stats.health;
            var behind = P.transform.position - P.transform.forward * 1.3f;                  // behind the player
            Pin(behind);
            foe.BeginSwing();
            yield return Hold(foe.StrikeDelay + 0.1f, behind);
            g.ForceGuard = false;
            c.Check(g.Stats.health < hp, $"a blow from behind gets past the guard ({g.Stats.health:0}/{hp:0})");
            c.Check(P.LastHitFrom.z < -0.5f, $"the reaction knows it came from behind (local from {P.LastHitFrom})");
        }
    }
}
