using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Mesh builder for instrument clusters: flat-shaded quads with explicit normals and sRGB vertex colours,
    /// discs, arcs, tubes, cones, boxes, a 5x7 dot font, lamp icons and seven-segment digits. Cluster axes: +x right, +y
    /// up, +z into the dashboard (the driver looks along +z, faces point at −z). <see cref="xf"/> transforms everything
    /// added (drum faces).</summary>
    public sealed class DashMesh
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Color32> c = new List<Color32>();
        readonly List<int> t = new List<int>();
        public Matrix4x4 xf = Matrix4x4.identity;
        static readonly Vector3 Front = new Vector3(0f, 0f, -1f);

        public int Count => v.Count;
        public void Clear() { v.Clear(); n.Clear(); c.Clear(); t.Clear(); xf = Matrix4x4.identity; }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ primitives
        /// <summary>A triangle facing <paramref name="normal"/> (winding fixed up).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color32 col, Vector3 normal)
        {
            a = xf.MultiplyPoint3x4(a); b = xf.MultiplyPoint3x4(b); cc = xf.MultiplyPoint3x4(cc);
            normal = xf.MultiplyVector(normal).normalized;
            if (Vector3.Dot(Vector3.Cross(b - a, cc - b), normal) < 0f) { var s = b; b = cc; cc = s; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc);
            for (int k = 0; k < 3; k++) { n.Add(normal); c.Add(col); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>A quad a-b-c-d (in order round the edge) facing <paramref name="normal"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 col, Vector3 normal)
        {
            a = xf.MultiplyPoint3x4(a); b = xf.MultiplyPoint3x4(b); cc = xf.MultiplyPoint3x4(cc); d = xf.MultiplyPoint3x4(d);
            normal = xf.MultiplyVector(normal).normalized;
            if (Vector3.Dot(Vector3.Cross(b - a, cc - b), normal) < 0f) { var s = b; b = d; d = s; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int k = 0; k < 4; k++) { n.Add(normal); c.Add(col); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>A rectangle in the plane z, facing the driver.</summary>
        public void Rect(float x0, float y0, float x1, float y1, float z, Color32 col) =>
            Quad(new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), col, Front);

        /// <summary>Point at <paramref name="deg"/> (0 = up, clockwise as the driver sees it) and radius r round o.</summary>
        public static Vector3 Polar(Vector2 o, float r, float deg, float z)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(o.x + Mathf.Sin(a) * r, o.y + Mathf.Cos(a) * r, z);
        }

        public void Disc(Vector2 o, float r, float z, Color32 col, int seg = 32)
        {
            var ctr = new Vector3(o.x, o.y, z);
            for (int k = 0; k < seg; k++) Tri(ctr, Polar(o, r, k * 360f / seg, z), Polar(o, r, (k + 1) * 360f / seg, z), col, Front);
        }

        public void Arc(Vector2 o, float r0, float r1, float a0, float a1, float z, Color32 col, int seg = 16)
        {
            for (int k = 0; k < seg; k++)
            {
                float u0 = Mathf.Lerp(a0, a1, k / (float)seg), u1 = Mathf.Lerp(a0, a1, (k + 1) / (float)seg);
                Quad(Polar(o, r0, u0, z), Polar(o, r1, u0, z), Polar(o, r1, u1, z), Polar(o, r0, u1, z), col, Front);
            }
        }

        public void Tick(Vector2 o, float deg, float r0, float r1, float width, float z, Color32 col)
        {
            var dir = Polar(Vector2.zero, 1f, deg, 0f);
            var side = new Vector3(dir.y, -dir.x, 0f) * (width / 2f);
            var a = new Vector3(o.x, o.y, z) + dir * r0; var b = new Vector3(o.x, o.y, z) + dir * r1;
            Quad(a - side, b - side, b + side, a + side, col, Front);
        }

        /// <summary>Shade function for round metal: brightness by the direction a facet looks (light from the top left).</summary>
        public delegate Color32 Shade(float deg);

        /// <summary>Cylinder wall of radius r from z0 to z1 (outward normals, or inward).</summary>
        public void Tube(Vector2 o, float r, float z0, float z1, Shade col, int seg = 32, bool inward = false)
        {
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * 360f / seg, a1 = (k + 1) * 360f / seg, mid = (a0 + a1) * 0.5f;
                var nr = Polar(Vector2.zero, 1f, mid, 0f);
                Quad(Polar(o, r, a0, z0), Polar(o, r, a0, z1), Polar(o, r, a1, z1), Polar(o, r, a1, z0), col(mid), inward ? -nr : nr);
            }
        }

        /// <summary>Sloped ring from (r0, z0) to (r1, z1): a bezel face; its normal leans outwards and towards the driver.</summary>
        public void Cone(Vector2 o, float r0, float z0, float r1, float z1, Shade col, int seg = 32)
        {
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * 360f / seg, a1 = (k + 1) * 360f / seg, mid = (a0 + a1) * 0.5f;
                var radial = Polar(Vector2.zero, 1f, mid, 0f);
                var along = new Vector3(radial.x * (r1 - r0), radial.y * (r1 - r0), z1 - z0);
                var tangent = new Vector3(radial.y, -radial.x, 0f);
                var nr = Vector3.Cross(along, tangent).normalized;
                if (nr.z > 0f) nr = -nr;                                                      // towards the driver
                Quad(Polar(o, r0, a0, z0), Polar(o, r1, a0, z1), Polar(o, r1, a1, z1), Polar(o, r0, a1, z0), col(mid), nr);
            }
        }

        /// <summary>An axis-aligned box (all six faces outward), the front face lighter, the bottom darker.</summary>
        public void Box(Vector3 min, Vector3 max, Color32 col, bool back = true)
        {
            Color32 front = Tone(col, 1.1f), top = Tone(col, 1.2f), side = Tone(col, 0.85f), under = Tone(col, 0.6f);
            Vector3 a = min, b = max;
            Quad(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), front, Front);
            if (back) Quad(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), side, Vector3.forward);
            Quad(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), top, Vector3.up);
            Quad(new Vector3(a.x, a.y, a.z), new Vector3(a.x, a.y, b.z), new Vector3(b.x, a.y, b.z), new Vector3(b.x, a.y, a.z), under, Vector3.down);
            Quad(new Vector3(a.x, a.y, a.z), new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), side, Vector3.left);
            Quad(new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), side, Vector3.right);
        }

        /// <summary>A short cylinder along z (knob, screw head, hub cap) with a flat front.</summary>
        public void Puck(Vector2 o, float r, float z0, float z1, Color32 col, int seg = 12)
        {
            Tube(o, r, z0, z1, d => Tone(col, 0.75f + 0.35f * Mathf.Cos((d + 40f) * Mathf.Deg2Rad)), seg);
            Disc(o, r, Mathf.Min(z0, z1), Tone(col, 1.1f), seg);
        }

        public static Color32 Tone(Color32 a, float f) => new Color32((byte)Mathf.Min(255, a.r * f), (byte)Mathf.Min(255, a.g * f), (byte)Mathf.Min(255, a.b * f), 255);

        /// <summary>A ramp entry by the facet direction: bright up-left, dark down-right (chrome reflections).</summary>
        public static Color32 Ramp(Color32[] ramp, float deg, float bias = 0f)
        {
            float f = Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos((deg + 45f) * Mathf.Deg2Rad) + bias);
            return ramp[Mathf.Clamp(Mathf.RoundToInt(f * (ramp.Length - 1)), 0, ramp.Length - 1)];
        }

        // ------------------------------------------------------------------ 5x7 dot font and lamp icons
        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            { '0', new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
            { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new[] { "11111", "00010", "00100", "00010", "00001", "10001", "01110" } },
            { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
            { '5', new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" } },
            { '6', new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" } },
            { '7', new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
            { '8', new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" } },
            { '9', new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" } },
            { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'B', new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" } },
            { 'C', new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" } },
            { 'D', new[] { "11100", "10010", "10001", "10001", "10001", "10010", "11100" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
            { 'G', new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01111" } },
            { 'H', new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'I', new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" } },
            { 'J', new[] { "00111", "00010", "00010", "00010", "00010", "10010", "01100" } },
            { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
            { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
            { 'M', new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" } },
            { 'N', new[] { "10001", "10001", "11001", "10101", "10011", "10001", "10001" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
            { 'Q', new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" } },
            { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
            { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
            { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
            { 'U', new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'V', new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" } },
            { 'W', new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" } },
            { 'X', new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" } },
            { 'Y', new[] { "10001", "10001", "10001", "01010", "00100", "00100", "00100" } },
            { 'Z', new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" } },
            { '/', new[] { "00001", "00001", "00010", "00100", "01000", "10000", "10000" } },
            { '-', new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" } },
            { '.', new[] { "00000", "00000", "00000", "00000", "00000", "01100", "01100" } },
            { '+', new[] { "00000", "00100", "00100", "11111", "00100", "00100", "00000" } },
            { '!', new[] { "00100", "00100", "00100", "00100", "00100", "00000", "00100" } },
            { 'x', new[] { "00000", "00000", "10001", "01010", "00100", "01010", "10001" } },
        };

        /// <summary>Lamp symbols (9x7 dots) by lamp; lamps without one show letters.</summary>
        public static readonly Dictionary<DashLamp, string[]> Icons = new Dictionary<DashLamp, string[]>
        {
            { DashLamp.Charge,  new[] { "011000110", "111111111", "101000001", "111101111", "101000001", "100000001", "111111111" } },
            { DashLamp.Oil,     new[] { "000011100", "000001000", "110111111", "011111010", "001111000", "001111001", "000000001" } },
            { DashLamp.Temp,    new[] { "000110000", "000111100", "000110000", "000111100", "000110000", "101111010", "010110101" } },
            { DashLamp.Fuel,    new[] { "111110000", "100010100", "100010010", "111110010", "111110110", "111110100", "111111000" } },
            { DashLamp.Beam,    new[] { "001110111", "010001000", "100001111", "100001000", "100001111", "010001000", "001110111" } },
            { DashLamp.Cold,    new[] { "010001000", "101010101", "101010101", "101010101", "101010101", "000100010", "000000000" } },
            { DashLamp.Engine,  new[] { "000111000", "000010000", "001111111", "111000001", "101000001", "111000001", "001111111" } },
            { DashLamp.Brake,   new[] { "001111100", "010000010", "100010001", "100010001", "100000001", "010010010", "001111100" } },
            { DashLamp.Wheel,   new[] { "001111100", "010000010", "100101001", "101000101", "100101001", "010000010", "001111100" } },
            { DashLamp.Fire,    new[] { "000010000", "000110000", "001110100", "011111010", "011111110", "111010111", "011111110" } },
            { DashLamp.Mud,     new[] { "000000000", "101010101", "010101010", "000000000", "101010101", "010101010", "111111111" } },
            { DashLamp.Service, new[] { "100000011", "110000110", "011001100", "001111000", "000110000", "001100000", "011000000" } },
            { DashLamp.DiffLock,new[] { "100000001", "100000001", "111111111", "100111001", "100101001", "100111001", "100000001" } },
        };

        public static float TextWidth(string s, float h) => s.Length == 0 ? 0f : (s.Length * 6 - 1) * h / 7f;

        /// <summary>Text of cap height <paramref name="h"/> centred on (cx, cy) in the plane z.</summary>
        public void Text(string s, float cx, float cy, float h, float z, Color32 col)
        {
            float cell = h / 7f, x = cx - TextWidth(s, h) / 2f, top = cy + h / 2f;
            foreach (char ch0 in s)
            {
                char ch = ch0 == 'x' ? 'x' : char.ToUpperInvariant(ch0);
                if (Font.TryGetValue(ch, out var g)) Dots(g, x, top, cell, z, col);
                x += cell * 6f;
            }
        }

        /// <summary>A dot bitmap (rows top to bottom) with its top-left corner at (x, top); runs merged into quads.</summary>
        public void Dots(string[] rows, float x, float top, float cell, float z, Color32 col)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                int k = 0;
                while (k < row.Length)
                {
                    if (row[k] != '1') { k++; continue; }
                    int e = k;
                    while (e < row.Length && row[e] == '1') e++;
                    Rect(x + k * cell, top - (r + 1) * cell, x + e * cell, top - r * cell, z, col);
                    k = e;
                }
            }
        }

        /// <summary>A lamp symbol (or the letters) centred on (cx, cy) fitting <paramref name="size"/>.</summary>
        public void Icon(DashLamp lamp, float cx, float cy, float size, float z, Color32 col)
        {
            if (Icons.TryGetValue(lamp, out var rows))
            {
                float cell = size / 9f;
                Dots(rows, cx - cell * 4.5f, cy + cell * 3.5f, cell, z, col);
                return;
            }
            string s = lamp == DashLamp.FourWD ? "4x4" : lamp == DashLamp.Air ? "AIR" : lamp.ToString().Substring(0, 1);
            float h = Mathf.Min(size * 0.55f, size / Mathf.Max(1f, (s.Length * 6 - 1) / 7f));
            Text(s, cx, cy, h, z, col);
        }

        // ------------------------------------------------------------------ seven segments
        static readonly Dictionary<char, int> Seg = new Dictionary<char, int>
        {
            { '0', 0x3F }, { '1', 0x06 }, { '2', 0x5B }, { '3', 0x4F }, { '4', 0x66 }, { '5', 0x6D }, { '6', 0x7D }, { '7', 0x07 },
            { '8', 0x7F }, { '9', 0x6F }, { 'R', 0x50 }, { 'N', 0x54 }, { 'P', 0x73 }, { '-', 0x40 }, { ' ', 0 },
        };

        /// <summary>The characters a seven-segment readout can show, in glyph-mesh order.</summary>
        public const string SegChars = "0123456789-RNP ";

        /// <summary>Seven-segment digit of height h centred on (x, y), italic like a VFD; <paramref name="all"/> draws
        /// every segment (the unlit ghost "8").</summary>
        public void Segments(char ch, float x, float y, float h, float z, Color32 col, bool all = false)
        {
            int m = 0x7F;
            if (!all) Seg.TryGetValue(char.ToUpperInvariant(ch), out m);
            float w = h * 0.55f, th = h * 0.12f, hw = w / 2f, hh = h / 2f, gap = th * 0.25f;
            const float slant = 0.12f;
            void S(int bit, float x0, float y0, float x1, float y1)
            {
                if ((m & (1 << bit)) == 0) return;
                Quad(new Vector3(x + x0 + y0 * slant, y + y0, z), new Vector3(x + x0 + y1 * slant, y + y1, z),
                     new Vector3(x + x1 + y1 * slant, y + y1, z), new Vector3(x + x1 + y0 * slant, y + y0, z), col, Front);
            }
            S(0, -hw + th + gap, hh - th, hw - th - gap, hh);                 // a
            S(1, hw - th, th / 2 + gap, hw, hh - th - gap);                   // b
            S(2, hw - th, -hh + th + gap, hw, -th / 2 - gap);                 // c
            S(3, -hw + th + gap, -hh, hw - th - gap, -hh + th);               // d
            S(4, -hw, -hh + th + gap, -hw + th, -th / 2 - gap);               // e
            S(5, -hw, th / 2 + gap, -hw + th, hh - th - gap);                 // f
            S(6, -hw + th + gap, -th / 2, hw - th - gap, th / 2);             // g
        }
    }
}
