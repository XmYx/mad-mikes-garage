using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Construction machines: excavator, backhoe loader, bulldozer, dump truck, paver, roller.
    /// Their working tools are parts (category Tool) on a "tool" socket; <see cref="Machine"/> drives them.</summary>
    public static partial class VehicleDesigns
    {
        static readonly Color32[] Cat = { Pal.Hex("6a5010"), Pal.Hex("9a7418"), Pal.Hex("c89a22"), Pal.Hex("e4bc34") };
        static VoxMat Yellow(int seed) => Pal.Weathered(Cat, 0.28f, seed, 2, 6);

        /// <summary>Operator cab: one-voxel shell, glass all round above the belt line, seats, controls and operator.</summary>
        static void Cab(VoxelGrid g, int hx, int z0, int z1, int floor, int top, VoxMat paint, int seed)
        {
            var glass = Pal.Ramp(Pal.Glass, 1, seed);
            for (int x = -hx; x <= hx; x++)
            for (int z = z0; z <= z1; z++)
            for (int y = floor; y <= top; y++)
            {
                bool wall = Abs(x) == hx || z == z0 || z == z1;
                if (!wall && y != floor && y != top) continue;
                bool post = Abs(x) == hx && (z == z0 || z == z1);
                bool window = wall && !post && y >= floor + 6 && y <= top - 2;
                g.Set(x, y, z, window ? glass : paint);
            }
            g.Box(-hx - 1, top + 1, z0 - 1, hx + 1, top + 1, z1 + 1, Pal.Ramp(Pal.Metal, 1, seed + 1));   // roof overhang
            Interior(g, floor, z1 - 4, z0 + 6, 4);
        }

        /// <summary>Crawler undercarriage (right side at +x..+x+w): the track frame between the road wheels and a
        /// fender over the belt. The belt, road wheels and sprocket are live parts (<see cref="CrawlerTracks"/>).</summary>
        static void Track(VoxelGrid g, int x0, int w, int z0, int z1, int h)
        {
            foreach (int s in new[] { -1, 1 })
            {
                // frame rail on the inner side of the belt, at axle height, with the roller brackets
                for (int z = z0 + 2; z <= z1 - 2; z++)
                    for (int y = 3; y <= 5; y++)
                        g.Set(s * (x0 - 1), y, z, Pal.Ramp(Pal.Metal, y == 5 ? 2 : 1, 1201));
                for (int z = z0 + 4; z <= z1 - 4; z += 8) g.Box(s * (x0 - 2), 2, z, s * (x0 - 1), 6, z, Pal.Solid(Pal.Metal[0]));
                // fender over the belt, clear of it
                g.Box(s * x0, h + 1, z0 - 1, s * (x0 + w + 1), h + 1, z1 + 1, Pal.Ramp(Pal.Metal, 1, 1202));
                g.Box(s * (x0 + w + 1), h, z0 - 1, s * (x0 + w + 1), h, z1 + 1, Pal.Solid(Pal.Metal[2]));
            }
        }

        static void Finish(VehicleDesign d, VoxelGrid g)
        {
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
        }

        static void Lamps(VoxelGrid g, int x, int y, int zFront, int zRear)
        {
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * x, y, zFront, s * (x - 1), y + 1, zFront, Pal.Solid(Pal.LightW));
                g.Box(s * x, y, zRear, s * (x - 1), y, zRear, Pal.Solid(Pal.TailR));
            }
        }

        // ================================================================== EXCAVATOR (crawler, 360° house, long arm)
        public static VehicleDesign Excavator()
        {
            var d = new VehicleDesign { name = "Excavator", machine = "Excavator", crawler = true, mass = 9000, drive = VehicleDriver.Drive.All, travel = 0.12f, frequency = 2.4f, finalDrive = 14f, brakeForce = 90000f, maxSteer = 34f, gears = new[] { 3.2f, 1.8f }, eye = new Vector3Int(4, 21, 11), fuelL = 180f, oilL = 20f, coolantL = 30f };
            var g = new VoxelGrid();
            var paint = Yellow(1201);
            Track(g, 9, 5, -24, 24, 9);
            g.Box(-8, 3, -18, 8, 7, 18, Pal.Ramp(Pal.Metal, 0, 1202));                       // undercarriage
            g.CylY(0, 0, 9f, 8, 9, Pal.Ramp(Pal.Metal, 1, 1203));                             // slew ring
            for (int z = -26; z <= 4; z++)                                                     // house
            for (int x = -14; x <= 14; x++)
            for (int y = 10; y <= 20; y++)
            {
                bool shell = Abs(x) == 14 || z == -26 || z == 4 || y == 10 || y == 20;
                if (!shell) continue;
                g.Set(x, y, z, z <= -20 ? Pal.Ramp(Pal.Metal, 1, 1204) : (y == 15 && Abs(x) == 14 ? Pal.Solid(Pal.Black[1]) : paint));
            }
            for (int z = -16; z <= -6; z += 2) g.Box(-12, 20, z, 12, 20, z, Pal.Solid(Pal.Void));     // engine grille
            g.CylY(-8, -12, 1f, 21, 26, Pal.Ramp(Pal.Black, 1));                              // exhaust
            Cab(g, 9, 5, 22, 10, 27, paint, 1205);
            Lamps(g, 8, 24, 23, -27);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 10, 4, 19, "wheel_track", true);      // idler
            d.Socket("wheel_rear", PartCategory.Wheel, 10, 4, -19, "wheel_track", true);      // sprocket
            d.Socket("engine", PartCategory.Engine, 0, 12, -12, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 12, -22, "radiator_truck");
            d.Socket("tool", PartCategory.Tool, 11, 17, 18, "tool_excavator_arm");
            return d;
        }

        // ================================================================== BACKHOE LOADER (wheeled, front bucket)
        public static VehicleDesign Backhoe()
        {
            var d = new VehicleDesign { name = "Backhoe", machine = "Backhoe", mass = 5200, drive = VehicleDriver.Drive.All, travel = 0.22f, frequency = 1.9f, finalDrive = 9f, brakeForce = 50000f, maxSteer = 36f, gears = new[] { 3.5f, 2.2f, 1.4f }, eye = new Vector3Int(4, 23, -9), awdSelectable = true, fuelL = 120f, oilL = 12f, coolantL = 20f };
            var g = new VoxelGrid();
            var paint = Yellow(1210);
            for (int z = -26; z <= 24; z++)                                                    // chassis / hood
            for (int x = -9; x <= 9; x++)
            for (int y = 7; y <= (z > 0 ? 17 : 12); y++)
            {
                bool shell = Abs(x) == 9 || z == -26 || z == 24 || y == 7 || y == (z > 0 ? 17 : 12);
                if (shell) g.Set(x, y, z, z > 20 ? Pal.Stripe(Pal.Solid(Pal.Void), Pal.Ramp(Pal.Metal, 1), 1, 2) : paint);
            }
            foreach (int s in new[] { -1, 1 })                                                  // fenders
                for (int z = -24; z <= -4; z++) g.Box(s * 10, 18, z, s * 17, 18, z, Pal.Ramp(Pal.Metal, 1, 1211));
            Cab(g, 10, -20, -2, 12, 30, paint, 1212);
            g.CylY(6, 18, 0.9f, 18, 30, Pal.Ramp(Pal.Black, 1));                                 // exhaust
            Lamps(g, 7, 15, 25, -27);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 10, 7, 16, "wheel_offroad", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 10, 8, -14, "wheel_truck", true);
            d.Socket("engine", PartCategory.Engine, 0, 9, 10, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 9, 22, "radiator_car");
            d.Socket("tool", PartCategory.Tool, 0, 16, 18, "tool_backhoe_loader");
            d.Socket("tool_rear", PartCategory.Tool, 0, 13, -28, "tool_hoe_arm");
            return d;
        }

        // ================================================================== BULLDOZER (crawler, front blade)
        public static VehicleDesign Bulldozer()
        {
            var d = new VehicleDesign { name = "Bulldozer", machine = "Dozer", crawler = true, mass = 11000, drive = VehicleDriver.Drive.All, travel = 0.1f, frequency = 2.4f, finalDrive = 16f, brakeForce = 110000f, maxSteer = 34f, gears = new[] { 3.4f, 2.0f }, eye = new Vector3Int(4, 23, -10), fuelL = 200f, oilL = 24f, coolantL = 34f };
            var g = new VoxelGrid();
            var paint = Yellow(1220);
            Track(g, 9, 6, -24, 24, 10);
            for (int z = -22; z <= 22; z++)
            for (int x = -9; x <= 9; x++)
            for (int y = 4; y <= (z > -4 ? 16 : 11); y++)
            {
                bool shell = Abs(x) == 9 || z == -22 || z == 22 || y == 4 || y == (z > -4 ? 16 : 11);
                if (shell) g.Set(x, y, z, z >= 20 ? Pal.Stripe(Pal.Solid(Pal.Void), Pal.Ramp(Pal.Metal, 1), 1, 2) : paint);
            }
            Cab(g, 9, -22, -4, 11, 29, paint, 1221);
            g.CylY(4, 10, 1f, 17, 24, Pal.Ramp(Pal.Black, 1));
            Lamps(g, 7, 14, 23, -23);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 10, 4, 19, "wheel_track", true);      // idler
            d.Socket("wheel_rear", PartCategory.Wheel, 10, 4, -19, "wheel_track", true);      // sprocket
            d.Socket("engine", PartCategory.Engine, 0, 7, 8, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 7, 20, "radiator_truck");
            d.Socket("tool", PartCategory.Tool, 0, 0, 30, "tool_dozer_blade");
            return d;
        }

        // ================================================================== DUMP TRUCK (6x4 tipper)
        public static VehicleDesign DumpTruck()
        {
            var d = new VehicleDesign { name = "DumpTruck", machine = "DumpTruck", mass = 8000, drive = VehicleDriver.Drive.Rear, travel = 0.26f, frequency = 1.6f, finalDrive = 6.2f, brakeForce = 70000f, maxSteer = 30f, eye = new Vector3Int(4, 27, 30), fuelL = 250f, oilL = 20f, coolantL = 30f };
            var g = new VoxelGrid();
            var paint = Yellow(1230);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 6, 9, -48, s * 8, 12, 44, Pal.Ramp(Pal.Metal, 0, 1231));   // frame rails
            for (int z = 22; z <= 52; z++)                                                     // cab-over body
            for (int x = -13; x <= 13; x++)
            for (int y = 10; y <= 18; y++)
            {
                bool shell = Abs(x) == 13 || z == 22 || z == 52 || y == 10 || y == 18;
                if (shell) g.Set(x, y, z, z == 52 && y < 17 && Abs(x) < 11 ? Pal.Stripe(Pal.Solid(Pal.Void), Pal.Ramp(Pal.Metal, 1), 1, 2) : paint);
            }
            Cab(g, 12, 32, 51, 18, 34, paint, 1232);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 13, 19, 34, s * 13, 19, 34, Pal.Solid(Pal.Amber));
            Lamps(g, 11, 14, 53, -49);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 12, 8, 40, "wheel_truck", true);
            d.Socket("wheel_mid", PartCategory.Wheel, 12, 8, -20, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 12, 8, -36, "wheel_truck", true);
            d.Socket("engine", PartCategory.Engine, 0, 12, 38, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 12, 50, "radiator_truck");
            d.Socket("tool", PartCategory.Tool, 0, 13, -48, "tool_dump_bed");
            return d;
        }

        // ================================================================== PAVER (asphalt / concrete)
        public static VehicleDesign Paver()
        {
            var d = new VehicleDesign { name = "Paver", machine = "Paver", crawler = true, mass = 7000, drive = VehicleDriver.Drive.All, travel = 0.12f, frequency = 2.2f, finalDrive = 16f, brakeForce = 70000f, maxSteer = 26f, gears = new[] { 3.6f, 2.4f }, eye = new Vector3Int(4, 25, -4), fuelL = 150f, oilL = 16f, coolantL = 24f };
            var g = new VoxelGrid();
            var paint = Yellow(1240);
            Track(g, 8, 5, -18, 18, 9);
            g.Box(-9, 5, -20, 9, 14, 18, paint);
            g.Box(-7, 6, -18, 7, 13, 16, Pal.Ramp(Pal.Metal, 0));
            g.Remove(p => Abs(p.x) < 9 && p.y > 5 && p.y < 14 && p.z > -20 && p.z < 18 && g.voxels[p].label == 0);
            // open operator platform with canopy
            g.Box(-9, 15, -14, 9, 15, 6, Pal.Ramp(Pal.Metal, 1, 1241));
            foreach (int x in new[] { -9, 9 }) foreach (int z in new[] { -14, 6 }) g.Box(x, 16, z, x, 30, z, Pal.Ramp(Pal.Metal, 2));
            g.Box(-10, 31, -15, 10, 31, 7, Pal.Stripe(Pal.Solid(Cat[3]), Pal.Solid(Pal.Black[1]), 2, 3));
            Interior(g, 15, 2, -6, 4);
            g.Box(-3, 16, 3, 3, 20, 5, Pal.Ramp(Pal.Black, 1));
            Lamps(g, 8, 12, 19, -21);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 9, 4, 13, "wheel_track", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 9, 4, -13, "wheel_track", true);
            d.Socket("engine", PartCategory.Engine, 0, 7, 6, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 7, 16, "radiator_car");
            d.Socket("tool", PartCategory.Tool, 0, 1, -21, "tool_paver_screed");
            return d;
        }

        // ================================================================== ROLLER (tandem compactor)
        public static VehicleDesign Roller()
        {
            var d = new VehicleDesign { name = "Roller", machine = "Roller", mass = 6000, drive = VehicleDriver.Drive.Rear, travel = 0.08f, frequency = 2.6f, finalDrive = 12f, brakeForce = 60000f, maxSteer = 30f, gears = new[] { 3.2f, 2.0f }, eye = new Vector3Int(4, 27, -6), fuelL = 110f, oilL = 12f, coolantL = 18f };
            var g = new VoxelGrid();
            var paint = Yellow(1250);
            foreach (int zc in new[] { 20, -20 })
            {
                g.CylX(8, zc, 7.2f, -15, 15, p => (p.x % 6 == 0) ? Pal.Metal[1] : Pal.Chrome[1], 6f);          // steel drums
                foreach (int s in new[] { -1, 1 }) g.Box(s * 16, 6, zc - 5, s * 16, 17, zc + 5, paint);          // yokes
            }
            for (int z = -12; z <= 12; z++)
            for (int x = -12; x <= 12; x++)
            for (int y = 12; y <= 18; y++)
            {
                bool shell = Abs(x) == 12 || z == -12 || z == 12 || y == 12 || y == 18;
                if (shell) g.Set(x, y, z, paint);
            }
            g.Box(-16, 18, -18, 16, 18, 18, Pal.Ramp(Pal.Metal, 1, 1251));
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -12, 4 }) g.Box(x, 19, z, x, 33, z, Pal.Ramp(Pal.Metal, 2));
            g.Box(-11, 34, -13, 11, 34, 5, Pal.Stripe(Pal.Solid(Cat[3]), Pal.Solid(Pal.Black[1]), 2, 3));
            Interior(g, 18, 1, -6, 4);
            Lamps(g, 10, 15, 29, -29);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 9, 8, 20, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 9, 8, -20, "wheel_truck", true);
            d.Socket("engine", PartCategory.Engine, 0, 13, -6, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 13, 10, "radiator_car");
            return d;
        }
    }
}
