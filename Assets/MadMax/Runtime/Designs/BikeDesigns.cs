using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Two-wheelers (roadmap 24): a dirt bike (tall, knobby, a thumper), a raked chopper (V-twin, ape
    /// hangers, forward controls), a bicycle (pedals = stamina, silent) and a sidecar outfit with a gun on the chair.
    /// Same part system as the cars (wheel, engine and weapon sockets); <see cref="BikeBalance"/> does the leaning.
    /// Each carries a baked rider (label "driver") for NPC bikers; the player sits astride instead.</summary>
    public static partial class VehicleDesigns
    {
        /// <summary>An astride rider: hips on the seat, hands on the grips, feet on the pegs, helmet and goggles.</summary>
        static void Rider(VoxelGrid g, float cx, int seatY, int seatZ, int barY, int barZ, int barX, int pegY, int pegZ)
        {
            int prev = g.label;
            g.Use("driver");
            var jacket = Pal.Ramp(Pal.Olive, 1, 2431);
            var jeans = Pal.Ramp(Pal.Navy, 1, 2432);
            var boots = Pal.Ramp(Pal.Black, 1, 2433);
            g.Box(Mathf.FloorToInt(cx) - 1, seatY + 1, seatZ - 1, Mathf.CeilToInt(cx) + 1, seatY + 2, seatZ + 1, jeans);                                        // hips
            var chest = new Vector3(cx, seatY + 7, seatZ + 1.5f);
            g.Tube(new Vector3(cx, seatY + 3, seatZ), chest, 1.7f, jacket);                                                                                         // torso
            g.Box(Mathf.FloorToInt(cx) - 1, seatY + 9, seatZ + 1, Mathf.CeilToInt(cx) + 1, seatY + 11, seatZ + 3, Pal.Ramp(Pal.Black, 2, 2434));                  // helmet
            g.Box(Mathf.FloorToInt(cx) - 1, seatY + 10, seatZ + 4, Mathf.CeilToInt(cx) + 1, seatY + 10, seatZ + 4, Pal.Ramp(Pal.Chrome, 2, 2435));               // goggles
            g.Box(Mathf.FloorToInt(cx), seatY + 8, seatZ + 3, Mathf.CeilToInt(cx), seatY + 9, seatZ + 3, Pal.Ramp(Pal.Skin, 1, 2436));                           // chin
            foreach (int s in new[] { -1, 1 })
            {
                g.Tube(chest + new Vector3(s * 1.8f, 0f, 0f), new Vector3(cx + s * barX, barY, barZ), 0.6f, jacket);                                             // arms to the grips
                var hip = new Vector3(cx + s * 1.5f, seatY + 1, seatZ);
                var knee = new Vector3(cx + s * 2.6f, Mathf.Max(pegY + 2, seatY - 2), (seatZ + pegZ) * 0.5f + 2f);
                g.Tube(hip, knee, 0.9f, jeans);                                                                                                                      // thigh
                g.Tube(knee, new Vector3(cx + s * 2.6f, pegY + 1, pegZ), 0.8f, jeans);                                                                              // shin
                g.Box(Mathf.RoundToInt(cx + s * 2.6f), pegY, pegZ - 1, Mathf.RoundToInt(cx + s * 2.6f), pegY, pegZ + 1, boots);                                 // boot
            }
            g.label = prev;
        }

        public static VehicleDesign DirtBike()
        {
            var d = new VehicleDesign
            {
                name = "DirtBike", mass = 150, drive = VehicleDriver.Drive.Rear, travel = 0.22f, finalDrive = 10f,
                gears = new[] { 2.6f, 1.8f, 1.4f, 1.15f, 0.95f }, frequency = 1.9f, brakeForce = 2600f, maxSteer = 36f,
                eye = new Vector3Int(-1, 23, -3), passenger = new Vector3Int(-1, 23, -8),
                fuelL = 9f, oilL = 1.2f, coolantL = 0f, usesCoolant = false, bike = true
            };
            var g = new VoxelGrid();
            var plastic = Pal.Weathered(Pal.Ochre, 0.12f, 2421, 3, 2);
            var frame = Pal.Ramp(Pal.Metal, 2, 2422);
            var black = Pal.Ramp(Pal.Black, 1, 2423);
            var chrome = Pal.Ramp(Pal.Chrome, 2, 2424);
            foreach (int x in new[] { -2, 1 }) g.Tube(new Vector3(x, 5, 9), new Vector3(x, 15, 6), 0.5f, chrome);          // fork legs
            g.Box(-2, 15, 5, 1, 16, 6, frame);                                                                               // triple clamp
            g.Box(-5, 17, 5, 4, 17, 5, black);                                                                               // bars
            g.Box(-6, 17, 5, -6, 17, 5, Pal.Ramp(Pal.Crimson, 2)); g.Box(5, 17, 5, 5, 17, 5, Pal.Ramp(Pal.Crimson, 2));     // grips
            g.Box(-2, 13, 7, 1, 15, 8, plastic);                                                                             // number plate
            g.Set(-1, 14, 9, Pal.Solid(Pal.LightW)); g.Set(0, 14, 9, Pal.Solid(Pal.LightY));                                 // headlight
            g.Box(-2, 12, 6, 1, 12, 13, plastic);                                                                            // high front fender
            g.Tube(new Vector3(-0.5f, 14, 5), new Vector3(-0.5f, 6, -1), 0.7f, frame);                                      // down tube
            g.Tube(new Vector3(-0.5f, 14, 5), new Vector3(-0.5f, 12, -8), 0.6f, frame);                                     // backbone
            g.Tube(new Vector3(-0.5f, 6, -1), new Vector3(-0.5f, 12, -4), 0.6f, frame);
            foreach (int x in new[] { -2, 1 }) g.Tube(new Vector3(x, 7, -2), new Vector3(x, 5, -9), 0.5f, frame);          // swingarm
            g.Tube(new Vector3(1, 7, -3), new Vector3(1, 12, -6), 0.4f, Pal.Ramp(Pal.Crimson, 2, 2425));                   // shock
            g.Box(-2, 12, 0, 1, 14, 4, plastic);                                                                             // tank
            g.Box(-1, 14, -7, 0, 14, 0, black);                                                                              // long flat seat
            g.Box(-2, 11, -7, 1, 13, -3, plastic);                                                                           // side panels
            g.Box(-1, 13, -13, 0, 13, -8, plastic);                                                                          // tail
            g.Box(-1, 12, -13, 0, 12, -13, Pal.Solid(Pal.TailR));
            g.Tube(new Vector3(1, 9, 2), new Vector3(2, 7, 3), 0.5f, Pal.Ramp(Pal.Rust, 2, 2426));                         // header
            g.Tube(new Vector3(2, 7, 3), new Vector3(2, 11, -4), 0.5f, Pal.Ramp(Pal.Rust, 2, 2426));
            g.CylZ(2.5f, 12, 1.1f, -11, -5, Pal.Ramp(Pal.Chrome, 1, 2427));                                                  // silencer
            g.Box(-4, 7, -1, -3, 7, 0, black); g.Box(2, 7, -1, 3, 7, 0, black);                                              // pegs
            Rider(g, -0.5f, 14, -3, 17, 5, 5, 7, 0);
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-2, 4, -13, 1, 16, 12));
            d.Socket("wheel_front", PartCategory.Wheel, -1, 5, 9, "wheel_bike");
            d.Socket("wheel_rear", PartCategory.Wheel, -1, 5, -9, "wheel_bike");
            d.Socket("engine", PartCategory.Engine, -1, 5, 1, "engine_single", false, 1);
            return d;
        }

        public static VehicleDesign Chopper()
        {
            var d = new VehicleDesign
            {
                name = "Chopper", mass = 300, drive = VehicleDriver.Drive.Rear, travel = 0.16f, finalDrive = 7.5f,
                gears = new[] { 2.5f, 1.7f, 1.3f, 1.05f, 0.88f }, frequency = 1.6f, brakeForce = 3600f, maxSteer = 30f,
                eye = new Vector3Int(0, 20, -3), passenger = new Vector3Int(0, 22, -9),
                fuelL = 15f, oilL = 3f, coolantL = 0f, usesCoolant = false, bike = true
            };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Crimson, 0.1f, 2441, 3, 2);
            var frame = Pal.Ramp(Pal.Black, 2, 2442);
            var chrome = Pal.Ramp(Pal.Chrome, 2, 2443);
            foreach (int x in new[] { -2, 2 }) g.Tube(new Vector3(x, 4, 14), new Vector3(x, 15, 7), 0.5f, chrome);         // raked fork
            g.Box(-2, 15, 6, 2, 16, 7, chrome);                                                                              // clamps
            foreach (int s in new[] { -1, 1 })
            {
                g.Tube(new Vector3(s, 16, 7), new Vector3(s * 5, 21, 6), 0.4f, chrome);                                     // ape hangers
                g.Box(s * 5, 21, 5, s * 6, 21, 6, Pal.Ramp(Pal.Black, 0));
                g.Tube(new Vector3(s * 2, 6, 3), new Vector3(s * 3, 5, 6), 0.4f, chrome);                                   // forward controls
                g.Box(s * 4, 5, 5, s * 4, 5, 7, Pal.Ramp(Pal.Black, 1));
            }
            g.CylZ(0, 13, 1.6f, 9, 10, p => (p.x * p.x + (p.y - 13) * (p.y - 13)) < 2 ? Pal.LightW : Pal.Chrome[2]);         // round headlight
            g.Tube(new Vector3(0, 15, 7), new Vector3(0, 11, -8), 0.7f, frame);                                             // backbone
            g.Tube(new Vector3(0, 15, 7), new Vector3(0, 5, 3), 0.7f, frame);                                               // down tube
            g.Tube(new Vector3(0, 5, 3), new Vector3(0, 5, -8), 0.6f, frame);                                               // cradle
            foreach (int x in new[] { -2, 2 }) g.Tube(new Vector3(x, 5, -4), new Vector3(x, 4, -9), 0.5f, frame);         // hardtail stays
            for (int z = 1; z <= 6; z++) { int h = z <= 2 || z >= 6 ? 12 : 13; g.Box(-1, 12, z, 1, h + 1, z, paint); }       // teardrop tank
            g.Set(0, 15, 4, Pal.Solid(Pal.Chrome[3]));                                                                       // filler cap
            g.Box(-2, 10, -5, 2, 11, -1, Pal.Ramp(Pal.Black, 2, 2444));                                                      // low saddle
            g.Box(-2, 11, -14, 2, 11, -6, paint); g.Box(-2, 10, -14, -2, 10, -7, paint); g.Box(2, 10, -14, 2, 10, -7, paint);   // bobbed fender
            g.Tube(new Vector3(0, 11, -12), new Vector3(0, 18, -13), 0.4f, chrome);                                         // sissy bar
            g.Box(-1, 18, -13, 1, 19, -13, chrome);
            g.Set(0, 10, -15, Pal.Solid(Pal.TailR));
            foreach (int y in new[] { 5, 7 }) g.Tube(new Vector3(2, y + 2, 2), new Vector3(3, y, -13), 0.5f, chrome);       // twin pipes
            Rider(g, 0f, 11, -3, 21, 6, 5, 5, 6);
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-2, 4, -15, 2, 15, 15));
            d.Socket("wheel_front", PartCategory.Wheel, -1, 4, 14, "wheel_bike_street");
            d.Socket("wheel_rear", PartCategory.Wheel, -1, 4, -9, "wheel_bike_street");
            d.Socket("engine", PartCategory.Engine, 0, 4, 0, "engine_vtwin", false, 2);
            return d;
        }

        public static VehicleDesign Bicycle()
        {
            var d = new VehicleDesign
            {
                name = "Bicycle", mass = 60, drive = VehicleDriver.Drive.Rear, travel = 0.05f, finalDrive = 2.2f,
                gears = new[] { 2.4f, 1.8f, 1.4f, 1.1f, 0.9f }, frequency = 2.4f, brakeForce = 900f, maxSteer = 38f,
                eye = new Vector3Int(0, 22, -3), fuelL = 0f, oilL = 0f, coolantL = 0f, usesCoolant = false, oilInFuel = true, bike = true
            };
            var g = new VoxelGrid();
            var tube = Pal.Weathered(Pal.Navy, 0.3f, 2451, 3, 1);
            var chrome = Pal.Ramp(Pal.Chrome, 2, 2452);
            var crate = Pal.Ramp(Pal.Wood, 1, 2455);
            g.Tube(new Vector3(0, 9, 6), new Vector3(0, 12, 5), 0.5f, tube);                                                 // head tube
            g.Tube(new Vector3(0, 12, 5), new Vector3(0, 12, -3), 0.5f, tube);                                               // top tube
            g.Tube(new Vector3(0, 11, 5), new Vector3(0, 4, 0), 0.5f, tube);                                                 // down tube
            g.Tube(new Vector3(0, 4, 0), new Vector3(0, 13, -3), 0.5f, tube);                                                // seat tube
            foreach (int x in new[] { -1, 1 })
            {
                g.Tube(new Vector3(x, 4, 0), new Vector3(x, 4, -7), 0.4f, tube);                                             // chain stays
                g.Tube(new Vector3(x, 12, -3), new Vector3(x, 4, -7), 0.4f, tube);                                           // seat stays
                g.Tube(new Vector3(x, 4, 7), new Vector3(x, 10, 6), 0.4f, chrome);                                           // fork
            }
            g.Box(0, 13, 5, 0, 14, 5, chrome);                                                                               // stem
            g.Box(-4, 14, 4, 4, 14, 4, chrome);                                                                              // bars
            g.Box(-5, 14, 4, -5, 14, 4, Pal.Ramp(Pal.Black, 1)); g.Box(5, 14, 4, 5, 14, 4, Pal.Ramp(Pal.Black, 1));
            g.Box(-1, 13, -4, 1, 13, -2, Pal.Ramp(Pal.Black, 2, 2453));                                                      // saddle
            g.Box(-2, 9, -10, 2, 9, -5, Pal.Ramp(Pal.Wood, 2, 2454));                                                        // rack
            g.Box(-2, 10, -10, 2, 11, -10, crate); g.Box(-2, 10, -5, 2, 11, -5, crate);                                     // crate
            g.Box(-2, 10, -9, -2, 11, -6, crate); g.Box(2, 10, -9, 2, 11, -6, crate);
            g.Set(0, 10, 7, Pal.Solid(Pal.LightW));                                                                          // lamp
            Rider(g, 0f, 13, -3, 14, 4, 5, 4, 0);
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-1, 3, -10, 1, 14, 8));
            d.Socket("wheel_front", PartCategory.Wheel, 0, 4, 7, "wheel_bicycle");
            d.Socket("wheel_rear", PartCategory.Wheel, 0, 4, -7, "wheel_bicycle");
            d.Socket("engine", PartCategory.Engine, 0, 4, 0, "engine_pedals", false, 1);
            return d;
        }

        public static VehicleDesign SidecarOutfit()
        {
            var d = new VehicleDesign
            {
                name = "SidecarOutfit", mass = 420, drive = VehicleDriver.Drive.Rear, travel = 0.16f, finalDrive = 8f,
                gears = new[] { 2.6f, 1.8f, 1.35f, 1.08f, 0.9f }, frequency = 1.7f, brakeForce = 5200f, maxSteer = 28f,
                eye = new Vector3Int(0, 21, -3), passenger = new Vector3Int(12, 15, -2),
                fuelL = 16f, oilL = 3f, coolantL = 0f, usesCoolant = false, bike = true, sidecar = true, comX = 0.3f
            };
            var g = new VoxelGrid();
            var drab = Pal.Weathered(Pal.RigGreen, 0.25f, 2461, 3, 3);
            var frame = Pal.Ramp(Pal.Black, 2, 2462);
            var chrome = Pal.Ramp(Pal.Chrome, 1, 2463);
            // the bike
            foreach (int x in new[] { -2, 2 }) g.Tube(new Vector3(x, 4, 11), new Vector3(x, 15, 7), 0.5f, chrome);
            g.Box(-2, 15, 6, 2, 16, 7, frame);
            g.Box(-5, 17, 6, 5, 17, 6, frame); g.Box(-6, 17, 6, -6, 17, 6, Pal.Ramp(Pal.Black, 0)); g.Box(6, 17, 6, 6, 17, 6, Pal.Ramp(Pal.Black, 0));
            g.Box(-2, 12, 7, 2, 12, 13, drab);                                                                               // fender
            g.CylZ(0, 13, 1.4f, 8, 9, p => (p.x * p.x + (p.y - 13) * (p.y - 13)) < 1 ? Pal.LightW : Pal.Metal[2]);
            g.Tube(new Vector3(0, 15, 7), new Vector3(0, 12, -8), 0.7f, frame);
            g.Tube(new Vector3(0, 15, 7), new Vector3(0, 5, 2), 0.7f, frame);
            g.Tube(new Vector3(0, 5, 2), new Vector3(0, 5, -8), 0.6f, frame);
            for (int z = 0; z <= 5; z++) g.Box(-2, 12, z, 2, 14, z, drab);                                                 // tank
            g.Box(-2, 13, -6, 2, 13, -1, Pal.Ramp(Pal.Olive, 1, 2464));                                                      // saddle
            g.Box(-2, 12, -13, 2, 12, -7, drab);                                                                             // rear fender
            g.Set(0, 11, -14, Pal.Solid(Pal.TailR));
            g.Tube(new Vector3(-2, 7, 2), new Vector3(-3, 7, -13), 0.5f, Pal.Ramp(Pal.Rust, 2, 2465));                     // exhaust (left: the chair is right)
            // struts to the chair
            g.Tube(new Vector3(1, 6, 4), new Vector3(8, 7, 4), 0.5f, frame);
            g.Tube(new Vector3(1, 6, -6), new Vector3(8, 7, -6), 0.5f, frame);
            g.Tube(new Vector3(1, 13, -2), new Vector3(9, 11, -2), 0.4f, frame);
            // the chair: a boat-shaped shell, open on top, a seat inside, a spare wheel on the back
            for (int z = -9; z <= 8; z++)
            for (int x = 8; x <= 16; x++)
            for (int y = 5; y <= 11; y++)
            {
                float taper = z > 3 ? (z - 3) * 0.8f : z < -7 ? (-7 - z) * 1.2f : 0f;
                float cxz = Mathf.Abs(x - 12f) + taper * 0.6f;
                int top = 11 - (z > 3 ? (z - 3) / 2 : 0);
                if (cxz > 4.2f || y > top) continue;
                bool shell = cxz > 3.2f || y == 5 || z == -9 || z == 8 || y == top;
                bool opening = y == top && Mathf.Abs(x - 12f) < 3.2f && z > -8 && z < 3;
                if (shell && !opening) g.Set(x, y, z, drab);
            }
            g.Box(10, 6, -6, 14, 7, -2, Pal.Ramp(Pal.Olive, 1, 2466)); g.Box(10, 8, -7, 14, 11, -7, Pal.Ramp(Pal.Olive, 1, 2466));   // chair seat + back
            g.CylX(8, -11, 2.5f, 12, 13, p => Pal.Tire[1]);                                                                  // spare on the back
            Rider(g, 0f, 13, -3, 17, 6, 6, 6, 1);
            g.Bevel();
            d.body = g.Extract("body", Vector3Int.zero);
            d.driver = g.Extract("driver", Vector3Int.zero);
            d.colliders.Add(VehicleDesign.Box(-2, 4, -14, 2, 16, 12));
            d.colliders.Add(VehicleDesign.Box(8, 4, -11, 16, 11, 8));
            d.Socket("wheel_front", PartCategory.Wheel, -1, 4, 11, "wheel_bike_street");
            d.Socket("wheel_rear", PartCategory.Wheel, -1, 4, -8, "wheel_bike_street");
            d.Socket("wheel_side", PartCategory.Wheel, 17, 4, -1, "wheel_bike_street");
            d.Socket("engine", PartCategory.Engine, 0, 4, 0, "engine_vtwin", false, 2);
            d.Socket("roof", PartCategory.Weapon, 12, 12, 5, null);                                                         // a gun on the chair's nose
            return d;
        }
    }
}
