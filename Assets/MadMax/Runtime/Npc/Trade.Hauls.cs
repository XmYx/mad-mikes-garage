using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>What a wreck scavenger stripped (<c>WastelandGame.Scavenge</c>) is their stock: parts and goods off the
    /// news wreck, sold at a scavenger's price until bought out. Saved with the road wrecks.</summary>
    public static partial class Trade
    {
        /// <summary>Scavenger NPC id (<c>WastelandGame.ScavengerId</c>) → goods (<c>part:key</c>, item ids, <c>res:N</c>) and counts.</summary>
        public static readonly Dictionary<string, Dictionary<string, int>> Hauls = new Dictionary<string, Dictionary<string, int>>();
        public const string ScavengerKind = "scavenger";
        /// <summary>Scavengers want rid of their haul before someone comes asking: under the usual markup.</summary>
        public const float HaulDiscount = 0.75f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHauls() => Hauls.Clear();

        public static void AddHaul(string scavenger, string id, int n)
        {
            if (n <= 0 || string.IsNullOrEmpty(id)) return;
            if (!Hauls.TryGetValue(scavenger, out var h)) Hauls[scavenger] = h = new Dictionary<string, int>();
            h[id] = (h.TryGetValue(id, out int had) ? had : 0) + n;
        }

        public static int HaulCount(string scavenger)
        {
            int n = 0;
            if (Hauls.TryGetValue(scavenger, out var h)) foreach (var kv in h) n += kv.Value;
            return n;
        }

        static void HaulStock(NpcProfile p, List<Offer> into, float bargain)
        {
            if (!Hauls.TryGetValue(p.id, out var h)) return;
            foreach (var kv in h)
                if (kv.Value > 0) into.Add(new Offer { id = kv.Key, count = kv.Value, price = Mathf.Max(1, Mathf.RoundToInt(BuyPrice(kv.Key, bargain) * HaulDiscount)) });
        }

        /// <summary>A haul line bought: it is gone for good (not restocked tomorrow).</summary>
        static void TakeFromHaul(NpcProfile p, string id, int n)
        {
            if (!Hauls.TryGetValue(p.id, out var h) || !h.TryGetValue(id, out int had)) return;
            if (had - n > 0) h[id] = had - n; else h.Remove(id);
        }

        /// <summary>Save lines "scav-id|id=n|id=n".</summary>
        public static List<string> SaveHauls()
        {
            var l = new List<string>();
            foreach (var kv in Hauls)
            {
                var sb = new System.Text.StringBuilder(kv.Key);
                foreach (var e in kv.Value) sb.Append('|').Append(e.Key).Append('=').Append(e.Value);
                l.Add(sb.ToString());
            }
            return l;
        }

        public static void LoadHauls(List<string> lines)
        {
            Hauls.Clear();
            if (lines == null) return;
            foreach (var line in lines)
            {
                var f = line.Split('|');
                for (int i = 1; i < f.Length; i++)
                {
                    int eq = f[i].LastIndexOf('=');
                    if (eq > 0 && int.TryParse(f[i].Substring(eq + 1), out int n)) AddHaul(f[0], f[i].Substring(0, eq), n);
                }
            }
        }
    }
}
