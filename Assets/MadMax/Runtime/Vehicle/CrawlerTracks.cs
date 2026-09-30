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
            if (sides == null) return;
            var cam = Camera.main;
            if (cam && (cam.transform.position - transform.position).sqrMagnitude > 70f * 70f && (Time.frameCount & 3) != 0) return;
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
    }
}
