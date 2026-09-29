using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wire fish trap for shallow water (0.15 m+): baited, it catches small fish of the lake on its own every
    /// few minutes (one bait per catch, up to 4 waiting). [E] empties it, [T] adds a bait from the pack.</summary>
    public class FishTrap : MonoBehaviour, IInteractable, IPlaceState
    {
        public int bait, fish, glow;
        float timer = 120f;
        const int MaxCatch = 4, MaxBait = 6;

        bool InWater()
        {
            var t = DeformableTerrain.Instance;
            var p = transform.position;
            return t && t.WaterDepth(p.x, p.z) > 0.15f;
        }

        void Update()
        {
            if (bait <= 0 || fish + glow >= MaxCatch || !InWater()) return;
            if ((timer -= Time.deltaTime) > 0f) return;
            timer = Random.Range(90f, 180f);
            if (Random.value < 0.25f) return;                                     // nothing swam in this time
            var t = DeformableTerrain.Instance;
            var p = transform.position;
            var biome = t.BiomeAt(p.x, p.z);
            var lake = t.World.LakeAt(p.x, p.z, out _);
            bool toxic = lake != null ? lake.toxic : biome == Biome.Nuclear;
            bait--;
            if (toxic) glow++; else fish++;
            GetComponent<Placeable>()?.Dirty();
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (!InWater()) return "FISH TRAP: SET IT IN WATER";
            string s = "FISH TRAP  BAIT " + bait + "  CATCH " + (fish + glow);
            if (fish + glow > 0) s += "  [E] EMPTY";
            if (bait < MaxBait && HasBait(g) != null) s += "  [T] BAIT";
            return s;
        }

        static string HasBait(MadMax.Game.WastelandGame g)
        {
            foreach (var b in FishLibrary.Baits) if (g.Inventory.GetItem(b) > 0) return b;
            if (g.Inventory.GetItem("food_rotten") > 0) return "food_rotten";          // rotten scraps work in a trap
            return null;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                var b = HasBait(g);
                if (b == null || bait >= MaxBait || !g.Inventory.TakeItem(b)) return;
                bait++;
                g.Toast("BAITED THE TRAP (" + bait + ")");
            }
            else if (fish + glow > 0)
            {
                if (fish > 0) g.Inventory.AddItem("food_fish_raw", fish);
                if (glow > 0) g.Inventory.AddItem("food_fish_glow", glow);
                g.Toast("TOOK " + (fish + glow) + " FISH FROM THE TRAP");
                MadMax.Audio.Sfx.Play("splash", transform.position, 0.5f, 1.2f);
                g.Stats.Practice(MadMax.RPG.Skill.Survival, 0.5f * (fish + glow));
                fish = glow = 0;
            }
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => bait + ";" + fish + ";" + glow;

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            if (p.Length > 0) int.TryParse(p[0], out bait);
            if (p.Length > 1) int.TryParse(p[1], out fish);
            if (p.Length > 2) int.TryParse(p[2], out glow);
        }
    }
}
