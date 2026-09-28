using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public struct RoadHit
    {
        public float dist, height, along, width;
        public bool paved;
        public int road;
    }

    /// <summary>Towns joined by a minimum spanning tree of paved highways plus extra dirt tracks.
    /// Roads wander (midpoint displacement), are smoothed, and carry their own graded height profile.</summary>
    public class RoadNetwork
    {
        public class Road
        {
            public readonly List<Vector3> points = new List<Vector3>();
            public float width;
            public bool paved;
        }

        struct Seg
        {
            public Vector2 a, b;
            public float ha, hb, la, lb;
            public int road;
        }

        public const float Reach = 18f; // how far beyond the road edge the query reports (shoulder blending)
        const float GridCell = 64f;

        public readonly List<Vector2> towns = new List<Vector2>();
        public readonly List<Road> roads = new List<Road>();
        readonly List<Seg> segs = new List<Seg>();
        readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();

        public RoadNetwork(WorldGen world, System.Random rnd, int townCount = 14)
        {
            towns.Add(Vector2.zero);
            float range = world.halfSize - 140f;
            for (int tries = 0; towns.Count < townCount && tries < 4000; tries++)
            {
                var p = new Vector2(((float)rnd.NextDouble() * 2 - 1) * range, ((float)rnd.NextDouble() * 2 - 1) * range);
                bool ok = true;
                foreach (var t in towns) if ((t - p).sqrMagnitude < 260f * 260f) { ok = false; break; }
                if (ok) towns.Add(p);
            }

            // Prim MST -> highways
            var edges = new HashSet<(int, int)>();
            var inTree = new List<int> { 0 };
            var outTree = new List<int>();
            for (int i = 1; i < towns.Count; i++) outTree.Add(i);
            while (outTree.Count > 0)
            {
                float best = float.MaxValue; int bi = 0, bo = 0;
                foreach (int i in inTree)
                foreach (int o in outTree)
                {
                    float d = (towns[i] - towns[o]).sqrMagnitude;
                    if (d < best) { best = d; bi = i; bo = o; }
                }
                edges.Add((Mathf.Min(bi, bo), Mathf.Max(bi, bo)));
                Build(world, rnd, towns[bi], towns[bo], true);
                inTree.Add(bo); outTree.Remove(bo);
            }
            // extra loops -> dirt tracks
            for (int i = 0; i < towns.Count; i++)
            {
                int bj = -1; float best = float.MaxValue;
                for (int j = 0; j < towns.Count; j++)
                {
                    if (i == j || edges.Contains((Mathf.Min(i, j), Mathf.Max(i, j)))) continue;
                    float d = (towns[i] - towns[j]).sqrMagnitude;
                    if (d < best) { best = d; bj = j; }
                }
                if (bj >= 0 && rnd.NextDouble() < 0.5)
                {
                    edges.Add((Mathf.Min(i, bj), Mathf.Max(i, bj)));
                    Build(world, rnd, towns[i], towns[bj], false);
                }
            }
        }

        void Build(WorldGen world, System.Random rnd, Vector2 a, Vector2 b, bool paved)
        {
            var pts = new List<Vector2> { a, b };
            float amp = 0.28f;
            for (int level = 0; level < 5; level++, amp *= 0.6f)
            {
                var next = new List<Vector2>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p = pts[i]; var q = pts[i + 1];
                    var dir = q - p;
                    var perp = new Vector2(-dir.y, dir.x);
                    next.Add(p);
                    next.Add((p + q) * 0.5f + perp * ((float)rnd.NextDouble() - 0.5f) * amp);
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }
            for (int it = 0; it < 3; it++) // Chaikin smoothing, endpoints kept
            {
                var next = new List<Vector2> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    next.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.75f));
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }

            var h = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) h[i] = world.BaseHeight(pts[i].x, pts[i].y);
            for (int pass = 0; pass < 3; pass++) // grade the road
            {
                var s = new float[h.Length];
                for (int i = 0; i < h.Length; i++)
                {
                    float sum = 0; int n = 0;
                    for (int k = -4; k <= 4; k++) { int j = Mathf.Clamp(i + k, 0, h.Length - 1); sum += h[j]; n++; }
                    s[i] = sum / n;
                }
                h = s;
            }

            var road = new Road { width = paved ? 8f : 5f, paved = paved };
            int id = roads.Count;
            roads.Add(road);
            float along = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                road.points.Add(new Vector3(pts[i].x, h[i], pts[i].y));
                if (i == 0) continue;
                float len = Vector2.Distance(pts[i - 1], pts[i]);
                var seg = new Seg { a = pts[i - 1], b = pts[i], ha = h[i - 1], hb = h[i], la = along, lb = along + len, road = id };
                along += len;
                int si = segs.Count;
                segs.Add(seg);
                float pad = road.width * 0.5f + Reach;
                var min = Vector2.Min(seg.a, seg.b) - Vector2.one * pad;
                var max = Vector2.Max(seg.a, seg.b) + Vector2.one * pad;
                for (int gx = Mathf.FloorToInt(min.x / GridCell); gx <= Mathf.FloorToInt(max.x / GridCell); gx++)
                for (int gz = Mathf.FloorToInt(min.y / GridCell); gz <= Mathf.FloorToInt(max.y / GridCell); gz++)
                {
                    var key = new Vector2Int(gx, gz);
                    if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                    list.Add(si);
                }
            }
        }

        public bool Query(float x, float z, out RoadHit hit)
        {
            hit = default;
            if (!grid.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / GridCell), Mathf.FloorToInt(z / GridCell)), out var list)) return false;
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            bool found = false;
            foreach (int si in list)
            {
                var s = segs[si];
                var ab = s.b - s.a;
                float t = Mathf.Clamp01(Vector2.Dot(p - s.a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = Vector2.Distance(p, s.a + ab * t);
                var r = roads[s.road];
                if (d > r.width * 0.5f + Reach) continue;
                // prefer paved roads where they overlap at junctions
                float score = d - (r.paved ? 1.5f : 0f);
                if (score >= best) continue;
                best = score; found = true;
                hit = new RoadHit { dist = d, height = Mathf.Lerp(s.ha, s.hb, t), along = Mathf.Lerp(s.la, s.lb, t), width = r.width, paved = r.paved };
            }
            return found;
        }

        /// <summary>Closest segment of every road within reach (one hit per road), unsorted.</summary>
        public void QueryAll(float x, float z, List<RoadHit> hits)
        {
            hits.Clear();
            if (!grid.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / GridCell), Mathf.FloorToInt(z / GridCell)), out var list)) return;
            var p = new Vector2(x, z);
            foreach (int si in list)
            {
                var s = segs[si];
                var r = roads[s.road];
                var ab = s.b - s.a;
                float t = Mathf.Clamp01(Vector2.Dot(p - s.a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = Vector2.Distance(p, s.a + ab * t);
                if (d > r.width * 0.5f + Reach) continue;
                var hit = new RoadHit { dist = d, height = Mathf.Lerp(s.ha, s.hb, t), along = Mathf.Lerp(s.la, s.lb, t), width = r.width, paved = r.paved, road = s.road };
                int existing = -1;
                for (int h = 0; h < hits.Count; h++) if (hits[h].road == s.road) { existing = h; break; }
                if (existing < 0) hits.Add(hit);
                else if (d < hits[existing].dist) hits[existing] = hit;
            }
        }

        /// <summary>Spawn on the first highway a little way out of the origin town.</summary>
        public void SpawnPoint(out Vector3 position, out Vector3 direction, int index = 10)
        {
            var road = roads[0];
            var pts = road.points;
            bool reversed = (new Vector2(pts[0].x, pts[0].z)).sqrMagnitude > 1f;
            int i = reversed ? pts.Count - 1 - index : index;
            int j = reversed ? i - 1 : i + 1;
            position = pts[i];
            direction = (pts[j] - pts[i]); direction.y = 0; direction.Normalize();
        }
    }
}
