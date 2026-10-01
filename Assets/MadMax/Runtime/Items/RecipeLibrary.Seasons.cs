using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Store the harvest (Seasons): the garden's surplus put up for the winter. The drying rack (no fuel, slow)
    /// dries fruit, tomatoes, mushrooms, corn, herbs, fish and meat; the pickling crock ferments cabbage, beets, eggs and
    /// fish in salt brine; the wood stove seals home jars; the smokehouse adds sausage and smoked cheese. Everything made
    /// here keeps (<see cref="FoodLibrary.Preserved"/>) and sells dear in winter and the hungry spring.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> SeasonsRecipes()
        {
            var Sa = ResourceType.Salt; var Wa = ResourceType.Water; var G = ResourceType.Glass; var W = ResourceType.Wood;

            // ---- drying rack: sun and wind, no fuel, slow
            yield return Rack("apple", "DRIED FRUIT X2 (APPLES)", "food_dried_fruit", 2, null, ("food_apple", 3));
            yield return Rack("berries", "DRIED FRUIT X2 (BERRIES)", "food_dried_fruit", 2, null, ("food_berries", 4));
            yield return Rack("tomato", "SUN-DRIED TOMATOES X2", "food_dried_tomato", 2, null, ("food_tomato", 3));
            yield return Rack("mushroom", "DRIED MUSHROOMS X2", "food_dried_mushroom", 2, null, ("food_mushroom", 3));
            yield return Rack("corn", "DRIED CORN X3", "food_dried_corn", 3, null, ("food_corn", 3));
            yield return Rack("herbs", "DRIED HERBS X2", "food_dried_herbs", 2, null, ("food_herbs", 2));
            yield return Rack("fish", "STOCKFISH", "food_fish_dried", 1, null, ("food_fish_raw", 2));
            yield return Rack("jerky", "AIR-DRIED JERKY X3", "food_jerky", 3, new[] { (Sa, 1) }, ("food_meat_raw", 3));

            // ---- pickling crock: salt brine under a stone
            yield return Crock("sauerkraut", "SAUERKRAUT X3", "food_sauerkraut", 3, new[] { (Sa, 1) }, ("food_cabbage", 2));
            yield return Crock("beets", "PICKLED BEETS X3", "food_pickled_beet", 3, new[] { (Sa, 1), (Wa, 1) }, ("food_beet", 3));
            yield return Crock("eggs", "PICKLED EGGS X4", "food_pickled_egg", 4, new[] { (Sa, 1), (Wa, 1) }, ("food_egg", 4));
            yield return Crock("fish", "PICKLED FISH X2", "food_pickled_fish", 2, new[] { (Sa, 1) }, ("food_fish_raw", 2));
            yield return Crock("veg", "PICKLED VEGETABLES X4", "food_pickles", 4, new[] { (Sa, 1), (Wa, 1) }, ("food_carrot", 2), ("food_cabbage", 1));

            // ---- wood stove: home jars (the range triples them, RangeBatches)
            yield return Jar("jar_tomato", "JARRED TOMATOES X2", "food_jar_tomato", new[] { (G, 1) }, ("food_tomato", 3));
            yield return Jar("jar_pumpkin", "JARRED PUMPKIN X2", "food_jar_pumpkin", new[] { (G, 1) }, ("food_pumpkin", 1), ("food_sugar", 1));

            // ---- smokehouse
            var sausage = Itm("smoke_sausage", "SMOKED SAUSAGE X3", RecipeCategory.Cooking, "smokehouse", "food_sausage", 3, "SALTED, HERBED, SMOKED: KEEPS FOREVER", new[] { ("food_meat_raw", 2), ("food_herbs", 1) }, (Sa, 1));
            sausage.fuel = W; sausage.fuelAmount = 1; sausage.seconds = 50f;
            yield return sausage;
            var cheese = Itm("smoke_cheese", "SMOKED CHEESE X2", RecipeCategory.Cooking, "smokehouse", "food_cheese_smoked", 2, "A RIND OF SMOKE: SELLS DEAR", new[] { ("food_cheese", 2) });
            cheese.fuel = W; cheese.fuelAmount = 1; cheese.seconds = 40f;
            yield return cheese;
        }

        static Recipe Rack(string id, string name, string output, int amount, (ResourceType, int)[] res, params (string, int)[] items)
        {
            var r = Itm("rack_" + id, name, RecipeCategory.Cooking, "drying_rack", output, amount, "DRIES IN SUN AND WIND: KEEPS FOREVER", items, res ?? new (ResourceType, int)[0]);
            r.seconds = 55f;
            return r;
        }

        static Recipe Crock(string id, string name, string output, int amount, (ResourceType, int)[] res, params (string, int)[] items)
        {
            var r = Itm("crock_" + id, name, RecipeCategory.Cooking, "crock", output, amount, "FERMENTED IN BRINE: KEEPS FOREVER", items, res);
            r.seconds = 60f;
            return r;
        }

        static Recipe Jar(string id, string name, string output, (ResourceType, int)[] res, params (string, int)[] items)
        {
            var r = Cook("stove", id, name, output, 2, ResourceType.Wood, items);
            r.resources = res; r.description = "BOILED AND SEALED IN GLASS (1 WOOD): KEEPS";
            return r;
        }
    }
}
