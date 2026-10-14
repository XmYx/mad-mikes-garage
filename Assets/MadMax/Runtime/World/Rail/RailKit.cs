using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Procedural mesh builder for the railway (track, platforms, bridges, signals, rolling stock), written
    /// straight in game space: oriented boxes, extrusions of a cross-section along a polyline (rails, girders), lathed
    /// cylinders. Colours come from <see cref="Pal"/> ramps picked per ~0.7 m cell with rust blotches on metal, so long
    /// faces vary like the voxel props. Output: the visible mesh, a glow mesh (lamps, lenses: unlit material) and a
    /// collision mesh of what is marked solid. Pure C#: safe on worker threads.</summary>
    public sealed class RailKit
    {
        public readonly VoxelMesher.MeshData mesh = new VoxelMesher.MeshData();
        public readonly VoxelMesher.MeshData glow = new VoxelMesher.MeshData();
        public readonly VoxelMesher.MeshData solid = new VoxelMesher.MeshData();
        public int seed;
        /// <summary>Rust blotch strength for the next shapes (0 = clean paint).</summary>
        public float rust = 0.25f;
        public bool emit, isSolid;
        /// <summary>Offset subtracted from every vertex (a tile's origin).</summary>
        public Vector3 origin;

        public RailKit(int seed = 0) { this.seed = seed; }

        /// <summary>A telegraph pole split out of a track tile (its own breakable body, <see cref="BreakablePole"/>):
        /// post + cross-arm and its wire spans to the next pole, both with origin = the buried foot.</summary>
        public sealed class Pole { public string key, prev, next; public Vector3 foot; public RailKit post, wires; }
        /// <summary>Telegraph poles of a track tile (spawned by <see cref="RailTrack"/>).</summary>
        public readonly List<Pole> poles = new List<Pole>();

        public int Triangles => (mesh.tris.Count + glow.tris.Count) / 3;

        Color32 Colour(Color32[] ramp, Vector3 p, int salt)
        {
            var cell = new Vector3Int(Mathf.FloorToInt(p.x * 1.4f), Mathf.FloorToInt(p.y * 1.4f), Mathf.FloorToInt(p.z * 1.4f));
            var c = ramp[Mathf.Clamp((int)(Pal.Hash(cell, seed * 31 + salt) * ramp.Length), 0, ramp.Length - 1)];
            if (rust <= 0f || emit) return c;
            float blot = Pal.Noise(p * 0.5f, seed + salt * 7);
            float r = Mathf.Clamp01((blot - (1f - rust)) * 3f);
            if (r <= 0f) return c;
            return Color32.Lerp(c, Pal.Rust[Mathf.Clamp((int)(Pal.Hash(cell, seed + 5) * 4f), 0, 4)], Mathf.Min(0.8f, r));
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32[] ramp, int salt)
        {
            var n = Vector3.Cross(b - a, c - b);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - b, d - c);
            if (n.sqrMagnitude < 1e-14f) return;
            n.Normalize();
            var md = emit ? glow : mesh;
            Color32 ca = Colour(ramp, a, salt), cb = Colour(ramp, b, salt), cc = Colour(ramp, c, salt), cd = Colour(ramp, d, salt);
            Emit(md, a - origin, b - origin, c - origin, d - origin, n, ca, cb, cc, cd);
            if (isSolid) Emit(solid, a - origin, b - origin, c - origin, d - origin, n, default, default, default, default);
        }

        static void Emit(VoxelMesher.MeshData md, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int k = md.verts.Count;
            md.verts.Add(a); md.verts.Add(b); md.verts.Add(c); md.verts.Add(d);
            for (int i = 0; i < 4; i++) { md.norms.Add(n); md.uv3.Add(n); }
            md.cols.Add(ca); md.cols.Add(cb); md.cols.Add(cc); md.cols.Add(cd);
            // front face: Cross(b - a, c - b) = n
            md.tris.Add(k); md.tris.Add(k + 1); md.tris.Add(k + 2);
            md.tris.Add(k); md.tris.Add(k + 2); md.tris.Add(k + 3);
        }

        /// <summary>Box centred at c with axes (r, u, f) and half extents (hx, hy, hz).</summary>
        public void Box(Vector3 c, Vector3 r, Vector3 u, Vector3 f, float hx, float hy, float hz, Color32[] ramp, int salt = 0)
        {
            r *= hx; u *= hy; f *= hz;
            Vector3 p000 = c - r - u - f, p100 = c + r - u - f, p110 = c + r + u - f, p010 = c - r + u - f;
            Vector3 p001 = c - r - u + f, p101 = c + r - u + f, p111 = c + r + u + f, p011 = c - r + u + f;
            Quad(p001, p101, p111, p011, ramp, salt);      // +f
            Quad(p100, p000, p010, p110, ramp, salt);      // -f
            Quad(p101, p100, p110, p111, ramp, salt);      // +r
            Quad(p000, p001, p011, p010, ramp, salt);      // -r
            Quad(p010, p011, p111, p110, ramp, salt);      // +u
            Quad(p000, p100, p101, p001, ramp, salt);      // -u
        }

        /// <summary>Axis-aligned box from a frame: centre in frame coords (x right, y up, z forward).</summary>
        public void Box(Matrix4x4 frame, Vector3 centre, Vector3 half, Color32[] ramp, int salt = 0)
        {
            Box(frame.MultiplyPoint3x4(centre), frame.MultiplyVector(Vector3.right).normalized, frame.MultiplyVector(Vector3.up).normalized, frame.MultiplyVector(Vector3.forward).normalized, half.x, half.y, half.z, ramp, salt);
        }

        /// <summary>A square beam from p to q (t across, t2 up when the beam is horizontal).</summary>
        public void Beam(Vector3 p, Vector3 q, float t, float t2, Color32[] ramp, int salt = 0)
        {
            var d = q - p; float len = d.magnitude;
            if (len < 1e-3f) return;
            var f = d / len;
            var up = Mathf.Abs(f.y) > 0.95f ? Vector3.forward : Vector3.up;
            var r = Vector3.Cross(up, f).normalized;
            var u = Vector3.Cross(f, r);
            Box((p + q) * 0.5f, r, u, f, t * 0.5f, t2 * 0.5f, len * 0.5f, ramp, salt);
        }

        /// <summary>Capped cylinder from p to q.</summary>
        public void Cylinder(Vector3 p, Vector3 q, float radius, int seg, Color32[] ramp, int salt = 0, bool caps = true)
        {
            var d = q - p; float len = d.magnitude;
            if (len < 1e-4f) return;
            var a = d / len;
            var up = Mathf.Abs(a.y) > 0.95f ? Vector3.forward : Vector3.up;
            var u = Vector3.Cross(up, a).normalized; var w = Vector3.Cross(a, u);
            for (int k = 0; k < seg; k++)
            {
                float t0 = k * Mathf.PI * 2f / seg, t1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector3 e0 = (u * Mathf.Cos(t0) + w * Mathf.Sin(t0)) * radius, e1 = (u * Mathf.Cos(t1) + w * Mathf.Sin(t1)) * radius;
                Quad(p + e0, p + e1, q + e1, q + e0, ramp, salt);
                if (!caps) continue;
                Tri(q, q + e0, q + e1, ramp, salt);
                Tri(p, p + e1, p + e0, ramp, salt);
            }
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Color32[] ramp, int salt)
        {
            var n = Vector3.Cross(b - a, c - b);
            if (n.sqrMagnitude < 1e-14f) return;
            n.Normalize();
            var md = emit ? glow : mesh;
            int k = md.verts.Count;
            var col = Colour(ramp, (a + b + c) / 3f, salt);
            md.verts.Add(a - origin); md.verts.Add(b - origin); md.verts.Add(c - origin);
            for (int i = 0; i < 3; i++) { md.norms.Add(n); md.uv3.Add(n); md.cols.Add(col); }
            md.tris.Add(k); md.tris.Add(k + 1); md.tris.Add(k + 2);
        }

        /// <summary>Sweep a closed cross-section (x right, y up, counter-clockwise in that plane) along a path with
        /// per-point right vectors (rails, girders, handrails).</summary>
        public void Sweep(IList<Vector3> path, IList<Vector3> right, IList<Vector2> section, Color32[] ramp, int salt = 0)
        {
            int n = path.Count, m = section.Count;
            if (n < 2) return;
            var prev = new Vector3[m]; var cur = new Vector3[m];
            for (int i = 0; i < n; i++)
            {
                var r = right[i];
                var f = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                var u = Vector3.Cross(f, r).normalized;
                for (int k = 0; k < m; k++) cur[k] = path[i] + r * section[k].x + u * section[k].y;
                if (i > 0)
                    for (int k = 0; k < m; k++)
                    {
                        int k2 = (k + 1) % m;
                        Quad(prev[k], prev[k2], cur[k2], cur[k], ramp, salt);
                    }
                var t = prev; prev = cur; cur = t;
            }
        }

        /// <summary>Merge another kit (same origin), offset in game space.</summary>
        public void Append(RailKit k)
        {
            Merge(mesh, k.mesh); Merge(glow, k.glow); Merge(solid, k.solid);
        }

        static void Merge(VoxelMesher.MeshData into, VoxelMesher.MeshData from)
        {
            int k = into.verts.Count;
            into.verts.AddRange(from.verts); into.norms.AddRange(from.norms); into.uv3.AddRange(from.uv3); into.cols.AddRange(from.cols);
            foreach (var t in from.tris) into.tris.Add(t + k);
        }

        // ------------------------------------------------------------------ palette shorthands (Pal ramps)
        public static readonly Color32[] Steel = Pal.Metal, Iron = Pal.Black, Timber = Pal.Wood, Paint = Pal.Cream, Lens = Pal.Glass;
        public static readonly Color32[] Concrete = { HDShapesTone(Pal.Cream[0], 0.62f), HDShapesTone(Pal.Cream[0], 0.7f), HDShapesTone(Pal.Cream[1], 0.62f), HDShapesTone(Pal.Cream[1], 0.68f) };
        public static readonly Color32[] Ballast = { HDShapesTone(Pal.Fur[1], 1f), HDShapesTone(Pal.Fur[2], 0.9f), HDShapesTone(Pal.Fur[2], 1f), HDShapesTone(Pal.Fur[1], 1.1f) };
        public static readonly Color32[] RailHead = { Pal.Chrome[1], Pal.Chrome[1], Pal.Chrome[2] };
        public static readonly Color32[] Red = { Pal.Crimson[2], Pal.Crimson[3] }, Green = { Pal.Moss[3], Pal.Moss[4] }, Amber = { Pal.Ochre[3], Pal.Ochre[4] };
        public static readonly Color32[] Warn = { Pal.Ochre[3], Pal.Ochre[4], Pal.Ochre[3] }, Lamp = { Pal.LightY, Pal.LightW };
        static Color32 HDShapesTone(Color32 c, float f) => new Color32((byte)Mathf.Min(255, c.r * f), (byte)Mathf.Min(255, c.g * f), (byte)Mathf.Min(255, c.b * f), 255);
    }
}
