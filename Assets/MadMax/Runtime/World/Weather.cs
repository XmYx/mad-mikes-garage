using UnityEngine;

namespace MadMax.World
{
    /// <summary>Global weather: rain soaks the ground (mud), snow builds a white cover, freezing wet ground turns to ice.
    /// Precipitation comes and goes on its own (frequency and season from the game rules).</summary>
    public class Weather : MonoBehaviour
    {
        public static bool Raining;                              // any precipitation (rain or snow)
        public static bool Snowing => Raining && Temperature < 0.5f && SnowAllowed;
        public static float Wetness { get; private set; }
        public static float Snow { get; private set; }           // 0 .. 1 ground cover
        /// <summary>Air temperature where the player is (°C): the start latitude's weather plus the planet's climate
        /// offset for <see cref="FocusZ"/> (colder poleward, seasons flipped in the south).</summary>
        public static float Temperature => baseTemperature + LatitudeOffset(FocusZ);
        /// <summary>The weather's temperature at the start latitude (saved and sent over the network).</summary>
        public static float BaseTemperature => baseTemperature;
        static float baseTemperature = 25f;
        /// <summary>World z the local weather is for (the player / the driven vehicle; set by the game every frame).</summary>
        public static float FocusZ;
        public static float TemperatureAt(float z) => baseTemperature + LatitudeOffset(z);
        static float LatitudeOffset(float z)
        {
            float s0 = WorldGen.SeasonSign(0f);
            return WorldGen.ClimateOffset(z) + (SeasonTemp - 13.25f) * (s0 != 0f ? WorldGen.SeasonSign(z) / s0 - 1f : 0f);
        }
        /// <summary>Ground snow at a latitude: the weather's cover, or lasting snow wherever the air stays below −2 °C.</summary>
        public static float SnowAt(float z) => Mathf.Max(Snow, Mathf.Clamp01((-2f - TemperatureAt(z)) / 6f));
        /// <summary>Snow cover around the player.</summary>
        public static float LocalSnow => SnowAt(FocusZ);
        /// <summary>0 .. 1 how icy wet / packed-snow ground is.</summary>
        public static float Ice => Temperature < 0f ? Mathf.Clamp01(Mathf.Max(Wetness * 1.2f - 0.15f, Snow * 0.6f)) * Mathf.Clamp01(-Temperature / 3f + 0.4f) : 0f;
        public static bool SnowAllowed = true;
        public static int Frequency = 2;                         // 0 never .. 3 stormy
        public static int Season;                                // 0 summer, 1 autumn, 2 winter, 3 spring
        /// <summary>Game days per season (0: the season never changes); the year runs summer → autumn → winter → spring.</summary>
        public static int DaysPerSeason = 4, SeasonStart;
        /// <summary>0..1 through the current season.</summary>
        public static float SeasonProgress { get; private set; }
        static readonly int AutumnId = Shader.PropertyToID("_MadMaxAutumn");
        public static bool Auto = true;
        /// <summary>Lakes rise while it rains (m above their dry level) and drain slowly afterwards.</summary>
        public static float LakeRise { get; private set; }
        public const float MaxLakeRise = 0.7f;
        static readonly int SnowId = Shader.PropertyToID("_MadMaxSnow");

        public float soakSeconds = 30f;
        public float drySeconds = 120f;
        public float snowSeconds = 150f;

        ParticleSystem rain;
        Transform follow;
        Light sun;
        float sunBase, timer, tempNoise;
        bool snowParticles;

        void Awake() { timer = 60f; tempNoise = Random.value * 100f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { LakeRise = 0f; Wetness = 0; Snow = 0; Raining = false; baseTemperature = 25f; FocusZ = 0f; Frequency = 2; Season = 0; SnowAllowed = true; Auto = true; DaysPerSeason = 4; SeasonStart = 0; SeasonProgress = 0f; }

        public static void Restore(bool raining, float wetness, float snow = 0f, float temperature = float.NaN, float lakeRise = 0f)
        {
            Raining = raining; Wetness = wetness; Snow = snow; LakeRise = Mathf.Clamp(lakeRise, 0f, MaxLakeRise);
            if (!float.IsNaN(temperature)) baseTemperature = temperature;
        }

        public void Init(Transform camera, Light sunLight)
        {
            follow = camera;
            sun = sunLight;
            if (sun) sunBase = sun.intensity;
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 40f, 0.1f);                // a thin horizontal sheet (local Z = down)
            go.transform.rotation = Quaternion.Euler(90f, 0, 0);   // emit downwards
            shape.rotation = Vector3.zero;
            var vel = rain.velocityOverLifetime;                      // wind (and the 2.5D slant) blows the drops sideways
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            var em = rain.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var sh = Fx.RuntimeShader("ParticlesUnlit", "Universal Render Pipeline/Particles/Unlit");
            if (sh) r.sharedMaterial = new Material(sh) { color = new Color(0.8f, 0.75f, 0.72f, 0.6f) };
            SetParticleMode(false);
            var coll = rain.collision;                                   // roofs and awnings keep the rain out
            coll.enabled = true; coll.type = ParticleSystemCollisionType.World; coll.mode = ParticleSystemCollisionMode.Collision3D;
            coll.quality = ParticleSystemCollisionQuality.Low; coll.lifetimeLoss = 1f; coll.radiusScale = 0.5f;
            rain.Play();
        }

