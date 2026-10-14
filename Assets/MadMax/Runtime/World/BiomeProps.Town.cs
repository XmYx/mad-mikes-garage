using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Town pieces for the town plans (<see cref="TownPlan"/>): street furniture (benches, bins, phone booths,
    /// bus stops, dead traffic lights, stop signs, hydrants, bollards, planters), park pieces (leafy trees, hedges,
    /// fountain, statue, pond, the village well), fences (picket, chain-link), warehouses, a fuel-station canopy, rubble,
    /// graffiti walls, burning and oil drums; batched decor (trash bags, litter, newspapers) without colliders. All
    /// destructible voxels with real materials; every piece faces its street with -Z. Placements come from the plan.</summary>
    public static partial class BiomeProps
    {
        static void AddTownTemplates(System.Action<Template> add)
        {
            add(T("Bench0", Bench(), 0.08f));
            add(T("Bin0", Bin(), 0.08f));
            add(T("TrashBags0", TrashBags(), 0.08f));
            for (int i = 0; i < 2; i++) add(T("Litter" + i, Litter(i), 0.08f));
            add(T("PhoneBooth0", PhoneBooth(), 0.08f));
            add(T("BusStop0", BusStop(), 0.08f));
            add(T("TrafficLight0", TrafficLight(), 0.08f));
            add(T("StopSign0", StopSign(), 0.08f));
            add(T("Hydrant0", Hydrant(), 0.08f));
            add(T("Bollard0", Bollard(), 0.08f));
            add(T("Planter0", Planter(), 0.08f));
            add(T("Hedge0", Hedge(), 0.1f));
            for (int i = 0; i < 2; i++) add(T("ParkTree" + i, ParkTree(i), 0.16f));
            add(T("Fountain0", Fountain(), 0.12f));
            add(T("Statue0", Statue(), 0.1f));
            add(T("Pond0", Pond(), 0.12f));
            add(T("Well0", Well(), 0.1f));
            for (int i = 0; i < 2; i++) add(T("Graffiti" + i, GraffitiWall(i), 0.1f));
            add(T("BurnBarrel0", Drum(true, 0), 0.08f));
            add(T("OilDrum0", Drum(false, 1), 0.08f));
            for (int i = 0; i < 2; i++) add(T("Warehouse" + i, Warehouse(i), 0.2f));
            add(T("Canopy0", Canopy(), 0.16f));
            add(T("FencePicket0", PicketFence(), 0.08f));
            add(T("ChainLink0", ChainLink(), 0.08f));
            add(T("Rubble0", Rubble(), 0.12f));
        }

        /// <summary>Town pieces that keep ground cover out from under them (buildings get overgrowth as well).</summary>
        static bool IsTownPiece(string id) => id.StartsWith("Warehouse") || id.StartsWith("Canopy") || id.StartsWith("BusStop") || id.StartsWith("PhoneBooth")
                                              || id.StartsWith("Fountain") || id.StartsWith("Statue") || id.StartsWith("Pond") || id.StartsWith("Well")
                                              || id.StartsWith("Graffiti") || id.StartsWith("Planter") || id.StartsWith("Rubble") || id.StartsWith("Bench");

        static readonly Color32[] Paint = { Pal.Hex("2a4a3a"), Pal.Hex("34584a"), Pal.Hex("406a58"), Pal.Hex("4e7c66") };     // municipal green
        static readonly Color32[] Water = { Pal.Hex("1a2a2c"), Pal.Hex("223836"), Pal.Hex("2c4a44"), Pal.Hex("3a5c52") };     // murky pond water
        static readonly Color32[] Flowers = { Pal.Crimson[4], Pal.Ochre[4], Pal.Pink[3], Pal.Cream[4], Pal.PaleBlue[2] };
        static readonly Color32[] Spray = { Pal.Crimson[4], Pal.Ochre[4], Pal.PaleBlue[1], Pal.Pink[2], Pal.Moss[4], Pal.Cream[4] };

        // ------------------------------------------------------------------ street furniture

        static VoxelGrid Bench()
        {
            var g = new VoxelGrid().Mat(Scrap);
            foreach (int x in new[] { -8, 8 })
            {
                g.Box(x, 0, -3, x, 5, -3, Pal.Ramp(Pal.Metal, 1, 2201));
                g.Box(x, 0, 2, x, 12, 2, Pal.Ramp(Pal.Metal, 1, 2202));
                g.Box(x, 5, -3, x, 5, 2, Pal.Ramp(Pal.Metal, 1, 2203));
            }
            g.Mat(Wood);
            for (int z = -3; z <= 1; z += 2) g.Box(-10, 6, z, 10, 6, z, Pal.Weathered(Paint, 0.45f, 2204 + z, 2, 0));    // seat slats
            foreach (int y in new[] { 7, 9, 11 })
                g.Box(-10, y, 2, 10, y, 2, p => Pal.Hash(p, 2207) < 0.08f ? Pal.Wood[1] : Pal.Weathered(Paint, 0.45f, 2205 + y, 2, 0)(p));
            g.Remove(p => p.y == 9 && p.x > 3 && p.z == 2);                                         // a broken slat
            return g;
        }

        static VoxelGrid Bin()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 3.2f, 0, 11, p => p.y == 11 || p.y == 0 ? Pal.Ramp(Pal.Metal, 2)(p) : Pal.Weathered(Paint, 0.4f, 2210, 1, 0)(p), 2.2f);
            g.Mat(Cloth);
            var rnd = new System.Random(2211);
            for (int i = 0; i < 30; i++)
            {
                int x = rnd.Next(-3, 4), z = rnd.Next(-3, 4), y = 10 + rnd.Next(4);
                if (x * x + z * z > 10) continue;
                g.Set(x, y, z, Pal.Solid(i % 5 == 0 ? Pal.Ochre[3] : i % 3 == 0 ? Pal.Cream[2] : Pal.Black[2]));
            }
            for (int y = 0; y <= 9; y++) g.Set(0, y, -3, Pal.Solid(Pal.Black[0]));                 // fill the shell (support)
            g.Box(-1, 0, -1, 1, 9, 1, Pal.Solid(Pal.Black[1]));
            return g;
        }

        static VoxelGrid TrashBags()
        {
            var g = new VoxelGrid().Mat(Cloth);
            var bags = new[] { new Vector3(-3, 3, 0), new Vector3(3, 3, 1), new Vector3(0, 3, 4), new Vector3(1, 7, 1) };
            for (int b = 0; b < bags.Length; b++)
            {
                var c = bags[b]; float r = b == 3 ? 2.6f : 3.4f;
                for (int x = -4; x <= 4; x++) for (int y = -4; y <= 4; y++) for (int z = -4; z <= 4; z++)
                {
                    var p = new Vector3Int(Mathf.RoundToInt(c.x) + x, Mathf.RoundToInt(c.y) + y, Mathf.RoundToInt(c.z) + z);
                    if (p.y < 0 || new Vector3(x, y * 1.2f, z).magnitude > r + Pal.Noise((Vector3)p * 0.6f, b) * 0.8f) continue;
                    g.Set(p, Pal.Solid(Pal.Black[Pal.Hash(p, 2220 + b) > 0.8f ? 3 : y > 1 ? 2 : 1]));
                }
                g.Set(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y + r * 0.85f) + 1, Mathf.RoundToInt(c.z), Pal.Solid(Pal.Black[3]));   // the tie
            }
            g.Mat(Wood); g.Box(-7, 0, -4, -4, 3, -1, Pal.Ramp(Pal.Ochre, 2, 2224));                 // a soggy box
            return g;
        }

        static VoxelGrid Litter(int v)
        {
            var g = new VoxelGrid().Mat(Cloth);
            var rnd = new System.Random(2230 + v);
            int n = v == 0 ? 7 : 4;
            for (int i = 0; i < n; i++)
            {
                int x = rnd.Next(-7, 6), z = rnd.Next(-7, 6), w = 1 + rnd.Next(2), l = 2 + rnd.Next(2);
                var c = v == 1 ? Pal.Cream[1 + rnd.Next(2)] : i % 3 == 0 ? Pal.Ochre[3] : Pal.Cream[2 + rnd.Next(2)];
                g.Box(x, 0, z, x + w, 0, z + l, p => v == 1 && (p.z & 1) == 0 && Pal.Hash(p, 2231) > 0.4f ? Pal.Black[2] : c);   // printed columns
                if (v == 1) g.Box(x, 1, z, x + w, 1, z + l - 1, Pal.Solid(Pal.Cream[1]));
            }
            g.Mat(Scrap);
            for (int i = 0; i < (v == 0 ? 3 : 1); i++) g.Set(rnd.Next(-6, 6), 0, rnd.Next(-6, 6), Pal.Solid(i == 0 ? Pal.Crimson[3] : Pal.Chrome[2]));   // cans
            if (v == 0) { g.Mat(Glass); g.Box(4, 0, -5, 4, 0, -3, Pal.Ramp(Pal.Moss, 3)); }              // a bottle
            return g;
        }

        static VoxelGrid PhoneBooth()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var frame = Pal.Weathered(Pal.Crimson, 0.35f, 2240, 3, 0);
            g.Box(-5, 0, -5, 5, 0, 5, Pal.Ramp(Pal.Metal, 1));
            foreach (int x in new[] { -5, 5 }) foreach (int z in new[] { -5, 5 }) g.Box(x, 1, z, x, 27, z, frame);
            g.Box(-5, 25, -5, 5, 28, 5, p => p.y == 26 && Mathf.Abs(p.x) < 4 && (p.z == -5 || p.z == 5) ? Pal.Cream[3] : frame(p));   // roof + sign band
            for (int y = 2; y <= 24; y++)
                for (int k = -4; k <= 4; k++)
                {
                    bool bar = y % 6 == 0;
                    void Pane(int x, int z) { if (bar) { g.Mat(Scrap); g.Set(x, y, z, frame); } else if (Pal.Hash(x, y, z, 2241) > 0.2f) { g.Mat(Glass); g.Set(x, y, z, Pal.Ramp(Pal.Glass, 2, 2242)); } }
                    Pane(k, -5); Pane(-5, k); Pane(5, k);
                    if (y > 11 || bar) { g.Mat(Scrap); g.Set(k, y, 5, frame); }
                }
            g.Mat(Scrap); g.Box(-2, 12, 4, 2, 17, 4, Pal.Ramp(Pal.Black, 2));                       // the phone
            g.Box(1, 13, 3, 1, 15, 3, Pal.Ramp(Pal.Black, 1));
            return g;
        }

        static VoxelGrid BusStop()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Metal, 0.5f, 2250, 2, 0);
            foreach (int x in new[] { -19, 19 }) foreach (int z in new[] { -6, 7 }) g.Box(x, 0, z, x, 28, z, steel);
            g.Box(-21, 29, -8, 21, 30, 8, p => p.y == 30 && Pal.Hash(p, 2251) < 0.1f ? Pal.Rust[2] : Pal.Weathered(Pal.Chrome, 0.5f, 2252, 1, 0)(p));   // roof
            for (int x = -18; x <= 18; x++)
                for (int y = 3; y <= 26; y++)
                {
                    if (Pal.Hash(x / 6, y / 8, 0, 2253) < 0.25f) continue;                             // panes smashed out
                    g.Mat(Glass); g.Set(x, y, 7, Pal.Ramp(Pal.Glass, 2, 2254));
                }
            g.Mat(Cloth);
            g.Box(-19, 4, -5, -19, 24, 5, p => p.z == -5 || p.z == 5 || p.y == 4 || p.y == 24 ? Pal.Metal[1] : Pal.Pick(Pal.Ochre, p, 2255, 2));   // advert panel
            g.Box(-19, 12, -2, -19, 18, 2, Pal.Ramp(Pal.Crimson, 3, 2256));
            g.Mat(Wood); g.Box(-14, 6, 3, 14, 6, 6, Pal.Weathered(Paint, 0.4f, 2257, 2, 0));          // bench
            g.Mat(Scrap); foreach (int x in new[] { -12, 12 }) g.Box(x, 0, 5, x, 5, 5, steel);
            g.Box(23, 0, -6, 23, 36, -6, steel);                                                     // the sign
            g.CylZ(23, 34, 3.2f, -7, -7, p => Mathf.Abs(p.y - 34) <= 1 ? Pal.Cream[3] : Pal.Navy[3]);
            return g;
        }

        static VoxelGrid TrafficLight()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Metal, 0.5f, 2260, 2, 0);
            g.Box(0, 0, 0, 1, 46, 1, steel);
            g.Box(-1, 0, -1, 2, 1, 2, Pal.Ramp(Pal.Metal, 0));
            g.Box(0, 46, -12, 1, 47, 1, steel);                                                      // arm over the road
            foreach (int z in new[] { -11, 0 })
            {
                int y0 = z == 0 ? 34 : 37;
                g.Box(-1, y0, z - 1, 2, y0 + 9, z + 1, Pal.Ramp(Pal.Black, 1, 2261));
                g.Mat(Glass);
                g.Set(0, y0 + 7, z - 2, Pal.Solid(Pal.Crimson[1])); g.Set(0, y0 + 4, z - 2, Pal.Solid(Pal.Ochre[1])); g.Set(0, y0 + 1, z - 2, Pal.Solid(Pal.Moss[1]));
                g.Mat(Scrap);
            }
            g.Box(2, 22, 0, 4, 26, 1, Pal.Ramp(Pal.Black, 2));                                       // the walk button box
            g.Set(4, 24, 1, Pal.Solid(Pal.Cream[3]));
            return g;
        }

        static VoxelGrid StopSign()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(0, 0, 0, 0, 32, 0, Pal.Weathered(Pal.Metal, 0.5f, 2270, 2, 0));
            for (int x = -5; x <= 5; x++)
                for (int y = -5; y <= 5; y++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) > 8) continue;                                      // octagon
                    bool rim = Mathf.Abs(x) == 5 || Mathf.Abs(y) == 5 || Mathf.Abs(x) + Mathf.Abs(y) == 8;
                    bool text = y == 0 && Mathf.Abs(x) <= 3 && (x & 1) == 0;
                    bool hole = Pal.Hash(x, y, 0, 2271) > 0.93f;                                         // buckshot
                    g.Set(x, 33 + y, -1, Pal.Solid(hole ? Pal.Black[0] : rim || text ? Pal.Cream[3] : Pal.Crimson[Pal.Hash(x, y, 1, 2272) > 0.7f ? 2 : 3]));
                }
            return g;
        }

        static VoxelGrid Hydrant()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var red = Pal.Weathered(Pal.Crimson, 0.4f, 2280, 3, 0);
            g.CylY(0, 0, 1.6f, 0, 8, red);
            g.CylY(0, 0, 2.2f, 0, 0, Pal.Ramp(Pal.Crimson, 2));
            g.CylY(0, 0, 1.1f, 9, 10, Pal.Ramp(Pal.Ochre, 3));
            g.CylX(6, 0, 0.8f, -3, 3, red);
            g.Set(0, 6, -2, Pal.Solid(Pal.Ochre[3]));
            return g;
        }

        static VoxelGrid Bollard()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 1.4f, 0, 11, p => p.y == 8 || p.y == 9 ? Hazard : Pal.Weathered(Pal.Metal, 0.5f, 2290, 2, 0)(p));
            return g;
        }

        static VoxelGrid Planter()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.Box(-7, 0, -7, 7, 6, 7, Pal.Weathered(Conc, 0.15f, 2300, 2, 0));
            g.Box(-6, 6, -6, 6, 6, 6, Pal.Ramp(Pal.Wood, 0, 2301));                                   // soil
            g.Mat(Wood);
            var rnd = new System.Random(2302);
            for (int x = -6; x <= 6; x++)
                for (int z = -6; z <= 6; z++)
                {
                    if (rnd.NextDouble() < 0.3) continue;
                    int h = 1 + rnd.Next(3);
                    bool dead = rnd.NextDouble() < 0.25;
                    for (int y = 7; y < 7 + h; y++) g.Set(x, y, z, Pal.Ramp(dead ? Scrub : Leaf, 1, 2303 + x * 13 + z));
                    if (!dead && rnd.NextDouble() < 0.35) g.Set(x, 7 + h, z, Pal.Solid(Flowers[rnd.Next(Flowers.Length)]));
                }
            return g;
        }

        static VoxelGrid Hedge()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -12; x <= 12; x++) for (int y = 0; y <= 10; y++) for (int z = -3; z <= 3; z++)
            {
                var p = new Vector3Int(x, y, z);
                float n = Pal.Noise((Vector3)p * 0.35f, 2310) * 1.4f;
                if (Mathf.Abs(z) + (y > 8 ? (y - 8) * 1.2f : 0f) > 3f + n - 0.3f) continue;
                if (Pal.Hash(p, 2311) < 0.06f) continue;
                bool dry = Pal.Noise(new Vector3(x * 0.2f, 0, z * 0.2f), 2312) > 0.45f;
                g.Set(p, Pal.Solid((dry ? Scrub : Leaf)[Mathf.Clamp(y * 3 / 11 + (Pal.Hash(p, 2313) > 0.75f ? 1 : 0), 0, 3)]));
            }
            return g;
        }

        static VoxelGrid ParkTree(int v)
        {
            var g = new VoxelGrid().Mat(Wood);
            var bark = Pal.Ramp(Pal.Wood, 1, 2320 + v);
            int top = v == 0 ? 20 : 16;
            g.Tube(Vector3.zero, new Vector3(0.5f, top, 0), 1.3f, bark);
            var rnd = new System.Random(2321 + v);
            var crowns = new List<Vector3> { new Vector3(0, top + 6, 0) };
            for (int b = 0; b < 4; b++)
            {
                float a = b * 1.57f + (float)rnd.NextDouble() * 0.8f;
                var end = new Vector3(Mathf.Cos(a) * 7f, top + 2 + rnd.Next(4), Mathf.Sin(a) * 7f);
                g.Tube(new Vector3(0, top - 4 + b, 0), end, 0.7f, bark);
                crowns.Add(end + Vector3.up * 3f);
            }
            int seedC = 2322 + v * 5;
            foreach (var c in crowns)
            {
                float r = c.y > top + 5 ? 8f : 6f;
                if (v == 1 && c.x > 2f) continue;                                                     // half the crown died back
                for (int x = -9; x <= 9; x++) for (int y = -7; y <= 7; y++) for (int z = -9; z <= 9; z++)
                {
                    var p = new Vector3Int(Mathf.RoundToInt(c.x) + x, Mathf.RoundToInt(c.y) + y, Mathf.RoundToInt(c.z) + z);
                    float d = new Vector3(x, y * 1.3f, z).magnitude + Pal.Noise((Vector3)p * 0.3f, seedC) * 2.4f;
                    if (d > r || (d < r - 2.4f && (p.y & 1) == 0) || Pal.Hash(p, seedC) < 0.1f) continue;
                    g.Set(p, Pal.Solid(Leaf[Mathf.Clamp((p.y - top) / 4 + (Pal.Hash(p, seedC + 1) > 0.7f ? 1 : 0), 0, 3)]));
                }
            }
            return g;
        }

        static VoxelGrid Fountain()
        {
            var g = new VoxelGrid().Mat(Stone);
            var stone = Pal.Weathered(Conc, 0.1f, 2330, 2, 0);
            g.CylY(0, 0, 20f, 0, 4, stone, 18.2f);                                                   // the basin wall
            g.CylY(0, 0, 18.5f, 0, 0, Pal.Ramp(Conc, 0, 2331));
            g.Mat(Glass);
            g.CylY(0, 0, 18.2f, 2, 2, p => Pal.Hash(p, 2332) > 0.85f ? Pal.Moss[2] : Pal.Pick(Water, p, 2333, 2));   // standing water, algae
            g.Mat(Stone);
            g.CylY(0, 0, 2.6f, 1, 12, stone);
            g.CylY(0, 0, 7f, 12, 12, stone);                                                        // the bowls (solid: they hang on the column)
            g.CylY(0, 0, 7f, 13, 13, stone, 5.8f);
            g.Mat(Glass); g.CylY(0, 0, 5.8f, 13, 13, Pal.Ramp(Water, 2, 2335)); g.Mat(Stone);
            g.CylY(0, 0, 3.4f, 18, 18, stone);
            g.CylY(0, 0, 3.4f, 19, 19, stone, 2.4f);
            g.CylY(0, 0, 1.4f, 13, 22, stone);
            g.Mat(Scrap); g.CylY(0, 0, 0.9f, 23, 25, Pal.Ramp(Pal.Bronze, 2, 2334));
            g.Mat(Stone); g.Remove(p => p.y >= 3 && p.y <= 4 && p.x > 12 && p.z > 8 && p.x * p.x + p.z * p.z > 18 * 18);   // a chipped rim
            return g;
        }

        static VoxelGrid Statue()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.Box(-6, 0, -6, 6, 1, 6, Pal.Ramp(Conc, 0, 2340));
            g.Box(-5, 2, -5, 5, 13, 5, Pal.Weathered(Conc, 0.1f, 2341, 2, 0));
            g.Mat(Scrap);
            g.Box(-3, 7, -6, 3, 10, -6, Pal.Ramp(Pal.Bronze, 1, 2342));                                // plaque
            g.Mat(Stone);
            foreach (var (x, y) in new[] { (-4, 4), (-3, 5), (-2, 4), (2, 3), (3, 4), (4, 3) }) g.Set(x, y, -6, Pal.Solid(y % 2 == 0 ? Pal.Crimson[4] : Pal.PaleBlue[1]));   // tag on the plinth
            g.Mat(Scrap);
            var bronze = (VoxMat)(p => Pal.Hash(p, 2343) > 0.65f ? Pal.Moss[3] : Pal.Pick(Pal.Bronze, p, 2344, 1));   // green with age
            g.Box(-2, 14, -1, -1, 22, 1, bronze); g.Box(1, 14, -1, 2, 22, 1, bronze);                // legs
            g.Box(-3, 23, -2, 3, 31, 2, bronze);                                                      // coat
            g.Box(-1, 32, -1, 1, 35, 1, bronze);                                                      // head
            g.Tube(new Vector3(3, 30, 0), new Vector3(6, 37, -2), 1f, bronze);                        // raised arm
            g.Tube(new Vector3(-3, 30, 0), new Vector3(-4, 24, -1), 1f, bronze);
            return g;
        }

        static VoxelGrid Pond()
        {
            var g = new VoxelGrid().Mat(Stone);
            for (int x = -26; x <= 26; x++) for (int z = -22; z <= 22; z++)
            {
                float d = new Vector2(x / 26f, z / 22f).magnitude + Pal.Noise(new Vector3(x * 0.2f, 0, z * 0.2f), 2350) * 0.12f;
                if (d > 1f) continue;
                var p = new Vector3Int(x, 0, z);
                if (d > 0.88f) { g.Mat(Stone); g.Set(x, 0, z, Pal.Ramp(Conc, d > 0.95f ? 1 : 2, 2351)); if (Pal.Hash(p, 2352) > 0.6f) g.Set(x, 1, z, Pal.Ramp(Conc, 2, 2353)); continue; }
                g.Mat(Glass);
                g.Set(x, 0, z, Pal.Solid(Pal.Hash(p, 2354) > 0.9f ? Pal.Moss[2] : Water[d < 0.5f ? 0 : d < 0.75f ? 1 : 2]));
                if (d > 0.7f && Pal.Hash(p, 2355) > 0.8f) { g.Mat(Wood); for (int y = 1; y <= 3 + (int)(Pal.Hash(p, 2356) * 4f); y++) g.Set(x, y, z, Pal.Ramp(Scrub, 2, 2357)); }   // reeds
            }
            return g;
        }

        static VoxelGrid Well()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.CylY(0, 0, 7f, 0, 8, Pal.Stripe(Pal.Ramp(Conc, 2, 2360), Pal.Ramp(Conc, 0, 2361), 1, 3), 5f);
            g.CylY(0, 0, 5f, 0, 0, Pal.Solid(Pal.Black[0]));
            g.Mat(Wood);
            foreach (int x in new[] { -6, 6 }) g.Box(x, 9, 0, x, 28, 0, Pal.Ramp(Pal.Wood, 1, 2362));
            g.CylX(20, 0, 1f, -6, 6, Pal.Ramp(Pal.Wood, 2, 2363));                                   // windlass
            for (int z = -6; z <= 6; z++) g.Box(-8, 25 + (6 - Mathf.Abs(z)) / 2, z, 8, 25 + (6 - Mathf.Abs(z)) / 2, z, Pal.Ramp(RoofRed, 1, 2364));
            g.Mat(Scrap); g.Box(1, 12, -1, 3, 15, 1, Pal.Ramp(Pal.Metal, 2, 2365));                  // the bucket
            g.Box(2, 16, 0, 2, 19, 0, Pal.Ramp(Pal.Metal, 1));
            return g;
        }

        static VoxelGrid GraffitiWall(int v)
        {
            var g = new VoxelGrid().Mat(Stone);
            var wall = v == 0 ? Pal.Weathered(Conc, 0.12f, 2370, 2, 0) : Pal.Stripe(Pal.Ramp(Brick, 2, 2371), Pal.Ramp(Brick, 0, 2372), 1, 4);
            var rnd = new System.Random(2373 + v);
            for (int x = -16; x <= 16; x++)
            {
                int top = 20 + (int)(Pal.Noise(new Vector3(x * 0.3f, 0, v), 2374) * 4f) - (x > 10 ? (x - 10) : 0);   // ragged, broken end
                for (int y = 0; y <= top; y++) for (int z = -1; z <= 1; z++) g.Set(x, y, z, wall);
            }
            // tags on the -Z face: blobby letters, an outline colour, drips
            var c0 = Spray[rnd.Next(Spray.Length)]; var c1 = Spray[rnd.Next(Spray.Length)];
            for (int i = 0; i < 6; i++)
            {
                int cx = -13 + i * 5 + rnd.Next(2), cy = 6 + rnd.Next(5), w = 1 + rnd.Next(2), h = 4 + rnd.Next(4);
                for (int x = cx - w; x <= cx + w; x++) for (int y = cy; y <= cy + h; y++)
                {
                    if (!g.Has(x, y, -1)) continue;
                    bool edge = x == cx - w || x == cx + w || y == cy || y == cy + h;
                    if (!edge && Pal.Hash(x, y, i, 2375) < 0.35f) continue;
                    g.Set(x, y, -1, Pal.Solid(edge ? c1 : c0));
                }
                for (int y = cy - 1; y >= cy - 1 - rnd.Next(4); y--) if (g.Has(cx, y, -1)) g.Set(cx, y, -1, Pal.Solid(c0));   // drip
            }
            if (v == 1)
            {
                // the gang mark: a skull
                for (int x = -3; x <= 3; x++) for (int y = 13; y <= 19; y++)
                {
                    bool eye = (x == -2 || x == 2) && y == 17, jaw = y < 15 && (x & 1) == 0;
                    if (new Vector2(x, (y - 16) * 0.9f).magnitude > 3.6f || eye || jaw) continue;
                    if (g.Has(x + 8, y, -1)) g.Set(x + 8, y, -1, Pal.Solid(Pal.Cream[4]));
                }
            }
            return g;
        }

        static VoxelGrid Drum(bool burning, int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            var paint = burning ? Pal.Rust : seed % 3 == 0 ? Pal.Navy : seed % 3 == 1 ? Pal.Crimson : Pal.Olive;
            g.CylY(0, 0, 3.3f, 0, 10, p => p.y == 3 || p.y == 7 ? Pal.Ramp(Pal.Metal, 2)(p) : Pal.Weathered(paint, burning ? 0.9f : 0.45f, 2380 + seed, 2, 0)(p));
            if (!burning) return g;
            g.Remove(p => p.y >= 2 && p.x * p.x + p.z * p.z < 2.4f * 2.4f);                          // open, hollow
            g.Mat(Wood);
            g.CylY(0, 0, 2.3f, 2, 8, p => Pal.Hash(p, 2381) > 0.5f ? Pal.Black[1] : Pal.Wood[0], 1.2f);   // charred wood
            g.Mat(Scrap);
            foreach (var p in new[] { new Vector3Int(3, 4, 0), new Vector3Int(-2, 5, 2), new Vector3Int(0, 3, -3), new Vector3Int(2, 6, 2) })
                g.Set(p, Pal.Solid(p.y % 2 == 0 ? Pal.Amber : Pal.LightY));                        // embers through the holes
            g.Set(0, 9, 0, Pal.Solid(Pal.Amber)); g.Set(1, 9, 0, Pal.Solid(Pal.LightY));
            return g;
        }

        // ------------------------------------------------------------------ buildings and fences

        static VoxelGrid Warehouse(int seed)
        {
            var g = new VoxelGrid();
            int w = 32, l = 23, h = 24;
            var sheet = Pal.Stripe(Pal.Weathered(Pal.Metal, 0.5f, 2400 + seed, 2, 0), Pal.Ramp(Pal.Rust, 2, 2401 + seed), 0, 2);
            var sheetZ = Pal.Stripe(Pal.Weathered(Pal.Metal, 0.5f, 2402 + seed, 2, 0), Pal.Ramp(Pal.Rust, 2, 2403 + seed), 2, 2);
            Slab(g, w, l, 0, Stone, Pal.Ramp(Conc, 1, 2404));
            for (int x = -w; x <= w; x++)
            for (int z = -l; z <= l; z++)
            {
                if (Mathf.Abs(x) != w && Mathf.Abs(z) != l) continue;
                for (int y = 1; y <= h; y++)
                {
                    bool roller = z == -l && Mathf.Abs(x) <= 10 && y <= 16;
                    bool door = z == -l && x >= 15 && x <= 18 && y <= 10;
                    if (door) continue;
                    if (roller)
                    {
                        if (seed == 1 && y <= 7) continue;                                             // jammed half open
                        g.Mat(Scrap); g.Set(x, y, z, p => p.y % 2 == 0 ? Pal.Metal[1] : Pal.Metal[2]);
                        continue;
                    }
                    if (y <= 2) { g.Mat(Stone); g.Set(x, y, z, Pal.Ramp(Conc, 0, 2405)); continue; }  // plinth
                    bool hazard = z == -l && Mathf.Abs(x) >= 11 && Mathf.Abs(x) <= 12 && y <= 16;
                    g.Mat(Scrap); g.Set(x, y, z, hazard ? ((x + y) % 4 < 2 ? Pal.Solid(Hazard) : Pal.Solid(Pal.Black[1])) : Mathf.Abs(z) == l ? sheet : sheetZ);
                }
            }
            g.Mat(Scrap);
            for (int x = -w - 1; x <= w + 1; x++)
            for (int z = -l - 1; z <= l + 1; z++)
            {
                int y = h + 1 + (l + 1 - Mathf.Abs(z)) / 5;
                bool sky = Mathf.Abs(z) < l - 3 && Mathf.Abs(z) > 3 && x % 12 >= 0 && x % 12 < 3;
                if (seed == 1 && x > 10 && x < 24 && z > -6 && z < 10) continue;                       // a fallen-in roof bay
                if (sky) { g.Mat(Glass); g.Set(x, y, z, Pal.Ramp(Pal.Glass, 2, 2406)); g.Mat(Scrap); }
                else g.Set(x, y, z, Pal.Stripe(Pal.Weathered(Pal.Rust, 0.3f, 2407 + seed, 2, 0), Pal.Ramp(Pal.Metal, 1, 2408), 0, 3));
            }
            foreach (int x in new[] { -w, w }) for (int z = -l; z <= l; z++) for (int y = h + 1; y <= h + (l + 1 - Mathf.Abs(z)) / 5; y++) g.Set(x, y, z, sheetZ);   // gable ends
            g.Box(-14, h - 4, -l - 1, 14, h - 1, -l - 1, p => (p.x % 3 == 0 && p.y == h - 3) ? Pal.Cream[1] : Pal.Weathered(Pal.Navy, 0.5f, 2409 + seed, 2, 0)(p));   // faded company sign
            g.Mat(Wood);
            for (int i = 0; i < 3; i++) g.Box(-24 + i * 6, 1, 10, -20 + i * 6, 5, 14, Pal.Ramp(Pal.Wood, 2, 2410 + i));   // pallets inside
            return g;
        }

        static VoxelGrid Canopy()
        {
            var g = new VoxelGrid().Mat(Scrap);
            foreach (int x in new[] { -26, 26 }) foreach (int z in new[] { -16, 16 }) g.Box(x, 0, z, x + 1, 27, z + 1, Pal.Weathered(Pal.Cream, 0.35f, 2420, 2, 0));
            for (int x = -30; x <= 30; x++)
            for (int z = -20; z <= 20; z++)
            {
                bool edge = Mathf.Abs(x) >= 29 || Mathf.Abs(z) >= 19;
                if (!edge && Pal.Hash(x / 5, 0, z / 5, 2421) < 0.15f) continue;                          // panels blown out
                g.Set(x, 28, z, Pal.Ramp(Pal.Cream, 2, 2422));
                if (edge) for (int y = 29; y <= 31; y++) g.Set(x, y, z, y == 30 ? Pal.Solid(Pal.Crimson[3]) : Pal.Weathered(Pal.Cream, 0.3f, 2423, 3, 0));
            }
            g.Mat(Glass);
            for (int x = -20; x <= 20; x += 10) g.Box(x - 1, 27, -2, x + 1, 27, 2, Pal.Solid(Pal.Cream[2]));   // dead strip lights
            return g;
        }

        static VoxelGrid PicketFence()
        {
            var g = new VoxelGrid().Mat(Wood);
            var white = Pal.Weathered(Pal.Cream, 0.25f, 2430, 2, 0);
            for (int x = -24; x <= 24; x += 3)
            {
                if (Pal.Hash(x, 0, 0, 2431) < 0.1f) continue;                                          // a picket kicked out
                g.Box(x, 0, 0, x, 10, 0, p => Pal.Hash(p, 2432) < 0.2f ? Pal.Wood[2] : white(p));
                g.Set(x, 11, 0, white);
            }
            foreach (int y in new[] { 3, 8 }) g.Box(-24, y, 1, 24, y, 1, Pal.Ramp(Pal.Wood, 2, 2433 + y));
            foreach (int x in new[] { -24, 0, 24 }) g.Box(x, 0, 1, x, 9, 1, Pal.Ramp(Pal.Wood, 1, 2435));
            return g;
        }

        static VoxelGrid ChainLink()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Metal, 0.5f, 2440, 2, 0);
            foreach (int x in new[] { -24, -8, 8, 24 }) g.Box(x, 0, 0, x, 23, 0, steel);
            g.Box(-24, 22, 0, 24, 22, 0, steel);
            for (int x = -24; x <= 24; x++)
                for (int y = 1; y <= 21; y++)
                {
                    if (((x + y) % 3 + 3) % 3 != 0 && ((x - y) % 3 + 3) % 3 != 0) continue;
                    if (new Vector2(x - 6, y - 6).magnitude < 4.5f + Pal.Hash(x, y, 0, 2441) * 1.5f) continue;   // cut open
                    g.Set(x, y, 0, Pal.Ramp(Pal.Chrome, 0, 2442));
                }
            for (int x = -24; x <= 24; x += 2) g.Set(x, 24 + (x / 2 & 1), 0, Pal.Ramp(Pal.Chrome, 1));   // barbed wire
            return g;
        }

        static VoxelGrid Rubble()
        {
            var g = new VoxelGrid().Mat(Stone);
            var rnd = new System.Random(2450);
            for (int i = 0; i < 9; i++)
            {
                int cx = rnd.Next(-8, 9), cz = rnd.Next(-6, 7), r = 2 + rnd.Next(3);
                for (int x = -r; x <= r; x++) for (int y = 0; y <= r; y++) for (int z = -r; z <= r; z++)
                {
                    var p = new Vector3Int(cx + x, y, cz + z);
                    if (new Vector3(x, y * 1.6f, z).magnitude > r + Pal.Noise((Vector3)p * 0.5f, 2451) * 1.2f) continue;
                    g.Set(p, i % 3 == 0 ? Pal.Ramp(Brick, 1, 2452 + i) : Pal.Ramp(Conc, 1 + i % 2, 2453 + i));
                }
            }
            g.Mat(Scrap);
            g.Tube(new Vector3(-3, 1, -2), new Vector3(2, 9, 1), 0.4f, Pal.Ramp(Pal.Rust, 2, 2454));    // rebar
            g.Tube(new Vector3(4, 1, 2), new Vector3(7, 7, -1), 0.4f, Pal.Ramp(Pal.Rust, 2, 2455));
            return g;
        }

        // ------------------------------------------------------------------ the plan's placements

        static readonly Dictionary<int, Dictionary<Vector2Int, List<int>>> layoutChunks = new Dictionary<int, Dictionary<Vector2Int, List<int>>>();
        static readonly Dictionary<int, Dictionary<Vector2Int, List<int>>> decorChunks = new Dictionary<int, Dictionary<Vector2Int, List<int>>>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTownStatics() { layoutChunks.Clear(); decorChunks.Clear(); layouts.Clear(); layoutSeed = int.MinValue; }

        /// <summary>A planned settlement's placements: the plan's prop spots in order (keys "s{town},{i}" are this index).</summary>
        static void PlanLayout(Settlement st, List<Placement> list)
        {
            foreach (var sp in st.plan.spots)
            {
                if (sp.kind != SpotKind.Prop) continue;
                list.Add(new Placement { id = sp.id, pos = sp.pos, yaw = sp.yaw, dynamic = sp.dynamic, crate = sp.crate, y = sp.y, table = sp.table, visual = sp.visual });
            }
        }

        /// <summary>Placement indices of a settlement's layout by chunk.</summary>
        static List<int> LayoutIn(Settlement st, List<Placement> list, Vector2Int c)
        {
            if (!layoutChunks.TryGetValue(st.index, out var map))
            {
                layoutChunks[st.index] = map = new Dictionary<Vector2Int, List<int>>();
                for (int i = 0; i < list.Count; i++)
                {
                    var k = DeformableTerrain.ChunkOf(new Vector3(list[i].pos.x, 0f, list[i].pos.y));
                    if (!map.TryGetValue(k, out var l)) map[k] = l = new List<int>();
                    l.Add(i);
                }
            }
            return map.TryGetValue(c, out var r) ? r : null;
        }

        static readonly List<CombineInstance> combine = new List<CombineInstance>();

        /// <summary>Small loose decor of planned towns in this chunk (trash bags, litter, newspapers) as one mesh, no
        /// colliders (cheap: dozens per chunk downtown).</summary>
        static void PopulateTownDecor(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat)
        {
            var world = terrain.World;
            float cx = (c.x + 0.5f) * DeformableTerrain.ChunkWorld, cz = (c.y + 0.5f) * DeformableTerrain.ChunkWorld;
            var plan = world.PlanAt(cx, cz);
            if (plan == null) return;
            if (!decorChunks.TryGetValue(plan.st.index, out var map))
            {
                decorChunks[plan.st.index] = map = new Dictionary<Vector2Int, List<int>>();
                for (int i = 0; i < plan.spots.Count; i++)
                {
                    if (plan.spots[i].kind != SpotKind.Decor) continue;
                    var k = DeformableTerrain.ChunkOf(new Vector3(plan.spots[i].pos.x, 0f, plan.spots[i].pos.y));
                    if (!map.TryGetValue(k, out var l)) map[k] = l = new List<int>();
                    l.Add(i);
                }
            }
            if (!map.TryGetValue(c, out var here)) return;
            combine.Clear();
            var origin = new Vector3(c.x * DeformableTerrain.ChunkWorld, 0f, c.y * DeformableTerrain.ChunkWorld);
            var decorMat = terrain.worldPropMaterial ? terrain.worldPropMaterial : mat;
            int hdFlags = HDProp.FlagsOf(decorMat) | MadMax.Rendering.HDAssets.NoShadow;
            int verts = 0;
            foreach (int i in here)
            {
                var sp = plan.spots[i];
                if (!templates.TryGetValue(sp.id, out var t) || !t.mesh) continue;
                if (world.YardWeight(sp.pos.x, sp.pos.y) > 0.05f) continue;
                var p = new Vector3(sp.pos.x, terrain.Height(sp.pos.x, sp.pos.y) - 0.02f, sp.pos.y);
                var hd = MadMax.Rendering.HDAssets.Get(MadMax.Rendering.HDDomain.World, sp.id);
                if (hd)                                                                            // HD pack: the model per spot instead of the batched voxels
                {
                    var d = new GameObject("TownDecorHD");
                    d.transform.SetParent(parent, false);
                    d.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, sp.yaw, 0f));
                    MadMax.Rendering.HDVisual.Dress(d, hd, hdFlags);
                    continue;
                }
                combine.Add(new CombineInstance { mesh = t.mesh, transform = Matrix4x4.TRS(p - origin, Quaternion.Euler(0f, sp.yaw, 0f), Vector3.one) });
                verts += t.mesh.vertexCount;
            }
            if (combine.Count == 0) return;
            var mesh = new Mesh { name = "TownDecor " + c };
            if (verts > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(combine.ToArray(), true, true);
            var go = new GameObject("TownDecor", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = decorMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<TownDecor>().mesh = mesh;
        }
    }
}
