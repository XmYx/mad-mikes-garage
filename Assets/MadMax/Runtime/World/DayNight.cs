using UnityEngine;

namespace MadMax.World
{
    /// <summary>Day/night cycle: rotates and dims the sun, darkens shadows/ambient (shader global _MadMaxNight) and the sky.
    /// Length from the game rules (0 = endless day).</summary>
    public class DayNight : MonoBehaviour
    {
        public static float Hours { get; private set; } = 10f;          // 0..24
        public static float Darkness { get; private set; }               // 0 day .. 1 night
        public static float DayMinutes = 24f;                            // real minutes per game day (0 = frozen at noon-ish)
        static readonly int NightId = Shader.PropertyToID("_MadMaxNight");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Hours = 10f; Darkness = 0f; DayMinutes = 24f; Shader.SetGlobalFloat(NightId, 0f); }

        public Light sun;
        float sunBase;
        Quaternion sunBaseRot;

        public void Init(Light s)
        {
            sun = s;
            if (sun) { sunBase = sun.intensity; sunBaseRot = sun.transform.rotation; }
        }

        public static void SetHours(float h) { Hours = Mathf.Repeat(h, 24f); }

        /// <summary>Sky / fog colour at the current time.</summary>
        public static Color Tint(Color day) => Color.Lerp(day, new Color(0.05f, 0.06f, 0.12f), Darkness * 0.92f);

        void Update()
        {
            if (DayMinutes > 0f) Hours = Mathf.Repeat(Hours + Time.deltaTime * 24f / (DayMinutes * 60f), 24f);
            // sun elevation: -1 at midnight, 1 at noon; darkness ramps through dusk/dawn
            float elev = Mathf.Sin((Hours - 6f) / 24f * Mathf.PI * 2f);
            Darkness = Mathf.Clamp01((0.18f - elev) / 0.36f);
            Shader.SetGlobalFloat(NightId, Darkness);
            if (!sun) return;
            float weatherDim = sun.intensity > 0f && sunBase > 0f ? 1f : 1f;
            // keep the authored light direction's azimuth, swing its elevation through the day
            float yaw = sunBaseRot.eulerAngles.y + (Hours - 12f) * 7.5f;
            float pitch = Mathf.Lerp(8f, sunBaseRot.eulerAngles.x, Mathf.Clamp01(elev));
            sun.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            sun.color = Color.Lerp(new Color(1f, 0.95f, 0.85f), new Color(1f, 0.55f, 0.3f), Mathf.Clamp01(1f - elev * 2.5f));
            RenderSettings.ambientLight = Color.Lerp(new Color(0.5f, 0.45f, 0.4f), new Color(0.05f, 0.06f, 0.1f), Darkness) * weatherDim;
        }

        /// <summary>Multiplier the weather applies to the sun's base intensity.</summary>
        public static float SunFactor => 1f - Darkness * 0.94f;
    }
}
