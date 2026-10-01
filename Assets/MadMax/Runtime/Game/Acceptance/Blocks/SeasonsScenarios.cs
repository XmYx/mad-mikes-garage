using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Audio;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the scheduled update 2026-10-01: seasonal vendor stock, road news on the boards,
    /// the root cellar and the working sounds of crafting stations; and the Seasons block (seasons.*): the preserving
    /// chain and its value, seasonal stock with residents' chores, a working station heard live, the news read at a board.</summary>
    public static class SeasonsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new SeasonalStock();
            yield return new RoadNews();
            yield return new RootCellar();
            yield return new StationSounds();
            yield return new SeasonsPreserves();
            yield return new SeasonsStock();
            yield return new SeasonsStationSounds();
            yield return new SeasonsNoticeBoard();
        }

        /// <summary>Holds the season still (the clock no longer turns it) and sets it; <see cref="Restore"/> undoes it.</summary>
        internal struct SeasonLock
        {
            int season, days;
            public static SeasonLock Take() => new SeasonLock { season = Weather.Season, days = Weather.DaysPerSeason };
            public void Set(int s) { Weather.DaysPerSeason = 0; Weather.Season = s; }
            public void Restore() { Weather.Season = season; Weather.DaysPerSeason = days; }
        }

        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        internal static void Grant(WastelandGame g, Recipe r, int times = 1)
        {
            foreach (var (t, n) in r.resources) if (t != ResourceType.None) g.Inventory.Add(t, RecipeLibrary.Amount(n) * times);
            foreach (var (it, n) in r.items) g.Inventory.AddItem(it, n * times);
            if (r.fuel != ResourceType.None) g.Inventory.Add(r.fuel, r.fuelAmount * times);
        }

        internal static int Output(WastelandGame g, Recipe r, CraftingStation st) =>
            r.kind == OutputKind.Resource ? g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) : g.Inventory.GetItem(r.output) + st.tray.GetItem(r.output);
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

    /// <summary>Store the harvest: sausage at the smokehouse, dried fruit on the rack and sauerkraut in the crock (each
    /// made through the station's job queue, advanced to 90 %); none of them rot. Apples keep in a root cellar and rot in
    /// an open crate, preserves in the open crate do not rot (spoilage advanced directly, disclosed). Preserves are worth
    /// more than the produce they took, and fetch more in winter than at the autumn harvest.</summary>
    class SeasonsPreserves : Scenario
    {
        public override string Id => "seasons.preserves";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(14f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -4f), 0f);
            yield return new WaitForSeconds(0.3f);
            c.Fixture("player on a clear pad; the pieces spawned there directly (no build costs); every recipe input granted");
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            Placeable Put(string id, float x, float z) => FurnitureLibrary.Spawn(id, g.Build.Structures, P(x, z), Quaternion.Euler(0f, 180f, 0f), g.propMaterial);

            // ---- the three preserving stations
            var steps = new[] { ("smokehouse", "smoke_sausage"), ("drying_rack", "rack_apple"), ("pickling_crock", "crock_sauerkraut") };
            int made = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                var (piece, id) = steps[i];
                var r = RecipeLibrary.Get(id);
                if (!c.Check(r != null, "recipe " + id + " exists")) continue;
                var pl = Put(piece, -5f + i * 4f, 0f);
                yield return null;
                var st = pl ? pl.GetComponentInChildren<CraftingStation>() : null;
                if (!c.Check(st && st.type == r.station, $"{piece} offers {r.station} recipes")) continue;
                SeasonsScenarios.Grant(g, r);
                string why = g.CraftBlockReason(r, st);
                if (!c.Check(why == null, $"{r.name} craftable at the {piece}" + (why != null ? ": " + why : ""))) continue;
                int before = SeasonsScenarios.Output(g, r, st);
                g.Craft(r, st);
                if (st.queue.Count > 0) st.queue[0].progress = 0.9f;
                c.Fixture($"{piece} job advanced to 90 %");
                yield return SeasonsScenarios.Until(() => st.queue.Count == 0, 20f);
                yield return new WaitForSeconds(0.3f);
                int got = SeasonsScenarios.Output(g, r, st) - before;
                var food = FoodLibrary.Get(r.output);
                if (c.Check(got == r.amount, $"{piece}: {r.name} delivers {got} of {r.amount}")) made++;
                c.Check(food != null && food.spoilMinutes == 0f && FoodLibrary.Preserved(r.output), $"{ItemIds.Name(r.output)} keeps (never spoils)");
                float outV = Trade.Value(r.output) * r.amount, inV = 0f;
                foreach (var (it, n) in r.items) inV += Trade.Value(it) * n;
                c.Metric("value_" + r.output, Trade.Value(r.output), "scrap");
                c.Check(outV > inV, $"{r.name} is worth {outV:0} scrap against {inV:0} for the fresh food it took");
            }
            c.Metric("preserves_made", made, "");

            // ---- the root cellar against an open crate
            var cellar = Put("root_cellar", 6f, -6f);
            var open = Put("crate", -8f, -9f);
            yield return null;
            var cBox = cellar ? cellar.GetComponent<Container>() : null;
            var oBox = open ? open.GetComponent<Container>() : null;
            if (!c.Check(cBox && oBox, "a root cellar and an open crate spawn with storage")) yield break;
            c.Metric("cellar_spoil_factor", cBox.SpoilFactor, "x");
            c.Check(cBox.SpoilFactor <= 0.34f && oBox.SpoilFactor >= 0.99f, $"rot runs at {cBox.SpoilFactor:0.00}x in the cellar, {oBox.SpoilFactor:0.00}x in the open");
            cBox.inventory.AddItem("food_apple", 4); oBox.inventory.AddItem("food_apple", 4);
            oBox.inventory.AddItem("food_dried_fruit", 2); oBox.inventory.AddItem("food_sausage", 2); oBox.inventory.AddItem("food_sauerkraut", 2);
            c.Fixture("4 apples each in the cellar and an open crate 15 m away; 2 each of dried fruit, sausage and sauerkraut in the open crate");
            g.SpoilContainer(cBox, 3000f); g.SpoilContainer(oBox, 3000f);
            c.Fixture("50 minutes of spoilage applied to both (WastelandGame.SpoilContainer; apples rot in 50 in the open)");
            int rotC = cBox.inventory.GetItem("food_rotten"), rotO = oBox.inventory.GetItem("food_rotten");
            c.Metric("rotten_cellar", rotC, ""); c.Metric("rotten_open", rotO, "");
            c.Check(rotO >= 3, $"apples rot in the open crate ({rotO} of 4)");
            c.Check(rotC <= 1 && cBox.inventory.GetItem("food_apple") >= 3, $"and keep in the root cellar ({rotC} rotten)");
            c.Check(oBox.inventory.GetItem("food_dried_fruit") == 2 && oBox.inventory.GetItem("food_sausage") == 2 && oBox.inventory.GetItem("food_sauerkraut") == 2, "preserves keep even in the open");

            // ---- sell the winter
            var sts = g.World.settlements;
            if (c.Check(sts.Count > 0, "the world has settlements"))
            {
                var lk = SeasonsScenarios.SeasonLock.Take();
                var town = Trade.Town;
                try
                {
                    Trade.Town = sts[0];
                    lk.Set(1); int autumn = Trade.SellPrice("food_sausage", 0f), autumnFresh = Trade.SellPrice("food_meat_raw", 0f);
                    lk.Set(2); int winter = Trade.SellPrice("food_sausage", 0f);
                    c.Fixture($"season held at AUTUMN, then WINTER (Weather.Season, DaysPerSeason 0); prices at {Market.TownName(sts[0])}");
                    c.Metric("sausage_autumn", autumn, "scrap"); c.Metric("sausage_winter", winter, "scrap");
                    c.Check(winter > autumn, $"a sausage sells for {autumn} at the harvest and {winter} in winter");
                    c.Check(autumn * 3 > autumnFresh * 2, $"three sausages ({autumn * 3}) fetch more than the two raw cuts they took ({autumnFresh * 2})");
                }
                finally { lk.Restore(); Trade.Town = town; }
            }

            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("seasons_preserves");
            yield return null;
        }
    }

    /// <summary>Seasonal stock and chores: the same food stall and pack trader on the same day, the season held at each
    /// of the four (disclosed): spring brings seed, summer fishing gear, autumn the harvest and salt, winter preserves and
    /// firewood while fresh food thins out. A village resident asks for the season's chore, takes the goods and pays.</summary>
    class SeasonsStock : Scenario
    {
        public override string Id => "seasons.stock";
        public override float Timeout => 90f;

        static Dictionary<string, int> Map(List<Trade.Offer> l) { var d = new Dictionary<string, int>(); foreach (var o in l) d[o.id] = o.count; return d; }
        static int Sum(Dictionary<string, int> d, System.Func<string, bool> f) => d.Where(kv => f(kv.Key)).Sum(kv => kv.Value);
        static bool Fresh(string id) { var f = FoodLibrary.Get(id); return f != null && f.spoilMinutes > 0f && !FoodLibrary.Preserved(id); }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var lk = SeasonsScenarios.SeasonLock.Take();
            Npc.Npc npc = null;
            try
            {
                var food = NpcProfile.Make("seasons:food", NpcRole.Stallkeeper, 424242, "food");
                var pack = NpcProfile.Make("seasons:pack", NpcRole.Trader, 515151, "pack");
                var fs = new NpcSave(); var ps = new NpcSave();
                c.Fixture("two test vendor profiles (a food stall, a pack trader), stock read without spawning them");
                c.Fixture("season held through Weather.Season with DaysPerSeason = 0, restored afterwards");
                var stall = new Dictionary<string, int>[4]; var trader = new Dictionary<string, int>[4];
                for (int s = 0; s < 4; s++) { lk.Set(s); stall[s] = Map(Trade.Stock(food, fs, 0f)); trader[s] = Map(Trade.Stock(pack, ps, 0f)); }
                for (int s = 0; s < 4; s++) c.Note(Weather.SeasonNames[s] + ": " + string.Join(", ", stall[s].Select(kv => kv.Key + " " + kv.Value)));
                string wood = "res:" + (int)ResourceType.Wood, salt = "res:" + (int)ResourceType.Salt;

                int seedKinds = stall[3].Keys.Count(k => k.StartsWith("seed_") || k.StartsWith("sapling_"));
                c.Metric("spring_seed_kinds", seedKinds, "");
                c.Check(seedKinds >= 3, $"spring: the stall carries {seedKinds} kinds of seed and saplings");
                int seedSpring = Sum(stall[3], k => k.StartsWith("seed_")), seedWinter = Sum(stall[2], k => k.StartsWith("seed_"));
                c.Check(seedSpring > seedWinter, $"seed is plentiful in spring ({seedSpring}) and scarce in winter ({seedWinter})");
                c.Check(trader[0].Keys.Any(k => k.StartsWith("bait_") || k == "tool_fishing_rod"), "summer: the pack trader carries fishing gear");
                c.Check(!trader[2].ContainsKey("tool_fishing_rod") && !trader[2].Keys.Any(k => k.StartsWith("bait_")), "no fishing gear in winter");
                c.Check(stall[1].ContainsKey("food_apple") && stall[1].ContainsKey(salt), "autumn: the harvest in, and salt for putting it up");
                c.Check(trader[2].TryGetValue(wood, out int logs) && logs >= 10, $"winter: the pack trader sells firewood ({logs})");
                int keepW = Sum(stall[2], FoodLibrary.Preserved), keepS = Sum(stall[0], FoodLibrary.Preserved);
                c.Metric("winter_preserves", keepW, ""); c.Metric("summer_preserves", keepS, "");
                c.Check(keepW > keepS, $"winter: more preserves on the stall ({keepW}) than in summer ({keepS})");
                int freshW = Sum(stall[2], Fresh), freshA = Sum(stall[1], Fresh);
                c.Metric("winter_fresh", freshW, ""); c.Metric("autumn_fresh", freshA, "");
                c.Check(freshW < freshA, $"fresh food thins out in winter ({freshW}) against the harvest ({freshA})");
                bool differ = true;
                for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++) differ &= !new HashSet<string>(stall[a].Keys).SetEquals(stall[b].Keys);
                c.Check(differ, "every season stocks the stall differently");

                // ---- chores
                c.Check(NpcLore.Chores[NpcLore.ChoreFor(1, false)].item == "any:harvest" && NpcLore.Chores[NpcLore.ChoreFor(1, true)].item == wood,
                    "autumn chores: bring in the crop, then cut firewood before the snow");
                c.Check(NpcLore.ChoreFor(2, false) >= 0 && NpcLore.ChoreFor(3, false) >= 0 && NpcLore.ChoreFor(0, true) >= 0, "every season has a chore");

                lk.Set(1);
                var me = g.Player.transform;
                var at = me.position + me.forward * 1.6f;
                at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.05f;
                var res = NpcProfile.Make("seasons:resident", NpcRole.Resident, 777001);
                var rs = NpcRegistry.Get(res);
                rs.Set(NpcSave.Met); rs.disposition = 20;
                npc = Npc.Npc.Spawn(res, at, me.eulerAngles.y + 180f, null, g.propMaterial);
                c.Fixture("a village resident spawned in front of the player, already met (disposition 20); season held at AUTUMN");
                yield return null;
                var d = new Dialogue(g, npc);
                var offer = d.choices.FirstOrDefault(ch => ch.label == "NEED A HAND AROUND THE PLACE?");
                if (!c.Check(offer.act != null, "the resident asks for a hand (" + string.Join(" / ", d.choices.Select(ch => ch.label)) + ")")) yield break;
                offer.act();
                c.Note(d.line);
                var take = d.choices.FirstOrDefault(ch => ch.label == "CONSIDER IT DONE.");
                if (!c.Check(take.act != null, "the chore can be taken")) yield break;
                take.act();
                var chore = NpcLore.Chores[rs.choreIndex];
                c.Check(rs.choreState == 1 && chore.season == 1, "an autumn chore is taken: " + chore.ask);
                string goods = chore.item == "any:harvest" ? "food_potato" : chore.item == "any:preserve" ? "food_jerky" : chore.item == "any:seed" ? "seed_corn" : chore.item;
                int Have() => goods.StartsWith("res:") ? g.Inventory.Get((ResourceType)int.Parse(goods.Substring(4))) : g.Inventory.GetItem(goods);
                if (goods.StartsWith("res:")) g.Inventory.Add((ResourceType)int.Parse(goods.Substring(4)), chore.n); else g.Inventory.AddItem(goods, chore.n);
                c.Fixture($"{chore.n} x {goods} granted for the chore");
                int scrap0 = g.Inventory.Get(ResourceType.Scrap), gift0 = chore.gift != null ? g.Inventory.GetItem(chore.gift) : 0, have0 = Have();
                var hand = d.choices.FirstOrDefault(ch => ch.label.StartsWith("ABOUT THE "));
                if (!c.Check(hand.act != null, "the chore can be handed in")) yield break;
                hand.act();
                c.Note(d.line);
                c.Metric("chore_pay", g.Inventory.Get(ResourceType.Scrap) - scrap0, "scrap");
                c.Check(rs.choreState == 2 && have0 - Have() == chore.n, $"the resident took the {chore.n} {chore.what}");
                c.Check(g.Inventory.Get(ResourceType.Scrap) > scrap0 && (chore.gift == null || g.Inventory.GetItem(chore.gift) > gift0), "and paid scrap and a gift from the stores");
                c.Check(!d.choices.Any(ch => ch.label == "NEED A HAND AROUND THE PLACE?"), "one chore per half season");
            }
            finally
            {
                lk.Restore();
                if (npc) Object.Destroy(npc.gameObject);
            }
        }
    }

    /// <summary>A wood stove working a job is heard: a loop voice plays its sizzle (synthesised by ProceduralSfx) beside
    /// the player while the queue runs and falls quiet when it is done; the pickling crock bubbles softly.</summary>
    class SeasonsStationSounds : Scenario
    {
        public override string Id => "seasons.station_sounds";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            var fwd = g.Player.transform.forward;
            var pl = FurnitureLibrary.Spawn("stove", g.Build.Structures, g.Player.transform.position + fwd * 1.8f, Quaternion.LookRotation(-fwd), g.propMaterial);
            yield return null;
            var st = pl ? pl.GetComponentInChildren<CraftingStation>() : null;
            if (!c.Check(st && st.type == "stove", "a wood stove spawns in front of the player")) yield break;
            c.Fixture("wood stove spawned directly (no build cost)");
            string key = CraftingStation.WorkLoop(st.type, out _);
            c.Check(key == "station_sizzle", "a stove's work loop is " + key);
            var clip = key != null ? Sfx.Clip(key) : null;
            if (c.Check(clip, "the loop has a clip" + (clip ? " (" + clip.name + ", " + clip.length.ToString("0.0") + " s)" : ""))) c.Metric("loop_seconds", clip.length, "s");
            c.Check(CraftingStation.WorkLoop("crock", out float crockVol) == "station_bubble" && crockVol < 0.2f, "the pickling crock bubbles, quietly");

            var r = RecipeLibrary.Get("stove_meat");
            if (!c.Check(r != null, "recipe stove_meat exists")) yield break;
            SeasonsScenarios.Grant(g, r, 2);
            c.Fixture("inputs for two stove jobs granted");
            g.Craft(r, st); g.Craft(r, st);
            if (!c.Check(st.Busy && st.Powered, "the stove works a job")) yield break;
            if (Application.isBatchMode) c.Note("batch mode: Sfx is muted, the voice checks are skipped");
            else
            {
                yield return SeasonsScenarios.Until(() => Sfx.Looping(st) == key, 2f);
                c.Check(Sfx.Looping(st) == key, "a loop voice plays " + key + " at the working stove (" + (Sfx.Looping(st) ?? "none") + ")");
            }
            c.Screenshot("seasons_station_sounds");
            yield return null;
            foreach (var j in st.queue) j.progress = 0.97f;
            c.Fixture("both jobs advanced to 97 %");
            yield return SeasonsScenarios.Until(() => st.queue.Count == 0, 10f);
            yield return null; yield return null;
            c.Check(st.queue.Count == 0, "the stove finished its jobs");
            if (!Application.isBatchMode) c.Check(Sfx.Looping(st) == null, "and fell quiet");
        }
    }

    /// <summary>The news read where the player trades: a board at the nearest town shows an off-screen skirmish near it
    /// (flashed with its place, as NpcDirector does), a dusk raid that sacked the town (TownQuests.PostSacked) and the
    /// radio's season prices; a board at a town far away carries the flash but not the local news. Read on the board
    /// page; the news ledger is restored afterwards.</summary>
    class SeasonsNoticeBoard : Scenario
    {
        public override string Id => "seasons.notice_board";
        public override float Timeout => 60f;

        static string Page(WastelandGame g) => string.Join(" ", g.Menus.Rows());

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var sts = g.World.settlements;
            if (sts.Count < 2) { c.Block("fewer than two settlements"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform;
            Vector2 mine = new Vector2(me.position.x, me.position.z);
            var st = sts.OrderBy(s => Vector2.Distance(s.pos, mine)).First();
            var far = sts.OrderByDescending(s => Vector2.Distance(s.pos, st.pos)).First();
            if (Vector2.Distance(far.pos, st.pos) < TownNews.Reach + 400f) { c.Block("no second town beyond the news reach"); yield break; }
            var saved = TownNews.Save();
            var made = new List<GameObject>();
            try
            {
                var board = BountyBoard.All.FirstOrDefault(b => b && b.town == st.index && Vector2.Distance(new Vector2(b.transform.position.x, b.transform.position.z), st.pos) < 300f);
                if (!board)
                {
                    var go = new GameObject("TestBoard");
                    go.transform.position = new Vector3(st.pos.x, 0f, st.pos.y);
                    board = go.AddComponent<BountyBoard>(); board.town = st.index; made.Add(go);
                    c.Fixture("no board streamed in for " + Market.TownName(st) + ": a bare board for it placed at the town");
                }
                var og = new GameObject("TestBoardFar");
                og.transform.position = new Vector3(far.pos.x, 0f, far.pos.y);
                var farBoard = og.AddComponent<BountyBoard>(); farBoard.town = far.index; made.Add(og);
                c.Fixture("a bare board for " + Market.TownName(far) + " at that town");

                var bp = board.transform.position;
                string gang = NpcLore.Gangs[0];
                string skirmish = "THE " + gang + " HIT A GUILD CONVOY NEAR " + Market.TownName(st) + ". DRIVE CAREFUL.";
                RadioNetwork.Flash("WORD ON THE ROAD: " + skirmish, 5f, bp + new Vector3(250f, 0f, 120f));
                c.Fixture("an off-screen convoy skirmish 280 m from the board, flashed with its place as NpcDirector.Skirmishes does");
                TownQuests.PostSacked(st);
                string sacked = Market.TownName(st) + " AT DUSK";
                c.Fixture("a dusk raid on " + Market.TownName(st) + " that nobody stopped (TownQuests.PostSacked)");
                string wire = Weather.SeasonNames[2] + " MARKETS: " + Market.SeasonNews(2);
                RadioNetwork.Flash(wire, 5f);
                c.Fixture("the season-price flash, region-wide (as Weather.UpdateSeason sends it)");

                g.Menus.OpenBoard(board);
                yield return null; yield return null;
                string page = Page(g);
                c.Check(page.Contains(skirmish), "the board page shows the skirmish headline (radio lead-in dropped)");
                c.Check(page.Contains("SACKED " + sacked), "and the sacked town");
                c.Check(page.Contains(wire), "and the season prices from the radio");
                var rig = Object.FindAnyObjectByType<CameraRig>();
                if (rig) rig.SetTarget(g.Player.transform);
                yield return new WaitForSeconds(0.5f);
                c.Screenshot("seasons_notice_board");
                yield return null;
                g.Menus.Close();
                g.Menus.OpenBoard(farBoard);
                yield return null;
                string page2 = Page(g);
                c.Check(!page2.Contains(skirmish) && !page2.Contains("SACKED " + sacked) && page2.Contains(wire), Market.TownName(far) + "'s board: the flash, not the other town's news");
                g.Menus.Close();
            }
            finally
            {
                if (g.Menus.IsOpen) g.Menus.Close();
                foreach (var go in made) if (go) Object.Destroy(go);
                TownNews.Load(saved);
            }
        }
    }
}
