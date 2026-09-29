using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Items
{
    public class FoodDef
    {
        public string id, name;
        public float hunger, thirst, heal;
        public float sickChance;          // food poisoning chance when eaten (raised by dirty hands)
        public float spoilMinutes;        // 0 = keeps forever
        public string spoilsTo = "food_rotten";
        public Color32 color;
    }

    /// <summary>A plant grown from a seed in a garden plot (or a sapling in the ground).</summary>
    public class CropDef
    {
        public string seed, name;
        public float growMinutes;
        public (string item, int min, int max)[] yields;
        public float seedChance = 0.6f;   // chance per harvest to get a seed back (plus the guaranteed one for trees' fruit)
        public bool tree;                 // saplings: planted in the ground, regrow fruit, felled for wood
        public float regrowMinutes;       // trees: fruit regrow time (0 = no fruit)
        public Color32 leaf, fruit;
        public int height = 8;            // voxels at full growth
        public bool dark;                 // grows only without daylight (mushrooms: under a roof or underground)
    }

    /// <summary>Food, drinks and crops (data).</summary>
    public static class FoodLibrary
    {
        static Dictionary<string, FoodDef> foods;
        static List<CropDef> crops;

        static FoodDef F(string id, string name, float hunger, float thirst, float spoil, Color32 c, float sick = 0f, float heal = 0f) =>
            new FoodDef { id = id, name = name, hunger = hunger, thirst = thirst, spoilMinutes = spoil, color = c, sickChance = sick, heal = heal };

        static Color32 H(string hex) => MadMax.Voxel.Pal.Hex(hex);

        static void Ensure()
        {
            if (foods != null) return;
            foods = new Dictionary<string, FoodDef>();
            foreach (var f in new[]
            {
                F("food_can", "CANNED BEANS", 35, -4, 0, H("b0a080")),
                F("food_ration", "RATION PACK", 50, 0, 0, H("6a7040")),
                F("drink_soda", "OLD SODA", 4, 30, 0, H("b02818")),
                F("drink_water", "BOTTLED WATER", 0, 40, 0, H("8cbcd6")),
                F("food_corn", "CORN", 10, 0, 40, H("e0c040"), 0.02f),
                F("food_corn_roast", "ROAST CORN", 24, 0, 30, H("c8902a")),
                F("food_potato", "RAW POTATO", 6, 0, 60, H("a88050"), 0.15f),
                F("food_potato_baked", "BAKED POTATO", 25, 0, 30, H("b07a40")),
                F("food_tomato", "TOMATO", 8, 6, 25, H("c83020")),
                F("food_carrot", "CARROT", 9, 2, 45, H("e07020")),
                F("food_cabbage", "CABBAGE", 12, 3, 40, H("78a040")),
                F("food_pumpkin", "PUMPKIN", 18, 2, 80, H("e08020")),
                F("food_apple", "APPLE", 10, 4, 50, H("c02c20")),
                F("food_berries", "BERRIES", 6, 3, 15, H("6a1c50")),
                F("food_herbs", "HERBS", 2, 0, 20, H("4a8030"), 0f, 4f),
                F("food_sunseeds", "SUNFLOWER SEEDS", 8, -2, 0, H("3a3020")),
                F("food_coconut", "COCONUT", 12, 14, 60, H("6a4a2a")),
                F("food_stew", "VEGETABLE STEW", 60, 10, 40, H("8a5a2a"), 0f, 8f),
                F("food_soup", "TOMATO SOUP", 40, 20, 30, H("a83020"), 0f, 5f),
                F("food_pie", "PUMPKIN PIE", 55, 0, 40, H("d09040")),
                F("food_jam", "APPLE JAM", 30, 0, 0, H("8a2030")),
                F("food_rotten", "ROTTEN FOOD", 2, 0, 0, H("4a4a2a"), 0.8f),
                F("food_salad", "GARDEN SALAD", 30, 8, 20, H("6a9a3a"), 0f, 4f),
                F("food_fruit", "FRUIT BOWL", 24, 10, 20, H("c04a3a"), 0f, 3f),
                F("food_trailmix", "TRAIL MIX", 18, -3, 0, H("6a4a2a")),
                F("food_meat_raw", "RAW MEAT", 10, 0, 20, H("a83a3a"), 0.35f),
                F("food_meat_cooked", "COOKED MEAT", 38, 0, 30, H("8a4a2a"), 0f, 4f),
                F("food_meat_smoked", "SMOKED MEAT", 32, -3, 0, H("6a3a24"), 0f, 3f),
                F("food_jerky", "JERKY", 20, -5, 0, H("5a2a1a")),
                F("food_fish_raw", "RAW FISH", 8, 2, 15, H("9ab0b8"), 0.3f),
                F("food_fish_cooked", "GRILLED FISH", 30, 2, 25, H("c09060"), 0f, 3f),
                F("food_fish_smoked", "SMOKED FISH", 26, -2, 0, H("a06a3a")),
                F("food_hempseed", "HEMP SEEDS", 6, -2, 0, H("4a4a2a")),
                F("food_mushroom", "MUSHROOMS", 6, 1, 20, H("b8a888"), 0.05f),
                F("food_mushsoup", "MUSHROOM SOUP", 34, 16, 30, H("8a7050"), 0f, 3f),
                F("food_beet", "SUGAR BEET", 8, 2, 70, H("7a2440")),
                F("food_sugar", "SUGAR", 8, -3, 0, H("eeeadc")),
                F("food_bread", "BREAD", 32, -4, 60, H("c89050")),
                F("food_porridge", "WHEAT PORRIDGE", 28, 6, 20, H("d0b080")),
            }) foods[f.id] = f;

            crops = new List<CropDef>
            {
                Crop("seed_corn", "CORN", 8, 14, H("5a8a2a"), H("e0c040"), ("food_corn", 2, 4)),
                Crop("seed_potato", "POTATO", 7, 5, H("4a7a2a"), H("a88050"), ("food_potato", 3, 5)),
                Crop("seed_tomato", "TOMATO", 6, 8, H("3a7024"), H("c83020"), ("food_tomato", 2, 4)),
                Crop("seed_carrot", "CARROT", 5, 4, H("5a9a30"), H("e07020"), ("food_carrot", 2, 3)),
                Crop("seed_cabbage", "CABBAGE", 6, 5, H("78a040"), H("9ac060"), ("food_cabbage", 1, 2)),
                Crop("seed_pumpkin", "PUMPKIN", 10, 5, H("3a6a20"), H("e08020"), ("food_pumpkin", 1, 2)),
                Crop("seed_sunflower", "SUNFLOWER", 8, 16, H("4a7a24"), H("f0c020"), ("food_sunseeds", 2, 4), ("crop_flower", 1, 1)),
                Crop("seed_cotton", "COTTON", 9, 9, H("4a6a2a"), H("eeeadc"), ("crop_cotton", 2, 4)),
                Crop("seed_hemp", "HEMP", 8, 16, H("3a6a24"), H("6a8a3a"), ("crop_hemp", 2, 4), ("food_hempseed", 1, 2)),
                Crop("seed_herbs", "HERBS", 4, 4, H("4a8030"), H("6aa040"), ("food_herbs", 2, 3)),
                Crop("seed_flower", "FLOWERS", 5, 6, H("3a7024"), H("d04070"), ("crop_flower", 1, 3)),
                Crop("seed_berries", "BERRY BUSH", 9, 7, H("2a5a24"), H("6a1c50"), ("food_berries", 3, 6)),
                Crop("seed_wheat", "WHEAT", 7, 10, H("8a9a3a"), H("d8b860"), ("crop_wheat", 3, 5)),
                Crop("seed_beet", "SUGAR BEET", 8, 5, H("3a7a2a"), H("7a2440"), ("food_beet", 2, 4)),
                Dark(Crop("seed_mushroom", "MUSHROOMS", 5, 3, H("8a7a60"), H("d8c8a8"), ("food_mushroom", 2, 4))),
                Tree("sapling_apple", "APPLE TREE", 14, 6, H("3a6a24"), H("c02c20"), 40, ("food_apple", 3, 6)),
                Tree("sapling_pine", "PINE", 16, 0, H("1e3020"), H("1e3020"), 40),
                Tree("sapling_palm", "PALM", 18, 10, H("367024"), H("6a4a2a"), 44, ("food_coconut", 1, 3)),
            };
        }

        static CropDef Crop(string seed, string name, float minutes, int height, Color32 leaf, Color32 fruit, params (string, int, int)[] yields) =>
            new CropDef { seed = seed, name = name, growMinutes = minutes, height = height, leaf = leaf, fruit = fruit, yields = yields };

        static CropDef Dark(CropDef c) { c.dark = true; return c; }

        static CropDef Tree(string seed, string name, float minutes, float regrow, Color32 leaf, Color32 fruit, int height, params (string, int, int)[] fruitYield) =>
            new CropDef { seed = seed, name = name, growMinutes = minutes, regrowMinutes = regrow, tree = true, leaf = leaf, fruit = fruit, height = height, yields = fruitYield, seedChance = 0.35f };

        public static FoodDef Get(string id) { Ensure(); return id != null && foods.TryGetValue(id, out var f) ? f : null; }
        public static IEnumerable<FoodDef> AllFood { get { Ensure(); return foods.Values; } }
        public static IReadOnlyList<CropDef> Crops { get { Ensure(); return crops; } }
        public static CropDef Crop(string seed) { Ensure(); foreach (var c in crops) if (c.seed == seed) return c; return null; }

        public static string SeedName(string id)
        {
            var c = Crop(id);
            return c == null ? null : c.tree ? c.name + " SAPLING" : c.dark ? c.name + " SPAWN" : c.name + " SEEDS";
        }
    }
}
