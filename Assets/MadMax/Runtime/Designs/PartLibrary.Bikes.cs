using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Two-wheeler parts (roadmap 24): spoked wheels (knobby dirt, chrome street, thin bicycle), a
    /// single-cylinder thumper, a V-twin and the pedal crank. Same part system as the cars: they mount on any
    /// wheel / engine socket that takes their size.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> Bikes()
        {
            yield return BikeWheel("wheel_bike", 4.6f, 2, true, false);
            yield return BikeWheel("wheel_bike_street", 4.2f, 3, false, true);
            yield return BikeWheel("wheel_bicycle", 4.4f, 1, false, false);
            yield return SingleCylinder();
            yield return VTwin();
            yield return Pedals();
        }

        /// <summary>Spoked motorcycle / bicycle wheel, <paramref name="w"/> voxels wide (authored from x = 0 outward).</summary>
        static PartDesign BikeWheel(string key, float R, int w, bool knobby, bool chrome)
        {
            var g = new VoxelGrid();
            float tyre = w == 1 ? 0.9f : 1.3f;
            int r = Mathf.CeilToInt(R);
            for (int x = 0; x < w; x++)
            for (int y = -r; y <= r; y++)
            for (int z = -r; z <= r; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                int seg = Mathf.FloorToInt(Angle01(y, z) * 20);
                Color32 c;
                if (d > R - tyre)
                {
                    if (knobby && d > R - 0.5f && (seg + x) % 2 == 0) continue;                     // knobs
                    c = Pal.Tire[x == 0 || x == w - 1 ? 1 : 2];
                    if (chrome && d < R - tyre + 0.5f && x == w - 1) c = Pal.Cream[3];                // whitewall
                }
                else if (d > R - tyre - 0.8f) c = chrome ? Pal.Chrome[2] : Pal.Metal[2];            // rim
                else if (d < 0.9f) c = chrome ? Pal.Chrome[3] : Pal.Metal[1];                      // hub
                else
                {
                    // spokes: every other spoke on each side of the hub
                    int sp = Mathf.FloorToInt(Angle01(y, z) * 16);
                    bool spoke = Mathf.Abs(Angle01(y, z) * 16 - sp - 0.5f) < 0.18f && (sp + x) % 2 == 0;
                    if (!spoke) continue;
                    c = chrome ? Pal.Chrome[1] : Pal.Metal[3];
                }
                g.Set(x, y, z, Pal.Solid(c));
            }
            var part = Make(key, PartCategory.Wheel, g, w == 1 ? 2f : w * 6f, 1, R * VoxelMesher.DefaultSize);
            part.width = w == 1 ? 0.05f : w * 0.07f;
            if (key == "wheel_bike") { part.grip = 1.0f; part.mudGrip = 0.8f; part.wetGrip = 0.8f; part.rolling = 1.05f; }
            else if (key == "wheel_bike_street") { part.grip = 1.08f; part.mudGrip = 0.35f; part.wetGrip = 0.78f; part.rolling = 0.9f; }
            else { part.grip = 0.9f; part.mudGrip = 0.3f; part.wetGrip = 0.75f; part.rolling = 0.55f; part.wearRate = 0.6f; }
            return part;
        }

        /// <summary>250 cc air-cooled single: finned barrel, carb, kick-start lever.</summary>
        static PartDesign SingleCylinder()
        {
            var g = new VoxelGrid();
            var cases = Pal.Weathered(Pal.Metal, 0.25f, 2401, 2, 0);
            g.Box(-1, 0, -2, 1, 2, 2, cases);                                                                      // crankcase
            g.Box(-1, 3, -1, 1, 6, 1, p => p.y % 2 == 0 ? Pal.Metal[3] : Pal.Metal[1]);                             // finned barrel
            g.Box(-1, 7, -1, 1, 7, 1, Pal.Ramp(Pal.Chrome, 1, 2402));                                              // head
            g.Box(2, 5, -1, 2, 5, 0, Pal.Ramp(Pal.Black, 1)); g.CylX(4, -1, 1f, 2, 3, Pal.Ramp(Pal.Black, 1));    // carb + filter
            g.Box(2, 1, -3, 2, 2, -3, Pal.Ramp(Pal.Chrome, 1)); g.Box(2, 2, -4, 2, 2, -5, Pal.Ramp(Pal.Black, 1)); // kick-start
            var d = Make("engine_single", PartCategory.Engine, g, 30, 1);
            d.torque = 26f; d.maxRpm = 9500f; d.peakAt = 0.72f;
            return d;
        }

        /// <summary>45° V-twin: two finned jugs, chrome rocker covers and a round air cleaner.</summary>
        static PartDesign VTwin()
        {
            var g = new VoxelGrid();
            g.Box(-1, 0, -2, 1, 2, 2, Pal.Ramp(Pal.Chrome, 1, 2411));                                              // cases
            foreach (int s in new[] { -1, 1 })
            {
                var fins = Pal.Stripe(Pal.Solid(Pal.Black[2]), Pal.Solid(Pal.Metal[2]), 1, 2);
                g.Tube(new Vector3(0, 2, s * 1), new Vector3(0, 7, s * 3.5f), 1.4f, fins);                         // jugs
                g.Box(-1, 7, Mathf.RoundToInt(s * 3.5f) - 1, 1, 8, Mathf.RoundToInt(s * 3.5f) + 1, Pal.Ramp(Pal.Chrome, 2, 2412));
            }
            g.CylX(5, 0, 1.8f, 2, 2, Pal.Ramp(Pal.Chrome, 3, 2413));                                               // air cleaner
            var d = Make("engine_vtwin", PartCategory.Engine, g, 95, 2);
            d.torque = 95f; d.maxRpm = 5800f; d.peakAt = 0.45f;
            return d;
        }

        /// <summary>Bicycle crank: chainring, cranks and pedals. Legs are the engine — it runs on stamina.</summary>
        static PartDesign Pedals()
        {
            var g = new VoxelGrid();
            g.CylX(0, 0, 2.2f, 1, 1, p => (p.y * p.y + p.z * p.z) > 2 ? Pal.Metal[3] : Pal.Metal[1]);           // chainring
            g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Metal, 2));                                                     // axle
            g.Box(2, -3, 0, 2, 0, 0, Pal.Ramp(Pal.Chrome, 1)); g.Box(-2, 0, 0, -2, 3, 0, Pal.Ramp(Pal.Chrome, 1));   // cranks
            g.Box(3, -3, -1, 3, -3, 1, Pal.Ramp(Pal.Black, 1)); g.Box(-3, 3, -1, -3, 3, 1, Pal.Ramp(Pal.Black, 1));  // pedals
            g.Tube(new Vector3(1, 2, 0), new Vector3(1, 1, -8), 0.3f, Pal.Ramp(Pal.Black, 0));                    // chain run
            var d = Make("engine_pedals", PartCategory.Engine, g, 1, 1);
            d.torque = 22f; d.maxRpm = 700f; d.peakAt = 0.4f;
            return d;
        }
    }
}
