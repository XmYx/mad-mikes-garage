using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Everyday cars from one parametric body builder: sedan (three-box), coupe (fastback), wagon (long roof,
    /// upright tailgate) and pickup (cab + open bed). Round or square lamps, two-tone roofs, 2 or 4 doors cut from the
    /// body, engine bay under a cut hood. Wheel arches are carved by the builder (VehicleDesign.CarveWheelArches).</summary>
    public static partial class VehicleDesigns
    {
        enum CarStyle { Sedan, Coupe, Wagon, Pickup }

        class CarSpec
        {
            public string name, key;
            public CarStyle style;
            public float mass = 1200f, travel = 0.24f, finalDrive = 3.7f, brake = 14000f, steer = 32f, fuel = 60f;
            public float[] gears;
            public VehicleDriver.Drive drive = VehicleDriver.Drive.Rear;
            public bool awd, diffLock, fourDoor, roundLamps;
            public int halfW = 11, zFront = 26, zRear = -26, floor = 3, belt = 10, noseY = 9;
            public int zWind = 6, zRoofF = 1, zRoofR = -8, roofY = 16, zTail = -18, cabinHalfW = 9;
            public int dashZ = 3, seatZ = -2, dx = -4;              // interior (LHD)
            public string wheel = "wheel_street", engine = "engine_i4", bumperF = "bumper_chrome", bumperR = "bumper_rear_chrome", cargo;
            public int wheelX = 9, wheelY = 5, wheelZF = 17, wheelZR = -17;
            public VoxMat paint, roof, accent;                        // accent: lower-side panels (wood for a woody wagon)
            public int seed = 500;
            public bool beacon;                                      // amber roof light (tow truck)
        }

        /// <summary>Tyre radius in voxels of the wheel parts road cars use (PartLibrary).</summary>
        static float WheelRadiusVox(string key) => key switch
        {
            "wheel_offroad" => 6.5f, "wheel_small" => 4.3f, "wheel_truck" => 6.7f, "wheel_sport" => 5.6f, "wheel_mud" => 6.8f, _ => 5.4f,
        };

        static VehicleDesign BuildCar(CarSpec c)
        {
            // stand the body on its wheels: the belt clears the tyre at full arch bump (radius + 0.8 carve + bump) and the
            // sills sit well above the ground; everything above the floor moves up together
            int beltMin = Mathf.FloorToInt(c.wheelY + VehicleDesign.ArchBumpVox + WheelRadiusVox(c.wheel) + 0.8f) + 1;
            int lift = Mathf.Max(2, beltMin - c.belt);
            c.floor += lift; c.belt += lift; c.noseY += lift; c.roofY += lift;
            var d = new VehicleDesign
            {
                name = c.name, mass = c.mass, drive = c.drive, travel = c.travel, finalDrive = c.finalDrive, gears = c.gears,
                brakeForce = c.brake, maxSteer = c.steer, fuelL = c.fuel, oilL = 5f, coolantL = 9f,
                awdSelectable = c.awd, diffLock = c.diffLock,
                eye = new Vector3Int(c.dx, c.floor + 11, c.seatZ - 1), hitch = new Vector3Int(0, c.floor + 2, c.zRear - 1)
            };
            var g = new VoxelGrid();
            var roofM = c.roof ?? c.paint;
            var glass = Pal.Ramp(Pal.Glass, 1, c.seed + 4);
            var trim = Pal.Solid(Pal.Chrome[2]);
            int W = c.halfW, zF = c.zFront, zR = c.zRear, belt = c.belt, f = c.floor;
            bool pickup = c.style == CarStyle.Pickup;

            // hood slope: belt height at the windshield, noseY at the front face
            int Top(int z) => z <= c.zWind ? belt : belt - Mathf.RoundToInt((z - c.zWind) / (float)(zF - c.zWind) * (belt - c.noseY));

            // ---- lower body with rounded edges and a body line two below the belt
            for (int z = zR; z <= zF; z++)
            for (int x = -W; x <= W; x++)
            for (int y = f; y <= belt; y++)
            {
                int ax = Abs(x), top = Top(z);
                if (y > top) continue;
                if (ax == W && (y == f || y == top)) continue;
                if ((z == zF || z == zR) && (ax >= W - 1 || y == f)) continue;
                if ((z == zF - 1 || z == zR + 1) && ax == W) continue;
                // pickup bed: open box behind the cab (floor at f+1, sides and tailgate one voxel thick)
                if (pickup && z < c.zRoofR - 1 && z > zR && y > f + 1 && ax < W) continue;
                var m = ax == W && y == belt - 2 ? trim : c.accent != null && ax == W && y > f && y < belt - 2 && z > zR + 3 && z < c.zWind ? c.accent : c.paint;
                g.Set(x, y, z, m);
            }

            // ---- lamps, grille, indicators, tail lights, plate
            int lx = W - 3, ly = belt - 3;
            foreach (int s in new[] { -1, 1 })
            {
                if (c.roundLamps)
                {
                    for (int ddx = -1; ddx <= 1; ddx++) for (int ddy = -1; ddy <= 1; ddy++)
                        if (Abs(ddx) + Abs(ddy) < 2) g.Set(s * lx + ddx, ly + ddy, zF, trim);
                    g.Set(s * lx, ly, zF, Pal.Solid(Pal.LightW)); g.Set(s * lx, ly, zF + 1, Pal.Solid(Pal.LightY));
                }
                else
                {
                    g.Box(s * (lx - 1), ly, zF, s * (lx + 1), ly + 1, zF, trim);
                    g.Box(s * (lx - 1), ly, zF, s * lx, ly, zF, Pal.Solid(Pal.LightY)); g.Set(s * lx, ly + 1, zF, Pal.Solid(Pal.LightW));
                }
                g.Set(s * lx, ly - 2, zF, Pal.Solid(Pal.Amber));                                   // indicator
                g.Box(s * (W - 4), belt - 4, zR, s * (W - 2), belt - 3, zR, Pal.Solid(Pal.TailR));   // tail lights
                g.Set(s * (W - 2), belt - 5, zR, Pal.Solid(Pal.Amber));
                g.Box(s * (W - 1), belt, 2, s * W, belt, 2, trim);                                  // mirror stubs
            }
            int gw = lx - 3;
            g.Box(-gw, f + 2, zF, gw, belt - 3, zF, p => (p.y + p.x) % 2 == 0 ? Pal.Chrome[1] : Pal.Void);   // grille
            g.Box(-2, f + 2, zR, 2, f + 3, zR, Pal.Solid(Pal.Black[1]));                         // plate recess
            foreach (int s in new[] { -1, 1 })
            {
                g.Repaint(s * W, f + 1, zR + 3, s * W, f + 1, zF - 3, Pal.Ramp(Pal.Black, 1, c.seed + 8));   // rocker panel
                g.Repaint(s * W, belt - 3, zF - 2, s * W, belt - 3, zF - 2, Pal.Solid(Pal.Amber));           // side markers
                g.Repaint(s * W, belt - 3, zR + 2, s * W, belt - 3, zR + 2, Pal.Solid(Pal.TailR));
            }
            g.Repaint(W, belt - 2, zR + 6, W, belt - 2, zR + 7, trim);                                     // fuel filler cap
            g.Repaint(-gw, f + 1, zF, gw, f + 1, zF, Pal.Ramp(Pal.Black, 1, c.seed + 9));                  // lower valance

            // ---- engine bay under a hood skin (one voxel thick, following the hood slope)
            bool InBay(Vector3Int p) => Abs(p.x) <= W - 3 && p.z > c.zWind + 1 && p.z < zF - 2;
            g.Remove(p => InBay(p) && p.y > f + 1 && p.y < Top(p.z));
            g.Repaint(-(W - 3), f + 1, c.zWind + 2, W - 3, f + 1, zF - 3, Pal.Ramp(Pal.Metal, 0, c.seed + 6));
            g.Use("hood");
            g.RelabelWhere((p, v) => v.label == 0 && Abs(p.x) <= W - 2 && p.z > c.zWind && p.z < zF && p.y >= Top(p.z) && p.y >= belt - 3, "hood");
            g.label = 0;

            // ---- glasshouse
            int cw = c.cabinHalfW, roofY = c.roofY, cabRear = pickup ? c.zRoofR : c.style == CarStyle.Wagon ? zR + 1 : c.zTail;
            int RoofTop(int z)
            {
                if (z > c.zRoofF) return roofY - Mathf.RoundToInt((z - c.zRoofF) * (roofY - belt - 1) / (float)Mathf.Max(1, c.zWind - c.zRoofF));   // windshield
                if (z >= c.zRoofR || c.style == CarStyle.Wagon || pickup) return roofY;
                return roofY - Mathf.RoundToInt((c.zRoofR - z) * (roofY - belt - 1) / (float)Mathf.Max(1, c.zRoofR - c.zTail));                    // rear glass / fastback
            }
            int bPillar = c.seatZ - 6;                                   // between front and rear side windows
            for (int z = cabRear; z <= c.zWind; z++)
            for (int x = -cw; x <= cw; x++)
            {
                int top = RoofTop(z);
                for (int y = belt + 1; y <= top; y++)
                {
                    int ax = Abs(x);
                    bool roofSkin = y >= top - 1, side = ax == cw, front = z == c.zWind, back = z == cabRear;
                    if (!roofSkin && !side && !front && !back) continue;
                    VoxMat m = y >= top - 1 && z <= c.zRoofF && z >= c.zRoofR ? roofM : c.paint;
                    bool windshield = z > c.zRoofF && ax <= cw - 2 && y > belt;
                    bool rearGlass = !pickup && c.style != CarStyle.Wagon && z < c.zRoofR && ax <= cw - 2 && y > belt;
                    bool sideWin = side && y > belt + 1 && y < top - 1 && z < c.zRoofF + 1 && z > cabRear + 1 && Abs(z - bPillar) > 0 && (c.fourDoor || z > bPillar - 1 || c.style == CarStyle.Wagon || c.style == CarStyle.Coupe);
                    bool tailGlass = back && (c.style == CarStyle.Wagon || pickup) && ax <= cw - 2 && y > belt + 1 && y < roofY - 1 && (!pickup || ax <= cw - 3);
                    if (windshield && y >= top - 1) m = glass;
                    else if (rearGlass && y >= top - 1) m = glass;
                    else if (sideWin || tailGlass) m = glass;
                    g.Set(x, y, z, m);
                }
            }
            // rain gutters along the roof edge, wipers parked at the foot of the windshield, a whip aerial on the fender
            foreach (int s in new[] { -1, 1 }) g.Repaint(s * cw, roofY - 2, cabRear + 1, s * cw, roofY - 2, c.zRoofF, trim);
            g.Repaint(-(cw - 2), belt + 1, c.zWind, -1, belt + 1, c.zWind, Pal.Solid(Pal.Black[1]));
            g.Repaint(1, belt + 1, c.zWind, cw - 2, belt + 1, c.zWind, Pal.Solid(Pal.Black[1]));
            g.Box(W - 1, belt + 1, c.zWind + 2, W - 1, belt + 6, c.zWind + 2, Pal.Solid(Pal.Chrome[1]));
            if (c.beacon) { g.Box(-2, roofY + 1, c.zRoofF - 3, 2, roofY + 1, c.zRoofF - 3, Pal.Solid(Pal.Amber)); g.Set(0, roofY + 2, c.zRoofF - 3, Pal.Solid(Pal.LightY)); }

            // ---- interior
            HollowTub(g, W, f, belt, cw, pickup ? c.zRoofR + 1 : cabRear + 1, c.zWind - 1);
            Interior(g, f, c.dashZ, c.seatZ, c.dx);
            if (c.fourDoor || c.style == CarStyle.Wagon)
            {
                var leather = Pal.Ramp(Pal.Olive, 1, 91);
                int rz = c.seatZ - 10;
                g.Box(-(cw - 2), f + 1, rz - 4, cw - 2, f + 3, rz, leather);                       // rear bench
                g.Box(-(cw - 2), f + 4, rz - 5, cw - 2, f + 10, rz - 5, leather);
            }

            // ---- doors: seams and labels; front door hinges at the A-pillar, rear door at the B-pillar
            int d0 = c.seatZ - 6, d1 = c.dashZ - 2;
            foreach (int s in new[] { -1, 1 })
            {
                g.Repaint(s * W, f + 1, d0, s * W, belt - 1, d0, Pal.Solid(Pal.Black[0]));
                g.Repaint(s * W, f + 1, d1, s * W, belt - 1, d1, Pal.Solid(Pal.Black[0]));
                g.Repaint(s * W, belt - 3, d0 + 1, s * W, belt - 3, d0 + 2, trim);                   // handle
            }
            g.RelabelWhere((p, v) => IsGlass(v) && v.label == 0, "glass");
            g.Bevel();
            g.Relabel(W - 2, f + 1, d0, W + 1, roofY - 1, d1, "door_R");
            g.Relabel(-W - 1, f + 1, d0, -(W - 2), roofY - 1, d1, "door_L");
            int r0 = c.seatZ - 15, r1 = d0 - 1;
            if (c.fourDoor)
            {
                g.Relabel(W - 2, f + 1, r0, W + 1, roofY - 1, r1, "rdoor_R");
                g.Relabel(-W - 1, f + 1, r0, -(W - 2), roofY - 1, r1, "rdoor_L");
            }

            d.Cut(g, "door_R", c.key + "_door", PartCategory.Door, new Vector3Int(W - 2, f, d1), c.mass * 0.03f);
            if (c.fourDoor) d.Cut(g, "rdoor_R", c.key + "_door_rear", PartCategory.Door, new Vector3Int(W - 2, f, r1), c.mass * 0.025f);
            d.Cut(g, "hood", c.key + "_hood", PartCategory.Hood, new Vector3Int(0, belt - 3, c.zWind + 1), c.mass * 0.015f);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, c.wheelX, c.wheelY, c.wheelZF, c.wheel, true);
            d.Socket("wheel_rear", PartCategory.Wheel, c.wheelX, c.wheelY, c.wheelZR, c.wheel, true);
            d.Socket("door", PartCategory.Door, W - 2, f, d1, c.key + "_door", true);
            if (c.fourDoor) d.Socket("door_rear", PartCategory.Door, W - 2, f, r1, c.key + "_door_rear", true);
            d.Socket("hood", PartCategory.Hood, 0, belt - 3, c.zWind + 1, c.key + "_hood");
            d.Socket("engine", PartCategory.Engine, 0, f + 1, c.zWind + 3, c.engine);
            d.Socket("radiator", PartCategory.Radiator, 0, f + 2, zF - 3, pickup ? "radiator_truck" : "radiator_car");
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, f + 1, zF + 1, c.bumperF);
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, f + 1, zR - 1, c.bumperR);
            d.Socket("armor", PartCategory.Armor, W + 1, f + 2, c.seatZ - 2, null, true);
            d.Socket("cargo", PartCategory.Cargo, 0, pickup ? f + 2 : roofY + 1, pickup ? (c.zRoofR + zR) / 2 : c.zRoofR / 2, c.cargo);
            d.Socket("roof", PartCategory.Weapon, 0, roofY + 1, c.zRoofF - 6, null);
            return d;
        }

        // ================================================================== ROUND-LAMP PICKUP (50s workhorse)
        public static VehicleDesign Pickup() => BuildCar(new CarSpec
        {
            name = "Pickup", key = "pickup", style = CarStyle.Pickup, mass = 1650f, travel = 0.3f, finalDrive = 4.1f, fuel = 80f,
            awd = true, diffLock = true, roundLamps = true,
            halfW = 12, zFront = 30, zRear = -32, floor = 5, belt = 13, noseY = 11, zWind = 9, zRoofF = 5, zRoofR = -6, roofY = 21, cabinHalfW = 10,
            dashZ = 7, seatZ = 1, dx = -5,
            wheel = "wheel_offroad", wheelX = 10, wheelY = 7, wheelZF = 21, wheelZR = -22, engine = "engine_i6", bumperR = "rear_spare_carrier",
            cargo = "cargo_jerry_rack",
            paint = Pal.Weathered(Pal.RigGreen, 0.35f, 520, 2, 6), roof = Pal.Weathered(Pal.Cream, 0.2f, 521, 3, 5), seed = 520
        });

        // ================================================================== SMALL COUPE (fastback)
        public static VehicleDesign Coupe() => BuildCar(new CarSpec
        {
            name = "Coupe", key = "coupe", style = CarStyle.Coupe, mass = 980f, travel = 0.2f, finalDrive = 3.9f, fuel = 50f,
            halfW = 11, zFront = 24, zRear = -24, floor = 3, belt = 9, noseY = 8, zWind = 6, zRoofF = 1, zRoofR = -6, roofY = 15, zTail = -19, cabinHalfW = 9,
            dashZ = 3, seatZ = -2, dx = -4,
            wheelX = 9, wheelY = 5, wheelZF = 16, wheelZR = -16, engine = "engine_i4",
            paint = Pal.Weathered(Pal.Bronze, 0.2f, 530, 2, 5), roof = Pal.Weathered(Pal.Black, 0.15f, 531, 1, 4), seed = 530
        });

        // ================================================================== LARGE 4-DOOR SEDAN
        public static VehicleDesign Sedan() => BuildCar(new CarSpec
        {
            name = "Sedan", key = "sedan", style = CarStyle.Sedan, mass = 1500f, travel = 0.26f, finalDrive = 3.5f, fuel = 75f, fourDoor = true,
            halfW = 12, zFront = 29, zRear = -29, floor = 3, belt = 10, noseY = 9, zWind = 9, zRoofF = 4, zRoofR = -12, roofY = 17, zTail = -17, cabinHalfW = 10,
            dashZ = 6, seatZ = 1, dx = -5,
            wheelX = 10, wheelY = 5, wheelZF = 19, wheelZR = -19, engine = "engine_i6",
            paint = Pal.Weathered(Pal.PaleBlue, 0.25f, 540, 2, 6), roof = Pal.Weathered(Pal.Cream, 0.2f, 541, 3, 5), seed = 540
        });

        // ================================================================== STATION WAGON (woody sides)
        public static VehicleDesign Wagon() => BuildCar(new CarSpec
        {
            name = "Wagon", key = "wagon", style = CarStyle.Wagon, mass = 1480f, travel = 0.26f, finalDrive = 3.7f, fuel = 70f, fourDoor = true,
            halfW = 12, zFront = 27, zRear = -29, floor = 3, belt = 10, noseY = 9, zWind = 8, zRoofF = 3, zRoofR = -27, roofY = 17, cabinHalfW = 10,
            dashZ = 5, seatZ = 0, dx = -5,
            wheelX = 10, wheelY = 5, wheelZF = 18, wheelZR = -19, engine = "engine_i6", cargo = "cargo_jerry_rack",
            paint = Pal.Weathered(Pal.Cream, 0.25f, 550, 2, 6), accent = Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 552), Pal.Ramp(Pal.Wood, 1, 553), 2, 2), seed = 550
        });

        // ================================================================== TOW TRUCK (pickup chassis, crane + winch, beacon)
        public static VehicleDesign TowTruck() => BuildCar(new CarSpec
        {
            name = "TowTruck", key = "towtruck", style = CarStyle.Pickup, mass = 2300f, travel = 0.3f, finalDrive = 4.6f, fuel = 90f, brake = 22000f,
            gears = new[] { 4.2f, 2.6f, 1.6f, 1.0f }, awd = true, diffLock = true, roundLamps = true, beacon = true,
            halfW = 12, zFront = 30, zRear = -34, floor = 5, belt = 13, noseY = 11, zWind = 9, zRoofF = 5, zRoofR = -6, roofY = 21, cabinHalfW = 10,
            dashZ = 7, seatZ = 1, dx = -5,
            wheel = "wheel_truck", wheelX = 10, wheelY = 7, wheelZF = 21, wheelZR = -24, engine = "engine_i6",
            bumperF = "bumper_winch", bumperR = "rear_spare_carrier", cargo = "cargo_crane",
            paint = Pal.Weathered(Pal.Sand, 0.3f, 560, 3, 6), roof = Pal.Weathered(Pal.Sand, 0.3f, 561, 4, 5), seed = 560
        });
    }
}
