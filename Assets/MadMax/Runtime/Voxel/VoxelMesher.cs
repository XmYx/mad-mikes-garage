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

        public static Mesh Build(VoxelGrid grid, string name, float size = DefaultSize)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color32>();
            var tris = new List<int>();
            var smooth = new Dictionary<Vector3, Vector3>();

            foreach (var kv in grid.voxels)
            {
                var p = kv.Key;
                foreach (var d in Dirs)
                {
                    if (grid.Has(p + d)) continue;
                    Vector3 n = d;
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(u, n); // Cross(u,v) = -n  => clockwise front face in Unity
                    Vector3 c = (Vector3)p + n * 0.5f;
                    int b = verts.Count;
                    Vector3[] q = { c - (u + v) * 0.5f, c + (-u + v) * 0.5f, c + (u + v) * 0.5f, c + (u - v) * 0.5f };
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

            var uv3 = new List<Vector3>(verts.Count);
            foreach (var w in verts) uv3.Add(smooth[w].normalized);

            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetUVs(3, uv3);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
