using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Shortest route along the road network (A* over road points; roads join where their points meet at
    /// towns and junctions). Used for the waypoint line on the minimap and the map page.</summary>
    public static class RoadRoute
    {
        static WorldGen built;
        static Vector3[] nodes;
        static List<int>[] links;
        static int builtPlayerRoads = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = null; nodes = null; links = null; builtPlayerRoads = -1; }

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
            builtPlayerRoads = terrain ? terrain.PlayerRoadCount : 0;
            if (terrain && builtPlayerRoads > 0)
            {
                int start = pts.Count;
                foreach (var (pos, _) in terrain.PlayerRoads()) { pts.Add(pos); adj.Add(new List<int>()); }
                for (int i = start; i < pts.Count; i++)
                {
                    var c = Cell(pts[i]);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (grid.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out var l))
                            foreach (int j in l) if (Flat(pts[i] - pts[j]) < Join) { adj[i].Add(j); adj[j].Add(i); }
                    }
                    for (int j = start; j < i; j++) if (Flat(pts[i] - pts[j]) < 6f) { adj[i].Add(j); adj[j].Add(i); }
                }
            }
            nodes = pts.ToArray();
            links = adj.ToArray();
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
            var tr = DeformableTerrain.Instance;
            if (built != world || nodes == null || (tr && tr.PlayerRoadCount != builtPlayerRoads)) Build(world);
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
