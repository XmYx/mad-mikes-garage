using UnityEngine;

namespace MadMax.World
{
    /// <summary>Emergency lamp in a bunker or tunnel: always on (no daylight down there), cold light, a buzzing flicker;
    /// some are dying and stutter. Within the light budget.</summary>
    public class BunkerLamp : MonoBehaviour
    {
        public bool dying;
        public Color color = new Color(0.75f, 0.95f, 0.8f);
        Light lamp;
        float seed;

        void Start()
        {
            lamp = gameObject.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.shadows = LightShadows.None;
            lamp.color = color; lamp.range = 7f; lamp.intensity = 7f;
            seed = transform.position.x * 0.37f + transform.position.z * 0.11f;
        }

        void Update()
        {
            if (!lamp) return;
            float t = Time.time * (dying ? 9f : 2f) + seed;
            bool on = !dying || Mathf.PerlinNoise(t, 0.5f) > 0.42f;
            lamp.enabled = on && LightBudget.Allowed(lamp);
            lamp.intensity = 7f * (0.9f + 0.1f * Mathf.PerlinNoise(t * 3f, 2f));
            if (on) MadMax.Audio.Sfx.Loop(this, "generator", 0.04f, 2.2f, 6f);
            else MadMax.Audio.Sfx.Loop(this, "generator", 0f);
        }
    }
}
