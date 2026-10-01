using System.Collections.Generic;
using System.Text;
using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>What a bag may hold.</summary>
    public enum BagFilter { Any, Tools, Arms }

    /// <summary>Storage stats of a wearable or luggable bag (a garment in <see cref="ClothingLibrary"/> with the same id).
    /// <see cref="capacity"/> kg in its own <see cref="Container"/> (and at most <see cref="slots"/> different goods, 0 =
    /// any number); its contents weigh <see cref="load"/> × their kg on the back (hip belts and frames &lt; 1, a bag in one
    /// hand &gt; 1); <see cref="side"/> = how one-sided the load is (one shoulder 1, one hand 1.2).</summary>
    public class BagSpec
    {
        public string id;
        public float capacity, load = 1f, weight, side, speed = 1f, wear, value = 10f;
        public int slots;
        public BagFilter filter;
        /// <summary>Carried in the hands (luggage): no tool in hand, [Q] sets it down.</summary>
        public bool hands;
        /// <summary>Tools on it are drawn straight from the hotbar (tool belt, gun belt).</summary>
        public bool quick;
    }

    /// <summary>Bags (roadmap "carrying"): data, item keys and the contents codec. A bag in the pack, a container or on the
    /// ground keeps its contents in its item key: <c>cloth_&lt;bag&gt;~&lt;contents&gt;</c> (the <see cref="InventoryCodec"/> text
    /// with '/', ';', '*' for '|', ',', '='), so it travels through every inventory, trade check, save line and network
    /// message like any other id. An empty bag is the plain <c>cloth_&lt;bag&gt;</c>. Worn, its contents live in a
    /// <see cref="Container"/> with <see cref="Container.worn"/> set (<see cref="WastelandGame.WornStorage"/>).</summary>
    public static class BagLibrary
    {
        public const char Sep = '~';
        /// <summary>Back brace: no storage, eases and heals the back (<see cref="WastelandGame.BackStrain"/>).</summary>
        public const string Brace = "back_brace";

        static readonly Dictionary<string, BagSpec> specs = new Dictionary<string, BagSpec>();
        static readonly Dictionary<string, float> filledWeight = new Dictionary<string, float>();

        static BagLibrary()
        {
            // backpacks (PACK): hip belts and frames carry the weight on the hips
            Add("schoolbag", 10f, 0.85f, 0.6f, value: 8f);
            Add("backpack", 20f, 0.8f, 1f, value: 18f);
            Add("hikingpack", 26f, 0.75f, 1.4f, value: 24f);
            Add("framepack", 40f, 0.6f, 2.5f, value: 40f);                                     // the tourist's aluminium frame
            Add("milpack", 50f, 0.62f, 3.2f, value: 60f);                                      // MOLLE, padded hip belt
            Add("craftpack", 15f, 0.9f, 1.2f, value: 6f, wear: 0.5f);                          // sticks, hide and twine: about 2 h full before it falls apart
            Add("leather_satchel", 16f, 0.85f, 1.2f, value: 16f);
            // belts (BELT): small, at the hips
            Add("work_belt", 6f, 0.7f, 0.8f, slots: 6, value: 10f);
            Add("gun_belt", 6f, 0.7f, 0.9f, slots: 4, filter: BagFilter.Arms, quick: true, value: 14f);
            Add("toolbelt", 12f, 0.7f, 1f, slots: 6, filter: BagFilter.Tools, quick: true, value: 16f);
            Add("fanny_bag", 3f, 0.7f, 0.3f, slots: 8, value: 6f);
            // one shoulder (SHOULDER)
            Add("shoulder_bag", 12f, 1f, 0.7f, side: 1f, value: 10f);
            Add("sling_bag", 7f, 0.9f, 0.5f, side: 0.6f, value: 10f);
            Add("canvas_satchel", 9f, 1f, 0.6f, side: 1f, value: 9f);
            // luggage (HAND): both hands busy, slower, [Q] drops it
            Add("duffel", 35f, 1.15f, 1.5f, side: 1.2f, speed: 0.85f, hands: true, value: 14f);
            Add("suitcase", 25f, 1.2f, 3f, side: 1.2f, speed: 0.85f, hands: true, value: 12f);
        }

        static void Add(string id, float capacity, float load, float weight, int slots = 0, float side = 0f, float speed = 1f, bool hands = false,
            BagFilter filter = BagFilter.Any, bool quick = false, float wear = 0f, float value = 10f)
        {
            specs[id] = new BagSpec { id = id, capacity = capacity, load = load, weight = weight, slots = slots, side = side, speed = speed, hands = hands, filter = filter, quick = quick, wear = wear, value = value };
        }

        public static IEnumerable<BagSpec> All => specs.Values;
        /// <summary>The bag behind a garment id, an item id or a filled key (null when it is not a bag).</summary>
        public static BagSpec Get(string id)
        {
            if (id == null) return null;
            id = DefId(id);
            return specs.TryGetValue(id, out var s) ? s : null;
        }
        public static bool IsBag(string id) => Get(id) != null;

        /// <summary>"cloth_milpack~r1*4;/..." → "milpack" (any garment id passes through).</summary>
        public static string DefId(string key)
        {
            if (key.StartsWith("cloth_")) key = key.Substring(6);
            int k = key.IndexOf(Sep);
            return k >= 0 ? key.Substring(0, k) : key;
        }

        static readonly Dictionary<string, string> plainIds = new Dictionary<string, string>();
        /// <summary>"cloth_" + a garment id, without allocating every frame.</summary>
        public static string Plain(string defId) { if (!plainIds.TryGetValue(defId, out var s)) plainIds[defId] = s = "cloth_" + defId; return s; }

        /// <summary>The plain (empty) item id of a bag key.</summary>
        public static string ItemId(string key) => "cloth_" + DefId(key);
        /// <summary>A bag item key carrying contents.</summary>
        public static bool IsFilled(string key) => key != null && key.StartsWith("cloth_") && key.IndexOf(Sep) > 0;

        /// <summary>The item key for a bag holding <paramref name="inv"/> (plain when empty).</summary>
        public static string Key(string defId, Inventory inv)
        {
            var plain = "cloth_" + DefId(defId);
            if (inv == null || Empty(inv)) return plain;
            var s = InventoryCodec.Encode(inv);
            var sb = new StringBuilder(plain.Length + s.Length + 1).Append(plain).Append(Sep);
            foreach (char ch in s) sb.Append(ch == '|' ? '/' : ch == ',' ? ';' : ch == '=' ? '*' : ch);
            return sb.ToString();
        }

        /// <summary>Fill <paramref name="into"/> with what a bag key holds (cleared first).</summary>
        public static void Contents(string key, Inventory into)
        {
            int k = key != null ? key.IndexOf(Sep) : -1;
            if (k < 0) { InventoryCodec.Decode(into, null); return; }
            var sb = new StringBuilder(key.Length - k);
            for (int i = k + 1; i < key.Length; i++) { char ch = key[i]; sb.Append(ch == '/' ? '|' : ch == ';' ? ',' : ch == '*' ? '=' : ch); }
            InventoryCodec.Decode(into, sb.ToString());
        }

        public static bool Empty(Inventory inv)
        {
            for (int t = 1; t < ResourceInfo.Count; t++) if (inv.Get((ResourceType)t) != 0) return false;
            foreach (var kv in inv.Items) if (kv.Value > 0) return false;
            return true;
        }

        static readonly Inventory scratch = new Inventory();

        /// <summary>Weight of a filled bag key: the bag and everything in it (cached per key).</summary>
        public static float FilledWeight(string key)
        {
            if (filledWeight.TryGetValue(key, out var w)) return w;
            Contents(key, scratch);
            var spec = Get(key);
            w = (spec != null ? spec.weight : 1f) + ItemCatalog.TotalWeight(scratch);
            if (filledWeight.Count > 512) filledWeight.Clear();
            filledWeight[key] = w;
            return w;
        }

        /// <summary>"MILITARY BACKPACK (12.5 KG INSIDE)" for a filled key.</summary>
        public static string FilledName(string key, string baseName)
        {
            var spec = Get(key);
            float w = FilledWeight(key) - (spec != null ? spec.weight : 0f);
            return baseName + " (" + w.ToString("0.#") + " KG INSIDE)";
        }

        /// <summary>Trade value of an empty bag (0 = not a bag).</summary>
        public static float Value(string id) { var s = Get(id); return s != null ? s.value : 0f; }

        /// <summary>Whether a bag may take this item id / "res:N": never a bag (empty or full), only tools on a tool belt,
        /// only weapons and ammunition on a gun belt.</summary>
        public static bool Accepts(BagSpec spec, string key)
        {
            if (spec == null || key == null) return false;
            if (key.StartsWith("cloth_") && IsBag(key)) return false;
            if (spec.filter == BagFilter.Any) return true;
            if (key.StartsWith("res:")) return false;
            var cat = ItemCatalog.Category(key);
            if (spec.filter == BagFilter.Tools) return cat == ItemCategory.Tool || cat == ItemCategory.Weapon && !key.StartsWith("tool_bolt") && key != "tool_crossbow";
            return cat == ItemCategory.Weapon || cat == ItemCategory.Ammo || cat == ItemCategory.Throwable;
        }

        /// <summary>A worn or ground bag's storage, set up from its spec.</summary>
        public static void Setup(Container c, BagSpec spec, string title)
        {
            c.title = title;
            c.capacity = spec.capacity;
            c.slots = spec.slots;
            c.accepts = key => Accepts(spec, key);
        }

        // ------------------------------------------------------------------ sources
        /// <summary>Loot table additions: everyday bags in houses and shops, the military pack in bunkers and on raiders,
        /// tool belts in garages and tool sheds, luggage at airfields (<c>LootTables</c> static constructor).</summary>
        public static void AddLoot(Dictionary<string, (string id, int min, int max, float w)[]> tables)
        {
            Append(tables, "house", ("cloth_backpack", 1, 1, 0.25f), ("cloth_shoulder_bag", 1, 1, 0.3f), ("cloth_fanny_bag", 1, 1, 0.3f), ("cloth_suitcase", 1, 1, 0.2f), ("cloth_" + Brace, 1, 1, 0.1f));
            Append(tables, "shop", ("cloth_backpack", 1, 1, 0.3f), ("cloth_sling_bag", 1, 1, 0.3f), ("cloth_fanny_bag", 1, 1, 0.3f));
            Append(tables, "bunker", ("cloth_milpack", 1, 1, 0.5f), ("cloth_gun_belt", 1, 1, 0.3f));
            Append(tables, "raider", ("cloth_milpack", 1, 1, 0.12f), ("cloth_sling_bag", 1, 1, 0.2f));
            Append(tables, "garage", ("cloth_toolbelt", 1, 1, 0.4f));
            Append(tables, "tools", ("cloth_toolbelt", 1, 1, 0.5f), ("cloth_" + Brace, 1, 1, 0.2f));
            Append(tables, "airfield", ("cloth_duffel", 1, 1, 0.4f), ("cloth_suitcase", 1, 1, 0.4f));
            Append(tables, "office", ("cloth_canvas_satchel", 1, 1, 0.4f));
        }

        static void Append(Dictionary<string, (string, int, int, float)[]> tables, string table, params (string, int, int, float)[] add)
        {
            if (!tables.TryGetValue(table, out var have)) return;
            var l = new List<(string, int, int, float)>(have);
            foreach (var e in add) if (!l.Exists(x => x.Item1 == e.Item1)) l.Add(e);
            tables[table] = l.ToArray();
        }

        /// <summary>Vendor stock additions (<c>Trade</c>): everyday bags from pack traders, military and tool gear from
        /// salvage traders.</summary>
        public static readonly (string kind, string id, int min, int max)[] Stock =
        {
            ("pack", "cloth_backpack", 0, 1), ("pack", "cloth_shoulder_bag", 0, 1), ("pack", "cloth_fanny_bag", 0, 1), ("pack", "cloth_duffel", 0, 1),
            ("pack", "cloth_" + Brace, 0, 1),
            ("salvage", "cloth_milpack", 0, 1), ("salvage", "cloth_toolbelt", 0, 1), ("salvage", "cloth_sling_bag", 0, 1), ("parts", "cloth_toolbelt", 0, 1),
        };
    }
}
