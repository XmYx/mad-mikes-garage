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
            // interleaved GPU layout (packed with the lists, so the upload is two copies, not one per channel)
            internal Vertex[] packed;
            internal ushort[] index16;
            internal int[] index32;
            internal Bounds bounds;

            internal void Pack()
            {
                int n = verts.Count;
                packed = new Vertex[n];
                var min = n > 0 ? verts[0] : Vector3.zero; var max = min;
                for (int i = 0; i < n; i++)
                {
                    var v = verts[i];
                    packed[i] = new Vertex { pos = v, normal = norms[i], color = cols[i], uv3 = uv3[i] };
                    min = Vector3.Min(min, v); max = Vector3.Max(max, v);
                }
                bounds = new Bounds((min + max) * 0.5f, max - min);
                if (n > 65535) index32 = tris.ToArray();
                else { index16 = new ushort[tris.Count]; for (int i = 0; i < index16.Length; i++) index16[i] = (ushort)tris[i]; }
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct Vertex { public Vector3 pos, normal; public Color32 color; public Vector3 uv3; }

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3),
        };

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
            md.Pack();
            return md;
        }

        public static Mesh ToMesh(MeshData md, string name)
        {
            if (md.packed == null || md.packed.Length != md.verts.Count) md.Pack();
            var mesh = new Mesh { name = name };
            if (md.packed.Length == 0) return mesh;
            const MeshUpdateFlags quiet = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers;
            int n = md.packed.Length;
            mesh.SetVertexBufferParams(n, Layout);
            mesh.SetVertexBufferData(md.packed, 0, 0, n, 0, quiet);
            if (md.index32 != null)
            {
                mesh.SetIndexBufferParams(md.index32.Length, IndexFormat.UInt32);
                mesh.SetIndexBufferData(md.index32, 0, 0, md.index32.Length, quiet);
            }
            else
            {
                mesh.SetIndexBufferParams(md.index16.Length, IndexFormat.UInt16);
                mesh.SetIndexBufferData(md.index16, 0, 0, md.index16.Length, quiet);
            }
            int count = md.index32 != null ? md.index32.Length : md.index16.Length;
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, count) { bounds = md.bounds, vertexCount = n }, quiet);
            mesh.bounds = md.bounds;
            return mesh;
        }
    }
}
