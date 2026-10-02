using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The winter plan: when autumn and winter arrive, a few seconds after the season toast, the pantry is
    /// counted (pack + the player's containers + any within 30 m) in days of eating, split into what keeps through the
    /// cold season at its storage spoil rate and what rots first — with the stations that would save it.</summary>
    public partial class WastelandGame
    {
        int pantrySeason = -1;
        float pantryAt = -1f;

        /// <summary>Hunger the player burns in a game day at the current rules (awake, average activity).</summary>
        public float HungerPerDay => 100f / (45f * 60f) * Rules.hungerRate * 1.15f * DayNight.DayMinutes * 60f;

        /// <summary>Days of food on hand: <paramref name="keeps"/> survives <paramref name="days"/> game days where it is
        /// stored, <paramref name="rots"/> spoils before that.</summary>
        public void Pantry(float days, out float keeps, out float rots)
        {
            float k = 0f, r = 0f, window = days * DayNight.DayMinutes;           // real minutes the food must last
            void Count(Inventory inv, float spoil)
            {
                foreach (var kv in inv.Items)
                {
                    var f = FoodLibrary.Get(kv.Key);
                    if (f == null || f.hunger <= 0f || kv.Value <= 0 || kv.Key == "food_rotten") continue;
                    bool lasts = f.spoilMinutes <= 0f || spoil <= 0f || f.spoilMinutes / spoil >= window;
                    if (lasts) k += f.hunger * kv.Value; else r += f.hunger * kv.Value;
                }
            }
            Count(Inventory, 1f);
            var me = Player ? Player.transform.position : Vector3.zero;
            foreach (var c in Container.All)
            {
                if (!c) continue;
                var p = c.GetComponent<Placeable>();
                bool mine = p && p.owner == Stats.name;
                if (!mine && (c.transform.position - me).sqrMagnitude > 30f * 30f) continue;
                Count(c.inventory, c.SpoilFactor);
            }
            float per = Mathf.Max(1f, HungerPerDay);
            keeps = k / per; rots = r / per;
        }

        /// <summary>The pantry line for the season that just began (null: nothing to say).</summary>
        public string PantryNote(int season)
        {
            if (season != 1 && season != 2) return null;
            float winter = Mathf.Max(1, Weather.DaysPerSeason) * (season == 1 ? 2f : 1f);   // autumn plans for autumn + winter
            Pantry(winter, out float keeps, out float rots);
            string need = Mathf.RoundToInt(winter) + " DAYS";
            string head = "PANTRY: " + keeps.ToString("0.#") + " DAYS OF FOOD THAT KEEPS (" + (season == 1 ? "TO SPRING " : "WINTER ") + need + ")";
            if (rots >= 0.5f) return head + ", " + rots.ToString("0.#") + " DAYS WILL ROT FIRST - SMOKE, CAN OR CELLAR IT";
            if (keeps < winter) return head + " - HUNT, FISH THE ICE OR STOCK A ROOT CELLAR";
            return head + " - THE LARDER WILL HOLD";
        }

        void UpdatePantry()
        {
            int s = Weather.Season;
            if (pantrySeason < 0 || !Ready) { pantrySeason = s; return; }
            if (s != pantrySeason) { pantrySeason = s; pantryAt = Time.unscaledTime + 4f; }
            if (pantryAt > 0f && Time.unscaledTime >= pantryAt)
            {
                pantryAt = -1f;
                if (!Rules.survival) return;
                string note = PantryNote(s);
                if (note == null) return;
                Toast(note);
                Journal.Add("PLAN", note);
            }
        }
    }
}
