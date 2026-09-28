using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Wasteland set dressing. Ground is authored in camera-aligned space (+Z = away from camera).</summary>
    public static class Scenery
    {
        public static VoxelGrid Ground(int halfX, int nearZ, int farZ, int clearRadius, int seed)
        {
            var g = new VoxelGrid();
            for (int x = -halfX; x <= halfX; x++)
            for (int z = nearZ; z <= farZ; z++)
            {
                var p = new Vector3Int(x, 0, z);
                float tone = Pal.Noise(new Vector3(x * 0.035f, 0, z * 0.05f), seed);
                float crack = Mathf.Abs(Pal.Noise(new Vector3(x * 0.09f, 3, z * 0.09f), seed + 1) - 0.5f);
                float streak = Pal.Noise(new Vector3(x * 0.012f, 7, z * 0.2f), seed + 2);
                int bias = tone > 0.62f ? 3 : tone > 0.4f ? 2 : 1;
                if (streak > 0.7f) bias = Mathf.Min(4, bias + 1);
                Color32 c = Pal.Pick(Pal.Sand, p, seed + 3, bias);
                if (crack < 0.018f) c = Pal.Rust[1];
                g.Set(x, -1, z, Pal.Solid(c));
                g.Set(x, -2, z, Pal.Solid(Pal.Sand[0]));

                bool nearCars = x * x + z * z < clearRadius * clearRadius;
                float dune = Pal.Noise(new Vector3(x * 0.04f, 11, z * 0.07f), seed + 4);
                if (!nearCars && dune > 0.66f)
                {
                    g.Set(x, 0, z, Pal.Solid(Pal.Pick(Pal.Sand, p, seed + 5, Mathf.Min(4, bias + 1))));
                    if (dune > 0.76f) g.Set(x, 1, z, Pal.Solid(Pal.Pick(Pal.Sand, p, seed + 6, Mathf.Min(4, bias + 1))));
                }
                float h = Pal.Hash(p, seed + 7);
                if (!nearCars && h > 0.994f) g.Set(x, 0, z, Pal.Solid(Pal.Hash(p, 8) > 0.5f ? Pal.Rust[1] : Pal.Metal[2]));  // pebbles
                else if (!nearCars && h > 0.9915f) { g.Set(x, 0, z, Pal.Solid(Pal.Rust[0])); g.Set(x, 1, z, Pal.Solid(Pal.Rust[1])); } // dead scrub
            }
            return g;
        }

        public static VoxelGrid DeadTree(int seed, int height) { var g = new VoxelGrid(); DeadTree(g, seed, height); return g; }

        /// <summary>Paint a dead tree into an existing grid (keeps the grid's current material).</summary>
        public static void DeadTree(VoxelGrid g, int seed, int height)
        {
            var bark = Pal.Ramp(Pal.Rust, 0, seed);
            g.Tube(Vector3.zero, new Vector3(0, height, 0), 0.6f, bark);
            var rnd = new System.Random(seed);
            for (int i = 0; i < 5; i++)
            {
                float y = height * (0.45f + 0.12f * i);
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                float len = height * (0.25f + 0.3f * (float)rnd.NextDouble());
                var start = new Vector3(0, y, 0);
                var end = start + new Vector3(Mathf.Cos(a) * len, len * 0.8f, Mathf.Sin(a) * len * 0.4f);
                g.Tube(start, end, 0.3f, bark);
                g.Tube(end, end + new Vector3(Mathf.Cos(a + 0.8f) * len * 0.4f, len * 0.4f, 0), 0f, bark);
            }
        }

        /// <summary>Flat-ish horizon silhouettes: mesas and buttes.</summary>
        public static VoxelGrid Mesas(int halfX, int seed, Color32[] ramp)
        {
            var g = new VoxelGrid();
            for (int x = -halfX; x <= halfX; x++)
            {
                float n = Pal.Noise(new Vector3(x * 0.06f, 0, 0), seed);
                float n2 = Pal.Noise(new Vector3(x * 0.25f, 5, 0), seed);
                int h = n > 0.55f ? Mathf.RoundToInt(6 + (n - 0.55f) * 30 + n2 * 2) : Mathf.RoundToInt(1 + n * 3 + n2);
                for (int y = 0; y <= h; y++)
                for (int z = 0; z <= 1; z++)
                    g.Set(x, y, z, Pal.Solid(y == h && n > 0.55f ? ramp[2] : ramp[(x + y) % 5 == 0 ? 0 : 1]));
            }
            return g;
        }

        /// <summary>Banded sunset sky wall with dithered transitions and cloud streaks.</summary>
        public static VoxelGrid Sky(int halfX, int height, int seed)
        {
            var bands = new[] { Pal.Hex("f2b25e"), Pal.Hex("eea052"), Pal.Hex("e98e47"), Pal.Hex("e27e3e"), Pal.Hex("d86e36"), Pal.Hex("cc6030") };
            var cloud = Pal.Hex("f6c574");
            var g = new VoxelGrid();
            for (int x = -halfX; x <= halfX; x++)
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)height * (bands.Length - 1);
                int b = Mathf.FloorToInt(t);
                float f = t - b;
                bool dither = f > 0.75f && ((x + y) & 1) == 0;
                Color32 c = bands[Mathf.Min(bands.Length - 1, b + (dither ? 1 : 0))];
                float s = Pal.Noise(new Vector3(x * 0.05f, y * 0.9f, 0), seed);
                if (y > 3 && s > 0.78f) c = cloud;
                else if (y > 3 && s > 0.72f && ((x + y) & 1) == 0) c = cloud;
                g.Set(x, y, 0, Pal.Solid(c));
            }
            return g;
        }
    }
}
