using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>Flat vector icons for the HD HUD (vitals, gauges, warning lamps), built from <see cref="HudVector"/>
    /// primitives in a box of <c>size</c> HUD pixels centred on (cx, cy), one colour each.</summary>
    public static class HudGlyphs
    {
        public enum Glyph { Heart, Bolt, Food, Drop, Soap, Thermo, Fuel, Oil, Engine, Battery, Fire, Coil, Tyre, Frame, Lamp, Snow, Air, Wrench, Rain, Sun, Moon, Anchor, Plane, Shield }

        public static void Draw(HudVector v, Glyph g, float cx, float cy, float size, Color32 c)
        {
            float s = size;
            float L(float t) => t * s;                                                             // box units → HUD pixels
            float lw = Mathf.Max(0.35f, s * 0.13f);
            switch (g)
            {
                case Glyph.Heart:
                    v.Circle(cx - L(0.2f), cy - L(0.1f), L(0.25f), c);
                    v.Circle(cx + L(0.2f), cy - L(0.1f), L(0.25f), c);
                    v.Tri(cx - L(0.44f), cy - L(0.01f), cx + L(0.44f), cy - L(0.01f), cx, cy + L(0.43f), c);
                    break;
                case Glyph.Bolt:
                    v.BeginPoly(); v.P(cx + L(0.16f), cy - L(0.48f)); v.P(cx - L(0.28f), cy + L(0.06f)); v.P(cx - L(0.02f), cy + L(0.06f)); v.P(cx + L(0.06f), cy - L(0.48f)); v.EndPoly(c);
                    v.BeginPoly(); v.P(cx - L(0.1f), cy + L(0.48f)); v.P(cx + L(0.28f), cy - L(0.06f)); v.P(cx + L(0.02f), cy - L(0.06f)); v.P(cx - L(0.04f), cy + L(0.48f)); v.EndPoly(c);
                    v.Fill(cx - L(0.2f), cy - L(0.04f), L(0.4f), L(0.12f), 0f, c);
                    break;
                case Glyph.Food:
                    v.Circle(cx, cy + L(0.08f), L(0.34f), c);
                    v.Line(cx, cy - L(0.24f), cx + L(0.06f), cy - L(0.46f), lw * 0.8f, c);
                    v.Tri(cx + L(0.06f), cy - L(0.3f), cx + L(0.36f), cy - L(0.44f), cx + L(0.22f), cy - L(0.22f), c);
                    break;
                case Glyph.Drop:
                    v.Circle(cx, cy + L(0.14f), L(0.3f), c);
                    v.Tri(cx - L(0.27f), cy + L(0.02f), cx + L(0.27f), cy + L(0.02f), cx, cy - L(0.48f), c);
                    break;
                case Glyph.Soap:
                    v.Fill(cx - L(0.42f), cy - L(0.02f), L(0.84f), L(0.44f), L(0.12f), c);
                    v.Ring(cx - L(0.16f), cy - L(0.26f), L(0.1f), lw * 0.6f, c);
                    v.Ring(cx + L(0.16f), cy - L(0.36f), L(0.07f), lw * 0.5f, c);
                    v.Circle(cx + L(0.3f), cy - L(0.16f), L(0.05f), c);
                    break;
                case Glyph.Thermo:
                    v.Fill(cx - L(0.1f), cy - L(0.48f), L(0.2f), L(0.66f), L(0.1f), c);
                    v.Circle(cx, cy + L(0.27f), L(0.19f), c);
                    v.Line(cx + L(0.16f), cy - L(0.32f), cx + L(0.3f), cy - L(0.32f), lw * 0.6f, c);
                    v.Line(cx + L(0.16f), cy - L(0.14f), cx + L(0.3f), cy - L(0.14f), lw * 0.6f, c);
                    break;
                case Glyph.Fuel:
                    v.Fill(cx - L(0.38f), cy - L(0.44f), L(0.46f), L(0.88f), L(0.06f), c);
                    v.Line(cx + L(0.08f), cy - L(0.2f), cx + L(0.32f), cy - L(0.06f), lw * 0.8f, c);
                    v.Line(cx + L(0.32f), cy - L(0.06f), cx + L(0.32f), cy + L(0.3f), lw * 0.8f, c);
                    v.Line(cx + L(0.32f), cy - L(0.06f), cx + L(0.22f), cy - L(0.36f), lw * 0.8f, c);
                    break;
                case Glyph.Oil:
                    v.Fill(cx - L(0.44f), cy - L(0.06f), L(0.56f), L(0.34f), L(0.05f), c);
                    v.Fill(cx - L(0.3f), cy - L(0.2f), L(0.16f), L(0.16f), 0f, c);
                    v.Line(cx + L(0.1f), cy + L(0.02f), cx + L(0.44f), cy - L(0.18f), lw, c);
                    v.Circle(cx + L(0.42f), cy + L(0.18f), L(0.07f), c);
                    break;
                case Glyph.Engine:
                    v.Fill(cx - L(0.3f), cy - L(0.18f), L(0.56f), L(0.42f), L(0.04f), c);
                    v.Fill(cx - L(0.16f), cy - L(0.34f), L(0.28f), L(0.12f), 0f, c);
                    v.Fill(cx - L(0.46f), cy - L(0.06f), L(0.14f), L(0.2f), 0f, c);
                    v.Fill(cx + L(0.28f), cy - L(0.1f), L(0.18f), L(0.28f), 0f, c);
                    break;
                case Glyph.Battery:
                    v.Fill(cx - L(0.42f), cy - L(0.22f), L(0.84f), L(0.52f), L(0.06f), c);
                    v.Fill(cx - L(0.32f), cy - L(0.34f), L(0.14f), L(0.12f), 0f, c);
                    v.Fill(cx + L(0.18f), cy - L(0.34f), L(0.14f), L(0.12f), 0f, c);
                    break;
                case Glyph.Fire:
                    v.Circle(cx, cy + L(0.16f), L(0.28f), c);
                    v.Tri(cx - L(0.26f), cy + L(0.08f), cx + L(0.26f), cy + L(0.08f), cx + L(0.04f), cy - L(0.48f), c);
                    v.Tri(cx - L(0.3f), cy + L(0.12f), cx - L(0.08f), cy, cx - L(0.26f), cy - L(0.24f), c);
                    break;
                case Glyph.Coil:
                    for (int k = 0; k < 4; k++)
                    {
                        float y0 = cy - L(0.4f) + k * L(0.2f);
                        v.Line(cx - L(0.3f), y0, cx + L(0.3f), y0 + L(0.1f), lw * 0.8f, c);
                        v.Line(cx + L(0.3f), y0 + L(0.1f), cx - L(0.3f), y0 + L(0.2f), lw * 0.8f, c);
                    }
                    break;
                case Glyph.Tyre:
                    v.Ring(cx, cy, L(0.36f), L(0.18f), c);
                    v.Circle(cx, cy, L(0.1f), c);
                    break;
                case Glyph.Frame:
                    v.Stroke(cx - L(0.26f), cy - L(0.44f), L(0.52f), L(0.88f), L(0.08f), lw, c);
                    v.Line(cx - L(0.26f), cy - L(0.15f), cx + L(0.26f), cy - L(0.15f), lw * 0.8f, c);
                    v.Line(cx - L(0.26f), cy + L(0.15f), cx + L(0.26f), cy + L(0.15f), lw * 0.8f, c);
                    break;
                case Glyph.Lamp:
                    v.Arc(cx - L(0.06f), cy, L(0.22f), lw * 1.6f, 180f, 180f, c);
                    for (int k = -1; k <= 1; k++) v.Line(cx + L(0.18f), cy + k * L(0.16f), cx + L(0.46f), cy + k * L(0.2f), lw * 0.8f, c);
                    break;
                case Glyph.Snow:
                    for (int k = 0; k < 3; k++)
                    {
                        var a = HudVector.Polar(cx, cy, L(0.44f), k * 60f); var b = HudVector.Polar(cx, cy, L(0.44f), k * 60f + 180f);
                        v.Line(a.x, a.y, b.x, b.y, lw * 0.8f, c);
                    }
                    break;
                case Glyph.Air:
                    v.Ring(cx - L(0.14f), cy + L(0.14f), L(0.2f), lw * 0.8f, c);
                    v.Ring(cx + L(0.2f), cy - L(0.16f), L(0.14f), lw * 0.7f, c);
                    v.Circle(cx + L(0.24f), cy + L(0.3f), L(0.07f), c);
                    break;
                case Glyph.Wrench:
                    v.Line(cx - L(0.3f), cy + L(0.3f), cx + L(0.12f), cy - L(0.12f), lw * 1.4f, c);
                    v.Arc(cx + L(0.2f), cy - L(0.2f), L(0.16f), lw * 1.1f, 90f, 270f, c);
                    break;
                case Glyph.Rain:
                    v.Circle(cx - L(0.14f), cy - L(0.12f), L(0.2f), c);
                    v.Circle(cx + L(0.14f), cy - L(0.18f), L(0.24f), c);
                    v.Fill(cx - L(0.34f), cy - L(0.12f), L(0.68f), L(0.18f), L(0.09f), c);
                    for (int k = -1; k <= 1; k++) v.Line(cx + k * L(0.2f), cy + L(0.16f), cx + k * L(0.2f) - L(0.06f), cy + L(0.42f), lw * 0.7f, c);
                    break;
                case Glyph.Sun:
                    v.Circle(cx, cy, L(0.2f), c);
                    for (int k = 0; k < 8; k++)
                    {
                        var a = HudVector.Polar(cx, cy, L(0.3f), k * 45f); var b = HudVector.Polar(cx, cy, L(0.44f), k * 45f);
                        v.Line(a.x, a.y, b.x, b.y, lw * 0.7f, c);
                    }
                    break;
                case Glyph.Moon:
                    v.Arc(cx, cy, L(0.3f), L(0.14f), 200f, 220f, c);
                    break;
                case Glyph.Anchor:
                    v.Line(cx, cy - L(0.32f), cx, cy + L(0.4f), lw, c);
                    v.Ring(cx, cy - L(0.38f), L(0.08f), lw * 0.7f, c);
                    v.Line(cx - L(0.2f), cy - L(0.18f), cx + L(0.2f), cy - L(0.18f), lw * 0.8f, c);
                    v.Arc(cx, cy + L(0.06f), L(0.34f), lw, 115f, 130f, c);
                    break;
                case Glyph.Plane:
                    v.Line(cx, cy - L(0.44f), cx, cy + L(0.4f), lw * 1.3f, c);
                    v.Tri(cx - L(0.46f), cy + L(0.06f), cx + L(0.46f), cy + L(0.06f), cx, cy - L(0.18f), c);
                    v.Tri(cx - L(0.2f), cy + L(0.42f), cx + L(0.2f), cy + L(0.42f), cx, cy + L(0.24f), c);
                    break;
                case Glyph.Shield:
                    v.BeginPoly(); v.P(cx - L(0.36f), cy - L(0.36f)); v.P(cx + L(0.36f), cy - L(0.36f)); v.P(cx + L(0.3f), cy + L(0.08f)); v.P(cx, cy + L(0.46f)); v.P(cx - L(0.3f), cy + L(0.08f)); v.EndPoly(c);
                    break;
            }
        }
    }
}
