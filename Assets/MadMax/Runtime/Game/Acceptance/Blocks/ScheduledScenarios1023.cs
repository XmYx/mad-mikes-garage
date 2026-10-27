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
    /// <summary>Scheduled update 2026-10-23: hens on the roost and the dog at the stable door, the crows' memory, sour and
    /// rain-filled slop, wash day on the clothes line, supper at the neighbour's, the chores line; remodels of the
    /// bookshelf (spines of what it holds), the dining table (the meal laid and eaten) and the geiger dial.</summary>
    public static class ScheduledScenarios1023
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new RoostAndDoor();
            yield return new ScarecrowMemory();
            yield return new SlopSours();
            yield return new WashDay();
            yield return new SupperNeighbour();
            yield return new ChoresLedger();
            yield return new ShelfTableDial();
        }

        internal static Animal Kept(WastelandGame g, string id, Vector3 home, string key)
        {
            var def = AnimalLibrary.Get(id);
            if (def == null) return null;
            var a = Animal.Spawn(def, home, 0f, g.propMaterial, key);
            a.owned = true; a.home = home; a.born = DayNight.Day - 30;
            return a;
        }

        internal static object Call(WastelandGame g, string method, params object[] args) =>
            typeof(WastelandGame).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, args);
    }

    /// <summary>At night a kept hen walks into the stable and hops up onto the roost bar; the guard dog lies across the
    /// stable mouth and barks at a coyote prowling near.</summary>
    class RoostAndDoor : Scenario
    {
        public override string Id => "animals.roost_door";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var st = PH.Piece(g, "stable", me + fwd * 10f, -fwd);
            if (!c.Check(st, "a stable")) yield break;
            var stable = st.GetComponent<Stable>();
            var hen = ScheduledScenarios1023.Kept(g, "chicken", PH.Ground(me + fwd * 5f + right * 4f), "t:roosthen");
            var dog = ScheduledScenarios1023.Kept(g, "guarddog", PH.Ground(me + fwd * 5f - right * 4f), "t:doordog");
            if (!c.Check(hen && dog, "a kept hen and a guard dog")) yield break;
            DayNight.SetHours(23f);
            yield return PH.Until(() => hen.Roosting && dog.AtDoor, 30f);
            c.Check(hen.Roosting, $"the hen hops up on the roost ({PH.Flat(hen.transform.position, stable.RoostSpot(0)):0.0} m from the bar)");
            c.Check(stable.transform.Find("Roost"), "the roost bar is up");
            c.Check(dog.AtDoor && PH.Flat(dog.transform.position, stable.DoorSpot) < 1.2f, $"the dog lies across the stable mouth ({PH.Flat(dog.transform.position, stable.DoorSpot):0.0} m)");
            yield return ScheduledScenarios1015.Shoot(c, g, st.transform.position + Vector3.up * 0.8f, right, 7f, "roost_door");
            var prowlerDef = AnimalLibrary.Get("coyote") ?? AnimalLibrary.Get("dog");
            var coyote = Animal.Spawn(prowlerDef, PH.Ground(stable.DoorSpot + stable.Outward * 24f), 180f, g.propMaterial, "t:prowler");
            float b0 = Animal.LastBarkAt;
            yield return PH.Until(() => Animal.LastBarkAt > b0, 8f);
            c.Note("barked at " + (Animal.LastBarkAtWhat ?? "(nothing)"));
            c.Check(Animal.LastBarkAt > b0, "a coyote prowling near: the dog barks");
            if (coyote) Object.Destroy(coyote.gameObject);
            DayNight.SetHours(9f);
            yield return PH.Until(() => !hen.Roosting, 6f);
            c.Check(!hen.Roosting, "in the morning she hops down");
            Object.Destroy(hen.gameObject); Object.Destroy(dog.gameObject); Object.Destroy(st.gameObject);
            DayNight.SetHours(12f);
        }
    }

    /// <summary>A scarecrow works for a few days, then the crows get used to it; dressing it (or moving it) works again;
    /// its state saves.</summary>
    class ScarecrowMemory : Scenario
    {
        public override string Id => "garden.scarecrow_memory";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var p = PH.Piece(g, "scarecrow", me + fwd * 4f, -fwd);
            if (!c.Check(p, "a scarecrow")) yield break;
            yield return null;
            var s = p.GetComponent<Scarecrow>();
            var near = p.transform.position + right * 3f;
            c.Check(s.Fresh && Scarecrow.Near(near), "new: the crows keep off");
            s.since = DayNight.TotalDays - Scarecrow.FreshDays - 0.5f;
            c.Check(!s.Fresh && !Scarecrow.Near(near) && Scarecrow.Stale(near) == s, "after a few days they're used to it");
            g.Inventory.AddItem("cloth_poncho");
            s.Dress("cloth_poncho", g.Inventory);                                                   // Use() takes the first garment in the pack (the starting t-shirt)
            yield return null;
            c.Check(s.Fresh && s.dressed == "cloth_poncho" && g.Inventory.GetItem("cloth_poncho") == 0, "dressed in a poncho: they keep off again");
            var coat = p.transform.Find("Coat");
            c.Check(coat && coat.gameObject.activeSelf, "the poncho hangs on it");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 1.2f, right, 3.5f, "scarecrow_dressed");
            string state = s.SaveState();
            var p2 = PH.Piece(g, "scarecrow", me + fwd * 4f + right * 3f, -fwd);
            yield return null;
            var s2 = p2.GetComponent<Scarecrow>();
            s2.LoadState(state);
            c.Check(s2.dressed == "cloth_poncho" && Mathf.Abs(s2.since - s.since) < 0.01f, "its state saves (" + state + ")");
            s.since = DayNight.TotalDays - 10f;
            p.transform.position += right * 1.5f;
            yield return null;
            c.Check(s.Fresh, "moved: a new thing to fear");
            Object.Destroy(p.gameObject); Object.Destroy(p2.gameObject);
        }
    }

    /// <summary>A half-full bucket in the warmth sours (more flies); rain fills it; [T] tips the water out as a puddle;
    /// the pigs still eat it; old saves load.</summary>
    class SlopSours : Scenario
    {
        public override string Id => "living.slop_sour";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var p = PH.Piece(g, "slop_bucket", me + fwd * 3f, -fwd);
            if (!c.Check(p, "a slop bucket")) yield break;
            var b = p.GetComponent<SlopBucket>();
            bool rain = Weather.Raining;
            Weather.Raining = false;
            DayNight.SetHours(13f);
            b.scraps = 10;
            var tick = typeof(SlopBucket).GetMethod("Weather1023", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            tick.Invoke(b, new object[] { 12f });
            float temp = Weather.TemperatureAt(p.transform.position.z);
            c.Note($"sour {b.sour:0.00} at {temp:0} C");
            c.Check(temp <= 8f || b.Sour, "half a day in the warm: the slop sours");
            Weather.Raining = true;
            tick.Invoke(b, new object[] { 6f });
            c.Check(temp <= 0f || b.water > 2f, $"rain fills it ({b.water:0.0} L)");
            Weather.Raining = false;
            b.water = Mathf.Max(b.water, 4f);
            yield return null;
            c.Check(b.Prompt(g).Contains("TIP OUT"), "it offers to tip the water out");
            float pool0 = Spills.PoolNear(p.transform.position, 1.5f, out _, 0.001f);
            b.Use(g, true);
            yield return null;
            c.Check(b.water == 0f && Spills.PoolNear(p.transform.position, 1.5f, out _, 0.001f) > pool0, "[T]: tipped out, a dirty puddle beside it");
            c.Check(b.Take() && b.scraps == 9, "the pigs still eat it");
            string st = b.SaveState();
            b.LoadState("7");
            c.Check(b.scraps == 7, "a 10-18 save (a bare count) loads");
            b.LoadState(st);
            c.Check(b.scraps == 9, "and the new state round-trips (" + st + ")");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 0.4f, g.Player.transform.right, 2f, "slop_sour");
            Weather.Raining = rain;
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>A clothes line by a water drum: wash what you wear (wear mends a little), the wash hangs and dries,
    /// taken in dry it freshens you, sodden it soaks you.</summary>
    class WashDay : Scenario
    {
        public override string Id => "living.wash_day";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var lp = PH.Piece(g, "clothes_line", me + fwd * 4f, -fwd);
            var dp = PH.Piece(g, "storage_drum", me + fwd * 4f + right * 3f, -fwd);
            if (!c.Check(lp && dp, "a clothes line by a drum")) yield break;
            var line = lp.GetComponent<ClothesLine>();
            bool rain = Weather.Raining; Weather.Raining = false;
            DayNight.SetHours(11f);
            var drum = dp.GetComponent<FluidStore>();
            drum.litres = 60f; drum.mix.Set(ResourceType.Water);
            string worn = null;
            foreach (var id in g.Player.Rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null && d.armor == null && d.slot != ClothingSlot.Pack && d.slot != ClothingSlot.Belt) { worn = d.id; break; } }
            if (!c.Check(worn != null, "the player wears something washable")) yield break;
            float w0 = g.ClothWear.TryGetValue(worn, out var ww) ? ww : 0f;
            g.ClothWear[worn] = 0.4f;
            c.Check(g.WashClothes(line) && line.Hung, "washed and hung out (" + WastelandGame.LastWash + ")");
            c.Check(drum.litres <= 60f - ClothesLine.WashLitres + 0.1f, $"it took water from the drum ({drum.litres:0} L left)");
            c.Check(g.ClothWear[worn] < 0.4f, $"scrubbing mended a little ({g.ClothWear[worn]:0.00})");
            yield return new WaitForSeconds(0.3f);
            var peg = lp.transform.Find("Peg0");
            c.Check(peg && peg.gameObject.activeSelf, "the wash flaps on the line");
            yield return ScheduledScenarios1015.Shoot(c, g, lp.transform.position + Vector3.up * 1.3f, right, 3.5f, "wash_day");
            float rate = ClothesLine.DryRate(lp.transform.position);
            c.Check(rate > 0.1f, $"it dries in the day ({rate:0.00}/h)");
            Weather.Raining = true;
            c.Check(ClothesLine.DryRate(lp.transform.position) < 0f, "rain wets it again");
            Weather.Raining = false;
            float h = g.Stats.hygiene;
            g.Stats.hygiene = 40f;
            line.dryness = 1f;
            g.TakeInWash(line);
            c.Check(!line.Hung && g.Stats.hygiene >= 40f + WastelandGame.FreshClothes - 0.1f, "taken in dry: fresh clothes");
            line.Hang(new[] { "cloth_poncho" }); line.dryness = 0.2f;
            string saved = line.SaveState();
            line.LoadState(saved);
            c.Check(line.Hung && line.wash[0] == "cloth_poncho", "the line's wash saves (" + saved + ")");
            g.Stats.wetness = 0f;
            g.TakeInWash(line);
            c.Check(g.Stats.wetness > 0.5f, "taken in sodden: they soak you");
            g.Stats.hygiene = h; g.Stats.wetness = 0f; g.ClothWear[worn] = w0;
            Weather.Raining = rain;
            Object.Destroy(lp.gameObject); Object.Destroy(dp.gameObject);
        }
    }

    /// <summary>After a call-in the neighbour's house is open: in the evening a context option sits you down to supper
    /// (fed, warmer), not again for a few days; invitations save.</summary>
    class SupperNeighbour : Scenario
    {
        public override string Id => "npc.supper";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var town = g.NearestTown(me);
            string key = "r" + (town != null ? town.index : 0) + ",2";
            g.SupperInvites.Clear();
            var n = ScheduledScenarios1012.Person(g, key, NpcRole.Resident, 1023, me + fwd * 2.5f);
            if (!c.Check(n, "the neighbour")) yield break;
            g.InviteToSupper(key, n.Profile.Name);
            DayNight.SetHours(12f);
            c.Check(g.SupperBlocked(n) == "SUPPER IS FROM 17:00", "by day: not yet");
            DayNight.SetHours(18f);
            ScheduledScenarios1023.Call(g, "UpdateNightly1023");
            var opts = ContextActions.Gather(g, new ContextTarget { target = n, point = n.transform.position });
            var o = opts.FirstOrDefault(x => x.label.StartsWith("SIT DOWN TO SUPPER"));
            c.Check(o != null && o.blocked == null, "in the evening: SIT DOWN TO SUPPER on them");
            float hunger = g.Stats.hunger;
            g.Stats.hunger = 30f;
            if (o != null) o.run(); else g.HaveSupper(n);
            yield return new WaitForSeconds(0.3f);
            c.Note(WastelandGame.LastSupper ?? "(none)");
            c.Check(WastelandGame.LastSupper != null && g.Stats.hunger >= 70f, "a hot meal at their table");
            c.Check(g.Stats.fedUntil > DayNight.TotalDays * 24f + 4f, "WELL FED");
            c.Check(g.SupperBlocked(n) == "YOU ATE THERE LATELY", "not again tomorrow");
            yield return ScheduledScenarios1015.Shoot(c, g, n.transform.position + Vector3.up * 1f, g.Player.transform.right, 3f, "supper");
            var lines = new List<string>();
            ScheduledScenarios1023.Call(g, "Save1023", lines);
            c.Check(lines.Any(l => l.StartsWith("v|" + key + "|")), "the invitation saves");
            g.Stats.hunger = hunger;
            g.SupperInvites.Clear();
            Object.Destroy(n.gameObject);
            DayNight.SetHours(12f);
        }
    }

    /// <summary>The HOME ledger lists the yard's chores: no bedding, dung to shovel, an empty slop bucket.</summary>
    class ChoresLedger : Scenario
    {
        public override string Id => "ui.chores_line";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var flag = PH.Piece(g, "claim_flag", me + fwd * 3f, -fwd);
            var st = PH.Piece(g, "stable", me + fwd * 9f, -fwd);
            var bp = PH.Piece(g, "slop_bucket", me + fwd * 5f + right * 3f, -fwd);
            if (!c.Check(flag && st && bp, "a claim with a stable and a slop bucket")) yield break;
            var claim = flag.GetComponent<ClaimFlag>();
            var goat = ScheduledScenarios1023.Kept(g, "goat", PH.Ground(me + fwd * 6f - right * 3f), "t:choregoat");
            st.GetComponent<Trough>().manure = 12;
            yield return null;
            string line = g.ChoresLine(claim, out _);
            c.Note(line ?? "(none)");
            c.Check(line != null && line.Contains("BEDDING") && line.Contains("DUNG") && line.Contains("SLOP BUCKET EMPTY"), "chores: bedding, dung, the slop bucket");
            st.GetComponent<Trough>().manure = 0;
            g.Inventory.AddItem(Sawdust.Sack);
            st.GetComponent<Stable>().Bed(g.Inventory);
            st.GetComponent<Trough>().feed = 20f; st.GetComponent<Trough>().water = 20f;
            bp.GetComponent<SlopBucket>().scraps = 3;
            line = g.ChoresLine(claim, out _);
            c.Note(line ?? "(none)");
            c.Check(line != null && line.EndsWith("ALL DONE"), "done: ALL DONE");
            var ledger = new List<string>();
            g.HomeLedger(ledger);
            c.Check(claim.Owner != g.Stats.name || ledger.Any(l => l.Contains("CHORES")), "it shows on the HOME ledger");
            if (goat) Object.Destroy(goat.gameObject);
            foreach (var p in new[] { flag, st, bp }) Object.Destroy(p.gameObject);
        }
    }

    /// <summary>Remodels: the bookshelf shows a spine per stored book or tape; a meal at the table is laid on a plate and
    /// eaten down; the geiger's needle swings with the reading.</summary>
    class ShelfTableDial : Scenario
    {
        public override string Id => "living.shelf_table_dial";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var sp = PH.Piece(g, "bookshelf", me + fwd * 3f + right * 2f, -fwd);
            var tp = PH.Piece(g, "dining_table", me + fwd * 2.2f - right * 1.5f, -fwd);
            if (!c.Check(sp && tp, "a bookshelf and a table")) yield break;
            yield return null;
            var shelf = sp.GetComponent<Bookshelf>();
            var box = sp.GetComponent<Container>();
            if (!MadMax.Rendering.HDBits.On)
            {
                c.Check(shelf.Shown == 0, "empty: bare shelves");
                box.inventory.AddItem("book_first_aid"); box.inventory.AddItem("book_railways", 2); box.inventory.AddItem("vhs_garage");
                yield return null;
                yield return null;
                c.Check(shelf.Shown == 4, $"a spine per volume ({shelf.Shown})");
                yield return ScheduledScenarios1015.Shoot(c, g, sp.transform.position + Vector3.up * 1f, right, 2.5f, "bookshelf");
            }
            else c.Note("HD look: the HD model carries its own books");
            var table = tp.GetComponent<DiningTable>();
            table.Serve(tp.transform.position - fwd, "food_meat_cooked", 1.2f);
            yield return null;
            c.Check(table.Plates == 1 && tp.transform.Find("Plate/Food"), "the meal is laid on the table");
            yield return ScheduledScenarios1015.Shoot(c, g, tp.transform.position + Vector3.up * 0.9f, right, 2f, "table_plate");
            yield return new WaitForSeconds(1.6f);
            c.Check(tp.transform.Find("Plate/Crumbs") && !tp.transform.Find("Plate/Food"), "eaten down to crumbs");
            table.Use(g, false);
            yield return null;
            c.Check(table.Plates == 0, "[E] clears the table");
            var gc = ToolLibrary.Create("tool_geiger", g.propMaterial);
            if (c.Check(gc is GeigerTool, "a geiger counter"))
            {
                gc.transform.position = me + Vector3.up;
                var hz = new GameObject("hot").AddComponent<Hazard>(); hz.radiation = 2f; hz.transform.position = me + Vector3.up + fwd * 0.5f;
                yield return new WaitForSeconds(1.5f);
                var geiger = (GeigerTool)gc;
                c.Check(geiger.NeedleAngle > 30f && gc.transform.Find("Needle"), $"near a hot source the needle swings over ({geiger.NeedleAngle:0} deg)");
                c.Check(ToolHolds.For(gc) == ToolHolds.Flash, "held out in front");
                Object.Destroy(hz.gameObject); Object.Destroy(gc.gameObject);
            }
            Object.Destroy(sp.gameObject); Object.Destroy(tp.gameObject);
        }
    }
}
