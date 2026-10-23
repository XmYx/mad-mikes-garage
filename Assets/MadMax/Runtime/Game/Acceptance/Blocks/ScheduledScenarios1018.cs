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
    /// <summary>Scheduled update 2026-10-18: the chores saved (latrine cover, stable bedding), animals bedding down in a
    /// strawed stable, crows at the sprinkler's puddles, the slop bucket the pigs empty, washing as a moment, and the
    /// neighbour calling in.</summary>
    public static class ScheduledScenarios1018
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ChoresSaved();
            yield return new AnimalsBedding();
            yield return new PuddleBirds();
            yield return new SlopBucketPigs();
            yield return new WashMoment();
            yield return new NeighbourCallsIn();
        }
    }

    /// <summary>A covered latrine and a bedded stable come back covered and bedded from their saved state; the old
    /// latrine format still loads.</summary>
    class ChoresSaved : Scenario
    {
        public override string Id => "living.chores_saved";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var lat = PH.Piece(g, "latrine", me + fwd * 4f, -fwd);
            var st = PH.Piece(g, "stable", me + fwd * 9f + right * 5f, -fwd);
            if (!c.Check(lat && st, "a latrine and a stable")) yield break;
            g.Inventory.AddItem(Sawdust.Sack, 2);
            var l = lat.GetComponent<Latrine>(); var stable = st.GetComponent<Stable>();
            c.Check(l.Cover(g.Inventory) && stable.Bed(g.Inventory), "covered and bedded");
            string ls = lat.SaveState(), ss = st.SaveState();
            c.Note("latrine " + ls + " / stable " + ss);
            var lat2 = PH.Piece(g, "latrine", me + fwd * 4f + right * 2.5f, -fwd);
            var st2 = PH.Piece(g, "stable", me + fwd * 9f - right * 5f, -fwd);
            yield return null;
            lat2.LoadState(ls); st2.LoadState(ss);
            yield return null;
            c.Check(lat2.GetComponent<Latrine>().Covered, "the latrine comes back covered");
            var s2 = st2.GetComponent<Stable>();
            c.Check(s2.Bedded && Mathf.Abs(s2.beddedAt - stable.beddedAt) < 0.01f, "the stable comes back bedded");
            var bed = st2.transform.Find("Bedding");
            c.Check(bed && bed.gameObject.activeSelf, "with the straw showing");
            var lat3 = PH.Piece(g, "latrine", me + fwd * 4f - right * 2.5f, -fwd);
            lat3.GetComponent<Latrine>().LoadState("120;30");
            c.Check(Mathf.Approximately(lat3.GetComponent<Latrine>().fresh, 120f) && !lat3.GetComponent<Latrine>().Covered, "an old save (fresh;ripe) still loads");
            foreach (var p in new[] { lat, lat2, lat3, st, st2 }) Object.Destroy(p.gameObject);
            for (int i = g.Inventory.GetItem(Sawdust.Sack); i > 0; i--) g.Inventory.TakeItem(Sawdust.Sack);
        }
    }

    /// <summary>At night a kept goat walks into the bedded stable and lies down on the straw, where it mends; a stable
    /// full of dung without bedding draws flies.</summary>
    class AnimalsBedding : Scenario
    {
        public override string Id => "animals.bedding";
        public override float Timeout => 45f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var st = PH.Piece(g, "stable", me + fwd * 10f, -fwd);
            if (!c.Check(st, "a stable")) yield break;
            var stable = st.GetComponent<Stable>(); var manger = st.GetComponent<Trough>();
            manger.manure = 20;
            DayNight.SetHours(12f);
            c.Check(stable.Filth > 0.5f, $"dung piled up with no bedding: a dirty stable ({stable.Filth:0.00})");
            g.Inventory.AddItem(Sawdust.Sack);
            stable.Bed(g.Inventory);
            c.Check(stable.Filth < 0.3f, $"fresh bedding over it: far fewer flies ({stable.Filth:0.00})");
            manger.manure = 0;
            var def = AnimalLibrary.Get("goat") ?? AnimalLibrary.Get("pig");
            if (!c.Check(def != null, "a goat")) yield break;
            var home = PH.Ground(me + fwd * 4f + right * 6f);
            var a = Animal.Spawn(def, home, 0f, g.propMaterial, "t:bedgoat");
            a.owned = true; a.home = home; a.born = DayNight.Day - 30;
            a.health = def.health * 0.5f;
            DayNight.SetHours(23f);
            yield return PH.Until(() => a.OnBedding, 25f);
            c.Note(a.BeddingDebug + " at " + PH.Flat(a.transform.position, st.transform.position).ToString("0.0") + " m from the stable");
            c.Check(a.OnBedding && PH.Flat(a.transform.position, st.transform.position) < 3.2f, "at night the goat goes in and lies down on the straw");
            yield return ScheduledScenarios1015.Shoot(c, g, st.transform.position + Vector3.up * 0.8f, right, 7f, "animals_bedding");
            float h0 = a.health;
            int day = DayNight.Day;
            DayNight.SetDay(day + 1); DayNight.SetHours(2f);                                   // three hours on, past midnight
            yield return new WaitForSeconds(1.2f);
            c.Metric("bedding_health_gain", a.health - h0, "hp");
            c.Check(a.health > h0, $"a night on the straw mends it ({h0:0} -> {a.health:0})");
            DayNight.SetDay(day); DayNight.SetHours(12f);
            Object.Destroy(a.gameObject); Object.Destroy(st.gameObject);
        }
    }

    /// <summary>A sprinkler's wet ground draws crows by day; they pull seedlings on the beds it waters unless a scarecrow
    /// stands near.</summary>
    class PuddleBirds : Scenario
    {
        public override string Id => "animals.puddle_birds";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            bool rain = Weather.Raining; Weather.Raining = false;
            DayNight.SetHours(11f);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            if (Weather.TemperatureAt(me.z) < 1f) { c.Block("frozen here"); Weather.Raining = rain; yield break; }
            var sp = PH.Piece(g, "sprinkler", me + fwd * 7f, -fwd);
            var bedP = PH.Piece(g, "garden_plot", me + fwd * 7f + right * 2f, -fwd);
            if (!c.Check(sp && bedP, "a sprinkler and a bed")) { Weather.Raining = rain; yield break; }
            var s = sp.GetComponent<Sprinkler>(); var plot = bedP.GetComponent<GardenPlot>();
            c.Check(!AnimalDirector.PuddleCalls(s), "dry ground: no birds");
            s.WetGround();
            c.Check(AnimalDirector.PuddleCalls(s), "wet ground calls them");
            var dir = Object.FindAnyObjectByType<AnimalDirector>();
            if (!c.Check(dir, "the animal director")) { Weather.Raining = rain; yield break; }
            yield return PH.Until(() => dir.PuddleBirds(s) >= 2, 12f);
            c.Check(dir.PuddleBirds(s) >= 2, $"crows come down to the puddles ({dir.PuddleBirds(s)})");
            yield return ScheduledScenarios1015.Shoot(c, g, sp.transform.position, right, 7f, "puddle_birds");
            plot.Sow("seed_potato", 1f);
            if (plot.crop == null) plot.crop = "seed_potato";
            plot.growth = 0.2f;
            c.Check(plot.Peck(0.04f) && plot.growth < 0.2f, "they pull a seedling");
            var sc = PH.Piece(g, "scarecrow", me + fwd * 7f - right * 2f, -fwd);
            c.Check(sc && !plot.Peck(0.04f) && !AnimalDirector.PuddleCalls(s), "a scarecrow keeps them off");
            yield return PH.Until(() => dir.PuddleBirds(s) == 0, 8f);
            c.Check(dir.PuddleBirds(s) == 0, "and they leave");
            Weather.Raining = rain;
            foreach (var p in new[] { sp, bedP, sc }) if (p) Object.Destroy(p.gameObject);
        }
    }

    /// <summary>The slop bucket takes the rotten food out of a chest beside it and a kept pig empties it.</summary>
    class SlopBucketPigs : Scenario
    {
        public override string Id => "living.slop_bucket";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(10f);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            for (int k = 0; k < 8; k++)                                                          // the yard away from the road (traffic runs pigs over)
            {
                var d = Quaternion.Euler(0f, k * 45f, 0f) * fwd; bool clear = true;
                for (float r = 3f; r <= 14f && clear; r += 2f) { var q = me + d * r; clear = g.World.Sample(q.x, q.z).roadDist > 12f; }
                if (clear) { fwd = d; break; }
            }
            var right = Vector3.Cross(Vector3.up, fwd);
            var bp = PH.Piece(g, "slop_bucket", me + fwd * 4f, -fwd);
            var chest = PH.Piece(g, "chest", me + fwd * 4f + right * 2f, -fwd);
            if (!c.Check(bp && chest, "a slop bucket by a chest")) yield break;
            var b = bp.GetComponent<SlopBucket>();
            var box = chest.GetComponent<Container>();
            box.inventory.AddItem("food_rotten", 3);
            c.Check(b.Collect() == 3 && b.scraps == 3 && box.inventory.GetItem("food_rotten") == 0, "it takes the rotten food out of the chest");
            g.Inventory.AddItem("food_rotten", 2);
            b.Use(g, false);
            c.Check(b.scraps == 5, $"[E] tips the pack's rotten food in ({b.scraps})");
            c.Check(bp.SaveState() == "5", "its fill is saved");
            var pigDef = AnimalLibrary.Get("pig");
            if (!c.Check(pigDef != null, "pigs exist")) yield break;
            var home = PH.Ground(me + fwd * 9f + right * 3f);
            var pig = Animal.Spawn(pigDef, home, 0f, g.propMaterial, "t:slop_pig");
            pig.owned = true; pig.home = home; pig.born = DayNight.Day - 20; pig.fedDay = -1;
            int s0 = Animal.SlopEaten; Animal.LastHit = null;
            yield return PH.Until(() => pig.SlopTarget == b, 8f);
            c.Check(pig.SlopTarget == b, "the pig goes to the bucket");
            var trace = new List<string>();
            for (float t0 = Time.time; Time.time - t0 < 25f && Animal.SlopEaten - s0 < 3; )
            {
                trace.Add($"{Time.time - t0:0}s {pig.state} d{pig.SlopDistance:0.0} home{PH.Flat(pig.transform.position, home):0.0}");
                yield return new WaitForSeconds(2f);
            }
            if (Animal.SlopEaten - s0 < 3) c.Note(string.Join(" | ", trace) + " / last hit: " + (Animal.LastHit ?? "none"));
            c.Note("pig " + pig.SlopDistance.ToString("0.0") + " m from the bucket, state " + pig.state);
            yield return ScheduledScenarios1015.Shoot(c, g, bp.transform.position, right, 4f, "slop_bucket");
            c.Metric("slop_eaten", Animal.SlopEaten - s0, "");
            c.Check(Animal.SlopEaten - s0 >= 3 && pig.fedDay == DayNight.Day, $"and eats its fill ({Animal.SlopEaten - s0}; fed for the day)");
            Object.Destroy(pig.gameObject); Object.Destroy(bp.gameObject); Object.Destroy(chest.gameObject);
        }
    }

    /// <summary>Washing clean throws a splash and scatters the flies; a companion nearby says so; the rain rinses you.</summary>
    class WashMoment : Scenario
    {
        public override string Id => "npc.wash_moment";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(12f);
            bool rain = Weather.Raining; Weather.Raining = false;
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var n = ScheduledScenarios1012.Person(g, "wash:crew", NpcRole.Wanderer, 7712, me + fwd * 2.5f);
            n.companion = true; n.home = n.transform.position;
            float hy = g.Stats.hygiene;
            g.Stats.hygiene = 10f;
            yield return new WaitForSeconds(0.6f);
            int m0 = WastelandGame.WashMoments;
            WastelandGame.LastWashLine = null;
            g.Wash(80f, "WASHED");
            yield return new WaitForSeconds(0.6f);
            c.Check(WastelandGame.WashMoments == m0 + 1, "getting clean is a moment (splash, the flies scatter)");
            c.Note(WastelandGame.LastWashLine ?? "(nothing said)");
            c.Check(WastelandGame.LastWashLine != null && NpcVoice.Captions.Any(k => k.text == WastelandGame.LastWashLine), "the crew says so");
            yield return ScheduledScenarios1015.Shoot(c, g, me + Vector3.up * 1f, g.Player.transform.right, 3.5f, "wash_moment");
            g.Wash(10f, "WASHED");
            yield return new WaitForSeconds(0.4f);
            c.Check(WastelandGame.WashMoments == m0 + 1, "already clean: no second moment");
            g.Stats.hygiene = 20f;
            Weather.Raining = true;
            yield return new WaitForSeconds(3.5f);
            c.Check(g.Sheltered || g.Stats.hygiene > 20f, $"the rain rinses you out in the open ({g.Stats.hygiene:0.00})");
            Weather.Raining = rain;
            g.Stats.hygiene = hy;
            Object.Destroy(n.gameObject);
        }
    }

    /// <summary>Three ALL WELL? calls on different days: the neighbour promises a visit, walks over, sits by the fire
    /// and leaves a gift on the ground.</summary>
    class NeighbourCallsIn : Scenario
    {
        public override string Id => "npc.neighbour_calls_in";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var town = g.NearestTown(me);
            if (town == null || Vector2.Distance(town.pos, new Vector2(me.x, me.z)) > WastelandGame.WatchReach - 200f) { c.Block("no town within watching reach"); yield break; }
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var flag = PH.Piece(g, "claim_flag", me + fwd * 3f - right * 3f, -fwd);
            var fire = PH.Piece(g, "campfire", me + fwd * 6f, -fwd);
            var bench = PH.Piece(g, "chair", me + fwd * 7.6f, -fwd);
            if (!c.Check(flag && fire && bench, "a claim with a fire and a chair")) yield break;
            var claim = flag.GetComponent<ClaimFlag>();
            string key = "r" + town.index + ",1";
            g.NeighbourErrands.Clear(); g.CallIns.Clear();
            var e = new WastelandGame.NeighbourErrand { kind = 0, state = 2, due = -1, town = town.index, name = "OLD MAE" };
            g.NeighbourErrands[key] = e;
            int day = DayNight.Day;
            g.NoteAllWell(e); g.NoteAllWell(e);
            c.Check(g.CallIns[key].calls == 1, "two calls the same day count once");
            DayNight.SetDay(day + 1); g.NoteAllWell(e);
            DayNight.SetDay(day + 2); g.NoteAllWell(e);
            c.Check(g.CallIns[key].due == day + 3, "the third day's call: she'll come tomorrow");
            DayNight.SetDay(day + 3); DayNight.SetHours(14f);
            var n = g.StartCallIn(key, e, g.CallIns[key], claim);
            if (!c.Check(n, "she sets off from the road")) { DayNight.SetDay(day); yield break; }
            c.Check(PH.Flat(n.transform.position, fire.transform.position) > 20f, "from well off, toward her town");
            yield return PH.Until(() => n.VisitSettled, 40f);
            c.Check(n.VisitSettled && PH.Flat(n.transform.position, fire.transform.position) < 3f, "she walks over and settles by the fire");
            yield return ScheduledScenarios1015.Shoot(c, g, n.transform.position + Vector3.up * 0.8f, right, 4f, "neighbour_calls_in");
            int items0 = WorldItem.All.Count(w => w && PH.Flat(w.transform.position, n.transform.position) < 2f);
            n.EndVisit(true);
            yield return null;
            c.Note(WastelandGame.LastCallIn ?? "(none)");
            c.Check(WastelandGame.LastCallIn != null && WastelandGame.LastCallIn.Contains("LEFT"), "and leaves a gift");
            c.Check(WorldItem.All.Count(w => w && PH.Flat(w.transform.position, n.transform.position) < 2f) > items0, "on the ground where she sat");
            c.Check(g.CallIns[key].calls == 0 && g.CallIns[key].due < 0, "the count starts over");
            var lines = new List<string>();
            var save = typeof(WastelandGame).GetMethod("Save1018", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            save.Invoke(g, new object[] { lines });
            c.Check(lines.Any(l => l.StartsWith("q|" + key + "|")), "the calls are saved");
            foreach (var w in WorldItem.All.ToList()) if (w && PH.Flat(w.transform.position, n.transform.position) < 2f) Object.Destroy(w.gameObject);
            Object.Destroy(n.gameObject);
            g.NeighbourErrands.Clear(); g.CallIns.Clear();
            DayNight.SetDay(day);
            foreach (var p in new[] { flag, fire, bench }) Object.Destroy(p.gameObject);
        }
    }
}
