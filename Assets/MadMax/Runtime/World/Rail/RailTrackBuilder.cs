using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Pure track and station geometry (worker threads): per 64 m tile the ballast shoulder, sleepers (timber,
    /// some missing or skewed on scrap track), flat-bottom rails (rusty web, worn bright heads), crossing panels where a
    /// road runs over, girder bridges on concrete piers with railings, buffer stops at dead ends, telegraph poles with
    /// sagging wires and mile posts along the main lines; per station its platforms (coping, warning strip, ramps,
    /// lamps, benches, name boards, a canopy), buildings by tier (halt shelter, brick station house, terminal head house
    /// with a clock tower, signal box, water tower, engine shed, fuel tank, loading ramp).</summary>
    public static class RailTrackBuilder
    {
        public const float Tile = 64f;
        static readonly Vector2[] RailBody = { new Vector2(-0.07f, 0f), new Vector2(0.07f, 0f), new Vector2(0.07f, 0.02f), new Vector2(0.012f, 0.025f), new Vector2(0.012f, 0.11f), new Vector2(-0.012f, 0.11f), new Vector2(-0.012f, 0.025f), new Vector2(-0.07f, 0.02f) };
        static readonly Vector2[] RailHeadSec = { new Vector2(-0.034f, 0.11f), new Vector2(0.034f, 0.11f), new Vector2(0.034f, 0.15f), new Vector2(-0.034f, 0.15f) };
        static readonly Vector2[] BallastSec = { new Vector2(-2.3f, -0.3f), new Vector2(2.3f, -0.3f), new Vector2(1.55f, 0.13f), new Vector2(-1.55f, 0.13f) };
        static readonly Vector2[] GirderSec = { new Vector2(-0.12f, -1.1f), new Vector2(0.12f, -1.1f), new Vector2(0.12f, -0.05f), new Vector2(-0.12f, -0.05f) };
        static readonly Color32[] Brick = { Pal.Crimson[1], Pal.Crimson[2], Pal.Bronze[1], Pal.Crimson[1], Pal.Bronze[0] };
        static readonly Color32[] Roof = { Pal.Black[1], Pal.Black[2], Pal.Metal[1] };
        static readonly Color32[] Sheet = { Pal.Metal[2], Pal.Metal[3], Pal.Rust[2], Pal.Metal[2] };

        /// <summary>What a tile needs of a node (copied on the main thread).</summary>
        public struct NodeInfo { public Vector3 pos, outward; public int degree; }

        static Vector3 Right(Vector3 t) { t.y = 0f; return Vector3.Cross(Vector3.up, t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward).normalized; }

        static Vector3 Tangent(RailLine l, int i)
        {
            var t = l.pts[Mathf.Min(l.pts.Length - 1, i + 1)] - l.pts[Mathf.Max(0, i - 1)];
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        static bool Owns(Vector3 a, Vector3 b, float x0, float z0) { float mx = (a.x + b.x) * 0.5f, mz = (a.z + b.z) * 0.5f; return mx >= x0 && mx < x0 + Tile && mz >= z0 && mz < z0 + Tile; }

        /// <summary>One tile's track: origin = the tile's corner at height 0.</summary>
        public static RailKit Track(WorldGen w, List<RailLine> lines, List<NodeInfo> nodes, Vector2Int tile, int seed)
        {
            float x0 = tile.x * Tile, z0 = tile.y * Tile;
            var k = new RailKit(seed ^ (tile.x * 73856093) ^ (tile.y * 19349663)) { origin = new Vector3(x0, 0f, z0) };
            var path = new List<Vector3>(); var right = new List<Vector3>();
            foreach (var l in lines)
            {
                int n = l.pts.Length;
                int i = 0;
                while (i < n - 1)
                {
                    if (!Owns(l.pts[i], l.pts[i + 1], x0, z0)) { i++; continue; }
                    int j = i;
                    while (j < n - 1 && Owns(l.pts[j], l.pts[j + 1], x0, z0)) j++;
                    Run(k, w, l, i, j, path, right);
                    i = j;
                }
                Lineside(k, w, l, x0, z0);
            }
            foreach (var nd in nodes)
            {
                if (nd.pos.x < x0 || nd.pos.x >= x0 + Tile || nd.pos.z < z0 || nd.pos.z >= z0 + Tile) continue;
                if (nd.degree == 1) Buffer(k, nd.pos, nd.outward);
            }
            return k;
        }

        /// <summary>Track along loose points (the build preview), origin = the first point.</summary>
        public static RailKit Preview(IList<Vector3> pts, bool scrap)
        {
            var arr = new Vector3[pts.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = pts[i];
            var l = new RailLine(arr, TrackKind.Built) { built = true, scrap = scrap };
            var k = new RailKit(5) { origin = arr[0] };
            Run(k, null, l, 0, arr.Length - 1, new List<Vector3>(), new List<Vector3>());
            return k;
        }

        /// <summary>Points i..j of a line: ballast, sleepers, rails, crossing panels, bridge decks.</summary>
        static void Run(RailKit k, WorldGen w, RailLine l, int i, int j, List<Vector3> path, List<Vector3> right)
        {
            bool scrap = l.scrap, built = l.built;
            // ballast shoulder (not on bridges, not on scrap track)
            if (!scrap)
            {
                int a = i;
                while (a < j)
                {
                    if ((l.flags[a] & RailLine.Bridge) != 0 && (l.flags[a + 1] & RailLine.Bridge) != 0) { a++; continue; }
                    int b = a;
                    path.Clear(); right.Clear();
                    while (b <= j && !((l.flags[b] & RailLine.Bridge) != 0 && b > a && (l.flags[b - 1] & RailLine.Bridge) != 0)) { path.Add(l.pts[b]); right.Add(Right(Tangent(l, b))); b++; }
                    if (path.Count >= 2) { k.rust = 0f; k.Sweep(path, right, BallastSec, RailKit.Ballast, 3); }
                    a = Mathf.Max(a + 1, b - 1);
                }
            }
            // sleepers every 0.65 m of line distance (stable across tiles)
            float spacing = scrap ? 0.8f : 0.65f;
            int s0 = Mathf.CeilToInt(l.cum[i] / spacing), s1 = Mathf.FloorToInt((l.cum[j] - 1e-3f) / spacing);
            k.rust = 0.1f;
            for (int s = s0; s <= s1; s++)
            {
                float d = s * spacing;
                uint hsh = (uint)(s * 2654435761u) ^ (uint)(l.pts.Length * 40503 + (int)(l.pts[0].x * 7f));
                float r01 = (hsh & 0xffff) / 65535f;
                if (scrap && r01 < 0.12f) continue;                                               // missing ones
                var p = l.At(d, out var t);
                var rr = Right(t);
                if (scrap) { float sk = (r01 - 0.5f) * 0.25f; rr = Quaternion.Euler(0f, sk * 20f, 0f) * rr; }
                var f = Vector3.Cross(rr, Vector3.up);
                var ramp = (hsh >> 16 & 7) == 0 && !scrap ? RailKit.Iron : RailKit.Timber;
                k.Box(p + Vector3.up * (scrap ? 0.08f : 0.11f), rr, Vector3.up, f, 1.25f, 0.06f, 0.11f, ramp, 11);
            }
            // rails
            path.Clear(); right.Clear();
            for (int q = i; q <= j; q++) { path.Add(l.pts[q]); right.Add(Right(Tangent(l, q))); }
            float baseY = RailNetwork.RailTop - 0.15f - (scrap ? 0.03f : 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                var rp = new List<Vector3>(path.Count);
                for (int q = 0; q < path.Count; q++) rp.Add(path[q] + right[q] * (side * RailNetwork.Gauge * 0.5f) + Vector3.up * baseY);
                k.rust = 0.7f; k.Sweep(rp, right, RailBody, Pal.Rust, 21);
                k.rust = scrap ? 0.5f : 0.05f; k.Sweep(rp, right, RailHeadSec, RailKit.RailHead, 22);
            }
            // level crossings: concrete panels flush with the rail heads
            for (int q = i; q < j; q++)
            {
                if ((l.flags[q] & RailLine.Crossing) == 0 && (l.flags[q + 1] & RailLine.Crossing) == 0) continue;
                var a = l.pts[q]; var b = l.pts[q + 1];
                var t = (b - a); float len = t.magnitude; t /= Mathf.Max(1e-3f, len);
                var rr = Right(t);
                k.rust = 0f;
                k.Box((a + b) * 0.5f + Vector3.up * (RailNetwork.RailTop - 0.08f), rr, Vector3.up, t, 0.62f, 0.08f, len * 0.5f + 0.02f, RailKit.Concrete, 31);
                for (int sd = -1; sd <= 1; sd += 2)
                    k.Box((a + b) * 0.5f + rr * (sd * 1.55f) + Vector3.up * (RailNetwork.RailTop - 0.1f), rr, Vector3.up, t, 0.7f, 0.1f, len * 0.5f + 0.02f, RailKit.Concrete, 32);
            }
            // bridges: two plate girders, a timber deck walk, railings, piers to the ground every 12 m
            for (int q = i; q < j; q++)
            {
                if ((l.flags[q] & RailLine.Bridge) == 0 || (l.flags[q + 1] & RailLine.Bridge) == 0) continue;
                var a = l.pts[q]; var b = l.pts[q + 1];
                var t = (b - a).normalized; var rr = Right(t);
                k.rust = 0.45f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    var g0 = a + rr * (sd * 0.95f); var g1 = b + rr * (sd * 0.95f);
                    k.Sweep(new[] { g0, g1 }, new[] { rr, rr }, GirderSec, RailKit.Steel, 41);
                    k.isSolid = true;
                    k.Box((a + b) * 0.5f + rr * (sd * 1.75f) + Vector3.down * 0.03f, rr, Vector3.up, t, 0.7f, 0.04f, Vector3.Distance(a, b) * 0.5f + 0.02f, RailKit.Timber, 42);
                    k.isSolid = false;
                    var rail0 = a + rr * (sd * 2.4f) + Vector3.up * 1.05f; var rail1 = b + rr * (sd * 2.4f) + Vector3.up * 1.05f;
                    k.Beam(rail0, rail1, 0.05f, 0.05f, RailKit.Steel, 43);
                    k.Beam(a + rr * (sd * 2.4f), rail0, 0.06f, 0.06f, RailKit.Steel, 43);
                }
                k.isSolid = true;
                k.Box((a + b) * 0.5f + Vector3.down * 0.06f, rr, Vector3.up, t, 1.1f, 0.06f, Vector3.Distance(a, b) * 0.5f, RailKit.Timber, 44);
                k.isSolid = false;
                float c0 = l.cum[q], c1 = l.cum[q + 1];
                for (float s = Mathf.Ceil(c0 / 12f) * 12f; s < c1; s += 12f)
                {
                    var p = l.At(s);
                    float ground = w.Sample(p.x, p.z).height;
                    if (p.y - ground < 1.6f) continue;
                    k.rust = 0.15f; k.isSolid = true;
                    k.Box(new Vector3(p.x, (p.y - 1.1f + ground - 0.6f) * 0.5f, p.z), rr, Vector3.up, t, 1.6f, (p.y - 1.1f - ground + 0.6f) * 0.5f, 0.45f, RailKit.Concrete, 45);
                    k.isSolid = false;
                    k.Box(p + Vector3.down * 1.18f, rr, Vector3.up, t, 1.9f, 0.08f, 0.6f, RailKit.Concrete, 46);
                }
            }
        }

        /// <summary>Telegraph poles with three sagging wires and mile posts along generated main lines.</summary>
        static void Lineside(RailKit k, WorldGen w, RailLine l, float x0, float z0)
        {
            if (l.built || l.kind != TrackKind.Main || l.Length < 60f) return;
            const float Every = 48f, Off = 4.4f;
            for (float s = Every * 0.5f; s < l.Length - 2f; s += Every)
            {
                var p = l.At(s, out var t);
                var rr = Right(t);
                var foot = p + rr * Off;
                if (foot.x < x0 || foot.x >= x0 + Tile || foot.z < z0 || foot.z >= z0 + Tile) continue;
                if ((l.FlagAt(s) & (RailLine.Bridge | RailLine.Crossing)) != 0) continue;
                foot.y = w.Sample(foot.x, foot.z).height;
                var top = foot + Vector3.up * 6.2f;
                // each pole is its own breakable body (cars snap them): post and wires built here, spawned by RailTrack
                var pole = new RailKit.Pole { key = "tp:" + l.id + ":" + Mathf.RoundToInt(s), prev = s - Every > 0f ? "tp:" + l.id + ":" + Mathf.RoundToInt(s - Every) : null,
                                             next = s + Every < l.Length - 2f ? "tp:" + l.id + ":" + Mathf.RoundToInt(s + Every) : null, foot = foot + Vector3.down * 0.3f };
                pole.post = new RailKit(k.seed) { origin = pole.foot, rust = 0.05f };
                pole.wires = new RailKit(k.seed) { origin = pole.foot, rust = 0f };
                k.poles.Add(pole);
                pole.post.Cylinder(foot + Vector3.down * 0.3f, top, 0.11f, 6, RailKit.Timber, 51);
                pole.post.Beam(top + Vector3.down * 0.35f - rr * 0.8f, top + Vector3.down * 0.35f + rr * 0.8f, 0.09f, 0.1f, RailKit.Timber, 52);
                // wires to the next pole
                if (s + Every < l.Length - 2f)
                {
                    var p2 = l.At(s + Every, out var t2);
                    var top2 = p2 + Right(t2) * Off; top2.y = w.Sample(top2.x, top2.z).height + 6.2f;
                    for (int c = -1; c <= 1; c++)
                    {
                        var a = top + Vector3.down * 0.28f + rr * (c * 0.6f);
                        var b = top2 + Vector3.down * 0.28f + Right(t2) * (c * 0.6f);
                        var prev = a;
                        for (int m = 1; m <= 6; m++)
                        {
                            float f = m / 6f;
                            var q = Vector3.Lerp(a, b, f) + Vector3.down * (0.9f * 4f * f * (1f - f));
                            pole.wires.Beam(prev, q, 0.025f, 0.025f, RailKit.Iron, 53);
                            prev = q;
                        }
                    }
                }
                if (Mathf.Repeat(s, 384f) < Every)                                                 // a mile post now and then
                {
                    k.rust = 0f;
                    var mp = p - rr * 3.2f; mp.y = w.Sample(mp.x, mp.z).height;
                    k.Box(mp + Vector3.up * 0.55f, rr, Vector3.up, t, 0.08f, 0.55f, 0.08f, RailKit.Paint, 54);
                    k.Box(mp + Vector3.up * 0.95f, rr, Vector3.up, t, 0.09f, 0.08f, 0.2f, RailKit.Iron, 55);
                }
            }
        }

        /// <summary>A buffer stop at a dead end: a timber beam on rail-built legs with a red-and-white board.</summary>
        static void Buffer(RailKit k, Vector3 at, Vector3 intoLine)
        {
            var f = intoLine; f.y = 0f; f.Normalize();
            var rr = Right(f);
            var c = at - f * 0.6f;
            k.rust = 0.4f;
            k.Box(c + Vector3.up * 0.95f, rr, Vector3.up, f, 1.4f, 0.18f, 0.2f, RailKit.Timber, 61);
            k.rust = 0f;
            for (int i = -3; i <= 3; i++) k.Box(c + rr * (i * 0.38f) + Vector3.up * 0.95f + f * 0.21f, rr, Vector3.up, f, 0.18f, 0.16f, 0.02f, (i & 1) == 0 ? RailKit.Red : RailKit.Paint, 62);
            k.rust = 0.6f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var foot = c + rr * (sd * RailNetwork.Gauge * 0.5f) + Vector3.up * RailNetwork.RailTop;
                k.Beam(foot - f * 2.2f, c + rr * (sd * RailNetwork.Gauge * 0.5f) + Vector3.up * 1.0f, 0.1f, 0.12f, Pal.Rust, 63);
                k.Beam(foot - f * 0.1f, c + rr * (sd * RailNetwork.Gauge * 0.5f) + Vector3.up * 1.0f, 0.1f, 0.12f, Pal.Rust, 63);
            }
            k.Box(c - f * 1.6f + Vector3.up * 0.35f, rr, Vector3.up, f, 1.3f, 0.35f, 1.0f, RailKit.Ballast, 64);
        }

        // ------------------------------------------------------------------ stations

        /// <summary>Things a station needs from the game besides meshes: loot spots, the loading ramp's deck.</summary>
        public struct Spot { public string kind, title, table; public Vector3 at, half; public float yaw; }

        /// <summary>A station's platforms and buildings; origin = the station centre at bed height.</summary>
        public static RailKit Station(WorldGen w, RailStation st, List<Spot> spots, int seed)
        {
            var k = new RailKit(seed ^ (st.id * 7919)) { origin = st.World(0f, 0f) };
            var D = new Vector3(st.dir.x, 0f, st.dir.y);
            var R = Vector3.Cross(Vector3.up, D);
            Vector3 P(float u, float v, float y) => st.World(u, v, y);
            void B(float u, float v, float y, float hu, float hv, float hy, Color32[] ramp, int salt = 0, bool solid = false)
            {
                k.isSolid = solid; k.Box(P(u, v, y), R, Vector3.up, D, hv, hy, hu, ramp, salt); k.isSolid = false;
            }
            float h = st.half;
            int pi = 0;
            foreach (var pf in st.platforms) { Platform(k, st, pf, pi == 0 && st.tier != StationTier.Halt && st.tier != StationTier.WaterStop, B, P, R, D); pi++; }
            switch (st.tier)
            {
                case StationTier.Halt:
                    Shelter(B, 0f, 7f);
                    NameBoard(B, -12f, 5.2f); NameBoard(B, 12f, 5.2f);
                    spots.Add(new Spot { kind = "loot", title = "SHELTER BOX", table = "rail_office", at = P(2f, 8.2f, 0.4f), half = new Vector3(0.4f, 0.4f, 0.3f), yaw = st.Yaw });
                    break;
                case StationTier.Through:
                    House(B, 0f, 11f, 9f, 4f, 6.2f, false);
                    spots.Add(new Spot { kind = "loot", title = "TICKET OFFICE", table = "rail_office", at = P(-4f, 6.6f, 0.9f), half = new Vector3(0.6f, 0.5f, 0.3f), yaw = st.Yaw });
                    WaterTower(k, B, P, h + 10f, 9f);
                    SignalBox(B, -h - 32f, 8f);
                    spots.Add(new Spot { kind = "loot", title = "SIGNAL BOX", table = "rail_signal", at = P(-h - 32f, 8f, 0.9f), half = new Vector3(0.7f, 0.7f, 0.7f), yaw = st.Yaw });
                    break;
                case StationTier.Terminal:
                    House(B, h + 9f, 0f, 7f, 14f, 8.5f, true);
                    spots.Add(new Spot { kind = "loot", title = "STATION OFFICE", table = "rail_office", at = P(h + 2.6f, -8f, 0.9f), half = new Vector3(0.6f, 0.6f, 0.4f), yaw = st.Yaw });
                    // concourse canopy over the buffers
                    for (float v = -12f; v <= 12.1f; v += 6f) { B(h - 1f, v, 2.4f, 0.12f, 0.12f, 2.4f, RailKit.Iron, 71); }
                    B(h - 4f, 0f, 4.9f, 4f, 13f, 0.12f, Sheet, 72);
                    WaterTower(k, B, P, -h - 56f, -8f);
                    SignalBox(B, -h - 62f, 8f);
                    spots.Add(new Spot { kind = "loot", title = "SIGNAL BOX", table = "rail_signal", at = P(-h - 62f, 8f, 0.9f), half = new Vector3(0.7f, 0.7f, 0.7f), yaw = st.Yaw });
                    EngineShed(B, -h - 4f, 14f);
                    spots.Add(new Spot { kind = "loot", title = "SHED WORKBENCH", table = "rail_depot", at = P(-h - 9f, 17.2f, 0.6f), half = new Vector3(0.9f, 0.5f, 0.4f), yaw = st.Yaw });
                    // end-loading ramp behind the yard siding's buffers (cars drive up onto a flatcar)
                    float rampTop = RailNetwork.RailTop + 0.75f;
                    B(-h + 23f, -15f, rampTop * 0.5f - 0.1f, 2.4f, 2.2f, rampTop * 0.5f + 0.1f, RailKit.Concrete, 73, true);
                    k.isSolid = true;
                    var r0 = P(-h + 25.4f, -15f, rampTop - 0.12f); var r1 = P(-h + 36f, -15f, -0.12f);
                    k.Beam(r0, r1, 4.4f, 0.24f, RailKit.Concrete, 74);
                    k.isSolid = false;
                    spots.Add(new Spot { kind = "ramp", at = P(-h + 23f, -15f, rampTop), half = new Vector3(2.2f, 0f, 2.4f), yaw = st.Yaw });
                    spots.Add(new Spot { kind = "ramp_slope", at = (P(-h + 25.4f, -15f, rampTop) + P(-h + 36f, -15f, 0f)) * 0.5f, half = new Vector3(2.2f, rampTop, 5.3f), yaw = st.Yaw });
                    spots.Add(new Spot { kind = "yard", at = P(-h + 4f, -15f, 0f), yaw = st.Yaw });
                    break;
                case StationTier.WaterStop:
                    WaterTower(k, B, P, 0f, 8f);
                    // fuel tank on saddles
                    k.rust = 0.4f;
                    k.Cylinder(P(7f, 8.5f, 1.6f), P(13f, 8.5f, 1.6f), 1.2f, 12, RailKit.Steel, 81);
                    for (int s = 0; s < 2; s++) B(8f + s * 4f, 8.5f, 0.3f, 0.3f, 0.9f, 0.3f, RailKit.Concrete, 82);
                    Shelter(B, -9f, 8.5f);
                    spots.Add(new Spot { kind = "loot", title = "PUMP SHED", table = "rail_depot", at = P(-7f, 9.6f, 0.4f), half = new Vector3(0.5f, 0.4f, 0.4f), yaw = st.Yaw });
                    NameBoard(B, -4f, 4.6f);
                    break;
                case StationTier.Junction:
                    SignalBox(B, 0f, 7.5f);
                    spots.Add(new Spot { kind = "loot", title = "SIGNAL BOX", table = "rail_signal", at = P(0f, 7.5f, 0.9f), half = new Vector3(0.7f, 0.7f, 0.7f), yaw = st.Yaw });
                    break;
            }
            return k;
        }

        delegate void BoxFn(float u, float v, float y, float hu, float hv, float hy, Color32[] ramp, int salt = 0, bool solid = false);

        static void Platform(RailKit k, RailStation st, Vector4 pf, bool canopy, BoxFn B, System.Func<float, float, float, Vector3> P, Vector3 R, Vector3 D)
        {
            float u0 = pf.x, u1 = pf.y, v0 = pf.z, v1 = pf.w, top = RailNetwork.PlatformTop;
            float uc = (u0 + u1) * 0.5f, vc = (v0 + v1) * 0.5f, hu = (u1 - u0) * 0.5f, hv = (v1 - v0) * 0.5f;
            k.rust = 0.05f;
            B(uc, vc, top * 0.5f - 0.15f, hu, hv - 0.15f, top * 0.5f + 0.15f, RailKit.Concrete, 91, true);
            for (int e = 0; e < 2; e++)
            {
                float ve = e == 0 ? v0 + 0.15f : v1 - 0.15f;
                B(uc, ve, top - 0.05f, hu, 0.17f, 0.07f, RailKit.Paint, 92, true);
                float vw = e == 0 ? v0 + 0.62f : v1 - 0.62f;
                if (hv > 1.2f) B(uc, vw, top + 0.003f, hu, 0.12f, 0.01f, RailKit.Warn, 93);
            }
            // ramps down at both ends
            for (int e = 0; e < 2; e++)
            {
                float ue = e == 0 ? u0 : u1, dirU = e == 0 ? -1f : 1f;
                k.isSolid = true;
                k.Beam(P(ue + dirU * 0.1f, vc, top - 0.12f), P(ue + dirU * 4.2f, vc, -0.15f), hv * 2f - 0.4f, 0.24f, RailKit.Concrete, 94);
                k.isSolid = false;
            }
            // lamps, benches, boards along it
            for (float u = u0 + 6f; u < u1 - 3f; u += 15f)
            {
                float vl = vc;
                B(u, vl, top + 2f, 0.06f, 0.06f, 2f, RailKit.Iron, 95);
                B(u + 0.35f, vl, top + 3.95f, 0.45f, 0.05f, 0.04f, RailKit.Iron, 96);
                k.emit = true; B(u + 0.7f, vl, top + 3.78f, 0.14f, 0.14f, 0.12f, RailKit.Lamp, 97); k.emit = false;
                if (hv > 1.2f) { B(u + 6f, vc, top + 0.4f, 0.9f, 0.22f, 0.03f, RailKit.Timber, 98); B(u + 6f, vc + 0.2f, top + 0.65f, 0.9f, 0.03f, 0.22f, RailKit.Timber, 98); B(u + 5.3f, vc, top + 0.2f, 0.04f, 0.2f, 0.2f, RailKit.Iron, 99); B(u + 6.7f, vc, top + 0.2f, 0.04f, 0.2f, 0.2f, RailKit.Iron, 99); }
            }
            NameBoardAt(B, uc, vc, top);
            if (!canopy) return;
            float cv = Mathf.Abs(v1) > Mathf.Abs(v0) ? v1 - 0.6f : v0 + 0.6f;
            float half = Mathf.Min(hu, 24f);
            for (float u = uc - half; u <= uc + half + 0.1f; u += 6f) B(u, cv, top + 1.7f, 0.08f, 0.08f, 1.7f, RailKit.Iron, 101);
            B(uc, (cv + vc) * 0.5f + (cv - vc) * 0.1f, top + 3.5f, half + 0.5f, hv + 0.4f, 0.08f, Sheet, 102);
        }

        static void NameBoard(BoxFn B, float u, float v) => NameBoardAt(B, u, v, 0f);

        /// <summary>Two posts and a cream board with dark letters (blocks) – the town's name.</summary>
        static void NameBoardAt(BoxFn B, float u, float v, float y)
        {
            B(u - 1.3f, v, y + 1.1f, 0.05f, 0.05f, 1.1f, RailKit.Iron, 111);
            B(u + 1.3f, v, y + 1.1f, 0.05f, 0.05f, 1.1f, RailKit.Iron, 111);
            B(u, v, y + 2.05f, 1.55f, 0.04f, 0.32f, RailKit.Paint, 112);
            for (int c = 0; c < 9; c++) B(u - 1.2f + c * 0.3f, v + 0.03f * 0f, y + 2.05f, 0.09f, 0.05f, 0.14f, RailKit.Iron, 113);
        }

        static void Shelter(BoxFn B, float u, float v)
        {
            B(u, v + 1.4f, 1.2f, 3f, 0.08f, 1.2f, RailKit.Timber, 121, true);
            B(u - 3f, v, 1.2f, 0.08f, 1.4f, 1.2f, RailKit.Timber, 121, true);
            B(u + 3f, v, 1.2f, 0.08f, 1.4f, 1.2f, RailKit.Timber, 121, true);
            B(u, v + 0.2f, 2.5f, 3.3f, 1.8f, 0.07f, Sheet, 122);
            B(u, v + 0.9f, 0.45f, 2.6f, 0.25f, 0.04f, RailKit.Timber, 123);
        }

        /// <summary>A brick station house: walls, dark windows in frames, a door, a pitched roof, a chimney; the
        /// terminal's has a clock tower.</summary>
        static void House(BoxFn B, float u, float v, float hu, float hv, float hgt, bool tower)
        {
            B(u, v, hgt * 0.5f, hu, hv, hgt * 0.5f, Brick, 131, true);
            B(u, v, hgt + 0.05f, hu + 0.3f, hv + 0.3f, 0.12f, RailKit.Concrete, 132);
            // pitched roof as stepped slabs
            for (int i = 0; i < 5; i++) B(u, v, hgt + 0.3f + i * 0.35f, hu + 0.25f - i * 0.05f, hv * (1f - i * 0.19f) + 0.2f, 0.18f, Roof, 133);
            B(u + hu * 0.6f, v + hv * 0.3f, hgt + 2.2f, 0.4f, 0.4f, 1.1f, Brick, 134);
            // windows and doors on all four faces
            for (float wu = u - hu + 1.6f; wu < u + hu - 1f; wu += 2.6f)
                for (int s = -1; s <= 1; s += 2)
                {
                    float fv = v + s * (hv + 0.02f);
                    bool door = Mathf.Abs(wu - u) < 1.4f;
                    B(wu, fv, door ? 1.2f : 1.9f, door ? 0.6f : 0.55f, 0.05f, door ? 1.2f : 0.7f, door ? RailKit.Timber : RailKit.Lens, 135);
                    B(wu, fv, door ? 2.5f : 2.7f, door ? 0.75f : 0.65f, 0.08f, 0.08f, RailKit.Paint, 136);
                    if (hgt > 6f && !door) B(wu, fv, 4.6f, 0.5f, 0.05f, 0.6f, RailKit.Lens, 135);
                }
            for (float wv = v - hv + 1.6f; wv < v + hv - 1f; wv += 2.6f)
                for (int s = -1; s <= 1; s += 2) B(u + s * (hu + 0.02f), wv, 1.9f, 0.05f, 0.55f, 0.7f, RailKit.Lens, 137);
            if (!tower) return;
            B(u, v, hgt + 3.2f, 1.8f, 1.8f, 3.2f, Brick, 138, true);
            for (int s = -1; s <= 1; s += 2) { B(u + s * 1.83f, v, hgt + 4.8f, 0.04f, 1.1f, 1.1f, RailKit.Paint, 139); B(u + s * 1.86f, v, hgt + 4.8f, 0.03f, 0.08f, 0.7f, RailKit.Iron, 140); }
            B(u, v, hgt + 6.7f, 2.1f, 2.1f, 0.2f, Roof, 141);
            B(u, v, hgt + 7.6f, 0.9f, 0.9f, 0.8f, Roof, 141);
        }

        static void SignalBox(BoxFn B, float u, float v)
        {
            B(u, v, 1.3f, 2.2f, 1.6f, 1.3f, Brick, 151, true);
            B(u, v, 3.6f, 2.3f, 1.7f, 1f, RailKit.Timber, 152, true);
            for (int s = -1; s <= 1; s += 2) { B(u, v + s * 1.73f, 3.75f, 2f, 0.04f, 0.6f, RailKit.Lens, 153); B(u + s * 2.33f, v, 3.75f, 0.04f, 1.4f, 0.6f, RailKit.Lens, 153); }
            B(u, v, 4.75f, 2.6f, 2f, 0.15f, Roof, 154);
            B(u, v, 5.2f, 1.6f, 1.2f, 0.3f, Roof, 154);
            // outside stair up the end
            for (int i = 0; i < 9; i++) B(u - 2.7f - i * 0.0f, v - 1.2f + i * 0.28f, 0.15f + i * 0.28f, 0.45f, 0.14f, 0.05f, RailKit.Timber, 155, true);
        }

        static void WaterTower(RailKit k, BoxFn B, System.Func<float, float, float, Vector3> P, float u, float v)
        {
            k.rust = 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float du = (i & 1) == 0 ? -1.6f : 1.6f, dv = (i & 2) == 0 ? -1.6f : 1.6f;
                k.Beam(P(u + du * 1.15f, v + dv * 1.15f, 0f), P(u + du, v + dv, 6.2f), 0.22f, 0.22f, RailKit.Steel, 161);
            }
            k.Beam(P(u - 1.75f, v - 1.75f, 2f), P(u + 1.75f, v + 1.75f, 4.5f), 0.08f, 0.08f, RailKit.Steel, 162);
            k.Beam(P(u + 1.75f, v - 1.75f, 2f), P(u - 1.75f, v + 1.75f, 4.5f), 0.08f, 0.08f, RailKit.Steel, 162);
            k.rust = 0.25f;
            k.Cylinder(P(u, v, 6.2f), P(u, v, 9.4f), 2.3f, 14, RailKit.Timber, 163);
            for (int b = 0; b < 3; b++) k.Cylinder(P(u, v, 6.6f + b), P(u, v, 6.7f + b), 2.36f, 14, Pal.Rust, 164, false);
            k.Cylinder(P(u, v, 9.4f), P(u, v, 10.1f), 2.4f, 14, Roof, 165);
            // spout swung toward the track
            float tv = v > 0f ? v - 2.4f : v + 2.4f, tend = v > 0f ? 1.4f : -1.4f;
            k.Beam(P(u, tv, 7.2f), P(u, tend, 6.0f), 0.28f, 0.28f, Pal.Rust, 166);
            k.Beam(P(u, tend, 6.0f), P(u, tend, 5.0f), 0.2f, 0.2f, RailKit.Iron, 167);
        }

        static void EngineShed(BoxFn B, float u, float v)
        {
            for (int s = -1; s <= 1; s += 2) B(u, v + s * 3.6f, 3.2f, 10f, 0.1f, 3.2f, Sheet, 171, true);
            B(u + 10f, v, 3.2f, 0.1f, 3.6f, 3.2f, Sheet, 171, true);
            for (int i = 0; i < 4; i++) B(u, v, 6.5f + i * 0.3f, 10.3f, 3.9f - i * 0.95f, 0.12f, Sheet, 172);
            B(u - 2f, v, 8.4f, 0.6f, 0.6f, 0.9f, Pal.Rust, 173);
            B(u + 4f, v, 8.4f, 0.6f, 0.6f, 0.9f, Pal.Rust, 173);
            B(u - 5f, v + 3.2f, 0.5f, 1f, 0.35f, 0.45f, RailKit.Timber, 174);
        }
    }
}
