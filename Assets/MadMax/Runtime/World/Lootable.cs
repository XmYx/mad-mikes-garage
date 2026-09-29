using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A searchable spot in a building (cupboard, shelf, toolbox, fridge, seed box). Searched once per world;
    /// the searched keys are saved.</summary>
    public class Lootable : MonoBehaviour, MadMax.Building.IInteractable
    {
        public static readonly HashSet<string> Searched = new HashSet<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Searched.Clear();

        public string key, table = "house", title = "CUPBOARD";
        /// <summary>Found for sure on top of the rolled table (a raider's armour).</summary>
        public readonly List<string> extra = new List<string>();
        /// <summary>Padlocked: needs a crowbar (pried open noisily) before it can be searched.</summary>
        public bool locked;
        void OnEnable() => MadMax.Game.WastelandGame.LootSpots.Add(this);
        void OnDisable() => MadMax.Game.WastelandGame.LootSpots.Remove(this);

        /// <summary>Lockers and crates in towns and bunkers are sometimes padlocked (deterministic per key).</summary>
        public static bool RollLocked(string key, string visual) => (visual == "locker" || visual == "crate") && ((key.GetHashCode() & 0x7fffffff) % 100) < 40;

        public string Prompt(MadMax.Game.WastelandGame g) => Searched.Contains(key) ? title + " (EMPTY)" : locked ? "[E] PRY OPEN " + title + (g.Inventory.GetItem("tool_crowbar") > 0 ? "" : " (NEED A CROWBAR)") : "[E] SEARCH " + title;

        /// <summary>Force the lock (crowbar in the pack): noise, a Strength roll, a little wear on the bar.</summary>
        public bool Pry(MadMax.Game.WastelandGame g)
        {
            if (!locked) return false;
            if (g.Inventory.GetItem("tool_crowbar") <= 0) { g.Toast("LOCKED - NEED A CROWBAR"); MadMax.Audio.Sfx.Play("lock", transform.position, 0.6f); return false; }
            MadMax.Audio.Sfx.Play("hit_metal", transform.position, 0.9f, 0.8f, 40f);
            g.WearTool("tool_crowbar", 0.02f);
            if (Random.value > 0.35f + g.Stats.Attribute(MadMax.RPG.Attr.Strength) * 0.08f) { g.Toast("THE LOCK HOLDS... TRY AGAIN"); return false; }
            locked = false;
            g.Toast("PRIED OPEN");
            g.Stats.Practice(MadMax.RPG.Skill.Salvaging, 2f);
            MadMax.Npc.NpcDirector.Instance?.Noise(transform.position, 25f);
            return true;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary || Searched.Contains(key)) return;
            if (locked && !Pry(g)) return;
            Searched.Add(key);
            MadMax.Net.NetSession.Instance?.SendSearched(key);
            var rnd = new System.Random(key.GetHashCode() ^ g.seed);
            var found = LootTables.Roll(table, rnd, MadMax.Game.GameRules.Current.loot, g.Stats.Attribute(MadMax.RPG.Attr.Perception));
            foreach (var x in extra) found.Add((x, 1));
            MadMax.Game.LastEngine.AddFinds(g, key, found);
            if (found.Count == 0) { g.Toast("NOTHING USEFUL"); return; }
            var names = new List<string>();
            foreach (var (id, n) in found)
            {
                if (id.StartsWith("res:")) { var t = (ResourceType)int.Parse(id.Substring(4)); g.Inventory.Add(t, n); names.Add(n + " " + ResourceInfo.Name(t)); }
                else if (id.StartsWith("part:")) { var part = g.SpawnPart(id.Substring(5), transform.position + Vector3.up, Quaternion.identity); if (part) { part.gameObject.AddComponent<Rigidbody>().mass = part.mass; MadMax.Net.NetSession.Instance?.SendLooseSpawn(part); names.Add(part.partId.ToUpperInvariant()); } }
                else { g.Inventory.AddItem(id, n); names.Add((n > 1 ? n + " " : "") + ItemCatalog.Name(id)); }
            }
            g.Toast("FOUND: " + string.Join(", ", names));
            g.Stats.Practice(MadMax.RPG.Skill.Salvaging, 2f);
        }
    }

    /// <summary>Weighted loot tables per building type.</summary>
    public static class LootTables
    {
        static readonly Dictionary<string, (string id, int min, int max, float w)[]> tables = new Dictionary<string, (string, int, int, float)[]>
        {
            { "house", new[] { ("tool_knife", 1, 1, 0.6f), ("med_bandage", 1, 3, 1.5f), ("med_disinfectant", 1, 1, 0.6f), ("cloth_hoodie", 1, 1, 0.4f), ("cloth_beanie", 1, 1, 0.4f), ("cloth_coat", 1, 1, 0.2f), ("cloth_shorts", 1, 1, 0.3f), ("cloth_sunhat", 1, 1, 0.3f), ("cloth_scarf", 1, 1, 0.3f), ("food_can", 1, 2, 3f), ("drink_soda", 1, 2, 2f), ("food_ration", 1, 1, 1f), ("med_pills", 1, 2, 1f), ("book_mechanics_1", 1, 1, 0.3f), ("book_chemistry", 1, 1, 0.3f), ("vhs_survival", 1, 1, 0.3f), ("res:6", 2, 5, 2f), ("res:4", 1, 3, 1f), ("seed_flower", 1, 3, 0.6f), ("dye_red", 1, 1, 0.2f), ("dye_blue", 1, 1, 0.2f), ("trophy_plate", 1, 1, 0.15f) } },
            { "kitchen", new[] { ("food_can", 1, 3, 4f), ("food_jam", 1, 1, 1f), ("drink_water", 1, 2, 2f), ("drink_soda", 1, 2, 2f), ("seed_tomato", 1, 3, 0.8f), ("seed_herbs", 1, 3, 0.8f), ("food_potato", 2, 4, 1f) } },
            { "farm", new[] { ("seed_corn", 2, 5, 3f), ("seed_potato", 2, 5, 3f), ("seed_carrot", 2, 4, 2f), ("seed_cabbage", 1, 3, 2f), ("seed_pumpkin", 1, 3, 1.5f), ("seed_sunflower", 1, 3, 1.5f), ("seed_cotton", 1, 3, 1.5f), ("seed_hemp", 1, 3, 1f), ("res:31", 1, 2, 0.6f), ("seed_berries", 1, 2, 1f), ("sapling_apple", 1, 1, 0.8f), ("sapling_pine", 1, 1, 0.5f), ("sapling_palm", 1, 1, 0.4f), ("seed_herbs", 1, 3, 1f), ("seed_flower", 1, 3, 1f), ("seed_wheat", 2, 5, 2f), ("tool_fishing_rod", 1, 1, 0.5f), ("tool_bow", 1, 1, 0.3f), ("ammo_arrow", 3, 8, 0.6f), ("tool_slingshot", 1, 1, 0.4f), ("bait_worms", 2, 6, 1f), ("seed_beet", 1, 4, 1.5f), ("seed_mushroom", 1, 3, 0.8f), ("tool_hoe", 1, 1, 0.5f), ("tool_watering_can", 1, 1, 0.5f), ("farm_fertilizer", 1, 3, 2f), ("tool_shovel", 1, 1, 0.5f), ("food_corn", 2, 5, 1.5f), ("trophy_skull", 1, 1, 0.3f), ("dye_yellow", 1, 1, 0.2f) } },
            { "tools", new[] { ("tool_wrench", 1, 1, 1f), ("tool_claw_hammer", 1, 1, 1f), ("tool_axe", 1, 1, 0.6f), ("tool_pickaxe", 1, 1, 0.5f), ("tool_shovel", 1, 1, 0.6f), ("res:1", 4, 10, 4f), ("res:20", 1, 4, 2f), ("res:21", 1, 3, 1.5f), ("res:8", 1, 3, 1.5f), ("ammo_shells", 3, 8, 0.8f) } },
            { "airfield", new[] { ("bp_aviation", 1, 1, 2f), ("res:7", 5, 20, 2f), ("res:23", 1, 4, 1f), ("tool_flare_gun", 1, 1, 0.4f), ("ammo_flare", 1, 3, 1f), ("book_mechanics_2", 1, 1, 0.3f),
                                  ("cloth_goggles", 1, 1, 0.6f), ("use_spark_plugs", 1, 2, 0.8f), ("tool_binoculars", 1, 1, 0.4f), ("res:6", 2, 6, 1f) } },
            { "garage", new[] { ("part:wheel_street", 1, 1, 1.5f), ("cloth_moto_helmet", 1, 1, 0.5f), ("part:wheel_offroad", 1, 1, 1f), ("part:radiator_car", 1, 1, 1f), ("part:exhaust_side_pipes", 1, 1, 0.6f), ("part:bumper_bull_bar", 1, 1, 0.5f), ("res:7", 5, 15, 3f), ("res:8", 2, 5, 2f), ("res:9", 2, 5, 2f), ("res:5", 2, 6, 2f), ("book_mechanics_2", 1, 1, 0.3f), ("vhs_driving", 1, 1, 0.3f), ("trophy_plate", 1, 1, 0.4f), ("trophy_hubcap", 1, 1, 0.4f), ("trophy_ornament", 1, 1, 0.2f), ("use_oil_filter", 1, 2, 1f), ("use_air_filter", 1, 2, 0.8f), ("use_spark_plugs", 1, 1, 0.8f), ("dye_red", 1, 2, 0.3f), ("dye_black", 1, 2, 0.3f) } },
            { "shop", new[] { ("med_bandage", 1, 4, 2f), ("med_splint", 1, 1, 0.6f), ("med_disinfectant", 1, 2, 1f), ("food_can", 2, 4, 4f), ("food_ration", 1, 2, 2f), ("drink_water", 1, 3, 3f), ("drink_soda", 1, 3, 3f), ("med_pills", 1, 3, 1.5f), ("seed_corn", 1, 3, 1f), ("seed_tomato", 1, 3, 1f), ("book_builder", 1, 1, 0.4f), ("book_scrapper", 1, 1, 0.4f), ("vhs_karate", 1, 1, 0.4f), ("vhs_demolition", 1, 1, 0.3f), ("throw_molotov", 1, 2, 0.4f), ("use_canteen", 1, 1, 0.5f) } },
            { "raider", new[] { ("ammo_cartridge", 2, 6, 1.5f), ("tool_pipe_pistol", 1, 1, 0.3f), ("tool_revolver", 1, 1, 0.12f), ("throw_smoke", 1, 2, 0.4f), ("tool_knife", 1, 1, 0.5f), ("cloth_vest_tyre", 1, 1, 0.4f), ("cloth_shin_guards", 1, 1, 0.3f), ("ammo_shells", 2, 8, 3f), ("res:7", 3, 12, 3f), ("res:1", 4, 15, 3f), ("food_ration", 1, 2, 1.5f), ("drink_water", 1, 2, 1.5f), ("throw_molotov", 1, 2, 1f), ("med_bandage", 1, 2, 1f), ("cloth_goggles", 1, 1, 0.3f), ("cloth_shoulder", 1, 1, 0.3f), ("tool_pipe_club", 1, 1, 0.4f), ("tool_machete", 1, 1, 0.3f), ("trophy_skull", 1, 1, 0.25f), ("trophy_ornament", 1, 1, 0.15f), ("res:31", 1, 2, 0.4f), ("res:33", 1, 2, 0.3f), ("bp_weapon_harpoon", 1, 1, 0.08f) } },
            { "bunker", new[] { ("tool_bolt_rifle", 1, 1, 0.15f), ("ammo_rifle", 2, 5, 0.8f), ("tool_flare_gun", 1, 1, 0.3f), ("tool_grapple", 1, 1, 0.25f), ("ammo_flare", 1, 3, 0.8f), ("cloth_vest_kevlar", 1, 1, 0.4f), ("cloth_gauntlets", 1, 1, 0.3f), ("food_ration", 1, 3, 3f), ("drink_water", 1, 3, 2.5f), ("ammo_shells", 3, 10, 2f), ("med_bandage", 1, 3, 1.5f), ("med_pills", 1, 2, 1f), ("med_splint", 1, 1, 0.6f), ("tool_lantern", 1, 1, 0.6f), ("tool_gas_torch", 1, 1, 0.3f), ("tool_pipe_shotgun", 1, 1, 0.15f), ("throw_molotov", 1, 2, 0.6f), ("kit_floodlight", 1, 1, 0.3f), ("book_gunsmith", 1, 1, 0.3f), ("vhs_survival", 1, 1, 0.4f), ("res:7", 5, 15, 1.5f), ("res:8", 2, 5, 1f), ("res:21", 1, 4, 1f), ("res:20", 1, 4, 1f),
                ("res:34", 1, 4, 0.8f), ("med_antibiotics", 1, 1, 0.4f), ("bp_weapon_mg", 1, 1, 0.2f), ("bp_weapon_flamer", 1, 1, 0.15f), ("bp_weapon_harpoon", 1, 1, 0.2f),
                ("bp_cargo_generator", 1, 1, 0.2f), ("bp_lights_search", 1, 1, 0.25f), ("bp_framepack", 1, 1, 0.2f), ("ammo_mg", 1, 2, 0.4f) } },
            { "cave", new[] { ("farm_fertilizer", 2, 5, 3f), ("food_can", 1, 2, 1f), ("tool_lantern", 1, 1, 0.5f), ("tool_pickaxe", 1, 1, 0.4f), ("res:3", 3, 8, 2f), ("throw_dynamite", 1, 2, 0.4f), ("book_charm", 1, 1, 0.2f), ("bait_insects", 2, 6, 1f) } },
            { "lockbox", new[] { ("res:21", 2, 6, 2f), ("res:23", 1, 3, 1f), ("med_pills", 1, 2, 2f), ("ammo_shells", 2, 6, 2f), ("book_gunsmith", 1, 1, 0.4f), ("bp_weapon_mg", 1, 1, 0.3f), ("trophy_plate", 1, 1, 0.8f), ("use_battery", 1, 1, 0.4f) } },
            { "office", new[] { ("misc_paper", 2, 6, 3f), ("book_gunsmith", 1, 1, 0.4f), ("book_mechanics_1", 1, 1, 0.4f), ("book_builder", 1, 1, 0.4f), ("res:21", 1, 3, 1f), ("res:4", 1, 2, 1f), ("drink_soda", 1, 1, 1f) } },
        };

        public static List<(string id, int n)> Roll(string table, System.Random rnd, int abundance, int perception)
        {
            var result = new List<(string, int)>();
            if (!tables.TryGetValue(table, out var t)) return result;
            float total = 0f; foreach (var e in t) total += e.w;
            int rolls = 1 + abundance + (rnd.NextDouble() < 0.1 * perception ? 1 : 0);
            for (int i = 0; i < rolls; i++)
            {
                if (rnd.NextDouble() < 0.18 - abundance * 0.06) continue;             // empty-handed roll
                double pick = rnd.NextDouble() * total;
                foreach (var e in t)
                {
                    pick -= e.w;
                    if (pick > 0) continue;
                    result.Add((e.id, rnd.Next(e.min, e.max + 1)));
                    break;
                }
            }
            return result;
        }
    }
}
