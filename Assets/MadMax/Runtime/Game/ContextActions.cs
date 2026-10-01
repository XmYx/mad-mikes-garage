using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>One entry of a context menu: a label and what it does — always the call the key, page or prompt makes
    /// for the same thing. <see cref="blocked"/> greys it out with the reason; <see cref="reach"/> walks up to the target
    /// first when it is out of arm's reach; <see cref="key"/> is the key that does the same (shown beside the label).</summary>
    public sealed class ContextOption
    {
        public string label;
        public System.Action run;
        public string blocked;
        public bool reach;
        public string key;
    }

    /// <summary>What a context menu is about: a thing in the world (<see cref="target"/>, clicked at <see cref="point"/>)
    /// or an entry of the pack (<see cref="item"/> = item id or "res:N"; <see cref="loot"/> = the loot window's open
    /// storage when there is one).</summary>
    public sealed class ContextTarget
    {
        public Component target;
        public Vector3 point;
        public string item;
        public LootSource loot;
        public string title;
        /// <summary>Nothing under the cursor: the ground at <see cref="point"/> (walk here, drop what you carry).</summary>
        public bool ground;
    }

    /// <summary>Registry of context menu providers: each kind of target (vehicles, parts, pieces with [E]/[T], world items,
    /// people, animals, pack entries...) adds its options to the list. Providers are data: a block adds its own with
    /// <see cref="Register"/> (same id replaces). The game's built-in providers are registered on first use.</summary>
    public static class ContextActions
    {
        public delegate void Provider(WastelandGame g, ContextTarget t, List<ContextOption> into);

        static readonly List<KeyValuePair<string, Provider>> providers = new List<KeyValuePair<string, Provider>>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => providers.Clear();

        public static bool Has(string id) { foreach (var kv in providers) if (kv.Key == id) return true; return false; }

        public static void Register(string id, Provider p)
        {
            providers.RemoveAll(kv => kv.Key == id);
            if (p != null) providers.Add(new KeyValuePair<string, Provider>(id, p));
        }

        /// <summary>Every option the providers offer for <paramref name="t"/>, first one per label.</summary>
        public static List<ContextOption> Gather(WastelandGame g, ContextTarget t)
        {
            g.EnsureContextProviders();
            var list = new List<ContextOption>();
            foreach (var kv in providers)
            {
                try { kv.Value(g, t, list); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            var seen = new HashSet<string>();
            list.RemoveAll(o => o == null || string.IsNullOrEmpty(o.label) || !seen.Add(o.label));
            return list;
        }
    }
}
