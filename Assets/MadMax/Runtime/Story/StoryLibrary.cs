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
            foreach (var q in quests) Author(q);
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
                .Pays(r => { r.items.Add((ItemIds.Wrench, 1)); r.items.Add(("tool_knife", 1)); r.items.Add((ItemIds.Canteen, 1)); r.items.Add(("food_can", 1)); r.items.Add(("med_bandage", 1)); r.items.Add(("misc_delivery_chit", 1)); r.resources.Add((ResourceType.Water, 1)); });
            Step(a1, "stop", "FOLLOW THE SMOKE TO THE ROADSIDE STOP", "nell").When(Goal.Reach, "nell", 9f);
            Step(a1, "nell", "TALK TO NELL MERCER", "nell").Says("nell", "a1_arrive", "I CRAWLED OUT OF A WRECKED CONVOY UP THE ROAD.",
                    "NELL MERCER. HEARD THE CRASH TWO NIGHTS BACK AND FIGURED NOBODY WALKED AWAY. WATER'S IN THE BARREL. MY RAIN COLLECTOR'S CRACKED: " +
                    "HERE'S MY SPARE CLAW HAMMER AND SOME PATCH TIN. FIX IT AND THE FUEL IN THAT CAN IS YOURS.")
                .Pays(r => { r.items.Add((ItemIds.ClawHammer, 1)); r.resources.Add((ResourceType.Scrap, 4)); });
            Step(a1, "collector", "PATCH NELL'S RAIN COLLECTOR: [B] WITH THE CLAW HAMMER, AIM AT IT, R TO REPAIR (OR BUILD A NEW ONE)", "nell")
                .When(Goal.Event, "repaired:rain_collector", label: "REPAIR THE OLD ONE")
                .When(Goal.Build, "rain_collector", 12f, "BUILD A NEW ONE")
                .Pays(r => { r.resources.Add((ResourceType.Fuel, 8)); r.resources.Add((ResourceType.Water, 3)); r.training.Add((Skill.Construction, 4f)); });
            Step(a1, "move", "GET MOVING: THE STRANDED CAR NEEDS ITS BATTERY LEAD ([G] WITH A WRENCH) AND FUEL, OR WALK TO THE FIRST TOWN", "car")
                .When(Goal.Drive, "fleet", 150f, "DRIVE THE CAR 150 M")
                .When(Goal.Reach, "town1", 70f, "REACH THE TOWN");
            Step(a1, "badge", "OPTIONAL: FIND YOUR CONVOY BADGE IN THE WRECK", "badge").When(Goal.Reach, "badge", 2.4f).Optional()
                .Pays(r => r.items.Add(("keepsake_badge", 1)));
            a1.reward.scrap = 10; a1.reward.training.Add((Skill.Mechanics, 6f)); a1.reward.flag = "a1_done";
            a1.payoff = "NELL KNOWS THAT VOICE: MARA'S TRANSMITTER WAS SUPPOSED TO HAVE BURNED. SOMEONE HAS BEEN USING IT.";

            // ---- the rest of arc A (Outline until its verbs exist)
            var a2 = Q("A2", Arc.A, "THE DEAD DON'T BUY DIESEL", "clerk", "A clerk says you collected your pay and died yesterday. Three leads in one town; find Len Pike.", new[] { "A1" }, "cast", "dialogue", "trade", "evidence");
            a2.build = Build.Playable;
            a2.offerSay = "I'M HERE TO CASH MY DELIVERY CHIT.";
            a2.offerReply = "THIS CHIT? IT WAS CASHED YESTERDAY. BY YOU, APPARENTLY. AND THEN YOU DIED: IT'S IN THE BOOK. I DON'T PAY DEAD PEOPLE TWICE.";
            a2.hook = "THE FREIGHT OFFICE SAYS YOU CASHED YOUR OWN CHIT AFTER THE CRASH, THEN DIED. SOMEONE IS WEARING YOUR NAME.";
            Step(a2, "receipt", "LEAD: THE FUEL PUMP'S RECEIPT SPIKE", "a2_pump").When(Goal.Reach, "a2_pump", 3.5f).Optional()
                .Pays(r => { r.items.Add(("evidence_receipt", 1)); r.evidence.Add("receipt"); });
            Step(a2, "cook", "LEAD: THE COOK WHO SERVED 'YOU'", "a2_diner").Says("mae", "a2_cook", "DID SOMEONE EAT HERE UNDER MY NAME?",
                    "A SKINNY KID. PAID IN GUILD COUPONS, WHICH NOBODY DOES, AND ASKED WHERE A MAN GETS A TYRE PATCHED CHEAP. I SENT HIM TO THE STALL ON THE EDGE OF TOWN.").Optional()
                .Pays(r => r.evidence.Add("cook"));
            Step(a2, "tracks", "LEAD: TYRE MARKS BY THE REPAIR STALL", "a2_tracks").When(Goal.Reach, "a2_tracks", 3.5f).Optional();
            Step(a2, "leads", "FOLLOW THE LEADS: THE PUMP, THE COOK, THE TYRE MARKS (ANY TWO)", "a2_pump").When(Goal.Steps, "receipt,cook,tracks", 2f);
            Step(a2, "clerk", "MAKE THE CLERK LISTEN", "a2_office")
                .Says("clerk", "a2_proof", "HERE'S YOUR RECEIPT: MY CHIT NUMBER, THE DAY AFTER THE CRASH, NOT MY HAND.", "...THAT'S NOT YOUR SIGNATURE. FINE. THE BOOK IS WRONG AND SO AM I. HE WENT TO THE STALL AT THE EDGE OF TOWN.").Needs("evidence_receipt")
                .Says("clerk", "a2_pay", "I'LL COVER THE DISPUTED FEE. NOW TELL ME WHO CASHED IT.", "MONEY TALKS. A KID WITH A LIMP AND A BIG GRIN. TRY THE REPAIR STALL ON THE EDGE OF TOWN.");
            a2.steps[a2.steps.Count - 1].any[1].price = 10;
            Step(a2, "len", "FIND WHOEVER WORE YOUR NAME: THE REPAIR STALL", "a2_stall")
                .Says("len", "a2_witness", "COME WITH ME AND TELL THEM WHO PAID YOU.", "I... YEAH. OKAY. THEY SAID YOU WERE DEAD. I'LL SAY IT TO ANYONE WHO ASKS.")
                .Says("len", "a2_turn", "YOU'RE GOING TO THE TOWN BOSS.", "PLEASE, I JUST DROVE WHAT THEY TOLD ME... FINE. FINE. I'LL GO.")
                .Says("len", "a2_flee", "GET OUT OF MY SIGHT BEFORE I CHANGE MY MIND.", "YOU WON'T SEE ME AGAIN. HERE: THE MANIFEST THEY GAVE ME. I DON'T WANT IT.");
            a2.reward.scrap = 25; a2.reward.items.Add(("use_repair_kit", 1)); a2.reward.items.Add(("evidence_manifest", 1)); a2.reward.evidence.Add("manifest");
            a2.reward.training.Add((Skill.Speech, 6f)); a2.reward.flag = "a2_done";
            a2.payoff = "THE FORGED MANIFEST CARRIES ADA VENN'S OFFICE STAMP. SOMEONE SCRAWLED A RADIO CALLSIGN ON THE BACK: JUNE BELL'S.";
            var a3 = Q("A3", Arc.A, "A VOICE WITH YESTERDAY'S WEATHER", "june", "Repair a relay mast's power and compare three recorded fragments with June.", new[] { "A2" }, "radio", "power", "evidence");
            a3.build = Build.Playable;
            a3.offerSay = "THE CALLSIGN ON THIS MANIFEST IS YOURS. WHAT DO YOU KNOW ABOUT MARA'S BROADCAST?";
            a3.offerReply = "THAT IT'S NEW EVERY NIGHT AND IT READS YESTERDAY'S WEATHER. GET MY RELAY BACK ON ITS FEET AND I'LL SHOW YOU. THE GENERATOR'S DEAD AND THE MODULE'S STILL UP THE MAST.";
            a3.hook = "JUNE 'SWITCH' BELL RUNS THE RELAY AT THE MAST. MARA'S VOICE IS NEW EVERY NIGHT BUT READS OLD WEATHER.";
            Step(a3, "power", "BRING THE RELAY'S GENERATOR BACK: FUEL IT (PETROL, [T]) AND SWITCH IT ON ([E])", "relay_gen").When(Goal.Event, "a3:power")
                .Pays(r => r.training.Add((Skill.Mechanics, 5f)));
            Step(a3, "module", "GET THE RECORDING MODULE DOWN FROM THE MAST", "relay")
                .When(Goal.Build, "ladder", 10f, "CLIMB FOR IT: BUILD A LADDER AT THE MAST")
                .Says("june", "a3_crew", "PAY THE MAINTENANCE CREW TO FETCH IT.", "THEY'LL DO IT FOR THE PRICE OF A HOT MEAL. HERE: ONE MODULE, SLIGHTLY SINGED.")
                .Pays(r => r.items.Add(("misc_relay_module", 1)));
            a3.steps[a3.steps.Count - 1].any[1].price = 15;
            Step(a3, "frag1", "FRAGMENT: THE COUNT", "relay").Says("june", "a3_f1", "PLAY THE FIRST FRAGMENT.",
                "\"...SEVEN, EIGHT...\" STAMPED 02:10, THREE NIGHTS AGO. SAME CARRIER HISS AS LAST NIGHT'S. IT'S A RECORDING.").Optional();
            Step(a3, "frag2", "FRAGMENT: THE WEATHER", "relay").Says("june", "a3_f2", "PLAY THE SECOND FRAGMENT.",
                "\"...DUST FROM THE WEST, CLEARING BY MORNING...\" THAT STORM PASSED DAYS AGO. SOMEONE REBROADCASTS IT FROM A DIFFERENT MAST EVERY NIGHT.").Optional();
            Step(a3, "frag3", "FRAGMENT: THE BELL", "relay").Says("june", "a3_f3", "PLAY THE LAST ONE.",
                "LISTEN UNDER THE VOICE. THAT DING? A LIFT BELL. THE OLD PUMPING DEPOT HAS ONE. THAT PART'S LIVE.").Optional();
            Step(a3, "compare", "COMPARE THE THREE FRAGMENTS WITH JUNE", "relay").When(Goal.Steps, "frag1,frag2,frag3", 3f);
            Step(a3, "june", "SOMEONE CUT CALLS FROM THAT ROUTE. ASK JUNE STRAIGHT", "relay")
                .Says("june", "a3_accuse", "YOU'VE BEEN CUTTING CALLS OFF THAT ROUTE, HAVEN'T YOU?", "...AT GUILD REQUEST. THEY PAY MY FUEL. I'M SORRY. I KNOW WHAT THAT'S WORTH TO YOU.")
                .Says("june", "a3_ally", "WHOEVER CUT THOSE CALLS, HELP ME PUT IT RIGHT.", "...IT WAS ME. THE GUILD ASKED. I'LL FIX IT. WHATEVER YOU NEED FROM A RADIO, IT'S YOURS.");
            a3.reward.items.Add(("misc_receiver", 1)); a3.reward.evidence.Add("recording"); a3.reward.training.Add((Skill.Speech, 4f)); a3.reward.flag = "masts_known";
            a3.payoff = "THE DISTRESS CALL IS A LOOP, MOVED FROM MAST TO MAST TO LEAD PEOPLE AWAY FROM ONE ROUTE. UNDER IT: THE PUMPING DEPOT'S LIFT BELL.";

            var a4 = Q("A4", Arc.A, "THE WEIGHT OF EMPTY TRUCKS", "june", "Get inside the depot the convoy's trucks are hauling to.", new[] { "A3" }, "evidence", "trade", "towing");
            a4.build = Build.Playable;
            a4.offerSay = "WHERE'S THAT DEPOT?";
            a4.offerReply = "OUT PAST THE LAST MAST: A PUMPING STATION THE GUILD SAYS IS EMPTY. YOUR CONVOY'S 'DESTROYED' TRUCKS DRIVE IN FULL AND COME OUT LIGHT.";
            a4.hook = "THE OLD PUMPING DEPOT IS OFFICIALLY EMPTY. YOUR CONVOY'S TRUCKS KEEP DRIVING IN.";
            Step(a4, "watch", "WATCH THE DEPOT FROM A DISTANCE", "depot").When(Goal.Reach, "depot", 70f);
            Step(a4, "inside", "GET INSIDE: PAPERWORK, A BRIBE, THE SERVICE HATCH, OR THROUGH THE GUARDS", "depot_gate")
                .Says("guard1", "a4_papers", "I'M THE DRIVER ON THIS MANIFEST. OPEN UP.", "...YOUR NAME'S ON IT. GUILD STAMP. GO ON THEN, BAY THREE.").Needs("evidence_manifest")
                .Says("guard1", "a4_bribe", "HERE'S SOMETHING FOR YOUR TROUBLE.", "I DIDN'T SEE YOU. NOBODY DID. TEN MINUTES.")
                .When(Goal.Reach, "depot_tunnel", 3f, "THE SERVICE HATCH")
                .When(Goal.Event, "a4:guards_down", label: "THROUGH THE GUARDS");
            a4.steps[a4.steps.Count - 1].any[1].price = 40;
            Step(a4, "store", "FIND WHAT THE TRUCKS DELIVER", "depot_store").When(Goal.Reach, "depot_store", 5f)
                .Pays(r => r.evidence.Add("cargo"));
            Step(a4, "choice", "REN OKAFOR, YOUR CONVOY'S MECHANIC, IS LOCKED IN WITH THE CARGO: DECIDE ABOUT THE MEDICINE", "depot_store")
                .Says("ren", "a4_take", "WE TAKE THE MEDICINE. THE REFUGE NEEDS IT NOW.", "GOOD. THEY WON'T MISS A CRATE BEFORE MORNING. TELL MARA WE'RE ALIVE. ALL OF US THEY DIDN'T BREAK.")
                .Says("ren", "a4_leave", "LEAVE IT. THE TRACE WILL SHOW WHO GETS IT.", "SMART. THE SERIAL ON THAT CRATE GOES WHEREVER IT GOES. THAT'S YOUR PROOF. TELL MARA WE'RE ALIVE.");
            a4.reward.scrap = 30; a4.reward.training.Add((Skill.Salvaging, 4f)); a4.reward.flag = "a4_done";
            a4.payoff = "ADA VENN DIVERTED PUMPS, FILTERS AND MECHANICS TO A CENTRAL FACILITY AND ERASED THE RECORDS. MARA AND SEVERAL OF YOUR CONVOY ARE ALIVE: THEY MOVE THEM WHEN THE NEXT TANKER ARRIVES.";
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
            var b2 = Q("B2", Arc.B, "SUPPER FOR FOUR", "nell", "Safe water and a shared meal for Nell, a driver and a displaced gardener.", new[] { "B1" }, "cooking", "water", "garden");
            b2.build = Build.Playable;
            b2.offerSay = "THE GARAGE HAS A ROOF AGAIN. WHAT NOW?";
            b2.offerReply = "NOW YOU FEED PEOPLE. WATER YOU'D DRINK, SOMEWHERE TO COOK, A TABLE, A HOT MEAL. I'LL BRING VIC, SHE DRIVES THE SALT ROUTE, AND THERE'S A GARDENER SLEEPING IN THE OLD BUS WHO COULD USE A PLATE.";
            b2.hook = "NELL WANTS SUPPER AT THE GARAGE: FOUR PLATES, SAFE WATER, A TABLE.";
            const string meals = "food_stew|food_meat_stew|food_soup|food_fish_soup|food_roast_dinner|food_meat_pie|food_pie|food_mushsoup|food_porridge|food_apple_pie|food_cornbread|food_pancakes";
            Step(b2, "water", "SAFE WATER AT THE GARAGE: A RAIN COLLECTOR, WELL OR WATER TANK", "garage").When(Goal.Build, "rain_collector|well|water_tank", 22f);
            Step(b2, "kitchen", "SOMEWHERE TO COOK AT THE GARAGE: A CAMPFIRE, STOVE, OVEN OR RANGE", "garage").When(Goal.Build, "campfire|stove|oven|kitchen_range", 22f);
            Step(b2, "table", "A TABLE TO EAT AT", "garage").When(Goal.Build, "dining_table|table", 22f);
            Step(b2, "meal", "COOK FOUR HOT PORTIONS (STEW, SOUP, PIE, ROAST, PORRIDGE...)", "garage").When(Goal.Have, meals, 4f);
            Step(b2, "supper", "SUPPER AT THE GARAGE: TELL NELL IT'S ON THE TABLE", "garage").Says("nell", "b2_supper", "SUPPER'S ON THE TABLE.",
                    "WELL, LOOK AT THAT. VIC, SIT. EZRA, YOU TOO, IT WON'T BITE. ...THIS IS GOOD. WHO MADE THIS? DON'T ANSWER, IT'LL GO TO YOUR HEAD.")
                .Pays(r => r.take.Add((meals, 4)));
            Step(b2, "welcome", "AFTER SUPPER, NELL ASKS: WHO IS THIS PLACE FOR?", "garage")
                .Says("nell", "b2_driver", "VIC CAN STOP HERE WHENEVER SHE'S PASSING.", "SHE'LL TELL EVERY DRIVER ON THE SALT ROUTE. EXPECT COMPANY.")
                .Says("nell", "b2_gardener", "EZRA CAN HAVE THE BACK ROOM. THE BEDS COULD USE HIM.", "HE CRIED A BIT, YOU KNOW. OUT BACK. DON'T MENTION IT.")
                .Says("nell", "b2_mine", "NOBODY YET. IT'S STILL MINE.", "FAIR. A PLACE HAS TO BE SOMEONE'S BEFORE IT'S ANYONE'S.");
            b2.reward.items.Add(("seed_tomato", 3)); b2.reward.items.Add(("seed_herbs", 3)); b2.reward.training.Add((Skill.Farming, 4f)); b2.reward.training.Add((Skill.Survival, 4f)); b2.reward.flag = "b2_done";
            b2.payoff = "FOUR PLATES AT ONE TABLE: NELL, VIC, EZRA AND YOU. THE GARAGE FELT LIKE SOMEWHERE.";
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
        public static readonly List<string> Anchors = new List<string> { "wreck", "satchel", "nell", "car", "badge", "town1", "garage", "relay", "depot", "dispatch", "una", "gus",
                                                     "a2_pump", "a2_diner", "a2_office", "a2_stall", "a2_tracks", "chapel", "hearse", "jo", "jo_t1", "jo_t2", "jo_t3", "jo_garden", "jo_digger",
                                                     "relay_gen", "depot_gate", "depot_tunnel", "depot_store", "garage_yard" };
    }
}
