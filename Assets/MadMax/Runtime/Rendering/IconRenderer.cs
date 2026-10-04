using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>CPU rasteriser for tiny pixel-art icons of voxel meshes (3/4 view, flat banded shading, z-buffer).
    /// Icons are cached per key.</summary>
    public static class IconRenderer
    {
        static readonly Dictionary<string, Color32[]> cache = new Dictionary<string, Color32[]>();
        static readonly Quaternion View = Quaternion.Euler(28f, -35f, 0f);

        public static Color32[] Get(string key, Mesh mesh, int size, bool diagonal = false)
        {
            if (HDIcons.TryGet(key, size, diagonal, out var hd)) return hd;                // rendered from the HD model once it is ready
            if (cache.TryGetValue(key, out var px)) return px;
            px = Render(mesh, size, diagonal);
            cache[key] = px;
            return px;
        }

        /// <summary>Already rendered (the HD one or the voxel one): drawing it costs nothing this frame.</summary>
        public static bool Cached(string key, int size) => cache.ContainsKey(key) || HDIcons.Ready(key, size);

        public static void Invalidate() { cache.Clear(); HDIcons.Invalidate(); }

        static Color32[] Render(Mesh mesh, int size, bool diagonal)
        {
            var px = new Color32[size * size];
            if (!mesh || !mesh.isReadable) return px;
            var verts = mesh.vertices; var cols = mesh.colors32; var tris = mesh.triangles;
            if (verts.Length == 0) return px;
            // long tools lie diagonally so they fill the icon
            var rot = Quaternion.Inverse(View) * (diagonal ? Quaternion.Euler(0f, 0f, -45f) * Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            var p = new Vector3[verts.Length];
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            for (int i = 0; i < verts.Length; i++) { p[i] = rot * verts[i]; min = Vector3.Min(min, p[i]); max = Vector3.Max(max, p[i]); }
            float span = Mathf.Max(max.x - min.x, max.y - min.y);
            float scale = (size - 2) / Mathf.Max(span, 1e-4f);
            var centre = (min + max) * 0.5f;
            var depth = new float[size * size];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.MaxValue;
            var light = new Vector3(-0.4f, 0.8f, -0.45f).normalized;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                Vector3 A = Screen(p[a], centre, scale, size), B = Screen(p[b], centre, scale, size), C = Screen(p[c], centre, scale, size);
                var n = Vector3.Cross(p[b] - p[a], p[c] - p[a]).normalized;
                float shade = 0.55f + 0.45f * Mathf.Abs(Vector3.Dot(n, light));
                shade = Mathf.Round(shade * 4f) / 4f;
                var col = cols.Length > a ? cols[a] : new Color32(200, 200, 200, 255);
                var sc = new Color32((byte)(col.r * shade), (byte)(col.g * shade), (byte)(col.b * shade), 255);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x)))), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y)))), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));
                float area = Edge(A, B, C);
                if (Mathf.Abs(area) < 1e-6f) continue;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector3(x + 0.5f, y + 0.5f, 0);
                    float w0 = Edge(B, C, q) / area, w1 = Edge(C, A, q) / area, w2 = Edge(A, B, q) / area;
                    if (w0 < -0.01f || w1 < -0.01f || w2 < -0.01f) continue;
                    float z = w0 * A.z + w1 * B.z + w2 * C.z;
                    int k = y * size + x;
                    if (z >= depth[k]) continue;
                    depth[k] = z; px[k] = sc;
                }
            }
            // 1 px dark outline around the silhouette
            var outlined = (Color32[])px.Clone();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (px[y * size + x].a > 0) continue;
                bool edge = (x > 0 && px[y * size + x - 1].a > 0) || (x < size - 1 && px[y * size + x + 1].a > 0) || (y > 0 && px[(y - 1) * size + x].a > 0) || (y < size - 1 && px[(y + 1) * size + x].a > 0);
                if (edge) outlined[y * size + x] = new Color32(18, 10, 8, 255);
            }
            return outlined;
        }

        static Vector3 Screen(Vector3 v, Vector3 centre, float scale, int size) =>
            new Vector3((v.x - centre.x) * scale + size * 0.5f, (v.y - centre.y) * scale + size * 0.5f, v.z);

        static float Edge(Vector3 a, Vector3 b, Vector3 c) => (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
    }
}
