using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>How each tool is carried at rest: the arm overlay clip (tools/blender/hd/character/clips.py HOLDS, fitted
    /// so neither the arms nor the tool pass through the body). By id first, then by kind: guns by length, light tools
    /// raised, long two-handed handles on the shoulder, everything else held at the side pointing ahead and down.</summary>
    public static class ToolHolds
    {
        public const string Side = "hold_side", Shoulder = "hold_shoulder", Staff = "hold_staff", Port = "hold_port",
            Torch = "hold_torch", Flash = "hold_flash", Hang = "hold_hang", Rifle = "hold_rifle", Pistol = "hold_pistol";

        static readonly Dictionary<string, string> ById = new Dictionary<string, string>
        {
            { "tool_sledgehammer", Shoulder }, { "tool_pickaxe", Shoulder }, { "tool_axe", Shoulder }, { "tool_shovel", Shoulder },
            { "tool_hoe", Shoulder }, { "tool_scythe", Shoulder }, { "tool_leaf_blade", Shoulder },
            { "tool_spear", Staff }, { "tool_fishing_rod", Staff },
            { "tool_cutter", Hang }, { "tool_detector", Side },
            { "tool_torch", Torch }, { "tool_flashlight", Flash },
            { "tool_lantern", Hang }, { "tool_watering_can", Hang }, { "tool_extinguisher", Hang }, { "tool_jack", Hang },
            { "tool_fuel_can", Hang }, { "tool_gold_pan", Hang },
            { "tool_pipe_shotgun", Rifle }, { "tool_bolt_rifle", Rifle }, { "tool_crossbow", Rifle },
            { "tool_pipe_pistol", Pistol }, { "tool_revolver", Pistol }, { "tool_flare_gun", Pistol }, { "tool_slingshot", Pistol },
            { "tool_bow", Side },
        };

        public static string For(HandTool t)
        {
            if (!t || t.id == null) return null;
            if (ById.TryGetValue(t.id, out var h)) return h;
            if (t.id.EndsWith("_can")) return Hang;
            if (t.style == ToolStyle.Gun) return t is RangedTool r && r.muzzle && r.muzzle.localPosition.y < -0.9f ? Rifle : Pistol;
            if (t is LightTool) return Torch;
            if (t is MeleeTool m && m.tip && m.tip.localPosition.y < -0.9f && t.TwoHanded)
                return t.style == ToolStyle.Thrust ? Staff : t.style == ToolStyle.Overhead ? Shoulder : Port;
            return t.IdlePose.HasValue ? null : Side;
        }

        /// <summary>The hold's clip if installed (null: the procedural poses).</summary>
        public static string Clip(HandTool t)
        {
            var h = t ? t.Hold : null;
            return h != null && HumanClips.Enabled && HumanClips.Get(h) != null ? h : null;
        }
    }
}
