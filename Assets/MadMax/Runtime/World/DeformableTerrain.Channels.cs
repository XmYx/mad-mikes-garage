using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Irrigation channels (roadmap 9): a trench dug out of a lake below its surface fills with the lake's
    /// water, cell by connected cell. Flooded cells take the lake level in the chunk water array, so WaterLevel /
    /// WaterDepth (pumps, garden plots, wading) and the water mesh all see them. Derived from the dug terrain: redone
    /// around every dig and when edited chunks stream in, never saved.</summary>
    public partial class DeformableTerrain
    {
        const float TrenchDepth = 0.15f;
        readonly Queue<(int x, int z, float lvl)> floodQ = new Queue<(int, int, float)>();
        readonly HashSet<Vector2Int> floodDirty = new HashSet<Vector2Int>();
        static readonly int[] FloodDX = { 1, -1, 0, 0 }, FloodDZ = { 0, 0, 1, -1 };

        bool LoadedCell(int ix, int iz, out Chunk ch, out int k)
        {
            var c = new Vector2Int(FloorDiv(ix, N), FloorDiv(iz, N));
            k = (iz - c.y * N) * V + (ix - c.x * N);
            return chunks.TryGetValue(c, out ch) && ch.h != null;
        }

        /// <summary>Surface level of a loaded cell that actually holds water (lake, flooded trench), else NaN. Dry shore
        /// cells carry the lake level too, but water does not cross them.</summary>
        float WetLevel(int ix, int iz)
        {
            if (!LoadedCell(ix, iz, out var ch, out int k)) return float.NaN;
            float lvl = ch.water[k];
            return !float.IsNaN(lvl) && ch.h[k] + ch.d[k] < lvl ? lvl : float.NaN;
        }

        static bool HasWater(Chunk ch) { foreach (var w in ch.water) if (!float.IsNaN(w)) return true; return false; }

        void PutWater(int cx, int cz, int li, int lj, float lvl)
        {
            var c = new Vector2Int(cx, cz);
            if (!chunks.TryGetValue(c, out var ch) || ch.h == null) return;
            ch.water[lj * V + li] = lvl;
            floodDirty.Add(c);
        }

        void SetWater(int ix, int iz, float lvl)
        {
            int cx = FloorDiv(ix, N), cz = FloorDiv(iz, N);
            int li = ix - cx * N, lj = iz - cz * N;
            PutWater(cx, cz, li, lj, lvl);
            if (li == 0) PutWater(cx - 1, cz, N, lj, lvl);
            if (lj == 0) PutWater(cx, cz - 1, li, N, lvl);
            if (li == 0 && lj == 0) PutWater(cx - 1, cz - 1, N, N, lvl);
        }

        /// <summary>Let lake water run into trenches dug below its surface around a point.</summary>
        public void FloodAround(Vector3 p, float radius)
        {
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell) - 1, x1 = Mathf.CeilToInt((p.x + radius) / Cell) + 1;
            int z0 = Mathf.FloorToInt((p.z - radius) / Cell) - 1, z1 = Mathf.CeilToInt((p.z + radius) / Cell) + 1;
            floodQ.Clear();
            // seeds: dry dug cells next to water
            for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                if (!LoadedCell(ix, iz, out var ch, out int k) || ch.d[k] > -TrenchDepth) continue;
                if (!float.IsNaN(ch.water[k]))
                {
                    // lake shore dug below the surface: its water mesh had no quad there yet
                    if (ch.h[k] > ch.water[k] + Weather.MaxLakeRise && ch.h[k] + ch.d[k] < ch.water[k]) floodDirty.Add(ch.c);
                    continue;
                }
                for (int n = 0; n < 4; n++)
                {
                    float lvl = WetLevel(ix + FloodDX[n], iz + FloodDZ[n]);
                    if (!float.IsNaN(lvl)) { floodQ.Enqueue((ix, iz, lvl)); break; }
                }
            }
            int budget = 4000;
            while (floodQ.Count > 0 && budget-- > 0)
            {
                var (x, z, lvl) = floodQ.Dequeue();
                if (!LoadedCell(x, z, out var ch, out int k) || !float.IsNaN(ch.water[k])) continue;
                if (ch.d[k] > -TrenchDepth || ch.h[k] + ch.d[k] > lvl - 0.05f) continue;
                SetWater(x, z, lvl);
                for (int n = 0; n < 4; n++) floodQ.Enqueue((x + FloodDX[n], z + FloodDZ[n], lvl));
            }
            if (floodDirty.Count == 0) return;
            var rebuild = new List<Vector2Int>(floodDirty);                    // (a rebuild may stream in a chunk that floods again)
            floodDirty.Clear();
            foreach (var c in rebuild)
                if (chunks.TryGetValue(c, out var ch) && ch.go)
                {
                    if (ch.waterGo) Destroy(ch.waterGo);
                    ch.waterGo = null;
                    BuildWater(ch);
                    ch.meshDirty = true;                                  // lake-bed tint under the new water
                }
        }
    }
}
