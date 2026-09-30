using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>The cooking ladder (depth stage A): campfire (spit, embers, billy can) → wood stove (pot and pan) and
    /// electric oven (baking) → powered kitchen range (triple batches, finer) → cannery (tins that never spoil), plus
    /// brewing at the still.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> Kitchen()
        {
            var Wa = ResourceType.Water; var G = ResourceType.Glass;

            // ---- campfire: slow, burns wood, only the simple things
            yield return Fire("meat", "SPIT-ROAST MEAT", "food_meat_cooked", 1, null, ("food_meat_raw", 1));
            yield return Fire("fish", "SPIT-ROAST FISH", "food_fish_cooked", 1, null, ("food_fish_raw", 1));
            yield return Fire("corn", "ROAST CORN", "food_corn_roast", 1, null, ("food_corn", 1));
            yield return Fire("potato", "POTATO IN THE EMBERS", "food_potato_baked", 1, null, ("food_potato", 1));
            yield return Fire("bugs", "BUG SKEWER", "food_bug_skewer", 1, null, ("food_bugmeat", 2));
            yield return Fire("mushroom", "MUSHROOM SKEWER", "food_mush_skewer", 1, null, ("food_mushroom", 2));
            yield return Fire("flatbread", "FLATBREAD X2", "food_flatbread", 2, new[] { (Wa, 1) }, ("crop_flour", 1));
            yield return Fire("tea", "HERB TEA", "drink_tea", 1, new[] { (Wa, 1) }, ("food_herbs", 1));
            var boil = Fire("boil", "BOIL WATER 2L", null, 2, new[] { (ResourceType.DirtyWater, 2) });
            boil.kind = OutputKind.Resource; boil.outputResource = Wa;
            yield return boil;

            // ---- wood stove: pot and pan
            var W = ResourceType.Wood;
            yield return Cook("stove", "meat_stew", "MEAT STEW X2", "food_meat_stew", 2, W, ("food_meat_raw", 1), ("food_potato", 1), ("food_carrot", 1));
            yield return Cook("stove", "fish_soup", "FISH SOUP X2", "food_fish_soup", 2, W, ("food_fish_raw", 1), ("food_potato", 1), ("food_herbs", 1));
            yield return Cook("stove", "pancakes", "PANCAKES X2", "food_pancakes", 2, W, ("crop_flour", 1), ("food_egg", 1), ("drink_milk", 1));
            var tea = Cook("stove", "tea", "HERB TEA X2", "drink_tea", 2, W, ("food_herbs", 1));
            tea.resources = new[] { (Wa, 2) };
            yield return tea;

            // ---- electric oven: baking
            var N = ResourceType.None;
            yield return Cook("oven", "meat_pie", "MEAT PIE X2", "food_meat_pie", 2, N, ("crop_flour", 2), ("food_meat_raw", 1));
            yield return Cook("oven", "roast_dinner", "ROAST DINNER X2", "food_roast_dinner", 2, N, ("food_meat_raw", 1), ("food_potato", 2), ("food_carrot", 1));
            yield return Cook("oven", "cornbread", "CORNBREAD X2", "food_cornbread", 2, N, ("crop_flour", 1), ("food_corn", 1), ("food_egg", 1));
            yield return Cook("oven", "apple_pie", "APPLE PIE X2", "food_apple_pie", 2, N, ("crop_flour", 1), ("food_apple", 2), ("food_sugar", 1));

            // ---- cannery: four tins at a time, never spoil
            yield return Can("stew", "TINNED STEW X4", "food_can_stew", ("food_stew", 2));
            yield return Can("meat", "TINNED MEAT X4", "food_can_meat", ("food_meat_cooked", 3));
            yield return Can("fish", "TINNED FISH X4", "food_can_fish", ("food_fish_cooked", 3));
            yield return Can("veg", "TINNED VEGETABLES X4", "food_can_veg", ("food_tomato", 2), ("food_carrot", 2), ("food_cabbage", 1));
            yield return Can("fruit", "TINNED FRUIT X4", "food_can_fruit", ("food_apple", 2), ("food_berries", 2), ("food_sugar", 1));
            yield return Can("beans", "TINNED BEANS X4", "food_can", ("food_corn", 2), ("food_potato", 2));
            var bottle = Itm("cannery_water", "BOTTLED WATER X4", RecipeCategory.Cooking, "cannery", "drink_water", 4, "SEALED: KEEPS FOREVER", null, (Wa, 4), (G, 1));
            bottle.seconds = 14f;
            yield return bottle;

            // ---- brewing at the still
            var beer = Itm("brew_beer", "BEER X4", RecipeCategory.Cooking, "still", "drink_beer", 4, "WHEAT MASH", new[] { ("crop_wheat", 4) }, (Wa, 3));
            beer.seconds = 40f;
            yield return beer;
            var cider = Itm("brew_cider", "CIDER X3", RecipeCategory.Cooking, "still", "drink_cider", 3, "PRESSED APPLES", new[] { ("food_apple", 4) });
            cider.seconds = 35f;
            yield return cider;
        }

        static Recipe Fire(string id, string name, string output, int amount, (ResourceType, int)[] res, params (string, int)[] items) => new Recipe
        {
            id = "fire_" + id, name = name, category = RecipeCategory.Cooking, station = "campfire", amount = amount,
            kind = OutputKind.Item, output = output, items = items, resources = res ?? new (ResourceType, int)[0],
            description = "OVER AN OPEN FIRE (1 WOOD)", fuel = ResourceType.Wood, fuelAmount = 1, seconds = 18f
        };

        static Recipe Can(string id, string name, string output, params (string, int)[] items)
        {
            var r = Itm("can_" + id, name, RecipeCategory.Cooking, "cannery", output, 4, "SEALED TINS: NEVER SPOIL", items, (ResourceType.Iron, 1), (ResourceType.Water, 1));
            r.seconds = 16f;
            return r;
        }

        /// <summary>The kitchen range cooks every stove and oven dish in triple batches in the same time.</summary>
        static List<Recipe> RangeBatches(List<Recipe> list)
        {
            var seen = new HashSet<string>();
            var outList = new List<Recipe>();
            foreach (var r in list)
            {
                if ((r.station != "stove" && r.station != "oven") || r.category != RecipeCategory.Cooking) continue;
                string key = r.id.Substring(r.station.Length + 1);
                if (!seen.Add(key)) continue;
                var items = new (string, int)[r.items != null ? r.items.Length : 0];
                for (int i = 0; i < items.Length; i++) items[i] = (r.items[i].Item1, r.items[i].Item2 * 3);
                var res = new (ResourceType, int)[r.resources != null ? r.resources.Length : 0];
                for (int i = 0; i < res.Length; i++) res[i] = (r.resources[i].Item1, r.resources[i].Item2 * 3);
                outList.Add(new Recipe
                {
                    id = "range_" + key, name = r.name + " (X3)", category = RecipeCategory.Cooking, station = "range", kind = r.kind,
                    output = r.output, outputResource = r.outputResource, amount = r.amount * 3, items = items, resources = res,
                    description = "TRIPLE BATCH; NEEDS POWER", seconds = Seconds(r)
                });
            }
            return outList;
        }
    }
}
