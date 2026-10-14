using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>The fishers' catch on the stall: food vendors of a settlement with open water within reach
    /// (<see cref="FisherTown"/>; its fisher residents sit at that shore at dawn, <see cref="Npc.Chore"/>) sell the day's
    /// fresh fish, as much as the season's bite tables give (summer runs fill the crate, winter leaves a few).</summary>
    public static partial class Trade
    {
        public const string FisherNote = "TODAY'S CATCH";
        static readonly Dictionary<int, bool> fisherTowns = new Dictionary<int, bool>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetFishers() => fisherTowns.Clear();

        /// <summary>Open water (lake, river, sea) within 260 m of the settlement's edge.</summary>
        public static bool FisherTown(Settlement st)
        {
            if (st == null) return false;
            if (fisherTowns.TryGetValue(st.index, out bool yes)) return yes;
            var t = DeformableTerrain.Instance;
            if (!t) return false;
            yes = false;
            for (int a = 0; a < 16 && !yes; a++)
            {
                var dir = new Vector2(Mathf.Cos(a * Mathf.PI / 8f), Mathf.Sin(a * Mathf.PI / 8f));
                for (float d = st.radius * 0.5f; d <= st.radius + 260f && !yes; d += 10f)
                {
                    var p = st.pos + dir * d;
                    yes = t.WaterDepthNoLoad(p.x, p.y) > 0.6f;
                }
            }
            fisherTowns[st.index] = yes;
            return yes;
        }

        /// <summary>The season's catch in fish (1 = an ordinary day).</summary>
        public static float CatchFactor(int season)
        {
            float price = Market.FishSeason("food_fish_raw", season);
            return Mathf.Clamp(1f / (price * price), 0.3f, 2.2f);
        }

        /// <summary>Adds the fishers' catch to a food vendor's list (deterministic per vendor and day).</summary>
        static void FisherStock(NpcProfile p, NpcSave s, List<Offer> list, float bargain)
        {
            if (p.kind != "food" || !FisherTown(Town)) return;
            int day = DayNight.Day;
            var r = new System.Random(p.seed * 53 + day * 389);
            int n = Mathf.RoundToInt(r.Next(2, 6) * CatchFactor(Weather.Season));
            int at = list.FindIndex(o => o.id == "food_fish_raw");
            if (at >= 0) { if (n <= 0) return; var o = list[at]; o.count += n; o.note = FisherNote; list[at] = o; return; }   // today's purchases came off the base line
            n -= s.Bought("food_fish_raw", day) - (stockRolled.TryGetValue("food_fish_raw", out int rolled) ? rolled : 0);
            if (n > 0) list.Add(new Offer { id = "food_fish_raw", count = n, price = BuyPrice("food_fish_raw", bargain), note = FisherNote });
        }
    }
}
