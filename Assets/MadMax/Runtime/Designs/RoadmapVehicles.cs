using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Roadmap 19 vehicles: a school bus turned mobile home (walk-in), a high-roof ambulance whose bay treats
    /// whoever lies in it, a 6×6 APC, a long-nose semi tractor with a box semi-trailer, a monster truck and a tube-frame
    /// dune buggy. Same conventions as the rest of <see cref="VehicleDesigns"/>: 1 voxel = 8 cm, +Z forward.</summary>
    public static partial class VehicleDesigns
    {
        // ================================================================== SCHOOL BUS (walk-in mobile home)
        public static VehicleDesign Bus()
        {
            var d = new VehicleDesign
            {
                name = "Bus", mass = 9500, drive = VehicleDriver.Drive.Rear, travel = 0.28f, finalDrive = 5.6f,
                gears = new[] { 6.2f, 3.8f, 2.4f, 1.6f, 1.15f, 0.9f }, frequency = 1.3f, brakeForce = 110000f, maxSteer = 30f,
                eye = new Vector3Int(-4, 27, 12), hitch = new Vector3Int(0, 10, -80),
                fuelL = 300f, oilL = 24f, coolantL = 40f
            };
            var g = new VoxelGrid();
            var yellow = Pal.Weathered(Pal.Ochre, 0.28f, 1921, 3, 10);
            var black = Pal.Ramp(Pal.Black, 1, 1922);
            var steel = Pal.Ramp(Pal.Metal, 1, 1923);
            var glass = Pal.Ramp(Pal.Glass, 1, 1924);
            var chrome = Pal.Weathered(Pal.Chrome, 0.3f, 1925, 2, 0);
            var voidM = Pal.Solid(Pal.Void);

            // frame and floor
            g.Box(-8, 8, -78, -6, 11, 44, steel); g.Box(6, 8, -78, 8, 11, 44, steel);
            for (int z = -74; z <= 40; z += 8) g.Box(-6, 9, z, 6, 10, z, steel);
            g.Box(-15, 15, -77, 15, 16, 22, Pal.Weathered(Pal.Metal, 0.4f, 1926, 2, 20));
            // sides: skirt, rub rails, the long row of windows, the folding entry door front right
            for (int z = -77; z <= 22; z++)
            for (int y = 11; y <= 44; y++)
            foreach (int x in new[] { -15, 15 })
            {
                if (y < 17 && (z < -70 || z > 20)) continue;
                VoxMat m = yellow;
                if (y == 22 || y == 40 || y == 14) m = black;
                else if (y >= 29 && y <= 37 && z >= -70 && z <= 6 && ((z + 70) % 12) < 9) m = glass;
                if (x == 15 && z >= 8 && z <= 20 && y >= 17)
                    m = y >= 24 && y <= 41 && z != 14 ? glass : z == 14 || z == 8 || z == 20 ? chrome : black;   // two-leaf glass door
                g.Set(x, y, z, m);
            }
            // rear wall: emergency door in the middle, two windows, lamps
            for (int x = -14; x <= 14; x++)
            for (int y = 17; y <= 44; y++)
            {
                int ax = Abs(x);
                VoxMat m = y == 22 || y == 40 ? black : yellow;
                if (y >= 30 && y <= 38 && ((ax >= 7 && ax <= 12) || ax <= 3)) m = glass;
                g.Set(x, y, -77, m);
            }
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 12, 18, -78, s * 13, 20, -78, Pal.Solid(Pal.TailR));
                g.Set(s * 12, 21, -78, Pal.Solid(Pal.Amber));
                g.Box(s * 13, 42, -78, s * 13, 43, -78, Pal.Solid(Pal.Amber));                              // warning lamps
            }
            g.Box(-3, 26, -78, 3, 26, -78, chrome);                                                        // door handle bar
            // front wall: split windscreen, destination sign
            for (int x = -14; x <= 14; x++)
            for (int y = 17; y <= 44; y++)
            {
                int ax = Abs(x);
                VoxMat m = y == 22 ? black : yellow;
                if (y >= 28 && y <= 41 && ax <= 13 && x != 0) m = glass;
                if (y >= 42 && ax <= 8) m = p => (p.x + p.y) % 3 == 0 ? Pal.LightY : Pal.Black[0];
                g.Set(x, y, 22, m);
            }
            // white roof with a hatch and marker lamps
            g.Box(-15, 45, -77, 15, 46, 22, Pal.Weathered(Pal.Cream, 0.3f, 1927, 2, 0));
            g.Box(-4, 47, -30, 4, 47, -22, steel);
            foreach (int s in new[] { -1, 1 }) { g.Set(s * 12, 47, 21, Pal.Solid(Pal.Amber)); g.Set(s * 12, 47, -76, Pal.Solid(Pal.TailR)); }
            // dog nose: sloped hood, fenders, grille, lamps
            int HoodTop(int z) => 27 - (z - 23) * 3 / 21;
            for (int z = 23; z <= 44; z++)
            for (int x = -11; x <= 11; x++)
            for (int y = 12; y <= HoodTop(z); y++)
            {
                if (Abs(x) == 11 && y == HoodTop(z)) continue;
                g.Set(x, y, z, yellow);
            }
            g.Repaint(-10, 13, 24, 10, 23, 43, Pal.Ramp(Pal.Metal, 0, 1928));
            g.ClearBox(-9, 13, 25, 9, 23, 43);
            g.Box(-9, 13, 45, 9, 24, 45, Pal.Stripe(voidM, chrome, 1, 2));
            g.Box(-10, 25, 45, 10, 25, 45, chrome);
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 10, 21, 45, s * 11, 23, 45, Pal.Solid(Pal.LightY)); g.Set(s * 10, 23, 45, Pal.Solid(Pal.LightW));
                g.Set(s * 11, 19, 45, Pal.Solid(Pal.Amber));
                for (int z = 24; z <= 44; z++)
                for (int y = 7; y <= 17; y++)
                {
                    float r = Mathf.Sqrt((y - 7) * (y - 7) + (z - 34) * (z - 34));
                    if (r > 7.4f && r <= 9f) for (int x = 11; x <= 14; x++) g.Set(s * x, y, z, black);
                }
                g.Box(s * 16, 30, 22, s * 16, 35, 22, steel);                                              // mirror arms
                g.Box(s * 17, 31, 22, s * 17, 35, 23, black);
            }
            g.Box(-17, 30, 12, -17, 34, 12, Pal.Ramp(Pal.Crimson, 3, 1929));                               // stop arm

            Interior(g, 16, 19, 13, -4);

            g.Relabel(15, 17, 8, 15, 43, 20, "door_R");
            g.Relabel(-3, 17, -77, 3, 42, -77, "rear_door");
            g.Relabel(-11, 24, 23, 11, 27, 44, "hood", p => p.y >= HoodTop(p.z) - 1);
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.RelabelWhere((p, v) => v.label == 0 && p.y >= 45, "roof");
            g.Bevel();

            d.Cut(g, "door_R", "bus_door", PartCategory.Door, new Vector3Int(15, 17, 20), 55, 2);
            d.Cut(g, "rear_door", "bus_rear_door", PartCategory.Door, new Vector3Int(0, 17, -77), 50, 2);
            d.Cut(g, "hood", "bus_hood", PartCategory.Hood, new Vector3Int(0, 24, 23), 50, 2);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.roof = g.Extract("roof", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 12, 7, 34, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 12, 7, -52, "wheel_truck", true);
            d.Socket("door", PartCategory.Door, 15, 17, 20, "bus_door");
            d.Socket("door_rear", PartCategory.Door, 0, 17, -77, "bus_rear_door");
            d.Socket("hood", PartCategory.Hood, 0, 24, 23, "bus_hood");
            d.Socket("engine", PartCategory.Engine, 0, 13, 27, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 13, 43, "radiator_truck");
            d.Socket("exhaust", PartCategory.Exhaust, 16, 12, -68, null, true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 4, 46, "bumper_chrome");
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, 6, -79, "bumper_rear_chrome");
            d.Socket("lights", PartCategory.Lights, 0, 47, 16, "lights_bar");
            d.Socket("roof", PartCategory.Weapon, 0, 47, -8, null);
            d.Socket("cargo", PartCategory.Cargo, 0, 47, -52, "cargo_roof_rack");
            d.Socket("snorkel", PartCategory.Snorkel, 16, 20, 20, null);
            d.Socket("steps", PartCategory.Steps, 16, 12, -60, null, true);

            d.colliders.Add(VehicleDesign.Box(-9, 8, -79, 9, 14, 46));      // chassis
            d.colliders.Add(VehicleDesign.Box(-15, 15, -77, 15, 16, 22));   // floor
            d.colliders.Add(VehicleDesign.Box(-15, 17, -77, -15, 44, 22));  // walls
            d.colliders.Add(VehicleDesign.Box(15, 17, -77, 15, 44, 22));
            d.colliders.Add(VehicleDesign.Box(-14, 17, -77, -4, 44, -77));  // rear wall around the door
            d.colliders.Add(VehicleDesign.Box(4, 17, -77, 14, 44, -77));
            d.colliders.Add(VehicleDesign.Box(-3, 43, -77, 3, 44, -77));
            d.colliders.Add(VehicleDesign.Box(-14, 17, 22, 14, 44, 22));    // front
            d.colliders.Add(VehicleDesign.Box(-15, 45, -77, 15, 46, 22));   // roof (hidden in cutaway)
            d.colliders.Add(VehicleDesign.Box(-11, 12, 23, 11, 27, 45));    // hood

            var i = d.interior = new InteriorDesign { floorY = 16.5f, ceilingY = 44.5f, min = new Vector2(-11f, -72.5f), max = new Vector2(11f, 16f) };
            i.doors.Add((new Vector2(2, -72), new Vector3(0, 0, -86)));                    // clear of the bed's foot
            i.doors.Add((new Vector2(11, 15), new Vector3(21, 0, 14)));                    // the stairwell, clear of the seat and the dash
            i.seat = new Vector2(-4, 12);
            i.stand = new Vector2(0, 4);
            i.obstacles.Add(VehicleDesign.Box(-7, 17, 8, -1, 27, 13));       // seats
            i.obstacles.Add(VehicleDesign.Box(1, 17, 8, 7, 27, 13));
            i.obstacles.Add(VehicleDesign.Box(-9, 17, 19, 9, 24, 22));       // dashboard
            i.furniture.Add(("bed", new Vector3(-8, 16.5f, -65), new Vector3(0, 0, 0)));
            // the aisle stays open (a person needs 0.52 m): the table stood across it from the sink and walled the
            // driver into the front of the bus. Lounge along the right wall, kitchen along the left.
            i.furniture.Add(("sofa", new Vector3(11, 16.5f, -44), new Vector3(0, -90, 0)));
            i.furniture.Add(("table", new Vector3(8, 16.5f, -21), new Vector3(0, 90, 0)));
            i.furniture.Add(("fridge", new Vector3(-12, 16.5f, -18), new Vector3(0, 90, 0)));
            i.furniture.Add(("stove", new Vector3(-12, 16.5f, -28), new Vector3(0, 90, 0)));
            i.furniture.Add(("sink", new Vector3(-12, 16.5f, -38), new Vector3(0, 90, 0)));
            i.furniture.Add(("locker", new Vector3(12, 16.5f, -64), new Vector3(0, -90, 0)));
            i.furniture.Add(("crate", new Vector3(-11, 16.5f, -49), new Vector3(0, 0, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 44.5f, -60), new Vector3(180, 0, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 44.5f, -36), new Vector3(180, 0, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 44.5f, -12), new Vector3(180, 0, 0)));
            return d;
        }

        // ================================================================== AMBULANCE (high-roof van, treatment bay)
        public static VehicleDesign Ambulance()
        {
            var d = new VehicleDesign
            {
                name = "Ambulance", mass = 3600, drive = VehicleDriver.Drive.Rear, travel = 0.26f, finalDrive = 4.1f,
                gears = new[] { 4.2f, 2.5f, 1.6f, 1.15f, 0.85f }, frequency = 1.5f, brakeForce = 30000f, maxSteer = 32f,
                eye = new Vector3Int(-4, 22, 9), hitch = new Vector3Int(0, 6, -42), awdSelectable = true,
                fuelL = 110f, oilL = 8f, coolantL = 16f, medical = true
            };
            var g = new VoxelGrid();
            var white = Pal.Weathered(Pal.Cream, 0.14f, 1931, 3, 6);
            var red = Pal.Ramp(Pal.Crimson, 3, 1932);
            var steel = Pal.Ramp(Pal.Metal, 1, 1933);
            var glass = Pal.Ramp(Pal.Glass, 1, 1934);
            var chrome = Pal.Weathered(Pal.Chrome, 0.2f, 1935, 2, 0);
            var voidM = Pal.Solid(Pal.Void);
            bool Cross(int u, int v, int cu, int cv, int arm) => (Abs(u - cu) <= 1 && Abs(v - cv) <= arm) || (Abs(v - cv) <= 1 && Abs(u - cu) <= arm);

            g.Box(-8, 6, -41, -6, 8, 32, steel); g.Box(6, 6, -41, 8, 8, 32, steel);                          // frame
            g.Box(-13, 10, -40, 13, 11, 20, Pal.Weathered(Pal.Metal, 0.3f, 1936, 2, 12));                    // floor
            // walls: red band at the belt, a big red cross on the box, windows in the cab doors
            for (int z = -40; z <= 20; z++)
            for (int y = 7; y <= 33; y++)
            foreach (int x in new[] { -13, 13 })
            {
                if (y < 12 && (z > -36 && z < 20)) { if (y < 9) continue; }
                VoxMat m = white;
                if (y >= 16 && y <= 18) m = red;
                else if (z >= 8 && z <= 18 && y >= 21 && y <= 30) m = glass;
                if (Cross(z, y, -16, 26, 5)) m = red;
                g.Set(x, y, z, m);
            }
            // rear doors with windows and a cross
            for (int x = -12; x <= 12; x++)
            for (int y = 9; y <= 33; y++)
            {
                VoxMat m = y >= 16 && y <= 18 ? red : white;
                if (y >= 27 && y <= 31 && Abs(x) >= 2 && Abs(x) <= 8) m = glass;
                if (Cross(x, y, 0, 22, 3)) m = red;
                if (x == 0 && y >= 9 && y <= 26) m = Pal.Solid(Pal.Chrome[1]);                               // the doors' seam
                g.Set(x, y, -40, m);
            }
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 11, 29, -41, s * 12, 32, -41, Pal.Solid(Pal.TailR));                               // high flashers
                g.Box(s * 11, 12, -41, s * 12, 14, -41, Pal.Solid(Pal.TailR));
                g.Set(s * 12, 15, -41, Pal.Solid(Pal.Amber));
                g.Box(s * 13, 32, -40, s * 13, 33, 20, Pal.Solid(Pal.Cream[4]));                           // roof rail
            }
            // front: windscreen over a short nose
            for (int x = -12; x <= 12; x++)
            for (int y = 12; y <= 33; y++)
            {
                VoxMat m = y >= 16 && y <= 18 ? red : white;
                if (y >= 21 && y <= 32 && Abs(x) <= 11) m = glass;
                g.Set(x, y, 20, m);
            }
            g.Box(-13, 34, -40, 13, 35, 20, Pal.Weathered(Pal.Cream, 0.2f, 1937, 2, 0));                      // high roof
            int HoodTop(int z) => 20 - (z - 21) / 5;
            for (int z = 21; z <= 32; z++)
            for (int x = -11; x <= 11; x++)
            for (int y = 11; y <= HoodTop(z); y++)
            {
                if (Abs(x) == 11 && y == HoodTop(z)) continue;
                g.Set(x, y, z, y >= 16 && y <= 17 ? red : white);
            }
            g.ClearBox(-9, 12, 22, 9, 16, 31);
            g.Repaint(-10, 11, 21, 10, 17, 32, p => p.x > -10 && p.x < 10 && p.y < 17 && p.z < 32 ? Pal.Metal[0] : Pal.Cream[3]);
            g.Box(-8, 12, 33, 8, 16, 33, Pal.Stripe(voidM, chrome, 1, 2));                                   // grille
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 9, 15, 33, s * 10, 16, 33, Pal.Solid(Pal.LightY)); g.Set(s * 9, 16, 33, Pal.Solid(Pal.LightW));
                g.Set(s * 10, 13, 33, Pal.Solid(Pal.Amber));
                g.Box(s * 14, 26, 19, s * 15, 29, 19, Pal.Ramp(Pal.Black, 1, 1938));                          // mirrors
                for (int z = 17; z <= 36; z++)
                for (int y = 6; y <= 15; y++)
                {
                    float r = Mathf.Sqrt((y - 6) * (y - 6) + (z - 27) * (z - 27));
                    if (r > 7.2f && r <= 8.4f) for (int x = 11; x <= 13; x++) g.Set(s * x, y, z, Pal.Ramp(Pal.Black, 1, 1939));
                }
            }

            Interior(g, 11, 16, 10, -4);

            g.Relabel(13, 12, 8, 13, 33, 19, "door_R");
            g.Relabel(-13, 12, 8, -13, 33, 19, "door_L");
            g.Relabel(-11, 9, -40, 11, 32, -40, "rear_door");
            g.Relabel(-11, 17, 21, 11, 20, 32, "hood", p => p.y >= HoodTop(p.z) - 1);
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.RelabelWhere((p, v) => v.label == 0 && p.y >= 34, "roof");
            g.Bevel();

            d.Cut(g, "door_R", "amb_door", PartCategory.Door, new Vector3Int(13, 11, 19), 40, 2);
            d.Cut(g, "rear_door", "amb_rear_door", PartCategory.Door, new Vector3Int(0, 9, -40), 45, 2);
            d.Cut(g, "hood", "amb_hood", PartCategory.Hood, new Vector3Int(0, 17, 21), 25, 1);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.roof = g.Extract("roof", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 10, 6, 27, "wheel_offroad", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 10, 6, -26, "wheel_offroad", true);
            d.Socket("door", PartCategory.Door, 13, 11, 19, "amb_door", true);
            d.Socket("door_rear", PartCategory.Door, 0, 9, -40, "amb_rear_door");
            d.Socket("hood", PartCategory.Hood, 0, 17, 21, "amb_hood");
            d.Socket("engine", PartCategory.Engine, 0, 11, 23, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 11, 32, "radiator_car");
            d.Socket("exhaust", PartCategory.Exhaust, 13, 5, -34, null, true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 5, 34, "bumper_chrome");
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, 6, -42, "bumper_rear_chrome");
            d.Socket("lights", PartCategory.Lights, 0, 36, 16, "lights_emergency");
            d.Socket("roof", PartCategory.Weapon, 0, 36, -10, null);
            d.Socket("cargo", PartCategory.Cargo, 0, 36, -30, null);
            d.Socket("snorkel", PartCategory.Snorkel, 13, 14, 20, null);
            d.Socket("steps", PartCategory.Steps, 13, 5, -2, null, true);

            d.colliders.Add(VehicleDesign.Box(-8, 5, -41, 8, 10, 34));      // chassis
            d.colliders.Add(VehicleDesign.Box(-13, 10, -40, 13, 11, 20));   // floor
            d.colliders.Add(VehicleDesign.Box(-13, 12, -40, -13, 33, 20));  // walls
            d.colliders.Add(VehicleDesign.Box(13, 12, -40, 13, 33, 20));
            d.colliders.Add(VehicleDesign.Box(-12, 12, -40, 12, 33, -40));  // rear doors
            d.colliders.Add(VehicleDesign.Box(-12, 12, 20, 12, 33, 20));    // front
            d.colliders.Add(VehicleDesign.Box(-13, 34, -40, 13, 35, 20));   // roof (hidden in cutaway)
            d.colliders.Add(VehicleDesign.Box(-11, 11, 21, 11, 20, 33));    // nose

            var i = d.interior = new InteriorDesign { floorY = 11.5f, ceilingY = 33.5f, min = new Vector2(-9.5f, -36.5f), max = new Vector2(9.5f, 2f) };
            i.doors.Add((new Vector2(0, -36), new Vector3(0, 0, -49)));
            i.doors.Add((new Vector2(8, 1), new Vector3(19, 0, 12)));
            i.doors.Add((new Vector2(-8, 1), new Vector3(-19, 0, 12)));
            i.seat = new Vector2(-4, 9);
            i.stand = new Vector2(0, 0);
            i.obstacles.Add(VehicleDesign.Box(-7, 12, 4, -1, 22, 10));       // seats
            i.obstacles.Add(VehicleDesign.Box(1, 12, 4, 7, 22, 10));
            i.obstacles.Add(VehicleDesign.Box(-11, 12, 15, 11, 20, 20));     // dashboard
            i.furniture.Add(("bed", new Vector3(6, 11.5f, -22), new Vector3(0, 0, 0)));      // the stretcher
            i.furniture.Add(("locker", new Vector3(-11, 11.5f, -28), new Vector3(0, 90, 0)));
            i.furniture.Add(("sink", new Vector3(-11, 11.5f, -14), new Vector3(0, 90, 0)));
            i.furniture.Add(("shelf", new Vector3(-12.5f, 25f, -20), new Vector3(90, 90, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 33.5f, -26), new Vector3(180, 0, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 33.5f, -8), new Vector3(180, 0, 0)));
            return d;
        }

        // ================================================================== APC 6×6 (armoured, both front axles steer)
        public static VehicleDesign Apc()
        {
            var d = new VehicleDesign
            {
                name = "APC", mass = 13500, drive = VehicleDriver.Drive.All, travel = 0.34f, finalDrive = 6.2f,
                gears = new[] { 6.5f, 4.0f, 2.6f, 1.7f, 1.2f, 0.9f }, frequency = 1.4f, brakeForce = 120000f, maxSteer = 26f,
                eye = new Vector3Int(-4, 21, 23), hitch = new Vector3Int(0, 10, -48), diffLock = true,
                fuelL = 300f, oilL = 25f, coolantL = 40f
            };
            var g = new VoxelGrid();
            VoxMat camo = p =>
            {
                float n = Pal.Noise(new Vector3(p.x * 0.09f, p.y * 0.09f, p.z * 0.09f), 1941);
                return n > 0.62f ? Pal.Pick(Pal.Sand, p, 1942, 2) : n < 0.34f ? Pal.Pick(Pal.Black, p, 1943, 2) : Pal.Pick(Pal.Moss, p, 1944, 2);
            };
            var steel = Pal.Ramp(Pal.Metal, 1, 1945);
            var glass = Pal.Ramp(Pal.Glass, 2, 1946);
            // hull: V-bottom, vertical sides, sloped shoulders, glacis down to the nose
            int Bottom(int x) => 8 + Mathf.Max(0, Abs(x) - 10) * 8 / 6;
            int Top(int x) => Abs(x) <= 12 ? 32 : 32 - (Abs(x) - 12) * 3 / 2;
            for (int z = -46; z <= 48; z++)
            for (int x = -16; x <= 16; x++)
            {
                int top = Top(x), bot = Bottom(x);
                if (z > 36) { top = Mathf.Min(top, 32 - (z - 36) * 5 / 3); bot += Mathf.Max(0, z - 44); }
                for (int y = bot; y <= top; y++) g.Set(x, y, z, camo);
            }
            // driver's compartment (commander beside him), engine bay at the back under its deck
            g.Remove(p => Abs(p.x) <= 14 && p.y >= 11 && p.y <= 30 && p.z >= 14 && p.z < 36 + (31 - p.y) * 3 / 5 - 1);
            g.Remove(p => Abs(p.x) <= 10 && p.y >= 11 && p.y <= 30 && p.z >= -45 && p.z <= -30);
            for (int y = 19; y <= 23; y++)                                                                     // vision blocks through the glacis
            for (int z = 36 + (31 - y) * 3 / 5 - 1; z <= 36 + (32 - y) * 3 / 5; z++)
            for (int x = -11; x <= 11; x++)
                if (x != 0 && g.Has(x, y, z)) g.Set(x, y, z, glass);
            // details: rivets, hatches, turret ring, lamps in cages, tow eyes, engine grille
            for (int z = -44; z <= 34; z += 3) foreach (int s in new[] { -1, 1 }) g.Repaint(s * 16, 26, z, s * 16, 26, z, Pal.Solid(Pal.Chrome[1]));
            g.CylY(-5, 24, 3.2f, 33, 33, steel); g.Set(-5, 34, 24, Pal.Solid(Pal.Metal[3]));
            g.CylY(0, -4, 6f, 33, 33, Pal.Ramp(Pal.Metal, 2, 1947), 4.5f);                                    // turret ring
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 12, 13, 47, s * 13, 14, 47, Pal.Solid(Pal.LightY)); g.Set(s * 12, 14, 47, Pal.Solid(Pal.LightW));
                g.Box(s * 11, 12, 48, s * 14, 15, 48, p => (p.x + p.y) % 2 == 0 ? Pal.Black[1] : Pal.Void);   // cage
                g.Box(s * 8, 11, 49, s * 9, 12, 49, steel);                                                   // tow eyes
                g.Box(s * 13, 20, -47, s * 14, 21, -47, Pal.Solid(Pal.TailR));
                for (int z = -44; z <= -32; z += 2) g.Set(s * 16, 24, z, Pal.Solid(Pal.Void));                 // engine louvres
            }
            g.Box(-3, 32, -30, 3, 32, -26, steel);
            Interior(g, 10, 30, 24, -4);

            g.Relabel(16, 16, -6, 16, 25, 6, "door_R");
            g.Relabel(-16, 16, -6, -16, 25, 6, "door_L");
            g.Relabel(-10, 31, -45, 10, 32, -30, "hood");
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();

            d.Cut(g, "door_R", "apc_door", PartCategory.Door, new Vector3Int(16, 16, 6), 160, 3);
            d.Cut(g, "hood", "apc_deck", PartCategory.Hood, new Vector3Int(0, 31, -30), 140, 3);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 12, 7, 30, "wheel_truck", true);
            d.Socket("wheel_mid", PartCategory.Wheel, 12, 7, 6, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 12, 7, -22, "wheel_truck", true);
            d.Socket("door", PartCategory.Door, 16, 16, 6, "apc_door", true);
            d.Socket("hood", PartCategory.Hood, 0, 31, -30, "apc_deck");
            d.Socket("engine", PartCategory.Engine, 0, 11, -44, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 11, -32, "radiator_truck");
            d.Socket("exhaust", PartCategory.Exhaust, 16, 22, -40, "exhaust_stack", true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 6, 49, "bumper_plow");
            d.Socket("armor", PartCategory.Armor, 17, 17, 0, "armor_smoke", true);
            d.Socket("roof", PartCategory.Weapon, 0, 34, -4, "weapon_mg");
            d.Socket("lights", PartCategory.Lights, 0, 33, 30, "lights_search");
            d.Socket("cargo", PartCategory.Cargo, 0, 33, -18, "cargo_jerry_rack");
            d.Socket("snorkel", PartCategory.Snorkel, 16, 24, 28, "snorkel");
            d.Socket("steps", PartCategory.Steps, 16, 12, -30, "steps_side", true);

            d.colliders.Add(VehicleDesign.Box(-16, 8, -46, 16, 32, 36));    // hull
            d.colliders.Add(VehicleDesign.Box(-16, 10, 37, 16, 30, 48));    // glacis
            return d;
        }

        // ================================================================== SEMI TRACTOR (long nose, sleeper, fifth wheel)
        public static VehicleDesign SemiTractor()
        {
            var d = new VehicleDesign
            {
                name = "Semi", mass = 8200, drive = VehicleDriver.Drive.Rear, travel = 0.28f, finalDrive = 4.6f,
                gears = new[] { 7.5f, 5.2f, 3.6f, 2.6f, 1.9f, 1.4f, 1.05f, 0.8f }, frequency = 1.4f, brakeForce = 110000f, maxSteer = 30f,
                eye = new Vector3Int(-4, 26, 9), hitch = new Vector3Int(0, 17, -35), diffLock = true,
                fuelL = 500f, oilL = 30f, coolantL = 45f
            };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Crimson, 0.25f, 1971, 3, 10);
            var steel = Pal.Ramp(Pal.Metal, 1, 1972);
            var glass = Pal.Ramp(Pal.Glass, 1, 1973);
            var chrome = Pal.Weathered(Pal.Chrome, 0.2f, 1974, 2, 0);
            var black = Pal.Ramp(Pal.Black, 1, 1975);
            var voidM = Pal.Solid(Pal.Void);

            g.Box(-8, 8, -52, -6, 11, 44, steel); g.Box(6, 8, -52, 8, 11, 44, steel);
            for (int z = -48; z <= 40; z += 8) g.Box(-6, 9, z, 6, 10, z, steel);
            // cab and sleeper shells
            g.Box(-14, 14, -16, 14, 15, 20, Pal.Weathered(Pal.Metal, 0.3f, 1976, 2, 12));
            for (int z = -16; z <= 20; z++)
            for (int y = 16; y <= 46; y++)
            foreach (int x in new[] { -14, 14 })
            {
                int top = z >= 2 ? 42 : 46;
                if (y > top) continue;
                VoxMat m = y == 24 ? chrome : paint;
                if (z >= 6 && z <= 16 && y >= 29 && y <= 38) m = glass;                                          // door windows
                if (z >= -12 && z <= -4 && y >= 33 && y <= 36) m = glass;                                        // sleeper bunk window
                g.Set(x, y, z, m);
            }
            for (int x = -13; x <= 13; x++)
            {
                for (int y = 16; y <= 42; y++) g.Set(x, y, 20, y >= 29 && y <= 40 && x != 0 ? glass : y == 24 ? chrome : paint);
                for (int y = 16; y <= 46; y++) g.Set(x, y, -16, paint);
            }
            g.Box(-14, 43, 2, 14, 44, 20, paint);                                                             // cab roof
            g.Box(-14, 47, -16, 14, 48, 1, paint);                                                            // sleeper roof
            for (int z = 2; z <= 6; z++) g.Box(-13, 43, z, 13, 42 + (7 - z), z, paint);                       // fairing step
            // long nose
            int HoodTop(int z) => 28 - (z - 21) / 8;
            for (int z = 21; z <= 44; z++)
            for (int x = -11; x <= 11; x++)
            for (int y = 12; y <= HoodTop(z); y++)
            {
                if (Abs(x) == 11 && y == HoodTop(z)) continue;
                g.Set(x, y, z, paint);
            }
            g.Repaint(-10, 13, 22, 10, 24, 43, Pal.Ramp(Pal.Metal, 0, 1977));
            g.ClearBox(-9, 13, 22, 9, 24, 43);
            g.Box(-9, 13, 45, 9, 26, 45, Pal.Stripe(voidM, chrome, 1, 2));                                   // tall chrome grille
            g.Box(-10, 27, 45, 10, 27, 45, chrome);
            g.Set(0, 29, 44, Pal.Solid(Pal.Chrome[3])); g.Set(0, 30, 44, Pal.Solid(Pal.Chrome[3]));             // hood ornament
            foreach (int s in new[] { -1, 1 })
            {
                for (int z = 23; z <= 45; z++)                                                                // front fenders with lamps
                for (int y = 7; y <= 18; y++)
                {
                    float r = Mathf.Sqrt((y - 7) * (y - 7) + (z - 34) * (z - 34));
                    if (r > 7.4f && r <= 9.4f) for (int x = 11; x <= 15; x++) g.Set(s * x, y, z, paint);
                }
                g.Box(s * 13, 17, 44, s * 14, 18, 44, Pal.Solid(Pal.LightY)); g.Set(s * 13, 18, 44, Pal.Solid(Pal.LightW));
                g.Set(s * 15, 16, 43, Pal.Solid(Pal.Amber));
                g.CylZ(s * 12, 11, 2.6f, -12, 0, chrome);                                                    // fuel tanks
                g.Box(s * 15, 10, 6, s * 16, 10, 16, chrome); g.Box(s * 15, 13, 6, s * 16, 13, 16, chrome);   // steps
                g.Box(s * 9, 16, -52, s * 15, 17, -22, black);                                                // tandem fenders
                g.Box(s * 11, 3, -53, s * 15, 12, -53, black);                                                // mud flaps
                g.Box(s * 10, 11, -53, s * 12, 12, -53, Pal.Solid(Pal.TailR));
                g.Box(s * 15, 30, 20, s * 16, 36, 20, black);                                                 // west-coast mirrors
                g.Set(s * 12, 49, -2, Pal.Solid(Pal.Amber));                                                  // cab lamps
            }
            g.Box(-7, 14, -42, 7, 15, -28, steel);                                                            // fifth wheel
            g.ClearBox(-1, 15, -42, 1, 15, -36);
            g.Box(-7, 16, -30, 7, 16, -28, Pal.Ramp(Pal.Metal, 2, 1978));

            Interior(g, 15, 16, 10, -4);

            g.Relabel(14, 16, 4, 14, 41, 18, "door_R");
            g.Relabel(-14, 16, 4, -14, 41, 18, "door_L");
            g.Relabel(-11, 25, 21, 11, 28, 44, "hood", p => p.y >= HoodTop(p.z) - 1);
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();

            d.Cut(g, "door_R", "semi_door", PartCategory.Door, new Vector3Int(14, 15, 18), 50, 2);
            d.Cut(g, "hood", "semi_hood", PartCategory.Hood, new Vector3Int(0, 25, 21), 70, 2);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 11, 7, 34, "wheel_truck", true);
            d.Socket("wheel_mid", PartCategory.Wheel, 11, 7, -30, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 11, 7, -44, "wheel_truck", true);
            d.Socket("door", PartCategory.Door, 14, 15, 18, "semi_door", true);
            d.Socket("hood", PartCategory.Hood, 0, 25, 21, "semi_hood");
            d.Socket("engine", PartCategory.Engine, 0, 13, 24, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 13, 43, "radiator_truck");
            d.Socket("exhaust", PartCategory.Exhaust, 15, 16, -1, "exhaust_stack", true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 5, 46, "bumper_chrome");
            d.Socket("armor", PartCategory.Armor, 15, 12, -8, null, true);
            d.Socket("lights", PartCategory.Lights, 0, 49, -4, "lights_bar");
            d.Socket("roof", PartCategory.Weapon, 0, 49, -10, null);
            d.Socket("cargo", PartCategory.Cargo, 0, 16, -20, null);
            d.Socket("snorkel", PartCategory.Snorkel, 15, 30, 19, null);
            d.Socket("steps", PartCategory.Steps, 15, 10, 2, null, true);

            d.colliders.Add(VehicleDesign.Box(-8, 8, -53, 8, 13, 46));      // chassis
            d.colliders.Add(VehicleDesign.Box(-14, 14, -16, 14, 48, 20));   // cab + sleeper
            d.colliders.Add(VehicleDesign.Box(-11, 12, 21, 11, 28, 45));    // nose
            d.colliders.Add(VehicleDesign.Box(-7, 14, -42, 7, 15, -28));    // fifth wheel
            return d;
        }

        // ================================================================== BOX SEMI-TRAILER (kingpin, landing legs, cargo hold)
        public static VehicleDesign BoxTrailer()
        {
            var d = new VehicleDesign
            {
                name = "BoxTrailer", mass = 5200, driveable = false, fuelL = 0f, oilL = 0f, coolantL = 0f, usesCoolant = false,
                maxSteer = 0f, travel = 0.2f, frequency = 1.8f, brakeForce = 60000f, coupler = new Vector3Int(0, 17, 54), cargoKg = 3000f
            };
            var g = new VoxelGrid();
            var skin = Pal.Weathered(Pal.Cream, 0.4f, 1951, 2, 20);
            var steel = Pal.Ramp(Pal.Metal, 1, 1952);
            var band = Pal.Ramp(Pal.Ochre, 3, 1954);
            g.Box(-8, 13, -82, -6, 17, 60, steel); g.Box(6, 13, -82, 8, 17, 60, steel);                       // rails
            g.Box(-15, 18, -82, 15, 19, 60, Pal.Weathered(Pal.Metal, 0.35f, 1955, 2, 30));                   // floor
            for (int z = -82; z <= 60; z++)
            for (int y = 20; y <= 50; y++)
            foreach (int x in new[] { -15, 15 })
                g.Set(x, y, z, y >= 42 && y <= 44 ? band : skin);
            for (int x = -14; x <= 14; x++) for (int y = 20; y <= 50; y++) g.Set(x, y, 60, y >= 42 && y <= 44 ? band : skin);
            g.Box(-15, 51, -82, 15, 52, 60, Pal.Weathered(Pal.Metal, 0.5f, 1956, 2, 0));                     // roof
            foreach (int s in new[] { -1, 1 })
            {
                for (int z = -80; z <= 58; z += 8) g.Box(s * 16, 20, z, s * 16, 50, z, steel);              // ribs
                g.Box(s * 16, 19, -82, s * 16, 19, 60, steel); g.Box(s * 16, 51, -82, s * 16, 51, 60, steel);
                g.Box(s * 12, 20, -83, s * 14, 22, -83, Pal.Solid(Pal.TailR));
                g.Set(s * 15, 52, -82, Pal.Solid(Pal.Amber)); g.Set(s * 15, 52, 60, Pal.Solid(Pal.Amber));
                g.Box(s * 11, 3, -80, s * 15, 12, -80, Pal.Ramp(Pal.Black, 0, 1957));                          // mud flaps
                g.Box(s * 9, 16, -78, s * 15, 17, -50, Pal.Ramp(Pal.Black, 1, 1958));                          // fenders
            }
            // rear doors: two leaves, hinges and lock bars
            for (int x = -14; x <= 14; x++)
            for (int y = 20; y <= 50; y++)
            {
                VoxMat m = x == 0 ? Pal.Solid(Pal.Metal[0]) : Abs(x) == 4 || Abs(x) == 10 ? Pal.Solid(Pal.Chrome[2]) : skin;
                g.Set(x, y, -82, m);
            }
            foreach (int x in new[] { -15, 15 }) for (int y = 22; y <= 48; y += 8) g.Set(x, y, -83, Pal.Solid(Pal.Metal[3]));
            // side door to the hold (right side, middle), stencil
            g.Box(16, 21, -4, 16, 46, -4, steel); g.Box(16, 21, 10, 16, 46, 10, steel); g.Box(16, 46, -4, 16, 46, 10, steel);
            g.Box(16, 30, 7, 16, 31, 8, Pal.Solid(Pal.Chrome[3]));
            foreach (int z in new[] { -40, -38, -36, -32, -30, -26, -24 }) g.Box(16, 36, z, 16, 39, z, Pal.Solid(Pal.Black[1]));
            // underride bar, kingpin plate, landing legs (wound up while hitched)
            g.Box(-13, 8, -84, 13, 10, -84, steel);
            foreach (int x in new[] { -10, 10 }) g.Box(x, 10, -83, x, 13, -83, steel);
            g.Box(-7, 17, 50, 7, 17, 58, Pal.Ramp(Pal.Metal, 2, 1959));
            g.Box(-1, 15, 53, 1, 16, 55, Pal.Solid(Pal.Metal[3]));
            g.Use("legs");
            foreach (int x in new[] { -10, 10 })
            {
                g.Box(x, 1, 46, x + 1, 16, 47, steel);
                g.Box(x - 1, 0, 45, x + 2, 0, 48, steel);
                g.Box(x, 12, 45, x + 1, 13, 45, Pal.Solid(Pal.Metal[3]));                                     // crank box
            }
            g.Box(-9, 8, 46, 9, 8, 46, steel);                                                                // cross brace
            g.label = 0;
            g.Bevel();
            d.legs = g.Take("legs", Vector3Int.zero);
            d.body = g;
            d.Socket("wheel_front", PartCategory.Wheel, 11, 7, -58, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 11, 7, -70, "wheel_truck", true);
            d.Socket("cargo", PartCategory.Cargo, 0, 53, -10, null);
            d.colliders.Add(VehicleDesign.Box(-16, 18, -83, 16, 52, 60));   // box
            d.colliders.Add(VehicleDesign.Box(-8, 12, -84, 8, 17, 58));     // running gear
            return d;
        }

        // ================================================================== MONSTER TRUCK (pickup body on a tube chassis)
        public static VehicleDesign MonsterTruck()
        {
            var d = new VehicleDesign
            {
                name = "MonsterTruck", mass = 5400, drive = VehicleDriver.Drive.All, travel = 0.62f, finalDrive = 5.4f,
                gears = new[] { 3.6f, 2.2f, 1.5f, 1.1f }, frequency = 1.05f, brakeForce = 50000f, maxSteer = 34f,
                eye = new Vector3Int(-4, 36, 0), hitch = new Vector3Int(0, 20, -34), diffLock = true,
                fuelL = 140f, oilL = 9f, coolantL = 16f
            };
            var g = new VoxelGrid();
            var red = Pal.Weathered(Pal.Crimson, 0.12f, 1981, 3, 30);
            var steel = Pal.Ramp(Pal.Metal, 1, 1982);
            var glass = Pal.Ramp(Pal.Glass, 1, 1983);
            var chrome = Pal.Weathered(Pal.Chrome, 0.15f, 1984, 2, 0);
            var black = Pal.Ramp(Pal.Black, 1, 1985);
            var voidM = Pal.Solid(Pal.Void);
            // hot-rod flames licking back from the nose along the lower body
            VoxMat paint = p =>
            {
                float edge = 3.5f + 2.6f * Mathf.Sin(p.z * 0.55f) + (p.z + 6) * 0.2f;
                float h = p.y - 26;
                if (p.z > -8 && h < edge) return h < edge - 2.2f ? Pal.Ochre[4] : Pal.Crimson[4];
                return red(p);
            };

            // tube chassis, four-link bars, shocks
            g.Box(-7, 17, -34, -6, 20, 34, steel); g.Box(6, 17, -34, 7, 20, 34, steel);
            for (int z = -30; z <= 30; z += 10) g.Box(-6, 18, z, 6, 19, z, steel);
            foreach (int s in new[] { -1, 1 })
            foreach (int zc in new[] { 24, -24 })
            {
                g.Tube(new Vector3(s * 7, 19, zc + 6), new Vector3(s * 12, 12, zc), 0.6f, chrome);        // coilover
                g.Tube(new Vector3(s * 7, 18, zc - 7), new Vector3(s * 11, 11, zc - 1), 0.5f, steel);      // link
            }
            g.Box(-12, 24, -32, 12, 25, 31, Pal.Weathered(Pal.Metal, 0.25f, 1986, 2, 0));                    // body floor
            // cab shell
            for (int z = -6; z <= 10; z++)
            for (int y = 26; y <= 42; y++)
            foreach (int x in new[] { -12, 12 })
                g.Set(x, y, z, z >= -3 && z <= 8 && y >= 34 && y <= 40 ? glass : paint);
            for (int x = -11; x <= 11; x++)
            for (int y = 26; y <= 42; y++)
            {
                g.Set(x, y, 10, y >= 34 && y <= 41 && x != 0 ? glass : paint);
                g.Set(x, y, -6, y >= 35 && y <= 40 && Abs(x) <= 7 ? glass : paint);
            }
            g.Box(-12, 43, -6, 12, 44, 10, paint);
            // hood with a blower scoop hole, grille, lamps
            int HoodTop(int z) => 35 - (z - 11) / 7;
            for (int z = 11; z <= 31; z++)
            for (int x = -11; x <= 11; x++)
            for (int y = 26; y <= HoodTop(z); y++)
            {
                if (Abs(x) == 11 && y == HoodTop(z)) continue;
                g.Set(x, y, z, paint);
            }
            g.ClearBox(-9, 26, 12, 9, 32, 30);
            g.Repaint(-10, 26, 11, 10, 32, 31, p => Abs(p.x) < 10 && p.z > 11 && p.z < 31 ? Pal.Metal[0] : Pal.Crimson[2]);
            g.ClearBox(-3, HoodTop(18), 16, 3, HoodTop(18), 20);                                            // blower pokes through
            g.Box(-9, 27, 32, 9, 33, 32, Pal.Stripe(voidM, chrome, 1, 2));
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 9, 31, 32, s * 10, 32, 32, Pal.Solid(Pal.LightY)); g.Set(s * 9, 32, 32, Pal.Solid(Pal.LightW));
                g.Set(s * 10, 29, 32, Pal.Solid(Pal.Amber));
                g.Box(s * 10, 27, -33, s * 11, 29, -33, Pal.Solid(Pal.TailR));
                // bed walls and the roll bar
                for (int z = -32; z <= -7; z++) for (int y = 26; y <= 33; y++) g.Set(s * 12, y, z, paint);
                g.Tube(new Vector3(s * 11, 33, -8), new Vector3(s * 11, 44, -9), 0.7f, chrome);
                g.Box(s * 13, 36, 10, s * 14, 38, 10, black);                                                 // mirrors
            }
            for (int x = -11; x <= 11; x++) for (int y = 26; y <= 33; y++) g.Set(x, y, -32, paint);            // tailgate
            g.Tube(new Vector3(-11, 44, -9), new Vector3(11, 44, -9), 0.7f, chrome);

            Interior(g, 25, 7, 1, -4);

            g.Relabel(12, 26, -4, 12, 42, 9, "door_R");
            g.Relabel(-12, 26, -4, -12, 42, 9, "door_L");
            g.Relabel(-11, 32, 11, 11, 35, 31, "hood", p => p.y >= HoodTop(p.z) - 1);
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();

            d.Cut(g, "door_R", "monster_door", PartCategory.Door, new Vector3Int(12, 25, 9), 35, 2);
            d.Cut(g, "hood", "monster_hood", PartCategory.Hood, new Vector3Int(0, 32, 11), 30, 2);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 13, 11, 24, "wheel_monster", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 13, 11, -24, "wheel_monster", true);
            d.Socket("door", PartCategory.Door, 12, 25, 9, "monster_door", true);
            d.Socket("hood", PartCategory.Hood, 0, 32, 11, "monster_hood");
            d.Socket("engine", PartCategory.Engine, 0, 26, 14, "engine_v8_blower");
            d.Socket("radiator", PartCategory.Radiator, 0, 26, 30, "radiator_car");
            d.Socket("exhaust", PartCategory.Exhaust, 12, 27, 12, "exhaust_side_pipes", true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 20, 33, "bumper_bull_bar");
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, 22, -34, "bumper_rear_chrome");
            d.Socket("lights", PartCategory.Lights, 0, 45, 6, "lights_bar");
            d.Socket("roof", PartCategory.Weapon, 0, 45, 0, null);
            d.Socket("cargo", PartCategory.Cargo, 0, 26, -20, "cargo_jerry_rack");
            d.Socket("armor", PartCategory.Armor, 13, 30, -18, null, true);
            d.Socket("snorkel", PartCategory.Snorkel, 12, 30, 10, null);
            d.Socket("steps", PartCategory.Steps, 12, 22, 0, "steps_side", true);
            return d;
        }

        // ================================================================== DUNE BUGGY (tube frame, rear engine, open)
        public static VehicleDesign DuneBuggy()
        {
            var d = new VehicleDesign
            {
                name = "DuneBuggy", mass = 650, drive = VehicleDriver.Drive.Rear, travel = 0.34f, finalDrive = 4.2f,
                gears = new[] { 3.2f, 2.1f, 1.5f, 1.1f, 0.85f }, frequency = 1.6f, brakeForce = 9000f, maxSteer = 34f,
                eye = new Vector3Int(-4, 17, 0), hitch = new Vector3Int(0, 7, -24),
                fuelL = 40f, oilL = 4f, coolantL = 0f, usesCoolant = false
            };
            var g = new VoxelGrid();
            var panel = Pal.Weathered(Pal.Ochre, 0.18f, 1961, 3, 4);
            var cage = Pal.Ramp(Pal.Black, 1, 1962);
            var steel = Pal.Ramp(Pal.Metal, 1, 1963);
            g.Box(-8, 5, -20, 8, 6, 12, steel);                                                               // floor pan
            foreach (int s in new[] { -1, 1 })
            {
                g.Tube(new Vector3(s * 9, 7, -20), new Vector3(s * 9, 7, 16), 0.6f, cage);                   // side rails
                g.Tube(new Vector3(s * 8, 6, 8), new Vector3(s * 7, 20, 6), 0.6f, cage);                     // front hoop
                g.Tube(new Vector3(s * 8, 6, -8), new Vector3(s * 7, 21, -8), 0.6f, cage);                   // main hoop
                g.Tube(new Vector3(s * 7, 20, 6), new Vector3(s * 7, 21, -8), 0.6f, cage);                   // roof bars
                g.Tube(new Vector3(s * 7, 21, -8), new Vector3(s * 6, 9, -22), 0.6f, cage);                  // rear stays
                g.Tube(new Vector3(s * 9, 7, 14), new Vector3(s * 5, 6, 26), 0.5f, cage);                    // nose frame
                g.Box(s * 5, 8, 26, s * 6, 9, 26, Pal.Solid(Pal.LightY)); g.Set(s * 5, 9, 26, Pal.Solid(Pal.LightW));
                g.Box(s * 5, 10, -23, s * 6, 11, -23, Pal.Solid(Pal.TailR));
            }
            g.Tube(new Vector3(-7, 20, 6), new Vector3(7, 20, 6), 0.6f, cage);
            g.Tube(new Vector3(-7, 21, -8), new Vector3(7, 21, -8), 0.6f, cage);
            g.Tube(new Vector3(-7, 8, -8), new Vector3(7, 20, -8), 0.5f, cage);                               // diagonal
            g.Tube(new Vector3(-6, 9, -22), new Vector3(6, 9, -22), 0.6f, cage);                              // rear bar
            // fibreglass nose tub
            int NoseTop(int z) => 12 - (z - 12) * 5 / 14;
            for (int z = 12; z <= 25; z++)
            for (int x = -6; x <= 6; x++)
            for (int y = 6; y <= NoseTop(z); y++)
                if (Abs(x) == 6 || y == NoseTop(z) || z == 25) g.Set(x, y, z, panel);
            g.Box(-3, 11, 16, 3, 11, 20, Pal.Ramp(Pal.Black, 0, 1964));                                        // spare-wheel well
            Interior(g, 6, 7, 1, -4);

            g.Relabel(-6, 9, 12, 6, 12, 25, "hood", p => p.y >= NoseTop(p.z) - 1 && Abs(p.x) <= 5);
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.Cut(g, "hood", "buggy_hood", PartCategory.Hood, new Vector3Int(0, 12, 12), 8, 1);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 9, 5, 16, "wheel_offroad", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 9, 6, -16, "wheel_mud", true);
            d.Socket("hood", PartCategory.Hood, 0, 12, 12, "buggy_hood");
            d.Socket("engine", PartCategory.Engine, 0, 6, -16, "engine_i4");
            d.Socket("exhaust", PartCategory.Exhaust, 5, 9, -21, null, true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 6, 27, "bumper_bull_bar");
            d.Socket("lights", PartCategory.Lights, 0, 22, 0, "lights_bar");
            d.Socket("roof", PartCategory.Weapon, 0, 22, -4, null);
            d.Socket("cargo", PartCategory.Cargo, 0, 13, 14, null);
            d.Socket("armor", PartCategory.Armor, 10, 9, 0, null, true);
            d.Socket("snorkel", PartCategory.Snorkel, 8, 12, -10, null);
            return d;
        }
    }
}
