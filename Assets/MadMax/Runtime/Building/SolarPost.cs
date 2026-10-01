using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The solar street lamp's own panel and battery: charges its node's battery by day (cloud, rain, dust and
    /// snow cut it like a <see cref="SolarPanel"/>), so the lamp burns dusk to dawn with no grid. It can still be cabled.</summary>
    public class SolarPost : MonoBehaviour
    {
        public float rated = 60f;
        UtilityNode node;
        void Awake() => node = GetComponent<UtilityNode>();

        public static float Share
        {
            get
            {
                float sun = Mathf.Clamp01(1f - DayNight.Darkness * 1.1f);
                float sky = (1f - 0.65f * Atmosphere.CloudCover) * (Weather.Raining ? 0.4f : 1f) * (1f - 0.8f * Storms.Dust);
                return sun * sky * (Weather.LocalSnow > 0.5f ? 0.3f : 1f);
            }
        }

        void Update() { if (node) node.produce = rated * Share; }
    }
}
