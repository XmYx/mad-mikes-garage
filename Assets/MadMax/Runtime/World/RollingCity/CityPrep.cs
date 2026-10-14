using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Worker-side finishing of a generated city (<see cref="TrackedCity.Built"/>, <see cref="MegaCity.Built"/>)
    /// so the main thread only uploads meshes: each block module's collision mesh (shell + furnishings) merged and every
    /// mesh's GPU layout packed (<see cref="VoxelMesher.ToMesh"/> then skips its per-vertex pass).</summary>
    public static class CityPrep
    {
        /// <summary>Collision mesh per block and module: the shell's solid then the furnishings' (as CityKit.Append
        /// merges them).</summary>
        public static VoxelMesher.MeshData[][] Solids(List<TrackedCity.BlockBuild> blocks)
        {
            var r = new VoxelMesher.MeshData[blocks.Count][];
            for (int bi = 0; bi < blocks.Count; bi++)
            {
                var bb = blocks[bi];
                r[bi] = new VoxelMesher.MeshData[bb.shells.Length];
                for (int v = 0; v < bb.shells.Length; v++)
                {
                    var md = new VoxelMesher.MeshData();
                    Merge(md, bb.shells[v].solid);
                    Merge(md, bb.insides[v].solid);
                    r[bi][v] = md;
                }
            }
            return r;
        }

        static void Merge(VoxelMesher.MeshData into, VoxelMesher.MeshData from)
        {
            int k = into.verts.Count;
            into.verts.AddRange(from.verts); into.norms.AddRange(from.norms); into.uv3.AddRange(from.uv3); into.cols.AddRange(from.cols);
            var tris = from.tris;
            if (into.tris.Capacity < into.tris.Count + tris.Count) into.tris.Capacity = into.tris.Count + tris.Count;
            for (int i = 0; i < tris.Count; i++) into.tris.Add(tris[i] + k);
        }

        public static void Pack(VoxelMesher.MeshData md)
        {
            if (md != null && md.verts.Count > 0 && (md.packed == null || md.packed.Length != md.verts.Count)) md.Pack();
        }

        public static void Pack(CityKit k)
        {
            if (k == null) return;
            Pack(k.mesh); Pack(k.glow); Pack(k.solid);
        }

        /// <summary>Pack the pieces, blocks (with their merged solids) of a generated city.</summary>
        public static void Pack(List<TrackedCity.BlockBuild> blocks, VoxelMesher.MeshData[][] solids)
        {
            for (int bi = 0; bi < blocks.Count; bi++)
            {
                var bb = blocks[bi];
                for (int v = 0; v < bb.shells.Length; v++) { Pack(bb.shells[v].mesh); Pack(bb.shells[v].glow); Pack(bb.insides[v].mesh); Pack(solids[bi][v]); }
                Pack(bb.extras.mesh); Pack(bb.extras.solid);
            }
        }
    }
}
