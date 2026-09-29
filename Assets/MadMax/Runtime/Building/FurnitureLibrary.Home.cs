using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Home furniture (roadmap 2): seats, storage with a purpose (outfits, books, weapons, trophies), clock,
    /// mirror, bath, latrine, kitchen counter and dining table. Wall pieces lie in the XZ plane (+Z up the wall,
    /// +Y out of it).</summary>
    public static partial class FurnitureLibrary
    {
        const byte Iron = (byte)ResourceType.Iron;

        static IEnumerable<FurnitureDef> Home()
        {
            var Fu = BuildCategory.Furniture; var U = BuildCategory.Utility; var De = BuildCategory.Decor;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var St = ResourceType.Stone; var C = ResourceType.Cloth;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper;
            yield return D("armchair", "ARMCHAIR", Fu, Armchair(), 3, false, go => Sit(go, 2f, 1.3f, new Vector3(0f, 0.6f, -0.08f)), (W, 3), (C, 4));
            yield return D("bench", "BENCH", Fu, Bench(), 3, false, go => Sit(go, 1.3f, 1.05f, new Vector3(-0.44f, 0.52f, 0f), new Vector3(0.44f, 0.52f, 0f)), (W, 4));
            yield return D("dining_table", "DINING TABLE", Fu, DiningTableGrid(), 4, false, go =>
            {
                go.AddComponent<DiningTable>().hours = 5f;
                Glow(go, new Vector3(0f, 1.22f, 0f), new Color(1f, 0.75f, 0.45f), 3f, 0.8f, false, 0f);
            }, (W, 8), (C, 2));
            yield return D("wardrobe", "WARDROBE", Fu, WardrobeGrid(), 5, false, go => { Box(go, "WARDROBE", 60f, false); go.AddComponent<Wardrobe>(); }, (W, 8), (S, 1));
            yield return D("bookshelf", "BOOKSHELF", Fu, BookshelfGrid(), 4, false, go => { Box(go, "BOOKSHELF", 30f, false); go.AddComponent<Bookshelf>(); }, (W, 6), (C, 1));
            yield return D("weapon_rack", "WEAPON RACK", Fu, RackGrid(), 4, false, go => { Box(go, "WEAPON RACK", 25f, false); go.AddComponent<WeaponRack>(); }, (W, 3), (S, 2));
            yield return D("mirror", "MIRROR", Fu, MirrorGrid(), 1, false, go => go.AddComponent<Mirror>(), (G, 2), (W, 1));
            yield return D("bathtub", "BATHTUB", Fu, Bathtub(), 8, false, go => { Node(go, UtilityKind.Water, 0.6f).waterCapacity = 5f; go.AddComponent<WaterOutlet>().kind = WaterOutlet.Kind.Bath; }, (S, 8), (Fe, 2));
            yield return D("kitchen_counter", "KITCHEN COUNTER", Fu, Counter(), 6, false, go =>
            {
                Node(go, UtilityKind.Water, 1.0f).waterCapacity = 5f;
                go.AddComponent<WaterOutlet>().kind = WaterOutlet.Kind.Sink;
                Station(go, "counter", "PREPARE FOOD (COUNTER)", 0f);
            }, (W, 5), (St, 3), (S, 2), (Fe, 1));
            yield return D("latrine", "LATRINE", U, Outhouse(), 8, false, go => go.AddComponent<Latrine>(), (W, 10), (S, 2));
            yield return D("sewing_table", "SEWING TABLE", Fu, SewingTableGrid(), 4, false, go => Station(go, "sewing", "SEW (SEWING TABLE)", 0f), (W, 4), (Fe, 2), (S, 1));
            yield return D("wall_clock", "WALL CLOCK", De, ClockGrid(), 1, false, go => go.AddComponent<WallClock>(), (S, 2), (G, 1), (Cu, 1));
            yield return D("trophy_mount", "TROPHY MOUNT", De, TrophyGrid(), 2, false, go => go.AddComponent<TrophyMount>(), (W, 2));
        }

        static Seat Sit(GameObject go, float rest, float reading, params Vector3[] spots)
        {
            var s = go.AddComponent<Seat>();
            s.rest = rest; s.reading = reading; s.spots = spots;
            return s;
        }

        static VoxelGrid Armchair()
        {
            var g = new VoxelGrid().Mat(Cloth);
            var leather = Pal.Weathered(Pal.Bronze, 0.12f, 1101, 2, 0);
            g.Box(-6, 1, -5, 6, 5, 5, leather);
            g.Box(-6, 6, -5, 6, 15, -3, leather);
            g.Box(-6, 6, -5, -5, 9, 5, leather); g.Box(5, 6, -5, 6, 9, 5, leather);
            g.Box(-4, 6, -2, 4, 6, 4, Pal.Ramp(Pal.Bronze, 3, 1102));
            for (int x = -4; x <= 4; x += 4) for (int y = 9; y <= 13; y += 4) g.Set(x, y, -2, Pal.Solid(Pal.Bronze[0]));   // tufting buttons
            g.Mat(Wood);
            foreach (int x in new[] { -6, 6 }) foreach (int z in new[] { -5, 5 }) g.Set(x, 0, z, Pal.Ramp(Pal.Wood, 0, 1103));
            return g;
        }

        static VoxelGrid Bench()
        {
            var g = new VoxelGrid().Mat(Wood);
            var legs = Pal.Ramp(Pal.Wood, 1, 1105);
            foreach (int x in new[] { -10, 10 }) { g.Box(x, 0, -2, x, 4, -2, legs); g.Box(x, 0, 2, x, 4, 2, legs); g.Box(x, 1, -1, x, 1, 1, legs); }
            g.Box(-12, 5, -2, 12, 5, 2, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 1106), Pal.Ramp(Pal.Wood, 1, 1107), 2, 2));
            return g;
        }

        static VoxelGrid DiningTableGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -13, 13 }) foreach (int z in new[] { -6, 6 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Wood, 1, 1201));
            g.Box(-13, 8, -6, 13, 8, -6, Pal.Ramp(Pal.Wood, 0, 1202)); g.Box(-13, 8, 6, 13, 8, 6, Pal.Ramp(Pal.Wood, 0, 1202));
            g.Box(-15, 10, -7, 15, 10, 7, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 1203), Pal.Ramp(Pal.Wood, 3, 1204), 2, 3));
            g.Mat(Cloth); g.Box(-12, 11, -2, 12, 11, 2, p => p.x % 4 == 0 ? Pal.Cream[3] : Pal.Crimson[2]);
            g.Mat(Scrap); g.Set(0, 12, 0, Pal.Solid(Pal.Bronze[2]));
            g.Mat(Wood); g.Box(0, 13, 0, 0, 14, 0, Pal.Solid(Pal.Cream[4])); g.Set(0, 15, 0, Pal.Solid(Pal.Amber));
            return g;
        }

        static VoxelGrid WardrobeGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-7, 1, -4, 7, 25, 4, Pal.Ramp(Pal.Wood, 1, 1111));
            g.Box(-8, 26, -5, 8, 26, 5, Pal.Ramp(Pal.Wood, 2, 1112));
            g.Box(-7, 0, -4, 7, 0, 4, Pal.Ramp(Pal.Wood, 0, 1113));
            g.Repaint(0, 1, 4, 0, 25, 4, Pal.Solid(Pal.Wood[0]));                        // door seam
            foreach (int x0 in new[] { -6, 2 })
            {
                g.Box(x0, 3, 5, x0 + 3, 11, 5, Pal.Ramp(Pal.Wood, 2, 1114 + x0));          // raised panels
                g.Box(x0, 14, 5, x0 + 3, 23, 5, Pal.Ramp(Pal.Wood, 2, 1116 + x0));
            }
            g.Mat(Iron); g.Set(-1, 13, 5, Pal.Solid(Pal.Chrome[2])); g.Set(1, 13, 5, Pal.Solid(Pal.Chrome[2]));
            return g;
        }

        static VoxelGrid BookshelfGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var frame = Pal.Ramp(Pal.Wood, 1, 1121);
            g.Box(-9, 0, -3, -9, 24, 3, frame); g.Box(9, 0, -3, 9, 24, 3, frame);
            g.Box(-8, 0, -3, 8, 24, -3, Pal.Ramp(Pal.Wood, 0, 1122));
            foreach (int y in new[] { 0, 8, 16, 24 }) g.Box(-8, y, -2, 8, y, 3, frame);
            g.Mat(Cloth);
            var spines = new[] { Pal.Rust, Pal.Olive, Pal.PaleBlue, Pal.Cream, Pal.RigGreen, Pal.Bronze, Pal.Crimson, Pal.Navy };
            foreach (int shelf in new[] { 1, 9, 17 })
            {
                var r = new System.Random(1123 + shelf);
                for (int x = -8; x <= 8;)
                {
                    int w = r.Next(1, 3), h = r.Next(4, 7);
                    if (r.NextDouble() < 0.12) { x += 2; continue; }                          // a gap on the shelf
                    g.Box(x, shelf, -2, Mathf.Min(8, x + w - 1), shelf + h - 1, 2, Pal.Ramp(spines[r.Next(spines.Length)], 1 + r.Next(2), 1124 + x));
                    x += w;
                }
            }
            return g;
        }

        static VoxelGrid RackGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-10, 0, -6, 10, 0, 6, Pal.Stripe(Pal.Ramp(Pal.Wood, 1, 1131), Pal.Ramp(Pal.Wood, 2, 1132), 2, 3));
            g.Mat(Iron);
            foreach (int z in new[] { 4, 0, -4 }) foreach (int x in new[] { -7, 7 })
            {
                g.Set(x, 1, z, Pal.Solid(Pal.Chrome[1])); g.Set(x, 2, z, Pal.Solid(Pal.Chrome[2])); g.Set(x, 3, z, Pal.Solid(Pal.Chrome[2])); g.Set(x, 3, z + 1, Pal.Solid(Pal.Chrome[3]));
            }
            return g;
        }

        static VoxelGrid TrophyGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -5; x <= 5; x++)
            for (int z = -7; z <= 6; z++)
            {
                // shield: straight top, curving to a point at the bottom
                float half = z >= 0 ? 5.4f : 5.4f * Mathf.Sqrt(Mathf.Max(0f, 1f - (z / 7.6f) * (z / 7.6f)));
                if (Mathf.Abs(x) > half) continue;
                bool rim = Mathf.Abs(x) > half - 1.2f || z == 6;
                g.Set(x, 0, z, Pal.Ramp(Pal.Wood, rim ? 1 : 3, 1141));
                if (rim) g.Set(x, 1, z, Pal.Ramp(Pal.Wood, 0, 1142));
            }
            return g;
        }

        static VoxelGrid ClockGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            for (int x = -6; x <= 6; x++)
            for (int z = -6; z <= 6; z++)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                if (r > 6.4f) continue;
                if (r > 5.2f) { g.Set(x, 0, z, Pal.Ramp(Pal.Bronze, 2, 1151)); g.Set(x, 1, z, Pal.Ramp(Pal.Bronze, 3, 1152)); }
                else g.Set(x, 0, z, Pal.Solid(Pal.Cream[3]));
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                g.Set(Mathf.RoundToInt(Mathf.Sin(a) * 4.4f), 1, Mathf.RoundToInt(Mathf.Cos(a) * 4.4f), Pal.Solid(i % 3 == 0 ? Pal.Black[0] : Pal.Black[2]));
            }
            g.Set(0, 1, 0, Pal.Solid(Pal.Black[0]));
            return g;
        }

        static VoxelGrid MirrorGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-5, 0, -8, 5, 0, 8, Pal.Ramp(Pal.Wood, 1, 1161));
            g.Box(-5, 1, -8, 5, 1, 8, Pal.Ramp(Pal.Bronze, 2, 1162));
            g.Mat(Glass); g.Box(-4, 1, -7, 4, 1, 7, p => (p.x + p.z + 40) % 6 == 0 ? Pal.Chrome[3] : Pal.Chrome[2]);
            return g;
        }

        static VoxelGrid Bathtub()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-10, 2, -5, 10, 8, 5, Pal.Weathered(Pal.Cream, 0.12f, 1171, 2, 0));
            g.Repaint(-10, 8, -5, 10, 8, 5, Pal.Ramp(Pal.Cream, 3, 1172));
            g.ClearBox(-9, 4, -4, 9, 8, 4);
            g.Box(-9, 4, -4, 9, 5, 4, Pal.Ramp(Pal.PaleBlue, 0, 1173));                   // bath water
            g.Mat(Iron);
            foreach (int x in new[] { -9, 9 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 1, z, Pal.Ramp(Pal.Bronze, 2, 1174));
            g.Box(-10, 9, 0, -10, 11, 0, Pal.Ramp(Pal.Chrome, 2)); g.Box(-9, 11, 0, -8, 11, 0, Pal.Ramp(Pal.Chrome, 2));
            return g;
        }

        static VoxelGrid Counter()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-12, 1, -4, 12, 10, 4, Pal.Ramp(Pal.Wood, 1, 1191));
            g.Mat(Scrap); g.Box(-12, 0, -4, 12, 0, 3, Pal.Ramp(Pal.Black, 1));                 // kick plate
            g.Mat(Wood);
            foreach (int x0 in new[] { -11, -5, 1, 7 }) g.Box(x0, 2, 5, x0 + 4, 9, 5, Pal.Ramp(Pal.Wood, 2, 1192 + x0));
            g.Mat(Stone); g.Box(-13, 11, -5, 13, 11, 5, Pal.Ramp(Pal.Cream, 1, 1196));
            g.ClearBox(3, 11, -3, 9, 11, 2);                                                  // sink basin
            g.Mat(Scrap); g.Repaint(3, 10, -3, 9, 10, 2, Pal.Ramp(Pal.Chrome, 2));
            g.Mat(Iron);
            foreach (int x0 in new[] { -11, -5, 1, 7 }) g.Set(x0 + 2, 8, 6, Pal.Solid(Pal.Chrome[2]));
            g.Box(6, 12, -4, 6, 14, -4, Pal.Ramp(Pal.Chrome, 2)); g.Box(6, 14, -3, 6, 14, -2, Pal.Ramp(Pal.Chrome, 2));
            return g;
        }

        static VoxelGrid SewingTableGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 8, z, Pal.Ramp(Pal.Wood, 1, 1211));
            g.Box(-9, 9, -5, 9, 9, 5, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 1212), Pal.Ramp(Pal.Wood, 1, 1213), 0, 4));
            g.Mat(Iron);
            g.Box(-6, 1, -3, 6, 1, 3, Pal.Stripe(Pal.Ramp(Pal.Metal, 1, 1214), Pal.Ramp(Pal.Metal, 0, 1215), 0, 2));   // treadle
            // the machine: bed, pillar, arm, needle, hand wheel
            g.Box(-5, 10, -2, 3, 11, 2, Pal.Ramp(Pal.Black, 2, 1216));
            g.Box(2, 12, -1, 3, 15, 1, Pal.Ramp(Pal.Black, 2, 1217));
            g.Box(-5, 15, -1, 3, 16, 1, Pal.Ramp(Pal.Black, 2, 1218));
            g.Set(-1, 16, 1, Pal.Solid(Pal.Ochre[3])); g.Set(0, 16, 1, Pal.Solid(Pal.Ochre[3]));             // gold decal
            g.Box(-4, 12, 0, -4, 14, 0, Pal.Ramp(Pal.Chrome, 2));
            g.CylX(14, 0, 1.6f, 4, 4, Pal.Ramp(Pal.Chrome, 1));
            g.Mat(Cloth); g.Box(-8, 10, 3, -6, 11, 4, Pal.Ramp(Pal.Crimson, 2, 1219));                         // a bolt of fabric
            return g;
        }

        static VoxelGrid Outhouse()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-7, 0, -7, 7, 26, 7, Pal.Stripe(Pal.Weathered(Pal.Wood, 0.08f, 1181, 2, 0), Pal.Ramp(Pal.Wood, 1, 1182), 0, 3));
            g.Repaint(-4, 1, 7, 4, 22, 7, Pal.Stripe(Pal.Ramp(Pal.Wood, 1, 1184), Pal.Ramp(Pal.Wood, 0, 1185), 0, 2));   // door
            for (int x = -3; x <= 3; x++)
            for (int y = 16; y <= 22; y++)
            {
                // the crescent moon: a disc minus an offset disc
                float a = x * x + (y - 19) * (y - 19), b = (x - 1.2f) * (x - 1.2f) + (y - 19.6f) * (y - 19.6f);
                if (a <= 6.2f && b > 4.2f) g.Set(x, y, 7, Pal.Solid(Pal.Black[0]));
            }
            g.Mat(Scrap);
            for (int z = -8; z <= 8; z++) g.Box(-8, 27, z, 8, 27 + (8 - z) / 5, z, Pal.Stripe(Pal.Weathered(Pal.Metal, 0.4f, 1183, 2, 0), Pal.Ramp(Pal.Metal, 0, 1186), 0, 3));
            g.Box(5, 28, -5, 5, 33, -5, Pal.Ramp(Pal.Metal, 1, 1187));                      // vent pipe
            g.Mat(Iron); g.Set(3, 12, 8, Pal.Ramp(Pal.Chrome, 1));
            return g;
        }
    }
}
