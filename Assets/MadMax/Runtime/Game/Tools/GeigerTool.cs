using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Geiger counter: clicks faster with the dose rate (fallout zones, waste barrels, hot loot and ore)
    /// and shows the reading on the HUD while held.</summary>
    public class GeigerTool : HandTool
    {
        public static float Reading { get; private set; } = -1f;
        float nextClick;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Reading = -1f;

        public override void Strike(PlayerCharacter user) { }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || g.World == null) { Reading = -1f; return; }
            var p = transform.position;
            float dose = g.World.Radiation(p.x, p.z);
            foreach (var h in Hazard.All) if (h) dose += h.radiation * Mathf.Clamp01(1f - Vector3.Distance(h.transform.position, p) / 8f);
            // uranium deposits glow faintly through the ground
            float ore = g.World.OreAt(p.x, p.z, out var kind);
            if (kind == MadMax.Items.ResourceType.UraniumOre) dose += ore * 0.35f;
            Reading = Mathf.Lerp(Reading < 0f ? dose : Reading, dose, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (Time.time >= nextClick && Reading > 0.005f)
            {
                MadMax.Audio.Sfx.Play("click", p, Mathf.Clamp01(0.3f + Reading), Random.Range(1.6f, 2.4f), 6f, 0f);
                nextClick = Time.time + Random.Range(0.02f, 0.06f) / Mathf.Clamp(Reading, 0.02f, 2f);
            }
        }

        void OnDisable() => Reading = -1f;
    }
}