        void SetParticleMode(bool snow)
        {
            snowParticles = snow;
            var main = rain.main;
            var r = rain.GetComponent<ParticleSystemRenderer>();
            var noise = rain.noise;
            if (snow)
            {
                main.startLifetime = FallHeight(true) / 2.2f + 1f; main.startSpeed = 2.2f; main.startSize = 0.08f;
                main.startColor = new Color(1f, 1f, 1f, 0.9f);
                r.renderMode = ParticleSystemRenderMode.Billboard;
                noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f;
            }
            else
            {
                main.startLifetime = FallHeight(false) / 22f + 0.15f; main.startSpeed = 22f; main.startSize = 0.05f;
                main.startColor = new Color(0.85f, 0.8f, 0.78f, 0.55f);
                r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2f; r.velocityScale = 0.018f;   // streaks along the fall
                noise.enabled = false;
            }
        }

        /// <summary>Height of the precipitation layer above the ground under the view (m).</summary>
        static float FallHeight(bool snow) => snow ? 11f : 16f;

        static readonly Vector3[] viewCorners = { new Vector3(0f, 0f), new Vector3(1f, 0f), new Vector3(0f, 1f), new Vector3(1f, 1f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, 0f), new Vector3(0.5f, 1f) };

        /// <summary>Fits the emitting sheet over everything the camera can see between the ground and the top of the
        /// layer: every view-corner ray is cut at the ground and at the layer top (perspective rays stop at the fog), in
        /// a frame turned with the camera, so iso, tilt-shift, top-down and the perspective views are all filled.</summary>
        void CoverView(Camera cam, bool snowing)
        {
            var t = DeformableTerrain.Instance;
            var ct = cam.transform;
            Vector3 o = ct.position;
            float gy = t ? t.Height(o.x, o.z) : 0f;
            var centreRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (centreRay.direction.y < -0.05f) { float d = (gy - o.y) / centreRay.direction.y; if (d > 0f && d < 400f) { var g = centreRay.GetPoint(d); gy = t ? t.Height(g.x, g.z) : gy; } }
            float h = FallHeight(snowing), top = gy + h;
            float reach = cam.orthographic ? 400f : Mathf.Clamp(RenderSettings.fog ? RenderSettings.fogEndDistance : 80f, 30f, 90f);
            var q = Quaternion.Euler(0f, ct.eulerAngles.y, 0f); var inv = Quaternion.Inverse(q);
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            void Add(Vector3 p) { var l = inv * (p - o); mn = Vector2.Min(mn, new Vector2(l.x, l.z)); mx = Vector2.Max(mx, new Vector2(l.x, l.z)); }
            if (!cam.orthographic) Add(o);
            foreach (var v in viewCorners)
            {
                var ray = cam.ViewportPointToRay(v);
                foreach (float plane in new[] { gy, top })
                {
                    float d = Mathf.Abs(ray.direction.y) > 1e-3f ? (plane - ray.origin.y) / ray.direction.y : -1f;
                    Add(ray.GetPoint(d > 0f ? Mathf.Min(d, reach) : cam.orthographic ? 0f : reach));
                }
            }
            mn -= Vector2.one * 3f; mx += Vector2.one * 3f;
            var size = Vector2.Min(mx - mn, new Vector2(220f, 220f));
            var mid = (mn + mx) * 0.5f;
            var centre = o + q * new Vector3(mid.x, 0f, mid.y);
            rain.transform.SetPositionAndRotation(new Vector3(centre.x, top, centre.z), q * Quaternion.Euler(90f, 0f, 0f));
            var shape = rain.shape;
            shape.scale = new Vector3(size.x, size.y, 0.1f);
            // density per square metre; very large views thin out (far drops are below a pixel anyway)
            float area = size.x * size.y;
            float perM2 = (snowing ? 0.55f : 1.5f) * Mathf.Clamp(3000f / Mathf.Max(1f, area), 0.35f, 1f);
            var em = rain.emission;
            em.rateOverTime = Raining && !MadMax.Game.OccluderFade.Underground && !MadMax.Game.TitleSequence.OnMoon ? perM2 * area : 0f;
            var main = rain.main;
            main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(perM2 * area * main.startLifetime.constant * 1.2f), 2000, 40000);
            // wind slant; in the 2.5D views the rain also leans across the screen so it reads as diagonal lines
            var rig = MadMax.Game.WastelandGame.Instance ? MadMax.Game.WastelandGame.Instance.cameraRig : null;
            bool flat = rig && rig.TopDownView;
            var side = Fx.Wind * (snowing ? 0.9f : 0.6f) + (flat ? ct.right * (snowing ? 1.2f : 9f) : Vector3.zero);
            var vel = rain.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(side.x); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(side.z);
        }

