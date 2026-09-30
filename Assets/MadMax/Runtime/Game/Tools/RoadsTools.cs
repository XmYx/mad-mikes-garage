using MadMax.Items;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand tools for depth stage C (roads): the road rake (gravel patches, pothole patching), the tamper
    /// (cobbles from stone) and the line painter (white / yellow road paint). Registered in <see cref="ToolLibrary.Extra"/>.</summary>
    public static class RoadsTools
    {
        const float S = VoxelMesher.DefaultSize;

        public static void Register()
        {
            ToolLibrary.Extra["tool_rake"] = (Rake, (go, tip) =>
            {
                var t = go.AddComponent<PavingTool>();
                t.kind = DeformableTerrain.PaveGravel; t.material = ResourceType.Gravel; t.cost = 1; t.radius = 1.2f;
                t.swingDuration = 0.8f; t.strikeAt = 0.5f; t.style = ToolStyle.Slash;
                tip.localPosition = new Vector3(0, -17f * S, 0);
                return t;
            });
            ToolLibrary.Extra["tool_tamper"] = (Tamper, (go, tip) =>
            {
                var t = go.AddComponent<PavingTool>();
                t.kind = DeformableTerrain.PaveCobbles; t.material = ResourceType.Stone; t.cost = 2; t.radius = 0.8f; t.reach = 1.2f;
                t.swingDuration = 0.9f; t.strikeAt = 0.62f; t.style = ToolStyle.Overhead;
                tip.localPosition = new Vector3(0, -17f * S, 0);
                return t;
            });
            ToolLibrary.Extra["tool_line_painter"] = (Painter, (go, tip) =>
            {
                var t = go.AddComponent<LinePainterTool>();
                t.swingDuration = 0.35f; t.strikeAt = 0.15f; t.style = ToolStyle.Twist;
                tip.localPosition = new Vector3(0, -17f * S, 0);
                return t;
            });
        }

        /// <summary>Long ash handle, a steel bar of eleven tines across the end.</summary>
        static VoxelGrid Rake()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            g.Box(0, -15, 0, 0, 1, 0, Pal.Ramp(Pal.Wood, 2, 3201));
            g.Mat((byte)ResourceType.Iron);
            g.Box(-5, -16, 0, 5, -16, 0, Pal.Weathered(Pal.Metal, 0.4f, 3202, 2, 0));                  // head
            for (int x = -5; x <= 5; x += 2) g.Box(x, -18, 0, x, -17, 1, p => p.y == -18 ? Pal.Chrome[2] : Pal.Metal[2]);   // tines
            g.Box(-1, -15, 0, 1, -15, 0, Pal.Ramp(Pal.Rust, 2));                                        // ferrule
            return g;
        }

        /// <summary>Hand tamper: T-grip, steel shaft, a heavy square plate that beats cobbles into their bed.</summary>
        static VoxelGrid Tamper()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            g.Box(-2, 1, 0, 2, 1, 0, Pal.Ramp(Pal.Black, 1, 3211));                                     // T-grip
            g.Box(0, -14, 0, 0, 0, 0, Pal.Weathered(Pal.Metal, 0.35f, 3212, 2, 0));                    // shaft
            g.Box(-3, -17, -3, 3, -15, 3, p => p.y == -17 ? Pal.Chrome[1] : Pal.Pick(Pal.Metal, p, 3213, 2));   // plate
            g.Box(-1, -14, -1, 1, -14, 1, Pal.Ramp(Pal.Rust, 2));                                       // weld collar
            return g;
        }

        /// <summary>Spray lance on a white paint tin with a yellow band.</summary>
        static VoxelGrid Painter()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            g.Box(0, -2, 0, 0, 1, 0, Pal.Ramp(Pal.Black, 1, 3221));                                     // grip
            g.Box(0, -2, 1, 0, -1, 1, Pal.Solid(Pal.Metal[3]));                                          // trigger
            g.CylY(0, 0, 2.2f, -9, -3, p => p.y == -6 ? Pal.Ochre[4] : p.y == -3 || p.y == -9 ? Pal.Metal[2] : Pal.Cream[3]);   // paint tin
            g.Box(0, -16, 0, 0, -10, 0, Pal.Ramp(Pal.Chrome, 1, 3222));                                  // lance
            g.Box(-1, -17, -1, 1, -17, 1, Pal.Ramp(Pal.Metal, 1, 3223));                                 // nozzle hood
            g.Set(0, -17, 0, Pal.Solid(Pal.Cream[4]));
            return g;
        }
    }
}
