using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A solar panel: power in daylight, less under cloud, rain, dust and snow, none at night. Pair it with a
    /// battery bank to keep the lights on after dark.</summary>
    public class SolarPanel : MonoBehaviour, IInteractable
    {
        public float rated = 400f;
        UtilityNode node;
        void Awake() => node = GetComponent<UtilityNode>();

        public float Share
        {
            get
            {
                float sun = Mathf.Clamp01(1f - DayNight.Darkness * 1.1f);
                float sky = (1f - 0.65f * Atmosphere.CloudCover) * (Weather.Raining ? 0.4f : 1f) * (1f - 0.8f * Storms.Dust);
                float snow = Weather.Snow > 0.5f ? 0.3f : 1f;                                      // snow on the glass
                return sun * sky * snow;
            }
        }

        void Update() { if (node) node.produce = rated * Share; }

        public string Prompt(MadMax.Game.WastelandGame g) => "SOLAR PANEL: " + Mathf.RoundToInt(node ? node.produce : 0f) + " W OF " + Mathf.RoundToInt(rated);
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { }
    }
}
