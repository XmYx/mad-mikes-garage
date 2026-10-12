using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>City materials (vertex colour ramps + weathering): the palette of the Blender city generator.</summary>
    public enum CM
    {
        Concrete, ConcreteDark, Panel, Plaster, Floor, Tiles, Wallpaper, Wood, Fabric, Rug, Door, Frame, Glass, Window,
        WindowLit, Lamp, LampOff, Steel, SteelDark, Rust, RustDark, DeckPlate, Corrugated, Awning, Pot, Foliage, Dish,
        Pipe, StackRed, StackWhite, Screen, Beacon, BeaconGreen, Track, Machinery, HullWhite, HullOrange, Tower, TowerB,
        Tarmac, PaintYellow, Rubber, LampCyan, StripCyan, LampAmber, Radiator, Switch, Canvas, Cloth, Ground, Count
    }

    /// <summary>Mesh kit for the generated cities (port of the Blender generator's accumulator): boxes, spans, beams,
    /// tubes, lathes, rings, discs, lofts, wing plates, cables, ladders and railings. Geometry is authored in the
    /// generator's own axes (Blender: x along the length, y across, z up — see <see cref="P"/>) so the Python layout
    /// ports line for line, and is written in deck-local game axes (+X right, +Y up, +Z forward). Output: the visible
    /// mesh (vertex colours, weathered per tile), an emissive mesh (lamps, lit windows: an unlit material) and a
    /// collision mesh of everything flagged solid. Pure C#: safe on worker threads.</summary>
    public sealed class CityKit
    {
        public readonly VoxelMesher.MeshData mesh = new VoxelMesher.MeshData();
        public readonly VoxelMesher.MeshData glow = new VoxelMesher.MeshData();
        public readonly VoxelMesher.MeshData solid = new VoxelMesher.MeshData();
        /// <summary>Generator-space point that becomes the game origin (the deck centre at deck-top height).</summary>
        public Vector3 origin;
        public int seed;
        /// <summary>Night lamps (game-space position, colour, range), lit within the <see cref="LightBudget"/>.</summary>
        public readonly List<(Vector3 at, Color color, float range)> lamps = new List<(Vector3, Color, float)>();

        public CityKit(Vector3 origin, int seed = 0) { this.origin = origin; this.seed = seed; }

        public int Triangles => (mesh.tris.Count + glow.tris.Count) / 3;

        // ------------------------------------------------------------------ palette
        static readonly Color32[][] Ramps = new Color32[(int)CM.Count][];
        static readonly float[] RustOf = new float[(int)CM.Count];
        static readonly bool[] Emits = new bool[(int)CM.Count];
        static CityKit()
        {
            void Def(CM m, float rust, params string[] hex)
            {
                var r = new Color32[hex.Length];
                for (int i = 0; i < hex.Length; i++) r[i] = Pal.Hex(hex[i]);
                Ramps[(int)m] = r; RustOf[(int)m] = rust;
            }
            Def(CM.Concrete, 0.12f, "7c766a", "8a8476", "989082", "a49c8c");
            Def(CM.ConcreteDark, 0.15f, "4e4a44", "5a5650", "66615a", "726c64");
            Def(CM.Panel, 0.1f, "8c8576", "9a9384", "a8a090", "b4ac9a");
            Def(CM.Plaster, 0f, "b8b0a0", "c4bcac", "cec6b6");
            Def(CM.Floor, 0f, "5a3a24", "64432a", "6e4c30");
            Def(CM.Tiles, 0f, "c8c8c0", "d4d4cc", "dedcd4");
            Def(CM.Wallpaper, 0f, "a08c6c", "ac9878", "b8a484");
            Def(CM.Wood, 0.05f, "56361e", "6a4428", "7c5232", "8e603a");
            Def(CM.Fabric, 0f, "6a3028", "7c3a30", "8c4438");
            Def(CM.Rug, 0f, "5a2420", "6a2c26", "7a342c");
            Def(CM.Door, 0f, "4a3020", "543826", "5e402c");
            Def(CM.Frame, 0.05f, "a8a496", "b8b4a6", "c6c2b4");
            Def(CM.Glass, 0f, "2c3c4c", "34485a", "3e5468");
            Def(CM.Window, 0f, "141a22", "1a222c", "222c38");
            Def(CM.WindowLit, 0f, "e8a050", "f0b060", "f8c070");
            Def(CM.Lamp, 0f, "ffd890", "ffe0a0", "ffe8b0");
            Def(CM.LampOff, 0f, "6a6450", "767060");
            Def(CM.Steel, 0.2f, "4a4440", "56504a", "625a54", "6e665e");
            Def(CM.SteelDark, 0.15f, "24211f", "2c2926", "34302c");
            Def(CM.Rust, 0.5f, "5e2c15", "6e3418", "80401d", "92492a");
            Def(CM.RustDark, 0.3f, "3a1e12", "442416", "4e2a1a", "58301e");
            Def(CM.DeckPlate, 0.35f, "443e38", "4e4740", "585048", "625a50");
            Def(CM.Corrugated, 0.45f, "6a645a", "766e62", "82786a", "8e8474");
            Def(CM.Awning, 0.1f, "2a5a5c", "306466", "386e70");
            Def(CM.Pot, 0.1f, "8c4a2c", "9a5432", "a85e38");
            Def(CM.Foliage, 0f, "20381a", "2a4620", "345428", "40642e");
            Def(CM.Dish, 0.1f, "a8a8a0", "b4b4ac", "c0c0b8");
            Def(CM.Pipe, 0.35f, "5a4c3e", "665646", "72604e");
            Def(CM.StackRed, 0.3f, "7c2a18", "8c321c", "9c3a22");
            Def(CM.StackWhite, 0.25f, "b4ae9e", "c2bcac", "d0cab8");
            Def(CM.Screen, 0f, "58e098", "68e8a8");
            Def(CM.Beacon, 0f, "ff2810", "ff3818");
            Def(CM.BeaconGreen, 0f, "18ff48", "28ff58");
            Def(CM.Track, 0.2f, "181614", "201d1a", "282420");
            Def(CM.Machinery, 0.3f, "4e4e4a", "5a5a56", "666660", "72726a");
            Def(CM.HullWhite, 0.3f, "a8a294", "b6b0a0", "c4beae", "d0caba");
            Def(CM.HullOrange, 0.25f, "a84a1c", "b85420", "c86028", "d46c30");
            Def(CM.Tower, 0.45f, "56646a", "627076", "6e7c82", "7a888c");
            Def(CM.TowerB, 0.3f, "5e6260", "6a6e6a", "767a74", "82867e");
            Def(CM.Tarmac, 0.05f, "54524e", "5e5c58", "686662", "72706a");
            Def(CM.PaintYellow, 0.15f, "c8941c", "d4a024", "e0ac2c");
            Def(CM.Rubber, 0f, "0e0e0e", "141414", "1a1a1a");
            Def(CM.LampCyan, 0f, "60d8ff", "70e0ff");
            Def(CM.StripCyan, 0f, "40b8f0", "50c4f8");
            Def(CM.LampAmber, 0f, "ff9030", "ffa040");
            Def(CM.Radiator, 0.05f, "c0bcb0", "ccc8bc");
            Def(CM.Switch, 0f, "d0ccc0", "dcd8cc");
            Def(CM.Canvas, 0.05f, "8a7c5c", "968866", "a29470");
            Def(CM.Cloth, 0f, "a0a8b0", "8c5848", "c4b890", "6a7c5a");
            Def(CM.Ground, 0f, "6e5034", "7a5a3a", "866440");
            foreach (var m in new[] { CM.WindowLit, CM.Lamp, CM.Screen, CM.Beacon, CM.BeaconGreen, CM.LampCyan, CM.StripCyan, CM.LampAmber }) Emits[(int)m] = true;
        }

        public static bool Glows(CM m) => Emits[(int)m];

        /// <summary>Weathered colour of material m at a game-space point: ramp pick by hash, broad rust blotches and
        /// rain streaks (vertical runs) on rusting materials.</summary>
        Color32 Colour(CM m, Vector3 p, Vector3 n)
        {
            var ramp = Ramps[(int)m];
            int seedM = seed * 31 + (int)m * 977;
            var cell = new Vector3Int(Mathf.FloorToInt(p.x * 0.7f), Mathf.FloorToInt(p.y * 0.7f), Mathf.FloorToInt(p.z * 0.7f));
            var c = ramp[Mathf.Clamp((int)(Pal.Hash(cell, seedM) * ramp.Length), 0, ramp.Length - 1)];
            float rust = RustOf[(int)m];
            if (rust <= 0f) return c;
            float blot = Pal.Noise(p * 0.11f, seedM + 7);
            float streak = Mathf.Abs(n.y) < 0.5f ? Pal.Noise(new Vector3((p.x + p.z) * 1.7f, p.y * 0.08f, 0f), seedM + 11) : 0f;
            float r = Mathf.Clamp01((blot - (1f - rust * 0.9f)) * 3f + (streak - 0.72f) * 2.2f * rust);
            if (r <= 0f) return c;
            var rc = Pal.Rust[Mathf.Clamp((int)(Pal.Hash(cell, seedM + 3) * 4f), 0, 4)];
            return Color32.Lerp(c, rc, Mathf.Min(0.85f, r));
        }

        // ------------------------------------------------------------------ mapping
        /// <summary>Generator space (bx along, by across, bz up) → deck-local game space.</summary>
        public Vector3 P(float bx, float by, float bz) => new Vector3(-(by - origin.y), bz - origin.z, bx - origin.x);
        public Vector3 P(Vector3 b) => P(b.x, b.y, b.z);

        // A polygon in generator space, counter-clockwise around its outward normal; after the reflection into game axes
        // its edges turn the other way, so the triangles are emitted reversed (Emit). With `inside` given, the winding is fixed to face away from it. Large
        // faces are split into ~3 m tiles so the weathering varies across them.
        void Quad(CM m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool isSolid, bool checkInside = false, Vector3 inside = default)
        {
            if (checkInside)
            {
                var nb = Vector3.Cross(b - a, c - b);
                if (Vector3.Dot(nb, (a + c) * 0.5f - inside) < 0f) { var t = b; b = d; d = t; }
            }
            Vector3 ga = P(a), gb = P(b), gc = P(c), gd = P(d);
            var n = Vector3.Cross(gb - ga, gc - gb);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(gc - gb, gd - gc);
            if (n.sqrMagnitude < 1e-14f) return;
            n = -n.normalized;
            if (isSolid) Emit(solid, ga, gb, gc, gd, n, default);
            bool emits = Emits[(int)m];
            var target = emits ? glow : mesh;
            float lu = (gb - ga).magnitude, lv = (gd - ga).magnitude;
            int nu = Mathf.Clamp(Mathf.CeilToInt(lu / 2f), 1, 32), nv = Mathf.Clamp(Mathf.CeilToInt(lv / 2f), 1, 32);
            if (emits)
            {
                Emit(target, ga, gb, gc, gd, n, Colour(m, (ga + gc) * 0.5f, n));
                return;
            }
            if (nu == 1 && nv == 1 && lu < 1.2f && lv < 1.2f)                                 // small faces: one colour
            {
                Emit(target, ga, gb, gc, gd, n, Colour(m, (ga + gc) * 0.5f, n));
                return;
            }
            // larger faces: colours per vertex (shared corners match) so blotches and streaks blend across the tiles
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                float u0 = i / (float)nu, u1 = (i + 1) / (float)nu, v0 = j / (float)nv, v1 = (j + 1) / (float)nv;
                Vector3 p00 = Bil(ga, gb, gc, gd, u0, v0), p10 = Bil(ga, gb, gc, gd, u1, v0), p11 = Bil(ga, gb, gc, gd, u1, v1), p01 = Bil(ga, gb, gc, gd, u0, v1);
                Emit4(target, p00, p10, p11, p01, n, Colour(m, p00, n), Colour(m, p10, n), Colour(m, p11, n), Colour(m, p01, n));
            }
        }

        static Vector3 Bil(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u, float v) => Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(d, c, u), v);

        void Tri(CM m, Vector3 a, Vector3 b, Vector3 c, bool isSolid)
        {
            Vector3 ga = P(a), gb = P(b), gc = P(c);
            var cr = Vector3.Cross(gb - ga, gc - gb);
            if (cr.sqrMagnitude < 1e-14f) return;
            var n = -cr.normalized;
            var target = Emits[(int)m] ? glow : mesh;
            Emit3(target, ga, gb, gc, n, Colour(m, (ga + gb + gc) / 3f, n));
            if (isSolid) Emit3(solid, ga, gb, gc, n, default);
        }

        static void Emit3(VoxelMesher.MeshData md, Vector3 a, Vector3 b, Vector3 c, Vector3 n, Color32 col)
        {
            int k = md.verts.Count;
            md.verts.Add(a); md.verts.Add(b); md.verts.Add(c);
            for (int i = 0; i < 3; i++) { md.norms.Add(n); md.uv3.Add(n); md.cols.Add(col); }
            md.tris.Add(k); md.tris.Add(k + 2); md.tris.Add(k + 1);
        }

        static void Emit4(VoxelMesher.MeshData md, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int k = md.verts.Count;
            md.verts.Add(a); md.verts.Add(b); md.verts.Add(c); md.verts.Add(d);
            for (int i = 0; i < 4; i++) { md.norms.Add(n); md.uv3.Add(n); }
            md.cols.Add(ca); md.cols.Add(cb); md.cols.Add(cc); md.cols.Add(cd);
            md.tris.Add(k); md.tris.Add(k + 2); md.tris.Add(k + 1);
            md.tris.Add(k); md.tris.Add(k + 3); md.tris.Add(k + 2);
        }

        static void Emit(VoxelMesher.MeshData md, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color32 col)
        {
            int k = md.verts.Count;
            md.verts.Add(a); md.verts.Add(b); md.verts.Add(c); md.verts.Add(d);
            for (int i = 0; i < 4; i++) { md.norms.Add(n); md.uv3.Add(n); md.cols.Add(col); }
            md.tris.Add(k); md.tris.Add(k + 2); md.tris.Add(k + 1);                 // Unity front face: Cross(b - a, c - b) = +n
            md.tris.Add(k); md.tris.Add(k + 3); md.tris.Add(k + 2);
        }

        // ------------------------------------------------------------------ primitives (generator space)
        // corners: 0 -x-y-z, 1 +x-y-z, 2 +x+y-z, 3 -x+y-z, 4..7 the same at +z (right-handed); faces CCW outward
        static readonly int[][] Faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 1, 2, 6, 5 }, new[] { 3, 0, 4, 7 } };
        readonly Vector3[] corner = new Vector3[8];

        void Hexa(CM m, bool isSolid)
        {
            foreach (var f in Faces) Quad(m, corner[f[0]], corner[f[1]], corner[f[2]], corner[f[3]], isSolid);
        }

        /// <summary>Oriented box: size along its local axes, centre, rotation (Blender XYZ Euler, radians).</summary>
        public void Box(CM m, Vector3 size, Vector3 loc, Vector3 euler = default, bool isSolid = false)
        {
            var R = BlenderEuler(euler);
            var h = size * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                var l = new Vector3((i == 1 || i == 2 || i == 5 || i == 6) ? h.x : -h.x, (i == 2 || i == 3 || i == 6 || i == 7) ? h.y : -h.y, i >= 4 ? h.z : -h.z);
                corner[i] = loc + R.MultiplyVector(l);
            }
            Hexa(m, isSolid);
        }

        public void Box(CM m, float sx, float sy, float sz, float x, float y, float z, bool isSolid = false) => Box(m, new Vector3(sx, sy, sz), new Vector3(x, y, z), default, isSolid);

        /// <summary>Axis-aligned box between bounds (solid by default: walls, slabs, decks).</summary>
        public void Span(CM m, float x0, float x1, float y0, float y1, float z0, float z1, bool isSolid = true)
        {
            if (x1 < x0) { var t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { var t = y0; y0 = y1; y1 = t; }
            if (x1 - x0 <= 1e-3f || y1 - y0 <= 1e-3f || z1 - z0 <= 1e-3f) return;
            corner[0] = new Vector3(x0, y0, z0); corner[1] = new Vector3(x1, y0, z0); corner[2] = new Vector3(x1, y1, z0); corner[3] = new Vector3(x0, y1, z0);
            corner[4] = new Vector3(x0, y0, z1); corner[5] = new Vector3(x1, y0, z1); corner[6] = new Vector3(x1, y1, z1); corner[7] = new Vector3(x0, y1, z1);
            Hexa(m, isSolid);
        }

        /// <summary>Square beam from p to q, cross-section t × t2 (mathutils to_track_quat("Z", "Y") frame).</summary>
        public void Beam(CM m, Vector3 p, Vector3 q, float t, float t2 = -1f, bool isSolid = false)
        {
            var d = q - p;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            if (t2 < 0f) t2 = t;
            var a = d / len;
            Frame(a, out var u, out var w);
            var c = (p + q) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                float x = (i == 1 || i == 2 || i == 5 || i == 6) ? t * 0.5f : -t * 0.5f;
                float y = (i == 2 || i == 3 || i == 6 || i == 7) ? t2 * 0.5f : -t2 * 0.5f;
                float z = i >= 4 ? len * 0.5f : -len * 0.5f;
                corner[i] = c + u * x + w * y + a * z;
            }
            Hexa(m, isSolid);
        }

        /// <summary>Capped cylinder p → q.</summary>
        public void Tube(CM m, Vector3 p, Vector3 q, float r, int seg = 12, bool isSolid = false)
        {
            var d = q - p;
            if (d.magnitude < 1e-3f) return;
            Lathe(m, p, d, new[] { new Vector2(r, 0f), new Vector2(r, d.magnitude) }, seg, true, isSolid);
        }

        /// <summary>Revolve [(radius, distance along axis)] around axis from base; capped where the end radius > 0.</summary>
        public void Lathe(CM m, Vector3 b, Vector3 axis, IList<Vector2> prof, int seg = 16, bool cap = true, bool isSolid = false)
        {
            var a = axis.normalized;
            Frame(a, out var u, out var w);
            bool rev = prof[prof.Count - 1].y < prof[0].y;           // profile running back along the axis: flip windings
            var ring = new Vector3[prof.Count, seg];
            for (int j = 0; j < prof.Count; j++)
                for (int k = 0; k < seg; k++)
                {
                    float t = 2f * Mathf.PI * k / seg;
                    ring[j, k] = b + a * prof[j].y + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * prof[j].x;
                }
            for (int j = 0; j < prof.Count - 1; j++)
                for (int k = 0; k < seg; k++)
                {
                    int k2 = (k + 1) % seg;
                    if (rev) Quad(m, ring[j, k], ring[j + 1, k], ring[j + 1, k2], ring[j, k2], isSolid);
                    else Quad(m, ring[j, k], ring[j, k2], ring[j + 1, k2], ring[j + 1, k], isSolid);
                }
            if (!cap) return;
            int last = prof.Count - 1;
            int lo = rev ? last : 0, hi = rev ? 0 : last;
            if (prof[lo].x > 1e-4f)
            {
                var c0 = b + a * prof[lo].y;
                for (int k = 0; k < seg; k++) Tri(m, c0, ring[lo, (k + 1) % seg], ring[lo, k], isSolid);
            }
            if (prof[hi].x > 1e-4f)
            {
                var c1 = b + a * prof[hi].y;
                for (int k = 0; k < seg; k++) Tri(m, c1, ring[hi, k], ring[hi, (k + 1) % seg], isSolid);
            }
        }

        /// <summary>Hollow duct: outer and inner walls and both lips.</summary>
        public void Ring(CM m, Vector3 c, Vector3 axis, float rOut, float rIn, float length, int seg = 28)
        {
            var a = axis.normalized;
            Frame(a, out var u, out var w);
            var c0 = c - a * length * 0.5f;
            var pr = new[] { new Vector2(rOut, 0f), new Vector2(rOut, length), new Vector2(rIn, length), new Vector2(rIn, 0f) };
            for (int j = 0; j < 4; j++)
            {
                Vector2 p0 = pr[j], p1 = pr[(j + 1) % 4];
                for (int k = 0; k < seg; k++)
                {
                    float t0 = 2f * Mathf.PI * k / seg, t1 = 2f * Mathf.PI * (k + 1) / seg;
                    Vector3 e0 = u * Mathf.Cos(t0) + w * Mathf.Sin(t0), e1 = u * Mathf.Cos(t1) + w * Mathf.Sin(t1);
                    Quad(m, c0 + a * p0.y + e0 * p0.x, c0 + a * p0.y + e1 * p0.x, c0 + a * p1.y + e1 * p1.x, c0 + a * p1.y + e0 * p1.x, false);
                }
            }
        }

        public void Disc(CM m, Vector3 c, Vector3 axis, float r, int seg = 28, float t = 0.02f)
        {
            var a = axis.normalized;
            Lathe(m, c - a * t * 0.5f, a, new[] { new Vector2(r, 0f), new Vector2(r, t) }, seg, true);
        }

        /// <summary>Hull along +x through sections (x, half width, half height, z centre) with corner radius fractions.</summary>
        public void Loft(CM m, IList<Vector4> secs, IList<float> rf, int n = 3, bool cap = true)
        {
            int count = secs.Count, per = 4 * (n + 1);
            var rings = new Vector3[count, per];
            for (int s = 0; s < count; s++)
            {
                float x = secs[s].x, hw = secs[s].y, hh = secs[s].z, z = secs[s].w;
                float r = Mathf.Max(1e-3f, Mathf.Min(hw, hh) * rf[s]);
                int i = 0;
                var cs = new[] { new Vector3(hw - r, hh - r, 0f), new Vector3(-(hw - r), hh - r, 90f), new Vector3(-(hw - r), -(hh - r), 180f), new Vector3(hw - r, -(hh - r), 270f) };
                foreach (var cc in cs)
                    for (int k = 0; k <= n; k++)
                    {
                        float t = (cc.z + 90f * k / n) * Mathf.Deg2Rad;
                        rings[s, i++] = new Vector3(x, cc.x + r * Mathf.Cos(t), z + cc.y + r * Mathf.Sin(t));
                    }
            }
            for (int s = 0; s < count - 1; s++)
            {
                var axisPt = new Vector3((secs[s].x + secs[s + 1].x) * 0.5f, 0f, (secs[s].w + secs[s + 1].w) * 0.5f);
                for (int k = 0; k < per; k++)
                    Quad(m, rings[s, k], rings[s, (k + 1) % per], rings[s + 1, (k + 1) % per], rings[s + 1, k], false, true, axisPt);
            }
            if (!cap) return;
            for (int e = 0; e < 2; e++)
            {
                int s = e == 0 ? 0 : count - 1;
                var c = Vector3.zero;
                for (int k = 0; k < per; k++) c += rings[s, k];
                c /= per;
                for (int k = 0; k < per; k++)
                {
                    if (e == 0) Tri(m, c, rings[s, k], rings[s, (k + 1) % per], false);
                    else Tri(m, c, rings[s, (k + 1) % per], rings[s, k], false);
                }
            }
        }

        public void Loft(CM m, IList<Vector4> secs, float rf, int n = 3, bool cap = true)
        {
            var r = new float[secs.Count];
            for (int i = 0; i < r.Length; i++) r[i] = rf;
            Loft(m, secs, r, n, cap);
        }

        /// <summary>Tapered plate (wing, fin): leading edges at root / tip, chord along -x, thickness across the span.</summary>
        public void Wing(CM m, Vector3 root, float rootChord, Vector3 tip, float tipChord, float tRoot, float tTip = -1f)
        {
            if (tTip < 0f) tTip = tRoot;
            var span = tip - root;
            var th = Vector3.Cross(Vector3.right, span).normalized;
            if (th.sqrMagnitude < 0.5f) th = Vector3.forward;
            if (th.z < 0f) th = -th;
            var p = new Vector3[12];
            int i = 0;
            var c = Vector3.zero;
            foreach (var (le, ch, t) in new[] { (root, rootChord, tRoot), (tip, tipChord, tTip) })
            {
                p[i++] = le + th * t * 0.5f; p[i++] = le + new Vector3(-ch * 0.3f, 0, 0) + th * t * 0.5f;
                p[i++] = le + new Vector3(-ch, 0, 0) + th * t * 0.08f; p[i++] = le + new Vector3(-ch, 0, 0) - th * t * 0.08f;
                p[i++] = le + new Vector3(-ch * 0.3f, 0, 0) - th * t * 0.5f; p[i++] = le - th * t * 0.5f;
            }
            foreach (var q in p) c += q;
            c /= 12f;
            for (int k = 0; k < 6; k++)
            {
                int k2 = (k + 1) % 6;
                Quad(m, p[k], p[k2], p[6 + k2], p[6 + k], false, true, c);
            }
            for (int e = 0; e < 2; e++)
            {
                int o = e * 6;
                var cc = Vector3.zero;
                for (int k = 0; k < 6; k++) cc += p[o + k];
                cc /= 6f;
                for (int k = 0; k < 6; k++)
                {
                    Vector3 a = p[o + k], b = p[o + (k + 1) % 6];
                    var nrm = Vector3.Cross(a - cc, b - a);
                    if (Vector3.Dot(nrm, cc - c) < 0f) Tri(m, cc, b, a, false); else Tri(m, cc, a, b, false);
                }
            }
        }

        public void Cable(Vector3 p, Vector3 q, float sag, int n = 10, float t = 0.05f, CM m = CM.SteelDark)
        {
            var prev = p;
            for (int i = 1; i <= n; i++)
            {
                float f = i / (float)n;
                var c = Vector3.Lerp(p, q, f) - new Vector3(0, 0, sag * 4f * f * (1f - f));
                Beam(m, prev, c, t);
                prev = c;
            }
        }

        public void Ladder(Vector3 p, Vector3 q, float w = 0.5f, float step = 0.3f, CM m = CM.Steel)
        {
            var d = q - p;
            var side = Vector3.Cross(d, Vector3.forward);
            side = (side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right) * w * 0.5f;
            Beam(m, p - side, q - side, 0.05f); Beam(m, p + side, q + side, 0.05f);
            int n = (int)(d.magnitude / step);
            for (int i = 1; i < n; i++) { var c = p + d * (i * step / d.magnitude); Beam(m, c - side, c + side, 0.03f); }
        }

        /// <summary>Posts + top rail + mid rail (+ toe board) along a polyline of (x, y) at floor height z; gaps
        /// (x0, x1, y0, y1) stay open. Each run is also a thin collision wall so nobody walks off an edge.</summary>
        public void Railing(IList<Vector2> pts, float z, bool closed = false, float h = 1.1f, float spacing = 1.5f, IList<Vector4> gaps = null, bool toe = true)
        {
            int segs = closed ? pts.Count : pts.Count - 1;
            var up = new Vector3(0, 0, h);
            for (int s = 0; s < segs; s++)
            {
                var p = new Vector3(pts[s].x, pts[s].y, z);
                var q = new Vector3(pts[(s + 1) % pts.Count].x, pts[(s + 1) % pts.Count].y, z);
                int n = Mathf.Max(1, Mathf.CeilToInt((q - p).magnitude / spacing));
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = Vector3.Lerp(p, q, i / (float)n), b = Vector3.Lerp(p, q, (i + 1) / (float)n);
                    if (InGap(gaps, (a + b) * 0.5f)) continue;
                    Beam(CM.Steel, a, a + up, 0.06f);
                    if (i == n - 1 || InGap(gaps, Vector3.Lerp(p, q, (i + 1.5f) / n))) Beam(CM.Steel, b, b + up, 0.06f);
                    Beam(CM.Steel, a + up, b + up, 0.06f);
                    Beam(CM.Steel, a + up * 0.5f, b + up * 0.5f, 0.035f);
                    if (toe) Beam(CM.SteelDark, a + new Vector3(0, 0, 0.08f), b + new Vector3(0, 0, 0.08f), 0.02f, 0.15f);
                    SolidWall(a, b, z, z + h);
                }
            }
        }

        static bool InGap(IList<Vector4> gaps, Vector3 p)
        {
            if (gaps == null) return false;
            foreach (var g in gaps) if (g.x <= p.x && p.x <= g.y && g.z <= p.y && p.y <= g.w) return true;
            return false;
        }

        /// <summary>Invisible collision wall between two generator-space points (railings, parapets).</summary>
        public void SolidWall(Vector3 a, Vector3 b, float z0, float z1, float t = 0.08f)
        {
            var d = new Vector3(b.x - a.x, b.y - a.y, 0f);
            if (d.sqrMagnitude < 1e-6f) return;
            var nrm = new Vector3(-d.y, d.x, 0f).normalized * t * 0.5f;
            var cc = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, (z0 + z1) * 0.5f);
            var v = new[]
            {
                new Vector3(a.x, a.y, z0) - nrm, new Vector3(b.x, b.y, z0) - nrm, new Vector3(b.x, b.y, z0) + nrm, new Vector3(a.x, a.y, z0) + nrm,
                new Vector3(a.x, a.y, z1) - nrm, new Vector3(b.x, b.y, z1) - nrm, new Vector3(b.x, b.y, z1) + nrm, new Vector3(a.x, a.y, z1) + nrm,
            };
            foreach (var f in Faces)
            {
                Vector3 p0 = v[f[0]], p1 = v[f[1]], p2 = v[f[2]], p3 = v[f[3]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p1), (p0 + p2) * 0.5f - cc) < 0f) { var t2 = p1; p1 = p3; p3 = t2; }
                Vector3 ga = P(p0), gb = P(p1), gc = P(p2), gd = P(p3);
                var n = Vector3.Cross(gb - ga, gc - gb);
                if (n.sqrMagnitude < 1e-12f) continue;
                Emit(solid, ga, gb, gc, gd, -n.normalized, default);
            }
        }

        /// <summary>A night lamp at a generator-space point.</summary>
        public void LampAt(Vector3 b, Color color, float range = 12f) => lamps.Add((P(b), color, range));

        // ------------------------------------------------------------------ helpers
        /// <summary>Right-handed frame (u, w, a): w as close to +z (up) as possible — mathutils to_track_quat("Z", "Y")
        /// keeps local Y nearest world Y, which for the generator's beams is the same up side.</summary>
        static void Frame(Vector3 a, out Vector3 u, out Vector3 w)
        {
            var up = Mathf.Abs(a.y) > 0.999f ? Vector3.forward : Vector3.up;
            w = (up - a * Vector3.Dot(up, a)).normalized;
            u = Vector3.Cross(w, a).normalized;
        }

        /// <summary>Blender's XYZ Euler (right-handed): R = Rz · Ry · Rx.</summary>
        static Matrix4x4 BlenderEuler(Vector3 e)
        {
            if (e == Vector3.zero) return Matrix4x4.identity;
            float cx = Mathf.Cos(e.x), sx = Mathf.Sin(e.x), cy = Mathf.Cos(e.y), sy = Mathf.Sin(e.y), cz = Mathf.Cos(e.z), sz = Mathf.Sin(e.z);
            var m = Matrix4x4.identity;
            m.m00 = cy * cz; m.m01 = sx * sy * cz - cx * sz; m.m02 = cx * sy * cz + sx * sz;
            m.m10 = cy * sz; m.m11 = sx * sy * sz + cx * cz; m.m12 = cx * sy * sz - sx * cz;
            m.m20 = -sy; m.m21 = sx * cy; m.m22 = cx * cy;
            return m;
        }

        /// <summary>Append another kit's meshes, offset in game space.</summary>
        public void Append(CityKit k, Vector3 offset)
        {
            Merge(mesh, k.mesh, offset); Merge(glow, k.glow, offset); Merge(solid, k.solid, offset);
            foreach (var l in k.lamps) lamps.Add((l.at + offset, l.color, l.range));
        }

        static void Merge(VoxelMesher.MeshData into, VoxelMesher.MeshData from, Vector3 offset)
        {
            int k = into.verts.Count;
            foreach (var v in from.verts) into.verts.Add(v + offset);
            into.norms.AddRange(from.norms); into.uv3.AddRange(from.uv3); into.cols.AddRange(from.cols);
            foreach (var t in from.tris) into.tris.Add(t + k);
        }
    }
}
