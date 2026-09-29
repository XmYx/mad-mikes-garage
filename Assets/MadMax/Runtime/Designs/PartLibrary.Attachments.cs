using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Vehicle attachments (roadmap 4): cargo-socket utilities (roof rack, cargo box, water tank, generator),
    /// lights, snorkel, steps and ladder, and working weapons (roof MG, harpoon, flamethrower, rear dropper, smoke
    /// dischargers). Functions are added at runtime by <c>PartFunctions</c> from the part id.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> Attachments()
        {
            yield return RoofRack();
            yield return CargoBox();
            yield return WaterTank();
            yield return OnboardGenerator();
            yield return LightBar();
            yield return Searchlight();
            yield return Snorkel();
            yield return SideSteps();
            yield return RoofLadder();
            yield return MachineGun();
            yield return HarpoonLauncher();
            yield return Flamethrower();
            yield return RearDropper();
            yield return SmokeLauncher();
        }

        // ---------------------------------------------------------------- cargo socket
        public static PartDesign RoofRack()
        {
            var g = new VoxelGrid();
            var rail = Pal.Ramp(Pal.Metal, 1, 1401);
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -7, 7 }) g.Box(x, 0, z, x, 4, z, rail);        // feet and corner posts
            foreach (int x in new[] { -10, 10 }) g.Tube(new Vector3(x, 1, -7), new Vector3(x, 1, 7), 0.4f, rail);
            foreach (int z in new[] { -7, 0, 7 }) g.Tube(new Vector3(-10, 1, z), new Vector3(10, 1, z), 0.4f, rail);
            foreach (int x in new[] { -10, 10 }) g.Tube(new Vector3(x, 4, -7), new Vector3(x, 4, 7), 0.4f, rail);         // side rails
            g.Box(-8, 2, -5, 2, 5, 5, Pal.Weathered(Pal.Sand, 0.15f, 1402, 2, 0));                                           // tarp bundle
            foreach (int z in new[] { -3, 2 }) g.Repaint(-8, 2, z, 2, 5, z, Pal.Ramp(Pal.Wood, 0, 1403));                    // ropes
            g.CylY(6, 0, 3.4f, 2, 3, Pal.Ramp(Pal.Tire, 1, 1404), 1.6f);                                                     // spare tyre
            return Make("cargo_roof_rack", PartCategory.Cargo, g, 40, 2);
        }

        public static PartDesign CargoBox()
        {
            var g = new VoxelGrid();
            g.Box(-10, 0, -6, 10, 7, 6, p => p.x % 4 == 0 ? Pal.Metal[0] : Pal.Pick(Pal.Metal, p, 1411, 2));               // ribbed steel
            g.Box(-10, 8, -6, 10, 8, 6, Pal.Weathered(Pal.Metal, 0.35f, 1412, 2, 0));                                        // lid
            g.Box(-1, 5, 7, 1, 7, 7, Pal.Ramp(Pal.Chrome, 2));                                                               // hasp
            g.Set(0, 5, 8, Pal.Solid(Pal.Ochre[3]));                                                                        // padlock
            g.Box(-7, 2, 7, -3, 3, 7, Pal.Solid(Pal.Ochre[2]));                                                             // stencil
            foreach (int x in new[] { -10, 10 }) g.Box(x, 3, -2, x, 3, 2, Pal.Ramp(Pal.Black, 1));                          // handles
            return Make("cargo_box", PartCategory.Cargo, g, 60, 2);
        }

        public static PartDesign WaterTank()
        {
            var g = new VoxelGrid();
            var frame = Pal.Ramp(Pal.Metal, 1, 1421);
            foreach (int x in new[] { -9, 9 }) { g.Box(x, 0, -5, x, 3, -5, frame); g.Box(x, 0, 5, x, 3, 5, frame); g.Box(x, 1, -4, x, 1, 4, frame); }
            g.CylX(5, 0, 4.6f, -10, 10, p => p.x % 5 == 0 ? Pal.Cream[1] : Pal.Pick(Pal.Cream, p, 1422, 2));              // poly tank, moulded ribs
            g.CylY(0, 0, 1.4f, 10, 10, Pal.Ramp(Pal.Navy, 2));                                                              // filler cap
            g.Box(-10, 2, 3, -10, 2, 4, Pal.Ramp(Pal.Chrome, 2));                                                           // tap
            g.Box(-3, 5, -5, 3, 6, -5, Pal.Solid(Pal.Navy[3]));                                                             // level window
            return Make("cargo_water_tank", PartCategory.Cargo, g, 120, 3);
        }

        public static PartDesign OnboardGenerator()
        {
            var g = new VoxelGrid();
            var frame = Pal.Ramp(Pal.Metal, 1, 1431);
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -5, 5 }) g.Box(x, 0, z, x, 9, z, frame);          // open frame
            foreach (int y in new[] { 0, 9 }) { g.Box(-8, y, -5, 8, y, -5, frame); g.Box(-8, y, 5, 8, y, 5, frame); }
            g.Box(-6, 1, -4, 1, 6, 4, Pal.Weathered(Cat, 0.3f, 1432, 2, 0));                                                // engine block
            g.CylX(3.5f, 0, 3f, 2, 6, Pal.Ramp(Pal.Metal, 2, 1433));                                                        // alternator
            g.Box(-5, 7, -3, 3, 8, 3, Pal.Weathered(Pal.Crimson, 0.2f, 1434, 2, 0));                                        // fuel tank
            g.Box(6, 3, 5, 7, 6, 5, Pal.Ramp(Pal.Black, 1)); g.Set(7, 5, 6, Pal.Solid(Pal.Amber));                          // panel and lamp
            g.Tube(new Vector3(-7, 4, -3), new Vector3(-7, 11, -3), 0.5f, Pal.Ramp(Pal.Rust, 2));                            // exhaust
            return Make("cargo_generator", PartCategory.Cargo, g, 110, 3);
        }

        // ---------------------------------------------------------------- lights
        public static PartDesign LightBar()
        {
            var g = new VoxelGrid();
            foreach (int x in new[] { -8, 8 }) g.Box(x, 0, 0, x, 2, 0, Pal.Ramp(Pal.Metal, 1, 1441));                      // brackets
            g.Box(-11, 3, -1, 11, 4, 0, Pal.Ramp(Pal.Black, 1, 1442));
            for (int x = -10; x <= 10; x += 4) g.Box(x, 3, 1, x + 2, 4, 1, p => p.x % 2 == 0 ? Pal.LightW : Pal.LightY);   // lamps
            return Make("lights_bar", PartCategory.Lights, g, 12, 1);
        }

        public static PartDesign Searchlight()
        {
            var g = new VoxelGrid();
            g.CylY(0, 0, 2.2f, 0, 1, Pal.Ramp(Pal.Metal, 1, 1451));
            g.Use("lamp");
            g.Box(-1, 2, -1, 1, 3, 1, Pal.Ramp(Pal.Metal, 2, 1452));                                                        // yoke
            g.CylZ(0, 5, 2.6f, -3, 2, Pal.Weathered(Pal.Olive, 0.3f, 1453, 2, 0));                                         // drum
            g.CylZ(0, 5, 2.1f, 3, 3, Pal.Solid(Pal.LightW));                                                                // lens
            g.Box(-1, 8, -2, 1, 8, 0, Pal.Ramp(Pal.Black, 1));                                                              // handle
            g.Use("body");
            return Make("lights_search", PartCategory.Lights, g, 25, 1).Segment("lamp", new Vector3Int(0, 2, 0));
        }

        // ---------------------------------------------------------------- snorkel, steps
        /// <summary>Raised air intake (right side): pipe up to a forward-facing scoop 1.4 m above the mount.</summary>
        public static PartDesign Snorkel()
        {
            var g = new VoxelGrid();
            var pipe = Pal.Ramp(Pal.Black, 1, 1461);
            g.Box(0, 0, -2, 1, 1, 2, Pal.Ramp(Pal.Metal, 1, 1462));                                                          // fender bracket
            g.Box(1, 1, 0, 2, 16, 1, pipe);
            g.Box(0, 16, -1, 3, 18, 3, pipe);                                                                                // head
            g.Box(1, 17, 4, 2, 17, 4, Pal.Solid(Pal.Void));                                                                 // intake mouth
            foreach (int y in new[] { 5, 11 }) g.Box(0, y, 0, 0, y, 1, Pal.Ramp(Pal.Chrome, 1));                             // pillar clamps
            return Make("snorkel", PartCategory.Snorkel, g, 8, 1);
        }

        /// <summary>Running board under the door (right side): step up to reach the roof.</summary>
        public static PartDesign SideSteps()
        {
            var g = new VoxelGrid();
            g.Box(0, 0, -9, 2, 0, 9, p => (p.x + p.z) % 2 == 0 ? Pal.Chrome[2] : Pal.Chrome[1]);                            // diamond plate
            foreach (int z in new[] { -6, 6 }) g.Box(0, 1, z, 0, 2, z, Pal.Ramp(Pal.Metal, 1, 1471));                        // brackets
            g.Box(3, 0, -9, 3, 0, 9, Pal.Ramp(Pal.Black, 1));
            return Make("steps_side", PartCategory.Steps, g, 15, 1);
        }

        /// <summary>Side ladder (right side) up to the roof edge.</summary>
        public static PartDesign RoofLadder()
        {
            var g = new VoxelGrid();
            var rail = Pal.Ramp(Pal.Metal, 2, 1481);
            foreach (int z in new[] { -3, 3 }) { g.Box(1, 0, z, 1, 15, z, rail); g.Box(0, 15, z, 0, 17, z, rail); }
            for (int y = 1; y <= 14; y += 3) g.Box(1, y, -2, 1, y, 2, Pal.Ramp(Pal.Chrome, 1));                              // rungs
            foreach (int y in new[] { 2, 12 }) g.Box(0, y, -3, 0, y, 3, Pal.Ramp(Pal.Metal, 0));                            // stand-offs
            return Make("steps_ladder", PartCategory.Steps, g, 12, 1);
        }

        // ---------------------------------------------------------------- weapons
        /// <summary>Pintle machine gun: "mount" yaws, "gun" pitches (the driver aims with the mouse).</summary>
        public static PartDesign MachineGun()
        {
            var g = new VoxelGrid();
            g.CylY(0, 0, 3.6f, 0, 1, Pal.Ramp(Pal.Metal, 0, 1491));                                                         // ring
            g.Use("mount");
            g.Box(-1, 2, -1, 1, 5, 1, Pal.Ramp(Pal.Metal, 1, 1492));                                                         // pintle
            g.Box(-5, 3, 4, 5, 9, 4, p => p.y == 7 && Mathf.Abs(p.x) <= 1 ? Pal.Void : Pal.Pick(Pal.Olive, p, 1493, 2));    // gun shield with a slit
            g.Use("gun");
            g.Box(-1, 5, -4, 1, 7, 2, Pal.Ramp(Pal.Black, 2, 1494));                                                         // receiver
            g.CylZ(0, 6, 0.9f, 3, 13, p => p.z % 2 == 0 ? Pal.Metal[0] : Pal.Metal[2]);                                     // barrel jacket
            g.Box(-1, 5, 14, 1, 7, 14, Pal.Solid(Pal.Black[0])); g.Set(0, 6, 15, Pal.Solid(Pal.Void));                       // muzzle
            g.Box(2, 4, -2, 4, 6, 1, Pal.Weathered(Pal.Olive, 0.2f, 1495, 2, 0));                                           // ammo can
            g.Box(1, 6, -1, 1, 6, 1, Pal.Ramp(Pal.Bronze, 3));                                                              // belt
            g.Box(-1, 5, -6, 1, 5, -5, Pal.Ramp(Pal.Chrome, 1));                                                            // spade grips
            g.Use("body");
            return Make("weapon_mg", PartCategory.Weapon, g, 90, 2).Segment("mount", new Vector3Int(0, 2, 0)).Segment("gun", new Vector3Int(0, 6, 0), "mount");
        }

        /// <summary>Harpoon launcher: fires a barbed bolt on a cable; reel in to drag what it hit.</summary>
        public static PartDesign HarpoonLauncher()
        {
            var g = new VoxelGrid();
            g.CylY(0, 0, 3.6f, 0, 1, Pal.Ramp(Pal.Metal, 0, 1501));
            g.Use("mount");
            g.Box(-3, 2, -2, 3, 4, 2, Pal.Weathered(Pal.Rust, 0.3f, 1502, 2, 0));                                         // cradle
            g.Use("launcher");
            g.CylZ(0, 6, 1.4f, -5, 10, Pal.Weathered(Pal.Metal, 0.35f, 1503, 2, 0));                                       // tube
            g.Box(0, 6, 11, 0, 6, 13, Pal.Ramp(Pal.Chrome, 2)); g.Set(-1, 6, 12, Pal.Solid(Pal.Chrome[3])); g.Set(1, 6, 12, Pal.Solid(Pal.Chrome[3]));   // barbed head
            g.CylX(6, -6, 1.8f, -2, 2, Pal.Ramp(Pal.Black, 1));                                                             // cable drum
            g.Box(-2, 8, -6, 2, 8, -6, Pal.Ramp(Pal.Bronze, 2));
            g.Use("body");
            return Make("weapon_harpoon", PartCategory.Weapon, g, 110, 2).Segment("mount", new Vector3Int(0, 2, 0)).Segment("launcher", new Vector3Int(0, 6, 0), "mount");
        }

        /// <summary>Flamethrower: burns the vehicle's own fuel through a pilot-lit nozzle.</summary>
        public static PartDesign Flamethrower()
        {
            var g = new VoxelGrid();
            g.CylY(0, 0, 3.6f, 0, 1, Pal.Ramp(Pal.Metal, 0, 1511));
            foreach (int x in new[] { -3, 3 }) g.CylY(x, -3, 1.4f, 2, 7, Pal.Weathered(Pal.Crimson, 0.25f, 1512, 2, 0));  // tanks
            g.Use("mount");
            g.Box(-1, 2, -1, 1, 4, 1, Pal.Ramp(Pal.Metal, 1, 1513));
            g.Use("nozzle");
            g.CylZ(0, 5, 0.8f, -2, 11, Pal.Ramp(Pal.Metal, 2, 1514));                                                       // pipe
            g.CylZ(0, 5, 1.4f, 12, 13, Pal.Ramp(Pal.Black, 1));                                                             // nozzle
            g.Set(0, 7, 12, Pal.Solid(Pal.Amber));                                                                          // pilot light
            g.Tube(new Vector3(0, 4, -2), new Vector3(3, 3, -3), 0.4f, Pal.Ramp(Pal.Black, 0));                              // hose
            g.Use("body");
            return Make("weapon_flamer", PartCategory.Weapon, g, 100, 2).Segment("mount", new Vector3Int(0, 2, 0)).Segment("nozzle", new Vector3Int(0, 5, 0), "mount");
        }

        /// <summary>Rear bumper with a hopper: drops caltrops or an oil slick behind the vehicle.</summary>
        public static PartDesign RearDropper()
        {
            var g = new VoxelGrid();
            g.Box(-12, 0, -1, 12, 3, 0, p => (p.x + p.y) % 4 < 2 ? Pal.Ochre[2] : Pal.Black[1]);                            // hazard-striped bar
            g.Box(-5, 4, -3, 5, 9, 1, Pal.Weathered(Pal.Metal, 0.45f, 1521, 2, 0));                                          // hopper
            g.Box(-3, 1, -3, 3, 3, -2, Pal.Ramp(Pal.Metal, 0, 1522));                                                        // chute
            g.Box(-2, 1, -4, 2, 1, -4, Pal.Solid(Pal.Void));
            return Make("rear_dropper", PartCategory.RearBumper, g, 60, 2);
        }

        /// <summary>Smoke dischargers on a side plate (right side): three tubes angled up and out.</summary>
        public static PartDesign SmokeLauncher()
        {
            var g = new VoxelGrid();
            g.Box(0, -1, -8, 1, 4, 8, Pal.Weathered(Pal.Metal, 0.4f, 1531, 2, 20));
            foreach (int z in new[] { -4, 0, 4 })
            {
                g.Tube(new Vector3(2, 2, z), new Vector3(5, 5, z), 0.8f, Pal.Ramp(Pal.Olive, 1, 1532 + z));
                g.Set(5, 6, z, Pal.Solid(Pal.Black[0]));
            }
            return Make("armor_smoke", PartCategory.Armor, g, 35, 1);
        }
    }
}
