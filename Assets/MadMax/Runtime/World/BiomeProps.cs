using System.Collections.Generic;
using MadMax.Designs;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Biome vegetation (pines, palms, bushes, cacti, logs, waste barrels) and settlement buildings
    /// (farmhouses, fences, haystacks, brick houses, ruined city towers). All destructible voxels.</summary>
    public static class BiomeProps
    {
        public class Template
        {
            public string id;
            public VoxelGrid grid;
            public Mesh mesh;
            public float size;
        }

        static Dictionary<string, Template> templates;
        const byte Scrap = (byte)ResourceType.Scrap, Wood = (byte)ResourceType.Wood, Stone = (byte)ResourceType.Stone, Glass = (byte)ResourceType.Glass, Cloth = (byte)ResourceType.Cloth;

        static readonly Color32[] Pine = { Pal.Hex("16241a"), Pal.Hex("1e3020"), Pal.Hex("283e28"), Pal.Hex("344e30") };
        static readonly Color32[] Leaf = { Pal.Hex("2a5a1c"), Pal.Hex("367024"), Pal.Hex("46862c"), Pal.Hex("5a9e36") };
        static readonly Color32[] Scrub = { Pal.Hex("3a4420"), Pal.Hex("4a5426"), Pal.Hex("5a642e"), Pal.Hex("6c7436") };
        static readonly Color32[] Cactus = { Pal.Hex("2e4a26"), Pal.Hex("3a5c2e"), Pal.Hex("4a7038"), Pal.Hex("5c8442") };
        static readonly Color32[] Brick = { Pal.Hex("5a2a1c"), Pal.Hex("6e3422"), Pal.Hex("823e28"), Pal.Hex("964a30") };
        static readonly Color32[] Conc = { Pal.Hex("4a4846"), Pal.Hex("5c5a56"), Pal.Hex("6e6b66"), Pal.Hex("807c76") };
        static readonly Color32[] Hay = { Pal.Hex("8a6a24"), Pal.Hex("a4802e"), Pal.Hex("bc9638"), Pal.Hex("d4ae48") };
        static readonly Color32[] RoofRed = { Pal.Hex("4a1c14"), Pal.Hex("5e241a"), Pal.Hex("742e20"), Pal.Hex("8a3828") };
        static readonly Color32 Hazard = Pal.Hex("d4b020"), GlowGreen = Pal.Hex("9cff3a");

        static Template T(string id, VoxelGrid g, float size)
        {
            g.PruneUnsupported();
            g.Bevel();
            return new Template { id = id, grid = g, mesh = VoxelMesher.Build(g, id, size), size = size };
        }

        static void Ensure()
        {
            if (templates != null) { bool ok = true; foreach (var t in templates.Values) if (!t.mesh) { ok = false; break; } if (ok) return; }
            templates = new Dictionary<string, Template>();
            void Add(Template t) => templates[t.id] = t;
            for (int i = 0; i < 3; i++) Add(T("Pine" + i, PineTree(i, 36 + i * 6), 0.16f));
            for (int i = 0; i < 2; i++) Add(T("Palm" + i, PalmTree(i, 44 + i * 6), 0.12f));
            for (int i = 0; i < 3; i++) Add(T("Bush" + i, Bush(i, 4 + i, i == 2 ? Leaf : i == 1 ? Scrub : Pine), 0.1f));
            Add(T("Fern0", Fern(3), 0.08f));
            for (int i = 0; i < 2; i++) Add(T("Cactus" + i, CactusPlant(i), 0.1f));
            Add(T("Log0", Log(), 0.1f));
            Add(T("Barrel0", WasteBarrel(), 0.08f));
            for (int i = 0; i < 2; i++) Add(T("Farmhouse" + i, Farmhouse(i), 0.16f));
            Add(T("Fence0", Fence(), 0.08f));
            Add(T("Haystack0", Haystack(), 0.12f));
            for (int i = 0; i < 2; i++) Add(T("BrickHouse" + i, BrickHouse(i), 0.16f));
            for (int i = 0; i < 4; i++) Add(T("Tower" + i, Tower(i, 3 + i), 0.2f));
            for (int i = 0; i < 2; i++) Add(T("Shop" + i, Shop(i), 0.2f));
            Add(T("Streetlight0", Streetlight(), 0.08f));
            Add(T("GasPump0", GasPumpGrid(), 0.08f));
        }

        public static VoxelGrid TemplateGrid(string id) { Ensure(); return templates.TryGetValue(id, out var t) ? t.grid : null; }

        // ------------------------------------------------------------------ vegetation

        static VoxelGrid PineTree(int seed, int h)
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Tube(Vector3.zero, new Vector3(0, h * 0.35f, 0), 0.9f, Pal.Ramp(Pal.Wood, 1, seed));
            for (int y = (int)(h * 0.2f); y < h; y++)
            {
                float t = (y - h * 0.2f) / (h * 0.8f);
                float r = (1f - t) * h * 0.2f * (1f + 0.3f * (1f - Mathf.Repeat(y, 6f) / 6f));    // tiered
                int ri = Mathf.CeilToInt(r);
                for (int x = -ri; x <= ri; x++)
                for (int z = -ri; z <= ri; z++)
                {
                    float d = Mathf.Sqrt(x * x + z * z);
                    if (d > r || (d < r - 2.2f && y % 4 != 0)) continue;
                    var p = new Vector3Int(x, y, z);
                    if (Pal.Hash(p, seed) < 0.12f && d > r - 1f) continue;
                    g.Set(p, Pal.Solid(Pine[Mathf.Clamp((int)(Pal.Hash(p, seed + 1) * 2 + (y > h * 0.6f ? 2 : x + z > 0 ? 1 : 0)), 0, 3)]));
                }
            }
            return g;
        }

        static VoxelGrid PalmTree(int seed, int h)
        {
            var g = new VoxelGrid().Mat(Wood);
            float lean = seed == 0 ? 0.25f : -0.2f;
            Vector3 prev = Vector3.zero;
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                var p = new Vector3(lean * h * t * t, h * t, 0);
                g.Tube(prev, p, 1.2f - t * 0.5f, i % 2 == 0 ? Pal.Ramp(Pal.Wood, 2, seed) : Pal.Ramp(Pal.Wood, 1, seed + 3));
                prev = p;
            }
            var top = prev;
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2f + seed;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 last = top;
                for (int s = 1; s <= 6; s++)
                {
                    float u = s / 6f;
                    var q = top + dir * (u * h * 0.32f) + Vector3.up * (h * 0.08f * Mathf.Sin(u * Mathf.PI) - u * u * h * 0.14f);
                    g.Tube(last, q, 0.7f * (1f - u * 0.6f), Pal.Ramp(Leaf, 2, seed + k));
                    last = q;
                }
            }
            g.Mat(Wood);
            for (int k = 0; k < 3; k++) g.Set(Vector3Int.RoundToInt(top + new Vector3(k - 1, -2, k % 2)), Pal.Solid(Pal.Wood[0]));  // coconuts
            return g;
        }

        static VoxelGrid Bush(int seed, int r, Color32[] ramp)
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -r - 1; x <= r + 1; x++)
            for (int y = 0; y <= r + 1; y++)
            for (int z = -r - 1; z <= r + 1; z++)
            {
                var p = new Vector3Int(x, y, z);
                float n = Pal.Noise((Vector3)p * 0.5f, seed) * 1.8f;
                float d = new Vector3(x, y * 1.3f, z).magnitude;
                if (d < r + n - 0.6f && Pal.Hash(p, seed) > 0.1f) g.Set(p, Pal.Solid(ramp[Mathf.Clamp(y * 3 / (r + 1) + (Pal.Hash(p, seed + 2) > 0.7f ? 1 : 0), 0, 3)]));
            }
            return g;
        }

        static VoxelGrid Fern(int seed)
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f;
                var end = new Vector3(Mathf.Cos(a) * 9f, 5f, Mathf.Sin(a) * 9f);
                g.Tube(Vector3.zero, end * 0.6f + Vector3.up * 4f, 0.5f, Pal.Ramp(Leaf, 2, seed + k));
                g.Tube(end * 0.6f + Vector3.up * 4f, end, 0.4f, Pal.Ramp(Leaf, 1, seed + k));
            }
            return g;
        }

        static VoxelGrid CactusPlant(int seed)
        {
            var g = new VoxelGrid().Mat(Wood);
            int h = 22 + seed * 6;
            g.CylY(0, 0, 2.2f, 0, h, Pal.Stripe(Pal.Ramp(Cactus, 2, seed), Pal.Ramp(Cactus, 0, seed), 0, 2));
            int side = seed == 0 ? 1 : -1;
            g.Tube(new Vector3(0, h * 0.45f, 0), new Vector3(side * 6, h * 0.45f, 0), 1.5f, Pal.Ramp(Cactus, 2, seed));
            g.CylY(side * 6, 0, 1.6f, (int)(h * 0.45f), (int)(h * 0.75f), Pal.Ramp(Cactus, 2, seed + 1));
            if (seed == 1) { g.Tube(new Vector3(0, h * 0.6f, 0), new Vector3(5, h * 0.6f, 0), 1.4f, Pal.Ramp(Cactus, 2, 9)); g.CylY(5, 0, 1.4f, (int)(h * 0.6f), (int)(h * 0.85f), Pal.Ramp(Cactus, 1, 9)); }
            return g;
        }

        static VoxelGrid Log()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.CylX(2.5f, 0, 2.6f, -14, 14, Pal.Ramp(Pal.Wood, 1, 5));
            g.Repaint(-14, 0, -3, -14, 5, 3, Pal.Ramp(Pal.Wood, 3, 6));
            g.Repaint(14, 0, -3, 14, 5, 3, Pal.Ramp(Pal.Wood, 3, 7));
            return g;
        }

        static VoxelGrid WasteBarrel()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 3.3f, 0, 9, (p) => p.y == 4 || p.y == 5 ? Hazard : Pal.Pick(Pal.Rust, p, 3, 2));
            g.Mat(Glass);
            g.Box(-1, 10, -1, 1, 10, 1, Pal.Solid(GlowGreen));                   // ooze
            return g;
        }

        // ------------------------------------------------------------------ buildings

        /// <summary>Hollow rectangular walls with door and windows. Voxels are placed per material by the callbacks.</summary>
        static void Walls(VoxelGrid g, int w, int l, int y0, int y1, byte mat, VoxMat paint, int seed, bool door, int windowEvery)
        {
            for (int x = -w; x <= w; x++)
            for (int z = -l; z <= l; z++)
            {
                if (Mathf.Abs(x) != w && Mathf.Abs(z) != l) continue;
                for (int y = y0; y <= y1; y++)
                {
                    int ly = y - y0;
                    if (door && z == -l && Mathf.Abs(x) <= 3 && ly <= 13) continue;
                    int along = Mathf.Abs(x) == w ? z : x;
                    bool win = windowEvery > 0 && ly >= 6 && ly <= 11 && Mathf.Abs(along) % windowEvery <= 1 && Mathf.Abs(along) < (Mathf.Abs(x) == w ? l : w) - 2;
                    if (win)
                    {
                        if (Pal.Hash(x, y, z, seed) > 0.35f) { g.Mat(Glass); g.Set(x, y, z, Pal.Ramp(Pal.Glass, 2, seed)); }
                        continue;
                    }
                    g.Mat(mat); g.Set(x, y, z, paint);
                }
            }
        }

        static void Slab(VoxelGrid g, int w, int l, int y, byte mat, VoxMat paint, int hole = 0)
        {
            g.Mat(mat);
            for (int x = -w; x <= w; x++) for (int z = -l; z <= l; z++) if (hole == 0 || !(Mathf.Abs(x - w / 2) < hole && Mathf.Abs(z) < hole)) g.Set(x, y, z, paint);
        }

        /// <summary>Straight stair flight (one voxel rise per voxel run) from a floor up to the next, and the slab opening above it.</summary>
        static void Stairs(VoxelGrid g, int cx, int halfW, int z0, int floorY, int rise, byte mat, VoxMat paint)
        {
            g.Mat(mat);
            for (int i = 0; i < rise; i++)
                for (int y = floorY + 1; y <= floorY + 1 + i; y++) g.Box(cx - halfW, y, z0 + i, cx + halfW, y, z0 + i, paint);
            g.ClearBox(cx - halfW - 1, floorY + rise, z0 - 1, cx + halfW + 1, floorY + rise, z0 + rise - 1);   // opening in the slab above
        }

        static VoxelGrid Shop(int seed)
        {
            var g = new VoxelGrid();
            int w = 30, l = 22, h = 16;
            var wall = Pal.Weathered(Conc, 0.2f, seed + 30, 2, 0);
            Slab(g, w, l, 0, Stone, Pal.Ramp(Conc, 1, seed));
            Walls(g, w, l, 1, h, Stone, wall, seed, false, 0);
            // storefront: big windows and a door in the front wall
            for (int x = -w + 2; x <= w - 2; x++)
            for (int y = 1; y <= 12; y++)
            {
                bool door = Mathf.Abs(x) <= 3 && y <= 11;
                bool frame = x % 8 == 0 || y == 12;
                g.voxels.Remove(new Vector3Int(x, y, -l));
                if (door) continue;
                if (frame) { g.Mat(Scrap); g.Set(x, y, -l, Pal.Ramp(Pal.Metal, 1)); }
                else if (y >= 3 && Pal.Hash(x, y, seed, 3) > 0.4f) { g.Mat(Glass); g.Set(x, y, -l, Pal.Ramp(Pal.Glass, 2, seed)); }
                else if (y < 3) { g.Mat(Stone); g.Set(x, y, -l, wall); }
            }
            Slab(g, w + 1, l + 1, h + 1, Stone, Pal.Ramp(Conc, 0, seed + 2));
            // sign board
            g.Mat(Scrap);
            var signCol = seed % 2 == 0 ? Pal.Hex("8a2a1a") : Pal.Hex("1a4a6a");
            g.Box(-20, h + 2, -l - 1, 20, h + 7, -l - 1, p => (p.x % 4 == 0 && p.y > h + 2 && p.y < h + 7) ? Pal.Cream[3] : signCol);
            // shelves
            g.Mat(Wood);
            foreach (int z in new[] { -8, 0, 8 })
                for (int y = 1; y <= 8; y += 3) g.Box(-18, y, z, 18, y, z + 1, Pal.Ramp(Pal.Wood, 2, seed + z));
            g.Box(18, 1, 14, 28, 5, 18, Pal.Ramp(Pal.Wood, 1, seed + 40));                  // counter
            return g;
        }

        static VoxelGrid Farmhouse(int seed)
        {
            var g = new VoxelGrid();
            int w = 20 + seed * 4, l = 16, h = 17;
            var planks = Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed + 10), Pal.Ramp(Pal.Wood, 1, seed + 11), 1, 3);
            Slab(g, w, l, 0, Wood, Pal.Ramp(Pal.Wood, 1, seed));
            Walls(g, w, l, 1, h, Wood, planks, seed, true, 7);
            // gable roof along X
            g.Mat(Scrap);
            for (int z = -l - 2; z <= l + 2; z++)
            {
                int rise = (l + 2 - Mathf.Abs(z)) * 2 / 3;
                for (int x = -w - 2; x <= w + 2; x++) g.Set(x, h + 1 + rise, z, Pal.Ramp(RoofRed, 1, seed + x));
            }
            g.Mat(Wood);                                                                      // gable ends
            foreach (int x in new[] { -w, w })
                for (int z = -l; z <= l; z++) for (int y = h + 1; y <= h + (l + 2 - Mathf.Abs(z)) * 2 / 3; y++) g.Set(x, y, z, planks);
            g.Mat(Stone); g.Box(w - 6, h - 2, 4, w - 3, h + 16, 7, Pal.Ramp(Brick, 1, seed));   // chimney
            return g;
        }

        static VoxelGrid Fence()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -24; x <= 24; x += 12) g.Box(x, 0, 0, x, 14, 0, Pal.Ramp(Pal.Wood, 1, x));
            g.Box(-24, 5, 0, 24, 5, 0, Pal.Ramp(Pal.Wood, 2, 3));
            g.Box(-24, 11, 0, 24, 11, 0, Pal.Ramp(Pal.Wood, 2, 4));
            return g;
        }

        static VoxelGrid Haystack()
        {
            var g = new VoxelGrid().Mat(Cloth);
            for (int x = -10; x <= 10; x++) for (int y = 0; y <= 10; y++) for (int z = -10; z <= 10; z++)
            {
                float d = new Vector3(x, y * 1.2f, z).magnitude;
                if (d < 10f && d > 7.5f) g.Set(x, y, z, Pal.Ramp(Hay, y > 6 ? 3 : 2, x * z));
            }
            return g;
        }

        static VoxelGrid BrickHouse(int seed)
        {
            var g = new VoxelGrid();
            int w = 24 + seed * 3, l = 20, storey = 18;
            var brick = Pal.Stripe(Pal.Ramp(Brick, 2, seed), Pal.Ramp(Brick, 0, seed + 1), 1, 4);
            Slab(g, w, l, 0, Stone, Pal.Ramp(Conc, 1, seed));
            Walls(g, w, l, 1, storey, Stone, brick, seed, true, 8);
            Slab(g, w, l, storey + 1, Stone, Pal.Ramp(Conc, 1, seed + 2));
            Walls(g, w, l, storey + 2, storey * 2 + 1, Stone, brick, seed + 5, false, 8);
            Stairs(g, w / 2, 3, -10, 0, storey + 1, Wood, Pal.Ramp(Pal.Wood, 2, seed + 7));
            Slab(g, w + 1, l + 1, storey * 2 + 2, Stone, Pal.Ramp(Conc, 0, seed + 3));
            g.Mat(Scrap);                                                                     // awning over the door
            g.Box(-5, 15, -l - 4, 5, 15, -l - 1, Pal.Stripe(Pal.Solid(Pal.Hex("8a2a1a")), Pal.Solid(Pal.Cream[2]), 0, 4, 2));
            if (seed == 1) g.ClearBox(w - 10, storey + 6, -l, w, storey * 2 + 3, -l + 12);      // collapsed corner
            return g;
        }

        static VoxelGrid Tower(int seed, int floors)
        {
            var g = new VoxelGrid();
            int w = 26 + (seed % 2) * 6, l = 22, storey = 15;
            var wall = Pal.Weathered(Conc, 0.15f, seed + 20, 2, 0);
            Slab(g, w, l, 0, Stone, Pal.Ramp(Conc, 1, seed));
            for (int f = 0; f < floors; f++)
            {
                int y0 = 1 + f * (storey + 1);
                Walls(g, w, l, y0, y0 + storey - 1, Stone, wall, seed + f * 7, f == 0, 5);
                Slab(g, w, l, y0 + storey, Stone, Pal.Ramp(Conc, 0, seed + f));
            }
            // flights alternate between two stairwells so they never stack under each other
            for (int f = 0; f < floors - 1; f++) Stairs(g, f % 2 == 0 ? w / 2 : 0, 3, -8, f * (storey + 1), storey + 1, Stone, Pal.Ramp(Conc, 2, seed + f * 3));
            // ruin: bite a chunk out of the top and one side, spill rubble
            var rnd = new System.Random(seed * 13 + 1);
            int top = floors * (storey + 1);
            int bx = rnd.Next(-w, 0), bz = rnd.Next(-l, 0);
            g.ClearBox(bx, top - storey * (1 + rnd.Next(2)), bz, bx + w, top + 2, bz + l);
            g.Mat(Stone);
            for (int i = 0; i < 60; i++)
            {
                int x = rnd.Next(-w - 8, w + 8), z = rnd.Next(-l - 8, l + 8);
                if (Mathf.Abs(x) < w && Mathf.Abs(z) < l) continue;
                g.Set(x, 0, z, Pal.Ramp(Conc, rnd.Next(4), i));
                if (rnd.NextDouble() < 0.3) g.Set(x, 1, z, Pal.Ramp(Conc, 1, i));
            }
            return g;
        }

        public static VoxelGrid GasPumpGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-5, 0, -4, 5, 1, 4, Pal.Ramp(Conc, 1));
            g.Box(-3, 2, -2, 3, 20, 2, p => p.y > 16 ? Pal.Cream[3] : p.y > 13 ? Pal.Hex("b02818") : Pal.Pick(Pal.Cream, p, 1500, 1));
            g.Mat(Glass); g.Box(-2, 12, 3, 2, 15, 3, Pal.Ramp(Pal.Glass, 3));               // dial window
            g.Mat(Scrap); g.Box(3, 8, 0, 4, 11, 1, Pal.Ramp(Pal.Black, 1));                 // nozzle holster
            g.Tube(new Vector3(4, 10, 1), new Vector3(6, 3, 2), 0.5f, Pal.Ramp(Pal.Black, 0));
            return g;
        }

        static VoxelGrid Streetlight()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(0, 0, 0, 0, 60, 0, Pal.Ramp(Pal.Metal, 2));
            g.Box(0, 60, 0, 0, 60, 10, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Glass); g.Box(-1, 58, 9, 1, 59, 11, Pal.Solid(Pal.LightY));
            return g;
        }

        // ------------------------------------------------------------------ placement

        struct Placement { public string id; public Vector2 pos; public float yaw; public bool dynamic; public bool crate; public float y; public string table, visual; }
        static readonly Dictionary<int, List<Placement>> layouts = new Dictionary<int, List<Placement>>();
        static int layoutSeed = int.MinValue;

        static List<Placement> Layout(WorldGen world, Settlement st)
        {
            if (layoutSeed != world.seed) { layouts.Clear(); layoutSeed = world.seed; }
            if (layouts.TryGetValue(st.index, out var list)) return list;
            list = new List<Placement>();
            var rnd = new System.Random(st.index * 7919 ^ world.seed);
            bool Free(Vector2 p, float clear) => world.Sample(p.x, p.y).roadDist > clear;
            // searchable furniture and lights inside a building: offsets in metres in the building's frame
            void Inside(Vector2 at, float yaw, float scale, params (float x, float y, float z, string table, string visual)[] spots)
            {
                var q = Quaternion.Euler(0, yaw, 0);
                foreach (var sp in spots)
                {
                    var o = q * new Vector3(sp.x, 0, sp.z);
                    list.Add(new Placement { id = sp.table == "light" ? "Light" : "Loot", pos = at + new Vector2(o.x, o.z), yaw = yaw + 180f, y = sp.y, table = sp.table, visual = sp.visual });
                }
            }
            float Face(Vector2 p) => Mathf.Round(Mathf.Atan2(st.pos.x - p.x, st.pos.y - p.y) * Mathf.Rad2Deg / 90f) * 90f;
            switch (st.kind)
            {
                case Biome.Village:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f, r = 14f + (float)rnd.NextDouble() * 18f;
                        var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (!Free(p, 9f)) continue;
                        float yaw = Face(p);
                        list.Add(new Placement { id = "Farmhouse" + (i % 2), pos = p, yaw = yaw });
                        Inside(p, yaw, 0.16f, (-2.2f, 0.16f, 1.8f, "kitchen", "fridge"), (2.4f, 0.16f, 1.8f, "farm", "crate"));
                        if (rnd.NextDouble() < 0.4) Inside(p, yaw, 0.16f, (0f, 2.3f, 0f, "light", null));
                        var q = Quaternion.Euler(0, yaw, 0);
                        var back = q * new Vector3(0, 0, 6f);
                        list.Add(new Placement { id = "Crate", pos = p + new Vector2(back.x + 4.5f, back.z), yaw = yaw + 10f, dynamic = true, crate = true });
                        for (int f = -1; f <= 1; f++)
                        {
                            var fp = q * new Vector3(f * 4f, 0, 9f);
                            list.Add(new Placement { id = "Fence0", pos = p + new Vector2(fp.x, fp.z), yaw = yaw });
                        }
                        if (rnd.NextDouble() < 0.5)
                        {
                            var hp = q * new Vector3(-7f, 0, 4f);
                            list.Add(new Placement { id = "Haystack0", pos = p + new Vector2(hp.x, hp.z), yaw = 0 });
                        }
                    }
                    break;
                case Biome.Town:
                    for (int i = 0; i < 2; i++)
                    {
                        // an old fuel station by the road
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        var gp = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (12f + i * 2.4f);
                        if (world.Sample(gp.x, gp.y).roadDist < 5f) continue;
                        list.Add(new Placement { id = "GasPump0", pos = gp, yaw = Face(gp) + 180f });
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.4f, r = 16f + (float)rnd.NextDouble() * 14f;
                        var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (!Free(p, 7f)) continue;
                        float yaw = Mathf.Round(-a * Mathf.Rad2Deg / 90f) * 90f;
                        list.Add(new Placement { id = "Shack" + (i % 3), pos = p, yaw = yaw });
                        Inside(p, yaw, 0.08f, (0.8f, 0.08f, 0.9f, i % 3 == 0 ? "tools" : "house", "locker"));
                        list.Add(new Placement { id = "Table", pos = p, yaw = yaw });
                        var q = Quaternion.Euler(0, yaw, 0);
                        foreach (var off in new[] { new Vector3(2.4f, 0, -2.2f), new Vector3(-2.3f, 0, -2.6f) })
                        {
                            var cp = q * off;
                            list.Add(new Placement { id = "Crate", pos = p + new Vector2(cp.x, cp.z), yaw = yaw + 15f, dynamic = true, crate = true });
                        }
                    }
                    {
                        // one shop on the main road
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        var sp = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 30f;
                        if (Free(sp, 12f))
                        {
                            float fy = Face(sp);
                            list.Add(new Placement { id = "Shop" + (st.index % 2), pos = sp, yaw = fy });
                            Inside(sp, fy, 0.2f, (-2f, 0.2f, -1.6f, "shop", "shelf"), (2f, 0.2f, -1.6f, "shop", "shelf"), (-2f, 0.2f, 1.6f, "shop", "shelf"), (4.5f, 0.2f, 3f, "shop", "locker"), (0f, 3f, 0f, "light", null));
                        }
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i + 0.5f) / 6f * Mathf.PI * 2f, r = 36f + (float)rnd.NextDouble() * 12f;
                        var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (!Free(p, 11f)) continue;
                        float fy = Face(p);
                        list.Add(new Placement { id = "BrickHouse" + (i % 2), pos = p, yaw = fy });
                        Inside(p, fy, 0.16f, (-2.6f, 0.16f, 2f, "kitchen", "fridge"), (-2.6f, 3.2f, 1.5f, "house", "locker"), (0.5f, 0.16f, 2.2f, i == 0 ? "garage" : "house", i == 0 ? "workbench" : "shelf"));
                        if (rnd.NextDouble() < 0.5) Inside(p, fy, 0.16f, (0f, 2.6f, 0f, "light", null));
                    }
                    break;
                case Biome.City:
                    for (float x = -st.radius; x <= st.radius; x += 22f)
                    for (float z = -st.radius; z <= st.radius; z += 22f)
                    {
                        var p = st.pos + new Vector2(x, z) + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 3f;
                        if ((p - st.pos).magnitude > st.radius * 0.85f || (p - st.pos).magnitude < 10f) continue;
                        var s = world.Sample(p.x, p.y);
                        if (s.roadDist < 12f)
                        {
                            if (s.roadDist > 6f && rnd.NextDouble() < 0.35) { list.Add(new Placement { id = "Streetlight0", pos = p, yaw = 0 }); list.Add(new Placement { id = "Light", pos = p + new Vector2(0f, 0.64f), yaw = 0, y = 4.6f, table = "street" }); }
                            continue;
                        }
                        float ty = rnd.Next(4) * 90f;
                        if (rnd.NextDouble() < 0.2)
                        {
                            list.Add(new Placement { id = "Shop" + rnd.Next(2), pos = p, yaw = ty });
                            Inside(p, ty, 0.2f, (-2f, 0.2f, -1.6f, "shop", "shelf"), (2f, 0.2f, 1.6f, "shop", "shelf"), (4.5f, 0.2f, 3f, "kitchen", "fridge"));
                            continue;
                        }
                        list.Add(new Placement { id = "Tower" + rnd.Next(4), pos = p, yaw = ty });
                        Inside(p, ty, 0.2f, (-3f, 0.2f, 3f, "office", "locker"), (-3.5f, 3.4f, -2.5f, "house", "shelf"), (1f, 3.4f, 3f, "kitchen", "fridge"));
                        list.Add(new Placement { id = "Crate", pos = p + new Vector2(7f, -6f), yaw = 20f, dynamic = true, crate = true });
                    }
                    break;
            }
            layouts[st.index] = list;
            return list;
        }

        /// <summary>Buildings placed in a settlement (template id, position, yaw) — NPCs use it to find shop counters.</summary>
        public static void Buildings(WorldGen world, Settlement st, List<(string id, Vector2 pos, float yaw)> into)
        {
            into.Clear();
            foreach (var pl in Layout(world, st)) if (pl.id != "Loot" && pl.id != "Light") into.Add((pl.id, pl.pos, pl.yaw));
        }

        public delegate (VoxelGrid grid, Mesh mesh, float size)? Lookup(string id);

        /// <summary>Spawn biome vegetation and any settlement pieces that fall inside this chunk.</summary>
        public static void Populate(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat, System.Random rnd, Lookup legacy)
        {
            Ensure();
            var world = terrain.World;
            var store = terrain.DestructionState;
            float cx = (c.x + 0.5f) * DeformableTerrain.ChunkWorld, cz = (c.y + 0.5f) * DeformableTerrain.ChunkWorld;
            var biome = world.BiomeAt(cx, cz);

            // ---- wild vegetation
            int count = biome switch { Biome.Forest => rnd.Next(1, 5), Biome.Tropical => rnd.Next(1, 4), Biome.Nuclear => rnd.Next(0, 3), Biome.Desert => rnd.Next(0, 3), Biome.Village => rnd.Next(0, 2), _ => 0 };
            for (int i = 0; i < count; i++)
            {
                float x = (c.x + (float)rnd.NextDouble()) * DeformableTerrain.ChunkWorld;
                float z = (c.y + (float)rnd.NextDouble()) * DeformableTerrain.ChunkWorld;
                double roll = rnd.NextDouble();
                int variant = rnd.Next(4), yawSteps = rnd.Next(4);
                string key = $"v{c.x},{c.y},{i}";
                var s = world.Sample(x, z);
                if (s.roadDist < 7f || !float.IsNaN(s.water) || s.shore > 0.3f || s.feature != 0) continue;
                if (world.SettlementAt(x, z) != null && biome != Biome.Village) continue;
                string id; string name; bool dyn = false;
                switch (biome)
                {
                    case Biome.Forest:
                        if (roll < 0.55) { id = "Pine" + variant % 3; name = "Tree"; }
                        else if (roll < 0.85) { id = "Bush" + (variant % 2); name = "Bush"; }
                        else if (roll < 0.93) { id = "Log0"; name = "Log"; }
                        else { id = "Rock" + variant; name = "Rock"; }
                        break;
                    case Biome.Tropical:
                        if (roll < 0.4) { id = "Palm" + variant % 2; name = "Tree"; }
                        else if (roll < 0.75) { id = "Bush2"; name = "Bush"; }
                        else { id = "Fern0"; name = "Bush"; }
                        break;
                    case Biome.Nuclear:
                        if (roll < 0.35) { id = "Tree" + variant; name = "Tree"; }
                        else if (roll < 0.7) { id = "Barrel0"; name = "Barrel"; dyn = true; }
                        else { id = "Rock" + variant; name = "Rock"; }
                        break;
                    case Biome.Village: id = "Bush1"; name = "Bush"; break;
                    default:
                        if (roll < 0.3) { id = "Tree" + variant; name = "Tree"; }
                        else if (roll < 0.55) { id = "Cactus" + variant % 2; name = "Cactus"; }
                        else if (roll < 0.65) { id = "Bush1"; name = "Bush"; }
                        else { id = "Rock" + variant; name = "Rock"; }
                        break;
                }
                SpawnById(terrain, id, name, parent, mat, new Vector3(x, terrain.Height(x, z) - 0.05f, z), yawSteps * 90f + (float)rnd.NextDouble() * 20f, dyn, store, key, legacy);
            }

            // ---- settlement pieces whose anchor is in this chunk
            foreach (var st in world.settlements)
            {
                if (Mathf.Abs(st.pos.x - cx) > st.radius + 20f || Mathf.Abs(st.pos.y - cz) > st.radius + 20f) continue;
                var list = Layout(world, st);
                for (int i = 0; i < list.Count; i++)
                {
                    var pl = list[i];
                    if (DeformableTerrain.ChunkOf(new Vector3(pl.pos.x, 0, pl.pos.y)) != c) continue;
                    if (pl.id == "Loot" || pl.id == "Light") { SpawnExtra(terrain, st, i, pl, parent, mat); continue; }
                    float y = terrain.Height(pl.pos.x, pl.pos.y) + (pl.dynamic ? 0.08f : -0.1f);
                    var d = SpawnById(terrain, pl.id, pl.id.TrimEnd('0', '1', '2', '3'), parent, mat, new Vector3(pl.pos.x, y, pl.pos.y), pl.yaw, pl.dynamic, store, $"s{st.index},{i}", legacy);
                    if (!d) continue;
                    if (pl.crate)
                    {
                        d.shatterBelow = 0.6f; d.impactThreshold = 2.5f;
                        d.loot = new[] { ResourceType.Scrap, ResourceType.Rubber, ResourceType.Cloth, ResourceType.Glass };
                        d.lootRolls = 2;
                    }
                    if (pl.id == "Table") d.shatterBelow = 0.5f;
                    if (pl.id == "GasPump0")
                    {
                        var pump = d.gameObject.AddComponent<GasPump>();
                        pump.key = "P" + st.index + "," + i;
                        pump.stock = 40f + (float)new System.Random(st.index * 31 + i).NextDouble() * 360f;
                        pump.nozzle = new Vector3(0.5f, 0.8f, 0.15f);
                    }
                }
            }
        }

        /// <summary>A searchable piece of furniture, or a light that comes on at night.</summary>
        static void SpawnExtra(DeformableTerrain terrain, Settlement st, int i, Placement pl, Transform parent, Material mat)
        {
            // the building sits on the ground at its own origin; interior offsets are measured from that floor
            float baseY = terrain.Height(pl.pos.x, pl.pos.y) - 0.1f;
            var pos = new Vector3(pl.pos.x, baseY + pl.y, pl.pos.y);
            if (pl.id == "Light")
            {
                var go = new GameObject(pl.table == "street" ? "StreetLight" : "HouseLight");
                go.transform.SetParent(parent, true);
                go.transform.position = pos;
                var nl = go.AddComponent<NightLight>();
                nl.street = pl.table == "street";
                return;
            }
            var def = MadMax.Building.FurnitureLibrary.Get(pl.visual ?? "shelf");
            var lg = new GameObject("LootSpot", typeof(MeshFilter), typeof(MeshRenderer));
            lg.transform.SetParent(parent, true);
            lg.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, pl.yaw, 0));
            lg.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            lg.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var box = lg.AddComponent<BoxCollider>(); box.center = def.mesh.bounds.center; box.size = def.mesh.bounds.size;
            var loot = lg.AddComponent<Lootable>();
            loot.key = "L" + st.index + "," + i; loot.table = pl.table;
            loot.title = pl.visual == "fridge" ? "FRIDGE" : pl.visual == "crate" ? "BOX" : pl.visual == "workbench" ? "WORKBENCH" : pl.visual == "locker" ? "LOCKER" : "SHELF";
        }

        static bool IsBuilding(string id) => id.StartsWith("Farmhouse") || id.StartsWith("BrickHouse") || id.StartsWith("Tower") || id.StartsWith("Shop")
                                             || id.StartsWith("Shack") || id.StartsWith("Haystack") || id.StartsWith("GasPump") || id.StartsWith("Fence");

        static DestructibleVoxels SpawnById(DeformableTerrain terrain, string id, string name, Transform parent, Material mat, Vector3 pos, float yaw, bool dyn, Dictionary<string, VoxelGrid> store, string key, Lookup legacy)
        {
            VoxelGrid grid; Mesh mesh; float size;
            if (templates.TryGetValue(id, out var t)) { grid = t.grid; mesh = t.mesh; size = t.size; }
            else
            {
                var l = legacy(id);
                if (l == null) return null;
                (grid, mesh, size) = l.Value;
            }
            bool plant = name == "Tree" || name == "Bush";
            if (plant && terrain.vegetationMaterial) mat = terrain.vegetationMaterial;
            var d = DestructibleVoxels.Spawn(name, grid, mesh, mat, parent, pos, yaw, dyn, store, key, size, id);
            if (!d) return null;
            if (id == "Barrel0") d.gameObject.AddComponent<Hazard>().radiation = 0.6f;
            if (!dyn && IsBuilding(id))
            {
                FloraBlocker.Add(d.gameObject);
                Overgrowth.Attach(d, grid, size, key, terrain.World.BiomeAt(pos.x, pos.z));
            }
            return d;
        }
    }
}
