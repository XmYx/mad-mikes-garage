using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> Homestead()
        {
            yield return D("porch_awning", "PATCHWORK AWNING", BuildCategory.Furniture, PorchAwning(), 10, true,
                null, (ResourceType.Wood, 8), (ResourceType.Cloth, 6), (ResourceType.Scrap, 3));
            yield return D("porch_lights", "PORCH LANTERNS", BuildCategory.Decor, PorchLights(), 4, true,
                go => Glow(go, new Vector3(0, 2.3f, 0), Pal.LightW, 6f, 3f, false, 0f),
                (ResourceType.Wood, 3), (ResourceType.Scrap, 3), (ResourceType.Glass, 2));
            yield return D("herb_planter", "TIN HERB PLANTER", BuildCategory.Decor, HerbPlanter(), 3, false,
                null, (ResourceType.Scrap, 2), (ResourceType.Wood, 1));
        }

        static VoxelGrid PorchAwning()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            foreach (int x in new[] { -24, 24 }) foreach (int z in new[] { -17, 17 })
            {
                g.Box(x, 0, z, x + 1, 33, z + 1, Pal.Ramp(Pal.Wood, 2, 2801));
                g.Mat((byte)ResourceType.Scrap);
                g.Box(x - 1, 2, z - 1, x + 2, 3, z + 2, Pal.Ramp(Pal.Metal, 2, 2802));
                g.Mat((byte)ResourceType.Wood);
            }
            g.Box(-24, 32, -17, 25, 33, -16, Pal.Ramp(Pal.Wood, 3, 2803));
            g.Box(-24, 32, 17, 25, 33, 18, Pal.Ramp(Pal.Wood, 3, 2804));
            g.Mat((byte)ResourceType.Cloth);
            for (int z = -19; z <= 20; z++)
            {
                int y = 34 + Mathf.RoundToInt((1f - Mathf.Abs(z) / 20f) * 4f);
                g.Box(-26, y, z, 27, y, z, Pal.Stripe(Pal.Ramp(Pal.Cream, 2, 2805), Pal.Ramp(Pal.RigGreen, 3, 2806), 0, 12, 5));
            }
            // A hand-sewn replacement patch and a scalloped front valance.
            g.Box(7, 36, 8, 14, 36, 13, Pal.Ramp(Pal.Ochre, 2, 2807));
            for (int x = -26; x <= 27; x++)
                g.Box(x, 31 + ((x + 26) / 3 % 2), 20, x, 34, 20,
                    Pal.Stripe(Pal.Ramp(Pal.Cream, 2), Pal.Ramp(Pal.RigGreen, 3), 0, 12, 5));
            return g;
        }

        static VoxelGrid PorchLights()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            foreach (int x in new[] { -23, 23 }) g.Box(x, 0, 0, x, 34, 0, Pal.Ramp(Pal.Wood, 2, 2810));
            g.Mat((byte)ResourceType.Scrap);
            for (int x = -23; x <= 23; x++)
            {
                int y = 30 + Mathf.RoundToInt(x * x / 132f);
                g.Set(x, y, 0, Pal.Solid(Pal.Metal[1]));
                if ((x + 18) % 9 != 0) continue;
                g.Box(x - 1, y - 5, -1, x + 1, y - 5, 1, Pal.Ramp(Pal.Bronze, 2));
                g.Box(x - 1, y - 1, -1, x + 1, y - 1, 1, Pal.Ramp(Pal.Bronze, 2));
                g.Mat((byte)ResourceType.Glass);
                g.Box(x, y - 4, 0, x, y - 2, 0, Pal.Solid(Pal.LightW));
                g.Mat((byte)ResourceType.Scrap);
            }
            return g;
        }

        static VoxelGrid HerbPlanter()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Scrap);
            g.Box(-6, 0, -3, 6, 4, 3, Pal.Weathered(Pal.PaleBlue, 0.2f, 2820, 2));
            g.Mat((byte)ResourceType.Wood);
            g.Box(-5, 4, -2, 5, 4, 2, Pal.Ramp(Pal.Wood, 0, 2821));
            for (int x = -4; x <= 4; x += 4)
            {
                g.Box(x, 5, 0, x, 10, 0, Pal.Ramp(Pal.Moss, 3, 2822));
                g.Box(x - 1, 7, -1, x + 1, 8, 1, Pal.Ramp(Pal.Moss, 4, 2823));
                g.Set(x, 11, 0, Pal.Solid(Pal.Cream[3]));
            }
            return g;
        }
    }
}
