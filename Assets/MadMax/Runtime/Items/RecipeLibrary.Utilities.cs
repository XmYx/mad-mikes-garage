using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Item ids of depth stage F (water quality, desalination, power control, biogas, cold storage).</summary>
    public static class UtilityIds
    {
        public const string WaterTest = "use_water_test", Cartridge = "use_filter_cartridge", DesalKit = "kit_desalinator", Ice = "misc_ice",
            SaltMeat = "food_meat_salted", SaltFish = "food_fish_salted", Pickles = "food_pickles";
    }

    /// <summary>Recipes for depth stage F: water quality, desalination, power control, biogas, cold storage. Salt
    /// comes up the same ladder as the water: boiled down over a campfire (salt only), a solar still (piece, free and
    /// slow), the distillery, the powered desalinator (fast, also pure water); it preserves meat, fish and vegetables at
    /// the counter.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> UtilitiesRecipes()
        {
            var Sw = ResourceType.SeaWater; var Wa = ResourceType.Water; var Dw = ResourceType.DirtyWater; var Sa = ResourceType.Salt;
            var G = ResourceType.Glass; var Ch = ResourceType.Charcoal; var C = ResourceType.Cloth; var Fe = ResourceType.Iron; var Cu = ResourceType.Copper;

            // ---- water quality: the test kit and filter cartridges (workbench)
            yield return Itm("water_test", "WATER TEST KIT", RecipeCategory.Supplies, "workbench", UtilityIds.WaterTest, 1,
                "USE NEAR A TANK, WELL, TAP OR OPEN WATER: CLEAN, DIRTY, OILY, SALTY, FOUL", new[] { (ItemIds.Paper, 1) }, (G, 2), (Ch, 1));
            yield return Itm("filter_cartridge", "FILTER CARTRIDGES X2", RecipeCategory.Supplies, "workbench", UtilityIds.Cartridge, 2,
                "FOR THE WATER FILTER: [E] AT THE FILTER. OIL CLOGS THEM FAST", null, (Ch, 3), (ResourceType.Sand, 2), (C, 1));
            var kit = Itm("desal_kit", "DESALINATOR KIT", RecipeCategory.Building, "workbench", UtilityIds.DesalKit, 1,
                "POWERED: SEA WATER TO DRINKING WATER AND SALT, ALSO ON A PIPE NETWORK", new[] { (ItemIds.Coil, 1) }, (Fe, 6), (Cu, 4), (G, 2), (ResourceType.Rubber, 2));
            kit.seconds = 30f;
            yield return kit;

            // ---- salt and sweet water up the ladder
            var boil = Res("fire_salt", "BOIL DOWN SEA WATER", RecipeCategory.Supplies, "campfire", Sa, 1, "OVER AN OPEN FIRE (1 WOOD): THE WATER IS LOST", (Sw, 5));
            boil.fuel = ResourceType.Wood; boil.fuelAmount = 1; boil.seconds = 20f;
            yield return boil;
            var still = Res("still_sea", "DISTIL SEA WATER 5L", RecipeCategory.Supplies, "still", Wa, 5, "SWEET WATER AND A SALT CAKE", (Sw, 6), (Ch, 1));
            still.byproducts = new[] { (Sa, 1) }; still.seconds = 25f;
            yield return still;
            var desal = Res("desal_sea", "DESALINATE 9L", RecipeCategory.Supplies, "desalinator", Wa, 9, "10 L SEA WATER: 9 L DRINKING WATER, 1 SALT", (Sw, 10));
            desal.byproducts = new[] { (Sa, 1) }; desal.seconds = 12f;
            yield return desal;
            var purify = Res("desal_dirty", "PURIFY 10L", RecipeCategory.Supplies, "desalinator", Wa, 10, "ANY DIRTY WATER THROUGH THE MEMBRANES", (Dw, 10));
            purify.seconds = 10f;
            yield return purify;

            // ---- preserving with salt (counter, no fire)
            yield return Itm("counter_salt_meat", "SALT MEAT X2", RecipeCategory.Cooking, "counter", UtilityIds.SaltMeat, 2, "PACKED IN SALT: KEEPS, BUT MAKES YOU THIRSTY", new[] { ("food_meat_raw", 2) }, (Sa, 1));
            yield return Itm("counter_salt_fish", "SALT FISH X2", RecipeCategory.Cooking, "counter", UtilityIds.SaltFish, 2, "SALTED AND DRIED: KEEPS", new[] { ("food_fish_raw", 2) }, (Sa, 1));
            yield return Itm("counter_pickles", "PICKLED VEGETABLES X3", RecipeCategory.Cooking, "counter", UtilityIds.Pickles, 3, "BRINE IN A JAR: KEEPS FOREVER", new[] { ("food_cabbage", 1), ("food_carrot", 1) }, (Sa, 1), (Wa, 1), (G, 1));
        }
    }
}
