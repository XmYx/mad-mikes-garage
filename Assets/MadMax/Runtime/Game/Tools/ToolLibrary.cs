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
        public static readonly string[] Order = { ItemIds.Sledgehammer, ItemIds.Wrench, ItemIds.Cutter, ItemIds.PipeClub, ItemIds.Machete, ItemIds.Shotgun, ItemIds.ClawHammer, "tool_shovel", "tool_axe", "tool_pickaxe", "tool_torch", "tool_gas_torch", "tool_lantern",
            "tool_crowbar", "tool_welder", "tool_jack", "tool_binoculars", "tool_geiger", "tool_flashlight", "tool_detector", "tool_hoe", "tool_watering_can", "tool_fishing_rod" };
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
                case "tool_torch":
                    g.Box(0, -9, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 1, 714));
                    g.Box(-1, -12, -1, 1, -10, 1, Pal.Ramp(Pal.Cream, 1, 715));                    // oil-soaked rag
                    g.Set(0, -13, 0, Pal.Solid(Pal.Amber)); g.Set(0, -14, 0, Pal.Solid(Pal.LightY)); // flame
                    break;
                case "tool_gas_torch":
                    g.CylY(0, 0, 1.4f, -6, 0, Pal.Ramp(Pal.Rust, 2, 716));                           // gas bottle
                    g.Box(0, -9, 0, 0, -7, 0, Pal.Ramp(Pal.Chrome, 2));                              // valve + nozzle
                    g.Set(0, -10, 0, Pal.Solid(Pal.PaleBlue[4]));                                    // blue jet
                    break;
                case "tool_lantern":
                    g.Box(0, -2, 0, 0, 0, 0, Pal.Ramp(Pal.Metal, 1, 717));                           // bail handle
                    g.Box(-1, -6, -1, 1, -3, 1, p => (p.y == -3 || p.y == -6) ? Pal.Metal[2] : Pal.LightY);   // glass chimney
                    break;
                case "tool_crowbar":
                    g.Box(0, -13, 0, 0, 0, 0, Pal.Ramp(Pal.Rust, 2, 718));
                    g.Set(0, 1, 1, Pal.Solid(Pal.Rust[1])); g.Set(0, 1, 2, Pal.Solid(Pal.Rust[2]));          // hook
                    g.Set(0, -14, 1, Pal.Solid(Pal.Chrome[2]));                                               // flat end
                    break;
                case "tool_welder":
                    g.Box(0, -4, 0, 0, 0, 0, Pal.Ramp(Pal.Black, 1, 719));                                    // insulated grip
                    g.Box(-1, -3, -1, 1, -2, 1, Pal.Ramp(Pal.Metal, 1));
                    g.Box(0, -9, 0, 0, -5, 0, Pal.Ramp(Pal.Chrome, 2));                                       // lance
                    g.Set(0, -10, 0, Pal.Solid(Pal.PaleBlue[4]));                                             // arc
                    g.Box(1, 0, 0, 2, 1, 0, Pal.Solid(Pal.Hex("b02818")));                                   // hose
                    break;
                case "tool_jack":
                    g.Box(-1, -4, -1, 1, 0, 1, p => p.y == 0 ? Pal.Metal[3] : Pal.Hex("b02818"));             // bottle jack
                    g.Box(0, -8, 0, 0, -5, 0, Pal.Ramp(Pal.Chrome, 2));                                       // ram
                    g.Box(-1, -9, -1, 1, -9, 1, Pal.Ramp(Pal.Metal, 1));                                      // saddle
                    g.Box(2, -2, 0, 5, -2, 0, Pal.Ramp(Pal.Metal, 2));                                        // pump handle
                    break;
                case "tool_binoculars":
                    g.Box(-2, -3, -1, -1, 0, 1, Pal.Ramp(Pal.Black, 1, 720)); g.Box(1, -3, -1, 2, 0, 1, Pal.Ramp(Pal.Black, 1, 721));
                    g.Box(-1, -2, 0, 1, -1, 0, Pal.Ramp(Pal.Metal, 1));
                    g.Set(-2, -4, 0, Pal.Solid(Pal.Glass[3])); g.Set(2, -4, 0, Pal.Solid(Pal.Glass[3]));
                    break;
                case "tool_geiger":
                    g.Box(-1, -4, -1, 1, 0, 1, Pal.Solid(Pal.Hex("d4b020")));                                 // yellow case
                    g.Box(0, -3, 2, 0, -1, 2, Pal.Solid(Pal.Glass[3]));                                       // dial
                    g.Box(0, -8, 0, 0, -5, 0, Pal.Ramp(Pal.Metal, 2));                                        // probe
                    break;
                case "tool_detector":
                    g.Box(0, -12, 0, 0, 0, 0, Pal.Ramp(Pal.Metal, 2, 723));                                   // shaft
                    g.Box(-1, -2, 1, 1, -1, 2, Pal.Ramp(Pal.Black, 1)); g.Set(0, -1, 3, Pal.Solid(Pal.Amber));  // control box
                    g.CylZ(0, -13, 2.4f, 1, 2, Pal.Ramp(Pal.Ochre, 2, 724), 1.2f);                            // search coil
                    break;
                case "tool_hoe":
                    g.Box(0, -15, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2, 725));                                    // long handle
                    g.Box(-2, -17, 0, 2, -16, 0, Pal.Ramp(Pal.Metal, 2, 726));                                // blade across the shaft
                    g.Box(-2, -17, 1, 2, -17, 1, Pal.Ramp(Pal.Chrome, 3));                                    // sharpened edge
                    break;
                case "tool_watering_can":
                    g.Box(0, -2, 0, 0, 0, 0, Pal.Ramp(Pal.Metal, 2));                                         // handle
                    g.CylY(0, 0, 2.2f, -7, -3, p => p.y == -5 ? Pal.Moss[1] : Pal.Moss[3]);                   // green tin can
                    g.Tube(new Vector3(0, -6, 2), new Vector3(0, -3, 6), 0.4f, Pal.Ramp(Pal.Moss, 2));         // spout
                    g.Box(-1, -3, 6, 1, -2, 7, Pal.Ramp(Pal.Chrome, 2));                                     // rose
                    break;
                case "tool_fishing_rod":
                    g.Box(0, -3, 0, 0, 1, 0, Pal.Ramp(Pal.Sand, 3));                                          // cork grip
                    g.Box(0, -24, 0, 0, -4, 0, p => p.y % 6 == 0 ? Pal.Chrome[2] : p.y < -16 ? Pal.Crimson[3] : Pal.Crimson[2]);   // rod, guides
                    g.CylX(-2, 1, 1.2f, 1, 2, Pal.Ramp(Pal.Chrome, 2));                                       // reel
                    g.Set(3, -2, 1, Pal.Solid(Pal.Black[1]));                                                 // crank
                    break;
                case "tool_flashlight":
                    g.CylY(0, 0, 1.2f, -6, 0, Pal.Ramp(Pal.Metal, 1, 722));
                    g.CylY(0, 0, 1.7f, -8, -7, Pal.Ramp(Pal.Chrome, 2));
                    g.Set(0, -9, 0, Pal.Solid(Pal.LightW));
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
            else if (id == "tool_welder")
            {
                var w = go.AddComponent<WelderTool>();
                w.tip = tip; tip.localPosition = new Vector3(0, -10f * S, 0);
                w.swingDuration = 0.7f; w.strikeAt = 0.5f; w.style = ToolStyle.Grind;
                tool = w;
            }
            else if (id == "tool_jack")
            {
                var j = go.AddComponent<JackTool>();
                j.swingDuration = 1.1f; j.strikeAt = 0.6f; j.style = ToolStyle.Twist;
                tool = j;
            }
            else if (id == "tool_binoculars")
            {
                var b = go.AddComponent<BinocularsTool>();
                b.swingDuration = 0.4f; b.strikeAt = 0.1f; b.style = ToolStyle.Twist;
                tool = b;
            }
            else if (id == "tool_detector")
            {
                var d = go.AddComponent<MetalDetectorTool>();
                d.swingDuration = 0.4f; d.strikeAt = 0.1f; d.style = ToolStyle.Twist;
                tool = d;
            }
            else if (id == "tool_hoe")
            {
                var h = go.AddComponent<HoeTool>();
                h.swingDuration = 0.7f; h.strikeAt = 0.62f; h.style = ToolStyle.Overhead;
                tool = h;
            }
            else if (id == "tool_watering_can")
            {
                var w = go.AddComponent<WateringCanTool>();
                w.swingDuration = 0.9f; w.strikeAt = 0.45f; w.style = ToolStyle.Twist;
                tool = w;
            }
            else if (id == "tool_fishing_rod")
            {
                var f = go.AddComponent<FishingRodTool>();
                f.tip = tip; tip.localPosition = new Vector3(0, -24f * S, 0);
                f.swingDuration = 0.7f; f.strikeAt = 0.5f; f.style = ToolStyle.Slash;
                tool = f;
            }
            else if (id == "tool_geiger")
            {
                var gc = go.AddComponent<GeigerTool>();
                gc.swingDuration = 0.4f; gc.strikeAt = 0.1f; gc.style = ToolStyle.Twist;
                tool = gc;
            }
            else if (id == "tool_torch" || id == "tool_gas_torch" || id == "tool_lantern" || id == "tool_flashlight")
            {
                var l = go.AddComponent<LightTool>();
                l.tip = tip;
                l.kind = id == "tool_torch" ? LightTool.Kind.Torch : id == "tool_gas_torch" ? LightTool.Kind.GasTorch : id == "tool_flashlight" ? LightTool.Kind.Flashlight : LightTool.Kind.Lantern;
                tip.localPosition = new Vector3(0, (id == "tool_torch" ? -13f : id == "tool_gas_torch" ? -10f : id == "tool_flashlight" ? -9f : -5f) * S, 0);
                l.power = id == "tool_gas_torch" ? 0.45f : 0.2f; l.carveRadius = 0.08f; l.swingDuration = 0.6f;
                l.style = id == "tool_gas_torch" ? ToolStyle.Grind : ToolStyle.Slash; l.strikeAt = 0.5f;
                l.salvage = id == "tool_gas_torch";                                     // cuts metal like the salvage cutter
                tool = l;
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
                    case "tool_crowbar": tip.localPosition = new Vector3(0, -14f * S, 0.08f); m.power = 0.55f; m.carveRadius = 0.1f; m.swingDuration = 0.55f; m.style = ToolStyle.Slash; m.strikeAt = 0.5f; m.pries = true; break;
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
