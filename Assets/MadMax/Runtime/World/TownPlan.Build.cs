using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Plan generation. Villages: a paved high street on the through road (a cross street when a highway comes
    /// in sideways), dirt lanes, a green with a well, farm lots with fields. Towns: boulevards on the highways meeting at
    /// a central roundabout, a grid of local streets with sidewalks, a market square and a park round the middle,
    /// alleys behind back-to-back lots, a fuel station on a boulevard. Cities: wider boulevards, a ring road with
    /// roundabouts where the boulevards cross it, a dense core of mid-rise blocks, shops and car parks, a plaza with a
    /// pedestrian street, a park with a pond, residential and industrial edges, a gang hangout.</summary>
    public sealed partial class TownPlan
    {
        class Line
        {
            public bool alongU, keep, paved = true, path;
            public float c, s0, s1, half, walk;
            public StreetKind kind;
            public readonly List<float> cuts = new List<float>();
            public Vector2 At(float s) => alongU ? new Vector2(s, c) : new Vector2(c, s);
        }

        struct Round { public Vector2 p; public float island, ring; }
        struct Block { public Vector2 min, max; public Line l, r, b, t; }

        readonly List<Round> rounds = new List<Round>();
        readonly bool[] axisRoad = new bool[4];
        readonly List<Line> lines = new List<Line>();
        readonly List<Block> blocks = new List<Block>();
        readonly List<(Vector2 a, Vector2 b, float r)> roadSegs = new List<(Vector2, Vector2, float)>();
        float bu, bv, ringU, ringV, indAngle;
        bool centreRound;
        int gasStations;

        public const float CutOut = 30f, Bypass = 15f;

        public TownPlan(WorldGen w, Settlement st, float angle, float maxOuter)
        {
            this.st = st; centre = st.pos; this.angle = angle; radius = st.radius;
            U = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); V = new Vector2(-U.y, U.x);
            seed = w.seed * 7919 + st.index * 104729 + 17;
            // street level: a least-squares plane through the land, slope capped (cities flat, villages follow the land)
            float sh = 0f, suh = 0f, svh = 0f, suu = 0f, svv = 0f; int cnt = 0;
            for (int i = -4; i <= 4; i++)
            for (int j = -4; j <= 4; j++)
            {
                var l = new Vector2(i, j) * (radius * 0.9f / 4f);
                if (l.magnitude > radius * 0.95f) continue;
                var p = World(l);
                float hb = w.BaseHeight(p.x, p.y);
                sh += hb; suh += l.x * hb; svh += l.y * hb; suu += l.x * l.x; svv += l.y * l.y; cnt++;
            }
            var g = new Vector2(suh / Mathf.Max(1f, suu), svh / Mathf.Max(1f, svv));
            float maxSlope = st.kind == Biome.City ? 0.015f : st.kind == Biome.Town ? 0.025f : 0.04f;
            if (g.magnitude > maxSlope) g = g.normalized * maxSlope;
            h0 = Mathf.Max(sh / Mathf.Max(1, cnt), WorldGen.SeaLevel + 1.5f); gu = g.x; gv = g.y;
            float dev = 0f;
            for (int k = 0; k < 16; k++)
            {
                var l = new Vector2(Mathf.Cos(k * Mathf.PI / 8f), Mathf.Sin(k * Mathf.PI / 8f)) * (radius + 15f);
                var p = World(l);
                dev = Mathf.Max(dev, Mathf.Abs(w.BaseHeight(p.x, p.y) - Level(l)));
            }
            shoulder = Mathf.Min(Mathf.Clamp(22f + dev * 3f, 22f, 50f), Mathf.Max(12f, maxOuter - radius));
            Skeleton();
        }

        void Skeleton()
        {
            var rnd = new System.Random(seed ^ 0x51ab);
            indAngle = (float)rnd.NextDouble() * Mathf.PI * 2f;
            if (st.kind == Biome.Village) return;
            centreRound = true;
            if (st.kind == Biome.City)
            {
                bu = Mathf.Min(42f, (radius - 6f) / 2.75f); bv = bu * 0.95f; ringU = 2f * bu; ringV = 2f * bv;
                rounds.Add(new Round { p = Vector2.zero, island = 6f, ring = 13.5f });
                foreach (var p in new[] { new Vector2(ringU, 0f), new Vector2(-ringU, 0f), new Vector2(0f, ringV), new Vector2(0f, -ringV) })
                    rounds.Add(new Round { p = p, island = 3.5f, ring = 10f });
            }
            else
            {
                bu = Mathf.Clamp(radius * 0.52f, 32f, 44f); bv = bu * 0.9f;
                rounds.Add(new Round { p = Vector2.zero, island = 4.5f, ring = 11.5f });
            }
        }

        // ------------------------------------------------------------------ highways through the plan

        public static Vector2 Axis(int k) => k == 0 ? Vector2.right : k == 1 ? Vector2.up : k == 2 ? Vector2.left : Vector2.down;
        public static int AxisOf(Vector2 l) { int k = Mathf.RoundToInt(Mathf.Atan2(l.y, l.x) / (Mathf.PI * 0.5f)); return ((k % 4) + 4) % 4; }
        public void MarkAxis(int k) => axisRoad[k] = true;

        static void Arc(List<Vector2> pts, Vector2 c, float r, float a0, float a1)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r / 3f));
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (pts.Count == 0 || (pts[pts.Count - 1] - p).sqrMagnitude > 0.01f) pts.Add(p);
            }
        }

        static void Straight(List<Vector2> pts, Vector2 d, float s0, float s1)
        {
            if (s1 <= s0) return;
            int n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 4f));
            for (int i = 1; i <= n; i++) pts.Add(d * Mathf.Lerp(s0, s1, i / (float)n));
        }

        /// <summary>Highway centre line inside the plan (local): from the hub on the central roundabout (anticlockwise
        /// round it), out along boulevard <paramref name="k"/>, half round any roundabout on the way, to the edge.</summary>
        public List<Vector2> PathOut(int k)
        {
            var d = Axis(k);
            var pts = new List<Vector2>();
            float s = 0f;
            if (centreRound)
            {
                var r0 = rounds[0]; float rr = (r0.island + r0.ring) * 0.5f;
                Arc(pts, Vector2.zero, rr, 0f, k * Mathf.PI * 0.5f);
                s = rr;
            }
            else pts.Add(Vector2.zero);
            for (int i = 1; i < rounds.Count; i++)
            {
                var ro = rounds[i];
                float dc = Vector2.Dot(ro.p, d);
                if (dc <= s || Mathf.Abs(d.x * ro.p.y - d.y * ro.p.x) > 0.5f) continue;
                float rr = (ro.island + ro.ring) * 0.5f;
                Straight(pts, d, s, dc - rr);
                float a0 = Mathf.Atan2(-d.y, -d.x);
                Arc(pts, ro.p, rr, a0, a0 + Mathf.PI);
                s = dc + rr;
            }
            Straight(pts, d, s, radius + 2f);
            return pts;
        }

        /// <summary>From the end of boulevard <paramref name="k"/> out to a highway point <paramref name="c"/> (local):
        /// straight when it lies ahead, else round the town on a bypass arc outside the lots. End points excluded.</summary>
        public List<Vector2> Connector(int k, Vector2 c)
        {
            var b = Axis(k) * (radius + 2f);
            var raw = new List<Vector2> { b };
            float ak = k * Mathf.PI * 0.5f, ac = Mathf.Atan2(c.y, c.x);
            float diff = Mathf.DeltaAngle(ak * Mathf.Rad2Deg, ac * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (Mathf.Abs(diff) > 10f * Mathf.Deg2Rad)
            {
                float rb = radius + Bypass;
                raw.Add(Axis(k) * rb);
                Arc(raw, Vector2.zero, rb, ak, ak + diff);
                if (c.magnitude > rb + 2f) raw.Add(new Vector2(Mathf.Cos(ac), Mathf.Sin(ac)) * (rb + 2f));
            }
            raw.Add(c);
            for (int pass = 0; pass < 2; pass++)
            {
                var next = new List<Vector2> { raw[0] };
                for (int i = 0; i < raw.Count - 1; i++) { next.Add(Vector2.Lerp(raw[i], raw[i + 1], 0.25f)); next.Add(Vector2.Lerp(raw[i], raw[i + 1], 0.75f)); }
                next.Add(raw[raw.Count - 1]);
                raw = next;
            }
            // even spacing (~4 m) without the end points
            var o = new List<Vector2>();
            float acc = 0f;
            for (int i = 1; i < raw.Count - 1; i++) { acc += (raw[i] - raw[i - 1]).magnitude; if (acc >= 3.5f) { o.Add(raw[i]); acc = 0f; } }
            return o;
        }

        // ------------------------------------------------------------------ build

        /// <summary>Streets, lots and everything on them (after the highways are rerouted and the start yard is known).</summary>
        public void Build(WorldGen w)
        {
            var rnd = new System.Random(seed);
            foreach (var road in w.roads.roads)
            {
                var pts = road.points;
                for (int i = 1; i < pts.Count; i++)
                {
                    var a = new Vector2(pts[i - 1].x, pts[i - 1].z); var b = new Vector2(pts[i].x, pts[i].z);
                    if ((a - centre).sqrMagnitude > (radius + 20f) * (radius + 20f) && (b - centre).sqrMagnitude > (radius + 20f) * (radius + 20f)) continue;
                    roadSegs.Add((Local(a.x, a.y), Local(b.x, b.y), road.width * 0.5f));
                }
            }
            if (st.kind == Biome.Village) VillageLines(rnd); else GridLines();
            Segment();
            if (st.kind == Biome.Village) VillageLots(w, rnd); else GridLots(w, rnd);
            BuildIndex();
            Dress(rnd);
        }

        Line AddLine(bool alongU, float c, float s0, float s1, StreetKind kind, float half, float walk, bool path, bool keep)
        {
            var l = new Line { alongU = alongU, c = c, s0 = s0, s1 = s1, kind = kind, half = half, walk = walk, path = path, keep = keep, paved = kind != StreetKind.Lane };
            lines.Add(l);
            return l;
        }

        void GridLines()
        {
            bool city = st.kind == Biome.City;
            float rb = radius + 2f, ah = city ? 6f : 5f, aw = city ? 3f : 2.5f, lh = city ? 3.5f : 3.25f, lw = city ? 2.5f : 2f;
            AddLine(true, 0f, -rb, rb, StreetKind.Arterial, ah, aw, true, true);
            AddLine(false, 0f, -rb, rb, StreetKind.Arterial, ah, aw, true, true);
            for (int axis = 0; axis < 2; axis++)
            {
                float step = axis == 0 ? bu : bv, ring = axis == 0 ? ringU : ringV, other = axis == 0 ? ringV : ringU;
                for (int k = 1; ; k++)
                {
                    float c = k * step;
                    if (c > radius - 12f) break;
                    foreach (float sg in new[] { 1f, -1f })
                    {
                        float cc = sg * c;
                        if (city && Mathf.Abs(c - ring) < 0.1f) AddLine(axis == 1, cc, -other, other, StreetKind.Ring, 4.5f, 2.5f, false, true);
                        else { float ext = Mathf.Sqrt(radius * radius - c * c) - 6f; AddLine(axis == 1, cc, -ext, ext, StreetKind.Local, lh, lw, false, true); }   // run on out of town
                    }
                }
            }
        }

        void VillageLines(System.Random rnd)
        {
            float rb = radius + 2f;
            AddLine(true, 0f, -rb, rb, StreetKind.Local, 3.25f, 1.5f, true, true);                   // the high street
            if (axisRoad[1] || axisRoad[3])
                AddLine(false, 0f, axisRoad[3] ? -rb : -radius * 0.4f, axisRoad[1] ? rb : radius * 0.4f, StreetKind.Local, 3f, 1.2f, true, true);
            int lanes = 1 + rnd.Next(2);
            for (int i = 0; i < lanes; i++)
            {
                float u = (i == 0 ? 1f : -1f) * (17f + (float)rnd.NextDouble() * 9f);
                bool up = rnd.Next(2) == 0;
                AddLine(false, u, up ? 0f : -radius * 0.72f, up ? radius * 0.72f : 0f, StreetKind.Lane, 2.4f, 0f, false, true);
            }
        }

        void Segment()
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var l in lines) l.cuts.Clear();
                foreach (var a in lines)
                {
                    if (!a.alongU) continue;
                    foreach (var b in lines)
                    {
                        if (b.alongU) continue;
                        float u = b.c, v = a.c;
                        if (u < a.s0 - 0.05f || u > a.s1 + 0.05f || v < b.s0 - 0.05f || v > b.s1 + 0.05f) continue;
                        a.cuts.Add(u); b.cuts.Add(v);
                    }
                }
                if (pass == 2) break;
                // local streets end at their last crossing (no stubs into the outskirts)
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    var l = lines[i];
                    if (l.keep) continue;
                    if (l.cuts.Count < 2) { lines.RemoveAt(i); continue; }
                    l.cuts.Sort(); l.s0 = l.cuts[0]; l.s1 = l.cuts[l.cuts.Count - 1];
                }
            }
            var byPos = new Dictionary<long, TownJunction>();
            TownJunction J(Vector2 p, Line x)
            {
                long key = ((long)Mathf.RoundToInt(p.x * 10f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 10f);
                if (!byPos.TryGetValue(key, out var j))
                {
                    j = new TownJunction { id = junctions.Count, p = p };
                    foreach (var r in rounds) if ((r.p - p).sqrMagnitude < 0.25f) { j.island = r.island; j.ring = r.ring; }
                    byPos[key] = j; junctions.Add(j);
                }
                j.box = Mathf.Max(j.box, x.half); j.walk = Mathf.Max(j.walk, x.walk);
                return j;
            }
            foreach (var l in lines)
            {
                var stops = new List<float> { l.s0, l.s1 };
                foreach (float c in l.cuts) stops.Add(Mathf.Clamp(c, l.s0, l.s1));
                stops.Sort();
                for (int i = 0; i + 1 < stops.Count; i++)
                {
                    float p = stops[i], q = stops[i + 1];
                    if (q - p < 0.5f) continue;
                    var s = new TownStreet { id = streets.Count, a = l.At(p), b = l.At(q), half = l.half, walk = l.walk, kind = l.kind, paved = l.paved, path = l.path };
                    s.len = q - p; s.dir = (s.b - s.a) / s.len;
                    bool cutP = false, cutQ = false;
                    foreach (float c in l.cuts) { if (Mathf.Abs(c - p) < 0.06f) cutP = true; if (Mathf.Abs(c - q) < 0.06f) cutQ = true; }
                    if (cutP) s.juncA = J(s.a, l).id;
                    if (cutQ) s.juncB = J(s.b, l).id;
                    streets.Add(s);
                }
            }
            foreach (var j in junctions)
                j.zebra = st.kind == Biome.City || (st.kind == Biome.Town && (j.ring > 0f || j.p.magnitude < radius * 0.55f));
        }

        // ------------------------------------------------------------------ lots

        static float SegRect(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            // segment against an axis-aligned rectangle: 0 when they touch
            var c = (min + max) * 0.5f; var e = (max - min) * 0.5f;
            float best = float.MaxValue;
            for (int i = 0; i <= 8; i++)
            {
                var p = Vector2.Lerp(a, b, i / 8f);
                var q = new Vector2(Mathf.Max(Mathf.Abs(p.x - c.x) - e.x, 0f), Mathf.Max(Mathf.Abs(p.y - c.y) - e.y, 0f));
                best = Mathf.Min(best, q.magnitude);
            }
            // rectangle corners against the segment (long segments past a small rectangle)
            var ab = b - a; float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            foreach (var k in new[] { min, max, new Vector2(min.x, max.y), new Vector2(max.x, min.y), c })
            {
                float t = Mathf.Clamp01(Vector2.Dot(k - a, ab) / len2);
                var p = a + ab * t;
                var q = new Vector2(Mathf.Max(Mathf.Abs(p.x - c.x) - e.x, 0f), Mathf.Max(Mathf.Abs(p.y - c.y) - e.y, 0f));
                best = Mathf.Min(best, q.magnitude);
            }
            return best;
        }

        bool Fits(WorldGen w, Vector2 min, Vector2 max, bool special)
        {
            foreach (var k in new[] { min, max, new Vector2(min.x, max.y), new Vector2(max.x, min.y) })
            {
                if (k.magnitude > radius - 1.5f) return false;
                var p = World(k);
                if (w.YardWeight(p.x, p.y) > 0f || w.Reserved(p.x, p.y)) return false;
            }
            foreach (var o in lots) if (min.x < o.max.x - 0.05f && max.x > o.min.x + 0.05f && min.y < o.max.y - 0.05f && max.y > o.min.y + 0.05f) return false;   // neighbours may share an edge
            if (special) return true;                                                           // squares and parks may round a roundabout's corner
            foreach (var s in streets)
            {
                float m = s.half + s.walk + 0.2f;
                if (Mathf.Min(s.a.x, s.b.x) > max.x + m || Mathf.Max(s.a.x, s.b.x) < min.x - m || Mathf.Min(s.a.y, s.b.y) > max.y + m || Mathf.Max(s.a.y, s.b.y) < min.y - m) continue;
                if (SegRect(s.a, s.b, min, max) < m) return false;
            }
            foreach (var r in rounds)
            {
                var q = new Vector2(Mathf.Max(min.x - r.p.x, 0f, r.p.x - max.x), Mathf.Max(min.y - r.p.y, 0f, r.p.y - max.y));
                if (q.magnitude < r.ring + 3f) return false;
            }
            foreach (var (a, b, rr) in roadSegs)
            {
                float m = rr + 1f;
                if (Mathf.Min(a.x, b.x) > max.x + m || Mathf.Max(a.x, b.x) < min.x - m || Mathf.Min(a.y, b.y) > max.y + m || Mathf.Max(a.y, b.y) < min.y - m) continue;
                if (SegRect(a, b, min, max) < m) return false;
            }
            return true;
        }

        TownLot AddLot(Vector2 min, Vector2 max, Vector2 front, LotUse use)
        {
            var lot = new TownLot { id = lots.Count, min = min, max = max, front = front, use = use };
            lots.Add(lot);
            return lot;
        }

        static float MinWidth(LotUse u) => u switch
        {
            LotUse.House => 12f, LotUse.Shop => 13.5f, LotUse.Apartment => 14.5f, LotUse.Warehouse => 16f, LotUse.GasStation => 20f,
            LotUse.Parking => 9f, LotUse.Farm => 13f, LotUse.Ruin => 8f, _ => 6f
        };

        static float MinDepth(LotUse u) => u switch
        {
            LotUse.House => 11f, LotUse.Shop => 10.2f, LotUse.Apartment => 11.6f, LotUse.Warehouse => 13.5f, LotUse.GasStation => 15f,
            LotUse.Farm => 13f, LotUse.Parking => 7f, LotUse.Shack => 6f, _ => 5f
        };

        /// <summary>How deep a lot of this use goes at most (the rest of a deep row stays open ground behind it).</summary>
        static float MaxDepth(LotUse u) => u switch
        {
            LotUse.Shack => 12f, LotUse.Empty => 14f, LotUse.Shop => 14f, LotUse.Ruin => 16f, LotUse.House => 18f, LotUse.Apartment => 18f, LotUse.Farm => 40f, _ => 22f
        };

        delegate LotUse Picker(Vector2 at, float depth, bool boulevard, System.Random rnd, out float width);

        LotUse PickGrid(Vector2 at, float depth, bool boulevard, System.Random rnd, out float width)
        {
            double r = rnd.NextDouble();
            bool city = st.kind == Biome.City;
            bool core = city ? Mathf.Abs(at.x) < ringU - 1f && Mathf.Abs(at.y) < ringV - 1f : at.magnitude < radius * 0.6f;
            float ang = Mathf.Atan2(at.y, at.x);
            bool industry = !core && Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, indAngle * Mathf.Rad2Deg)) < (city ? 60f : 40f);
            LotUse u;
            if (boulevard && !core && gasStations < (city ? 2 : 1) && depth >= 16f && rnd.NextDouble() < 0.45) { gasStations++; width = 24f + (float)rnd.NextDouble() * 4f; return LotUse.GasStation; }
            if (city && core) u = r < 0.45 ? LotUse.Apartment : r < 0.77 ? LotUse.Shop : r < 0.84 ? LotUse.Ruin : r < 0.9 ? LotUse.Parking : r < 0.94 ? LotUse.Empty : LotUse.Apartment;
            else if (industry) u = r < 0.55 ? LotUse.Warehouse : r < 0.68 ? LotUse.Parking : r < 0.8 ? LotUse.Empty : r < 0.9 ? LotUse.Ruin : LotUse.Shack;
            else if (city) u = r < 0.5 ? LotUse.House : r < 0.62 ? LotUse.Shop : r < 0.7 ? LotUse.Apartment : r < 0.8 ? LotUse.Shack : r < 0.9 ? LotUse.Empty : LotUse.Ruin;
            else if (core) u = r < 0.45 ? LotUse.Shop : r < 0.58 ? LotUse.Apartment : r < 0.85 ? LotUse.House : r < 0.93 ? LotUse.Ruin : LotUse.Empty;
            else u = r < 0.45 ? LotUse.House : r < 0.57 ? LotUse.Shack : r < 0.72 ? LotUse.Shop : r < 0.82 ? LotUse.Empty : r < 0.92 ? LotUse.Ruin : LotUse.Warehouse;
            if (depth < MinDepth(u)) u = depth >= 6f ? LotUse.Shack : LotUse.Empty;
            width = u switch
            {
                LotUse.House => 13f, LotUse.Shop => 14f, LotUse.Apartment => 15f, LotUse.Warehouse => 18f, LotUse.Shack => 9f,
                LotUse.Ruin => 12f, LotUse.Parking => 20f, _ => 10f
            } + (float)rnd.NextDouble() * (u == LotUse.Apartment || u == LotUse.Warehouse || u == LotUse.Parking ? 4f : 2.5f);
            return u;
        }

        LotUse PickVillage(Vector2 at, float depth, bool boulevard, System.Random rnd, out float width)
        {
            double r = rnd.NextDouble();
            var u = depth >= 13f && r < 0.8 ? LotUse.Farm : r < 0.93 ? LotUse.Shack : LotUse.Empty;
            width = u == LotUse.Farm ? (depth > 20f ? 20f + (float)rnd.NextDouble() * 6f : 15f + (float)rnd.NextDouble() * 3f) : 10f + (float)rnd.NextDouble() * 2f;
            return u;
        }

        /// <summary>Fill a row of lots facing a street: the row spans <paramref name="a0"/>..<paramref name="a1"/> along
        /// the street and <paramref name="d0"/>..<paramref name="d1"/> across it (front edge at d0 when
        /// <paramref name="frontLow"/>).</summary>
        void Row(WorldGen w, System.Random rnd, bool alongU, float a0, float a1, float d0, float d1, bool frontLow, Line street, Picker pick)
        {
            float depth = d1 - d0;
            if (depth < 5f || street == null) return;
            bool boulevard = street.path;
            a0 = Mathf.Max(a0, street.s0 - 2f); a1 = Mathf.Min(a1, street.s1 + 2f);           // only along the street itself
            var front = alongU ? new Vector2(0f, frontLow ? -1f : 1f) : new Vector2(frontLow ? -1f : 1f, 0f);
            TownLot last = null;
            float s = a0;
            int guard = 0;
            while (a1 - s >= 6f && guard++ < 200)
            {
                var at = alongU ? new Vector2(s + 5f, (d0 + d1) * 0.5f) : new Vector2((d0 + d1) * 0.5f, s + 5f);
                var use = pick(at, depth, boulevard, rnd, out float width);
                width = Mathf.Min(width, a1 - s);
                if (a1 - s - width < 7f) width = a1 - s;
                if (width < MinWidth(use)) use = width >= 8f ? LotUse.Empty : LotUse.Shack;
                if (use == LotUse.GasStation && width < MinWidth(use)) { use = LotUse.Empty; gasStations--; }
                // near the town's edge the lots get shallower instead of failing
                float amax = Mathf.Max(Mathf.Abs(s), Mathf.Abs(s + width)), lim = radius - 1.6f;
                float far = amax < lim ? Mathf.Sqrt(lim * lim - amax * amax) : 0f;
                float ld0 = d0, ld1 = d1;
                if (frontLow) ld1 = Mathf.Min(d1, d0 >= 0f ? far : d1); else ld0 = Mathf.Max(d0, d1 <= 0f ? -far : d0);
                if (ld1 - ld0 < depth && ld1 - ld0 < MinDepth(use)) { if (use == LotUse.GasStation) gasStations--; use = ld1 - ld0 >= 6f ? LotUse.Shack : LotUse.Empty; }
                if (ld1 - ld0 < 5f) { s += 2f; continue; }
                if (ld1 - ld0 > MaxDepth(use)) { if (frontLow) ld1 = ld0 + MaxDepth(use); else ld0 = ld1 - MaxDepth(use); }
                Vector2 min = alongU ? new Vector2(s, ld0) : new Vector2(ld0, s), max = alongU ? new Vector2(s + width, ld1) : new Vector2(ld1, s + width);
                if (!Fits(w, min, max, false)) { if (use == LotUse.GasStation) gasStations--; s += 2f; continue; }
                last = AddLot(min, max, front, use);
                s += width;
            }
            if (last != null) last.last = true;
        }

        Line LineAt(bool alongU, float c, float lo, float hi)
        {
            foreach (var l in lines)
            {
                if (l.alongU != alongU || Mathf.Abs(l.c - c) > 0.01f) continue;
                float ov = Mathf.Min(hi, l.s1) - Mathf.Max(lo, l.s0);
                if (ov > (hi - lo) * 0.5f) return l;
            }
            return null;
        }

        void GridLots(WorldGen w, System.Random rnd)
        {
            var us = new List<float> { -radius, radius }; var vs = new List<float> { -radius, radius };
            foreach (var l in lines) { var into = l.alongU ? vs : us; if (!into.Contains(l.c)) into.Add(l.c); }
            us.Sort(); vs.Sort();
            int plazaQ = rnd.Next(4), parkQ = (plazaQ + 2) % 4;
            bool city = st.kind == Biome.City, parking = false;
            for (int i = 0; i + 1 < us.Count; i++)
            for (int j = 0; j + 1 < vs.Count; j++)
            {
                float u0 = us[i], u1 = us[i + 1], v0 = vs[j], v1 = vs[j + 1];
                var b = new Block { l = LineAt(false, u0, v0, v1), r = LineAt(false, u1, v0, v1), b = LineAt(true, v0, u0, u1), t = LineAt(true, v1, u0, u1) };
                b.min = new Vector2(u0 + (b.l != null ? b.l.half + b.l.walk + 0.4f : 0f), v0 + (b.b != null ? b.b.half + b.b.walk + 0.4f : 0f));
                b.max = new Vector2(u1 - (b.r != null ? b.r.half + b.r.walk + 0.4f : 0f), v1 - (b.t != null ? b.t.half + b.t.walk + 0.4f : 0f));
                b.min = Vector2.Max(b.min, Vector2.one * -radius); b.max = Vector2.Min(b.max, Vector2.one * radius);
                if (b.max.x - b.min.x < 8f || b.max.y - b.min.y < 8f) continue;
                blocks.Add(b);
                var mid = (b.min + b.max) * 0.5f;
                bool centreBlock = (Mathf.Abs(u0) < 0.01f || Mathf.Abs(u1) < 0.01f) && (Mathf.Abs(v0) < 0.01f || Mathf.Abs(v1) < 0.01f);
                int q = mid.x >= 0f ? (mid.y >= 0f ? 0 : 3) : (mid.y >= 0f ? 1 : 2);
                if (centreBlock && (q == plazaQ || q == parkQ))
                {
                    var use = q == parkQ ? LotUse.Park : city ? LotUse.Plaza : LotUse.Market;
                    var front = new Vector2(0f, mid.y >= 0f ? -1f : 1f);
                    if (Fits(w, b.min, b.max, true)) AddLot(b.min, b.max, front, use);
                    if (city && use == LotUse.Plaza) Pedestrianise(mid.x >= 0f ? u1 : u0, v0, v1);
                    continue;
                }
                bool core = city && Mathf.Abs(mid.x) < ringU && Mathf.Abs(mid.y) < ringV;
                if (core && !parking && !centreBlock && rnd.NextDouble() < 0.3 && b.b != null)
                {
                    parking = true;
                    if (Fits(w, b.min, b.max, false)) { AddLot(b.min, b.max, new Vector2(0f, -1f), LotUse.Parking); continue; }
                }
                bool bt = b.b != null && b.t != null, lr = b.l != null && b.r != null;
                bool anyU = b.b != null || b.t != null, anyV = b.l != null || b.r != null;
                bool rowsU = bt || (!lr && anyU && (!anyV || b.max.x - b.min.x >= b.max.y - b.min.y));
                Line s1 = rowsU ? b.b : b.l, s2 = rowsU ? b.t : b.r;
                if (s1 == null && s2 == null) continue;
                float d0 = rowsU ? b.min.y : b.min.x, d1 = rowsU ? b.max.y : b.max.x, a0 = rowsU ? b.min.x : b.min.y, a1 = rowsU ? b.max.x : b.max.y;
                float dt = d1 - d0;
                if (s1 != null && s2 != null)
                {
                    bool alley = dt >= 2f * 12.5f + 4.1f && rnd.NextDouble() < 0.8;
                    float rowD = Mathf.Min(22f, alley ? (dt - 3.5f) * 0.5f - 0.3f : dt * 0.5f - 0.15f);
                    Row(w, rnd, rowsU, a0, a1, d0, d0 + rowD, true, s1, PickGrid);
                    Row(w, rnd, rowsU, a0, a1, d1 - rowD, d1, false, s2, PickGrid);
                    if (alley)
                    {
                        float c = (d0 + d1) * 0.5f;
                        float e0 = rowsU ? (b.l != null ? u0 : a0) : (b.b != null ? v0 : a0), e1 = rowsU ? (b.r != null ? u1 : a1) : (b.t != null ? v1 : a1);
                        var s = new TownStreet { id = streets.Count, kind = StreetKind.Alley, half = 1.75f, walk = 0f, paved = true };
                        s.a = rowsU ? new Vector2(e0, c) : new Vector2(c, e0); s.b = rowsU ? new Vector2(e1, c) : new Vector2(c, e1);
                        s.len = e1 - e0; s.dir = (s.b - s.a) / Mathf.Max(0.01f, s.len);
                        if (s.len > 6f) streets.Add(s);
                    }
                }
                else
                {
                    bool low = s1 != null;
                    float rowD = Mathf.Min(22f, dt);
                    if (low) Row(w, rnd, rowsU, a0, a1, d0, d0 + rowD, true, s1, PickGrid);
                    else Row(w, rnd, rowsU, a0, a1, d1 - rowD, d1, false, s2, PickGrid);
                    // corner blocks: the other street's side fills what is left
                    Line p1 = rowsU ? b.l : b.b, p2 = rowsU ? b.r : b.t;
                    float pd0 = rowsU ? b.min.x : b.min.y, pd1 = rowsU ? b.max.x : b.max.y;
                    float pD = Mathf.Min(16f, pd1 - pd0);
                    if (p1 != null) Row(w, rnd, !rowsU, d0, d1, pd0, pd0 + pD, true, p1, PickGrid);
                    if (p2 != null) Row(w, rnd, !rowsU, d0, d1, pd1 - pD, pd1, false, p2, PickGrid);
                }
            }
            // rows along the boulevards outside the grid blocks were covered above; the gang's corner of town
            PickHangout(rnd);
        }

        /// <summary>The local street on the far side of the city plaza becomes a pedestrian street.</summary>
        void Pedestrianise(float u, float v0, float v1)
        {
            foreach (var s in streets)
            {
                if (s.kind != StreetKind.Local || Mathf.Abs(s.a.x - u) > 0.05f || Mathf.Abs(s.b.x - u) > 0.05f) continue;
                if (Mathf.Min(s.a.y, s.b.y) < v0 - 0.05f || Mathf.Max(s.a.y, s.b.y) > v1 + 0.05f) continue;
                s.kind = StreetKind.Pedestrian;
            }
        }

        void VillageLots(WorldGen w, System.Random rnd)
        {
            var main = lines[0];
            Line cross = null;
            foreach (var l in lines) if (!l.alongU && l.kind == StreetKind.Local) cross = l;
            float hs = main.half + main.walk + 0.4f;
            // the green: a lawn off the junction, with the well
            float cs = cross != null ? cross.half + cross.walk + 0.4f : -10f;
            for (int q = 0; q < 4; q++)
            {
                float sx = q == 1 || q == 3 ? -1f : 1f, sy = q >= 2 ? -1f : 1f;
                var a = new Vector2(sx * cs, sy * hs); var b = new Vector2(sx * (cs + 20f), sy * (hs + 18f));
                var gmin = Vector2.Min(a, b); var gmax = Vector2.Max(a, b);
                if (!Fits(w, gmin, gmax, false)) continue;
                AddLot(gmin, gmax, new Vector2(0f, -sy), LotUse.Green);
                break;
            }
            Row(w, rnd, true, -radius, radius, hs, hs + 32f, true, main, PickVillage);
            Row(w, rnd, true, -radius, radius, -hs - 32f, -hs, false, main, PickVillage);
            foreach (var l in lines)
            {
                if (l.alongU) continue;
                float s = l.half + l.walk + 0.4f, depth = l.kind == StreetKind.Lane ? 14f : 18f;
                float a0 = Mathf.Max(l.s0, -radius), a1 = Mathf.Min(l.s1, radius);
                Row(w, rnd, false, a0, a1, l.c + s, l.c + s + depth, true, l, PickVillage);
                Row(w, rnd, false, a0, a1, l.c - s - depth, l.c - s, false, l, PickVillage);
            }
        }

        void PickHangout(System.Random rnd)
        {
            bool city = st.kind == Biome.City;
            if (st.kind == Biome.Village || st.index == 0 || (!city && rnd.NextDouble() > 0.55)) return;
            TownLot best = null; float bd = -1f;
            foreach (var l in lots)
            {
                if (l.use != LotUse.Empty && l.use != LotUse.Ruin && l.use != LotUse.Shack && l.use != LotUse.Parking) continue;
                if (l.Width < 9f || l.Depth < 8f) continue;
                float d = ((l.min + l.max) * 0.5f).magnitude + (float)rnd.NextDouble() * 10f;
                if (d > bd) { bd = d; best = l; }
            }
            if (best != null) best.use = LotUse.Hangout;
        }
    }
}
