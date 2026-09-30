using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // F1 THE LONG WAY HOME (storyline §8), the campaign finale. Ada Venn waits at the Guild's central pumping and dispatch
    // yard outside the dispatch city with an offer: restored names, compensation and secure supply for one town if the
    // records stay private. "I had eight running trucks and twelve towns. You found my ugly answer. Show me a working one."
    // The player may walk away; the finale waits. Taking her up on it runs three operations and one commitment:
    //   1. keep people supplied: share the Guild's fuel week (Allocation) among five visible places with Sera; transport
    //      from the earlier chapters (C5's trucks and route, a tanker, C4's crossing) sets how much there is to share;
    //   2. take control of the decision: published terms at Ada's table (authenticated records and three voices of
    //      support; a refusal says why and leaves the rest open), a work stoppage called at the dispatch board (Sera's
    //      drivers and a route that isn't the Guild's), or the yard taken by force with its pumps and tanks whole;
    //   3. commit (the consequences are shown and the game saves first), then make the run: the first critical load to
    //      the place with the biggest need, by the repaired road, a risky shortcut or a prepared water route, while
    //      allies take the other agreed jobs. A lost load is replaced at the yard; a hurt scout can be patched up.
    // The control route becomes the ending (A BETTER BARGAIN, OPEN ROADS, BREAK THE LOCKS), stronger or weaker by support,
    // transport and losses; the last scene is a driver on the refuge's radio asking whether the light means she can stop.
    public static partial class StoryLibrary
    {
        public const string F1Posted = "story_f1_posted", F1Can = "story_f1_can", F1NoWater = "story_f1_nowater";
        public const int F1Cans = 6, F1TableSupport = 3;
        internal const string F1Terms = "PUBLISHED TERMS AT THE TABLE", F1Stop = "A WORK STOPPAGE", F1Seize = "THE YARD TAKEN";
        internal const string F1Road = "THE REPAIRED ROAD", F1Short = "THE RISKY SHORTCUT", F1Water = "THE WATER ROUTE";
        internal const string F1Answer = "ANSWERED THE DRIVER", F1Silent = "LET THE LIGHT ANSWER";

        public static readonly string[] F1Keys = { "town", "village", "clinic", "refuge", "remote" };
        public static readonly string[] F1Names = { "THE FIRST TOWN", "BO'S VILLAGE", "THE CLINIC", "THE REFUGE", "THE NEXT TOWN OUT" };
        static readonly int[] F1Claims = { 5, 4, 3, 3, 6 };

        static partial void Author_F1(QuestDef q)
        {
            ItemIds.Register(F1Posted, "POSTED FUEL WEEK (COPY)");
            ItemIds.Register(F1Can, "GUILD FUEL CAN (FIRST LOAD)");
            ItemIds.Register(F1NoWater, "NO WATER ROUTE");
            foreach (var k in new[] { "f1_yard", "f1_drop", "f1_short" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "YOU WANTED TO SEE ME, ADA. WHAT ARE YOU OFFERING?";
            q.offerReply = "YOUR NAME BACK, AND YOUR CONVOY'S. COMPENSATION. A SECURE SUPPLY FOR ONE TOWN OF YOUR CHOOSING, IF THE RECORDS STAY PRIVATE; THE REST " +
                           "STAY RATIONED. I WON'T PRETEND IT WAS AN ACCIDENT. I HAD EIGHT RUNNING TRUCKS AND TWELVE TOWNS. YOU FOUND MY UGLY ANSWER. SHOW ME A WORKING ONE. " +
                           "THE BOOKS ARE ON THE BOARD, SERA DUNE IS ALREADY READING THEM, AND I'M NOT GOING ANYWHERE.";
            q.hook = "ADA VENN'S OFFER STANDS: YOUR NAME BACK AND ONE TOWN SUPPLIED, IF THE RECORDS STAY QUIET. YOU TOLD HER YOU'D SHOW HER A WORKING ANSWER INSTEAD.";

            Step(q, "plan", "SHARE OUT THE GUILD'S FUEL WEEK WITH SERA AT THE DISPATCH YARD: FIVE PLACES, NOT ENOUGH LOADS", "f1_yard")
                .Says("sera_f1", "f1_byneed", "BY NEED, POSTED ON THE YARD BOARD.", "BY NEED.")
                .Says("sera_f1", "f1_even", "EVEN SHARES, POSTED ON THE YARD BOARD.", "EVEN SHARES.")
                .Says("sera_f1", "f1_town", "THE FIRST TOWN FIRST, AS ADA OFFERED, AND WE SAY SO.", "THE FIRST TOWN FIRST.")
                .Says("sera_f1", "f1_remote", "THE NEXT TOWN OUT FIRST, AND WE SAY SO.", "THE NEXT TOWN OUT FIRST.")
                .Pays(r => r.items.Add((F1Posted, 1)));
            Step(q, "ask", "OPTIONAL: ASK ADA WHAT WOULD MAKE HER PUBLISH TERMS", "f1_yard")
                .Says("ada", "f1_ask", "WHAT WOULD MAKE YOU PUBLISH THE ALLOCATION?", "SHOW ME.").Optional();
            Step(q, "control", "TAKE CONTROL OF THE DECISION: PUT PUBLISHED TERMS TO ADA AT HER TABLE ([E]), CALL A WORK STOPPAGE AT THE DISPATCH BOARD, " +
                               "OR TAKE THE YARD FROM ITS GUARDS WITH THE PUMPS AND TANKS WHOLE", "f1_yard")
                .When(Goal.Event, "f1:agreed", label: F1Terms)
                .When(Goal.Event, "f1:stoppage", label: F1Stop)
                .When(Goal.Event, "f1:seized", label: F1Seize);
            Step(q, "route", "CHOOSE WITH SERA HOW THE FIRST LOAD GOES: THE REPAIRED ROAD, THE RISKY SHORTCUT, OR THE WATER", "f1_yard")
                .Says("sera_f1", "f1_road", "THE ROAD. THE ONE WE MENDED.", "THE ROAD.")
                .Says("sera_f1", "f1_short", "STRAIGHT ACROSS COUNTRY. IT'S SHORTER.", "THE SHORTCUT.")
                .Says("sera_f1", "f1_water", "BY WATER. HAVE A BOAT MADE READY.", "THE WATER.");
            q.steps[q.steps.Count - 1].any[0].label = F1Road;
            q.steps[q.steps.Count - 1].any[1].label = F1Short;
            q.steps[q.steps.Count - 1].any[2].label = F1Water;
            q.steps[q.steps.Count - 1].any[2].requires = F1NoWater;                                   // shown once the tick finds water by the destination
            Step(q, "commit", "AT THE DISPATCH BOARD: READ WHAT COMMITTING MEANS ([E]), THEN COMMIT ([T]). THE GAME SAVES FIRST", "f1_yard")
                .When(Goal.Event, "f1:committed");
            Step(q, "run", "MAKE THE RUN: THE FIRST LOAD TO THE PLACE WITH THE BIGGEST NEED (THE FUEL TRUCK AT THE YARD, OR CANS FROM THE BOARD IN YOUR OWN VEHICLE)", "f1_drop")
                .When(Goal.Event, "f1:delivered");
            Step(q, "treat", "OPTIONAL: KIT ARNO CAME OFF HER BIKE SCOUTING THE SHORTCUT. BANDAGE HER", "f1_short")
                .Says("f1_rider", "f1_treat", "HOLD STILL. THIS IS GOING TO STING.", "...IT DID. THANKS. I'LL RIDE BEHIND YOU FROM HERE, AND SLOWER.").Needs("med_bandage")
                .When(Goal.Event, "f1:fetched", label: "THE REFUGE'S RESIDENTS FETCHED HER").Optional();
            Step(q, "resolve", "THE ROADS HEAR WHAT YOU DID", "f1_drop").When(Goal.Event, "f1:resolved");
            Step(q, "home", "GO HOME TO THE REFUGE AT THE BEND. SOMEONE LEFT THE RADIO ON", "garage_yard").When(Goal.Reach, "garage_yard", 14f);
            Step(q, "answer", "A DRIVER ON THE RADIO ASKS IF THE LIGHT MEANS SHE CAN STOP FOR THE NIGHT: ANSWER HER ([E] AT THE RADIO TABLE), OR LET THE LIGHT ANSWER ([T])", "garage_yard")
                .When(Goal.Event, "f1:answered", label: F1Answer)
                .When(Goal.Event, "f1:silent", label: F1Silent);
            q.reward.scrap = 60; q.reward.training.Add((Skill.Speech, 10f)); q.reward.training.Add((Skill.Driving, 8f)); q.reward.flag = "f1_done";
            q.payoff = "THE FIRST LOAD ARRIVED. WHAT HAPPENS TO THE REST IS EVERYONE'S BUSINESS NOW.";
        }

        // ------------------------------------------------------------------ support from earlier decisions

        /// <summary>Who stands with the player, from the earlier chapters' outcomes (one line each).</summary>
        public static List<string> F1Support()
        {
            var l = new List<string>();
            if (Story.Flag("mara_stays")) l.Add("MARA TESTIFIES (SHE STAYED AT THE GARAGE)");
            else if (Story.Flag("mara_travels")) l.Add("MARA RIDES WITH YOU AND WILL TESTIFY");
            if (Story.Flag("broadcast:full") || Story.Flag("broadcast:redacted")) l.Add("JUNE BROADCASTS FROM THE RELAY (" + (Story.Flag("broadcast:full") ? "FULL RECORDS" : "REDACTED") + ")");
            if (Story.Flag("refuge")) l.Add("NELL RUNS REPAIRS AT THE REFUGE");
            if (Story.Flag("arc_c_done")) l.Add("SERA COORDINATES THE CONVOY DRIVERS");
            if (Story.Flag("residents") || Story.Flag("b4_done")) l.Add("THE REFUGE'S RESIDENTS STAND IN");
            if (Story.Flag("c3_done")) l.Add("TWO TOWNS' PASSAGE AGREEMENT");
            if (Story.Flag("c4_done")) l.Add("THE CROSSING YOU REBUILT");
            return l;
        }

        /// <summary>Support still within reach (chapters that can still be done).</summary>
        static string F1Missing()
        {
            var l = new List<string>();
            if (!Story.Flag("arc_c_done")) l.Add("SERA'S CONVOY (NO EMPTY SEAT)");
            if (!Story.Flag("residents") && !Story.Flag("b4_done")) l.Add("RESIDENTS AT THE REFUGE (WHO GETS A KEY?)");
            if (!Story.Flag("c4_done")) l.Add("THE CROSSING (A BRIDGE YOU CAN AFFORD)");
            if (!Story.Flag("refuge")) l.Add("A REFUGE AT THE GARAGE");
            return l.Count > 0 ? string.Join(", ", l) : "NOTHING LEFT TO BRING";
        }

        /// <summary>Why Ada won't publish terms yet (null: she will).</summary>
        public static string F1NegotiationBlock()
        {
            if (!Story.Evidence("tally") && !Story.Evidence("logbook"))
                return "ADA: 'NOBODY HAS CHECKED A PAGE OF THAT. FORGERIES CUT BOTH WAYS.' THE RECORDS NEED AUTHENTICATING (IVO'S CROSS-CHECK OR WES'S LOGBOOK).";
            int n = F1Support().Count;
            if (n < F1TableSupport)
                return "ADA COUNTS WHO CAME WITH YOU: " + n + ". 'BRING ME " + F1TableSupport + " VOICES AND I'LL PUBLISH. " + (n == 1 ? "ONE IS A COMPLAINT." : n + " IS A COMPLAINT.") + "' MORE SUPPORT: " + F1Missing() + ".";
            return null;
        }

        /// <summary>C5 drivers who said yes.</summary>
        public static int F1Drivers()
        {
            int n = 0;
            foreach (var s in new[] { "isaac", "pru", "tobias" }) if (Story.StepDone("C5", s)) n++;
            return n;
        }

        /// <summary>Why a work stoppage can't hold yet (null: it can). <paramref name="water"/>: open water near the destination.</summary>
        public static string F1StoppageBlock(bool water, string target)
        {
            if (!Story.Flag("arc_c_done") && F1Drivers() < 2)
                return "SERA: 'A STOPPAGE NEEDS DRIVERS WHO'LL PARK UP AND STAY PARKED, AND I DON'T HAVE THEM YET.' (SERA'S DEMONSTRATION CONVOY, NO EMPTY SEAT, WOULD GIVE HER THEM.)";
            if (!Story.Flag("c5_route_open") && !Story.Flag("c4_done") && !water)
                return "SERA: 'IF THE GUILD'S TRUCKS STOP, WHAT MOVES INSTEAD? WE NEED A ROUTE THAT ISN'T THEIRS.' (THE CONVOY ROUTE, THE REBUILT CROSSING, OR WATER NEAR " + target + ".)";
            return null;
        }

        // ------------------------------------------------------------------ the fuel week

        /// <summary>What a place really burns in a week, in loads (lowered by what earlier chapters mended).</summary>
        static int F1LiveNeed(int i) => i switch
        {
            0 => Story.StepDone("C3", "fix_lamps") ? 3 : 4,
            1 => Story.StepDone("C3", "fix_farm") ? 2 : 3,
            2 => 3,
            3 => Story.Flag("residents") ? 3 : 2,
            _ => Story.Flag("c5_route_open") ? 4 : 5,
        };

        /// <summary>The five places as they stand (fixed once the plan is posted).</summary>
        public static List<Allocation.Customer> F1Customers()
        {
            var l = new List<Allocation.Customer>();
            for (int i = 0; i < F1Keys.Length; i++)
            {
                int need = ArcCRecord.Has("f1:need:" + F1Keys[i]) ? ArcCRecord.Get("f1:need:" + F1Keys[i]) : F1LiveNeed(i);
                l.Add(new Allocation.Customer(F1Keys[i], F1Names[i], F1Claims[i], need, i == 2));
            }
            return l;
        }

        public static Allocation.Plan F1PlanOf(string label, out int favoured)
        {
            favoured = -1;
            if (label == null) return Allocation.Plan.ByNeed;
            if (label.StartsWith("EVEN")) return Allocation.Plan.Even;
            if (label.StartsWith("THE FIRST TOWN")) { favoured = 0; return Allocation.Plan.Favour; }
            if (label.StartsWith("THE NEXT TOWN")) { favoured = 4; return Allocation.Plan.Favour; }
            return Allocation.Plan.ByNeed;
        }

        /// <summary>The posted shares (null before posting).</summary>
        public static int[] F1Shares()
        {
            if (!ArcCRecord.Has("f1:supply")) return null;
            var sh = new int[F1Keys.Length];
            for (int i = 0; i < sh.Length; i++) sh[i] = ArcCRecord.Get("f1:share:" + F1Keys[i]);
            return sh;
        }

        /// <summary>Which commitments the shares can actually meet: "CAN BE MET: ...; SHORT: ...".</summary>
        public static string F1CanMeet(List<Allocation.Customer> cs, int[] sh)
        {
            var met = new List<string>(); var shortOf = new List<string>();
            for (int i = 0; i < cs.Count; i++)
                if (sh[i] >= cs[i].need) met.Add(cs[i].name);
                else shortOf.Add(cs[i].name + " (" + sh[i] + " OF " + cs[i].need + ")");
            return "CAN BE MET: " + (met.Count > 0 ? string.Join(", ", met) : "NONE") + (shortOf.Count > 0 ? ". SHORT: " + string.Join(", ", shortOf) : ". EVERY NEED COVERED") + ".";
        }

        /// <summary>What Sera says to a plan: the numbers it would post with <paramref name="supply"/> loads today.</summary>
        public static string F1PlanReply(string label, int supply)
        {
            var cs = F1Customers();
            var plan = F1PlanOf(label, out int fav);
            var sh = Allocation.Shares(cs, supply, plan, fav);
            string head = plan == Allocation.Plan.Even ? "EVEN SHARES: NOBODY CAN SAY THEY WERE CHEATED, ONLY THAT A CLINIC AND A GARAGE WERE COUNTED THE SAME."
                : plan == Allocation.Plan.Favour ? (fav == 0 ? "ADA'S OFFER, IN PUBLIC: THE FIRST TOWN IN FULL, AND EVERYONE CAN READ WHO PAYS FOR IT."
                                                             : "THE NEXT TOWN OUT FIRST: THE ONE THE TOLL BOOK LIED ABOUT LONGEST. SAY IT LOUD.")
                : "BY NEED: THE CLINIC FIRST, THE REST IN PROPORTION TO WHAT THEY REALLY BURN.";
            return head + " " + supply + " LOADS THIS WEEK. POSTED: " + Allocation.Posted(cs, sh, supply, "LOADS") + ". " + F1CanMeet(cs, sh);
        }

        /// <summary>The place with the biggest need once the plan is posted: the largest shortfall, then the largest need
        /// (-1 before posting).</summary>
        public static int F1Target()
        {
            var sh = F1Shares();
            if (sh == null) return -1;
            var cs = F1Customers();
            int best = 0;
            for (int i = 1; i < cs.Count; i++)
            {
                int gap = cs[i].need - sh[i], bg = cs[best].need - sh[best];
                if (gap > bg || (gap == bg && cs[i].need > cs[best].need)) best = i;
            }
            return best;
        }

        public static string F1TargetName() { int t = F1Target(); return t < 0 ? "THE PLACE WITH THE BIGGEST NEED" : F1Names[t]; }

        /// <summary>The anchor the load goes to for a place.</summary>
        public static string F1DropAnchor(int i)
        {
            string a = i == 0 ? "c3_generator" : i == 1 ? "c3_farm" : i == 2 ? "c3_clinic" : i == 3 ? "garage_yard" : "c5_depot";
            if (StoryAnchors.Has(a)) return a;
            return i == 1 && StoryAnchors.Has("c_village") ? "c_village" : "town1";
        }

        // ------------------------------------------------------------------ endings

        public static string F1EndingOf(string control) => control == F1Stop ? "open_roads" : control == F1Seize ? "break_locks" : control == F1Terms ? "better_bargain" : null;
        public static string F1EndingTitle(string e) => e == "open_roads" ? "OPEN ROADS" : e == "better_bargain" ? "A BETTER BARGAIN" : e == "break_locks" ? "BREAK THE LOCKS" : "";

        /// <summary>Strength of an ending: support, how much of the week's need the posted plan covers, the clinic, and
        /// losses on the way (lost loads, an untreated scout). Not a morality score: it's printed with its parts.</summary>
        public static int F1Strength(int support, int shortfall, bool clinicShort, int losses) =>
            support + (shortfall == 0 ? 2 : shortfall <= 2 ? 1 : 0) + (clinicShort ? 0 : 1) - losses;
        public const int F1Strong = 6;

        /// <summary>What improves, what stays hard, what play looks like after (storyline §8 table), by strength.</summary>
        public static string[] F1EndingLines(string e, bool strong)
        {
            switch (e)
            {
                case "open_roads":
                    return new[]
                    {
                        strong ? "THE RECORDS ARE CORROBORATED IN PUBLIC AND THE TOWNS SHARE ALLOCATION AUTHORITY: EVERY TOWN ON THE ROUTES HAS A SEAT AT THE DISPATCH TABLE."
                               : "THE TOWNS SHARE ALLOCATION AUTHORITY, THINLY: TOO FEW TRUCKS AND TOO FEW WITNESSES, AND SOME SEATS AT THE TABLE STAY EMPTY.",
                        "MEETINGS, MAINTENANCE AND UNEVEN LOCAL CAPACITY ARE REAL WORK NOW, AND NOBODY IS PAID TO DO IT.",
                        "INDEPENDENT CONVOYS RUN; NOTICE BOARDS POST SHORTAGES; REPAIR CONTRACTS ARE VOLUNTARY.",
                    };
                case "break_locks":
                    return new[]
                    {
                        strong ? "THE DEPOT REGIME IS GONE AND ITS PUMPS STILL WORK: THE TOWNS RUN THE YARD THEMSELVES."
                               : "THE DEPOT REGIME IS GONE. WHAT'S LEFT OF THE YARD RUNS SHORT, AND PEOPLE REMEMBER HOW IT WENT.",
                        "LOST INFRASTRUCTURE AND FRACTURED TRUST MEAN LESS FUEL THIS SEASON. PEOPLE NEED HELP REBUILDING.",
                        "SALVAGE, REPAIR AND RECONCILIATION WORK. NO MAGIC PROSPERITY AFTER THE BOSS FALLS.",
                    };
                default:
                    return new[]
                    {
                        strong ? "THE GUILD KEEPS ITS PUMPS AND TRUCKS UNDER PUBLISHED, ENFORCEABLE QUOTAS, AUDITED BY THE TOWNS."
                               : "THE GUILD PUBLISHES QUOTAS, BUT OVERSIGHT IS THIN: THE REMOTE TOWNS WILL HAVE TO KEEP SHOUTING.",
                        "ADA, OR WHOEVER FOLLOWS HER, STILL HOLDS THE PUMPS. THE REMOTE SETTLEMENTS NEED SOMEONE AT THE TABLE EVERY SEASON.",
                        "THE MAIN ROUTES RUN ON TIME: AUDITS, AND ARGUMENTS ABOUT THE TERMS.",
                    };
            }
        }

        /// <summary>The news that goes out over WasteTalk.</summary>
        public static string F1Headline(string e, bool june) => (june ? "JUNE BELL, WASTETALK 90.1: " : "WORD ON THE ROADS: ") + (e switch
        {
            "open_roads" => "THE GUILD'S TRUCKS STOOD STILL AND THE FUEL MOVED ANYWAY. THE TOWNS WILL SHARE THE DISPATCH TABLE FROM NOW ON",
            "break_locks" => "THE DISPATCH YARD HAS CHANGED HANDS. ITS PUMPS ARE WORKING; ITS LOCKS ARE NOT",
            _ => "THE FUEL GUILD PUBLISHES ITS ALLOCATION: A QUOTA FOR EVERY TOWN, AUDITED IN PUBLIC",
        });

        /// <summary>The closing line: how the decision was taken, the run, what it cost, and the answer on the radio.</summary>
        public static string F1Payoff(string answer)
        {
            string control = Story.Route("F1", "control"), route = Story.Route("F1", "route");
            string e = F1EndingOf(control);
            bool strong = Story.Flag("f1_strong");
            var parts = new List<string>();
            parts.Add(F1EndingTitle(e) + (strong ? "" : ", HARD WON") + ".");
            parts.Add(control == F1Stop ? "SERA'S DRIVERS PARKED UP AND THE FUEL MOVED WITHOUT THE GUILD."
                : control == F1Seize ? "YOU TOOK THE YARD AND KEPT ITS PUMPS WORKING."
                : (Story.Flag("f1_neg_failed") ? "ADA TURNED YOU DOWN ONCE; THE SECOND TIME SHE PUBLISHED THE TERMS." : "ADA PUBLISHED THE TERMS AT HER OWN TABLE."));
            parts.Add("THE FIRST LOAD REACHED " + F1TargetName() + " BY " + (route ?? "ROAD") + (Story.Flag("f1_via_crossing") ? ", OVER THE CROSSING YOU REBUILT" : "") + ".");
            int lost = 0; while (Story.Flag("f1_lost:" + lost)) lost++;
            if (lost > 0) parts.Add(lost == 1 ? "ONE LOAD WAS LOST AND REPLACED." : lost + " LOADS WERE LOST AND REPLACED.");
            if (Story.Flag("f1_rider_down")) parts.Add(Story.StepDone("F1", "treat") ? "KIT ARNO RIDES AGAIN." : "KIT ARNO IS STILL LIMPING.");
            if (answer == F1Answer) parts.Add("A DRIVER ASKED IF THE LIGHT MEANT SHE COULD STOP. YOU TOLD HER YES.");
            else if (answer == F1Silent) parts.Add("A DRIVER ASKED IF THE LIGHT MEANT SHE COULD STOP. THE LIGHT ANSWERED.");
            return string.Join(" ", parts);
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_F1(List<Member> into)
        {
            // Ada (core "ada", anchored at the dispatch city) waits at the yard itself once the finale is open
            for (int i = 0; i < into.Count; i++)
            {
                if (into[i].key != "ada") continue;
                var ada = into[i];
                ada.anchor = "f1_yard";
                ada.present = () => Story.Campaign && Story.StateOf("F1") != Story.State.Locked && !Story.Flag("f1_ending_break_locks");
                into[i] = ada;
            }
            System.Func<bool> guarding = () => Story.StateOf("F1") != Story.State.Locked && Story.StateOf("F1") != Story.State.Done && !Story.Flag("f1_seized");
            into.Add(new Member { key = "f1_guard1", name = "BRASK", title = "GUILD YARD GUARD", anchor = "f1_yard", temper = Temper.Gruff,
                                  outfit = new[] { "vest_scrap", "pants", "combat_boots", "helmet" }, tool = "tool_pipe_shotgun", present = guarding });
            into.Add(new Member { key = "f1_guard2", name = "ODELL", title = "GUILD YARD GUARD", anchor = "f1_yard", temper = Temper.Greedy,
                                  outfit = new[] { "jacket", "pants", "boots", "bandana" }, tool = "tool_pipe_club", present = guarding });
            // Sera at the yard (same person as "sera", seen at another place) while the week is planned and the run prepared
            into.Add(new Member { key = "sera_f1", name = "SERA DUNE", title = "WATER SURVEYOR", anchor = "f1_yard", temper = Temper.Proud, female = true,
                                  outfit = new[] { "duster", "pants", "boots", "shemagh" },
                                  present = () => Story.StateOf("F1") == Story.State.Active && !Story.StepDone("F1", "run") });
            into.Add(new Member { key = "f1_rider", name = "KIT ARNO", title = "SCOUT RIDER, HURT", anchor = "f1_short", temper = Temper.Joker, female = true,
                                  outfit = new[] { "bomber", "jeans", "boots", "goggles" },
                                  present = () => Story.StateOf("F1") == Story.State.Active && Story.Flag("f1_rider_down") && !Story.StepDone("F1", "treat") });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>F1: the Guild's central pumping and dispatch yard: open, level ground beside the road out of the dispatch
        /// city towards home, just past its edge. The dispatch city is the core binding's (the city farthest from the start);
        /// a world without a city uses the settlement farthest from the start and binds "dispatch" there. "f1_drop" (where
        /// the first load goes) and "f1_short" (the shortcut's scouting point) are moved at runtime once the plan is posted
        /// (<see cref="ArcCMove"/>; rebound from the seed on load and moved again by the quest's tick).</summary>
        static partial void Anchors_F1(WorldGen world, Settlement town)
        {
            world.Yard(out var o, out _, out _);
            var o2 = new Vector2(o.x, o.z);
            Settlement city = null; float cd = -1f;
            foreach (var st in world.settlements)
                if (st.kind == Biome.City && st != town) { float d = Vector2.Distance(st.pos, o2); if (d > cd) { cd = d; city = st; } }
            Vector3 centre; float rad;
            if (city != null) { centre = new Vector3(city.pos.x, 0f, city.pos.y); rad = city.radius; }
            else
            {
                Settlement far = null; float fd = -1f;
                foreach (var st in world.settlements) if (st != town) { float d = Vector2.Distance(st.pos, o2); if (d > fd) { fd = d; far = st; } }
                if (far != null) { centre = new Vector3(far.pos.x, 0f, far.pos.y); rad = far.radius; }
                else { centre = o + new Vector3(1400f, 0f, 600f); rad = 30f; }
                ArcC.Put(world, "dispatch", centre, 0f);                                           // the core binds "dispatch" only when there is a city
            }
            var home = at.TryGetValue("town1", out var t1) ? t1 : o;
            var toHome = new Vector3(home.x - centre.x, 0f, home.z - centre.z);
            toHome = toHome.sqrMagnitude > 1f ? toHome.normalized : Vector3.forward;
            // beside a road just past the city's edge on the side facing home; then any side; then the plain line home
            if (!Roadside(world, centre, rad + 20f, rad + 180f, 20f, p => Vector3.Dot((p - centre).normalized, toHome) > 0.3f && ArcC.Free(p, 40f) && Level(world, p, 9f, 1.6f), out var yard, out float yf)
                && !Roadside(world, centre, rad + 15f, rad + 320f, 20f, p => ArcC.Free(p, 30f) && Level(world, p, 7f, 2.2f), out yard, out yf))
            {
                var line = new List<Vector3> { centre, centre + toHome * (rad + 400f) };
                yard = ArcC.Beside(world, line, rad + 40f, 18f, p => ArcC.Free(p, 30f), out yf, 1f);
            }
            ArcC.Put(world, "f1_yard", yard, yf);
            Clearing("f1_yard", 26f);
            var drop = at.TryGetValue("c5_depot", out var dp) ? dp : home;
            ArcC.Put(world, "f1_drop", drop, 0f);
            ArcC.Put(world, "f1_short", Vector3.Lerp(yard, drop, 0.5f), 0f);
        }

    }
}
