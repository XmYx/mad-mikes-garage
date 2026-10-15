using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-15: the digger's word (a caught digger sours the stalls of their town or, let go
    /// with their half, tips the player off), the neighbour's errand (the visit opens a thread), the crew medic restocking
    /// at a town stall out of the crew's scrap (once trusted), and a part a rescued survivor has held for the player.
    /// The chase on the radio lives in <c>Npc/Convoy.Call.cs</c>, the held part and sour prices in <c>Npc/Trade.Held.cs</c>.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ the digger's word

        /// <summary>Stalls of a settlement charge <see cref="SourMarkup"/> more from the day after a caught digger gets
        /// home until <c>until</c> (settlement index → (from, until)).</summary>
        public readonly Dictionary<int, (int from, int until)> SourStalls = new Dictionary<int, (int, int)>();
        public const float SourMarkup = 1.1f;
        public const int SourDays = 3;

        /// <summary>A digger let go with their half who will find the player in their town (from day <c>day</c>).</summary>
        public class DiggerTip { public string name; public int town, day; }
        public readonly List<DiggerTip> DiggerTips = new List<DiggerTip>();
        /// <summary>The last digger's word: "sour:town" / "tip:town" when caught, the tip line when given (tests).</summary>
        public static string LastDiggerWord;
        float diggerWordNext;

        public float StallSour(int town) => SourStalls.TryGetValue(town, out var s) && DayNight.Day >= s.from && DayNight.Day <= s.until ? SourMarkup : 1f;

        /// <summary>From <see cref="CatchDigger"/>: how the digger takes it. With Charisma they feel let off lightly
        /// (and owe one); otherwise they tell their town's stalls about the player. True = grateful.</summary>
        public bool DiggerWord(MadMax.Npc.Npc n, Settlement town)
        {
            if (!n || town == null) return false;
            var rnd = new System.Random(Market.Seed(seed, n.Profile.id.GetHashCode() * 13 + 5));
            float grateful = Mathf.Clamp(0.35f + 0.07f * (Stats.Attribute(MadMax.RPG.Attr.Charisma) - 5), 0.1f, 0.8f);
            if (rnd.NextDouble() < grateful)
            {
                DiggerTips.RemoveAll(t => t.town == town.index);
                DiggerTips.Add(new DiggerTip { name = n.Profile.Name, town = town.index, day = DayNight.Day + 1 });
                LastDiggerWord = "tip:" + town.index;
                return true;
            }
            SourStalls[town.index] = (DayNight.Day + 1, DayNight.Day + SourDays);
            MadMax.Npc.TownNews.Post(n.Profile.Name + " TELLS THE STALLS OF " + Market.TownName(town) + " ABOUT A ROAD THIEF WHO ROBS HONEST DIGGERS",
                                     new Vector3(town.pos.x, 0f, town.pos.y));
            Journal.Add("TRADE", "THE DIGGER YOU CAUGHT WILL TALK IN " + Market.TownName(town) + ": ITS STALLS CHARGE YOU MORE FOR A FEW DAYS");
            LastDiggerWord = "sour:" + town.index;
            return false;
        }

        /// <summary>A grateful digger finds the player in their town and tells of the next stash.</summary>
        void UpdateDiggerTips()
        {
            if (DiggerTips.Count == 0 || !Player || World == null || Time.time < diggerWordNext) return;
            diggerWordNext = Time.time + 3f;
            if (DayNight.Hours < 7f || DayNight.Hours > 21f) return;
            var me = Player.transform.position;
            for (int i = DiggerTips.Count - 1; i >= 0; i--)
            {
                var t = DiggerTips[i];
                if (DayNight.Day < t.day || t.town < 0 || t.town >= World.settlements.Count) continue;
                var town = World.settlements[t.town];
                if (Flat(new Vector3(town.pos.x, 0f, town.pos.y) - me) > town.radius + 30f) continue;
                GiveDiggerTip(t);
            }
        }

        public string GiveDiggerTip(DiggerTip t)
        {
            DiggerTips.Remove(t);
            if (World == null || t.town < 0 || t.town >= World.settlements.Count) return null;
            var town = World.settlements[t.town];
            var tp = new Vector3(town.pos.x, 0f, town.pos.y);
            var c = CacheNear(town, DayNight.Day / 7 + 1);
            string tip;
            if (c != null && !Caches.Exists(k => k.key == c.key))
            {
                Caches.Add(c);
                tip = "ANOTHER ONE'S BURIED " + NpcLore.Compass(c.at.x - tp.x, c.at.z - tp.z) + " OF TOWN. I WON'T BE DIGGING IT";
            }
            else tip = "WATCH THE ROADS OUT OF " + Market.TownName(town) + " AFTER DARK. THAT'S ALL I'VE GOT";
            LastDiggerWord = (t.name ?? "THE DIGGER") + ", THE DIGGER YOU LET GO WITH HALF: \"FAIR'S FAIR. " + tip + ".\"";
            Toast(LastDiggerWord);
            Journal.Add("FOUND", LastDiggerWord);
            return LastDiggerWord;
        }

        // ------------------------------------------------------------------ the neighbour's errand

        /// <summary>A favour a neighbour who looked in asks later: kind 0 water for the garden, 1 leathers for the pump,
        /// 2 seed potatoes. State 0 = due to ask, 1 = taken on, 2 = done.</summary>
        public class NeighbourErrand { public int kind, state, due, town = -1; public string name; }
        public readonly Dictionary<string, NeighbourErrand> NeighbourErrands = new Dictionary<string, NeighbourErrand>();
        public const int ErrandAfterDays = 2;
        public static readonly (string ask, string item, int n, string thanks)[] NeighbourAsks =
        {
            ("MY BEANS ARE WILTING IN THIS HEAT. FOUR BOTTLES OF WATER FOR THE GARDEN? I'D CARRY THEM MYSELF IF I COULD.", "drink_water", 4, "THE BEANS THANK YOU. HAVE SOME TOMATO SEED FROM THE PLOT YOU JUST SAVED."),
            ("THE PUMP'S LEATHERS ARE SHOT - IT SUCKS AIR. TWO PIECES OF LEATHER AND IT DRAWS AGAIN.", "res:" + (int)ResourceType.Leather, 2, "LISTEN TO HER DRAW! HERE, SCRAP FOR YOUR TROUBLE."),
            ("MICE HAD MY SEED POTATOES. THREE, IF YOU CAN SPARE THEM? NO POTATOES, NO WINTER.", "seed_potato", 3, "BLESS YOU. TAKE THESE CANS - I WON'T NEED THEM NOW THERE'S A CROP COMING."),
        };
        /// <summary>The last favour asked or done (tests).</summary>
        public static string LastErrand;
        float errandNext;

        /// <summary>From <see cref="NeighbourCame"/>: the visit opens a thread — a small favour asked a couple of days on.</summary>
        void PlanNeighbourErrand(NpcProfile p, Settlement town)
        {
            if (p == null || (NeighbourErrands.TryGetValue(p.id, out var had) && had.state < 2)) return;
            NeighbourErrands[p.id] = new NeighbourErrand
            {
                kind = (p.id.GetHashCode() & 0x7fffffff) % NeighbourAsks.Length, state = 0, due = DayNight.Day + ErrandAfterDays,
                town = town != null ? town.index : -1, name = p.Name
            };
        }

        public NeighbourErrand ErrandOf(NpcProfile p) => p != null && NeighbourErrands.TryGetValue(p.id, out var e) ? e : null;

        /// <summary>The favour can be asked (talk or wave-over): not yet taken on and the day has come.</summary>
        public static bool ErrandDue(NeighbourErrand e) => e != null && e.state == 0 && (e.due < 0 || DayNight.Day >= e.due);

        /// <summary>Due favours wave the player over when they come into the neighbour's town.</summary>
        void UpdateNeighbourErrands()
        {
            if (NeighbourErrands.Count == 0 || !Player || World == null || Time.time < errandNext) return;
            errandNext = Time.time + 4f;
            var me = Player.transform.position;
            foreach (var e in NeighbourErrands.Values)
            {
                if (e.state != 0 || e.due < 0 || DayNight.Day < e.due || e.town < 0 || e.town >= World.settlements.Count) continue;
                var town = World.settlements[e.town];
                if (Flat(new Vector3(town.pos.x, 0f, town.pos.y) - me) > town.radius + 20f) continue;
                e.due = -1;                                                                       // said once; the ask waits in their talk
                LastErrand = (e.name ?? "A NEIGHBOUR") + " WAVES YOU OVER: THEY WANT A WORD";
                Toast(LastErrand);
            }
        }

        /// <summary>Accept the favour (dialogue).</summary>
        public void TakeErrand(NpcProfile p)
        {
            var e = ErrandOf(p);
            if (e == null) return;
            e.state = 1;
            LastErrand = p.Name + ": " + NeighbourAsks[e.kind].ask;
            Journal.Add("ERRAND", LastErrand);
        }

        /// <summary>Hand the favour over (dialogue): null when the player lacks the goods, else the thanks.</summary>
        public string DoErrand(NpcProfile p)
        {
            var e = ErrandOf(p);
            if (e == null || e.state != 1) return null;
            var a = NeighbourAsks[e.kind];
            bool res = a.item.StartsWith("res:");
            var rt = res ? (ResourceType)int.Parse(a.item.Substring(4)) : ResourceType.None;
            int have = res ? Inventory.Get(rt) : Inventory.GetItem(a.item);
            if (have < a.n) return null;
            using (Inventory.Source("FAVOUR", "GAVE"))
            {
                if (res) Inventory.TrySpend(rt, a.n); else Inventory.TakeItem(a.item, a.n);
                if (e.kind == 0) Inventory.AddItem("seed_tomato", 2);
                else if (e.kind == 1) Inventory.Add(ResourceType.Scrap, 18);
                else Inventory.AddItem("food_can", 2);
            }
            e.state = 2;
            var s = NpcRegistry.Get(p);
            s.disposition = Mathf.Min(100, s.disposition + 12);
            s.Set(NpcSave.Helped);
            MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.Settlers, 2);
            MadMax.Audio.Sfx.Play2D("cash", 0.5f);
            LastErrand = a.thanks;
            Journal.Add("ERRAND", "DID " + p.Name + " A FAVOUR");
            return a.thanks;
        }

        // ------------------------------------------------------------------ the medic restocks in town

        /// <summary>Crew medics trusted to buy their own dressings at a town stall out of the crew's scrap (saved).</summary>
        public readonly HashSet<string> MedicMayShop = new HashSet<string>();
        public const int MedicRestockBandages = 3, MedicShopTrust = 25;
        readonly Dictionary<string, int> restockDay = new Dictionary<string, int>(), askedShopDay = new Dictionary<string, int>();
        /// <summary>The last restock line (tests).</summary>
        public static string LastRestock;
        float restockNext;

        /// <summary>What <see cref="MedicRestockBandages"/> bandages cost the crew at <paramref name="town"/>'s stalls.</summary>
        public int RestockCost(Settlement town)
        {
            var was = MadMax.Npc.Trade.Town;
            MadMax.Npc.Trade.Town = town;
            int each = MadMax.Npc.Trade.BuyPrice("med_bandage", 0.15f);
            MadMax.Npc.Trade.Town = was;
            return each * MedicRestockBandages;
        }

        /// <summary>Every few seconds: a crew medic with an empty kit standing in a friendly town asks once a day to buy
        /// dressings out of the crew's scrap; once trusted (<see cref="MedicMayShop"/>) they just do it, once a day.</summary>
        public void UpdateMedicRestock(bool force = false)
        {
            if (!force && Time.time < restockNext) return;
            restockNext = Time.time + 6f;
            if (World == null) return;
            var radio = Companions.OnRadio();
            foreach (var n in Companions.Live)
            {
                if (!n || !n.Alive || !Companions.Patches(n.Profile) || MedicKit(n) > 0) continue;
                var p = n.transform.position;
                var town = World.SettlementAt(p.x, p.z);
                if (town == null || MadMax.Npc.Factions.Hostile(MadMax.Npc.Factions.OfSettlement(town))) continue;
                string id = n.Profile.id;
                bool far = Player && Flat(p - Player.transform.position) > Companions.RadioRange;
                bool onRadio = radio.Contains(n);
                if (!MedicMayShop.Contains(id))
                {
                    if (!force && askedShopDay.TryGetValue(id, out int ad) && ad == DayNight.Day) continue;
                    askedShopDay[id] = DayNight.Day;
                    CrewSay(n, "THERE'S A STALL HERE. WANT ME TO BUY BANDAGES OUT OF THE CREW'S SCRAP? SAY THE WORD AND I'LL SEE TO IT MYSELF.", far, onRadio);
                    continue;
                }
                if (!force && restockDay.TryGetValue(id, out int rd) && rd == DayNight.Day) continue;
                restockDay[id] = DayNight.Day;
                int cost = RestockCost(town);
                if (!Inventory.TrySpend(ResourceType.Scrap, cost))
                {
                    CrewSay(n, "WANTED BANDAGES IN " + Market.TownName(town) + " BUT THE CREW PURSE IS SHORT (" + cost + " SCRAP).", far, onRadio);
                    continue;
                }
                n.EnsurePack();
                n.pack.inventory.AddItem("med_bandage", MedicRestockBandages);
                Market.Bought(town, "med_bandage", MedicRestockBandages);
                CrewSay(n, "BOUGHT " + MedicRestockBandages + " BANDAGES IN " + Market.TownName(town) + " FOR " + cost + " SCRAP. KIT'S STOCKED.", far, onRadio);
            }
        }

        /// <summary>Companion dialogue: let a trusted medic shop for their own dressings (or stop them).</summary>
        public void LetMedicShop(NpcProfile p, bool on) { if (p == null) return; if (on) MedicMayShop.Add(p.id); else MedicMayShop.Remove(p.id); }

        void CrewSay(MadMax.Npc.Npc n, string text, bool far, bool radio)
        {
            LastRestock = n.KnownName + ": \"" + text + "\"";
            if (far && radio) SendHomeCall("HOME FREQUENCY: " + LastRestock, n.transform.position);
            else if (!far) { Toast(LastRestock); Journal.Add("CREW", LastRestock); }
        }

        // ------------------------------------------------------------------ a tip's cheap part

        static readonly string[] HeldParts = { "wheel_offroad", "radiator_car", "exhaust_side_pipes", "bumper_bull_bar", "wheel_street" };

        /// <summary>From <see cref="HitcherTip"/>: a stall in the survivor's town holds a part for the player at half
        /// price until their week is out. Returns the tip text.</summary>
        string HoldPartTip(string id, Settlement town, NpcSave s)
        {
            if (town == null) return null;
            var key = HeldParts[(id.GetHashCode() & 0x7fffffff) % HeldParts.Length];
            int until = (s != null && s.rescuedDay >= 0 ? s.rescuedDay : DayNight.Day) + RescueDays;
            MadMax.Npc.Trade.Held[town.index] = ("part:" + key, until);
            Journal.Add("TRADE", "A " + key.Replace('_', ' ').ToUpperInvariant() + " IS HELD FOR YOU AT " + Market.TownName(town) + "'S PARTS STALL, HALF PRICE UNTIL DAY " + until);
            return "I TALKED THE PARTS STALL IN " + Market.TownName(town) + " INTO HOLDING A " + key.Replace('_', ' ').ToUpperInvariant() + " FOR YOU. HALF PRICE, TILL THE WEEK'S OUT";
        }

        // ------------------------------------------------------------------ tick, LOOK FOR, save

        void UpdateNightly1015()
        {
            UpdateDiggerTips();
            UpdateNeighbourErrands();
            UpdateMedicRestock();
        }

        void LookFor1015(List<Pin> into, Color32 col)
        {
            if (World == null) return;
            foreach (var e in NeighbourErrands.Values)
                if ((e.state == 1 || ErrandDue(e)) && e.town >= 0 && e.town < World.settlements.Count)
                {
                    var t = World.settlements[e.town];
                    into.Add(new Pin { label = (e.name ?? "A NEIGHBOUR") + "'S FAVOUR", pos = new Vector3(t.pos.x, 0f, t.pos.y), color = col });
                }
            foreach (var kv in MadMax.Npc.Trade.Held)
                if (kv.Key >= 0 && kv.Key < World.settlements.Count && DayNight.Day <= kv.Value.until)
                {
                    var t = World.settlements[kv.Key];
                    into.Add(new Pin { label = "PART HELD AT " + Market.TownName(t), pos = new Vector3(t.pos.x, 0f, t.pos.y), color = col });
                }
        }

        void Save1015(List<string> lines)
        {
            foreach (var kv in SourStalls) lines.Add("d|" + kv.Key + "|" + kv.Value.from + "|" + kv.Value.until);
            foreach (var t in DiggerTips) lines.Add("t|" + t.town + "|" + t.day + "|" + (t.name ?? "").Replace("|", " "));
            foreach (var kv in NeighbourErrands) lines.Add("e|" + kv.Key + "|" + kv.Value.kind + "|" + kv.Value.state + "|" + kv.Value.due + "|" + kv.Value.town + "|" + (kv.Value.name ?? "").Replace("|", " "));
            foreach (var id in MedicMayShop) lines.Add("s|" + id);
            foreach (var kv in MadMax.Npc.Trade.Held) lines.Add("p|" + kv.Key + "|" + kv.Value.id + "|" + kv.Value.until);
        }

        void Load1015(string[] f)
        {
            if (f[0] == "d" && f.Length >= 4 && int.TryParse(f[1], out int t) && int.TryParse(f[2], out int a) && int.TryParse(f[3], out int b)) SourStalls[t] = (a, b);
            else if (f[0] == "t" && f.Length >= 4 && int.TryParse(f[1], out int tt) && int.TryParse(f[2], out int day)) DiggerTips.Add(new DiggerTip { town = tt, day = day, name = f[3].Length > 0 ? f[3] : null });
            else if (f[0] == "e" && f.Length >= 7 && int.TryParse(f[2], out int k) && int.TryParse(f[3], out int st) && int.TryParse(f[4], out int due) && int.TryParse(f[5], out int et))
                NeighbourErrands[f[1]] = new NeighbourErrand { kind = Mathf.Clamp(k, 0, NeighbourAsks.Length - 1), state = st, due = due, town = et, name = f[6].Length > 0 ? f[6] : null };
            else if (f[0] == "s" && f.Length >= 2) MedicMayShop.Add(f[1]);
            else if (f[0] == "p" && f.Length >= 4 && int.TryParse(f[1], out int pt) && int.TryParse(f[3], out int until)) MadMax.Npc.Trade.Held[pt] = (f[2], until);
        }

        void Clear1015()
        {
            SourStalls.Clear(); DiggerTips.Clear(); NeighbourErrands.Clear(); MedicMayShop.Clear(); restockDay.Clear(); askedShopDay.Clear();
            MadMax.Npc.Trade.Held.Clear();
        }
    }
}
