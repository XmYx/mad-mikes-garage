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
        /// <summary>Every gain or loss with its signed amount: an item id (resource None) or a resource (id null). The
        /// player's pack feeds the HUD item feed from it; <see cref="Label"/> says why it happened.</summary>
        public event Action<string, ResourceType, int> Delta;

        static string gainLabel, lossLabel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { gainLabel = lossLabel = null; }

        /// <summary>Why the change in progress happens ("BOUGHT", "MADE"...; null = not said), for a gain or a loss.</summary>
        public static string Label(int amount) => amount >= 0 ? gainLabel : lossLabel;

        /// <summary>Label the changes made inside a using block: <c>using (Inventory.Source("BOUGHT", "PAID")) { ... }</c>
        /// (loss label defaults to the gain label; nested blocks restore the outer label).</summary>
        public static SourceScope Source(string gain, string loss = null) => new SourceScope(gain, loss ?? gain);

        public readonly struct SourceScope : IDisposable
        {
            readonly string outerGain, outerLoss;
            internal SourceScope(string gain, string loss) { outerGain = gainLabel; outerLoss = lossLabel; gainLabel = gain; lossLabel = loss; }
            public void Dispose() { gainLabel = outerGain; lossLabel = outerLoss; }
        }

        public int Get(ResourceType t) => resources[(int)t];

        public void Add(ResourceType t, int amount)
        {
            if (t == ResourceType.None || amount == 0) return;
            resources[(int)t] += amount;
            Changed?.Invoke();
            Delta?.Invoke(null, t, amount);
        }

        public bool TrySpend(ResourceType t, int amount)
        {
            if (resources[(int)t] < amount) return false;
            resources[(int)t] -= amount;
            Changed?.Invoke();
            if (amount != 0) Delta?.Invoke(null, t, -amount);
            return true;
        }

        public int GetItem(string id) => items.TryGetValue(id, out var n) ? n : 0;

        public void AddItem(string id, int amount = 1)
        {
            items[id] = GetItem(id) + amount;
            Changed?.Invoke();
            if (amount != 0) Delta?.Invoke(id, ResourceType.None, amount);
        }

        public bool TakeItem(string id, int amount = 1)
        {
            if (GetItem(id) < amount) return false;
            items[id] = GetItem(id) - amount;
            Changed?.Invoke();
            if (amount != 0) Delta?.Invoke(id, ResourceType.None, -amount);
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
