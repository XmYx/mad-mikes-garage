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

        /// <summary>Unbought hauls carried to market: settlement index → goods, sold by the town's salvage vendors from
        /// <see cref="MarketHaulDay"/> on, under the usual price (<see cref="HaulDiscount"/>) until bought out. Saved.</summary>
        public static readonly Dictionary<int, Dictionary<string, int>> MarketHauls = new Dictionary<int, Dictionary<string, int>>();
        /// <summary>Settlement index → the day the haul reaches the stalls.</summary>
        public static readonly Dictionary<int, int> MarketHaulDay = new Dictionary<int, int>();
        public const string MarketHaulNote = "OFF A WRECK", StolenNote = "STOLEN FROM YOU";
        /// <summary>Settlement index → where the haul on its stalls was stripped (x, z): the stall names the place.</summary>
        public static readonly Dictionary<int, Vector2> MarketHaulFrom = new Dictionary<int, Vector2>();
        /// <summary>Parts taken off the player's vehicles (car breakers) still out there: "part:key" → count. Wherever
        /// they turn up on a stall the player buys them back at <see cref="BuyBackFactor"/>. Saved with the hauls.</summary>
        public static readonly Dictionary<string, int> Stolen = new Dictionary<string, int>();
        public const float BuyBackFactor = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHauls() { Hauls.Clear(); MarketHauls.Clear(); MarketHaulDay.Clear(); MarketHaulFrom.Clear(); Stolen.Clear(); }

        /// <summary>The stall line's note for goods off a wreck: where it was, from the town ("OFF A WRECK A SHORT
        /// DRIVE NORTH").</summary>
        public static string HaulNote(MadMax.World.Settlement town)
        {
            if (town == null || !MarketHaulFrom.TryGetValue(town.index, out var w)) return MarketHaulNote;
            var d = w - town.pos;
            return MarketHaulNote + " " + (d.magnitude < 150f ? "NEAR TOWN" : NpcLore.Distance(d.magnitude) + " " + NpcLore.Compass(d.x, d.y));
        }

        public static bool IsHaulNote(string note) => note != null && (note.StartsWith(MarketHaulNote) || note == StolenNote);

        /// <summary>A part taken off one of the player's vehicles goes to <paramref name="town"/>'s salvage stalls
        /// tomorrow, marked as theirs.</summary>
        public static void AddStolen(MadMax.World.Settlement town, string id, Vector3 from)
        {
            if (town == null || string.IsNullOrEmpty(id)) return;
            Stolen[id] = (Stolen.TryGetValue(id, out int had) ? had : 0) + 1;
            if (!MarketHauls.TryGetValue(town.index, out var m)) MarketHauls[town.index] = m = new Dictionary<string, int>();
            m[id] = (m.TryGetValue(id, out int n) ? n : 0) + 1;
            if (!MarketHaulDay.TryGetValue(town.index, out int day) || day <= MadMax.World.DayNight.Day) MarketHaulDay[town.index] = MadMax.World.DayNight.Day + 1;
            MarketHaulFrom[town.index] = new Vector2(from.x, from.z);
        }

        /// <summary>The scavenger leaves the wreck with what nobody bought: it reaches <paramref name="town"/>'s salvage
        /// stalls a day later. Returns the goods carried.</summary>
        public static int HaulToMarket(string scavenger, MadMax.World.Settlement town, Vector3? from = null)
        {
            if (!Hauls.TryGetValue(scavenger, out var h)) return 0;
            Hauls.Remove(scavenger);
            if (town == null) return 0;
            int n = 0;
            if (!MarketHauls.TryGetValue(town.index, out var m)) MarketHauls[town.index] = m = new Dictionary<string, int>();
            foreach (var kv in h) { if (kv.Value <= 0) continue; m[kv.Key] = (m.TryGetValue(kv.Key, out int had) ? had : 0) + kv.Value; n += kv.Value; }
            int arrive = MadMax.World.DayNight.Day + 1;
            if (!MarketHaulDay.TryGetValue(town.index, out int day) || day <= MadMax.World.DayNight.Day) MarketHaulDay[town.index] = arrive;   // a haul already on the stalls waits for the new one
            if (n == 0) { MarketHauls.Remove(town.index); MarketHaulDay.Remove(town.index); MarketHaulFrom.Remove(town.index); }
            else if (from.HasValue) MarketHaulFrom[town.index] = new Vector2(from.Value.x, from.Value.z);
            return n;
        }

        /// <summary>Goods off wrecks on the stalls of <paramref name="town"/> today (0 before they arrive).</summary>
        public static int MarketHaulCount(MadMax.World.Settlement town)
        {
            if (town == null || !MarketHauls.TryGetValue(town.index, out var m) || MarketHaulDay[town.index] > MadMax.World.DayNight.Day) return 0;
            int n = 0;
            foreach (var kv in m) n += kv.Value;
            return n;
        }

        static void MarketHaulStock(List<Offer> into, float bargain)
        {
            if (Town == null || MarketHaulCount(Town) == 0) return;
            string note = HaulNote(Town);
            foreach (var kv in MarketHauls[Town.index])
            {
                if (kv.Value <= 0) continue;
                bool theirs = Stolen.TryGetValue(kv.Key, out int st) && st > 0;                   // the player's own part: half to buy it back
                into.Add(new Offer { id = kv.Key, count = kv.Value, price = Mathf.Max(1, Mathf.RoundToInt(BuyPrice(kv.Key, bargain) * (theirs ? BuyBackFactor : HaulDiscount))), note = theirs ? StolenNote : note });
            }
        }

        static void TakeFromMarketHaul(string id, int n)
        {
            if (Town == null || !MarketHauls.TryGetValue(Town.index, out var m) || !m.TryGetValue(id, out int had)) return;
            if (had - n > 0) m[id] = had - n; else m.Remove(id);
            if (Stolen.TryGetValue(id, out int st)) { if (st - n > 0) Stolen[id] = st - n; else Stolen.Remove(id); }
            if (m.Count == 0) { MarketHauls.Remove(Town.index); MarketHaulDay.Remove(Town.index); MarketHaulFrom.Remove(Town.index); }
        }

        /// <summary>Save lines "@town|day|id=n|…" (market hauls), appended to the scavenger hauls.</summary>
        static void SaveMarketHauls(List<string> l)
        {
            foreach (var kv in MarketHauls)
            {
                var sb = new System.Text.StringBuilder("@").Append(kv.Key).Append('|').Append(MarketHaulDay.TryGetValue(kv.Key, out int d) ? d : 0);
                if (MarketHaulFrom.TryGetValue(kv.Key, out var w)) sb.Append("|~").Append(Mathf.RoundToInt(w.x)).Append(',').Append(Mathf.RoundToInt(w.y));
                foreach (var e in kv.Value) sb.Append('|').Append(e.Key).Append('=').Append(e.Value);
                l.Add(sb.ToString());
            }
            if (Stolen.Count > 0)
            {
                var sb = new System.Text.StringBuilder("!");
                foreach (var e in Stolen) sb.Append('|').Append(e.Key).Append('=').Append(e.Value);
                l.Add(sb.ToString());
            }
        }

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
            SaveMarketHauls(l);
            return l;
        }

        public static void LoadHauls(List<string> lines)
        {
            Hauls.Clear(); MarketHauls.Clear(); MarketHaulDay.Clear(); MarketHaulFrom.Clear(); Stolen.Clear();
            if (lines == null) return;
            foreach (var line in lines)
            {
                var f = line.Split('|');
                if (line.StartsWith("!"))                                                         // the player's parts still out there
                {
                    for (int i = 1; i < f.Length; i++)
                    {
                        int eq = f[i].LastIndexOf('=');
                        if (eq > 0 && int.TryParse(f[i].Substring(eq + 1), out int n) && n > 0) Stolen[f[i].Substring(0, eq)] = n;
                    }
                    continue;
                }
                if (line.StartsWith("@"))                                                         // a haul carried to market
                {
                    if (f.Length < 2 || !int.TryParse(f[0].Substring(1), out int town) || !int.TryParse(f[1], out int day)) continue;
                    var m = new Dictionary<string, int>();
                    for (int i = 2; i < f.Length; i++)
                    {
                        if (f[i].StartsWith("~"))
                        {
                            var xz = f[i].Substring(1).Split(',');
                            if (xz.Length == 2 && int.TryParse(xz[0], out int wx) && int.TryParse(xz[1], out int wz)) MarketHaulFrom[town] = new Vector2(wx, wz);
                            continue;
                        }
                        int eq = f[i].LastIndexOf('=');
                        if (eq > 0 && int.TryParse(f[i].Substring(eq + 1), out int n) && n > 0) m[f[i].Substring(0, eq)] = n;
                    }
                    if (m.Count > 0) { MarketHauls[town] = m; MarketHaulDay[town] = day; } else MarketHaulFrom.Remove(town);
                    continue;
                }
                for (int i = 1; i < f.Length; i++)
                {
                    int eq = f[i].LastIndexOf('=');
                    if (eq > 0 && int.TryParse(f[i].Substring(eq + 1), out int n)) AddHaul(f[0], f[i].Substring(0, eq), n);
                }
            }
        }
    }
}
