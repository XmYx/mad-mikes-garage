using System.Globalization;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The water filter's charcoal-and-sand cartridge (depth stage F): it wears with every litre it cleans —
    /// silt at 1×, sewage and fallout at 2×, oil at 5× (a new one cleans <see cref="Litres"/> L of silty water, 80 L of
    /// oily). Spent, the filter passes everything through dirty. [E] slots in a new cartridge (use_filter_cartridge,
    /// made at the workbench).</summary>
    public class FilterCartridge : MonoBehaviour, IPlaceState, IInteractable
    {
        public const string Item = "use_filter_cartridge";
        public const float Litres = 400f;
        /// <summary>1 new .. 0 spent.</summary>
        public float life = 1f;
        UtilityNode node;

        void Awake() => node = GetComponent<UtilityNode>();

        public bool Spent => life <= 0f;

        void Update()
        {
            if (!node || node.converted <= 0f) return;
            var t = node.convertedTaint;
            float wear = (t & WaterTaint.Oil) != 0 ? 5f : (t & (WaterTaint.Sewage | WaterTaint.Toxic)) != 0 ? 2f : 1f;
            float before = life;
            life = Mathf.Max(0f, life - node.converted * wear / Litres);
            node.converted = 0f; node.convertedTaint = WaterTaint.None;
            if (Mathf.CeilToInt(before * 10f) != Mathf.CeilToInt(life * 10f)) GetComponent<Placeable>()?.Dirty();
        }

        public string Prompt(MadMax.Game.WastelandGame g) =>
            (Spent ? "WATER FILTER: CARTRIDGE SPENT, WATER PASSES DIRTY" : "WATER FILTER: CARTRIDGE " + Mathf.CeilToInt(life * 100f) + "%") + "  [E] REPLACE CARTRIDGE";

        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) Replace(g); }

        /// <summary>Swap in a new cartridge from the pack.</summary>
        public bool Replace(MadMax.Game.WastelandGame g)
        {
            if (life >= 0.995f) { g.Toast("THE CARTRIDGE IS STILL NEW"); return false; }
            if (!g.Inventory.TakeItem(Item)) { g.Toast("NEED A FILTER CARTRIDGE (WORKBENCH: CHARCOAL, SAND, CLOTH)"); return false; }
            life = 1f;
            MadMax.Audio.Sfx.Play("click", transform.position, 0.7f, 0.8f);
            g.Stats.Practice(MadMax.RPG.Skill.Survival, 1f);
            g.Toast("NEW FILTER CARTRIDGE IN");
            MadMax.Story.Story.Note("filter_replaced");
            GetComponent<Placeable>()?.Dirty();
            return true;
        }

        public string SaveState() => life.ToString("0.###", CultureInfo.InvariantCulture);
        public void LoadState(string s) { if (!string.IsNullOrEmpty(s)) float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out life); }
    }
}
