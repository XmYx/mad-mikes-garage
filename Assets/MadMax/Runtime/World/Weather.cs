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
        public static float Temperature { get; private set; } = 25f;
        /// <summary>0 .. 1 how icy wet / packed-snow ground is.</summary>
        public static float Ice => Temperature < 0f ? Mathf.Clamp01(Mathf.Max(Wetness * 1.2f - 0.15f, Snow * 0.6f)) * Mathf.Clamp01(-Temperature / 3f + 0.4f) : 0f;
        public static bool SnowAllowed = true;
        public static int Frequency = 2;                         // 0 never .. 3 stormy
        public static int Season;                                // 0 summer, 1 autumn, 2 winter
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
        static void ResetStatics() { LakeRise = 0f; Wetness = 0; Snow = 0; Raining = false; Temperature = 25f; Frequency = 2; Season = 0; SnowAllowed = true; Auto = true; }

        public static void Restore(bool raining, float wetness, float snow = 0f, float temperature = float.NaN, float lakeRise = 0f)
        {
            Raining = raining; Wetness = wetness; Snow = snow; LakeRise = Mathf.Clamp(lakeRise, 0f, MaxLakeRise);
            if (!float.IsNaN(temperature)) Temperature = temperature;
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
            shape.scale = new Vector3(40f, 0.1f, 40f);
            go.transform.rotation = Quaternion.Euler(90f, 0, 0);   // emit downwards
            shape.rotation = Vector3.zero;
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
                main.startLifetime = 7f; main.startSpeed = 2.2f; main.startSize = 0.08f;
                main.startColor = new Color(1f, 1f, 1f, 0.9f);
                r.renderMode = ParticleSystemRenderMode.Billboard;
                noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f;
            }
            else
            {
                main.startLifetime = 1.2f; main.startSpeed = 22f; main.startSize = 0.05f;
                main.startColor = new Color(0.85f, 0.8f, 0.78f, 0.55f);
                r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 4f;
                noise.enabled = false;
            }
        }

        public static void Configure(int frequency, int season, bool snow)
        {
            Frequency = frequency; Season = season; SnowAllowed = snow; Wetness = 0f; Raining = false; LakeRise = 0f;
            Temperature = SeasonTemp;
            Snow = season == 2 && snow ? 0.6f : 0f;
        }

        static float SeasonTemp => Season == 0 ? 30f : Season == 1 ? 11f : -5f;

        void Update()
        {
            float dt = Time.deltaTime;
            bool authority = !(MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient);
            if (authority)
            {
                // temperature drifts slowly around the season value, colder while precipitating
                float drift = (Mathf.PerlinNoise(Time.time * 0.004f, tempNoise) - 0.5f) * 14f;
                Temperature = Mathf.MoveTowards(Temperature, SeasonTemp + drift - (Raining ? 3f : 0f), dt * 0.2f);
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
                var em = rain.emission;
                // cover what the camera sees: centre on the view's ground point, size and rate follow the zoom
                float span = 40f;
                var cam = follow ? follow.GetComponent<Camera>() : null;
                Vector3 centre = follow ? follow.position + follow.forward * 30f : Vector3.zero;
                if (cam)
                {
                    var t = DeformableTerrain.Instance;
                    float gy = t ? t.Height(follow.position.x, follow.position.z) : 0f;
                    var ray = new Ray(follow.position, follow.forward);
                    if (Mathf.Abs(ray.direction.y) > 0.05f) { float d = (gy - ray.origin.y) / ray.direction.y; if (d > 0f && d < 400f) centre = ray.GetPoint(d); }
                    span = cam.orthographic ? Mathf.Max(40f, cam.orthographicSize * 2.6f * cam.aspect) : Mathf.Clamp(Vector3.Distance(follow.position, centre) * 1.2f, 40f, 160f);
                }
                var shape = rain.shape;
                shape.scale = new Vector3(span, 0.1f, span);
                float area = span * span / 1600f;
                em.rateOverTime = Raining ? (snowing ? 900f : 2500f) * area : 0f;
                var main = rain.main;
                main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(6000 * area), 6000, 40000);
                rain.transform.position = new Vector3(centre.x, centre.y + 14f, centre.z);
            }
            if (sun) sun.intensity = DayNight.SunFactor * Mathf.Lerp(sunBase, sunBase * 0.55f, Raining ? Mathf.Min(1, Wetness * 3 + (snowing ? 0.6f : 0f)) : Wetness * 0.5f);
        }
    }
}
