using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Items
{
    /// <summary>A catchable species (or a piece of junk on the lake bed).</summary>
    public class FishDef
    {
        public string id, name;
        public float minKg, maxKg;
        public float strength;            // 0..1: how hard it pulls on the line
        public string bait;               // favourite bait id (bites far more often on it)
        public int time;                  // 0 any time, 1 dawn / dusk, 2 night, 3 day
        public float weight;              // abundance in its lakes
        public Biome[] biomes;            // lakes of these biomes (null: any clean lake)
        public bool mutant, junk;
        public bool sea;                  // salt water only (the ocean); lake fish stay in lakes and rivers
        public float[] seasons;           // bite factor per Weather.Season (summer, autumn, winter, spring); null = all year
        public Color32 color;
    }

    /// <summary>Fishing data (roadmap 10): species per biome, toxic-lake mutants, junk; bite rates by bait, hour,
    /// weather and cold; sizes skewed small, bigger in deep water.</summary>
    public static class FishLibrary
    {
        public const string Worms = "bait_worms", Insects = "bait_insects", Meat = "bait_meat", Corn = "bait_corn";
        public static readonly string[] Baits = { Worms, Insects, Corn, Meat };

        static readonly Biome[] Temperate = { Biome.Forest, Biome.Village, Biome.Town, Biome.City };
        static readonly Biome[] WarmWater = { Biome.Desert, Biome.Tropical, Biome.Village, Biome.Town, Biome.City };
        // bite factors by season (summer, autumn, winter, spring): warm-water fish sulk in the cold, cold-water fish
        // feed hardest under the ice, the sea's shoals run through summer and autumn
        static readonly float[] Warm = { 1.5f, 1f, 0.25f, 1.1f }, Cold = { 0.6f, 1.2f, 1.6f, 1.3f }, Run = { 1.7f, 1.3f, 0.3f, 0.7f };
        static Color32 H(string hex) => MadMax.Voxel.Pal.Hex(hex);

        public static readonly FishDef[] All =
        {
            new FishDef { id = "perch", seasons = Cold, name = "PERCH", minKg = 0.1f, maxKg = 1.4f, strength = 0.25f, bait = Worms, time = 3, weight = 6f, biomes = Temperate, color = H("8a9a4a") },
            new FishDef { id = "carp", seasons = Warm, name = "CARP", minKg = 0.8f, maxKg = 14f, strength = 0.55f, bait = Corn, time = 0, weight = 4f, biomes = new[] { Biome.Forest, Biome.Village, Biome.Town, Biome.City, Biome.Tropical }, color = H("a88a4a") },
            new FishDef { id = "catfish", seasons = Warm, name = "CATFISH", minKg = 1f, maxKg = 28f, strength = 0.75f, bait = Meat, time = 2, weight = 3f, biomes = WarmWater, color = H("5a5448") },
            new FishDef { id = "trout", seasons = Cold, name = "TROUT", minKg = 0.3f, maxKg = 4.5f, strength = 0.45f, bait = Insects, time = 1, weight = 4f, biomes = new[] { Biome.Forest }, color = H("9aa8a0") },
            new FishDef { id = "pike", seasons = Cold, name = "PIKE", minKg = 1f, maxKg = 16f, strength = 0.7f, bait = Meat, time = 3, weight = 2f, biomes = new[] { Biome.Forest, Biome.Village }, color = H("6a7a3a") },
            new FishDef { id = "tilapia", seasons = Warm, name = "TILAPIA", minKg = 0.2f, maxKg = 2.6f, strength = 0.3f, bait = Insects, time = 3, weight = 5f, biomes = new[] { Biome.Tropical, Biome.Desert }, color = H("8a9098") },
            new FishDef { id = "bass", seasons = Warm, name = "BASS", minKg = 0.4f, maxKg = 5f, strength = 0.5f, bait = Insects, time = 1, weight = 3f, biomes = new[] { Biome.Desert, Biome.Town, Biome.City, Biome.Village }, color = H("5a7040") },
            // toxic lakes
            new FishDef { id = "glowcarp", name = "GLOWING CARP", minKg = 2f, maxKg = 20f, strength = 0.65f, bait = Corn, time = 0, weight = 4f, mutant = true, color = H("8aff5a") },
            new FishDef { id = "twohead", name = "TWO-HEADED CATFISH", minKg = 3f, maxKg = 35f, strength = 0.85f, bait = Meat, time = 2, weight = 2f, mutant = true, color = H("6ad04a") },
            new FishDef { id = "eyeless", name = "EYELESS PIKE", minKg = 2f, maxKg = 18f, strength = 0.8f, bait = Meat, time = 3, weight = 1.5f, mutant = true, color = H("b0e060") },
            // junk
            // ---- the sea (user additions)
            new FishDef { id = "sardine", seasons = Run, name = "SARDINE", minKg = 0.05f, maxKg = 0.2f, strength = 0.15f, bait = Insects, time = 3, weight = 8f, sea = true, color = H("a8b8c8") },
            new FishDef { id = "mackerel", seasons = Run, name = "MACKEREL", minKg = 0.3f, maxKg = 1.6f, strength = 0.4f, bait = Meat, time = 1, weight = 6f, sea = true, color = H("4a7a8a") },
            new FishDef { id = "seabass", seasons = Warm, name = "SEA BASS", minKg = 0.8f, maxKg = 8f, strength = 0.55f, bait = Meat, time = 1, weight = 3f, sea = true, color = H("8a9aa0") },
            new FishDef { id = "cod", seasons = Cold, name = "COD", minKg = 1.5f, maxKg = 25f, strength = 0.6f, bait = Meat, time = 0, weight = 3f, sea = true, biomes = new[] { Biome.Tundra, Biome.Forest, Biome.Village }, color = H("8a8a6a") },
            new FishDef { id = "snapper", seasons = Warm, name = "RED SNAPPER", minKg = 1f, maxKg = 12f, strength = 0.6f, bait = Meat, time = 3, weight = 3f, sea = true, biomes = new[] { Biome.Tropical, Biome.Desert, Biome.Town, Biome.City }, color = H("c04a3a") },
            new FishDef { id = "tuna", seasons = Run, name = "BLUEFIN TUNA", minKg = 20f, maxKg = 250f, strength = 1f, bait = Meat, time = 1, weight = 0.6f, sea = true, color = H("2a3a5a") },
            new FishDef { id = "flounder3", name = "THREE-EYED FLOUNDER", minKg = 1f, maxKg = 9f, strength = 0.45f, bait = Worms, time = 0, weight = 1.2f, sea = true, mutant = true, color = H("9ac070") },
            new FishDef { id = "angler", name = "GLOWING ANGLERFISH", minKg = 2f, maxKg = 30f, strength = 0.8f, bait = Meat, time = 2, weight = 0.8f, sea = true, mutant = true, color = H("3a4a2a") },
            new FishDef { id = "boot", name = "OLD BOOT", minKg = 0.6f, maxKg = 1.2f, strength = 0.1f, weight = 0.6f, junk = true, color = H("3a2a1a") },
            new FishDef { id = "can", name = "RUSTY CAN", minKg = 0.2f, maxKg = 0.5f, strength = 0.05f, weight = 0.6f, junk = true, color = H("80401d") },
            new FishDef { id = "lockbox", name = "LOCKBOX", minKg = 3f, maxKg = 6f, strength = 0.2f, weight = 0.12f, junk = true, color = H("4f4842") },
        };

        public static FishDef Get(string id) { foreach (var f in All) if (f.id == id) return f; return null; }

        public static string BaitName(string bait) => bait == Worms ? "WORMS" : bait == Insects ? "INSECTS" : bait == Meat ? "CUT MEAT" : bait == Corn ? "CORN DOUGH" : "NO BAIT";

        /// <summary>What lives in a lake: toxic lakes hold only mutants (and junk).</summary>
        public static void Pool(Biome biome, bool toxic, List<FishDef> into, bool sea = false)
        {
            into.Clear();
            foreach (var f in All)
            {
                if (f.junk) { into.Add(f); continue; }
                if (f.sea != sea) continue;
                if (sea && f.mutant) { if (toxic || biome == Biome.Nuclear) into.Add(f); continue; }   // fallout seas breed a few oddities among the rest
                if (f.mutant != toxic) continue;
                if (f.biomes == null || System.Array.IndexOf(f.biomes, biome) >= 0) into.Add(f);
            }
            if (!toxic && !sea && into.Count <= 3) into.Add(All[0]);           // perch get everywhere
        }

        /// <summary>Bite factor of a species in a season (0 summer .. 3 spring).</summary>
        public static float SeasonFactor(FishDef f, int season) => f.seasons == null || season < 0 ? 1f : f.seasons[season & 3];

        /// <summary>The best biters this season in a pool (for the HUD hint and the stalls), best first.</summary>
        public static string InSeason(List<FishDef> pool, int season, int max = 2)
        {
            var best = new List<FishDef>();
            foreach (var f in pool) if (!f.junk && SeasonFactor(f, season) > 1.15f) best.Add(f);
            best.Sort((a, b) => SeasonFactor(b, season).CompareTo(SeasonFactor(a, season)));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < best.Count && i < max; i++) sb.Append(i > 0 ? ", " : "").Append(best[i].name);
            return sb.ToString();
        }

        /// <summary>Relative bite rate of a species (junk only snags). <paramref name="season"/> -1 ignores the season;
        /// <paramref name="iceHole"/> = fishing through a hole in a frozen lake (cold-water fish crowd to it).</summary>
        public static float BiteRate(FishDef f, string bait, float hour, bool raining, float temp, int season = -1, bool iceHole = false)
        {
            if (f.junk) return f.weight;
            float r = f.weight * (bait == null ? 0.25f : bait == f.bait ? 1.6f : 0.7f);
            bool dawnDusk = (hour >= 5f && hour < 8.5f) || (hour >= 17f && hour < 20.5f), night = hour < 5f || hour >= 20.5f;
            r *= f.time switch { 1 => dawnDusk ? 1.8f : 0.7f, 2 => night ? 1.8f : 0.5f, 3 => night ? 0.4f : 1.2f, _ => 1f };
            if (dawnDusk) r *= 1.25f;
            if (raining) r *= 1.3f;
            float sf = SeasonFactor(f, season);
            r *= sf;
            if (temp < 3f && sf < 1f) r *= 0.5f;                              // cold water fish don't mind the cold
            if (iceHole) r *= sf > 1f ? 1.4f : 0.6f;
            return r;
        }

        /// <summary>Pick the species that bites, weighted by rate.</summary>
        public static FishDef Pick(List<FishDef> pool, string bait, float hour, bool raining, float temp, out float total, int season = -1, bool iceHole = false)
        {
            total = 0f;
            foreach (var f in pool) total += BiteRate(f, bait, hour, raining, temp, season, iceHole);
            float pick = Random.value * total;
            foreach (var f in pool) { pick -= BiteRate(f, bait, hour, raining, temp, season, iceHole); if (pick <= 0f) return f; }
            return pool.Count > 0 ? pool[pool.Count - 1] : null;
        }

        /// <summary>Weight of a caught fish: mostly small, deep water holds the big ones.</summary>
        public static float RollKg(FishDef f, float depth, float luck)
        {
            float depthF = Mathf.Clamp(depth / 1.5f, 0.5f, 1.2f);
            float t = Mathf.Pow(Random.value, 2.4f - luck) * depthF;
            return Mathf.Round(Mathf.Lerp(f.minKg, f.maxKg, Mathf.Clamp01(t)) * 100f) / 100f;
        }
    }
}
