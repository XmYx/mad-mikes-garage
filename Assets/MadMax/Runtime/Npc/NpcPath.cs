using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>On-foot pathfinding for people (gaps list: NPCs cannot path). When the straight line to a goal is blocked
    /// (<see cref="Clear"/>), <see cref="Find"/> runs A* on a 0.5 m grid laid over the ground around start and goal
    /// (at most 48 m across): a cell is open when nothing solid stands at knee-to-head height (buildings, fences, built
    /// pieces, vehicles, props), the water is shallow and the step from its neighbour is under 0.6 m. Unlocked doors are
    /// portals (the walker opens them), locked ones walls. Budgeted per frame; the result is string-pulled into a few
    /// waypoints.</summary>
    public static class NpcPath
    {
        const float Cell = 0.5f;
        const int MaxSide = 96, MaxExpand = 5000, PerFrame = 3;
        static readonly Collider[] overlap = new Collider[16];
        static readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly int Mask = ~((1 << Layers.Terrain) | (1 << Layers.MachineTool));
        static int frame, used;

        // per search scratch (main thread only)
        static readonly Dictionary<int, float> g = new Dictionary<int, float>();
        static readonly Dictionary<int, int> came = new Dictionary<int, int>();
        static readonly Dictionary<int, sbyte> open = new Dictionary<int, sbyte>();   // 1 free, 0 blocked
        static readonly List<(float f, int k)> heap = new List<(float, int)>();
        static readonly Dictionary<int, float> ground = new Dictionary<int, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { frame = used = 0; }

        /// <summary>Something that stops a walker, not the walker itself or other people and animals.</summary>
        static bool Solid(Collider c, Transform self)
        {
            if (!c || c.isTrigger || c is CharacterController) return false;
            if (self && c.transform.IsChildOf(self)) return false;
            if (c.GetComponentInParent<Npc>() || c.GetComponentInParent<MadMax.Game.PlayerCharacter>() || c.GetComponentInParent<MadMax.Animals.Animal>()) return false;
            var door = c.GetComponentInParent<MadMax.Building.Door>();
            if (door && !door.locked) return false;                                                  // a portal: walked through (opened)
            return true;
        }

        /// <summary>The straight walk from <paramref name="from"/> to <paramref name="to"/> is free of solid things.</summary>
        public static bool Clear(Vector3 from, Vector3 to, Transform self)
        {
            var d = to - from; d.y = 0f;
            float dist = Mathf.Min(d.magnitude, 30f);
            if (dist < 0.5f) return true;
            int n = Physics.SphereCastNonAlloc(from + Vector3.up * 0.9f, 0.28f, d / d.magnitude, hits, dist - 0.3f, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Solid(hits[i].collider, self)) return false;
            return true;
        }

        /// <summary>A walkable route (waypoints, excluding the start). Far goals are approached through the grid window's
        /// nearest point. False when out of budget this frame or no route.</summary>
        public static bool Find(Vector3 from, Vector3 to, List<Vector3> path, Transform self)
        {
            path.Clear();
            if (Time.frameCount != frame) { frame = Time.frameCount; used = 0; }
            if (used >= PerFrame) return false;
            used++;
            var t = DeformableTerrain.Instance;
            if (!t) return false;
            var flat = to - from; flat.y = 0f;
            if (flat.magnitude > 40f) to = from + flat.normalized * 40f;                            // leg by leg
            float minX = Mathf.Min(from.x, to.x) - 8f, minZ = Mathf.Min(from.z, to.z) - 8f;
            int w = Mathf.Min(MaxSide, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + 8f - minX) / Cell));
            int h = Mathf.Min(MaxSide, Mathf.CeilToInt((Mathf.Max(from.z, to.z) + 8f - minZ) / Cell));
            int Key(int x, int z) => z * w + x;
            Vector3 Centre(int x, int z) => new Vector3(minX + (x + 0.5f) * Cell, 0f, minZ + (z + 0.5f) * Cell);
            float Ground(int k, int x, int z) { if (!ground.TryGetValue(k, out var y)) { var c = Centre(x, z); ground[k] = y = t.HeightNoLoad(c.x, c.z); } return y; }
            bool Free(int x, int z)
            {
                int k = Key(x, z);
                if (open.TryGetValue(k, out var o)) return o == 1;
                var c = Centre(x, z);
                c.y = Ground(k, x, z);
                bool free = t.WaterDepthNoLoad(c.x, c.z) < 0.7f;
                if (free)
                {
                    int n = Physics.OverlapBoxNonAlloc(c + Vector3.up * 1.1f, new Vector3(0.22f, 0.55f, 0.22f), overlap, Quaternion.identity, Mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n && free; i++) if (Solid(overlap[i], self)) free = false;
                }
                open[k] = (sbyte)(free ? 1 : 0);
                return free;
            }
            int sx = Mathf.Clamp(Mathf.FloorToInt((from.x - minX) / Cell), 0, w - 1), sz = Mathf.Clamp(Mathf.FloorToInt((from.z - minZ) / Cell), 0, h - 1);
            int tx = Mathf.Clamp(Mathf.FloorToInt((to.x - minX) / Cell), 0, w - 1), tz = Mathf.Clamp(Mathf.FloorToInt((to.z - minZ) / Cell), 0, h - 1);
            g.Clear(); came.Clear(); open.Clear(); heap.Clear(); ground.Clear();
            int start = Key(sx, sz), goal = Key(tx, tz);
            open[start] = 1;                                                                        // standing there already
            g[start] = 0f;
            Push(0f, start);
            int bestK = start; float bestH = float.MaxValue;
            int expanded = 0;
            while (heap.Count > 0 && expanded++ < MaxExpand)
            {
                int k = Pop();
                if (k == goal) { bestK = goal; break; }
                int x = k % w, z = k / w;
                float hk = Mathf.Abs(x - tx) + Mathf.Abs(z - tz);
                if (hk < bestH) { bestH = hk; bestK = k; }
                float gy = Ground(k, x, z), gk = g[k];
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h || !Free(nx, nz)) continue;
                    if (dx != 0 && dz != 0 && (!Free(x + dx, z) || !Free(x, z + dz))) continue;     // no corner cutting
                    int nk = Key(nx, nz);
                    if (Mathf.Abs(Ground(nk, nx, nz) - gy) > 0.6f) continue;                         // a step too high
                    float cost = gk + (dx != 0 && dz != 0 ? 1.414f : 1f);
                    if (g.TryGetValue(nk, out var old) && old <= cost) continue;
                    g[nk] = cost; came[nk] = k;
                    float octile = Mathf.Max(Mathf.Abs(nx - tx), Mathf.Abs(nz - tz)) + 0.414f * Mathf.Min(Mathf.Abs(nx - tx), Mathf.Abs(nz - tz));
                    Push(cost + octile, nk);
                }
            }
            if (bestK == start) return false;
            // walk back, then keep only the corners
            var cells = new List<Vector3>();
            for (int k = bestK; k != start; k = came[k]) { var c = Centre(k % w, k / w); c.y = Ground(k, k % w, k / w); cells.Add(c); }
            cells.Reverse();
            var prev = from;
            for (int i = 0; i < cells.Count; i++)
            {
                bool last = i == cells.Count - 1;
                if (!last && Clear(prev, cells[i + 1], self)) continue;                              // can skip this corner
                path.Add(cells[i]);
                prev = cells[i];
            }
            if (bestK == goal) path.Add(new Vector3(to.x, path.Count > 0 ? path[path.Count - 1].y : to.y, to.z));
            return path.Count > 0;
        }

        static void Push(float f, int k)
        {
            heap.Add((f, k));
            int i = heap.Count - 1;
            while (i > 0) { int p = (i - 1) / 2; if (heap[p].f <= heap[i].f) break; (heap[p], heap[i]) = (heap[i], heap[p]); i = p; }
        }

        static int Pop()
        {
            var top = heap[0].k;
            heap[0] = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < heap.Count && heap[l].f < heap[m].f) m = l;
                if (r < heap.Count && heap[r].f < heap[m].f) m = r;
                if (m == i) break;
                (heap[m], heap[i]) = (heap[i], heap[m]); i = m;
            }
            return top;
        }
    }
}
