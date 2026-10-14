using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>"If you see it in the game, you can have it": what taking world things apart remembers. Designs learned
    /// (<see cref="Known"/>: <c>prop:&lt;template&gt;</c> pseudo pieces of world props, hidden pieces), world things gone
    /// for good that have no destruction state of their own (<see cref="Removed"/>: searched furniture spots, street
    /// lamps) and city fixtures taken out (<see cref="CityRemoved"/>). Small things come apart into a kit item that places
    /// the piece for free: <c>kit_prop:&lt;template&gt;</c> for props, <c>kit_piece:&lt;piece id&gt;</c> for pieces
    /// without a crafted kit of their own. Saved in <c>SaveData.blockSalvage</c>.</summary>
    public static class SalvageDesigns
    {
        public const string PropPrefix = "prop:", PropKit = "kit_prop:", PieceKit = "kit_piece:";
        /// <summary>Things up to this size (m) and weight (kg) come apart whole, as a kit you can carry.</summary>
        public const float KitMaxSize = 2.5f, KitMaxKg = 150f;

        public static readonly HashSet<string> Known = new HashSet<string>();
        public static readonly HashSet<string> Removed = new HashSet<string>();
        public static readonly HashSet<string> CityRemoved = new HashSet<string>();
        /// <summary>Pieces that go back into their kit when dismantled (small salvaged designs, small city pieces).</summary>
        public static readonly HashSet<string> Portable = new HashSet<string>();
        /// <summary>The player's pack: kits held show their designs in the build menu.</summary>
        public static Inventory Pack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Known.Clear(); Removed.Clear(); CityRemoved.Clear(); Pack = null; }

        public enum MatClass { Soft, Metal, Stone }

        /// <summary>What a thing is made of and how big it is: decides the tool, the skill, the time and the price.</summary>
        public struct Survey
        {
            public float size, kg;
            public int voxels;
            public MatClass cls;
            public bool plant;
            public (ResourceType type, int amount)[] cost;
            public bool Kit => size <= KitMaxSize && kg <= KitMaxKg;
        }

        public static MatClass ClassOf(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Wood: case ResourceType.Plank: case ResourceType.Cloth: case ResourceType.Hay: case ResourceType.Rubber:
                case ResourceType.Wool: case ResourceType.Hide: case ResourceType.Leather: case ResourceType.Thread:
                    return MatClass.Soft;
                case ResourceType.Scrap: case ResourceType.Iron: case ResourceType.Steel: case ResourceType.Copper: case ResourceType.Bronze:
                case ResourceType.Aluminium: case ResourceType.Lead: case ResourceType.Gold: case ResourceType.Glass:
                    return MatClass.Metal;
                default: return MatClass.Stone;
            }
        }

        static readonly int[] counts = new int[ResourceInfo.Count];

        /// <summary>Survey a grid of <paramref name="voxel"/>-sized voxels. The cost is what breaking every voxel of it
        /// would yield (the same table as carving), so building it never makes material out of nothing.</summary>
        public static Survey Of(VoxelGrid g, float voxel, bool plant = false)
        {
            var s = new Survey { plant = plant };
            if (g == null || g.Count == 0) { s.cost = new[] { (ResourceType.Scrap, 1) }; return s; }
            System.Array.Clear(counts, 0, counts.Length);
            Vector3Int lo = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), hi = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var kv in g.voxels)
            {
                lo = Vector3Int.Min(lo, kv.Key); hi = Vector3Int.Max(hi, kv.Key);
                if (kv.Value.mat < counts.Length) counts[kv.Value.mat]++;
            }
            var ext = (Vector3)(hi - lo + Vector3Int.one) * voxel;
            s.size = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
            s.voxels = g.Count;
            s.kg = g.Count * voxel * voxel * voxel * 600f;
            int best = -1;
            var cost = new List<(ResourceType, int)>();
            for (int t = 1; t < counts.Length; t++)
            {
                if (counts[t] == 0) continue;
                if (best < 0 || counts[t] > counts[best]) best = t;
                float y = counts[t] * ResourceInfo.YieldPerVoxel((ResourceType)t);
                if (y >= 0.5f) cost.Add(((ResourceType)t, Mathf.Max(1, Mathf.RoundToInt(y))));
            }
            cost.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            if (cost.Count > 5) cost.RemoveRange(5, cost.Count - 5);
            if (cost.Count == 0) cost.Add((best > 0 ? (ResourceType)best : ResourceType.Scrap, 1));
            s.cost = cost.ToArray();
            s.cls = best > 0 ? ClassOf((ResourceType)best) : MatClass.Metal;
            return s;
        }

        /// <summary>Tools that take it apart, best first.</summary>
        public static string[] Tools(MatClass c, bool plant) => plant ? new[] { "tool_axe", "tool_shovel", ItemIds.Machete }
            : c == MatClass.Soft ? new[] { ItemIds.ClawHammer, "tool_crowbar", "tool_axe" }
            : c == MatClass.Metal ? new[] { ItemIds.Wrench, ItemIds.Cutter, "tool_gas_torch" }
            : new[] { ItemIds.Sledgehammer, "tool_pickaxe" };

        /// <summary>Salvaging or Construction level a job of this size needs.</summary>
        public static int SkillNeeded(float size) => size <= KitMaxSize ? 0 : size <= 5f ? 1 : size <= 9f ? 3 : 5;

        /// <summary>Seconds of work at skill 0.</summary>
        public static float Seconds(float size, float kg) => Mathf.Clamp(2.5f + size * 0.9f + kg / 500f, 2.5f, 45f);

        // ------------------------------------------------------------------ kits

        public static bool IsKit(string item) => item != null && (item.StartsWith(PropKit) || item.StartsWith(PieceKit));

        /// <summary>The piece a salvage kit places (null: not a salvage kit).</summary>
        public static string PieceOfKit(string item) =>
            item == null ? null : item.StartsWith(PropKit) ? PropPrefix + item.Substring(PropKit.Length) : item.StartsWith(PieceKit) ? item.Substring(PieceKit.Length) : null;

        /// <summary>The kit a piece comes apart into: its crafted kit, else a salvage kit.</summary>
        public static string KitFor(FurnitureDef def) => def == null ? null : def.kit ?? (def.id.StartsWith(PropPrefix) ? PropKit + def.id.Substring(PropPrefix.Length) : PieceKit + def.id);

        /// <summary>A salvage kit for <paramref name="def"/> in the pack (it places the piece without materials).</summary>
        public static string HeldKit(FurnitureDef def, Inventory inv)
        {
            if (def == null || def.kit != null || inv == null) return null;
            var k = KitFor(def);
            return inv.GetItem(k) > 0 ? k : null;
        }

        public static string KitName(string item)
        {
            var def = FurnitureLibrary.Get(PieceOfKit(item));
            return (def != null ? def.name : "PARTS") + " (KIT)";
        }

        // ------------------------------------------------------------------ names

        static readonly (string prefix, string name)[] Names =
        {
            ("Tree", "DEAD TREE"), ("Pine", "PINE"), ("Palm", "PALM"), ("Bush", "BUSH"), ("Fern", "FERN"), ("Cactus", "CACTUS"), ("Log", "FALLEN LOG"),
            ("Barrel", "WASTE BARREL"), ("Farmhouse", "FARMHOUSE"), ("Fence", "FENCE RUN"), ("Haystack", "HAYSTACK"), ("BrickHouse", "BRICK HOUSE"),
            ("Tower", "RUINED TOWER"), ("Shop", "SHOP FRONT"), ("Streetlight", "CITY STREETLIGHT"), ("GasPump", "OLD FUEL PUMP"), ("Rock", "BOULDER"),
            ("Shack", "SCRAP SHACK"), ("Crate", "SUPPLY CRATE"), ("Table", "OLD TABLE"), ("Mast", "RADIO MAST"), ("GuardHut", "GUARD HUT"),
            ("Sandbags", "SANDBAG WALL"), ("CarStack", "CAR STACK"), ("Tank", "STORAGE TANK"), ("Column", "STONE COLUMN"),
        };

        public static bool IsPlant(string tid) => tid != null && (tid.StartsWith("Tree") || tid.StartsWith("Pine") || tid.StartsWith("Palm") || tid.StartsWith("Bush") || tid.StartsWith("Fern") || tid.StartsWith("Cactus"));

        /// <summary>The menu name of a world prop template ("FARMHOUSE 2", "IRON ORE OUTCROP", "FOOD STALL").</summary>
        public static string TemplateName(string tid)
        {
            if (string.IsNullOrEmpty(tid)) return "THING";
            if (tid.StartsWith("Stall_")) return tid.Substring(6).ToUpperInvariant() + " STALL";
            int end = tid.Length;
            while (end > 0 && char.IsDigit(tid[end - 1])) end--;
            int variant = end < tid.Length && int.TryParse(tid.Substring(end), out int v) ? v : 0;
            string stem = tid.Substring(0, end), name = null;
            if (stem.StartsWith("Ore_")) name = stem.Substring(4).Replace("Ore", " ORE").ToUpperInvariant().Trim() + " OUTCROP";
            else foreach (var (p, n) in Names) if (stem == p) { name = n; break; }
            name ??= stem.ToUpperInvariant();
            return variant > 0 ? name + " " + (variant + 1) : name;
        }

        // ------------------------------------------------------------------ save

        public static void Save(List<string> into)
        {
            into.Clear();
            foreach (var k in Known) into.Add("d:" + k);
            foreach (var k in Removed) into.Add("r:" + k);
            foreach (var k in CityRemoved) into.Add("c:" + k);
        }

        public static void Load(List<string> from)
        {
            Known.Clear(); Removed.Clear(); CityRemoved.Clear();
            if (from == null) return;
            foreach (var s in from)
            {
                if (s == null || s.Length < 3 || s[1] != ':') continue;
                var v = s.Substring(2);
                if (s[0] == 'd') Known.Add(v); else if (s[0] == 'r') Removed.Add(v); else if (s[0] == 'c') CityRemoved.Add(v);
            }
        }

        public static void NewGame() { Known.Clear(); Removed.Clear(); CityRemoved.Clear(); }
    }
}
