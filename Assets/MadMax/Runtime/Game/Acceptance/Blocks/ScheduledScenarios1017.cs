using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-17: wet ground under the sprinkler, sawdust in use (sacks, the sawdust toilet,
    /// stable bedding, soaking spills), kept animals eating scraps off the ground, people noticing the smell, watching
    /// TV together, the neighbour's lamp and ALL WELL? on the radio.</summary>
    public static class ScheduledScenarios1017
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new SprinklerWetGround();
            yield return new SawdustUses();
            yield return new AnimalScraps();
            yield return new SmellAndCompany();
            yield return new WatchTogether();
            yield return new NeighbourLamp();
        }
    }

    /// <summary>A spray leaves its water on the ground across the arc: the soil soaks it and shows darker.</summary>
    class SprinklerWetGround : Scenario
    {
        public override string Id => "living.sprinkler_wet";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(11f);
            var fwd = ScheduledScenarios1016.Fwd(g);
            // on soil (the claim is that the soil soaks it; paving only gets a film that runs off)
            var t = DeformableTerrain.Instance;
            Vector3 at = g.Player.transform.position + fwd * 5f; bool soil = false;
            for (int ring = 0; ring < 6 && !soil; ring++)
                for (int k = 0; k < 12 && !soil; k++)
                {
                    var q = g.Player.transform.position + Quaternion.Euler(0f, k * 30f, 0f) * fwd * (5f + ring * 5f);
                    bool ok = true;
                    for (int j = 0; j < 8 && ok; j++)
                    {
                        var e = q + Quaternion.Euler(0f, j * 45f, 0f) * Vector3.forward * 2.2f;
                        t.SeepAt(e.x, e.z, out float cp, out _);
                        ok = cp > 0f && t.WaterDepthNoLoad(e.x, e.z) <= 0f;
                    }
                    if (ok) { at = q; soil = true; }
                }
            if (!soil) { c.Block("no open soil near the start"); yield break; }
            var sp = PH.Piece(g, "sprinkler", at, -fwd);
            if (!c.Check(sp, "a sprinkler")) yield break;
            var s = sp.GetComponent<Sprinkler>();
            float pool0 = Spills.TotalPool + Spills.TotalSoaked;
            s.WetGround();
            s.Spray(Sprinkler.SpraySeconds);
            float poured = Spills.TotalPool + Spills.TotalSoaked - pool0;                        // as it lands (films then spread thin and dry)
            DeformableTerrain.Instance.SeepAt(sp.transform.position.x + 2f, sp.transform.position.z, out float cap, out float rate);
            c.Note($"soil capacity {cap} L/m2 rate {rate}; pool {Spills.TotalPool:0.00} soaked {Spills.TotalSoaked:0.00}; wet now {WetHeadings(sp, s)}/24");
            c.Metric("sprinkler_wet_litres", poured, "L");
            c.Check(s.Wettings == 1 && poured > Sprinkler.WetLitres * 0.8f, $"one spray put its water on the ground ({poured:0.0} L)");
            yield return new WaitForSeconds(2f);
            c.Metric("sprinkler_wet_after_2s", Spills.TotalPool + Spills.TotalSoaked - pool0, "L");
            c.Note($"after 2 s: pool {Spills.TotalPool:0.00} soaked {Spills.TotalSoaked:0.00}");
            int wet = 0;
            for (int i = 0; i < 24; i++)
            {
                var p = sp.transform.TransformPoint(Quaternion.Euler(0f, -Sprinkler.Arc + i * Sprinkler.Arc * 2f / 23f, 0f) * Vector3.forward * (s.reach * 0.6f));
                for (float r = -0.9f; r <= 0.9f; r += 0.3f) if (Spills.WetAt(p.x + r, p.z) > 0.05f || Spills.WetAt(p.x, p.z + r) > 0.05f) { wet++; break; }
            }
            c.Check(wet >= 3, $"the ground is wet in patches across the arc ({wet}/24 headings)");
            yield return ScheduledScenarios1015.Shoot(c, g, sp.transform.position, sp.transform.right, 6f, "sprinkler_wet");
            Object.Destroy(sp.gameObject);
        }

        static int WetHeadings(Placeable sp, Sprinkler s)
        {
            int wet = 0;
            for (int i = 0; i < 24; i++)
            {
                var p = sp.transform.TransformPoint(Quaternion.Euler(0f, -Sprinkler.Arc + i * Sprinkler.Arc * 2f / 23f, 0f) * Vector3.forward * (s.reach * 0.6f));
                for (float r = -0.9f; r <= 0.9f; r += 0.3f) if (Spills.WetAt(p.x + r, p.z) > 0.05f || Spills.WetAt(p.x, p.z + r) > 0.05f) { wet++; break; }
            }
            return wet;
        }
    }

    /// <summary>Sawdust bags into sacks; a sack covers the latrine (fewer flies), beds the stable (more manure) and soaks
    /// up a spill.</summary>
    class SawdustUses : Scenario
    {
        public override string Id => "living.sawdust_uses";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(12f);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var saw = PH.Piece(g, "saw_bench", me + fwd * 4f - right * 3f, -fwd);
            if (!c.Check(saw, "a saw bench")) yield break;
            var dust = saw.GetComponent<Sawdust>() ? saw.GetComponent<Sawdust>() : saw.gameObject.AddComponent<Sawdust>();
            dust.amount = 2.2f;
            int n0 = g.Inventory.GetItem(Sawdust.Sack);
            int bagged = dust.Bag(g.Inventory);
            c.Check(bagged == 4 && g.Inventory.GetItem(Sawdust.Sack) == n0 + 4, $"the pile fills sacks ({bagged})");
            c.Check(ItemCatalog.Name(Sawdust.Sack) == "SACK OF SAWDUST", "named " + ItemCatalog.Name(Sawdust.Sack));
            // the sawdust toilet
            var lat = PH.Piece(g, "latrine", me + fwd * 4f, -fwd);
            if (!c.Check(lat, "a latrine")) yield break;
            var l = lat.GetComponent<Latrine>();
            l.fresh = 120f;
            c.Check(!l.Covered, "an open pit");
            c.Check(l.Cover(g.Inventory) && l.Covered, "a scoop of sawdust covers it");
            // stable bedding
            var st = PH.Piece(g, "stable", me + fwd * 9f + right * 4f, -fwd);
            if (!c.Check(st, "a stable")) yield break;
            var stable = st.GetComponent<Stable>(); var manger = st.GetComponent<Trough>();
            int sacks = g.Inventory.GetItem(Sawdust.Sack);
            c.Check(manger.StableOf() == stable, "the manger knows its stable");
            c.Check(stable.Bed(g.Inventory) && stable.Bedded && g.Inventory.GetItem(Sawdust.Sack) == sacks - 1, "a sack spread as bedding");
            c.Check(!stable.Bed(g.Inventory), "not again the same day");
            var bed = st.transform.Find("Bedding");
            c.Check(bed && bed.gameObject.activeSelf, "the bedding shows on the stall floor");
            manger.manure = 4;
            int m0 = g.Inventory.GetItem("farm_manure");
            manger.Use(g, true);
            c.Check(g.Inventory.GetItem("farm_manure") - m0 == 6, $"soiled bedding comes out with the dung ({g.Inventory.GetItem("farm_manure") - m0})");
            yield return ScheduledScenarios1015.Shoot(c, g, st.transform.position + Vector3.up * 0.8f, right, 7f, "stable_bedding");
            // a spill soaked up
            var at = g.Player.transform.position + g.Player.transform.forward * 0.6f;
            Spills.Pour(at, ResourceType.Oil, 3f);
            yield return new WaitForSeconds(0.5f);
            float before = Spills.PoolNear(at, WastelandGame.SawdustReach, out _, 0.01f);
            sacks = g.Inventory.GetItem(Sawdust.Sack);
            bool used = g.UseSawdust();
            float after = Spills.PoolNear(at, WastelandGame.SawdustReach, out _, 0.01f);
            c.Check(used && after < before * 0.5f && g.Inventory.GetItem(Sawdust.Sack) == sacks - 1, $"a sack soaks up spilled oil ({before:0.0} -> {after:0.0} L)");
            int extra = g.Inventory.GetItem(Sawdust.Sack) - n0;
            for (int i = 0; i < extra; i++) g.Inventory.TakeItem(Sawdust.Sack);
            Object.Destroy(saw.gameObject); Object.Destroy(lat.gameObject); Object.Destroy(st.gameObject);
        }
    }

    /// <summary>A kept pig trots to food lying in its yard and eats it; three scraps feed it for the day.</summary>
    class AnimalScraps : Scenario
    {
        public override string Id => "animals.scraps";
        public override float Timeout => 45f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(10f);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var pigDef = AnimalLibrary.Get("pig");
            if (!c.Check(pigDef != null, "pigs exist")) yield break;
            var home = PH.Ground(me + fwd * 6f);
            var pig = Animal.Spawn(pigDef, home, 0f, g.propMaterial, "t:pig");
            pig.owned = true; pig.home = home; pig.born = DayNight.Day - 20; pig.fedDay = -1;
            c.Check(pig.ScrapEater && pig.EatsScrap("food_rotten") && !pig.EatsScrap("tool_wrench"), "a pig eats rotten food, not tools");
            int eaten0 = Animal.ScrapsEaten;
            var scraps = new List<WorldItem>();
            for (int i = 0; i < 3; i++)
                scraps.Add(WorldItem.Create("food_rotten", 1, -1f, g.propMaterial, home + right * (3f + i * 0.6f) + fwd * 1.5f + Vector3.up * 0.3f, Quaternion.identity));
            yield return PH.Until(() => pig.ScrapTarget != null, 6f);
            c.Check(pig.ScrapTarget != null, "the pig spots the scraps and goes for them");
            yield return PH.Until(() => pig.EatingScraps, 12f);
            c.Check(pig.EatingScraps, "and roots at them");
            yield return ScheduledScenarios1015.Shoot(c, g, pig.transform.position, right, 4f, "pig_scraps");
            yield return PH.Until(() => Animal.ScrapsEaten - eaten0 >= 3, 25f);
            yield return null;                                                                   // Destroy lands at the end of the frame
            int ate = Animal.ScrapsEaten - eaten0;
            c.Metric("scraps_eaten", ate, "");
            c.Check(ate >= 3 && scraps.All(s => !s), $"the pig cleared the yard ({ate} eaten)");
            c.Check(pig.fedDay == DayNight.Day, "three scraps feed a pig for the day");
            foreach (var s in scraps) if (s) Object.Destroy(s.gameObject);
            Object.Destroy(pig.gameObject);
        }
    }

    /// <summary>An unwashed player: a proud stranger takes it badly on a first meeting, someone close by says so, and
    /// a washed player passes unremarked.</summary>
    class SmellAndCompany : Scenario
    {
        public override string Id => "npc.smell";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(12f);
            bool wasRain = Weather.Raining; Weather.Raining = false;
            var me = g.Player.transform.position;
            if (Weather.TemperatureAt(me.z) < 6f) { c.Block("too cold for flies here"); Weather.Raining = wasRain; yield break; }
            float hy = g.Stats.hygiene;
            g.Stats.hygiene = 95f;
            c.Check(g.SmellReaction(Temper.Proud) == 0 && g.SmellLook(me) == null, "washed: nobody minds");
            g.Stats.hygiene = 5f;
            c.Check(g.PlayerFlies() > 0.7f, "unwashed: flies");
            c.Check(g.SmellReaction(Temper.Proud) < g.SmellReaction(Temper.Friendly) && g.SmellReaction(Temper.Joker) == 0, $"temperaments take it differently (proud {g.SmellReaction(Temper.Proud)}, friendly {g.SmellReaction(Temper.Friendly)}, joker 0)");
            var fwd = ScheduledScenarios1016.Fwd(g);
            WastelandGame.LastSmellRemark = null;
            var n = ScheduledScenarios1012.Person(g, "smell:resident", NpcRole.Resident, 9113, me + fwd * 1.5f);
            yield return new WaitForSeconds(0.5f);
            me = g.Player.transform.position;
            string remark = g.SmellLook(me) ?? WastelandGame.LastSmellRemark;                     // the game's own 1 Hz look may have got there first
            c.Note(remark ?? "(no remark)");
            c.Check(remark != null && NpcVoice.Captions.Any(k => k.text == remark), "someone close by remarks on it");
            c.Check(g.SmellLook(me) == null, "once a day each");
            var talk = new Dialogue(g, n);
            c.Note(talk.line);
            c.Check(talk.line.Contains("NOSE") || talk.line.Contains("FLIES"), "the first meeting mentions it");
            g.Stats.hygiene = hy;
            Weather.Raining = wasRain;
            Object.Destroy(n.gameObject);
        }
    }

    /// <summary>Sitting beside someone who is watching a tape gets a word about the programme; the karate tape a guard
    /// lesson.</summary>
    class WatchTogether : Scenario
    {
        public override string Id => "living.watch_together";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(20f);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var tvp = PH.Piece(g, "tv", me + fwd * 6f, -fwd);
            var chair = PH.Piece(g, "chair", me + fwd * 3.6f, fwd);
            if (!c.Check(tvp && chair, "a TV and a chair")) yield break;
            var tv = tvp.GetComponent<TvSet>();
            tv.Play("vhs_karate");
            var n = ScheduledScenarios1012.Person(g, "tv:friend", NpcRole.Resident, 5150, me + fwd * 2f + right * 2.5f);
            n.home = n.transform.position;
            n.State.Set(NpcSave.Met);
            yield return PH.Until(() => n.Watching == tv, 20f);
            if (!c.Check(n.Watching == tv, "a neighbour comes to watch")) { Object.Destroy(n.gameObject); yield break; }
            var beside = n.transform.position + right * 1.2f;
            c.Check(g.WatchTogether(beside + right * 6f) == null, "too far away: nothing said");
            float melee = g.Stats.skillXp[(int)MadMax.RPG.Skill.Melee];
            string line = g.WatchTogether(beside);
            c.Note(line ?? "(silence)");
            c.Check(line != null && NpcVoice.Captions.Any(k => k.text == line), "sitting by them gets a word about the programme");
            c.Check(g.Stats.skillXp[(int)MadMax.RPG.Skill.Melee] > melee, "the karate tape is a guard lesson");
            c.Check(g.WatchTogether(beside) == null, "once per programme and day");
            yield return ScheduledScenarios1015.Shoot(c, g, n.transform.position + Vector3.up * 1f, right, 3.5f, "watch_together");
            tv.Stop();
            Object.Destroy(n.gameObject);
            Object.Destroy(tvp.gameObject); Object.Destroy(chair.gameObject);
        }
    }

    /// <summary>The watching neighbour shows on the map and the HOME ledger (lamp lit at night) and answers ALL WELL?
    /// by day; asleep after 23 h.</summary>
    class NeighbourLamp : Scenario
    {
        public override string Id => "npc.neighbour_lamp";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var town = g.NearestTown(me);
            if (town == null || Vector2.Distance(town.pos, new Vector2(me.x, me.z)) > WastelandGame.WatchReach - 200f) { c.Block("no town within watching reach"); yield break; }
            var fwd = ScheduledScenarios1016.Fwd(g);
            var flag = PH.Piece(g, "claim_flag", me + fwd * 4f, -fwd);
            if (!c.Check(flag, "a claim")) yield break;
            var claim = flag.GetComponent<ClaimFlag>();
            g.NeighbourErrands.Clear();
            c.Check(g.AskAllWell() == null && g.NeighbourLedgerLine(claim) == null, "no neighbour watching: nothing");
            g.NeighbourErrands["r" + town.index + ",0"] = new WastelandGame.NeighbourErrand { kind = 0, state = 2, due = -1, town = town.index, name = "OLD MAE" };
            DayNight.SetHours(20.5f);
            yield return null;
            var pins = new List<WastelandGame.Pin>();
            g.NeighbourPins(pins);
            c.Note(string.Join(" / ", pins.Select(p => p.label)));
            c.Check(pins.Any(p => p.label.Contains("OLD MAE") && p.label.Contains("LAMP")), "at night her lamp shows on the map");
            var ledger = new List<string>();
            g.HomeLedger(ledger);
            c.Check(ledger.Any(l => l.Contains("NEIGHBOUR: OLD MAE") && l.Contains("LAMP")), "and on the HOME line");
            float next = BaseRaid.NextDay; BaseRaid.NextDay = -1f;
            DayNight.SetHours(14f);
            yield return null;
            string a = g.AskAllWell();
            c.Note(a ?? "(none)");
            c.Check(a != null && a.Contains("QUIET") && WastelandGame.LastNeighbourWatch != null && WastelandGame.LastNeighbourWatch.Contains("OLD MAE"), "ALL WELL? by day: she answers on the home frequency");
            DayNight.SetHours(2f);
            yield return null;
            string b = g.AskAllWell();
            c.Check(b != null && b.Contains("ASLEEP"), "at 2 am: no answer");
            BaseRaid.NextDay = next;
            g.NeighbourErrands.Clear();
            Object.Destroy(flag.gameObject);
        }
    }
}
