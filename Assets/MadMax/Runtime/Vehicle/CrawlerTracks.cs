using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Visible crawler tracks: per side a loop of voxel links (plate + grouser) wrapped round the road wheels
    /// (<see cref="VehicleDriver.TrackProfile"/>: sprocket, three road wheels, idler, each on its own suspension), running
    /// with the belt's travel so the links lie still on the ground while the machine drives; road wheels roll. Added by
    /// <see cref="VehicleDriver"/> for tracked vehicles. Far from the camera the belt updates every fourth frame.</summary>
    public class CrawlerTracks : MonoBehaviour
    {
        const float Pitch = 0.17f, Half = 0.04f;

        class Side
        {
            public bool left;
            public Transform[] links;
            public readonly List<Transform> rollers = new List<Transform>();
        }

        VehicleDriver driver;
        Side[] sides;
        Transform root;
        readonly List<VehicleDriver.TrackPoint> prof = new List<VehicleDriver.TrackPoint>();
        readonly List<Vector3> path = new List<Vector3>();
        readonly List<float> cum = new List<float>();
        static readonly Dictionary<int, Mesh> linkMeshes = new Dictionary<int, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => linkMeshes.Clear();

        void Start()
        {
            driver = GetComponent<VehicleDriver>();
            var body = transform.Find("Body");
            if (driver && driver.Tracked && body && (body.Find("Track_L") || body.Find("Track_R")))
            {
                belts = new[] { MakeBelt(body.Find("Track_L"), true), MakeBelt(body.Find("Track_R"), false) };   // HD tracks: no voxel links
                return;
            }
            var mat = body && body.TryGetComponent<MeshRenderer>(out var br) ? br.sharedMaterial : null;
            if (!driver || !driver.Tracked || !mat) { enabled = false; return; }
            root = new GameObject("Tracks").transform;
            root.SetParent(transform, false);
            sides = new Side[2];
            for (int k = 0; k < 2; k++)
            {
                var sd = sides[k] = new Side { left = k == 0 };
                driver.TrackProfile(sd.left, prof);
                if (prof.Count < 2) continue;
                BuildPath(sd.left);
                int n = Mathf.Max(8, Mathf.RoundToInt(cum[cum.Count - 1] / Pitch));
                var mesh = LinkMesh(Mathf.Max(3, Mathf.RoundToInt(prof[0].width / VoxelMesher.DefaultSize)));
                sd.links = new Transform[n];
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject("Link", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    sd.links[i] = go.transform;
                }
                // road wheels of their own for the three middle contacts (sprocket and idler are the mounted parts)
                foreach (var p in prof)
                {
                    if (!p.virt || !p.part) continue;
                    var pmf = p.part.GetComponent<MeshFilter>();
                    if (!pmf) continue;
                    var go = new GameObject("RoadWheel", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root, false);
                    go.GetComponent<MeshFilter>().sharedMesh = pmf.sharedMesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    if (sd.left) go.transform.localScale = new Vector3(-1f, 1f, 1f);                 // mirrored like the left sockets
                    sd.rollers.Add(go.transform);
                }
            }
        }

        static Mesh LinkMesh(int width)
        {
            if (linkMeshes.TryGetValue(width, out var m) && m) return m;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            int x0 = -(width / 2), x1 = x0 + width - 1;
            for (int x = x0; x <= x1; x++)
            {
                bool edge = x == x0 || x == x1;
                g.Set(x, 0, 0, Pal.Solid(edge ? Pal.Metal[1] : Pal.Tire[1]));
                g.Set(x, 0, 1, Pal.Solid(edge ? Pal.Metal[2] : Pal.Tire[2]));
                if (!edge) g.Set(x, -1, 0, Pal.Solid(Pal.Tire[(x & 1) == 0 ? 0 : 2]));            // grouser bar
            }
            return linkMeshes[width] = VoxelMesher.Build(g, "TrackLink" + width);
        }

        /// <summary>The belt centreline (vehicle space): bottom run under the road wheels, round the rear sprocket, back
        /// along the top, round the front idler. Fills <see cref="path"/> and the cumulative lengths <see cref="cum"/>.</summary>
        void BuildPath(bool left)
        {
            path.Clear(); cum.Clear();
            var f = prof[0]; var r = prof[prof.Count - 1];
            float x = f.axle.x + (left ? -0.5f : 0.5f) * f.width;
            float rr = f.radius + Half;
            foreach (var p in prof) path.Add(new Vector3(x, p.axle.y - p.radius - Half, p.axle.z));
            for (int i = 1; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI;
                path.Add(new Vector3(x, r.axle.y - rr * Mathf.Cos(a), r.axle.z - rr * Mathf.Sin(a)));
            }
            for (int i = 1; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI;
                path.Add(new Vector3(x, f.axle.y + rr * Mathf.Cos(a), f.axle.z + rr * Mathf.Sin(a)));
            }
            float s = 0f;
            cum.Add(0f);
            for (int i = 1; i < path.Count; i++) { s += Vector3.Distance(path[i - 1], path[i]); cum.Add(s); }
            s += Vector3.Distance(path[path.Count - 1], path[0]);
            cum.Add(s);
        }

        void Sample(float s, out Vector3 pos, out Vector3 tangent)
        {
            float total = cum[cum.Count - 1];
            s = Mathf.Repeat(s, total);
            int i = 1;
            while (i < cum.Count - 1 && cum[i] < s) i++;
            var a = path[i - 1]; var b = i < path.Count ? path[i] : path[0];
            float seg = Mathf.Max(1e-4f, cum[i] - cum[i - 1]);
            pos = Vector3.Lerp(a, b, (s - cum[i - 1]) / seg);
            tangent = (b - a).normalized;
        }

        void LateUpdate()
        {
            if (sides == null && belts == null) return;
            var cam = Camera.main;
            if (cam && (cam.transform.position - transform.position).sqrMagnitude > 70f * 70f && (Time.frameCount & 3) != 0) return;
            if (belts != null) { foreach (var b in belts) if (b != null) RunBelt(b); return; }
            foreach (var sd in sides)
            {
                if (sd == null || sd.links == null) continue;
                driver.TrackProfile(sd.left, prof);
                if (prof.Count < 2) continue;
                BuildPath(sd.left);
                float total = cum[cum.Count - 1], step = total / sd.links.Length, travel = driver.TrackTravel(sd.left);
                for (int i = 0; i < sd.links.Length; i++)
                {
                    Sample(i * step + travel, out var pos, out var tan);
                    var normal = Vector3.Cross(tan, Vector3.right);                                // bottom run: points at the ground
                    sd.links[i].localPosition = pos;
                    sd.links[i].localRotation = Quaternion.LookRotation(tan, -normal);
                }
                int k = 0;
                float spin = travel / Mathf.Max(0.05f, prof[0].radius) * Mathf.Rad2Deg;
                foreach (var p in prof)
                {
                    if (!p.virt || k >= sd.rollers.Count) continue;
                    var t = sd.rollers[k++];
                    t.localPosition = p.axle;
                    t.localRotation = Quaternion.Euler(spin % 360f, 0f, 0f);
                }
            }
        }
    
        // ---------------------------------------------------------------- HD belts
        /// <summary>An HD track (Body/Track_L|R: link plates with grousers round idler and sprocket, road wheels and carrier
        /// rollers in one mesh, tools/blender/hd/heavy/hvlib.track_assembly). The belt's vertices slide round the stadium
        /// path by the track travel modulo the link pitch (the belt repeats every link, so the wrap is invisible) while the
        /// wheels and frame stay; normals and tangents turn with the links on the arcs.</summary>
        class Belt
        {
            public bool left;
            public Mesh mesh;
            public Vector3[] src, dst, nSrc, nDst;
            public Vector4[] tSrc, tDst;
            public int[] idx;
            public float[] s0, h0, a0;
            public float axle, zRear, zFront, R, L1, total, pitch, shown = -1f;
        }

        Belt[] belts;
        const float GrouserOut = 0.87f;          // link outer face beyond the belt centreline (voxels, hvlib.track_assembly)

        Belt MakeBelt(Transform t, bool left)
        {
            if (!t || !t.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh || !mf.sharedMesh.isReadable) return null;
            var m = mf.sharedMesh;
            var bb = m.bounds;
            float S = VoxelMesher.DefaultSize, rOut = bb.extents.y;
            var b = new Belt { left = left, axle = bb.center.y, zRear = bb.min.z + rOut, zFront = bb.max.z - rOut, R = rOut - GrouserOut * S };
            if (b.zFront <= b.zRear || b.R < 0.05f) return null;
            b.L1 = b.zFront - b.zRear;
            b.total = 2f * b.L1 + 2f * Mathf.PI * b.R;
            b.pitch = b.total / Mathf.Max(8, Mathf.FloorToInt(b.total / (2f * S)));
            b.src = m.vertices; b.nSrc = m.normals; b.tSrc = m.tangents;
            // the belt is made of separate pieces (plate, grouser, guide horn per link): a piece whose centre lies on the
            // belt moves; wheels, rollers, sprocket teeth have their centres well inside it
            int nv = b.src.Length;
            var up = new int[nv];
            for (int i = 0; i < nv; i++) up[i] = i;
            int Root(int i) { while (up[i] != i) { up[i] = up[up[i]]; i = up[i]; } return i; }
            var tris = m.triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int ra = Root(tris[i]), rb = Root(tris[i + 1]), rc = Root(tris[i + 2]);
                up[rb] = ra; up[Root(rc)] = ra;
            }
            var sum = new Dictionary<int, Vector3>(); var count = new Dictionary<int, int>();
            for (int i = 0; i < nv; i++)
            {
                int r = Root(i);
                sum[r] = (sum.TryGetValue(r, out var acc) ? acc : Vector3.zero) + b.src[i];
                count[r] = (count.TryGetValue(r, out int c) ? c : 0) + 1;
            }
            var onBelt = new HashSet<int>();
            foreach (var kv in sum)
            {
                var c = kv.Value / count[kv.Key];
                Param(b, c.z, c.y, out _, out float cr);
                if (cr >= b.R - 0.5f * S) onBelt.Add(kv.Key);
            }
            var idx = new List<int>(); var s0 = new List<float>(); var h0 = new List<float>(); var a0 = new List<float>();
            for (int i = 0; i < nv; i++)
            {
                if (!onBelt.Contains(Root(i))) continue;
                Param(b, b.src[i].z, b.src[i].y, out float s, out float radial);
                Point(b, s, radial - b.R, out _, out _, out float a);
                idx.Add(i); s0.Add(s); h0.Add(radial - b.R); a0.Add(a);
            }
            if (idx.Count == 0) return null;
            b.idx = idx.ToArray(); b.s0 = s0.ToArray(); b.h0 = h0.ToArray(); b.a0 = a0.ToArray();
            b.dst = (Vector3[])b.src.Clone();
            b.nDst = b.nSrc.Length == b.src.Length ? (Vector3[])b.nSrc.Clone() : null;
            b.tDst = b.tSrc.Length == b.src.Length ? (Vector4[])b.tSrc.Clone() : null;
            b.mesh = Instantiate(m);
            b.mesh.name = m.name + " (belt)";
            b.mesh.MarkDynamic();
            mf.sharedMesh = b.mesh;
            return b;
        }

        /// <summary>Stadium coordinates of a point (mesh z, y): arc length along the centreline from the front of the bottom
        /// run (front → rear, up round the sprocket, back along the top, down round the idler) and the distance from the axle line.</summary>
        static void Param(Belt b, float z, float y, out float s, out float radial)
        {
            float dy = y - b.axle;
            if (z >= b.zRear && z <= b.zFront)
            {
                if (dy < 0f) { s = b.zFront - z; radial = -dy; }
                else { s = b.L1 + Mathf.PI * b.R + (z - b.zRear); radial = dy; }
            }
            else if (z < b.zRear)
            {
                float dz = z - b.zRear;
                radial = Mathf.Sqrt(dz * dz + dy * dy);
                s = b.L1 + Mathf.Atan2(-dz, -dy) * b.R;
            }
            else
            {
                float dz = z - b.zFront;
                radial = Mathf.Sqrt(dz * dz + dy * dy);
                s = 2f * b.L1 + Mathf.PI * b.R + Mathf.Atan2(dz, dy) * b.R;
            }
        }

        /// <summary>The point at arc length <paramref name="s"/>, <paramref name="h"/> outside the centreline, and the angle of
        /// the outward normal in the (z, y) plane.</summary>
        static void Point(Belt b, float s, float h, out float z, out float y, out float angle)
        {
            s = Mathf.Repeat(s, b.total);
            float r = b.R + h, arc = Mathf.PI * b.R;
            if (s < b.L1) { z = b.zFront - s; y = b.axle - r; angle = -Mathf.PI * 0.5f; return; }
            if (s < b.L1 + arc)
            {
                float t = (s - b.L1) / b.R;
                z = b.zRear - Mathf.Sin(t) * r; y = b.axle - Mathf.Cos(t) * r;
                angle = Mathf.Atan2(-Mathf.Cos(t), -Mathf.Sin(t));
                return;
            }
            if (s < 2f * b.L1 + arc) { z = b.zRear + (s - b.L1 - arc); y = b.axle + r; angle = Mathf.PI * 0.5f; return; }
            float u = (s - 2f * b.L1 - arc) / b.R;
            z = b.zFront + Mathf.Sin(u) * r; y = b.axle + Mathf.Cos(u) * r;
            angle = Mathf.Atan2(Mathf.Cos(u), Mathf.Sin(u));
        }

        void RunBelt(Belt b)
        {
            if (!b.mesh) return;
            float shift = Mathf.Repeat(driver.TrackTravel(b.left), b.pitch);
            if (Mathf.Abs(shift - b.shown) < 0.002f) return;
            b.shown = shift;
            for (int k = 0; k < b.idx.Length; k++)
            {
                int i = b.idx[k];
                Point(b, b.s0[k] + shift, b.h0[k], out float z, out float y, out float a);
                b.dst[i] = new Vector3(b.src[i].x, y, z);
                float da = a - b.a0[k];
                if (Mathf.Abs(da) < 1e-4f) { if (b.nDst != null) b.nDst[i] = b.nSrc[i]; if (b.tDst != null) b.tDst[i] = b.tSrc[i]; continue; }
                float c = Mathf.Cos(da), sn = Mathf.Sin(da);
                if (b.nDst != null) { var n = b.nSrc[i]; b.nDst[i] = new Vector3(n.x, n.z * sn + n.y * c, n.z * c - n.y * sn); }
                if (b.tDst != null) { var tg = b.tSrc[i]; b.tDst[i] = new Vector4(tg.x, tg.z * sn + tg.y * c, tg.z * c - tg.y * sn, tg.w); }
            }
            b.mesh.SetVertices(b.dst);
            if (b.nDst != null) b.mesh.SetNormals(b.nDst);
            if (b.tDst != null) b.mesh.SetTangents(b.tDst);
        }

        void OnDestroy()
        {
            if (belts != null) foreach (var b in belts) if (b != null && b.mesh) Destroy(b.mesh);
        }
    }
}
