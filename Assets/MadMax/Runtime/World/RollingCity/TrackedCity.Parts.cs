using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public static partial class TrackedCity
    {
        static readonly Color Warm = new Color(1f, 0.78f, 0.52f), Cool = new Color(0.85f, 0.92f, 1f), Sodium = new Color(1f, 0.62f, 0.28f);
        /// <summary>Half width of the gangway opening in the starboard deck railing (along the length).</summary>
        public const float GangHalfW = 3.4f;

        /// <summary>A named mesh piece of the city (deck-local game space once built).</summary>
        public class Piece
        {
            public string name;
            public CityKit kit;
            public bool cutaway, collide = true;
        }

        /// <summary>A block's storey modules: [0] the ground storey, [1..] the upper variants; floor → module index.</summary>
        public class BlockBuild
        {
            public Block block;
            public CityKit[] shells, insides;
            public int[] floorModule;
            public CityKit extras;
            /// <summary>Game-space offset of storey f's module origin from the deck origin.</summary>
            public Vector3 FloorAt(Plan plan, int f) => plan.P(block.cx, block.cy, block.z0 + f * Storey);
        }

        public class Built
        {
            public Plan plan;
            public readonly List<Piece> pieces = new List<Piece>();
            public readonly List<BlockBuild> blocks = new List<BlockBuild>();
            public CityKit unit;
            public CityKit[] treads;
            public List<Vector3> stacks;
            public int Triangles
            {
                get
                {
                    int n = 0;
                    foreach (var p in pieces) n += p.kit.Triangles;
                    foreach (var b in blocks)
                    {
                        n += b.extras.Triangles;
                        foreach (int m in b.floorModule) n += b.shells[m].Triangles + b.insides[m].Triangles;
                    }
                    return n + (unit.Triangles + treads[0].Triangles) * plan.units.Count * 2;
                }
            }
        }

        /// <summary>Generate everything for these params (worker thread).</summary>
        public static Built Build(Params p)
        {
            var plan = Layout(p);
            var B = new Built { plan = plan };
            var rnd = plan.rnd;
            var hull = plan.Kit(1);
            Hull(plan, hull);
            B.pieces.Add(new Piece { name = "Hull", kit = hull });
            foreach (var b in plan.blocks) B.blocks.Add(BuildBlock(plan, b));
            for (int i = 0; i < plan.halls.Count; i++)
            {
                var h = plan.halls[i];
                var k = plan.Kit(100 + i);
                Hall(plan, k, h.x0, h.x1, h.yc, h.h, h.side, p.seed + i);
                B.pieces.Add(new Piece { name = "Hall" + i, kit = k, cutaway = true });
            }
            var ind = plan.Kit(2);
            Chimneys(plan, ind, rnd);
            if (p.crane) Crane(plan, ind, rnd);
            PipeAndShanty(plan, ind, rnd);
            B.pieces.Add(new Piece { name = "Industry", kit = ind });
            var fill = plan.Kit(3);
            Fill(plan, fill, rnd);
            B.pieces.Add(new Piece { name = "Plants", kit = fill, cutaway = true });
            var det = plan.Kit(4);
            Pods(plan, det, rnd);
            Cab(plan, det);
            Cables(plan, det);
            RailingsAndGreen(plan, det, rnd);
            B.pieces.Add(new Piece { name = "Details", kit = det });
            var fix = plan.Kit(5);
            Lighting(plan, fix, rnd);
            B.pieces.Add(new Piece { name = "Fixtures", kit = fix, collide = false });
            B.unit = Unit(plan);
            B.treads = new CityKit[4];
            for (int i = 0; i < 4; i++) B.treads[i] = Tread(plan, i);
            B.stacks = Stacks(plan);
            return B;
        }

        internal static BlockBuild BuildBlock(Plan plan, Block b)
        {
            var p = plan.p;
            var rnd = new Rng(b.seed);
            var facade = rnd.Pick(CM.Panel, CM.Panel, CM.Concrete, CM.TowerB);
            int variants = Mathf.Max(1, p.variants);
            var bb = new BlockBuild { block = b, shells = new CityKit[variants + 1], insides = new CityKit[variants + 1], floorModule = new int[b.floors] };
            for (int v = 0; v <= variants; v++)
            {
                var shell = new CityKit(Vector3.zero, b.seed * 31 + v);
                var inside = new CityKit(Vector3.zero, b.seed * 31 + v + 7);
                var lights = new List<Vector3>();
                Module(shell, inside, b.w, b.d, b.seed * 31 + (v == 0 ? 99 : v), p.litWindows, v == 0, facade, lights);
                foreach (var l in lights) shell.LampAt(l, Warm, 6f);
                bb.shells[v] = shell; bb.insides[v] = inside;
            }
            for (int f = 0; f < b.floors; f++) bb.floorModule[f] = f == 0 ? 0 : 1 + rnd.Range(0, variants);
            bb.extras = new CityKit(Vector3.zero, b.seed + 5);
            BlockExtras(bb.extras, b);
            return bb;
        }

        // ------------------------------------------------------------------ light fixtures (modelbench/lib/lights.py)
        internal static void StreetLamp(CityKit A, Vector3 b, float facing)
        {
            var mk = A.BeginMark("city_lamp_post");
            var f = new Vector3(Mathf.Cos(facing), Mathf.Sin(facing), 0);
            A.Beam(CM.SteelDark, b, b + new Vector3(0, 0, 0.6f), 0.35f, -1f, true);
            A.Beam(CM.Steel, b, b + new Vector3(0, 0, 6.2f), 0.16f, -1f, true);
            var top = b + new Vector3(0, 0, 6.2f);
            var tip = top + f * 1.6f + new Vector3(0, 0, 0.25f);
            A.Beam(CM.Steel, top, tip, 0.09f);
            A.Box(CM.SteelDark, new Vector3(0.75f, 0.32f, 0.18f), tip + f * 0.3f, new Vector3(0, 0, facing));
            A.Box(CM.Lamp, new Vector3(0.55f, 0.24f, 0.03f), tip + f * 0.3f - new Vector3(0, 0, 0.1f), new Vector3(0, 0, facing));
            A.LampAt(tip + f * 0.3f - new Vector3(0, 0, 0.6f), Sodium, 14f);
            A.EndMark(mk);
        }

        static void Bulkhead(CityKit A, Vector3 at, float outY)
        {
            var o = new Vector3(0, outY, 0);
            A.Box(CM.SteelDark, new Vector3(0.45f, 0.25f, 0.3f), at + o * 0.12f);
            A.Box(CM.Lamp, new Vector3(0.32f, 0.06f, 0.2f), at + o * 0.27f);
            foreach (float dz in new[] { -0.1f, 0.1f }) A.Box(CM.Steel, new Vector3(0.46f, 0.04f, 0.03f), at + o * 0.3f + new Vector3(0, 0, dz));
            A.LampAt(at + o * 0.6f, Warm, 7f);
        }

        internal static void Searchlight(CityKit A, Vector3 p, Vector3 aim, float r = 0.9f)
        {
            var mk = A.BeginMark("city_searchlight");
            var d = (aim - p).normalized;
            A.Beam(CM.SteelDark, p - new Vector3(0, 0, 1.6f), p - new Vector3(0, 0, 0.6f), 0.6f);
            A.Box(CM.SteelDark, new Vector3(0.3f, 2 * r + 0.5f, 0.3f), p - new Vector3(0, 0, 0.6f));
            foreach (int s in new[] { -1, 1 }) A.Beam(CM.Steel, p + new Vector3(0, s * (r + 0.15f), -0.6f), p + new Vector3(0, s * (r + 0.15f), 0), 0.15f);
            A.Beam(CM.Steel, p - d * 0.9f, p + d * 0.6f, 2 * r);
            A.Beam(CM.SteelDark, p + d * 0.6f, p + d * 0.75f, 2 * r + 0.2f);
            A.Beam(CM.LampCyan, p + d * 0.75f, p + d * 0.8f, 2 * r - 0.15f);
            A.LampAt(p + d * 2f, Cool, 18f);
            A.EndMark(mk);
        }

        internal static void Flood(CityKit A, Vector3 p, Vector3 aim, Color color, float range = 12f)
        {
            var mk = A.BeginMark("city_floodlight");
            var d = (aim - p).normalized;
            A.Beam(CM.SteelDark, p - d * 0.25f, p + d * 0.15f, 0.7f, 0.55f);
            A.Beam(CM.Lamp, p + d * 0.15f, p + d * 0.19f, 0.6f, 0.45f);
            A.Beam(CM.Steel, p - new Vector3(0, 0, 0.6f), p - new Vector3(0, 0, 0.2f), 0.12f);
            A.LampAt(Vector3.Lerp(p, aim, 0.35f), color, range);
            A.EndMark(mk);
        }

        // ------------------------------------------------------------------ hull, belly, end platforms, flank walls, galleries
        static void Hull(Plan plan, CityKit A)
        {
            var p = plan.p; float L = plan.L, Wd = plan.Wd, Z0 = plan.Z0, ZD = plan.ZD, s = plan.s; var rnd = new Rng(p.seed + 3);
            A.Span(CM.RustDark, -L / 2 + 4, L / 2 - 4, -Wd / 2 + 2, Wd / 2 - 2, Z0, Z0 + 3);
            var beamsY = new List<float>();
            foreach (float y in new[] { -Wd / 2 + 6, -7f, 7f, Wd / 2 - 6 }) if (Mathf.Abs(y) < Wd / 2 - 2) beamsY.Add(y);
            foreach (float y in beamsY) A.Span(CM.RustDark, -L / 2, L / 2, y - 1.1f, y + 1.1f, Z0 - 1.1f, Z0 + 3.1f);
            A.Span(CM.DeckPlate, -L / 2, L / 2, -Wd / 2, Wd / 2, Z0 + 3, ZD);                                          // deck
            for (float x = -L / 2 + 6; x < L / 2 - 4; x += 12) A.Span(CM.PaintYellow, x, x + 3, -0.12f, 0.12f, ZD, ZD + 0.01f, false);   // centre line
            foreach (int sx in new[] { -1, 1 })                                                                           // end platforms
            {
                A.Span(CM.DeckPlate, sx * L / 2, sx * (L / 2 + 14), -Wd / 2 + 4, Wd / 2 - 4, ZD - 0.45f, ZD + 0.55f);
                A.Span(CM.DeckPlate, sx * L / 2, sx * (L / 2 + 9), -Wd / 2 + 9, Wd / 2 - 9, ZD - 4.45f, ZD - 3.45f);
                foreach (float y in beamsY)
                {
                    A.Beam(CM.RustDark, new Vector3(sx * (L / 2 + 12.5f), y, ZD - 0.45f), new Vector3(sx * (L / 2 + 0.3f), y, Z0 - 0.6f), 0.7f);
                    A.Box(CM.SteelDark, new Vector3(0.6f, 1.4f, 1.4f), new Vector3(sx * (L / 2 + 0.3f), y, Z0 - 0.6f));
                    if (Mathf.Abs(y) < Wd / 2 - 9) A.Beam(CM.RustDark, new Vector3(sx * (L / 2 + 8.5f), y, ZD - 4.45f), new Vector3(sx * (L / 2 + 0.3f), y, Z0 - 0.8f), 0.5f);
                }
                foreach (float x in new[] { L / 2 + 3, L / 2 + 7, L / 2 + 11 })
                    foreach (float y in new[] { -Wd / 2 + 9, 0f, Wd / 2 - 9 })
                    {
                        A.Box(CM.Lamp, new Vector3(0.5f, 0.5f, 0.15f), new Vector3(sx * x, y, ZD - 0.55f));
                        if (p.lights > 0.3f) A.LampAt(new Vector3(sx * x, y, ZD - 1.2f), Warm, 8f);
                    }
                A.Ladder(new Vector3(sx * (L / 2 + 6), Wd / 2 - 9.5f, ZD - 3.45f), new Vector3(sx * (L / 2 + 6), Wd / 2 - 9.5f, ZD + 0.55f), 0.6f);
            }
            float eh = Mathf.Max(4f, plan.uy - 14f * s - 1.2f);                                                          // engine house
            A.Span(CM.RustDark, -(L / 2 - 14), L / 2 - 14, -eh, eh, Z0 - 9, Z0);
            foreach (int sy in new[] { -1, 1 })
                for (int i = 0; i < (int)((L - 28) / 6); i++)
                {
                    float x = -(L / 2 - 14) + 3 + i * 6;
                    A.Box(rnd.Chance(0.3f) ? CM.WindowLit : CM.Window, new Vector3(3.0f, 0.1f, 1.0f), new Vector3(x, sy * (eh + 0.05f), Z0 - 6.5f));
                    A.Box(CM.Rust, new Vector3(0.3f, 0.3f, 9.0f), new Vector3(x + 3, sy * (eh + 0.12f), Z0 - 4.5f));
                }
            for (float x = -(L / 2 - 22); x < L / 2 - 21; x += 14) A.Tube(CM.Rust, new Vector3(x, -eh + 1, Z0 - 9.6f), new Vector3(x, eh - 1, Z0 - 9.6f), 2.6f, 24);   // boilers
            float band0 = plan.uy - 9f * s + 0.6f, band1 = Wd / 2 - 1.2f, floor = 13.3f * s + 1.8f;                      // machinery bays
            if (band1 - band0 > 2.5f)
                foreach (int sy in new[] { -1, 1 })
                {
                    float x = -L / 2 + 2;
                    while (x < L / 2 - 6)
                    {
                        float x1 = Mathf.Min(x + rnd.Range(6f, 11f), L / 2 - 2), zb = Mathf.Max(floor, Z0 - rnd.Range(4f, 9f));
                        float y0 = Mathf.Min(sy * band0, sy * band1), y1 = Mathf.Max(sy * band0, sy * band1);
                        A.Span(rnd.Pick(CM.Rust, CM.Rust, CM.RustDark, CM.Steel), x, x1, y0, y1, zb, Z0);
                        float yf = sy > 0 ? y1 : y0;
                        for (int k = 0; k < (int)((Z0 - zb) / 3.2f); k++)
                            A.Box(rnd.Pick(CM.Window, CM.Window, CM.WindowLit), new Vector3((x1 - x) * 0.6f, 0.08f, 0.8f), new Vector3((x + x1) / 2, yf + sy * 0.03f, zb + 1.4f + k * 3.2f));
                        for (int i = 0; i < (int)((x1 - x) / 1.6f); i++) A.Box(CM.RustDark, new Vector3(0.2f, 0.25f, Z0 - zb), new Vector3(x + 0.8f + i * 1.6f, yf + sy * 0.12f, (zb + Z0) / 2));
                        A.Box(CM.Lamp, new Vector3(0.4f, 0.4f, 0.2f), new Vector3((x + x1) / 2, (y0 + y1) / 2, zb - 0.3f));
                        x = x1 + rnd.Range(0.4f, 1.2f);
                    }
                }
            foreach (float ux in plan.units)                                                                             // gearbox → hull drops
                foreach (int sy in new[] { -1, 1 })
                {
                    float gx = ux + (ux >= 0 ? 1 : -1) * (-(plan.Lu / 2 - 6f) + 0.6f) * s;
                    float gy = sy * (plan.uy - 10.6f * s);
                    float gz = (Mathf.Min(19.6f, Z0 / s - 4f) + 3.6f) * s;
                    A.Box(CM.SteelDark, new Vector3(0.9f, 0.9f, 0.9f), new Vector3(gx, gy, gz));
                    A.Beam(CM.Steel, new Vector3(gx, gy, gz), new Vector3(gx, gy, Z0), 0.45f);
                }
            Galleries(plan, A);
        }

        static void Galleries(Plan plan, CityKit A)
        {
            var p = plan.p; float L = plan.L, Wd = plan.Wd, Z0 = plan.Z0, ZD = plan.ZD;
            if (p.galleries <= 0) return;
            var tiers = new List<(float top, float o)> { (Z0 + 2.8f, 6f) };
            if (p.galleries >= 2) tiers.Add((Z0 - 2.2f, 4.5f));
            float wallBottom = p.galleries >= 2 ? Z0 - 6.1f : Z0 - 1f, gx = L / 2 - 2;
            foreach (int sy in new[] { -1, 1 })
            {
                A.Span(CM.Rust, -gx, gx, sy * (Wd / 2 - 0.6f), sy * Wd / 2, wallBottom, Z0 + 2.95f);
                for (int i = 0; i < (int)(2 * gx / 2.5f); i++)
                {
                    float xw = -gx + 1.25f + i * 2.5f;
                    A.Box(CM.RustDark, new Vector3(0.25f, 0.3f, Z0 + 2.95f - wallBottom), new Vector3(xw, sy * (Wd / 2 + 0.15f), (wallBottom + Z0 + 2.95f) / 2));
                    if (p.galleries >= 2 && i % 3 != 1) A.Box(CM.Window, new Vector3(1.4f, 0.06f, 0.9f), new Vector3(xw + 1.25f, sy * (Wd / 2 + 0.04f), Z0));
                }
                foreach (var (top, o) in tiers)
                {
                    A.Span(CM.DeckPlate, -gx, gx, sy * Wd / 2, sy * (Wd / 2 + o), top - 0.6f, top);
                    float yo = sy * (Wd / 2 + o - 0.15f);
                    A.Box(CM.RustDark, new Vector3(2 * gx, 0.3f, 0.5f), new Vector3(0, yo, top - 0.85f));
                    for (int i = 0; i <= (int)(2 * gx / 5); i++)
                    {
                        float x = -gx + i * 5;
                        if (top < Z0) A.Beam(CM.RustDark, new Vector3(x, yo, top - 0.6f), new Vector3(x, sy * Wd / 2, top - 0.6f - o * 0.4f), 0.3f);
                        else
                        {
                            A.Beam(CM.Steel, new Vector3(x, yo, top + 0.05f), new Vector3(x, sy * (Wd / 2 - 0.1f), ZD + 3.6f), 0.12f);
                            A.Box(CM.SteelDark, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(x, sy * (Wd / 2 - 0.3f), ZD + 3.6f));
                        }
                        A.Beam(CM.RustDark, new Vector3(x, sy * Wd / 2, top - 0.6f), new Vector3(x, yo, top - 0.6f), 0.25f, 0.35f);
                        if (p.lights > 0 && i % Mathf.Max(1, (int)(2 / Mathf.Max(p.lights, 0.25f))) == 0) Bulkhead(A, new Vector3(x + 2.5f, sy * (Wd / 2 + 0.05f), top + 2.2f), sy);
                    }
                    A.Railing(new List<Vector2> { new Vector2(-gx, sy * Wd / 2), new Vector2(-gx, sy * (Wd / 2 + o - 0.08f)), new Vector2(gx, sy * (Wd / 2 + o - 0.08f)), new Vector2(gx, sy * Wd / 2) }, top);
                    for (int k = 0; k < (int)(2 * gx / 5); k++)
                        A.Cable(new Vector3(-gx + k * 5, sy * (Wd / 2 + 0.3f), top - 0.75f), new Vector3(-gx + k * 5 + 5, sy * (Wd / 2 + 0.3f), top - 0.75f), 0.4f, 6, 0.05f);
                }
                float up = tiers[0].top;
                foreach (float sx in plan.stairX)                                                                         // upper gallery → deck
                {
                    int n2 = 10; float rise2 = (ZD - up) / n2;
                    for (int i = 0; i < n2; i++)
                        A.Span(CM.Steel, sx + i * 0.28f, sx + (i + 1) * 0.28f + 0.02f, sy * (Wd / 2 + 0.4f), sy * (Wd / 2 + 1.6f), up + i * rise2, up + (i + 1) * rise2);
                    foreach (float yy in new[] { Wd / 2 + 0.35f, Wd / 2 + 1.65f })
                        A.Beam(CM.SteelDark, new Vector3(sx, sy * yy, up), new Vector3(sx + n2 * 0.28f, sy * yy, ZD), 0.12f, 0.3f);
                    A.Beam(CM.Steel, new Vector3(sx, sy * (Wd / 2 + 1.65f), up + 1.0f), new Vector3(sx + n2 * 0.28f, sy * (Wd / 2 + 1.65f), ZD + 1.0f), 0.05f);
                    float lx = sx + n2 * 0.28f;
                    A.Span(CM.DeckPlate, lx, lx + 1.6f, sy * (Wd / 2 - 0.2f), sy * (Wd / 2 + 1.7f), ZD - 0.1f, ZD);
                    if (tiers.Count > 1)                                                                                   // lower → upper
                    {
                        float lo = tiers[1].top, bx = sx - 9f; int n1 = 18; float rise1 = (up - lo) / n1;
                        for (int i = 0; i < n1; i++)
                            A.Span(CM.Steel, bx + i * 0.28f, bx + (i + 1) * 0.28f + 0.02f, sy * (Wd / 2 + 2.7f), sy * (Wd / 2 + 4.1f), lo + i * rise1, lo + (i + 1) * rise1);
                        A.Beam(CM.Steel, new Vector3(bx, sy * (Wd / 2 + 4.15f), lo + 1.0f), new Vector3(bx + n1 * 0.28f, sy * (Wd / 2 + 4.15f), up + 1.0f), 0.05f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ halls (modelbench/lib/shells.py)
        static void Hall(Plan plan, CityKit M, float hx0, float hx1, float yc, float h, int outSign, int seed)
        {
            var rnd = new Rng(seed);
            const float T = 0.25f;
            float w = hx1 - hx0, d = 6.5f, cx = (hx0 + hx1) / 2, z = plan.ZD, hw = w / 2, hd = d / 2;
            Vector3 O(float x, float y, float zz) => new Vector3(cx + x, yc + y, z + zz);
            void S(CM m, float x0, float x1, float y0, float y1, float z0, float z1, bool solid = true) => M.Span(m, cx + x0, cx + x1, yc + y0, yc + y1, z + z0, z + z1, solid);
            S(CM.Floor, -hw + T, hw - T, -hd + T, hd - T, 0, 0.05f, false);
            var bands = new List<Vector4>();
            for (int k = 0; k < (int)(h / 4); k++)
            {
                float zz = 1.4f + k * 4; int n = Mathf.Max(1, (int)((w - 1) / 2.4f));
                for (int i = 0; i < n; i++) { float xc = -hw + (i + 0.5f) * w / n; bands.Add(new Vector4(xc - 0.8f, xc + 0.8f, zz, zz + 1.2f)); }
            }
            var door = w > 6 ? new Vector4(-1.6f, 1.6f, 0.05f, 3.4f) : new Vector4(-0.6f, 0.6f, 0.05f, 2.3f);
            foreach (int sign in new[] { -1, 1 })
            {
                float y0 = sign > 0 ? hd - T : -hd, y1 = sign > 0 ? hd : -hd + T;
                var holes = sign == outSign ? new List<Vector4>(bands) : new List<Vector4> { door };
                holes.Sort((a, b) => a.x.CompareTo(b.x));
                float x = -hw;
                foreach (var hole in holes)
                {
                    S(CM.Corrugated, x, hole.x, y0, y1, 0, h);
                    S(CM.Corrugated, hole.x, hole.y, y0, y1, 0, hole.z);
                    S(CM.Corrugated, hole.x, hole.y, y0, y1, hole.w, h);
                    x = hole.y;
                }
                S(CM.Corrugated, x, hw, y0, y1, 0, h);
                if (sign == outSign)
                {
                    float ym = (y0 + y1) / 2;
                    foreach (var hole in bands)
                    {
                        S(CM.Glass, hole.x, hole.y, ym - 0.01f, ym + 0.01f, hole.z, hole.w);
                        S(CM.Frame, hole.x, hole.y, ym - 0.03f, ym + 0.03f, hole.z, hole.z + 0.06f, false);
                    }
                }
            }
            foreach (int sx in new[] { -1, 1 }) S(CM.Corrugated, sx > 0 ? hw - T : -hw, sx > 0 ? hw : -hw + T, -hd + T, hd - T, 0, h);
            S(CM.SteelDark, -hw - 0.3f, hw + 0.3f, -hd - 0.3f, hd + 0.3f, h, h + 0.5f);                                // roof
            for (int i = 0; i < (int)(w / 2.2f); i++) M.Box(CM.RustDark, new Vector3(0.35f, 0.4f, h), O(-hw + 0.6f + i * 2.2f, outSign * 3.45f, h / 2));
            if (h >= 9)                                                                                                   // mezzanine + steel stair
            {
                const float mz = 4.4f;
                S(CM.ConcreteDark, -hw + T, 0, -hd + T, hd - T, mz - 0.2f, mz);
                S(CM.Frame, -0.05f, 0.05f, -hd + T, hd - T, mz, mz + 1.0f);
                for (int i = 0; i < 14; i++) S(CM.SteelDark, 0.2f + i * 0.28f, 0.48f + i * 0.28f, hd - T - 1.0f, hd - T, i * mz / 14, (i + 1) * mz / 14);
            }
            int crates = rnd.Range(2, 5);
            for (int i = 0; i < crates; i++)
            {
                float s = rnd.Range(0.6f, 1.2f);
                M.Box(rnd.Pick(CM.Wood, CM.Frame, CM.Door), new Vector3(s, s, s), O(rnd.Range(-hw + 1, hw - 1), rnd.Range(-hd + 1, hd - 1), s / 2 + 0.05f), default, true);
            }
            M.Box(CM.Wood, new Vector3(0.8f, Mathf.Min(3f, d - 1), 0.08f), O(hw - 1.0f, 0, 0.9f));                        // workbench
            M.Box(CM.Frame, new Vector3(0.7f, Mathf.Min(2.8f, d - 1.2f), 0.9f), O(hw - 1.0f, 0, 0.45f), default, true);
            M.Box(CM.Frame, new Vector3(0.5f, 1.6f, 2.2f), O(-hw + 0.6f, -hd + 1.0f, 1.1f), default, true);               // shelving
            M.Box(CM.Machinery, new Vector3(1.6f, 1.0f, 1.5f), O(0.5f, -0.3f, 0.8f), default, true);                      // machine
            M.Box(CM.Plaster, new Vector3(0.6f, 0.6f, 0.4f), O(0.5f, -0.3f, 1.7f));
            M.Box(CM.Lamp, new Vector3(0.4f, 0.4f, 0.2f), O(0, 0, h - 0.4f));
            M.LampAt(O(0, 0, h - 1.0f), Warm, 9f);
        }

        // ------------------------------------------------------------------ plants and sheds, chimneys, crane, pipe trunk
        static void Fill(Plan plan, CityKit A, Rng rnd)
        {
            var p = plan.p; var g = plan.grid; float ZD = plan.ZD;
            float target = 0.25f + 0.55f * p.density, hs = Mathf.Clamp(p.maxFloors / 25f, 0.6f, 1.6f);
            for (int tries = 0; g.Coverage() < target && tries < 600; tries++)
            {
                bool plant = rnd.Chance(0.35f);
                float w = plant ? rnd.Range(6f, 12f) : rnd.Range(5f, 12f), d = plant ? rnd.Range(6f, 10f) : rnd.Range(5f, 10f);
                float x = rnd.Range(g.x0 + w / 2, g.x1 - w / 2), y = rnd.Range(g.y0 + d / 2, g.y1 - d / 2);
                if (!g.Free(x - w / 2, x + w / 2, y - d / 2, y + d / 2, 1.6f)) continue;
                float h = plant ? rnd.Range(12f, 12f + 18f * hs) : rnd.Pick(3.5f, 5f, 6.5f, 8f, 10f);
                A.Span(rnd.Pick(CM.Rust, CM.RustDark, CM.ConcreteDark, CM.Corrugated), x - w / 2, x + w / 2, y - d / 2, y + d / 2, ZD, ZD + h);
                A.Span(CM.SteelDark, x - w / 2 - 0.3f, x + w / 2 + 0.3f, y - d / 2 - 0.3f, y + d / 2 + 0.3f, ZD + h, ZD + h + 0.4f);
                foreach (int sy in new[] { -1, 1 })
                    for (int k = 0; k < (int)(h / 3.5f); k++)
                        if (rnd.Chance(0.7f)) A.Box(rnd.Pick(CM.Window, CM.Window, CM.WindowLit), new Vector3(w * rnd.Range(0.4f, 0.8f), 0.1f, 0.9f), new Vector3(x, y + sy * (d / 2 + 0.03f), ZD + 1.8f + k * 3.5f));
                A.Box(CM.Door, new Vector3(1.3f, 0.08f, 2.3f), new Vector3(x - w * 0.25f, y - d / 2 - 0.04f, ZD + 1.15f));
                if (plant)
                {
                    A.Span(CM.Rust, x - w * 0.1f, x + w * 0.35f, y - d * 0.3f, y + d * 0.3f, ZD + h + 0.4f, ZD + h + rnd.Range(2.5f, 5f));
                    A.Span(CM.Steel, x - w / 2 - 2.7f, x - w / 2 - 0.1f, y - 1.3f, y + 1.3f, ZD, ZD + h + 2);              // stair tower
                    int vents = rnd.Range(1, 4);
                    for (int i = 0; i < vents; i++)
                    {
                        float vx = x + rnd.Range(-w / 3, w / 3), vy = y + rnd.Range(-d / 3, d / 3);
                        A.Tube(CM.Steel, new Vector3(vx, vy, ZD + h + 0.4f), new Vector3(vx, vy, ZD + h + rnd.Range(1.5f, 4f)), rnd.Range(0.4f, 0.9f), 12);
                    }
                    for (int i = 0; i < 3; i++)
                        A.Tube(CM.Pipe, new Vector3(x + w / 2 + 0.4f, y - d / 2 + 1 + i * 0.7f, ZD), new Vector3(x + w / 2 + 0.4f, y - d / 2 + 1 + i * 0.7f, ZD + h), 0.22f, 10);
                }
                g.Mark(x - w / 2 - (plant ? 2.7f : 0f), x + w / 2, y - d / 2, y + d / 2, plant ? 2 : 1);
                if (w * d >= 30) plan.roofs.Add((x - w / 2, x + w / 2, y - d / 2, y + d / 2, ZD + h + 0.4f));
            }
        }

        static void Chimneys(Plan plan, CityKit A, Rng rnd)
        {
            var p = plan.p; var g = plan.grid; float ZD = plan.ZD;
            float hs = Mathf.Sqrt(Mathf.Clamp(p.maxFloors / 25f, 0.6f, 1.6f));
            for (int c = 0; c < p.chimneys; c++)
            {
                float r = 0, x = 0, y = 0; bool ok = false;
                for (int t = 0; t < 120 && !ok; t++)
                {
                    r = rnd.Range(1.1f, 2.2f); x = rnd.Range(g.x0 + 4, g.x1 - 4); y = rnd.Range(g.y0 + 4, g.y1 - 4);
                    ok = g.Free(x - r * 1.9f, x + r * 1.9f, y - r * 1.9f, y + r * 1.9f, 0.8f, 2);
                }
                if (!ok) continue;
                float h = rnd.Range(30f, 56f) * hs; bool banded = rnd.Chance(0.6f);
                A.Tube(CM.RustDark, new Vector3(x, y, ZD), new Vector3(x, y, ZD + 5), r * 1.8f, 24, true);
                int segs = (int)(h / 6);
                for (int i = 0; i < segs; i++)
                {
                    float rr = r * (1 - 0.12f * i / segs);
                    var m = banded && i >= segs - 4 ? (i % 2 == 1 ? CM.StackWhite : CM.StackRed) : (i % 2 == 1 ? CM.Rust : CM.Pipe);
                    A.Tube(m, new Vector3(x, y, ZD + i * 6), new Vector3(x, y, ZD + (i + 1) * 6), rr, 24, true);
                }
                float top = ZD + segs * 6;
                A.Tube(CM.SteelDark, new Vector3(x, y, top), new Vector3(x, y, top + 0.8f), r * 0.95f, 24);
                foreach (float fz in new[] { 0.45f, 0.8f })
                {
                    float zz = ZD + segs * 6 * fz;
                    A.Tube(CM.SteelDark, new Vector3(x, y, zz - 0.25f), new Vector3(x, y, zz), r + 1.4f, 24);
                    var circle = new List<Vector2>();
                    for (int k = 0; k < 24; k++) circle.Add(new Vector2(x + (r + 1.3f) * Mathf.Cos(2 * Mathf.PI * k / 24), y + (r + 1.3f) * Mathf.Sin(2 * Mathf.PI * k / 24)));
                    A.Railing(circle, zz, true, 1.1f, 1.0f, null, false);
                }
                foreach (float dy in new[] { -0.3f, 0.3f }) A.Beam(CM.Steel, new Vector3(x + r + 0.4f, y + dy, ZD + 5), new Vector3(x + r * 0.85f + 0.4f, y + dy, top), 0.08f);
                for (float zz = ZD + 5.3f; zz < top; zz += 0.3f)
                {
                    float rr = Mathf.Lerp(r + 0.4f, r * 0.85f + 0.4f, (zz - ZD - 5) / (top - ZD - 5));
                    A.Beam(CM.Steel, new Vector3(x + rr, y - 0.3f, zz), new Vector3(x + rr, y + 0.3f, zz), 0.03f);
                }
                A.Box(CM.Beacon, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(x, y, top + 1.1f));
                if (p.lights > 0) A.LampAt(new Vector3(x, y, top + 1.6f), new Color(1f, 0.1f, 0.05f), 10f);
                g.Mark(x - r * 1.9f, x + r * 1.9f, y - r * 1.9f, y + r * 1.9f, 2);
                plan.stacks.Add(new Vector4(x, y, top + 0.8f, r));
            }
        }

        static void Crane(Plan plan, CityKit A, Rng rnd)
        {
            var g = plan.grid; float ZD = plan.ZD; int sy = 1; float x = 0, y = 0; bool ok = false;
            for (int t = 0; t < 200 && !ok; t++)
            {
                sy = rnd.Pick(-1, 1); x = rnd.Range(g.x0 + 4, g.x1 - 4); y = sy * rnd.Range(plan.Wd / 2 - 9, plan.Wd / 2 - 3);
                ok = g.Free(x - 1.5f, x + 1.5f, y - 1.5f, y + 1.5f, 0.5f, 2);
            }
            if (!ok) return;
            const float H = 30;
            A.Span(CM.Rust, x - 1, x + 1, y - 1, y + 1, ZD, ZD + H);
            A.Span(CM.RustDark, x - 1.5f, x + 1.5f, y - 1.5f, y + 1.5f, ZD + H - 3, ZD + H);
            A.Ladder(new Vector3(x + 1.1f, y, ZD), new Vector3(x + 1.1f, y, ZD + H - 3), 0.5f);
            var p0 = new Vector3(x, y, ZD + H); var p1 = new Vector3(x, y + sy * 30, ZD + H + 7);
            foreach (float dx in new[] { -0.9f, 0.9f }) A.Beam(CM.Rust, p0 + new Vector3(dx, 0, 0), p1 + new Vector3(dx, 0, 0), 0.3f);
            A.Beam(CM.Rust, p0 + new Vector3(0, 0, 1.8f), p1 + new Vector3(0, 0, 0.6f), 0.3f);
            for (int i = 0; i < 14; i++)
            {
                var a = Vector3.Lerp(p0, p1, i / 14f); var b = Vector3.Lerp(p0, p1, (i + 1) / 14f);
                foreach (float dx in new[] { -0.9f, 0.9f }) A.Beam(CM.Rust, a + new Vector3(dx, 0, 0), b + new Vector3(0, 0, 1.8f - 1.2f * (i + 1) / 14f), 0.14f);
            }
            A.Span(CM.SteelDark, x - 3, x + 3, y - 1, y + 1, ZD + H - 1.5f, ZD + H + 0.5f, false);
            A.Box(CM.Glass, new Vector3(1.6f, 0.05f, 1.0f), new Vector3(x, y + sy * 1.03f, ZD + H - 0.6f));
            var hook = p1 - new Vector3(0, sy * 2, 0);
            A.Beam(CM.Steel, hook, hook - new Vector3(0, 0, 22), 0.06f);
            A.Box(CM.StackRed, new Vector3(0.8f, 0.8f, 1.2f), hook - new Vector3(0, 0, 22.5f));
            g.Mark(x - 1.5f, x + 1.5f, y - 1.5f, y + 1.5f, 2);
        }

        static void PipeTrunk(CityKit A, Vector3 p, Vector3 q, float r, float zTo, float every = 8f)
        {
            var d = q - p; float L = d.magnitude; var u = d / L;
            A.Tube(CM.Pipe, p, q, r, 32);
            int n = Mathf.Max(1, (int)(L / 6));
            for (int i = 0; i <= n; i++)
            {
                var c = p + d * (i / (float)n);
                A.Tube(CM.RustDark, c - u * 0.15f, c + u * 0.15f, r * 1.12f, 32);
                if (i < n) { var m = p + d * ((i + 0.5f) / n); A.Tube(CM.Rust, m - u * 0.4f, m + u * 0.4f, r * 1.02f, 32); }
            }
            var side = new Vector3(-d.y, d.x, 0).normalized;
            for (int i = 0; i <= (int)(L / every); i++)
            {
                var c = p + u * Mathf.Min(L, 1f + i * every);
                foreach (int s in new[] { -1, 1 })
                    A.Beam(CM.RustDark, c + side * s * (r + 0.3f) - new Vector3(0, 0, r), new Vector3(c.x, c.y, zTo) + side * s * (r + 0.3f), 0.35f, -1f, true);
                A.Beam(CM.RustDark, c - side * (r + 0.5f) - new Vector3(0, 0, r + 0.2f), c + side * (r + 0.5f) - new Vector3(0, 0, r + 0.2f), 0.4f);
            }
        }

        internal static void Cabin(CityKit A, Rng rnd, Vector3 c, int face, float w = 3.2f, float d = 2.8f, float h = 2.6f)
        {
            var mk = A.BeginMark("city_cabin");
            float x = c.x, y = c.y, z = c.z;
            var wallM = rnd.Pick(CM.Corrugated, CM.Rust, CM.Steel);
            float fy = y + face * (d / 2 + 0.03f), by = y - face * d / 2;
            // walls (door in the +x end, window at the front), roof, awning
            A.Span(wallM, x - w / 2, x + w / 2, by - face * 0.1f, by, z, z + h);
            A.Span(wallM, x - w / 2, x - w / 2 + 0.1f, y - d / 2, y + d / 2, z, z + h);
            A.Span(wallM, x + w / 2 - 0.1f, x + w / 2, y - d / 2, y - d / 2 + 0.6f, z, z + h);
            A.Span(wallM, x + w / 2 - 0.1f, x + w / 2, y + d / 2 - 0.6f, y + d / 2, z, z + h);
            A.Span(wallM, x + w / 2 - 0.1f, x + w / 2, y - d / 2, y + d / 2, z + 2.25f, z + h);
            A.Span(wallM, x - w / 2, x + w / 2, fy - face * 0.1f, fy, z, z + 1.0f);
            A.Span(wallM, x - w / 2, x + w / 2, fy - face * 0.1f, fy, z + 2.0f, z + h);
            A.Span(wallM, x - w / 2, x - 1.1f, fy - face * 0.1f, fy, z + 1.0f, z + 2.0f);
            A.Span(wallM, x + 0.1f, x + w / 2, fy - face * 0.1f, fy, z + 1.0f, z + 2.0f);
            bool lit = rnd.Chance(0.4f);
            A.Box(lit ? CM.WindowLit : CM.Glass, new Vector3(1.2f, 0.05f, 0.9f), new Vector3(x - 0.5f, fy - face * 0.05f, z + 1.5f), default, true);
            A.Box(CM.Wood, new Vector3(1.36f, 0.06f, 0.08f), new Vector3(x - 0.5f, fy, z + 1.03f));
            A.Box(CM.Wood, new Vector3(1.36f, 0.06f, 0.08f), new Vector3(x - 0.5f, fy, z + 1.97f));
            A.Box(CM.Awning, new Vector3(w + 0.4f, 1.0f, 0.05f), new Vector3(x, fy + face * 0.45f, z + h + 0.1f), new Vector3(face * 0.3f, 0, 0));
            if (rnd.Chance(0.6f)) A.Box(CM.Dish, new Vector3(0.75f, 0.3f, 0.5f), new Vector3(x + rnd.Range(-1f, 1f), fy + face * 0.18f, z + rnd.Range(0.4f, 2f)));
            if (rnd.Chance(0.5f)) A.Tube(CM.SteelDark, new Vector3(x - w / 2 + 0.4f, y, z + h), new Vector3(x - w / 2 + 0.4f, y, z + h + 1.2f), 0.1f, 8);
            A.Span(CM.Corrugated, x - w / 2 - 0.15f, x + w / 2 + 0.15f, y - d / 2 - 0.15f, y + d / 2 + 0.15f, z + h, z + h + 0.08f);
            A.Box(CM.Wood, new Vector3(1.2f, 0.6f, 0.5f), new Vector3(x - 0.6f, by + face * 0.4f, z + 0.25f), default, true);     // cot
            if (lit) A.LampAt(new Vector3(x, y, z + 2.0f), Warm, 5f);
            A.EndMark(mk);
        }

        static void ShantyStack(CityKit A, Rng rnd, float x, float y, float z0, int floors, int face)
        {
            float z = z0;
            for (int f = 0; f < floors; f++)
            {
                float ox = rnd.Range(-0.8f, 0.8f);
                A.Span(CM.DeckPlate, x + ox - 2.2f, x + ox + 2.2f, y - 2.2f, y + 2.2f, z - 0.15f, z);
                foreach (int s in new[] { -1, 1 }) A.Beam(CM.RustDark, new Vector3(x + ox + s * 2.0f, y + face * 2.1f, z - 0.15f), new Vector3(x + ox + s * 2.0f, y, z - 1.4f), 0.15f);
                Cabin(A, rnd, new Vector3(x + ox, y, z), face);
                A.Ladder(new Vector3(x + ox + 2.1f, y + face * 1.6f, z), new Vector3(x + ox + 2.1f, y + face * 1.6f, z + 2.75f), 0.5f);
                z += 2.75f;
            }
        }

        static void PipeAndShanty(Plan plan, CityKit A, Rng rnd)
        {
            var p = plan.p; var g = plan.grid; float ZD = plan.ZD;
            if (p.shanty <= 0) return;
            int bi = -1, bj = -1, brun = 0;
            for (int j = 3; j < g.ny - 3; j++)
            {
                int run = 0;
                for (int i = 0; i < g.nx; i++)
                {
                    bool ok = true;
                    for (int jj = j - 3; jj <= j + 3 && ok; jj++) ok = g.c[i, jj] < 2;
                    run = ok ? run + 1 : 0;
                    if (run >= 20 && run > brun) { bi = i - run + 1; bj = j; brun = run; }
                }
            }
            if (brun == 0) return;
            float y = g.y0 + bj + 0.5f, x0 = g.x0 + bi + 1, x1 = g.x0 + bi + brun - 1, z = ZD + 15.5f;
            PipeTrunk(A, new Vector3(x0, y, z), new Vector3(x1, y, z), 1.6f, ZD);
            foreach (float xe in new[] { x0, x1 }) A.Tube(CM.RustDark, new Vector3(xe - 0.15f, y, z), new Vector3(xe + 0.15f, y, z), 1.9f, 32);
            int face = y < 0 ? -1 : 1;
            int n = Mathf.Min(p.shanty, Mathf.Max(1, (int)((x1 - x0) / 9)));
            for (int k = 0; k < n; k++)
            {
                float xs = n > 1 ? x0 + 4.5f + k * ((x1 - x0 - 9) / (n - 1)) : (x0 + x1) / 2;
                ShantyStack(A, rnd, xs, y, z + 1.8f, rnd.Int(2, 3), face);
                A.Ladder(new Vector3(xs - 2.6f, y + face * 2.0f, ZD), new Vector3(xs - 2.6f, y + face * 2.0f, z + 1.8f), 0.55f);
            }
            g.Mark(x0, x1, y - 3, y + 3, 1);
        }

        // ------------------------------------------------------------------ pods, pilot bridge, railings + greenery, lamps, cables
        internal static void Pod(CityKit A, Rng rnd, Vector3 c, int outY, bool lit, float w = 4f, float h = 3f, float d = 3f)
        {
            var mk = A.BeginMark("city_pod");
            float x = c.x, y = c.y, z = c.z, r = 0.5f;
            A.Span(CM.Rust, x - w / 2 + r, x + w / 2 - r, y - d / 2, y + d / 2, z, z + h);
            A.Span(CM.Rust, x - w / 2, x + w / 2, y - d / 2, y + d / 2, z + r, z + h - r);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { 0, 1 })
                {
                    float zz = z + r + sz * (h - 2 * r);
                    A.Tube(CM.Rust, new Vector3(x + sx * (w / 2 - r), y - d / 2, zz), new Vector3(x + sx * (w / 2 - r), y + d / 2, zz), r, 16);
                }
            float fy = y + outY * (d / 2 + 0.02f);
            A.Box(lit ? CM.WindowLit : CM.Window, new Vector3(2.2f, 0.05f, 1.5f), new Vector3(x - 0.4f, fy, z + 1.5f));
            A.Box(CM.SteelDark, new Vector3(2.4f, 0.08f, 0.12f), new Vector3(x - 0.4f, fy, z + 0.72f));
            A.Box(CM.SteelDark, new Vector3(2.4f, 0.08f, 0.12f), new Vector3(x - 0.4f, fy, z + 2.28f));
            A.Tube(CM.SteelDark, new Vector3(x + 1.4f, fy - outY * 0.05f, z + 1.6f), new Vector3(x + 1.4f, fy + outY * 0.08f, z + 1.6f), 0.42f, 20);
            A.Tube(CM.Glass, new Vector3(x + 1.4f, fy, z + 1.6f), new Vector3(x + 1.4f, fy + outY * 0.1f, z + 1.6f), 0.32f, 20);
            for (int k = 0; k < 6; k++) A.Box(CM.Steel, new Vector3(0.08f, 0.06f, 0.08f), new Vector3(x - w / 2 + 0.4f + k * 0.65f, fy, z + h - 0.25f));
            foreach (int sx in new[] { -1, 1 }) A.Beam(CM.RustDark, new Vector3(x + sx * 1.2f, y - outY * d / 2, z), new Vector3(x + sx * 1.2f, y - outY * (d / 2 + 2f), z + 2.2f), 0.3f);
            for (int k = 0; k < 5; k++)
                A.Cable(new Vector3(x - 1.5f + k * 0.6f, y, z), new Vector3(x - 1.2f + k * 0.5f, y - outY * (d / 2 + 1.8f), z + 0.3f), rnd.Range(0.8f, 2f), 6, 0.05f);
            if (lit) A.LampAt(new Vector3(x, y, z + 1.6f), Warm, 6f);
            A.EndMark(mk);
        }

        static void Pods(Plan plan, CityKit A, Rng rnd)
        {
            var cand = plan.blocks.FindAll(b => b.floors >= 8);
            if (cand.Count == 0) return;
            for (int k = 0; k < plan.p.pods; k++)
            {
                var b = cand[rnd.Range(0, cand.Count)];
                int sy = rnd.Pick(-1, 1);
                float x = b.cx + rnd.Range(-b.w / 2 + 3, b.w / 2 - 3), z = plan.ZD + rnd.Int(4, b.floors - 2) * Storey + 0.5f, yc = b.cy + sy * (b.d / 2 + 3.5f);
                Pod(A, rnd, new Vector3(x, yc, z), sy, rnd.Chance(0.8f));
            }
        }

        static void Cab(Plan plan, CityKit A)
        {
            var p = plan.p; if (p.pilot == 0) return;
            float Wd = plan.Wd, L = plan.L, ZD = plan.ZD;
            int side = p.pilot;
            float x0 = L / 2 + 3f, x1 = L / 2 + 12.2f, yc = side * (Wd / 2 - 9f), y0 = yc - 4.5f, y1 = yc + 4.5f;
            float bas = ZD + 0.55f, fz = bas + 6f, H = 3f;
            foreach (float cx in new[] { x0 + 0.8f, x1 - 0.8f })
                foreach (float cy in new[] { y0 + 0.8f, y1 - 0.8f }) A.Span(CM.Rust, cx - 0.4f, cx + 0.4f, cy - 0.4f, cy + 0.4f, bas, fz - 0.3f);
            var edges = new[] { (new Vector2(x0 + 0.8f, y0 + 0.8f), new Vector2(x1 - 0.8f, y0 + 0.8f)), (new Vector2(x0 + 0.8f, y1 - 0.8f), new Vector2(x1 - 0.8f, y1 - 0.8f)),
                                (new Vector2(x0 + 0.8f, y0 + 0.8f), new Vector2(x0 + 0.8f, y1 - 0.8f)), (new Vector2(x1 - 0.8f, y0 + 0.8f), new Vector2(x1 - 0.8f, y1 - 0.8f)) };
            foreach (var (a, b) in edges)
            {
                A.Beam(CM.RustDark, new Vector3(a.x, a.y, bas), new Vector3(b.x, b.y, fz - 0.3f), 0.25f);
                A.Beam(CM.RustDark, new Vector3(b.x, b.y, bas), new Vector3(a.x, a.y, fz - 0.3f), 0.25f);
            }
            A.Span(CM.DeckPlate, x0, x1 + 1.2f, y0, y1, fz - 0.3f, fz);
            A.Span(CM.Steel, x0, x0 + 0.2f, y0, yc - 0.55f, fz, fz + H);
            A.Span(CM.Steel, x0, x0 + 0.2f, yc + 0.55f, y1, fz, fz + H);
            A.Span(CM.Steel, x0, x0 + 0.2f, yc - 0.55f, yc + 0.55f, fz + 2.25f, fz + H);
            foreach (var (ys, yo) in new[] { (y0, y0 + 0.2f), (y1 - 0.2f, y1) })
            {
                A.Span(CM.Steel, x0, x1, ys, yo, fz, fz + 1.0f);
                A.Span(CM.Steel, x0, x1, ys, yo, fz + 2.7f, fz + H);
                for (int i = 0; i < 6; i++)
                {
                    float xa = x0 + 0.3f + i * (x1 - x0 - 0.6f) / 6;
                    A.Span(CM.Glass, xa + 0.08f, xa + (x1 - x0 - 0.6f) / 6 - 0.08f, ys + 0.08f, yo - 0.08f, fz + 1.0f, fz + 2.7f);
                    A.Span(CM.SteelDark, xa - 0.04f, xa + 0.04f, ys - 0.02f, yo + 0.02f, fz + 1.0f, fz + 2.7f, false);
                }
            }
            float lean = 22f * Mathf.Deg2Rad, gh = (H - 0.9f) / Mathf.Cos(lean), gx = x1 + 0.5f * gh * Mathf.Sin(lean);
            for (int i = 0; i < 6; i++)
            {
                float ya = y0 + 0.2f + i * (y1 - y0 - 0.4f) / 6, yb = ya + (y1 - y0 - 0.4f) / 6;
                A.Box(CM.Glass, new Vector3(0.04f, yb - ya - 0.1f, gh), new Vector3(gx, (ya + yb) / 2, fz + 0.9f + (H - 0.9f) / 2), new Vector3(0, lean, 0), true);
                A.Box(CM.SteelDark, new Vector3(0.1f, 0.1f, gh), new Vector3(gx, ya, fz + 0.9f + (H - 0.9f) / 2), new Vector3(0, lean, 0));
            }
            A.Span(CM.Steel, x1 - 0.1f, x1 + 1.2f, y0, y1, fz, fz + 0.9f);
            A.Span(CM.SteelDark, x0 - 0.2f, x1 + 2.4f, y0 - 0.3f, y1 + 0.3f, fz + H, fz + H + 0.25f);
            A.Span(CM.SteelDark, x1 - 1.6f, x1 - 0.2f, y0 + 0.6f, y1 - 0.6f, fz, fz + 0.95f);
            for (int i = 0; i < 6; i++) A.Box(CM.Screen, new Vector3(0.04f, 0.9f, 0.55f), new Vector3(x1 - 1.0f, y0 + 1.2f + i * (y1 - y0 - 2.4f) / 5, fz + 1.25f), new Vector3(0, -0.5f, 0));
            foreach (float ys in new[] { yc - 2.1f, yc + 2.1f })
            {
                A.Span(CM.Fabric, x1 - 3.6f, x1 - 2.8f, ys - 0.35f, ys + 0.35f, fz + 0.45f, fz + 0.6f);
                A.Span(CM.Fabric, x1 - 3.8f, x1 - 3.6f, ys - 0.35f, ys + 0.35f, fz + 0.6f, fz + 1.4f);
                A.Span(CM.SteelDark, x1 - 3.45f, x1 - 3.0f, ys - 0.1f, ys + 0.1f, fz, fz + 0.45f);
                foreach (int s2 in new[] { -1, 1 }) A.Beam(CM.Steel, new Vector3(x1 - 2.6f, ys + s2 * 0.45f, fz + 0.7f), new Vector3(x1 - 2.45f, ys + s2 * 0.45f, fz + 1.05f), 0.05f);
            }
            float wy = side > 0 ? y1 : y0 - 1.6f;                                                                          // bridge wing
            A.Span(CM.DeckPlate, x0 + 2, x1, wy, wy + 1.6f, fz - 0.25f, fz);
            A.Railing(new List<Vector2> { new Vector2(x0 + 2, wy + (side > 0 ? 1.55f : 0.05f)), new Vector2(x1, wy + (side > 0 ? 1.55f : 0.05f)) }, fz);
            float rz = fz + H + 0.25f;
            A.Beam(CM.Steel, new Vector3(x0 + 3, yc, rz), new Vector3(x0 + 3, yc, rz + 3.5f), 0.2f);
            A.Box(CM.SteelDark, new Vector3(0.3f, 3.4f, 0.25f), new Vector3(x0 + 3, yc, rz + 3.6f), new Vector3(0, 0, 0.6f));
            A.Box(CM.Beacon, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(x1 + 1.8f, y0 - 0.2f, rz + 0.2f));
            A.Box(CM.BeaconGreen, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(x1 + 1.8f, y1 + 0.2f, rz + 0.2f));
            A.LampAt(new Vector3(x0 + 4.5f, yc, fz + 2.5f), Warm, 7f);
            foreach (float yy in new[] { y0 + 0.8f, y1 - 0.8f }) Searchlight(A, new Vector3(x1 + 1.2f, yy, rz + 1.6f), new Vector3(L / 2 + 160, yy * 2, 0), 0.5f);
            int n = 22; float rise = (fz - bas) / n, syy = yc - side * 5.4f;                                              // stair up the pedestal
            for (int i = 0; i < n; i++) A.Span(CM.Steel, x0 + 0.3f + i * 0.26f - 0.13f, x0 + 0.3f + i * 0.26f + 0.15f, syy - 0.6f, syy + 0.6f, bas + i * rise, bas + (i + 1) * rise);
            A.Beam(CM.Steel, new Vector3(x0 + 0.3f, syy + 0.6f * side, bas + 1.0f), new Vector3(x0 + 0.3f + n * 0.26f, syy + 0.6f * side, fz + 1.0f), 0.05f);
            A.Span(CM.DeckPlate, x0 + 0.3f + n * 0.26f - 0.2f, x0 + 0.3f + n * 0.26f + 1.4f, syy - 0.6f, syy + 0.6f, fz - 0.3f, fz);
        }

        internal static void Greenhouse(CityKit A, Vector3 c, float w = 5f, float d = 3.6f, float h = 2.2f)
        {
            var mk = A.BeginMark("city_greenhouse");
            float x = c.x, y = c.y, z = c.z;
            A.Span(CM.ConcreteDark, x - w / 2, x + w / 2, y - d / 2, y + d / 2, z, z + 0.5f);
            A.Span(CM.Glass, x - w / 2 + 0.05f, x + w / 2 - 0.05f, y - d / 2 + 0.05f, y + d / 2 - 0.05f, z + 0.5f, z + h);
            int n = (int)(w / 0.6f);
            for (int i = 0; i <= n; i++)
            {
                float xx = x - w / 2 + i * w / n;
                foreach (int s in new[] { -1, 1 })
                {
                    A.Beam(CM.Wood, new Vector3(xx, y + s * d / 2, z + 0.5f), new Vector3(xx, y + s * d / 2, z + h), 0.06f);
                    A.Beam(CM.Wood, new Vector3(xx, y + s * d / 2, z + h), new Vector3(xx, y, z + h + 1.0f), 0.06f);
                }
            }
            foreach (int s in new[] { -1, 1 }) A.Box(CM.Glass, new Vector3(w, d / 2 * 1.08f, 0.03f), new Vector3(x, y + s * d / 4, z + h + 0.5f), new Vector3(-s * Mathf.Atan2(1f, d / 2), 0, 0));
            A.Beam(CM.Wood, new Vector3(x - w / 2, y, z + h + 1f), new Vector3(x + w / 2, y, z + h + 1f), 0.08f);
            for (int i = 0; i < 4; i++) A.Box(CM.Foliage, new Vector3(0.8f, 0.6f, 0.6f), new Vector3(x - w / 2 + 0.8f + i * 1.1f, y - d / 4, z + 0.85f));
            A.EndMark(mk);
        }

        static void RoofKit(CityKit A, Rng rnd, float cx, float cy, float w, float d, float top)
        {
            Greenhouse(A, new Vector3(cx - w * 0.18f, cy - d * 0.18f, top));
            int n = rnd.Range(4, 9);
            for (int i = 0; i < n; i++)
            {
                float bx = cx + rnd.Range(-w * 0.4f, w * 0.1f), by = cy + rnd.Range(0.05f, 0.4f) * d;
                A.Tube(rnd.Pick(CM.Rust, CM.Pot), new Vector3(bx, by, top), new Vector3(bx, by, top + 0.7f), 0.35f, 12);
                A.Box(CM.Foliage, new Vector3(0.7f, 0.7f, 0.4f), new Vector3(bx, by, top + 0.85f));
            }
            n = rnd.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                float px = cx + rnd.Range(-w * 0.45f, w * 0.45f), py = cy + rnd.Range(-d * 0.45f, d * 0.45f);
                A.Span(CM.ConcreteDark, px - 0.4f, px + 0.4f, py - 0.4f, py + 0.4f, top, top + 1.4f);
                A.Tube(CM.Pot, new Vector3(px, py, top + 1.4f), new Vector3(px, py, top + 2f), 0.18f, 10);
            }
            float sx = cx + w * 0.3f, sy = cy - d * 0.3f;
            A.Span(CM.Corrugated, sx - 1.3f, sx + 1.3f, sy - 1f, sy + 1f, top, top + 2.3f);
            A.Box(CM.Awning, new Vector3(2f, 1f, 0.05f), new Vector3(sx, sy - 1.45f, top + 2.2f), new Vector3(-0.3f, 0, 0));
            A.Box(CM.WindowLit, new Vector3(0.8f, 0.05f, 1.9f), new Vector3(sx, sy - 1.02f, top + 0.95f));
            float tx = cx - w / 2 + 2.5f, ty = cy + d / 2 - 2.5f;                                                          // water tank on legs
            for (int k = 0; k < 4; k++) A.Beam(CM.SteelDark, new Vector3(tx - 1 + 2 * (k % 2), ty - 1 + 2 * (k / 2), top), new Vector3(tx - 1 + 2 * (k % 2), ty - 1 + 2 * (k / 2), top + 2.2f), 0.12f);
            A.Tube(CM.Rust, new Vector3(tx, ty, top + 2.2f), new Vector3(tx, ty, top + 4.4f), 1.3f, 14);
            int ant = rnd.Range(1, 4);
            for (int i = 0; i < ant; i++) { float ax = cx + rnd.Range(-w / 3, w / 3), ay = cy + rnd.Range(-d / 3, d / 3); A.Beam(CM.Steel, new Vector3(ax, ay, top), new Vector3(ax, ay, top + rnd.Range(3f, 8f)), 0.07f); }
        }

        static void RailingsAndGreen(Plan plan, CityKit A, Rng rnd)
        {
            var p = plan.p; float ZD = plan.ZD, L = plan.L, Wd = plan.Wd;
            foreach (int sy in new[] { -1, 1 })
            {
                var gaps = new List<Vector4>();
                if (p.galleries > 0) foreach (float sx in plan.stairX) gaps.Add(new Vector4(sx + 2.7f, sx + 4.5f, Mathf.Min(sy * Wd / 2 - 1, sy * Wd / 2 + 1), Mathf.Max(sy * Wd / 2 - 1, sy * Wd / 2 + 1)));
                if (sy < 0) gaps.Add(new Vector4(-GangHalfW, GangHalfW, -Wd / 2 - 1, -Wd / 2 + 1));                         // gangway (starboard)
                A.Railing(new List<Vector2> { new Vector2(-L / 2, sy * Wd / 2), new Vector2(L / 2, sy * Wd / 2) }, ZD, false, 1.1f, 1.5f, gaps);
                foreach (int sx in new[] { -1, 1 }) A.Railing(new List<Vector2> { new Vector2(sx * L / 2, sy * (Wd / 2 - 4)), new Vector2(sx * L / 2, sy * Wd / 2) }, ZD);
            }
            foreach (int sx in new[] { -1, 1 })
            {
                A.Railing(new List<Vector2> { new Vector2(sx * L / 2, -Wd / 2 + 4), new Vector2(sx * (L / 2 + 14), -Wd / 2 + 4), new Vector2(sx * (L / 2 + 14), Wd / 2 - 4), new Vector2(sx * L / 2, Wd / 2 - 4) }, ZD + 0.55f);
                A.Railing(new List<Vector2> { new Vector2(sx * L / 2, -Wd / 2 + 9), new Vector2(sx * (L / 2 + 9), -Wd / 2 + 9), new Vector2(sx * (L / 2 + 9), Wd / 2 - 9), new Vector2(sx * L / 2, Wd / 2 - 9) }, ZD - 3.45f);
            }
            foreach (var r in plan.roofs)
                A.Railing(new List<Vector2> { new Vector2(r.x0 + 0.2f, r.y0 + 0.2f), new Vector2(r.x1 - 0.2f, r.y0 + 0.2f), new Vector2(r.x1 - 0.2f, r.y1 - 0.2f), new Vector2(r.x0 + 0.2f, r.y1 - 0.2f) }, r.z, true, 1.1f, 1.8f, null, false);
            foreach (var r in plan.roofs)                                                                                  // roof trees
            {
                int n = (int)(rnd.Value * 4 * p.greenery);
                for (int i = 0; i < n; i++)
                {
                    float x = rnd.Range(r.x0 + 0.8f, r.x1 - 0.8f), y = rnd.Range(r.y0 + 0.8f, r.y1 - 0.8f), hgt = rnd.Range(2.5f, 5f);
                    int balls = rnd.Range(2, 5);
                    for (int k = 0; k < balls; k++)
                    {
                        var c = new Vector3(x + rnd.Range(-0.5f, 0.5f), y + rnd.Range(-0.5f, 0.5f), r.z + hgt * rnd.Range(0.5f, 0.9f));
                        float rr = hgt * 0.25f;
                        A.Lathe(CM.Foliage, c - new Vector3(0, 0, rr * 0.75f), new Vector3(0, 0, 1), new[] { new Vector2(0.01f, 0), new Vector2(rr * 0.7f, rr * 0.2f), new Vector2(rr, rr * 0.75f), new Vector2(rr * 0.7f, rr * 1.3f), new Vector2(0.01f, rr * 1.5f) }, 8);
                    }
                    A.Beam(CM.RustDark, new Vector3(x, y, r.z), new Vector3(x, y, r.z + hgt * 0.6f), 0.15f);
                }
            }
            int tallest = 0;
            foreach (var b in plan.blocks) tallest = Mathf.Max(tallest, b.floors);
            foreach (var b in plan.blocks)
            {
                float top = b.top;
                if (b.floors < tallest && rnd.Chance(p.greenery)) RoofKit(A, rnd, b.cx, b.cy, b.w, b.d, top);
                else { int ant = rnd.Range(1, 5); for (int i = 0; i < ant; i++) { float ax = b.cx + rnd.Range(-b.w / 3, b.w / 3), ay = b.cy + rnd.Range(-b.d / 3, b.d / 3); A.Beam(CM.Steel, new Vector3(ax, ay, top), new Vector3(ax, ay, top + rnd.Range(3f, 9f)), 0.07f); } }
                int dishes = rnd.Range(0, 4);
                for (int i = 0; i < dishes; i++) A.Box(CM.Dish, new Vector3(0.9f, 0.3f, 0.9f), new Vector3(b.cx + rnd.Range(-b.w / 3, b.w / 3), b.cy + rnd.Range(-b.d / 3, b.d / 3), top + 0.8f), new Vector3(0.4f, 0, rnd.Range(0f, 6f)));
            }
        }

        static void Lighting(Plan plan, CityKit A, Rng rnd)
        {
            var p = plan.p; var g = plan.grid; float ZD = plan.ZD, Z0 = plan.Z0, s = plan.s;
            if (p.lights <= 0) return;
            float spacing = 9f / Mathf.Max(0.3f, p.lights);
            var free = new List<Vector2>();
            for (int i = 0; i < g.nx; i++)
                for (int j = 0; j < g.ny; j++)
                {
                    if (g.c[i, j] != 0) continue;
                    var c = g.Centre(i, j);
                    if (g.Free(c.x - 1.3f, c.x + 1.3f, c.y - 1.3f, c.y + 1.3f)) free.Add(c);
                }
            free.Sort((a, b) => { int k = Mathf.Abs(a.y).CompareTo(Mathf.Abs(b.y)); return k != 0 ? k : a.x.CompareTo(b.x); });
            var chosen = new List<Vector2>();
            foreach (var q in free)
            {
                bool ok = true;
                foreach (var c in chosen) if (Vector2.Distance(q, c) < spacing) { ok = false; break; }
                if (ok) chosen.Add(q);
            }
            foreach (var c in chosen)
            {
                int best = 0, bestN = -1;
                for (int k = 0; k < 4; k++)
                {
                    var t = c + new Vector2(4 * Mathf.Cos(k * Mathf.PI / 2), 4 * Mathf.Sin(k * Mathf.PI / 2));
                    int n = 0;
                    foreach (var q in free) if ((q - t).sqrMagnitude < 9f) n++;
                    if (n > bestN) { bestN = n; best = k; }
                }
                StreetLamp(A, new Vector3(c.x, c.y, ZD), best * Mathf.PI / 2);
            }
            var tall = new List<Block>(plan.blocks);
            tall.Sort((a, b) => b.floors.CompareTo(a.floors));
            for (int i = 0; i < Mathf.Min(tall.Count, 1 + (int)(3 * p.lights)); i++)
            {
                var b = tall[i]; int sy = b.cy < 0 ? -1 : 1;
                Searchlight(A, new Vector3(b.cx + b.w * 0.3f, b.cy + sy * b.d * 0.3f, b.top + 1.6f), new Vector3(b.cx + rnd.Range(-150f, 150f), b.cy + sy * 220, rnd.Pick(0f, 150f, 230f)));
            }
            foreach (float ux in plan.units)                                                                               // hull floods onto the tracks
                foreach (int sy in new[] { -1, 1 })
                    foreach (float dx in new[] { -5 * s, 5 * s })
                        Flood(A, new Vector3(ux + dx, sy * (plan.Wd / 2 - 3), Z0 - 1f), new Vector3(ux + dx, sy * plan.uy, 3 * s), Cool, 14f);
            if (p.lights >= 0.5f)                                                                                          // facade washes
                foreach (var b in plan.blocks)
                    foreach (float sx in new[] { -0.3f, 0.3f })
                        foreach (int sy in new[] { -1, 1 })
                        {
                            float z = b.top + 0.6f;
                            Flood(A, new Vector3(b.cx + sx * b.w, b.cy + sy * (b.d / 2 + 0.6f), z), new Vector3(b.cx + sx * b.w, b.cy + sy * (b.d / 2 + 3), z - b.floors * Storey * 0.7f), Warm, 10f);
                        }
        }

        static void Cables(Plan plan, CityKit A)
        {
            var tops = new List<Vector3>();
            foreach (var s in plan.stacks) tops.Add(new Vector3(s.x, s.y, s.z));
            foreach (var b in plan.blocks) tops.Add(new Vector3(b.cx + b.w / 2 - 0.5f, b.cy, b.top + 0.6f));
            for (int i = 0; i < tops.Count; i++)
            {
                var near = new List<(float d, int j)>();
                for (int j = 0; j < tops.Count; j++) if (j != i) near.Add((Vector2.Distance(new Vector2(tops[i].x, tops[i].y), new Vector2(tops[j].x, tops[j].y)), j));
                near.Sort((a, b) => a.d.CompareTo(b.d));
                for (int k = 0; k < Mathf.Min(2, near.Count); k++)
                {
                    var (dist, j) = near[k];
                    if (i < j && dist < 45)
                        foreach (float off in new[] { 0f, 1f })
                            A.Cable(tops[i] - new Vector3(0, 0, off), tops[j] - new Vector3(0, 0, off), dist * 0.07f, 14, 0.07f);
                }
            }
        }
    }
}
