using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Whole-vehicle voxel designs. Axes: +X right, +Y up, +Z forward; 1 voxel = 8 cm.
    /// Doors/hoods are cut out of the body; everything else mounts from PartLibrary.</summary>
    public static partial class VehicleDesigns
    {
        static int Abs(int v) => v < 0 ? -v : v;

        /// <summary>Seats, dashboard, steering wheel and a human-scale seated driver (label "driver").
        /// f = floor voxel layer; the driver's head spans f+10..f+12 and the eye sits at (dx, f+11, seatZ-1).</summary>
        static void Interior(VoxelGrid g, int f, int dashZ, int seatZ, int dx)
        {
            var leather = Pal.Ramp(Pal.Olive, 1, 91);
            var dark = Pal.Ramp(Pal.Black, 0, 92);
            g.Box(-8, f + 5, dashZ, 8, f + 8, dashZ + 3, dark);                                  // dashboard
            foreach (int sx in new[] { -1, 1 })
            {
                int x0 = sx > 0 ? 2 : -6, x1 = sx > 0 ? 6 : -2;
                g.Box(x0, f + 1, seatZ - 4, x1, f + 3, seatZ, leather);                          // seat cushion
                g.Box(x0, f + 4, seatZ - 5, x1, f + 11, seatZ - 5, leather);                     // backrest
            }
            var ring = Pal.Solid(Pal.Black[0]);                                                  // steering wheel
            for (int x = dx - 2; x <= dx + 2; x++) { g.Set(x, f + 7, dashZ - 1, ring); g.Set(x, f + 10, dashZ - 1, ring); }
            for (int y = f + 8; y <= f + 9; y++) { g.Set(dx - 2, y, dashZ - 1, ring); g.Set(dx + 2, y, dashZ - 1, ring); }

            int prev = g.label;
            g.Use("driver");
            var jacket = Pal.Ramp(Pal.Black, 2, 93);
            var skin = Pal.Ramp(Pal.Skin, 1, 94);
            g.Box(dx - 2, f + 4, seatZ - 4, dx + 2, f + 9, seatZ - 3, jacket);                   // torso
            g.Box(dx - 2, f + 4, seatZ - 2, dx + 2, f + 5, seatZ + 1, Pal.Ramp(Pal.Black, 1, 95)); // thighs
            g.Box(dx - 3, f + 8, seatZ - 3, dx - 3, f + 8, dashZ - 2, jacket);                   // arms to the wheel
            g.Box(dx + 3, f + 8, seatZ - 3, dx + 3, f + 8, dashZ - 2, jacket);
            g.Box(dx - 1, f + 10, seatZ - 4, dx + 1, f + 12, seatZ - 2, skin);                   // head
            g.Box(dx - 1, f + 13, seatZ - 4, dx + 1, f + 13, seatZ - 2, Pal.Solid(Pal.Black[1])); // hair
            g.Box(dx - 1, f + 10, seatZ - 4, dx + 1, f + 12, seatZ - 4, Pal.Solid(Pal.Black[1]));
            g.label = prev;
        }

        /// <summary>Hollow a body tub to one-voxel walls: removes body voxels with floorY &lt; y &lt; beltY inside |x| &lt; wallX,
        /// and the belt layer inside |x| &lt; cabinX, for z0..z1.</summary>
        static void HollowTub(VoxelGrid g, int wallX, int floorY, int beltY, int cabinX, int z0, int z1)
        {
            g.Remove(p => g.voxels[p].label == 0 && p.z >= z0 && p.z <= z1 &&
                          ((p.y > floorY && p.y < beltY && Mathf.Abs(p.x) < wallX) || (p.y == beltY && Mathf.Abs(p.x) < cabinX)));
        }

        static bool IsGlass(Vox v)
        {
            foreach (var c in Pal.Glass) if (c.r == v.color.r && c.g == v.color.g && c.b == v.color.b) return true;
            return false;
        }

        static void CutArch(VoxelGrid g, int cy, int cz, float r, int innerX)
        {
            float D(Vector3Int p) => Mathf.Sqrt((p.y - cy) * (p.y - cy) + (p.z - cz) * (p.z - cz));
            g.Remove(p => Abs(p.x) >= innerX && D(p) <= r);
            g.Recolor(p => Abs(p.x) >= innerX - 1 && Abs(p.x) <= 11 && D(p) <= r + 1.3f, Pal.Solid(Pal.Void)); // wheel-well liners
        }

        // ================================================================== V8 INTERCEPTOR (black, rusty)
        public static VehicleDesign Interceptor()
        {
            var d = new VehicleDesign { name = "Interceptor", mass = 1100, drive = VehicleDriver.Drive.Rear, travel = 0.24f, finalDrive = 3.5f, eye = new Vector3Int(4, 14, -3), hitch = new Vector3Int(0, 5, -31), fuelL = 70f, oilL = 6f, coolantL = 12f };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Black, 0.26f, 11, 1, 6);
            var chrome = Pal.Weathered(Pal.Chrome, 0.25f, 13, 2, 0);
            var glass = Pal.Ramp(Pal.Glass, 1, 14);
            var voidM = Pal.Solid(Pal.Void);

            // lower body
            for (int z = -28; z <= 26; z++)
            for (int x = -12; x <= 12; x++)
            for (int y = 3; y <= 10; y++)
            {
                int ax = Abs(x);
                if (ax == 12 && (y == 10 || y == 3)) continue;
                if (z == 26 && ax >= 11) continue;
                if (z == 25 && ax == 12) continue;
                if (z <= -27 && ax == 12) continue;
                g.Set(x, y, z, paint);
            }
            // nose cone with grille and quad headlights
            g.Box(-10, 6, 27, 10, 9, 27, paint);
            g.Box(-9, 6, 27, 9, 8, 27, Pal.Stripe(voidM, Pal.Solid(Pal.Metal[1]), 0, 2));
            foreach (int s in new[] { -1, 1 })
            foreach (int cx in new[] { 5, 8 })
            {
                g.Box(s * cx, 7, 27, s * (cx + 1), 8, 27, Pal.Solid(Pal.LightY));
                g.Set(s * cx, 8, 27, Pal.Solid(Pal.LightW));
            }
            g.Box(-11, 3, 27, 11, 3, 27, Pal.Ramp(Pal.Black, 0));          // chin spoiler
            g.Box(-12, 9, -20, -12, 9, 7, chrome); g.Box(12, 9, -20, 12, 9, 7, chrome); // belt trim
            // tail lights & plate
            foreach (int s in new[] { -1, 1 }) g.Box(s * 6, 7, -28, s * 10, 8, -28, p => p.x % 2 == 0 ? Pal.TailR : Pal.Hex("7a1a10"));
            g.Box(-2, 5, -28, 2, 6, -28, p => Pal.Hash(p, 3) > 0.6f ? Pal.Black[1] : Pal.Sand[4]);

            // wheel arches + flares
            foreach (int zc in new[] { 18, -18 })
            {
                CutArch(g, 5, zc, 6.6f, 5);
                for (int z = zc - 9; z <= zc + 9; z++)
                for (int y = 5; y <= 13; y++)
                {
                    float r = Mathf.Sqrt((y - 5) * (y - 5) + (z - zc) * (z - zc));
                    if (r > 6.6f && r <= 7.9f) { g.Set(13, y, z, Pal.Ramp(Pal.Black, 1, 15)); g.Set(-13, y, z, Pal.Ramp(Pal.Black, 1, 15)); }
                }
            }

            // engine bay
            g.Repaint(-9, 4, 9, 9, 10, 25, Pal.Ramp(Pal.Metal, 0, 16));
            g.ClearBox(-8, 5, 10, 8, 10, 24);

            // cabin shell: windshield z2..7, roof z-9..1, fastback z-20..-10
            int Roof(int z) => z >= -9 ? Mathf.Min(17, 11 + (8 - z)) : Mathf.Max(11, 17 - Mathf.CeilToInt((-9 - z) * 0.55f));
            for (int z = -20; z <= 7; z++)
            for (int x = -9; x <= 9; x++)
            {
                int top = Roof(z);
                for (int y = 11; y <= top; y++)
                {
                    int ax = Abs(x);
                    bool shell = y >= top - 1 || ax == 9 || z == -20 || z == 7;
                    if (!shell) continue;
                    bool sloped = z >= 2 || z <= -10;
                    VoxMat m = paint;
                    if (y >= top - 1 && sloped && ax <= 7 && y > 11) m = glass;
                    if (ax == 9 && z >= -17 && z <= -11 && y >= 12 && y <= top - 2) m = glass;   // quarter windows
                    if (ax == 9 && z >= -7 && z <= 1 && y >= 12 && y <= top - 2) continue;       // open door windows
                    g.Set(x, y, z, m);
                }
            }
            HollowTub(g, 12, 3, 10, 9, -19, 6);
            Interior(g, 3, 4, -2, 4);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 10, 12, 3, s * 11, 12, 3, chrome);   // mirrors

            // door seams + handles (both sides), then label doors
            foreach (int s in new[] { -1, 1 })
            {
                g.Repaint(s * 12, 4, -8, s * 12, 8, -8, Pal.Solid(Pal.Black[0]));
                g.Repaint(s * 12, 4, 2, s * 12, 8, 2, Pal.Solid(Pal.Black[0]));
                g.Repaint(s * 12, 7, -6, s * 12, 7, -5, Pal.Solid(Pal.Chrome[2]));
            }

            // hood (with blower hole)
            g.Use("hood");
            g.Box(-10, 11, 8, 10, 11, 25, Pal.Weathered(Pal.Black, 0.34f, 17, 1, 0));
            g.ClearBox(-3, 11, 13, 3, 11, 19);
            g.label = 0;

            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            g.Relabel(9, 4, -8, 13, 16, 2, "door_R");
            g.Relabel(-13, 4, -8, -9, 16, 2, "door_L");

            d.Cut(g, "door_R", "interceptor_door", PartCategory.Door, new Vector3Int(9, 3, 2), 40);
            d.Cut(g, "hood", "interceptor_hood", PartCategory.Hood, new Vector3Int(0, 11, 8), 18);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 9, 5, 18, "wheel_street", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 9, 5, -18, "wheel_street", true);
            d.Socket("door", PartCategory.Door, 9, 3, 2, "interceptor_door", true);
            d.Socket("exhaust", PartCategory.Exhaust, 13, 3, 9, "exhaust_side_pipes", true);
            d.Socket("hood", PartCategory.Hood, 0, 11, 8, "interceptor_hood");
            d.Socket("engine", PartCategory.Engine, 0, 5, 11, "engine_v8_blower");
            d.Socket("radiator", PartCategory.Radiator, 0, 5, 23, "radiator_car");
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 4, 27, "bumper_chrome");
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, 4, -29, "bumper_rear_chrome");
            d.Socket("spoiler", PartCategory.Spoiler, 0, 11, -28, "spoiler_rear");
            d.Socket("armor", PartCategory.Armor, 13, 5, -3, null, true);
            d.Socket("cargo", PartCategory.Cargo, 0, 11, -24, "cargo_twin_fuel_tanks");
            d.Socket("roof", PartCategory.Weapon, 0, 18, -4, null);
            d.Socket("lights", PartCategory.Lights, 0, 18, 1, null);
            d.Socket("snorkel", PartCategory.Snorkel, 13, 9, 6, null);
            d.Socket("steps", PartCategory.Steps, 12, 2, -3, null, true);
            return d;
        }

        // ================================================================== SCAVENGER (rusty armoured 4x4)
        public static VehicleDesign Scavenger()
        {
            var d = new VehicleDesign { name = "Scavenger", mass = 1600, drive = VehicleDriver.Drive.All, travel = 0.36f, finalDrive = 4.3f, eye = new Vector3Int(4, 16, -2), hitch = new Vector3Int(0, 6, -30), awdSelectable = true, diffLock = true, fuelL = 90f, oilL = 7f, coolantL = 14f };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Olive, 0.34f, 21, 2, 8);
            var glass = Pal.Ramp(Pal.Glass, 1, 24);
            var voidM = Pal.Solid(Pal.Void);
            var steel = Pal.Ramp(Pal.Metal, 1, 25);

            // lower body
            for (int z = -25; z <= 23; z++)
            for (int x = -12; x <= 12; x++)
            for (int y = 5; y <= 13; y++)
            {
                int ax = Abs(x);
                if (ax == 12 && y == 13) continue;
                if ((z == 23 || z == -25) && ax == 12) continue;
                g.Set(x, y, z, paint);
            }
            // grille, headlights, indicators
            g.Box(-10, 6, 24, 10, 12, 24, Pal.Stripe(voidM, Pal.Ramp(Pal.Olive, 1), 0, 2));
            g.Box(-10, 13, 24, 10, 13, 24, paint);
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 8, 10, 24, s * 9, 11, 24, Pal.Solid(Pal.LightY));
                g.Set(s * 8, 11, 24, Pal.Solid(Pal.LightW));
                g.Set(s * 11, 8, 24, Pal.Solid(Pal.Amber));
                g.Box(s * 9, 11, -26, s * 11, 12, -26, Pal.Solid(Pal.TailR));
            }

            foreach (int zc in new[] { 17, -17 })
            {
                CutArch(g, 6, zc, 7.6f, 5);
                for (int z = zc - 10; z <= zc + 10; z++)
                for (int y = 6; y <= 15; y++)
                {
                    float r = Mathf.Sqrt((y - 6) * (y - 6) + (z - zc) * (z - zc));
                    if (r > 7.6f && r <= 9.0f)
                    {
                        var m = Pal.Weathered(Pal.Metal, 0.5f, 26, 2, 0);
                        g.Set(13, y, z, m); g.Set(-13, y, z, m); g.Set(14, y, z, m); g.Set(-14, y, z, m);
                    }
                }
            }

            g.Repaint(-9, 5, 9, 9, 13, 23, Pal.Ramp(Pal.Metal, 0, 27));
            g.ClearBox(-8, 6, 10, 8, 13, 22);

            // cab: windshield z3..8, roof z-10..2 at y21
            int Roof(int z) => z >= 3 ? 21 - Mathf.RoundToInt((z - 3) * 1.4f) : 21;
            for (int z = -10; z <= 8; z++)
            for (int x = -11; x <= 11; x++)
            {
                int top = Roof(z);
                for (int y = 14; y <= top; y++)
                {
                    int ax = Abs(x);
                    bool shell = y >= top - 1 || ax == 11 || z == -10 || z == 8;
                    if (!shell) continue;
                    VoxMat m = paint;
                    if (z >= 3 && y >= top - 1 && ax <= 9 && y > 14)
                    {
                        float t = (y - 14) / 7f, xl = -9 + 18 * t;
                        m = Mathf.Abs(x - xl) < 0.8f || Mathf.Abs(x + xl) < 0.8f ? steel : glass;  // X-braced windshield
                    }
                    if (ax == 11 && z >= -8 && z <= 1 && y >= 15 && y <= top - 2) continue;       // open windows
                    g.Set(x, y, z, m);
                }
            }
            // rear armoured box
            for (int z = -25; z <= -11; z++)
            for (int x = -12; x <= 12; x++)
            for (int y = 14; y <= 20; y++)
            {
                int ax = Abs(x);
                if (ax == 12 && y == 20) continue;
                VoxMat m = paint;
                if (ax == 12 && y >= 16 && y <= 17 && z >= -22 && z <= -14) m = p => (p.z % 2 == 0) ? Pal.Void : Pal.Metal[1]; // gun slits
                else if (ax == 12 && (z % 6 == 0)) m = Pal.Ramp(Pal.Rust, 1, 28);                         // panel seams
                g.Set(x, y, z, m);
            }
            g.Box(-6, 16, -26, 6, 18, -26, glass);
            g.Box(-12, 5, -26, 12, 13, -26, paint);
            // rivet rows
            for (int z = -24; z <= 22; z += 3) { g.Repaint(12, 12, z, 12, 12, z, Pal.Solid(Pal.Chrome[1])); g.Repaint(-12, 12, z, -12, 12, z, Pal.Solid(Pal.Chrome[1])); }
            HollowTub(g, 12, 5, 13, 11, -9, 7);
            HollowTub(g, 12, 5, 20, 0, -24, -11);
            Interior(g, 5, 5, -1, 4);
            // snorkel + mirrors
            g.Tube(new Vector3(13, 10, 7), new Vector3(13, 22, 4), 0.5f, Pal.Ramp(Pal.Black, 1, 29));
            g.Box(13, 22, 5, 13, 22, 6, Pal.Solid(Pal.Black[0]));
            g.Box(-13, 16, 4, -12, 16, 4, steel);

            foreach (int s in new[] { -1, 1 })
            {
                g.Repaint(s * 12, 6, -9, s * 12, 12, -9, Pal.Solid(Pal.Black[0]));
                g.Repaint(s * 12, 6, 2, s * 12, 12, 2, Pal.Solid(Pal.Black[0]));
                g.Repaint(s * 12, 11, -7, s * 12, 11, -6, Pal.Solid(Pal.Chrome[2]));
            }

            g.Use("hood");
            g.Box(-11, 14, 9, 11, 14, 23, paint);
            foreach (int s in new[] { -1, 1 }) for (int z = 12; z <= 18; z += 2) g.Box(s * 5, 14, z, s * 8, 14, z, voidM);
            g.label = 0;

            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            g.Relabel(10, 6, -9, 12, 20, 2, "door_R");
            g.Relabel(-12, 6, -9, -10, 20, 2, "door_L");

            d.Cut(g, "door_R", "scavenger_door", PartCategory.Door, new Vector3Int(10, 5, 2), 70, 2);
            d.Cut(g, "hood", "scavenger_hood", PartCategory.Hood, new Vector3Int(0, 14, 9), 30, 2);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 9, 6, 17, "wheel_offroad", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 9, 6, -17, "wheel_offroad", true);
            d.Socket("door", PartCategory.Door, 10, 5, 2, "scavenger_door", true);
            d.Socket("exhaust", PartCategory.Exhaust, 14, 4, 8, null, true);
            d.Socket("hood", PartCategory.Hood, 0, 14, 9, "scavenger_hood");
            d.Socket("engine", PartCategory.Engine, 0, 6, 10, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 6, 22, "radiator_car");
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 4, 25, "bumper_bull_bar");
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, 5, -27, "rear_spare_carrier");
            d.Socket("cargo", PartCategory.Cargo, 0, 21, -18, "cargo_jerry_rack");
            d.Socket("armor", PartCategory.Armor, 13, 8, 0, "armor_plate", true);
            d.Socket("roof", PartCategory.Weapon, 0, 22, -3, "weapon_turret_cannon");
            d.Socket("lights", PartCategory.Lights, 0, 22, 2, null);
            d.Socket("snorkel", PartCategory.Snorkel, 13, 10, 5, "snorkel");
            d.Socket("steps", PartCategory.Steps, 12, 4, 1, null, true);
            return d;
        }
        // ================================================================== TRABANT P50 (two-tone, round lamps)
        public static VehicleDesign Trabant()
        {
            var d = new VehicleDesign
            {
                name = "Trabant", mass = 620, drive = VehicleDriver.Drive.Front, travel = 0.2f, finalDrive = 4.1f,
                gears = new[] { 3.8f, 2.2f, 1.4f, 1.0f }, frequency = 1.9f, brakeForce = 9000f,
                eye = new Vector3Int(-4, 14, -5), hitch = new Vector3Int(0, 4, -23),
                fuelL = 24f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true
            };
            var g = new VoxelGrid();
            var blue = Pal.Weathered(Pal.PaleBlue, 0.12f, 301, 2, 5);
            var white = Pal.Weathered(Pal.Cream, 0.1f, 302, 3, 5);
            var glass = Pal.Ramp(Pal.Glass, 1, 303);
            var voidM = Pal.Solid(Pal.Void);

            // P601 lower body: flat hood with a rolled nose, tall flat front, shoulder crease at y 9, short flat boot.
            var crease = Pal.Weathered(Pal.PaleBlue, 0.1f, 305, 3, 5);
            for (int z = -21; z <= 20; z++)
            for (int x = -9; x <= 9; x++)
            for (int y = 3; y <= 10; y++)
            {
                int ax = Abs(x);
                if (ax == 9 && (y == 3 || y == 10)) continue;                              // rounded side edges
                if ((z == 20 || z == -21) && (ax >= 8 || y == 3 || y == 10)) continue;     // inset front and rear faces
                if ((z == 19 || z == -20) && ax == 9) continue;                             // rounded plan corners
                if (z == 20 && y == 9 && ax >= 6) continue;                                 // nose rolls into the hood
                g.Set(x, y, z, y == 9 ? crease : blue);
            }
            foreach (int s in new[] { -1, 1 })
            {
                // round headlamps set into the front corners (chrome ring, lens), indicator below
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    if (Abs(dx) + Abs(dy) < 2) g.Set(s * 6 + dx, 7 + dy, 20, Pal.Solid(Pal.Chrome[2]));
                g.Set(s * 6, 7, 20, Pal.Solid(Pal.LightW)); g.Set(s * 6, 7, 21, Pal.Solid(Pal.LightY));
                g.Set(s * 6, 5, 20, Pal.Solid(Pal.Amber));
                // upright tail-light clusters, red over amber
                g.Box(s * 7, 6, -21, s * 7, 8, -21, Pal.Solid(Pal.TailR));
                g.Set(s * 7, 5, -21, Pal.Solid(Pal.Amber));
            }
            g.Box(-4, 7, 20, 4, 8, 20, p => p.y == 8 ? Pal.Chrome[2] : Pal.Chrome[1]);   // full-width grille band between the lamps
            for (int x = -3; x <= 3; x += 2) g.Set(x, 7, 20, Pal.Solid(Pal.Void));      // grille slots
            g.Set(0, 9, 20, Pal.Solid(Pal.Chrome[3]));                                   // badge on the nose
            g.Repaint(-9, 6, -20, -9, 6, 19, Pal.Solid(Pal.Chrome[2])); g.Repaint(9, 6, -20, 9, 6, 19, Pal.Solid(Pal.Chrome[2]));   // side trim
            g.Box(-3, 5, -21, 3, 6, -21, Pal.Solid(Pal.Black[1]));                      // number-plate recess
            foreach (int zc in new[] { 13, -13 }) CutArch(g, 4, zc, 5.2f, 5);

            // engine bay under the front lid
            g.Repaint(-7, 4, 7, 7, 9, 19, Pal.Ramp(Pal.Metal, 0, 304));
            g.ClearBox(-6, 4, 8, 6, 8, 18);

            int Roof(int z) => z >= 1 ? 16 - Mathf.RoundToInt((z - 1) * 1.25f) : z >= -10 ? 17 : 17 - Mathf.RoundToInt((-10 - z) * 1.5f);
            for (int z = -14; z <= 5; z++)
            for (int x = -8; x <= 8; x++)
            {
                int top = Roof(z);
                for (int y = 11; y <= top; y++)
                {
                    int ax = Abs(x);
                    bool shell = y >= top - 1 || ax == 8 || z == -14 || z == 5;
                    if (!shell) continue;
                    VoxMat m = white;
                    bool sloped = z >= 1 || z <= -11;
                    if (y >= top - 1 && sloped && ax <= 6 && y > 11) m = glass;
                    if (ax == 8 && y >= 12 && y <= top - 2 && ((z >= -3 && z <= 3) || (z >= -10 && z <= -5))) m = glass;
                    g.Set(x, y, z, m);
                }
            }
            HollowTub(g, 9, 3, 10, 8, -13, 4);
            Interior(g, 3, 2, -4, -4);
            foreach (int s in new[] { -1, 1 })
            {
                g.Repaint(s * 9, 4, -4, s * 9, 9, -4, Pal.Solid(Pal.Black[1]));
                g.Repaint(s * 9, 4, 3, s * 9, 9, 3, Pal.Solid(Pal.Black[1]));
                g.Repaint(s * 9, 9, -2, s * 9, 9, -1, Pal.Solid(Pal.Chrome[2]));
                g.Box(s * 9, 11, 5, s * 10, 11, 5, Pal.Solid(Pal.Chrome[2]));                 // mirrors
            }

            g.Use("hood");
            g.Relabel(-6, 9, 7, 6, 10, 19, "hood");
            g.label = 0;
            g.Relabel(8, 4, -4, 9, 16, 3, "door_R");
            g.Relabel(-9, 4, -4, -8, 16, 3, "door_L");
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();

            d.Cut(g, "door_R", "trabant_door", PartCategory.Door, new Vector3Int(8, 4, 3), 18);
            d.Cut(g, "hood", "trabant_hood", PartCategory.Hood, new Vector3Int(0, 9, 7), 8);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 7, 4, 13, "wheel_small", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 7, 4, -13, "wheel_small", true);
            d.Socket("door", PartCategory.Door, 8, 4, 3, "trabant_door", true);
            d.Socket("hood", PartCategory.Hood, 0, 9, 7, "trabant_hood");
            d.Socket("engine", PartCategory.Engine, 0, 4, 8, "engine_2stroke", false, 2);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 3, 21, "bumper_trabant_front");
            d.Socket("bumper_rear", PartCategory.RearBumper, 0, 3, -22, "bumper_trabant_rear");
            d.Socket("armor", PartCategory.Armor, 10, 4, -2, null, true, 1);
            d.Socket("roof", PartCategory.Weapon, 0, 18, -4, null);
            d.Socket("lights", PartCategory.Lights, 0, 18, 0, null);
            d.Socket("snorkel", PartCategory.Snorkel, 10, 7, 6, null);
            d.Socket("steps", PartCategory.Steps, 9, 2, 0, null, true);
            return d;
        }

        // ================================================================== HAULER (war-rig tractor with walk-in living module)
        public static VehicleDesign Hauler()
        {
            var d = new VehicleDesign
            {
                name = "Hauler", mass = 6500, drive = VehicleDriver.Drive.Rear, travel = 0.3f, finalDrive = 5.2f,
                gears = new[] { 5.5f, 3.6f, 2.4f, 1.7f, 1.25f, 1.0f }, frequency = 1.4f, brakeForce = 90000f, maxSteer = 28f,
                eye = new Vector3Int(-4, 27, 13), hitch = new Vector3Int(0, 11, -62),
                awdSelectable = true, diffLock = true, fuelL = 400f, oilL = 30f, coolantL = 45f
            };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.RigGreen, 0.32f, 131, 2, 12);
            var steel = Pal.Ramp(Pal.Metal, 1, 132);
            var glass = Pal.Ramp(Pal.Glass, 1, 133);
            var chrome = Pal.Weathered(Pal.Chrome, 0.3f, 134, 2, 0);
            var voidM = Pal.Solid(Pal.Void);

            // frame
            g.Box(-8, 8, -60, -6, 11, 54, steel); g.Box(6, 8, -60, 8, 11, 54, steel);
            for (int z = -56; z <= 50; z += 8) g.Box(-6, 9, z, 6, 10, z, steel);
            // deck: living module + cab share one floor
            g.Box(-15, 15, -58, 15, 16, 25, Pal.Weathered(Pal.Metal, 0.4f, 135, 2, 20));
            for (int z = -58; z <= 25; z++)
            for (int y = 17; y <= 47; y++)
            foreach (int x in new[] { -15, 15 })
            {
                VoxMat m = z % 10 == 0 ? Pal.Ramp(Pal.RigGreen, 0, 136) : paint;
                if (y >= 32 && y <= 36 && ((z >= -50 && z <= -42) || (z >= -22 && z <= -14))) m = glass;   // module windows
                if (y >= 30 && y <= 40 && z >= 10 && z <= 18) m = glass;                                   // cab door windows
                g.Set(x, y, z, m);
            }
            for (int x = -14; x <= 14; x++)
            for (int y = 17; y <= 47; y++)
            {
                g.Set(x, y, -58, paint);                                                                   // rear wall (door cut later)
                VoxMat front = y >= 24 && y <= 44 && Abs(x) <= 13 ? glass : paint;
                g.Set(x, y, 25, front);                                                                    // cab front / windscreen
            }
            foreach (int x in new[] { -14, 14 }) g.Box(x, 17, 3, x, 47, 3, steel);                         // cab/module frame
            g.Box(-14, 44, 3, 14, 47, 3, steel);
            g.Box(-15, 48, -58, 15, 49, 25, paint);                                                        // roof
            g.Box(-15, 46, 26, 15, 47, 27, paint);                                                         // sun visor
            // hood + grille + lamps
            for (int z = 26; z <= 52; z++)
            for (int x = -11; x <= 11; x++)
            for (int y = 13; y <= 24; y++)
            {
                int ax = Abs(x);
                if (ax == 11 && y == 24) continue;
                if (z == 52 && ax >= 10) continue;
                g.Set(x, y, z, paint);
            }
            g.Repaint(-10, 14, 27, 10, 23, 51, Pal.Ramp(Pal.Metal, 0, 137));
            g.ClearBox(-9, 14, 28, 9, 23, 50);
            g.Box(-9, 13, 53, 9, 23, 53, Pal.Stripe(voidM, chrome, 0, 2));
            g.Box(-10, 24, 53, 10, 24, 53, chrome);
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 10, 19, 53, s * 11, 21, 53, Pal.Solid(Pal.LightY)); g.Set(s * 10, 21, 53, Pal.Solid(Pal.LightW));
                // front fenders
                for (int z = 32; z <= 52; z++)
                for (int y = 7; y <= 17; y++)
                {
                    float r = Mathf.Sqrt((y - 7) * (y - 7) + (z - 42) * (z - 42));
                    if (r > 7.4f && r <= 9f) for (int x = 11; x <= 15; x++) g.Set(s * x, y, z, Pal.Ramp(Pal.Black, 1, 138));
                }
                g.Box(s * 15, 12, 4, s * 16, 13, 24, steel);                                               // running board
                g.Box(s * 16, 14, 10, s * 16, 14, 14, steel);                                              // step
                g.CylZ(s * 12, 11, 2.4f, -16, 28, chrome);                                                 // fuel tanks
                g.Box(s * 11, 3, -49, s * 15, 12, -49, Pal.Ramp(Pal.Black, 0, 139));                       // mud flaps
                g.Box(s * 12, 17, -58, s * 14, 18, -58, Pal.Solid(Pal.TailR));
            }
            g.Box(-15, 10, -60, 15, 12, -59, steel);                                                       // rear beam
            g.Box(-1, 10, -62, 1, 12, -61, Pal.Solid(Pal.Metal[3]));                                       // tow hitch
            for (int y = 18; y <= 46; y += 3) g.Box(11, y, -59, 13, y, -59, steel);                        // ladder
            g.Box(11, 17, -59, 11, 47, -59, steel); g.Box(13, 17, -59, 13, 47, -59, steel);

            Interior(g, 16, 21, 14, -4);

            g.Relabel(15, 17, 8, 15, 42, 20, "door_R");
            g.Relabel(-15, 17, 8, -15, 42, 20, "door_L");
            g.Relabel(-4, 17, -58, 4, 42, -58, "rear_door");
            g.Relabel(-10, 24, 26, 10, 24, 52, "hood");
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.RelabelWhere((p, v) => v.label == 0 && p.y >= 48, "roof");
            g.Bevel();

            d.Cut(g, "door_R", "hauler_door", PartCategory.Door, new Vector3Int(15, 17, 20), 60, 2);
            d.Cut(g, "rear_door", "hauler_rear_door", PartCategory.Door, new Vector3Int(0, 17, -58), 70, 2);
            d.Cut(g, "hood", "hauler_hood", PartCategory.Hood, new Vector3Int(0, 24, 26), 60, 2);
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.roof = g.Extract("roof", Vector3Int.zero);

            d.Socket("wheel_front", PartCategory.Wheel, 11, 7, 42, "wheel_truck", true);
            d.Socket("wheel_mid", PartCategory.Wheel, 11, 7, -26, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 11, 7, -40, "wheel_truck", true);
            d.Socket("door", PartCategory.Door, 15, 17, 20, "hauler_door", true);
            d.Socket("door_rear", PartCategory.Door, 0, 17, -58, "hauler_rear_door");
            d.Socket("hood", PartCategory.Hood, 0, 24, 26, "hauler_hood");
            d.Socket("engine", PartCategory.Engine, 0, 14, 29, "engine_truck_diesel");
            d.Socket("radiator", PartCategory.Radiator, 0, 14, 46, "radiator_truck");
            d.Socket("exhaust", PartCategory.Exhaust, 16, 20, 22, "exhaust_stack", true);
            d.Socket("armor", PartCategory.Armor, 16, 10, 2, "armor_spikes", true);
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 3, 54, "bumper_plow");
            d.Socket("roof", PartCategory.Weapon, 0, 50, 0, "weapon_turret_cannon");
            d.Socket("cargo", PartCategory.Cargo, 0, 50, -40, "cargo_jerry_rack");
            d.Socket("lights", PartCategory.Lights, 0, 50, 22, "lights_bar");
            d.Socket("snorkel", PartCategory.Snorkel, 16, 20, 0, null);
            d.Socket("steps", PartCategory.Steps, 16, 12, 6, null);

            d.colliders.Add(VehicleDesign.Box(-9, 8, -61, 9, 14, 54));      // chassis
            d.colliders.Add(VehicleDesign.Box(-15, 15, -58, 15, 16, 25));   // floor
            d.colliders.Add(VehicleDesign.Box(-15, 17, -58, -15, 47, 25));  // walls
            d.colliders.Add(VehicleDesign.Box(15, 17, -58, 15, 47, 25));
            d.colliders.Add(VehicleDesign.Box(-14, 17, -58, -5, 47, -58));  // rear wall around the door
            d.colliders.Add(VehicleDesign.Box(5, 17, -58, 14, 47, -58));
            d.colliders.Add(VehicleDesign.Box(-4, 43, -58, 4, 47, -58));
            d.colliders.Add(VehicleDesign.Box(-14, 17, 25, 14, 47, 25));    // cab front
            d.colliders.Add(VehicleDesign.Box(-15, 48, -58, 15, 49, 25));   // roof (index 8, disabled in cutaway)
            d.colliders.Add(VehicleDesign.Box(-11, 13, 26, 11, 24, 53));    // hood

            var i = d.interior = new InteriorDesign { floorY = 16.5f, ceilingY = 47.5f, min = new Vector2(-11f, -54.5f), max = new Vector2(11f, 19f) };
            i.doors.Add((new Vector2(0, -54), new Vector3(0, 0, -66)));
            i.doors.Add((new Vector2(11, 14), new Vector3(20, 0, 14)));
            i.doors.Add((new Vector2(-11, 14), new Vector3(-20, 0, 14)));
            i.seat = new Vector2(-4, 12);
            i.stand = new Vector2(-4, 5);
            i.obstacles.Add(VehicleDesign.Box(-7, 17, 9, -1, 27, 14));       // seats
            i.obstacles.Add(VehicleDesign.Box(1, 17, 9, 7, 27, 14));
            i.obstacles.Add(VehicleDesign.Box(-9, 17, 20, 9, 24, 24));       // dashboard
            i.furniture.Add(("bed", new Vector3(-9, 16.5f, -46), new Vector3(0, 0, 0)));
            i.furniture.Add(("fridge", new Vector3(11, 16.5f, -30), new Vector3(0, -90, 0)));
            i.furniture.Add(("workbench", new Vector3(10, 16.5f, -46), new Vector3(0, -90, 0)));
            i.furniture.Add(("locker", new Vector3(-12, 16.5f, -28), new Vector3(0, 90, 0)));
            i.furniture.Add(("stove", new Vector3(11, 16.5f, -16), new Vector3(0, -90, 0)));
            i.furniture.Add(("crate", new Vector3(-10, 16.5f, -14), new Vector3(0, 0, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 47.5f, -30), new Vector3(180, 0, 0)));
            i.furniture.Add(("lamp", new Vector3(0, 47.5f, -8), new Vector3(180, 0, 0)));
            i.furniture.Add(("shelf", new Vector3(-14.5f, 36f, -46), new Vector3(90, 90, 0)));
            return d;
        }

        // ================================================================== TRAILERS
        public static VehicleDesign Tanker()
        {
            var d = new VehicleDesign { name = "Tanker", mass = 2600, driveable = false, fuelL = 2000f, oilL = 0f, coolantL = 0f, usesCoolant = false, maxSteer = 0f, travel = 0.25f, frequency = 1.5f, brakeForce = 40000f, coupler = new Vector3Int(0, 11, 34), pumpLps = 25f };
            var g = new VoxelGrid();
            var tank = Pal.Weathered(Pal.Black, 0.35f, 201, 2, 14);
            var steel = Pal.Ramp(Pal.Metal, 1, 202);
            g.Box(-8, 9, -54, -6, 12, 22, steel); g.Box(6, 9, -54, 8, 12, 22, steel);
            for (int z = -49; z <= 17; z++)
                g.CylZ(0, 25, 12f, z, z, z % 8 == 0 ? Pal.Ramp(Pal.Rust, 1, 203) : tank, 10.5f);   // shell with rust bands
            g.CylZ(0, 25, 11.5f, -50, -50, tank); g.CylZ(0, 25, 11.5f, 18, 18, tank);                 // end caps
            g.Box(-3, 37, -40, 3, 37, 8, Pal.Stripe(Pal.Solid(Pal.Metal[2]), voidM(), 2, 2));             // catwalk
            g.CylY(0, -16, 2f, 37, 38, Pal.Ramp(Pal.Metal, 2)); g.CylY(0, 4, 2f, 37, 38, Pal.Ramp(Pal.Metal, 2)); // hatches
            for (int y = 14; y <= 36; y += 3) g.Box(-13, y, -51, -11, y, -51, steel);                     // ladder
            g.Tube(new Vector3(0, 11, 20), new Vector3(0, 11, 33), 1f, steel);                           // drawbar
            g.Box(-1, 10, 34, 1, 12, 34, Pal.Solid(Pal.Metal[3]));
            g.Use("legs");
            g.Box(-11, 1, 10, -10, 12, 11, steel); g.Box(10, 1, 10, 11, 12, 11, steel);                   // landing legs
            g.Box(-12, 1, 9, -9, 1, 12, steel); g.Box(9, 1, 9, 12, 1, 12, steel);
            g.label = 0;
            g.Box(-14, 12, -52, 14, 13, -52, Pal.Solid(Pal.TailR));
            g.Bevel();
            d.legs = g.Take("legs", Vector3Int.zero);   // legs leave the body mesh/colliders: they wind up when hitched
            d.body = g;
            d.Socket("wheel_mid", PartCategory.Wheel, 11, 7, -30, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 11, 7, -44, "wheel_truck", true);
            d.Socket("roof", PartCategory.Weapon, 0, 39, -30, null);
            return d;
        }

        static VoxMat voidM() => Pal.Solid(Pal.Void);

        /// <summary>Single-axle bowser: 450 L, tows behind anything with a hitch.</summary>
        public static VehicleDesign TankerSmall()
        {
            var d = new VehicleDesign { name = "TankerSmall", mass = 420, driveable = false, fuelL = 450f, oilL = 0f, coolantL = 0f, usesCoolant = false, maxSteer = 0f, travel = 0.18f, frequency = 2f, brakeForce = 6000f, coupler = new Vector3Int(0, 7, 22), pumpLps = 12f };
            var g = new VoxelGrid();
            var tank = Pal.Weathered(Pal.Olive, 0.45f, 221, 2, 10);
            var steel = Pal.Ramp(Pal.Metal, 1, 222);
            g.Box(-5, 5, -22, -4, 6, 6, steel); g.Box(4, 5, -22, 5, 6, 6, steel);                          // frame rails
            for (int z = -20; z <= 4; z++)
                g.CylZ(0, 13, 7f, z, z, z % 6 == 0 ? Pal.Ramp(Pal.Rust, 1, 223) : tank, 6f);               // shell with rust bands
            g.CylZ(0, 13, 6.5f, -21, -21, tank); g.CylZ(0, 13, 6.5f, 5, 5, tank);                         // end caps
            g.CylY(0, -8, 1.5f, 20, 21, Pal.Ramp(Pal.Metal, 2));                                          // filler hatch
            g.Box(-1, 6, -24, 1, 9, -22, Pal.Ramp(Pal.Black, 1, 224));                                     // pump box
            g.Set(0, 8, -25, Pal.Solid(Pal.Chrome[1]));                                                    // outlet
            foreach (int s in new[] { -1, 1 }) g.Box(s * 8, 9, -13, s * 10, 9, -5, Pal.Ramp(Pal.Black, 1, 225));  // fenders
            g.Tube(new Vector3(0, 6, 5), new Vector3(0, 6, 21), 0.6f, steel);                             // drawbar
            g.Box(-1, 6, 22, 1, 8, 22, Pal.Solid(Pal.Metal[3]));
            g.Use("legs");
            g.Box(0, 1, 16, 0, 5, 16, steel); g.Box(-1, 1, 15, 1, 1, 17, steel);                          // jockey leg
            g.label = 0;
            g.Box(-5, 7, -23, -3, 8, -23, Pal.Solid(Pal.TailR)); g.Box(3, 7, -23, 5, 8, -23, Pal.Solid(Pal.TailR));
            g.Bevel();
            d.legs = g.Take("legs", Vector3Int.zero);   // legs leave the body mesh/colliders: they wind up when hitched
            d.body = g;
            d.Socket("wheel", PartCategory.Wheel, 7, 4, -9, "wheel_small", true);
            return d;
        }

        public static VehicleDesign CargoTrailer()
        {
            var d = new VehicleDesign { name = "CargoTrailer", mass = 260, driveable = false, fuelL = 0f, oilL = 0f, coolantL = 0f, usesCoolant = false, maxSteer = 0f, travel = 0.18f, frequency = 2f, brakeForce = 4000f, coupler = new Vector3Int(0, 6, 20) };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Olive, 0.4f, 211, 2, 8);
            var steel = Pal.Ramp(Pal.Metal, 1, 212);
            g.Box(-10, 5, -16, 10, 6, 6, steel);                                  // bed
            for (int z = -16; z <= 6; z++) for (int y = 7; y <= 12; y++) { g.Set(-10, y, z, paint); g.Set(10, y, z, paint); }
            for (int x = -9; x <= 9; x++) for (int y = 7; y <= 12; y++) { g.Set(x, y, -16, paint); g.Set(x, y, 6, paint); }
            g.Box(-10, 13, -16, 10, 13, -16, steel); g.Box(-10, 13, 6, 10, 13, 6, steel);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 11, 9, -10, s * 13, 9, 0, Pal.Ramp(Pal.Black, 1, 213));  // fenders
            g.Tube(new Vector3(0, 6, 6), new Vector3(0, 6, 19), 0.6f, steel);
            g.Use("legs");
            g.Box(0, 1, 14, 0, 5, 14, steel); g.Box(-1, 1, 13, 1, 1, 15, steel);  // jockey leg
            g.label = 0;
            g.Box(-9, 7, -17, -7, 8, -17, Pal.Solid(Pal.TailR)); g.Box(7, 7, -17, 9, 8, -17, Pal.Solid(Pal.TailR));
            g.Bevel();
            d.legs = g.Take("legs", Vector3Int.zero);   // legs leave the body mesh/colliders: they wind up when hitched
            d.body = g;
            d.Socket("wheel", PartCategory.Wheel, 11, 4, -5, "wheel_small", true);
            d.Socket("cargo", PartCategory.Cargo, 0, 7, -5, null);
            return d;
        }
    }
}
