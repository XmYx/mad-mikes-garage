using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Rain into dug holes (<see cref="Spills"/>): the low points of terraformed ground near the player, one per
    /// 2 × 2 m block that was dug at least 25 cm, each catching the rain of its block. The soil drinks what it can
    /// (clay slowly, sand fast, wet ground little); the rest stands — a pit in clay becomes a puddle pond.</summary>
    public partial class DeformableTerrain
    {
        public const int PitBlock = 8;                   // cells per side of a catchment block (2 m)
        public const float PitDepth = 0.25f;

        /// <summary>Low points of dug ground within <paramref name="reach"/> (xyz = the cell centre on the ground,
        /// w = catchment m²).</summary>
        public void RainPits(Vector3 focus, float reach, List<Vector4> into)
        {
            into.Clear();
            float r2 = (reach + ChunkWorld) * (reach + ChunkWorld);
            foreach (var kv in chunks)
            {
                var ch = kv.Value;
                if (!ch.terraformed || ch.d == null) continue;
                float cx = (ch.c.x + 0.5f) * ChunkWorld - focus.x, cz = (ch.c.y + 0.5f) * ChunkWorld - focus.z;
                if (cx * cx + cz * cz > r2) continue;
                for (int bj = 0; bj < N; bj += PitBlock)
                for (int bi = 0; bi < N; bi += PitBlock)
                {
                    int best = -1; float low = float.MaxValue;
                    for (int j = bj; j < bj + PitBlock; j++)
                    for (int i = bi; i < bi + PitBlock; i++)
                    {
                        int k = j * V + i;
                        if (ch.d[k] > -PitDepth || !float.IsNaN(ch.water[k])) continue;
                        float y = ch.h[k] + ch.d[k];
                        if (y < low) { low = y; best = k; }
                    }
                    if (best < 0) continue;
                    int bi0 = best % V, bj0 = best / V;
                    bool sink = true;                                                    // lower than its ring (inside the chunk)
                    for (int dj = -1; dj <= 1 && sink; dj++)
                    for (int di = -1; di <= 1 && sink; di++)
                    {
                        int ii = bi0 + di, jj = bj0 + dj;
                        if (ii < 0 || jj < 0 || ii >= V || jj >= V || (di == 0 && dj == 0)) continue;
                        int k = jj * V + ii;
                        if (ch.h[k] + ch.d[k] < low - 0.005f) sink = false;
                    }
                    if (!sink) continue;
                    float x = (ch.c.x * N + bi0) * Cell, z = (ch.c.y * N + bj0) * Cell;
                    into.Add(new Vector4(x, low, z, PitBlock * PitBlock * Cell * Cell));
                }
            }
        }
    }
}
