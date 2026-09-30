using System.Collections.Generic;
using System.Text;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>What knocks and scrapes leave on a vehicle besides dents: glass panes that shatter (Body/Glass quads
    /// removed, shards on the ground), lamps that break (lens voxels go dark, their light goes out, red / amber / clear
    /// shards), and paint scraped to bare metal where it slid along something. Works on per-instance mesh copies (shared
    /// with <see cref="DeformableMesh"/>). Saved per vehicle (<c>VehicleSave.wear</c>).</summary>
    public class VehicleBreakables : MonoBehaviour
    {
        /// <summary>Broken lamps: 1 front-left, 2 front-right, 4 rear-left, 8 rear-right.</summary>
        public int BrokenLamps { get; private set; }
        /// <summary>Diagnostics: scrapes received and where the last one touched.</summary>
        [System.NonSerialized] public int ScrapeCalls; [System.NonSerialized] public Vector3 LastScrape;

        class Editable
        {
            public MeshFilter mf;
            public Mesh mesh;
            public Vector3[] centres, normals;           // per quad, mesh space
            public Color32[] colors;
            public int[] tris;                           // current index buffer (quads removed = zeroed)
            public readonly HashSet<int> removed = new HashSet<int>(), scratched = new HashSet<int>();
            public bool glass, dirtyTris, dirtyColors;
        }

        readonly Dictionary<MeshFilter, Editable> meshes = new Dictionary<MeshFilter, Editable>();
        readonly List<Mesh> owned = new List<Mesh>();
        Bounds bodyBounds;
        Transform body;
        VehicleLights lights;
        VehicleLights Lights => lights ? lights : lights = GetComponent<VehicleLights>();     // added after us (fleet registration)
        bool dirty;

        void Awake()
        {
            body = transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            bodyBounds = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds();
        }

        void OnDestroy() { foreach (var m in owned) if (m) Destroy(m); }

        Editable Edit(MeshFilter mf)
        {
            if (!mf || !mf.sharedMesh) return null;
            if (meshes.TryGetValue(mf, out var e) && e.mesh == mf.sharedMesh) return e;
            var mesh = mf.sharedMesh;
            if (!mesh.name.EndsWith("(dented)") && !owned.Contains(mesh))
            {
                mesh = Instantiate(mesh); mesh.name = mf.sharedMesh.name + " (worn)"; mf.sharedMesh = mesh; owned.Add(mesh);
            }
            var v = mesh.vertices; var n = mesh.normals;
            e = new Editable { mf = mf, mesh = mesh, colors = mesh.colors32, tris = mesh.triangles, glass = mf.name == "Glass" };
            int quads = e.tris.Length / 6;
            e.centres = new Vector3[quads]; e.normals = new Vector3[quads];
            for (int q = 0; q < quads; q++)
            {
                int a = e.tris[q * 6], b = e.tris[q * 6 + 1], c = e.tris[q * 6 + 2], d = e.tris[q * 6 + 5];
                e.centres[q] = (v[a] + v[b] + v[c] + v[d]) * 0.25f;
                e.normals[q] = n.Length > a ? n[a] : Vector3.up;
            }
            if (meshes.TryGetValue(mf, out var old)) { foreach (var q in old.removed) Remove(e, q); foreach (var q in old.scratched) Scratch(e, q); }
            meshes[mf] = e;
            return e;
        }

        void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            foreach (var e in meshes.Values)
            {
                if (!e.mesh) continue;
                if (e.dirtyTris) { e.mesh.triangles = e.tris; e.dirtyTris = false; }
                if (e.dirtyColors) { e.mesh.colors32 = e.colors; e.dirtyColors = false; }
            }
        }

        static void Remove(Editable e, int q)
        {
            if (!e.removed.Add(q) && e.tris[q * 6] == e.tris[q * 6 + 1]) return;
            for (int i = 0; i < 6; i++) e.tris[q * 6 + i] = e.tris[q * 6];            // degenerate: the quad disappears
            e.dirtyTris = true;
        }

        static readonly Color32 Bare = Pal.Metal[3], BareDark = Pal.Metal[2];

        static void Scratch(Editable e, int q)
        {
            e.scratched.Add(q);
            int v0 = e.tris[q * 6];
            if (v0 >= e.colors.Length) return;
            var c = (q * 7919) % 5 < 2 ? BareDark : Bare;                              // speckled bare metal
            var o = e.colors[v0];
            var s = Color32.Lerp(o, c, 0.7f);
            foreach (int vi in new[] { e.tris[q * 6], e.tris[q * 6 + 1], e.tris[q * 6 + 2], e.tris[q * 6 + 5] }) e.colors[vi] = s;
            e.dirtyColors = true;
        }

        static bool Near(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 12;
        static bool IsLamp(Color32 c) => Near(c, Pal.LightW) || Near(c, Pal.LightY) || Near(c, Pal.TailR) || Near(c, Pal.Amber);
        static bool IsGlassColor(Color32 c) { foreach (var g in Pal.Glass) if (Near(c, g)) return true; return false; }

        /// <summary>A knock at <paramref name="point"/>: panes of glass and lamps within <paramref name="radius"/> break.</summary>
        public void Smash(Vector3 point, float radius, float power)
        {
            if (power < 1.5f) return;
            var glassT = body ? body.Find("Glass") : null;
            var gmf = glassT ? glassT.GetComponent<MeshFilter>() : null;
            var e = Edit(gmf);
            if (e != null)
            {
                var lp = gmf.transform.InverseTransformPoint(point);
                float r = radius / Mathf.Max(0.01f, gmf.transform.lossyScale.y);
                int n = 0; Vector3 hit = Vector3.zero;
                for (int q = 0; q < e.centres.Length; q++)
                    if (!e.removed.Contains(q) && (e.centres[q] - lp).sqrMagnitude < r * r) { Remove(e, q); n++; hit += e.centres[q]; }
                if (n > 0)
                {
                    var at = gmf.transform.TransformPoint(hit / n);
                    MadMax.Audio.Sfx.Play("glass_break", at, 0.8f, Random.Range(0.9f, 1.15f), 40f, 0.1f);
                    Burst(at, Pal.Glass, Mathf.Clamp(n / 3, 4, 18));
                    Shards.Drop(at, Shards.Kind.Glass);
                    dirty = true;
                }
            }
            SmashLamps(point, radius);
        }

        void SmashLamps(Vector3 point, float radius)
        {
            var bmf = body ? body.GetComponent<MeshFilter>() : null;
            var e = Edit(bmf);
            if (e == null) return;
            var lp = bmf.transform.InverseTransformPoint(point);
            float r = radius + 0.25f;
            int before = BrokenLamps;
            for (int q = 0; q < e.centres.Length; q++)
            {
                if (e.removed.Contains(q) || e.scratched.Contains(q) || (e.centres[q] - lp).sqrMagnitude > r * r) continue;
                int v0 = e.tris[q * 6];
                if (v0 >= e.colors.Length || !IsLamp(e.colors[v0])) continue;
                var ctr = e.centres[q];
                bool front = ctr.z > bodyBounds.center.z, left = ctr.x < 0f;
                int bit = front ? (left ? 1 : 2) : (left ? 4 : 8);
                var was = e.colors[v0];
                Remove(e, q);                                                               // the lens is gone: a dark hole
                if ((BrokenLamps & bit) == 0)
                {
                    BrokenLamps |= bit;
                    var at = bmf.transform.TransformPoint(ctr);
                    var kind = Near(was, Pal.TailR) ? Shards.Kind.Red : Near(was, Pal.Amber) ? Shards.Kind.Amber : Shards.Kind.Clear;
                    MadMax.Audio.Sfx.Play("glass_break", at, 0.5f, Random.Range(1.2f, 1.5f), 30f, 0.1f);
                    Burst(at, kind == Shards.Kind.Red ? new[] { Pal.TailR } : kind == Shards.Kind.Amber ? new[] { Pal.Amber } : new[] { Pal.LightW, Pal.Glass[3] }, 6);
                    Shards.Drop(at, kind);
                }
            }
            if (BrokenLamps != before) { dirty = true; if (Lights) Lights.SetBroken(BrokenLamps); }
        }

        /// <summary>Paint scraped to bare metal on every panel near <paramref name="point"/> facing the contact.</summary>
        public void Scrape(Vector3 point, Vector3 normal, float radius)
        {
            ScrapeCalls++; LastScrape = point;
            // collider boxes stand proud of the rounded voxel body: find the nearest facing paint within reach, then
            // wear a band of the given radius around it
            const float Reach = 0.6f;
            float nearest = float.MaxValue;
            scrapeCandidates.Clear();
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == "Glass" || mf.name == "Driver" || mf.name.StartsWith("Beam") || mf.GetComponent<MadMax.Building.Placeable>()) continue;
                var part = mf.GetComponentInParent<VehiclePart>();
                if (part && part.category == PartCategory.Wheel) continue;
                var r = mf.GetComponent<Renderer>();
                if (!r || r.bounds.SqrDistance(point) > Reach * Reach) continue;
                var e = Edit(mf);
                if (e == null) continue;
                var lp = mf.transform.InverseTransformPoint(point);
                var ln = -mf.transform.InverseTransformDirection(normal);                          // contact normals point at us: our rubbed faces face back along it
                float scale = Mathf.Max(0.01f, mf.transform.lossyScale.y);
                for (int q = 0; q < e.centres.Length; q++)
                {
                    if (e.removed.Contains(q) || e.scratched.Contains(q)) continue;
                    float d = (e.centres[q] - lp).magnitude * scale;
                    if (d > Reach || Vector3.Dot(e.normals[q], ln) < 0.2f) continue;
                    int v0 = e.tris[q * 6];
                    if (v0 < e.colors.Length && (IsLamp(e.colors[v0]) || IsGlassColor(e.colors[v0]))) continue;
                    scrapeCandidates.Add((e, q, d));
                    nearest = Mathf.Min(nearest, d);
                }
            }
            foreach (var (e, q, d) in scrapeCandidates)
            {
                if (d > nearest + radius) continue;
                if (((q * 2654435761u) >> 28) < 5) continue;                                   // leave flecks of paint
                Scratch(e, q);
                dirty = true;
            }
        }

        static readonly List<(Editable e, int q, float d)> scrapeCandidates = new List<(Editable, int, float)>();

        static readonly List<DebrisSystem.Chunk> burst = new List<DebrisSystem.Chunk>();
        static void Burst(Vector3 at, Color32[] ramp, int n)
        {
            var ds = DebrisSystem.Instance;
            if (!ds) return;
            burst.Clear();
            for (int i = 0; i < n; i++) burst.Add(new DebrisSystem.Chunk { position = at + Random.insideUnitSphere * 0.2f, color = ramp[i % ramp.Length] });
            ds.Emit(burst, 0.035f, Vector3.down, 1.2f);
        }

        // ---------------------------------------------------------------- save: "lamps|path:r1,r2;s5,s6|path:..."
        public string SaveState()
        {
            if (BrokenLamps == 0 && meshes.Count == 0) return null;
            var sb = new StringBuilder();
            sb.Append(BrokenLamps);
            foreach (var e in meshes.Values)
            {
                if (e.removed.Count == 0 && e.scratched.Count == 0) continue;
                sb.Append('|').Append(Path(e.mf.transform)).Append(':');
                sb.Append(string.Join(",", e.removed)).Append(';').Append(string.Join(",", e.scratched));
            }
            return sb.ToString();
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split('|');
            int.TryParse(parts[0], out int lamps);
            for (int i = 1; i < parts.Length; i++)
            {
                int colon = parts[i].LastIndexOf(':');
                if (colon < 0) continue;
                var t = parts[i].Substring(0, colon) == "" ? transform : transform.Find(parts[i].Substring(0, colon));
                var e = t ? Edit(t.GetComponent<MeshFilter>()) : null;
                if (e == null) continue;
                var lists = parts[i].Substring(colon + 1).Split(';');
                foreach (var q in lists[0].Split(',')) if (int.TryParse(q, out int qi) && qi < e.centres.Length) Remove(e, qi);
                if (lists.Length > 1) foreach (var q in lists[1].Split(',')) if (int.TryParse(q, out int qi) && qi < e.centres.Length) Scratch(e, qi);
            }
            BrokenLamps = lamps;
            if (Lights) Lights.SetBroken(lamps);
            dirty = true;
        }

        string Path(Transform t)
        {
            var names = new List<string>();
            for (var p = t; p && p != transform; p = p.parent) names.Insert(0, p.name);
            return string.Join("/", names);
        }
    }
}
