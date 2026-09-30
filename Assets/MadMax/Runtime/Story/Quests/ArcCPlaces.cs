using System.Collections.Generic;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    public static partial class StoryAnchors
    {
        /// <summary>Arc C (THE PRICE OF PASSAGE) geography, shared by C1-C5 and bound once per world: the market (the
        /// first town), the village nearest it, the public road between their edges, the convoy's destination (the next
        /// settlement out) and the road to it. Routes run on the generated road network only (a road the player builds
        /// never moves a place), so every place is deterministic from the seed; each lookup has a fallback so a seed never
        /// loses a chapter.</summary>
        static class ArcC
        {
            public static WorldGen world;
            public static Settlement town, village, dest;
            public static Vector3 market, villageAt;                                  // settlement centres
            /// <summary>The public road from the market's edge to the village's edge.</summary>
            public static readonly List<Vector3> road = new List<Vector3>();
            /// <summary>The convoy's road from the market's edge to the destination's edge.</summary>
            public static readonly List<Vector3> run = new List<Vector3>();
            /// <summary>Side of the public road (+1 right of market→village) the warden's short track lies on.</summary>
            public static float trackSide = 1f;

            static WorldGen graphWorld;
            static Vector3[] nodes;
            static List<int>[] links;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void ResetStatics() { world = null; town = village = dest = null; road.Clear(); run.Clear(); graphWorld = null; nodes = null; links = null; }

            public static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

            public static void Put(WorldGen w, string key, Vector3 p, float face)
            {
                p.y = w.Sample(p.x, p.z).height;
                at[key] = p; yaw[key] = face;
            }

            /// <summary>No bound place (other than <paramref name="mine"/>) within <paramref name="d"/> m.</summary>
            public static bool Free(Vector3 p, float d, params string[] mine)
            {
                foreach (var kv in at)
                {
                    if (Flat(kv.Value, p) >= d) continue;
                    if (System.Array.IndexOf(mine, kv.Key) < 0) return false;
                }
                return true;
            }

            /// <summary>Open ground off the road: no water, site, river or settlement (unless <paramref name="inTown"/>).</summary>
            public static bool OffRoad(WorldGen w, Vector3 p, float road, bool inTown = false)
            {
                var s = w.Sample(p.x, p.z);
                return s.roadDist >= road && float.IsNaN(s.water) && s.feature == 0 && w.SiteAt(p.x, p.z) == null && !w.RiverAt(p.x, p.z, out _, out _, out _)
                       && (inTown || (w.SettlementAt(p.x, p.z) == null && w.YardWeight(p.x, p.z) <= 0f));
            }

            public static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

            // ------------------------------------------------------------------ polylines
            public static float Length(List<Vector3> pts)
            {
                float l = 0f;
                for (int i = 1; i < pts.Count; i++) l += Flat(pts[i], pts[i - 1]);
                return l;
            }

            /// <summary>The point <paramref name="d"/> m along a polyline (clamped) and the heading there.</summary>
            public static Vector3 At(List<Vector3> pts, float d, out Vector3 dir)
            {
                dir = Vector3.forward;
                if (pts.Count == 0) return Vector3.zero;
                if (pts.Count == 1) return pts[0];
                for (int i = 1; i < pts.Count; i++)
                {
                    var a = pts[i - 1]; var b = pts[i];
                    float l = Flat(a, b);
                    var seg = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    if (l > 0.01f) dir = seg / l;
                    if (d <= l || i == pts.Count - 1) return Vector3.Lerp(a, b, l > 0.01f ? Mathf.Clamp01(d / l) : 0f);
                    d -= l;
                }
                return pts[pts.Count - 1];
            }

            /// <summary>Flat distance from a point to a polyline.</summary>
            public static float Distance(List<Vector3> pts, Vector3 p)
            {
                float best = float.MaxValue;
                var q = new Vector2(p.x, p.z);
                for (int i = 1; i < pts.Count; i++)
                {
                    var a = new Vector2(pts[i - 1].x, pts[i - 1].z); var ab = new Vector2(pts[i].x, pts[i].z) - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    best = Mathf.Min(best, (a + ab * t - q).magnitude);
                }
                return best;
            }

            /// <summary>A spot beside a polyline about <paramref name="d"/> m along it, <paramref name="off"/> m to either
            /// side (the first that is open, level-ish and passes <paramref name="ok"/>), facing the road. Walks outwards
            /// along the line; the last resort is the plain offset.</summary>
            public static Vector3 Beside(WorldGen w, List<Vector3> pts, float d, float off, System.Func<Vector3, bool> ok, out float face, float preferSide = 1f)
            {
                float len = Length(pts);
                foreach (float step in new[] { 0f, 8f, -8f, 16f, -16f, 26f, -26f, 40f, -40f, 60f, -60f, 85f, -85f, 120f, -120f })
                {
                    float dd = Mathf.Clamp(d + step, 0f, len);
                    var q = At(pts, dd, out var dir);
                    var across = new Vector3(dir.z, 0f, -dir.x);
                    foreach (float o in new[] { off * preferSide, -off * preferSide, (off + 5f) * preferSide, -(off + 5f) * preferSide })
                    {
                        var p = q + across * o;
                        if (!OffRoad(w, p, Mathf.Min(5f, off * 0.6f)) || !Level(w, p, 3.5f, 1.1f) || (ok != null && !ok(p))) continue;
                        face = Yaw(q - p);
                        return p;
                    }
                }
                var q0 = At(pts, Mathf.Clamp(d, 0f, len), out var d0);
                var p0 = q0 + new Vector3(d0.z, 0f, -d0.x) * off * preferSide;
                face = Yaw(q0 - p0);
                return p0;
            }

            // ------------------------------------------------------------------ the generated road graph
            static void Graph(WorldGen w)
            {
                if (graphWorld == w && nodes != null) return;
                graphWorld = w;
                var pts = new List<Vector3>(); var adj = new List<List<int>>();
                foreach (var r in w.roads.roads)
                {
                    int first = pts.Count;
                    for (int i = 0; i < r.points.Count; i++)
                    {
                        pts.Add(r.points[i]); adj.Add(new List<int>());
                        if (i > 0) { adj[first + i].Add(first + i - 1); adj[first + i - 1].Add(first + i); }
                    }
                }
                const float Join = 14f;
                var grid = new Dictionary<Vector2Int, List<int>>();
                Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Join), Mathf.FloorToInt(p.z / Join));
                for (int i = 0; i < pts.Count; i++) { var c = Cell(pts[i]); if (!grid.TryGetValue(c, out var l)) grid[c] = l = new List<int>(); l.Add(i); }
                for (int i = 0; i < pts.Count; i++)
                {
                    var c = Cell(pts[i]);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!grid.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out var l)) continue;
                        foreach (int j in l) if (j > i && !adj[i].Contains(j) && Flat(pts[i], pts[j]) < Join) { adj[i].Add(j); adj[j].Add(i); }
                    }
                }
                nodes = pts.ToArray(); links = adj.ToArray();
            }

            /// <summary>Shortest route along the generated roads (A*), ends included; the straight line when unconnected.</summary>
            public static bool RoadPath(WorldGen w, Vector3 from, Vector3 to, List<Vector3> into)
            {
                into.Clear();
                Graph(w);
                if (nodes.Length == 0) { into.Add(from); into.Add(to); return false; }
                int a = -1, b = -1; float ba = float.MaxValue, bb = float.MaxValue;
                for (int i = 0; i < nodes.Length; i++)
                {
                    float da = Flat(nodes[i], from), db = Flat(nodes[i], to);
                    if (da < ba) { ba = da; a = i; }
                    if (db < bb) { bb = db; b = i; }
                }
                var g = new Dictionary<int, float> { [a] = 0f };
                var came = new Dictionary<int, int>();
                var open = new List<(float f, int n)> { (Flat(nodes[a], nodes[b]), a) };
                var closed = new HashSet<int>();
                while (open.Count > 0)
                {
                    int bi = 0; for (int i = 1; i < open.Count; i++) if (open[i].f < open[bi].f) bi = i;
                    int n = open[bi].n; open.RemoveAt(bi);
                    if (n == b) break;
                    if (!closed.Add(n)) continue;
                    foreach (int m in links[n])
                    {
                        float cost = g[n] + Flat(nodes[n], nodes[m]);
                        if (g.TryGetValue(m, out float old) && old <= cost) continue;
                        g[m] = cost; came[m] = n;
                        open.Add((cost + Flat(nodes[m], nodes[b]), m));
                    }
                }
                if (!g.ContainsKey(b)) { into.Add(from); into.Add(to); return false; }
                var path = new List<Vector3>();
                for (int n = b; ; n = came[n]) { path.Add(nodes[n]); if (n == a) break; }
                path.Reverse();
                into.Add(from); into.AddRange(path); into.Add(to);
                return true;
            }

            /// <summary>The stretch of a centre-to-centre route between the two settlements' edges (a straight line of
            /// points every 20 m when there is no road).</summary>
            static void EdgeToEdge(WorldGen w, Vector3 a, float ra, Vector3 b, float rb, List<Vector3> into)
            {
                var full = new List<Vector3>();
                if (!RoadPath(w, a, b, full) || full.Count < 3)
                {
                    full.Clear();
                    int n = Mathf.Max(2, Mathf.CeilToInt(Flat(a, b) / 20f));
                    for (int i = 0; i <= n; i++) full.Add(Vector3.Lerp(a, b, i / (float)n));
                }
                into.Clear();
                int first = 0, last = full.Count - 1;
                while (first < full.Count - 2 && Flat(full[first], a) < ra + 14f) first++;
                while (last > first + 1 && Flat(full[last], b) < rb + 14f) last--;
                for (int i = first; i <= last; i++) into.Add(full[i]);
                if (into.Count < 2) { into.Clear(); into.Add(a + (b - a).normalized * (ra + 14f)); into.Add(b - (b - a).normalized * (rb + 14f)); }
            }

            /// <summary>Choose the village, the destination and their roads (once per world).</summary>
            public static void Bind(WorldGen w, Settlement first)
            {
                if (world == w && road.Count > 1) return;
                world = w; town = first; village = dest = null;
                market = first != null ? new Vector3(first.pos.x, 0f, first.pos.y) : at.TryGetValue("town1", out var t1) ? t1 : at.TryGetValue("wreck", out var wr) ? wr : Vector3.zero;
                float tr = first != null ? first.radius : 40f;
                // the village: the nearest village 300-3000 m from the market's edge (else the nearest settlement at all)
                float best = float.MaxValue;
                foreach (var st in w.settlements)
                {
                    if (st == first) continue;
                    float gap = Vector2.Distance(st.pos, new Vector2(market.x, market.z)) - st.radius - tr;
                    float score = gap + (st.kind == Biome.Village ? 0f : 700f) + (gap < 300f ? 2000f : 0f) + (gap > 3000f ? 3000f : 0f);
                    if (score < best) { best = score; village = st; }
                }
                villageAt = village != null ? new Vector3(village.pos.x, 0f, village.pos.y) : market + new Vector3(900f, 0f, 300f);
                EdgeToEdge(w, market, tr, villageAt, village != null ? village.radius : 30f, road);
                // which side of the road the straight line runs (the warden's short track): where the road bulges away from it
                var mid = At(road, Length(road) * 0.5f, out var md);
                var toMid = mid - Vector3.Lerp(market, villageAt, 0.5f);
                trackSide = Vector3.Dot(toMid, new Vector3(md.z, 0f, -md.x)) > 0f ? -1f : 1f;
                // the convoy's destination: the next settlement out from the market that isn't the village
                best = float.MaxValue;
                foreach (var st in w.settlements)
                {
                    if (st == first || st == village) continue;
                    float gap = Vector2.Distance(st.pos, new Vector2(market.x, market.z)) - st.radius - tr;
                    float score = gap + (gap < 400f ? 2500f : 0f) + (gap > 3500f ? 3500f : 0f);
                    if (score < best) { best = score; dest = st; }
                }
                if (dest != null) EdgeToEdge(w, market, tr, new Vector3(dest.pos.x, 0f, dest.pos.y), dest.radius, run);
                else { run.Clear(); run.AddRange(road); }
            }
        }

        /// <summary>Move a place at runtime (arc C: where a broken-down convoy truck stopped); rebound from the seed on load.</summary>
        internal static void ArcCMove(string key, Vector3 p, float face) { at[key] = p; yaw[key] = face; }
    }

    public static partial class StoryCast
    {
        /// <summary>Arc C: the same person at another place (<paramref name="twin"/>, e.g. Sera at the convoy's destination):
        /// a profile with the twin's key but the looks of <paramref name="of"/>, so the body is recognisably them. Call
        /// before the cast update spawns the twin; harmless to repeat.</summary>
        internal static void ArcCTwin(string twin, string of, int worldSeed)
        {
            int seed = worldSeed * 7919; foreach (char ch in of) seed = unchecked(seed * 31 + ch);
            if (made.TryGetValue(twin, out var have) && have.seed == seed) return;
            var src = Find(of); var me = Find(twin);
            if (src == null || me == null) return;
            var p = NpcProfile.Make("cast:" + twin, NpcRole.Resident, seed);
            var mv = src.Value;
            p.fullName = mv.name; p.title = mv.title; p.temper = mv.temper; p.female = mv.female;
            p.first = mv.name.Split(' ')[0];
            if (mv.female) p.look.beard = 0;
            p.outfit.Clear(); p.outfit.AddRange(mv.outfit);
            p.tool = mv.tool;
            made[twin] = p;
        }
    }
}
