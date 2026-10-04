using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-04 tools: the fire extinguisher (<see cref="ExtinguisherTool"/>), the crutch
    /// (a light <see cref="MeleeTool"/> that lifts a one-legged hop, <see cref="WastelandGame.LimbSpeed"/>) and the
    /// handheld radio (<see cref="HandRadioTool"/>; it also carries the home frequency from the pack).</summary>
    public static class SafetyTools
    {
        public const string Extinguisher = "tool_extinguisher", Crutch = "tool_crutch", HandRadio = "tool_radio";

        public static void Register()
        {
            ItemIds.Register(Extinguisher, "FIRE EXTINGUISHER");
            ItemIds.Register(Crutch, "CRUTCH");
            ItemIds.Register(HandRadio, "HANDHELD RADIO");
            ToolLibrary.Extra[Extinguisher] = (ExtinguisherModel, (go, tip) =>
            {
                var t = go.AddComponent<ExtinguisherTool>();
                t.nozzle = tip; tip.localPosition = new Vector3(0f, -1f * VoxelMesher.DefaultSize, 3f * VoxelMesher.DefaultSize);
                t.swingDuration = 0.9f; t.strikeAt = 0.3f; t.style = ToolStyle.Gun;
                return t;
            });
            ToolLibrary.Extra[Crutch] = (CrutchModel, (go, tip) =>
            {
                var m = go.AddComponent<MeleeTool>();
                m.tip = tip; tip.localPosition = new Vector3(0f, -14f * VoxelMesher.DefaultSize, 0f);
                m.power = 0.3f; m.hitRadius = 0.25f; m.carveRadius = 0.08f; m.wearPerHit = 0.01f;
                m.swingDuration = 0.7f; m.strikeAt = 0.45f; m.style = ToolStyle.Thrust;
                return m;
            });
            ToolLibrary.Extra[HandRadio] = (RadioModel, (go, tip) =>
            {
                var r = go.AddComponent<HandRadioTool>();
                r.swingDuration = 0.5f; r.strikeAt = 0.4f; r.style = ToolStyle.Twist;
                return r;
            });
        }

        /// <summary>A red dry-powder bottle held by the valve, gauge and a short hose to the nozzle.</summary>
        static VoxelGrid ExtinguisherModel()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            g.Box(-1, 0, -1, 1, 0, 1, Pal.Ramp(Pal.Black, 1));                                    // valve and squeeze lever
            g.Box(-2, 1, 0, 1, 1, 0, Pal.Ramp(Pal.Black, 2));
            g.CylY(0, 0, 1.8f, -10, -1, p => p.y == -1 ? Pal.Crimson[3] : p.y == -10 ? Pal.Crimson[0] : Pal.Crimson[(p.x + p.z + 4) % 3 + 1]);
            g.Box(-1, -6, 2, 1, -4, 2, Pal.Ramp(Pal.Cream, 2));                                   // instruction label
            g.Set(1, -2, 2, Pal.Ramp(Pal.Chrome, 3));                                             // pressure gauge
            g.Tube(new Vector3(0, 0, 1), new Vector3(0, -1, 3), 0.4f, Pal.Ramp(Pal.Black, 1));    // hose to the nozzle
            return g;
        }

        /// <summary>A wooden forearm crutch: the grip in the hand, a padded cuff above, a rubber foot below.</summary>
        static VoxelGrid CrutchModel()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Black, 1));                                     // hand grip
            g.Box(0, 1, 0, 0, 5, 0, Pal.Ramp(Pal.Wood, 2));                                       // upper shaft
            g.Mat((byte)ResourceType.Cloth);
            g.Box(-1, 5, -1, 1, 6, 1, Pal.Ramp(Pal.Olive, 1));                                    // padded cuff
            g.Mat((byte)ResourceType.Wood);
            g.Box(0, -13, 0, 0, -1, 0, p => p.y % 4 == 0 ? Pal.Wood[1] : Pal.Wood[3]);            // shaft, lashed every few hands
            g.Mat((byte)ResourceType.Rubber);
            g.Box(0, -14, 0, 0, -14, 0, Pal.Ramp(Pal.Black, 0));                                  // rubber foot
            return g;
        }

        /// <summary>A brick of a handheld set with a whip antenna and a speaker grille.</summary>
        static VoxelGrid RadioModel()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Scrap);
            g.Box(-1, -5, -1, 1, 0, 1, Pal.Weathered(Pal.Olive, 0.2f, 1431, 2, 0));
            for (int y = -4; y <= -2; y++) g.Set(0, y, 1, Pal.Ramp(Pal.Black, (y & 1) + 1));     // grille
            g.Set(1, -1, 1, Pal.Ramp(Pal.Chrome, 3));                                             // tuning knob
            g.Box(-1, 1, 0, -1, 6, 0, Pal.Ramp(Pal.Black, 2));                                    // whip antenna
            return g;
        }
    }
}
