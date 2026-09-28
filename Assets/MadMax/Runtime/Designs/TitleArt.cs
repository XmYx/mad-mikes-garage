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
    }
}
