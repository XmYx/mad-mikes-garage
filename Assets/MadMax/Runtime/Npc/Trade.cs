using System.Collections.Generic;
using MadMax.Designs;
using MadMax.Game;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Barter with scrap as the wasteland's money. Every good has a base value; vendors sell at a markup and
    /// buy at a discount that shrink with charisma, speech, how much they like you and a won haggle. Stock per trade
    /// kind (fuel, parts, scrap, food, salvage, building material) is rolled per vendor per game day; purchases
    /// count against it until the next day. Goods: item ids, <c>res:N</c> resources, <c>part:key</c> vehicle parts.</summary>
    public static class Trade
    {
        public struct Offer { public string id; public int count, price; }

        static Dictionary<string, PartDesign> parts;
        static PartDesign Part(string key)
        {
            if (parts == null) { parts = new Dictionary<string, PartDesign>(); foreach (var p in PartLibrary.All()) parts[p.key] = p; }
            return parts.TryGetValue(key, out var d) ? d : null;
        }

        /// <summary>Base value in scrap for one unit (one litre for fluids).</summary>
        public static float Value(string id)
        {
            if (id.StartsWith("res:")) return Value((ResourceType)int.Parse(id.Substring(4)));
            if (id.StartsWith("part:"))
            {
                var d = Part(id.Substring(5));
                if (d == null) return 30f;
                float v = 12f + d.mass * 0.25f + d.sizeClass * 8f;
                return d.category == MadMax.Vehicles.PartCategory.Engine ? v * 1.8f : v;
            }
            if (id.StartsWith("tool_")) return id == "tool_bolt_rifle" ? 120f : id == "tool_revolver" ? 90f : id == "tool_crossbow" ? 60f : id == "tool_pipe_pistol" ? 45f : id == "tool_flare_gun" ? 35f : id == "tool_bow" || id == "tool_leaf_blade" ? 30f : id.Contains("shotgun") ? 70f : id.Contains("gas_torch") || id.Contains("cutter") ? 45f : id.Contains("wrench") ? 30f : id.Contains("lantern") ? 18f : id.Contains("torch") ? 6f : 22f;
            if (id == Contracts.Chit) return ChitValue;
            if (id.StartsWith("ammo_")) return id == "ammo_flare" ? 8f : id == "ammo_rifle" ? 4f : id == "ammo_cartridge" ? 3f : id == "ammo_arrow" ? 1f : 2f;
            if (id.StartsWith("bait_")) return 1f;
            if (id.StartsWith("animal_")) return id == "animal_chick" ? 8f : id == "animal_calf" ? 45f : id == "animal_puppy" ? 30f : id == "animal_kid" ? 25f : 20f;
            if (id == "use_saddle") return 40f;
            if (id == "misc_bone") return 1f;
            if (id == "misc_feather") return 0.5f;
            if (id == "trophy_pelt") return 35f;
            if (id == "trophy_bearskin") return 80f;
            if (id == "trophy_antlers") return 22f;
            if (id == "misc_shell" || id == "misc_chitin") return 6f;
            if (id == "misc_venom") return 12f;
            if (id == "misc_silk") return 4f;
            if (id == "use_o2_bottle") return 10f;
            if (id == "bp_submarine") return 150f;
            if (id == "trophy_tusks" || id == "trophy_horns") return 20f;
            if (id == "food_fish_glow") return 18f;                                            // collectors pay for mutants
            if (id.StartsWith("trophy_fish")) return id.EndsWith("mutant") ? 60f : 25f;
            if (id.StartsWith("food_")) return id == "food_ration" ? 9f : id == "food_can" ? 6f : id == "food_rotten" ? 0f : id.Contains("stew") || id.Contains("pie") || id.Contains("soup") ? 8f : 3f;
            if (id.StartsWith("drink_")) return id == "drink_water" ? 4f : 3f;
            if (id.StartsWith("med_")) return id == "med_splint" ? 10f : id == "med_pills" ? 14f : 8f;
            if (id.StartsWith("book_")) return 16f;
            if (id.StartsWith("vhs_")) return 14f;
            if (id.StartsWith("seed_") || id.StartsWith("sapling_")) return 3f;
            if (id.StartsWith("kit_")) return 28f;
            if (id.StartsWith("cloth_")) { var cd = MadMax.Game.ClothingLibrary.Get(id); return cd?.armor != null ? 14f + cd.weight * 5f + cd.armor[1] * 40f : 10f; }
            if (id.StartsWith("throw_")) return 12f;
            if (id.StartsWith("farm_")) return 5f;
            if (id.StartsWith("use_")) return 8f;
            return 1f;
        }

        public static float Value(ResourceType t) => t switch
        {
            ResourceType.Scrap => 1f, ResourceType.Wood => 0.8f, ResourceType.Stone => 0.6f, ResourceType.Glass => 2f, ResourceType.Rubber => 2f,
            ResourceType.Cloth => 1.5f, ResourceType.Fuel => 2f, ResourceType.Oil => 3f, ResourceType.Coolant => 2f,
            ResourceType.IronOre or ResourceType.CopperOre or ResourceType.TinOre or ResourceType.Bauxite or ResourceType.Silica => 1.5f,
            ResourceType.Iron => 4f, ResourceType.Copper => 5f, ResourceType.Bronze => 6f, ResourceType.Aluminium => 6f, ResourceType.Charcoal => 1.5f,
            ResourceType.Asphalt => 2f, ResourceType.Concrete => 2.5f, ResourceType.Lime => 1.5f, ResourceType.Water => 0.8f, ResourceType.Ethanol => 3f,
            ResourceType.DirtyWater => 0.1f,
            ResourceType.Hide => 2f, ResourceType.Leather => 4f, ResourceType.Gunpowder => 6f, ResourceType.Sulfur => 3f, ResourceType.CrudeOil => 1.5f,
            ResourceType.Diesel => 2.2f, ResourceType.Tar => 1f, ResourceType.SeedOil => 2f, ResourceType.Coal => 1.2f, ResourceType.LeadOre => 1.5f,
            ResourceType.Lead => 4f, ResourceType.Acid => 3f, ResourceType.UraniumOre => 8f, _ => 0.5f
        };

        public static string Name(string id) => id.StartsWith("res:") ? ResourceInfo.Name((ResourceType)int.Parse(id.Substring(4)))
            : id.StartsWith("part:") ? id.Substring(5).Replace('_', ' ').ToUpperInvariant() : ItemCatalog.Name(id);

        /// <summary>0 (no advantage) .. 0.45 (best friends, silver tongue, won a haggle today).</summary>
        public static float Bargain(WastelandGame g, NpcSave s)
        {
            float b = g.Stats.Bargain + Mathf.Clamp(s.disposition, -50, 80) * 0.0015f + (s.haggleDay == MadMax.World.DayNight.Day ? 0.08f : 0f);
            return Mathf.Clamp(b, -0.1f, 0.45f);
        }

        /// <summary>The market being traded in (set when a trade page opens; null: the road).</summary>
        public static MadMax.World.Settlement Town;
        /// <summary>Fuel Guild chits count for this much scrap at fuel vendors.</summary>
        public const int ChitValue = 6;

        /// <summary>The faction the current trader answers to (prices by standing, roadmap 21).</summary>
        public static Faction Seller = Faction.None;
        public static int BuyPrice(string id, float bargain) => Mathf.Max(1, Mathf.CeilToInt(Value(id) * Market.Factor(Town, id) * (1.45f - bargain) * (TownQuests.Friend(Town) ? 0.9f : 1f) * Factions.PriceFactor(Seller)));
        public static int SellPrice(string id, float bargain) => Mathf.Max(0, Mathf.FloorToInt(Value(id) * Market.Factor(Town, id) * (Town == null ? 0.9f : 1f) * (0.45f + bargain * 0.8f) * (TownQuests.Friend(Town) ? 1.1f : 1f) / Factions.PriceFactor(Seller)));

        // ------------------------------------------------------------------ stock

        static readonly Dictionary<string, (string id, int min, int max)[]> sells = new Dictionary<string, (string, int, int)[]>
        {
            { "fuel", new[] { ("res:7", 30, 120), ("res:8", 8, 30), ("res:9", 8, 30), ("res:30", 0, 20), ("use_canteen", 0, 2) } },
            { "parts", new[] { ("part:wheel_street", 0, 2), ("part:wheel_offroad", 0, 2), ("part:wheel_small", 0, 1), ("part:radiator_car", 0, 1), ("part:exhaust_side_pipes", 0, 1),
                               ("part:bumper_bull_bar", 0, 1), ("part:engine_i4", 0, 1), ("part:engine_i6", 0, 1), ("part:engine_v8_blower", 0, 1), ("part:armor_plate", 0, 1),
                               ("part:armor_spikes", 0, 1), ("part:bumper_ram", 0, 1), ("part:cargo_jerry_rack", 0, 1), ("tool_wrench", 0, 1),
                               ("use_oil_filter", 0, 3), ("use_air_filter", 0, 3), ("use_spark_plugs", 0, 2), ("part:lights_emergency", 0, 1), ("part:wheel_monster", 0, 1) } },
            { "scrap", new[] { ("res:1", 20, 80), ("res:20", 2, 10), ("res:21", 1, 8), ("res:4", 4, 16), ("res:5", 4, 16), ("res:6", 4, 12), ("kit_wall_scrap", 0, 2), ("kit_barricade", 0, 2), ("tool_cutter", 0, 1) } },
            { "pack", new[] { ("food_can", 1, 4), ("drink_water", 2, 6), ("med_bandage", 1, 4), ("med_pills", 0, 2), ("ammo_shells", 0, 8), ("ammo_cartridge", 0, 6),
                              ("use_oil_filter", 0, 2), ("use_air_filter", 0, 1), ("dye_red", 0, 2), ("dye_blue", 0, 2), ("seed_tomato", 0, 3), ("res:6", 2, 8), ("res:32", 0, 4),
                              ("animal_puppy", 0, 1), ("animal_calf", 0, 1), ("use_saddle", 0, 1) } },
            { "food", new[] { ("food_can", 2, 8), ("food_ration", 1, 5), ("drink_water", 3, 10), ("drink_soda", 1, 6), ("food_potato", 2, 8), ("food_corn", 2, 8), ("food_stew", 0, 3),
                              ("seed_corn", 0, 4), ("seed_tomato", 0, 4), ("seed_potato", 0, 4), ("res:28", 10, 40),
                              ("animal_chick", 0, 4), ("animal_piglet", 0, 1), ("animal_kid", 0, 1), ("food_egg", 0, 6), ("drink_milk", 0, 3) } },
            { "salvage", new[] { ("med_bandage", 1, 5), ("med_pills", 0, 3), ("med_splint", 0, 2), ("med_disinfectant", 0, 2), ("ammo_shells", 5, 20), ("throw_molotov", 0, 3),
                                 ("tool_torch", 1, 3), ("tool_lantern", 0, 2), ("tool_gas_torch", 0, 1), ("tool_pipe_shotgun", 0, 1), ("tool_machete", 0, 1),
                                 ("book_charm", 0, 1), ("book_mechanics_1", 0, 1), ("vhs_salesman", 0, 1), ("vhs_driving", 0, 1), ("kit_floodlight", 0, 1) } },
            { "build", new[] { ("res:2", 20, 80), ("res:3", 20, 80), ("res:26", 5, 30), ("res:27", 5, 20), ("res:10", 10, 40), ("res:11", 10, 40), ("kit_chest", 0, 2), ("kit_wall_scrap", 0, 3) } },
        };

        /// <summary>What a vendor buys, by item prefix or res:N.</summary>
        static readonly Dictionary<string, string[]> buys = new Dictionary<string, string[]>
        {
            { "fuel", new[] { "res:7", "res:8", "res:9", "res:30", "res:5" } },
            { "parts", new[] { "res:20", "res:21", "res:23", "res:5", "tool_" } },
            { "scrap", new[] { "res:4", "res:5", "res:6", "res:15", "res:16", "res:17", "res:18", "res:20", "res:21", "res:22", "res:23", "kit_" } },
            { "food", new[] { "food_", "drink_", "seed_", "res:28" } },
            { "pack", new[] { "food_", "crop_", "med_", "cloth_", "misc_", "trophy_", "res:6", "res:31", "res:32" } },
            { "salvage", new[] { "book_", "vhs_", "med_", "tool_", "ammo_", "cloth_", "misc_", "throw_" } },
            { "build", new[] { "res:2", "res:3", "res:10", "res:11", "res:12", "res:13", "res:24", "res:26", "res:27" } },
        };

        public static bool Buys(string kind, string id)
        {
            if (!buys.TryGetValue(kind, out var l)) return false;
            if (id == "res:1" || id == "food_rotten") return false;       // scrap is the money
            foreach (var p in l) if (p.EndsWith("_") ? id.StartsWith(p) : id == p) return true;
            return false;
        }

        /// <summary>Today's goods (deterministic per vendor and day) minus what the player already bought today.</summary>
        public static List<Offer> Stock(NpcProfile p, NpcSave s, float bargain)
        {
            var list = new List<Offer>();
            if (p.kind == null || !sells.TryGetValue(p.kind, out var l)) return list;
            int day = MadMax.World.DayNight.Day;
            var r = new System.Random(p.seed * 31 + day * 977);
            foreach (var (id, min, max) in l)
            {
                int n = r.Next(min, max + 1) - s.Bought(id, day);
                if (n > 0) list.Add(new Offer { id = id, count = n, price = BuyPrice(id, bargain) });
            }
            return list;
        }

        /// <summary>Pay scrap and receive the goods (parts spawn next to the vendor). Returns false if unaffordable.</summary>
        public static bool Buy(WastelandGame g, Npc vendor, Offer o, int n)
        {
            n = Mathf.Min(n, o.count);
            if (n <= 0) return false;
            int cost = o.price * n;
            // the Fuel Guild's chits pay at fuel vendors, worth a little more than their scrap
            int chits = vendor.Profile.kind == "fuel" ? Mathf.Min(g.Inventory.GetItem(Contracts.Chit), cost / ChitValue) : 0;
            if (g.Inventory.Get(ResourceType.Scrap) < cost - chits * ChitValue) { g.Toast("NOT ENOUGH SCRAP (" + cost + ")"); NpcVoice.Say(vendor, "broke", true); return false; }
            if (o.id.StartsWith("part:"))
            {
                var at = vendor.transform.position + vendor.transform.right * 1.2f + Vector3.up * 0.6f;
                var part = g.SpawnPart(o.id.Substring(5), at, vendor.transform.rotation);
                if (!part) return false;
                part.gameObject.AddComponent<Rigidbody>().mass = part.mass;
                MadMax.Net.NetSession.Instance?.SendLooseSpawn(part);
            }
            else if (o.id.StartsWith("res:")) g.Inventory.Add((ResourceType)int.Parse(o.id.Substring(4)), n);
            else { g.Inventory.AddItem(o.id, n); if (o.id.StartsWith("tool_")) g.UpdateHotbarNow(); }
            if (chits > 0) g.Inventory.TakeItem(Contracts.Chit, chits);
            g.Inventory.TrySpend(ResourceType.Scrap, cost - chits * ChitValue);
            Market.Bought(Town, o.id, n);
            vendor.State.AddBought(o.id, n, MadMax.World.DayNight.Day);
            vendor.State.disposition = Mathf.Min(100, vendor.State.disposition + 1);
            NpcVoice.Say(vendor, "sell");
            g.Stats.Practice(MadMax.RPG.Skill.Speech, 0.5f * n);
            MadMax.Audio.Sfx.Play2D("cash", 0.6f);
            return true;
        }

        public static bool Sell(WastelandGame g, Npc vendor, string id, int n, float bargain)
        {
            int have = id.StartsWith("res:") ? g.Inventory.Get((ResourceType)int.Parse(id.Substring(4))) : g.Inventory.GetItem(id);
            n = Mathf.Min(n, have);
            int price = SellPrice(id, bargain);
            if (n <= 0 || price <= 0) return false;
            bool ok = id.StartsWith("res:") ? g.Inventory.TrySpend((ResourceType)int.Parse(id.Substring(4)), n) : g.Inventory.TakeItem(id, n);
            if (!ok) return false;
            if (id.StartsWith("tool_")) g.UpdateHotbarNow();
            g.Inventory.Add(ResourceType.Scrap, price * n);
            NpcVoice.Say(vendor, "buy");
            Market.Sold(Town, id, n);                                                          // selling floods the market
            g.Stats.Practice(MadMax.RPG.Skill.Speech, 0.3f * n);
            MadMax.Audio.Sfx.Play2D("cash", 0.5f);
            return true;
        }
    }
}
