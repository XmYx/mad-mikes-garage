using UnityEngine;
using UnityEngine.UI;

namespace MadMax.Rendering
{
    /// <summary>The HD HUD font (setting HUD FONT): draws the text runs a <see cref="PixelCanvas"/> recorded as smooth
    /// glyph quads from the baked atlas (Resources/HDFont/hud_font.png, tools/hud_font.py), over the pixel HUD image
    /// and in its exact layout (each glyph in the 3x5 font's cell, a little wider).</summary>
    public class HudTextGraphic : MaskableGraphic
    {
        const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ .:/-%<>[]+!#='(),?&`";
        const int PerRow = 16; const float CellW = 54f, CellH = 75f, AtlasW = 1024f, AtlasH = 512f;
        public PixelCanvas source;
        static Texture2D atlas;
        static int[] index;

        public override Texture mainTexture => Atlas ? (Texture)atlas : s_WhiteTexture;

        public static Texture2D Atlas
        {
            get
            {
                if (!atlas) atlas = Resources.Load<Texture2D>("HDFont/hud_font");
                if (index == null)
                {
                    index = new int[128];
                    for (int i = 0; i < index.Length; i++) index[i] = -1;
                    for (int i = 0; i < Chars.Length; i++) index[Chars[i]] = i;
                }
                return atlas;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || source.runs == null || !Atlas) return;
            var r = rectTransform.rect;
            float sx = r.width / source.w, sy = r.height / source.h;
            foreach (var run in source.runs)
            {
                float x = run.x;
                foreach (char ch in run.text)
                {
                    int gi = ch < 128 ? index[ch] : -1;
                    if (gi >= 0 && ch != ' ')
                    {
                        // the glyph box: 3.6 x 5 logical pixels around the 3x5 cell (top-left origin, y down)
                        float gx0 = x - 0.3f * run.scale, gx1 = gx0 + 3.6f * run.scale, gy0 = run.y, gy1 = run.y + 5f * run.scale;
                        float u0 = (gi % PerRow) * CellW / AtlasW, u1 = u0 + CellW / AtlasW;
                        float v1 = 1f - (gi / PerRow) * CellH / AtlasH, v0 = v1 - CellH / AtlasH;
                        int b = vh.currentVertCount;
                        Vector2 P(float px, float py) => new Vector2(r.xMin + px * sx, r.yMax - py * sy);
                        vh.AddVert(P(gx0, gy1), run.color, new Vector4(u0, v0));
                        vh.AddVert(P(gx0, gy0), run.color, new Vector4(u0, v1));
                        vh.AddVert(P(gx1, gy0), run.color, new Vector4(u1, v1));
                        vh.AddVert(P(gx1, gy1), run.color, new Vector4(u1, v0));
                        vh.AddTriangle(b, b + 1, b + 2); vh.AddTriangle(b, b + 2, b + 3);
                    }
                    x += 4 * run.scale;
                }
            }
        }
    }
}
