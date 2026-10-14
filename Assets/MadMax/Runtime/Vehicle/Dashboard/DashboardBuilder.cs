using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Scale-dependent numbers of one cluster: speed unit and top of the speedo, tacho range from the engine.</summary>
    public struct DashScale
    {
        public bool metric;
        public float speedMax, rpmMax, rpmRed;
        public string Key => (metric ? "k" : "m") + speedMax.ToString("0") + ":" + rpmMax.ToString("0") + ":" + rpmRed.ToString("0");
    }

    /// <summary>One live instrument of a built cluster (what <see cref="VehicleDashboard"/> moves).</summary>
    public sealed class DashInstrument
    {
        public GaugeDef def;
        public float min, max;
        public float sweepFrom, sweepTo;          // dial angles of min and max
        public Transform needle;
        public Transform[] wheels;                 // drums
        public MeshFilter[] digits;                // digital readouts
        public Mesh[] glyphs;                      // per DashMesh.SegChars
        public MeshFilter level;                   // bars / LEDs
        public Mesh[] levels;
        // state
        public float shown, vel, drawn = -9999f;
        public int shownLevel = -1;
        public char[] shownChars;
        public float[] wheelAngle;
    }

    /// <summary>A warning lamp of a built cluster: its renderer and the state shown last.</summary>
    public sealed class DashLampView
    {
        public DashLamp lamp;
        public Renderer renderer;
        public int shown = -1;
    }

    /// <summary>Everything one built cluster consists of.</summary>
    public sealed class DashParts
    {
        public GameObject root;
        public MeshFilter face;
        public Renderer faceRenderer;
        public readonly List<Renderer> illuminated = new List<Renderer>();     // face + needles: backlight tint
        public readonly List<Renderer> selfLit = new List<Renderer>();         // digits, LED bars: own glow
        public readonly List<DashInstrument> gauges = new List<DashInstrument>();
        public readonly List<DashLampView> lamps = new List<DashLampView>();
        public bool hd;
        public string faceKey;
    }

    /// <summary>Builds an instrument cluster from a <see cref="DashboardStyle"/> as real geometry: a lit housing
    /// (binnacle or panel, chrome / black / square bezels, switches, rivets), an unlit illuminated face (dials, ticks,
    /// numerals, red zones, glints, ghost segments, drum windows), needles, drum wheels, digit and LED renderers whose
    /// meshes are swapped, and lamp lenses tinted through a MaterialPropertyBlock. Meshes are cached per style and
    /// shared by every vehicle; materials are MadMax/PixelVoxel copies (pixel art) or HDLit copies (HD pack).</summary>
    public static class DashboardBuilder
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Material hdLit, hdGlow, pxLit, pxGlow, pxSrc;
        static readonly DashMesh mb = new DashMesh();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { meshes.Clear(); hdLit = hdGlow = pxLit = pxGlow = pxSrc = null; }

        const float GlyphZ = -0.0008f, NeedleZ = -0.0026f;

        // ------------------------------------------------------------------ materials
        /// <summary>Lit (housing) and unlit (faces, lamps) materials for the render path; false when none exists.</summary>
        public static bool Materials(bool hd, Material pixelSource, out Material lit, out Material glow)
        {
            if (hd)
            {
                if (!hdLit)
                {
                    var src = MadMax.Rendering.HDShapes.Solid;
                    if (src) { hdLit = Copy(src, "DashLitHD", 0f); hdGlow = Copy(src, "DashGlowHD", 1f); }
                }
                lit = hdLit; glow = hdGlow;
                return lit && glow;
            }
            if (pixelSource && (pxSrc != pixelSource || !pxLit || !pxGlow))
            {
                pxSrc = pixelSource;
                pxLit = Copy(pixelSource, "DashLit", 0f); pxGlow = Copy(pixelSource, "DashGlow", 1f);
            }
            lit = pxLit; glow = pxGlow;
            return lit && glow;
        }

        static Material Copy(Material src, string name, float unlit)
        {
            var m = new Material(src) { name = name };
            if (m.HasProperty("_Unlit")) m.SetFloat("_Unlit", unlit);
            if (m.HasProperty("_OutlinePx")) m.SetFloat("_OutlinePx", 0f);
            if (m.HasProperty("_SnowMask")) m.SetFloat("_SnowMask", 0f);
            if (m.HasProperty("_Dirt")) m.SetFloat("_Dirt", 0f);
            return m;
        }

        // ------------------------------------------------------------------ scale
        /// <summary>The numbers the faces are drawn for: units setting, speedo top in those units, tacho range of the engine.</summary>
        public static DashScale ScaleFor(DashboardStyle s, EngineStats engine, bool metric)
        {
            float top = 200f;
            foreach (var g in s.gauges) if (g.source == GaugeSource.Speed) { top = g.max > 0f ? g.max : 200f; break; }
            if (!metric) top = Mathf.Max(10f, Mathf.Round(top * 0.6214f / 10f) * 10f);
            float rpm = engine ? engine.maxRpm : 6000f;
            return new DashScale
            {
                metric = metric, speedMax = top,
                rpmMax = Mathf.Max(3000f, Mathf.Ceil(rpm / 1000f) * 1000f),
                rpmRed = Mathf.Round(rpm * 0.9f / 250f) * 250f,
            };
        }

        static void Range(GaugeDef g, DashScale sc, out float min, out float max)
        {
            min = g.min; max = g.max;
            if (g.source == GaugeSource.Speed) { min = 0f; max = sc.speedMax; }
            else if (g.source == GaugeSource.Rpm || g.source == GaugeSource.ShiftLights) { min = 0f; max = sc.rpmMax; }
            if (max <= min) max = min + 1f;
        }

        static float RedAbove(GaugeDef g, DashScale sc) => g.source == GaugeSource.Rpm ? sc.rpmRed : g.redAbove;

        public static float Angle(GaugeDef g, float frac) => g.centre - g.sweep / 2f + Mathf.Clamp01(frac) * g.sweep;

        // ------------------------------------------------------------------ build
        /// <summary>Builds the cluster under <paramref name="parent"/> (an object named "Dashboard").</summary>
        public static DashParts Build(Transform parent, DashboardStyle s, DashScale sc, Material lit, Material glow, bool hd)
        {
            var p = new DashParts { hd = hd };
            p.root = new GameObject("Dashboard");
            p.root.transform.SetParent(parent, false);
            Part(p.root.transform, "DashHousing", Cached("h:" + s.id, () => Housing(s)), lit, Vector3.zero);
            p.faceKey = "f:" + s.id + ":" + sc.Key;
            p.faceRenderer = Part(p.root.transform, "DashFace", Cached(p.faceKey, () => Face(s, sc)), glow, Vector3.zero);
            p.face = p.faceRenderer.GetComponent<MeshFilter>();
            p.illuminated.Add(p.faceRenderer);

            for (int i = 0; i < s.gauges.Count; i++)
            {
                var g = s.gauges[i];
                var ins = new DashInstrument { def = g };
                Range(g, sc, out ins.min, out ins.max);
                ins.sweepFrom = Angle(g, 0f); ins.sweepTo = Angle(g, 1f);
                ins.shown = ins.min;
                int gi = i;
                switch (g.kind)
                {
                    case GaugeKind.Dial:
                        {
                            var r = Part(p.root.transform, "DashNeedle" + i, Cached("n:" + s.id + ":" + i, () => Needle(s, g)), glow, new Vector3(g.at.x, g.at.y, NeedleZ));
                            ins.needle = r.transform;
                            ins.needle.localRotation = Quaternion.Euler(0f, 0f, -ins.sweepFrom);
                            p.illuminated.Add(r);
                            break;
                        }
                    case GaugeKind.Digital:
                        {
                            ins.glyphs = new Mesh[DashMesh.SegChars.Length];
                            for (int k = 0; k < ins.glyphs.Length; k++)
                            {
                                char ch = DashMesh.SegChars[k];
                                ins.glyphs[k] = Cached("g:" + s.id + ":" + i + ":" + ch, () => Glyph(s, g, ch));
                            }
                            ins.digits = new MeshFilter[g.digits];
                            ins.shownChars = new char[g.digits];
                            float step = g.size * 0.78f;
                            for (int k = 0; k < g.digits; k++)
                            {
                                float x = g.at.x + (k - (g.digits - 1) / 2f) * step;
                                var r = Part(p.root.transform, "DashDigit" + i + "_" + k, ins.glyphs[ins.glyphs.Length - 1], glow, new Vector3(x, g.at.y, GlyphZ));
                                ins.digits[k] = r.GetComponent<MeshFilter>();
                                ins.shownChars[k] = ' ';
                                p.selfLit.Add(r);
                            }
                            break;
                        }
                    case GaugeKind.Drum:
                        {
                            ins.wheels = new Transform[g.digits];
                            ins.wheelAngle = new float[g.digits];
                            DrumSize(g, out float w, out _, out float rad, out float apo);
                            for (int k = 0; k < g.digits; k++)
                            {
                                bool dec = k >= g.digits - g.decimals;
                                float x = g.at.x + (k - (g.digits - 1) / 2f) * w;
                                var r = Part(p.root.transform, "DashDrum" + i + "_" + k, Cached("d:" + s.id + ":" + i + ":" + dec, () => Drum(s, g, dec)), glow, new Vector3(x, g.at.y, apo * 0.6f));
                                ins.wheels[k] = r.transform;
                                p.illuminated.Add(r);
                            }
                            break;
                        }
                    case GaugeKind.Bar:
                    case GaugeKind.Leds:
                        {
                            ins.levels = new Mesh[g.digits + 1];
                            for (int k = 0; k <= g.digits; k++) { int lv = k; ins.levels[k] = Cached("l:" + s.id + ":" + gi + ":" + lv, () => Level(s, g, sc, lv)); }
                            var r = Part(p.root.transform, "DashLevel" + i, ins.levels[0], glow, new Vector3(g.at.x, g.at.y, GlyphZ));
                            ins.level = r.GetComponent<MeshFilter>();
                            ins.shownLevel = 0;
                            p.selfLit.Add(r);
                            break;
                        }
                }
                p.gauges.Add(ins);
            }

            for (int i = 0; i < s.lamps.Count; i++)
            {
                var l = s.lamps[i];
                var r = Part(p.root.transform, "DashLamp" + i, Cached("lamp:" + l.lamp + ":" + l.size.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), () => Lamp(l)), glow, new Vector3(l.at.x, l.at.y, -0.0006f));
                p.lamps.Add(new DashLampView { lamp = l.lamp, renderer = r });
            }
            return p;
        }

        /// <summary>The face mesh for another scale (units / engine changed): swapped in place.</summary>
        public static Mesh FaceFor(DashboardStyle s, DashScale sc, out string key)
        {
            key = "f:" + s.id + ":" + sc.Key;
            return Cached(key, () => Face(s, sc));
        }

        static MeshRenderer Part(Transform root, string name, Mesh mesh, Material mat, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (meshes.TryGetValue(key, out var m) && m) return m;
            m = make();
            meshes[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ housing (lit)
        static Mesh Housing(DashboardStyle s)
        {
            mb.Clear();
            float w = s.size.x / 2f, h = s.size.y / 2f, d = s.depth;
            var H = s.housing;
            Color32 dark = H[0], mid = H[Mathf.Min(1, H.Length - 1)], light = H[Mathf.Min(2, H.Length - 1)];
            if (s.visor > 0f)
            {
                // binnacle: back shell, hood, cheeks and a lip under the faces
                mb.Box(new Vector3(-w - 0.006f, -h - 0.006f, 0.0012f), new Vector3(w + 0.006f, h + 0.006f, d), dark);
                mb.Box(new Vector3(-w - 0.01f, h + 0.002f, -s.visor), new Vector3(w + 0.01f, h + 0.009f, d), mid);
                mb.Box(new Vector3(-w - 0.01f, h - 0.004f, -s.visor - 0.004f), new Vector3(w + 0.01f, h + 0.009f, -s.visor), light);   // rolled front edge
                mb.Box(new Vector3(-w - 0.01f, -h - 0.008f, -s.visor * 0.55f), new Vector3(-w - 0.003f, h + 0.002f, d), mid);
                mb.Box(new Vector3(w + 0.003f, -h - 0.008f, -s.visor * 0.55f), new Vector3(w + 0.01f, h + 0.002f, d), mid);
                mb.Box(new Vector3(-w - 0.003f, -h - 0.008f, -0.012f), new Vector3(w + 0.003f, -h - 0.003f, d), mid);
                // grain on the back shell: ramp tiles
                for (float x = -w; x < w - 0.001f; x += 0.02f)
                    for (float y = -h; y < h - 0.001f; y += 0.02f)
                    {
                        float k = Pal.Hash(Mathf.RoundToInt(x * 500f), Mathf.RoundToInt(y * 500f), 7);
                        if (k > 0.7f) mb.Rect(x, y, Mathf.Min(w, x + 0.02f), Mathf.Min(h, y + 0.02f), 0.0011f, mid);
                    }
            }
            else
            {
                // flat instrument panel with a raised edge frame
                mb.Box(new Vector3(-w, -h, 0.0012f), new Vector3(w, h, d), mid);
                for (float x = -w; x < w - 0.001f; x += 0.025f)
                    for (float y = -h; y < h - 0.001f; y += 0.025f)
                    {
                        float k = Pal.Hash(Mathf.RoundToInt(x * 400f), Mathf.RoundToInt(y * 400f), 11);
                        mb.Rect(x, y, Mathf.Min(w, x + 0.025f), Mathf.Min(h, y + 0.025f), 0.0011f, k < 0.25f ? dark : k > 0.8f ? light : mid);
                    }
                const float e = 0.006f;
                mb.Box(new Vector3(-w - e, h, -0.004f), new Vector3(w + e, h + e, d), light);
                mb.Box(new Vector3(-w - e, -h - e, -0.004f), new Vector3(w + e, -h, d), dark);
                mb.Box(new Vector3(-w - e, -h, -0.004f), new Vector3(-w, h, d), mid);
                mb.Box(new Vector3(w, -h, -0.004f), new Vector3(w + e, h, d), mid);
                if (s.rivets)
                {
                    for (float x = -w + 0.01f; x < w; x += 0.032f)
                    {
                        mb.Puck(new Vector2(x, h - 0.004f), 0.0016f, -0.0012f, 0.001f, Pal.Chrome[1], 6);
                        mb.Puck(new Vector2(x, -h + 0.004f), 0.0016f, -0.0012f, 0.001f, Pal.Chrome[1], 6);
                    }
                }
            }
            foreach (var g in s.gauges)
            {
                if (g.kind != GaugeKind.Dial) continue;
                Bezel(s, g);
            }
            foreach (var sw in s.switches) Switch(s, sw);
            if (s.bezel == BezelKind.Square)
                foreach (var l in s.lamps)                                                            // lamp guards
                    mb.Tube(l.at, l.size * 0.62f, -0.003f, 0.001f, a => DashMesh.Ramp(Pal.Chrome, a, -0.25f), 10);
            return mb.ToMesh("DashHousing_" + s.id);
        }

        static void Bezel(DashboardStyle s, GaugeDef g)
        {
            var kind = g.bezel ?? s.bezel;
            float r = g.size, depth = Mathf.Max(0.004f, r * 0.14f);
            if (kind == BezelKind.None) return;
            if (kind == BezelKind.Square || g.square)
            {
                float a = r, b = r * 1.13f + 0.002f;
                var col = s.housing;
                Color32 top = DashMesh.Tone(col[Mathf.Min(1, col.Length - 1)], 1.25f), side = col[Mathf.Min(1, col.Length - 1)], bot = DashMesh.Tone(col[0], 0.8f);
                var o = g.at;
                Vector3 P(float x, float y, float z) => new Vector3(o.x + x, o.y + y, z);
                mb.Quad(P(-b, b, -0.001f), P(b, b, -0.001f), P(a, a, -depth), P(-a, a, -depth), top, new Vector3(0f, 0.6f, -1f));
                mb.Quad(P(-b, -b, -0.001f), P(-a, -a, -depth), P(a, -a, -depth), P(b, -b, -0.001f), bot, new Vector3(0f, -0.6f, -1f));
                mb.Quad(P(-b, -b, -0.001f), P(-b, b, -0.001f), P(-a, a, -depth), P(-a, -a, -depth), side, new Vector3(-0.6f, 0f, -1f));
                mb.Quad(P(b, -b, -0.001f), P(a, -a, -depth), P(a, a, -depth), P(b, b, -0.001f), side, new Vector3(0.6f, 0f, -1f));
                for (int k = 0; k < 4; k++)                                                          // corner screws
                {
                    float sx = (k & 1) == 0 ? -1f : 1f, sy = (k & 2) == 0 ? -1f : 1f;
                    var at = new Vector2(o.x + sx * (a + b) * 0.5f, o.y + sy * (a + b) * 0.5f);
                    mb.Puck(at, Mathf.Max(0.0015f, r * 0.05f), -depth * 0.55f - 0.0012f, -depth * 0.3f, Pal.Chrome[1], 8);
                }
                return;
            }
            var ramp = kind == BezelKind.Chrome ? Pal.Chrome : Pal.Black;
            float ro = r * 1.1f + 0.0025f, ri = r + 0.0008f;
            mb.Tube(g.at, ro, 0.002f, -depth * 0.55f, a => DashMesh.Ramp(ramp, a, -0.2f), 40);
            mb.Cone(g.at, ro, -depth * 0.55f, ri + (ro - ri) * 0.35f, -depth, a => DashMesh.Ramp(ramp, a), 40);
            mb.Cone(g.at, ri + (ro - ri) * 0.35f, -depth, ri, -depth * 0.85f, a => DashMesh.Ramp(ramp, a + 180f), 40);
            mb.Tube(g.at, ri, -depth * 0.85f, 0f, a => DashMesh.Ramp(Pal.Black, a + 180f), 40, true);
            if (kind == BezelKind.Black)                                                             // thin chrome trim ring
                mb.Cone(g.at, ri + (ro - ri) * 0.38f, -depth - 0.0002f, ri + (ro - ri) * 0.2f, -depth - 0.0002f, a => DashMesh.Ramp(Pal.Chrome, a), 40);
        }

        static void Switch(DashboardStyle s, SwitchDef sw)
        {
            var o = sw.at;
            mb.Rect(o.x - 0.011f, o.y - 0.0175f, o.x + 0.011f, o.y - 0.0105f, -0.0004f, Pal.Black[0]);       // label plate
            if (!string.IsNullOrEmpty(sw.label))
            {
                float th = Mathf.Min(0.0032f, 0.02f / Mathf.Max(1f, (sw.label.Length * 6 - 1) / 7f));
                mb.Text(sw.label, o.x, o.y - 0.014f, th, -0.0006f, s.minorInk);
            }
            switch (sw.kind)
            {
                case 0:                                                                                 // toggle: nut, lever, ball
                    mb.Puck(o, 0.0042f, -0.003f, 0.001f, Pal.Chrome[2], 10);
                    mb.Box(new Vector3(o.x - 0.0011f, o.y - 0.0008f, -0.014f), new Vector3(o.x + 0.0011f, o.y + 0.0026f, -0.003f), Pal.Chrome[3]);
                    mb.Puck(new Vector2(o.x, o.y + 0.0009f), 0.0019f, -0.0165f, -0.013f, Pal.Chrome[3], 8);
                    break;
                case 1:                                                                                 // rocker
                    mb.Box(new Vector3(o.x - 0.0075f, o.y - 0.0085f, -0.002f), new Vector3(o.x + 0.0075f, o.y + 0.0085f, 0.001f), Pal.Black[1]);
                    mb.Box(new Vector3(o.x - 0.006f, o.y, -0.007f), new Vector3(o.x + 0.006f, o.y + 0.007f, -0.002f), Pal.Black[2]);
                    mb.Box(new Vector3(o.x - 0.006f, o.y - 0.007f, -0.0045f), new Vector3(o.x + 0.006f, o.y, -0.002f), Pal.Black[3]);
                    mb.Rect(o.x - 0.004f, o.y + 0.004f, o.x + 0.004f, o.y + 0.0055f, -0.0072f, Pal.Amber);
                    break;
                case 2:                                                                                 // rotary knob with pointer
                    mb.Puck(o, 0.0085f, -0.002f, 0.001f, Pal.Chrome[1], 16);
                    mb.Puck(o, 0.0065f, -0.013f, -0.002f, Pal.Black[1], 14);
                    mb.Rect(o.x - 0.0007f, o.y + 0.001f, o.x + 0.0007f, o.y + 0.006f, -0.0132f, Pal.Cream[3]);
                    break;
                default:                                                                                // push button
                    mb.Puck(o, 0.0062f, -0.003f, 0.001f, Pal.Chrome[1], 14);
                    mb.Puck(o, 0.0045f, -0.008f, -0.003f, Pal.Crimson[3], 12);
                    break;
            }
        }

        // ------------------------------------------------------------------ face (unlit, backlit)
        static Mesh Face(DashboardStyle s, DashScale sc)
        {
            mb.Clear();
            foreach (var g in s.gauges)
            {
                switch (g.kind)
                {
                    case GaugeKind.Dial: DialFace(s, g, sc); break;
                    case GaugeKind.Digital: DigitalFace(s, g, sc); break;
                    case GaugeKind.Drum: DrumFace(s, g); break;
                    case GaugeKind.Bar: BarFace(s, g); break;
                    case GaugeKind.Leds: LedFace(s, g); break;
                }
            }
            foreach (var l in s.lamps)                                                               // lamp masks
            {
                float m = l.size * 0.62f;
                mb.Rect(l.at.x - m, l.at.y - m, l.at.x + m, l.at.y + m, -0.0003f, Pal.Void);
            }
            return mb.ToMesh("DashFace_" + s.id);
        }

        static float NiceStep(float range, int count)
        {
            float raw = range / Mathf.Max(1, count);
            float mag = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            float f = raw / mag;
            return (f < 1.5f ? 1f : f < 2.25f ? 2f : f < 3.5f ? 2.5f : f < 7.5f ? 5f : 10f) * mag;
        }

        static string Caption(GaugeDef g, DashScale sc)
        {
            if (g.caption != null) return g.caption;
            switch (g.source)
            {
                case GaugeSource.Speed: return sc.metric ? "KM/H" : "MPH";
                case GaugeSource.Rpm: return "RPM";
                default: return null;
            }
        }

        static void DialFace(DashboardStyle s, GaugeDef g, DashScale sc)
        {
            Range(g, sc, out float min, out float max);
            float r = g.size;
            bool small = r < 0.03f;
            var o = g.at;
            Color32 face = g.face ?? s.face, ink = g.ink ?? s.ink;
            Color32 minor = g.ink.HasValue ? DashMesh.Tone(g.ink.Value, 1.6f) : s.minorInk;
            if (g.square || (g.bezel ?? s.bezel) == BezelKind.Square) mb.Rect(o.x - r, o.y - r, o.x + r, o.y + r, 0f, face);
            else mb.Disc(o, r + 0.0009f, 0f, face, 48);
            float zt = -0.0003f, zl = -0.0005f;
            float Frac(float val) => (val - min) / (max - min);
            float A(float val) => Angle(g, Frac(val));
            float redA = RedAbove(g, sc);
            bool Red(float val) => val >= redA - 1e-3f || val <= g.redBelow + 1e-3f;

            // scale ring and red zones
            mb.Arc(o, r * 0.935f, r * 0.95f, A(min), A(max), zt, minor, Mathf.Max(8, Mathf.RoundToInt(g.sweep / 8f)));
            if (redA < max) mb.Arc(o, r * 0.80f, r * 0.87f, A(Mathf.Max(min, redA)), A(max), zt, s.red, 10);
            if (g.redBelow > min) mb.Arc(o, r * 0.80f, r * 0.87f, A(min), A(Mathf.Min(max, g.redBelow)), zt, s.red, 6);

            if (!string.IsNullOrEmpty(g.ends))
            {
                for (int k = 0; k <= 4; k++)
                {
                    float val = Mathf.Lerp(min, max, k / 4f);
                    bool major = k % 2 == 0;
                    mb.Tick(o, A(val), r * (major ? 0.70f : 0.80f), r * 0.95f, r * (major ? 0.07f : 0.04f), zl, Red(val) ? s.red : major ? ink : minor);
                }
                float lh = r * (small ? 0.3f : 0.18f);
                Color32 c0 = g.redBelow > min ? s.red : ink, c1 = redA < max ? s.red : ink;
                var p0 = DashMesh.Polar(o, r * 0.5f, A(min) + 8f, 0f); var p1 = DashMesh.Polar(o, r * 0.5f, A(max) - 8f, 0f);
                mb.Text(g.ends.Substring(0, 1), p0.x, p0.y, lh, zl, c0);
                mb.Text(g.ends.Substring(1, 1), p1.x, p1.y, lh, zl, c1);
            }
            else
            {
                float step = NiceStep(max - min, g.labels > 0 ? g.labels : 6);
                float lead = step / Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(step)));
                int sub = small ? 2 : Mathf.Approximately(lead, 2f) ? 4 : Mathf.Approximately(lead, 5f) ? 5 : 2;
                float minorStep = step / sub;
                float lh = r * (small ? 0.24f : 0.15f);
                float rpmDiv = max <= 4000f ? 100f : 1000f;
                int k0 = Mathf.CeilToInt(min / minorStep - 1e-3f), k1 = Mathf.FloorToInt(max / minorStep + 1e-3f);
                for (int k = k0; k <= k1; k++)                                                        // ticks on whole multiples of the steps
                {
                    float val = k * minorStep;
                    bool major = ((k % sub) + sub) % sub == 0;
                    bool half = !major && sub == 4 && k % 2 == 0;
                    float r0 = major ? 0.76f : half ? 0.83f : 0.87f;
                    mb.Tick(o, A(val), r * r0, r * 0.95f, r * (major ? 0.04f : 0.018f), zl, Red(val) ? s.red : major ? ink : minor);
                    if (!major || g.labels <= 0) continue;
                    float shownVal = g.source == GaugeSource.Rpm ? val / rpmDiv : val / g.labelDiv;
                    string txt = Mathf.Abs(shownVal - Mathf.Round(shownVal)) < 0.01f ? Mathf.RoundToInt(shownVal).ToString() : shownVal.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                    var lp = DashMesh.Polar(o, r * (small ? 0.5f : 0.6f), A(val), 0f);
                    mb.Text(txt, lp.x, lp.y, lh, zl, Red(val) ? s.red : ink);
                }
                if (g.source == GaugeSource.Rpm) mb.Text(rpmDiv > 500f ? "x1000" : "x100", o.x, o.y + r * 0.2f, r * 0.06f, zl, minor);
            }
            string cap = Caption(g, sc);
            if (!string.IsNullOrEmpty(cap))
            {
                float ch = r * (small ? 0.15f : 0.085f);
                ch = Mathf.Min(ch, r * 0.9f / Mathf.Max(1f, (cap.Length * 6 - 1) / 7f));
                mb.Text(cap, o.x, o.y + r * (small ? -0.42f : 0.32f), ch, zl, minor);
            }
            // glass: a thin reflection arc up-left in front of everything
            float gz = -Mathf.Max(0.004f, r * 0.14f) * 0.8f;
            mb.Arc(o, r * 0.9f, r * 0.94f, -72f, -28f, gz, DashMesh.Tone(Pal.Glass[3], 0.8f), 8);
            mb.Arc(o, r * 0.82f, r * 0.84f, -62f, -40f, gz, DashMesh.Tone(Pal.Glass[3], 0.55f), 5);
        }

        static void DigitalFace(DashboardStyle s, GaugeDef g, DashScale sc)
        {
            float step = g.size * 0.78f, hw = step * g.digits / 2f + g.size * 0.12f, hh = g.size * 0.64f;
            var o = g.at;
            mb.Rect(o.x - hw - 0.001f, o.y - hh - 0.001f, o.x + hw + 0.001f, o.y + hh + 0.001f, -0.0002f, DashMesh.Tone(s.minorInk, 0.35f));
            mb.Rect(o.x - hw, o.y - hh, o.x + hw, o.y + hh, -0.0004f, Pal.Void);
            var ghost = DashMesh.Tone(s.digit, 0.13f);
            for (int k = 0; k < g.digits; k++) mb.Segments('8', o.x + (k - (g.digits - 1) / 2f) * step, o.y, g.size, -0.0006f, ghost, true);
            if (g.source == GaugeSource.Speed) mb.Text(Caption(g, sc), o.x, o.y - hh - g.size * 0.32f, g.size * 0.3f, -0.0004f, g.ink ?? s.minorInk);
        }

        static void DrumSize(GaugeDef g, out float w, out float fh, out float rad, out float apo)
        {
            fh = g.size / 0.72f;
            w = g.size * 0.82f;
            rad = fh / (2f * Mathf.Sin(18f * Mathf.Deg2Rad));
            apo = rad * Mathf.Cos(18f * Mathf.Deg2Rad);
        }

        static void DrumFace(DashboardStyle s, GaugeDef g)
        {
            DrumSize(g, out float w, out float fh, out float rad, out float apo);
            float x0 = g.at.x - w * g.digits / 2f, x1 = g.at.x + w * g.digits / 2f, y = g.at.y;
            float z = -apo * 0.4f - 0.0006f, b = 0.0014f, wy = fh * 0.54f;
            var frame = DashMesh.Tone(s.face, 1.6f);
            mb.Rect(x0 - b, y + wy, x1 + b, y + rad * 0.8f + b, z, s.face);                          // covers above / below the window
            mb.Rect(x0 - b, y - rad * 0.8f - b, x1 + b, y - wy, z, s.face);
            mb.Rect(x0 - b, y - wy, x0, y + wy, z, frame);
            mb.Rect(x1, y - wy, x1 + b, y + wy, z, frame);
            mb.Rect(x0 - b, y + wy, x1 + b, y + wy + 0.0005f, z - 0.0001f, frame);
            mb.Rect(x0 - b, y - wy - 0.0005f, x1 + b, y - wy, z - 0.0001f, frame);
            if (!string.IsNullOrEmpty(g.caption)) mb.Text(g.caption, g.at.x, y - wy - g.size * 0.7f, g.size * 0.5f, -0.0004f, s.minorInk);
        }

        static float BarStep(GaugeDef g) => g.size / g.digits;

        static void BarFace(DashboardStyle s, GaugeDef g)
        {
            float st = BarStep(g), hw = 0.0045f;
            var ghost = DashMesh.Tone(s.digit, 0.12f);
            mb.Rect(g.at.x - hw - 0.0015f, g.at.y - g.size / 2f - 0.0015f, g.at.x + hw + 0.0015f, g.at.y + g.size / 2f + 0.0015f, -0.0002f, Pal.Void);
            for (int k = 0; k < g.digits; k++)
            {
                float y0 = g.at.y - g.size / 2f + k * st + st * 0.15f;
                mb.Rect(g.at.x - hw, y0, g.at.x + hw, y0 + st * 0.7f, -0.0004f, ghost);
            }
            if (!string.IsNullOrEmpty(g.caption)) mb.Text(g.caption, g.at.x, g.at.y - g.size / 2f - 0.006f, 0.0045f, -0.0004f, s.minorInk);
        }

        static void LedFace(DashboardStyle s, GaugeDef g)
        {
            float st = g.size / g.digits;
            mb.Rect(g.at.x - g.size / 2f - 0.002f, g.at.y - st * 0.45f, g.at.x + g.size / 2f + 0.002f, g.at.y + st * 0.45f, -0.0002f, Pal.Void);
            for (int k = 0; k < g.digits; k++) mb.Disc(new Vector2(g.at.x - g.size / 2f + (k + 0.5f) * st, g.at.y), st * 0.32f, -0.0004f, Pal.Black[2], 10);
        }

        // ------------------------------------------------------------------ moving / swapped pieces
        static Mesh Needle(DashboardStyle s, GaugeDef g)
        {
            mb.Clear();
            float r = g.size, len = r * 0.9f, tail = r * 0.22f, wb = Mathf.Max(0.0012f, r * 0.045f), wt = Mathf.Max(0.0004f, r * 0.012f), th = 0.0012f;
            var col = s.needle;
            var side = DashMesh.Tone(col, 0.6f);
            Vector3 tl = new Vector3(-wb * 0.8f, -tail, 0f), tr = new Vector3(wb * 0.8f, -tail, 0f), bl = new Vector3(-wb, 0f, 0f), br = new Vector3(wb, 0f, 0f);
            Vector3 pl = new Vector3(-wt, len, 0f), pr = new Vector3(wt, len, 0f);
            mb.Quad(bl, pl, pr, br, col, Vector3.back);
            mb.Quad(tl, bl, br, tr, DashMesh.Tone(col, 0.5f), Vector3.back);
            var dz = new Vector3(0f, 0f, th);
            mb.Quad(bl, pl, pl + dz, bl + dz, side, Vector3.left);
            mb.Quad(br, br + dz, pr + dz, pr, side, Vector3.right);
            mb.Quad(pl, pl + dz, pr + dz, pr, side, Vector3.up);
            mb.Puck(Vector2.zero, Mathf.Max(0.002f, r * 0.1f), -0.0016f, th, s.bezel == BezelKind.Chrome ? Pal.Chrome[1] : Pal.Black[2], 14);
            return mb.ToMesh("DashNeedle");
        }

        static Mesh Glyph(DashboardStyle s, GaugeDef g, char ch)
        {
            mb.Clear();
            if (ch != ' ') mb.Segments(ch, 0f, 0f, g.size, 0f, s.digit);
            else mb.Rect(0f, 0f, 0.0001f, 0.0001f, 0.0005f, Pal.Void);                      // an (invisible) placeholder
            return mb.ToMesh("DashGlyph");
        }

        static Mesh Drum(DashboardStyle s, GaugeDef g, bool dec)
        {
            mb.Clear();
            DrumSize(g, out float w, out float fh, out float rad, out float apo);
            Color32 bg = dec ? Pal.Crimson[2] : Pal.Black[1], ink = Pal.Cream[4];
            for (int k = 0; k < 10; k++)
            {
                mb.xf = Matrix4x4.Rotate(Quaternion.AngleAxis(-36f * k, Vector3.right)) * Matrix4x4.Translate(new Vector3(0f, 0f, -apo));
                mb.Rect(-w * 0.46f, -fh / 2f, w * 0.46f, fh / 2f, 0f, k % 2 == 0 ? bg : DashMesh.Tone(bg, 0.9f));
                mb.Text(k.ToString(), 0f, 0f, g.size, -0.0001f, ink);
            }
            mb.xf = Matrix4x4.identity;
            return mb.ToMesh("DashDrum");
        }

        static Mesh Level(DashboardStyle s, GaugeDef g, DashScale sc, int lit)
        {
            mb.Clear();
            if (lit == 0) { mb.Rect(0f, 0f, 0.0001f, 0.0001f, 0.0005f, Pal.Void); return mb.ToMesh("DashLevel"); }
            if (g.kind == GaugeKind.Leds)
            {
                float st = g.size / g.digits;
                for (int k = 0; k < lit; k++)
                {
                    float f = k / (float)Mathf.Max(1, g.digits - 1);
                    var col = f < 0.5f ? Pal.Moss[4] : f < 0.8f ? Pal.Amber : Pal.Crimson[4];
                    mb.Disc(new Vector2(-g.size / 2f + (k + 0.5f) * st, 0f), st * 0.3f, 0f, col, 10);
                }
                return mb.ToMesh("DashLevel");
            }
            float bs = BarStep(g), hw = 0.0045f;
            Range(g, sc, out float min, out float max);
            for (int k = 0; k < lit; k++)
            {
                float y0 = -g.size / 2f + k * bs + bs * 0.15f;
                float val = Mathf.Lerp(min, max, (k + 0.5f) / g.digits);
                Color32 col;
                if (g.source == GaugeSource.Temp)
                    col = val < VehicleGauges.ColdBelow ? Pal.PaleBlue[2] : val <= VehicleSystems.HotLimit ? Pal.Moss[4] : val <= VehicleSystems.CriticalLimit ? Pal.Amber : Pal.Crimson[4];
                else col = val <= g.redBelow || val >= g.redAbove ? Pal.Crimson[4] : s.digit;
                mb.Rect(-hw, y0, hw, y0 + bs * 0.7f, 0f, col);
            }
            return mb.ToMesh("DashLevel");
        }

        static Mesh Lamp(LampDef l)
        {
            mb.Clear();
            float h = l.size * 0.5f;
            mb.Rect(-h, -h, h, h, 0f, Pal.Cream[0]);
            mb.Icon(l.lamp, 0f, 0f, l.size * 0.8f, -0.0002f, Pal.Cream[4]);
            return mb.ToMesh("DashLamp");
        }
    }
}
