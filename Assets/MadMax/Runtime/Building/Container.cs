using System.Collections.Generic;
using System.Text;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Storage with its own inventory (chest, locker, shelf, crate, fridge, vehicle trunk). A cold fridge
    /// keeps food fresh (<see cref="ColdStore"/>: cold only while powered, warms up after an outage; freezers stop
    /// spoilage). Nearby containers feed crafting at stations.</summary>
    public class Container : MonoBehaviour, IPlaceState, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var door = GetComponent<Door>();
            if (door && door.locked && !g.OwnsPiece(GetComponent<Placeable>())) return title + " (LOCKED)";
            return "[E] OPEN " + title + (fridge ? (Cold ? " (" + Cold.Label + ")" : Cooling ? " (COLD)" : " (NO POWER)") : "");
        }
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) g.Menus.OpenContainer(this); }

        public static readonly List<Container> All = new List<Container>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public string title = "STORAGE";
        /// <summary>Where it is reached from (a vehicle trunk's lid, a door for the glovebox); null = the object itself.</summary>
        public Transform accessPoint;
        /// <summary>Carried by the player (a worn bag): part of the player's own storage, never "nearby" loot.</summary>
        public bool worn;
        /// <summary>The point to stand at / measure reach from.</summary>
        public Vector3 AccessAt => accessPoint ? accessPoint.position : transform.position;
        public float capacity = 80f;          // kg
        /// <summary>Most different goods it holds (bags, belts); 0 = no limit.</summary>
        public int slots;
        /// <summary>What it takes (item id or "res:N"); null = anything (a tool belt takes tools only).</summary>
        public System.Func<string, bool> accepts;
        public bool fridge;
        /// <summary>Passive keeping without power (root cellar): the spoil rate in here, halved again in winter.</summary>
        public float keep = 1f;
        public readonly Inventory inventory = new Inventory();
        UtilityNode node;

        void Awake() { node = GetComponent<UtilityNode>(); inventory.Changed += () => GetComponent<Placeable>()?.Dirty(); }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        ColdStore cold; bool coldLooked;
        /// <summary>The fridge's temperature model, if it has one.</summary>
        public ColdStore Cold { get { if (!coldLooked) { cold = GetComponent<ColdStore>(); coldLooked = true; } return cold; } }
        public bool Cooling => Cold ? Cold.Chilled : fridge && node && node.Powered;
        /// <summary>How fast food rots in here against the open pack (0 frozen .. 1).</summary>
        public float SpoilFactor => Cold ? Cold.SpoilFactor : Cooling ? 0.12f : keep < 1f && MadMax.World.Weather.Season == 2 ? keep * 0.5f : keep;
        public float Weight => ItemCatalog.TotalWeight(inventory);

        /// <summary>How many of <paramref name="n"/> × item id / "res:N" it takes: its filter, a free slot (or a stack
        /// already there) and the room left by weight.</summary>
        public int Fits(string key, int n)
        {
            if (key == null || n <= 0 || (accepts != null && !accepts(key))) return 0;
            int ri = 0;
            bool res = key.StartsWith("res:") && int.TryParse(key.Substring(4), out ri) && ri > 0 && ri < ResourceInfo.Count;
            var rt = res ? (ResourceType)ri : ResourceType.None;
            if (slots > 0 && (res ? inventory.Get(rt) : inventory.GetItem(key)) <= 0)
            {
                int used = 0;
                for (int t = 1; t < ResourceInfo.Count; t++) if (inventory.Get((ResourceType)t) > 0) used++;
                foreach (var kv in inventory.Items) if (kv.Value > 0) used++;
                if (used >= slots) return 0;
            }
            float w = res ? ItemCatalog.ResourceWeight(rt) : ItemCatalog.Weight(key);
            return w > 0f ? Mathf.Clamp(Mathf.FloorToInt((capacity - Weight) / w + 1e-4f), 0, n) : n;
        }

        void Update() { if (node && fridge && !Cold) node.demand = 150f; }

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

        /// <summary>Contents fall out when the container breaks (or a companion empties their pack): resources as pickups,
        /// items as objects on the ground ([E] picks them up); fluids are lost. The container is left empty (it used to
        /// keep its contents, so a dismissed companion still carried them and items in a broken chest were gone).</summary>
        public void Spill()
        {
            var at = transform.position + Vector3.up * 0.4f;
            for (int t = 1; t < ResourceInfo.Count; t++)
            {
                var rt = (ResourceType)t;
                int n = inventory.Get(rt);
                if (n <= 0) continue;
                if (PickupSystem.Instance && !ResourceInfo.IsFluid(rt)) PickupSystem.Instance.Spawn(rt, n, at, Random.insideUnitSphere * 1.5f + Vector3.up * 2f);
                inventory.TrySpend(rt, n);
            }
            var g = MadMax.Game.WastelandGame.Instance;
            foreach (var kv in new List<KeyValuePair<string, int>>(inventory.Items))
            {
                if (kv.Value <= 0) continue;
                if (g) { var off = Random.insideUnitCircle * 0.6f; g.SpawnWorldItem(kv.Key, kv.Value, -1f, at + new Vector3(off.x, 0f, off.y), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null); }
                inventory.TakeItem(kv.Key, kv.Value);
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
