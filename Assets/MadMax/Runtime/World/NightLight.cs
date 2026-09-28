using UnityEngine;

namespace MadMax.World
{
    /// <summary>Settlement lighting: street lamps and the odd lit window come on at night (within the light budget).</summary>
    public class NightLight : MonoBehaviour
    {
        public bool street;
        Light lamp;

        void Start()
        {
            lamp = gameObject.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.shadows = LightShadows.None;
            lamp.color = street ? new Color(1f, 0.78f, 0.45f) : new Color(1f, 0.85f, 0.6f);
            lamp.range = street ? 16f : 7f;
            lamp.intensity = street ? 30f : 8f;
            lamp.enabled = false;
        }

        void Update()
        {
            if (!lamp) return;
            bool flicker = !street || Mathf.PerlinNoise(Time.time * 3f, transform.position.x) > 0.08f;
            lamp.enabled = DayNight.Darkness > 0.3f && flicker && LightBudget.Allowed(lamp);
        }
    }
}
