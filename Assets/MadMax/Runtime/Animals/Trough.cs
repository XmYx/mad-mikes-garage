using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Feed and water trough (roadmap 23 pens): [E] tips crops (and scraps, for pigs) from the pack into it
    /// and pours water; rain tops the water up. Once a day every kept animal whose home is within reach eats its
    /// share; fed animals give eggs and milk, grow and breed (<see cref="AnimalDirector"/>).</summary>
    public class Trough : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<Trough> All = new List<Trough>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public const float Cap = 24f, Reach = 14f;
        static readonly string[] Feeds = { "crop_wheat", "food_corn", "food_cabbage", "food_carrot", "food_potato", "food_beet", "food_pumpkin", "food_apple", "food_sunseeds", "food_rotten" };

        public float feed, water;
        float rainT;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Dung shovelled up around the pen (for the composter).</summary>
        public int manure;
        public const int ManureCap = 30;

        public string Prompt(WastelandGame g) => "[E] FILL TROUGH (FEED " + Mathf.FloorToInt(feed) + "/" + Cap + ", WATER " + Mathf.FloorToInt(water) + " L)" + (manure > 0 ? "  [T] SHOVEL MANURE (" + manure + ")" : "");

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                if (manure <= 0) return;
                g.Inventory.AddItem("farm_manure", manure);
                g.Toast("+" + manure + " MANURE (COMPOSTER: FERTILIZER)");
                MadMax.Audio.Sfx.Play("dig", transform.position, 0.5f);
                manure = 0;
                GetComponent<Placeable>()?.Dirty();
                return;
            }
            int items = 0, litres = 0;
            foreach (var f in Feeds)
                while (feed <= Cap - 2f && g.Inventory.TakeItem(f)) { feed += f == "food_pumpkin" ? 4f : 2f; items++; }
            while (water < Cap && (g.Inventory.TrySpend(ResourceType.Water, 1) || g.Inventory.TrySpend(ResourceType.DirtyWater, 1))) { water += 1f; litres++; }
            if (items + litres == 0) { g.Toast(feed >= Cap - 2f && water >= Cap ? "THE TROUGH IS FULL" : "BRING CROPS (WHEAT, CORN, CABBAGE...) OR WATER"); return; }
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.5f);
            g.Toast("TROUGH: +" + items + " FEED, +" + litres + " L WATER");
            GetComponent<Placeable>()?.Dirty();
        }

        void Update()
        {
            if ((rainT -= Time.deltaTime) > 0f) return;
            rainT = 10f;
            if (Weather.Raining && water < Cap && !Physics.Raycast(transform.position + Vector3.up * 0.6f, Vector3.up, 20f, ~0, QueryTriggerInteraction.Ignore)) water = Mathf.Min(Cap, water + 0.3f);
        }

        /// <summary>Take a day's ration: feed plus a little water.</summary>
        public bool Eat(float amount)
        {
            if (feed < amount || water < amount * 0.25f) return false;
            feed -= amount; water -= amount * 0.25f;
            GetComponent<Placeable>()?.Dirty();
            return true;
        }

        /// <summary>The trough nearest a pen's home spot (any fill), or null.</summary>
        public static Trough Nearest(Vector3 p)
        {
            Trough best = null; float bd = Reach * Reach;
            foreach (var t in All) { if (!t) continue; float d = (t.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = t; } }
            return best;
        }

        public static Trough Near(Vector3 p, float feedNeeded)
        {
            Trough best = null; float bd = Reach * Reach;
            foreach (var t in All)
            {
                if (!t || t.feed < feedNeeded) continue;
                float d = (t.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        public string SaveState() => feed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + water.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + manure;

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split('|');
            float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out feed);
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out water);
            if (p.Length > 2) int.TryParse(p[2], out manure);
        }
    }
}
