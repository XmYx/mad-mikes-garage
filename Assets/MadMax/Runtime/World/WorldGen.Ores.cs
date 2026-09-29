using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Ore deposits (roadmap 7): the world is cut into 140 m cells and most carry one deposit — a kind (by
    /// weight, biome-bound: uranium only in nuclear zones, sulfur in nuclear and desert ground, more coal under forest)
    /// centred at a jittered point, 22–40 m across. Strength 0..1 falls off from the centre with a little noise. Rich
    /// cores show as outcrops (BiomeProps), deep digs over a deposit bring ore up (DeformableTerrain.SoilAt), and the
    /// metal detector and geiger read it.</summary>
    public partial class WorldGen
    {
        const float OreCell = 140f;
        static readonly (ResourceType ore, float weight)[] OreKinds =
        {
            (ResourceType.IronOre, 30f), (ResourceType.CopperOre, 18f), (ResourceType.Coal, 18f), (ResourceType.TinOre, 10f),
            (ResourceType.Bauxite, 10f), (ResourceType.LeadOre, 8f), (ResourceType.Sulfur, 4f), (ResourceType.UraniumOre, 2f),
        };

        public struct Deposit { public bool valid; public ResourceType ore; public Vector2 center; public float radius; }

        /// <summary>The deposit of an ore cell (deterministic from the seed), or none.</summary>
        public Deposit DepositIn(int cx, int cz)
        {
            var r = new System.Random(Mix(cx, cz, seed + 7777));
            if (r.NextDouble() > 0.62) return default;
            var center = new Vector2((cx + 0.2f + 0.6f * (float)r.NextDouble()) * OreCell, (cz + 0.2f + 0.6f * (float)r.NextDouble()) * OreCell);
            if (SettlementAt(center.x, center.y) != null) return default;
            float total = 0f; foreach (var k in OreKinds) total += k.weight;
            double pick = r.NextDouble() * total;
            var ore = ResourceType.IronOre;
            foreach (var k in OreKinds) { pick -= k.weight; if (pick <= 0) { ore = k.ore; break; } }
            var biome = NaturalBiome(center.x, center.y);
            if (ore == ResourceType.UraniumOre && biome != Biome.Nuclear) ore = ResourceType.LeadOre;
            if (ore == ResourceType.Sulfur && biome != Biome.Nuclear && biome != Biome.Desert) ore = ResourceType.IronOre;
            if (biome == Biome.Forest && ore == ResourceType.IronOre && r.NextDouble() < 0.4) ore = ResourceType.Coal;
            if (biome == Biome.Nuclear && r.NextDouble() < 0.3) ore = ResourceType.UraniumOre;
            return new Deposit { valid = true, ore = ore, center = center, radius = 22f + 18f * (float)r.NextDouble() };
        }

        /// <summary>Depth of the water table (m) under a point: shallow under forest and jungle and near lakes, deep
        /// under the desert. Wells pump faster where it is shallow.</summary>
        public float WaterTable(float x, float z)
        {
            float depth = NaturalBiome(x, z) switch { Biome.Forest => 3f, Biome.Tropical => 2f, Biome.Nuclear => 10f, _ => 16f };
            var lake = LakeAt(x, z, out float t);
            if (lake != null) depth = Mathf.Min(depth, 1f + Mathf.Max(0f, t - 1f) * 12f);
            return depth * (0.8f + 0.4f * Mathf.PerlinNoise(x * 0.01f + 3f, z * 0.01f - 5f));
        }

        /// <summary>Ore strength (0..1) at a point and the deposit's kind.</summary>
        public float OreAt(float x, float z, out ResourceType ore)
        {
            ore = ResourceType.None;
            float best = 0f;
            int cx = Mathf.FloorToInt(x / OreCell), cz = Mathf.FloorToInt(z / OreCell);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var d = DepositIn(cx + dx, cz + dz);
                if (!d.valid) continue;
                float s = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), d.center) / d.radius);
                if (s > best) { best = s; ore = d.ore; }
            }
            if (best > 0f) best *= 0.8f + 0.2f * Mathf.PerlinNoise(x * 0.08f + 31f, z * 0.08f - 7f);
            return best;
        }
    }
}
