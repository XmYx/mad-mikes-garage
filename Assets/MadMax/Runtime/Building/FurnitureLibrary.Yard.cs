using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;

namespace MadMax.Building
{
    /// <summary>Yard chores (scheduled update 2026-10-18): the slop bucket by the kitchen door that the pigs empty
    /// (<see cref="SlopBucket"/>).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> YardPieces()
        {
            yield return D("slop_bucket", "SLOP BUCKET", BuildCategory.Garden, SlopBucketGrid(), 2, false, go => go.AddComponent<SlopBucket>(),
                (ResourceType.Wood, 3), (ResourceType.Scrap, 1));
            yield return D("clothes_line", "CLOTHES LINE", BuildCategory.Garden, ClothesLineGrid(), 2, false, go => go.AddComponent<ClothesLine>(),
                (ResourceType.Wood, 4), (ResourceType.Cloth, 1));
        }

        /// <summary>Two weathered T-posts 1.8 m apart with a sagging cord between the crossbars and a peg bag on one post.</summary>
        static VoxelGrid ClothesLineGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -11, 11 })
            {
                g.Box(x, 0, 0, x, 22, 0, Pal.Weathered(Pal.Wood, 0.5f, 4710 + x, 1, 0));                    // post
                g.Box(x, 22, -2, x, 22, 2, Pal.Ramp(Pal.Wood, 2, 4711));                                        // crossbar
                g.Box(x - 1, 0, -1, x + 1, 1, 1, Pal.Ramp(Pal.Metal, 0, 4712));                                 // footing stones
            }
            g.Mat(Cloth);
            for (int x = -10; x <= 10; x++)
            {
                int sag = x * x < 16 ? 1 : 0;                                                                   // the cord sags in the middle
                g.Set(x, 21 - sag, 0, Pal.Ramp(Pal.Cream, 2, 4713));
            }
            g.Box(-10, 17, 1, -9, 19, 2, Pal.Ramp(Pal.Moss, 2, 4714));                                         // peg bag
            g.Bevel();
            return g;
        }

        /// <summary>A stave bucket with two iron hoops and a wire bail, slop stains down one side.</summary>
        static VoxelGrid SlopBucketGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -3; x <= 3; x++)
                for (int z = -3; z <= 3; z++)
                {
                    float r = UnityEngine.Mathf.Sqrt(x * x + z * z);
                    if (r > 3.4f) continue;
                    g.Set(x, 0, z, Pal.Ramp(Pal.Wood, 0, 4700));                                                         // bottom
                    if (r < 2.4f) continue;
                    var stave = (x * 7 + z * 3 & 1) == 0 ? Pal.Ramp(Pal.Wood, 1, 4701) : Pal.Ramp(Pal.Wood, 2, 4702);
                    g.Box(x, 1, z, x, 5, z, stave);                                                                      // staves
                    if (x + z > 2) g.Set(x, 1, z, Pal.Solid(Pal.Moss[0]));                                               // slop down the side
                }
            g.Mat(Iron);
            for (int x = -3; x <= 3; x++)
                for (int z = -3; z <= 3; z++)
                {
                    float r = UnityEngine.Mathf.Sqrt(x * x + z * z);
                    if (r > 3.4f || r < 2.4f) continue;
                    g.Set(x, 1, z, Pal.Weathered(Pal.Metal, 0.6f, 4703, 1, 0));                                          // hoops
                    g.Set(x, 4, z, Pal.Weathered(Pal.Metal, 0.6f, 4704, 1, 0));
                }
            g.Box(-3, 6, 0, -3, 6, 0, Pal.Ramp(Pal.Black, 1)); g.Box(3, 6, 0, 3, 6, 0, Pal.Ramp(Pal.Black, 1));      // bail ears
            g.Box(-2, 7, 0, 2, 7, 0, Pal.Ramp(Pal.Metal, 0, 4705));                                                     // bail
            g.Bevel();
            return g;
        }
    }
}
