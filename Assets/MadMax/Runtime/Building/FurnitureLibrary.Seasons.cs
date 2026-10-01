using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Store the harvest (Seasons): a drying rack (station <c>drying_rack</c>: sun and wind, no fuel) and a
    /// pickling crock (station <c>crock</c>: salt brine). The root cellar that keeps the produce is a Utilities piece
    /// (<c>root_cellar</c>, <see cref="Container.keep"/>).</summary>
    public static partial class FurnitureLibrary
    {
        const byte ClayMat = (byte)ResourceType.Clay;

        static IEnumerable<FurnitureDef> SeasonsPieces()
        {
            var W = ResourceType.Wood; var St = ResourceType.Stone; var C = ResourceType.Cloth; var Cl = ResourceType.Clay;
            yield return D("drying_rack", "DRYING RACK", BuildCategory.Garden, DryingRackGrid(), 3, false,
                go => Station(go, "drying_rack", "DRY FOOD (RACK)", 0f).output = new Vector3(0f, 0.7f, 0.4f), (W, 6), (C, 2));
            yield return D("pickling_crock", "PICKLING CROCK", BuildCategory.Furniture, CrockGrid(), 2, false,
                go => Station(go, "crock", "PICKLE (CROCK)", 0f).output = new Vector3(0.3f, 0.5f, 0.2f), (Cl, 4), (St, 1), (W, 1));
        }

        /// <summary>A plank A-frame with four slatted trays under a cloth fly: apple rings, tomatoes, corn cobs and a
        /// split fish hang to dry.</summary>
        static VoxelGrid DryingRackGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var post = Pal.Ramp(Pal.Wood, 1, 4101);
            foreach (int x in new[] { -9, 9 })
            {
                for (int y = 0; y <= 16; y++) { g.Set(x, y, -4 + y / 5, post); g.Set(x, y, 4 - y / 5, post); }   // splayed legs
                g.Box(x, 16, -1, x, 17, 1, post);
            }
            g.Box(-10, 17, 0, 10, 17, 0, Pal.Ramp(Pal.Wood, 2, 4102));                                  // ridge pole
            for (int t = 0; t < 4; t++)
            {
                int y = 3 + t * 4, half = 4 - (y / 5);
                g.Box(-8, y, -half, 8, y, half, p => (p.x & 1) == 0 ? Pal.Wood[2] : Pal.Wood[1]);     // slatted tray
            }
            // produce on the trays
            for (int x = -7; x <= 7; x += 2) g.Set(x, 4, (x / 2) % 2, Pal.Ramp(Pal.Crimson, 3, 4103));                          // apple rings
            for (int x = -6; x <= 6; x += 3) g.Set(x, 8, 0, Pal.Ramp(Pal.Crimson, 2, 4104));                                    // tomatoes
            for (int x = -7; x <= 5; x += 4) g.Box(x, 12, 0, x + 1, 12, 0, Pal.Ramp(Pal.Ochre, 3, 4105));                       // corn
            g.Box(-3, 14, 0, 3, 16, 0, p => p.y == 16 ? Pal.Chrome[1] : Pal.Cream[1]);                                        // split fish hanging
            // cloth fly against flies and rain
            g.Mat(Cloth);
            for (int z = -5; z <= 5; z++) g.Box(-10, 18 - Mathf.Abs(z) / 2, z, 10, 18 - Mathf.Abs(z) / 2, z, p => (p.x / 4 & 1) == 0 ? Pal.Cream[2] : Pal.Cream[1]);
            return g;
        }

        /// <summary>A glazed stoneware crock with a plank lid weighted by a river stone, two sealed jars beside it.</summary>
        static VoxelGrid CrockGrid()
        {
            var g = new VoxelGrid().Mat(ClayMat);
            g.CylY(0, 0, 3.6f, 0, 7, p => p.y == 5 ? Pal.Navy[2] : p.y < 2 ? Pal.Ochre[1] : Pal.Weathered(Pal.Cream, 0.08f, 4111, 1, 0)(p));   // glazed body, blue band
            g.CylY(0, 0, 2.6f, 8, 8, Pal.Ramp(Pal.Ochre, 2, 4112));                                                             // rim
            g.Mat(Wood); g.CylY(0, 0, 2.4f, 9, 9, Pal.Ramp(Pal.Wood, 2, 4113));                                                   // lid
            g.Mat(Stone); g.Box(-1, 10, -1, 1, 11, 1, Pal.Ramp(Pal.Fur, 2, 4114));                                                // weight stone
            g.Mat(Glass);
            foreach (var (x, c) in new[] { (5, Pal.Crimson), (7, Pal.Moss) })
            {
                g.CylY(x, 2, 1.1f, 0, 3, Pal.Ramp(c, 2, 4115 + x));                                                              // jar of beets, of kraut
                g.Set(x, 4, 2, Pal.Solid(Pal.Chrome[2]));                                                                        // lid
            }
            return g;
        }
    }
}
