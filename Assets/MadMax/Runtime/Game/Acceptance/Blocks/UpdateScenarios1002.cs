using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the scheduled update 2026-10-02: seasonal fishing and ice holes, road news that
    /// marks a wreck on the map, the winter pantry plan and the home workshop on the map.</summary>
    public static class UpdateScenarios1002
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new FishingSeasons();
            yield return new NewsWreckPin();
            yield return new WinterPantry();
            yield return new WorkshopPins();
        }
    }

    /// <summary>Bite rates follow the season: warm-water carp sulk in winter, pike feed hardest under the ice, the sea's
    /// shoals run in summer; a hole in a frozen lake favours cold-water fish; a lake freezes in a hard frost, the sea
    /// never does.</summary>
    class FishingSeasons : Scenario
    {
        public override string Id => "fishing.seasons";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var carp = FishLibrary.Get("carp"); var pike = FishLibrary.Get("pike"); var sardine = FishLibrary.Get("sardine");
            float R(FishDef f, int season, float temp = 12f, bool hole = false) => FishLibrary.BiteRate(f, f.bait, 12f, false, temp, season, hole);
            c.Metric("carp_summer_winter", R(carp, 0) / R(carp, 2, -5f), "x");
            c.Check(R(carp, 0) > R(carp, 2, -5f) * 4f, "carp bite far more in summer than in a frozen winter");
            c.Check(R(pike, 2, -5f) > R(pike, 0), "pike bite better in winter than in summer");
            c.Check(R(sardine, 0) > R(sardine, 2) * 3f, "sardine shoals run in summer");
            c.Check(R(pike, 2, -5f, true) > R(pike, 2, -5f) && R(carp, 2, -5f, true) < R(carp, 2, -5f), "an ice hole draws cold-water fish and not warm-water ones");
            c.Check(FishLibrary.BiteRate(carp, carp.bait, 12f, false, 12f) == R(carp, -1), "season -1 = the old season-blind rate");
            var pool = new List<FishDef>();
            FishLibrary.Pool(Biome.Forest, false, pool);
            string winter = FishLibrary.InSeason(pool, 2), summer = FishLibrary.InSeason(pool, 0);
            c.Note("forest lake in season: winter " + winter + " / summer " + summer);
            c.Check(winter.Contains("PIKE") || winter.Contains("TROUT"), "forest lakes name pike or trout in winter");
            c.Check(summer.Contains("CARP"), "and carp in summer");
            // a real lake near the start and the open sea
            var t = DeformableTerrain.Instance; var me = c.Game.Player.transform.position;
            Lake lake = null;
            for (float r = 0f; r < 2400f && lake == null; r += 100f)
                for (float a = 0f; a < 360f && lake == null; a += r > 0f ? 4000f / r : 360f)
                {
                    var q = me + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                    var l = t.World.LakeAt(q.x, q.z, out float lt);
                    if (l != null && !l.toxic && lt < 0.3f) lake = l;
                }
            if (lake == null) { c.Block("no lake within 2.4 km of the start"); yield break; }
            var centre = new Vector3(lake.pos.x, 0f, lake.pos.y);
            c.Fixture("lake at " + Mathf.RoundToInt(centre.x) + "," + Mathf.RoundToInt(centre.z) + " checked at fixed temperatures");
            c.Check(FishingRodTool.Frozen(t, centre, -8f), "the lake ices over at -8 C");
            c.Check(!FishingRodTool.Frozen(t, centre, 2f), "and is open water at 2 C");
            yield break;
        }
    }

    /// <summary>A skirmish headline tied to its wreck lists on a board as markable; marking pins it on the map, sets the
    /// waypoint, survives a save round trip, and the pin clears when the player arrives.</summary>
    class NewsWreckPin : Scenario
    {
        public override string Id => "towns.news_wreck_pin";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var saved = TownNews.Save();
            TownNews.Load(null);
            var me = g.Player.transform.position;
            var fight = me + new Vector3(260f, 0f, 40f); var wreck = fight + new Vector3(0f, 0f, 7f);
            MadMax.Audio.RadioNetwork.Flash("WORD ON THE ROAD: THE TEST GANG HIT A GUILD CONVOY.", 1f, fight);
            TownNews.MarkWreck(fight, wreck);
            c.Fixture("a skirmish flashed 260 m away and tied to a wreck as NpcDirector.Skirmishes does");
            var near = TownNews.Near(me, 5);
            bool tagged = near.Count > 0 && near[0].wreck && Mathf.Abs(near[0].z - wreck.z) < 0.5f;
            c.Check(tagged, "the headline carries the wreck's place");
            var round = TownNews.Save(); TownNews.Load(round);
            near = TownNews.Near(me, 5);
            c.Check(near.Count > 0 && near[0].wreck, "the wreck tag survives a save round trip");
            g.MarkNewsWreck(wreck);
            var pins = new List<WastelandGame.Pin>(); g.JobPins(pins);
            c.Check(pins.Exists(p => p.label == "WRECK (NEWS)"), "marking pins it on the map");
            c.Check(g.HasWaypoint && (g.Waypoint - wreck).sqrMagnitude < 400f, "and sets the waypoint there");
            g.MarkNewsWreck(wreck + Vector3.right * 2f);
            c.Check(g.NewsPins.Count == 1, "marking it twice keeps one pin");
            var d = new SaveData(); d.newsPins = new List<Vector3>(g.NewsPins);
            c.Check(d.newsPins.Count == 1, "pins go into SaveData.newsPins");
            var spot = wreck; spot.y = DeformableTerrain.Instance.Height(spot.x, spot.z) + 0.3f;
            g.Player.Teleport(spot + new Vector3(4f, 0f, 0f), 0f);
            yield return SeasonsScenarios.Until(() => g.NewsPins.Count == 0, 4f);
            c.Check(g.NewsPins.Count == 0, "the pin clears on arrival");
            g.ClearWaypoint();
            TownNews.Load(saved);
        }
    }

    /// <summary>The pantry counts days of food: meat in an open crate rots before winter is out, the same meat in a root
    /// cellar or tinned food keeps; the note names what to do.</summary>
    class WinterPantry : Scenario
    {
        public override string Id => "food.winter_pantry";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -5f), 0f);
            yield return new WaitForSeconds(0.3f);
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            var crate = FurnitureLibrary.Spawn("crate", g.Build.Structures, P(2f, 0f), Quaternion.identity, g.propMaterial);
            var box = crate ? crate.GetComponent<Container>() : null;
            if (!c.Check(box, "a crate with storage")) yield break;
            var lock_ = SeasonsScenarios.SeasonLock.Take(); lock_.Set(1);
            g.Pantry(8f, out float k0, out float r0);
            box.inventory.AddItem("food_meat_raw", 10);
            g.Pantry(8f, out float k1, out float r1);
            c.Metric("hunger_per_day", g.HungerPerDay, "");
            c.Check(r1 > r0 + 0.1f && Mathf.Abs(k1 - k0) < 0.01f, $"raw meat in a crate counts as food that rots first ({r1 - r0:0.0} days)");
            string note = g.PantryNote(1);
            c.Note(note);
            c.Check(note != null && note.Contains("ROT FIRST"), "the autumn note says it will rot");
            box.inventory.TakeItem("food_meat_raw", 10);
            var tin = FoodLibrary.Get("can_beans") != null ? "can_beans" : null;
            if (tin == null) foreach (var f in FoodLibrary.AllFood) if (f.spoilMinutes <= 0f && f.hunger > 5f) { tin = f.id; break; }
            box.inventory.AddItem(tin, 10);
            g.Pantry(8f, out float k2, out float r2);
            c.Check(k2 > k0 + 0.1f, $"food that keeps ({tin}) counts toward the winter ({k2 - k0:0.0} days)");
            c.Check(g.PantryNote(0) == null && g.PantryNote(3) == null, "no pantry note in summer or spring");
            lock_.Restore();
            Object.Destroy(crate.gameObject);
        }
    }

    /// <summary>The player's stations away from them show on the map: WORKING with progress, NO POWER when a powered
    /// station's grid is down, READY with goods on the tray; stations of others and near ones are left out.</summary>
    class WorkshopPins : Scenario
    {
        public override string Id => "base.workshop_pins";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -5f), 0f);
            yield return new WaitForSeconds(0.3f);
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            var a = FurnitureLibrary.Spawn("workbench", g.Build.Structures, P(-2f, 2f), Quaternion.identity, g.propMaterial);
            var b = FurnitureLibrary.Spawn("workbench", g.Build.Structures, P(2f, 2f), Quaternion.identity, g.propMaterial);
            yield return null;
            var sa = a ? a.GetComponent<CraftingStation>() : null; var sb = b ? b.GetComponent<CraftingStation>() : null;
            if (!c.Check(sa && sb, "two workbenches with stations")) yield break;
            a.owner = g.Stats.name; b.owner = "SOMEONE ELSE";
            Recipe r = null;
            foreach (var x in RecipeLibrary.All) if (x.station == "workbench") { r = x; break; }
            if (r == null) { c.Block("no workbench recipe"); yield break; }
            sa.Enqueue(r, 0.0001f); sb.Enqueue(r, 0.0001f);
            c.Fixture("two benches with a near-frozen job: one the player's, one someone else's");
            var pins = new List<WastelandGame.Pin>();
            g.WorkshopPins(pins);
            c.Check(pins.Count == 0, "nothing listed while the player stands at the benches");
            var far = pad + new Vector3(0f, 0f, -70f); far.y = t.Height(far.x, far.z) + 0.3f;
            g.Player.Teleport(far, 0f);
            yield return new WaitForSeconds(0.3f);
            g.WorkshopPins(pins);
            c.Check(pins.Count == 1 && pins[0].label.Contains("WORKING"), "70 m away: the player's bench shows WORKING" + (pins.Count > 0 ? " (" + pins[0].label + ")" : ""));
            sa.watts = 200f;
            g.WorkshopPins(pins);
            c.Check(pins.Count == 1 && pins[0].label.Contains("NO POWER"), "a powered station off the grid shows NO POWER");
            sa.watts = 0f;
            sa.CancelLast(); sb.CancelLast();
            sa.tray.AddItem("food_bread", 2);
            g.WorkshopPins(pins);
            c.Check(pins.Count == 1 && pins[0].label.Contains("READY"), "goods on the tray show READY");
            sa.tray.TakeItem("food_bread", 2);
            Object.Destroy(a.gameObject); Object.Destroy(b.gameObject);
        }
    }
}
