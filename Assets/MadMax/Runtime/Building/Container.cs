using System.Collections.Generic;
using System.Text;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Storage with its own inventory (chest, locker, shelf, crate, fridge, vehicle trunk). A powered fridge
    /// keeps food fresh. Nearby containers feed crafting at stations.</summary>
    public class Container : MonoBehaviour, IPlaceState, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var door = GetComponent<Door>();
            if (door && door.locked && !g.OwnsPiece(GetComponent<Placeable>())) return title + " (LOCKED)";
            return "[E] OPEN " + title + (fridge ? (Cooling ? " (COLD)" : " (NO POWER)") : "");
        }
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) g.Menus.OpenContainer(this); }

        public static readonly List<Container> All = new List<Container>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public string title = "STORAGE";
        public float capacity = 80f;          // kg
        public bool fridge;
        public readonly Inventory inventory = new Inventory();
        UtilityNode node;

        void Awake() { node = GetComponent<UtilityNode>(); inventory.Changed += () => GetComponent<Placeable>()?.Dirty(); }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Cooling => fridge && node && node.Powered;
        public float Weight => ItemCatalog.TotalWeight(inventory);

        void Update() { if (node && fridge) node.demand = 150f; }

        public static Container Nearest(Vector3 p, float max)
        {
            Container best = null; float bd = max;
            foreach (var c in All) { if (!c) continue; float d = Vector3.Distance(c.transform.position, p); if (d < bd) { bd = d; best = c; } }
            return best;
        }

        public static void Near(Vector3 p, float max, List<Container> into)
        {
            into.Clear();
            foreach (var c in All) if (c && Vector3.Distance(c.transform.position, p) < max) into.Add(c);
        }

        /// <summary>Contents fall out as pickups when the container breaks.</summary>
        public void Spill()
        {
            if (!PickupSystem.Instance) return;
            for (int t = 1; t < ResourceInfo.Count; t++)
            {
                int n = inventory.Get((ResourceType)t);
                if (n > 0 && !ResourceInfo.IsFluid((ResourceType)t)) PickupSystem.Instance.Spawn((ResourceType)t, n, transform.position + Vector3.up * 0.4f, Random.insideUnitSphere * 1.5f + Vector3.up * 2f);
            }
        }

        public string SaveState() => InventoryCodec.Encode(inventory);
        public void LoadState(string s) => InventoryCodec.Decode(inventory, s);
    }

    /// <summary>Compact text form of an Inventory ("r3=10,r7=4|food_corn=2").</summary>
    public static class InventoryCodec
    {
        public static string Encode(Inventory inv)
        {
            var sb = new StringBuilder();
            for (int t = 1; t < ResourceInfo.Count; t++) { int n = inv.Get((ResourceType)t); if (n != 0) sb.Append('r').Append(t).Append('=').Append(n).Append(','); }
            sb.Append('|');
            foreach (var kv in inv.Items) if (kv.Value > 0) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(',');
            return sb.ToString();
        }

        public static void Decode(Inventory inv, string s)
        {
            var res = new int[ResourceInfo.Count];
            var items = new List<KeyValuePair<string, int>>();
            if (!string.IsNullOrEmpty(s))
            {
                var halves = s.Split('|');
                foreach (var e in halves[0].Split(',')) { var kv = e.Split('='); if (kv.Length == 2 && int.TryParse(kv[0].Substring(1), out int t) && t < res.Length) int.TryParse(kv[1], out res[t]); }
                if (halves.Length > 1) foreach (var e in halves[1].Split(',')) { var kv = e.Split('='); if (kv.Length == 2 && int.TryParse(kv[1], out int n)) items.Add(new KeyValuePair<string, int>(kv[0], n)); }
            }
            inv.Restore(res, items);
        }
    }
}
