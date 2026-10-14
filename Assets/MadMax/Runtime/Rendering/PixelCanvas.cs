using System;
using MadMax.Voxel;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>CPU pixel buffer with a 3x5 bitmap font. Coordinates are top-left origin, in logical pixels
    /// (<see cref="w"/> x <see cref="h"/>). Used for the HUD and dashboards. With <see cref="res"/> &gt; 1 (HD HUD / HD
    /// MAP) the buffer holds res x res physical pixels per logical one: shapes and the pixel font look the same, while
    /// icons and maps can fill the finer pixels (<see cref="Blit"/> with a res-times larger icon, <see cref="SetFine"/>).</summary>
    public class PixelCanvas
    {
        public int w, h;
        /// <summary>Physical pixels per logical pixel (1 = the pixel-art HUD).</summary>
        public int res = 1;
        /// <summary>Physical size of the buffer and texture.</summary>
        public int pw, ph;
        public Color32[] px;
        public Texture2D texture;

        /// <summary>One recorded line of text (HD text: drawn by <see cref="HudTextGraphic"/> over the canvas).</summary>
        public struct TextRun { public int x, y, scale; public Color32 color; public string text; public float fx, fy, fs; }
        /// <summary>Non-null: <see cref="Text"/> records runs here instead of drawing pixels (the HUD's HD font); a later
        /// opaque rect over half a run hides it, as it would cover the pixels.</summary>
        public List<TextRun> runs;
        /// <summary>Non-null (HD HUD): crisp vector shapes drawn by <see cref="HudVectorGraphic"/> under (back) and over
        /// (front) the pixel image; <see cref="Panel"/> becomes a vector panel while <see cref="HudVector.on"/>.</summary>
        public HudVector vec;

        public PixelCanvas(int width, int height, int resolution = 1) { Resize(width, height, resolution); }

        public void Resize(int width, int height, int resolution = 1)
        {
            w = width; h = height; res = Mathf.Max(1, resolution);
            pw = w * res; ph = h * res;
            px = new Color32[pw * ph];
            if (texture) UnityEngine.Object.Destroy(texture);
            texture = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelCanvas" };
        }

        public void Clear(Color32 c)
        {
            if (c.r == 0 && c.g == 0 && c.b == 0 && c.a == 0) Array.Clear(px, 0, px.Length); else Array.Fill(px, c);
            runs?.Clear(); vec?.Clear();
        }

        public void Upload() { texture.SetPixelData(px, 0); texture.Apply(false); }

        /// <summary>Make a logical rect transparent (an HD panel shows the vector layer beneath).</summary>
        public void ClearRect(int x, int y, int rw, int rh)
        {
            int fx0 = Mathf.Max(0, x * res), fx1 = Mathf.Min(pw, (x + rw) * res), fy0 = Mathf.Max(0, y * res), fy1 = Mathf.Min(ph, (y + rh) * res);
            if (fx1 <= fx0) return;
            for (int fy = fy0; fy < fy1; fy++) Array.Clear(px, (ph - 1 - fy) * pw + fx0, fx1 - fx0);
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            if (res == 1) { Put((h - 1 - y) * w + x, c); return; }
            for (int j = 0; j < res; j++)
            {
                int row = (ph - 1 - (y * res + j)) * pw + x * res;
                for (int i = 0; i < res; i++) Put(row + i, c);
            }
        }

        /// <summary>One physical pixel (fine detail on an HD canvas; the same as <see cref="Set"/> at res 1).</summary>
        public void SetFine(int fx, int fy, Color32 c)
        {
            if (fx < 0 || fy < 0 || fx >= pw || fy >= ph) return;
            Put((ph - 1 - fy) * pw + fx, c);
        }

        void Put(int i, Color32 c)
        {
            if (c.a == 255) { px[i] = c; return; }
            var d = px[i];
            float a = c.a / 255f;
            px[i] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), (byte)Mathf.Max(d.a, c.a));
        }

        /// <summary>Draw an icon (rows bottom-up, alpha-tested) <paramref name="size"/> logical pixels square with its
        /// top-left corner at x, y. An icon rendered at size x <see cref="res"/> fills the physical pixels one to one
        /// (sharp on an HD canvas); one at size is drawn in blocks.</summary>
        public void Blit(int x, int y, int size, Color32[] src) => Blit(x, y, size, src, false);

        /// <summary><see cref="Blit(int,int,int,Color32[])"/>, <paramref name="grey"/>: drained of colour and dimmed (not
        /// available yet).</summary>
        public void Blit(int x, int y, int size, Color32[] src, bool grey)
        {
            if (src == null) return;
            if (grey)
            {
                var g = new Color32[src.Length];
                for (int i = 0; i < src.Length; i++) { var c = src[i]; byte l = (byte)((c.r * 30 + c.g * 59 + c.b * 11) / 100 * 0.55f); g[i] = new Color32(l, l, l, c.a); }
                src = g;
            }
            int n = Mathf.RoundToInt(Mathf.Sqrt(src.Length));
            if (n == size * res && res > 1)
            {
                int ox = x * res, oy = y * res;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var c = src[(n - 1 - j) * n + i];
                    if (c.a > 128) SetFine(ox + i, oy + j, c);
                }
                return;
            }
            if (n != size) return;
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                var c = src[(size - 1 - j) * size + i];
                if (c.a > 128) Set(x + i, y + j, c);
            }
        }

        public void Rect(int x, int y, int rw, int rh, Color32 c)
        {
            if ((runs != null || vec != null) && c.a >= 180 && rw * rh >= 12) Cover(x, y, rw, rh);
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Set(i, j, c);
        }

        public void Frame(int x, int y, int rw, int rh, Color32 c)
        {
            for (int i = x; i < x + rw; i++) { Set(i, y, c); Set(i, y + rh - 1, c); }
            for (int j = y; j < y + rh; j++) { Set(x, j, c); Set(x + rw - 1, j, c); }
        }

        public void Panel(int x, int y, int rw, int rh)
        {
            if (vec != null && vec.on) { VPanel(x, y, rw, rh); return; }
            Rect(x + 2, y + 2, rw, rh, new Color32(8, 12, 12, 100));
            var fill = Pal.Panel; fill.a = 235;
            Rect(x, y, rw, rh, fill);
            Frame(x, y, rw, rh, Pal.PanelEdge);
            for (int i = x + 1; i < x + rw - 1; i++) Set(i, y + 1, Pal.PanelLight);
        }

        /// <summary>HD HUD panel: covers what was drawn under it (pixels, text, front shapes), then a rounded vector panel
        /// with a soft shadow, a gradient fill, an edge and a top highlight on the back layer.</summary>
        public void VPanel(float x, float y, float rw, float rh, float radius = 2.6f, byte alpha = 235)
        {
            Cover(Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.RoundToInt(rw), Mathf.RoundToInt(rh));
            ClearRect(Mathf.CeilToInt(x + 0.5f), Mathf.CeilToInt(y + 0.5f), Mathf.FloorToInt(rw - 1f), Mathf.FloorToInt(rh - 1f));
            vec.Panel(x, y, rw, rh, radius, alpha);
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Dial ticks + needle. t in 0..1 sweeps 270 degrees clockwise from bottom-left.</summary>
        public void Dial(int cx, int cy, int r, float t, Color32 tick, Color32 needle, float redFrom = 2f)
        {
            for (int k = 0; k <= 10; k++)
            {
                float a = (225f - k * 27f) * Mathf.Deg2Rad;
                var col = k / 10f >= redFrom ? new Color32(220, 40, 30, 255) : tick;
                Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * r), cy - Mathf.RoundToInt(Mathf.Sin(a) * r), col);
                if (k % 5 == 0) Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * (r - 1)), cy - Mathf.RoundToInt(Mathf.Sin(a) * (r - 1)), col);
            }
            float na = (225f - Mathf.Clamp01(t) * 270f) * Mathf.Deg2Rad;
            Line(cx, cy, cx + Mathf.RoundToInt(Mathf.Cos(na) * (r - 2)), cy - Mathf.RoundToInt(Mathf.Sin(na) * (r - 2)), needle);
            Set(cx, cy, tick);
        }

        // ------------------------------------------------------------------ font
        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            {'0',"111101101101111"},{'1',"010110010010111"},{'2',"111001111100111"},{'3',"111001111001111"},{'4',"101101111001001"},
            {'5',"111100111001111"},{'6',"111100111101111"},{'7',"111001001001001"},{'8',"111101111101111"},{'9',"111101111001111"},
            {'A',"010101111101101"},{'B',"110101110101110"},{'C',"011100100100011"},{'D',"110101101101110"},{'E',"111100110100111"},
            {'F',"111100110100100"},{'G',"011100101101011"},{'H',"101101111101101"},{'I',"111010010010111"},{'J',"001001001101010"},
            {'K',"101101110101101"},{'L',"100100100100111"},{'M',"101111111101101"},{'N',"110101101101101"},{'O',"010101101101010"},
            {'P',"110101110100100"},{'Q',"010101101110011"},{'R',"110101110101101"},{'S',"011100010001110"},{'T',"111010010010010"},
            {'U',"101101101101111"},{'V',"101101101101010"},{'W',"101101111111101"},{'X',"101101010101101"},{'Y',"101101010010010"},
            {'Z',"111001010100111"},{' ',"000000000000000"},{'.',"000000000000010"},{':',"000010000010000"},{'/',"001001010100100"},
            {'-',"000000111000000"},{'%',"101001010100101"},{'<',"001010100010001"},{'>',"100010001010100"},{'[',"110100100100110"},
            {']',"011001001001011"},{'+',"000010111010000"},{'!',"010010010000010"},{'#',"101111101111101"},{'=',"000111000111000"},{'\'',"010010000000000"},{',',"000000000010100"},{'(',"010100100100010"},{')',"010001001001010"},{'?',"111001010000010"},{'&',"010101010101011"},{'`',"100010000000000"}
        };

        public static int TextWidth(string s, int scale = 1) => s.Length * 4 * scale - scale;

        void Cover(int x, int y, int rw, int rh)
        {
            vec?.Cover(x, y, rw, rh);
            if (runs == null) return;
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                var r = runs[i];
                int tw = TextWidth(r.text, r.scale), th = 5 * r.scale;
                if (r.fs > 0f) { tw = Mathf.CeilToInt(TextWidthF(r.text, r.fs)); th = Mathf.CeilToInt(5f * r.fs); }
                int ox = Mathf.Max(0, Mathf.Min(x + rw, r.x + tw) - Mathf.Max(x, r.x)), oy = Mathf.Max(0, Mathf.Min(y + rh, r.y + th) - Mathf.Max(y, r.y));
                if (ox * oy * 2 >= tw * th) runs.RemoveAt(i);
            }
        }

        public static float TextWidthF(string s, float size) => (s.Length * 4 - 1) * size;

        /// <summary>Text at a fractional position and size (HD HUD). With the HD font it is a smooth run with a fine drop
        /// shadow; with the pixel font it snaps to whole pixels and scales.</summary>
        public float TextF(float x, float y, string s, Color32 c, float size = 1f, bool shadow = true)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            if (runs == null) return Text(Mathf.RoundToInt(x), Mathf.RoundToInt(y), s, c, Mathf.Max(1, Mathf.RoundToInt(size)), shadow);
            s = Upper(s);
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), sc = Mathf.Max(1, Mathf.CeilToInt(size));
            if (shadow)
            {
                float o = Mathf.Max(0.3f, 0.28f * size);
                runs.Add(new TextRun { x = ix, y = iy, scale = sc, color = new Color32(6, 4, 2, (byte)Mathf.Min(200, c.a)), text = s, fx = x + o, fy = y + o, fs = size });
            }
            runs.Add(new TextRun { x = ix, y = iy, scale = sc, color = c, text = s, fx = x, fy = y, fs = size });
            return s.Length * 4 * size;
        }

        static readonly Dictionary<string, string> upper = new Dictionary<string, string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => upper.Clear();
        /// <summary>Upper case without a new string per frame for repeated labels (cache grows only with distinct texts).</summary>
        static string Upper(string s)
        {
            bool lower = false;
            for (int i = 0; i < s.Length && !lower; i++) lower = char.IsLower(s[i]);
            if (!lower) return s;
            if (upper.Count > 4096) upper.Clear();
            if (!upper.TryGetValue(s, out var u)) upper[s] = u = s.ToUpperInvariant();
            return u;
        }

        public int Text(int x, int y, string s, Color32 c, int scale = 1, bool shadow = true)
        {
            s = Upper(s);
            if (runs != null)
            {
                if (shadow) runs.Add(new TextRun { x = x + scale, y = y + scale, scale = scale, color = new Color32(10, 5, 3, 220), text = s });
                runs.Add(new TextRun { x = x, y = y, scale = scale, color = c, text = s });
                return s.Length * 4 * scale;
            }
            if (shadow) Text(x + scale, y + scale, s, new Color32(10, 5, 3, 220), scale, false);
            int cx = x;
            foreach (char ch in s)
            {
                if (Glyphs.TryGetValue(ch, out var g))
                    for (int r = 0; r < 5; r++)
                    for (int col = 0; col < 3; col++)
                        if (g[r * 3 + col] == '1') Rect(cx + col * scale, y + r * scale, scale, scale, c);
                cx += 4 * scale;
            }
            return cx - x;
        }
    }
}
