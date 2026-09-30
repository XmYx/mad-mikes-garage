using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Beehive (depth stage E): the colony makes honey and beeswax over game days (<see cref="DayNight.TotalDays"/>)
    /// by what there is to forage within 14 m — flowering beds and trees count most, a green biome some, the desert
    /// only after rain; cold weather slows it to a trickle and rain keeps the bees in. [E] takes the honey and wax; without
    /// a beekeeper's veil or a smoking torch in hand the bees sting and some honey is spilled.</summary>
    public class Beehive : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<Beehive> All = new List<Beehive>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public const float HoneyCap = 8f, WaxCap = 3f, Reach = 14f;
        /// <summary>Crops whose flowers feed bees (seed ids) and trees (sapling ids).</summary>
        static readonly HashSet<string> Flowering = new HashSet<string>
        {
            "seed_flower", "seed_sunflower", "seed_berries", "seed_pumpkin", "seed_herbs", "seed_tomato", "seed_cotton", "seed_hemp", "sapling_apple",
        };

        public float honey, wax, lastDays = -1f;
        /// <summary>Last worked-out forage (1 = a fair meadow).</summary>
        public float forage = 1f;
        float checkT, buzzT;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if ((checkT -= Time.deltaTime) > 0f) return;
            checkT = 2f;
            Tick();
            var g = MadMax.Game.WastelandGame.Instance;
            if ((buzzT -= 2f) <= 0f && g && g.Player && DayNight.Darkness < 0.5f && (g.Player.transform.position - transform.position).sqrMagnitude < 12f * 12f)
            {
                buzzT = Random.Range(6f, 12f);
                MadMax.Audio.Sfx.Play("insects", transform.position + Vector3.up * 0.8f, 0.35f, 1.4f, 12f);
            }
        }

        /// <summary>Work off the game time since the last look.</summary>
        public void Tick()
        {
            float now = DayNight.TotalDays;
            if (lastDays < 0f || lastDays > now) { lastDays = now; return; }
            float days = Mathf.Min(10f, now - lastDays);
            if (days < 0.01f) return;
            lastDays = now;
            forage = Forage();
            float temp = Weather.TemperatureAt(transform.position.z);
            float season = temp < 6f ? 0.3f : temp > 38f ? 0.6f : 1f;                                // cold: the colony clusters; heat: they fan the hive
            float wet = Weather.Raining ? 0.6f : 1f;
            honey = Mathf.Min(HoneyCap, honey + days * 0.8f * forage * season * wet);
            wax = Mathf.Min(WaxCap, wax + days * 0.25f * forage * season);
            GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>What the bees find: 0.35 anywhere, more in green country, most over flowering beds and fruit trees.</summary>
        public float Forage()
        {
            var p = transform.position;
            float f = 0.35f;
            var t = DeformableTerrain.Instance;
            if (t && t.World != null)
            {
                var b = t.BiomeAt(p.x, p.z);
                f += b == Biome.Forest || b == Biome.Tropical || b == Biome.Village ? 0.35f : b == Biome.Desert && Weather.Wetness > 0.35f ? 0.3f : b == Biome.Nuclear || b == Biome.City ? -0.15f : 0f;
            }
            float beds = 0f;
            foreach (var plot in GardenPlot.All)
                if (plot && plot.crop != null && plot.health > 0f && plot.growth > 0.3f && Flowering.Contains(plot.crop) && (plot.transform.position - p).sqrMagnitude < Reach * Reach) beds += 0.25f * plot.yieldScale;
            foreach (var tree in FindObjectsByType<PlantedTree>(FindObjectsSortMode.None))
                if (tree && tree.growth > 0.5f && Flowering.Contains(tree.sapling) && (tree.transform.position - p).sqrMagnitude < Reach * Reach) beds += 0.3f;
            return Mathf.Clamp(f + Mathf.Min(1.4f, beds), 0.2f, 2f);
        }

        string ForageName => forage < 0.5f ? "POOR" : forage < 0.9f ? "FAIR" : forage < 1.4f ? "GOOD" : "RICH";

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string s = "BEEHIVE  HONEY " + Mathf.FloorToInt(honey) + "/" + HoneyCap + "  WAX " + Mathf.FloorToInt(wax) + "  FORAGE " + ForageName;
            if (honey >= 1f || wax >= 1f) s = "[E] TAKE HONEY AND WAX" + (Protected(g) ? "" : " (NO VEIL OR SMOKE: THEY STING)") + "  " + s;
            return s;
        }

        /// <summary>A beekeeper's veil on, or a burning torch in hand (smoke calms them).</summary>
        public static bool Protected(MadMax.Game.WastelandGame g)
        {
            if (g.Player.Rig && g.Player.Rig.outfit.Contains("bee_veil")) return true;
            var tool = g.Player.Tool;
            return tool && (tool.id == "tool_torch" || tool.id == "tool_gas_torch");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            Tick();
            int h = Mathf.FloorToInt(honey), w = Mathf.FloorToInt(wax);
            if (h + w <= 0) { g.Toast("THE BEES NEED MORE TIME (FORAGE " + ForageName + ")"); return; }
            bool safe = Protected(g);
            int spilled = safe ? 0 : Mathf.CeilToInt(h * 0.3f);
            honey -= h; wax -= w;
            h -= spilled;
            if (h > 0) g.Inventory.Add(ResourceType.Honey, h);
            if (w > 0) g.Inventory.Add(ResourceType.Beeswax, w);
            if (!safe)
            {
                g.Vitals.Hurt(3f + h, "STINGS");
                MadMax.Audio.Sfx.Play("insects", transform.position + Vector3.up, 0.9f, 1.8f, 15f);
            }
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.4f, 0.8f);
            g.Stats.Practice(MadMax.RPG.Skill.Farming, 1f + h * 0.5f);
            g.HusbandryTally("honey", h);
            g.Toast((safe ? "" : "STUNG! ") + "+" + h + " HONEY, +" + w + " BEESWAX" + (safe ? "" : " (A VEIL OR A SMOKING TORCH CALMS THEM)"));
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => honey.ToString("0.##", CultureInfo.InvariantCulture) + ";" + wax.ToString("0.##", CultureInfo.InvariantCulture) + ";" + lastDays.ToString("0.####", CultureInfo.InvariantCulture);

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            if (p.Length > 0) float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out honey);
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out wax);
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out lastDays);
        }
    }
}
