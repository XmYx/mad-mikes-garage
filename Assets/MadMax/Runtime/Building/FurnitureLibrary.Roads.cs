using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Build pieces for depth stage C (roads): the powered rock crusher (stone and rubble → gravel), road
    /// signs (stop, speed limit, direction arrow), a 2 m guard rail, curbs and bollards, and two bridges placed from
    /// the bank that span forward (away from the builder, local −Z): a timber trestle bridge (8 × 4 m, log piers 4 m
    /// deep) and a steel girder bridge (12 × 4 m on steel trestles and concrete abutments). Bridge decks carry wheels
    /// through <see cref="RoadBridge"/>. Coarse 0.16 m voxels for the bridges.</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> RoadsPieces()
        {
            var B = BuildCategory.Structure; var De = BuildCategory.Decor; var In = BuildCategory.Industry;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var St = ResourceType.Stone; var Fe = ResourceType.Iron;
            var Cu = ResourceType.Copper; var Co = ResourceType.Concrete; var Rb = ResourceType.Rubber;

            yield return D("rock_crusher", "ROCK CRUSHER", In, RockCrusherGrid(), 16, false, go =>
            {
                Node(go, UtilityKind.Power, 0.5f);
                var st = Station(go, "rock_crusher", "CRUSH ROCK (ROCK CRUSHER)", 2000f);
                st.tier = 0.5f; st.output = new Vector3(0f, 0.4f, 1.25f);                              // the heap under the chute
            }, (Fe, 14), (S, 10), (Cu, 4), (Rb, 2));
            yield return D("sign_stop", "STOP SIGN", De, StopSignGrid(), 4, false, null, (Fe, 2), (S, 2));
            yield return D("sign_speed", "SPEED LIMIT SIGN", De, SpeedSignGrid(), 4, false, null, (Fe, 2), (S, 2));
            yield return D("sign_direction", "DIRECTION SIGN", De, DirectionSignGrid(), 4, false, null, (Fe, 3), (S, 3));
            yield return D("guard_rail", "GUARD RAIL", B, GuardRailGrid(), 14, false, null, (Fe, 4), (S, 2));
            yield return D("curb", "CURB", B, CurbGrid(), 20, false, null, (Co, 2));
            yield return D("bollard", "BOLLARD", B, BollardGrid(), 20, false, null, (Fe, 3));
            yield return Big("bridge_timber", "TIMBER BRIDGE", B, TimberBridgeGrid(), 25, go => go.AddComponent<RoadBridge>().Set(1.84f, 8f, 0.08f), (W, 40), (Fe, 4));
            yield return Big("bridge_steel", "STEEL BRIDGE", B, SteelBridgeGrid(), 60, go => go.AddComponent<RoadBridge>().Set(1.84f, 12f, 0.08f), (Fe, 40), (Co, 12), (S, 10));
        }

        /// <summary>Jaw crusher on a skid: stone hopper on top, twin flywheels, belt from the motor, a chute
        /// spilling gravel onto a heap at the front (+Z).</summary>
        static VoxelGrid RockCrusherGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-12, 0, -9, 12, 1, 9, p => (Mathf.Abs(p.x) == 12 || Mathf.Abs(p.z) == 9) && ((p.x + p.z) / 2 & 1) == 0 ? Pal.Ochre[3] : Pal.Metal[1]);   // skid, hazard edge
            g.Box(-6, 2, -6, 6, 14, 3, Pal.Weathered(Pal.Ochre, 0.4f, 3301, 2, 0));                   // crusher body
            g.Box(-4, 13, -4, 4, 14, 1, Pal.Solid(Pal.Black[0]));                                     // the jaws' mouth
            for (int y = 15; y <= 20; y++)                                                           // flared hopper
            {
                int r = 5 + (y - 15);
                for (int x = -r; x <= r; x++) for (int z = -r - 1; z <= r - 2; z++)
                    if (Mathf.Abs(x) == r || z == -r - 1 || z == r - 2) g.Set(x, y, z, Pal.Weathered(Pal.Metal, 0.5f, 3302, 2, 0));
            }
            g.Mat(Stone);
            for (int x = -4; x <= 4; x++) for (int z = -5; z <= 2; z++)                              // rock in the hopper
            {
                int h = 16 + Mathf.RoundToInt(Mathf.PerlinNoise(x * 0.6f, z * 0.6f) * 3f);
                g.Box(x, 15, z, x, h, z, Pal.Ramp(Pal.Chrome, 0, 3303));
            }
            g.Mat(Iron);
            foreach (int x in new[] { -9, 8 }) g.CylX(9, -2, 5f, x, x + 1, p => ((p.y + p.z) & 3) == 0 ? Pal.Crimson[2] : Pal.Metal[1], 1.5f);   // flywheels
            g.CylX(9, -2, 1f, -9, 9, Pal.Ramp(Pal.Chrome, 2));                                        // shaft
            g.Box(7, 2, -9, 11, 6, -5, Pal.Weathered(Pal.RigGreen, 0.2f, 3304, 2, 0));                // motor
            g.Mat((byte)ResourceType.Rubber);
            g.Tube(new Vector3(9, 5, -7), new Vector3(9, 12, -4), 0.5f, Pal.Solid(Pal.Black[1]));       // drive belt
            g.Mat(Iron);
            for (int z = 4; z <= 13; z++) { int y = 4 - (z - 4) / 3; g.Box(-3, y, z, 3, y, z, Pal.Ramp(Pal.Metal, 2, 3305)); g.Set(-4, y + 1, z, Pal.Solid(Pal.Metal[1])); g.Set(4, y + 1, z, Pal.Solid(Pal.Metal[1])); }   // chute
            g.Mat(Stone);
            for (int x = -5; x <= 5; x++) for (int z = 11; z <= 18; z++)                             // gravel heap
            {
                float d = Mathf.Sqrt(x * x * 0.5f + (z - 15) * (z - 15));
                int h = Mathf.RoundToInt(2.5f - d * 0.6f);
                if (h >= 0) g.Box(x, 0, z, x, h, z, Pal.Ramp(GravelHeap, 1, 3306));
            }
            return g;
        }

        /// <summary>Octagonal red stop sign with a white rim and a white legend band on a 2.2 m post.</summary>
        static VoxelGrid StopSignGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(0, 0, -2, 0, 25, -2, Pal.Weathered(Pal.Metal, 0.3f, 3311, 2, 0));                   // post
            g.Mat(Scrap);
            const int cy = 30; const float r = 5.5f;
            for (int x = -6; x <= 6; x++)
            for (int y = cy - 6; y <= cy + 6; y++)
            {
                int dx = Mathf.Abs(x), dy = Mathf.Abs(y - cy);
                if (!Octagon(dx, dy, r)) continue;
                bool rim = !Octagon(dx, dy, r - 1f);
                bool legend = dy <= 1 && dx <= 3 && ((x + y) & 1) == 0;
                g.Set(x, y, 0, Pal.Solid(rim || legend ? Pal.Cream[4] : Pal.Crimson[3]));
                g.Set(x, y, -1, Pal.Solid(Pal.Metal[2]));
            }
            return g;
        }

        static bool Octagon(int dx, int dy, float r) => dx <= r && dy <= r && dx + dy <= r * 1.42f;

        static readonly Color32[] GravelHeap = { Pal.Metal[2], Pal.Metal[3], Pal.Chrome[0], Pal.Sand[0] };
        static readonly string[] Digits35 = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111" };

        /// <summary>Round speed-limit plate (white, red ring, black "40") on a post.</summary>
        static VoxelGrid SpeedSignGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(0, 0, -2, 0, 25, -2, Pal.Weathered(Pal.Metal, 0.3f, 3321, 2, 0));
            g.Mat(Scrap);
            const int cy = 30;
            for (int x = -6; x <= 6; x++)
            for (int y = cy - 6; y <= cy + 6; y++)
            {
                float d = Mathf.Sqrt(x * x + (y - cy) * (y - cy));
                if (d > 6.1f) continue;
                g.Set(x, y, 0, Pal.Solid(d > 4.6f ? Pal.Crimson[3] : Pal.Cream[4]));
                g.Set(x, y, -1, Pal.Solid(Pal.Metal[2]));
            }
            Digit(g, 4, -3, cy + 2); Digit(g, 0, 1, cy + 2);
            return g;
        }

        /// <summary>A 3 × 5 digit in black on the plate face (<paramref name="x0"/> left, <paramref name="yTop"/> top row).</summary>
        static void Digit(VoxelGrid g, int d, int x0, int yTop)
        {
            var bits = Digits35[d];
            for (int row = 0; row < 5; row++) for (int col = 0; col < 3; col++)
                if (bits[row * 3 + col] == '1') g.Set(x0 + col, yTop - row, 0, Pal.Solid(Pal.Black[1]));
        }

        /// <summary>Green direction board with a white rim and a white arrow pointing right (+X; rotate to aim it).</summary>
        static VoxelGrid DirectionSignGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            foreach (int x in new[] { -7, 7 }) g.Box(x, 0, -2, x, 31, -2, Pal.Weathered(Pal.Metal, 0.3f, 3331 + x, 2, 0));
            g.Mat(Scrap);
            for (int x = -10; x <= 10; x++)
            for (int y = 23; y <= 32; y++)
            {
                bool rim = x == -10 || x == 10 || y == 23 || y == 32;
                float dy = Mathf.Abs(y - 27.5f);                                                    // from the middle of the board
                bool arrow = (x >= -7 && x < 2 && dy <= 0.5f) || (x >= 2 && x <= 7 && dy <= (7 - x) * 0.6f + 0.5f);
                g.Set(x, y, 0, Pal.Solid(rim || arrow ? Pal.Cream[4] : Pal.Moss[2]));
                g.Set(x, y, -1, Pal.Solid(Pal.Metal[2]));
            }
            return g;
        }

        /// <summary>2 m galvanised W-beam on three I-posts with spacer blocks and amber reflectors (road side +Z).</summary>
        static VoxelGrid GuardRailGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            foreach (int x in new[] { -12, 0, 12 })
            {
                g.Box(x, 0, 0, x, 9, 0, Pal.Weathered(Pal.Metal, 0.35f, 3341 + x, 2, 0));           // post
                g.Box(x, 7, 1, x, 8, 1, Pal.Ramp(Pal.Metal, 1));                                     // spacer block
                g.Set(x, 9, 3, Pal.Solid(Pal.Amber));                                                // reflector
            }
            var beam = Pal.Weathered(Pal.Chrome, 0.25f, 3345, 1, 0);
            g.Box(-13, 6, 2, 13, 9, 2, p => p.y == 7 || p.y == 8 ? Pal.Chrome[0] : beam(p));             // the W profile: ridges light, valley dark
            g.Box(-13, 7, 3, 13, 8, 3, p => (p.x % 6 == 0) ? Pal.Chrome[2] : beam(p));                  // bolt heads on the face
            return g;
        }

        /// <summary>1 m precast concrete curb, chamfered on the road side (+Z), joints at the ends.</summary>
        static VoxelGrid CurbGrid()
        {
            var g = new VoxelGrid().Mat(Concrete);
            g.Box(-6, 0, -2, 6, 2, 1, p => Mathf.Abs(p.x) == 6 ? ConcRamp[0] : Pal.Pick(ConcRamp, p, 3351, 2));
            g.ClearBox(-6, 2, 1, 6, 2, 1);                                                                // chamfered road-side edge
            g.Box(-5, 2, 0, 5, 2, 0, p => (p.x & 3) == 0 ? ConcRamp[0] : Pal.Cream[1]);                   // pale worn arris
            return g;
        }

        /// <summary>Steel bollard, 1 m, black and yellow bands, a bright cap.</summary>
        static VoxelGrid BollardGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.CylY(0, 0, 1.6f, 0, 11, p => (p.y / 3 & 1) == 0 ? Pal.Black[1] : Pal.Ochre[4]);
            g.CylY(0, 0, 1.2f, 12, 12, Pal.Ramp(Pal.Chrome, 2));
            g.Box(-2, 0, -2, 2, 0, 2, Pal.Ramp(Pal.Metal, 1, 3361));                                   // base plate
            return g;
        }

        /// <summary>8 × 4 m timber trestle bridge (0.16 m voxels) spanning from the origin along −Z: plank deck flush at
        /// the top (y 0), four log stringers, kerb logs, a post-and-rail parapet, log piers at both ends and mid-span
        /// that reach 4 m down.</summary>
        static VoxelGrid TimberBridgeGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            const int L = 50;
            for (int z = 0; z > -L; z--)
            {
                var plank = (z & 1) == 0 ? Pal.Ramp(Pal.Wood, 2, 3401) : Pal.Ramp(Pal.Wood, 1, 3402);
                g.Box(-12, 0, z, 12, 0, z, p => (Mathf.Abs(p.x) >= 4 && Mathf.Abs(p.x) <= 7) ? Pal.Wood[3] : plank(p));   // wheel lines worn pale
                if (z % 7 == 0) g.Box(-12, 0, z, 12, 0, z, Pal.Ramp(Pal.Wood, 0, 3403));
            }
            foreach (int x in new[] { -9, -3, 3, 9 }) g.Box(x, -2, -L + 1, x, -1, 0, Pal.Ramp(Pal.Wood, 1, 3404 + x));   // stringers
            foreach (int x in new[] { -12, 12 })
            {
                g.Box(x, 1, -L + 1, x, 1, 0, Pal.Ramp(Pal.Wood, 1, 3410));                               // kerb log
                for (int z = 0; z > -L; z -= 7) g.Box(x, 2, z, x, 6, z, Pal.Ramp(Pal.Wood, 2, 3411));      // posts
                g.Box(x, 6, -L + 1, x, 6, 0, Pal.Ramp(Pal.Wood, 3, 3412));                               // top rail
                g.Box(x, 4, -L + 1, x, 4, 0, Pal.Ramp(Pal.Wood, 2, 3413));                               // mid rail
            }
            foreach (int pz in new[] { -2, -25, -47 })
            {
                foreach (int x in new[] { -9, 9 }) g.CylY(x, pz, 1.3f, -25, -2, Pal.Ramp(Pal.Wood, 1, 3420 + pz));   // log piles
                g.Box(-11, -3, pz - 1, 11, -3, pz + 1, Pal.Ramp(Pal.Wood, 0, 3421));                          // cap beam
                g.Tube(new Vector3(-9, -22, pz), new Vector3(9, -5, pz), 0.5f, Pal.Ramp(Pal.Wood, 1, 3422));  // cross braces
                g.Tube(new Vector3(9, -22, pz), new Vector3(-9, -5, pz), 0.5f, Pal.Ramp(Pal.Wood, 1, 3423));
            }
            g.Mat(Iron);
            foreach (int pz in new[] { -2, -25, -47 }) foreach (int x in new[] { -10, -8, 8, 10 }) g.Set(x, -3, pz + 1, Pal.Solid(Pal.Metal[3]));   // bolts
            return g;
        }

        /// <summary>12 × 4 m steel girder bridge (0.16 m voxels) spanning from the origin along −Z: a riveted through-girder
        /// each side (the parapet), cross girders under a steel deck plate with hazard edges, concrete abutments at both
        /// ends and two braced steel trestles that reach 4 m down.</summary>
        static VoxelGrid SteelBridgeGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            const int L = 75;
            var primer = Pal.Weathered(Pal.Ochre, 0.4f, 3431, 1, 0);
            g.Box(-11, 0, -L + 1, 11, 0, 0, p => Mathf.Abs(p.x) >= 10 ? (((p.z / 2) & 1) == 0 ? Pal.Ochre[4] : Pal.Black[1]) : ((p.x + p.z) % 3 == 0 ? Pal.Metal[3] : Pal.Metal[2]));   // checker plate
            for (int z = 0; z > -L; z -= 6) g.Box(-11, -3, z, 11, -1, z, primer);                         // cross girders
            foreach (int x in new[] { -12, 12 })
            {
                int o = x > 0 ? 1 : -1;
                g.Box(x, -4, -L + 1, x, 4, 0, p => (p.y == 0 || p.y == 3) && p.z % 4 == 0 ? Pal.Chrome[1] : primer(p));   // web, rivets
                foreach (int y in new[] { -4, 4 }) g.Box(x - 1, y, -L + 1, x + 1, y, 0, primer);          // flanges
                for (int z = -3; z > -L; z -= 9) g.Box(x + o, -3, z, x + o, 3, z, Pal.Ramp(Pal.Rust, 2, 3432));   // stiffeners
            }
            g.Mat(Concrete);
            foreach (int z0 in new[] { 0, -L + 3 })
                g.Box(-13, -12, z0 - 3, 13, -5, z0, p => p.y == -5 ? ConcRamp[0] : Pal.Pick(ConcRamp, p, 3433, 2));   // abutments
            g.Mat(Iron);
            foreach (int pz in new[] { -25, -50 })
            {
                foreach (int x in new[] { -9, 9 }) g.Box(x - 1, -25, pz - 1, x, -5, pz, Pal.Weathered(Pal.Metal, 0.5f, 3434 + x, 2, 0));   // columns
                g.Box(-11, -5, pz - 1, 11, -4, pz, primer);                                                // cap
                g.Tube(new Vector3(-9, -24, pz), new Vector3(9, -7, pz), 0.5f, Pal.Ramp(Pal.Rust, 1, 3435));   // X bracing
                g.Tube(new Vector3(9, -24, pz), new Vector3(-9, -7, pz), 0.5f, Pal.Ramp(Pal.Rust, 1, 3436));
                g.Mat(Concrete); g.Box(-11, -26, pz - 2, 11, -25, pz + 1, Pal.Ramp(ConcRamp, 1, 3437)); g.Mat(Iron);   // footing
            }
            return g;
        }
    }
}
