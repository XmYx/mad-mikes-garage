using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.Voxel
{
    /// <summary>Builds a flat-shaded, vertex-coloured mesh from a voxel grid (hidden faces culled).
    /// UV3 stores a smoothed normal used by the pixel outline pass.</summary>
    public static class VoxelMesher
    {
        public const float DefaultSize = 0.08f;

        static readonly Vector3Int[] Dirs =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        /// <summary>Mesh arrays built off the main thread (<see cref="BuildData"/>), turned into a Mesh by <see cref="ToMesh"/>.</summary>
        public class MeshData
        {
            public readonly List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>(), uv3 = new List<Vector3>();
            public readonly List<Color32> cols = new List<Color32>();
            public readonly List<int> tris = new List<int>();
        }

        public static Mesh Build(VoxelGrid grid, string name, float size = DefaultSize) => ToMesh(BuildData(grid, size), name);

        /// <summary>Pure C# (no Unity objects): safe on worker threads for a grid nobody is modifying.</summary>
        public static MeshData BuildData(VoxelGrid grid, float size = DefaultSize)
        {
            var md = new MeshData();
            var verts = md.verts; var norms = md.norms; var cols = md.cols; var tris = md.tris;
            var smooth = new Dictionary<Vector3, Vector3>();
            var q = new Vector3[4];

            foreach (var kv in grid.voxels)
            {
                var p = kv.Key;
                foreach (var d in Dirs)
                {
                    if (grid.voxels.ContainsKey(p + d)) continue;
                    Vector3 n = d;
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(u, n); // Cross(u,v) = -n  => clockwise front face in Unity
                    Vector3 c = (Vector3)p + n * 0.5f;
                    int b = verts.Count;
                    q[0] = c - (u + v) * 0.5f; q[1] = c + (-u + v) * 0.5f; q[2] = c + (u + v) * 0.5f; q[3] = c + (u - v) * 0.5f;
                    foreach (var corner in q)
                    {
                        var w = corner * size;
                        verts.Add(w); norms.Add(n); cols.Add(kv.Value.color);
                        smooth.TryGetValue(w, out var s); smooth[w] = s + n;
                    }
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            foreach (var w in verts) md.uv3.Add(smooth[w].normalized);
            return md;
        }

        public static Mesh ToMesh(MeshData md, string name)
        {
            var mesh = new Mesh { name = name };
            if (md.verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(md.verts);
            mesh.SetNormals(md.norms);
            mesh.SetColors(md.cols);
            mesh.SetUVs(3, md.uv3);
            mesh.SetTriangles(md.tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
