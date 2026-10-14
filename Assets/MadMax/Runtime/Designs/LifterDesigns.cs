using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Heavy lifters: the MagnetCrane (8x8 cab-over with a lattice-boom lifting magnet on a tread-plate bed,
    /// timber magnet mats at the tail, outriggers fore and aft) and the CarCrusher (6x6 cab-over with a knuckle-boom
    /// grapple and a hydraulic crusher bay over the rear tandem: walls, bulkhead, a side-hinged press lid). Both cranes
    /// are parts on the "cargo" socket; the outriggers and the lid are movable pieces their components drive.</summary>
    public static partial class VehicleDesigns
    {
        const int CabZ0 = 42, CabZ1 = 64, CabFloor = 22, CabTop = 46;

        /// <summary>Cab-over heavy cab (front face at z <see cref="CabZ1"/>): glasshouse with split windscreen, door seams,
        /// grille, lamps, a striped push bumper, mirrors on arms, roof beacon bar, air horns, steps and an exhaust stack.</summary>
        static void HeavyCab(VoxelGrid g, VoxMat paint, int seed)
        {
            var glass = Pal.Ramp(Pal.Glass, 1, seed);
            var steel = Pal.Ramp(Pal.Metal, 1, seed + 1);
            var chrome = Pal.Weathered(Pal.Chrome, 0.25f, seed + 2, 2, 0);
            var dark = Pal.Ramp(Pal.Black, 1, seed + 3);
            for (int x = -15; x <= 15; x++)
            for (int y = 12; y <= CabTop; y++)
            for (int z = CabZ0; z <= CabZ1; z++)
            {
                int ax = Abs(x);
                bool wall = ax == 15 || z == CabZ0 || z == CabZ1 || y == CabTop || (y == CabFloor && z > CabZ0 && z < CabZ1);
                if (!wall) continue;
                if (y < CabFloor && z < CabZ1 && ax < 15) continue;                                             // open under the floor
                if (y == CabTop && ax == 15 && (z == CabZ0 || z == CabZ1)) continue;                            // rounded roof corners
                VoxMat m = paint;
                if (z == CabZ1 && y >= 31 && y <= 43 && ax <= 13 && x != 0) m = glass;                          // split windscreen
                else if (ax == 15 && y >= 31 && y <= 42 && z >= 50 && z <= 62 && z != 56) m = glass;            // door + quarter windows
                else if (z == CabZ0 && y >= 34 && y <= 40 && ax <= 5) m = glass;                                 // rear window
                else if (ax == 15 && (z == 48 || z == 63) && y >= 15) m = Pal.Ramp(Pal.Metal, 0, seed + 4);      // door seams
                g.Set(x, y, z, m);
            }
            // a thin cream coachline under the windows
            g.Repaint(-15, 29, CabZ0, 15, 29, CabZ1, Pal.Ramp(Pal.Cream, 1, seed + 5));
            g.Repaint(-14, 29, CabZ1, 14, 29, CabZ1, Pal.Ramp(Pal.Cream, 1, seed + 5));
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 16, 31, 58, s * 16, 31, 60, chrome);                                                  // door handle
                g.Tube(new Vector3(s * 15, 39, 62), new Vector3(s * 19, 39, 65), 0.5f, steel);                   // mirror arm
                g.Box(s * 19, 33, 64, s * 20, 39, 65, dark);
                g.Box(s * 19, 34, 66, s * 19, 38, 66, Pal.Ramp(Pal.Glass, 3, seed + 6));
                g.Box(s * 15, 16, 50, s * 17, 16, 58, steel);                                                   // steps
                g.Box(s * 15, 11, 51, s * 17, 11, 57, steel);
                g.Box(s * 14, 21, 65, s * 12, 23, 65, Pal.Solid(Pal.LightW));                                    // headlamps
                g.Set(s * 14, 24, 65, Pal.Solid(Pal.Amber));
                g.Set(s * 9, 15, 67, Pal.Solid(Pal.LightY));                                                    // fog lamps in the bumper
                g.Set(s * 15, 46, 64, Pal.Solid(Pal.Amber));                                                    // roof marker lamps
                g.Tube(new Vector3(s * 3, 48, 52), new Vector3(s * 3, 48, 58), 0.6f, chrome);                    // air horns
            }
            g.Box(-10, 15, 65, 10, 27, 65, Pal.Stripe(Pal.Solid(Pal.Void), chrome, 1, 2));                       // grille
            g.Box(-11, 28, 65, 11, 28, 65, chrome);
            g.Box(-3, 17, 66, 3, 18, 66, Pal.Ramp(Pal.Cream, 2, seed + 7));                                       // plate
            g.Box(-16, 12, 66, 16, 15, 68, Pal.Stripe(Pal.Ramp(Pal.Black, 1, seed + 8), Pal.Solid(Pal.Amber), 0, 4, 2));   // push bumper
            foreach (int s in new[] { -1, 1 }) g.Box(s * 6, 12, 69, s * 7, 13, 69, Pal.Solid(Pal.Metal[3]));      // tow eyes
            g.Box(-15, 44, 65, 15, 45, 66, paint);                                                              // sun visor
            g.Box(-7, 47, 58, 7, 47, 61, steel);                                                                // beacon bar
            for (int x = -6; x <= 6; x += 3) g.Set(x, 48, 59, Pal.Solid(Pal.Amber));
            g.Tube(new Vector3(13, 20, 41), new Vector3(13, 50, 41), 1.1f, chrome);                              // exhaust stack
            g.Box(12, 51, 40, 14, 51, 42, Pal.Ramp(Pal.Rust, 2, seed + 9));
            g.Tube(new Vector3(-13, 22, 41), new Vector3(-13, 44, 41), 1.4f, Pal.Ramp(Pal.Black, 2, seed + 10));  // air intake
            g.Box(-14, 45, 40, -12, 46, 42, dark);
            Interior(g, CabFloor, 58, 52, 4);
            g.Box(-1, CabFloor + 1, 44, 1, CabFloor + 3, 56, dark);                                              // console over the engine
        }

        /// <summary>Chassis rails, cross members, bed, side skirts, tanks, toolboxes and the tail (lamps, under-run bar).</summary>
        static void HeavyChassis(VoxelGrid g, int zRear, int bedFront, VoxMat paint, int seed, bool bed)
        {
            var steel = Pal.Ramp(Pal.Metal, 1, seed);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 6, 8, zRear, s * 8, 12, 64, steel);
            for (int z = zRear + 4; z <= 60; z += 10) g.Box(-6, 9, z, 6, 11, z, Pal.Ramp(Pal.Metal, 0, seed + 1));
            if (bed)
            {
                VoxMat tread = p => ((p.x + p.z * 2) % 5 + 5) % 5 == 0 ? Pal.Metal[3] : (p.z % 2 == 0 ? Pal.Metal[1] : Pal.Metal[2]);
                g.Box(-15, 15, zRear, 15, 16, bedFront, tread);                                                // tread-plate bed
                foreach (int s in new[] { -1, 1 })
                {
                    g.Box(s * 15, 13, zRear, s * 15, 16, bedFront, paint);                                      // side skirt
                    for (int z = zRear + 3; z <= bedFront; z += 8) g.Set(s * 16, 15, z, Pal.Solid(Pal.Metal[3]));   // rope hooks
                }
            }
            foreach (int s in new[] { -1, 1 })
            {
                g.CylZ(s * 12, 11, 2.6f, 4, 22, Pal.Weathered(Pal.Chrome, 0.3f, seed + 2, 2, 0));               // fuel tanks
                g.Box(s * 12, 13, 8, s * 12, 14, 8, Pal.Ramp(Pal.Metal, 2, seed + 3));
                g.Box(s * 11, 8, -22, s * 15, 13, -12, Pal.Ramp(Pal.Black, 1, seed + 4));                        // toolboxes
                g.Box(s * 15, 11, -20, s * 15, 11, -14, Pal.Solid(Pal.Chrome[2]));
                g.Box(s * 14, 13, zRear - 1, s * 12, 15, zRear - 1, Pal.Solid(Pal.TailR));                        // tail lamps
                g.Set(s * 11, 14, zRear - 1, Pal.Solid(Pal.Amber));
                g.Box(s * 11, 3, zRear + 7, s * 16, 12, zRear + 7, Pal.Ramp(Pal.Black, 0, seed + 5));            // mud flaps
            }
            g.Box(-13, 5, zRear - 1, 13, 7, zRear - 1, Pal.Stripe(Pal.Ramp(Pal.Black, 1, seed + 6), Pal.Solid(Pal.Amber), 0, 4, 2));   // under-run bar
            foreach (int s in new[] { -1, 1 }) g.Box(s * 10, 7, zRear - 1, s * 10, 12, zRear, steel);
        }

        /// <summary>An outrigger housing across the frame at <paramref name="z"/> and its two beams + legs as movable pieces.</summary>
        static void Outrigger(VehicleDesign d, VoxelGrid g, int z, string tag, int seed)
        {
            g.Box(-14, 9, z - 2, 14, 13, z + 2, Pal.Ramp(Pal.Metal, 0, seed));
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 14, 9, z - 2, s * 14, 13, z + 2, Pal.Stripe(Pal.Ramp(Pal.Black, 1, seed + 1), Pal.Solid(Pal.Amber), 1, 4, 2));
                var beam = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
                beam.Box(s * -12, 0, -1, 0, 3, 1, Pal.Weathered(Cat, 0.3f, seed + 2, 2, 0));
                beam.Box(0, 0, -1, 0, 3, 1, Pal.Stripe(Pal.Ramp(Pal.Black, 1, seed + 3), Pal.Solid(Pal.Amber), 1, 2));
                beam.Bevel();
                var leg = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
                leg.Box(s * -1, -1, -2, s * 2, 7, 2, Pal.Weathered(Cat, 0.3f, seed + 4, 2, 0));                 // sleeve
                leg.Box(s * 0, -6, -1, s * 1, -2, 1, Pal.Ramp(Pal.Chrome, 2, seed + 5));                        // ram rod
                leg.Box(s * -2, -8, -3, s * 3, -7, 3, Pal.Ramp(Pal.Black, 1, seed + 6));                         // pad
                leg.Box(s * -1, 6, -2, s * 2, 7, 2, Pal.Stripe(Pal.Ramp(Pal.Black, 1, seed + 7), Pal.Solid(Pal.Amber), 2, 2));
                leg.Bevel();
                string side = tag + (s > 0 ? "R" : "L");
                d.movable.Add(("Outrigger_" + side, beam, new Vector3Int(s * 15, 9, z), Vector3.zero));
                d.movable.Add(("OutriggerLeg_" + side, leg, new Vector3Int(s * 15, 10, z), Vector3.zero));
            }
        }

        // ================================================================== MAGNET CRANE (8x8)
        public static VehicleDesign MagnetCrane()
        {
            var d = new VehicleDesign
            {
                name = "MagnetCrane", mass = 14000, drive = VehicleDriver.Drive.All, awdSelectable = true, diffLock = true,
                travel = 0.24f, frequency = 1.5f, finalDrive = 6.6f, brakeForce = 160000f, maxSteer = 26f,
                gears = new[] { 6.4f, 4.2f, 2.8f, 1.9f, 1.35f, 1.0f }, eye = new Vector3Int(4, CabFloor + 11, 51),
                fuelL = 380f, oilL = 32f, coolantL = 48f
            };
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
            var paint = Pal.Weathered(Pal.Navy, 0.3f, 7401, 2, 10);
            HeavyChassis(g, -70, 41, paint, 7410, true);
            HeavyCab(g, paint, 7420);
            // timber magnet mats and a scrap tyre buffer at the tail, the magnet rests there on the road
            for (int z = -68; z <= -56; z += 3) g.Box(-9, 17, z, 9, 17, z + 1, Pal.Ramp(Pal.Wood, 2, 7430));
            g.CylY(0, -62, 5f, 18, 18, Pal.Ramp(Pal.Tire, 1, 7431), 3f);
            // chain basket and lashing chains by the turret
            g.Box(9, 17, 8, 14, 20, 16, Pal.Ramp(Pal.Metal, 0, 7432));
            for (int x = 10; x <= 13; x++) g.Set(x, 21, 10 + (x % 3), Pal.Solid(x % 2 == 0 ? Pal.Metal[3] : Pal.Metal[1]));
            g.Box(-14, 17, 10, -9, 22, 16, Pal.Weathered(Pal.Olive, 0.3f, 7433, 2, 0));                          // site hut box
            g.Set(-11, 23, 13, Pal.Solid(Pal.Amber));
            // warning plates on the skirts
            foreach (int s in new[] { -1, 1 }) g.Box(s * 16, 13, -40, s * 16, 15, -30, Pal.Stripe(Pal.Ramp(Pal.Crimson, 2, 7434), Pal.Ramp(Pal.Cream, 2, 7435), 2, 3));
            Outrigger(d, g, 27, "F", 7440);
            Outrigger(d, g, -66, "B", 7450);
            Finish(d, g);
            d.colliders.Add(VehicleDesign.Box(-15, 8, -71, 15, 16, 41));                                          // frame + bed
            d.colliders.Add(VehicleDesign.Box(-15, 12, CabZ0, 15, CabTop, CabZ1));                                // cab
            d.colliders.Add(VehicleDesign.Box(-16, 12, CabZ1 + 1, 16, 15, CabZ1 + 4));                            // bumper
            d.Socket("wheel_front", PartCategory.Wheel, 11, 7, 54, "wheel_truck", true);
            d.Socket("wheel_front2", PartCategory.Wheel, 11, 7, 38, "wheel_truck", true);
            d.Socket("wheel_mid", PartCategory.Wheel, 11, 7, -42, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 11, 7, -56, "wheel_truck", true);
            d.Socket("engine", PartCategory.Engine, 0, 9, 40, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 12, 61, "radiator_truck");
            d.Socket("cargo", PartCategory.Cargo, 0, 17, 26, "magnet_crane");
            return d;
        }

        // ================================================================== CAR CRUSHER (6x6)
        public static VehicleDesign CarCrusher()
        {
            var d = new VehicleDesign
            {
                name = "CarCrusher", mass = 12500, drive = VehicleDriver.Drive.Rear, awdSelectable = true, diffLock = true,
                travel = 0.24f, frequency = 1.5f, finalDrive = 6.4f, brakeForce = 140000f, maxSteer = 30f,
                gears = new[] { 6.2f, 4.1f, 2.8f, 1.9f, 1.35f, 1.0f }, eye = new Vector3Int(4, CabFloor + 11, 51),
                fuelL = 340f, oilL = 30f, coolantL = 44f
            };
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
            var paint = Pal.Weathered(Pal.RigGreen, 0.32f, 7501, 2, 10);
            var bay = Pal.Weathered(Pal.Metal, 0.5f, 7502, 2, 30);
            var ribs = Pal.Ramp(Pal.Metal, 2, 7503);
            HeavyChassis(g, -78, 41, paint, 7510, false);
            HeavyCab(g, paint, 7520);
            // front bed under the turret
            VoxMat tread = p => ((p.x + p.z * 2) % 5 + 5) % 5 == 0 ? Pal.Metal[3] : (p.z % 2 == 0 ? Pal.Metal[1] : Pal.Metal[2]);
            g.Box(-15, 15, -14, 15, 16, 41, tread);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 15, 13, -14, s * 15, 16, 41, paint);
            // hydraulic power pack beside the turret (gauges, filters, tank)
            g.Box(-15, 17, -10, -9, 25, 8, paint);
            g.Box(-15, 22, -6, -15, 24, 4, Pal.Stripe(Pal.Ramp(Pal.Black, 0, 7504), Pal.Ramp(Pal.Metal, 2, 7505), 2, 2));   // vents
            g.Set(-16, 21, -2, Pal.Solid(Pal.Cream[3])); g.Set(-16, 21, 1, Pal.Solid(Pal.Cream[3]));                  // gauges
            g.CylY(-12, 6, 1.2f, 26, 28, Pal.Ramp(Pal.Chrome, 2, 7506));                                           // filter
            g.Box(10, 17, -10, 15, 21, 4, Pal.Ramp(Pal.Black, 1, 7507));                                           // battery box
            // ---- crusher bay over the rear tandem: floor at y 17, walls to y 30, bulkhead, tail gate
            g.Box(-16, 13, -77, 16, 16, -15, Pal.Ramp(Pal.Metal, 0, 7508));                                         // floor + bearers
            g.Box(-14, 16, -76, 14, 16, -19, p => (p.z % 4 == 0 ? Pal.Rust[1] : Pal.Metal[1]));                    // scarred floor plate
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 15, 17, -77, s * 16, 30, -15, bay);                                                        // side walls
                for (int z = -74; z <= -18; z += 7) g.Box(s * 17, 17, z, s * 17, 30, z, ribs);                       // outside ribs
                g.Box(s * 17, 30, -77, s * 17, 30, -15, ribs);
                g.Box(s * 16, 31, -77, s * 16, 31, -15, Pal.Stripe(Pal.Ramp(Pal.Black, 1, 7509), Pal.Solid(Pal.Amber), 2, 4, 2));   // hazard capping
            }
            g.Box(-16, 17, -18, 16, 35, -15, bay);                                                                   // bulkhead
            g.Box(-16, 33, -18, 16, 35, -18, Pal.Stripe(Pal.Ramp(Pal.Black, 1, 7511), Pal.Solid(Pal.Amber), 0, 4, 2));
            g.Box(-16, 17, -77, 16, 29, -76, bay);                                                                   // tail gate
            for (int x = -12; x <= 12; x += 6) g.Box(x, 17, -78, x, 29, -78, ribs);
            g.Box(-12, 25, -78, 12, 25, -78, ribs);
            // hinge rail on the left wall, lid rests, drain spout with its stain, warning signs, control station
            g.Box(-17, 30, -76, -16, 31, -18, Pal.Ramp(Pal.Metal, 2, 7512));
            for (int z = -72; z <= -22; z += 10) g.CylZ(-16, 31, 1.3f, z, z + 2, Pal.Ramp(Pal.Chrome, 2, 7513));
            g.Box(14, 31, -74, 16, 32, -70, Pal.Ramp(Pal.Metal, 1, 7514)); g.Box(14, 31, -24, 16, 32, -20, Pal.Ramp(Pal.Metal, 1, 7514));
            g.Tube(new Vector3(-10, 15, -77), new Vector3(-10, 15, -81), 0.7f, Pal.Ramp(Pal.Chrome, 1, 7515));
            g.Box(-11, 14, -82, -9, 14, -81, Pal.Solid(Pal.Black[0]));
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 18, 22, -50, s * 18, 27, -42, Pal.Ramp(Pal.Crimson, 2, 7516));                              // DANGER plates
                g.Box(s * 18, 24, -49, s * 18, 25, -43, Pal.Ramp(Pal.Cream, 3, 7517));
            }
            g.Box(16, 18, -80, 18, 26, -79, Pal.Ramp(Pal.Black, 1, 7518));                                           // rear control station
            g.Box(17, 24, -81, 17, 26, -81, Pal.Solid(Pal.Metal[3])); g.Box(16, 24, -81, 16, 25, -81, Pal.Solid(Pal.TailR));
            g.Set(18, 27, -80, Pal.Solid(Pal.Amber));
            Outrigger(d, g, 31, "F", 7540);
            Outrigger(d, g, -75, "B", 7550);
            Finish(d, g);

            // press lid (authored closed, hinged on the left wall top; starts swung open)
            var lid = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
            lid.Box(0, 0, -29, 32, 2, 29, Pal.Weathered(Pal.Metal, 0.4f, 7530, 2, 0));
            for (int x = 3; x <= 30; x += 6) lid.Box(x, 3, -28, x, 4, 28, Pal.Ramp(Pal.Metal, 2, 7531));             // stiffeners
            for (int z = -24; z <= 24; z += 12) lid.Box(1, 3, z, 31, 3, z, Pal.Ramp(Pal.Metal, 1, 7532));
            lid.Box(30, 0, -29, 32, 3, 29, Pal.Stripe(Pal.Ramp(Pal.Black, 1, 7533), Pal.Solid(Pal.Amber), 2, 4, 2));   // free edge
            lid.Box(-3, 1, -2, -1, 4, 2, Pal.Ramp(Pal.Metal, 2, 7534));                                                // ram lever
            lid.CylZ(0, 1, 1.4f, -29, 29, Pal.Ramp(Pal.Chrome, 1, 7535));                                              // hinge tube
            for (int z = -26; z <= 26; z += 4) lid.Set(31, 0, z, Pal.Solid(Pal.Chrome[3]));                            // wear teeth
            lid.Bevel();
            d.movable.Add(("CrusherLid", lid, new Vector3Int(-16, 31, -47), new Vector3(0f, 0f, GrappleCrusher.LidOpen)));

            d.colliders.Add(VehicleDesign.Box(-15, 8, -14, 15, 16, 41));                                           // frame + front bed
            d.colliders.Add(VehicleDesign.Box(-15, 12, CabZ0, 15, CabTop, CabZ1));
            d.colliders.Add(VehicleDesign.Box(-16, 12, CabZ1 + 1, 16, 15, CabZ1 + 4));
            d.colliders.Add(VehicleDesign.Box(-16, 8, -77, 16, 16, -15));                                          // bay floor
            d.colliders.Add(VehicleDesign.Box(-17, 17, -77, -15, 31, -15));                                        // walls
            d.colliders.Add(VehicleDesign.Box(15, 17, -77, 17, 31, -15));
            d.colliders.Add(VehicleDesign.Box(-16, 17, -18, 16, 35, -15));
            d.colliders.Add(VehicleDesign.Box(-16, 17, -78, 16, 29, -76));
            d.Socket("wheel_front", PartCategory.Wheel, 11, 7, 52, "wheel_truck", true);
            d.Socket("wheel_mid", PartCategory.Wheel, 11, 7, -44, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 11, 7, -60, "wheel_truck", true);
            d.Socket("engine", PartCategory.Engine, 0, 9, 40, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 12, 61, "radiator_truck");
            d.Socket("cargo", PartCategory.Cargo, 0, 17, 26, "grapple_crane");
            return d;
        }
    }
}
