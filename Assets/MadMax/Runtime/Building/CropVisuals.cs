using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Procedural voxel plants per crop and growth stage (0 sprout .. 3 ripe), cached.</summary>
    public static class CropVisuals
    {
        static readonly Dictionary<(string, int), Mesh> cache = new Dictionary<(string, int), Mesh>();

        public static Mesh Get(CropDef c, int stage)
        {
            if (cache.TryGetValue((c.seed, stage), out var m) && m) return m;
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
    }
}
