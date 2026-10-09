using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    public enum FrameKind { None, Beam, Ladder, Span }

    /// <summary>The free frame (roadmap 29): beams from any point to any point (any angle, snapped to other beam ends),
    /// ladders of any length, and spans laid over three to six beam ends — the bones of shacks hung off towers and
    /// walkways strung between roofs. <see cref="FrameBeam"/>, <see cref="FrameSpan"/>; placed by BuildMode.</summary>
    public static class Frame
    {
        public const float MaxBeam = 8f, MinBeam = 0.3f, NodeSnap = 0.35f, V = 0.08f;

        /// <summary>The nearest beam / ladder end within <paramref name="radius"/> (snap target).</summary>
        public static bool NearestNode(Vector3 p, float radius, out Vector3 node)
        {
            node = p;
            float best = radius * radius;
            bool found = false;
            foreach (var b in FrameBeam.All)
            {
                if (!b) continue;
                for (int k = 0; k < 2; k++)
                {
                    var e = k == 0 ? b.A : b.B;
                    float d = (e - p).sqrMagnitude;
                    if (d < best) { best = d; node = e; found = true; }
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ meshes (cached per kind, style and length)
        static readonly Dictionary<(int, int), Mesh> beams = new Dictionary<(int, int), Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => beams.Clear();

        public static int Voxels(float length) => Mathf.Max(2, Mathf.RoundToInt(length / V));

        /// <summary>A member from its origin along +Z, <paramref name="n"/> voxels long: timber, a riveted steel I-beam, or a
        /// ladder (two rails, rungs across X every 0.32 m).</summary>
        public static Mesh BeamMesh(FrameKind kind, int style, int n)
        {
            var key = ((int)kind * 8 + style, n);
            if (beams.TryGetValue(key, out var m) && m) return m;
            var g = new VoxelGrid();
            if (kind == FrameKind.Ladder)
            {
                g.Mat((byte)(style == 1 ? ResourceType.Scrap : ResourceType.Wood));
                var rail = style == 1 ? Pal.Ramp(Pal.Metal, 2, 2901) : Pal.Ramp(Pal.Wood, 2, 2902);
                g.Box(-3, 0, 0, -3, 0, n - 1, rail); g.Box(3, 0, 0, 3, 0, n - 1, rail);
                for (int z = 2; z < n - 1; z += 4) g.Box(-2, 0, z, 2, 0, z, style == 1 ? Pal.Ramp(Pal.Chrome, 1) : Pal.Ramp(Pal.Wood, 1, 2903));
            }
            else if (style == 1)
            {
                // steel I-beam: flanges top and bottom, a web, rivets every half metre
                g.Mat((byte)ResourceType.Scrap);
                var steel = Pal.Weathered(Pal.Metal, 0.35f, 2904, 2, -40f);
                g.Box(-1, 1, 0, 1, 1, n - 1, steel); g.Box(-1, -1, 0, 1, -1, n - 1, steel); g.Box(0, 0, 0, 0, 0, n - 1, steel);
                for (int z = 3; z < n; z += 6) { g.Set(-1, 1, z, Pal.Solid(Pal.Chrome[1])); g.Set(1, -1, z, Pal.Solid(Pal.Chrome[1])); }
            }
            else
            {
                g.Mat((byte)ResourceType.Wood);
                g.Box(-1, -1, 0, 0, 0, n - 1, p => (p.z % 9 == 0) ? Pal.Wood[1] : Pal.Pick(Pal.Wood, p, 2905, 2));
            }
            g.Bevel();
            m = VoxelMesher.Build(g, "Frame_" + kind + style + "_" + n, V);
            beams[key] = m;
            return m;
        }

        /// <summary>A span's slab over a polygon (local x/z; y = 0 the walking surface), one voxel thick: planks, riveted
        /// sheet, or open grating.</summary>
        public static Mesh SpanMesh(int style, IReadOnlyList<Vector3> corners)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var c in corners) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); }
            var g = new VoxelGrid();
            g.Mat((byte)(style == 0 ? ResourceType.Wood : ResourceType.Scrap));
            VoxMat paint = style == 0 ? (p => (p.x % 3 == 0) ? Pal.Wood[1] : Pal.Pick(Pal.Wood, p, 2906, 2))
                         : style == 1 ? (p => (p.x % 6 == 0 && p.z % 6 == 0) ? Pal.Chrome[1] : Pal.Pick(Pal.Metal, p, 2907, 2))
                         : Pal.Ramp(Pal.Rust, 2, 2908);
            int x0 = Mathf.FloorToInt(minX / V), x1 = Mathf.CeilToInt(maxX / V), z0 = Mathf.FloorToInt(minZ / V), z1 = Mathf.CeilToInt(maxZ / V);
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                if (!Inside(corners, x * V, z * V)) continue;
                if (style == 2 && (x & 1) == 1 && (z & 1) == 1) continue;                          // grating: holes
                g.Set(x, -1, z, paint);
            }
            g.Bevel();
            return VoxelMesher.Build(g, "FrameSpan", V);
        }

        /// <summary>Point in polygon on x/z (even-odd).</summary>
        public static bool Inside(IReadOnlyList<Vector3> poly, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a.z > z) != (b.z > z) && x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Area of a polygon on x/z (m²).</summary>
        public static float Area(IReadOnlyList<Vector3> poly)
        {
            float a = 0f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += (poly[j].x + poly[i].x) * (poly[j].z - poly[i].z);
            return Mathf.Abs(a) * 0.5f;
        }

        /// <summary>A frame for a span over world corners: the centroid as origin, up = the best-fit plane's normal
        /// (upward), x along the first edge. Returns how far the farthest corner lies off that plane.</summary>
        public static float SpanFrame(IReadOnlyList<Vector3> world, out Vector3 origin, out Quaternion rot)
        {
            origin = Vector3.zero;
            foreach (var p in world) origin += p;
            origin /= world.Count;
            var n = Vector3.zero;
            for (int i = 0; i < world.Count; i++) n += Vector3.Cross(world[i] - origin, world[(i + 1) % world.Count] - origin);
            if (n.sqrMagnitude < 1e-6f) n = Vector3.up;
            n.Normalize();
            if (n.y < 0f) n = -n;
            var edge = Vector3.ProjectOnPlane(world[1] - world[0], n);
            if (edge.sqrMagnitude < 1e-6f) edge = Vector3.ProjectOnPlane(Vector3.forward, n);
            rot = Quaternion.LookRotation(Vector3.Cross(edge.normalized, n), n);
            float off = 0f;
            foreach (var p in world) off = Mathf.Max(off, Mathf.Abs(Vector3.Dot(p - origin, n)));
            return off;
        }
    }

    /// <summary>A straight member from its origin along +Z (<see cref="length"/> saved as its state): timber, steel, or a
    /// climbable ladder. Its ends are snap points for the next beam and for spans.</summary>
    public class FrameBeam : MonoBehaviour, IPlaceState
    {
        public static readonly List<FrameBeam> All = new List<FrameBeam>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public FrameKind kind = FrameKind.Beam;
        public int style;                // 0 wood, 1 steel
        public float length = 1f;

        public Vector3 A => transform.position;
        public Vector3 B => transform.TransformPoint(0f, 0f, length);

        public void SetLength(float l)
        {
            int n = Frame.Voxels(Mathf.Clamp(l, Frame.MinBeam, Frame.MaxBeam));
            length = n * Frame.V;
            var mesh = Frame.BeamMesh(kind, style, n);
            if (TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = mesh;
            if (!TryGetComponent<BoxCollider>(out var box)) box = gameObject.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center; box.size = Vector3.Max(mesh.bounds.size, new Vector3(0.12f, 0.12f, 0.1f));
            if (kind == FrameKind.Ladder)
            {
                if (!TryGetComponent<Ladder>(out var lad)) lad = gameObject.AddComponent<Ladder>();
                lad.localBounds = mesh.bounds;
                lad.alongZ = true;
            }
        }

        public string SaveState() => length.ToString("0.###", CultureInfo.InvariantCulture);
        public void LoadState(string s) { if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var l)) SetLength(l); }
    }

    /// <summary>A walkable slab over three to six beam ends (its local corners saved as state): planks, sheet or grating.
    /// Near-level spans are drivable decks too (<see cref="StructureGround.AddPolygonDeck"/>).</summary>
    public class FrameSpan : MonoBehaviour, IPlaceState
    {
        public int style;
        public readonly List<Vector3> corners = new List<Vector3>();

        public void SetCorners(IReadOnlyList<Vector3> local)
        {
            corners.Clear(); corners.AddRange(local);
            var mesh = Frame.SpanMesh(style, corners);
            if (TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = mesh;
            foreach (var b in GetComponents<BoxCollider>()) Destroy(b);
            if (!TryGetComponent<MeshCollider>(out var mc)) mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            StructureGround.Remove(this);
            if (transform.up.y > 0.97f && !GetComponentInParent<Rigidbody>()) StructureGround.AddPolygonDeck(this, transform, mc, corners);
        }

        void OnDestroy() => StructureGround.Remove(this);

        public string SaveState()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in corners) sb.Append(c.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(',').Append(c.z.ToString("0.###", CultureInfo.InvariantCulture)).Append(';');
            return sb.ToString();
        }

        public void LoadState(string s)
        {
            var list = new List<Vector3>();
            foreach (var part in (s ?? "").Split(';'))
            {
                var xz = part.Split(',');
                if (xz.Length == 2 && float.TryParse(xz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && float.TryParse(xz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) list.Add(new Vector3(x, 0f, z));
            }
            if (list.Count >= 3) SetCorners(list);
        }
    }
}
