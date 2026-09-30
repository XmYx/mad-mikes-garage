using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;

namespace MadMax.Story
{
    /// <summary>The campaign KEEP THE LIGHT ON (storyline.md) as data: stable quest ids, the prerequisite graph, the
    /// systems each quest needs and, for Playable quests, their steps and rewards. Anchors named in steps ("wreck",
    /// "satchel", "nell", "car", "badge", "town1", "garage", "relay", "depot", "dispatch") are bound per world by
    /// <see cref="StoryAnchors"/>.</summary>
    public static partial class StoryLibrary
    {
        static List<QuestDef> quests;
        static Dictionary<string, QuestDef> byId;

        public static IReadOnlyList<QuestDef> All { get { Ensure(); return quests; } }
        public static QuestDef Get(string id) { Ensure(); return id != null && byId.TryGetValue(id, out var q) ? q : null; }

        static void Ensure()
        {
            if (quests != null) return;
            quests = new List<QuestDef>();
            Main();
            Side();
            byId = new Dictionary<string, QuestDef>();
            foreach (var q in quests) byId[q.id] = q;
        }

        static QuestDef Q(string id, Arc arc, string title, string giver, string summary, string[] after = null, params string[] needs)
        {
            var q = new QuestDef { id = id, arc = arc, title = title, giver = giver, summary = summary };
            if (after != null) q.after.AddRange(after);
            q.needs.AddRange(needs);
            quests.Add(q);
            return q;
        }

        static StepDef Step(QuestDef q, string id, string text, string waypoint = null)
        {
            var s = new StepDef { id = id, text = text, waypoint = waypoint };
            q.steps.Add(s);
            return s;
        }

        static StepDef When(this StepDef s, Goal goal, string key, float amount = 1f, string label = null, string topic = null)
        {
            s.any.Add(new Condition { goal = goal, key = key, amount = amount, label = label, topic = topic });
            return s;
        }

        static StepDef Says(this StepDef s, string npc, string topic, string say, string reply)
        {
            s.any.Add(new Condition { goal = Goal.Talk, key = npc, topic = topic, say = say, reply = reply, label = say });
            return s;
        }

        static StepDef Needs(this StepDef s, string item) { s.any[s.any.Count - 1].requires = item; return s; }
        static StepDef Pays(this StepDef s, System.Action<Reward> fill) { s.reward = new Reward(); fill(s.reward); return s; }
        static StepDef Optional(this StepDef s) { s.optional = true; return s; }

