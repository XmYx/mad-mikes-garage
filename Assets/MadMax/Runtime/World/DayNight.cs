using UnityEngine;
using MadMax.Voxel;

namespace MadMax.World
{
    /// <summary>Day/night cycle: rotates and dims the sun, darkens shadows/ambient (shader global _MadMaxNight) and the sky.
    /// Length from the game rules (0 = endless day).</summary>
    public class DayNight : MonoBehaviour
    {
        public static float Hours { get; private set; } = 10f;          // 0..24
        public static float Darkness { get; private set; }               // 0 day .. 1 night
        public static int Day { get; private set; }                      // whole days since the world began
        /// <summary>World age in game days (growth clocks: vegetation, overgrowth).</summary>
        public static float TotalDays => Day + Hours / 24f;
        /// <summary>Unit vector towards the sun / the moon (world: +X east, +Z north, +Y up), from the hour, the
        /// season's declination (the planet's tilt as it orbits the sun) and the player's latitude.</summary>
        public static Vector3 SunDirection { get; private set; } = Vector3.up;
        public static Vector3 MoonDirection { get; private set; } = Vector3.down;
        /// <summary>0 new moon .. 0.5 full .. 1 new again (a lunar month is <see cref="MoonDays"/> game days).</summary>
        public static float MoonPhase => Mathf.Repeat((TotalDays + 2.5f) / MoonDays, 1f);
        public const float MoonDays = 8f, Tilt = 23.4f;

        /// <summary>Direction to a body with this hour angle (degrees, 0 = due south at noon) and declination, seen
        /// from a latitude.</summary>
        public static Vector3 SkyDirection(float hourAngle, float declination, float latitude)
        {
            float H = hourAngle * Mathf.Deg2Rad, d = declination * Mathf.Deg2Rad, p = latitude * Mathf.Deg2Rad;
            float east = -Mathf.Cos(d) * Mathf.Sin(H);
            float north = Mathf.Sin(d) * Mathf.Cos(p) - Mathf.Cos(d) * Mathf.Sin(p) * Mathf.Cos(H);
            float up = Mathf.Sin(d) * Mathf.Sin(p) + Mathf.Cos(d) * Mathf.Cos(p) * Mathf.Cos(H);
            return new Vector3(east, up, north).normalized;
        }

        /// <summary>The sun's declination through the year: +Tilt at midsummer (the middle of season 0), −Tilt at
        /// midwinter.</summary>
        public static float Declination
        {
            get
            {
                float year = (Weather.Season + (Weather.DaysPerSeason > 0 ? Weather.SeasonProgress : 0.5f)) / 4f;
                return Tilt * Mathf.Cos((year - 0.125f) * Mathf.PI * 2f);
            }
        }
        public static float DayMinutes = 24f;                            // real minutes per game day (0 = frozen at noon-ish)
        static readonly int NightId = Shader.PropertyToID("_MadMaxNight");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Hours = 10f; Darkness = 0f; Day = 0; DayMinutes = 24f; Shader.SetGlobalFloat(NightId, 0f); }

        public Light sun;
        float sunBase;
        /// <summary>The sun's full daylight intensity (before night and weather).</summary>
        public float SunBase => sunBase;
        Quaternion sunBaseRot;

        public void Init(Light s)
        {
            sun = s;
            if (sun) { sunBase = sun.intensity; sunBaseRot = sun.transform.rotation; }
        }

        public static void SetHours(float h) { Hours = Mathf.Repeat(h, 24f); }
        public static void SetDay(int d) { Day = Mathf.Max(0, d); }

        /// <summary>Sky / fog colour at the current time.</summary>
        public static Color Tint(Color day)
        {
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(SunDirection.y - 0.12f) * 4f) * (1f - Darkness);
            return Color.Lerp(Color.Lerp(day, Pal.HazeDusk, dusk * 0.35f), Pal.HazeNight, Darkness * 0.92f);
        }

        void Update()
        {
            if (DayMinutes > 0f)
            {
                float h = Hours + Time.deltaTime * 24f / (DayMinutes * 60f);
                if (h >= 24f) Day++;
                Hours = Mathf.Repeat(h, 24f);
            }
            // the planet (user additions): the sun's path from the hour, the season's declination and the latitude —
            // high at the equator, low towards the poles, midnight sun in the polar summer, polar night in winter
            float lat = WorldGen.Latitude(Weather.FocusZ);
            SunDirection = SkyDirection((Hours - 12f) * 15f, Declination, lat);
            // the moon lags the sun by its phase: new moon rides with the sun, full moon rises at dusk
            MoonDirection = SkyDirection((Hours - 12f) * 15f - MoonPhase * 360f, Declination * 0.9f, lat);
            float elev = SunDirection.y;
            Darkness = Mathf.Clamp01((0.18f - elev) / 0.36f);
            Shader.SetGlobalFloat(NightId, Darkness);
            if (!sun) return;
            float weatherDim = sun.intensity > 0f && sunBase > 0f ? 1f : 1f;
            // light from the sun's direction, never flatter than 8° (low sun: long shadows; at night the moon's side)
            var toSun = elev > 0f ? SunDirection : MoonDirection.y > 0f ? MoonDirection : SunDirection;
            var flat = new Vector3(toSun.x, 0f, toSun.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            float el = Mathf.Max(8f, Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg);
            var dir = Quaternion.AngleAxis(-el, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
            sun.transform.rotation = Quaternion.LookRotation(-dir);
            sun.color = Color.Lerp(Color.Lerp(Pal.SunDay, Pal.SunDusk, Mathf.Clamp01(1f - elev * 2.5f)), Pal.MoonLight, Darkness);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.5f, 0.45f, 0.4f), new Color(0.05f, 0.06f, 0.1f), Darkness) * weatherDim;
        }

        /// <summary>Multiplier the weather applies to the sun's base intensity.</summary>
        public static float SunFactor => 1f - Darkness * 0.94f;
    }
}
