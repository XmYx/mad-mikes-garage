using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand liquid containers (fluids block): voxel models hanging from the hand and the <see cref="FluidCanTool"/>
    /// component, added to <see cref="ToolLibrary.Extra"/>.</summary>
    public static class FluidTools
    {
        public static void Register()
        {
            foreach (var d in FluidContainers.All)
            {
                var def = d;
                ItemIds.Register(def.id, def.name);
                ToolLibrary.Extra[def.id] = (() => Model(def.id), (go, tip) =>
                {
                    var t = go.AddComponent<FluidCanTool>();
                    tip.localPosition = new Vector3(0f, -4f * VoxelMesher.DefaultSize, 0f);
                    t.swingDuration = 0.9f; t.strikeAt = 0.5f; t.style = ToolStyle.Twist;
                    return t;
                });
            }
        }

        static VoxelGrid Model(string id)
        {
            var g = new VoxelGrid();
            switch (id)
            {
                case FluidContainers.JerryCan:
                    g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Black, 1));                                              // handle
                    g.Box(-3, -9, -1, 3, -1, 1, Pal.Weathered(Pal.Olive, 0.3f, 1420, 2, 0));                       // pressed steel body
                    for (int k = -3; k <= 3; k++) { g.Set(k, -5 + k, 1, Pal.Ramp(Pal.Olive, 3)); g.Set(k, -5 - k, 1, Pal.Ramp(Pal.Olive, 3)); }   // embossed X
                    g.Box(3, 0, 0, 4, 1, 0, Pal.Ramp(Pal.Metal, 2));                                               // spout
                    break;
                case FluidContainers.FuelCan:
                    g.Box(0, -1, 0, 0, 0, 0, Pal.Ramp(Pal.Black, 1));
                    g.Box(-1, -6, -1, 1, -2, 1, Pal.Weathered(Pal.Crimson, 0.2f, 1421, 2, 0));
                    g.Tube(new Vector3(1, -2, 0), new Vector3(3, 0, 0), 0.4f, Pal.Ramp(Pal.Ochre, 2));             // flexible spout
                    break;
                case FluidContainers.Bottle:
                    g.CylY(0, 0, 1.1f, -6, -2, Pal.Ramp(Pal.Glass, 2));
                    g.Box(0, -1, 0, 0, 0, 0, Pal.Ramp(Pal.Navy, 2));                                               // cap
                    g.Box(-1, -4, 1, 1, -3, 1, Pal.Ramp(Pal.Cream, 2));                                            // label
                    break;
                case FluidContainers.Bucket:
                    g.Box(0, 0, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2));                                                // grip
                    g.Box(-2, -1, 0, -2, -1, 0, Pal.Ramp(Pal.Chrome, 1)); g.Box(2, -1, 0, 2, -1, 0, Pal.Ramp(Pal.Chrome, 1));
                    g.CylY(0, 0, 2.4f, -8, -2, p => p.y == -2 ? Pal.Chrome[2] : Pal.Metal[(p.x + p.z + p.y) & 3]); // galvanised pail
                    break;
                default:                                                                                            // oil jug
                    g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Black, 1));
                    g.Box(-1, -6, -1, 1, -1, 1, Pal.Weathered(Pal.Ochre, 0.15f, 1422, 2, 0));
                    g.Box(-1, -4, 1, 1, -3, 1, Pal.Ramp(Pal.Cream, 2));
                    g.Set(1, 1, 0, Pal.Ramp(Pal.Black, 2));
                    break;
            }
            return g;
        }
    }
}
