using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Regional prices (roadmap 15). Every settlement has a standing price level per good, deterministic from the
    /// world: villages grow food and want tools and parts, cities pay for food and water and sell tools and media, towns
    /// by an oil field sell cheap fuel, by an ore deposit cheap metal, in the nuclear zone medicine is dear. On top sits
    /// the market's pressure: selling a good floods it (the price drops), buying drains it (the price rises); it drifts
    /// back ~15 % a day, plus a small daily wobble and the season (<see cref="SeasonFactor"/>). Roadside vendors charge a
    /// little more and pay a little less.</summary>
    public static class Market
    {
        public static readonly string[] Goods = { "FUEL", "WATER", "FOOD", "MEDICINE", "ARMS", "TOOLS", "PARTS", "METALS", "BUILDING", "CLOTH", "MEDIA" };
        const int Fuel = 0, Water = 1, Food = 2, Medicine = 3, Arms = 4, Tools = 5, Parts = 6, Metals = 7, Building = 8, Cloth = 9, Media = 10;
        static readonly Dictionary<int, float[]> pressure = new Dictionary<int, float[]>();
        static int lastDay = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { pressure.Clear(); lastDay = -1; }

        /// <summary>Well-mixed seed from a few integers (System.Random's first draw follows close seeds).</summary>
        public static int Seed(int a, int b, int c = 0)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>Market good of an item id (-1: not traded as a good, e.g. scrap, the money).</summary>
        public static int GoodOf(string id)
        {
            if (id == null) return -1;
            if (id.StartsWith("res:"))
            {
                var t = (ResourceType)int.Parse(id.Substring(4));
                switch (t)
                {
                    case ResourceType.Scrap: return -1;
                    case ResourceType.Fuel: case ResourceType.Diesel: case ResourceType.Oil: case ResourceType.Ethanol: case ResourceType.CrudeOil: case ResourceType.Coolant: case ResourceType.SeedOil: return Fuel;
                    case ResourceType.Water: case ResourceType.DirtyWater: return Water;
                    case ResourceType.Iron: case ResourceType.Copper: case ResourceType.Bronze: case ResourceType.Aluminium: case ResourceType.Lead:
                    case ResourceType.IronOre: case ResourceType.CopperOre: case ResourceType.TinOre: case ResourceType.Bauxite: case ResourceType.LeadOre: case ResourceType.UraniumOre: case ResourceType.Coal: return Metals;
                    case ResourceType.Cloth: case ResourceType.Leather: case ResourceType.Hide: return Cloth;
                    case ResourceType.Gunpowder: case ResourceType.Sulfur: return Arms;
                    default: return Building;
                }
            }
            if (id.StartsWith("part:") || id.StartsWith("kit_turbo") || id.StartsWith("kit_super")) return Parts;
            if (id.StartsWith("drink_")) return Water;
            if (id.StartsWith("food_") || id.StartsWith("seed_") || id.StartsWith("crop_") || id.StartsWith("sapling_") || id.StartsWith("farm_") || id.StartsWith("bait_")) return Food;
            if (id.StartsWith("med_")) return Medicine;
            if (id.StartsWith("ammo_") || id.StartsWith("throw_") || ItemCatalog.Category(id) == ItemCategory.Weapon) return Arms;
            if (id.StartsWith("tool_") || id.StartsWith("kit_") || id.StartsWith("use_")) return Tools;
            if (id.StartsWith("cloth_")) return Cloth;
            if (id.StartsWith("book_") || id.StartsWith("vhs_") || id.StartsWith("bp_")) return Media;
            return Tools;
        }

        /// <summary>The settlement whose market a spot trades in (null: out on the road).</summary>
        public static Settlement Near(Vector3 p)
        {
            var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            if (w == null) return null;
            foreach (var st in w.settlements) if (Vector2.Distance(st.pos, new Vector2(p.x, p.z)) < st.radius + 25f) return st;
            return null;
        }

        /// <summary>Standing price level of a good in a town (0.6 .. 1.5).</summary>
        static float Base(Settlement st, int good)
        {
            var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            var r = new System.Random(Seed(st.index, good, w != null ? w.seed : 0));
            float f = 0.88f + 0.24f * (float)r.NextDouble();
            switch (st.kind)
            {
                case Biome.Village: f *= good == Food ? 0.75f : good == Water ? 0.9f : good == Tools || good == Parts ? 1.2f : good == Media ? 1.25f : 1f; break;
                case Biome.City: f *= good == Food ? 1.3f : good == Water ? 1.2f : good == Tools || good == Media ? 0.85f : good == Parts ? 0.9f : 1f; break;
                default: f *= good == Fuel ? 0.95f : good == Arms ? 1.1f : 1f; break;
            }
            if (w != null)
            {
                if (good == Fuel && w.OilAt(st.pos.x, st.pos.y) > 0.2f) f *= 0.72f;              // pumpjacks in the backyard
                if (good == Metals && w.OreAt(st.pos.x, st.pos.y, out _) > 0.1f) f *= 0.8f;
                if (good == Medicine && w.NaturalBiome(st.pos.x, st.pos.y) == Biome.Nuclear) f *= 1.4f;
                if (good == Water && w.NaturalBiome(st.pos.x, st.pos.y) == Biome.Desert) f *= 1.35f;
            }
            return f;
        }

        static float[] Pressure(int town)
        {
            Decay();
            if (!pressure.TryGetValue(town, out var p)) pressure[town] = p = new float[Goods.Length];
            return p;
        }

        static void Decay()
        {
            int day = DayNight.Day;
            if (day == lastDay) return;
            int d = lastDay < 0 ? 0 : Mathf.Max(0, day - lastDay);
            lastDay = day;
            if (d == 0) return;
            float k = Mathf.Pow(0.85f, d);
            foreach (var p in pressure.Values) for (int i = 0; i < p.Length; i++) p[i] *= k;
        }

        /// <summary>Price multiplier for a good at a settlement today (null: roadside, a flat markup).</summary>
        public static float Factor(Settlement st, string id)
        {
            int g = GoodOf(id);
            if (g < 0) return 1f;
            if (st == null) return 1.1f;
            var r = new System.Random(Seed(st.index, g * 7717 + 1, DayNight.Day));
            float wobble = 0.94f + 0.12f * (float)r.NextDouble();
            return Mathf.Clamp(Base(st, g) * (1f + Pressure(st.index)[g]) * wobble * SeasonFactor(g, st.kind), 0.4f, 2.2f);
        }

        /// <summary>The season's pull on a good: food is cheap at the autumn harvest (cheapest where it grows) and dear
        /// through winter and the hungry spring, winter wants fuel, cloth and medicine, summer wants water.</summary>
        public static float SeasonFactor(int good, Biome kind)
        {
            int season = Weather.Season;                        // 0 summer, 1 autumn, 2 winter, 3 spring
            switch (good)
            {
                case Food:
                    float f = season == 1 ? 0.8f : season == 2 ? 1.3f : season == 3 ? 1.15f : 0.95f;
                    return kind == Biome.Village ? 1f + (f - 1f) * 1.3f : f;       // farm towns swing hardest
                case Fuel: return season == 2 ? 1.18f : season == 0 ? 0.95f : 1f;
                case Cloth: return season == 2 ? 1.2f : season == 0 ? 0.9f : 1f;
                case Medicine: return season == 2 ? 1.12f : 1f;
                case Water: return season == 0 ? 1.18f : season == 2 ? 0.9f : 1f;
                case Building: return season == 3 ? 1.1f : season == 2 ? 0.92f : 1f;   // spring is building season
                default: return 1f;
            }
        }

        /// <summary>What the season does to prices, one line (journal, radio).</summary>
        public static string SeasonNews(int season) =>
            season == 1 ? "HARVEST IS IN: FOOD IS CHEAP, CHEAPEST IN THE FARM VILLAGES"
            : season == 2 ? "WINTER PRICES: FOOD, FUEL, CLOTH AND MEDICINE GO DEAR"
            : season == 3 ? "HUNGRY SPRING: FOOD STAYS DEAR, TIMBER AND BRICK ARE IN DEMAND"
            : "SUMMER: WATER FETCHES A PRICE, WARM CLOTHES SELL CHEAP";

        /// <summary>The player sold <paramref name="n"/> of a good here: the market floods.</summary>
        public static void Sold(Settlement st, string id, int n)
        {
            int g = GoodOf(id);
            if (st == null || g < 0) return;
            var p = Pressure(st.index);
            p[g] = Mathf.Max(-0.6f, p[g] - n * 0.012f * Mathf.Sqrt(Mathf.Max(1f, Trade.Value(id))));
        }

        /// <summary>The player bought: the good gets scarcer here.</summary>
        public static void Bought(Settlement st, string id, int n)
        {
            int g = GoodOf(id);
            if (st == null || g < 0) return;
            var p = Pressure(st.index);
            p[g] = Mathf.Min(0.8f, p[g] + n * 0.008f * Mathf.Sqrt(Mathf.Max(1f, Trade.Value(id))));
        }

        static readonly string[] NameA = { "RUST", "DUST", "BONE", "GAS", "IRON", "SALT", "DRY", "ASH", "TIN", "OIL", "SCRAP", "RED", "DEAD", "BLACK", "SAND", "COPPER", "LAST", "BURNT" };
        static readonly string[] NameB = { "WATER", "TOWN", "YARD", "GULCH", "HOLLOW", "CROSS", "FALLS", "PIT", "CREEK", "RIDGE", "FORD", "WELLS", "SPRINGS", "BEND", "FLATS", "CHANCE", "HOPE", "STOP" };

        /// <summary>A settlement's name (deterministic from the world).</summary>
        public static string TownName(Settlement st)
        {
            if (st == null) return "THE ROAD";
            var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            var r = new System.Random(Seed(st.index, 48271, w != null ? w.seed : 0));
            string n = NameA[r.Next(NameA.Length)] + NameB[r.Next(NameB.Length)];
            return st.kind == Biome.City ? n + " CITY" : n;
        }

        /// <summary>One line about the town's prices: its cheapest and its dearest good.</summary>
        public static string Hint(Settlement st)
        {
            if (st == null) return "ROADSIDE PRICES";
            int lo = 0, hi = 0; float flo = 9f, fhi = 0f;
            for (int g = 0; g < Goods.Length; g++)
            {
                var r = new System.Random(Seed(st.index, g * 7717 + 1, DayNight.Day));
                float f = Base(st, g) * (1f + Pressure(st.index)[g]) * (0.94f + 0.12f * (float)r.NextDouble()) * SeasonFactor(g, st.kind);
                if (f < flo) { flo = f; lo = g; }
                if (f > fhi) { fhi = f; hi = g; }
            }
            return Goods[lo] + (Goods[lo].EndsWith("S") ? " ARE" : " IS") + " CHEAP HERE, " + Goods[hi] + (Goods[hi].EndsWith("S") ? " FETCH" : " FETCHES") + " A PRICE";
        }

        // ------------------------------------------------------------------ save
        public static string Save()
        {
            Decay();
            var sb = new System.Text.StringBuilder();
            sb.Append(lastDay).Append('|');
            foreach (var kv in pressure)
            {
                sb.Append(kv.Key).Append(':');
                for (int i = 0; i < kv.Value.Length; i++) sb.Append(kv.Value[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(i < kv.Value.Length - 1 ? "," : "");
                sb.Append(';');
            }
            return sb.ToString();
        }

        public static void Load(string s)
        {
            pressure.Clear(); lastDay = -1;
            if (string.IsNullOrEmpty(s)) return;
            var head = s.Split('|');
            if (head.Length < 2) return;
            int.TryParse(head[0], out lastDay);
            foreach (var town in head[1].Split(';'))
            {
                var kv = town.Split(':');
                if (kv.Length < 2 || !int.TryParse(kv[0], out int idx)) continue;
                var vals = kv[1].Split(',');
                var p = new float[Goods.Length];
                for (int i = 0; i < p.Length && i < vals.Length; i++) float.TryParse(vals[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out p[i]);
                pressure[idx] = p;
            }
        }
    }
}
