using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Store the harvest (Seasons): foods put up for the winter from the garden and the hunt — dried on the
    /// rack, fermented in the crock, jarred on the stove, smoked in the smokehouse. None of them rot. <see cref="Preserved"/>
    /// lists every keeping food (also the older smoked, salted, tinned ones) for trade values, stock and chores.</summary>
    public static partial class FoodLibrary
    {
        static IEnumerable<FoodDef> SeasonFoods()
        {
            // drying rack: sun and wind, no fuel
            yield return F("food_dried_fruit", "DRIED FRUIT", 16, -4, 0, H("8a3a24"), 0f, 1f);
            yield return F("food_dried_tomato", "SUN-DRIED TOMATOES", 12, -3, 0, H("8a2418"));
            yield return F("food_dried_mushroom", "DRIED MUSHROOMS", 8, -2, 0, H("8a7a5a"));
            yield return F("food_dried_corn", "DRIED CORN", 14, -3, 0, H("c8a030"));
            yield return F("food_fish_dried", "STOCKFISH", 24, -6, 0, H("b8a888"), 0f, 2f);
            yield return F("food_dried_herbs", "DRIED HERBS", 2, 0, 0, H("5a6a30"), 0f, 5f);
            // pickling crock: salt brine, weeks under a stone
            yield return F("food_sauerkraut", "SAUERKRAUT", 18, 2, 0, H("c8c070"), 0f, 2f);
            yield return F("food_pickled_beet", "PICKLED BEETS", 14, 4, 0, H("7a1c3a"));
            yield return F("food_pickled_egg", "PICKLED EGGS", 20, 0, 0, H("e0d0a0"), 0f, 1f);
            yield return F("food_pickled_fish", "PICKLED FISH", 24, 2, 0, H("c0c8c0"), 0f, 2f);
            // stove: home jars
            yield return F("food_jar_tomato", "JARRED TOMATOES", 20, 12, 0, H("b02818"), 0f, 2f);
            yield return F("food_jar_pumpkin", "JARRED PUMPKIN", 28, 6, 0, H("d07820"), 0f, 2f);
            // smokehouse
            yield return F("food_sausage", "SMOKED SAUSAGE", 36, -4, 0, H("7a2a1a"), 0f, 3f);
            yield return F("food_cheese_smoked", "SMOKED CHEESE", 28, -3, 0, H("b07a30"), 0f, 2f);
        }

        static HashSet<string> preserved;

        /// <summary>Foods that keep through the winter: smoked, dried, salted, pickled, jarred, tinned, sealed.</summary>
        public static bool Preserved(string id)
        {
            if (preserved == null)
            {
                preserved = new HashSet<string>
                {
                    "food_meat_smoked", "food_fish_smoked", "food_jerky", "food_meat_salted", "food_fish_salted", "food_pickles", "food_jam",
                    "food_cheese", "food_honey", "food_can", "food_ration", "food_can_stew", "food_can_meat", "food_can_fish", "food_can_veg", "food_can_fruit",
                };
                foreach (var f in SeasonFoods()) preserved.Add(f.id);
            }
            return id != null && preserved.Contains(id);
        }

        /// <summary>Field and garden produce brought in at the harvest (village chores, autumn stock).</summary>
        public static bool Harvest(string id) => id == "food_corn" || id == "food_potato" || id == "food_pumpkin" || id == "food_cabbage" || id == "food_carrot"
            || id == "food_beet" || id == "food_tomato" || id == "food_apple" || id == "crop_wheat";
    }
}
