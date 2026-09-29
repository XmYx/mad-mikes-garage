using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Voxel grids for structure, utility, industry, garden and decor pieces (0.08 m voxels; origin = bottom centre
    /// on the mounting surface, +Y up / away from the surface, +Z front).</summary>
    public static class BuildPieces
    {
        const byte Scrap = (byte)ResourceType.Scrap, Wood = (byte)ResourceType.Wood, Cloth = (byte)ResourceType.Cloth, Glass = (byte)ResourceType.Glass,
            Stone = (byte)ResourceType.Stone, Iron = (byte)ResourceType.Iron, Copper = (byte)ResourceType.Copper, Concrete = (byte)ResourceType.Concrete;

        static readonly Color32[] Brick = { Pal.Hex("5a2a1c"), Pal.Hex("6e3422"), Pal.Hex("823e28"), Pal.Hex("964a30") };
        static readonly Color32[] Conc = { Pal.Hex("5c5a56"), Pal.Hex("6e6b66"), Pal.Hex("807c76"), Pal.Hex("908c86") };
        static readonly Color32[] Soil = { Pal.Hex("2e1c10"), Pal.Hex("3a2414"), Pal.Hex("4a2e1a"), Pal.Hex("5a3a22") };
        static readonly Color32[] Leaf = { Pal.Hex("2a5a1c"), Pal.Hex("367024"), Pal.Hex("46862c"), Pal.Hex("5a9e36") };
        static readonly Color32 Mortar = Pal.Hex("9a9080"), Hazard = Pal.Hex("d4b020"), Flower = Pal.Hex("d04070"), Water = Pal.Hex("3a6a8a");

        static VoxMat Planks(int seed) => Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed), Pal.Ramp(Pal.Wood, 1, seed + 1), 0, 4);
        static VoxMat Bricks(int seed) => p => (p.y % 3 == 0 || (p.x + (p.y / 3 % 2) * 3) % 6 == 0) ? Mortar : Pal.Pick(Brick, p, seed, 2);
        static VoxMat Concrete_(int seed) => p => (p.y % 12 == 0 || p.x % 12 == 0) ? Conc[0] : Pal.Pick(Conc, p, seed, 2);

        // ------------------------------------------------------------------ structure
        public enum WallStyle { Wood, Brick, Concrete, Scrap }

        static void WallMat(VoxelGrid g, WallStyle s)
        {
            g.Mat(s == WallStyle.Wood ? Wood : s == WallStyle.Brick ? Stone : s == WallStyle.Concrete ? Concrete : Scrap);
        }
        static VoxMat WallPaint(WallStyle s, int seed) => s switch
        {
            WallStyle.Wood => Planks(seed),
            WallStyle.Brick => Bricks(seed),
            WallStyle.Concrete => Concrete_(seed),
            _ => Pal.Stripe(Pal.Weathered(Pal.Metal, 0.55f, seed, 2, 0), Pal.Ramp(Pal.Rust, 2, seed + 1), 0, 4)
        };

        /// <summary>2 m × 2.4 m wall; hole: 0 none, 1 window, 2 doorway.</summary>
        public static VoxelGrid Wall(WallStyle style, int hole, int seed)
        {
            var g = new VoxelGrid();
            WallMat(g, style);
            var paint = WallPaint(style, seed);
            for (int x = -12; x <= 12; x++)
            for (int y = 0; y <= 29; y++)
            {
                bool window = hole == 1 && Mathf.Abs(x) <= 5 && y >= 12 && y <= 22;
                bool door = hole == 2 && Mathf.Abs(x) <= 6 && y <= 27;          // 1.04 × 2.24 m: roomy for a third-person camera
                if (door) continue;
                if (window)
                {
                    if (y == 17 || x == 0) { g.Mat(Wood); g.Set(x, y, 1, Pal.Ramp(Pal.Wood, 1)); WallMat(g, style); continue; }   // mullions
                    g.Mat(Glass); g.Set(x, y, 1, Pal.Ramp(Pal.Glass, 2, seed)); WallMat(g, style); continue;
                }
                g.Set(x, y, 0, paint); g.Set(x, y, 1, paint);
            }
            if (hole == 2) { g.Mat(Wood); g.Box(-7, 0, 2, -7, 28, 2, Pal.Ramp(Pal.Wood, 1)); g.Box(7, 0, 2, 7, 28, 2, Pal.Ramp(Pal.Wood, 1)); g.Box(-7, 28, 2, 7, 28, 2, Pal.Ramp(Pal.Wood, 1)); }
            return g;
        }

        public static VoxelGrid Door(bool metal)
        {
            var g = new VoxelGrid().Mat(metal ? Iron : Wood);
            var m = metal ? Pal.Weathered(Pal.Metal, 0.35f, 811, 2, 0) : Planks(812);
            g.Box(-6, 0, 0, 6, 27, 1, m);
            g.Mat(Scrap);
            g.Box(4, 12, 2, 5, 12, 2, Pal.Solid(Pal.Chrome[2]));                        // handle at 1 m
            if (metal) { g.Box(-6, 6, 2, 6, 6, 2, Pal.Ramp(Pal.Rust, 2)); g.Box(-6, 21, 2, 6, 21, 2, Pal.Ramp(Pal.Rust, 2)); }
            else { g.Box(-6, 4, 2, 6, 4, 2, Pal.Ramp(Pal.Wood, 0)); g.Box(-6, 23, 2, 6, 23, 2, Pal.Ramp(Pal.Wood, 0)); }
            return g;
        }

        public static VoxelGrid Floor(WallStyle style, int seed)
        {
            var g = new VoxelGrid();
            WallMat(g, style);
            g.Box(-12, 0, -12, 12, 1, 12, style == WallStyle.Wood ? Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed), Pal.Ramp(Pal.Wood, 1, seed + 1), 2, 3) : WallPaint(style, seed));
            return g;
        }

        public static VoxelGrid Stairs()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int i = 0; i < 15; i++)
            {
                int z0 = -20 + i * 8 / 3, y = i * 2;
                g.Box(-6, y, z0, 6, y + 1, z0 + 3, Planks(820 + i));
            }
            foreach (int x in new[] { -7, 7 })
                for (int i = 0; i < 40; i++) { int y = Mathf.RoundToInt(i * 30f / 40f); g.Box(x, Mathf.Max(0, y - 2), -20 + i, x, y + 1, -20 + i, Pal.Ramp(Pal.Wood, 1, 830)); }
            return g;
        }

        public static VoxelGrid Ladder()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -4, 4 }) g.Box(x, 0, 0, x, 30, 0, Pal.Ramp(Pal.Wood, 1, 840));
            for (int y = 3; y <= 29; y += 4) g.Box(-3, y, 0, 3, y, 0, Pal.Ramp(Pal.Wood, 2, 841));
            return g;
        }

        public static VoxelGrid Roof(bool slope)
        {
            var g = new VoxelGrid().Mat(Scrap);
            for (int x = -12; x <= 12; x++)
            for (int z = -12; z <= 12; z++)
            {
                int y = slope ? (z + 12) / 2 : 0;
                Color32 c = (x + 12) % 4 == 0 ? Pal.Rust[1] : Pal.Pick(Pal.Rust, new Vector3Int(x, y, z), 850, 3);
                g.Set(x, y, z, _ => c);
                if (slope && (z + 12) % 2 == 1) g.Set(x, y - 1, z, _ => c);
            }
            return g;
        }

        public static VoxelGrid Fence(bool wire)
        {
            var g = new VoxelGrid();
            g.Mat(wire ? Scrap : Wood);
            foreach (int x in new[] { -12, 0, 12 }) g.Box(x, 0, 0, x, 16, 0, wire ? Pal.Ramp(Pal.Metal, 2, 860) : Pal.Ramp(Pal.Wood, 1, 861 + x));
            if (wire) { for (int x = -12; x <= 12; x++) for (int y = 1; y <= 15; y++) if ((x + y) % 3 == 0 || (x - y) % 3 == 0) g.Set(x, y, 0, Pal.Ramp(Pal.Chrome, 1)); }
            else { g.Box(-12, 5, 1, 12, 6, 1, Planks(862)); g.Box(-12, 12, 1, 12, 13, 1, Planks(863)); }
            return g;
        }

        public static VoxelGrid Gate()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-12, 2, 0, 12, 3, 0, Planks(870)); g.Box(-12, 12, 0, 12, 13, 0, Planks(871));
            for (int x = -12; x <= 12; x += 4) g.Box(x, 1, 0, x, 15, 0, Pal.Ramp(Pal.Wood, 1, 872));
            g.Tube(new Vector3(-12, 3, 0), new Vector3(12, 12, 0), 0.5f, Pal.Ramp(Pal.Wood, 1, 873));
            return g;
        }

        public static VoxelGrid Post()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-1, 0, -1, 1, 29, 1, Pal.Ramp(Pal.Wood, 1, 880));
            return g;
        }

        // ------------------------------------------------------------------ furniture & decor
        public static VoxelGrid Chair()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -3, 3 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 5, z, Pal.Ramp(Pal.Wood, 1, 900));
            g.Box(-3, 6, -3, 3, 6, 3, Planks(901));
            g.Box(-3, 7, -3, 3, 14, -3, Planks(902));
            return g;
        }

        public static VoxelGrid Table()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -5, 5 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Wood, 1, 905));
            g.Box(-9, 10, -6, 9, 10, 6, Planks(906));
            return g;
        }

        public static VoxelGrid Sofa()
        {
            var g = new VoxelGrid().Mat(Cloth);
            var fabric = Pal.Weathered(Pal.Olive, 0.25f, 910, 2, 0);
            g.Box(-12, 0, -4, 12, 5, 4, fabric);
            g.Box(-12, 6, -4, 12, 11, -2, fabric);
            g.Box(-12, 6, -4, -10, 8, 4, fabric); g.Box(10, 6, -4, 12, 8, 4, fabric);
            g.Box(-11, 6, -1, -1, 6, 3, Pal.Ramp(Pal.Olive, 3, 911)); g.Box(1, 6, -1, 11, 6, 3, Pal.Ramp(Pal.Olive, 3, 912));
            return g;
        }

        public static VoxelGrid Rug()
        {
            var g = new VoxelGrid().Mat(Cloth);
            for (int x = -12; x <= 12; x++) for (int z = -8; z <= 8; z++)
            {
                bool border = Mathf.Abs(x) >= 11 || Mathf.Abs(z) >= 7, diamond = (Mathf.Abs(x) + Mathf.Abs(z)) % 6 == 0;
                g.Set(x, 0, z, Pal.Solid(border ? Brick[2] : diamond ? Pal.Cream[2] : Pal.Olive[1]));
            }
            return g;
        }

        public static VoxelGrid Painting()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-8, 0, -6, 8, 0, 6, Pal.Ramp(Pal.Wood, 0, 915));
            g.Mat(Cloth);
            for (int x = -7; x <= 7; x++) for (int z = -5; z <= 5; z++)
                g.Set(x, 1, z, Pal.Solid(z > 1 ? Pal.PaleBlue[2] : z > -1 ? Pal.Sand[3] : (x + z) % 3 == 0 ? Pal.Rust[2] : Pal.Sand[1]));
            return g;
        }

        public static VoxelGrid Flag()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(0, 0, 0, 0, 40, 0, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Cloth);
            for (int x = 1; x <= 14; x++) for (int y = 30; y <= 39; y++) g.Set(x, y + (x % 5 == 0 ? 1 : 0), 0, Pal.Solid(y > 34 ? Brick[3] : Pal.Black[2]));
            return g;
        }

        public static VoxelGrid Tyres()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Rubber);
            for (int k = 0; k < 3; k++) g.CylY(0, 0, 5.5f, k * 3, k * 3 + 2, Pal.Ramp(Pal.Tire, 2, 920 + k), 2.5f);
            return g;
        }

        public static VoxelGrid Barrel()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 3.3f, 0, 10, p => p.y == 3 || p.y == 7 ? Pal.Rust[0] : Pal.Pick(Pal.Rust, p, 925, 3));
            return g;
        }

        public static VoxelGrid Sign()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(0, 0, 0, 0, 14, 0, Pal.Ramp(Pal.Wood, 1));
            g.Mat(Scrap);
            g.Box(-7, 10, 1, 7, 18, 1, p => (p.x + p.y) % 7 == 0 ? Pal.Black[1] : Hazard);
            return g;
        }

        public static VoxelGrid SkullPole()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(0, 0, 0, 0, 22, 0, Pal.Ramp(Pal.Wood, 1));
            g.Mat(Stone);
            g.Box(-2, 23, -2, 2, 27, 2, Pal.Ramp(Pal.Cream, 3));
            g.Set(-1, 25, 2, Pal.Solid(Pal.Void)); g.Set(1, 25, 2, Pal.Solid(Pal.Void)); g.Box(-1, 23, 2, 1, 23, 2, Pal.Solid(Pal.Black[1]));
            return g;
        }

        /// <summary>Livestock trough (roadmap 23): a plank box on legs, split for feed and water.</summary>
        public static VoxelGrid Trough()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -9, 9 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 2, z, Pal.Ramp(Pal.Wood, 1, 2301));
            g.Box(-10, 3, -4, 10, 3, 4, Pal.Ramp(Pal.Wood, 2, 2302));                                     // floor
            g.Box(-10, 4, -4, 10, 7, -4, Pal.Ramp(Pal.Wood, 2, 2303)); g.Box(-10, 4, 4, 10, 7, 4, Pal.Ramp(Pal.Wood, 2, 2303));
            g.Box(-10, 4, -3, -10, 7, 3, Pal.Ramp(Pal.Wood, 1, 2304)); g.Box(10, 4, -3, 10, 7, 3, Pal.Ramp(Pal.Wood, 1, 2304));
            g.Box(0, 4, -3, 0, 7, 3, Pal.Ramp(Pal.Wood, 1, 2304));                                        // divider
            g.Box(-9, 4, -3, -1, 5, 3, Pal.Ramp(Pal.Ochre, 2, 2305));                                     // feed
            g.Mat(Glass); g.Box(1, 4, -3, 9, 5, 3, Pal.Ramp(Pal.PaleBlue, 1, 2306));                     // water
            g.Mat(Scrap); for (int x = -10; x <= 10; x += 5) g.Box(x, 7, -4, x, 7, 4, Pal.Ramp(Pal.Metal, 2, 2307));   // straps
            return g;
        }

        /// <summary>Nest box (roadmap 23): three straw-lined cubbies under a tin roof; hens lay here.</summary>
        public static VoxelGrid NestBox()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-8, 0, -3, 8, 0, 3, Pal.Ramp(Pal.Wood, 2, 2311));
            g.Box(-8, 1, -3, 8, 8, -3, Pal.Ramp(Pal.Wood, 1, 2312));
            foreach (int x in new[] { -8, -3, 2, 8 }) g.Box(x, 1, -2, x, 8, 3, Pal.Ramp(Pal.Wood, 2, 2313));
            g.Box(-7, 1, -2, 7, 1, 2, Pal.Ramp(Pal.Ochre, 3, 2314));                                      // straw
            foreach (int x in new[] { -5, 0, 5 }) g.Set(x, 2, 0, Pal.Solid(Pal.Cream[4]));                  // an egg in each
            g.Box(-8, 1, 3, 8, 2, 3, Pal.Ramp(Pal.Wood, 1, 2315));                                        // lip
            g.Mat(Scrap); g.Box(-9, 9, -4, 9, 9, 4, Pal.Weathered(Pal.Metal, 0.4f, 2316, 2));             // tin roof
            return g;
        }

        public static VoxelGrid FlowerPot()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.CylY(0, 0, 2.5f, 0, 3, Pal.Ramp(Brick, 2));
            g.Mat(Wood);
            for (int i = 0; i < 5; i++) { int x = i - 2, z = (i * 7) % 3 - 1; g.Box(x, 4, z, x, 7 + i % 3, z, Pal.Ramp(Leaf, 1)); g.Set(x, 8 + i % 3, z, Pal.Solid(Flower)); }
            return g;
        }

        public static VoxelGrid CeilingLight()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-3, 0, -3, 3, 0, 3, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Glass); g.Box(-2, 1, -2, 2, 1, 2, Pal.Solid(Pal.LightW));
            return g;
        }

        public static VoxelGrid LampPost()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-1, 0, -1, 1, 1, 1, Pal.Ramp(Pal.Metal, 1));
            g.Box(0, 2, 0, 0, 48, 0, Pal.Ramp(Pal.Metal, 2));
            g.Box(0, 48, 0, 0, 48, 8, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Glass); g.Box(-1, 46, 7, 1, 47, 9, Pal.Solid(Pal.LightY));
            return g;
        }

        public static VoxelGrid Heater(bool cooler)
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-5, 0, -2, 5, 8, 2, cooler ? Pal.Weathered(Pal.Cream, 0.2f, 1510, 2, 0) : Pal.Weathered(Pal.Metal, 0.3f, 1511, 2, 0));
            for (int x = -4; x <= 4; x += 2) g.Box(x, 2, 3, x, 7, 3, Pal.Solid(cooler ? Pal.PaleBlue[2] : Pal.Amber));
            return g;
        }

        public static VoxelGrid Fireplace()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.Box(-9, 0, -4, 9, 14, 3, Bricks(1512));
            g.ClearBox(-5, 1, 0, 5, 8, 3);
            g.Box(-5, 1, -3, 5, 1, 0, Pal.Ramp(Pal.Black, 1));
            g.Mat(Wood); g.Box(-3, 2, -1, 3, 3, -1, Pal.Ramp(Pal.Wood, 1)); g.Box(-2, 4, -2, 2, 4, -2, Pal.Solid(Pal.Amber));
            g.Mat(Stone); g.Box(-5, 15, -3, 5, 30, 1, Bricks(1513));                           // chimney
            return g;
        }

        // ------------------------------------------------------------------ utilities
        public static VoxelGrid Generator()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-7, 0, -4, 7, 1, 4, Pal.Ramp(Pal.Metal, 0));
            g.Box(-6, 2, -3, 3, 8, 3, Pal.Weathered(Pal.Olive, 0.3f, 940, 2, 0));
            g.Box(4, 2, -3, 6, 6, 3, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Scrap); g.Box(-5, 9, -2, -1, 11, 2, Pal.Ramp(Pal.Rust, 2)); g.Box(4, 7, -1, 4, 11, -1, Pal.Ramp(Pal.Metal, 1));
            g.Mat(Copper); g.Box(-7, 4, 0, -7, 5, 1, Pal.Solid(Pal.Bronze[2]));
            return g;
        }

        public static VoxelGrid WindmillTower()
        {
            var g = new VoxelGrid().Mat(Iron);
            for (int y = 0; y <= 70; y++) { int w = Mathf.Max(1, 6 - y / 14); foreach (int x in new[] { -w, w }) foreach (int z in new[] { -w, w }) g.Set(x, y, z, Pal.Ramp(Pal.Metal, 1, 950)); if (y % 10 == 0) { g.Box(-w, y, -w, w, y, -w, Pal.Ramp(Pal.Metal, 1)); g.Box(-w, y, w, w, y, w, Pal.Ramp(Pal.Metal, 1)); } }
            g.Box(-2, 70, -3, 2, 73, 3, Pal.Weathered(Pal.Cream, 0.3f, 951, 2, 0));
            g.Mat(Scrap); g.Box(0, 71, -8, 0, 73, -4, Pal.Ramp(Pal.Rust, 2));               // tail vane
            return g;
        }

        public static VoxelGrid WindmillRotor()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-1, -1, 0, 1, 1, 1, Pal.Ramp(Pal.Metal, 2));
            for (int b = 0; b < 3; b++)
            {
                float a = b * Mathf.PI * 2f / 3f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                g.Tube(d * 1.5f, d * 22f, 0.9f, Pal.Weathered(Pal.Cream, 0.25f, 952 + b, 2, 0));
            }
            return g;
        }

        public static VoxelGrid Battery()
        {
            var g = new VoxelGrid().Mat(Copper);
            for (int i = 0; i < 3; i++) g.Box(-7 + i * 5, 0, -3, -4 + i * 5, 7, 3, Pal.Ramp(Pal.Black, 2, 960 + i));
            g.Mat(Scrap);
            for (int i = 0; i < 3; i++) { g.Set(-6 + i * 5, 8, 0, Pal.Solid(Pal.TailR)); g.Set(-5 + i * 5, 8, 0, Pal.Solid(Pal.Chrome[1])); }
            return g;
        }

        public static VoxelGrid PowerPole()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-1, 0, -1, 1, 60, 1, Pal.Ramp(Pal.Wood, 1, 965));
            g.Box(-8, 56, 0, 8, 57, 0, Pal.Ramp(Pal.Wood, 1, 966));
            g.Mat(Glass); foreach (int x in new[] { -7, 7 }) g.Box(x, 58, 0, x, 59, 0, Pal.Ramp(Pal.Glass, 3));
            return g;
        }

        public static VoxelGrid RainCollector()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 5f, 0, 12, p => p.y % 4 == 0 ? Pal.Metal[1] : Pal.PaleBlue[0]);
            for (int y = 13; y <= 16; y++) g.CylY(0, 0, 5f + (y - 13) * 1.5f, y, y, Pal.Ramp(Pal.Metal, 2), 4f + (y - 13) * 1.5f);   // funnel
            g.Mat(Iron); g.Box(0, 2, 5, 0, 2, 6, Pal.Solid(Pal.Chrome[2]));
            return g;
        }

        public static VoxelGrid WaterTank()
        {
            var g = new VoxelGrid().Mat(Scrap);
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -8, 8 }) g.Box(x, 0, z, x, 14, z, Pal.Ramp(Pal.Metal, 1));
            g.CylY(0, 0, 10f, 15, 32, p => p.y % 5 == 0 ? Pal.Rust[1] : Pal.PaleBlue[1], 9f);
            g.CylY(0, 0, 10f, 33, 33, Pal.Ramp(Pal.Metal, 2));
            return g;
        }

        public static VoxelGrid Filter()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 3.5f, 0, 14, p => p.y > 10 ? Pal.Ramp(Pal.Glass, 2)(p) : p.y > 4 ? Pal.Black[2] : Pal.Sand[3]);
            g.Box(-4, 14, -1, 4, 14, 1, Pal.Ramp(Pal.Metal, 2));
            return g;
        }

        public static VoxelGrid Pump()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-5, 0, -4, 5, 1, 4, Pal.Ramp(Pal.Metal, 0));
            g.CylX(5, 0, 3f, -4, 3, Pal.Ramp(Pal.PaleBlue, 1));
            g.Box(4, 3, -2, 7, 8, 2, Pal.Ramp(Pal.Metal, 2));
            g.CylZ(-5, 3, 1.3f, -8, 4, Pal.Ramp(Pal.Metal, 1));
            return g;
        }

        public static VoxelGrid Sink()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-6, 0, -4, 6, 10, 4, Pal.Weathered(Pal.Cream, 0.2f, 970, 2, 0));
            g.Box(-5, 10, -3, 5, 11, 3, Pal.Ramp(Pal.Chrome, 2)); g.Box(-4, 11, -2, 4, 11, 2, Pal.Solid(Water));
            g.Mat(Iron); g.Box(0, 12, -3, 0, 15, -3, Pal.Ramp(Pal.Chrome, 2)); g.Box(0, 15, -3, 0, 15, -1, Pal.Ramp(Pal.Chrome, 2));
            return g;
        }

        public static VoxelGrid Shower()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-6, 0, -6, 6, 0, 6, Pal.Ramp(Pal.Chrome, 1));
            g.Box(-6, 1, -6, 6, 28, -6, Pal.Weathered(Pal.Cream, 0.25f, 975, 2, 0)); g.Box(-6, 1, -6, -6, 28, 6, Pal.Weathered(Pal.Cream, 0.25f, 976, 2, 0));
            g.Mat(Iron); g.Box(0, 29, -5, 0, 29, 0, Pal.Ramp(Pal.Chrome, 2)); g.Box(-1, 28, -1, 1, 28, 1, Pal.Ramp(Pal.Chrome, 3));
            g.Mat(Cloth); g.Box(6, 6, -6, 6, 27, 6, p => (p.z % 3 == 0) ? Pal.PaleBlue[1] : Pal.PaleBlue[3]);
            return g;
        }

        public static VoxelGrid Sprinkler()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(0, 0, 0, 0, 6, 0, Pal.Ramp(Pal.Metal, 2));
            g.Box(-2, 7, 0, 2, 7, 0, Pal.Ramp(Pal.Chrome, 2)); g.Box(0, 7, -2, 0, 7, 2, Pal.Ramp(Pal.Chrome, 2));
            return g;
        }

        // ------------------------------------------------------------------ garden & industry
        public static VoxelGrid GardenBed(int hx, int hz)
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -hx; x <= hx; x++) for (int z = -hz; z <= hz; z++)
            {
                bool edge = Mathf.Abs(x) == hx || Mathf.Abs(z) == hz;
                if (edge) { g.Mat(Wood); g.Box(x, 0, z, x, 2, z, Pal.Ramp(Pal.Wood, 1, 980)); }
                else { g.Mat((byte)ResourceType.Clay); g.Set(x, 0, z, Pal.Ramp(Soil, 1, 981)); g.Set(x, 1, z, Pal.Ramp(Soil, (x + z) % 2 == 0 ? 2 : 1, 982)); }
            }
            return g;
        }

        // ------------------------------------------------------------------ irrigation & gardens (roadmap 8-9)
        /// <summary>Fieldstone well ring (dark water far below) with a cast-iron hand pump on a plank cover and a bucket.</summary>
        public static VoxelGrid Well()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.CylY(0, 0, 7f, 0, 8, p => p.y % 3 == 0 ? Mortar : Pal.Pick(Conc, p, 1051, 1), 5.2f);                      // stone ring
            g.Mat(Scrap); g.CylY(0, 0, 5.2f, 0, 0, Pal.Solid(Pal.Glass[0]));                                              // water down the shaft
            g.Mat(Wood); g.Box(-5, 8, -5, 5, 8, 0, Planks(1052));                                                         // half cover
            g.Mat(Iron);
            g.CylY(0, -3, 1.3f, 9, 18, Pal.Weathered(Pal.Crimson, 0.4f, 1053, 2, 0));                                     // pump barrel
            g.Box(0, 15, -2, 0, 15, 1, Pal.Ramp(Pal.Metal, 1)); g.Set(0, 14, 1, Pal.Solid(Pal.Metal[0]));                 // spout
            g.Box(-1, 19, -4, 1, 19, -2, Pal.Ramp(Pal.Metal, 0));                                                         // cap
            g.Tube(new Vector3(0, 19, -3), new Vector3(0, 23, -10), 0.5f, Pal.Ramp(Pal.Metal, 2));                        // handle
            g.Mat(Wood); g.CylY(9, 2, 1.8f, 0, 3, p => p.y == 2 ? Pal.Metal[1] : Pal.Wood[2], 1.0f);                      // bucket
            return g;
        }

        /// <summary>Stave tank on a 3 m timber tower with a service ladder: big storage that stands above the beds.</summary>
        public static VoxelGrid WaterTower()
        {
            var g = new VoxelGrid().Mat(Wood);
            var post = Pal.Ramp(Pal.Wood, 1, 1061);
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -10, 10 }) g.Box(x - 1, 0, z - 1, x, 38, z, post);   // legs
            g.Tube(new Vector3(-10, 4, -10), new Vector3(10, 34, -10), 0.5f, post); g.Tube(new Vector3(10, 4, 10), new Vector3(-10, 34, 10), 0.5f, post);
            g.Tube(new Vector3(-10, 4, 10), new Vector3(-10, 34, -10), 0.5f, post); g.Tube(new Vector3(10, 4, -10), new Vector3(10, 34, 10), 0.5f, post);
            g.Box(-12, 39, -12, 12, 39, 12, Planks(1062));                                                                  // deck
            g.CylY(0, 0, 12f, 40, 60, p => (p.y - 40) % 7 == 3 ? Pal.Metal[1]
                : ((int)((Mathf.Atan2(p.z, p.x) + 3.1416f) * 6f) & 1) == 0 ? Pal.Wood[2] : Pal.Pick(Pal.Wood, p, 1063, 3), 10.5f);   // staves and hoops
            g.Mat(Scrap); for (int y = 61; y <= 66; y++) g.CylY(0, 0, 12.5f - (y - 61) * 2.3f, y, y, Pal.Ramp(Pal.Rust, 2, 1064));   // conical lid
            g.Mat(Wood);
            g.Box(-3, 0, -13, -3, 39, -13, post); g.Box(3, 0, -13, 3, 39, -13, post);                                      // ladder
            for (int y = 3; y < 39; y += 3) g.Box(-2, y, -13, 2, y, -13, Pal.Ramp(Pal.Wood, 2, 1065));
            g.Mat(Iron); g.Box(0, 1, 0, 0, 38, 0, Pal.Ramp(Pal.Metal, 1));                                               // down pipe
            return g;
        }

        /// <summary>Timber-framed glasshouse, 3.3 × 4.1 m, gable roof to 3.2 m, a doorway at the front (+Z).</summary>
        public static VoxelGrid Greenhouse()
        {
            var g = new VoxelGrid();
            const int hx = 20, hz = 25, wall = 27, ridge = 40;
            var frame = Pal.Ramp(Pal.Wood, 2, 1071);
            VoxMat pane = p => Pal.Hash(p, 1072) < 0.06f ? Pal.PaleBlue[3] : Pal.Hash(p, 1073) < 0.06f ? Pal.Cream[0]           // glints, grime
                             : ((p.x + p.y + p.z) & 7) == 0 ? Pal.PaleBlue[1] : Pal.PaleBlue[0];
            void Put(int x, int y, int z, bool fr) { g.Mat(fr ? Wood : Glass); g.Set(x, y, z, fr ? frame : pane); }
            for (int x = -hx; x <= hx; x++)
            for (int z = -hz; z <= hz; z++)
            {
                bool sx = Mathf.Abs(x) == hx, sz = Mathf.Abs(z) == hz;
                if (!sx && !sz) continue;
                bool door = z == hz && Mathf.Abs(x) <= 6;
                for (int y = 0; y <= wall; y++)
                {
                    if (door && y <= 25) continue;
                    bool fr = y == 0 || y == wall || (sx && (z % 8 == 0 || sz)) || (sz && (x % 8 == 0 || sx)) || (z == hz && Mathf.Abs(x) == 7) || (door && y == 26);
                    Put(x, y, z, fr);
                }
            }
            for (int x = -hx; x <= hx; x++)
            {
                int top = wall + Mathf.RoundToInt((hx - Mathf.Abs(x)) * (float)(ridge - wall) / hx);
                for (int z = -hz; z <= hz; z++) Put(x, top, z, x == 0 || Mathf.Abs(x) == hx || z % 8 == 0 || Mathf.Abs(z) == hz);   // roof, rafters
                foreach (int z in new[] { -hz, hz })
                    for (int y = wall + 1; y < top; y++) Put(x, y, z, x % 8 == 0);                                          // gable ends
            }
            return g;
        }

        /// <summary>Scarecrow on crossed poles: sack head with stitched face, straw hat, patched coat, straw hands.</summary>
        public static VoxelGrid Scarecrow()
        {
            var g = new VoxelGrid().Mat(Wood);
            var pole = Pal.Ramp(Pal.Wood, 1, 1081);
            g.Box(0, 0, 0, 0, 20, 0, pole);
            g.Box(-9, 16, 0, 9, 16, 0, pole);
            g.Mat(Cloth);
            g.Box(-4, 9, -1, 4, 17, 1, p => Pal.Hash(p, 1082) < 0.1f ? Pal.Crimson[1] : Pal.Pick(Pal.Navy, p, 1083, 1));       // patched coat
            g.Box(-8, 15, -1, -5, 17, 1, Pal.Ramp(Pal.Navy, 1, 1084)); g.Box(5, 15, -1, 8, 17, 1, Pal.Ramp(Pal.Navy, 1, 1085));
            g.Box(-3, 18, -2, 3, 23, 2, p => p.z == 2 && ((Mathf.Abs(p.x) == 1 && p.y == 21) || (p.y == 19 && Mathf.Abs(p.x) <= 1)) ? Pal.Black[0] : Pal.Pick(Pal.Wood, p, 1086, 3));   // sack head
            g.Box(-5, 24, -4, 5, 24, 4, Pal.Ramp(Pal.Ochre, 3, 1087)); g.Box(-3, 25, -2, 3, 26, 2, Pal.Ramp(Pal.Ochre, 2, 1088));  // straw hat
            foreach (int x in new[] { -10, 10 }) g.Box(x, 14, 0, x, 16, 0, Pal.Ramp(Pal.Ochre, 4));                                 // straw hands
            return g;
        }

        /// <summary>Tuning bench: a steel bench with a vice, a tool board and the dyno monitor on a cart.</summary>
        /// <summary>Paint station (roadmap 19): compressor, spray gun on its hose, a rack of paint tins.</summary>
        public static VoxelGrid PaintBooth()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-8, 0, -5, 8, 0, 5, Pal.Ramp(Pal.Metal, 1, 1991));                                               // skid
            g.CylX(4, 1, 3.2f, -7, 1, Pal.Weathered(Pal.Crimson, 0.3f, 1992, 2, 0));                                // compressor tank
            g.Box(-6, 8, -1, -3, 10, 2, Pal.Ramp(Pal.Black, 1, 1993));                                              // motor
            g.Box(-2, 8, 0, -1, 9, 0, Pal.Solid(Pal.Chrome[2]));                                                     // gauge
            g.Mat(Wood);
            foreach (int x in new[] { 3, 7 }) g.Box(x, 1, -4, x, 16, -4, Pal.Ramp(Pal.Wood, 1, 1994));             // rack
            foreach (int y in new[] { 6, 11, 16 }) g.Box(3, y, -5, 7, y, -3, Pal.Ramp(Pal.Wood, 2, 1995));
            g.Mat(Scrap);
            var tins = new[] { Pal.Crimson[3], Pal.Navy[3], Pal.Ochre[3], Pal.Moss[3], Pal.Cream[3], Pal.Black[2] };
            for (int i = 0; i < 6; i++) { int x = 4 + (i % 3), y = i < 3 ? 7 : 12; g.Box(x, y, -4, x, y + 1, -4, Pal.Solid(tins[i])); }
            g.Tube(new Vector3(-2, 6, 2), new Vector3(2, 3, 5), 0.5f, Pal.Ramp(Pal.Black, 1, 1996));              // hose
            g.Box(2, 3, 5, 3, 5, 6, Pal.Ramp(Pal.Chrome, 2, 1997));                                                  // spray gun
            g.Set(3, 6, 6, Pal.Solid(Pal.Crimson[4]));                                                               // a drip of red
            return g;
        }

        public static VoxelGrid TuningBench()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-9, 10, -4, 9, 11, 4, Pal.Ramp(Pal.Metal, 2, 1401));                                          // top
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Metal, 1));
            g.Box(-8, 3, -3, 8, 3, 3, Pal.Ramp(Pal.Metal, 0));                                                    // shelf
            g.Box(5, 12, -1, 7, 14, 1, Pal.Ramp(Pal.Crimson, 3, 1402));                                           // vice
            g.Box(-9, 12, -4, 9, 24, -4, p => (p.x + p.y) % 3 == 0 ? Pal.Chrome[1] : Pal.Pick(Pal.Olive, p, 1403, 1));   // tool board
            g.Box(-7, 12, -1, -3, 17, 2, Pal.Ramp(Pal.Black, 1));                                                 // dyno monitor
            g.Mat(Glass); g.Box(-6, 13, 3, -4, 16, 3, p => (p.y == 14 && p.x != -6) || (p.y == 15 && p.x == -6) ? Pal.Hex("58c858") : Pal.Glass[1]);   // green trace
            return g;
        }

        /// <summary>Wire fish trap (a funnel-mouthed cylinder on the lake bed) with a rope up to a red float.</summary>
        public static VoxelGrid FishTrap()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.CylZ(0, 4, 4f, -6, 6, p => ((p.x + p.y + p.z) & 1) == 0 ? Pal.Metal[2] : Pal.Rust[2], 3f);                    // mesh drum
            g.CylZ(0, 4, 4f, -6, -6, p => ((p.x + p.y) & 1) == 0 ? Pal.Metal[1] : Pal.Rust[1]);                                   // closed end
            g.CylZ(0, 4, 4f, 6, 6, Pal.Ramp(Pal.Metal, 1), 1.6f);                                                                  // funnel mouth
            g.Box(-4, 0, -5, 4, 0, 5, Pal.Ramp(Pal.Metal, 0, 1095));                                                               // skid
            g.Mat((byte)ResourceType.Cloth); g.Box(0, 9, 0, 0, 14, 0, Pal.Ramp(Pal.Sand, 3));                                       // rope
            g.Mat((byte)ResourceType.Rubber); g.Box(-1, 15, -1, 1, 16, 1, Pal.Ramp(Pal.Crimson, 3));                                 // float
            return g;
        }

        /// <summary>Drip line: a hose along a bed with an emitter every 24 cm and an inlet riser with its valve.</summary>
        public static VoxelGrid DripLine()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Rubber);
            g.Box(0, 0, -10, 0, 0, 10, p => p.z % 3 == 0 ? Pal.Moss[2] : Pal.Tire[1]);
            g.Mat(Iron); g.Box(0, 0, -11, 0, 3, -11, Pal.Ramp(Pal.Metal, 2)); g.Set(0, 4, -11, Pal.Solid(Pal.Crimson[3]));
            return g;
        }

        public static VoxelGrid Composter()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int y = 0; y <= 10; y += 2) { g.Box(-6, y, -6, 6, y, -6, Planks(985 + y)); g.Box(-6, y, 6, 6, y, 6, Planks(986 + y)); g.Box(-6, y, -6, -6, y, 6, Planks(987 + y)); g.Box(6, y, -6, 6, y, 6, Planks(988 + y)); }
            g.Mat((byte)ResourceType.Clay); g.Box(-5, 0, -5, 5, 7, 5, Pal.Ramp(Soil, 2));
            return g;
        }

        public static VoxelGrid Furnace(bool arc)
        {
            var g = new VoxelGrid().Mat(Stone);
            if (arc)
            {
                g.Mat(Iron);
                g.CylY(0, 0, 7f, 0, 14, Pal.Weathered(Pal.Metal, 0.3f, 990, 2, 0));
                g.Mat(Copper); foreach (int x in new[] { -3, 0, 3 }) g.Box(x, 15, 0, x, 24, 0, Pal.Ramp(Pal.Bronze, 2));
                g.Mat(Scrap); g.Box(-2, 4, 7, 2, 6, 7, Pal.Solid(Pal.Amber));
                return g;
            }
            for (int y = 0; y <= 20; y++)
            {
                float r = Mathf.Lerp(7f, 3f, y / 20f);
                g.CylY(0, 0, r, y, y, Bricks(991), r - 1.5f);
            }
            g.Box(-3, 2, 6, 3, 6, 6, Pal.Solid(Pal.Amber)); g.Box(-2, 3, 6, 2, 5, 6, Pal.Solid(Pal.LightY));
            return g;
        }

        public static VoxelGrid Kiln()
        {
            var g = new VoxelGrid().Mat(Stone);
            for (int x = -8; x <= 8; x++) for (int z = -6; z <= 6; z++) for (int y = 0; y <= 12; y++)
            {
                float d = new Vector2(x / 8f, (y - 0) / 12f).magnitude;
                if (d <= 1f && (d > 0.8f || y == 0 || Mathf.Abs(z) == 6)) g.Set(x, y, z, Bricks(995));
            }
            g.ClearBox(-2, 1, 6, 2, 5, 6); g.Box(-2, 1, 5, 2, 3, 5, Pal.Solid(Pal.Amber));
            g.CylY(5, 0, 1.2f, 12, 20, Pal.Ramp(Pal.Rust, 1));
            return g;
        }

        public static VoxelGrid WashPlant()
        {
            var g = new VoxelGrid().Mat(Iron);
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -5, 5 }) g.Box(x, 0, z, x, 12, z, Pal.Ramp(Pal.Metal, 1));
            for (int x = -10; x <= 10; x++) { int y = 12 - (x + 10) / 4; g.Box(x, y, -5, x, y, 5, p => (p.x + p.z) % 2 == 0 ? Pal.Metal[2] : Pal.Void); }   // sieve deck
            g.Mat(Scrap); g.Box(-12, 13, -6, -8, 18, 6, Pal.Weathered(Pal.Olive, 0.4f, 1000, 2, 0));           // hopper
            g.Box(6, 0, -4, 12, 4, 4, Pal.Ramp(Pal.Rust, 2)); g.Box(7, 4, -3, 11, 4, 3, Pal.Solid(Water));     // tub
            return g;
        }

        public static VoxelGrid Mixer()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-6, 0, -3, 6, 2, 3, Pal.Ramp(Pal.Metal, 0));
            for (int y = 3; y <= 14; y++) { float r = 5f - Mathf.Abs(y - 8) * 0.5f; g.CylZ(0, y, r, -1, 1, p => (p.x + p.y) % 4 == 0 ? Pal.Rust[1] : Hazard); }
            return g;
        }

        public static VoxelGrid Still()
        {
            var g = new VoxelGrid().Mat(Copper);
            g.CylY(-4, 0, 4f, 0, 10, Pal.Ramp(Pal.Bronze, 2, 1010));
            g.CylY(-4, 0, 2.5f, 11, 13, Pal.Ramp(Pal.Bronze, 3, 1011));
            g.Tube(new Vector3(-4, 13, 0), new Vector3(5, 10, 0), 0.6f, Pal.Ramp(Pal.Bronze, 2));
            for (int y = 2; y <= 10; y += 2) g.CylY(5, 0, 2.5f, y, y, Pal.Ramp(Pal.Bronze, 1), 1.5f);   // coil
            g.Mat(Glass); g.Box(4, 0, -1, 6, 3, 1, Pal.Ramp(Pal.Glass, 3));
            return g;
        }

        public static VoxelGrid Garage()
        {
            var g = new VoxelGrid().Mat(Concrete);
            g.Box(-25, 0, -38, 25, 0, 38, p => (p.x % 12 == 0 || p.z % 12 == 0) ? Conc[0] : Pal.Pick(Conc, p, 1020, 2));
            g.Mat(Iron);
            foreach (int x in new[] { -25, 25 }) foreach (int z in new[] { -38, 0, 38 }) g.Box(x, 1, z, x, 40, z, Pal.Ramp(Pal.Metal, 1));
            g.Box(-25, 40, -38, 25, 40, 38, p => (p.x % 3 == 0) ? Pal.Rust[1] : Pal.Rust[2]);
            g.Box(-24, 1, 30, -18, 12, 37, Pal.Weathered(Pal.Olive, 0.3f, 1021, 2, 0));                    // tool cabinet
            g.Box(-3, 1, -20, 3, 1, 20, p => p.z % 4 == 0 ? Hazard : Pal.Black[1]);                        // lift strip
            return g;
        }
    }
}
