using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the scheduled update 2026-10-01: seasonal vendor stock, road news on the boards,
    /// the root cellar and the working sounds of crafting stations.</summary>
    public static class SeasonsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new SeasonalStock();
            yield return new RoadNews();
            yield return new RootCellar();
            yield return new StationSounds();
        }
    }

    /// <summary>The same food stallkeeper on the same day sells seeds and saplings in spring, preserves in winter and
    /// fishing gear in summer (season forced, disclosed); every seasonal id resolves to a price.</summary>
    class SeasonalStock : Scenario
    {
        public override string Id => "economy.seasonal_stock";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            int was = Weather.Season;
            var p = NpcProfile.Make("t:seasonal", NpcRole.Stallkeeper, 7, "food");
            var s = new NpcSave();
            bool Has(List<Trade.Offer> l, string prefix) { foreach (var o in l) if (o.id.StartsWith(prefix) && o.count > 0) return true; return false; }
            var stock = new List<Trade.Offer>[4];
            for (int season = 0; season < 4; season++) { Weather.Season = season; stock[season] = Trade.Stock(p, s, 0.3f); }
            Weather.Season = was;
            c.Fixture("Weather.Season set to each season in turn for one stallkeeper (kind food), then restored");
            c.Check(Has(stock[3], "sapling_") || Has(stock[3], "seed_carrot") || Has(stock[3], "seed_cabbage"), "spring: seeds and saplings on the stall");
            c.Check(Has(stock[2], "food_jerky") || Has(stock[2], "food_pickles") || Has(stock[2], "food_meat_salted"), "winter: preserves on the stall");
            c.Check(Has(stock[0], "bait_") || Has(stock[0], "tool_fishing_rod"), "summer: fishing gear on the stall");
            c.Check(!Has(stock[2], "sapling_") && !Has(stock[0], "food_pickles"), "out of season lines are gone");
            int bad = 0;
            for (int season = 0; season < 4; season++)
                foreach (var kind in new[] { "food", "pack", "fuel", "build" })
                    foreach (var e in Trade.SeasonalStock(kind, season))
                        if (Trade.BuyPrice(e.id, 0.3f) <= 0 || string.IsNullOrEmpty(MadMax.Items.ItemCatalog.Name(e.id))) { bad++; c.Note("unpriced or unnamed: " + e.id); }
            c.Check(bad == 0, "every seasonal line has a name and a price");
            yield break;
        }
    }

    /// <summary>Headlines carry their place: a board shows the news from near its town and the region-wide flashes, not
    /// a skirmish two towns away; the ledger survives a save round trip; radio lead-ins are dropped.</summary>
    class RoadNews : Scenario
    {
        public override string Id => "towns.road_news";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var saved = TownNews.Save();
            TownNews.Load(null);
            var board = new Vector3(100f, 0f, 100f);
            MadMax.Audio.RadioNetwork.Flash("WORD ON THE ROAD: THE TEST GANG HIT A GUILD CONVOY NEAR HERE.", 1f, board + new Vector3(300f, 0f, 0f));
            TownNews.Post("A FAR AWAY FIGHT", board + new Vector3(5000f, 0f, 0f));
            TownNews.Post("AUTUMN MARKETS: CHEAP FOOD");
            c.Fixture("three headlines posted directly (near, far, region-wide); the news ledger restored afterwards");
            var near = TownNews.Near(board, 5);
            bool HasText(List<TownNews.Entry> l, string t) { foreach (var e in l) if (e.text.Contains(t)) return true; return false; }
            c.Check(HasText(near, "TEST GANG"), "the board carries the skirmish 300 m away");
            c.Check(!HasText(near, "FAR AWAY"), "but not the one 5 km away");
            c.Check(HasText(near, "AUTUMN MARKETS"), "and the region-wide market news");
            c.Check(near.Count > 0 && !near[0].text.StartsWith("WORD ON THE ROAD"), "radio lead-ins are dropped on paper");
            var round = TownNews.Save();
            TownNews.Load(round);
            var again = TownNews.Near(board, 5);
            c.Check(again.Count == near.Count && HasText(again, "TEST GANG") && !HasText(again, "FAR AWAY"), "the ledger survives a save round trip with its places");
            TownNews.Load(saved);
            yield break;
        }
    }

    /// <summary>A root cellar keeps raw meat through 25 minutes that rot it in a crate beside it (spoilage advanced
    /// directly, disclosed).</summary>
    class RootCellar : Scenario
    {
        public override string Id => "food.root_cellar";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -5f), 0f);
            yield return new WaitForSeconds(0.3f);
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            var cellar = FurnitureLibrary.Spawn("root_cellar", g.Build.Structures, P(-2f, 0f), Quaternion.identity, g.propMaterial);
            var crate = FurnitureLibrary.Spawn("crate", g.Build.Structures, P(2f, 0f), Quaternion.identity, g.propMaterial);
            yield return null;
            var inCellar = cellar ? cellar.GetComponent<Container>() : null;
            var inCrate = crate ? crate.GetComponent<Container>() : null;
            if (!c.Check(inCellar && inCrate, "root cellar and crate spawn with storage")) yield break;
            c.Screenshot("root_cellar");
            yield return null;
            inCellar.inventory.AddItem("food_meat_raw", 2); inCrate.inventory.AddItem("food_meat_raw", 2);
            c.Metric("cellar_spoil", inCellar.SpoilFactor, "x");
            c.Check(inCellar.SpoilFactor <= 0.34f, $"the cellar slows rot without power ({inCellar.SpoilFactor:0.00}x)");
            g.SpoilContainer(inCellar, 1500f); g.SpoilContainer(inCrate, 1500f);
            c.Fixture("25 minutes of spoilage applied to both (raw meat rots in 20 in the open)");
            c.Check(inCrate.inventory.GetItem("food_rotten") == 2, "both pieces of meat rot in the crate");
            c.Check(inCellar.inventory.GetItem("food_meat_raw") == 2, "and keeps in the root cellar");
        }
    }

    /// <summary>Every crafting station type has a working sound (bench ratchet, or a seamless `station_*` loop
    /// synthesised by ProceduralSfx); a running stove asks for its loop near the player.</summary>
    class StationSounds : Scenario
    {
        public override string Id => "audio.station_sounds";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var silent = new List<string>();
            foreach (var type in new[] { "stove", "oven", "range", "smokehouse", "campfire", "cannery", "furnace", "arc_furnace", "kiln", "forge", "refinery",
                                         "mixer", "washplant", "feedmill", "composter", "desalinator", "still", "chemlab", "tanning", "sewing", "loom", "spinning",
                                         "rock_crusher", "stamp_mill", "press", "machine_shop", "sawmill", "saw_bench", "hangar", "slipway" })
            {
                var key = CraftingStation.WorkLoop(type, out float vol);
                if (key == null) { silent.Add(type); continue; }
                var clip = MadMax.Audio.Sfx.Clip(key);
                if (!clip || clip.length < 1f || vol <= 0f) silent.Add(type + "(" + key + ")");
            }
            c.Check(silent.Count == 0, "every powered or fired station type has a working loop" + (silent.Count > 0 ? ": missing " + string.Join(", ", silent) : ""));
            // seamless: the loop's last sample meets its first
            foreach (var key in new[] { "station_sizzle", "station_roar", "station_churn", "station_bubble", "station_clack", "station_grind", "station_saw", "station_hum" })
            {
                var clip = MadMax.Audio.Sfx.Clip(key);
                if (!clip) { c.Check(false, key + " synthesises"); continue; }
                var d = new float[clip.samples]; clip.GetData(d, 0);
                float peak = 0f, step = 0f;
                for (int i = 0; i < d.Length; i++) { peak = Mathf.Max(peak, Mathf.Abs(d[i])); if (i > 0) step = Mathf.Max(step, Mathf.Abs(d[i] - d[i - 1])); }
                float seam = Mathf.Abs(d[0] - d[d.Length - 1]);
                c.Metric(key + "_seam", seam / Mathf.Max(step, 1e-4f), "of max step");
                c.Check(peak > 0.05f && seam <= step, $"{key}: {clip.length:0.0} s, loops without a click (seam {seam:0.000} vs largest step {step:0.000})");
            }
            yield break;
        }
    }
}
