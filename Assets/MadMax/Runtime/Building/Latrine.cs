using System.Globalization;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Outhouse. Meals add up (<c>CharacterStats.waste</c>); using the latrine empties you into the pit instead
    /// of behind a bush (which costs hygiene). The pit composts over a game day: [T] digs out fertilizer for the garden.</summary>
    public class Latrine : MonoBehaviour, IInteractable, IPlaceState
    {
        public float fresh, ripe;                // waste units in the pit: composting / ready
        const float PerBag = 60f, MaxPit = 600f, RotPerHour = 4f;
        float lastT = -1f;

        int Bags => Mathf.FloorToInt(ripe / PerBag);

        public string Prompt(MadMax.Game.WastelandGame g) =>
            (g.Stats.waste >= 20f ? "[E] USE LATRINE" : "LATRINE") + (Bags > 0 ? "  [T] DIG OUT COMPOST (" + Bags + ")" : fresh > 1f ? "  (COMPOSTING)" : "");

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                int n = Bags;
                if (n <= 0) { g.Toast(fresh > 1f ? "STILL COMPOSTING" : "THE PIT IS EMPTY"); return; }
                ripe -= n * PerBag;
                g.Inventory.AddItem(MadMax.Items.ItemIds.Fertilizer, n);
                g.Soil(12f);
                MadMax.Audio.Sfx.Play("dig", transform.position, 0.7f);
                g.Toast("DUG OUT " + n + " FERTILIZER");
                GetComponent<Placeable>()?.Dirty();
                return;
            }
            if (g.Stats.waste < 20f) { g.Toast("YOU DON'T NEED TO GO"); return; }
            if (fresh + ripe >= MaxPit) { g.Toast("THE PIT IS FULL: DIG IT OUT [T]"); return; }
            MadMax.Audio.Sfx.Play("door_open", transform.position, 0.6f);
            MadMax.Game.ScreenFader.FadeThrough(() =>
            {
                fresh += g.Stats.waste;
                g.Stats.waste = 0f;
                g.Stats.Practice(MadMax.RPG.Skill.Survival, 0.5f);
                MadMax.Audio.Sfx.Play("door_close", transform.position, 0.6f);
                g.Toast("RELIEVED");
                GetComponent<Placeable>()?.Dirty();
            }, 0.8f);
        }

        void Update()
        {
            float now = DayNight.TotalDays * 24f;
            if (lastT < 0f || now < lastT) { lastT = now; return; }
            float rot = Mathf.Min(fresh, (now - lastT) * RotPerHour);
            lastT = now;
            if (rot <= 0f) return;
            fresh -= rot; ripe += rot;
        }

        public string SaveState() => fresh.ToString("0.#", CultureInfo.InvariantCulture) + ";" + ripe.ToString("0.#", CultureInfo.InvariantCulture);
        public void LoadState(string s)
        {
            var p = (s ?? "").Split(';');
            if (p.Length == 2) { float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out fresh); float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out ripe); }
        }
    }
}
