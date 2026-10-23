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

        /// <summary>Flies over a fresh pit (fewer in the cold and at night; a scoop of sawdust keeps most of them off).</summary>
        void Start() => Swarm.Attach(gameObject, Swarm.Kind.Flies, new Vector3(0f, 0.9f, 0.6f), 0.45f, 6,
            () => fresh < 1f || DayNight.Darkness > 0.6f || Weather.TemperatureAt(transform.position.z) < 4f ? 0f : Mathf.Clamp01(0.3f + fresh / 150f) * (Covered ? CoveredFlies : 1f));

        /// <summary>Sawdust toilet (2026-10-17): with a sack of sawdust in the pack a scoop goes down after each use. A
        /// covered pit draws a fraction of the flies and composts faster (carbon with the nitrogen) for a day.</summary>
        public float coveredUntil = -1f;
        public const float CoveredFlies = 0.15f, CoveredRot = 1.5f;
        public bool Covered => DayNight.TotalDays < coveredUntil;

        /// <summary>A scoop of sawdust from <paramref name="inv"/> over the pit (one sack lasts six uses).</summary>
        public bool Cover(MadMax.Items.Inventory inv)
        {
            if (inv == null || inv.GetItem(Sawdust.Sack) <= 0) return false;
            if (++scoops >= 6) { scoops = 0; inv.TakeItem(Sawdust.Sack); }
            coveredUntil = DayNight.TotalDays + 1f;
            GetComponent<Placeable>()?.Dirty();
            return true;
        }
        int scoops;

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
            PieceMotion.Use(gameObject, "open", 1.6f);
            MadMax.Game.ScreenFader.FadeThrough(() =>
            {
                fresh += g.Stats.waste;
                g.Stats.waste = 0f;
                g.Stats.Practice(MadMax.RPG.Skill.Survival, 0.5f);
                MadMax.Audio.Sfx.Play("door_close", transform.position, 0.6f);
                g.Toast(Cover(g.Inventory) ? "RELIEVED. A SCOOP OF SAWDUST AFTER" : "RELIEVED");
                GetComponent<Placeable>()?.Dirty();
            }, 0.8f);
        }

        void Update()
        {
            float now = DayNight.TotalDays * 24f;
            if (lastT < 0f || now < lastT) { lastT = now; return; }
            float rot = Mathf.Min(fresh, (now - lastT) * RotPerHour * (Covered ? CoveredRot : 1f));
            lastT = now;
            if (rot <= 0f) return;
            fresh -= rot; ripe += rot;
        }

        /// <summary>fresh;ripe (+ ;coveredUntil;scoops since 2026-10-18: a reload keeps the sawdust cover).</summary>
        public string SaveState() => F(fresh) + ";" + F(ripe) + ";" + coveredUntil.ToString("0.###", CultureInfo.InvariantCulture) + ";" + scoops;
        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        public void LoadState(string s)
        {
            var p = (s ?? "").Split(';');
            if (p.Length < 2) return;
            float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out fresh);
            float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out ripe);
            if (p.Length >= 4) { float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out coveredUntil); int.TryParse(p[3], out scoops); }
        }
    }
}
