using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand tools for depth stage E: the scythe (grass and grain into hay) and the shears (wool), registered in
    /// <see cref="ToolLibrary.Extra"/>. Models are held along the arm, -Y away from the hand.</summary>
    public static class HusbandryTools
    {
        const float S = VoxelMesher.DefaultSize;

        public static void Register()
        {
            ToolLibrary.Extra["tool_scythe"] = (Scythe, (go, tip) =>
            {
                tip.localPosition = new Vector3(0f, -26f * S, 0.4f);
                var t = go.AddComponent<ScytheTool>();
                t.style = ToolStyle.Slash; t.swingDuration = 0.8f; t.strikeAt = 0.5f;
                return t;
            });
            ToolLibrary.Extra["tool_shears"] = (ShearsModel, (go, tip) =>
            {
                tip.localPosition = new Vector3(0f, -9f * S, 0f);
                var t = go.AddComponent<ShearsTool>();
                t.style = ToolStyle.Twist; t.swingDuration = 0.7f; t.strikeAt = 0.45f;
                return t;
            });
        }

        /// <summary>A long ash snath with two hand nibs and a curved blade at the end.</summary>
        static VoxelGrid Scythe()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            g.Box(0, -25, 0, 0, 4, 0, Pal.Ramp(Pal.Wood, 2, 3601));                               // snath
            g.Box(0, -1, 1, 0, -1, 2, Pal.Ramp(Pal.Wood, 3, 3602));                               // upper nib
            g.Box(0, -12, 1, 0, -12, 2, Pal.Ramp(Pal.Wood, 3, 3603));                             // lower nib
            g.Mat((byte)MadMax.Items.ResourceType.Iron);
            g.Box(-1, -26, -1, 1, -25, 1, Pal.Ramp(Pal.Metal, 1, 3604));                          // tang ring
            for (int z = 1; z <= 11; z++)
            {
                int y = -26 + (z * z) / 30;                                                           // the blade sweeps up to its tip
                g.Set(0, y, z, Pal.Solid(Pal.Chrome[3]));                                            // edge
                if (z < 11) g.Set(0, y + 1, z, Pal.Ramp(Pal.Metal, 2, 3605));
            }
            return g;
        }

        /// <summary>Hand shears: a sprung bow at the grip and two broad blades.</summary>
        static VoxelGrid ShearsModel()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Metal, 1, 3611));                               // spring bow
            g.Box(-1, -3, 0, -1, -1, 0, Pal.Ramp(Pal.Metal, 2, 3612)); g.Box(1, -3, 0, 1, -1, 0, Pal.Ramp(Pal.Metal, 2, 3613));
            g.Box(-1, -4, 0, 1, -4, 0, Pal.Ramp(Pal.Rust, 2));                                    // pivot band
            g.Box(-1, -9, 0, -1, -5, 0, Pal.Ramp(Pal.Chrome, 2, 3614)); g.Box(1, -9, 0, 1, -5, 0, Pal.Ramp(Pal.Chrome, 2, 3615));
            g.Set(0, -10, 0, Pal.Solid(Pal.Chrome[3]));                                              // points
            return g;
        }
    }
}
