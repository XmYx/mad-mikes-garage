using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Frost on the glass (2026-10-31, the 1029 suggestion; added by <see cref="VehicleDamage"/> with the grime).
    /// Below 0 °C a parked car's windows ice over — crystals grow in over a few minutes, thicker on clear nights, in snow
    /// and below −5 °C — drawn by the PixelVoxel / HDLit <c>_Frost</c> pattern on every "Glass" renderer. The heater
    /// clears it from the bottom up once the engine is warm (<c>_FrostLine</c> climbs the glass); air above +1 °C melts
    /// it; SCRAPE THE ICE (context menu, <see cref="StartScrape"/>) clears it by hand while you stay beside the car.
    /// Not saved: a loaded cold car starts frosted to its level.</summary>
    [DefaultExecutionOrder(220)]
    public class GlassFrost : MonoBehaviour
    {
        public const float FrostBelow = 0f, MeltAbove = 1f, GrowSeconds = 240f, ClearSeconds = 50f, ScrapeSeconds = 7f, ScrapeReach = 3f;
        static readonly int FrostId = Shader.PropertyToID("_Frost"), LineId = Shader.PropertyToID("_FrostLine");
        /// <summary>Frost cover 0..1 and how far up the glass the heater has cleared it (0..1).</summary>
        public float Frost { get; private set; }
        public float Cleared { get; private set; }
        public bool Scraping => scraper && Time.time < scrapeUntil;
        /// <summary>Cars that iced over, cars cleared (by heater or hand) and scrapes started this session (tests).</summary>
        public static int IcedOver, Defrosted, Scrapes;
        public static readonly List<GlassFrost> All = new List<GlassFrost>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        VehicleDriver driver;
        VehicleSystems sys;
        VehicleClimate climate;
        readonly List<Renderer> glass = new List<Renderer>();
        MaterialPropertyBlock mpb;
        float rescanT, applyT, shownFrost = -1f, shownLine = 1e9f, scrapeUntil;
        Transform scraper;
        bool iced, pristine = true;     // not driven or scraped since it was spawned / loaded: it sat out all night

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { IcedOver = 0; Defrosted = 0; Scrapes = 0; All.Clear(); }

        void Awake() { driver = GetComponent<VehicleDriver>(); sys = GetComponent<VehicleSystems>(); mpb = new MaterialPropertyBlock(); }

        /// <summary>The frost this weather builds to on a parked car (0 above freezing).</summary>
        public static float TargetFor(float air)
        {
            if (air >= FrostBelow) return 0f;
            float t = Mathf.Clamp01(0.45f + (FrostBelow - air) / 10f);                       // −5.5 °C covers it whole
            if (Weather.Snowing) t = Mathf.Max(t, 0.85f);
            if (DayNight.Darkness > 0.5f && !Weather.Raining) t = Mathf.Min(1f, t + 0.15f);  // clear nights frost hardest
            return t;
        }

        /// <summary>Scrape the glass by hand: the frost goes over <see cref="ScrapeSeconds"/> while
        /// <paramref name="who"/> stays within reach.</summary>
        public bool StartScrape(Transform who)
        {
            if (Frost < 0.05f || !who) return false;
            pristine = false;
            scraper = who; scrapeUntil = Time.time + ScrapeSeconds * Mathf.Max(0.4f, Frost) + 0.5f;
            Scrapes++;
            return true;
        }

        /// <summary>Tests: put the frost straight to <paramref name="f"/> (nothing cleared).</summary>
        public void Force(float f) { Frost = Mathf.Clamp01(f); Cleared = 0f; pristine = false; if (Frost > 0.3f) iced = true; applyT = 0f; }

        bool Warm => sys && driver && driver.Occupied && sys.Started && sys.fuel > 0f && sys.Temperature > 35f && (!climate || climate.on);

        void Update()
        {
            if (!driver) return;
            float dt = Time.deltaTime;
            if (!climate) climate = GetComponent<VehicleClimate>();
            float air = Weather.TemperatureAt(transform.position.z);
            float target = TargetFor(air);
            if (driver.Occupied) pristine = false;
            if (pristine && Frost < target) { Frost = target; iced = Frost > 0.3f; }   // a wreck or parked car found in the cold
            bool parked = !driver.Occupied || !sys || !sys.Started;
            if (air > MeltAbove) Frost = Mathf.MoveTowards(Frost, 0f, dt * (air - MeltAbove + 1f) / 90f);
            else if (parked && Frost < target) Frost = Mathf.MoveTowards(Frost, target, dt / GrowSeconds);
            if (parked && Frost > 0.05f && Cleared > 0f) Cleared = Mathf.MoveTowards(Cleared, 0f, dt / GrowSeconds);   // ices back over once the heat is gone
            if (Warm && Frost > 0f)
            {
                float heat = Mathf.Clamp01((sys.Temperature - 35f) / 40f);
                Cleared = Mathf.MoveTowards(Cleared, 1f, dt * (0.4f + heat) / ClearSeconds);
                if (Cleared >= 1f) { Frost = 0f; Cleared = 0f; Defrosted++; iced = false; }
            }
            if (Scraping)
            {
                if ((scraper.position - transform.position).sqrMagnitude > (ScrapeReach + 2.5f) * (ScrapeReach + 2.5f)) scrapeUntil = 0f;
                else
                {
                    Frost = Mathf.MoveTowards(Frost, 0f, dt / ScrapeSeconds);
                    MadMax.Audio.Sfx.Loop(this, "scratch", 0.45f, 1.5f + Mathf.Sin(Time.time * 9f) * 0.15f, 18f);
                    if (Random.value < dt * 6f && DebrisSystem.Instance) DebrisSystem.Instance.EmitPuff(scraper.position + scraper.forward * 0.6f + Vector3.up * 1.2f, new Color32(225, 232, 238, 255), 0.03f, Vector3.down, 0.4f);
                    if (Frost <= 0f) { scrapeUntil = 0f; Defrosted++; iced = false; Cleared = 0f; }
                }
            }
            if (!iced && Frost > 0.3f) { iced = true; IcedOver++; }
            if ((applyT -= dt) > 0f) return;
            applyT = 0.25f;
            if ((rescanT -= 0.25f) <= 0f) { rescanT = 5f; glass.Clear(); foreach (var r in GetComponentsInChildren<MeshRenderer>()) if (r.name == "Glass") glass.Add(r); }
            float line = -1e5f;
            if (Cleared > 0f && glass.Count > 0)
            {
                Bounds b = default; bool any = false;
                foreach (var r in glass) if (r) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (any) line = Mathf.Lerp(b.min.y - 0.05f, b.max.y + 0.05f, Cleared);
            }
            if (Mathf.Abs(Frost - shownFrost) < 0.01f && Mathf.Abs(line - shownLine) < 0.02f) return;
            shownFrost = Frost; shownLine = line;
            foreach (var r in glass)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(FrostId, Frost);
                mpb.SetFloat(LineId, line);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>The frost the renderers show and how many glass renderers there are (tests).</summary>
        public float ShownFrost => shownFrost;
        public int GlassRenderers => glass.Count;
    }
}
