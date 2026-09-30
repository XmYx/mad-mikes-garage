using System;
using MadMax.Voxel;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>CPU pixel buffer with a 3x5 bitmap font. Coordinates are top-left origin. Used for the HUD and dashboards.</summary>
    public class PixelCanvas
    {
        public int w, h;
        public Color32[] px;
        public Texture2D texture;

        public PixelCanvas(int width, int height) { Resize(width, height); }

        public void Resize(int width, int height)
        {
            w = width; h = height;
            px = new Color32[w * h];
            if (texture) UnityEngine.Object.Destroy(texture);
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelCanvas" };
        }

        public void Clear(Color32 c) => Array.Fill(px, c);

        public void Upload() { texture.SetPixels32(px); texture.Apply(false); }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = (h - 1 - y) * w + x;
            if (c.a == 255) { px[i] = c; return; }
            var d = px[i];
            float a = c.a / 255f;
            px[i] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), (byte)Mathf.Max(d.a, c.a));
        }

        /// <summary>Draw an icon (rows bottom-up, alpha-tested) with its top-left corner at x, y.</summary>
        public void Blit(int x, int y, int size, Color32[] src)
        {
            if (src == null) return;
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                var c = src[(size - 1 - j) * size + i];
                if (c.a > 128) Set(x + i, y + j, c);
            }
        }

        public void Rect(int x, int y, int rw, int rh, Color32 c)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Set(i, j, c);
        }

        public void Frame(int x, int y, int rw, int rh, Color32 c)
        {
            for (int i = x; i < x + rw; i++) { Set(i, y, c); Set(i, y + rh - 1, c); }
            for (int j = y; j < y + rh; j++) { Set(x, j, c); Set(x + rw - 1, j, c); }
        }

        public void Panel(int x, int y, int rw, int rh)
        {
            Rect(x + 2, y + 2, rw, rh, new Color32(8, 12, 12, 100));
            var fill = Pal.Panel; fill.a = 235;
            Rect(x, y, rw, rh, fill);
            Frame(x, y, rw, rh, Pal.PanelEdge);
            for (int i = x + 1; i < x + rw - 1; i++) Set(i, y + 1, Pal.PanelLight);
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
            {']',"011001001001011"},{'+',"000010111010000"},{'!',"010010010000010"},{'#',"101111101111101"},{'=',"000111000111000"},{'\'',"010010000000000"},{',',"000000000010100"},{'(',"010100100100010"},{')',"010001001001010"},{'?',"111001010000010"},{'&',"010101010101011"}
        };

        public static int TextWidth(string s, int scale = 1) => s.Length * 4 * scale - scale;

        public int Text(int x, int y, string s, Color32 c, int scale = 1, bool shadow = true)
        {
            s = s.ToUpperInvariant();
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
