using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>Small procedural HD meshes for what is still built at runtime (crops, overgrowth, debris, bulbs, levers,
    /// shards, pickups, armour plates ...): smooth low-poly ellipsoids, tapered tubes, leaves, bevelled boxes and plates
    /// with vertex colours (sRGB, HDLit linearises) and vertex alpha = wind weight (255 rooted, as the voxel meshes).
    /// Draw them with <see cref="Solid"/> (one-sided) or <see cref="Foliage"/> (two-sided).</summary>
    public sealed class HDShapes
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<Color32> c = new List<Color32>();
        public readonly List<int> t = new List<int>();
        /// <summary>Wind weight reference height: vertex alpha falls with the height above it (null = all rooted).</summary>
        public float? swayBase;
        public float swayScale = 520f;

        static Material solid, foliage;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { solid = foliage = null; }

        /// <summary>HDLit with vertex colours (one-sided).</summary>
        public static Material Solid { get { if (!solid) solid = HDModel.VoxelMaterial; return solid; } }

        /// <summary>HDLit with vertex colours, two-sided, wind tips (leaves, petals, vines).</summary>
        public static Material Foliage
        {
            get
            {
                if (foliage) return foliage;
                var src = HDModel.VoxelMaterial;
                if (!src) return null;
                foliage = new Material(src) { name = "HDFoliage" };
                foliage.SetFloat("_Cull", 0f);
                foliage.SetFloat("_SwayTip", 0.03f);
                foliage.SetFloat("_OutlinePx", 0f);
                return foliage;
            }
        }

        public void Clear() { v.Clear(); n.Clear(); c.Clear(); t.Clear(); }

        byte A(float y) => swayBase.HasValue ? (byte)Mathf.Clamp(255 - (y - swayBase.Value) * swayScale, 20, 255) : (byte)255;

        public static Color32 Tone(Color32 col, float f) => new Color32((byte)Mathf.Min(255, col.r * f), (byte)Mathf.Min(255, col.g * f), (byte)Mathf.Min(255, col.b * f), 255);

        Color32 Col(Color32 col, float f, float y) { var k = Tone(col, f); k.a = A(y); return k; }

        /// <summary>Low-poly ellipsoid (or its upper half), darker underneath.</summary>
        public void Ellipsoid(Vector3 o, Vector3 r, Color32 col, int seg = 8, int rings = 5, bool half = false, Quaternion? rot = null)
        {
            var q = rot ?? Quaternion.identity;
            int b = v.Count, r0 = half ? rings / 2 : 0;
            for (int i = r0; i <= rings; i++)
            {
                float vv = i / (float)rings, phi = (vv - 0.5f) * Mathf.PI;
                float cy = Mathf.Sin(phi), cr = Mathf.Cos(phi);
                for (int k = 0; k <= seg; k++)
                {
                    float th = k * Mathf.PI * 2f / seg;
                    var d = new Vector3(Mathf.Cos(th) * cr, cy, Mathf.Sin(th) * cr);
                    var p = o + q * Vector3.Scale(d, r);
                    v.Add(p); n.Add(q * new Vector3(d.x / r.x, d.y / r.y, d.z / r.z).normalized);
                    c.Add(Col(col, 0.7f + 0.4f * vv, p.y));
                }
            }
            int cols = seg + 1;
            for (int i = 0; i < rings - r0; i++)
                for (int k = 0; k < seg; k++)
                {
                    int a = b + i * cols + k, d = a + cols;
                    t.Add(a); t.Add(d); t.Add(d + 1); t.Add(a); t.Add(d + 1); t.Add(a + 1);
                }
        }

        /// <summary>Tapered tube with optional end caps.</summary>
        public void Tube(Vector3 a, Vector3 bTop, float ra, float rb, Color32 col, int seg = 6, bool caps = false)
        {
            var ax = (bTop - a).normalized;
            var u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(ax, u);
            int b = v.Count;
            for (int k = 0; k <= seg; k++)
            {
                float th = k * Mathf.PI * 2f / seg;
                var d = u * Mathf.Cos(th) + w * Mathf.Sin(th);
                v.Add(a + d * ra); n.Add(d); c.Add(Col(col, 0.8f, a.y));
                v.Add(bTop + d * rb); n.Add(d); c.Add(Col(col, 1.05f, bTop.y));
            }
            for (int k = 0; k < seg; k++)
            {
                int i0 = b + k * 2;
                t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 3); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2);
            }
            if (!caps) return;
            Disc(bTop, ax, rb, col, seg);
            Disc(a, -ax, ra, col, seg);
        }

        public void Disc(Vector3 o, Vector3 normal, float r, Color32 col, int seg = 8)
        {
            var u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(normal, u);
            int b = v.Count;
            v.Add(o); n.Add(normal); c.Add(Col(col, 1f, o.y));
            for (int k = 0; k <= seg; k++)
            {
                float th = -k * Mathf.PI * 2f / seg;
                var p = o + (u * Mathf.Cos(th) + w * Mathf.Sin(th)) * r;
                v.Add(p); n.Add(normal); c.Add(Col(col, 0.95f, p.y));
            }
            for (int k = 0; k < seg; k++) { t.Add(b); t.Add(b + 1 + k); t.Add(b + 2 + k); }
        }

        /// <summary>Leaf / petal / frond: a diamond from the root through its widest point to the tip; <paramref name="droop"/>
        /// bends the tip half down. Two-sided material.</summary>
        public void Leaf(Vector3 root, Vector3 tip, float width, Color32 col, float droop = 0f, Vector3? sideHint = null, Vector3? faceOut = null)
        {
            var d = tip - root;
            var side = sideHint ?? Vector3.Cross(Vector3.up, d);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            side.Normalize();
            var mid = root + d * 0.45f + Vector3.up * (droop * d.magnitude * 0.25f);
            var tp = tip - Vector3.up * (droop * d.magnitude * 0.35f);
            var nn = Vector3.Cross(d, side).normalized;
            if (faceOut.HasValue) { if (Vector3.Dot(nn, faceOut.Value) < 0f) nn = -nn; nn = (nn * 0.7f + faceOut.Value * 0.3f).normalized; }
            else { if (nn.y < 0f) nn = -nn; nn = (nn * 0.6f + Vector3.up * 0.4f).normalized; }
            int b = v.Count;
            v.Add(root); v.Add(mid - side * width * 0.5f); v.Add(tp); v.Add(mid + side * width * 0.5f);
            for (int k = 0; k < 4; k++) n.Add(nn);
            c.Add(Col(col, 0.78f, root.y)); c.Add(Col(col, 1f, mid.y)); c.Add(Col(col, 1.12f, tp.y)); c.Add(Col(col, 1f, mid.y));
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }

        /// <summary>A box with chamfered edges (bevel <paramref name="bevel"/> m): HD bricks, plates, chips, bulbs.</summary>
        public void Box(Vector3 o, Vector3 size, Color32 col, float bevel = 0.01f, Quaternion? rot = null)
        {
            var q = rot ?? Quaternion.identity;
            var h = size * 0.5f;
            bevel = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            var inner = h - Vector3.one * bevel;
            // six faces, each inset by the bevel, plus the chamfer strips between them
            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    var nrm = Vector3.zero; nrm[axis] = s;
                    int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                    var p = Vector3.zero; p[axis] = s * h[axis];
                    Vector3 Pt(float x, float y) { var r = p; r[a1] = x; r[a2] = y; return r; }
                    var c0 = Pt(-inner[a1], -inner[a2]); var c1 = Pt(inner[a1], -inner[a2]); var c2 = Pt(inner[a1], inner[a2]); var c3 = Pt(-inner[a1], inner[a2]);
                    Face(o, q, c0, c1, c2, c3, nrm, col, s > 0);
                    // chamfer towards +a1 and +a2 neighbours (each edge once: s on this axis, both signs on a1)
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                    {
                        var e0 = Pt(s1 * inner[a1], -inner[a2]); var e1 = Pt(s1 * inner[a1], inner[a2]);
                        var f0 = e0; f0[axis] = s * inner[axis]; f0[a1] = s1 * h[a1];
                        var f1 = e1; f1[axis] = s * inner[axis]; f1[a1] = s1 * h[a1];
                        var en = nrm; en[a1] = s1; en.Normalize();
                        Face(o, q, e0, e1, f1, f0, en, col, false);
                    }
                }
            if (bevel > 1e-4f) Corners(o, q, h, inner, col);
        }

        void Corners(Vector3 o, Quaternion q, Vector3 h, Vector3 inner, Color32 col)
        {
            for (int k = 0; k < 8; k++)
            {
                var sg = new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1);
                var a = Vector3.Scale(new Vector3(h.x, inner.y, inner.z), sg);
                var b = Vector3.Scale(new Vector3(inner.x, h.y, inner.z), sg);
                var cc = Vector3.Scale(new Vector3(inner.x, inner.y, h.z), sg);
                var wn = q * sg.normalized;
                int i = v.Count;
                foreach (var p in new[] { a, b, cc }) { var w = o + q * p; v.Add(w); n.Add(wn); c.Add(Col(col, 0.9f, w.y)); }
                if (Vector3.Dot(Vector3.Cross(q * (b - a), q * (cc - a)), wn) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
                else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            }
        }

        void Face(Vector3 o, Quaternion q, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 nrm, Color32 col, bool flip)
        {
            var wn = q * nrm;
            int i = v.Count;
            foreach (var p in new[] { a, b, cc, d }) { var w = o + q * p; v.Add(w); n.Add(wn); c.Add(Col(col, 0.85f + 0.2f * Mathf.Max(0f, wn.y), w.y)); }
            // wind each quad so its front faces along the normal
            var tn = Vector3.Cross(q * (b - a), q * (cc - a));
            if (Vector3.Dot(tn, wn) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        /// <summary>A thin flat plate (sheet metal, glass shard) with an optional ring of rivets.</summary>
        public void Plate(Vector3 o, Vector3 size, Color32 col, Quaternion rot, int rivets = 0, Color32? rivet = null)
        {
            Box(o, size, col, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f, rot);
            if (rivets <= 0) return;
            var rc = rivet ?? Tone(col, 1.25f);
            float inset = Mathf.Min(size.x, size.z) * 0.12f;
            for (int k = 0; k < rivets; k++)
            {
                float f = k / (float)rivets;
                float per = 2f * (size.x + size.z - 4f * inset), d = f * per;
                float x, z, hx = size.x * 0.5f - inset, hz = size.z * 0.5f - inset;
                if (d < 2f * hx) { x = -hx + d; z = -hz; }
                else if ((d -= 2f * hx) < 2f * hz) { x = hx; z = -hz + d; }
                else if ((d -= 2f * hz) < 2f * hx) { x = hx - d; z = hz; }
                else { d -= 2f * hx; x = -hx; z = hz - d; }
                Ellipsoid(o + rot * new Vector3(x, size.y * 0.5f, z), Vector3.one * Mathf.Max(0.004f, inset * 0.35f), rc, 5, 3, true, rot);
            }
        }

        public Mesh ToMesh(string name, Mesh into = null)
        {
            var m = into ? into : new Mesh();
            m.Clear();
            m.name = name;
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
