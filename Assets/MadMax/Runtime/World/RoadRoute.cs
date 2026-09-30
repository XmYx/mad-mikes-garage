using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Shortest route along the road network (A* over road points; roads join where their points meet at
    /// towns and junctions) and the player's own roads (one node per paved 4 m map cell, at the paving's centroid).
    /// Used for the waypoint line on the minimap and the map page (<see cref="Find"/>) and by AI drivers
    /// (<see cref="FindForDriving"/>: open ground costs more than road, so a paved bypass wins over a shortcut).</summary>
    public static class RoadRoute
    {
        static WorldGen built;
        static Vector3[] nodes;
        static List<int>[] links;
        static bool[] player;
        static int builtPlayerRoads = -1;

        /// <summary>AI routes: a metre of open ground costs this many metres of road.</summary>
        public const float OffRoadCost = 2f;
        /// <summary>AI routes: road nodes within this of either end are tried as ways on and off the network.</summary>
        public const float AccessRadius = 90f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = null; nodes = null; links = null; player = null; builtPlayerRoads = -1; }

        static void Build(WorldGen world)
        {
            built = world;
            var pts = new List<Vector3>();
            var adj = new List<List<int>>();
            foreach (var r in world.roads.roads)
            {
                int first = pts.Count;
                for (int i = 0; i < r.points.Count; i++)
                {
                    pts.Add(r.points[i]); adj.Add(new List<int>());
                    if (i > 0) { adj[first + i].Add(first + i - 1); adj[first + i - 1].Add(first + i); }
                }
            }
            // junctions: points of different roads close together (a coarse grid keeps it cheap)
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
                    foreach (int j in l)
                        if (j > i && !adj[i].Contains(j) && Flat(pts[i] - pts[j]) < Join) { adj[i].Add(j); adj[j].Add(i); }
                }
            }
            // roads the player built (gravel, cobbles, asphalt, 4 m cells): their cells join each other and any road
            // point or player cell within reach, so a route can use a new gravel track to a base
            var terrain = DeformableTerrain.Instance;
            builtPlayerRoads = terrain ? terrain.PlayerRoadVersion : 0;
            int start = pts.Count;
            if (terrain && terrain.PlayerRoadCount > 0)
            {
                var byCell = new Dictionary<Vector2Int, int>();
                foreach (var (pos, cell, _) in terrain.PlayerRoadNodes()) { byCell[cell] = pts.Count; pts.Add(pos); adj.Add(new List<int>()); }
                foreach (var kv in byCell)
                {
                    int i = kv.Value;
                    var c = Cell(pts[i]);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (grid.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out var l))
                            foreach (int j in l) if (Flat(pts[i] - pts[j]) < Join) { adj[i].Add(j); adj[j].Add(i); }
                        // neighbouring map cells of the player's roads (diagonals too) join each other
                        if (byCell.TryGetValue(new Vector2Int(kv.Key.x + dx, kv.Key.y + dz), out int j2) && j2 > i) { adj[i].Add(j2); adj[j2].Add(i); }
                    }
                }
            }
            nodes = pts.ToArray();
            links = adj.ToArray();
            player = new bool[nodes.Length];
            for (int i = start; i < nodes.Length; i++) player[i] = true;
        }

        static void Ensure(WorldGen world)
        {
            var tr = DeformableTerrain.Instance;
            if (built != world || nodes == null || (tr && tr.PlayerRoadVersion != builtPlayerRoads)) Build(world);
        }

        /// <summary>Is route point <paramref name="p"/> one of the player's road nodes (exact node positions).</summary>
        public static bool IsPlayerNode(Vector3 p)
        {
            if (nodes == null) return false;
            for (int i = nodes.Length - 1; i >= 0 && player[i]; i--) if (Flat(nodes[i] - p) < 0.01f) return true;
            return false;
        }

        /// <summary>Route for a driver from <paramref name="from"/> to <paramref name="to"/>: along generated and player
        /// roads alike, with open ground (the way on, the way off, or the straight line) costing
        /// <see cref="OffRoadCost"/> times as much, so a road is taken whenever it isn't a long detour. Both ends are
        /// included; the road part is the road's points in order. <paramref name="usesPlayerRoad"/>: it runs along the
        /// player's paving. False (straight line in <paramref name="into"/>) when no road route beats it.</summary>
        public static bool FindForDriving(WorldGen world, Vector3 from, Vector3 to, List<Vector3> into, out bool usesPlayerRoad)
        {
            into.Clear();
            usesPlayerRoad = false;
            if (world == null) { into.Add(from); into.Add(to); return false; }
            Ensure(world);
            int n = nodes.Length;
            if (n == 0) { into.Add(from); into.Add(to); return false; }
            // multi-source A*: every node near the start is a way on (open ground to it), every node near the end a way off
            var g = new Dictionary<int, float>();
            var came = new Dictionary<int, int>();
            var open = new List<(float f, int n)>();
            int nearA = -1, nearB = -1; float da = float.MaxValue, db = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float a = Flat(nodes[i] - from), b = Flat(nodes[i] - to);
                if (a < da) { da = a; nearA = i; }
                if (b < db) { db = b; nearB = i; }
            }
            for (int i = 0; i < n; i++)
            {
                float a = Flat(nodes[i] - from);
                if (a > AccessRadius && i != nearA) continue;
                g[i] = a * OffRoadCost;
                open.Add((g[i] + Flat(nodes[i] - to), i));
            }
            float best = Flat(to - from) * OffRoadCost; int exit = -1;                           // the straight line to beat
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int bi = 0; for (int i = 1; i < open.Count; i++) if (open[i].f < open[bi].f) bi = i;
                var (f, m) = open[bi]; open.RemoveAt(bi);
                if (f >= best) break;                                                               // nothing left can beat it
                if (!closed.Add(m)) continue;
                float off = Flat(nodes[m] - to);
                if ((off <= AccessRadius || m == nearB) && g[m] + off * OffRoadCost < best) { best = g[m] + off * OffRoadCost; exit = m; }
                foreach (int k in links[m])
                {
                    float cost = g[m] + Flat(nodes[m] - nodes[k]);
                    if (g.TryGetValue(k, out float old) && old <= cost) continue;
                    g[k] = cost; came[k] = m;
                    open.Add((cost + Flat(nodes[k] - to), k));
                }
            }
            into.Add(from);
            if (exit < 0) { into.Add(to); return false; }
            var path = new List<int>();
            for (int k = exit; ; k = came[k]) { path.Add(k); if (!came.ContainsKey(k)) break; }
            path.Reverse();
            foreach (int k in path) { into.Add(nodes[k]); if (player[k]) usesPlayerRoad = true; }
            into.Add(to);
            return true;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        static int Nearest(Vector3 p)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < nodes.Length; i++) { float d = Flat(nodes[i] - p); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>Road route from <paramref name="from"/> to <paramref name="to"/> (both ends included). False when
        /// there are no roads or no connection (the route is then the straight line).</summary>
        public static bool Find(WorldGen world, Vector3 from, Vector3 to, List<Vector3> into)
        {
            into.Clear();
            if (world == null) return false;
            Ensure(world);
            if (nodes.Length == 0) { into.Add(from); into.Add(to); return false; }
            int a = Nearest(from), b = Nearest(to);
            // closer than the roads are: straight line
            if (Flat(from - to) < Flat(from - nodes[a]) + Flat(to - nodes[b])) { into.Add(from); into.Add(to); return true; }
            var g = new Dictionary<int, float> { [a] = 0f };
            var came = new Dictionary<int, int>();
            var open = new List<(float f, int n)> { (Flat(nodes[a] - nodes[b]), a) };
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int bi = 0; for (int i = 1; i < open.Count; i++) if (open[i].f < open[bi].f) bi = i;
                int n = open[bi].n; open.RemoveAt(bi);
                if (n == b) break;
                if (!closed.Add(n)) continue;
                foreach (int m in links[n])
                {
                    float cost = g[n] + Flat(nodes[n] - nodes[m]);
                    if (g.TryGetValue(m, out float old) && old <= cost) continue;
                    g[m] = cost; came[m] = n;
                    open.Add((cost + Flat(nodes[m] - nodes[b]), m));
                }
            }
            if (!g.ContainsKey(b)) { into.Add(from); into.Add(to); return false; }
            var path = new List<Vector3>();
            for (int n = b; ; n = came[n]) { path.Add(nodes[n]); if (n == a) break; }
            path.Reverse();
            into.Add(from); into.AddRange(path); into.Add(to);
            return true;
        }
    }
}
