using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Voxel title art: the slanted 3D arcade logo and the neon garage sign ("MAD MIKE'S GARAGE").</summary>
    public static class TitleArt
    {
        // 5x7 bitmap letters (rows top to bottom)
        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            { 'M', new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" } },
            { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
            { 'I', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" } },
            { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
            { 'G', new[] { "01111", "10000", "10000", "10011", "10001", "10001", "01111" } },
            { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
            { 'N', new[] { "10001", "11001", "10101", "10101", "10011", "10001", "10001" } },
            { '\'', new[] { "00100", "00100", "01000", "00000", "00000", "00000", "00000" } },
            { ' ', new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" } },
        };

        static readonly Color32[] Sunset = { Pal.Hex("fff07a"), Pal.Hex("f8d040"), Pal.Hex("f4a030"), Pal.Hex("ee7424"), Pal.Hex("d8401e"), Pal.Hex("b02818"), Pal.Hex("7e1c14") };

        /// <summary>Stamp text into a grid. Each font pixel = px voxels; shear = italic slant (x shift per voxel of height).</summary>
        static void Stamp(VoxelGrid g, string text, int px, int depth, int x0, int y0, float shear, System.Func<int, int, int, Color32> color)
        {
            int cx = x0;
            foreach (char ch in text.ToUpperInvariant())
            {
                if (!Font.TryGetValue(ch, out var rows)) { cx += 6 * px; continue; }
                for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                {
                    if (rows[r][c] != '1') continue;
                    for (int sy = 0; sy < px; sy++)
                    for (int sx = 0; sx < px; sx++)
                    {
                        int y = y0 + (6 - r) * px + sy;
                        int x = cx + c * px + sx + Mathf.RoundToInt((y - y0) * shear);
                        for (int z = 0; z < depth; z++) g.Set(x, y, z, _ => color(y - y0, z, r));
                    }
                }
                cx += (ch == '\'' ? 3 : 6) * px;
            }
        }

        static int Width(string text, int px)
        {
            int w = 0;
            foreach (char ch in text) w += (ch == '\'' ? 3 : 6) * px;
            return w - px;
        }

        /// <summary>Two-line chunky logo: sunset gradient faces, dark red extrusion, chrome top lip; slanted like an 80s arcade marquee.</summary>
        public static Mesh Logo()
        {
            if (MadMax.Rendering.HDBits.On) return LogoHD();
            var g = new VoxelGrid();
            const int px = 2, depth = 5;
            string top = "MAD MIKE'S", bottom = "GARAGE";
            int wTop = Width(top, px), wBot = Width(bottom, px + 1);
            System.Func<int, int, int, Color32> face(int h) => (y, z, r) =>
            {
                if (z > 0) return z == depth - 1 ? Pal.Hex("3a0c08") : Pal.Hex("6a1810");
                if (r == 0 && y % px == px - 1) return Pal.Hex("fffbe0");
                return Sunset[Mathf.Clamp(Mathf.FloorToInt((1f - y / (float)h) * (Sunset.Length - 1)), 0, Sunset.Length - 1)];
            };
            Stamp(g, top, px, depth, -wTop / 2, 7 * (px + 1) + 4, 0.38f, face(7 * px));
            Stamp(g, bottom, px + 1, depth, -wBot / 2 - 3, 0, 0.38f, face(7 * (px + 1)));
            // underline swoosh
            for (int x = -wBot / 2 - 6; x <= wBot / 2 + 10; x++)
                for (int z = 0; z < depth - 1; z++) { g.Set(x, -3, z, Pal.Solid(z == 0 ? Pal.Hex("30c8e0") : Pal.Hex("0e3a48"))); if (x % 3 != 0) g.Set(x, -4, z, Pal.Solid(z == 0 ? Pal.Hex("1890b0") : Pal.Hex("0e3a48"))); }
            g.Bevel(0.18f, 0.2f);
            return VoxelMesher.Build(g, "TitleLogo");
        }

        /// <summary>Garage sign: steel posts and a dark board (frame mesh) with neon tube letters (neon mesh).</summary>
        public static (Mesh frame, Mesh neon) NeonSign()
        {
            if (MadMax.Rendering.HDBits.On) return NeonHD();
            var frame = new VoxelGrid();
            var steel = Pal.Ramp(Pal.Metal, 1, 1601);
            foreach (int x in new[] { -46, 46 }) frame.Box(x - 1, 0, -1, x + 1, 74, 1, steel);
            frame.Box(-48, 30, -2, 48, 74, -1, p => (p.x % 12 == 0 || p.y % 11 == 0) ? Pal.Metal[0] : Pal.Pick(Pal.Black, p, 1602, 2));
            frame.Box(-48, 29, -3, 48, 29, 0, Pal.Ramp(Pal.Rust, 1));
            foreach (int x in new[] { -30, 0, 30 }) frame.Box(x, 75, -1, x, 80, -1, Pal.Ramp(Pal.Metal, 2));   // lamp brackets
            frame.Bevel();

            var neon = new VoxelGrid();
            var pink = Pal.Hex("ff3cc8"); var cyan = Pal.Hex("3cf0ff"); var yellow = Pal.Hex("ffe040"); var green = Pal.Hex("60ff60");
            Stamp(neon, "MAD MIKE'S", 1, 1, -Width("MAD MIKE'S", 1) / 2, 60, 0.15f, (y, z, r) => pink);
            Stamp(neon, "GARAGE", 2, 1, -Width("GARAGE", 2) / 2, 38, 0.15f, (y, z, r) => cyan);
            for (int x = -46; x <= 46; x++) { neon.Set(x, 32, 0, Pal.Solid(yellow)); neon.Set(x, 72, 0, Pal.Solid(yellow)); }
            for (int y = 32; y <= 72; y++) { neon.Set(-46, y, 0, Pal.Solid(yellow)); neon.Set(46, y, 0, Pal.Solid(yellow)); }
            Stamp(neon, "OPEN", 1, 1, 30, 34, 0f, (y, z, r) => green);
            // wrench icon
            for (int i = -6; i <= 6; i++) neon.Set(-36 + i, 52 + i, 0, Pal.Solid(yellow));
            foreach (var o in new[] { new Vector2Int(-43, 45), new Vector2Int(-29, 59) }) { neon.Set(o.x - 1, o.y, 0, Pal.Solid(yellow)); neon.Set(o.x + 1, o.y, 0, Pal.Solid(yellow)); neon.Set(o.x, o.y - 1, 0, Pal.Solid(yellow)); neon.Set(o.x, o.y + 1, 0, Pal.Solid(yellow)); }
            return (VoxelMesher.Build(frame, "NeonSignFrame"), VoxelMesher.Build(neon, "NeonSignTubes"));
        }
    

        // ------------------------------------------------------------------ HD (HD pack on)
        const float S = VoxelMesher.DefaultSize;

        /// <summary>Glyph runs of a line: (x0, x1, y) in voxel units, one per horizontal run of lit font pixels.</summary>
        static List<(float x0, float x1, float y, int r)> Runs(string text, int px, int x0, int y0)
        {
            var l = new List<(float, float, float, int)>();
            int cx = x0;
            foreach (char ch in text.ToUpperInvariant())
            {
                if (!Font.TryGetValue(ch, out var rows)) { cx += 6 * px; continue; }
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                    {
                        if (rows[r][c] != '1' || (c > 0 && rows[r][c - 1] == '1')) continue;
                        int e = c; while (e + 1 < 5 && rows[r][e + 1] == '1') e++;
                        l.Add((cx + c * px - 0.5f, cx + (e + 1) * px - 0.5f, y0 + (6 - r) * px - 0.5f, r));
                    }
                cx += (ch == '\'' ? 3 : 6) * px;
            }
            return l;
        }

        static void Shear(MadMax.Rendering.HDShapes b, int from, float y0, float shear)
        {
            for (int i = from; i < b.v.Count; i++) { var v = b.v[i]; v.x += (v.y - y0 * S) * shear; b.v[i] = v; }
        }

        /// <summary>HD logo: each run of font pixels a bevelled bar, sunset face over a dark red extrusion, a cream lip on
        /// the top rows, sheared like the arcade marquee; the cyan swoosh under it.</summary>
        static Mesh LogoHD()
        {
            var b = new MadMax.Rendering.HDShapes();
            const int px = 2, depth = 5;
            string top = "MAD MIKE'S", bottom = "GARAGE";
            int wTop = Width(top, px), wBot = Width(bottom, px + 1);
            void Line(string text, int p, int x0, int y0)
            {
                int from = b.v.Count;
                float h = 7 * p;
                foreach (var (a, e, y, r) in Runs(text, p, x0, y0))
                {
                    float cy = y + p * 0.5f, cxm = (a + e) * 0.5f;
                    var face = Sunset[Mathf.Clamp(Mathf.FloorToInt((1f - (cy - y0) / h) * (Sunset.Length - 1)), 0, Sunset.Length - 1)];
                    b.Box(new Vector3(cxm, cy, 0.1f) * S, new Vector3(e - a, p, 1.2f) * S, face, 0.28f * S);
                    b.Box(new Vector3(cxm, cy, depth * 0.5f + 0.3f) * S, new Vector3(e - a - 0.15f, p - 0.15f, depth - 1f) * S, Pal.Hex("6a1810"), 0.22f * S);
                    if (r == 0) b.Box(new Vector3(cxm, cy + p * 0.42f, -0.35f) * S, new Vector3(e - a - 0.2f, 0.28f, 0.3f) * S, Pal.Hex("fffbe0"), 0.1f * S);
                }
                Shear(b, from, y0, 0.38f);
            }
            Line(top, px, -wTop / 2, 7 * (px + 1) + 4);
            Line(bottom, px + 1, -wBot / 2 - 3, 0);
            float sx0 = -wBot / 2 - 6, sx1 = wBot / 2 + 10;
            b.Box(new Vector3((sx0 + sx1) * 0.5f, -3f, 1f) * S, new Vector3(sx1 - sx0, 0.9f, 3f) * S, Pal.Hex("30c8e0"), 0.3f * S);
            b.Box(new Vector3((sx0 + sx1) * 0.5f + 1f, -4.1f, 1.2f) * S, new Vector3(sx1 - sx0 - 2f, 0.6f, 2.6f) * S, Pal.Hex("1890b0"), 0.2f * S);
            return b.ToMesh("TitleLogoHD");
        }

        /// <summary>HD garage sign: steel posts and board, neon as round tubes along the letter strokes (joints as
        /// beads), the yellow frame, OPEN and the wrench.</summary>
        static (Mesh frame, Mesh neon) NeonHD()
        {
            var f = new MadMax.Rendering.HDShapes();
            foreach (int x in new[] { -46, 46 }) f.Box(new Vector3(x, 37f, 0f) * S, new Vector3(3f, 75f, 3f) * S, Pal.Metal[2], 0.6f * S);
            f.Box(new Vector3(0f, 52f, -1.5f) * S, new Vector3(97f, 45f, 2f) * S, Pal.Black[1], 0.5f * S);
            f.Box(new Vector3(0f, 29f, -1.5f) * S, new Vector3(97f, 1f, 4f) * S, Pal.Rust[2], 0.3f * S);
            foreach (int x in new[] { -30, 0, 30 }) f.Tube(new Vector3(x, 75f, -1f) * S, new Vector3(x, 80f, -1f) * S, 0.5f * S, 0.4f * S, Pal.Metal[2], 6, true);
            var n = new MadMax.Rendering.HDShapes();
            var pink = Pal.Hex("ff3cc8"); var cyan = Pal.Hex("3cf0ff"); var yellow = Pal.Hex("ffe040"); var green = Pal.Hex("60ff60");
            void Tubes(string text, int px, int x0, int y0, float shear, Color32 col)
            {
                int cx = x0;
                foreach (char ch in text.ToUpperInvariant())
                {
                    if (!Font.TryGetValue(ch, out var rows)) { cx += 6 * px; continue; }
                    Vector3 P(int r, int c) { float y = y0 + (6 - r) * px + (px - 1) * 0.5f; return new Vector3(cx + c * px + (px - 1) * 0.5f + (y - y0) * shear, y, 0f) * S; }
                    for (int r = 0; r < 7; r++)
                        for (int c = 0; c < 5; c++)
                        {
                            if (rows[r][c] != '1') continue;
                            n.Ellipsoid(P(r, c), Vector3.one * 0.42f * S * px, col, 6, 4);
                            if (c + 1 < 5 && rows[r][c + 1] == '1') n.Tube(P(r, c), P(r, c + 1), 0.38f * S * px, 0.38f * S * px, col, 6);
                            if (r + 1 < 7 && rows[r + 1][c] == '1') n.Tube(P(r, c), P(r + 1, c), 0.38f * S * px, 0.38f * S * px, col, 6);
                        }
                    cx += (ch == '\'' ? 3 : 6) * px;
                }
            }
            Tubes("MAD MIKE'S", 1, -Width("MAD MIKE'S", 1) / 2, 60, 0.15f, pink);
            Tubes("GARAGE", 2, -Width("GARAGE", 2) / 2, 38, 0.15f, cyan);
            Tubes("OPEN", 1, 30, 34, 0f, green);
            float r0 = 0.45f * S;
            n.Tube(new Vector3(-46, 32, 0) * S, new Vector3(46, 32, 0) * S, r0, r0, yellow, 6); n.Tube(new Vector3(-46, 72, 0) * S, new Vector3(46, 72, 0) * S, r0, r0, yellow, 6);
            n.Tube(new Vector3(-46, 32, 0) * S, new Vector3(-46, 72, 0) * S, r0, r0, yellow, 6); n.Tube(new Vector3(46, 32, 0) * S, new Vector3(46, 72, 0) * S, r0, r0, yellow, 6);
            n.Tube(new Vector3(-42, 46, 0) * S, new Vector3(-30, 58, 0) * S, r0, r0, yellow, 6);
            foreach (var o in new[] { new Vector3(-43, 45, 0), new Vector3(-29, 59, 0) }) n.Ellipsoid(o * S, Vector3.one * 1.3f * S, yellow, 8, 5);
            return (f.ToMesh("NeonSignFrameHD"), n.ToMesh("NeonSignTubesHD"));
        }
    }
}
