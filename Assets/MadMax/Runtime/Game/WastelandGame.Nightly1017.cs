using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-17: people notice the smell of a player who hasn't washed (a first meeting goes
    /// cooler, townsfolk passing close remark on it, a companion says so once a day), watching TV together (sitting by
    /// someone who is watching gets a word about the programme; a companion warms to it, the karate tape turns into a
    /// sparring lesson), the neighbour's lamp (the watching neighbour's house on the map and on the HOME line, lit at
    /// night, and ALL WELL? on the handheld radio), and sawdust soaking up spills. Sprinkler wet ground, the sawdust
    /// toilet, stable bedding and kept animals eating scraps live in <c>Sprinkler</c>, <c>Latrine</c>,
    /// <c>Stable</c>/<c>Trough</c> and <c>Animals/Animal.Scraps</c>. Nothing here is saved: remarks and checks are
    /// per session.</summary>
    public partial class WastelandGame
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset1017() { LastSmellRemark = null; LastWatchLine = null; LastAllWell = null; }

        // ------------------------------------------------------------------ smell and company

        /// <summary>The last remark about the player's smell (tests).</summary>
        public static string LastSmellRemark;
        public const float SmellRemarkReach = 2.6f;
        readonly Dictionary<string, int> smellRemarked = new Dictionary<string, int>();
        float smellAt, smellQuietUntil;
        int crewSmellDay = -1;

        /// <summary>How a temperament takes a smelly stranger on a first meeting: disposition change (0 = clean enough).</summary>
        public int SmellReaction(Temper t)
        {
            float f = PlayerFlies();
            if (f <= 0f) return 0;
            int bad = f > 0.7f ? 2 : 1;
            switch (t)
            {
                case Temper.Proud: return -6 * bad;
                case Temper.Nervous: return -4 * bad;
                case Temper.Greedy: case Temper.Gruff: return -3 * bad;
                case Temper.Pious: return -2 * bad;
                case Temper.Friendly: return -1 * bad;
                default: return 0;                                                               // the joker finds it funny
            }
        }

        /// <summary>The aside a first meeting gets for the smell (null: none).</summary>
        public string SmellNote(Temper t, int d)
        {
            if (PlayerFlies() <= 0f) return null;
            if (t == Temper.Joker) return "(THEY WAVE AT THE FLIES ROUND YOUR HEAD AND GRIN: \"BRING YOUR OWN WEATHER, DO YOU?\")";
            return d <= -6 ? "(THEY STEP BACK, A HAND OVER THEIR NOSE.)" : "(THEY WRINKLE THEIR NOSE AT THE FLIES ON YOU.)";
        }

        static readonly string[] SmellLines =
        {
            "PHEW. SOMEBODY FIND THIS ONE A BUCKET.", "STAND DOWNWIND, FRIEND.", "WHAT CRAWLED IN YOUR JACKET AND DIED?",
            "THE FLIES FOLLOW YOU LIKE A DOG.", "THERE'S A RAIN BARREL ROUND THE BACK. USE IT.", "MY EYES ARE WATERING.",
        };
        static readonly string[] CrewSmellLines =
        {
            "NO OFFENCE, BOSS, BUT YOU STINK. A SHOWER, A LAKE, ANYTHING.", "THE FLIES ARE PICKING YOU OVER ME. TAKE THAT AS A HINT.",
            "I'M RIDING IN THE BACK UNTIL YOU'VE WASHED.", "PEOPLE IN TOWN ARE TALKING ABOUT THE SMELL. WE COULD STAND A WASH.",
        };

        /// <summary>A remark over <paramref name="n"/>'s head (a caption bubble; no voice clip).</summary>
        public static void Remark(MadMax.Npc.Npc n, string text, float seconds = 3.5f)
        {
            if (!n || !n.Head) return;
            NpcVoice.Captions.Add(new NpcVoice.Caption { at = n.Head, text = text, until = Time.unscaledTime + seconds });
        }

        void UpdateSmell()
        {
            if (Time.time < smellAt) return;
            smellAt = Time.time + 1f;
            if (!Player || Current || PlayerFlies() <= 0f) return;
            SmellLook(Player.transform.position);
        }

        /// <summary>One look round <paramref name="me"/> for people close enough to notice; the remark made (null: none).</summary>
        public string SmellLook(Vector3 me)
        {
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || n.Hostile || n.Profile == null || n.Profile.Raider || n.proxy) continue;
                float d = Flat(n.transform.position - me);
                if (n.companion)
                {
                    if (d > 6f || crewSmellDay == DayNight.Day) continue;
                    crewSmellDay = DayNight.Day;
                    LastSmellRemark = CrewSmellLines[(DayNight.Day + n.Profile.id.Length) % CrewSmellLines.Length];
                    Remark(n, LastSmellRemark, 4.5f);
                    Hints.Show("wash", "WASH OFF THE SMELL: A SHOWER, A LAKE, OR STANDING IN THE RAIN");
                    return LastSmellRemark;
                }
                if (d > SmellRemarkReach || Time.time < smellQuietUntil) continue;
                if (smellRemarked.TryGetValue(n.Profile.id, out int day) && day == DayNight.Day) continue;
                smellRemarked[n.Profile.id] = DayNight.Day;
                smellQuietUntil = Time.time + 20f;
                LastSmellRemark = SmellLines[(n.Profile.id.GetHashCode() & 0x7fffffff) % SmellLines.Length];
                Remark(n, LastSmellRemark);
                if (n.State != null && n.State.Has(NpcSave.Met)) n.State.disposition = Mathf.Max(-100, n.State.disposition - 1);
                return LastSmellRemark;
            }
            return null;
        }

        // ------------------------------------------------------------------ watching together

        /// <summary>The last word said in front of the TV (tests).</summary>
        public static string LastWatchLine;
        public const float WatchTogetherReach = 3.5f;
        readonly Dictionary<string, int> watchTalked = new Dictionary<string, int>();
        float watchTogetherAt;

        static readonly string[][] ShowLines =
        {
            new[] { "I DROVE THAT ROAD ONCE. BEFORE.", "HE'LL NEVER MAKE THAT BEND AT THAT SPEED. ...HUH. HE DID." },
            new[] { "NEWS FROM BEFORE. NONE OF IT CAME TRUE THE WAY THEY SAID.", "THAT ANCHORMAN HAD THE BEST HAIR IN THE WORLD." },
            new[] { "I USED TO WATCH THIS ONE AS A KID. THE COYOTE NEVER WINS.", "WHY DOES THE ANVIL ALWAYS MISS?" },
            new[] { "SEE HOW HE SEATS THE BEARING? THAT'S HOW YOU DO IT.", "WE COULD BUILD THAT. WE'VE GOT THE BITS." },
            new[] { "THAT'S A GOOD FIRE. LOW AND HOT.", "I'D NEVER CAMP THAT CLOSE TO THE WATER. FLOODS." },
            new[] { "WATCH THE FEET, NOT THE HANDS. COME ON, I'LL SHOW YOU.", "KEEP YOUR GUARD UP, LIKE HIM. HERE - LIKE THIS." },
            new[] { "NOW THAT'S A DERBY. THE ARENA IN THE CITY STILL RUNS THEM, YOU KNOW.", "I'D PUT MY SCRAP ON THE RED ONE." },
            new[] { "HE COULD SELL SAND IN THE DESERT, THAT ONE.", "THE CHART ONLY EVER GOES UP. FUNNY, THAT." },
            new[] { "LOOK AT THOSE BEANS GO. WE SHOULD PLANT SOME.", "TIME-LAPSE. IF ONLY OUR GARDEN GREW THAT FAST." },
        };

        /// <summary>Someone watching within <see cref="WatchTogetherReach"/> of the seated player says a word about the
        /// programme, once per person, programme and day; a companion warms to it (+2), the karate tape is a sparring
        /// lesson (melee practice), the derby a dare to the city arena (journal).</summary>
        void UpdateWatchTogether()
        {
            if (Time.time < watchTogetherAt) return;
            watchTogetherAt = Time.time + 1.5f;
            if (!Player || !Player.Sitting || Current) return;
            WatchTogether(Player.transform.position);
        }

        /// <summary>One look round from <paramref name="me"/>: the line said (null: nobody watching beside the player).</summary>
        public string WatchTogether(Vector3 me)
        {
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || n.Hostile) continue;
                var tv = n.Watching;
                if (!tv || Flat(n.transform.position - me) > WatchTogetherReach) continue;
                if (!n.companion && (n.State == null || !n.State.Has(NpcSave.Met))) continue;     // strangers watch in silence
                int show = tv.Show;
                string key = n.Profile.id + ":" + show;
                if (watchTalked.TryGetValue(key, out int day) && day == DayNight.Day) continue;
                watchTalked[key] = DayNight.Day;
                var lines = ShowLines[Mathf.Clamp(show, 0, ShowLines.Length - 1)];
                LastWatchLine = lines[(DayNight.Day + n.Profile.id.Length) % lines.Length];
                Remark(n, LastWatchLine, 5f);
                if (n.companion && n.State != null) n.State.disposition = Mathf.Min(100, n.State.disposition + 2);
                if (show == 5) { Stats.Practice(MadMax.RPG.Skill.Melee, 0.4f); Toast(n.KnownName + " SHOWS YOU A GUARD FROM THE TAPE (MELEE PRACTICE)"); }
                else if (show == 6) Journal.Add("RUMOUR", n.KnownName + " DARES YOU TO ENTER THE DERBY IN THE ROLLING CITY'S ARENA");
                return LastWatchLine;
            }
            return null;
        }

        // ------------------------------------------------------------------ the neighbour's lamp

        /// <summary>The last answer to ALL WELL? (tests).</summary>
        public static string LastAllWell;
        float allWellAt = -999f;

        /// <summary>The first neighbour watching one of the player's claims, with that claim (null: none).</summary>
        public NeighbourErrand WatchingNeighbour(out ClaimFlag claim)
        {
            claim = null;
            if (Stats == null) return null;
            foreach (var c in ClaimFlag.All)
            {
                if (!c || c.Owner != Stats.name) continue;
                var w = Watchers(c);
                if (w.Count > 0) { claim = c; return w[0]; }
            }
            return null;
        }

        /// <summary>A neighbour's lamp is lit from dusk until they turn in (23 h).</summary>
        public static bool NeighbourLampLit => DayNight.Darkness > 0.35f && (DayNight.Hours >= 16f && DayNight.Hours < 23f);

        /// <summary>Map pins: the watching neighbours' houses (from WorkshopPins), gold while their lamp is lit.</summary>
        public void NeighbourPins(List<Pin> into)
        {
            if (World == null || Stats == null) return;
            var seen = new HashSet<int>();
            foreach (var e in NeighbourErrands.Values)
            {
                if (e.state != 2 || e.town < 0 || e.town >= World.settlements.Count || !seen.Add(e.town)) continue;
                var t = World.settlements[e.town];
                bool watching = false;
                foreach (var c in ClaimFlag.All)
                    if (c && c.Owner == Stats.name && Vector2.Distance(t.pos, new Vector2(c.transform.position.x, c.transform.position.z)) < WatchReach) { watching = true; break; }
                if (!watching) continue;
                bool lit = NeighbourLampLit;
                into.Add(new Pin { label = (e.name ?? "NEIGHBOUR") + (lit ? ": LAMP LIT" : ": WATCHING YOUR PLACE"), pos = new Vector3(t.pos.x, 0f, t.pos.y), color = lit ? new Color32(255, 214, 120, 255) : new Color32(200, 190, 160, 255) });
            }
        }

        /// <summary>The HOME ledger line for <paramref name="claim"/>'s watching neighbour (null: none).</summary>
        public string NeighbourLedgerLine(ClaimFlag claim)
        {
            var w = Watchers(claim);
            if (w.Count == 0) return null;
            var e = w[0];
            string town = e.town >= 0 && World != null && e.town < World.settlements.Count ? Market.TownName(World.settlements[e.town]) : "TOWN";
            return "  NEIGHBOUR: " + (e.name ?? "SOMEONE") + " IN " + town + (NeighbourLampLit ? " - THEIR LAMP IS LIT" : " KEEPS AN EYE OUT") + (w.Count > 1 ? " (+" + (w.Count - 1) + ")" : "");
        }

        /// <summary>ALL WELL? on the handheld radio: the watching neighbour's answer about the claim (null: no neighbour).
        /// After 23 h nobody answers unless raiders are due; asking again within the hour gets a short reply.</summary>
        public string AskAllWell()
        {
            var w = WatchingNeighbour(out var claim);
            if (w == null) return null;
            string who = w.name ?? "YOUR NEIGHBOUR";
            bool raidSoon = BaseRaid.NextDay > DayNight.TotalDays && BaseRaid.NextDay - DayNight.TotalDays < 0.5f;
            if ((DayNight.Hours >= 23f || DayNight.Hours < 5.5f) && !raidSoon) { LastAllWell = "NO ANSWER. " + who + " MUST BE ASLEEP"; Toast(LastAllWell); return LastAllWell; }
            string line;
            if (DayNight.TotalDays - allWellAt < 1f / 24f) line = "SAME AS AN HOUR AGO. GO ON, I'VE GOT IT";
            else if (raidSoon) line = "NOT REALLY. THERE'S TALK OF RAIDERS ON THE ROAD. COME HOME IF YOU CAN";
            else
            {
                var worries = new List<string>();
                HomeWorries(claim.transform.position, worries);
                line = worries.Count > 0 ? "ALL QUIET. THOUGH FROM THE ROAD IT LOOKS LIKE " + worries[0]
                     : NeighbourLampLit ? "ALL QUIET. YOUR PLACE IS DARK, MY LAMP'S ON. SLEEP EASY" : "ALL QUIET. NOBODY'S BEEN BY BUT THE CROWS";
            }
            allWellAt = DayNight.TotalDays;
            NeighbourWord(w, line, claim, false);
            LastAllWell = line;
            return line;
        }

        void NeighbourRadioOptions(List<ContextOption> into)
        {
            if (Inventory.GetItem(SafetyTools.HandRadio) <= 0 || !Player) return;
            var w = WatchingNeighbour(out var claim);
            if (w == null) return;
            bool away = Flat(claim.transform.position - FocusPos) >= PlayerAwayFrom;
            into.Add(Opt("RADIO " + (w.name ?? "THE NEIGHBOUR") + ": ALL WELL?", () => AskAllWell(), false, away ? null : "YOU ARE HOME"));
        }

        // ------------------------------------------------------------------ sawdust for spills

        /// <summary>Litres of a spill one sack of sawdust soaks up.</summary>
        public const float SawdustSoaks = 4f, SawdustReach = 1.6f;
        /// <summary>Litres the last sack soaked up (tests).</summary>
        public static float LastSoaked;

        /// <summary>A sack of sawdust used from the pack: soaks up liquid pooled within <see cref="SawdustReach"/> m
        /// (fuel, oil, water) so it stops slicking the floor and burning; false when there is nothing to soak.</summary>
        public bool UseSawdust()
        {
            if (!Player || Inventory.GetItem(Sawdust.Sack) <= 0) return false;
            var at = Player.transform.position + Player.transform.forward * 0.6f;
            if (Spills.PoolNear(at, SawdustReach, out _, 0.01f) < 0.05f) { Toast("NOTHING SPILLED HERE TO SOAK UP (SAWDUST IS FOR BEDDING AND THE LATRINE TOO)"); return false; }
            LastSoaked = Spills.Take(at, SawdustReach, SawdustSoaks, new FluidMix(), 0.01f);
            Inventory.TakeItem(Sawdust.Sack);
            MadMax.Audio.Sfx.Play("scratch", at, 0.4f, 0.9f, 8f);
            Toast(LastSoaked >= SawdustSoaks * 0.95f ? "THE SAWDUST SOAKS UP WHAT IT CAN: THERE'S MORE" : "THE SAWDUST SOAKS UP THE SPILL");
            return true;
        }

        // ------------------------------------------------------------------ tick

        void UpdateNightly1017()
        {
            ItemIds.Register(Sawdust.Sack, "SACK OF SAWDUST");
            if (!ContextActions.Has("home.neighbour")) ContextActions.Register("home.neighbour", (g, t, into) => { if (t.item == null) g.NeighbourRadioOptions(into); });
            UpdateSmell();
            UpdateWatchTogether();
        }
    }
}
