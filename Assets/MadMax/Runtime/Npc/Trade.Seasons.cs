using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Seasons on the stall, beyond the season's own lines (<see cref="SeasonalStock"/>): fresh food thins out
    /// through winter and the hungry spring and piles up at the harvest; seed is scarce off-season. Preserved food is
    /// worth more than the produce it took (<see cref="PreserveValue"/>), and the season's prices
    /// (<see cref="Market.SeasonFactor"/>) do the rest: put the harvest up in autumn, sell it in winter.</summary>
    public static partial class Trade
    {
        /// <summary>A vendor's rolled count through the year's lean and fat seasons.</summary>
        static int SeasonAdjust(string id, int n)
        {
            if (n <= 0) return n;
            int season = Weather.Season;
            if (id.StartsWith("seed_") || id.StartsWith("sapling_")) return season == 3 ? n : n / 2;
            var f = FoodLibrary.Get(id);
            if (f == null || f.spoilMinutes <= 0f || FoodLibrary.Preserved(id)) return n;
            return Mathf.RoundToInt(n * (season == 2 ? 0.35f : season == 3 ? 0.6f : season == 1 ? 1.5f : 1f));
        }

        static readonly Dictionary<string, float> preserveValues = new Dictionary<string, float>
        {
            { "food_meat_smoked", 7f }, { "food_fish_smoked", 6f }, { "food_jerky", 6f }, { "food_sausage", 9f }, { "food_cheese_smoked", 10f },
            { "food_meat_salted", 6f }, { "food_fish_salted", 5f }, { "food_fish_dried", 6f },
            { "food_pickles", 5f }, { "food_sauerkraut", 5f }, { "food_pickled_beet", 5f }, { "food_pickled_egg", 6f }, { "food_pickled_fish", 6f },
            { "food_dried_fruit", 5f }, { "food_dried_tomato", 5f }, { "food_dried_mushroom", 5f }, { "food_dried_corn", 4f }, { "food_dried_herbs", 4f },
            { "food_jar_tomato", 7f }, { "food_jar_pumpkin", 8f }, { "food_jam", 6f }, { "food_cheese", 6f }, { "food_honey", 8f },
        };

        /// <summary>Base value of a preserved food (false for anything else): about twice the fresh produce it took.</summary>
        public static bool PreserveValue(string id, out float value) => preserveValues.TryGetValue(id, out value);
    }
}
