using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Biogas digester (depth stage F): a sealed tank fed manure (<c>farm_manure</c>, 6 units) and rotten food
    /// (3 units) with [E]. Bacteria digest <see cref="PerHour"/> units per game hour (slower in the cold) into biogas
    /// (<see cref="GasPerUnit"/> L a unit, stored under the dome) and digestate that becomes fertilizer. [T] fills the
    /// gas into the pack and takes the fertilizer; a biogas generator within <see cref="HoseReach"/> m draws the gas by
    /// hose on its own.</summary>
    public class BiogasDigester : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<BiogasDigester> All = new List<BiogasDigester>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public const float MaxSlurry = 60f, MaxGas = 400f, PerHour = 2f, GasPerUnit = 25f, UnitsPerFertilizer = 12f, HoseReach = 8f;
        public const int ManureUnits = 6, RottenUnits = 3;
        public float slurry, gas, digestate;
        float lastT = -1f, fx;

        /// <summary>Digestion speed from the air temperature: bacteria like it warm.</summary>
        public static float Warmth => Mathf.Clamp(Mathf.InverseLerp(0f, 25f, Weather.Temperature), 0.15f, 1f);

        void Update()
        {
            float now = DayNight.TotalDays * 24f;
            if (lastT < 0f || now < lastT) { lastT = now; return; }
            float hours = now - lastT;
            lastT = now;
            if (Advance(hours) > 0f && (fx -= Time.deltaTime) <= 0f)
            {
                fx = Random.Range(2f, 4f);
                MadMax.World.Fx.Smoke(transform.TransformPoint(new Vector3(0f, 1.3f, 0f)), Vector3.up * 0.2f, 0.12f, new Color(0.55f, 0.6f, 0.45f, 0.25f), 1.2f);   // a whiff at the vent
            }
        }

        /// <summary>Digest for <paramref name="hours"/> game hours; returns the units digested.</summary>
        public float Advance(float hours)
        {
            if (hours <= 0f || slurry <= 0f || gas >= MaxGas) return 0f;
            float units = Mathf.Min(slurry, PerHour * Warmth * hours);
            units = Mathf.Min(units, (MaxGas - gas) / GasPerUnit);
            slurry -= units;
            gas += units * GasPerUnit;
            digestate += units;
            return units;
        }

        /// <summary>Take up to <paramref name="litres"/> of gas (a generator's hose).</summary>
        public float Draw(float litres)
        {
            float t = Mathf.Clamp(litres, 0f, gas);
            gas -= t;
            return t;
        }

        /// <summary>Feed from an inventory: manure first, then rotten food. Returns the units added.</summary>
        public float Feed(Inventory inv)
        {
            float added = 0f;
            foreach (var (id, per) in new[] { ("farm_manure", ManureUnits), ("food_rotten", RottenUnits) })
                while (slurry + per <= MaxSlurry + 0.01f && inv.TakeItem(id)) { slurry += per; added += per; }
            if (added > 0f) GetComponent<Placeable>()?.Dirty();
            return added;
        }

        int Fertilizer => Mathf.FloorToInt(digestate / UnitsPerFertilizer);

        public string Prompt(MadMax.Game.WastelandGame g) =>
            "BIOGAS DIGESTER: GAS " + Mathf.RoundToInt(gas) + "/" + MaxGas + " L  SLURRY " + Mathf.RoundToInt(slurry) + "/" + MaxSlurry + (Warmth < 0.5f && slurry > 0f ? " (SLOW: COLD)" : "")
            + (slurry < MaxSlurry - 2.9f ? "  [E] FEED MANURE / ROTTEN FOOD" : "") + (gas >= 1f || Fertilizer > 0 ? "  [T] FILL GAS" + (Fertilizer > 0 ? " + FERTILIZER" : "") : "");

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary)
            {
                float n = Feed(g.Inventory);
                if (n <= 0f) { g.Toast(slurry >= MaxSlurry - 2.9f ? "THE DIGESTER IS FULL" : "NO MANURE OR ROTTEN FOOD"); return; }
                MadMax.Audio.Sfx.Play("dig", transform.position, 0.6f, 0.8f);
                g.Soil(4f);
                g.Toast("FED THE DIGESTER (" + Mathf.RoundToInt(slurry) + "/" + MaxSlurry + ")");
                MadMax.Story.Story.Note("digester_fed");
                return;
            }
            int litres = Mathf.FloorToInt(gas);
            int f = Fertilizer;
            if (litres <= 0 && f <= 0) { g.Toast("NO GAS YET: IT TAKES A FEW HOURS"); return; }
            gas -= litres; digestate -= f * UnitsPerFertilizer;
            if (litres > 0) g.Inventory.Add(ResourceType.Biogas, litres);
            if (f > 0) g.Inventory.AddItem(ItemIds.Fertilizer, f);
            MadMax.Audio.Sfx.Play("hydraulic", transform.position, 0.4f, 1.3f);
            g.Toast("FILLED " + litres + " L BIOGAS" + (f > 0 ? " AND " + f + " FERTILIZER" : ""));
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState()
        {
            var ci = CultureInfo.InvariantCulture;
            return slurry.ToString("0.##", ci) + ";" + gas.ToString("0.#", ci) + ";" + digestate.ToString("0.##", ci);
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            var ci = CultureInfo.InvariantCulture;
            if (p.Length > 0) float.TryParse(p[0], NumberStyles.Float, ci, out slurry);
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, ci, out gas);
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, ci, out digestate);
        }
    }
}
