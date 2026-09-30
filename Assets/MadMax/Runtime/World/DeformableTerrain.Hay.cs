using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Hay from the ground cover (depth stage E): the scythe and the tractor's baler cut the standing grass,
    /// flowers, reeds and village crop rows of the flora (<see cref="Trample"/> flattens what was cut; it grows back at
    /// the biome's rate). Only cover that has grown past the sprout stage counts; a full-grown cell is one unit.</summary>
    public partial class DeformableTerrain
    {
        /// <summary>Hay per unit of standing cover: by hand (scythe) and with a baler (tighter pickup).</summary>
        public const float HayPerCell = 0.12f, BalerHayPerCell = 0.15f;
        static float baleCarry;
        static readonly System.Collections.Generic.List<Rect> hayBlockers = new System.Collections.Generic.List<Rect>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetHay() => baleCarry = 0f;

        /// <summary>Cut the ground cover in a disc: returns how much stood there (grown cells) and flattens it.</summary>
        public float Mow(Vector3 p, float radius) => Cut(p, radius, true);

        /// <summary>How much ground cover stands in a disc (grown cells), without cutting it.</summary>
        public float Cover(Vector3 p, float radius) => Cut(p, radius, false);

        float Cut(Vector3 p, float radius, bool cut)
        {
            if (World == null) return 0f;
            float now = DayNight.TotalDays, got = 0f, r2 = radius * radius;
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell), x1 = Mathf.FloorToInt((p.x + radius) / Cell);
            int z0 = Mathf.FloorToInt((p.z - radius) / Cell), z1 = Mathf.FloorToInt((p.z + radius) / Cell);
            hayBlockers.Clear();                                                                     // nothing grows under buildings and pieces
            var disc = new Rect(p.x - radius, p.z - radius, radius * 2f, radius * 2f);
            foreach (var b in FloraBlocker.All) if (b && b.rect.Overlaps(disc)) hayBlockers.Add(b.rect);
            for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                float dx = (ix + 0.5f) * Cell - p.x, dz = (iz + 0.5f) * Cell - p.z;
                if (dx * dx + dz * dz > r2) continue;
                bool under = false;
                foreach (var b in hayBlockers) if (b.Contains(new Vector2(p.x + dx, p.z + dz))) { under = true; break; }
                if (under) continue;
                var ch = Locate(ix, iz, out int k);
                if (ch == null) continue;
                float s = Standing(ch, k, ix, iz, now);
                if (s <= 0f) continue;
                got += s;
                if (!cut) continue;
                MarkTrampled(ch, k);
                ch.floraNext = 0f;                                                                    // show the cut swath at once
            }
            return got;
        }

        /// <summary>Standing cover of one cell (0, or 1/3..1 by growth stage): the same rules as <see cref="BuildFlora"/>.</summary>
        float Standing(Chunk ch, int k, int gi, int gj, float now)
        {
            if (ch.feature == null || ch.biome == null) return 0f;
            byte feat = ch.feature[k];
            if (feat >= 2 || (ch.pave != null && ch.pave[k] != 0) || (ch.road != null && ch.road[k] > 0.25f)) return 0f;
            float water = ch.water != null ? ch.water[k] : float.NaN;
            float y = ch.h[k] + ch.d[k];
            var biome = (Biome)ch.biome[k];
            bool sea = !float.IsNaN(water) && Mathf.Abs(water - WorldGen.SeaLevel) < 0.01f;
            bool wetFoot = !float.IsNaN(water) && y < water + Weather.LakeRise + 0.05f;
            if (wetFoot && (sea || biome == Biome.Desert || y < water + Weather.LakeRise - 0.35f)) return 0f;   // open water, the sea bed
            if (sea && ch.shore != null && ch.shore[k] > 0.15f) return 0f;                              // bare beach sand
            float gx = gi * Cell, gz = gj * Cell;
            float wet = ch.wet != null ? ch.wet[k] : 0.3f;
            bool rows = false;
            if (biome == Biome.Village)
            {
                float field = Mathf.PerlinNoise(gx * 0.02f + 40f, gz * 0.02f + 12f);
                if (field > 0.55f)
                {
                    rows = !(Mathf.Repeat(gx + (field > 0.62f ? gz : 0f), 1.5f) < 0.6f) && Hash(gi, gj) > 0.3f;
                    if (!rows) return 0f;                                                              // bare furrows
                }
            }
            bool shore = wetFoot || (ch.shore != null && ch.shore[k] > 0.3f && biome != Biome.Desert);   // reeds
            float density = feat == 1 ? 0.05f : BaseDensity(biome);
            density *= 0.45f + Mathf.PerlinNoise(gx * 0.13f + 7f, gz * 0.13f + 3f) * 1.1f;
            density *= 0.75f + wet * 0.5f;
            if (rows || shore) density = 0.9f;
            if (Hash(gi * 5 + 11, gj * 3 + 7) > density) return 0f;
            float since = ch.trampled != null ? now - ch.trampled[k] : 1e6f;
            float grow = Mathf.Clamp01(since / (RegrowDays(biome) / (0.6f + wet)));
            int stage = Mathf.Min(3, Mathf.FloorToInt(grow * 4f));
            return stage < 2 ? 0f : stage / 3f;
        }

        /// <summary>A baler working one spot of its swath: the cut goes into <paramref name="into"/> as hay.</summary>
        public bool Bale(Vector3 p, Inventory into)
        {
            if (into == null) return false;
            float got = Mow(p, 0.6f) * BalerHayPerCell;
            if (got <= 0f) return false;
            baleCarry += got;
            int n = Mathf.FloorToInt(baleCarry);
            baleCarry -= n;
            if (n > 0) into.Add(ResourceType.Hay, n);
            Fx.Smoke(new Vector3(p.x, Height(p.x, p.z) + 0.5f, p.z), Vector3.up * 0.6f, 0.35f, MadMax.Voxel.Pal.Ochre[3], 1f);
            return true;
        }
    }
}
