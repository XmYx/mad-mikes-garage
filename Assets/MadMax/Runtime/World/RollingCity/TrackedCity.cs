using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The generated tracked city (roadmap 28, port of the Blender generator modelbench/lib/citygen.py): a
    /// rolling Soviet panel-block town on crawler units. A hull deck carried on trucks, trestles and slewing rings over
    /// giant crawlers with chain drives; flank walls with two gallery tiers and stairs; walkable panel blocks (storeys
    /// with a corridor, stair core and flats with furniture, balconies, AC units, dishes, laundry); workshop halls along
    /// the edges; plants and sheds; banded chimneys; a crane; a pipe trunk with cabins stacked on it; capsule pods;
    /// a pilot bridge on the nose; railings on every edge; street lamps, floods, searchlights; cables between roofs.
    ///
    /// Everything is decided by <see cref="Params"/> on a 1 m occupancy grid (same seed + params = same city) and
    /// authored in the generator's axes (x along the length, nose +x; y across, port +y; z up) through
    /// <see cref="CityKit"/>, which maps it to deck-local game axes (+Z forward, +X starboard, deck top y = 0).
    /// Pure and thread-safe.</summary>
    public static partial class TrackedCity
    {
        public const float Storey = 2.8f;

        public class Params
        {
            public string name = "Granite Falls";
            public int seed = 7;
            public float length = 104f, width = 54f;
            public int trackPairs = 3;
            public float trackScale = 1.0f;
            public float density = 0.6f, houseVariation = 0.6f;
            public int minFloors = 6, maxFloors = 22, towers = 0, variants = 3, chimneys = 8, galleries = 2;
            public bool edgeHalls = true, crane = true;
            public int shanty = 3, pods = 4;
            public float greenery = 0.6f, lights = 1f, litWindows = 0.3f;
            /// <summary>+1 port (left of travel), -1 starboard, 0 none: the pilot bridge on the nose.</summary>
            public int pilot = 1;
            public bool smoke = true;

            /// <summary>A copy with another seed (the circuit carries the profile, each world rolls its own city).</summary>
            public Params With(int newSeed) { var c = (Params)MemberwiseClone(); c.seed = newSeed; return c; }

            public float Z0 => 21.28f * trackScale;           // hull underside above the ground
            public float ZD => Z0 + 5.45f;                    // deck top above the ground
            public float HalfWidth => width * 0.5f;
            public float HalfLength => length * 0.5f;
            /// <summary>Lateral reach of the crawlers from the centre line (for the graded bed).</summary>
            public float Reach => width * 0.5f + 1f + 5.5f * trackScale;
            /// <summary>Half the distance between the front and rear crawler centres (the deck's pose chord).</summary>
            public float Chord
            {
                get
                {
                    int n = Mathf.Max(1, trackPairs);
                    float span = length * 1.2f, uw = (span - (n - 1) * 2f) / n;
                    return n == 1 ? length * 0.3f : span / 2 - uw / 2;
                }
            }
        }

        /// <summary>The two cities on the world's circuits.</summary>
        public static Params Profile(int index, int seed) => index == 0
            ? new Params { name = "Granite Falls", seed = seed }
            : new Params
            {
                name = "Saint Rust", seed = seed, length = 120f, width = 58f, trackPairs = 4, density = 0.75f, houseVariation = 0.3f,
                minFloors = 5, maxFloors = 14, towers = 2, chimneys = 16, shanty = 4, greenery = 0.2f, pilot = -1
            };

        // ------------------------------------------------------------------ deterministic random (Python-style API)
        public sealed class Rng
        {
            readonly System.Random r;
            public Rng(int seed) { r = new System.Random(seed); }
            public float Value => (float)r.NextDouble();
            public float Range(float a, float b) => a + (b - a) * (float)r.NextDouble();
            public int Int(int a, int bInclusive) => r.Next(a, bInclusive + 1);
            public int Range(int a, int bExclusive) => r.Next(a, Mathf.Max(a + 1, bExclusive));
            public T Pick<T>(params T[] items) => items[r.Next(0, items.Length)];
            public bool Chance(float p) => r.NextDouble() < p;
            public float Gauss(float mu, float sigma)
            {
                double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
                return mu + sigma * (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
            }
        }

        // ------------------------------------------------------------------ occupancy grid (1 m cells; 0 free, 1 low, 2 tall)
        public sealed class Grid
        {
            public readonly float x0, x1, y0, y1;
            public readonly int nx, ny;
            public readonly byte[,] c;
            public Grid(float x0, float x1, float y0, float y1)
            {
                this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1;
                nx = Mathf.CeilToInt(x1 - x0); ny = Mathf.CeilToInt(y1 - y0);
                c = new byte[nx, ny];
            }
            void Ix(float a0, float a1, float o, int n, out int i0, out int i1)
            {
                i0 = Mathf.Max(0, Mathf.FloorToInt(a0 - o)); i1 = Mathf.Min(n, Mathf.CeilToInt(a1 - o));
            }
            public bool Free(float ax0, float ax1, float ay0, float ay1, float pad = 0f, int level = 1)
            {
                if (ax0 < x0 || ax1 > x1 || ay0 < y0 || ay1 > y1) return false;
                Ix(ax0 - pad, ax1 + pad, x0, nx, out int i0, out int i1);
                Ix(ay0 - pad, ay1 + pad, y0, ny, out int j0, out int j1);
                for (int i = i0; i < i1; i++) for (int j = j0; j < j1; j++) if (c[i, j] >= level) return false;
                return true;
            }
            public void Mark(float ax0, float ax1, float ay0, float ay1, int v)
            {
                Ix(ax0, ax1, x0, nx, out int i0, out int i1);
                Ix(ay0, ay1, y0, ny, out int j0, out int j1);
                for (int i = i0; i < i1; i++) for (int j = j0; j < j1; j++) c[i, j] = (byte)Mathf.Max(c[i, j], v);
            }
            public float Coverage()
            {
                int n = 0;
                for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) if (c[i, j] > 0) n++;
                return n / (float)(nx * ny);
            }
            public Vector2 Centre(int i, int j) => new Vector2(x0 + i + 0.5f, y0 + j + 0.5f);
        }

        // ------------------------------------------------------------------ the plan (layout decisions, generator space)
        public class Block
        {
            public int index, floors, seed;
            public float cx, cy, w, d, top;      // top = roof slab top (generator z)
            public float z0;                     // underside of the ground storey (generator z)
        }

        /// <summary>A rentable flat (or a ground-floor shop): generator-space floor bounds and its corridor door.</summary>
        public class Room
        {
            public int block, floor, index;
            public bool shop;
            public float x0, x1, y0, y1, z0, z1;
            public Vector3 door;
        }

        public class Plan
        {
            public Params p;
            public Grid grid;
            public Rng rnd;
            public float L, Wd, Z0, ZD, s, uy, Lu;
            public readonly List<float> units = new List<float>();
            public readonly List<Block> blocks = new List<Block>();
            public readonly List<(float x0, float x1, float yc, float h, int side)> halls = new List<(float, float, float, float, int)>();
            public readonly List<(float x0, float x1, float y0, float y1, float z)> roofs = new List<(float, float, float, float, float)>();
            public readonly List<Vector4> stacks = new List<Vector4>();                    // x, y, top z, r
            public readonly List<float> stairX = new List<float>();
            public readonly List<Room> rooms = new List<Room>();
            /// <summary>Generator → game mapping origin: deck centre at deck-top height.</summary>
            public Vector3 Origin => new Vector3(0f, 0f, ZD);
            public CityKit Kit(int salt = 0) => new CityKit(Origin, p.seed * 131 + salt);
        }

        /// <summary>Deck-local game-space smoke outlets (stack tops) for the plan.</summary>
        public static List<Vector3> Stacks(Plan plan)
        {
            var k = plan.Kit();
            var list = new List<Vector3>();
            foreach (var s in plan.stacks) list.Add(k.P(s.x, s.y, s.z + 0.8f));
            return list;
        }

        /// <summary>The layout: units, towers, halls (plants, chimneys, crane and pipe lane are placed while building,
        /// in <see cref="Build"/>, in the same order as the Python generator).</summary>
        public static Plan Layout(Params p)
        {
            p.maxFloors = Mathf.Max(p.maxFloors, p.minFloors);
            var plan = new Plan { p = p, s = p.trackScale, L = p.length, Wd = p.width, rnd = new Rng(p.seed) };
            plan.Z0 = p.Z0; plan.ZD = p.ZD; plan.uy = p.width * 0.5f + 1f;
            plan.grid = new Grid(-plan.L / 2 + 1.5f, plan.L / 2 - 1.5f, -plan.Wd / 2 + 1f, plan.Wd / 2 - 1f);
            var rnd = plan.rnd;
            int n = Mathf.Max(1, p.trackPairs);
            float span = plan.L * 1.2f, gap = 2f, uw = (span - (n - 1) * gap) / n;
            plan.Lu = Mathf.Max(22f, uw / plan.s);
            for (int i = 0; i < n; i++) plan.units.Add(-span / 2 + uw / 2 + i * (uw + gap));
            if (p.galleries > 0)
            {
                float gx = plan.L / 2 - 2;
                int nst = Mathf.Max(1, (int)(plan.L / 35));
                if (nst > 1) for (int i = 0; i < nst; i++) plan.stairX.Add(-gx + 12 + i * (2 * gx - 26) / (nst - 1));
                else plan.stairX.Add(-6f);
            }
            // the gangway landing (starboard = -y, mid-deck) and the pilot's stair stay clear
            plan.grid.Mark(-7f, 7f, -plan.Wd / 2, -plan.Wd / 2 + 8f, 1);
            if (p.pilot != 0)
            {
                float yc = p.pilot * (plan.Wd / 2 - 9f);
                plan.grid.Mark(plan.L / 2 - 4, plan.L / 2, yc - 2, yc + 2, 1);
            }
            LayoutTowers(plan, rnd);
            LayoutHalls(plan, rnd);
            return plan;
        }

        static int Floors(Plan plan, Rng rnd, float x, bool first)
        {
            var p = plan.p;
            if (first) return p.maxFloors;
            float c = 1f - Mathf.Abs(x) / (plan.L / 2);
            float t = (0.25f + 0.75f * rnd.Value) * (0.55f + 0.45f * c);
            t = 0.6f * (1 - p.houseVariation) + t * p.houseVariation;
            return Mathf.RoundToInt(p.minFloors + (p.maxFloors - p.minFloors) * Mathf.Clamp01(t));
        }

        static void LayoutTowers(Plan plan, Rng rnd)
        {
            var p = plan.p; var g = plan.grid; float var = p.houseVariation;
            int n = p.towers > 0 ? p.towers : Mathf.Max(1, Mathf.RoundToInt(p.density * plan.L * plan.Wd / 700f));
            for (int i = 0; i < n; i++)
            {
                float w = 0, d = 0, cx = 0, cy = 0; bool ok = false;
                for (int t = 0; t < 300 && !ok; t++)
                {
                    w = Mathf.Min(16 + (rnd.Range(12f, 28f) - 16) * var, plan.L * 0.4f);
                    d = 11 + (rnd.Range(9.5f, 13f) - 11) * var;
                    cx = Mathf.Clamp(rnd.Gauss(0, plan.L * (i == 0 ? 0.12f : 0.3f)), -plan.L / 2 + w / 2 + 2, plan.L / 2 - w / 2 - 2);
                    cy = rnd.Range(-plan.Wd / 2 + d / 2 + 1.5f, plan.Wd / 2 - d / 2 - 1.5f);
                    ok = g.Free(cx - w / 2, cx + w / 2, cy - d / 2 - 2.4f, cy + d / 2, 3f, 1);   // porch on -y
                }
                if (!ok) continue;
                int f = Floors(plan, rnd, cx, i == 0);
                var b = new Block { index = plan.blocks.Count, cx = cx, cy = cy, w = w, d = d, floors = f, seed = p.seed * 97 + i, z0 = plan.ZD };
                b.top = plan.ZD + f * Storey + Slab + 0.1f;
                g.Mark(cx - w / 2 - 1.3f, cx + w / 2 + 1.3f, cy - d / 2 - 2.4f, cy + d / 2 + 1.3f, 2);
                plan.blocks.Add(b);
                Rooms(plan, b);
            }
        }

        static void LayoutHalls(Plan plan, Rng rnd)
        {
            if (!plan.p.edgeHalls) return;
            var g = plan.grid;
            foreach (int sy in new[] { -1, 1 })
            {
                float yc = sy * (plan.Wd / 2 - 1f - 3.25f);
                float x = -plan.L / 2 + 2;
                while (x < plan.L / 2 - 8)
                {
                    float w = rnd.Range(7f, 14f), h = rnd.Pick(7f, 9f, 11f, 13f);
                    if (rnd.Value < plan.p.density + 0.2f && g.Free(x, x + w, yc - 3.25f, yc + 3.25f, 0.4f))
                    {
                        plan.halls.Add((x, x + w, yc, h, sy));
                        g.Mark(x, x + w, yc - 3.25f, yc + 3.25f, 1);
                        plan.roofs.Add((x, x + w, yc - 3.55f, yc + 3.55f, plan.ZD + h + 0.5f));
                        x += w + rnd.Range(0.3f, 1.2f);
                    }
                    else x += 2f;
                }
            }
        }

        /// <summary>The flats of a block (same layout on every storey): rooms between the partitions either side of the
        /// corridor, except the stair core; the ground floor's are shops (and the lobby).</summary>
        internal static void Rooms(Plan plan, Block b)
        {
            Bays(b.w, out var bays, out float core);
            float hw = b.w / 2, hd = b.d / 2;
            var xs = Partitions(b.w, bays);
            float cw = Bay - Inner, cx0 = core - cw / 2, cx1 = core + cw / 2;
            for (int f = 0; f < b.floors; f++)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i + 1 < xs.Count; i++)
                    {
                        float a = xs[i], c = xs[i + 1];
                        if (side > 0 && a < cx1 && c > cx0) continue;                         // stair core
                        if (f == 0 && side < 0 && a < core && core < c) continue;              // lobby
                        float ya = side < 0 ? -hd + Wall : 0.9f + Inner, yb = side < 0 ? -0.9f - Inner : hd - Wall;
                        float z = b.z0 + f * Storey + Slab;
                        plan.rooms.Add(new Room
                        {
                            block = b.index, floor = f, index = plan.rooms.Count, shop = f == 0,
                            x0 = b.cx + a + Inner / 2, x1 = b.cx + c - Inner / 2, y0 = b.cy + ya, y1 = b.cy + yb, z0 = z, z1 = z + Storey - Slab,
                            door = new Vector3(b.cx + (a + c) / 2 - 0.8f, b.cy + (side < 0 ? -0.9f : 0.9f), z)
                        });
                    }
            }
        }
    }
}
