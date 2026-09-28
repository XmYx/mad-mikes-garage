using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Tool/weapon definitions: voxel meshes (held along the arm, -Y = away from the hand) and stats.</summary>
    public static class ToolLibrary
    {
        // index = network tool id: append only
        public static readonly string[] Order = { ItemIds.Sledgehammer, ItemIds.Wrench, ItemIds.Cutter, ItemIds.PipeClub, ItemIds.Machete, ItemIds.Shotgun, ItemIds.ClawHammer, "tool_shovel", "tool_axe", "tool_pickaxe" };
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        const float S = VoxelMesher.DefaultSize;

        public static bool Has(string id) => System.Array.IndexOf(Order, id) >= 0;

        public static Mesh MeshFor(string id)
        {
            if (meshes.TryGetValue(id, out var m) && m) return m;
            var g = new VoxelGrid();
            switch (id)
            {
                case ItemIds.Wrench:
                    g.Box(0, -9, 0, 0, 0, 0, Pal.Weathered(Pal.Chrome, 0.3f, 701, 1, 0));
                    g.Box(-1, -11, 0, 1, -10, 0, Pal.Ramp(Pal.Chrome, 2)); g.Set(-1, -12, 0, Pal.Ramp(Pal.Chrome, 2)); g.Set(1, -12, 0, Pal.Ramp(Pal.Chrome, 2));
                    break;
                case ItemIds.Cutter:
                    g.Box(0, -8, 0, 0, 0, 0, Pal.Ramp(Pal.Metal, 1, 702));
                    g.Box(-1, -2, 0, 1, 0, 1, Pal.Ramp(Pal.Black, 1));                           // grip
                    g.Box(-1, -11, -1, 1, -9, 1, Pal.Solid(Pal.Amber));                          // motor
                    g.CylX(-12, 1, 2.6f, 2, 2, Pal.Ramp(Pal.Chrome, 2), 0.5f);                    // disc
                    break;
                case ItemIds.PipeClub:
                    g.Box(0, -11, 0, 0, 0, 0, Pal.Weathered(Pal.Metal, 0.5f, 703, 2, 0));
                    g.Box(0, -3, 0, 0, 0, 0, Pal.Ramp(Pal.Cream, 1));                            // taped grip
                    g.Box(-1, -12, -1, 1, -11, 1, Pal.Ramp(Pal.Rust, 2));                         // fitting
                    break;
                case ItemIds.Machete:
                    g.Box(0, -3, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 1, 704));
                    g.Box(0, -12, 0, 0, -4, 2, p => p.z == 2 ? Pal.Chrome[3] : Pal.Chrome[1]);
                    g.Box(-1, -4, 0, 1, -4, 1, Pal.Ramp(Pal.Metal, 0));
                    break;
                case ItemIds.Shotgun:
                    g.Box(-1, -14, 0, -1, -3, 0, Pal.Weathered(Pal.Metal, 0.4f, 705, 1, 0));
                    g.Box(1, -14, 0, 1, -3, 0, Pal.Weathered(Pal.Metal, 0.4f, 706, 1, 0));
                    g.Box(-1, -3, -1, 1, 3, 1, Pal.Ramp(Pal.Wood, 2, 707));                        // stock
                    g.Box(0, -1, -2, 0, 0, -2, Pal.Ramp(Pal.Metal, 0));                            // trigger guard
                    g.Box(-1, -8, 0, 1, -8, 0, Pal.Ramp(Pal.Rust, 2));                             // band
                    break;
                case ItemIds.ClawHammer:
                    g.Box(0, -8, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 3, 708));
                    g.Box(0, -10, -1, 0, -9, 2, Pal.Ramp(Pal.Metal, 2, 709));
                    g.Set(0, -9, -2, Pal.Solid(Pal.Metal[1])); g.Set(0, -8, -2, Pal.Solid(Pal.Metal[1]));   // claw
                    g.Set(0, -10, 2, Pal.Solid(Pal.Chrome[3]));
                    break;
                case "tool_shovel":
                    g.Box(0, -12, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2, 710));
                    g.Box(-1, 1, 0, 1, 1, 0, Pal.Ramp(Pal.Black, 1));
                    g.Box(-2, -17, 0, 2, -13, 0, p => p.y == -17 ? Pal.Chrome[3] : Pal.Metal[2]);
                    break;
                case "tool_axe":
                    g.Box(0, -13, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2, 711));
                    g.Box(0, -14, -1, 0, -11, 3, p => p.z == 3 ? Pal.Chrome[3] : Pal.Metal[1]);
                    break;
                case "tool_pickaxe":
                    g.Box(0, -13, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2, 712));
                    g.Box(0, -14, -5, 0, -13, 5, Pal.Ramp(Pal.Metal, 2, 713));
                    g.Set(0, -12, -6, Pal.Solid(Pal.Chrome[3])); g.Set(0, -12, 6, Pal.Solid(Pal.Chrome[3]));
                    break;
                default: // sledgehammer
                    g.Box(0, -12, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2, 401));
                    g.Box(-1, -15, -3, 1, -13, 2, Pal.Weathered(Pal.Metal, 0.35f, 402, 2, -20));
                    g.Box(-1, -15, 2, 1, -13, 2, Pal.Solid(Pal.Chrome[2]));
                    break;
            }
            g.Bevel();
            return meshes[id] = VoxelMesher.Build(g, "Tool_" + id);
        }

        public static HandTool Create(string id, Material mat)
        {
            var go = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = MeshFor(id);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var tip = new GameObject("Tip").transform;
            tip.SetParent(go.transform, false);
            HandTool tool;
            if (id == ItemIds.Shotgun)
            {
                var r = go.AddComponent<RangedTool>();
                r.muzzle = tip; tip.localPosition = new Vector3(0, -15f * S, 0);
                r.swingDuration = 0.9f; r.strikeAt = 0.03f; r.style = ToolStyle.Gun;
                tool = r;
            }
            else
            {
                var m = go.AddComponent<MeleeTool>();
                m.tip = tip;
                switch (id)
                {
                    case ItemIds.Wrench: tip.localPosition = new Vector3(0, -11f * S, 0); m.power = 0.25f; m.carveRadius = 0.08f; m.swingDuration = 0.8f; m.style = ToolStyle.Twist; m.strikeAt = 0.4f; break;
                    case ItemIds.Cutter: tip.localPosition = new Vector3(0, -12f * S, 0); m.power = 0.5f; m.carveRadius = 0.12f; m.swingDuration = 0.9f; m.salvage = true; m.style = ToolStyle.Grind; m.strikeAt = 0.45f; break;
                    case ItemIds.PipeClub: tip.localPosition = new Vector3(0, -12f * S, 0); m.power = 0.6f; m.carveRadius = 0.12f; m.swingDuration = 0.5f; m.style = ToolStyle.Slash; m.strikeAt = 0.5f; break;
                    case ItemIds.ClawHammer: tip.localPosition = new Vector3(0, -10f * S, 0.05f); m.power = 0.3f; m.carveRadius = 0.08f; m.swingDuration = 0.42f; m.style = ToolStyle.Slash; m.strikeAt = 0.5f; break;
                    case "tool_shovel": tip.localPosition = new Vector3(0, -17f * S, 0); m.power = 0.35f; m.carveRadius = 0.12f; m.swingDuration = 0.8f; m.style = ToolStyle.Overhead; m.strikeAt = 0.6f; m.digs = true; break;
                    case "tool_axe": tip.localPosition = new Vector3(0, -13f * S, 0.2f); m.power = 0.5f; m.carveRadius = 0.14f; m.swingDuration = 0.7f; m.style = ToolStyle.Slash; m.strikeAt = 0.5f; m.woodMult = 3f; break;
                    case "tool_pickaxe": tip.localPosition = new Vector3(0, -13f * S, 0.45f); m.power = 0.6f; m.carveRadius = 0.14f; m.swingDuration = 0.85f; m.style = ToolStyle.Overhead; m.strikeAt = 0.6f; m.stoneMult = 3f; break;
                    case ItemIds.Machete: tip.localPosition = new Vector3(0, -12f * S, 0.1f); m.power = 0.5f; m.carveRadius = 0.1f; m.swingDuration = 0.42f; m.style = ToolStyle.Slash; m.strikeAt = 0.5f; break;
                    default: tip.localPosition = new Vector3(0, -14f * S, 0.1f); m.power = 1f; m.carveRadius = 0.2f; m.swingDuration = 1.0f; m.style = ToolStyle.Overhead; m.strikeAt = 0.62f; break;
                }
                tool = m;
            }
            tool.id = id;
            tool.toolName = ItemIds.Name(id);
            return tool;
        }
    }
}
