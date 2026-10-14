using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Generation: a station per settlement at the edge of its plan (tier by kind: city terminal laid
    /// radially with its buffers at the town, town through station and village halt laid along the edge on the side
    /// with the fewest roads, water and slope), their track layouts, then lines joining them as a spanning tree (Prim,
    /// same landmass only) with a few loops; a new station joins the nearest point of the network when that is much
    /// closer than any station (a junction in the wasteland). Each line is routed by A* on a 24 m grid around towns,
    /// lakes, sites, the sea and the crawlerways (rivers cost a bridge, roads a level crossing), string-pulled, smoothed
    /// and given a profile under 2.5 % pinned to the stations and the roads it crosses. Long lines get a water stop.</summary>
    public sealed partial class RailNetwork
    {
        static readonly string[] StopA = { "DRY", "RED", "BONE", "SALT", "IRON", "DUST", "CINDER", "LONG", "LAST", "GRIT", "COAL", "WIDOW'S" };
        static readonly string[] StopB = { "TANK", "WELL", "SIDING", "WATER", "STOP", "HALT" };

        readonly Dictionary<int, List<int>> ports = new Dictionary<int, List<int>>();     // station → port nodes
        readonly HashSet<long> joined = new HashSet<long>();
        System.Random rnd;

        public RailNetwork(WorldGen w)
        {
            world = w;
            rnd = new System.Random(w.seed * 6151 + 29);
            foreach (var st in w.settlements) PlaceStation(st);
            Connect();
            WaterStops();
            foreach (var n in nodes) Configure(n);
            Freeze();
        }

        // ------------------------------------------------------------------ stations

        void PlaceStation(Settlement town)
        {
            var plan = town.plan;
            if (plan == null) return;
            var tier = town.kind == Biome.City ? StationTier.Terminal : town.kind == Biome.Town ? StationTier.Through : StationTier.Halt;
            float half = tier == StationTier.Terminal ? 70f : tier == StationTier.Through ? 60f : 26f;
            RailStation best = null; float bestScore = float.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                float ang = plan.angle + Mathf.PI / 4f + k * Mathf.PI / 4f;
                var radial = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var perp = new Vector2(-radial.y, radial.x);
                for (int g = 0; g < 3; g++)
                {
                    var st = new RailStation { tier = tier, settlement = town.index, half = half };
                    if (tier == StationTier.Terminal)
                    {
                        st.pos = town.pos + radial * (plan.radius + 20f + g * 16f + half);
                        st.dir = -radial; st.side = perp;
                    }
                    else
                    {
                        st.pos = town.pos + radial * (plan.radius + 16f + g * 14f);
                        st.dir = perp; st.side = -radial;
                    }
                    float s = Score(st, town, out int hard, out int samples);
                    if (hard > samples / 10) continue;                                     // sea, ice or the date line under it
                    if (s < bestScore) { bestScore = s; best = st; }
                }
            }
            if (best == null) return;
            var lvl = tier == StationTier.Terminal ? best.World(half + 8f, 0f) : best.World(0f, 8f);
            best.height = Mathf.Max(plan.LevelAt(lvl.x, lvl.z) + TownPlan.Kerb, WorldGen.SeaLevel + 1.5f);
            best.id = stations.Count;
            stations.Add(best);
            Layout(best);
        }

        float Score(RailStation st, Settlement own, out int hard, out int samples)
        {
            hard = 0; samples = 0;
            float u0, u1, v0, v1;
            if (st.tier == StationTier.Terminal) { u0 = -st.half - 66f; u1 = st.half + 16f; v0 = -22f; v1 = 20f; }
            else if (st.tier == StationTier.Through) { u0 = -st.half - 50f; u1 = st.half + 50f; v0 = -12f; v1 = 17f; }
            else { u0 = -st.half - 24f; u1 = st.half + 24f; v0 = -4f; v1 = 11f; }
            var c = st.World(0f, 0f);
            float href = own.plan.LevelAt(c.x, c.z), score = 0f;
            for (float u = u0; u <= u1; u += 12f)
            for (float v = v0; v <= v1; v += 6f)
            {
                var p = st.World(u, v);
                float x = p.x, z = p.z;
                samples++;
                if (!world.Habitable(x, z, 0.55f)) { score += 1000f; hard++; continue; }
                float bh = world.BaseHeight(x, z);
                if (bh < WorldGen.SeaLevel + 1f) { score += 1000f; hard++; continue; }
                if (world.LakeAt(x, z, out float t) != null && t < 1.5f) score += 500f;
                if (world.RiverAt(x, z, out _, out _, out _)) score += 200f;
                if (world.SiteAt(x, z) != null) score += 500f;
                if (Blocked(x, z)) score += 500f;
                foreach (var s in world.settlements)
                {
                    float r = s.plan != null ? s.plan.radius + 3f : s.radius;
                    if ((s.pos - new Vector2(x, z)).sqrMagnitude < r * r) { score += 300f; break; }
                }
                foreach (var o in stations) if ((o.pos - new Vector2(x, z)).sqrMagnitude < (o.half + 30f) * (o.half + 30f)) { score += 400f; break; }
                if (Mathf.Abs(u) <= st.half + 12f && world.roads.Query(x, z, out var hit) && hit.dist < hit.width * 0.5f + 2.5f) score += 60f;
                score += Mathf.Abs(bh - href) * 1.5f;
            }
            return score;
        }

        /// <summary>On a rolling city's crawlerway bed (lines cross it on the level, pinned to the bed, like the roads do).</summary>
        bool OnCrawlerway(float x, float z)
        {
            if (world.city != null && world.city.Nearest(x, z, out _, out float lat) && Mathf.Abs(lat) < world.city.bedHalf + 40f) return true;
            if (world.city2 != null && world.city2.Nearest(x, z, out _, out float lat2) && Mathf.Abs(lat2) < world.city2.bedHalf + 40f) return true;
            return false;
        }

        /// <summary>The megacity plaza and the start yard: never railed over (routing); stations also keep off the crawlerways.</summary>
        bool BlockedRoute(float x, float z) => world.YardWeight(x, z) > 0f || (world.mega != null && world.mega.Edge(x, z) < 80f);

        /// <summary>The crawlerways, the megacity plaza and the start yard: never railed over.</summary>
        bool Blocked(float x, float z)
        {
            if (world.YardWeight(x, z) > 0f) return true;
            if (world.city != null && world.city.Nearest(x, z, out _, out float lat) && Mathf.Abs(lat) < world.city.bedHalf + 40f) return true;
            if (world.city2 != null && world.city2.Nearest(x, z, out _, out float lat2) && Mathf.Abs(lat2) < world.city2.bedHalf + 40f) return true;
            if (world.mega != null && world.mega.Edge(x, z) < 80f) return true;
            return false;
        }

        // station-frame helpers
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Track in the station frame from u0 to u1 with lateral v(u), flat at the station height.</summary>
        List<Vector3> Run(RailStation st, float u0, float u1, System.Func<float, float> v)
        {
            var o = new List<Vector3>();
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(u1 - u0) / Step));
            for (int i = 0; i <= n; i++) { float u = Mathf.Lerp(u0, u1, i / (float)n); o.Add(st.World(u, v(u))); }
            return o;
        }

        int Line(List<Vector3> pts, int a, int b, TrackKind kind, RailStation st = null)
        {
            pts[0] = nodes[a].pos; pts[pts.Count - 1] = nodes[b].pos;
            var l = new RailLine(pts.ToArray(), kind);
            int id = AddLine(l, a, b);
            if (st != null && (kind == TrackKind.Platform || kind == TrackKind.Loop)) { l.station = st.id; st.platformLines.Add(id); }
            return id;
        }

        void Layout(RailStation st)
        {
            float h = st.half, H = st.height;
            var pl = ports[st.id] = new List<int>();
            int N(float u, float v) => NewNode(st.World(u, v));
            byte F = 20, Y = 6;
            switch (st.tier)
            {
                case StationTier.Halt:
                {
                    int w = N(-h - 24f, 0f), e = N(h + 24f, 0f);
                    Line(Run(st, -h - 24f, h + 24f, u => 0f), w, e, TrackKind.Platform, st);
                    pl.Add(w); pl.Add(e);
                    st.platforms.Add(new Vector4(-h, h, 1.65f, 4.6f));
                    AddPad(st, -h - 8f, h + 8f, -4f, 11f, H, Y, 0);
                    AddPad(st, -6f, 6f, 4.6f, 11f, H, F, 1);
                    break;
                }
                case StationTier.Through:
                {
                    int pw = N(-h - 50f, 0f), sw = N(-h - 20f, 0f), se = N(h + 20f, 0f), pe = N(h + 50f, 0f);
                    Line(Run(st, -h - 50f, -h - 20f, u => 0f), pw, sw, TrackKind.Main);
                    Line(Run(st, -h - 20f, h + 20f, u => 0f), sw, se, TrackKind.Platform, st);
                    Line(Run(st, h + 20f, h + 50f, u => 0f), se, pe, TrackKind.Main);
                    Line(Run(st, -h - 20f, h + 20f, u => -5f * Ease((u + h + 20f) / 26f) * Ease((h + 20f - u) / 26f)), sw, se, TrackKind.Loop, st);
                    pl.Add(pw); pl.Add(pe);
                    st.platforms.Add(new Vector4(-h, h, 1.65f, 5.2f));
                    st.platforms.Add(new Vector4(-h + 6f, h - 6f, -10.2f, -6.65f));
                    AddPad(st, -h - 52f, h + 52f, -14f, 18f, H, Y, 0);
                    AddPad(st, -14f, 14f, 5.2f, 18f, H, F, 1);
                    break;
                }
                case StationTier.Terminal:
                {
                    int p = N(-h - 60f, 0f), c2 = N(-h - 52f, 0f), c = N(-h - 44f, 0f), b = N(-h - 28f, 0f), a = N(-h - 14f, 0f);
                    int b0 = N(h, 0f), b1 = N(h, 6.5f), b2 = N(h, -6.5f), bs = N(-h + 20f, -15f), bd = N(-h + 6f, 14f);
                    Line(Run(st, -h - 60f, -h - 52f, u => 0f), p, c2, TrackKind.Main);
                    Line(Run(st, -h - 52f, -h - 44f, u => 0f), c2, c, TrackKind.Main);
                    Line(Run(st, -h - 44f, -h - 28f, u => 0f), c, b, TrackKind.Main);
                    Line(Run(st, -h - 28f, -h - 14f, u => 0f), b, a, TrackKind.Main);
                    Line(Run(st, -h - 14f, h, u => 0f), a, b0, TrackKind.Platform, st);
                    Line(Run(st, -h - 14f, h, u => 6.5f * Ease((u + h + 14f) / 20f)), a, b1, TrackKind.Platform, st);
                    Line(Run(st, -h - 28f, h, u => -6.5f * Ease((u + h + 28f) / 22f)), b, b2, TrackKind.Platform, st);
                    Line(Run(st, -h - 44f, -h + 20f, u => -15f * Ease((u + h + 44f) / 30f)), c, bs, TrackKind.Siding);
                    Line(Run(st, -h - 52f, -h + 6f, u => 14f * Ease((u + h + 52f) / 26f)), c2, bd, TrackKind.Siding);
                    pl.Add(p);
                    st.platforms.Add(new Vector4(-h + 8f, h, 1.65f, 4.85f));
                    st.platforms.Add(new Vector4(-h + 8f, h, -4.85f, -1.65f));
                    st.platforms.Add(new Vector4(-h + 8f, h, 8.15f, 11.5f));
                    AddPad(st, -h - 66f, h + 18f, -22f, 20f, H, Y, 0);
                    AddPad(st, h - 2f, h + 18f, -16f, 16f, H, F, 1);
                    break;
                }
            }
            foreach (var pf in st.platforms) AddPad(st, pf.x, pf.y, pf.z, pf.w, H, F, 2);
        }

        // ------------------------------------------------------------------ connections

        struct End { public int node; public Vector3 pos; public Vector2 heading; public bool branch; }

        /// <summary>An end of a new line at station port <paramref name="port"/>: the port itself while free, else a
        /// junction 40 m out on the line already there.</summary>
        End AtPort(int port, RailStation st)
        {
            var n = nodes[port];
            var outward = new Vector2(n.pos.x - st.pos.x, n.pos.z - st.pos.y);
            var u = Vector2.Dot(outward, st.dir) >= 0f ? st.dir : -st.dir;
            if (n.ends.Count < 2) return new End { node = port, pos = n.pos, heading = u };
            foreach (var e in n.ends)
            {
                var l = lines[e.x];
                if (l.kind != TrackKind.Main || l.station >= 0 || l.Length < 90f) continue;
                if (StationOfNode(l, e.y, st)) continue;
                float s = e.y == 0 ? 40f : l.Length - 40f;
                var tan = l.Outward(e.y);
                int j = Split(l, s);
                return new End { node = j, pos = nodes[j].pos, heading = new Vector2(tan.x, tan.z), branch = true };
            }
            return new End { node = port, pos = n.pos, heading = u };
        }

        bool StationOfNode(RailLine l, int end, RailStation st) => false;

        static long Pair(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ------------------------------------------------------------------ landmasses (coarse flood fill)
        const float LandCell = 32f;
        int[] land; int landW, landH; Vector2 landMin;

        /// <summary>Labels the habitable land (continent noise ≥ 0.54 off the ice caps and the date line, what the routing
        /// may cross) on a 32 m grid round the stations, so stations join whenever there is a way over land, not only a
        /// straight dry line.</summary>
        void Lands(List<RailStation> pass)
        {
            Vector2 mn = pass[0].pos, mx = pass[0].pos;
            foreach (var st in pass) { mn = Vector2.Min(mn, st.pos); mx = Vector2.Max(mx, st.pos); }
            mn -= Vector2.one * 800f; mx += Vector2.one * 800f;
            landMin = mn; landW = Mathf.CeilToInt((mx.x - mn.x) / LandCell) + 1; landH = Mathf.CeilToInt((mx.y - mn.y) / LandCell) + 1;
            land = new int[landW * landH];
            for (int j = 0; j < landH; j++)
            for (int i = 0; i < landW; i++)
                land[j * landW + i] = world.Habitable(mn.x + i * LandCell, mn.y + j * LandCell, 0.54f) ? 0 : -1;   // as the routing sees it (no ice caps, no date line)
            int label = 0; var q = new Queue<int>();
            for (int k = 0; k < land.Length; k++)
            {
                if (land[k] != 0) continue;
                label++; land[k] = label; q.Enqueue(k);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(), ci = c % landW, cj = c / landW;
                    if (ci > 0 && land[c - 1] == 0) { land[c - 1] = label; q.Enqueue(c - 1); }
                    if (ci < landW - 1 && land[c + 1] == 0) { land[c + 1] = label; q.Enqueue(c + 1); }
                    if (cj > 0 && land[c - landW] == 0) { land[c - landW] = label; q.Enqueue(c - landW); }
                    if (cj < landH - 1 && land[c + landW] == 0) { land[c + landW] = label; q.Enqueue(c + landW); }
                }
            }
        }

        /// <summary>The landmass label under <paramref name="p"/> (nearest land cell within 3 cells; −1 = sea).</summary>
        public int LandOf(Vector2 p)
        {
            if (land == null) return -1;
            int i0 = Mathf.RoundToInt((p.x - landMin.x) / LandCell), j0 = Mathf.RoundToInt((p.y - landMin.y) / LandCell);
            for (int r = 0; r <= 3; r++)
            for (int dj = -r; dj <= r; dj++)
            for (int di = -r; di <= r; di++)
            {
                int i = i0 + di, j = j0 + dj;
                if (i < 0 || j < 0 || i >= landW || j >= landH) continue;
                int v = land[j * landW + i];
                if (v > 0) return v;
            }
            return -1;
        }

        bool Dry(Vector2 a, Vector2 b)
        {
            for (int i = 1; i < 24; i++) { var p = Vector2.Lerp(a, b, i / 24f); if (world.ContinentNoise(p.x, p.y) < 0.54f) return false; }
            return true;
        }

        int PortToward(RailStation st, Vector2 target)
        {
            int best = -1; float bd = -9f;
            foreach (int p in ports[st.id])
            {
                var n = nodes[p];
                var out2 = new Vector2(n.pos.x - st.pos.x, n.pos.z - st.pos.y).normalized;
                float d = Vector2.Dot(out2, (target - st.pos).normalized) - nodes[p].ends.Count * 0.3f;
                if (d > bd) { bd = d; best = p; }
            }
            return best;
        }

        void Connect()
        {
            var pass = new List<RailStation>();
            foreach (var st in stations) if (st.Passenger) pass.Add(st);
            if (pass.Count < 2) return;
            Lands(pass);
            var inTree = new List<RailStation> { pass[0] };
            var outTree = new List<RailStation>(pass); outTree.RemoveAt(0);
            var failed = new HashSet<long>();
            while (outTree.Count > 0)
            {
                float best = float.MaxValue; RailStation bi = null, bo = null;
                foreach (var i in inTree)
                foreach (var o in outTree)
                {
                    float d = (i.pos - o.pos).sqrMagnitude;
                    if (d < best && LandOf(i.pos) == LandOf(o.pos) && !failed.Contains(Pair(i.id, o.id))) { best = d; bi = i; bo = o; }
                }
                if (bo == null) { inTree.Add(outTree[0]); outTree.RemoveAt(0); continue; }             // another landmass: a network of its own
                float dStation = Mathf.Sqrt(best);
                // a junction out on the line when the network passes much nearer than any station
                RailLine jl = null; float js = 0f, jd = float.MaxValue;
                foreach (var l in lines)
                {
                    if (l.dead || l.kind != TrackKind.Main || l.station >= 0 || l.Length < 400f) continue;
                    for (int k = 20; k < l.pts.Length - 20; k += 4)
                    {
                        if ((l.flags[k] & (RailLine.Bridge | RailLine.Crossing)) != 0) continue;
                        float d = (new Vector2(l.pts[k].x, l.pts[k].z) - bo.pos).magnitude;
                        if (d < jd) { jd = d; jl = l; js = l.cum[k]; }
                    }
                }
                bool junction = jl != null && jd < dStation * 0.62f && jd > 260f && FarFromStations(jl.At(js), 220f) && Dry(bo.pos, new Vector2(jl.At(js).x, jl.At(js).z));
                if (junction)
                {
                    var jp = jl.At(js, out var jt);
                    var toward = new Vector2(bo.pos.x - jp.x, bo.pos.y - jp.z);
                    var head = new Vector2(jt.x, jt.z).normalized;
                    if (Vector2.Dot(head, toward) < 0f) head = -head;
                    int jn = Split(jl, js);
                    var from = AtPort(PortToward(bo, new Vector2(jp.x, jp.z)), bo);
                    if (Route(from, new End { node = jn, pos = nodes[jn].pos, heading = head, branch = true }))
                    {
                        var js2 = new RailStation { id = stations.Count, tier = StationTier.Junction, pos = new Vector2(jp.x, jp.z), dir = head, height = jp.y, half = 6f };
                        js2.side = new Vector2(head.y, -head.x);
                        if (Vector2.Dot(js2.side, toward) > 0f) js2.side = -js2.side;                   // the box on the side away from the branch
                        js2.name = StopA[rnd.Next(StopA.Length)] + " " + (rnd.NextDouble() < 0.5 ? "JUNCTION" : "FORK");
                        stations.Add(js2);
                        AddPad(js2, -5f, 5f, 4f, 11f, js2.height, 6, 0);
                        joined.Add(Pair(bo.id, bi.id));
                        inTree.Add(bo); outTree.Remove(bo);
                        continue;
                    }
                }
                if (Route(AtPort(PortToward(bo, bi.pos), bo), AtPort(PortToward(bi, bo.pos), bi))) { joined.Add(Pair(bo.id, bi.id)); inTree.Add(bo); outTree.Remove(bo); }
                else failed.Add(Pair(bo.id, bi.id));                                            // no way through: try the next nearest pair
            }
            // a few loops so there is more than one way round
            foreach (var st in pass)
            {
                if (rnd.NextDouble() > 0.3) continue;
                RailStation bj = null; float bd = 1900f * 1900f;
                foreach (var o in pass)
                {
                    if (o == st || joined.Contains(Pair(st.id, o.id))) continue;
                    float d = (o.pos - st.pos).sqrMagnitude;
                    if (d < bd && d > 500f * 500f && Dry(st.pos, o.pos)) { bd = d; bj = o; }
                }
                if (bj == null) continue;
                if (Route(AtPort(PortToward(st, bj.pos), st), AtPort(PortToward(bj, st.pos), bj))) joined.Add(Pair(st.id, bj.id));
            }
        }

        bool FarFromStations(Vector3 p, float d)
        {
            foreach (var st in stations) if ((st.pos - new Vector2(p.x, p.z)).sqrMagnitude < d * d) return false;
            return true;
        }

        /// <summary>A lead out of an end: straight on from a free port, a gentle curve away from a junction.</summary>
        static List<Vector2> Lead(End e, Vector2 toward)
        {
            var o = new List<Vector2>();
            var p = new Vector2(e.pos.x, e.pos.z);
            if (!e.branch) { o.Add(p + e.heading * 12f); o.Add(p + e.heading * 26f); return o; }
            float side = Mathf.Sign(e.heading.x * (toward.y - p.y) - e.heading.y * (toward.x - p.x));   // +1 left
            const float R = 140f;
            var nrm = new Vector2(-e.heading.y, e.heading.x) * side;
            var c = p + nrm * R;
            for (int i = 1; i <= 9; i++)
            {
                float a = i * 4f / R;
                o.Add(c - nrm * R * Mathf.Cos(a) + e.heading * R * Mathf.Sin(a));
            }
            return o;
        }

        const float GridCell = 24f;

        /// <summary>Route and lay a main line between two ends (false: no way found).</summary>
        bool Route(End a, End b)
        {
            if (a.node == b.node) return false;
            var leadA = Lead(a, new Vector2(b.pos.x, b.pos.z));
            var leadB = Lead(b, new Vector2(a.pos.x, a.pos.z));
            var pa = leadA[leadA.Count - 1]; var pb = leadB[leadB.Count - 1];
            var mid = AStar(pa, pb, 320f) ?? AStar(pa, pb, 1400f);                                 // round a bay or a lake: a wider search
            if (mid == null) return false;
            var poly = new List<Vector2> { new Vector2(a.pos.x, a.pos.z) };
            poly.AddRange(leadA);
            poly.AddRange(mid);
            for (int i = leadB.Count - 1; i >= 0; i--) poly.Add(leadB[i]);
            poly.Add(new Vector2(b.pos.x, b.pos.z));
            for (int it = 0; it < 4; it++)
            {
                var n = new List<Vector2> { poly[0] };
                for (int i = 0; i < poly.Count - 1; i++) { n.Add(Vector2.Lerp(poly[i], poly[i + 1], 0.25f)); n.Add(Vector2.Lerp(poly[i], poly[i + 1], 0.75f)); }
                n.Add(poly[poly.Count - 1]);
                poly = n;
            }
            var pts2 = Resample(poly, Step);
            if (pts2.Count < 3) return false;
            var pts = Profile(pts2, a.pos.y, b.pos.y, out var flags);
            pts[0] = a.pos; pts[pts.Count - 1] = b.pos;
            var line = new RailLine(pts.ToArray(), TrackKind.Main, flags);
            AddLine(line, a.node, b.node);
            return true;
        }

        static List<Vector2> Resample(List<Vector2> poly, float step)
        {
            var o = new List<Vector2> { poly[0] };
            float acc = 0f;
            for (int i = 1; i < poly.Count; i++)
            {
                var a = poly[i - 1]; var b = poly[i];
                float d = Vector2.Distance(a, b);
                while (d > 0f && acc + d >= step)
                {
                    float t = (step - acc) / d;
                    a = Vector2.Lerp(a, b, t); d = Vector2.Distance(a, b); acc = 0f;
                    o.Add(a);
                }
                acc += d;
            }
            if (Vector2.Distance(o[o.Count - 1], poly[poly.Count - 1]) < step * 0.4f && o.Count > 1) o[o.Count - 1] = poly[poly.Count - 1];
            else o.Add(poly[poly.Count - 1]);
            return o;
        }

        /// <summary>Bed heights: the land smoothed over ~80 m, pinned to both ends and to the roads crossed, grades held
        /// under <see cref="MaxGrade"/>, lifted over rivers (bridges); bridge and crossing flags.</summary>
        List<Vector3> Profile(List<Vector2> p, float h0, float h1, out byte[] flags)
        {
            int n = p.Count;
            var raw = new float[n]; flags = new byte[n];
            var pin = new float[n]; var lift = new float[n];
            for (int i = 0; i < n; i++)
            {
                raw[i] = world.BaseHeight(p[i].x, p[i].y);
                pin[i] = float.NaN; lift[i] = float.NegativeInfinity;
                if (world.roads.Query(p[i].x, p[i].y, out var hit) && hit.dist < hit.width * 0.5f + 1f) { flags[i] |= RailLine.Crossing; pin[i] = hit.height; }
                if (OnCrawlerway(p[i].x, p[i].y)) { flags[i] |= RailLine.Crossing; pin[i] = world.Sample(p[i].x, p[i].y).height; }   // on the crawler's bed as graded
                if (world.RiverAt(p[i].x, p[i].y, out float level, out float half, out float dist) && dist < half + 4f) { flags[i] |= RailLine.Bridge; lift[i] = level + 2.6f; pin[i] = float.NaN; }
            }
            pin[0] = h0; pin[n - 1] = h1;
            var prof = new float[n];
            const int win = 10;
            for (int i = 0; i < n; i++)
            {
                float sum = 0f, ws = 0f;
                for (int k = -win; k <= win; k++) { int j = Mathf.Clamp(i + k, 0, n - 1); float w = 1f - Mathf.Abs(k) / (win + 1f); sum += raw[j] * w; ws += w; }
                prof[i] = sum / ws;
            }
            for (int i = 0; i < Mathf.Min(8, n); i++) { float t = i / 8f; prof[i] = Mathf.Lerp(h0, prof[i], t); prof[n - 1 - i] = Mathf.Lerp(h1, prof[n - 1 - i], t); }
            for (int pass = 0; pass < 6; pass++)
            {
                for (int i = 0; i < n; i++) { if (!float.IsNaN(pin[i])) prof[i] = pin[i]; prof[i] = Mathf.Max(prof[i], lift[i]); }
                for (int i = 1; i < n; i++) { float lim = MaxGrade * Step; prof[i] = Mathf.Clamp(prof[i], prof[i - 1] - lim, prof[i - 1] + lim); }
                for (int i = n - 2; i >= 0; i--) { float lim = MaxGrade * Step; prof[i] = Mathf.Clamp(prof[i], prof[i + 1] - lim, prof[i + 1] + lim); }
            }
            for (int i = 0; i < n; i++) { if (!float.IsNaN(pin[i])) prof[i] = pin[i]; prof[i] = Mathf.Max(prof[i], Mathf.Max(lift[i], WorldGen.SeaLevel + 1.5f)); }
            // viaducts where the bed runs well above the land
            for (int i = 1; i < n - 1; i++) if (prof[i] - raw[i] > 5.5f) flags[i] |= RailLine.Bridge;
            var dil = (byte[])flags.Clone();
            for (int i = 1; i < n - 1; i++) if ((flags[i - 1] & RailLine.Bridge) != 0 || (flags[i + 1] & RailLine.Bridge) != 0) dil[i] |= RailLine.Bridge;
            flags = dil;
            flags[0] &= unchecked((byte)~RailLine.Bridge); flags[n - 1] &= unchecked((byte)~RailLine.Bridge);
            var o = new List<Vector3>(n);
            for (int i = 0; i < n; i++) o.Add(new Vector3(p[i].x, prof[i], p[i].y));
            return o;
        }

        // ------------------------------------------------------------------ A*

        List<Vector2> AStar(Vector2 from, Vector2 to, float margin)
        {
            var min = Vector2.Min(from, to) - Vector2.one * margin;
            var max = Vector2.Max(from, to) + Vector2.one * margin;
            // only what can lie in the box
            var towns = new List<(Vector2 p, float r)>(); var stops = new List<(Vector2 p, float r)>();
            foreach (var s in world.settlements)
            {
                float r = (s.plan != null ? s.plan.radius : s.radius) + 8f;
                if (s.pos.x + r > min.x && s.pos.x - r < max.x && s.pos.y + r > min.y && s.pos.y - r < max.y) towns.Add((s.pos, r));
            }
            foreach (var st in stations)
            {
                float r = st.half + 30f;
                if ((st.pos - from).sqrMagnitude < (r + 60f) * (r + 60f) || (st.pos - to).sqrMagnitude < (r + 60f) * (r + 60f)) r = st.half * 0.9f + 8f;   // our own ends: just not across the platforms
                if (st.pos.x + r > min.x && st.pos.x - r < max.x && st.pos.y + r > min.y && st.pos.y - r < max.y) stops.Add((st.pos, r));
            }
            int w = Mathf.CeilToInt((max.x - min.x) / GridCell) + 1, h = Mathf.CeilToInt((max.y - min.y) / GridCell) + 1;
            if (w * h > 400000) return null;
            var cost = new float[w * h]; var hgt = new float[w * h];
            for (int i = 0; i < cost.Length; i++) cost[i] = -1f;
            Vector2 C(int i) => new Vector2(min.x + (i % w) * GridCell, min.y + (i / w) * GridCell);
            int I(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y - min.y) / GridCell), 0, h - 1) * w + Mathf.Clamp(Mathf.RoundToInt((p.x - min.x) / GridCell), 0, w - 1);
            float Cost(int i)
            {
                if (cost[i] >= 0f || float.IsPositiveInfinity(cost[i])) return cost[i];
                var p = C(i);
                float c = 1f;
                hgt[i] = world.BaseHeight(p.x, p.y);
                if (!world.Habitable(p.x, p.y, 0.53f) || hgt[i] < WorldGen.SeaLevel + 1f) c = float.PositiveInfinity;
                else if (world.LakeAt(p.x, p.y, out float t) != null && t < 1.35f) c = float.PositiveInfinity;
                else if (world.SiteAt(p.x, p.y) != null || BlockedRoute(p.x, p.y)) c = float.PositiveInfinity;
                else
                {
                    foreach (var (sp, r) in towns) if ((sp - p).sqrMagnitude < r * r) { c = float.PositiveInfinity; break; }
                    if (!float.IsPositiveInfinity(c))
                        foreach (var (sp, r) in stops) if ((sp - p).sqrMagnitude < r * r) { c = float.PositiveInfinity; break; }
                    if (!float.IsPositiveInfinity(c))
                    {
                        if (OnCrawlerway(p.x, p.y)) c += 25f;                                    // a level crossing of the crawlerway: cross it, don't run along it
                        if (world.RiverAt(p.x, p.y, out _, out _, out _)) c += 30f;
                        if (world.roads.Query(p.x, p.y, out var hit) && hit.dist < hit.width * 0.5f + 4f) c += 5f;
                    }
                }
                cost[i] = c;
                return c;
            }
            int start = I(from), goal = I(to);
            cost[start] = 1f; hgt[start] = world.BaseHeight(C(start).x, C(start).y);
            cost[goal] = 1f; hgt[goal] = world.BaseHeight(C(goal).x, C(goal).y);
            var g = new float[w * h]; var came = new int[w * h];
            for (int i = 0; i < g.Length; i++) { g[i] = float.MaxValue; came[i] = -1; }
            var heap = new MinHeap();
            g[start] = 0f; heap.Push(start, 0f);
            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int expanded = 0;
            while (heap.Count > 0 && expanded < 250000)
            {
                int cur = heap.Pop(out float f);
                if (cur == goal) break;
                expanded++;
                int cx = cur % w, cz = cur / w;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + dx[k], nz = cz + dz[k];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int ni = nz * w + nx;
                    float c = Cost(ni);
                    if (float.IsPositiveInfinity(c)) continue;
                    float len = k < 4 ? GridCell : GridCell * 1.4142f;
                    float slope = Mathf.Abs(hgt[ni] - hgt[cur]) / len;
                    float step = len * (c + slope * 60f + Mathf.Max(0f, slope - 0.04f) * 400f);
                    float ng = g[cur] + step;
                    if (ng >= g[ni]) continue;
                    g[ni] = ng; came[ni] = cur;
                    heap.Push(ni, ng + (C(ni) - to).magnitude);
                }
            }
            if (came[goal] < 0 && goal != start) return null;
            var cells = new List<int>();
            for (int c = goal; c >= 0; c = came[c]) { cells.Add(c); if (c == start) break; }
            cells.Reverse();
            // string-pull: skip cells while the straight line stays clear and gentle
            var o = new List<Vector2>();
            int anchor = 0;
            o.Add(from);
            while (anchor < cells.Count - 1)
            {
                int far = anchor + 1;
                for (int j = cells.Count - 1; j > anchor + 1; j--) if (Clear(C(cells[anchor]), C(cells[j]), Cost, I, hgt)) { far = j; break; }
                if (far < cells.Count - 1) o.Add(C(cells[far]));
                anchor = far;
            }
            o.Add(to);
            return o;
        }

        static bool Clear(Vector2 a, Vector2 b, System.Func<int, float> cost, System.Func<Vector2, int> index, float[] hgt)
        {
            float d = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(d / (GridCell * 0.5f)));
            float prev = float.NaN;
            for (int i = 0; i <= n; i++)
            {
                int c = index(Vector2.Lerp(a, b, i / (float)n));
                float k = cost(c);
                if (float.IsPositiveInfinity(k) || k > 6f) return false;
                if (!float.IsNaN(prev) && Mathf.Abs(hgt[c] - prev) / (GridCell * 0.5f) > 0.05f) return false;
                prev = hgt[c];
            }
            return true;
        }

        internal sealed class MinHeap
        {
            readonly List<int> ids = new List<int>(); readonly List<float> keys = new List<float>();
            public int Count => ids.Count;
            public void Push(int id, float k)
            {
                ids.Add(id); keys.Add(k);
                int i = ids.Count - 1;
                while (i > 0) { int p = (i - 1) >> 1; if (keys[p] <= keys[i]) break; Swap(i, p); i = p; }
            }
            public int Pop(out float k)
            {
                int top = ids[0]; k = keys[0];
                int last = ids.Count - 1;
                ids[0] = ids[last]; keys[0] = keys[last]; ids.RemoveAt(last); keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < ids.Count && keys[l] < keys[m]) m = l;
                    if (r < ids.Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
                return top;
            }
            void Swap(int a, int b) { int t = ids[a]; ids[a] = ids[b]; ids[b] = t; float f = keys[a]; keys[a] = keys[b]; keys[b] = f; }
        }

        // ------------------------------------------------------------------ water stops

        void WaterStops()
        {
            int count = lines.Count;
            for (int li = 0; li < count; li++)
            {
                var l = lines[li];
                if (l.dead || l.kind != TrackKind.Main || l.station >= 0 || l.Length < 1500f) continue;
                int best = -1; float bestTurn = 9f;
                for (int k = l.pts.Length / 3; k < l.pts.Length * 2 / 3; k += 2)
                {
                    if (k < 10 || k > l.pts.Length - 11) continue;
                    bool flagged = false;
                    for (int j = k - 10; j <= k + 10; j++) if (l.flags[j] != 0) { flagged = true; break; }
                    if (flagged) continue;
                    var d0 = l.pts[k] - l.pts[k - 8]; var d1 = l.pts[k + 8] - l.pts[k]; d0.y = d1.y = 0f;
                    float turn = Vector3.Angle(d0, d1) + Mathf.Abs(l.pts[k + 8].y - l.pts[k - 8].y) * 4f;
                    if (turn < bestTurn && FarFromStations(l.pts[k], 450f)) { bestTurn = turn; best = k; }
                }
                if (best < 0 || bestTurn > 4f) continue;
                var p = l.At(l.cum[best], out var t);
                var st = new RailStation { id = stations.Count, tier = StationTier.WaterStop, pos = new Vector2(p.x, p.z), height = p.y, half = 15f };
                st.dir = new Vector2(t.x, t.z).normalized;
                st.side = new Vector2(st.dir.y, -st.dir.x);
                var probe = st.World(0f, 8f); var probe2 = st.World(0f, -8f);
                if (Mathf.Abs(world.BaseHeight(probe2.x, probe2.z) - p.y) < Mathf.Abs(world.BaseHeight(probe.x, probe.z) - p.y)) st.side = -st.side;
                st.name = StopA[rnd.Next(StopA.Length)] + " " + StopB[rnd.Next(StopB.Length)];
                st.platforms.Add(new Vector4(-14f, 14f, 1.65f, 4.2f));
                stations.Add(st);
                AddPad(st, -20f, 20f, 2.6f, 13f, p.y, 6, 0);
                AddPad(st, -14f, 14f, 1.65f, 4.2f, p.y, 20, 2);
            }
        }
    }
}
