using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Items
{
    /// <summary>Player-owned stock: raw resources plus named items (tools, loot, car parts — by id).</summary>
    public class Inventory
    {
        readonly int[] resources = new int[ResourceInfo.Count];
        readonly Dictionary<string, int> items = new Dictionary<string, int>();

        public event Action Changed;

        public int Get(ResourceType t) => resources[(int)t];

        public void Add(ResourceType t, int amount)
        {
            if (t == ResourceType.None || amount == 0) return;
            resources[(int)t] += amount;
            Changed?.Invoke();
        }

        public bool TrySpend(ResourceType t, int amount)
        {
            if (resources[(int)t] < amount) return false;
            resources[(int)t] -= amount;
            Changed?.Invoke();
            return true;
        }

        public int GetItem(string id) => items.TryGetValue(id, out var n) ? n : 0;

        public void AddItem(string id, int amount = 1)
        {
            items[id] = GetItem(id) + amount;
            Changed?.Invoke();
        }

        public bool TakeItem(string id, int amount = 1)
        {
            if (GetItem(id) < amount) return false;
            items[id] = GetItem(id) - amount;
            Changed?.Invoke();
            return true;
        }

        public IEnumerable<KeyValuePair<string, int>> Items => items;

        /// <summary>Raw counts for saving.</summary>
        public int[] ResourceArray => (int[])resources.Clone();

        public void Restore(int[] res, IEnumerable<KeyValuePair<string, int>> itemCounts)
        {
            System.Array.Clear(resources, 0, resources.Length);
            if (res != null) System.Array.Copy(res, resources, Mathf.Min(res.Length, resources.Length));
            items.Clear();
            foreach (var kv in itemCounts) items[kv.Key] = kv.Value;
            Changed?.Invoke();
        }
    }
}
