using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // A5 NO ONE RIDES IN THE BACK (storyline §5): a survivor's message names the Guild's transfer yard, where the convoy
    // workers sit in the back of a box trailer until the next full tanker comes in (no clock). Mara won't leave without
    // Wes, the driver she left on the secret delivery: stabilise him (first aid kit, a clinic bed in the hut, the town's
    // medic cart, or a guarded shelter at the staging point). Everyone now or one group plus an agreement (the risks are
    // said out loud). How they walk out: buy the contracts, trade repair labour and what you know about the depot, sneak
    // them out after dark, or stop the escort. Then Mara's list (seats, water, medicine) and the player's own "go" at the
    // trailer door (the transfer operation, <see cref="Operation"/>); up to two trips from the staging point, or a hired
    // bus. At the garage the survivors choose where they go; a horn and Ada's allocation schedule change hands.
    public static partial class StoryLibrary
    {
        internal const string A5Kit = "STABILISED WITH A FIRST AID KIT", A5Clinic = "A CLINIC BED IN THE HUT", A5Medic = "THE TOWN'S MEDIC CART",
            A5Shelter = "A GUARDED SHELTER AT THE STAGING POINT";
        internal const string A5All = "EVERYONE AT ONCE", A5Group = "ONE GROUP NOW, THE REST BY AGREEMENT";
        internal const string A5Bought = "BOUGHT THEIR CONTRACTS", A5Labour = "REPAIR LABOUR AND WHAT YOU KNOW", A5Sneak = "SNUCK THEM OUT AFTER DARK", A5Escort = "STOPPED THE ESCORT";
        internal const string A5PlanAll = "story_a5_plan_all", A5PlanGroup = "story_a5_plan_group", A5Worksheet = "story_a5_worksheet", A5Schedule = "story_schedule", A5Horn = "story_convoy_horn";

        static StepDef Q7Label(this StepDef s, string label) { s.any[s.any.Count - 1].label = label; return s; }

        /// <summary>The closing line for how the rescue went (called by the quest's game hook while it runs).</summary>
        internal static string A5_Payoff(string plan, string way)
        {
            string how = way == A5Bought ? "THE OVERSEER SIGNED THEM OFF FOR SCRAP" : way == A5Labour ? "YOU PAID IN WORK AND SILENCE"
                : way == A5Sneak ? "YOU LIFTED THE LATCH WHILE THE ESCORT SLEPT" : way == A5Escort ? "THE ESCORT WON'T BE ESCORTING ANYONE FOR A WHILE" : "THEY WALKED OUT";
            string who = plan == A5Group ? "THREE CAME OUT WITH YOU; THE REST ARE SIGNED OFF ON PAPER AND WILL WALK UP THE ROAD WHEN THE AGREEMENT HOLDS."
                : "NOBODY RODE IN THE BACK AND NOBODY WAS LEFT BEHIND.";
            return "MARA'S CONVOY IS OUT: " + how + ". " + who + " MARA HANDED OVER ADA'S ALLOCATION SCHEDULE: THE COMING RATION SHUTDOWN IS A DECISION, NOT A BREAKDOWN. " +
                   "DR. IVO RUSK AT THE REMNANT CHECKPOINT CAN SAY WHETHER THE PAPERS WILL STAND UP.";
        }

        static partial void Author_A5(QuestDef q)
        {
            q.build = Build.Playable;
            q.giver = null;                                                                   // no one offers it: the message arrives when A4 ends
            ItemIds.Register(A5PlanAll, "MARA'S LIST: EVERYONE");
            ItemIds.Register(A5PlanGroup, "MARA'S LIST: ONE GROUP");
            ItemIds.Register(A5Worksheet, "SIGNED WORK SHEET (TRANSFER PUMP)");
            ItemIds.Register(A5Schedule, "ADA'S ALLOCATION SCHEDULE (EVIDENCE)");
            ItemIds.Register(A5Horn, "CONVOY AIR HORN (REPAIRED)");
            Q7Anchors("a5_yard", "a5_trailer", "a5_hut", "a5_office", "a5_staging", "q7_porch");
            q.hook = "A SURVIVOR'S MESSAGE, PASSED HAND TO HAND: \"THEY MOVE US WHENEVER THE NEXT TANKER COMES IN FULL. THE GUILD TRANSFER YARD. " +
                     "NO RUSH: IT'S NEVER EARLY.\" NO COUNTDOWN, JUST A PLACE.";

            Step(q, "yard", "FIND THE GUILD'S TRANSFER YARD. NOBODY MOVES UNTIL THE NEXT TANKER COMES IN FULL", "a5_yard").When(Goal.Reach, "a5_yard", 45f);
            Step(q, "mara", "THE BOX TRAILER: TALK TO WHOEVER'S INSIDE", "a5_trailer")
                .Says("mara_yard", "a5_mara", "MARA. IT'S ME. I'M NOT DEAD.",
                    "...THE LIST SAYS YOU ARE. THE LIST SAYS A LOT OF THINGS. SIX OF US IN THIS BOX, AND WES IN THE HUT WITH A LEG THAT WON'T HOLD HIM. " +
                    "I LEFT WES ON THE DEPOT ROAD ONCE, WHEN I TOOK ADA'S MONEY. I'M NOT DOING IT TWICE. THEY WON'T MOVE US TILL THE TANKER'S FULL: GET IT RIGHT, NOT FAST.")
                .Pays(r => r.training.Add((Skill.Speech, 2f)));
            Step(q, "wes", "WES CALLOWAY CAN'T TRAVEL AS HE IS: STABILISE HIM (FIRST AID KIT), PUT A CLINIC BED IN THE HUT, PAY FOR THE TOWN'S MEDIC CART, OR BUILD A BED AT THE STAGING POINT", "a5_hut")
                .Says("a5_wes", "a5_kit", "HOLD STILL. THIS KIT HAS SEEN WORSE.", "...HAVE YOU? DON'T ANSWER. ...THAT'S BETTER. I CAN SIT A CAR SEAT. NOT A TRUCK BED. NOT AGAIN.").Needs("med_firstaid").Q7Label(A5Kit)
                .When(Goal.Build, "clinic_bed", 10f, A5Clinic)
                .Says("a5_overseer", "a5_medic", "RADIO THE TOWN MEDIC. I'LL PAY FOR THE CART.", "...FINE. IT'S NOT MY LEG. THE CART COMES AT FIRST LIGHT AND HE GOES STRAIGHT TO A BED. ONE LESS FOR YOUR SEATS.").Q7Price(25).Q7Label(A5Medic)
                .When(Goal.Event, "a5:shelter", label: A5Shelter)
                .Pays(r => r.training.Add((Skill.Survival, 4f)));
            Step(q, "plan", "EVERYONE AT ONCE, OR ONE GROUP NOW? DECIDE WITH MARA (SHE'LL SAY WHAT EACH COSTS)", "a5_trailer")
                .Says("mara_yard", "a5_all", "EVERYONE. NOBODY STAYS IN THAT BOX.",
                    "THEN IT'S SIX SEATS, TWO LITRES OF WATER A HEAD AND SOMETHING FOR THE CUTS. MORE TO CARRY, MORE TO GO WRONG, AND THE OVERSEER NOTICES AN EMPTY TRAILER. BUT NOBODY'S LEFT.").Q7Label(A5All)
                .Says("mara_yard", "a5_group", "THREE NOW. THE REST SIGNED OFF ON PAPER.",
                    "WES, TALLY AND ME. THREE SEATS, LESS TO HIDE. REN AND THE OTHERS STAY ON THE BOOKS UNTIL THE AGREEMENT CLEARS: IF IT GOES SOUR THEY WAIT LONGER. NOBODY GETS HURT FOR IT. I'LL HOLD YOU TO THAT.").Q7Label(A5Group);
            Step(q, "pump", "OPTIONAL: THE YARD'S TRANSFER PUMP IS BROKEN. FIX IT ([B], AIM, R) AND THE OVERSEER OWES YOU A SIGNED WORK SHEET", "a5_yard")
                .When(Goal.Event, "a5:pump").Optional()
                .Pays(r => { r.items.Add((A5Worksheet, 1)); r.training.Add((Skill.Mechanics, 4f)); });
            Step(q, "hire", "OPTIONAL: OTIS BRAND AT THE STAGING POINT HIRES OUT HIS BUS FOR NIGHT RUNS (EIGHT SEATS)", "a5_staging")
                .Says("a5_otis", "a5_hire", "I NEED EIGHT SEATS AND NO QUESTIONS.", "EIGHT SEATS, NO QUESTIONS, SCRAP UP FRONT. I'LL SIT RIGHT HERE WITH THE ENGINE WARM UNTIL YOU SAY GO.").Q7Price(30).Optional();
            Step(q, "way", "HOW DO THEY WALK OUT? BUY THE CONTRACTS, TRADE WORK AND WHAT YOU KNOW, SNEAK THEM OUT AFTER DARK (CROUCH AT THE TRAILER DOOR), OR STOP THE ESCORT", "a5_office")
                .Says("a5_overseer", "a5_buy_all", "I'M BUYING ALL SIX CONTRACTS.", "SIX CONTRACTS, SIX SIGNATURES. THE GUILD GETS ITS SCRAP AND I GET A QUIET NIGHT. THEY'RE YOURS WHEN YOU'RE READY.").Needs(A5PlanAll).Q7Price(90).Q7Label(A5Bought)
                .Says("a5_overseer", "a5_buy_group", "THREE CONTRACTS NOW. SIGN THE OTHER THREE OFF WHEN THE TANKER CLEARS.", "THREE NOW, THREE ON PAPER. I KEEP MY WORD WHEN IT'S WRITTEN DOWN. THEY WALK UP YOUR ROAD TOMORROW.").Needs(A5PlanGroup).Q7Price(40).Q7Label(A5Bought)
                .Says("a5_overseer", "a5_labour", "YOUR PUMP WORKS AGAIN. AND I KNOW WHAT'S IN BAY THREE AT THE DEPOT.", "...THE PUMP I'LL PAY FOR IN PAPERWORK. THE OTHER THING I NEVER HEARD. SIGN HERE. TAKE THEM.").Needs(A5Worksheet).Q7Label(A5Labour)
                .When(Goal.Event, "a5:sneak", label: A5Sneak)
                .When(Goal.Event, "a5:escort_down", label: A5Escort);
            Step(q, "ready", "GET READY (MARA'S LIST): SEATS, WATER, MEDICINE", "a5_trailer").When(Goal.Event, "a5:ready");
            Step(q, "go", "WHEN YOU SAY SO: [E] AT THE TRAILER DOOR", "a5_trailer").When(Goal.Event, "a5:go");
            Step(q, "deliver", "BRING THEM IN: DRIVE THEM TO THE GARAGE AT THE BEND", "garage_yard").When(Goal.Event, "a5:delivered")
                .Pays(r => { r.training.Add((Skill.Driving, 5f)); r.resources.Add((ResourceType.Water, 4)); });
            Step(q, "choose", "AT THE GARAGE: ASK WHERE THEY'LL GO", "q7_porch")
                .Says("mara_home", "a5_where", "YOU'RE OUT. WHERE WILL YOU ALL GO?",
                    "WHERE WE LIKE. THAT'S THE POINT. REN WANTS HIS SISTER'S KITCHEN IN TOWN. THE PELLS WANT THE COAST. WES STAYS TILL THE LEG HOLDS. ME? I OWE SOME PEOPLE THE TRUTH FIRST. " +
                    "REN FIXED THIS ON THE WAY: THE CONVOY'S AIR HORN. AND THIS: ADA'S ALLOCATION SCHEDULE. THE SHUTDOWN THEY'RE CALLING A BREAKDOWN? IT'S TYPED, DATED AND SIGNED.")
                .Pays(r => { r.items.Add((A5Horn, 1)); r.items.Add((A5Schedule, 1)); r.evidence.Add("schedule"); });
            q.reward.scrap = 20; q.reward.training.Add((Skill.Speech, 6f)); q.reward.training.Add((Skill.Driving, 3f)); q.reward.flag = "a5_done";
            q.payoff = A5_Payoff(null, null);
        }
    }

    public static partial class StoryCast
    {
        static bool A5Running => Story.StateOf("A5") == Story.State.Active;
        static bool A5Started => Story.Flag("a5:started");

        static partial void Cast_A5(List<Member> into)
        {
            // at the yard until the door opens
            into.Add(new Member { key = "mara_yard", name = "MARA VALE", title = "CONVOY LEADER", anchor = "a5_trailer", temper = Temper.Proud, female = true,
                outfit = new[] { "bomber", "jeans", "combat_boots" }, present = () => A5Running && !A5Started });
            into.Add(new Member { key = "ren_yard", name = "REN OKAFOR", title = "CONVOY MECHANIC", anchor = "a5_trailer", temper = Temper.Proud,
                outfit = new[] { "overalls", "boots", "gloves" }, present = () => A5Running && !A5Started });
            into.Add(new Member { key = "a5_tally", name = "TALLY PELL", title = "CONVOY RADIO OPERATOR", anchor = "a5_trailer", temper = Temper.Joker, female = true,
                outfit = new[] { "hoodie", "pants", "boots", "beanie" },
                present = () => A5Running && (!A5Started || (Q7State.Number("a5:rest:") > 0 && !Story.Flag("a5:trip2_loaded"))),
                anchorNow = () => A5Started ? "a5_staging" : "a5_trailer" });
            into.Add(new Member { key = "a5_wes", name = "WES CALLOWAY", title = "CONVOY DRIVER, INJURED", anchor = "a5_hut", temper = Temper.Nervous,
                outfit = new[] { "jacket", "jeans", "boots" },
                present = () => A5Running && !A5Started && Story.Route("A5", "wes") != StoryLibrary.A5Medic,
                anchorNow = () => Story.Route("A5", "wes") == StoryLibrary.A5Shelter ? "a5_staging" : "a5_hut" });
            into.Add(new Member { key = "a5_overseer", name = "PRUE HALLORAN", title = "GUILD YARD OVERSEER", anchor = "a5_office", temper = Temper.Greedy, female = true,
                outfit = new[] { "coat", "pants", "boots", "cowboy" }, present = () => A5Running });
            into.Add(new Member { key = "a5_escort", name = "BRANDT", title = "GUILD ESCORT", anchor = "a5_yard", temper = Temper.Gruff,
                outfit = new[] { "vest_scrap", "pants", "combat_boots", "helmet" }, tool = "tool_pipe_shotgun", present = () => A5Running });
            into.Add(new Member { key = "a5_otis", name = "OTIS BRAND", title = "BUS FOR HIRE", anchor = "a5_staging", temper = Temper.Joker,
                outfit = new[] { "sweater", "jeans", "boots", "cowboy" }, present = () => A5Running && !Story.Flag("a5:bus_gone") });
            // at the garage once they're out (Mara stays for A6 and after, as the player decides there)
            into.Add(new Member { key = "mara_home", name = "MARA VALE", title = "CONVOY LEADER", anchor = "q7_porch", temper = Temper.Proud, female = true,
                outfit = new[] { "bomber", "jeans", "combat_boots" },
                present = () => (Story.Flag("a5:trip1") || Story.StateOf("A5") == Story.State.Done) && !Story.Flag("mara_travels") && !Story.Flag("mara_departs") });
            into.Add(new Member { key = "ren_home", name = "REN OKAFOR", title = "CONVOY MECHANIC", anchor = "q7_porch", temper = Temper.Proud,
                outfit = new[] { "overalls", "boots", "gloves" }, present = () => Story.Flag("a5:trip1") && A5Running });
            into.Add(new Member { key = "wes_home", name = "WES CALLOWAY", title = "CONVOY DRIVER, MENDING", anchor = "q7_porch", temper = Temper.Nervous,
                outfit = new[] { "jacket", "jeans", "boots" },
                present = () => (Story.Flag("a5:trip1") || Story.StateOf("A5") == Story.State.Done) && Story.StateOf("A6") != Story.State.Done });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>A5: the Guild transfer yard beside a road 0.7-1.6 km out of the first town (box trailer, tanker bay,
        /// crew hut, the overseer's table), the staging point 160-380 m back along the roads (an old weigh stop where a
        /// bus driver waits for night work), and the porch at the garage where the survivors land.</summary>
        static partial void Anchors_A5(WorldGen world, Settlement town)
        {
            var tc = Q7Town(world, town);
            Q7Place(world, town, "a5_yard", tc, 700f, 1600f, 220f, 9f, 200f, 26f);
            Q7From(world, "a5_trailer", "a5_yard", 1.5f, -4f);
            Q7From(world, "a5_hut", "a5_yard", 9f, -6f, 180f);
            Q7From(world, "a5_office", "a5_yard", 4f, 5f, 180f);
            Q7Place(world, town, "a5_staging", at["a5_yard"], 160f, 380f, 120f, 5f, 120f, 12f);
            Q7From(world, "q7_porch", "garage", -6.5f, 5.5f, 180f);
        }
    }
}
