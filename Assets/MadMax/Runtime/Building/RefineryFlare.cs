using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The refinery reads as working (2026-10-26): a pilot flame sits on the flare stack while it has power;
    /// a run of crude lights the flare up — a tall, wind-bent, flickering flame with sooty smoke, the stack's light
    /// throbbing with it — the fired heater glows and smokes at its burner, steam wisps off the column top, and the flare
    /// roars. Surges every few seconds (gas pockets). Runs after <see cref="PieceMotion"/> (its candle flicker would
    /// otherwise own the stack light).</summary>
    [DefaultExecutionOrder(100)]
    public class RefineryFlare : MonoBehaviour
    {
        const float VS = VoxelMesher.DefaultSize;
        static readonly Vector3 Tip = new Vector3(14f * VS, 50.5f * VS, -10f * VS);
        static readonly Vector3 Burner = new Vector3(2f * VS, 4.5f * VS, 15.6f * VS);
        static readonly Vector3 ColumnTop = new Vector3(-6f * VS, 44.5f * VS, -4f * VS);
        static readonly Color32 Outer = Pal.Hex("e8601c"), Mid = Pal.Hex("f6a030"), Core = Pal.Hex("fff0a8");
        static readonly Color Soot = new Color(0.12f, 0.11f, 0.1f, 0.5f);

        CraftingStation station;
        UtilityNode node;
        Light stackLight, burnerLight;
        float stackBase = 2f;
        Transform flame;
        readonly Transform[] tongues = new Transform[3];
        static readonly Vector3[] TongueSize = { new Vector3(0.34f, 1f, 0.34f), new Vector3(0.24f, 0.8f, 0.24f), new Vector3(0.13f, 0.55f, 0.13f) };
        float heat, surge, nextSurge, sootT, smokeT, steamT, phase;

        /// <summary>Flame size 0..~1.4: 0 = out, ~0.12 = pilot, 1 = flaring a run (tests).</summary>
        public float Flame => heat * (1f + surge);
        /// <summary>A job is running with power.</summary>
        public bool Working => station && station.Busy && station.Powered;
        /// <summary>Soot puffs sent up the flare so far (tests).</summary>
        public int SootPuffs { get; private set; }
        /// <summary>Height of the visible flame above the stack top in metres (tests).</summary>
        public float FlameHeight => flame && flame.gameObject.activeSelf ? tongues[0].localScale.y : 0f;

        void Start()
        {
            station = GetComponent<CraftingStation>();
            node = GetComponent<UtilityNode>();
            foreach (var l in GetComponentsInChildren<Light>(true)) if (l.name == "Light") { stackLight = l; stackBase = l.intensity; break; }
            phase = Random.Range(0f, 50f);
            nextSurge = Time.time + Random.Range(4f, 9f);
        }

        void Build()
        {
            flame = new GameObject("Flare").transform;
            flame.SetParent(transform, false);
            flame.localPosition = Tip;
            Color32[] cols = { Outer, Mid, Core };
            for (int i = 0; i < 3; i++) tongues[i] = PieceBits.Glow(flame, "Tongue" + i, Vector3.zero, Vector3.one * 0.01f, cols[i]);
            burnerLight = new GameObject("BurnerLight").AddComponent<Light>();
            burnerLight.transform.SetParent(transform, false);
            burnerLight.transform.localPosition = Burner + Vector3.forward * 0.25f;
            burnerLight.type = LightType.Point; burnerLight.range = 3f; burnerLight.intensity = 1.6f; burnerLight.shadows = LightShadows.None;
            burnerLight.color = new Color(1f, 0.5f, 0.2f);
            burnerLight.enabled = false;
        }

        void Update()
        {
            bool work = Working, pilot = node && node.Powered;
            float target = work ? 1f : pilot ? 0.12f : 0f;
            heat = Mathf.MoveTowards(heat, target, Time.deltaTime * (target > heat ? 0.6f : 0.35f));     // lights up, dies back
            float t = Time.time;
            if (work && t >= nextSurge) { surge = 0.5f; nextSurge = t + Random.Range(5f, 12f); MadMax.Audio.Sfx.Play("pop", transform.TransformPoint(Tip), 0.35f, 0.45f, 40f, 1f); }
            surge = Mathf.MoveTowards(surge, 0f, Time.deltaTime * 0.6f);
            bool near = PieceBits.Near(transform.position, 120f);
            MadMax.Audio.Sfx.Loop(this, "fire", work && near ? 0.25f + 0.25f * surge : 0f, 0.6f, 40f);
            if (!near) { if (flame) flame.gameObject.SetActive(false); if (burnerLight) burnerLight.enabled = false; return; }
            if (!flame) Build();
            float f = Flame;
            bool show = f > 0.01f;
            if (flame.gameObject.activeSelf != show) flame.gameObject.SetActive(show);
            if (show) Tongues(f, t);
            bool burn = heat > 0.5f;
            if (burnerLight.enabled != burn) burnerLight.enabled = burn && LightBudget.Allowed(burnerLight);
            if (burnerLight.enabled) burnerLight.intensity = 1.6f * heat * (0.75f + 0.5f * Mathf.PerlinNoise(t * 6f, phase));
            if (!work || !PieceBits.Near(transform.position, 45f)) return;
            float dt = Time.deltaTime;
            var wind = Fx.Wind;
            if ((sootT -= dt) <= 0f)
            {
                sootT = Random.Range(0.18f, 0.32f) / (1f + surge);
                var top = transform.TransformPoint(Tip) + Vector3.up * (tongues[0].localScale.y * 0.9f);
                Fx.Smoke(top, Vector3.up * (1.3f + surge) + wind * 0.5f, 0.45f + 0.3f * surge, Soot, 4f);
                SootPuffs++;
            }
            if ((smokeT -= dt) <= 0f)
            {
                smokeT = Random.Range(0.5f, 0.9f);
                var c = Pal.WorkshopDust; c.a = 90;
                Fx.Smoke(transform.TransformPoint(new Vector3(Burner.x, 10f * VS, 10f * VS)), Vector3.up * 0.7f + wind * 0.3f, 0.3f, c, 2.5f);
                if (Random.value < 0.4f) Fx.Sparks(transform.TransformPoint(Burner), transform.forward * 0.4f + Vector3.up * 0.3f, 1, Pal.Amber);
            }
            if ((steamT -= dt) <= 0f)
            {
                steamT = Random.Range(1.2f, 2.6f);
                var c = Pal.Steam; c.a = 70;
                Fx.Smoke(transform.TransformPoint(ColumnTop), Vector3.up * 0.4f + wind * 0.4f, 0.22f, c, 2f);
            }
        }

        /// <summary>Three nested tongues, each flickering on its own noise and leaning downwind.</summary>
        void Tongues(float f, float t)
        {
            var wind = Fx.Wind; wind.y = 0f;
            var up = (Vector3.up + wind * 0.09f).normalized;
            flame.rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, t * 40f, 0f);
            float h0 = Mathf.Lerp(0.1f, 1.25f, Mathf.Clamp01(f));
            for (int i = 0; i < 3; i++)
            {
                float n = Mathf.PerlinNoise(t * (6f + i * 3f), phase + i * 7f);
                var s = TongueSize[i];
                float h = Mathf.Max(0.02f, h0 * s.y * (0.75f + 0.5f * n) * (1f + surge * 0.6f));
                float w = Mathf.Max(0.01f, s.x * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(f)) * (0.9f + 0.2f * n));
                tongues[i].localScale = new Vector3(w, h, w);
                tongues[i].localPosition = new Vector3(0f, h * 0.5f, 0f);
            }
        }

        void LateUpdate()
        {
            if (!stackLight || !stackLight.enabled) return;
            float n = Mathf.PerlinNoise(Time.time * 7f, phase) * 0.6f + Mathf.PerlinNoise(Time.time * 17f, phase + 3f) * 0.4f;
            stackLight.intensity = stackBase * Mathf.Max(0.05f, Flame) * (0.7f + 0.6f * n);
        }
    }
}
