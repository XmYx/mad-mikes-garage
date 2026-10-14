using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Visible load in a bucket, in front of a blade or in a tipper bed, made from the tool's own mesh whenever
    /// the fill or the tool's pose changes. Bucket / bed: the tool's surfaces are seen from above on a grid (lowest =
    /// floor, low rims at the footprint's edge = the spill level) and the load's volume (a share of the struck volume,
    /// up to 1.25 × heaped) is poured in — a level surface that stays level as the bucket tilts, crowned into a heap at
    /// the angle of repose past the rim. Blade: a windrow on the ground in front of the blade sized by the volume.
    /// Lumps and clod colours ride with the tool.</summary>
    [DefaultExecutionOrder(200)]                 // after Machine has posed the arm this frame
    public class SoilHeap : MonoBehaviour
    {
        public enum Shape { Bucket, Blade, Bed }

        /// <summary>Fallback mound size in voxels (a tool mesh that can't be read).</summary>
        public Vector3Int size = new Vector3Int(5, 4, 3);
        public Shape shape;

        const int N = 30;                       // grid cells across the tool's footprint
        const float Repose = 0.7f;              // tan of loose soil's angle of repose (~35°)
        const float HeapedFactor = 1.25f;       // heaped capacity over struck

        MeshFilter mf;
        MeshRenderer mr;
        Mesh mesh;
        Transform seg;
        Vector3[] pts;                          // samples on the tool's surfaces, tool-local
        Bounds local;
        Vector3 fallbackAt;
        ResourceType shown = ResourceType.None;
        float fill, lastFill = -1f, bladeVolume;
        Matrix4x4 lastPose;
        Color32 colour;

        readonly float[] lo = new float[N * N], hi = new float[N * N], floorY = new float[N * N], surf = new float[N * N];
        readonly int[] count = new int[N * N];
        readonly bool[] wet = new bool[N * N];
        readonly int[] corner = new int[(N + 1) * (N + 1)];
        readonly List<float> rims = new List<float>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color32> cols = new List<Color32>();
        readonly List<int> tris = new List<int>();

        /// <summary>Triangles of the current load (tests).</summary>
        public int Triangles => mesh ? (int)mesh.GetIndexCount(0) / 3 : 0;
        /// <summary>World bounds of the current load (tests).</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (!mesh || mesh.vertexCount == 0) return new Bounds(transform.position, Vector3.zero);
                var b = mesh.bounds; var m = transform.localToWorldMatrix;
                var w = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
                for (int i = 0; i < 8; i++) w.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                return w;
            }
        }

        /// <summary>A load on <paramref name="parent"/> (the bucket / blade segment or the bed part).
        /// <paramref name="localVoxels"/> and <paramref name="size"/> only place the fallback mound.</summary>
        public static SoilHeap Create(Transform parent, Vector3 localVoxels, Shape shape, Vector3Int size, Material mat)
        {
            var go = new GameObject("SoilHeap", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<SoilHeap>();
            h.shape = shape; h.size = size; h.seg = parent;
            h.fallbackAt = localVoxels * VoxelMesher.DefaultSize;
            h.mf = go.GetComponent<MeshFilter>(); h.mr = go.GetComponent<MeshRenderer>();
            h.mr.sharedMaterial = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : mat;
            h.mesh = new Mesh { name = "SoilLoad" };
            h.mesh.MarkDynamic();
            h.mf.sharedMesh = h.mesh;
            go.SetActive(false);
            return h;
        }

        /// <summary>Show <paramref name="amount"/> (0..1 of capacity) of <paramref name="type"/>; <paramref name="volume"/>
        /// (m³) sizes a blade's windrow (default amount × 1.2).</summary>
        public void Set(float amount, ResourceType type, float volume = -1f)
        {
            amount = Mathf.Clamp01(amount);
            bool show = amount > 0.02f && type != ResourceType.None;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show) return;
            if (type != shown) { shown = type; colour = ResourceInfo.Color(type); lastFill = -1f; }
            fill = amount;
            bladeVolume = volume >= 0f ? volume : amount * 1.2f;
        }

        void LateUpdate()
        {
            if (!seg) return;
            if (pts == null) Sample();
            var pose = seg.localToWorldMatrix;
            if (Mathf.Abs(fill - lastFill) < 0.004f && Same(pose, lastPose)) return;   // a parked load costs nothing
            lastFill = fill; lastPose = pose;
            if (pts.Length < 32) { Fallback(); return; }
            // worked out in world space (level with gravity), stored in the tool's space: the load rides with the tool
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
            if (shape == Shape.Blade) BuildBlade(); else BuildContainer();
        }

        static bool Same(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > 0.002f) return false;
            return true;
        }

        // ------------------------------------------------------------------ the tool's surfaces
        void Sample()
        {
            var list = new List<Vector3>(4096);
            var rnd = new System.Random(4211);
            var inv = seg.worldToLocalMatrix;
            var tv = new List<Vector3>(); var tt = new List<int>();
            foreach (var r in seg.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform == transform || r.name == "Glass" || r.name.StartsWith("LOD") || r is ParticleSystemRenderer) continue;   // LOD0 only
                Mesh m = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;
                if (!m || !m.isReadable) continue;
                var toSeg = inv * r.transform.localToWorldMatrix;
                tv.Clear(); m.GetVertices(tv);
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    if (m.GetTopology(s) != MeshTopology.Triangles) continue;
                    tt.Clear(); m.GetTriangles(tt, s);
                    for (int t = 0; t + 2 < tt.Count; t += 3)
                    {
                        Vector3 a = toSeg.MultiplyPoint3x4(tv[tt[t]]), b = toSeg.MultiplyPoint3x4(tv[tt[t + 1]]), c = toSeg.MultiplyPoint3x4(tv[tt[t + 2]]);
                        float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                        int k = 1 + Mathf.Min(60, Mathf.FloorToInt(area / (0.03f * 0.03f)));
                        for (int i = 0; i < k; i++)
                        {
                            float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble();
                            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                            list.Add(a + (b - a) * u + (c - a) * v);
                        }
                    }
                }
            }
            if (list.Count > 12000)                                                             // thin evenly
            {
                var thin = new List<Vector3>(12000); float step = list.Count / 12000f;
                for (float i = 0; i < list.Count; i += step) thin.Add(list[(int)i]);
                list = thin;
            }
            pts = list.ToArray();
            local = new Bounds(pts.Length > 0 ? pts[0] : Vector3.zero, Vector3.zero);
            foreach (var p in pts) local.Encapsulate(p);
        }

        // grid frame: world up, columns along the tool's forward on the ground plane
        Vector3 origin, gx, gz;
        float cell;

        bool Frame()
        {
            var f = Vector3.ProjectOnPlane(seg.forward, Vector3.up);
            if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(seg.up, Vector3.up);
            if (f.sqrMagnitude < 1e-4f) return false;
            gz = f.normalized; gx = Vector3.Cross(Vector3.up, gz);
            float ext = Vector3.Scale(local.extents, seg.lossyScale).magnitude;
            cell = Mathf.Max(0.01f, ext * 2f / N);
            origin = seg.TransformPoint(local.center) - gx * ext - gz * ext;
            return true;
        }

        // ------------------------------------------------------------------ bucket / bed: pour the volume in
        void BuildContainer()
        {
            if (!Frame()) { Clear(); return; }
            for (int k = 0; k < N * N; k++) { lo[k] = float.MaxValue; hi[k] = float.MinValue; count[k] = 0; }
            var m = seg.localToWorldMatrix;
            for (int n = 0; n < pts.Length; n++)
            {
                var p = m.MultiplyPoint3x4(pts[n]);
                var d = p - origin;
                int i = (int)(Vector3.Dot(d, gx) / cell), j = (int)(Vector3.Dot(d, gz) / cell);
                if (i < 0 || j < 0 || i >= N || j >= N) continue;
                int k = j * N + i;
                if (p.y < lo[k]) lo[k] = p.y;
                if (p.y > hi[k]) hi[k] = p.y;
                count[k]++;
            }
            for (int pass = 0; pass < 2; pass++)                                               // close pinholes between samples
                for (int j = 1; j < N - 1; j++)
                for (int i = 1; i < N - 1; i++)
                {
                    int k = j * N + i;
                    if (count[k] != 0) continue;
                    int n = 0; float l = 0f, h = float.MinValue;
                    for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                    { int q = k + dj * N + di; if (count[q] != 0) { n++; l += lo[q]; h = Mathf.Max(h, hi[q]); } }
                    if (n >= 6) { lo[k] = l / n; hi[k] = h; count[k] = -1; }
                }
            // the spill level: the low rims at the footprint's edge (cutting edge, tail gate)
            rims.Clear();
            float minFloor = float.MaxValue;
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = j * N + i;
                if (count[k] == 0) continue;
                floorY[k] = lo[k] + 0.02f;
                minFloor = Mathf.Min(minFloor, floorY[k]);
                bool edge = i == 0 || j == 0 || i == N - 1 || j == N - 1 || count[k - 1] == 0 || count[k + 1] == 0 || count[k - N] == 0 || count[k + N] == 0;
                if (edge) rims.Add(hi[k]);
            }
            if (rims.Count == 0) { Clear(); return; }
            rims.Sort();
            float rim = rims[Mathf.Min(rims.Count - 1, rims.Count / 8)];
            float area = cell * cell;
            float struck = 0f;
            for (int k = 0; k < N * N; k++) if (count[k] != 0) struck += Mathf.Max(0f, rim - floorY[k]) * area;
            if (struck <= 1e-4f) { Clear(); return; }                                            // tipped over: nothing stays in
            float want = fill * struck * HeapedFactor;
            float level = rim, crown = 0f;
            if (want < struck)
            {
                float a = minFloor, b = rim;
                for (int it = 0; it < 18; it++)
                {
                    float L = (a + b) * 0.5f, v = 0f;
                    for (int k = 0; k < N * N; k++) if (count[k] != 0) v += Mathf.Max(0f, L - floorY[k]) * area;
                    if (v > want) b = L; else a = L;
                }
                level = (a + b) * 0.5f;
            }
            Vector2 c0 = Vector2.zero; int nw = 0;
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            {
                int k = j * N + i;
                // the outermost ring of the footprint is the steel itself (side walls, tail gate, cutting edge)
                bool ring = i == 0 || j == 0 || i == N - 1 || j == N - 1 || count[k - 1] == 0 || count[k + 1] == 0 || count[k - N] == 0 || count[k + N] == 0;
                wet[k] = count[k] != 0 && !ring && floorY[k] < level - 0.004f;
                if (wet[k]) { c0 += new Vector2(i, j); nw++; }
            }
            if (nw < 3) { Clear(); return; }
            c0 /= nw;
            float rx = 0.5f, rz = 0.5f;
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) if (wet[j * N + i]) { rx = Mathf.Max(rx, Mathf.Abs(i - c0.x) + 0.5f); rz = Mathf.Max(rz, Mathf.Abs(j - c0.y) + 0.5f); }
            if (want > struck)                                                                 // heaped: a dome no steeper than the repose
            {
                float sum = 0f;
                for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) if (wet[j * N + i]) sum += Dome(i, j, c0, rx, rz) * area;
                crown = sum > 0f ? Mathf.Min((want - struck) / sum, Repose * Mathf.Min(rx, rz) * cell) : 0f;
            }
            var toTool = seg.worldToLocalMatrix;
            float lumpy = 0.3f + 0.7f * Mathf.Clamp01(fill * 1.5f);
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = j * N + i;
                if (!wet[k]) { surf[k] = float.NaN; continue; }
                var lp = toTool.MultiplyPoint3x4(Point(i + 0.5f, j + 0.5f, level));
                surf[k] = Mathf.Max(floorY[k] + 0.01f, level + crown * Dome(i, j, c0, rx, rz) + Lumps(lp) * lumpy);
            }
            Emit(toTool);
        }

        Vector3 Point(float i, float j, float y) { var p = origin + gx * (i * cell) + gz * (j * cell); p.y = y; return p; }

        static float Dome(int i, int j, Vector2 c, float rx, float rz)
        {
            float x = (i - c.x) / rx, z = (j - c.y) / rz;
            return Mathf.Max(0f, 1f - (x * x + z * z));
        }

        void Emit(Matrix4x4 toTool)
        {
            verts.Clear(); cols.Clear(); tris.Clear();
            // corner heights: the mean of the wetted cells around each corner; next to a dry cell (a wall, the open edge)
            // the surface dips a little so it meets the steel instead of hanging over it
            for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                float s = 0f; int n = 0, dry = 0;
                for (int dj = -1; dj <= 0; dj++) for (int di = -1; di <= 0; di++)
                {
                    int ci = i + di, cj = j + dj;
                    if (ci < 0 || cj < 0 || ci >= N || cj >= N || float.IsNaN(surf[cj * N + ci])) { dry++; continue; }
                    s += surf[cj * N + ci]; n++;
                }
                if (n == 0) { corner[j * (N + 1) + i] = -1; continue; }
                float y = s / n - (dry > 0 ? 0.025f : 0f);
                corner[j * (N + 1) + i] = verts.Count;
                var p = Point(i, j, y);
                verts.Add(p); cols.Add(Tone(toTool.MultiplyPoint3x4(p)));
            }
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                if (float.IsNaN(surf[j * N + i])) continue;
                int a = corner[j * (N + 1) + i], b = corner[j * (N + 1) + i + 1], c = corner[(j + 1) * (N + 1) + i], d = corner[(j + 1) * (N + 1) + i + 1];
                if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            Upload();
        }

        // ------------------------------------------------------------------ blade: a windrow on the ground in front
        void BuildBlade()
        {
            var t = DeformableTerrain.Instance;
            var f = Vector3.ProjectOnPlane(seg.forward, Vector3.up);
            if (!t || f.sqrMagnitude < 0.01f) { Clear(); return; }
            gz = f.normalized; gx = Vector3.Cross(Vector3.up, gz);
            var m = seg.localToWorldMatrix;
            var c = seg.TransformPoint(local.center);
            float minX = float.MaxValue, maxX = float.MinValue, front = float.MinValue, top = float.MinValue, bottom = float.MaxValue;
            for (int n = 0; n < pts.Length; n++)
            {
                var p = m.MultiplyPoint3x4(pts[n]); var d = p - c;
                float x = Vector3.Dot(d, gx), z = Vector3.Dot(d, gz);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); front = Mathf.Max(front, z);
                top = Mathf.Max(top, p.y); bottom = Mathf.Min(bottom, p.y);
            }
            float width = Mathf.Max(0.5f, maxX - minX - 0.1f);
            // cross-section of a windrow against the blade = h² / (2 · repose); the ends slump off at the same slope
            float h = Mathf.Min(Mathf.Sqrt(2f * Repose * bladeVolume / width), Mathf.Max(0.2f, top - bottom) * 0.95f);
            float reach = h / Repose + 0.05f;
            origin = c + gx * (minX + 0.05f) + gz * (front - 0.15f);
            int nx = N, nz = Mathf.Max(4, N / 2);
            float sx = width / (nx - 1), sz = reach / (nz - 1);
            verts.Clear(); cols.Clear(); tris.Clear();
            var toTool = seg.worldToLocalMatrix;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                var p = origin + gx * (i * sx) + gz * (j * sz);
                float ground = t.Height(p.x, p.z);
                float ends = Mathf.Clamp01(Mathf.Min(i, nx - 1 - i) * sx * Repose / Mathf.Max(0.05f, h));
                float y = Mathf.Max(0f, (h - Repose * j * sz) * ends);
                var lp = toTool.MultiplyPoint3x4(p);
                p.y = ground + (y > 0.001f ? y + Lumps(lp) : -0.03f);
                verts.Add(p); cols.Add(Tone(lp));
            }
            for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i;
                tris.Add(a); tris.Add(a + nx); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + nx); tris.Add(a + nx + 1);
            }
            Upload();
        }

        // ------------------------------------------------------------------ mesh
        void Upload()
        {
            mesh.Clear();
            if (tris.Count == 0) return;
            {
                var toLocal = transform.worldToLocalMatrix;
                if (fallbackMode) toLocal = Matrix4x4.identity;
                for (int i = 0; i < verts.Count; i++) verts[i] = toLocal.MultiplyPoint3x4(verts[i]);
                if (!fallbackMode && toLocal.determinant < 0f)                                   // a mirrored tool: keep the faces up
                    for (int i = 0; i + 2 < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        void Clear() { if (mesh) mesh.Clear(); }

        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Value(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float a = Mathf.Lerp(Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), fx), Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx), fy);
            float b = Mathf.Lerp(Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx), Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx), fy);
            return Mathf.Lerp(a, b, fz);
        }

        // lumps (metres) and clod colour, in tool-local coordinates so they ride with the bucket
        static float Lumps(Vector3 lp) => (Value(lp * 9f) - 0.5f) * 0.08f + (Value(lp * 23f) - 0.5f) * 0.035f;

        Color32 Tone(Vector3 lp)
        {
            // freshly dug: darker and moister than the sun-dried ground it came from, mottled, with dark clods
            float v = 0.62f + Value(lp * 14f) * 0.3f + Value(lp * 5f) * 0.08f - (Value(lp * 41f) > 0.78f ? 0.2f : 0f);
            return new Color32((byte)Mathf.Clamp(colour.r * v, 0, 255), (byte)Mathf.Clamp(colour.g * v, 0, 255), (byte)Mathf.Clamp(colour.b * v, 0, 255), 255);
        }

        /// <summary>A tool whose mesh can't be read: a rough mound at the given offset, scaled by the fill.</summary>
        bool fallbackMode;

        void Fallback()
        {
            fallbackMode = true;
            transform.localPosition = fallbackAt; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
            verts.Clear(); cols.Clear(); tris.Clear();
            const int R = 10;
            var s = new Vector3(size.x, size.y, size.z) * VoxelMesher.DefaultSize * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(fill));
            for (int j = 0; j <= R; j++)
            for (int i = 0; i <= R; i++)
            {
                float x = i / (float)R * 2f - 1f, z = j / (float)R * 2f - 1f;
                var p = new Vector3(x * s.x, Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - z * z)) * s.y, z * s.z);
                verts.Add(p); cols.Add(Tone(p));
            }
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                int a = j * (R + 1) + i;
                tris.Add(a); tris.Add(a + R + 1); tris.Add(a + 1); tris.Add(a + 1); tris.Add(a + R + 1); tris.Add(a + R + 2);
            }
            Upload();
            fallbackMode = false;
        }

        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
