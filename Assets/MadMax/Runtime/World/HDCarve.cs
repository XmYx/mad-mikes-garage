using System.Collections.Generic;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Destruction of an HD-dressed <see cref="DestructibleVoxels"/>: the HD model keeps drawing but is clipped
    /// (shader <c>MadMax/HDLit</c> <c>_CarveMask</c>, a 3D texture over the template's voxel cells) wherever its cell
    /// was carved away; empty cells next to carved ones are cleared too, so trims, awnings and pipes hanging off a broken
    /// wall go with it. The broken faces of the remaining voxels (towards carved cells) are drawn as a rim mesh with the
    /// voxels' colours in the HD lighting (<c>HDLitVoxel</c>). Nothing happens until the first carve: pristine props keep
    /// the shared, SRP-batched materials. A prop streaming back in damaged rebuilds its mask from the template.</summary>
    public class HDCarve : MonoBehaviour
    {
        DestructibleVoxels dv;
        HDVisual vis;
        VoxelGrid template;
        float size;
        bool active;
        Rigidbody body;

        Texture3D mask;
        byte[] data;
        Vector3Int min, dims;
        readonly HashSet<Vector3Int> gone = new HashSet<Vector3Int>();
        readonly List<Vector3Int> log = new List<Vector3Int>();
        readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        Mesh rim;
        MeshFilter rimFilter;

        static readonly int MaskId = Shader.PropertyToID("_CarveMask"), MatrixId = Shader.PropertyToID("_CarveMatrix"), MinId = Shader.PropertyToID("_CarveMin"),
            SizeId = Shader.PropertyToID("_CarveSize"), OnId = Shader.PropertyToID("_CarveOn"), InsetId = Shader.PropertyToID("_CarveInset");

        /// <summary>Cells carved so far (template cells no longer in the grid).</summary>
        public int CarvedCells => gone.Count;
        public bool Active => active;

        public static HDCarve Attach(DestructibleVoxels d, HDVisual v)
        {
            if (!d || !v || d.Template == null) return null;
            var c = d.gameObject.AddComponent<HDCarve>();
            c.dv = d; c.vis = v; c.template = d.Template; c.size = d.voxelSize;
            c.body = d.GetComponent<Rigidbody>();
            d.takenLog = c.log;
            d.Carved += c.OnCarved;
            c.enabled = false;
            if (d.Owned && d.Grid != null && d.Grid != d.Template)
            {
                // restored damage: every template cell missing from the saved grid
                foreach (var p in c.template.voxels.Keys) if (!d.Grid.voxels.ContainsKey(p)) c.log.Add(p);
                if (c.log.Count > 0) c.OnCarved();
            }
            return c;
        }

        void OnCarved()
        {
            if (!dv || log.Count == 0) return;
            if (!active) Activate();
            int r = log.Count > 4000 ? 1 : 2;                                             // big collapses: a thinner fringe
            foreach (var p in log)
            {
                if (!gone.Add(p)) continue;
                Set(p, 0);
                for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                for (int z = -r; z <= r; z++)
                {
                    var q = new Vector3Int(p.x + x, p.y + y, p.z + z);
                    if (!template.voxels.ContainsKey(q)) Set(q, 0);                          // HD trims in the air beside it
                }
            }
            log.Clear();
            mask.SetPixelData(data, 0);
            mask.Apply(false);
            BuildRim();
        }

        void Set(Vector3Int p, byte v)
        {
            var q = p - min;
            if ((uint)q.x >= (uint)dims.x || (uint)q.y >= (uint)dims.y || (uint)q.z >= (uint)dims.z) return;
            data[q.x + dims.x * (q.y + dims.y * q.z)] = v;
        }

        void Activate()
        {
            active = true;
            var lo = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue); var hi = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var p in template.voxels.Keys) { lo = Vector3Int.Min(lo, p); hi = Vector3Int.Max(hi, p); }
            min = lo - new Vector3Int(2, 2, 2);
            dims = hi - lo + new Vector3Int(5, 5, 5);
            data = new byte[dims.x * dims.y * dims.z];
            for (int i = 0; i < data.Length; i++) data[i] = 255;
            mask = new Texture3D(dims.x, dims.y, dims.z, TextureFormat.R8, false) { name = "CarveMask_" + dv.name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            vis.materialHook = Carved;
            vis.RefreshMaterials();
            var go = new GameObject("HDRim", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            rimFilter = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            var voxelMat = vis.host ? vis.host.sharedMaterial : null;                       // the prop's own voxel material (world cut, sway)
            var vm = HDModel.VoxelMaterial;
            mr.sharedMaterial = vm ? HDAssets.Variant(vm, HDProp.FlagsOf(voxelMat)) : voxelMat;
            rim = new Mesh { name = "HDRim", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            rim.MarkDynamic();
            rimFilter.sharedMesh = rim;
            vis.Adopt(mr);
            enabled = body;                                                                  // loose props keep the mask on them
        }

        Matrix4x4 WorldToGrid => Matrix4x4.Scale(Vector3.one / size) * transform.worldToLocalMatrix;

        Material Carved(Material src)
        {
            if (!src) return src;
            if (copies.TryGetValue(src, out var m) && m) return m;
            m = new Material(src) { name = src.name + "_carved" };
            m.SetTexture(MaskId, mask);
            m.SetMatrix(MatrixId, WorldToGrid);
            m.SetVector(MinId, new Vector4(min.x, min.y, min.z, 0f));
            m.SetVector(SizeId, new Vector4(dims.x, dims.y, dims.z, 0f));
            m.SetFloat(OnId, 1f);
            m.SetFloat(InsetId, size * 0.5f);
            return copies[src] = m;
        }

        void LateUpdate()
        {
            if (!body || !transform.hasChanged) return;
            transform.hasChanged = false;
            var w = WorldToGrid;
            foreach (var m in copies.Values) if (m) m.SetMatrix(MatrixId, w);
        }

        static readonly Vector3Int[] Dirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
        static readonly List<Vector3> verts = new List<Vector3>();
        static readonly List<Vector3> norms = new List<Vector3>();
        static readonly List<Color32> cols = new List<Color32>();
        static readonly List<int> tris = new List<int>();

        /// <summary>Faces of the remaining voxels that look into a carved cell (the broken cross-section).</summary>
        void BuildRim()
        {
            var grid = dv.Grid;
            if (grid == null || !rim) return;
            verts.Clear(); norms.Clear(); cols.Clear(); tris.Clear();
            float h = size * 0.5f;
            foreach (var c in gone)
                for (int d = 0; d < 6; d++)
                {
                    var n = c + Dirs[d];                                                      // a voxel still there, beside the hole
                    if (!grid.voxels.TryGetValue(n, out var v)) continue;
                    Vector3 nrm = -(Vector3)Dirs[d];                                          // its face towards the hole
                    Vector3 centre = ((Vector3)n + nrm * 0.5f) * size;
                    Vector3 u = d < 2 ? Vector3.up : Vector3.right, w = Vector3.Cross(nrm, u);
                    int b = verts.Count;
                    verts.Add(centre + (-u - w) * h); verts.Add(centre + (u - w) * h); verts.Add(centre + (u + w) * h); verts.Add(centre + (-u + w) * h);
                    for (int k = 0; k < 4; k++) { norms.Add(nrm); cols.Add(v.color); }
                    // wind so the face points along nrm
                    if (Vector3.Dot(Vector3.Cross(verts[b + 1] - verts[b], verts[b + 2] - verts[b]), nrm) > 0f) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                    else { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                }
            rim.Clear();
            rim.SetVertices(verts); rim.SetNormals(norms); rim.SetColors(cols); rim.SetTriangles(tris, 0);
            rim.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (dv) { dv.Carved -= OnCarved; if (dv.takenLog == log) dv.takenLog = null; }
            foreach (var m in copies.Values) if (m) Destroy(m);
            if (mask) Destroy(mask);
            if (rim) Destroy(rim);
        }
    }
}
