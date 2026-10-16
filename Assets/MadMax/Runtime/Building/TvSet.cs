using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A placed TV: plays VHS tapes. While running the screen shows a picture (a small procedural programme
    /// picked by the tape: a road movie, a talking head, a cartoon, a workshop show) with scan roll, tracking noise and
    /// cuts, and casts its flickering light into the room (2026-10-15: the screen used to stay dark).
    /// 2026-10-16: every tape keeps its own programme (<see cref="ShowFor"/>: driving school = the road movie, garage
    /// hour = the workshop show, survival = a night camp, karate = a dojo bout, demolition = the derby, salesman = the
    /// seminar, grow your own = the garden show; other tapes a newsreader or the cartoon), and people idling near a
    /// playing set sit or stand and watch it (Npc.Tv).</summary>
    public class TvSet : MonoBehaviour
    {
        public static readonly List<TvSet> All = new List<TvSet>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => All.Clear();

        public string Playing { get; private set; }
        Light screen;
        Renderer picture;
        Texture2D tex;
        Color32[] px;
        Material mat;
        float nextFrame, started;
        int show;
        /// <summary>Picture resolution (texels).</summary>
        public const int W = 40, H = 30;
        const float Fps = 12f;
        /// <summary>Screen centre and size on the piece (metres, piece space; matches the voxel and HD cabinets).</summary>
        static readonly Vector3 ScreenAt = new Vector3(-0.04f, 0.48f, 0.302f);
        static readonly Vector2 ScreenSize = new Vector2(0.54f, 0.44f);
        /// <summary>Mean brightness of the last frame (0..1; drives the room light, tests).</summary>
        public float Brightness { get; private set; }
        public int Show => show;
        public Renderer Picture => picture;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void OnDestroy() { if (tex) Destroy(tex); if (mat) Destroy(mat); }

        public static TvSet Nearest(Vector3 p, float max)
        {
            TvSet best = null; float bd = max;
            foreach (var t in All) { if (!t) continue; float d = Vector3.Distance(t.transform.position, p); if (d < bd) { bd = d; best = t; } }
            return best;
        }

        public void Play(string id)
        {
            Playing = id;
            show = ShowFor(id);
            started = Time.time;
            if (!screen)
            {
                screen = new GameObject("Screen").AddComponent<Light>();
                screen.transform.SetParent(transform, false);
                screen.transform.localPosition = new Vector3(0f, 0.3f, 0.35f);
                screen.type = LightType.Point; screen.range = 4f; screen.shadows = LightShadows.None;
            }
            screen.enabled = true;
            EnsurePicture();
            picture.enabled = true;
            nextFrame = 0f;
        }

        /// <summary>The programme a tape carries (0 road movie, 1 newsreader, 2 cartoon, 3 workshop, 4 night camp, 5 dojo,
        /// 6 derby, 7 seminar, 8 garden show). Unknown tapes: newsreader or cartoon by name, stable across runs.</summary>
        public static int ShowFor(string id)
        {
            switch (id)
            {
                case "vhs_driving": return 0;
                case "vhs_garage": return 3;
                case "vhs_survival": return 4;
                case "vhs_karate": return 5;
                case "vhs_demolition": return 6;
                case "vhs_salesman": return 7;
                case "vhs_garden": return 8;
            }
            if (id == null) return 1;
            if (id.Contains("news")) return 1;
            int h = 0; foreach (char ch in id) h = h * 31 + ch;
            return (h & 0x7fffffff) % 2 == 0 ? 1 : 2;
        }

        /// <summary>One-word label of a programme (tests, prompts).</summary>
        public static readonly string[] ShowNames = { "ROAD MOVIE", "NEWS", "CARTOON", "WORKSHOP", "NIGHT CAMP", "KARATE", "DERBY", "SEMINAR", "GARDEN SHOW" };

        public void Stop()
        {
            Playing = null;
            if (screen) screen.enabled = false;
            if (picture) picture.enabled = false;
        }

        void EnsurePicture()
        {
            if (picture) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Picture";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localPosition = ScreenAt;
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);                        // a quad faces −Z; the screen faces +Z
            q.transform.localScale = new Vector3(ScreenSize.x, ScreenSize.y, 1f);
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[W * H];
            mat = new Material(MadMax.World.Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            picture = q.GetComponent<Renderer>();
            picture.sharedMaterial = mat;
            picture.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            picture.receiveShadows = false;
        }

        void Update()
        {
            if (!screen || !screen.enabled) return;
            if (Time.time >= nextFrame && picture && picture.isVisible) { nextFrame = Time.time + 1f / Fps; Draw(Time.time - started); }
            float t = Time.time * 7f;
            screen.intensity = (0.8f + Brightness * 1.6f) * (0.85f + Mathf.PerlinNoise(t, 0.3f) * 0.3f);
            screen.color = Color.Lerp(new Color(0.55f, 0.7f, 1f), new Color(1f, 0.85f, 0.7f), Mathf.PerlinNoise(0.7f, t * 0.5f));
        }

        /// <summary>One frame of the programme at <paramref name="t"/> seconds into the tape.</summary>
        public void Draw(float t)
        {
            if (px == null) EnsurePicture();
            int shot = Mathf.FloorToInt(t / 4.5f);                                              // a cut every few seconds
            var rnd = new System.Random(shot * 7919 + show * 131 + Mathf.FloorToInt(t * Fps));
            float roll = (t * 0.35f) % 1f;                                                       // the hum bar rolling up
            bool tracking = (shot % 5 == 3) && (t % 4.5f) < 0.6f;                                // a bad patch of tape after a cut
            long sum = 0;
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                float hum = 1f - 0.18f * Mathf.Clamp01(1f - Mathf.Abs(v - roll) * 6f);
                int shift = tracking && rnd.Next(4) == 0 ? rnd.Next(-4, 5) : 0;
                for (int x = 0; x < W; x++)
                {
                    float u = Mathf.Clamp01((x + shift) / (float)(W - 1));
                    Color c = Pixel(u, v, t, shot);
                    if (tracking && rnd.Next(9) == 0) c = Color.Lerp(c, Color.white, 0.7f);
                    c *= hum * ((y & 1) == 0 ? 1f : 0.92f);                                      // scan lines
                    float ex = Mathf.Abs(u - 0.5f) * 2f, ey = Mathf.Abs(v - 0.5f) * 2f;
                    c *= Mathf.Clamp01(1.25f - (ex * ex * ex * ex + ey * ey * ey * ey) * 0.6f);    // a tube darkens to its corners
                    var c32 = (Color32)c; c32.a = 255;
                    px[y * W + x] = c32;
                    sum += c32.r + c32.g + c32.b;
                }
            }
            Brightness = sum / (W * H * 765f);
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>The programme: 0 road movie, 1 newsreader, 2 cartoon, 3 workshop show. u right, v up.</summary>
        Color Pixel(float u, float v, float t, int shot)
        {
            switch (show)
            {
                case 0:
                {
                    // dusk desert, a road to the horizon, a car crossing, the sun low
                    bool close = (shot & 1) == 1;
                    float hz = close ? 0.42f : 0.5f;
                    if (v > hz)
                    {
                        var sky = Color.Lerp(new Color(0.95f, 0.55f, 0.25f), new Color(0.25f, 0.2f, 0.45f), (v - hz) / (1f - hz));
                        float sd = new Vector2(u - 0.7f, (v - hz - 0.08f) * 1.3f).magnitude;
                        return sd < 0.09f ? new Color(1f, 0.9f, 0.55f) : sky;
                    }
                    bool road = Mathf.Abs(u - 0.5f) < (hz - v) * 0.9f + 0.01f;
                    var ground = road ? new Color(0.3f, 0.3f, 0.32f) : new Color(0.55f, 0.35f, 0.2f);
                    if (road && Mathf.Abs(u - 0.5f) < 0.012f + (hz - v) * 0.03f && ((v * 18f + t * 6f) % 1f) < 0.5f) ground = new Color(0.9f, 0.85f, 0.5f);
                    float cx = ((t * 0.22f + shot * 0.37f) % 1.4f) - 0.2f, cy = hz - 0.12f;
                    if (Mathf.Abs(u - cx) < (close ? 0.16f : 0.08f) && v > cy - 0.05f && v < cy + (close ? 0.07f : 0.035f)) return new Color(0.6f, 0.12f, 0.08f);
                    return ground;
                }
                case 1:
                {
                    // the newsreader: head and shoulders, the mouth moving, a ticker along the bottom
                    if (v < 0.12f) return ((u * 12f + t * 2f) % 1f) < 0.65f ? new Color(0.85f, 0.85f, 0.2f) : new Color(0.1f, 0.1f, 0.4f);
                    var back = Color.Lerp(new Color(0.15f, 0.25f, 0.5f), new Color(0.3f, 0.45f, 0.75f), v);
                    float sx = (u - 0.5f) / 0.3f, sy = (v - 0.12f) / 0.25f;
                    if (sx * sx + sy * sy < 1f && v < 0.37f) return new Color(0.25f, 0.22f, 0.2f);         // jacket
                    float hx = (u - 0.5f) / 0.15f, hy = (v - 0.55f) / 0.2f;
                    if (hx * hx + hy * hy < 1f)
                    {
                        if (hy > 0.55f) return new Color(0.2f, 0.13f, 0.08f);                               // hair
                        if (Mathf.Abs(hy - 0.15f) < 0.1f && Mathf.Abs(Mathf.Abs(hx) - 0.4f) < 0.12f) return new Color(0.1f, 0.08f, 0.06f);
                        float talk = Mathf.PerlinNoise(t * 6f, 1.3f) > 0.5f ? 0.12f : 0.05f;
                        if (Mathf.Abs(hy + 0.45f) < talk && Mathf.Abs(hx) < 0.3f) return new Color(0.35f, 0.1f, 0.1f);
                        return new Color(0.85f, 0.65f, 0.5f);
                    }
                    return back;
                }
                case 2:
                {
                    // cartoon: a red ball bouncing over a checker floor, squashed on landing
                    var floor = v < 0.25f ? ((((int)(u * 10f + t * 2f)) + (int)(v * 12f)) % 2 == 0 ? new Color(0.95f, 0.85f, 0.3f) : new Color(0.2f, 0.6f, 0.3f)) : new Color(0.55f, 0.85f, 1f);
                    float ph = (t * 1.6f) % 1f, by = 0.25f + 0.55f * 4f * ph * (1f - ph);
                    float bx = 0.5f + 0.35f * Mathf.Sin(t * 0.9f);
                    float squash = ph < 0.08f || ph > 0.92f ? 1.4f : 1f;
                    float dx = (u - bx) / (0.1f * squash), dy = (v - by - 0.1f / squash) / (0.1f / squash);
                    if (dx * dx + dy * dy < 1f) return dx < -0.3f && dy > 0.3f ? Color.white : new Color(0.9f, 0.15f, 0.1f);
                    float shx = (u - bx) / 0.12f, shy = (v - 0.25f) / 0.025f;
                    if (shx * shx + shy * shy < 1f) return floor * 0.5f;
                    return floor;
                }
                case 4: return Camp(u, v, t, shot);
                case 5: return Dojo(u, v, t, shot);
                case 6: return Derby(u, v, t, shot);
                case 7: return Seminar(u, v, t, shot);
                case 8: return Garden(u, v, t, shot);
                default:
                {
                    // the workshop show: an engine block on a bench, a spinning flywheel, a wrench swinging in
                    var wall = Color.Lerp(new Color(0.35f, 0.3f, 0.25f), new Color(0.5f, 0.45f, 0.38f), v);
                    if (v < 0.3f) return new Color(0.4f, 0.25f, 0.12f);
                    if (u > 0.2f && u < 0.55f && v < 0.62f) return ((int)(u * 30f) % 4 == 0) ? new Color(0.25f, 0.27f, 0.3f) : new Color(0.45f, 0.48f, 0.52f);
                    float fx = (u - 0.72f) / 0.13f, fy = (v - 0.48f) / 0.17f, r2 = fx * fx + fy * fy;
                    if (r2 < 1f)
                    {
                        float a = Mathf.Atan2(fy, fx) + t * 5f;
                        return Mathf.Sin(a * 3f) > 0.6f && r2 > 0.15f ? new Color(0.9f, 0.9f, 0.85f) : new Color(0.3f, 0.32f, 0.35f);
                    }
                    float swing = Mathf.Sin(t * 2.2f) * 0.5f;
                    var d = new Vector2(u - 0.4f, v - 0.95f);
                    var dir = new Vector2(Mathf.Sin(swing), -Mathf.Cos(swing));
                    float along = Vector2.Dot(d, dir), across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                    if (along > 0.05f && along < 0.32f && across < 0.025f) return new Color(0.75f, 0.75f, 0.8f);
                    return wall;
                }
            }
        }

        static bool Box(float u, float v, float x0, float y0, float x1, float y1) => u >= x0 && u <= x1 && v >= y0 && v <= y1;

        /// <summary>Desert survival: a night camp — stars, a dune line, a fire whose flames lick and spit, a figure feeding it.</summary>
        static Color Camp(float u, float v, float t, int shot)
        {
            float dune = 0.32f + 0.05f * Mathf.Sin(u * 7f + shot) + 0.03f * Mathf.Sin(u * 17f);
            if (v > dune)
            {
                float star = Mathf.Sin(u * 913.1f + v * 271.7f) * 43758.55f; star -= Mathf.Floor(star);
                if (star > 0.985f && Mathf.PerlinNoise(u * 40f, t * 3f) > 0.35f) return new Color(0.9f, 0.9f, 0.8f);
                return Color.Lerp(new Color(0.08f, 0.06f, 0.18f), new Color(0.02f, 0.02f, 0.06f), (v - dune) / (1f - dune));
            }
            float fx = 0.5f, fdx = u - fx;
            float flameH = 0.2f + 0.08f * Mathf.PerlinNoise(t * 5f, u * 9f);
            if (Mathf.Abs(fdx) < 0.09f * (1f - (v - 0.2f) / flameH) && v > 0.2f && v < 0.2f + flameH)
                return Color.Lerp(new Color(1f, 0.9f, 0.4f), new Color(0.9f, 0.3f, 0.05f), (v - 0.2f) / flameH);
            float spark = ((t * 0.7f + u * 3f) % 1f);
            if (Mathf.Abs(fdx - 0.04f * Mathf.Sin(t * 3f + shot)) < 0.01f && Mathf.Abs(v - (0.25f + spark * 0.5f)) < 0.012f) return new Color(1f, 0.7f, 0.3f);
            // the figure crouched at the fire, arm reaching in now and then
            float px = 0.7f, reach = Mathf.Max(0f, Mathf.Sin(t * 1.3f)) * 0.08f;
            if (Box(u, v, px - 0.04f, 0.18f, px + 0.04f, 0.36f) || (new Vector2(u - px, v - 0.4f).magnitude < 0.04f) || Box(u, v, px - 0.08f - reach, 0.28f, px, 0.3f))
                return new Color(0.05f, 0.04f, 0.04f);
            float glow = Mathf.Clamp01(1f - new Vector2(fdx, (v - 0.2f) * 1.6f).magnitude * 2.2f) * (0.8f + 0.2f * Mathf.Sin(t * 13f));
            return Color.Lerp(new Color(0.2f, 0.13f, 0.1f), new Color(0.75f, 0.4f, 0.15f), glow);
        }

        /// <summary>Street karate: two fighters on a dojo floor trading kicks; a slow-motion replay every other shot.</summary>
        static Color Dojo(float u, float v, float t, int shot)
        {
            float tt = (shot & 1) == 1 ? t * 0.35f : t;
            if (v < 0.22f) return ((int)(u * 8f) % 2 == 0) ? new Color(0.75f, 0.6f, 0.35f) : new Color(0.68f, 0.53f, 0.3f);
            var wall = ((int)(u * 5f) % 2 == 0) ? new Color(0.85f, 0.82f, 0.7f) : new Color(0.55f, 0.35f, 0.2f);
            if (v > 0.7f && Box(u, v, 0.42f, 0.74f, 0.58f, 0.9f)) return new Color(0.75f, 0.1f, 0.1f);       // the scroll
            for (int f = 0; f < 2; f++)
            {
                float dir = f == 0 ? 1f : -1f;
                float lunge = Mathf.Max(0f, Mathf.Sin(tt * 2.4f + f * Mathf.PI)) * 0.06f;
                float x = (f == 0 ? 0.36f : 0.64f) + dir * lunge;
                var gi = f == 0 ? new Color(0.95f, 0.95f, 0.92f) : new Color(0.15f, 0.15f, 0.2f);
                if (Box(u, v, x - 0.035f, 0.38f, x + 0.035f, 0.58f)) return gi;                                // body
                if (new Vector2(u - x, (v - 0.63f) * 1.2f).magnitude < 0.04f) return new Color(0.8f, 0.6f, 0.45f);
                if (Box(u, v, x - 0.03f, 0.22f, x - 0.005f, 0.38f)) return gi;                                // standing leg
                float kick = Mathf.Max(0f, Mathf.Sin(tt * 2.4f + f * Mathf.PI + 0.6f));
                var hip = new Vector2(x + 0.015f, 0.38f);
                var foot = hip + new Vector2(dir * (0.04f + 0.14f * kick), -0.16f + 0.2f * kick);
                var d = new Vector2(u, v) - hip; var seg = foot - hip;
                float along = Mathf.Clamp01(Vector2.Dot(d, seg) / seg.sqrMagnitude);
                if ((d - seg * along).magnitude < 0.02f) return gi;                                         // kicking leg
                if (Box(u, v, x - 0.04f, 0.47f, x + 0.04f, 0.49f)) return f == 0 ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.85f, 0.7f, 0.1f);   // belt
            }
            return wall;
        }

        /// <summary>Demolition derby: a mud arena from the stands, two cars ramming, dust where they meet, the crowd's flags.</summary>
        static Color Derby(float u, float v, float t, int shot)
        {
            if (v > 0.72f)
            {
                float flag = Mathf.Sin(u * 40f + t * 4f) > 0.75f ? 1f : 0f;
                return flag > 0f && v < 0.8f ? new Color(0.9f, 0.8f, 0.1f) : (((int)(u * 60f) + (int)(v * 40f)) % 3 == 0 ? new Color(0.5f, 0.3f, 0.25f) : new Color(0.3f, 0.25f, 0.3f));
            }
            if (v > 0.66f) return new Color(0.7f, 0.7f, 0.7f);                                              // the barrier
            var mud = Color.Lerp(new Color(0.35f, 0.24f, 0.14f), new Color(0.45f, 0.32f, 0.18f), Mathf.PerlinNoise(u * 12f, v * 12f));
            float ph = (t * 0.5f + shot * 0.3f) % 1f;                                                       // a run-up and a hit each 2 s
            float meet = 0.5f + 0.1f * Mathf.Sin(shot * 1.7f);
            float gap = Mathf.Max(0f, 0.35f * (1f - ph * 1.6f));
            float ax = meet - 0.1f - gap, bx = meet + 0.1f + gap;
            float jolt = ph > 0.62f && ph < 0.7f ? 0.02f : 0f;
            if (Box(u, v, ax - 0.1f, 0.35f + jolt, ax + 0.1f, 0.45f + jolt)) return v > 0.42f + jolt ? new Color(0.7f, 0.15f, 0.1f) : new Color(0.5f, 0.1f, 0.08f);
            if (Box(u, v, bx - 0.1f, 0.33f, bx + 0.1f, 0.43f)) return v > 0.4f ? new Color(0.15f, 0.35f, 0.7f) : new Color(0.1f, 0.25f, 0.5f);
            if (ph > 0.62f)
            {
                float dust = Mathf.PerlinNoise(u * 14f + t, v * 14f) * (1f - (ph - 0.62f) / 0.38f);
                if (new Vector2(u - meet, (v - 0.45f) * 1.5f).magnitude < 0.08f + (ph - 0.62f) * 0.35f && dust > 0.3f) return new Color(0.7f, 0.6f, 0.45f);
            }
            return mud;
        }

        /// <summary>The salesman seminar: a grinning presenter at a lectern, a sales chart climbing behind, a slogan banner.</summary>
        static Color Seminar(float u, float v, float t, int shot)
        {
            var back = Color.Lerp(new Color(0.45f, 0.1f, 0.12f), new Color(0.65f, 0.2f, 0.2f), v);
            if (v > 0.86f) return ((u * 14f + t * 1.5f) % 1f) < 0.7f ? new Color(0.95f, 0.85f, 0.2f) : new Color(0.2f, 0.1f, 0.05f);   // banner
            if (Box(u, v, 0.55f, 0.35f, 0.92f, 0.8f))
            {
                // the chart: bars rising one by one, an arrow up
                float x = (u - 0.55f) / 0.37f, y = (v - 0.35f) / 0.45f;
                int bar = Mathf.FloorToInt(x * 5f);
                float grown = Mathf.Clamp01((t % 6f) * 0.9f - bar * 0.6f);
                if ((x * 5f % 1f) > 0.25f && y < (0.15f + bar * 0.17f) * grown) return new Color(0.2f, 0.75f, 0.3f);
                return new Color(0.92f, 0.92f, 0.88f);
            }
            float sway = Mathf.Sin(t * 1.4f) * 0.02f;
            if (Box(u, v, 0.18f, 0.0f, 0.42f, 0.32f)) return new Color(0.35f, 0.22f, 0.12f);                // lectern
            if (Box(u, v, 0.22f + sway, 0.3f, 0.38f + sway, 0.55f)) return Box(u, v, 0.29f + sway, 0.38f, 0.31f + sway, 0.55f) ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.25f, 0.27f, 0.35f);   // suit + tie
            float hx = (u - 0.3f - sway) / 0.07f, hy = (v - 0.64f) / 0.09f;
            if (hx * hx + hy * hy < 1f)
            {
                if (hy > 0.5f) return new Color(0.9f, 0.75f, 0.2f);                                       // slick hair
                if (Mathf.Abs(hy + 0.35f) < 0.12f && Mathf.Abs(hx) < 0.5f) return Mathf.PerlinNoise(t * 6f, 2.1f) > 0.45f ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.4f, 0.1f, 0.1f);   // the grin
                return new Color(0.9f, 0.7f, 0.55f);
            }
            float point = Mathf.Max(0f, Mathf.Sin(t * 0.9f));                                              // the arm points at the chart
            var sh = new Vector2(0.38f + sway, 0.52f); var hand = sh + new Vector2(0.06f + 0.12f * point, 0.02f + 0.08f * point);
            var d = new Vector2(u, v) - sh; var seg = hand - sh;
            float along = Mathf.Clamp01(Vector2.Dot(d, seg) / seg.sqrMagnitude);
            if ((d - seg * along).magnitude < 0.018f) return new Color(0.25f, 0.27f, 0.35f);
            return back;
        }

        /// <summary>Grow your own: a sunny bed in time-lapse — rows sprouting, leaves opening, a watering can passing.</summary>
        static Color Garden(float u, float v, float t, int shot)
        {
            if (v > 0.55f)
            {
                float sd = new Vector2(u - 0.85f, v - 0.85f).magnitude;
                return sd < 0.07f ? new Color(1f, 0.95f, 0.6f) : Color.Lerp(new Color(0.6f, 0.8f, 0.95f), new Color(0.35f, 0.6f, 0.9f), (v - 0.55f) / 0.45f);
            }
            var soil = ((int)(v * 18f) % 3 == 0) ? new Color(0.3f, 0.2f, 0.12f) : new Color(0.4f, 0.27f, 0.15f);
            float grow = (t % 9f) / 9f;                                                                   // the time-lapse loops
            for (int i = 0; i < 6; i++)
            {
                float x = 0.12f + i * 0.155f, h = Mathf.Clamp01(grow * 1.4f - i * 0.05f) * 0.32f;
                if (Mathf.Abs(u - x) < 0.008f && v > 0.18f && v < 0.18f + h) return new Color(0.25f, 0.55f, 0.15f);
                float lx = u - x, ly = v - (0.18f + h * 0.7f);
                if (h > 0.08f && Mathf.Abs(ly + Mathf.Abs(lx) * 0.6f) < 0.02f && Mathf.Abs(lx) < h * 0.35f) return new Color(0.35f, 0.7f, 0.2f);
                if (grow > 0.75f && new Vector2(lx, v - (0.18f + h)).magnitude < 0.025f) return new Color(0.95f, 0.85f, 0.2f);   // the flower opens
            }
            float cx = (t * 0.12f) % 1.4f - 0.2f;
            if (Box(u, v, cx - 0.06f, 0.42f, cx + 0.04f, 0.5f)) return new Color(0.4f, 0.5f, 0.45f);        // watering can
            if (u < cx - 0.06f && u > cx - 0.16f && Mathf.Abs(v - (0.45f - (cx - 0.06f - u) * 0.6f)) < 0.01f) return new Color(0.4f, 0.5f, 0.45f);
            if (u > cx - 0.2f && u < cx - 0.12f && v < 0.38f && v > 0.18f && ((v * 30f + t * 8f + u * 50f) % 1f) < 0.25f) return new Color(0.6f, 0.75f, 0.95f);   // the water falling
            return soil;
        }
    }
}
