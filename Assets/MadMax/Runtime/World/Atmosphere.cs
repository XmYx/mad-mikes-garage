using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Fog and clouds. <see cref="Fog"/> (0..1) rises for morning mist, evening haze, rain/snow and damp places
    /// (lakes, forest, tropics); CameraRig turns it into render fog per view. Clouds: drifting cloud shadows on everything
    /// (PixelVoxel globals <c>_MadMaxClouds</c>/<c>_MadMaxCloudOffset</c>) — sparse fair-weather cover or a heavy dark deck
    /// when it rains — plus voxel cloud puffs in the sky for the perspective views. Visuals (roadmap 16): the night sky
    /// (a dome of stars and the moon, perspective views), lightning (flash + bolt, see <see cref="Lightning"/>), and the
    /// grade pass (PixelArtCamera.Grade): colour per biome, weather and night, heat haze over hot ground by day.</summary>
    public class Atmosphere : MonoBehaviour
    {
        public static float Fog { get; private set; }
        public static float CloudCover { get; private set; }
        public static bool SkyVisible;             // set by CameraRig: perspective views show the puffs
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Fog = 0f; CloudCover = 0f; SkyVisible = false; flash = 0f; bolts = 0; forcedStrike = null; }

        static readonly int CloudsId = Shader.PropertyToID("_MadMaxClouds"), OffsetId = Shader.PropertyToID("_MadMaxCloudOffset");
        const float CloudScale = 1f / 70f;         // noise cells per metre
        const int PuffCount = 14;

        public Transform focus;
        Vector2 offset;
        float localDamp, dampTimer;
        readonly List<Transform> puffs = new List<Transform>();
        Material puffMat;
        readonly List<Mesh> puffMeshes = new List<Mesh>();

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            var g = MadMax.Game.WastelandGame.Instance;
            var f = focus ? focus : g && g.Current ? g.Current.transform : g && g.Player ? g.Player.transform : null;
            Vector3 at = f ? f.position : cam ? cam.transform.position : Vector3.zero;

            // damp places: sampled twice a second around the focus
            if ((dampTimer -= dt) <= 0f)
            {
                dampTimer = 0.5f;
                localDamp = 0f;
                var t = DeformableTerrain.Instance;
                if (t)
                {
                    var b = t.BiomeAt(at.x, at.z);
                    localDamp = b == Biome.Tropical ? 0.45f : b == Biome.Forest ? 0.3f : b == Biome.Nuclear ? 0.2f : 0f;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        if (!float.IsNaN(t.WaterLevel(at.x + Mathf.Cos(a) * 18f, at.z + Mathf.Sin(a) * 18f))) { localDamp = Mathf.Max(localDamp, 0.4f); break; }
                    }
                }
            }

            float h = DayNight.Hours;
            float morning = Bell(h, 6.3f, 1.6f), evening = Bell(h, 19.6f, 1.4f) * 0.55f;
            float precip = Weather.Raining ? (Weather.Snowing ? 0.8f : 0.65f) : 0f;
            float target = Mathf.Clamp01(Mathf.Max(morning * (0.45f + localDamp), evening * (0.6f + localDamp)) + precip + Weather.Wetness * 0.15f + localDamp * 0.25f
                                         + Storms.Dust * 0.95f + Storms.Rad * 0.45f);
            Fog = Mathf.MoveTowards(Fog, target, dt * (Storms.Dust > 0.05f ? 0.2f : 0.05f));

            float cover = Weather.Raining ? 0.9f : 0.3f + 0.18f * Mathf.Sin(Time.time * 0.01f);
            CloudCover = Mathf.MoveTowards(CloudCover, cover, dt * 0.02f);
            var wind = Fx.Wind;
            offset += new Vector2(wind.x, wind.z) * (dt * 2.2f * CloudScale);
            Shader.SetGlobalVector(CloudsId, new Vector4(CloudCover, Weather.Raining ? 0.5f : 0.24f, CloudScale, 0f));
            Shader.SetGlobalVector(OffsetId, new Vector4(-offset.x, -offset.y, 0f, 0f));

            UpdatePuffs(cam, dt);
            UpdateGrade(at, dt);
            UpdateNightSky(cam);
            UpdateLightning(cam, dt);
        }

        // ------------------------------------------------------------------ grade: biome / weather / night colour, heat haze
        Material grade;
        Vector3 gTint = Vector3.one; float gSat = 1f, gContrast = 1f, haze;
        static float flash; static int bolts;

        void UpdateGrade(Vector3 at, float dt)
        {
            if (Application.isBatchMode) return;
            if (!grade)
            {
                var src = Resources.Load<Material>("RuntimeMaterials/Grade");
                if (!src) return;
                grade = new Material(src);
                MadMax.Rendering.PixelArtCamera.Grade = grade;
            }
            var t = DeformableTerrain.Instance;
            var b = t ? t.BiomeAt(at.x, at.z) : Biome.Desert;
            Vector3 tint; float sat, con;
            switch (b)
            {
                case Biome.Desert: tint = new Vector3(1.05f, 1.0f, 0.92f); sat = 0.98f; con = 0.96f; break;
                case Biome.Forest: tint = new Vector3(0.96f, 1.02f, 0.98f); sat = 1.05f; con = 1f; break;
                case Biome.Tropical: tint = new Vector3(0.97f, 1.03f, 1f); sat = 1.04f; con = 0.98f; break;
                case Biome.Nuclear: tint = new Vector3(0.95f, 1.06f, 0.86f); sat = 0.85f; con = 1.08f; break;
                case Biome.Tundra: tint = new Vector3(0.93f, 0.98f, 1.06f); sat = 0.8f; con = 1.04f; break;
                default: tint = new Vector3(1.02f, 1f, 0.96f); sat = 0.98f; con = 0.97f; break;
            }
            if (MadMax.Game.TitleSequence.OnMoon) { tint = new Vector3(0.97f, 1f, 1.05f); sat = 0.85f; con = 1.12f; }   // airless: cold and hard
            bool moonSet = MadMax.Game.TitleSequence.OnMoon;
            float rain = Weather.Raining && !moonSet ? 1f : 0f, snow = Weather.Snowing && !moonSet ? 1f : 0f, night = DayNight.Darkness;
            tint = Vector3.Scale(tint, Vector3.Lerp(Vector3.one, new Vector3(0.95f, 0.98f, 1.04f), rain));
            tint = Vector3.Scale(tint, Vector3.Lerp(Vector3.one, new Vector3(1.0f, 1.02f, 1.06f), snow));
            tint = Vector3.Scale(tint, Vector3.Lerp(Vector3.one, new Vector3(0.9f, 0.95f, 1.1f), night));
            sat *= (1f - 0.2f * rain) * (1f - 0.12f * snow) * (1f - 0.2f * night);
            // storms: orange murk in a dust storm, a sick green glow in a radiation storm
            tint = Vector3.Scale(tint, Vector3.Lerp(Vector3.one, new Vector3(1.14f, 0.95f, 0.7f), Storms.Dust));
            tint = Vector3.Scale(tint, Vector3.Lerp(Vector3.one, new Vector3(0.9f, 1.1f, 0.84f), Storms.Rad));
            sat *= (1f - 0.3f * Storms.Dust) * (1f - 0.25f * Storms.Rad);
            con *= (1f - 0.05f * rain) * (1f - 0.1f * Fog);
            float k = 1f - Mathf.Exp(-0.8f * dt);                                                    // eases across a biome edge
            gTint = Vector3.Lerp(gTint, tint, k); gSat = Mathf.Lerp(gSat, sat, k); gContrast = Mathf.Lerp(gContrast, con, k);
            // heat haze over hot, dry ground in the day
            float hot = moonSet ? 0f : Mathf.Clamp01((Weather.Temperature - 27f) / 10f) * (1f - night) * (1f - rain) * (b == Biome.Desert ? 1f : b == Biome.Nuclear ? 0.7f : b == Biome.Town || b == Biome.City || b == Biome.Village ? 0.5f : 0.2f);
            haze = Mathf.MoveTowards(haze, hot, dt * 0.2f);
            flash = Mathf.Max(0f, flash - dt * 4f);
            grade.SetColor("_GradeTint", new Color(gTint.x, gTint.y, gTint.z, 1f));
            grade.SetFloat("_Saturation", gSat);
            grade.SetFloat("_Contrast", gContrast);
            grade.SetFloat("_Haze", haze);
            grade.SetFloat("_Flash", flash);
        }

        // ------------------------------------------------------------------ lightning
        Transform bolt;
        float boltUntil, secondFlash = -1f;

        /// <summary>A lightning strike: the scene flashes (twice), and in the perspective views a bolt stands in the sky.</summary>
        public static void Lightning() { flash = 1f; bolts++; }

        /// <summary>A strike rolled by the host at <paramref name="ground"/> (network clients): flash and bolt only, the
        /// host ran its effects.</summary>
        public static void LightningAt(Vector3 ground) { forcedStrike = ground; Lightning(); }
        static Vector3? forcedStrike;

        void UpdateLightning(Camera cam, float dt)
        {
            if (bolts > 0)
            {
                bolts = 0;
                secondFlash = Time.time + Random.Range(0.08f, 0.16f);
                // a real strike near the player (it seeks tall things, lights fires, electrifies metal)
                var g = MadMax.Game.WastelandGame.Instance;
                var focusPos = g && g.Current ? g.Current.transform.position : g && g.Player ? g.Player.transform.position : cam ? cam.transform.position : Vector3.zero;
                Vector3 ground;
                if (forcedStrike.HasValue) { ground = forcedStrike.Value; forcedStrike = null; }
                else { ground = Storms.PickStrike(focusPos); Storms.Strike(ground); }
                if (cam)
                {
                    if (!bolt) bolt = BuildBolt();
                    bolt.position = ground;
                    bolt.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    bolt.gameObject.SetActive(true);
                    boltUntil = Time.time + 0.22f;
                }
            }
            if (secondFlash > 0f && Time.time > secondFlash) { flash = Mathf.Max(flash, 0.7f); secondFlash = -1f; }
            if (bolt && bolt.gameObject.activeSelf && Time.time > boltUntil) bolt.gameObject.SetActive(false);
        }

        /// <summary>HD lightning: the same jagged path as glowing tubes, with its forks.</summary>
        static Mesh BoltHD()
        {
            var b = new MadMax.Rendering.HDShapes();
            var rr = new System.Random(707);
            float x = 0f, z = 0f;
            var prev = Vector3.zero;
            for (int y = 0; y < 80; y++)
            {
                if (rr.NextDouble() < 0.25) x += (float)(rr.NextDouble() * 2 - 1) * 2f;
                if (rr.NextDouble() < 0.25) z += (float)(rr.NextDouble() * 2 - 1) * 2f;
                var p = new Vector3(x, y, z) * 1.1f;
                if (y > 0) b.Tube(prev, p, 0.35f, 0.35f, y % 7 == 0 ? Pal.PaleBlue[4] : Pal.Cream[4], 6);
                if (y > 20 && y % 17 == 0) b.Tube(p, p + new Vector3(7f, -7f, 0f) * 1.1f, 0.25f, 0.08f, Pal.PaleBlue[3], 5);
                prev = p;
            }
            return b.ToMesh("LightningHD");
        }

        Transform BuildBolt()
        {
            var g = new VoxelGrid();
            var rr = new System.Random(707);
            float x = 0f, z = 0f;
            for (int y = 0; y < 80; y++)
            {
                if (rr.NextDouble() < 0.25) x += (float)(rr.NextDouble() * 2 - 1) * 2f;
                if (rr.NextDouble() < 0.25) z += (float)(rr.NextDouble() * 2 - 1) * 2f;
                g.Set(Mathf.RoundToInt(x), y, Mathf.RoundToInt(z), Pal.Solid(y % 7 == 0 ? Pal.PaleBlue[4] : Pal.Cream[4]));
                if (y > 20 && y % 17 == 0) for (int k = 1; k < 8; k++) g.Set(Mathf.RoundToInt(x) + k, y - k, Mathf.RoundToInt(z), Pal.Solid(Pal.PaleBlue[3]));   // a fork
            }
            var go = new GameObject("Lightning", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = MadMax.Rendering.HDBits.On ? BoltHD() : VoxelMesher.Build(g, "LightningBolt", 1.1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SkyMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        // ------------------------------------------------------------------ night sky
        Material skyMat;
        Transform stars, moon, sun;
        MeshFilter sunMesh;
        static readonly Mesh[] sunMeshes = new Mesh[3];

        Material SkyMaterial()
        {
            if (skyMat) return skyMat;
            var game = MadMax.Game.WastelandGame.Instance;
            if (!game || !game.propMaterial) return null;
            bool hd = MadMax.Rendering.HDBits.On;
            skyMat = new Material(hd ? MadMax.Rendering.HDShapes.Solid : game.propMaterial);
            if (hd) skyMat.SetFloat("_Cull", 0f);
            skyMat.SetFloat("_Unlit", 1f); skyMat.SetFloat("_OutlinePx", 0f); skyMat.SetFloat("_NoFog", 1f); skyMat.SetFloat("_SnowMask", 0f);
            return skyMat;
        }

        void UpdateNightSky(Camera cam)
        {
            if (!cam || Application.isBatchMode) return;
            float dark = DayNight.Darkness;
            float phase = DayNight.MoonPhase, lit = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            bool onMoon = MadMax.Game.TitleSequence.OnMoon || MadMax.Game.TitleSequence.SpaceFade > 0.6f;   // the title's climb and lunar set: stars, no moon
            bool sky = onMoon || SkyVisible && !OccluderFadeUnderground;               // perspective views see the sky, day and night
            if (sky && !stars) BuildSky();
            if (!stars) return;
            bool starsOn = sky && (onMoon || dark > 0.35f && CloudCover < 0.75f);
            if (stars.gameObject.activeSelf != starsOn) stars.gameObject.SetActive(starsOn);
            var dir = DayNight.MoonDirection;
            bool moonUp = sky && !onMoon && dir.y > -0.02f && lit > 0.06f && CloudCover < 0.9f;   // the moon rides by day too
            if (moon.gameObject.activeSelf != moonUp) moon.gameObject.SetActive(moonUp);
            var sd = DayNight.SunDirection;
            bool sunUp = sky && !onMoon && sd.y > -0.04f && CloudCover < 0.85f;
            if (sun.gameObject.activeSelf != sunUp) sun.gameObject.SetActive(sunUp);
            if (!sky) return;
            var c = cam.transform.position;
            stars.position = c;
            if (sunUp)
            {
                // white-gold at noon, orange low down, red on the horizon
                int si = sd.y > 0.35f ? 0 : sd.y > 0.1f ? 1 : 2;
                if (sunMesh.sharedMesh != SunMesh(si)) sunMesh.sharedMesh = SunMesh(si);
                sun.position = c + sd * 104f;
                sun.rotation = Quaternion.LookRotation(-sd);
            }
            if (!moonUp) return;
            // the moon on its orbit (DayNight), lit on the sun's side: eight phase meshes from crescent to full
            int pi = Mathf.Clamp(Mathf.RoundToInt(phase * 8f) % 8, 0, 7);
            if (moonPhaseShown != pi) { moonPhaseShown = pi; moon.GetComponent<MeshFilter>().sharedMesh = MoonMesh(pi); }
            moon.position = c + dir * 100f;
            moon.rotation = Quaternion.LookRotation(-dir);
        }

        static bool OccluderFadeUnderground => MadMax.Game.OccluderFade.Underground;

        /// <summary>The sun disc: a bright core with a softer rim, by height in the sky (0 high .. 2 on the horizon).</summary>
        static Mesh SunMesh(int kind)
        {
            if (sunMeshes[kind]) return sunMeshes[kind];
            var core = kind == 0 ? Pal.SunDay : kind == 1 ? Pal.LightY : Pal.SunDusk;
            var rim = kind == 0 ? Pal.LightW : kind == 1 ? Pal.Ochre[4] : Pal.Rust[4];
            if (MadMax.Rendering.HDBits.On)
            {
                var b = new MadMax.Rendering.HDShapes();
                b.Disc(Vector3.zero, Vector3.back, 3.1f, core, 48);
                for (int k = 0; k < 48; k++)                                                        // the glow ring
                {
                    float a0 = k * Mathf.PI * 2f / 48f, a1 = (k + 1) * Mathf.PI * 2f / 48f;
                    Ring(b, a0, a1, 3.1f, 4.2f, rim);
                }
                for (int i = 0; i < b.c.Count; i++) { var cc = b.c[i]; cc = b.v[i].magnitude > 3.05f ? rim : core; b.c[i] = cc; }
                return sunMeshes[kind] = b.ToMesh("SunHD" + kind);
            }
            var g = new VoxelGrid();
            for (int x = -8; x <= 8; x++)
            for (int y = -8; y <= 8; y++)
            {
                int d2 = x * x + y * y;
                if (d2 > 64) continue;
                if (d2 > 42 && ((x + y) & 1) == 0) continue;                                    // ragged glow at the edge
                g.Set(x, y, 0, Pal.Solid(d2 > 30 ? rim : core));
            }
            return sunMeshes[kind] = VoxelMesher.Build(g, "Sun" + kind, 0.5f);
        }

        int moonPhaseShown = -1;
        static readonly Mesh[] moonMeshes = new Mesh[8];

        /// <summary>The moon disc at phase index 0..7 (0 new, 4 full): the unlit side is dropped (waxing lit on the right).</summary>
        static Mesh MoonMesh(int phase)
        {
            if (moonMeshes[phase]) return moonMeshes[phase];
            float t = phase / 8f;                                    // 0..1 through the month
            float k = Mathf.Cos(t * Mathf.PI * 2f);                  // terminator: +1 new .. -1 full
            bool waxing = t < 0.5f;
            if (MadMax.Rendering.HDBits.On) return moonMeshes[phase] = MoonHD(k, waxing, phase);
            var mg = new VoxelGrid();
            for (int x = -6; x <= 6; x++)
            for (int y = -6; y <= 6; y++)
            {
                if (x * x + y * y > 36) continue;
                float half = Mathf.Sqrt(Mathf.Max(0f, 36f - y * y));
                float u = half > 0f ? x / half : 0f;                  // -1 left limb .. +1 right limb
                bool lit = waxing ? u > k : u < -k;
                if (!lit) continue;
                bool mare = (x - 2) * (x - 2) + (y - 1) * (y - 1) < 5 || (x + 2) * (x + 2) + (y + 3) * (y + 3) < 3 || (x + 1) * (x + 1) + (y - 3) * (y - 3) < 2;
                mg.Set(x, y, 0, Pal.Solid(mare ? Pal.Cream[1] : Pal.Cream[4]));
            }
            if (mg.Count == 0) mg.Set(6, 0, 0, Pal.Solid(Pal.Cream[4]));
            return moonMeshes[phase] = VoxelMesher.Build(mg, "Moon" + phase, 0.5f);
        }

        static void Ring(MadMax.Rendering.HDShapes b, float a0, float a1, float r0, float r1, Color32 col)
        {
            int i = b.v.Count;
            b.v.Add(new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0, 0f)); b.v.Add(new Vector3(Mathf.Cos(a0) * r1, Mathf.Sin(a0) * r1, 0f));
            b.v.Add(new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1, 0f)); b.v.Add(new Vector3(Mathf.Cos(a1) * r0, Mathf.Sin(a1) * r0, 0f));
            for (int q = 0; q < 4; q++) { b.n.Add(Vector3.back); b.c.Add(col); }
            b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2); b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 3);
        }

        /// <summary>HD moon: the lit part of a 3 m disc as rows of quads between the limb and the terminator (x = k × the
        /// half-chord), maria shaded per vertex.</summary>
        static Mesh MoonHD(float k, bool waxing, int phase)
        {
            var b = new MadMax.Rendering.HDShapes();
            const float R = 3f; const int rows = 40, cols = 14;
            for (int j = 0; j < rows; j++)
            {
                float y0 = -R + 2f * R * j / rows, y1 = -R + 2f * R * (j + 1) / rows;
                float h0 = Mathf.Sqrt(Mathf.Max(0f, R * R - y0 * y0)), h1 = Mathf.Sqrt(Mathf.Max(0f, R * R - y1 * y1));
                for (int c = 0; c < cols; c++)
                {
                    float u0 = c / (float)cols, u1 = (c + 1) / (float)cols;
                    // lit span of the row in limb units: waxing (k .. 1), waning (-1 .. -k)
                    float lo = waxing ? Mathf.Clamp(k, -1f, 1f) : -1f, hi = waxing ? 1f : Mathf.Clamp(-k, -1f, 1f);
                    if (hi <= lo) continue;
                    float a = Mathf.Lerp(lo, hi, u0), bb = Mathf.Lerp(lo, hi, u1);
                    var p00 = new Vector3(a * h0, y0, 0f); var p01 = new Vector3(a * h1, y1, 0f); var p11 = new Vector3(bb * h1, y1, 0f); var p10 = new Vector3(bb * h0, y0, 0f);
                    int i = b.v.Count;
                    foreach (var p in new[] { p00, p01, p11, p10 })
                    {
                        b.v.Add(p); b.n.Add(Vector3.back);
                        float x = p.x * 2f, y = p.y * 2f;                                           // the voxel moon's maria, in its units
                        bool mare = (x - 2) * (x - 2) + (y - 1) * (y - 1) < 5 || (x + 2) * (x + 2) + (y + 3) * (y + 3) < 3 || (x + 1) * (x + 1) + (y - 3) * (y - 3) < 2;
                        b.c.Add(mare ? Pal.Cream[1] : Pal.Cream[4]);
                    }
                    b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2); b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 3);
                }
            }
            if (b.v.Count == 0) b.Disc(new Vector3(R * 0.98f, 0f, 0f), Vector3.back, 0.05f, Pal.Cream[4], 4);
            return b.ToMesh("MoonHD" + phase);
        }

        void BuildSky()
        {
            var mat = SkyMaterial();
            if (!mat) return;
            // stars: tiny voxels scattered over the upper dome (one mesh)
            var g = new VoxelGrid();
            var rr = new System.Random(9001);
            for (int i = 0; i < 420; i++)
            {
                float az = (float)rr.NextDouble() * Mathf.PI * 2f, el = Mathf.Asin((float)rr.NextDouble() * 0.95f + 0.05f);
                var d = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el)) * 110f / 0.35f;
                double t = rr.NextDouble();
                var col = t < 0.1 ? Pal.PaleBlue[4] : t < 0.18 ? Pal.Ochre[4] : Pal.Cream[t < 0.6 ? 3 : 4];
                g.Set(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y), Mathf.RoundToInt(d.z), Pal.Solid(col));
            }
            var sg = new GameObject("Stars", typeof(MeshFilter), typeof(MeshRenderer));
            sg.transform.SetParent(transform, false);
            sg.GetComponent<MeshFilter>().sharedMesh = MadMax.Rendering.HDBits.On ? StarsHD() : VoxelMesher.Build(g, "Stars", 0.35f);
            var smr = sg.GetComponent<MeshRenderer>(); smr.sharedMaterial = mat;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false;
            stars = sg.transform;
            // the moon: a pale disc with dark maria
            var mg = new VoxelGrid();
            for (int x = -6; x <= 6; x++)
            for (int y = -6; y <= 6; y++)
            {
                if (x * x + y * y > 36) continue;
                bool mare = (x - 2) * (x - 2) + (y - 1) * (y - 1) < 5 || (x + 2) * (x + 2) + (y + 3) * (y + 3) < 3 || (x + 1) * (x + 1) + (y - 3) * (y - 3) < 2;
                mg.Set(x, y, 0, Pal.Solid(mare ? Pal.Cream[1] : Pal.Cream[4]));
            }
            var mo = new GameObject("Moon", typeof(MeshFilter), typeof(MeshRenderer));
            mo.transform.SetParent(transform, false);
            mo.GetComponent<MeshFilter>().sharedMesh = MadMax.Rendering.HDBits.On ? MoonMesh(4) : VoxelMesher.Build(mg, "Moon", 0.5f);
            var mmr = mo.GetComponent<MeshRenderer>(); mmr.sharedMaterial = mat;
            mmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mmr.receiveShadows = false;
            moon = mo.transform;
            var su = new GameObject("SunDisc", typeof(MeshFilter), typeof(MeshRenderer));
            su.transform.SetParent(transform, false);
            sunMesh = su.GetComponent<MeshFilter>();
            sunMesh.sharedMesh = SunMesh(0);
            var sr = su.GetComponent<MeshRenderer>(); sr.sharedMaterial = mat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
            sun = su.transform;
        }

        /// <summary>HD stars: small diamonds on the dome (same scatter as the voxel stars).</summary>
        static Mesh StarsHD()
        {
            var b = new MadMax.Rendering.HDShapes();
            var rr = new System.Random(9001);
            for (int i = 0; i < 420; i++)
            {
                float az = (float)rr.NextDouble() * Mathf.PI * 2f, el = Mathf.Asin((float)rr.NextDouble() * 0.95f + 0.05f);
                var d = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el)) * 110f;
                double t = rr.NextDouble();
                var col = t < 0.1 ? Pal.PaleBlue[4] : t < 0.18 ? Pal.Ochre[4] : Pal.Cream[t < 0.6 ? 3 : 4];
                b.Ellipsoid(d, Vector3.one * (0.1f + (float)rr.NextDouble() * 0.12f), col, 4, 2);
            }
            for (int i = 0; i < b.c.Count; i++) { var c = b.c[i]; c.a = 255; b.c[i] = c; }
            return b.ToMesh("StarsHD");
        }

        static float Bell(float x, float centre, float width) { float d = (x - centre) / width; return Mathf.Exp(-d * d); }

        void UpdatePuffs(Camera cam, float dt)
        {
            if (!cam || Application.isBatchMode) return;
            if (puffs.Count == 0) BuildPuffs();
            var c = cam.transform.position;
            var wind = Fx.Wind * 2.2f;
            var tint = Color.Lerp(new Color(1.15f, 1.12f, 1.08f), new Color(0.55f, 0.57f, 0.62f), Weather.Raining ? 1f : 0f);
            tint = DayNight.Tint(tint);
            if (puffMat) puffMat.SetColor("_Tint", new Color(tint.r, tint.g, tint.b, 1f));
            int visible = Mathf.RoundToInt(PuffCount * Mathf.Clamp01(CloudCover * 1.4f));
            for (int i = 0; i < puffs.Count; i++)
            {
                var p = puffs[i];
                bool on = SkyVisible && i < visible && !MadMax.Game.TitleSequence.OnMoon;
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
                if (!on) continue;
                var pos = p.position + wind * dt;
                var d = new Vector2(pos.x - c.x, pos.z - c.z);
                if (d.magnitude > 85f) { var r = Random.insideUnitCircle.normalized * 80f; pos = new Vector3(c.x - r.x, 0f, c.z - r.y); }   // recycle on the upwind side
                pos.y = c.y + 38f + (i % 4) * 6f;
                p.position = pos;
            }
        }

        void BuildPuffs()
        {
            var game = MadMax.Game.WastelandGame.Instance;
            if (!game || !game.propMaterial) return;
            bool hd = MadMax.Rendering.HDBits.On;
            puffMat = new Material(hd ? MadMax.Rendering.HDShapes.Solid : game.propMaterial);
            puffMat.SetFloat("_Unlit", hd ? 0.45f : 0.55f); puffMat.SetFloat("_OutlinePx", 0f); puffMat.SetFloat("_NoFog", 1f); puffMat.SetFloat("_SnowMask", 0f);
            var rnd = new System.Random(4242);
            for (int m = 0; m < 4; m++)
            {
                var g = new VoxelGrid();
                int blobs = 5 + m;
                for (int b = 0; b < blobs; b++)
                {
                    float cx = (float)(rnd.NextDouble() * 2 - 1) * (10 + m * 3), cz = (float)(rnd.NextDouble() * 2 - 1) * 6, rr = 4f + (float)rnd.NextDouble() * 4f;
                    for (int x = (int)(cx - rr); x <= cx + rr; x++)
                    for (int y = 0; y <= rr * 0.8f; y++)
                    for (int z = (int)(cz - rr); z <= cz + rr; z++)
                    {
                        float dx = x - cx, dy = y * 1.5f, dz = z - cz;
                        if (dx * dx + dy * dy + dz * dz <= rr * rr) g.Set(x, y, z, Pal.Solid(y > rr * 0.4f ? Pal.Cream[4] : Pal.Cream[2]));
                    }
                }
                if (hd)
                {
                    // HD: the same blobs as smooth cushions (flattened bottoms, brighter crowns)
                    var hb = new MadMax.Rendering.HDShapes();
                    var r2 = new System.Random(4242 + m * 31);
                    for (int b = 0; b < blobs; b++)
                    {
                        float cx = (float)(r2.NextDouble() * 2 - 1) * (10 + m * 3), cz = (float)(r2.NextDouble() * 2 - 1) * 6, rr = 4f + (float)r2.NextDouble() * 4f;
                        hb.Ellipsoid(new Vector3(cx, rr * 0.25f, cz) * 0.9f, new Vector3(rr, rr * 0.62f, rr) * 0.9f, Pal.Cream[3], 14, 9);
                    }
                    puffMeshes.Add(hb.ToMesh("CloudHD" + m));
                    continue;
                }
                puffMeshes.Add(VoxelMesher.Build(g, "CloudPuff" + m, 0.9f));
            }
            for (int i = 0; i < PuffCount; i++)
            {
                var go = new GameObject("Cloud", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = puffMeshes[i % puffMeshes.Count];
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = puffMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                var r = Random.insideUnitCircle * 80f;
                go.transform.position = new Vector3(r.x, 60f, r.y);
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.SetActive(false);
                puffs.Add(go.transform);
            }
        }

        void OnDestroy()
        {
            if (puffMat) Destroy(puffMat);
            if (skyMat) Destroy(skyMat);
            if (grade) { if (MadMax.Rendering.PixelArtCamera.Grade == grade) MadMax.Rendering.PixelArtCamera.Grade = null; Destroy(grade); }
            foreach (var m in puffMeshes) if (m) Destroy(m);
            Shader.SetGlobalVector(CloudsId, Vector4.zero);
        }
    }
}
