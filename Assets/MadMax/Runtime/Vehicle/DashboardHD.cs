using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>HD gauge cluster geometry for <see cref="VehicleDashboard"/> (HD pack on): a real mesh in metres instead
    /// of a 64x24 pixel texture, so it stays sharp at any resolution — dial faces with major / minor ticks and a red
    /// zone, needles as child transforms (turned every frame), seven-segment gear and speed readouts, LED fuel and
    /// temperature bars and warning lamps whose vertex colours change. Faces −Z (towards the driver); unlit HDLit with
    /// vertex colours (backlit instruments).</summary>
    public sealed class DashboardHD
    {
        public const float W = 0.3f, H = 0.11f;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color32> c = new List<Color32>();
        readonly List<int> t = new List<int>();
        Color32[] colors;
        Mesh mesh;
        public Transform speedNeedle, rpmNeedle;
        readonly int[][] gearSeg = new int[1][], speedSeg = new int[3][];
        readonly int[] fuelLed = new int[10], tempLed = new int[10];
        readonly int[] lamps = new int[9];
        public static readonly Vector2 SpeedAt = new Vector2(-0.105f, 0.004f), RpmAt = new Vector2(0.105f, 0.004f);
        public const float DialR = 0.046f, Sweep = 270f;

        // seven-segment masks a b c d e f g (bit 0 = a)
        static readonly Dictionary<char, int> Seg = new Dictionary<char, int>
        {
            { '0', 0x3F }, { '1', 0x06 }, { '2', 0x5B }, { '3', 0x4F }, { '4', 0x66 }, { '5', 0x6D }, { '6', 0x7D }, { '7', 0x07 },
            { '8', 0x7F }, { '9', 0x6F }, { 'R', 0x50 }, { 'N', 0x54 }, { '-', 0x40 }, { ' ', 0 },
        };

        public GameObject Build(Transform parent, Material mat, Color32 bg, Color32 rim, Color32 tick, Color32 needle, Color32 red, Color32 off)
        {
            var go = new GameObject("Dashboard");
            go.transform.SetParent(parent, false);
            // backing and bezel
            Quad(-W / 2 - 0.004f, -H / 2 - 0.004f, W / 2 + 0.004f, H / 2 + 0.004f, 0.004f, rim);
            Quad(-W / 2, -H / 2, W / 2, H / 2, 0.002f, bg);
            foreach (var at in new[] { SpeedAt, RpmAt })
            {
                bool rpm = at == RpmAt;
                Disc(at, DialR + 0.003f, 0.0015f, rim, 40);
                Disc(at, DialR, 0.001f, Shade(bg, 0.8f), 40);
                Ring(at, DialR - 0.0025f, DialR - 0.0012f, 0.0005f, Shade(tick, 0.7f), 48);
                for (int k = 0; k <= 40; k++)
                {
                    float f = k / 40f;
                    bool major = k % 5 == 0, hot = rpm && f > 0.85f;
                    float a = Angle(f);
                    Tick(at, a, DialR - (major ? 0.011f : 0.0065f), DialR - 0.003f, major ? 0.0016f : 0.0008f, 0.0003f, hot ? red : major ? tick : Shade(tick, 0.75f));
                }
                if (rpm) Arc(at, DialR - 0.0055f, DialR - 0.003f, Angle(0.85f), Angle(1f), 0.0004f, Shade(red, 0.75f));
                Disc(at, 0.005f, -0.004f, Shade(rim, 1.3f), 16);
            }
            // centre: gear and speed readouts, fuel and temperature LED bars
            Quad(-0.032f, -0.04f, 0.032f, 0.044f, 0.0012f, Shade(bg, 0.7f));
            gearSeg[0] = Digit(0f, 0.022f, 0.026f, off);
            for (int k = 0; k < 3; k++) speedSeg[k] = Digit(-0.0125f + k * 0.0125f, -0.014f, 0.014f, off);
            for (int k = 0; k < 10; k++)
            {
                float y = -0.03f + k * 0.0068f;
                fuelLed[k] = Quad(-0.029f, y, -0.022f, y + 0.0052f, 0f, off);
                tempLed[k] = Quad(0.022f, y, 0.029f, y + 0.0052f, 0f, off);
            }
            // warning lamps along the bottom
            for (int k = 0; k < lamps.Length; k++)
            {
                float x = -0.064f + k * 0.016f;
                Quad(x - 0.0052f, -0.0515f, x + 0.0052f, -0.0425f, 0.0005f, Shade(rim, 0.8f));
                lamps[k] = Quad(x - 0.0042f, -0.0505f, x + 0.0042f, -0.0435f, 0f, off);
            }
            mesh = new Mesh { name = "DashboardHD" };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            colors = c.ToArray();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            speedNeedle = Needle(go.transform, SpeedAt, needle, mat);
            rpmNeedle = Needle(go.transform, RpmAt, needle, mat);
            return go;
        }

        /// <summary>Dial angle (degrees, 0 = up, clockwise) of a 0..1 reading.</summary>
        public static float Angle(float f) => -Sweep / 2f + Mathf.Clamp01(f) * Sweep;

        public void SetNeedle(Transform n, float f) { if (n) n.localRotation = Quaternion.Euler(0f, 0f, -Angle(f)); }

        public void SetText(string gear, string speed, Color32 onGear, Color32 onSpeed, Color32 off)
        {
            Paint(gearSeg[0], gear.Length > 0 ? gear[0] : ' ', onGear, off);
            for (int k = 0; k < 3; k++) Paint(speedSeg[k], k < speed.Length ? speed[k] : ' ', onSpeed, off);
        }

        public void SetBars(float fuel, float temp, Color32 fuelOn, Color32 tempOn, Color32 off)
        {
            for (int k = 0; k < 10; k++)
            {
                Fill(fuelLed[k], fuel * 10f > k + 0.25f ? fuelOn : off);
                Fill(tempLed[k], temp * 10f > k + 0.25f ? tempOn : off);
            }
        }

        public void SetLamp(int index, Color32 col) { if (index >= 0 && index < lamps.Length) Fill(lamps[index], col); }

        public void Upload() { if (mesh) mesh.SetColors(colors); }

        void Paint(int[] seg, char ch, Color32 on, Color32 off)
        {
            Seg.TryGetValue(char.ToUpperInvariant(ch), out int m);
            for (int s = 0; s < 7; s++) Fill(seg[s], (m & (1 << s)) != 0 ? on : off);
        }

        void Fill(int start, Color32 col) { for (int k = 0; k < 4; k++) colors[start + k] = col; }

        static Color32 Shade(Color32 a, float f) => new Color32((byte)Mathf.Min(255, a.r * f), (byte)Mathf.Min(255, a.g * f), (byte)Mathf.Min(255, a.b * f), 255);

        // ---- geometry (x right, y up, z towards the windscreen: smaller z = nearer the driver)
        int Quad(float x0, float y0, float x1, float y1, float z, Color32 col)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, y0, z)); v.Add(new Vector3(x0, y1, z)); v.Add(new Vector3(x1, y1, z)); v.Add(new Vector3(x1, y0, z));
            for (int k = 0; k < 4; k++) c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            return i;
        }

        void Quad4(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 col)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int k = 0; k < 4; k++) c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static Vector3 Polar(Vector2 o, float r, float deg, float z) { float a = deg * Mathf.Deg2Rad; return new Vector3(o.x + Mathf.Sin(a) * r, o.y + Mathf.Cos(a) * r, z); }

        void Disc(Vector2 o, float r, float z, Color32 col, int seg)
        {
            int i0 = v.Count;
            v.Add(new Vector3(o.x, o.y, z)); c.Add(col);
            for (int k = 0; k <= seg; k++) { v.Add(Polar(o, r, k * 360f / seg, z)); c.Add(col); }
            for (int k = 0; k < seg; k++) { t.Add(i0); t.Add(i0 + 1 + k); t.Add(i0 + 2 + k); }
        }

        void Ring(Vector2 o, float r0, float r1, float z, Color32 col, int seg) => Arc(o, r0, r1, 0f, 360f, z, col, seg);

        void Arc(Vector2 o, float r0, float r1, float a0, float a1, float z, Color32 col, int seg = 16)
        {
            for (int k = 0; k < seg; k++)
            {
                float u0 = Mathf.Lerp(a0, a1, k / (float)seg), u1 = Mathf.Lerp(a0, a1, (k + 1) / (float)seg);
                Quad4(Polar(o, r0, u0, z), Polar(o, r1, u0, z), Polar(o, r1, u1, z), Polar(o, r0, u1, z), col);
            }
        }

        void Tick(Vector2 o, float deg, float r0, float r1, float width, float z, Color32 col)
        {
            var dir = Polar(Vector2.zero, 1f, deg, 0f); var side = new Vector3(dir.y, -dir.x, 0f) * (width / 2f);
            var a = new Vector3(o.x, o.y, z) + dir * r0; var b = new Vector3(o.x, o.y, z) + dir * r1;
            Quad4(a - side, b - side, b + side, a + side, col);
        }

        /// <summary>Seven segments a..g of one digit (centre x, y, height); returns the first vertex of each.</summary>
        int[] Digit(float x, float y, float h, Color32 off)
        {
            float w = h * 0.55f, th = h * 0.13f, hw = w / 2f, hh = h / 2f;
            var s = new int[7];
            s[0] = Quad(x - hw + th, y + hh - th, x + hw - th, y + hh, 0f, off);              // a
            s[1] = Quad(x + hw - th, y + th / 2, x + hw, y + hh - th, 0f, off);               // b
            s[2] = Quad(x + hw - th, y - hh + th, x + hw, y - th / 2, 0f, off);               // c
            s[3] = Quad(x - hw + th, y - hh, x + hw - th, y - hh + th, 0f, off);              // d
            s[4] = Quad(x - hw, y - hh + th, x - hw + th, y - th / 2, 0f, off);               // e
            s[5] = Quad(x - hw, y + th / 2, x - hw + th, y + hh - th, 0f, off);               // f
            s[6] = Quad(x - hw + th, y - th / 2, x + hw - th, y + th / 2, 0f, off);           // g
            return s;
        }

        static Transform Needle(Transform parent, Vector2 at, Color32 col, Material mat)
        {
            var go = new GameObject("Needle");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, at.y, -0.002f);
            var m = new Mesh { name = "Needle" };
            float len = DialR - 0.006f;
            m.SetVertices(new List<Vector3> { new Vector3(-0.0014f, -0.008f, 0f), new Vector3(0f, len, 0f), new Vector3(0.0014f, -0.008f, 0f) });
            m.SetColors(new List<Color32> { col, col, col });
            m.SetTriangles(new[] { 0, 1, 2 }, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
