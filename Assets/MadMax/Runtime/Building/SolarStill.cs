using System.Globalization;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Solar still (depth stage F, the unpowered tier of sea water): a black basin under a glass roof. [E] pours
    /// sea water (then dirty water) from the pack into the basin; the sun evaporates it and the condensate runs into the
    /// still's own cistern (a water node: pipe it to a tank), <see cref="PerHour"/> L per game hour in full sun, less under
    /// cloud and rain, nothing at night. Sea water leaves a salt crust (1 per 8 L). Piped into a network it also slowly
    /// distils the network's dirty water. [T] takes the water and the salt.</summary>
    public class SolarStill : MonoBehaviour, IInteractable, IPlaceState
    {
        public const float Basin = 20f, PerHour = 1.2f, SeaPerSalt = 8f, NetRate = 0.006f;
        /// <summary>Litres in the basin, and how many of them are sea water.</summary>
        public float brine, sea;
        public float salt;
        float lastT = -1f;
        UtilityNode node;

        void Awake() => node = GetComponent<UtilityNode>();

        /// <summary>0..1 sunshine on the glass (like a solar panel).</summary>
        public static float Sun
        {
            get
            {
                float sun = Mathf.Clamp01(1f - DayNight.Darkness * 1.1f);
                return sun * (1f - 0.65f * Atmosphere.CloudCover) * (Weather.Raining ? 0.3f : 1f) * (1f - 0.7f * Storms.Dust);
            }
        }

        void Update()
        {
            if (!node) return;
            float sun = Sun;
            node.desalRate = sun * NetRate;
            if (node.converted > 0f)
            {
                if ((node.convertedTaint & WaterTaint.Salt) != 0) salt += node.converted / SeaPerSalt;
                node.converted = 0f; node.convertedTaint = WaterTaint.None;
            }
            float now = DayNight.TotalDays * 24f;
            if (lastT < 0f || now < lastT) { lastT = now; return; }
            float hours = now - lastT;
            lastT = now;
            Distil(hours * sun);
        }

        /// <summary>Evaporate <paramref name="sunHours"/> hours of full sun's worth from the basin into the cistern.</summary>
        public float Distil(float sunHours)
        {
            float room = Mathf.Max(0f, node.waterCapacity - node.Water);
            float make = Mathf.Min(brine, Mathf.Min(room, PerHour * sunHours));
            if (make <= 0f) return 0f;
            float fromSea = brine > 0f ? make * sea / brine : 0f;
            sea = Mathf.Max(0f, sea - fromSea);
            brine -= make;
            node.clean += make;
            salt += fromSea / SeaPerSalt;
            return make;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            float w = UtilityGrid.NetWater(node, out _);
            return "SOLAR STILL: BASIN " + Mathf.RoundToInt(brine) + "/" + Basin + " L  WATER " + Mathf.RoundToInt(w) + " L" + (Sun < 0.1f ? " (NEEDS SUN)" : "")
                 + (brine < Basin - 0.5f ? "  [E] POUR IN SEA / DIRTY WATER" : "") + (w >= 1f || salt >= 1f ? "  [T] TAKE WATER" + (salt >= 1f ? " AND SALT" : "") : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary)
            {
                float poured = 0f;
                foreach (var t in new[] { ResourceType.SeaWater, ResourceType.DirtyWater })
                {
                    int n = Mathf.Min(Mathf.FloorToInt(Basin - brine), g.Inventory.Get(t));
                    if (n <= 0 || !g.Inventory.TrySpend(t, n)) continue;
                    brine += n; poured += n;
                    if (t == ResourceType.SeaWater) sea += n;
                }
                if (poured <= 0f) { g.Toast(brine >= Basin - 0.5f ? "THE BASIN IS FULL" : "NO SEA WATER OR DIRTY WATER TO POUR IN"); return; }
                MadMax.Audio.Sfx.Play("pour", transform.position, 0.6f, 0.9f);
                g.Toast("POURED " + Mathf.RoundToInt(poured) + " L INTO THE STILL");
                GetComponent<Placeable>()?.Dirty();
                return;
            }
            float got = UtilityGrid.Draw(node, 10f, out bool clean);
            int litres = Mathf.FloorToInt(got + 0.01f);
            int s = Mathf.FloorToInt(salt);
            if (litres > 0) g.Inventory.Add(clean ? ResourceType.Water : ResourceType.DirtyWater, litres);
            if (s > 0) { salt -= s; g.Inventory.Add(ResourceType.Salt, s); }
            g.Toast(litres > 0 || s > 0 ? "TOOK " + litres + " L WATER" + (s > 0 ? " AND " + s + " SALT" : "") : "NOTHING DISTILLED YET");
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState()
        {
            var ci = CultureInfo.InvariantCulture;
            return brine.ToString("0.##", ci) + ";" + sea.ToString("0.##", ci) + ";" + salt.ToString("0.##", ci);
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            var ci = CultureInfo.InvariantCulture;
            if (p.Length > 0) float.TryParse(p[0], NumberStyles.Float, ci, out brine);
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, ci, out sea);
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, ci, out salt);
        }
    }
}
