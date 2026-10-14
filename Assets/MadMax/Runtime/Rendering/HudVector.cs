using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>The HD HUD's vector shapes, recorded per frame in the <see cref="PixelCanvas"/>'s logical coordinates
    /// (top-left origin, y down) and drawn anti-aliased at screen resolution by two <see cref="HudVectorGraphic"/>s: the
    /// back layer under the pixel image (panels, shadows) and the front layer over it (bars, rings, needles, glyphs).
    /// Angles are degrees clockwise from 12 o'clock.</summary>
    public class HudVector
    {
        public enum Kind : byte { Rect, Stroke, Shadow, Line, Arc, Poly }

        public struct Shape
        {
            public Kind kind;
            public float x, y, w, h, r, t, a0, a1;   // Line: (x, y) → (w, h); Arc: centre x, y, radius r, a0..a1; Poly: a0 = first point, a1 = count
            public Color32 c, c2;                    // c2: bottom colour of a gradient fill (Rect)
            public float bx0, by0, bx1, by1;         // bounding box (cover tests)
        }

        public readonly List<Shape> back = new List<Shape>(256), front = new List<Shape>(1024);
        public readonly List<Vector2> points = new List<Vector2>(256);
        /// <summary><see cref="PixelCanvas.Panel"/> draws vector panels (off while the menus draw in pixels).</summary>
        public bool on = true;

        /// <summary>A screen-space image over the pixel layer (the damage view): <see cref="holeCovered"/> when a panel
        /// drawn later covers part of it.</summary>
        public Rect hole;
        public bool holeCovered;

        /// <summary>A cheap fingerprint of a layer (FNV over the shape fields and the polygon points): the UI mesh is
        /// rebuilt only when it changed (a canvas rebuild every frame cost ~1.6 ms).</summary>
        public int Hash(List<Shape> layer)
        {
            unchecked
            {
                int h = (int)2166136261;
                void F(float v) { h = (h ^ System.BitConverter.SingleToInt32Bits(v)) * 16777619; }
                for (int i = 0; i < layer.Count; i++)
                {
                    var s = layer[i];
                    h = (h ^ (int)s.kind) * 16777619;
                    F(s.x); F(s.y); F(s.w); F(s.h); F(s.r); F(s.t); F(s.a0); F(s.a1);
                    h = (h ^ (s.c.r | s.c.g << 8 | s.c.b << 16 | s.c.a << 24)) * 16777619;
                    h = (h ^ (s.c2.r | s.c2.g << 8 | s.c2.b << 16 | s.c2.a << 24)) * 16777619;
                    if (s.kind == Kind.Poly) for (int k = (int)s.a0, e = k + (int)s.a1; k < e && k < points.Count; k++) { F(points[k].x); F(points[k].y); }
                }
                return h ^ layer.Count;
            }
        }

        public void Clear() { back.Clear(); front.Clear(); points.Clear(); on = true; hole = default; holeCovered = false; }

        /// <summary>The menus draw over the HUD in the pixel image: everything recorded so far goes under it.</summary>
        public void Sink() { back.AddRange(front); front.Clear(); on = false; }

        /// <summary>Drop front shapes mostly inside a rect drawn over them later (as <see cref="PixelCanvas"/> does for text).</summary>
        public void Cover(float x, float y, float w, float h)
        {
            if (hole.width > 0f && !holeCovered)
            {
                float hx = Mathf.Max(0f, Mathf.Min(x + w, hole.xMax) - Mathf.Max(x, hole.xMin)), hy = Mathf.Max(0f, Mathf.Min(y + h, hole.yMax) - Mathf.Max(y, hole.yMin));
                if (hx * hy > 0.08f * hole.width * hole.height) holeCovered = true;
            }
            for (int i = front.Count - 1; i >= 0; i--)
            {
                var s = front[i];
                float sw = s.bx1 - s.bx0, sh = s.by1 - s.by0;
                float ox = Mathf.Max(0f, Mathf.Min(x + w, s.bx1) - Mathf.Max(x, s.bx0)), oy = Mathf.Max(0f, Mathf.Min(y + h, s.by1) - Mathf.Max(y, s.by0));
                if (ox * oy * 2f >= Mathf.Max(0.01f, sw * sh)) front.RemoveAt(i);
            }
        }

        void Add(Shape s, bool backLayer)
        {
            if (backLayer) back.Add(s); else front.Add(s);
        }

        static Shape Box(Kind k, float x, float y, float w, float h, float pad)
        {
            return new Shape { kind = k, x = x, y = y, w = w, h = h, bx0 = x - pad, by0 = y - pad, bx1 = x + w + pad, by1 = y + h + pad };
        }

        // ------------------------------------------------------------------ primitives
        /// <summary>Rounded rect, filled; <paramref name="bottom"/> = gradient towards the bottom edge.</summary>
        public void Fill(float x, float y, float w, float h, float radius, Color32 c, bool backLayer = false) => Fill(x, y, w, h, radius, c, c, backLayer);

        public void Fill(float x, float y, float w, float h, float radius, Color32 top, Color32 bottom, bool backLayer = false)
        {
            if (w <= 0f || h <= 0f) return;
            var s = Box(Kind.Rect, x, y, w, h, 0f); s.r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f); s.c = top; s.c2 = bottom;
            Add(s, backLayer);
        }

        /// <summary>Rounded rect with a gradient from the left edge to the right.</summary>
        public void FillH(float x, float y, float w, float h, float radius, Color32 left, Color32 right, bool backLayer = false)
        {
            if (w <= 0f || h <= 0f) return;
            var s = Box(Kind.Rect, x, y, w, h, 0f); s.r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f); s.c = left; s.c2 = right; s.t = 1f;
            Add(s, backLayer);
        }

        /// <summary>Rounded rect outline <paramref name="thick"/> wide, inside the rect.</summary>
        public void Stroke(float x, float y, float w, float h, float radius, float thick, Color32 c, bool backLayer = false)
        {
            if (w <= 0f || h <= 0f) return;
            var s = Box(Kind.Stroke, x, y, w, h, 0f); s.r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f); s.t = thick; s.c = c;
            Add(s, backLayer);
        }

        /// <summary>Soft shadow: a rounded rect blurred over <paramref name="blur"/>.</summary>
        public void Shadow(float x, float y, float w, float h, float radius, float blur, Color32 c, bool backLayer = true)
        {
            var s = Box(Kind.Shadow, x, y, w, h, blur * 0.5f); s.r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f); s.t = blur; s.c = c;
            Add(s, backLayer);
        }

        public void Circle(float cx, float cy, float rad, Color32 c, bool backLayer = false) => Fill(cx - rad, cy - rad, rad * 2f, rad * 2f, rad, c, c, backLayer);

        public void Ring(float cx, float cy, float rad, float thick, Color32 c, bool backLayer = false)
            => Stroke(cx - rad - thick * 0.5f, cy - rad - thick * 0.5f, rad * 2f + thick, rad * 2f + thick, rad + thick * 0.5f, thick, c, backLayer);

        /// <summary>An arc centred on radius <paramref name="rad"/>, from <paramref name="deg0"/> sweeping
        /// <paramref name="sweep"/> degrees clockwise (negative: anticlockwise).</summary>
        public void Arc(float cx, float cy, float rad, float thick, float deg0, float sweep, Color32 c, bool backLayer = false)
        {
            if (Mathf.Abs(sweep) < 0.05f) return;
            var s = Box(Kind.Arc, cx - rad, cy - rad, rad * 2f, rad * 2f, thick); s.r = rad; s.t = thick; s.a0 = deg0; s.a1 = sweep; s.c = c;
            Add(s, backLayer);
        }

        public void Line(float x0, float y0, float x1, float y1, float thick, Color32 c, bool backLayer = false)
        {
            var s = new Shape { kind = Kind.Line, x = x0, y = y0, w = x1, h = y1, t = thick, c = c,
                bx0 = Mathf.Min(x0, x1) - thick, by0 = Mathf.Min(y0, y1) - thick, bx1 = Mathf.Max(x0, x1) + thick, by1 = Mathf.Max(y0, y1) + thick };
            Add(s, backLayer);
        }

        /// <summary>A point on a circle (angle clockwise from 12 o'clock).</summary>
        public static Vector2 Polar(float cx, float cy, float rad, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(cx + Mathf.Sin(a) * rad, cy - Mathf.Cos(a) * rad);
        }

        int polyStart = -1;
        /// <summary>Begin a convex polygon: <see cref="P"/> its corners in order, then <see cref="EndPoly"/>.</summary>
        public void BeginPoly() => polyStart = points.Count;
        public void P(float x, float y) => points.Add(new Vector2(x, y));
        public void P(Vector2 p) => points.Add(p);

        public void EndPoly(Color32 c, bool backLayer = false)
        {
            int n = points.Count - polyStart;
            if (polyStart < 0 || n < 3) { if (polyStart >= 0) points.RemoveRange(polyStart, n); polyStart = -1; return; }
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = polyStart; i < points.Count; i++) { var p = points[i]; x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            Add(new Shape { kind = Kind.Poly, a0 = polyStart, a1 = n, c = c, bx0 = x0, by0 = y0, bx1 = x1, by1 = y1 }, backLayer);
            polyStart = -1;
        }

        public void Tri(float x0, float y0, float x1, float y1, float x2, float y2, Color32 c, bool backLayer = false)
        {
            BeginPoly(); P(x0, y0); P(x1, y1); P(x2, y2); EndPoly(c, backLayer);
        }

        // ------------------------------------------------------------------ composites
        public static readonly Color32 ShadowCol = new Color32(4, 6, 6, 120);

        /// <summary>The HD HUD panel (back layer): soft shadow, gradient fill, edge, top highlight.</summary>
        public void Panel(float x, float y, float w, float h, float radius = 2.6f, byte alpha = 235)
        {
            var sc = ShadowCol; sc.a = (byte)(ShadowCol.a * alpha / 255);
            Shadow(x + 0.4f, y + 1.1f, w, h, radius + 1f, 3.2f, sc, true);
            Color32 top = Lighten(Pal.Panel, 14), bot = Darken(Pal.Panel, 10);
            top.a = alpha; bot.a = alpha;
            Fill(x, y, w, h, radius, top, bot, true);
            var edge = Pal.PanelEdge; edge.a = (byte)(170 * alpha / 255);
            Stroke(x, y, w, h, radius, 0.42f, edge, true);
            var hi = Pal.PanelLight; hi.a = (byte)(60 * alpha / 255);
            Line(x + radius, y + 0.75f, x + w - radius, y + 0.75f, 0.3f, hi, true);
        }

        public static Color32 Lighten(Color32 c, int d) => new Color32((byte)Mathf.Min(255, c.r + d), (byte)Mathf.Min(255, c.g + d), (byte)Mathf.Min(255, c.b + d), c.a);
        public static Color32 Darken(Color32 c, int d) => new Color32((byte)Mathf.Max(0, c.r - d), (byte)Mathf.Max(0, c.g - d), (byte)Mathf.Max(0, c.b - d), c.a);
        public static Color32 Alpha(Color32 c, byte a) { c.a = a; return c; }
    }
}
