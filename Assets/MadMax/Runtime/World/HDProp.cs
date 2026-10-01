using MadMax.Rendering;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Hybrid destruction for world props (tools/blender/hd/PIPELINE.md, props and buildings): the HD model of
    /// the template (same id: <c>BrickHouse0</c>, <c>Pine1</c>, <c>Ore_Coal0</c>, <c>Stall_food</c> ...) is what the
    /// player sees; the voxel template stays the truth for collision, carving, structural support, debris and yields.
    /// Until the first hit only the HD model draws; from then on <see cref="HDCarve"/> clips it to the carved cells and
    /// shows the broken voxel faces. Materials follow the voxel material the prop spawned with: the underground cutaway
    /// for every world prop, wind sway for vegetation.</summary>
    public static class HDProp
    {
        static readonly int WorldCutId = Shader.PropertyToID("_WorldCut"), SwayId = Shader.PropertyToID("_Sway");

        /// <summary>World-material flags of a voxel material (<see cref="HDAssets.WorldCut"/>, <see cref="HDAssets.Sway"/>).</summary>
        public static int FlagsOf(Material voxelMat)
        {
            int f = 0;
            if (voxelMat && voxelMat.HasProperty(WorldCutId) && voxelMat.GetFloat(WorldCutId) > 0.5f) f |= HDAssets.WorldCut;
            if (voxelMat && voxelMat.HasProperty(SwayId) && voxelMat.GetFloat(SwayId) > 0f) f |= HDAssets.Sway;
            return f;
        }

        public static HDVisual Attach(DestructibleVoxels d, string templateId, Material voxelMat)
        {
            if (!d || string.IsNullOrEmpty(templateId) || templateId.StartsWith("site:")) return null;
            var a = HDAssets.Get(HDDomain.World, templateId);
            if (!a) return null;
            var vis = HDVisual.Dress(d.gameObject, a, FlagsOf(voxelMat));
            if (vis) HDCarve.Attach(d, vis);
            return vis;
        }
    }
}
