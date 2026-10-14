using UnityEngine;

namespace MadMax.World
{
    /// <summary>A drum fire in a town alley or a gang's hangout: flames, sparks and smoke out of the top, a warm
    /// flickering light. Gang barrels burn day and night, the rest from dusk to dawn; rain puts them out.</summary>
    public class BurnBarrel : MonoBehaviour
    {
        public bool gang;
        Light glow;
        float fxT;

        public bool Lit => (gang || DayNight.Hours >= 18.5f || DayNight.Hours < 6f) && !(Weather.Raining && !Weather.Snowing && Weather.Wetness > 0.7f);

        void Start()
        {
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 1.2f;
            glow.type = LightType.Point; glow.color = new Color(1f, 0.5f, 0.2f); glow.range = 6f; glow.shadows = LightShadows.None;
        }

        void Update()
        {
            bool lit = Lit;
            if (glow)
            {
                glow.enabled = lit && LightBudget.Allowed(glow);
                glow.intensity = 2f * (0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 8f, transform.position.z));
            }
            if (!lit) return;
            MadMax.Audio.Sfx.Loop(this, "fire", 0.25f, 1.2f, 14f);
            if ((fxT -= Time.deltaTime) > 0f) return;
            fxT = 0.1f;
            var p = transform.position + Vector3.up * 0.85f;
            Fx.Smoke(p + Random.insideUnitSphere * 0.1f, Vector3.up * 1.4f + Fx.Wind * 0.3f, Random.Range(0.2f, 0.35f), new Color(1f, Random.Range(0.45f, 0.7f), 0.15f, 0.9f), 0.5f);
            if (Random.value < 0.35f) Fx.Smoke(p + Vector3.up * 0.5f, Vector3.up * 0.9f + Fx.Wind * 0.5f, 0.5f, new Color(0.3f, 0.29f, 0.28f, 0.35f), 3f);
            if (Random.value < 0.12f) Fx.Sparks(p, Vector3.up, 2, new Color(1f, 0.6f, 0.2f));
        }
    }
}
