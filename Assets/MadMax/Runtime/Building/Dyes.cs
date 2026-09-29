using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Painted furniture: a recoloured copy of the piece's voxels, meshed once per (piece, dye). Fabric pieces
    /// dye only their cloth; everything else gets its wood, metal and masonry painted. Shading survives: each voxel
    /// maps its brightness (relative to the piece) onto the dye's ramp.</summary>
    public static class Dyes
    {
        static readonly Dictionary<(string, int), Mesh> cache = new Dictionary<(string, int), Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => cache.Clear();

        public static int IndexOf(string item) => System.Array.IndexOf(ItemIds.Dyes, item);

        public static Mesh MeshFor(FurnitureDef def, int dye)
        {
            var ramp = Pal.DyeRamp(dye);
            if (ramp == null || def.grid == null) return def.mesh;
            if (cache.TryGetValue((def.id, dye), out var m) && m) return m;
            var g = def.grid.Clone();
            bool cloth = false;
            foreach (var v in g.voxels.Values) if (v.mat == (byte)ResourceType.Cloth) { cloth = true; break; }
            float lo = 1f, hi = 0f;
            foreach (var v in g.voxels.Values) if (Paintable(v.mat, cloth)) { float l = Lum(v.color); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
            var keys = new List<Vector3Int>(g.voxels.Keys);
            foreach (var k in keys)
            {
                var v = g.voxels[k];
                if (!Paintable(v.mat, cloth)) continue;
                float t = hi - lo < 0.02f ? 0.6f : Mathf.InverseLerp(lo, hi, Lum(v.color));
                v.color = ramp[Mathf.Clamp(Mathf.RoundToInt(t * (ramp.Length - 1)), 0, ramp.Length - 1)];
                g.voxels[k] = v;
            }
            m = VoxelMesher.Build(g, "Furniture_" + def.id + "_dye" + dye, def.voxel);
            cache[(def.id, dye)] = m;
            return m;
        }

        static bool Paintable(byte mat, bool clothOnly) => clothOnly ? mat == (byte)ResourceType.Cloth
            : mat == (byte)ResourceType.Wood || mat == (byte)ResourceType.Scrap || mat == (byte)ResourceType.Stone || mat == (byte)ResourceType.Concrete || mat == (byte)ResourceType.Iron;

        static float Lum(Color32 c) => (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
    }
}
