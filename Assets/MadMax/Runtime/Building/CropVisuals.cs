using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Procedural plants per crop and growth stage (0 sprout .. 3 ripe), cached: voxel, or with the HD pack
    /// smooth leaves, stalks, canopies and fruit (<see cref="MadMax.Rendering.HDShapes"/>, two-sided
    /// <see cref="Material"/>).</summary>
    public static class CropVisuals
    {
        static readonly Dictionary<(string, int), Mesh> cache = new Dictionary<(string, int), Mesh>();

        /// <summary>The material the plant meshes need (HD foliage with the HD pack, else <paramref name="voxel"/>).</summary>
        public static Material Material(Material voxel) => HD ? MadMax.Rendering.HDShapes.Foliage ?? voxel : voxel;

        static bool HD => MadMax.Rendering.HDAssets.Enabled && MadMax.Rendering.HDShapes.Foliage;

        public static Mesh Get(CropDef c, int stage)
        {
            if (cache.TryGetValue((c.seed, stage), out var m) && m) return m;
            if (HD) { m = BuildHD(c, stage); cache[(c.seed, stage)] = m; return m; }
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            var rnd = new System.Random(c.seed.GetHashCode() + stage);
            int h = Mathf.Max(2, Mathf.RoundToInt(c.height * (0.25f + stage * 0.25f)));
            var leaf = Pal.Solid(c.leaf);
            var leafDark = Pal.Solid(new Color32((byte)(c.leaf.r * 0.7f), (byte)(c.leaf.g * 0.7f), (byte)(c.leaf.b * 0.7f), 255));
            if (c.tree)
            {
                int trunk = Mathf.Max(2, h * 2 / 3);
                g.Box(0, 0, 0, 0, trunk, 0, Pal.Ramp(Pal.Wood, 1));
                int r = Mathf.Max(1, h / 3);
                for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++) for (int z = -r; z <= r; z++)
                    if (x * x + y * y + z * z <= r * r && rnd.NextDouble() > 0.15) g.Set(x, trunk + y + 1, z, rnd.NextDouble() > 0.5 ? leaf : leafDark);
                if (stage >= 3 && c.regrowMinutes > 0f)
                    for (int i = 0; i < 6; i++) g.Set(rnd.Next(-r, r + 1), trunk + rnd.Next(-r + 1, r), r + 1, Pal.Solid(c.fruit));
            }
            else
            {
                for (int k = 0; k < 3 + stage; k++)
                {
                    int x = rnd.Next(-2, 3), z = rnd.Next(-2, 3);
                    int top = Mathf.Max(1, h - rnd.Next(0, 3));
                    for (int y = 0; y < top; y++) g.Set(x, y, z, y % 2 == 0 ? leaf : leafDark);
                    g.Set(x + 1, top - 1, z, leaf); g.Set(x - 1, top - 2 > 0 ? top - 2 : 0, z, leafDark);
                    if (stage >= 3) g.Set(x, top, z, Pal.Solid(c.fruit));
                }
                if (stage >= 3 && (c.seed == "seed_pumpkin" || c.seed == "seed_cabbage"))
                    g.Box(-1, 0, -1, 1, 2, 1, Pal.Solid(c.fruit));
            }
            g.Bevel();
            m = VoxelMesher.Build(g, "Crop_" + c.seed + stage, 0.06f);
            cache[(c.seed, stage)] = m;
            return m;
        }

        static Mesh BuildHD(CropDef c, int stage)
        {
            var b = new MadMax.Rendering.HDShapes { swayBase = 0f, swayScale = 300f };
            var rnd = new System.Random(c.seed.GetHashCode() + stage);
            float R() => (float)rnd.NextDouble();
            float h = Mathf.Max(0.08f, c.height * 0.06f * (0.25f + stage * 0.25f));
            var leaf = c.leaf; var dark = MadMax.Rendering.HDShapes.Tone(c.leaf, 0.7f);
            if (c.tree)
            {
                float trunk = h * 0.62f, cr = Mathf.Max(0.08f, h * 0.3f);
                var top = new Vector3((R() - 0.5f) * 0.04f, trunk, (R() - 0.5f) * 0.04f);
                b.Tube(Vector3.zero, top, Mathf.Max(0.015f, h * 0.05f), Mathf.Max(0.01f, h * 0.03f), new Color32(0x6a, 0x4a, 0x2e, 255), 7);
                int lumps = 3 + stage;
                for (int k = 0; k < lumps; k++)
                {
                    var o = top + new Vector3((R() - 0.5f) * cr * 1.2f, cr * (0.4f + R() * 0.6f), (R() - 0.5f) * cr * 1.2f);
                    b.Ellipsoid(o, Vector3.one * cr * (0.6f + R() * 0.4f), k % 2 == 0 ? leaf : dark, 9, 6);
                }
                if (stage >= 3 && c.regrowMinutes > 0f)
                    for (int k = 0; k < 8; k++)
                    {
                        float a = R() * 6.283f;
                        b.Ellipsoid(top + new Vector3(Mathf.Cos(a) * cr * 1.05f, cr * (0.3f + R() * 0.8f), Mathf.Sin(a) * cr * 1.05f), Vector3.one * Mathf.Max(0.02f, cr * 0.13f), c.fruit, 6, 4);
                    }
            }
            else
            {
                bool tall = c.height >= 12, ground = c.seed == "seed_pumpkin" || c.seed == "seed_cabbage" || c.seed == "seed_melon";
                int stems = tall ? 1 + stage / 2 : 1;
                for (int s = 0; s < stems; s++)
                {
                    var root = new Vector3((R() - 0.5f) * 0.12f, 0f, (R() - 0.5f) * 0.12f);
                    var top = root + new Vector3(0f, tall ? h : h * 0.35f, 0f);
                    if (tall) b.Tube(root, top, 0.012f, 0.007f, dark, 5);
                    int leaves = 4 + stage * 2;
                    for (int k = 0; k < leaves; k++)
                    {
                        float a = k * 2.4f + R(), y = tall ? h * (0.15f + 0.75f * k / leaves) : 0.01f;
                        float len = tall ? h * 0.45f : h * (0.7f + R() * 0.4f);
                        var at = root + Vector3.up * y;
                        b.Leaf(at, at + new Vector3(Mathf.Sin(a) * len, tall ? len * 0.35f : h * (0.6f + R() * 0.5f), Mathf.Cos(a) * len), len * (tall ? 0.18f : 0.38f), k % 2 == 0 ? leaf : dark, tall ? 0.7f : 0.35f);
                    }
                    if (stage >= 3 && !ground)
                    {
                        if (tall) b.Ellipsoid(top + Vector3.down * h * 0.3f + new Vector3(0.025f, 0f, 0f), new Vector3(0.025f, 0.07f, 0.025f), c.fruit, 7, 5);
                        else for (int k = 0; k < 4; k++)
                            b.Ellipsoid(root + new Vector3((R() - 0.5f) * h * 0.8f, h * (0.3f + R() * 0.5f), (R() - 0.5f) * h * 0.8f), Vector3.one * Mathf.Max(0.015f, h * 0.08f), c.fruit, 6, 4);
                    }
                }
                if (stage >= 3 && ground) b.Ellipsoid(new Vector3(0f, 0.07f, 0f), new Vector3(0.11f, 0.08f, 0.11f), c.fruit, 12, 6);
            }
            return b.ToMesh("CropHD_" + c.seed + stage);
        }
    }
}
