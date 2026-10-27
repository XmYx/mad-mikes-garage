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
    /// <summary>Scheduled update 2026-10-26: eggs in the straw (hens lay in the nest box in the morning, the box shows
    /// them; no box: in the stable straw), the yard dog (a name, its blanket, the welcome home), the wash in the wind
    /// (a storm tears a garment off the line, dust settles on it), bringing something to supper, dressed scarecrows in
    /// town.</summary>
    public static class ScheduledScenarios1026
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new EggsInTheStraw();
            yield return new YardDog();
            yield return new WashInTheWind();
            yield return new SupperGift();
            yield return new TownScarecrowsShown();
        }
    }

    /// <summary>A kept hen walks to the nest box in the morning, sits in a compartment and leaves an egg the box shows;
    /// with no box she lays in the stable straw; fresh straw means more eggs, a night of barking fewer.</summary>
    class EggsInTheStraw : Scenario
    {
        public override string Id => "animals.eggs_nest";
        public override float Timeout => 70f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var st = PH.Piece(g, "stable", me + fwd * 11f, -fwd);
            var nb = PH.Piece(g, "nest_box", me + fwd * 5f + right * 3f, -fwd);
            if (!c.Check(st && nb, "a stable and a nest box")) yield break;
            yield return null;
            var box = nb.GetComponent<NestBox>();
            c.Check(box && box.Shown == 0, "a new nest box is bare straw (no baked eggs)");
            var hen = ScheduledScenarios1023.Kept(g, "chicken", PH.Ground(me + fwd * 4f - right * 2f), "t:layhen");
            if (!c.Check(hen, "a kept hen")) yield break;
            DayNight.SetHours(7f);
            int e0 = Animal.EggsLaid;
            hen.LayPending = 1;
            yield return PH.Until(() => hen.Nesting, 25f);
            c.Check(hen.Nesting, $"she goes to the box and sits in a compartment ({PH.Flat(hen.transform.position, box.transform.position):0.0} m from it)");
            yield return ScheduledScenarios1015.Shoot(c, g, nb.transform.position + Vector3.up * 0.3f, right, 2.5f, "hen_nesting");
            yield return PH.Until(() => Animal.EggsLaid > e0, Animal.LaySeconds + 4f);
            yield return new WaitForSeconds(0.7f);
            c.Check(Animal.EggsLaid > e0 && Animal.LastLaid == "NEST BOX" && box.Eggs >= 1, $"an egg in the box ({box.Eggs})");
            c.Check(box.Shown >= 1, $"the box shows it ({box.Shown} drawn)");
            yield return ScheduledScenarios1015.Shoot(c, g, nb.transform.position + Vector3.up * 0.2f, right, 1.6f, "nest_egg");
            // no box: the straw
            Object.Destroy(nb.gameObject);
            yield return null;
            var stable = st.GetComponent<Stable>();
            stable.Restore(DayNight.TotalDays);
            int e1 = Animal.EggsLaid;
            hen.LayPending = 1;
            yield return PH.Until(() => Animal.EggsLaid > e1, 30f);
            c.Check(Animal.LastLaid == "STRAW", "no box: she lays in the stable");
            bool loose = WorldItem.All.Any(w => w && w.key == "food_egg" && PH.Flat(w.transform.position, st.transform.position) < 5f);
            c.Check(loose, "an egg lies in the straw to pick up");
            // fresh straw vs a night of barking
            int fresh = 0, barked = 0;
            for (int i = 0; i < 60; i++) { hen.LayPending = 0; hen.QueueEggs(1); fresh += hen.LayPending; }
            stable.disturbedAt = DayNight.TotalDays;
            for (int i = 0; i < 60; i++) { hen.LayPending = 0; hen.QueueEggs(1); barked += hen.LayPending; }
            hen.LayPending = 0;
            c.Check(fresh > 60 && barked < fresh, $"fresh straw lays more ({fresh}/60), a barking night fewer ({barked}/60)");
            foreach (var w in WorldItem.All.ToArray()) if (w && w.key == "food_egg" && PH.Flat(w.transform.position, st.transform.position) < 6f) Object.Destroy(w.gameObject);
            Object.Destroy(hen.gameObject); Object.Destroy(st.gameObject);
            DayNight.SetHours(12f);
        }
    }

    /// <summary>The yard dog takes a name from the context menu (saved), naps on its blanket in the afternoon, keeps the
    /// stable door from the blanket at night, and runs out to meet the player who comes home after a while.</summary>
    class YardDog : Scenario
    {
        public override string Id => "animals.yard_dog";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var dog = ScheduledScenarios1023.Kept(g, "guarddog", PH.Ground(me + fwd * 4f), "t:yarddog") ?? ScheduledScenarios1023.Kept(g, "dog", PH.Ground(me + fwd * 4f), "t:yarddog");
            if (!c.Check(dog && dog.IsDog, "a kept dog")) yield break;
            ScheduledScenarios1023.Call(g, "UpdateNightly1026");
            var opts = ContextActions.Gather(g, new ContextTarget { target = dog, point = dog.transform.position });
            var name = opts.FirstOrDefault(o => o.label.StartsWith("NAME THE DOG "));
            c.Check(name != null, "NAME THE DOG on it (" + string.Join(", ", opts.Select(o => o.label)) + ")");
            if (name != null) name.run(); else g.NameDog(dog, "REX");
            c.Note(WastelandGame.LastDogName ?? "(no name)");
            c.Check(dog.petName != null && dog.Label == dog.petName, "it answers to " + dog.petName);
            if (!AnimalDirector.Instance.kept.Contains(dog)) AnimalDirector.Instance.kept.Add(dog);   // spawned straight, not through the director
            var d = new SaveData();
            AnimalDirector.Instance.Save(d);
            c.Check(d.animals.Any(a => a.petName == dog.petName), "the name saves with the animal");
            // the blanket: afternoon nap
            var bp = PH.Piece(g, "dog_blanket", me + fwd * 6f + right * 3f, -fwd);
            if (!c.Check(bp, "a dog blanket")) yield break;
            yield return null;
            dog.home = bp.transform.position;
            DayNight.SetHours(14f);
            yield return PH.Until(() => dog.Napping, 20f);
            c.Check(dog.Napping && PH.Flat(dog.transform.position, bp.transform.position) < 1f, $"it naps on the blanket in the afternoon ({PH.Flat(dog.transform.position, bp.transform.position):0.0} m)");
            yield return ScheduledScenarios1015.Shoot(c, g, bp.transform.position + Vector3.up * 0.3f, right, 3f, "dog_nap");
            // the welcome home
            DayNight.SetHours(10f);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var away = typeof(WastelandGame).GetField("dogAwaySince", flags).GetValue(g) as Dictionary<Animal, float>;
            away[dog] = DayNight.TotalDays * 24f - 5f;                                               // left five hours ago
            typeof(WastelandGame).GetField("dogT", flags).SetValue(g, 0f);
            ScheduledScenarios1023.Call(g, "UpdateDogs");
            c.Check(dog.Greeting, "home after hours away: " + (Animal.LastWelcome ?? "(nothing)"));
            yield return PH.Until(() => PH.Flat(dog.transform.position, g.Player.transform.position) < 2.6f, 6f);
            c.Check(PH.Flat(dog.transform.position, g.Player.transform.position) < 2.6f, "it runs right up to the player");
            yield return ScheduledScenarios1015.Shoot(c, g, g.Player.transform.position + Vector3.up * 0.6f, right, 4f, "dog_welcome");
            // the door from the blanket
            var st = PH.Piece(g, "stable", me + fwd * 14f, -fwd);
            yield return null;
            var stable = st.GetComponent<Stable>();
            bp.transform.position = stable.DoorSpot + stable.transform.right * 1.2f;
            dog.home = stable.transform.position;
            DayNight.SetHours(23f);
            yield return PH.Until(() => dog.AtDoor, 25f);
            var bed = bp.GetComponent<DogBed>();
            c.Check(dog.AtDoor && PH.Flat(dog.transform.position, bed.LieSpot) < 1.3f, $"at night it keeps the door from its blanket ({PH.Flat(dog.transform.position, bed.LieSpot):0.0} m)");
            Object.Destroy(dog.gameObject); Object.Destroy(bp.gameObject); Object.Destroy(st.gameObject);
            DayNight.SetHours(12f);
        }
    }

    /// <summary>A storm gust tears a garment off the line and drops it downwind; picked up it goes back on the line; a
    /// dust storm leaves the wash dusty (taken in it barely freshens); lost garments save with the line.</summary>
    class WashInTheWind : Scenario
    {
        public override string Id => "living.wash_wind";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var lp = PH.Piece(g, "clothes_line", me + fwd * 4f, -fwd);
            if (!c.Check(lp, "a clothes line")) yield break;
            var line = lp.GetComponent<ClothesLine>();
            line.Hang(new[] { "cloth_poncho", "work_jacket" });
            line.dryness = 0.8f;
            int t0 = ClothesLine.Torn;
            ClothesLine.ForceStorm = () => true;
            yield return PH.Until(() => ClothesLine.Torn > t0, 8f);
            ClothesLine.ForceStorm = null;
            c.Check(ClothesLine.Torn > t0 && line.lost.Count == 1 && line.wash.Count == 1, $"a gust tears one off ({line.lost.Count} lost, {line.wash.Count} left)");
            var lw = Object.FindObjectsByType<LostWash>(FindObjectsSortMode.None).FirstOrDefault(w => w.line == line);
            c.Check(lw && PH.Flat(lw.transform.position, lp.transform.position) >= 7f, $"it lies downwind ({(lw ? PH.Flat(lw.transform.position, lp.transform.position) : 0f):0} m)");
            if (lw) yield return ScheduledScenarios1015.Shoot(c, g, lw.transform.position + Vector3.up * 0.2f, right, 3f, "lost_wash");
            string saved = line.SaveState();
            c.Check(saved.Contains("@"), "the lost garment saves with the line (" + saved + ")");
            if (lw) { lw.Use(g, false); yield return null; }
            c.Check(line.wash.Count == 2 && line.lost.Count == 0, "picked up: pegged back on the line");
            float h = g.Stats.hygiene;
            g.Stats.hygiene = 30f;
            line.dryness = 1f; line.dust = 0.8f;
            g.TakeInWash(line);
            c.Note(WastelandGame.LastWash ?? "(none)");
            c.Check(WastelandGame.LastWash != null && WastelandGame.LastWash.Contains("DUSTY") && g.Stats.hygiene < 30f + WastelandGame.FreshClothes * 0.5f, $"dusty wash barely freshens (hygiene {g.Stats.hygiene:0})");
            g.Stats.hygiene = h;
            foreach (var w in Object.FindObjectsByType<LostWash>(FindObjectsSortMode.None)) if (w.line == line) Object.Destroy(w.gameObject);
            Object.Destroy(lp.gameObject);
        }
    }

    /// <summary>A dish brought to the neighbour's table warms them more and loosens a second tale; coming empty-handed
    /// three times running cools them; the count saves.</summary>
    class SupperGift : Scenario
    {
        public override string Id => "npc.supper_gift";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var town = g.NearestTown(me);
            string key = "r" + (town != null ? town.index : 0) + ",3";
            g.SupperInvites.Clear(); g.SupperEmpty.Clear();
            var n = ScheduledScenarios1012.Person(g, key, NpcRole.Resident, 1026, me + fwd * 2.5f);
            if (!c.Check(n, "the neighbour")) yield break;
            g.InviteToSupper(key, n.Profile.Name);
            DayNight.SetHours(18f);
            g.Inventory.AddItem("food_stew");
            c.Check(g.SupperGift() != null, "something to bring: " + ItemIds.Name(g.SupperGift() ?? ""));
            var opts = ContextActions.Gather(g, new ContextTarget { target = n, point = n.transform.position });
            var bring = opts.FirstOrDefault(o => o.label.StartsWith("BRING "));
            c.Check(bring != null && bring.blocked == null, "BRING ... TO SUPPER on them");
            var s = NpcRegistry.Get(n.Profile);
            int disp = s.disposition, stew = g.Inventory.GetItem("food_stew");
            if (bring != null) bring.run();
            yield return new WaitForSeconds(0.2f);
            c.Note(WastelandGame.LastSupper ?? "(none)");
            c.Check(WastelandGame.LastSupper != null && WastelandGame.LastSupper.Contains("BROUGHT"), "supper with a dish on the table");
            c.Check(s.disposition - disp >= 10 && g.Inventory.GetItem("food_stew") == stew - 1, $"warmer for it (+{s.disposition - disp}), the stew went on the table");
            yield return ScheduledScenarios1015.Shoot(c, g, n.transform.position + Vector3.up * 1f, g.Player.transform.right, 3f, "supper_gift");
            int last = 0;
            for (int i = 0; i < WastelandGame.SupperEmptyCools; i++)
            {
                g.SupperInvites[key] = new Vector2Int(DayNight.Day, -99);
                int before = s.disposition;
                g.HaveSupper(n);
                last = s.disposition - before;
            }
            c.Check(last < 0 && g.SupperEmpty[key] == WastelandGame.SupperEmptyCools, $"empty-handed {WastelandGame.SupperEmptyCools} times running: cooler ({last})");
            var lines = new List<string>();
            ScheduledScenarios1023.Call(g, "Save1023", lines);
            c.Check(lines.Any(l => l.StartsWith("y|" + key + "|")), "the count saves");
            g.SupperInvites.Clear(); g.SupperEmpty.Clear();
            Object.Destroy(n.gameObject);
            DayNight.SetHours(12f);
        }
    }

    /// <summary>Farm lots in village and town fields get scarecrows in cast-offs (spawned near the player, deterministic);
    /// a town on a gang's road has one in the gang's colours.</summary>
    class TownScarecrowsShown : Scenario
    {
        public override string Id => "towns.scarecrows";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            ScheduledScenarios1023.Call(g, "UpdateNightly1026");
            var ts = Object.FindAnyObjectByType<TownScarecrows>();
            if (!c.Check(ts, "the town scarecrow dresser runs")) yield break;
            int farms = 0, gangs = 0, best = -1, bestCount = 0;
            for (int i = 0; i < g.World.settlements.Count; i++)
            {
                var st = g.World.settlements[i];
                if (st.plan == null || st.kind == Biome.City) continue;
                var list = ts.Plan(i, st, g);
                int f = list.Count(s => s.gang < 0);
                farms += f; gangs += list.Count(s => s.gang >= 0);
                if (f > bestCount) { bestCount = f; best = i; }
            }
            c.Note($"{farms} field scarecrows, {gangs} in gang colours across the world");
            c.Check(farms > 0, "village fields have scarecrows");
            if (best < 0) yield break;
            var spot = ts.Plan(best, g.World.settlements[best], g).First(s => s.gang < 0);
            var back = g.Player.transform.position;
            var at = PH.Ground(spot.at + new Vector3(6f, 0f, 6f));
            g.Player.Teleport(at + Vector3.up * 0.2f, 0f);
            yield return PH.Until(() => TownScarecrows.Shown.ContainsKey(spot.key), 8f);
            c.Check(TownScarecrows.Shown.ContainsKey(spot.key), "one stands in the field when you come near");
            var t = TownScarecrows.Shown.ContainsKey(spot.key) ? TownScarecrows.Shown[spot.key] : null;
            c.Check(t && t.Find("Coat"), "in a cast-off coat");
            if (t) yield return ScheduledScenarios1015.Shoot(c, g, t.position + Vector3.up * 1.2f, Vector3.right, 4f, "town_scarecrow");
            g.Player.Teleport(back + Vector3.up * 0.2f, 0f);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
