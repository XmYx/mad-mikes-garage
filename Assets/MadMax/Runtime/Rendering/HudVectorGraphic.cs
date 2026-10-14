using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MadMax.Rendering
{
    /// <summary>Draws one layer of a <see cref="PixelCanvas"/>'s <see cref="HudVector"/> shapes as an anti-aliased UI mesh
    /// (a one-pixel alpha feather on every edge) at the screen's own resolution, sibling of the pixel HUD image.</summary>
    public class HudVectorGraphic : MaskableGraphic
    {
        public PixelCanvas source;
        public bool frontLayer;

        // scratch (no allocations per frame)
        static readonly List<Vector2> path = new List<Vector2>(256), norm = new List<Vector2>(256);
        static readonly float[] offs = new float[4], alphas = new float[4];
        float ox, oy, sx, sy, feather;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || source.vec == null) return;
            var list = frontLayer ? source.vec.front : source.vec.back;
            var r = rectTransform.rect;
            sx = r.width / Mathf.Max(1, source.w); sy = r.height / Mathf.Max(1, source.h);
            ox = r.xMin; oy = r.yMax;
            feather = 1.05f / Mathf.Max(0.01f, sx);                                                 // one screen pixel, in HUD pixels
            var pts = source.vec.points;
            for (int i = 0; i < list.Count; i++)
            {
                if (vh.currentVertCount > 60000) return;
                var s = list[i];
                switch (s.kind)
                {
                    case HudVector.Kind.Rect:
                        RoundPath(s.x, s.y, s.w, s.h, s.r);
                        FillPath(vh, s.c, s.c2, s.t > 0.5f ? s.x : s.y, s.t > 0.5f ? s.w : s.h, feather, s.x + s.w * 0.5f, s.y + s.h * 0.5f, s.t > 0.5f);
                        break;
                    case HudVector.Kind.Shadow:
                    {
                        float blur = Mathf.Max(feather, s.t);
                        RoundPath(s.x, s.y, s.w, s.h, s.r);
                        FillPath(vh, s.c, s.c, s.y, s.h, Mathf.Min(blur, Mathf.Min(s.w, s.h) * 0.9f) + feather, s.x + s.w * 0.5f, s.y + s.h * 0.5f);
                        break;
                    }
                    case HudVector.Kind.Stroke:
                    {
                        float t = s.t;
                        RoundPath(s.x + t * 0.5f, s.y + t * 0.5f, s.w - t, s.h - t, Mathf.Max(0f, s.r - t * 0.5f));
                        StrokePath(vh, s.c, t, true);
                        break;
                    }
                    case HudVector.Kind.Line:
                    {
                        path.Clear(); norm.Clear();
                        var d = new Vector2(s.w - s.x, s.h - s.y);
                        if (d.sqrMagnitude < 1e-6f) break;
                        d.Normalize();
                        var n = new Vector2(-d.y, d.x);
                        float ext = feather * 0.35f;                                                // a hint of the feather at the ends
                        path.Add(new Vector2(s.x, s.y) - d * ext); path.Add(new Vector2(s.w, s.h) + d * ext);
                        norm.Add(n); norm.Add(n);
                        StrokePath(vh, s.c, s.t, false);
                        break;
                    }
                    case HudVector.Kind.Arc:
                    {
                        path.Clear(); norm.Clear();
                        float lenPx = Mathf.Abs(s.a1) * Mathf.Deg2Rad * s.r * sx;
                        int n = Mathf.Clamp(Mathf.CeilToInt(lenPx / 3f), 2, 160);
                        for (int k = 0; k <= n; k++)
                        {
                            float a = (s.a0 + s.a1 * k / n) * Mathf.Deg2Rad;
                            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                            path.Add(new Vector2(s.x + s.w * 0.5f, s.y + s.h * 0.5f) + dir * s.r);
                            norm.Add(dir);
                        }
                        StrokePath(vh, s.c, s.t, false);
                        break;
                    }
                    case HudVector.Kind.Poly:
                        PolyPath(pts, (int)s.a0, (int)s.a1, out float cx, out float cy);
                        FillPath(vh, s.c, s.c, 0f, 1f, feather, cx, cy);
                        break;
                }
            }
        }

        void Vert(VertexHelper vh, Vector2 p, Color32 c) => vh.AddVert(new Vector3(ox + p.x * sx, oy - p.y * sy, 0f), c, Vector4.zero);

        static Color32 Fade(Color32 c, float a) { c.a = (byte)Mathf.Clamp(c.a * a, 0f, 255f); return c; }

        /// <summary>A rounded rect's outline clockwise (y down) with outward normals; tiny radii keep a bevelled corner.</summary>
        void RoundPath(float x, float y, float w, float h, float r)
        {
            path.Clear(); norm.Clear();
            r = Mathf.Clamp(r, 0f, Mathf.Min(w, h) * 0.5f);
            int n = r <= 0.001f ? 1 : Mathf.Clamp(Mathf.CeilToInt(r * sx * 0.45f), 2, 14);
            Corner(x + w - r, y + r, r, 0f, n);
            Corner(x + w - r, y + h - r, r, 90f, n);
            Corner(x + r, y + h - r, r, 180f, n);
            Corner(x + r, y + r, r, 270f, n);
        }

        static void Corner(float cx, float cy, float r, float from, int n)
        {
            for (int k = 0; k <= n; k++)
            {
                float a = (from + 90f * k / n) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                path.Add(new Vector2(cx, cy) + d * r);
                norm.Add(d);
            }
        }

        /// <summary>A convex polygon from the shared point list: outward mitred normals.</summary>
        static void PolyPath(List<Vector2> pts, int start, int count, out float cx, out float cy)
        {
            path.Clear(); norm.Clear();
            float area = 0f; cx = 0f; cy = 0f;
            for (int i = 0; i < count; i++)
            {
                var a = pts[start + i]; var b = pts[start + (i + 1) % count];
                area += a.x * b.y - b.x * a.y;
                cx += a.x; cy += a.y;
            }
            cx /= count; cy /= count;
            float sign = area >= 0f ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                var p = pts[start + i]; var prev = pts[start + (i + count - 1) % count]; var next = pts[start + (i + 1) % count];
                var e0 = (p - prev).normalized; var e1 = (next - p).normalized;
                var n0 = new Vector2(e0.y, -e0.x) * sign; var n1 = new Vector2(e1.y, -e1.x) * sign;
                var m = (n0 + n1);
                if (m.sqrMagnitude < 1e-6f) m = n1;
                m.Normalize();
                float cos = Mathf.Max(0.35f, Vector2.Dot(m, n1));
                path.Add(p); norm.Add(m / cos);
            }
        }

        /// <summary>Fill the closed path: a fan from the centre to an inner ring, a feather out to transparent.</summary>
        void FillPath(VertexHelper vh, Color32 top, Color32 bottom, float y0, float h, float soft, float cx, float cy, bool across = false)
        {
            int n = path.Count;
            if (n < 3) return;
            bool grad = !top.Equals(bottom);
            int b = vh.currentVertCount;
            Vert(vh, new Vector2(cx, cy), grad ? Color32.Lerp(top, bottom, ((across ? cx : cy) - y0) / Mathf.Max(0.01f, h)) : top);
            for (int i = 0; i < n; i++)
            {
                var p = path[i]; var nn = norm[i];
                var c = grad ? Color32.Lerp(top, bottom, Mathf.Clamp01(((across ? p.x : p.y) - y0) / Mathf.Max(0.01f, h))) : top;
                Vert(vh, p - nn * (soft * 0.5f), c);
                Vert(vh, p + nn * (soft * 0.5f), Fade(c, 0f));
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int ii = b + 1 + i * 2, jj = b + 1 + j * 2;
                vh.AddTriangle(b, ii, jj);
                vh.AddTriangle(ii, ii + 1, jj + 1);
                vh.AddTriangle(ii, jj + 1, jj);
            }
        }

        /// <summary>A band of width <paramref name="t"/> centred on the path, feathered on both sides.</summary>
        void StrokePath(VertexHelper vh, Color32 c, float t, bool closed)
        {
            int n = path.Count;
            if (n < 2) return;
            int rows;
            float f = feather;
            if (t >= f)
            {
                rows = 4;
                offs[0] = -t * 0.5f - f * 0.5f; alphas[0] = 0f;
                offs[1] = -t * 0.5f + f * 0.5f; alphas[1] = 1f;
                offs[2] = t * 0.5f - f * 0.5f; alphas[2] = 1f;
                offs[3] = t * 0.5f + f * 0.5f; alphas[3] = 0f;
            }
            else
            {
                rows = 3;
                float half = (t + f) * 0.5f;
                offs[0] = -half; alphas[0] = 0f;
                offs[1] = 0f; alphas[1] = Mathf.Min(1f, 2f * t / (t + f));
                offs[2] = half; alphas[2] = 0f;
            }
            int b = vh.currentVertCount;
            for (int i = 0; i < n; i++)
                for (int k = 0; k < rows; k++)
                    Vert(vh, path[i] + norm[i] * offs[k], Fade(c, alphas[k]));
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                for (int k = 0; k < rows - 1; k++)
                {
                    int a0 = b + i * rows + k, a1 = a0 + 1, b0 = b + j * rows + k, b1 = b0 + 1;
                    vh.AddTriangle(a0, a1, b1);
                    vh.AddTriangle(a0, b1, b0);
                }
            }
        }
    }
}
