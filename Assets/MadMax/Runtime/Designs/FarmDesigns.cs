using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Farm machines (depth stage B): the tractor. Its implements are Tool parts on the rear "tool" socket
    /// (plough, seeder, harvester, sprayer); <see cref="Machine"/> (kind Tractor) lifts them and works the field.</summary>
    public static partial class VehicleDesigns
    {
        static readonly Color32[] TractorRed = { Pal.Hex("5a1410"), Pal.Hex("8a2018"), Pal.Hex("b0302a"), Pal.Hex("d04a38") };

        public static VehicleDesign Tractor()
        {
            var d = new VehicleDesign
            {
                name = "Tractor", machine = "Tractor", mass = 3200, drive = VehicleDriver.Drive.Rear, travel = 0.16f, frequency = 1.8f, finalDrive = 16f,
                brakeForce = 32000f, maxSteer = 38f, gears = new[] { 5.2f, 3.4f, 2.2f, 1.45f }, eye = new Vector3Int(4, 25, -17), fuelL = 120f, oilL = 10f, coolantL = 16f
            };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(TractorRed, 0.22f, 3401, 2, 4);
            var dark = Pal.Ramp(Pal.Metal, 0, 3402);
            // chassis and belly: engine block and gearbox between the wheels
            g.Box(-4, 6, -26, 4, 9, 30, dark);
            // bonnet: narrow between the front wheels, rounded top, louvres, grille and lamps
            for (int z = -4; z <= 31; z++)
            for (int x = -5; x <= 5; x++)
            for (int y = 10; y <= 17; y++)
            {
                bool round = y == 17 && Mathf.Abs(x) == 5;
                if (round) continue;
                bool louvre = Mathf.Abs(x) == 5 && y >= 12 && y <= 15 && z % 3 == 0 && z > 4 && z < 26;
                g.Set(x, y, z, louvre ? Pal.Ramp(Pal.Black, 1) : paint);
            }
            g.Box(-4, 10, 32, 4, 16, 32, Pal.Stripe(Pal.Solid(Pal.Black[0]), Pal.Ramp(Pal.Chrome, 1), 0, 2));   // grille
            foreach (int s in new[] { -1, 1 }) g.Box(s * 3, 17, 30, s * 4, 17, 31, Pal.Solid(Pal.LightW));       // lamps on the bonnet
            g.Box(-5, 6, 32, 5, 9, 36, Pal.Ramp(Pal.Metal, 1, 3403));                                             // front weights
            for (int x = -4; x <= 4; x += 2) g.Box(x, 7, 37, x, 8, 37, Pal.Solid(Pal.Metal[2]));
            g.CylY(4, 6, 1.0f, 18, 34, Pal.Ramp(Pal.Black, 1));                                                   // exhaust stack
            g.Set(4, 35, 6, Pal.Solid(Pal.Rust[1]));
            // cab over the rear axle
            Cab(g, 8, -24, -5, 14, 34, paint, 3404);
            g.Box(-8, 14, -24, 8, 14, -5, Pal.Ramp(Pal.Metal, 1));                                               // floor plate
            // rear fenders over the big wheels, with a step and a tool box
            foreach (int s in new[] { -1, 1 })
            {
                for (int z = -30; z <= -3; z++)
                {
                    float dz = (z + 16) / 14f;
                    int top = 21 + Mathf.RoundToInt(4f * (1f - dz * dz));
                    g.Box(s * 9, top, z, s * 15, top, z, paint);
                }
                g.Box(s * 9, 12, -6, s * 10, 13, -2, Pal.Ramp(Pal.Metal, 2));                                   // step
                g.Box(s * 9, 16, -30, s * 14, 20, -27, dark);                                                   // tool box / light mount
                g.Box(s * 12, 21, -30, s * 13, 21, -30, Pal.Solid(Pal.TailR));
            }
            // three-point hitch arms and the PTO
            foreach (int s in new[] { -1, 1 }) g.Box(s * 4, 3, -30, s * 4, 6, -26, Pal.Ramp(Pal.Metal, 2));
            g.Box(-1, 10, -30, 1, 12, -27, Pal.Ramp(Pal.Metal, 2));
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 6, 6, 22, "wheel_tractor_front", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 9, 10, -16, "wheel_tractor", true);
            d.Socket("engine", PartCategory.Engine, 0, 10, 14, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 11, 29, "radiator_car");
            d.Socket("tool", PartCategory.Tool, 0, 1, -31, "tool_plough");
            return d;
        }
    }
}