        // ------------------------------------------------------------------ main arcs
        static void Main()
        {
            // ---- A1: the opening (Playable)
            var a1 = Q("A1", Arc.A, "SOMEONE LEFT THE RADIO ON", "nell",
                "Crawl out of the convoy wreck, reach Nell's roadside stop, earn fuel by fixing her rain collector, then get the stranded car running or walk to the first town.",
                null, "story_start", "cast", "water", "driving");
            a1.build = Build.Playable;
            a1.hook = "A VOICE ON THE RADIO COUNTED THE CONVOY. IT STOPPED AT YOUR CALLSIGN: KEEP THE LIGHT ON.";
            Step(a1, "things", "SEARCH THE WRECK FOR YOUR THINGS", "satchel").When(Goal.Reach, "satchel", 2.6f)
                .Pays(r => { r.items.Add((ItemIds.Wrench, 1)); r.items.Add(("tool_knife", 1)); r.items.Add((ItemIds.Canteen, 1)); r.items.Add(("food_can", 1)); r.items.Add(("med_bandage", 1)); r.resources.Add((ResourceType.Water, 1)); });
            Step(a1, "stop", "FOLLOW THE SMOKE TO THE ROADSIDE STOP", "nell").When(Goal.Reach, "nell", 9f);
            Step(a1, "nell", "TALK TO NELL MERCER", "nell").Says("nell", "a1_arrive", "I CRAWLED OUT OF A WRECKED CONVOY UP THE ROAD.",
                    "NELL MERCER. HEARD THE CRASH TWO NIGHTS BACK AND FIGURED NOBODY WALKED AWAY. WATER'S IN THE BARREL. MY RAIN COLLECTOR'S CRACKED: " +
                    "HERE'S MY SPARE CLAW HAMMER AND SOME PATCH TIN. FIX IT AND THE FUEL IN THAT CAN IS YOURS.")
                .Pays(r => { r.items.Add((ItemIds.ClawHammer, 1)); r.resources.Add((ResourceType.Scrap, 4)); });
            Step(a1, "collector", "PATCH NELL'S RAIN COLLECTOR: {Build} WITH THE CLAW HAMMER, AIM AT IT, R TO REPAIR (OR BUILD A NEW ONE)", "nell")
                .When(Goal.Event, "repaired:rain_collector", label: "REPAIR THE OLD ONE")
                .When(Goal.Build, "rain_collector", 12f, "BUILD A NEW ONE")
                .Pays(r => { r.resources.Add((ResourceType.Fuel, 8)); r.resources.Add((ResourceType.Water, 3)); r.training.Add((Skill.Construction, 4f)); });
            Step(a1, "move", "GET MOVING: THE STRANDED CAR NEEDS ITS BATTERY LEAD ({Service} WITH A WRENCH) AND FUEL, OR WALK TO THE FIRST TOWN", "car")
                .When(Goal.Drive, "fleet", 150f, "DRIVE THE CAR 150 M")
                .When(Goal.Reach, "town1", 70f, "REACH THE TOWN");
            Step(a1, "badge", "OPTIONAL: FIND YOUR CONVOY BADGE IN THE WRECK", "badge").When(Goal.Reach, "badge", 2.4f).Optional()
                .Pays(r => r.items.Add(("keepsake_badge", 1)));
            a1.reward.scrap = 10; a1.reward.training.Add((Skill.Mechanics, 6f)); a1.reward.flag = "a1_done";
            a1.payoff = "NELL KNOWS THAT VOICE: MARA'S TRANSMITTER WAS SUPPOSED TO HAVE BURNED. SOMEONE HAS BEEN USING IT.";

            // ---- the rest of arc A (Outline until its verbs exist)
            Q("A2", Arc.A, "THE DEAD DON'T BUY DIESEL", "clerk", "A clerk says you collected your pay and died yesterday. Three leads in one town; find Len Pike.", new[] { "A1" }, "cast", "dialogue", "trade", "evidence");
            Q("A3", Arc.A, "A VOICE WITH YESTERDAY'S WEATHER", "june", "Repair a relay mast's power and compare three recorded fragments with June.", new[] { "A2" }, "radio", "power", "evidence");
            Q("A4", Arc.A, "THE WEIGHT OF EMPTY TRUCKS", "june", "Get inside the depot the convoy's trucks are hauling to.", new[] { "A3" }, "evidence", "trade", "towing");
            Q("A5", Arc.A, "NO ONE RIDES IN THE BACK", "mara", "Prepare seats, water and medicine, then free the convoy workers.", new[] { "A4" }, "transfer", "companions", "clinic");
            Q("A6", Arc.A, "TELL IT STRAIGHT", "ivo", "Verify the records with Ivo and decide what June broadcasts.", new[] { "A5" }, "evidence", "broadcast");
            // ---- arc B: the home
            var b1 = Q("B1", Arc.B, "THE SIGN STILL STANDS", "nell", "Nell offers the derelict garage for clearing its entrance and restoring one work area.", new[] { "A1" }, "building", "salvage");
            b1.build = Build.Playable;
            b1.hook = "NELL: THERE'S A GARAGE AT THE BEND WITH MIKE'S NAME STILL ON THE SIGN. NOBODY'S WORKED IT IN YEARS.";
            b1.offerSay = "ANYWHERE AROUND HERE A MECHANIC COULD WORK?";
            b1.offerReply = "THERE'S A GARAGE AT THE BEND. MIKE'S NAME IS STILL ON THE SIGN; MIKE ISN'T. CLEAR THE DOOR, GET ONE BENCH WORKING AND IT'S YOURS. I'LL TELL ANYONE WHO ASKS.";
            Step(b1, "look", "LOOK OVER THE OLD GARAGE AT THE BEND", "garage").When(Goal.Reach, "garage", 10f);
            Step(b1, "clear", "CLEAR THE BLOCKED DOORWAY: BREAK THE BARRICADE, OR DISMANTLE IT IN BUILD MODE (X)", "garage").When(Goal.Event, "removed_story:barricade");
            Step(b1, "bench", "RESTORE ONE WORK AREA: BUILD A WORKBENCH INSIDE", "garage").When(Goal.Build, "workbench", 9f)
                .Pays(r => { r.resources.Add((ResourceType.Scrap, 12)); r.training.Add((Skill.Construction, 5f)); });
            Step(b1, "claim", "PLANT A CLAIM FLAG TO MAKE IT YOURS", "garage").When(Goal.Build, "claim_flag", 30f);
            b1.reward.scrap = 15; b1.reward.flag = "garage_claimed";
            b1.payoff = "THE GARAGE AT THE BEND IS YOURS. PAINT THE SIGN OR KEEP MIKE'S NAME.";
            Q("B2", Arc.B, "SUPPER FOR FOUR", "nell", "Safe water and a shared meal for Nell, a driver and a displaced gardener.", new[] { "B1" }, "cooking", "water", "garden");
            Q("B3", Arc.B, "LIGHTS WORTH COMING BACK TO", "nell", "Power a lamp and an essential service, a roof, a bed and sanitation; survive a test outage.", new[] { "B1" }, "power", "power_control", "building");
            Q("B4", Arc.B, "WHO GETS A KEY?", "nell", "Rooms and rules for survivors and strangers; one staffed service.", new[] { "B3" }, "residents", "building", "dialogue");
            Q("B5", Arc.B, "THE NIGHT WE STAYED", "nell", "A gang demands protection; pay, expose, negotiate or defend.", new[] { "B3" }, "dialogue", "building");
            // ---- arc C: the routes
            Q("C1", Arc.C, "WATER HAS NO FLAG", "sera", "A detained Nomad water shipment: check quantity and quality, fix a leaking valve, negotiate a route.", new[] { "A2" }, "water_quality", "towing", "dialogue");
            Q("C2", Arc.C, "THE CHEAP ROAD", "sera", "Survey two routes with a trial load; bribe, improve a crossing or share hauling.", new[] { "C1" }, "roads", "trade", "driving");
            Q("C3", Arc.C, "ENOUGH TO GO AROUND", "sera", "Supply a clinic, a farm and a generator from one finite shipment.", new[] { "C2" }, "allocation", "trade");
            Q("C4", Arc.C, "A BRIDGE YOU CAN AFFORD", "sera", "Restore a crossing: earthwork, a built deck or a ferry; test it loaded.", new[] { "C2" }, "bridges", "machinery");
            Q("C5", Arc.C, "NO EMPTY SEAT", "sera", "Organise a demonstration convoy through a disputed checkpoint.", new[] { "C3" }, "player_convoy", "trade");
            // ---- finale
            Q("F1", Arc.F, "THE LONG WAY HOME", "ada", "Keep people supplied, take control of the decision, make the run.", new[] { "A6", "B3", "C3" }, "allocation", "player_convoy", "broadcast");
            // ---- the Last Engine epic (extends the existing relic hunt)
            Q("L1", Arc.L, "THE SOUND BEFORE THE STORM", "cask", "Brother Cask's engine recording and the workshop that built it.", null, "last_engine", "radio");
            Q("L2", Arc.L, "IRON PILGRIMAGE", "cask", "The bunker block and the airfield heads, each with its own small story.", new[] { "L1" }, "last_engine");
            Q("L3", Arc.L, "A CRANKSHAFT AND A GRUDGE", "cask", "Race, trade, negotiate or fight for the gang boss's crankshaft.", new[] { "L1" }, "last_engine", "racing", "dialogue");
            Q("L4", Arc.L, "WHAT A RELIC IS FOR", "cask", "Earn the Church's trust for its blowers.", new[] { "L1" }, "last_engine", "factions");
            Q("L5", Arc.L, "THE LAST RUN", "cask", "Assemble the V12 and drive it to a named far landmark.", new[] { "L2", "L3", "L4" }, "last_engine", "driving");
        }

        /// <summary>Every step's anchors and every quest's givers the catalogue refers to.</summary>
        public static readonly string[] Anchors = { "wreck", "satchel", "nell", "car", "badge", "town1", "garage", "relay", "depot", "dispatch", "una", "gus" };
    }
}
