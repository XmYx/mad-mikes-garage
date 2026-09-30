using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand tools for depth stages G and H: the gold pan (<see cref="GoldPanTool"/>), added to
    /// <see cref="ToolLibrary.Extra"/> in <see cref="Register"/>.</summary>
    public static class MedMineTools
    {
        public static void Register()
        {
            ToolLibrary.Extra["tool_gold_pan"] = (GoldPan, (go, tip) =>
            {
                var p = go.AddComponent<GoldPanTool>();
                tip.localPosition = new Vector3(0f, -5f * VoxelMesher.DefaultSize, 0f);
                p.swingDuration = 1.4f; p.strikeAt = 0.55f; p.style = ToolStyle.Twist;
                return p;
            });
        }

        /// <summary>A steel gold pan held by the rim: a shallow dish with riffled sides, gravel and a glint of colour.</summary>
        static VoxelGrid GoldPan()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            g.Box(0, -1, 0, 0, 0, 0, Pal.Ramp(Pal.Metal, 2));                                    // grip on the rim
            g.CylZ(0, -6, 5.2f, 0, 0, Pal.Ramp(Pal.Black, 2, 4901));                              // bottom
            g.CylZ(0, -6, 6.2f, 1, 2, p => (p.x + p.y) % 3 == 0 ? Pal.Black[3] : Pal.Black[1], 5.2f);   // flared rim with riffles
            g.Mat((byte)MadMax.Items.ResourceType.Sand);
            g.CylZ(0, -7, 2.4f, 1, 1, Pal.Ramp(Pal.Sand, 2, 4902));                               // gravel in the pan
            g.Mat((byte)MadMax.Items.ResourceType.Gold);
            g.Set(1, -6, 1, Pal.Solid(Pal.Ochre[4])); g.Set(-1, -8, 1, Pal.Solid(Pal.Ochre[3]));  // colour
            return g;
        }
    }
}
