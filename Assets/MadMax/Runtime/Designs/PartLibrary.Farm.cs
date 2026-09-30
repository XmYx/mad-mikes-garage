using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Farm parts (depth stage B): lugged tractor tyres and the three-point-hitch implements the tractor lowers
    /// onto its field (<see cref="Machine.Kind.Tractor"/>): plough, seeder, harvester, sprayer. Implements are authored
    /// from the hitch (origin) backwards (-z), resting on the ground at y 0 when lowered.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> Farm()
        {
            yield return TractorWheel(true);
            yield return TractorWheel(false);
            yield return Plough();
            yield return Seeder();
            yield return Harvester();
            yield return Sprayer();
        }

        static readonly Color32[] FarmRed = { Pal.Hex("5a1410"), Pal.Hex("8a2018"), Pal.Hex("b0302a"), Pal.Hex("d04a38") };
        static VoxMat FarmPaint(int seed) => Pal.Weathered(FarmRed, 0.25f, seed, 2, 0);

        /// <summary>Lugged tractor tyre: rear 1.5 m tall with chevron bars, front 0.9 m with ribs. Grips in mud.</summary>
        public static PartDesign TractorWheel(bool rear)
        {
            var g = new VoxelGrid();
            float R = rear ? 9.4f : 5.4f, rim = rear ? 5.6f : 3.2f;
            int w = rear ? 5 : 3, r = Mathf.CeilToInt(R);
            for (int x = 0; x <= w; x++)
            for (int y = -r; y <= r; y++)
            for (int z = -r; z <= r; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                float a = Angle01(y, z);
                if (d > R - 1.3f)
                {
                    // chevron lugs: the bar shifts round the tread across the width (rear), plain ribs (front)
                    int seg = rear ? Mathf.FloorToInt((a * 20f + Mathf.Abs(x - w * 0.5f) * 0.18f) * 2f) : Mathf.FloorToInt(a * 32f);
                    if (seg % 2 == 1) continue;
                }
                Color32 c;
                if (d > rim) c = x == 0 || x == w ? Pal.Tire[1] : Pal.Tire[d > R - 1.3f ? 2 : 0];
                else
                {
                    if (x == w) continue;
                    if (x < w - 1) c = Pal.Metal[0];
                    else if (d < 1.4f) c = Pal.Metal[3];
                    else c = d > rim - 1f ? FarmRed[1] : Pal.Pick(FarmRed, p, 3301, 2);
                }
                g.Set(p, Pal.Solid(c));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2;
                g.Set(w - 1, Mathf.RoundToInt(Mathf.Sin(a) * rim * 0.6f), Mathf.RoundToInt(Mathf.Cos(a) * rim * 0.6f), Pal.Solid(Pal.Chrome[2]));   // wheel nuts
            }
            var part = Make(rear ? "wheel_tractor" : "wheel_tractor_front", PartCategory.Wheel, g, rear ? 140 : 45, rear ? 3 : 2, R * VoxelMesher.DefaultSize);
            part.grip = 0.95f; part.mudGrip = rear ? 1.25f : 0.85f; part.width = rear ? 0.46f : 0.26f; part.wetGrip = 0.9f; part.rolling = 1.3f; part.wearRate = 0.5f;
            return part;
        }

        /// <summary>Four-furrow mouldboard plough (about 2.6 m of work): a headstock at the hitch, a beam running back
        /// on the diagonal, four curved boards with shares at the soil line and a depth wheel.</summary>
        public static PartDesign Plough()
        {
            var g = new VoxelGrid();
            var paint = FarmPaint(3311); var steel = Pal.Ramp(Pal.Metal, 2, 3312);
            g.Box(-6, 6, -2, 6, 8, 0, paint);                                                      // headstock
            g.Tube(new Vector3(0, 7.5f, -2), new Vector3(13, 7.5f, -30), 1.1f, paint);             // beam, on the diagonal across the boards
            for (int i = 0; i < 4; i++)
            {
                int z = -6 - i * 7, x = -16 + i * 9;
                g.Box(x, 1, z, x + 1, 7, z, paint);                                                // leg
                for (int k = 0; k < 6; k++) g.Box(x + 1 + k / 2, 0 + k / 3, z - k, x + 4 + k / 2, 3 + k / 3, z - k, steel);   // curved board
                g.Box(x - 1, 0, z + 1, x + 2, 0, z + 2, Pal.Solid(Pal.Chrome[2]));                 // share
            }
            g.CylX(2.4f, -32, 2.4f, 15, 16, Pal.Ramp(Pal.Tire, 1));                                // depth wheel
            return Make("tool_plough", PartCategory.Tool, g, 420, 3);
        }

        /// <summary>Seed drill: a seed hopper on a frame, twelve disc coulters and a press roller behind.</summary>
        public static PartDesign Seeder()
        {
            var g = new VoxelGrid();
            var paint = FarmPaint(3321);
            g.Box(-14, 6, -4, 14, 7, -2, Pal.Ramp(Pal.Metal, 1, 3322));                           // frame
            g.Box(-13, 8, -10, 13, 16, -3, paint);                                                 // hopper
            g.Box(-12, 17, -9, 12, 17, -4, Pal.Ramp(Pal.Metal, 2));                                // lid
            for (int x = -12; x <= 12; x += 2) { g.Box(x, 1, -8, x, 7, -8, Pal.Ramp(Pal.Metal, 0)); g.CylX(1.6f, -8, 1.6f, x, x, Pal.Solid(Pal.Chrome[1])); }   // coulters
            g.CylX(2.2f, -14, 2.2f, -14, 14, Pal.Ramp(Pal.Metal, 2));                              // press roller
            g.Box(-14, 4, -14, 14, 4, -12, Pal.Ramp(Pal.Metal, 1));
            return Make("tool_seeder", PartCategory.Tool, g, 380, 3);
        }

        /// <summary>Trailed harvester: a reel over the cutter bar, an auger, and a grain tank with a spout.</summary>
        public static PartDesign Harvester()
        {
            var g = new VoxelGrid();
            var paint = FarmPaint(3331);
            g.Box(-1, 5, -8, 1, 7, 0, Pal.Ramp(Pal.Metal, 1));                                     // drawbar
            g.Box(-15, 0, -14, 15, 3, -8, Pal.Ramp(Pal.Metal, 1, 3332));                           // header tray
            g.Box(-15, 1, -8, 15, 1, -8, Pal.Stripe(Pal.Solid(Pal.Chrome[2]), Pal.Solid(Pal.Metal[0]), 0, 2));   // cutter bar
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2;
                int y = 7 + Mathf.RoundToInt(Mathf.Sin(a) * 3), z = -11 + Mathf.RoundToInt(Mathf.Cos(a) * 3);
                g.Box(-14, y, z, 14, y, z, Pal.Ramp(Pal.Ochre, 2));                                // reel bats
            }
            g.Box(-10, 4, -24, 10, 14, -15, paint);                                                // grain tank
            g.Box(-9, 15, -23, 9, 15, -16, Pal.Ramp(Pal.Metal, 2));
            g.Box(10, 12, -20, 16, 13, -19, Pal.Ramp(Pal.Metal, 2));                               // spout
            g.CylX(3.2f, -20, 3.2f, -12, -11, Pal.Ramp(Pal.Tire, 1)); g.CylX(3.2f, -20, 3.2f, 11, 12, Pal.Ramp(Pal.Tire, 1));
            return Make("tool_harvester", PartCategory.Tool, g, 900, 4);
        }

        /// <summary>Boom sprayer: a tank on a frame and folding booms with nozzles either side.</summary>
        public static PartDesign Sprayer()
        {
            var g = new VoxelGrid();
            var paint = FarmPaint(3341);
            g.Box(-6, 6, -4, 6, 7, -2, Pal.Ramp(Pal.Metal, 1));
            g.CylZ(0, 12, 5.2f, -16, -4, Pal.Weathered(Pal.Cream, 0.2f, 3342, 2, 0));             // tank
            g.Box(-1, 17, -11, 1, 18, -9, Pal.Ramp(Pal.Metal, 2));                                 // filler cap
            g.Box(-26, 5, -17, 26, 6, -17, Pal.Ramp(Pal.Metal, 2, 3343));                          // booms
            for (int x = -24; x <= 24; x += 4) g.Box(x, 3, -17, x, 4, -17, Pal.Solid(Pal.Black[1]));   // nozzles
            foreach (int x in new[] { -5, 5 }) g.CylX(2.6f, -12, 2.6f, x, x, Pal.Ramp(Pal.Tire, 1));
            return Make("tool_sprayer", PartCategory.Tool, g, 350, 3);
        }
    }
}
