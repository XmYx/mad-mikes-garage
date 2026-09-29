using UnityEngine;

namespace MadMax.World
{
    /// <summary>The red aircraft-warning lamp on top of a radio mast: blinks after dusk (a landmark for night drives and
    /// flights).</summary>
    public class MastLight : MonoBehaviour
    {
        Light lamp;
        float phase;

        void Start()
        {
            lamp = new GameObject("MastLamp").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0f, 152 * 0.16f, 0f);
            lamp.type = LightType.Point; lamp.color = new Color(1f, 0.15f, 0.1f); lamp.range = 10f; lamp.intensity = 4f; lamp.shadows = LightShadows.None;
            lamp.enabled = false;
            phase = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 1.5f;
        }

        void Update()
        {
            if (!lamp) return;
            bool on = DayNight.Darkness > 0.3f && Mathf.Repeat(Time.time + phase, 1.5f) < 0.6f;
            lamp.enabled = on && LightBudget.Allowed(lamp);
        }
    }
}
