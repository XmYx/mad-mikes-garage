using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Game
{
    public partial class PixelHud
    {
        /// <summary>Lens geometry (fractions of the HUD height): two overlapping circles side by side.</summary>
        public const float LensRadius = 0.40f, LensOffset = 0.25f;
        static readonly Color32 LensDark = new Color32(4, 5, 6, 255), LensSoft = new Color32(4, 5, 6, 120);

        /// <summary>The mask is drawn this frame (tests).</summary>
        public static bool LensMaskShown { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetLens() => LensMaskShown = false;

        /// <summary>Looking through binoculars in a crosshair view: everything outside the two lenses goes black (drawn
        /// first, so the HUD stays on top), a fine range scale across the middle and, with the sun in the lenses, a warm
        /// flare on their rims.</summary>
        void DrawLensMask()
        {
            LensMaskShown = false;
            if (!BinocularsTool.Looking || game.Current || !rig.CrosshairView || (game.Menus && game.Menus.IsOpen)) return;
            LensMaskShown = true;
            float R = canvas.h * LensRadius, off = canvas.h * LensOffset, cy = canvas.h * 0.5f;
            float cxL = canvas.w * 0.5f - off, cxR = canvas.w * 0.5f + off;
            float glint = WastelandGame.GlintStrength;
            if (HD) { LensMaskHD(R, off, cy, cxL, cxR, glint); return; }
            for (int x = 0; x < canvas.w; x++)
            {
                float px = x + 0.5f;
                if (!LensSpan(px, R, cy, cxL, cxR, out float top, out float bot)) { canvas.Rect(x, 0, 1, canvas.h, LensDark); continue; }
                int t = Mathf.RoundToInt(top), b = Mathf.RoundToInt(bot);
                canvas.Rect(x, 0, 1, t, LensDark);
                canvas.Rect(x, b, 1, canvas.h - b, LensDark);
                canvas.Rect(x, t, 1, 2, LensSoft);                                                // the rim's shade
                canvas.Rect(x, b - 2, 1, 2, LensSoft);
            }
            int cx = canvas.w / 2, ry = Mathf.RoundToInt(cy);
            for (int i = -4; i <= 4; i++) canvas.Rect(cx + i * 6, ry + (i == 0 ? -2 : 0), 1, i == 0 ? 5 : 2, Dim);   // range scale
            if (glint > 0.2f)
            {
                var warm = Amber; warm.a = (byte)(60 + 140 * glint);
                for (int a = -70; a <= 70; a += 3)
                {
                    float r = a * Mathf.Deg2Rad;
                    canvas.Set(Mathf.RoundToInt(cxL + Mathf.Sin(r) * (R - 1.5f)), Mathf.RoundToInt(cy - Mathf.Cos(r) * (R - 1.5f)), warm);
                    canvas.Set(Mathf.RoundToInt(cxR + Mathf.Sin(r) * (R - 1.5f)), Mathf.RoundToInt(cy - Mathf.Cos(r) * (R - 1.5f)), warm);
                }
            }
        }

        /// <summary>The union of the two lenses in column <paramref name="x"/>: false when the column misses both.</summary>
        static bool LensSpan(float x, float R, float cy, float cxL, float cxR, out float top, out float bot)
        {
            top = float.MaxValue; bot = float.MinValue;
            float dl = x - cxL, dr = x - cxR;
            if (dl * dl < R * R) { float h = Mathf.Sqrt(R * R - dl * dl); top = Mathf.Min(top, cy - h); bot = Mathf.Max(bot, cy + h); }
            if (dr * dr < R * R) { float h = Mathf.Sqrt(R * R - dr * dr); top = Mathf.Min(top, cy - h); bot = Mathf.Max(bot, cy + h); }
            return top < bot;
        }

        /// <summary>HD: the mask as trapezoid strips above and below the lenses (back layer), rims and flare as arcs.</summary>
        void LensMaskHD(float R, float off, float cy, float cxL, float cxR, float glint)
        {
            float x0 = cxL - R, x1 = cxR + R;
            if (x0 > 0f) V.Fill(0f, 0f, x0 + 0.5f, canvas.h, 0f, LensDark, true);
            if (x1 < canvas.w) V.Fill(x1 - 0.5f, 0f, canvas.w - x1 + 0.5f, canvas.h, 0f, LensDark, true);
            const int Strips = 48;
            float step = (x1 - x0) / Strips;
            for (int i = 0; i < Strips; i++)
            {
                float a = x0 + i * step, b = a + step;
                LensSpan(Mathf.Min(a + 0.01f, x1), R, cy, cxL, cxR, out float ta, out float ba);
                LensSpan(Mathf.Max(b - 0.01f, x0), R, cy, cxL, cxR, out float tb, out float bb);
                float e = b + 0.3f;                                                              // overlap: no hairline seams
                V.BeginPoly(); V.P(a, -1f); V.P(e, -1f); V.P(e, tb); V.P(a, ta); V.EndPoly(LensDark, true);
                V.BeginPoly(); V.P(a, ba); V.P(e, bb); V.P(e, canvas.h + 1f); V.P(a, canvas.h + 1f); V.EndPoly(LensDark, true);
            }
            // rims: the outer arc of each lens (where it isn't inside the other)
            float cut = Mathf.Asin(Mathf.Clamp01(off / R)) * Mathf.Rad2Deg, sweep = 360f - (180f - 2f * cut);
            var rim = HudVector.Alpha(LensDark, 150);
            V.Arc(cxL, cy, R - 1f, 2.5f, 180f - cut, sweep, rim);
            V.Arc(cxR, cy, R - 1f, 2.5f, -cut, sweep, rim);
            for (int i = -4; i <= 4; i++) V.Line(canvas.w * 0.5f + i * 6f, cy - (i == 0 ? 2.5f : 1f), canvas.w * 0.5f + i * 6f, cy + (i == 0 ? 2.5f : 1f), 0.5f, Dim);
            if (glint > 0.2f)
            {
                var warm = HudVector.Alpha(Amber, (byte)(50 + 150 * glint));
                V.Arc(cxL, cy, R - 2.5f, 1.6f, -70f, 140f, warm);
                V.Arc(cxR, cy, R - 2.5f, 1.6f, -70f, 140f, warm);
                V.Circle(cxL - R * 0.35f, cy - R * 0.45f, 2f + 3f * glint, HudVector.Alpha(Amber, (byte)(40 + 80 * glint)));
            }
        }
    }
}
