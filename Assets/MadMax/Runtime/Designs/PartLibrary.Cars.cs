using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;

namespace MadMax.Designs
{
    /// <summary>Small-car parts: the air-cooled four-stroke twin of the rear-engined city cars (Fiat 126p, Fiat 500).</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> CarParts()
        {
            yield return AirTwin();
        }

        /// <summary>650 cc air-cooled inline twin: finned barrels, a fan shroud and the cooling-air duct on top.</summary>
        static PartDesign AirTwin()
        {
            var g = new VoxelGrid();
            g.Box(-3, 0, 0, 3, 2, 5, Pal.Weathered(Pal.Metal, 0.3f, 2501, 1, 2));                                      // crankcase + sump
            g.Box(-2, 3, 1, 2, 5, 4, p => p.y % 2 == 0 ? Pal.Metal[3] : Pal.Metal[1]);                                // finned barrels
            g.Box(-2, 6, 1, 2, 6, 4, Pal.Ramp(Pal.Metal, 2, 2502));                                                    // heads
            g.Box(3, 2, 0, 4, 6, 5, Pal.Weathered(Pal.Ochre, 0.4f, 2503, 1, 0));                                       // fan shroud
            g.CylX(4, 2.5f, 1.4f, 5, 5, Pal.Ramp(Pal.Black, 1, 2504));                                                 // fan hub
            g.CylY(-1, 2.5f, 1.2f, 7, 7, Pal.Ramp(Pal.Black, 1, 2505));                                                // air cleaner
            for (int z = 1; z <= 4; z += 3) g.Set(-3, 4, z, Pal.Ramp(Pal.Rust, 1));                                    // exhaust ports
            var d = Make("engine_twin_air", PartCategory.Engine, g, 70, 1);
            d.torque = 70f; d.maxRpm = 5200f; d.peakAt = 0.65f;
            return d;
        }
    }
}
