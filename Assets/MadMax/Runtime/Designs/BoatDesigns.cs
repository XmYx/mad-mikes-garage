using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Marine engines (user additions): a two-stroke outboard that clamps on any transom (its leg and
    /// three-blade prop hang below the keel) and a big inboard diesel for work boats and the submarine.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> Marine()
        {
            // outboard: cowling behind the clamp (origin on the transom), leg down past the keel, prop, tiller forward
            var g = new VoxelGrid().Mat((byte)ResourceType.Aluminium);
            var cowl = Pal.Weathered(Pal.Ochre, 0.3f, 2701, 2, 0);
            g.Box(-3, 1, -6, 3, 7, -1, cowl);
            g.Box(-3, 8, -6, 3, 8, -1, Pal.Ramp(Pal.Black, 1, 2702));                                  // cap
            g.Box(-2, 3, -7, 2, 5, -7, p => (p.x & 1) == 0 ? Pal.Black[0] : Pal.Black[2]);             // vents
            g.Box(-1, -1, -1, 1, 1, 0, Pal.Ramp(Pal.Metal, 1, 2704));                                  // clamp bracket
            g.Box(-1, -12, -4, 1, 0, -3, Pal.Weathered(Pal.Metal, 0.35f, 2705, 2, 0));                  // leg
            g.Box(-2, -14, -5, 2, -12, -2, Pal.Ramp(Pal.Metal, 1, 2706));                              // gearcase
            g.Box(0, -13, -7, 0, -13, -6, Pal.Ramp(Pal.Black, 1, 2707));                               // prop shaft
            for (int b = 0; b < 3; b++)
            {
                float a = b * Mathf.PI * 2f / 3f;
                g.Tube(new Vector3(0, -13, -8), new Vector3(Mathf.Cos(a) * 3f, -13 + Mathf.Sin(a) * 3f, -8), 0.6f, Pal.Ramp(Pal.Chrome, 2, 2708));
            }
            g.Tube(new Vector3(0, 5, -1), new Vector3(0, 6, 7), 0.5f, Pal.Ramp(Pal.Black, 1, 2709));    // tiller
            g.Set(0, 6, 7, Pal.Solid(Pal.Crimson[3]));
            var o = Make("engine_outboard", PartCategory.Engine, g, 45, 1);
            o.torque = 55f; o.maxRpm = 5800f; o.peakAt = 0.8f;
            yield return o;

            // marine diesel: a big green block with a rusty manifold and a tall stack
            var m = new VoxelGrid().Mat((byte)ResourceType.Iron);
            var block = Pal.Weathered(Pal.RigGreen, 0.35f, 2711, 2, 0);
            m.Box(-5, 0, -8, 5, 9, 8, block);
            m.Box(-6, 4, -7, -6, 8, 7, p => (p.z & 3) == 0 ? Pal.Rust[0] : Pal.Rust[2]);               // exhaust manifold
            m.Box(-4, 10, -7, 4, 11, 7, Pal.Ramp(Pal.Crimson, 1, 2712));                               // rocker covers
            m.Box(-5, 1, -10, 5, 8, -9, Pal.Ramp(Pal.Metal, 1, 2713));                                 // flywheel housing
            m.Tube(new Vector3(-6, 8, -6), new Vector3(-6, 24, -6), 1.1f, Pal.Weathered(Pal.Black, 0.5f, 2714, 1, 0));   // stack
            m.Box(-7, 25, -7, -5, 25, -5, Pal.Ramp(Pal.Rust, 1, 2715));
            m.Box(5, 2, 4, 6, 7, 7, Pal.Ramp(Pal.Metal, 2, 2716));                                     // raw-water pump
            var dm = Make("engine_marine_diesel", PartCategory.Engine, m, 420, 3);
            dm.torque = 620f; dm.maxRpm = 2600f; dm.peakAt = 0.55f;
            yield return dm;
        }
    }

    /// <summary>Watercraft (user additions): an oil-drum raft, an aluminium fishing skiff, a rust-streaked trawler with
    /// a wheelhouse and a trawl net, a shanty houseboat you can live in, and the IRON EEL — a riveted submarine that
    /// doubles as a base. <see cref="BoatModel"/> floats and drives them (and <see cref="Submarine"/> dives the Eel);
    /// they ride on the same sockets as cars (engines swap). Built at a SLIPWAY.</summary>
    public static partial class VehicleDesigns
    {
        const int TrawlerDeck = 20;

        public static VehicleDesign Raft()
        {
            var d = new VehicleDesign
            {
                name = "Raft", mass = 220, finalDrive = 1f, gears = new[] { 1f }, brakeForce = 0f, maxSteer = 0f, eye = new Vector3Int(0, 23, -13),
                fuelL = 20f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true, com = new Vector3(0f, 0.45f, 0f),
                boat = "raft", boatHull = new Vector3(2.2f, 0.75f, 3.3f), boatDraft = 0.22f, boatThrust = 1800f, boatRudder = 0.7f, boatDeck = new Vector3Int(6, 11, -4)
            };
            var g = new VoxelGrid().Mat((byte)ResourceType.Scrap);
            foreach (int x in new[] { -8, 8 })
            foreach (int z in new[] { -14, 0, 14 })
            {
                var drum = Pal.Weathered(((x + z) & 8) == 0 ? Pal.Olive : Pal.Navy, 0.45f, 2721 + x + z, 2, 0);
                int zz = z;
                g.CylZ(x, 4, 4f, z - 6, z + 6, p => (p.z - zz + 6) % 6 == 0 ? Pal.Rust[1] : drum(p));     // hoops
            }
            g.Mat((byte)ResourceType.Wood);
            for (int x = -13; x <= 13; x++) g.Box(x, 9, -21, x, 9, 21, Pal.Ramp(Pal.Wood, ((x + 13) / 3) % 2 == 0 ? 2 : 1, 2730 + x));
            foreach (int z in new[] { -18, -6, 6, 18 }) g.Box(-13, 10, z, 13, 10, z, Pal.Ramp(Pal.Cream, 1, 2732));      // lashings
            g.Tube(new Vector3(0, 10, 14), new Vector3(0, 44, 14), 0.7f, Pal.Ramp(Pal.Wood, 1, 2733));                  // mast
            g.Mat((byte)ResourceType.Cloth);
            for (int y = 16; y <= 40; y++)
            {
                int w = (40 - y) / 2 + 1;
                g.Box(1, y, 14 - w, 1, y, 14, p => ((p.y + p.z) % 7 == 0) ? Pal.Crimson[1] : Pal.Cream[2]);            // patched tarp sail
            }
            g.Mat((byte)ResourceType.Wood);
            g.Box(-3, 10, -16, 3, 14, -11, Pal.Weathered(Pal.Wood, 0.2f, 2734, 2, 0));                                // crate seat
            g.Tube(new Vector3(-10, 10, -8), new Vector3(-6, 10, 12), 0.5f, Pal.Ramp(Pal.Wood, 3, 2735));             // paddle
            g.Box(-7, 10, 11, -5, 10, 13, Pal.Ramp(Pal.Wood, 3, 2735));
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-13, 0, -21, 13, 10, 21));
            d.Socket("engine", PartCategory.Engine, 0, 10, -22, null, false, 1);                                       // clamp an outboard on: no more paddling
            return d;
        }

        public static VehicleDesign Skiff()
        {
            var d = new VehicleDesign
            {
                name = "Skiff", mass = 320, finalDrive = 1f, gears = new[] { 1f }, brakeForce = 0f, maxSteer = 0f, eye = new Vector3Int(3, 19, -19),
                fuelL = 25f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true, com = new Vector3(0f, 0.3f, -0.2f),
                boat = "skiff", boatHull = new Vector3(1.7f, 0.7f, 4.4f), boatDraft = 0.2f, boatThrust = 2600f, boatRudder = 1.1f, boatDeck = new Vector3Int(0, 3, -8)
            };
            var g = new VoxelGrid().Mat((byte)ResourceType.Aluminium);
            var hullC = Pal.Weathered(Pal.Metal, 0.35f, 2741, 2, 0);
            var stripe = Pal.Ramp(Pal.Olive, 2, 2742);
            for (int z = -27; z <= 27; z++)
            {
                float bow = Mathf.Clamp01((z - 17) / 10f);                                                              // the scow bow sweeps up
                int bottom = Mathf.RoundToInt(bow * bow * 7f), hw = 10 - Mathf.RoundToInt(bow * 2f);
                for (int x = -hw + 1; x <= hw - 1; x++) g.Set(x, bottom, z, (z & 7) == 0 ? Pal.Solid(Pal.Metal[1]) : hullC);   // bottom sheet, ribs
                for (int y = bottom; y <= 9; y++)
                    foreach (int sx in new[] { -1, 1 })
                        g.Set(sx * hw, y, z, y == 7 ? stripe : (y == 3 && (z & 3) == 0) ? Pal.Solid(Pal.Chrome[2]) : hullC);   // sides, rivets
                g.Set(-hw, 10, z, Pal.Solid(Pal.Metal[3])); g.Set(hw, 10, z, Pal.Solid(Pal.Metal[3]));                          // gunwale rail
            }
            g.Box(-10, 0, -27, 10, 9, -27, hullC);                                                                      // transom
            g.Box(-8, 7, 27, 8, 10, 27, hullC);                                                                         // bow plate
            g.Mat((byte)ResourceType.Wood);
            foreach (int z in new[] { -19, -2, 14 }) g.Box(-9, 6, z, 9, 6, z + 3, Pal.Ramp(Pal.Wood, 2, 2743 + z));      // benches
            g.Mat((byte)ResourceType.Scrap);
            g.Box(-7, 1, 3, -3, 5, 8, p => p.y == 5 ? Pal.Cream[4] : Pal.Crimson[2]);                                  // cooler
            foreach (int sx in new[] { -1, 1 }) g.Tube(new Vector3(sx * 9, 10, -8), new Vector3(sx * 12, 30, -16), 0.35f, Pal.Ramp(Pal.Black, 1, 2744));   // fishing rods
            g.Set(0, 11, 26, Pal.Solid(Pal.LightW));
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-10, 0, -27, 10, 10, 27));
            d.Socket("engine", PartCategory.Engine, 0, 9, -28, "engine_outboard", false, 1);
            return d;
        }

        public static VehicleDesign Trawler()
        {
            var d = new VehicleDesign
            {
                name = "Trawler", mass = 7000, finalDrive = 1f, gears = new[] { 1f }, brakeForce = 0f, maxSteer = 0f, eye = new Vector3Int(0, TrawlerDeck + 15, 26),
                fuelL = 300f, oilL = 20f, coolantL = 30f, cargoKg = 800f, com = new Vector3(0f, 0.9f, 0f),
                boat = "trawler", boatHull = new Vector3(3.2f, 1.6f, 9.6f), boatDraft = 0.75f, boatThrust = 26000f, boatRudder = 0.7f, boatDeck = new Vector3Int(0, TrawlerDeck + 1, -8)
            };
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            var anti = Pal.Weathered(Pal.Crimson, 0.4f, 2751, 0, 0);
            var top = Pal.Weathered(Pal.Navy, 0.55f, 2752, 2, 0);
            var deckC = Pal.Weathered(Pal.Metal, 0.3f, 2753, 1, 0);
            for (int z = -60; z <= 60; z++)
            {
                float bowT = Mathf.Clamp01((z - 20f) / 40f);
                int w = Mathf.RoundToInt(20f * Mathf.Sqrt(Mathf.Max(0f, 1f - bowT * bowT)));
                int deckY = TrawlerDeck + Mathf.RoundToInt(Mathf.Max(0f, z - 30f) * 0.15f);
                float keel = Mathf.Max(0f, z - 30f) * 0.35f;
                for (int x = -w; x <= w; x++)
                {
                    int yb = Mathf.RoundToInt(keel + Mathf.Abs(x) * 0.35f);
                    for (int y = yb; y <= deckY; y++)
                    {
                        bool edge = Mathf.Abs(x) >= w - 1 || y == yb || z == -60;
                        if (!edge && y != deckY) continue;                                                            // a shell with a deck
                        VoxMat c = y == deckY ? deckC : y < 9 ? anti : y <= 10 ? Pal.Solid(Pal.Cream[2]) : top;
                        g.Set(x, y, z, c);
                    }
                    if (Mathf.Abs(x) >= w - 1) g.Box(x, deckY + 1, z, x, deckY + 4, z, (z & 7) == 0 ? Pal.Solid(Pal.Rust[1]) : top);   // bulwark, streaks
                }
            }
            // wheelhouse with windows, a roof, a mast with lights and radar
            int f = TrawlerDeck + 1;
            for (int y = f; y <= f + 21; y++)
            for (int z = 8; z <= 32; z++)
            for (int x = -12; x <= 12; x++)
            {
                bool wall = Mathf.Abs(x) == 12 || z == 8 || z == 32;
                if (!wall) continue;
                bool door = z == 8 && Mathf.Abs(x) <= 3 && y <= f + 14;
                if (door) continue;
                bool window = y >= f + 12 && y <= f + 18 && (z == 32 ? Mathf.Abs(x) <= 10 && (x + 12) % 5 != 0 : Mathf.Abs(x) == 12 && z >= 18 && z <= 30 && z % 5 != 0);
                g.Set(x, y, z, window ? Pal.Ramp(Pal.Glass, 3, 2754) : y == f + 10 ? Pal.Solid(Pal.Cream[2]) : Pal.Weathered(Pal.Cream, 0.4f, 2755, 2, 0));
            }
            g.Box(-13, f + 22, 7, 13, f + 23, 33, Pal.Weathered(Pal.Metal, 0.4f, 2756, 1, 0));                        // roof
            g.Tube(new Vector3(0, f + 24, 20), new Vector3(0, f + 46, 20), 0.7f, Pal.Ramp(Pal.Metal, 2, 2757));       // mast
            g.Box(-6, f + 40, 20, 6, f + 40, 20, Pal.Ramp(Pal.Metal, 2, 2757));
            g.Set(-6, f + 41, 20, Pal.Solid(Pal.Crimson[4])); g.Set(6, f + 41, 20, Pal.Solid(Pal.Moss[4])); g.Set(0, f + 47, 20, Pal.Solid(Pal.LightW));   // lights
            g.Box(-3, f + 24, 24, 3, f + 25, 26, Pal.Ramp(Pal.Black, 1, 2758));                                        // radar
            g.Box(-1, f + 24, 30, 1, f + 26, 31, Pal.Solid(Pal.LightW));                                               // searchlight
            // stern: an A-frame gantry, the net drum, a heap of net with floats, the fish hold hatch
            foreach (int s in new[] { -1, 1 }) g.Tube(new Vector3(s * 18, f, -54), new Vector3(s * 14, f + 34, -58), 0.9f, Pal.Weathered(Pal.Ochre, 0.4f, 2759, 2, 0));
            g.Tube(new Vector3(-14, f + 34, -58), new Vector3(14, f + 34, -58), 0.9f, Pal.Weathered(Pal.Ochre, 0.4f, 2759, 2, 0));
            g.Mat((byte)ResourceType.Cloth);
            g.CylX(f + 5, -40, 4.5f, -10, 10, p => (p.x & 1) == 0 && ((p.y + p.z) & 1) == 0 ? Pal.Cream[3] : Pal.Olive[1]);   // net drum
            g.Box(-9, f, -58, 9, f + 3, -46, p => ((p.x + p.z) % 5 == 0) ? Pal.Crimson[3] : ((p.x ^ p.z) & 1) == 0 ? Pal.Olive[2] : Pal.Olive[0]);
            g.Mat((byte)ResourceType.Iron);
            g.Box(-6, TrawlerDeck, -22, 6, TrawlerDeck, -10, p => Mathf.Abs(p.x) == 6 || p.z == -22 || p.z == -10 ? Pal.Metal[3] : Pal.Metal[0]);   // hold hatch
            g.Mat((byte)ResourceType.Rubber);
            foreach (int z in new[] { -30, -5, 20 }) foreach (int s in new[] { -1, 1 }) g.CylX(TrawlerDeck - 2, z, 2.5f, s * 20, s * 21, Pal.Ramp(Pal.Tire, 1, 2760));   // tyre fenders
            g.Mat((byte)ResourceType.Iron);
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-20, 0, -60, 20, TrawlerDeck, 30));
            d.colliders.Add(VehicleDesign.Box(-14, 6, 30, 14, TrawlerDeck + 4, 58));
            d.colliders.Add(VehicleDesign.Box(-12, f, 8, 12, f + 23, 32));
            d.spinners.Add(("Prop", Propeller(6), new Vector3Int(0, 5, -62)));
            d.Socket("engine", PartCategory.Engine, 0, TrawlerDeck + 1, -32, "engine_marine_diesel", false, 3);
            return d;
        }

        public static VehicleDesign Houseboat()
        {
            const int F = 14;                                                                                            // deck top
            var d = new VehicleDesign
            {
                name = "Houseboat", mass = 4800, finalDrive = 1f, gears = new[] { 1f }, brakeForce = 0f, maxSteer = 0f, eye = new Vector3Int(0, F + 22, 16),
                fuelL = 60f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true, cargoKg = 300f, com = new Vector3(0f, 0.8f, 0f),
                boat = "houseboat", boatHull = new Vector3(4.2f, 1.1f, 10f), boatDraft = 0.45f, boatThrust = 5200f, boatRudder = 0.6f, boatDeck = new Vector3Int(0, F + 1, 34)
            };
            var g = new VoxelGrid().Mat((byte)ResourceType.Scrap);
            foreach (int x in new[] { -16, 16 }) g.CylZ(x, 6, 6f, -58, 58, p => (p.z & 15) == 0 ? Pal.Rust[0] : Pal.Weathered(Pal.Rust, 0.5f, 2771, 2, 0)(p));   // pontoon tanks
            for (int z = -56; z <= 56; z += 14) g.Box(-22, 11, z, 22, 12, z + 1, Pal.Ramp(Pal.Metal, 1, 2772));               // cross beams
            g.Mat((byte)ResourceType.Wood);
            for (int x = -24; x <= 24; x++) g.Box(x, 13, -60, x, F, 60, Pal.Ramp(Pal.Wood, ((x + 24) / 3) % 2 == 0 ? 2 : 1, 2773 + x));   // plank deck
            // the shack: plank walls with windows, a door onto the porch, a tin roof
            for (int y = F + 1; y <= F + 30; y++)
            for (int z = -30; z <= 20; z++)
            for (int x = -18; x <= 18; x++)
            {
                bool wall = Mathf.Abs(x) == 18 || z == -30 || z == 20;
                if (!wall) continue;
                if (z == 20 && Mathf.Abs(x) <= 6 && y <= F + 27) continue;                                             // door
                bool window = y >= F + 14 && y <= F + 22 && ((z == 20 && Mathf.Abs(x) >= 10 && Mathf.Abs(x) <= 15) || (Mathf.Abs(x) == 18 && ((z >= -22 && z <= -14) || (z >= -2 && z <= 6))) || (z == -30 && Mathf.Abs(x) <= 4));
                g.Set(x, y, z, window ? Pal.Ramp(Pal.Glass, 3, 2774) : ((x + z) & 3) == 0 ? Pal.Solid(Pal.Wood[0]) : Pal.Weathered(Pal.Wood, 0.25f, 2775, 2, 0));
            }
            g.Mat((byte)ResourceType.Scrap);
            for (int x = -20; x <= 20; x++)                                                                             // corrugated tin roof, a shallow ridge
            {
                int ry = F + 31 + (20 - Mathf.Abs(x)) / 8;
                g.Box(x, ry, -32, x, ry, 22, (x & 1) == 0 ? Pal.Weathered(Pal.Metal, 0.5f, 2776, 2, 0) : Pal.Weathered(Pal.Metal, 0.6f, 2777, 1, 0));
            }
            g.Tube(new Vector3(10, F + 33, -20), new Vector3(10, F + 44, -20), 1f, Pal.Weathered(Pal.Black, 0.5f, 2778, 1, 0));   // stovepipe
            g.Box(9, F + 45, -21, 11, F + 45, -19, Pal.Ramp(Pal.Rust, 1, 2779));
            // porch: railing, a bench, potted plants in paint cans
            g.Mat((byte)ResourceType.Wood);
            for (int x = -22; x <= 22; x += 4) g.Box(x, F + 1, 58, x, F + 10, 58, Pal.Ramp(Pal.Wood, 1, 2780));
            g.Box(-22, F + 10, 58, 22, F + 10, 58, Pal.Ramp(Pal.Wood, 2, 2781));
            foreach (int s in new[] { -1, 1 })
            {
                for (int z = 22; z <= 58; z += 4) g.Box(s * 24, F + 1, z, s * 24, F + 10, z, Pal.Ramp(Pal.Wood, 1, 2780));   // side rails
                g.Box(s * 24, F + 10, 22, s * 24, F + 10, 58, Pal.Ramp(Pal.Wood, 2, 2781));
            }
            g.Box(12, F + 1, 40, 22, F + 5, 44, Pal.Ramp(Pal.Wood, 2, 2782));                                           // bench
            foreach (int x in new[] { -20, -14 }) { g.Box(x, F + 1, 52, x + 2, F + 3, 54, Pal.Ramp(Pal.Crimson, 1, 2783)); g.Box(x, F + 4, 52, x + 2, F + 6, 54, Pal.Ramp(Pal.Moss, 2, 2784)); }
            // stern deck: a washing line
            g.Tube(new Vector3(-20, F + 1, -50), new Vector3(-20, F + 20, -50), 0.5f, Pal.Ramp(Pal.Wood, 1, 2785));
            g.Tube(new Vector3(20, F + 1, -50), new Vector3(20, F + 20, -50), 0.5f, Pal.Ramp(Pal.Wood, 1, 2785));
            g.Mat((byte)ResourceType.Cloth);
            g.Box(-20, F + 20, -50, 20, F + 20, -50, Pal.Ramp(Pal.Cream, 1, 2786));
            foreach (int x in new[] { -14, -4, 8 }) g.Box(x, F + 13, -50, x + 4, F + 19, -50, x < 0 ? Pal.Ramp(Pal.Navy, 2, 2787 + x) : Pal.Ramp(Pal.Crimson, 2, 2788));
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            var i = d.interior = new InteriorDesign { floorY = F + 0.5f, ceilingY = F + 30.5f, min = new Vector2(-17f, -29f), max = new Vector2(17f, 19f) };
            i.doors.Add((new Vector2(0, 18), new Vector3(0, F + 1, 26)));
            i.seat = new Vector2(0, 15); i.stand = new Vector2(0, 8);
            i.obstacles.Add(VehicleDesign.Box(-4, F + 1, 17, 4, F + 10, 19));                                         // helm
            i.furniture.Add(("bed", new Vector3(-12, F + 0.5f, -24), new Vector3(0, 0, 0)));
            i.furniture.Add(("stove", new Vector3(14, F + 0.5f, -10), new Vector3(0, -90, 0)));
            i.furniture.Add(("table", new Vector3(-8, F + 0.5f, 2), new Vector3(0, 0, 0)));
            i.furniture.Add(("shelf", new Vector3(17, F + 12f, 6), new Vector3(90, -90, 0)));
            d.colliders.Add(VehicleDesign.Box(-24, 0, -60, 24, F, 60));
            d.colliders.Add(VehicleDesign.Box(-18, F + 1, -30, -18, F + 30, 20));
            d.colliders.Add(VehicleDesign.Box(18, F + 1, -30, 18, F + 30, 20));
            d.colliders.Add(VehicleDesign.Box(-18, F + 1, -30, 18, F + 30, -30));
            d.colliders.Add(VehicleDesign.Box(-18, F + 1, 20, -7, F + 30, 20));
            d.colliders.Add(VehicleDesign.Box(7, F + 1, 20, 18, F + 30, 20));
            d.colliders.Add(VehicleDesign.Box(-6, F + 28, 20, 6, F + 30, 20));
            d.colliders.Add(VehicleDesign.Box(-20, F + 31, -32, 20, F + 34, 22));
            d.Socket("engine", PartCategory.Engine, 0, F, -61, "engine_outboard", false, 1);
            return d;
        }

        public static VehicleDesign IronEel()
        {
            const int C = 20, R = 17;                                                                                   // hull axis height, radius
            var d = new VehicleDesign
            {
                name = "IronEel", mass = 15000, finalDrive = 1f, gears = new[] { 1f }, brakeForce = 0f, maxSteer = 0f, eye = new Vector3Int(0, 27, 47),
                fuelL = 400f, oilL = 20f, coolantL = 30f, com = new Vector3(0f, 1.3f, 0f), airtight = true,
                boat = "sub", boatHull = new Vector3(2.8f, 2.8f, 12f), boatDraft = 2.2f, boatThrust = 9000f, boatRudder = 0.6f, boatDeck = new Vector3Int(0, C + R + 20, 20)
            };
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            var iron = Pal.Weathered(Pal.Metal, 0.5f, 2791, 1, 0);
            var olive = Pal.Weathered(Pal.RigGreen, 0.45f, 2792, 1, 0);
            float Radius(int z) => z > 45 ? R * Mathf.Sqrt(Mathf.Max(0f, 1f - ((z - 45f) / 30f) * ((z - 45f) / 30f))) : z < -45 ? R * (1f - (-45f - z) / 30f * 0.8f) : R;
            for (int z = -75; z <= 75; z++)
            {
                float r = Radius(z);
                if (r < 1f) continue;
                int ri = Mathf.CeilToInt(r);
                for (int x = -ri; x <= ri; x++)
                for (int y = C - ri; y <= C + ri; y++)
                {
                    float dist = Mathf.Sqrt(x * x + (y - C) * (y - C));
                    if (Mathf.Abs(dist - r) > 0.8f && !(Mathf.Abs(z) >= 74 && dist < r)) continue;                  // a shell, closed at the ends
                    bool port = Mathf.Abs(y - C - 2) <= 1 && Mathf.Abs(x) >= r - 1.5f && z % 15 == 0 && z > -40 && z < 50;
                    bool rivet = (z % 6 == 0) && ((x + y) & 3) == 0;
                    g.Set(x, y, z, port ? Pal.Ramp(Pal.Glass, 3, 2793) : rivet ? Pal.Solid(Pal.Chrome[1]) : y > C + 8 ? olive : iron);
                }
            }
            for (int z = -44; z <= 60; z++) g.Box(-12, 6, z, 12, 6, z, (z & 3) == 0 ? Pal.Solid(Pal.Metal[3]) : Pal.Weathered(Pal.Metal, 0.3f, 2794, 1, 0));   // deck plates inside
            // the sail: conning tower with the hatch, periscope and dive planes
            for (int y = C + R - 1; y <= C + R + 18; y++)
            for (int z = 5; z <= 30; z++)
            for (int x = -5; x <= 5; x++)
            {
                bool shell = Mathf.Abs(x) == 5 || z == 5 || z == 30 || y == C + R + 18;
                if (!shell || (z > 27 && Mathf.Abs(x) == 5 && y > C + R + 14)) continue;
                g.Set(x, y, z, y == C + R + 18 && Mathf.Abs(x) <= 1 && Mathf.Abs(z - 20) <= 1 ? Pal.Solid(Pal.Black[0]) : olive);   // hatch in the roof
            }
            g.CylY(0, 20, 1.6f, C + R + 19, C + R + 19, Pal.Ramp(Pal.Metal, 3, 2795));                                  // hatch ring
            g.Tube(new Vector3(-2, C + R + 18, 14), new Vector3(-2, C + R + 27, 14), 0.5f, Pal.Ramp(Pal.Metal, 2, 2796));   // periscope
            g.Box(-3, C + R + 27, 14, -2, C + R + 27, 15, Pal.Ramp(Pal.Glass, 3, 2797));
            g.Box(-12, C + R + 10, 18, 12, C + R + 10, 24, iron);                                                       // sail planes
            g.Box(-24, C, -64, 24, C, -58, iron);                                                                       // stern planes
            g.Box(0, C - 13, -74, 0, C + 13, -70, iron);                                                                // rudder
            g.Set(0, C + 2, 75, Pal.Solid(Pal.LightW)); g.Set(0, C + 3, 75, Pal.Solid(Pal.LightW));                    // bow lamp
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            var i = d.interior = new InteriorDesign { floorY = 6.5f, ceilingY = 33.5f, min = new Vector2(-11f, -44f), max = new Vector2(11f, 58f) };
            i.doors.Add((new Vector2(0, 20), new Vector3(0, C + R + 20, 20)));                                         // the hatch: out onto the sail
            i.seat = new Vector2(0, 48); i.stand = new Vector2(0, 40);
            i.obstacles.Add(VehicleDesign.Box(-6, 7, 51, 6, 18, 55));                                                  // helm and gauges
            i.furniture.Add(("bed", new Vector3(-8, 6.5f, -26), new Vector3(0, 0, 0)));
            i.furniture.Add(("locker", new Vector3(10, 6.5f, -12), new Vector3(0, -90, 0)));
            i.furniture.Add(("table", new Vector3(-5, 6.5f, 6), new Vector3(0, 0, 0)));
            i.furniture.Add(("o2_rack", new Vector3(10, 6.5f, 30), new Vector3(0, -90, 0)));
            d.colliders.Add(VehicleDesign.Box(-14, 2, -60, 14, 6, 62));
            d.colliders.Add(VehicleDesign.Box(-17, 7, -60, -13, 33, 62));
            d.colliders.Add(VehicleDesign.Box(13, 7, -60, 17, 33, 62));
            d.colliders.Add(VehicleDesign.Box(-14, 34, -60, 14, 37, 62));
            d.colliders.Add(VehicleDesign.Box(-12, 7, 62, 12, 33, 75));
            d.colliders.Add(VehicleDesign.Box(-10, 7, -75, 10, 30, -61));
            d.colliders.Add(VehicleDesign.Box(-5, C + R - 1, 5, 5, C + R + 18, 30));
            d.spinners.Add(("Prop", Propeller(7), new Vector3Int(0, C, -78)));
            d.Socket("engine", PartCategory.Engine, 0, 7, -40, "engine_marine_diesel", false, 3);
            return d;
        }
    }
}
