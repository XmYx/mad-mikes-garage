using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Anti-aliased shapes on a <see cref="PixelCanvas"/>' physical pixels (the round minimap, the map page, the
    /// waypoint marker). Coordinates are physical pixels from the top-left, pixel centres at +0.5; an optional circle clips
    /// everything (the minimap face). <see cref="smooth"/> false gives hard pixel edges (the pixel-art HUD).</summary>
    public static class MapDraw
    {
        public static bool smooth = true;
        static bool clip; static float ccx, ccy, cr;
        static bool rectClip; static float rx0, ry0, rx1, ry1;

        /// <summary>Clip to a circle (physical pixels) until <see cref="NoClip"/>.</summary>
        public static void ClipCircle(float cx, float cy, float r) { clip = true; ccx = cx; ccy = cy; cr = r; }
        /// <summary>Clip to a rectangle (physical pixels) until <see cref="NoClip"/>.</summary>
        public static void ClipRect(float x0, float y0, float x1, float y1) { rectClip = true; rx0 = x0; ry0 = y0; rx1 = x1; ry1 = y1; }
        public static void NoClip() { clip = false; rectClip = false; }

        /// <summary>Coverage of a pixel whose centre lies <paramref name="inside"/> pixels inside an edge.</summary>
        public static float Cov(float inside) => smooth ? Mathf.Clamp01(inside + 0.5f) : (inside >= 0f ? 1f : 0f);

        public static void Plot(PixelCanvas c, int fx, int fy, Color32 col, float cov)
        {
            if (clip) { float dx = fx + 0.5f - ccx, dy = fy + 0.5f - ccy; cov *= Cov(cr - Mathf.Sqrt(dx * dx + dy * dy)); }
            if (rectClip && (fx < rx0 || fy < ry0 || fx >= rx1 || fy >= ry1)) return;
            if (cov <= 0.02f) return;
            if (cov < 0.98f) col.a = (byte)(col.a * cov);
            c.SetFine(fx, fy, col);
        }

        static bool Box(PixelCanvas c, float x0, float y0, float x1, float y1, out int ix0, out int iy0, out int ix1, out int iy1)
        {
            if (clip) { x0 = Mathf.Max(x0, ccx - cr - 1f); y0 = Mathf.Max(y0, ccy - cr - 1f); x1 = Mathf.Min(x1, ccx + cr + 1f); y1 = Mathf.Min(y1, ccy + cr + 1f); }
            if (rectClip) { x0 = Mathf.Max(x0, rx0); y0 = Mathf.Max(y0, ry0); x1 = Mathf.Min(x1, rx1); y1 = Mathf.Min(y1, ry1); }
            ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)); iy0 = Mathf.Max(0, Mathf.FloorToInt(y0));
            ix1 = Mathf.Min(c.pw - 1, Mathf.CeilToInt(x1)); iy1 = Mathf.Min(c.ph - 1, Mathf.CeilToInt(y1));
            return ix1 >= ix0 && iy1 >= iy0;
        }

        /// <summary>A thick line with round ends (half width <paramref name="hw"/>); thinner than a pixel fades instead.</summary>
        public static void Capsule(PixelCanvas c, float ax, float ay, float bx, float by, float hw, Color32 col)
        {
            float fade = 1f;
            if (hw < 0.5f) { fade = Mathf.Max(0.2f, hw * 2f); hw = 0.5f; }
            if (!Box(c, Mathf.Min(ax, bx) - hw - 1f, Mathf.Min(ay, by) - hw - 1f, Mathf.Max(ax, bx) + hw + 1f, Mathf.Max(ay, by) + hw + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            float abx = bx - ax, aby = by - ay, len2 = Mathf.Max(1e-5f, abx * abx + aby * aby);
            float R = hw + 1f;
            for (int j = y0; j <= y1; j++)
            {
                // only the span of this row within R of the segment (its part inside the band yc ± R, widened by R):
                // long diagonal roads no longer scan their whole bounding box
                float yc = j + 0.5f, lo, hi;
                if (Mathf.Abs(aby) < 1e-4f)
                {
                    if (Mathf.Abs(yc - ay) > R) continue;
                    lo = Mathf.Min(ax, bx); hi = Mathf.Max(ax, bx);
                }
                else
                {
                    float t0 = (yc - R - ay) / aby, t1 = (yc + R - ay) / aby;
                    if (t0 > t1) { float tt = t0; t0 = t1; t1 = tt; }
                    t0 = Mathf.Max(t0, 0f); t1 = Mathf.Min(t1, 1f);
                    if (t0 > t1) continue;
                    float xa = ax + abx * t0, xb = ax + abx * t1;
                    lo = Mathf.Min(xa, xb); hi = Mathf.Max(xa, xb);
                }
                int i0 = Mathf.Max(x0, Mathf.FloorToInt(lo - R) - 1), i1 = Mathf.Min(x1, Mathf.CeilToInt(hi + R));
                for (int i = i0; i <= i1; i++)
                    CapsulePixel(c, i, j, ax, ay, abx, aby, len2, hw, fade, col);
            }
        }

        static void CapsulePixel(PixelCanvas c, int i, int j, float ax, float ay, float abx, float aby, float len2, float hw, float fade, Color32 col)
        {
            float px = i + 0.5f - ax, py = j + 0.5f - ay;
            float t = Mathf.Clamp01((px * abx + py * aby) / len2);
            float dx = px - abx * t, dy = py - aby * t;
            float cov = Cov(hw - Mathf.Sqrt(dx * dx + dy * dy)) * fade;
            if (cov > 0f) Plot(c, i, j, col, cov);
        }

        public static void Disc(PixelCanvas c, float cx, float cy, float r, Color32 col)
        {
            if (!Box(c, cx - r - 1f, cy - r - 1f, cx + r + 1f, cy + r + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            float R = r + 1f;
            for (int j = y0; j <= y1; j++)
            {
                float ry = j + 0.5f - cy;
                if (ry * ry > R * R) continue;
                float hx = Mathf.Sqrt(R * R - ry * ry);                                             // this row's chord: nothing outside it is covered
                int i0 = Mathf.Max(x0, Mathf.FloorToInt(cx - hx) - 1), i1 = Mathf.Min(x1, Mathf.CeilToInt(cx + hx));
                for (int i = i0; i <= i1; i++)
                {
                    float dx = i + 0.5f - cx;
                    float cov = Cov(r - Mathf.Sqrt(dx * dx + ry * ry));
                    if (cov > 0f) Plot(c, i, j, col, cov);
                }
            }
        }

        /// <summary>A ring of radius <paramref name="r"/> and half width <paramref name="hw"/>; an arc from
        /// <paramref name="from"/> degrees (clockwise from up) over <paramref name="sweep"/> degrees.</summary>
        public static void Ring(PixelCanvas c, float cx, float cy, float r, float hw, Color32 col, float from = 0f, float sweep = 360f)
        {
            if (sweep <= 0f) return;
            float o = r + hw + 1f;
            if (!Box(c, cx - o, cy - o, cx + o, cy + o, out int x0, out int y0, out int x1, out int y1)) return;
            bool full = sweep >= 360f;
            float inner = r - hw - 1f;
            for (int j = y0; j <= y1; j++)
            {
                // the row crosses the band in at most two spans: skip the hole in the middle and what lies outside
                float ry = j + 0.5f - cy;
                if (ry * ry > o * o) continue;
                float ho = Mathf.Sqrt(o * o - ry * ry);
                float hi = inner > 0f && ry * ry < inner * inner ? Mathf.Sqrt(inner * inner - ry * ry) : -1f;
                int a0 = Mathf.Max(x0, Mathf.FloorToInt(cx - ho) - 1), b1 = Mathf.Min(x1, Mathf.CeilToInt(cx + ho));
                int a1 = hi > 1f ? Mathf.Min(b1, Mathf.CeilToInt(cx - hi) + 1) : b1, b0 = hi > 1f ? Mathf.Max(a0, Mathf.FloorToInt(cx + hi) - 1) : b1 + 1;
                for (int i = a0; i <= a1; i++) RingPixel(c, i, j, cx, cy, r, hw, col, from, sweep, full);
                for (int i = Mathf.Max(b0, a1 + 1); i <= b1; i++) RingPixel(c, i, j, cx, cy, r, hw, col, from, sweep, full);
            }
        }

        static void RingPixel(PixelCanvas c, int i, int j, float cx, float cy, float r, float hw, Color32 col, float from, float sweep, bool full)
        {
            float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
            float cov = Cov(hw - Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r));
            if (cov <= 0f) return;
            if (!full)
            {
                float a = Mathf.Repeat(Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg - from, 360f);
                if (a > sweep) return;
            }
            Plot(c, i, j, col, cov);
        }

        /// <summary>A filled triangle (any winding).</summary>
        public static void Triangle(PixelCanvas c, Vector2 a, Vector2 b, Vector2 d, Color32 col)
        {
            if (!Box(c, Mathf.Min(a.x, Mathf.Min(b.x, d.x)) - 1f, Mathf.Min(a.y, Mathf.Min(b.y, d.y)) - 1f,
                    Mathf.Max(a.x, Mathf.Max(b.x, d.x)) + 1f, Mathf.Max(a.y, Mathf.Max(b.y, d.y)) + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            float s = Mathf.Sign((b.x - a.x) * (d.y - a.y) - (b.y - a.y) * (d.x - a.x));
            if (s == 0f) return;
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                var p = new Vector2(i + 0.5f, j + 0.5f);
                float e = Mathf.Min(Edge(a, b, p, s), Mathf.Min(Edge(b, d, p, s), Edge(d, a, p, s)));
                float cov = Cov(e);
                if (cov > 0f) Plot(c, i, j, col, cov);
            }
        }

        /// <summary>A diamond of half size <paramref name="r"/> (waypoints, pins).</summary>
        public static void Diamond(PixelCanvas c, float cx, float cy, float r, Color32 col)
        {
            if (!Box(c, cx - r - 1f, cy - r - 1f, cx + r + 1f, cy + r + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                float d = (Mathf.Abs(i + 0.5f - cx) + Mathf.Abs(j + 0.5f - cy)) * 0.7071f;
                float cov = Cov(r * 0.7071f - d);
                if (cov > 0f) Plot(c, i, j, col, cov);
            }
        }

        /// <summary>An arrowhead (chevron) pointing along <paramref name="angle"/> degrees clockwise from up, length
        /// <paramref name="len"/>; a dark outline <paramref name="edge"/> pixels wide under it.</summary>
        public static void Arrow(PixelCanvas c, float cx, float cy, float angle, float len, Color32 col, Color32 outline, float edge)
        {
            float a = angle * Mathf.Deg2Rad;
            var f = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)); var r = new Vector2(-f.y, f.x);
            var o = new Vector2(cx, cy);
            if (edge > 0f)
            {
                float k = 1f + edge / len * 2.2f;
                Triangle(c, o + f * len * 0.62f * k, o - f * len * 0.38f * k + r * len * 0.42f * k, o - f * len * 0.38f * k - r * len * 0.42f * k, outline);
            }
            Vector2 tip = o + f * len * 0.62f, bl = o - f * len * 0.38f + r * len * 0.42f, br = o - f * len * 0.38f - r * len * 0.42f, notch = o - f * len * 0.14f;
            // the two halves as one shape (max coverage), so the shared edge leaves no seam
            float s1 = Mathf.Sign((bl.x - tip.x) * (notch.y - tip.y) - (bl.y - tip.y) * (notch.x - tip.x));
            float s2 = Mathf.Sign((notch.x - tip.x) * (br.y - tip.y) - (notch.y - tip.y) * (br.x - tip.x));
            if (!Box(c, Mathf.Min(tip.x, Mathf.Min(bl.x, br.x)) - 1f, Mathf.Min(tip.y, Mathf.Min(bl.y, br.y)) - 1f,
                    Mathf.Max(tip.x, Mathf.Max(bl.x, br.x)) + 1f, Mathf.Max(tip.y, Mathf.Max(bl.y, br.y)) + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                var p = new Vector2(i + 0.5f, j + 0.5f);
                float e1 = Mathf.Min(Edge(tip, bl, p, s1), Mathf.Min(Edge(bl, notch, p, s1), Edge(notch, tip, p, s1)));
                float e2 = Mathf.Min(Edge(tip, notch, p, s2), Mathf.Min(Edge(notch, br, p, s2), Edge(br, tip, p, s2)));
                float cov = Cov(Mathf.Max(e1, e2));
                if (cov > 0f) Plot(c, i, j, col, cov);
            }
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p, float s)
        {
            var e = b - a;
            float len = e.magnitude;
            if (len < 1e-5f) return 99f;
            return s * (e.x * (p.y - a.y) - e.y * (p.x - a.x)) / len;
        }
    }
}
