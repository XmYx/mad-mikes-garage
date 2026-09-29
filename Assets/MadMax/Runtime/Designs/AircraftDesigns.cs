using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Aircraft parts (roadmap 25): a 50 hp two-stroke pusher and small tundra tyres.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> Aircraft()
        {
            // two-stroke pusher: twin finned jugs, tuned pipe, prop flange at the back
            var g = new VoxelGrid();
            g.Box(-2, 0, -2, 2, 2, 2, Pal.Weathered(Pal.Metal, 0.2f, 2501, 2, 0));
            foreach (int s in new[] { -1, 1 }) g.Box(s * 2, 3, -1, s * 2, 5, 1, p => p.y % 2 == 0 ? Pal.Metal[3] : Pal.Metal[1]);
            g.Tube(new Vector3(0, 1, 2), new Vector3(0, -1, -3), 0.8f, Pal.Ramp(Pal.Chrome, 1, 2502));                 // tuned pipe
            g.CylZ(0, 1, 1.2f, -3, -3, Pal.Ramp(Pal.Black, 1, 2503));                                                     // prop flange
            g.CylX(4, 1, 1.2f, 3, 4, Pal.Ramp(Pal.Crimson, 2, 2504));                                                     // air box
            var e = Make("engine_2stroke_aero", PartCategory.Engine, g, 34, 1);
            e.torque = 75f; e.maxRpm = 6500f; e.peakAt = 0.8f;
            yield return e;

            var w = new VoxelGrid();
            const float R = 3.5f;
            for (int x = 0; x < 2; x++)
            for (int y = -4; y <= 4; y++)
            for (int z = -4; z <= 4; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                w.Set(x, y, z, Pal.Solid(d > 2.2f ? Pal.Tire[x == 1 ? 1 : 2] : d < 0.8f ? Pal.Chrome[3] : Pal.Cream[2]));
            }
            var wheel = Make("wheel_aero", PartCategory.Wheel, w, 5, 1, R * VoxelMesher.DefaultSize);
            wheel.grip = 0.9f; wheel.mudGrip = 0.6f; wheel.width = 0.14f; wheel.rolling = 0.6f;
            yield return wheel;
        }
    }

    /// <summary>Flying machines (roadmap 25): a flex-wing ultralight trike and an open-frame gyrocopter. Their wings and
    /// rotors are the <see cref="FlightModel"/>'s job; the airframes ride on the same sockets as cars (engine, wheels).</summary>
    public static partial class VehicleDesigns
    {
        /// <summary>Two-blade propeller (spins about local Z).</summary>
        static VoxelGrid Propeller(int r)
        {
            var g = new VoxelGrid();
            g.Box(-1, -1, 0, 1, 1, 1, Pal.Ramp(Pal.Metal, 2, 2511));
            for (int x = 2; x <= r; x++)
            {
                var c = x >= r - 1 ? Pal.Solid(Pal.Amber) : Pal.Ramp(Pal.Wood, 2, 2512);
                g.Set(x, x % 3 == 0 ? 1 : 0, 0, c); g.Set(-x, x % 3 == 0 ? -1 : 0, 0, c);
            }
            return g;
        }

        public static VehicleDesign Ultralight()
        {
            var d = new VehicleDesign
            {
                name = "Ultralight", mass = 250, drive = VehicleDriver.Drive.Rear, travel = 0.18f, finalDrive = 1f, gears = new[] { 1f },
                frequency = 1.5f, brakeForce = 1500f, maxSteer = 30f, eye = new Vector3Int(0, 16, 4), passenger = new Vector3Int(0, 17, -1),
                fuelL = 30f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true, aircraft = "trike", com = new Vector3(0f, 0.75f, 0.3f)
            };
            var g = new VoxelGrid();
            var pod = Pal.Weathered(Pal.Cream, 0.12f, 2521, 3, 2);
            var stripe = Pal.Ramp(Pal.Crimson, 2, 2522);
            var tube = Pal.Ramp(Pal.Metal, 2, 2523);
            // fibreglass nose pod with a tandem cockpit
            for (int z = 2; z <= 13; z++)
            {
                int hw = z > 10 ? 13 - z + 1 : 4, top = z > 9 ? 12 - (z - 9) : 12;
                for (int x = -hw; x <= hw; x++)
                for (int y = 5; y <= top; y++)
                {
                    bool shell = Mathf.Abs(x) == hw || y == 5 || z == 13 || (y == top && z > 8);
                    if (shell) g.Set(x, y, z, y == 9 || y == 10 ? stripe : pod);
                }
            }
            g.Box(-1, 12, 9, 1, 14, 9, Pal.Ramp(Pal.Glass, 3, 2524));                                                        // windscreen
            g.Box(-2, 6, 2, 2, 7, 5, Pal.Ramp(Pal.Black, 1, 2525)); g.Box(-2, 6, -3, 2, 8, 0, Pal.Ramp(Pal.Black, 1, 2525));   // seats
            g.Tube(new Vector3(0, 5, 13), new Vector3(0, 6, -9), 0.7f, tube);                                                // keel
            g.Tube(new Vector3(-9, 5, -5), new Vector3(9, 5, -5), 0.6f, tube);                                               // main gear axle
            foreach (int s in new[] { -1, 1 }) g.Tube(new Vector3(s * 8, 5, -5), new Vector3(s * 1, 9, -2), 0.5f, tube);     // gear struts
            g.Tube(new Vector3(0, 7, -2), new Vector3(0, 28, 0), 0.7f, tube);                                                // mast
            g.Tube(new Vector3(0, 11, 8), new Vector3(0, 28, 1), 0.5f, tube);                                                // front strut
            foreach (int s in new[] { -1, 1 }) g.Tube(new Vector3(0, 28, 1), new Vector3(s * 5, 17, 8), 0.4f, tube);         // control A-frame
            g.Box(-5, 17, 8, 5, 17, 8, Pal.Ramp(Pal.Black, 1, 2526));                                                        // base bar
            g.Box(-1, 10, -9, 1, 13, -5, Pal.Ramp(Pal.Metal, 1, 2527));                                                      // engine mount
            // the flex wing: swept delta, striped sail, leading-edge tubes, king post
            for (int x = -62; x <= 62; x++)
            {
                int ax = Mathf.Abs(x);
                int le = Mathf.RoundToInt(11 - ax * 0.20f), te = Mathf.RoundToInt(-8 - ax * 0.02f);
                for (int z = te; z <= le; z++) g.Set(x, 29, z, z == le ? tube : ((x + 64) / 8) % 2 == 0 ? stripe : pod);
            }
            g.Box(0, 30, 0, 0, 33, 0, tube);                                                                                  // king post
            g.Set(-1, 12, 13, Pal.Solid(Pal.LightW)); g.Set(1, 12, 13, Pal.Solid(Pal.LightW));
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-5, 4, -10, 5, 14, 14));
            d.spinners.Add(("Prop", Propeller(10), new Vector3Int(0, 12, -11)));
            d.Socket("wheel_nose", PartCategory.Wheel, -1, 4, 14, "wheel_aero");
            d.Socket("wheel_main", PartCategory.Wheel, 9, 4, -5, "wheel_aero", true);
            d.Socket("engine", PartCategory.Engine, 0, 10, -7, "engine_2stroke_aero", false, 1);
            return d;
        }

        public static VehicleDesign Gyrocopter()
        {
            var d = new VehicleDesign
            {
                name = "Gyrocopter", mass = 320, drive = VehicleDriver.Drive.Rear, travel = 0.2f, finalDrive = 1f, gears = new[] { 1f },
                frequency = 1.6f, brakeForce = 1800f, maxSteer = 30f, eye = new Vector3Int(0, 17, 3),
                fuelL = 40f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true, aircraft = "gyro", com = new Vector3(0f, 0.8f, 0.15f)
            };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Ochre, 0.18f, 2531, 3, 2);
            var tube = Pal.Ramp(Pal.Metal, 2, 2532);
            // enclosed nose pod with a bubble windscreen
            for (int z = 1; z <= 12; z++)
            {
                int hw = z > 9 ? 12 - z + 1 : 4, top = z > 7 ? 13 - (z - 7) : 13;
                for (int x = -hw; x <= hw; x++)
                for (int y = 5; y <= top; y++)
                {
                    bool shell = Mathf.Abs(x) == hw || y == 5 || z == 12 || y == top || z == 1;
                    if (!shell) continue;
                    bool glass = y > 10 && z > 4;
                    g.Set(x, y, z, glass ? Pal.Ramp(Pal.Glass, 3, 2533) : y == 8 ? Pal.Ramp(Pal.Black, 1, 2534) : paint);
                }
            }
            g.Box(-2, 6, 1, 2, 8, 4, Pal.Ramp(Pal.Black, 1, 2535));                                                          // seat
            g.Tube(new Vector3(0, 5, 12), new Vector3(0, 6, -10), 0.7f, tube);                                               // keel
            g.Tube(new Vector3(0, 11, -8), new Vector3(0, 12, -27), 0.6f, tube);                                             // tail boom
            g.Tube(new Vector3(-8, 5, -3), new Vector3(8, 5, -3), 0.6f, tube);                                               // main gear
            foreach (int s in new[] { -1, 1 }) g.Tube(new Vector3(s * 7, 5, -3), new Vector3(s * 1, 9, 0), 0.5f, tube);
            g.Tube(new Vector3(0, 8, -2), new Vector3(0, 33, 0), 0.8f, tube);                                                // mast
            g.Box(-1, 32, -1, 1, 33, 1, Pal.Ramp(Pal.Chrome, 2, 2536));                                                      // rotor head
            g.Box(-8, 12, -26, 8, 12, -23, paint);                                                                            // tailplane
            foreach (int s in new[] { -1, 1 }) g.Box(s * 8, 12, -27, s * 8, 18, -23, p => p.y >= 17 ? Pal.Crimson[3] : paint(p));   // twin fins
            g.Box(-1, 8, -9, 1, 11, -5, Pal.Ramp(Pal.Metal, 1, 2537));                                                       // engine mount
            g.Set(0, 9, 12, Pal.Solid(Pal.LightW));
            g.RelabelWhere((p, v) => v.label == 0 && IsGlass(v), "glass");
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.glass = g.Extract("glass", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-5, 4, -10, 5, 14, 13));
            d.colliders.Add(VehicleDesign.Box(-1, 10, -28, 1, 13, -9));
            // two-blade rotor, 8 m across
            var rot = new VoxelGrid();
            rot.Box(-2, 0, -2, 2, 1, 2, Pal.Ramp(Pal.Chrome, 1, 2538));
            for (int x = 3; x <= 50; x++)
            {
                var c = x >= 47 ? Pal.Solid(Pal.Crimson[3]) : Pal.Ramp(Pal.Metal, 3, 2539);
                rot.Box(x, 0, -1, x, 0, 1, c); rot.Box(-x, 0, -1, -x, 0, 1, c);
            }
            d.spinners.Add(("Rotor", rot, new Vector3Int(0, 34, 0)));
            d.spinners.Add(("Prop", Propeller(9), new Vector3Int(0, 10, -10)));
            d.Socket("wheel_nose", PartCategory.Wheel, -1, 4, 13, "wheel_aero");
            d.Socket("wheel_main", PartCategory.Wheel, 8, 4, -3, "wheel_aero", true);
            d.Socket("engine", PartCategory.Engine, 0, 8, -6, "engine_2stroke_aero", false, 1);
            return d;
        }
    }
}