        public static void Configure(int frequency, int season, bool snow, int daysPerSeason = 4)
        {
            Frequency = frequency; Season = season; SeasonStart = season; DaysPerSeason = daysPerSeason; SnowAllowed = snow; Wetness = 0f; Raining = false; LakeRise = 0f;
            baseTemperature = SeasonTemp;
            Snow = season == 2 && snow ? 0.6f : 0f;
        }

        static float SeasonTemp => Season == 0 ? 30f : Season == 1 ? 11f : Season == 2 ? -5f : 17f;

        /// <summary>The year turns with the days: season from the start season and the day count; autumn colours.</summary>
        static void UpdateSeason()
        {
            if (DaysPerSeason > 0)
            {
                float d = (DayNight.Day + DayNight.Hours / 24f) / DaysPerSeason;
                int s = (SeasonStart + Mathf.FloorToInt(d)) % 4;
                if (s != Season) { Season = s; MadMax.Game.WastelandGame.Instance?.Toast(SeasonNames[s] + " IS HERE"); }
                SeasonProgress = d - Mathf.Floor(d);
            }
            // foliage turns gold and rust through autumn, stays brown in winter, greens up in spring
            float autumn = Season == 1 ? SeasonProgress * 0.9f : Season == 2 ? 0.75f : Season == 3 ? Mathf.Max(0f, 0.75f - SeasonProgress * 1.5f) : 0f;
            Shader.SetGlobalFloat(AutumnId, autumn);
        }
        public static readonly string[] SeasonNames = { "SUMMER", "AUTUMN", "WINTER", "SPRING" };

        void Update()
        {
            float dt = Time.deltaTime;
            bool authority = !(MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient);
            UpdateSeason();
            if (authority)
            {
                // temperature drifts slowly around the season value, colder while precipitating
                float drift = (Mathf.PerlinNoise(Time.time * 0.004f, tempNoise) - 0.5f) * 14f;
                baseTemperature = Mathf.MoveTowards(baseTemperature, SeasonTemp + drift - (Raining ? 3f : 0f), dt * 0.2f);
                if (Auto && Frequency > 0)
                {
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        Raining = !Raining;
                        // average dry spell shrinks with frequency, showers last 1.5-5 min
                        timer = Raining ? Random.Range(90f, 300f) : Random.Range(1f, 2f) * new[] { 0f, 1500f, 600f, 240f }[Frequency];
                        MadMax.Net.NetSession.Instance?.SendWeather();
                    }
                }
                else if (Frequency == 0 && Auto) Raining = false;
            }

            bool snowing = Snowing;
            Shader.SetGlobalFloat(SnowId, Snow);
            LakeRise = Raining && !snowing ? Mathf.Min(MaxLakeRise, LakeRise + dt * MaxLakeRise / 300f) : Mathf.Max(0f, LakeRise - dt * MaxLakeRise / (Temperature < 0f ? 3000f : 900f));
            if (snowing)
            {
                Snow = Mathf.MoveTowards(Snow, 1f, dt / snowSeconds);
                Wetness = Mathf.MoveTowards(Wetness, 0.3f, dt / soakSeconds * 0.3f);
            }
            else
            {
                float target = Raining ? 1f : 0f;
                float rate = Raining ? 1f / soakSeconds : 1f / drySeconds;
                if (!Raining) rate *= Mathf.Lerp(0.3f, 1.6f, DayNight.SunFactor) * Mathf.Clamp(Temperature / 22f, 0.4f, 1.6f);   // the sun dries it out, nights keep the puddles
                if (Temperature < 0f && !Raining) rate *= 0.1f;                          // frozen ground stays icy
                Wetness = Mathf.MoveTowards(Wetness, target, rate * dt);
                if (Temperature > 0.5f && Snow > 0f)
                {
                    float melt = dt * Temperature * 0.0015f + (Raining ? dt * 0.004f : 0f);
                    Snow = Mathf.Max(0f, Snow - melt);
                    Wetness = Mathf.Min(1f, Wetness + melt * 1.5f);                    // slush
                }
            }
            if (rain)
            {
                if (snowing != snowParticles) SetParticleMode(snowing);
                var cam = follow ? follow.GetComponent<Camera>() : null;
                if (cam) CoverView(cam, snowing);
                else { var em = rain.emission; em.rateOverTime = 0f; }
            }
            if (sun) sun.intensity = DayNight.SunFactor * Mathf.Lerp(sunBase, sunBase * 0.55f, Raining ? Mathf.Min(1, Wetness * 3 + (snowing ? 0.6f : 0f)) : Wetness * 0.5f);
        }
    }
}
