using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Seasonal chores in conversation (Seasons): a resident asks for the season's chore
    /// (<see cref="NpcLore.Chores"/>) once per half season; bringing it pays scrap by the goods' worth, a small gift from
    /// their stores, goodwill and a little reputation.</summary>
    public partial class Dialogue
    {
        bool pantryTalked;

        /// <summary>Autumn and winter: townsfolk weigh in on the player's stores (<c>WastelandGame.Pantry</c>, days of food
        /// that keep through the cold vs. what rots first).</summary>
        void PantryChoice()
        {
            int season = MadMax.World.Weather.Season;
            if (pantryTalked || (season != 1 && season != 2) || S.disposition < -10) return;
            if (P.role != NpcRole.Resident && P.role != NpcRole.Leader && P.role != NpcRole.Wanderer && P.role != NpcRole.Shopkeeper) return;
            Add("WILL MY STORES SEE ME THROUGH THE WINTER?", () =>
            {
                pantryTalked = true; asked = true;
                float need = Mathf.Max(1, MadMax.World.Weather.DaysPerSeason) * (season == 1 ? 2f : 1f);
                g.Pantry(need, out float keeps, out float rots);
                line = PantryVerdict(P.temper, keeps, rots, need);
                Change(1);
                g.Stats.Practice(MadMax.RPG.Skill.Speech, 0.5f);
                Hub(false);
            }, "WHAT THEY THINK OF YOUR LARDER");
        }

        /// <summary>A townsperson's verdict on stores of <paramref name="keeps"/> days that keep and <paramref name="rots"/>
        /// days that spoil first, against the <paramref name="need"/> days to spring.</summary>
        public static string PantryVerdict(Temper t, float keeps, float rots, float need)
        {
            string days = Mathf.RoundToInt(keeps) + " DAYS";
            string head;
            if (keeps >= need)
                head = t == Temper.Gruff ? days + " PUT BY? YOU'LL DO." : t == Temper.Joker ? days + "? YOU'LL BE THE FATTEST THING ALIVE COME SPRING."
                    : t == Temper.Pious ? days + " IN THE LARDER. THE LORD PROVIDES, AND SO DID YOU." : "YOUR LARDER WILL HOLD - " + days + " OF FOOD THAT KEEPS.";
            else if (keeps >= need * 0.5f)
                head = t == Temper.Nervous ? "ONLY " + days + "? THAT WON'T REACH SPRING. IT WON'T." : t == Temper.Greedy ? days + ". I COULD SELL YOU THE REST. AT WINTER PRICES."
                    : days + " THAT KEEPS. HALF A WINTER. HUNT, FISH THE ICE, PUT UP MORE.";
            else
                head = t == Temper.Joker ? days + "? HOPE YOU LIKE THE TASTE OF BOOT LEATHER." : t == Temper.Gruff ? days + ". YOU'LL STARVE BY MIDWINTER."
                    : "WITH " + days + " PUT BY WE'LL BE EATING BOOT LEATHER BY SPRING. STOCK A CELLAR.";
            if (rots >= 1f) head += " AND " + Mathf.RoundToInt(rots) + " DAYS OF IT WILL TURN - SMOKE IT, SALT IT OR CAN IT.";
            return head;
        }

        void SeasonChoreChoice()
        {
            if (P.role != NpcRole.Resident || S.disposition < -5) return;
            int stamp = NpcLore.ChoreStamp() + 1;
            if (S.choreStamp == stamp)
            {
                if (S.choreState == 1) Add("ABOUT THE " + NpcLore.Chores[S.choreIndex].what + "...", TurnInChore);
                return;
            }
            int now = NpcLore.ChoreNow();
            if (now < 0) return;
            Add("NEED A HAND AROUND THE PLACE?", () => OfferChore(now, stamp), "A SEASONAL CHORE");
        }

        void OfferChore(int index, int stamp)
        {
            asked = true;
            var c = NpcLore.Chores[index];
            line = c.ask;
            choices.Clear();
            Add("CONSIDER IT DONE.", () =>
            {
                S.choreStamp = stamp; S.choreIndex = index; S.choreState = 1;
                Change(2);
                line = "BLESS YOU. I'LL BE HERE.";
                NpcVoice.Say(npc, "thanks", true);
                MadMax.Game.Journal.Add("CHORE", P.Name + ": " + c.ask);
                Hub(false);
            });
            Add("NOT THIS TIME.", () => Hub());
        }

        void TurnInChore()
        {
            var c = NpcLore.Chores[S.choreIndex];
            int have = ChoreCount(c);
            if (have < c.n) { line = "COME BACK WITH " + c.n + " " + c.what + ". YOU'VE GOT " + have + "."; Hub(false); return; }
            float worth = TakeChore(c);
            int scrap = Mathf.CeilToInt(worth * 1.6f) + 6;
            g.Inventory.Add(ResourceType.Scrap, scrap);
            if (c.gift != null) g.Inventory.AddItem(c.gift, c.giftN);
            S.choreState = 2;
            Change(8);
            NpcRegistry.Reputation = Mathf.Min(100, NpcRegistry.Reputation + 1);
            MadMax.Audio.Sfx.Play2D("cash", 0.5f);
            g.Stats.Practice(MadMax.RPG.Skill.Speech, 1f);
            line = c.thanks + " (" + scrap + " SCRAP)";
            Hub(false);
        }

        int ChoreCount(NpcLore.Chore c)
        {
            if (c.item.StartsWith("res:")) return g.Inventory.Get((ResourceType)int.Parse(c.item.Substring(4)));
            if (!c.item.StartsWith("any:")) return g.Inventory.GetItem(c.item);
            int n = 0;
            foreach (var kv in g.Inventory.Items) if (kv.Value > 0 && NpcLore.ChoreTakes(c, kv.Key)) n += kv.Value;
            return n;
        }

        /// <summary>Takes the chore's goods from the pack; returns their trade worth.</summary>
        float TakeChore(NpcLore.Chore c)
        {
            if (c.item.StartsWith("res:")) { g.Inventory.TrySpend((ResourceType)int.Parse(c.item.Substring(4)), c.n); return Trade.Value(c.item) * c.n; }
            if (!c.item.StartsWith("any:")) { g.Inventory.TakeItem(c.item, c.n); return Trade.Value(c.item) * c.n; }
            var ids = new List<string>();
            foreach (var kv in g.Inventory.Items) if (kv.Value > 0 && NpcLore.ChoreTakes(c, kv.Key)) ids.Add(kv.Key);
            ids.Sort((a, b) => Trade.Value(a).CompareTo(Trade.Value(b)));                  // the cheapest first
            int left = c.n; float worth = 0f;
            foreach (var id in ids)
            {
                int k = Mathf.Min(left, g.Inventory.GetItem(id));
                if (k <= 0) continue;
                g.Inventory.TakeItem(id, k);
                worth += Trade.Value(id) * k;
                if ((left -= k) <= 0) break;
            }
            return worth;
        }
    }
}
