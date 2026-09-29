using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Metal detector: sweeps the ground ahead and beeps faster over buried ore (WorldGen.OreAt); the HUD names
    /// the ore and its strength while held. Dig deep (shovel, excavator, dynamite) where it sings.</summary>
    public class MetalDetectorTool : HandTool
    {
        public static float Reading { get; private set; } = -1f;
        public static ResourceType Kind { get; private set; }
        float nextBeep;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Reading = -1f; Kind = ResourceType.None; }

        public override void Strike(PlayerCharacter user) { }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || g.World == null || !g.Player) { Reading = -1f; return; }
            // the coil sweeps a metre ahead of the feet
            var p = g.Player.transform.position + g.Player.transform.forward * 1f;
            float s = g.World.OreAt(p.x, p.z, out var kind);
            Kind = kind;
            Reading = Mathf.Lerp(Reading < 0f ? s : Reading, s, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (Time.time >= nextBeep && Reading > 0.05f)
            {
                MadMax.Audio.Sfx.Play("ding", p, 0.25f + Reading * 0.4f, 0.8f + Reading * 1.2f, 8f, 0f);
                nextBeep = Time.time + Mathf.Lerp(1.2f, 0.12f, Reading);
            }
        }

        void OnDisable() => Reading = -1f;
    }
}
