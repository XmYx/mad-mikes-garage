using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A placed TV: plays VHS tapes. While running the screen shows a picture (a small procedural programme
    /// picked by the tape: a road movie, a talking head, a cartoon, a workshop show) with scan roll, tracking noise and
    /// cuts, and casts its flickering light into the room (2026-10-15: the screen used to stay dark).</summary>
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
            show = id == null ? 0 : (id.GetHashCode() & 0x7fffffff) % 4;
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
    }
}
