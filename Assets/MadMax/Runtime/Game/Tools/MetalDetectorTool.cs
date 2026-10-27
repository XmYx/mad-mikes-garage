using MadMax.Building;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Metal detector: sweeps the ground ahead and beeps faster over buried ore (WorldGen.OreAt); the HUD names
    /// the ore and its strength while held. Dig deep (shovel, excavator, dynamite) where it sings.
    /// 2026-10-26: it reads where its search coil actually is (swept across in a slow arc in front of the feet, so a
    /// deposit's edge comes and goes), the signal lamp on the control box flashes with each beep and glows steady over
    /// a strong find, and a strong pass glints in the ore's colour on the ground under the coil.</summary>
    public class MetalDetectorTool : HandTool
    {
        public static float Reading { get; private set; } = -1f;
        public static ResourceType Kind { get; private set; }
        float nextBeep, flash, sweep;
        Transform lamp;
        Renderer lampR;
        static Material lampOff, lampOn;
        static readonly Color LampDark = new Color(0.3f, 0.16f, 0.04f), LampLit = new Color(1f, 0.72f, 0.2f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Reading = -1f; Kind = ResourceType.None; lampOff = lampOn = null; }

        /// <summary>Signal lamp brightness 0..1 (tests).</summary>
        public float LampLevel => flash;
        /// <summary>Where the coil reads the ground now (world, tests).</summary>
        public Vector3 CoilPoint { get; private set; }

        public override void Strike(PlayerCharacter user) { }

        void Start()
        {
            float s = VoxelMesher.DefaultSize;
            lamp = PieceBits.Glow(transform, "SignalLamp", new Vector3(0f, -1f * s, 3.55f * s), new Vector3(0.05f, 0.05f, 0.03f), Pal.Amber);
            lampR = lamp.GetComponent<Renderer>();
            if (!lampOff) { lampOff = new Material(lampR.sharedMaterial); lampOff.SetColor("_BaseColor", LampDark); }
            if (!lampOn) { lampOn = new Material(lampR.sharedMaterial); lampOn.SetColor("_BaseColor", LampLit); }
            lampR.sharedMaterial = lampOff;
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || g.World == null || !g.Player) { Reading = -1f; return; }
            // the coil sweeps an arc a metre ahead of the feet
            sweep += Time.deltaTime * 1.6f;
            var pt = g.Player.transform;
            var p = pt.position + Quaternion.AngleAxis(Mathf.Sin(sweep) * 35f, Vector3.up) * pt.forward * 1f;
            CoilPoint = p;
            float s = g.World.OreAt(p.x, p.z, out var kind);
            Kind = kind;
            Reading = Mathf.Lerp(Reading < 0f ? s : Reading, s, 1f - Mathf.Exp(-6f * Time.deltaTime));
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);                          // fade before a new beep lights it (a long frame would eat the flash)
            if (Time.time >= nextBeep && Reading > 0.05f)
            {
                MadMax.Audio.Sfx.Play("ding", p, 0.25f + Reading * 0.4f, 0.8f + Reading * 1.2f, 8f, 0f);
                nextBeep = Time.time + Mathf.Lerp(1.2f, 0.12f, Reading);
                flash = 1f;
                if (Reading > 0.55f)
                {
                    var t = MadMax.World.DeformableTerrain.Instance;
                    float y = t ? t.Height(p.x, p.z) + 0.03f : p.y;
                    MadMax.World.Fx.Sparks(new Vector3(p.x, y, p.z), Vector3.up * 0.3f, 1, ResourceInfo.Color(kind));
                }
            }
            float hold = Reading > 0.7f ? 0.8f : 0f;                                             // a strong find: the lamp stays lit
            flash = Mathf.Max(hold, flash);
            if (lampR)
            {
                var want = flash > 0.4f ? lampOn : lampOff;
                if (lampR.sharedMaterial != want) lampR.sharedMaterial = want;
            }
        }

        void OnDisable() { Reading = -1f; flash = 0f; }
    }
}
