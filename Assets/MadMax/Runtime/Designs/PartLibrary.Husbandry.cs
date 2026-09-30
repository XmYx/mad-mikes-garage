using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Vehicle parts for depth stage E: the tractor's hay baler (a three-point-hitch implement like the plough,
    /// <see cref="Machine.Kind.Tractor"/>). Lowered and driven over grass it cuts a 4 m swath into hay in the tractor's
    /// store (<see cref="MadMax.World.DeformableTerrain.Bale"/>).</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> HusbandryParts()
        {
            yield return Baler();
        }

        /// <summary>Round baler, authored from the hitch backwards: a drawbar, a tined pick-up across the swath, the
        /// bale drum on two wheels with its tailgate, and a finished bale of hay riding on the back.</summary>
        public static PartDesign Baler()
        {
            var g = new VoxelGrid();
            var paint = FarmPaint(3951);
            g.Box(-1, 5, -9, 1, 7, 0, Pal.Ramp(Pal.Metal, 1, 3952));                                // drawbar
            g.Box(-14, 1, -12, 14, 3, -9, Pal.Ramp(Pal.Metal, 1, 3953));                             // pick-up
            for (int x = -13; x <= 13; x += 2) g.Box(x, 0, -9, x, 1, -9, Pal.Solid(Pal.Chrome[2]));   // tines
            g.CylX(10, -19, 8f, -10, 10, paint);                                                     // bale chamber
            foreach (int x in new[] { -11, 11 }) g.CylX(10, -19, 8.6f, x, x, Pal.Ramp(Pal.Metal, 2, 3954));   // side plates
            g.CylX(10, -19, 2f, -12, 12, Pal.Ramp(Pal.Chrome, 1));                                    // hub
            g.Box(-10, 17, -19, 10, 18, -12, Pal.Ramp(Pal.Metal, 2, 3955));                          // hood over the pick-up
            foreach (int x in new[] { -14, 13 }) g.CylX(3.4f, -19, 3.4f, x, x + 1, Pal.Ramp(Pal.Tire, 1));   // wheels
            g.CylX(5, -30, 4.6f, -6, 6, p => (p.x & 1) == 0 ? Pal.Ochre[3] : Pal.Ochre[2]);          // a finished bale on the tailgate
            g.Box(-7, 0, -32, 7, 1, -26, Pal.Ramp(Pal.Metal, 1));                                    // tailgate ramp
            return Make("tool_baler", PartCategory.Tool, g, 800, 3);
        }
    }
}
