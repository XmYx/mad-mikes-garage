using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Station tiers by where they stand: a city terminal (platforms ending at buffer stops, a yard, a water
    /// tower, a signal box, an engine shed), a town through station (two platforms, a passing loop), a village halt (one
    /// platform, a shelter), a water and fuel stop out on a long line, a junction in the wasteland (a signal box).</summary>
    public enum StationTier : byte { Terminal, Through, Halt, WaterStop, Junction }
    public enum TrackKind : byte { Main, Loop, Platform, Siding, Built }

    /// <summary>A station: a straight frame (u along <see cref="dir"/>, v toward <see cref="side"/>, the town side),
    /// the flat graded height of its yard, the platform half length and its platform tracks.</summary>
    public sealed class RailStation
    {
        public int id, settlement = -1;
        public StationTier tier;
        public string name;
        public Vector2 pos, dir, side;
        public float height, half;
        public readonly List<int> platformLines = new List<int>();
        public readonly List<Vector4> platforms = new List<Vector4>();   // u0, u1, v0, v1 (top at RailNetwork.PlatformTop)

        public Vector3 World(float u, float v, float y = 0f) => new Vector3(pos.x + dir.x * u + side.x * v, height + y, pos.y + dir.y * u + side.y * v);
        public Vector2 Local(Vector3 p) { var d = new Vector2(p.x - pos.x, p.z - pos.y); return new Vector2(Vector2.Dot(d, dir), Vector2.Dot(d, side)); }
        /// <summary>Yaw (degrees) that turns +Z along the station's u axis.</summary>
        public float Yaw => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        /// <summary>+X of a frame yawed by <see cref="Yaw"/> points to -side (true) or +side (false).</summary>
        public bool SideIsLeft => dir.x * side.y - dir.y * side.x > 0f;
        public bool Passenger => tier == StationTier.Terminal || tier == StationTier.Through || tier == StationTier.Halt;

        /// <summary>The name on the boards (towns' stations after the town, the rest by their own).</summary>
        public string Name(WorldGen w)
        {
            if (settlement >= 0 && w != null && settlement < w.settlements.Count)
            {
                string town = MadMax.Npc.Market.TownName(w.settlements[settlement]);
                return tier == StationTier.Terminal ? town + " TERMINAL" : tier == StationTier.Through ? town + " STATION" : town + " HALT";
            }
            return name;
        }
    }

    /// <summary>A length of track between two nodes: points every ~4 m at the rail bed height (rails on top at
    /// <see cref="RailNetwork.RailTop"/>), distance along, per-point flags (bridge, level crossing). Immutable once made;
    /// splitting makes two new lines and marks this one dead.</summary>
    public sealed class RailLine
    {
        public const byte Bridge = 1, Crossing = 2, Platform = 4;
        public int id, a = -1, b = -1, station = -1;
        public TrackKind kind;
        public bool dead, built, scrap;
        public readonly Vector3[] pts;
        public readonly float[] cum;
        public readonly byte[] flags;
        public readonly float Length;
        public Bounds bounds;

        public RailLine(Vector3[] pts, TrackKind kind, byte[] flags = null)
        {
            this.pts = pts; this.kind = kind;
            this.flags = flags ?? new byte[pts.Length];
            cum = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            Length = cum[pts.Length - 1];
            bounds = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) bounds.Encapsulate(p);
        }

        public int Seg(float s)
        {
            if (s <= 0f) return 0;
            if (s >= Length) return pts.Length - 2;
            int lo = 0, hi = pts.Length - 1;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (cum[m] <= s) lo = m; else hi = m; }
            return lo;
        }

        /// <summary>Bed point and unit tangent (toward b) at distance s.</summary>
        public Vector3 At(float s, out Vector3 tangent)
        {
            int i = Seg(s);
            float len = Mathf.Max(1e-4f, cum[i + 1] - cum[i]);
            float t = Mathf.Clamp01((s - cum[i]) / len);
            tangent = (pts[i + 1] - pts[i]) / len;
            return Vector3.LerpUnclamped(pts[i], pts[i + 1], t);
        }

        public Vector3 At(float s) => At(s, out _);
        public byte FlagAt(float s) { int i = Seg(s); return (byte)(flags[i] | flags[i + 1]); }

        /// <summary>Nearest point on the line (plan view): distance along and the horizontal distance to it.</summary>
        public float Nearest(Vector3 p, out float dist)
        {
            float best = float.MaxValue, along = 0f;
            var q = new Vector2(p.x, p.z);
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = new Vector2(pts[i].x, pts[i].z); var ab = new Vector2(pts[i + 1].x, pts[i + 1].z) - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2) : 0f;
                float d2 = (a + ab * t - q).sqrMagnitude;
                if (d2 < best) { best = d2; along = cum[i] + t * (cum[i + 1] - cum[i]); }
            }
            dist = Mathf.Sqrt(best);
            return along;
        }

        /// <summary>Unit direction leaving node <paramref name="end"/> (0 = a, 1 = b) along the line.</summary>
        public Vector3 Outward(int end)
        {
            int n = pts.Length;
            var d = end == 0 ? pts[Mathf.Min(2, n - 1)] - pts[0] : pts[Mathf.Max(0, n - 3)] - pts[n - 1];
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }
    }

    /// <summary>Where line ends meet: a joint (two ends), points (three ends: a toe and two heels set by
    /// <see cref="state"/>), or a dead end with a buffer stop.</summary>
    public sealed class RailNode
    {
        public int id;
        public Vector3 pos;
        public readonly List<Vector2Int> ends = new List<Vector2Int>();   // (line, 0 = its a end / 1 = its b end)
        public int toe = -1, heelA = -1, heelB = -1;                       // indices into ends; heelA = the straight road
        public int state;                                                  // 0 = heelA, 1 = heelB
        public int lever = -1;                                             // which side the lever stand is on (+1 right of the toe, -1 left)
        public bool Switch => toe >= 0;
        public bool DeadEnd => ends.Count == 1;
    }

    /// <summary>The railway (generated with the world, extended by the player at runtime): stations by tier, lines,
    /// nodes and points. The ground grading (<see cref="Grade"/>) reads only arrays frozen at generation, so terrain
    /// workers can call it while the main thread splits lines to branch new track off them. Topology queries and edits
    /// (<see cref="Next"/>, <see cref="AddBuilt"/>, <see cref="NearestTrack"/>) are main-thread.</summary>
    public sealed partial class RailNetwork
    {
        public const float RailTop = 0.32f, PlatformTop = 0.68f, Gauge = 1.435f, BedHalf = 2.2f, Step = 4f, MaxGrade = 0.025f;
        public const float JoinSnap = 0.6f;
        const float Cell = 64f, Reach = BedHalf + 40f;

        public readonly WorldGen world;
        public readonly List<RailStation> stations = new List<RailStation>();
        public readonly List<RailLine> lines = new List<RailLine>();
        public readonly List<RailNode> nodes = new List<RailNode>();
        /// <summary>Bumped on every topology change (visual tiles and trains re-read the lines).</summary>
        public int Version { get; private set; }
        /// <summary>World-space bounds of the last change (tiles inside rebuild).</summary>
        public event System.Action<Bounds> Changed;

        // ------------------------------------------------------------------ frozen grading data (any thread)
        struct GradeSeg { public Vector2 a, b; public float ha, hb; public byte fa, fb; }
        struct Pad { public Vector2 c, u, v; public float u0, u1, v0, v1, h; public byte feat; public int prio; }
        GradeSeg[] gsegs = new GradeSeg[0];
        Pad[] pads = new Pad[0];
        readonly Dictionary<long, int[]> segGrid = new Dictionary<long, int[]>(), padGrid = new Dictionary<long, int[]>();

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        /// <summary>Grade the land to the rail bed, cuttings and embankments (the shoulder widens with the height to
        /// make up), level station yards; features: 19 ballast on the bed, 20 platforms and forecourts, 6 yard gravel.
        /// Bridges leave the ground alone. Thread-safe (frozen arrays).</summary>
        public void Grade(float x, float z, ref float h, ref GroundSample s)
        {
            long k = Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell));
            float wBest = 0f, target = h;
            byte feat = 0; int prio = -1;
            var q = new Vector2(x, z);
            if (padGrid.TryGetValue(k, out var pl))
                foreach (int i in pl)
                {
                    var p = pads[i];
                    var r = q - p.c;
                    float u = Vector2.Dot(r, p.u), v = Vector2.Dot(r, p.v);
                    float e = Mathf.Max(Mathf.Max(p.u0 - u, u - p.u1), Mathf.Max(p.v0 - v, v - p.v1));
                    if (e <= 0f && p.prio > prio) { prio = p.prio; feat = p.feat; }
                    if (p.prio > 0) continue;                                                    // sub-pads only paint
                    float sh = Mathf.Clamp(3f + Mathf.Abs(p.h - h) * 3.2f, 4f, 40f);                // yard banks ~20°, like the cuttings
                    float w = 1f - Mathf.Clamp01(e / sh);
                    if (w > wBest) { wBest = w; target = p.h; }
                }
            float bedDist = 999f;
            if (segGrid.TryGetValue(k, out var sl))
            {
                float best = float.MaxValue, bh = 0f; byte bf = 0;
                foreach (int i in sl)
                {
                    var g = gsegs[i];
                    var ab = g.b - g.a; float l2 = ab.sqrMagnitude;
                    float t = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(q - g.a, ab) / l2) : 0f;
                    float d2 = (g.a + ab * t - q).sqrMagnitude;
                    if (d2 >= best) continue;
                    best = d2; bh = Mathf.Lerp(g.ha, g.hb, t); bf = (byte)(t < 0.5f ? g.fa : g.fb);
                }
                if (best < Reach * Reach)
                {
                    bedDist = Mathf.Sqrt(best);
                    if ((bf & RailLine.Bridge) == 0)
                    {
                        float e = bedDist - BedHalf;
                        float sh = Mathf.Clamp(3f + Mathf.Abs(bh - h) * 3.2f, 3f, 40f);              // cuttings and embankments ~20°: climbable
                        float w = 1f - Mathf.Clamp01(e / sh);
                        if (w > wBest) { wBest = w; target = bh; }
                    }
                    else bedDist = 999f;                                                         // under a bridge: the ground stays
                }
            }
            if (wBest > 0f)
            {
                float w = Mathf.Lerp(wBest, wBest * wBest * (3f - 2f * wBest), 0.35f);             // mostly linear: the gentlest bank for its width
                h = Mathf.Lerp(h, target, w);
                if (w > 0.6f && wBest >= 0.999f) { s.water = float.NaN; s.shore = 0f; }
            }
            bool onRoad = s.road > 0.5f;
            if (bedDist < BedHalf + 0.4f && !onRoad) { s.feature = 19; s.roadDist = Mathf.Min(s.roadDist, 0f); }
            else if (feat != 0 && !onRoad) s.feature = feat;
            if (bedDist < 999f) s.roadDist = Mathf.Min(s.roadDist, Mathf.Max(0f, bedDist - BedHalf - 1f));
        }

        void AddPad(RailStation st, float u0, float u1, float v0, float v1, float h, byte feat, int prio)
        {
            var p = new Pad { c = st.pos, u = st.dir, v = st.side, u0 = u0, u1 = u1, v0 = v0, v1 = v1, h = h, feat = feat, prio = prio };
            padList.Add(p);
        }
        readonly List<Pad> padList = new List<Pad>();

        /// <summary>Freeze the generated beds and pads into the grading grids (end of generation).</summary>
        void Freeze()
        {
            var segs = new List<GradeSeg>();
            var sg = new Dictionary<long, List<int>>();
            foreach (var l in lines)
            {
                if (l.dead) continue;
                for (int i = 0; i < l.pts.Length - 1; i++)
                {
                    var g = new GradeSeg { a = new Vector2(l.pts[i].x, l.pts[i].z), b = new Vector2(l.pts[i + 1].x, l.pts[i + 1].z), ha = l.pts[i].y, hb = l.pts[i + 1].y, fa = l.flags[i], fb = l.flags[i + 1] };
                    int id = segs.Count; segs.Add(g);
                    Stamp(sg, Vector2.Min(g.a, g.b) - Vector2.one * Reach, Vector2.Max(g.a, g.b) + Vector2.one * Reach, id);
                }
            }
            gsegs = segs.ToArray();
            foreach (var kv in sg) segGrid[kv.Key] = kv.Value.ToArray();
            pads = padList.ToArray();
            var pg = new Dictionary<long, List<int>>();
            for (int i = 0; i < pads.Length; i++)
            {
                var p = pads[i];
                float r = Mathf.Max(Mathf.Abs(p.u0), Mathf.Abs(p.u1)) + Mathf.Max(Mathf.Abs(p.v0), Mathf.Abs(p.v1)) + 42f;
                Stamp(pg, p.c - Vector2.one * r, p.c + Vector2.one * r, i);
            }
            foreach (var kv in pg) padGrid[kv.Key] = kv.Value.ToArray();
        }

        static void Stamp(Dictionary<long, List<int>> grid, Vector2 min, Vector2 max, int id)
        {
            for (int cx = Mathf.FloorToInt(min.x / Cell); cx <= Mathf.FloorToInt(max.x / Cell); cx++)
            for (int cz = Mathf.FloorToInt(min.y / Cell); cz <= Mathf.FloorToInt(max.y / Cell); cz++)
            {
                long k = Key(cx, cz);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(id);
            }
        }

        // ------------------------------------------------------------------ topology (main thread after generation)
        readonly Dictionary<long, List<int>> liveGrid = new Dictionary<long, List<int>>();

        int NewNode(Vector3 p) { var n = new RailNode { id = nodes.Count, pos = p }; nodes.Add(n); return n.id; }

        int AddLine(RailLine l, int a, int b)
        {
            l.id = lines.Count; l.a = a; l.b = b;
            lines.Add(l);
            nodes[a].ends.Add(new Vector2Int(l.id, 0));
            nodes[b].ends.Add(new Vector2Int(l.id, 1));
            IndexLive(l);
            return l.id;
        }

        void IndexLive(RailLine l)
        {
            for (int i = 0; i < l.pts.Length - 1; i++)
            {
                var a = l.pts[i]; var b = l.pts[i + 1];
                for (int cx = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - 4f) / Cell); cx <= Mathf.FloorToInt((Mathf.Max(a.x, b.x) + 4f) / Cell); cx++)
                for (int cz = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - 4f) / Cell); cz <= Mathf.FloorToInt((Mathf.Max(a.z, b.z) + 4f) / Cell); cz++)
                {
                    long k = Key(cx, cz);
                    if (!liveGrid.TryGetValue(k, out var list)) liveGrid[k] = list = new List<int>();
                    if (list.Count == 0 || list[list.Count - 1] != l.id) list.Add(l.id);
                }
            }
        }

        void Kill(RailLine l)
        {
            l.dead = true;
            for (int e = 0; e < 2; e++)
            {
                var n = nodes[e == 0 ? l.a : l.b];
                n.ends.Remove(new Vector2Int(l.id, e));
            }
        }

        /// <summary>Points config of a node: with three ends, the heels are the pair leaving most alike, the toe the
        /// odd one out; heelA is the straighter road through.</summary>
        void Configure(RailNode n)
        {
            n.toe = n.heelA = n.heelB = -1;
            if (n.ends.Count != 3) return;
            var o = new Vector3[3];
            for (int i = 0; i < 3; i++) o[i] = lines[n.ends[i].x].Outward(n.ends[i].y);
            float best = -2f; int hi = 0, hj = 1;
            for (int i = 0; i < 3; i++) for (int j = i + 1; j < 3; j++) { float d = Vector3.Dot(o[i], o[j]); if (d > best) { best = d; hi = i; hj = j; } }
            int toe = 3 - hi - hj;
            n.toe = toe;
            bool iStraighter = Vector3.Dot(o[hi], o[toe]) < Vector3.Dot(o[hj], o[toe]);
            n.heelA = iStraighter ? hi : hj; n.heelB = iStraighter ? hj : hi;
            var side = Vector3.Cross(Vector3.up, -o[toe]);                                    // right of a train leaving through the toe... reversed
            n.lever = Vector3.Dot(o[n.heelB], side) > 0f ? -1 : 1;                              // the lever stands on the side away from the diverging road
            n.state = Mathf.Clamp(n.state, 0, 1);
        }

        /// <summary>Which way the diverging heel goes for a train running from the toe: -1 left, +1 right.</summary>
        public int DivergeSide(RailNode n)
        {
            if (!n.Switch) return 0;
            var toeDir = -lines[n.ends[n.toe].x].Outward(n.ends[n.toe].y);                  // travelling through the toe into the heels
            var b = lines[n.ends[n.heelB].x].Outward(n.ends[n.heelB].y);
            return Vector3.Cross(toeDir, b).y > 0f ? -1 : 1;
        }

        /// <summary>The ends a train coming in along (line, end) may leave by (roughly straight on), in no order.</summary>
        public void Exits(int nodeId, int line, int end, List<Vector2Int> into)
        {
            into.Clear();
            var n = nodes[nodeId];
            var inOut = lines[line].Outward(end);
            foreach (var e in n.ends)
            {
                if (e.x == line && e.y == end) continue;
                if (Vector3.Dot(lines[e.x].Outward(e.y), inOut) < -0.3f) into.Add(e);
            }
        }

        readonly List<Vector2Int> exitBuf = new List<Vector2Int>();

        /// <summary>The end a train coming in along (line, end) leaves by, following the points (none: a dead end).</summary>
        public bool Next(int nodeId, int line, int end, out Vector2Int next)
        {
            next = default;
            var n = nodes[nodeId];
            Exits(nodeId, line, end, exitBuf);
            if (exitBuf.Count == 0) return false;
            if (n.Switch)
            {
                var from = new Vector2Int(line, end);
                if (n.ends[n.toe] == from) { next = n.ends[n.state == 0 ? n.heelA : n.heelB]; return true; }
                next = n.ends[n.toe];
                return exitBuf.Contains(next);
            }
            if (exitBuf.Count == 1) { next = exitBuf[0]; return true; }
            float best = 2f;
            foreach (var e in exitBuf) { float d = Vector3.Dot(lines[e.x].Outward(e.y), lines[line].Outward(end)); if (d < best) { best = d; next = e; } }
            return true;
        }

        /// <summary>Trailing through points from a heel sets them (a train never runs through the wrong road).</summary>
        public void SetFor(int nodeId, int line, int end)
        {
            var n = nodes[nodeId];
            if (!n.Switch) return;
            var e = new Vector2Int(line, end);
            if (n.ends[n.heelA] == e) n.state = 0; else if (n.ends[n.heelB] == e) n.state = 1;
        }

        /// <summary>The nearest live track within <paramref name="max"/> m (plan view).</summary>
        public RailLine NearestTrack(Vector3 p, float max, out float s, out float dist)
        {
            s = 0f; dist = float.MaxValue;
            RailLine best = null;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!liveGrid.TryGetValue(Key(Mathf.FloorToInt(p.x / Cell) + dx, Mathf.FloorToInt(p.z / Cell) + dz), out var list)) continue;
                foreach (int id in list)
                {
                    var l = lines[id];
                    if (l.dead) continue;
                    var b = l.bounds; b.Expand(max * 2f);
                    if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                    float along = l.Nearest(p, out float d);
                    if (d < dist && d <= max) { dist = d; s = along; best = l; }
                }
            }
            return best;
        }

        /// <summary>Live lines whose bounds touch a box (tiles).</summary>
        public void LinesIn(Bounds box, List<RailLine> into)
        {
            into.Clear();
            int x0 = Mathf.FloorToInt(box.min.x / Cell), x1 = Mathf.FloorToInt(box.max.x / Cell), z0 = Mathf.FloorToInt(box.min.z / Cell), z1 = Mathf.FloorToInt(box.max.z / Cell);
            for (int cx = x0; cx <= x1; cx++)
            for (int cz = z0; cz <= z1; cz++)
            {
                if (!liveGrid.TryGetValue(Key(cx, cz), out var list)) continue;
                foreach (int id in list) { var l = lines[id]; if (!l.dead && !into.Contains(l)) into.Add(l); }
            }
        }

        /// <summary>The node nearest a point within <paramref name="max"/> (null: none).</summary>
        public RailNode NodeNear(Vector3 p, float max, bool openOnly = false)
        {
            RailNode best = null; float bd = max * max;
            foreach (var n in nodes)
            {
                if (n.ends.Count == 0 || (openOnly && n.ends.Count != 1)) continue;
                float d = (new Vector2(n.pos.x - p.x, n.pos.z - p.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        /// <summary>Split a line at distance s into two (a node with points between them). Returns the node.</summary>
        public int Split(RailLine l, float s)
        {
            int i = l.Seg(s);
            var p = l.At(s);
            var first = new List<Vector3>(); var second = new List<Vector3>();
            var f1 = new List<byte>(); var f2 = new List<byte>();
            for (int k = 0; k <= i; k++) { first.Add(l.pts[k]); f1.Add(l.flags[k]); }
            if ((first[first.Count - 1] - p).sqrMagnitude > 0.01f) { first.Add(p); f1.Add(l.flags[i]); }
            else first[first.Count - 1] = p;
            second.Add(p); f2.Add(l.flags[i + 1]);
            for (int k = i + 1; k < l.pts.Length; k++) { if (k == i + 1 && (l.pts[k] - p).sqrMagnitude < 0.01f) continue; second.Add(l.pts[k]); f2.Add(l.flags[k]); }
            if (first.Count < 2 || second.Count < 2) return (first.Count < 2) ? l.a : l.b;
            int a = l.a, b = l.b;
            Kill(l);
            int mid = NewNode(p);
            var la = new RailLine(first.ToArray(), l.kind, f1.ToArray()) { built = l.built, station = l.station, scrap = l.scrap };
            var lb = new RailLine(second.ToArray(), l.kind, f2.ToArray()) { built = l.built, station = l.station, scrap = l.scrap };
            AddLine(la, a, mid); AddLine(lb, mid, b);
            Configure(nodes[a]); Configure(nodes[b]); Configure(nodes[mid]);
            foreach (var st in stations) { int k = st.platformLines.IndexOf(l.id); if (k >= 0) { st.platformLines[k] = la.id; st.platformLines.Add(lb.id); } }
            return mid;
        }

        /// <summary>Player-laid track: bed points (≥ 2). Each end joins a node within <see cref="JoinSnap"/>, or splits
        /// a line it lands on (new points), or stays an open end. Returns the new line.</summary>
        public RailLine AddBuilt(IList<Vector3> pts, bool scrap)
        {
            var arr = new Vector3[pts.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = pts[i];
            int a = Attach(arr[0]), b = Attach(arr[arr.Length - 1]);
            arr[0] = nodes[a].pos; arr[arr.Length - 1] = nodes[b].pos;
            var l = new RailLine(arr, TrackKind.Built) { built = true, scrap = scrap };
            AddLine(l, a, b);
            Configure(nodes[a]); Configure(nodes[b]);
            Touch(l.bounds);
            return l;
        }

        int Attach(Vector3 p)
        {
            var n = NodeNear(p, JoinSnap);
            if (n != null) return n.id;
            var l = NearestTrack(p, JoinSnap, out float s, out _);
            if (l != null && s > 0.5f && s < l.Length - 0.5f) return Split(l, s);
            if (l != null) return s <= 0.5f ? l.a : l.b;
            return NewNode(p);
        }

        /// <summary>Take up a player-laid line (its nodes stay; a branch's points become a plain joint).</summary>
        public void RemoveBuilt(RailLine l)
        {
            if (l == null || !l.built || l.dead) return;
            Kill(l);
            Configure(nodes[l.a]); Configure(nodes[l.b]);
            Touch(l.bounds);
        }

        void Touch(Bounds b) { Version++; b.Expand(8f); Changed?.Invoke(b); }

        /// <summary>Stations within a radius of a point.</summary>
        public RailStation StationNear(Vector3 p, float max)
        {
            RailStation best = null; float bd = max * max;
            foreach (var st in stations) { float d = (st.pos - new Vector2(p.x, p.z)).sqrMagnitude; if (d < bd) { bd = d; best = st; } }
            return best;
        }

        /// <summary>The station whose platform track this line is (null: none).</summary>
        public RailStation StationOf(RailLine l) => l != null && l.station >= 0 && l.station < stations.Count ? stations[l.station] : null;
    }
}
