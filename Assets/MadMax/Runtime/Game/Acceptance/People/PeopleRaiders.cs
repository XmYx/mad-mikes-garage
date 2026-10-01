using System.Collections;
using System.Linq;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Raiders on the road (roadmap 20/21): standing on foot ahead of a raider convoy on its road (out of town),
    /// the gang spots the player, closes in, and the boss walks up to demand a toll; the parley is the talk page ([E]).
    /// Paying 15 L of fuel costs exactly that, sends them on their way, buys the bases a few days' peace and a little
    /// standing with the gang, and the truce holds while the player stands next to them. A second gang is bluffed (the
    /// Fuel Guild lie, CHA 10): fooled they leave, caught out they attack.</summary>
    class PeopleRaiders : Scenario
    {
        public override string Id => "people.raiders";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            PH.Attribute(c, g, Attr.Charisma, 10);
            var dir = NpcDirector.Instance;
            if (!dir) { c.Block("no NPC director"); yield break; }
            var gangs = dir.Convoys.Where(k => k.raiders && k.phase == Convoy.Phase.Travel && !k.Spawned && !k.Friendly).ToList();
            c.Metric("raider_gangs", gangs.Count, "");
            if (!c.Check(gangs.Count > 0, "raider gangs ride the roads")) yield break;

            // ---- the first gang: pay the toll
            var first = gangs[0];
            yield return Meet(c, first);
            if (c.Failed) yield break;
            var boss = first.Boss;
            string gang = first.Gang;
            var side = Factions.OfGang(gang);
            c.Check(boss.Profile.role == NpcRole.RaiderBoss && boss.Prompt(g).StartsWith("[E] PARLEY WITH"), "the boss walks up: " + boss.Prompt(g));
            PH.Grant(c, g, "res:" + (int)ResourceType.Fuel, 15);
            int fuel = g.Inventory.Get(ResourceType.Fuel), rep = Factions.Rep(side), day = DayNight.Day;
            boss.Use(g, false);
            if (!c.Check(g.Menus.Current == MenuSystem.Page.Talk && first.phase == Convoy.Phase.Parley, "[E] opens the parley")) yield break;
            c.Check(NpcLore.RaiderDemand.Any(d => PH.Line(g) == d.Replace("{GANG}", gang)), "a toll demand: " + PH.Line(g));
            c.Check(g.Menus.Labels().Any(r => r.StartsWith("(PAY) 15 LITRES")) && g.Menus.Labels().Any(r => r.StartsWith("[CHA 7] (LIE)")) && g.Menus.Labels().Any(r => r.StartsWith("(INSULT)")),
                "pay, bluff or insult: " + PH.Rows(g));
            c.Screenshot("parley");
            yield return null;
            g.Menus.Pick("(PAY) 15 LITRES");
            c.Check(g.Inventory.Get(ResourceType.Fuel) == fuel - 15, "15 L of fuel paid, not a drop more");
            c.Check(first.phase == Convoy.Phase.Leave && first.save.spareUntil >= day + 3, $"they move on and spare the bases till day {first.save.spareUntil}");
            c.Check(Factions.Rep(side) == Mathf.Clamp(rep + 3, -100, 100), $"{Factions.Names[(int)side]} standing {rep} -> {Factions.Rep(side)}");
            c.Check(NpcRegistry.Get(first.crew[0]).Has(NpcSave.Parleyed), "the boss remembers the deal");
            g.Menus.Pick("(LEAVE)");
            if (g.Menus.IsOpen) g.Menus.Close();
            float t0 = Time.time;
            bool held = true;
            while (Time.time - t0 < 6f) { if (first.phase == Convoy.Phase.Attack || first.phase == Convoy.Phase.Confront) held = false; yield return null; }
            c.Check(held, "the truce holds (" + first.phase + ")");

            // ---- a second gang: the bluff
            var second = gangs.Skip(1).FirstOrDefault(k => k.phase == Convoy.Phase.Travel && !k.Spawned);
            if (second == null) { c.Note("only one gang on the roads: the bluff is not tried"); yield break; }
            yield return Meet(c, second);
            if (c.Failed) yield break;
            second.Boss.Use(g, false);
            if (!c.Check(g.Menus.Labels().Any(r => r.StartsWith("[CHA 7] (LIE)")), "the lie is offered with CHA 10: " + PH.Rows(g))) yield break;
            int rep2 = Factions.Rep(Factions.OfGang(second.Gang));
            g.Menus.Pick("[CHA 7] (LIE)");
            string said = PH.Line(g);
            bool fooled = said.StartsWith("THE GUILD");
            c.Note((fooled ? "fooled: " : "caught out: ") + said);
            if (fooled) c.Check(second.phase == Convoy.Phase.Leave && second.save.spareUntil >= DayNight.Day + 1 && Factions.Rep(Factions.OfGang(second.Gang)) == rep2, "fooled, they leave (a day's peace, no standing gained)");
            else c.Check(second.phase == Convoy.Phase.Attack && Factions.Rep(Factions.OfGang(second.Gang)) == rep2 - 5, "caught lying: they attack, and the gang remembers (-5)");
            if (g.Menus.IsOpen) g.Menus.Close();
        }

        /// <summary>Stand on the gang's road ahead of it (out of town) and wait for the boss to come and make the demand.</summary>
        static IEnumerator Meet(ScenarioContext c, Convoy k)
        {
            var g = c.Game;
            Vector3 spot = default; bool found = false; float skipped = 0f;
            for (float ahead = 90f; ahead < 2500f && !found; ahead += 45f)
            {
                var p = k.PointAt(k.travel + ahead, out var d);
                var q = k.PointAt(k.travel, out _);
                if (g.World.SettlementAt(p.x, p.z) != null || g.World.SettlementAt(q.x, q.z) != null) { k.travel += 45f; skipped += 45f; continue; }   // past the town
                spot = p + Vector3.Cross(Vector3.up, d).normalized * 3f; found = true;
            }
            if (!found) { c.Block("the " + k.Gang + " road runs through towns all the way"); yield break; }
            if (skipped > 0f) c.Fixture("the " + k.Gang + " moved " + skipped.ToString("0") + " m on along their road (out of a town)");
            yield return PH.Go(c, spot, PH.Yaw(k.PointAt(k.travel, out _) - spot), "the " + k.Gang + "'s road, ahead of them", 2f);
            float t0 = Time.time;
            while (Time.time - t0 < 70f)
            {
                var b = k.Boss;
                if (b && b.Alive && PH.Flat(b.transform.position, g.Player.transform.position) < 5.8f && k.phase == Convoy.Phase.Confront) break;
                if (k.phase == Convoy.Phase.Attack || k.phase == Convoy.Phase.Gone) break;
                yield return null;
            }
            c.Metric("boss_arrived_" + k.id, Time.time - t0, "s");
            var lead = k.cars.FirstOrDefault(a => a);
            c.Note($"{k.Gang}: phase {k.phase}, {k.cars.Count} cars, lead {(lead ? PH.Flat(lead.transform.position, g.Player.transform.position).ToString("0") + " m (" + lead.goal + ")" : "-")}, boss {(k.Boss ? PH.Flat(k.Boss.transform.position, g.Player.transform.position).ToString("0.0") + " m" : "-")}");
            c.Check(k.Spawned, "the " + k.Gang + " come down the road");
            c.Check(k.phase == Convoy.Phase.Confront && k.Boss && k.Boss.Alive, "they spot you and the boss walks up to talk (" + k.phase + ")");
        }
    }
}
