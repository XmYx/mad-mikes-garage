using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;

namespace MadMax.Building
{
    /// <summary>Winter starts (scheduled update 2026-10-07): the engine block heater — a powered post with a cord that
    /// keeps a vehicle parked beside it warm (<see cref="BlockHeater"/>).</summary>
    public static partial class FurnitureLibrary
    {
        const byte WiRubber = (byte)ResourceType.Rubber;

        static IEnumerable<FurnitureDef> WinterPieces()
        {
            yield return D("block_heater", "BLOCK HEATER", BuildCategory.Utility, WiHeaterGrid(), 4, false, go =>
            {
                Node(go, UtilityKind.Power, 0.6f);
                go.AddComponent<BlockHeater>();
            }, (ResourceType.Copper, 3), (ResourceType.Scrap, 2), (ResourceType.Rubber, 1));
        }

        static VoxelGrid WiHeaterGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(0, 0, 0, 1, 12, 1, Pal.Ramp(Pal.Wood, 1, 4600));                                                          // post
            g.Mat(Iron);
            g.Box(-2, 8, 2, 3, 13, 3, Pal.Weathered(Pal.RigGreen, 0.3f, 4601, 3, 0));                                         // outlet box
            g.Box(-1, 14, 2, 2, 14, 3, Pal.Ramp(Pal.Metal, 1, 4602));                                                        // rain lid
            g.Mat(Glass); g.Set(2, 12, 4, Pal.Solid(Pal.Amber)); g.Mat(Iron);                                             // pilot lamp
            g.Box(-1, 10, 4, 0, 11, 4, Pal.Ramp(Pal.Black, 1));                                                              // socket
            g.Mat(WiRubber);
            for (int y = 3; y <= 9; y++) g.Set(y % 2 == 0 ? -1 : -2, y, 4, Pal.Solid(Pal.Ochre[3]));                         // coiled cord
            g.Box(-3, 2, 4, -1, 2, 5, Pal.Solid(Pal.Ochre[2]));
            g.Box(-4, 2, 6, -4, 3, 7, Pal.Ramp(Pal.Black, 2));                                                               // plug
            g.Mat(Copper); g.Box(0, 6, 2, 1, 7, 2, Pal.Solid(Pal.Bronze[2]));                                                // cable gland
            return g;
        }
    }
}
