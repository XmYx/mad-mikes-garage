using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;

namespace MadMax.Story
{
    // B5 THE NIGHT WE STAYED (storyline §6): word of the refuge reaches a road crew, and its captain Silas Vance parks at
    // the gate asking thirty scrap a week for "quiet". Pay a levy he signs for (one season), trade safe passage for
    // repairs at cost, expose his double-dealing (Moth, a fed-up rider at the crew's camp, has the book where Silas
    // keeps the Guild's money for clearing "squatters" next to yours), or build defences (mines, gate, watchtower, MG
    // nest...) and raise the flag at the gate when you're ready: strong enough and they think twice, otherwise the raid
    // (Npc/BaseRaid) comes to the garage. Then a shared meal, the start of a wall of keepsakes, and the garage's charter:
    // a private home, a paid workshop or a cooperative refuge.
    public static partial class StoryLibrary
    {
        internal const string B5Levy = "A WRITTEN LEVY, ONE SEASON", B5Passage = "SAFE PASSAGE FOR REPAIRS", B5Expose = "EXPOSED HIS DOUBLE-DEALING", B5Stood = "WE STOOD OUR GROUND";
        internal const string B5Private = "PRIVATE HOME", B5Workshop = "PAID ROADSIDE WORKSHOP", B5Coop = "COOPERATIVE REFUGE";
        internal const string B5Ledger = "story_b5_ledger";
        internal const string B5Meals = "food_stew|food_meat_stew|food_soup|food_fish_soup|food_roast_dinner|food_meat_pie|food_pie|food_mushsoup|food_porridge|food_apple_pie|food_cornbread|food_pancakes";
        /// <summary>Defence score (ClaimFlag.Defence) at which Silas's crew thinks twice instead of trying the garage.</summary>
        internal const int B5Deter = 9;

        internal static string B5_Payoff(string answer, string charter, bool deterred, bool looted)
        {
            string night = answer == B5Levy ? "SILAS SIGNED FOR THIRTY SCRAP AND ONE SEASON; HIS RIDERS WAVE AT YOUR GUESTS NOW. WHEN THE SEASON ENDS, SO DOES THE PAPER."
                : answer == B5Passage ? "SILAS'S RIDERS GET REPAIRS AT COST AND EVERYONE ON THIS ROAD PASSES SAFE. NOBODY PAYS ANYBODY, WHICH CONFUSES THEM."
                : answer == B5Expose ? "SILAS'S CREW READ HIS DOUBLE BOOKS AND LEFT HIM AT THE ROADSIDE. MOTH'S CREW PASSES AS NEIGHBOURS."
                : answer == B5Stood ? (deterred ? "SILAS COUNTED YOUR GUNS FROM THE GATE, SPAT, AND TURNED HIS CREW ROUND. NOBODY FIRED A SHOT."
                    : looted ? "THEY CAME, THEY TOOK SOME STORES, THEY LEFT. THE GARAGE IS STILL STANDING AND SO ARE YOU."
                    : "THEY CAME AT NIGHT AND THE GARAGE HELD.") : "";
            string home = charter == B5Workshop ? "THE CHARTER ON THE WALL SAYS PAID ROADSIDE WORKSHOP: DRIVERS STOP, YOU FIX, THEY PAY, IF THERE ARE PARTS IN THE CHEST."
                : charter == B5Coop ? "THE CHARTER ON THE WALL SAYS COOPERATIVE REFUGE: EVERYONE'S, AND EVERYONE ARGUES ABOUT THE WASHING-UP."
                : charter == B5Private ? "THE CHARTER ON THE WALL SAYS HOME: YOURS, WITH A SPARE BED FOR WHOEVER NEEDS IT." : "";
            return night + " A SHARED MEAL, A WALL FOR KEEPSAKES. " + home + " SAFETY LASTS AS LONG AS THE AGREEMENT OR THE DEFENCES DO.";
        }

        static partial void Author_B5(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register(B5Ledger, "SILAS'S DOUBLE LEDGER");
            Q7Anchors("garage", "garage_yard", "b5_gate", "b5_camp", "b5_ledger");
            q.offerSay = "YOU LOOK WORRIED, NELL.";
            q.offerReply = "A CREW'S BEEN ASKING ABOUT THE LIGHTS AT YOUR GARAGE. THAT'S THE TROUBLE WITH LIGHTS. THEIR CAPTAIN'S PARKED AT YOUR GATE. " +
                           "HEAR WHAT HE WANTS, AND DON'T ANSWER TILL YOU'RE READY: YOU DON'T OWE HIM SPEED.";
            q.hook = "A ROAD CREW HAS HEARD ABOUT THE REFUGE. ITS CAPTAIN WANTS PROTECTION MONEY. NOTHING HAPPENS UNTIL YOU ANSWER.";
            Step(q, "demand", "A ROAD CAPTAIN IS WAITING AT THE GARAGE GATE: HEAR WHAT HE WANTS", "b5_gate")
                .Says("b5_boss", "b5_what", "YOU'RE BLOCKING MY ROAD. WHAT DO YOU WANT?",
                    "SILAS VANCE. MY CREW KEEPS THIS ROAD QUIET, AND QUIET COSTS. THIRTY SCRAP A WEEK, EVERY WEEK, OR THINGS GET LOUD. NO HURRY. I'M A PATIENT MAN. I'LL BE RIGHT HERE WHEN YOU'VE DECIDED.");
            Step(q, "camp", "OPTIONAL: SILAS'S CREW CAMPS DOWN THE ROAD. SOMEBODY THERE ISN'T HAPPY", "b5_camp")
                .Says("b5_moth", "b5_moth", "YOU DON'T LOOK LIKE YOU WANT TO BE HERE.",
                    "I DON'T. SILAS TAKES YOUR THIRTY, AND FIFTY FROM THE GUILD TO CLEAR 'SQUATTERS' OFF THIS ROAD. SAME WEEK, SAME ROAD. HE KEEPS BOTH IN THIS BOOK SO HE DOESN'T MIX THEM UP. " +
                    "TAKE IT. THE CREW DON'T KNOW.").Q7Label("MOTH HANDED IT OVER")
                .When(Goal.Reach, "b5_ledger", 2.2f, "LIFTED IT FROM HIS SADDLEBAG").Optional()
                .Pays(r => r.items.Add((B5Ledger, 1)));
            Step(q, "answer", "ANSWER SILAS: A LEVY IN WRITING, SAFE PASSAGE FOR REPAIRS, HIS DOUBLE BOOKS, OR DEFENCES (MINES, GATE, WATCHTOWER, MG NEST) AND THE FLAG AT THE GATE WHEN YOU'RE READY", "b5_gate")
                .Says("b5_boss", "b5_levy", "WRITE IT DOWN: THIRTY SCRAP, ONE SEASON, SAFE PASSAGE FOR ANYONE WHO STOPS HERE.",
                    "IN WRITING? ...FINE. ONE SEASON. MY RIDERS WAVE AT YOUR GUESTS AND NOTHING ELSE. YOU'RE A STRANGE ONE.").Q7Price(30).Q7Label(B5Levy)
                .Says("b5_boss", "b5_passage", "NO LEVY. YOUR RIDERS GET REPAIRS HERE AT COST AND EVERYONE PASSES SAFE. HERE'S A KIT ON ACCOUNT.",
                    "...REPAIRS AT COST. MY BIKES ARE HELD TOGETHER WITH WIRE AND HOPE. DEAL. DON'T TELL THE OTHER CREWS I'M THIS REASONABLE.").Needs("use_repair_kit").Q7Label(B5Passage)
                .Says("b5_boss", "b5_expose", "READ THIS TO YOUR CREW, SILAS. OR I WILL.",
                    "...WHERE'D YOU... THAT'S NOT... BOYS? IT'S A MISUNDERSTANDING. ...THEY'RE NOT LISTENING TO ME ANY MORE. KEEP YOUR SCRAP. KEEP YOUR ROAD.").Needs(B5Ledger).Q7Label(B5Expose)
                .When(Goal.Event, "b5:stood", label: B5Stood)
                .Pays(r => r.training.Add((Skill.Speech, 4f)));
            Step(q, "meal", "AFTERWARDS, A SHARED MEAL: FOUR HOT PORTIONS FOR THE TABLE AT THE GARAGE", "garage").When(Goal.Have, B5Meals, 4f);
            Step(q, "supper", "EVERYONE AT ONE TABLE: TELL NELL IT'S READY", "garage_yard")
                .Says("nell_garage", "b5_supper", "EVERYONE, SIT DOWN. THERE'S ENOUGH.",
                    "NOW THAT'S A TABLE. ...WELL, DON'T JUST LOOK AT ME, PASS THE BREAD. HERE: VIC SENT A PLATE OFF HER OLD TRUCK FOR THE WALL, AND I FOUND THIS HUBCAP IN MY SHED. " +
                    "A PLACE NEEDS A WALL OF JUNK PEOPLE LEFT BEHIND ON PURPOSE.")
                .Pays(r => { r.take.Add((B5Meals, 4)); r.items.Add(("trophy_plate", 1)); r.items.Add(("trophy_hubcap", 1)); r.training.Add((Skill.Survival, 3f)); });
            Step(q, "charter", "WHAT IS THE GARAGE NOW? DECIDE ITS CHARTER WITH NELL", "garage_yard")
                .Says("nell_garage", "b5_private", "A HOME. MINE, WITH A SPARE BED FOR WHOEVER NEEDS IT.", "A HOME WITH A SPARE BED. THAT'S NOT A SMALL THING. IT'S HOW I STARTED.").Q7Label(B5Private)
                .Says("nell_garage", "b5_workshop", "A WORKSHOP. PAID REPAIRS FOR ANYONE ON THE ROAD.", "HONEST WORK AT HONEST PRICES. YOU'LL BE BUSY, AND YOU'LL NEED PARTS IN THE CHEST. MIKE WOULD'VE LIKED THAT.").Q7Label(B5Workshop)
                .Says("nell_garage", "b5_coop", "A REFUGE. WE RUN IT TOGETHER AND SHARE WHAT IT MAKES.", "EVERYONE'S, THEN. MEANS EVERYONE ARGUES ABOUT THE WASHING-UP. GOOD. THAT'S HOW YOU KNOW IT'S WORKING.").Q7Label(B5Coop);
            Step(q, "posted", "THE CHARTER GOES UP ON THE WALL", "garage").When(Goal.Event, "b5:posted");
            q.reward.scrap = 15; q.reward.training.Add((Skill.Speech, 5f)); q.reward.training.Add((Skill.Construction, 3f)); q.reward.flag = "b5_done";
            q.payoff = B5_Payoff(null, null, false, false);
        }
    }

    public static partial class StoryCast
    {
        static bool B5AtGate => Q7State.At("B5", "demand", "answer") && !Story.Flag("b5:raid");

        static partial void Cast_B5(List<Member> into)
        {
            into.Add(new Member { key = "b5_boss", name = "SILAS VANCE", title = "ROAD CAPTAIN", anchor = "b5_gate", temper = Temper.Greedy,
                outfit = new[] { "duster", "pants", "combat_boots", "goggles", "shoulder" }, tool = "tool_pipe_shotgun", present = () => B5AtGate });
            into.Add(new Member { key = "b5_heavy", name = "KNUCKLES", title = "SILAS'S RIDER", anchor = "b5_gate", temper = Temper.Gruff,
                outfit = new[] { "vest", "jeans", "boots", "bandana", "gloves" }, tool = "tool_pipe_club", present = () => B5AtGate });
            into.Add(new Member { key = "b5_moth", name = "MOTH", title = "YOUNG RIDER, FED UP", anchor = "b5_camp", temper = Temper.Nervous,
                outfit = new[] { "hoodie", "jeans", "boots", "goggles" },
                present = () => Story.StateOf("B5") == Story.State.Active && !Story.StepDone("B5", "answer") });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>B5: the gate on the road in front of the garage, and the crew's camp 0.3-0.8 km down the roads (a
        /// fire, a tarp, bikes, Silas's saddlebag).</summary>
        static partial void Anchors_B5(WorldGen world, Settlement town)
        {
            Q7From(world, "b5_gate", "garage", 0f, 16f, 180f);
            if (!at.ContainsKey("garage")) return;
            Q7Place(world, town, "b5_camp", at["garage"], 300f, 800f, 150f, 5f, 150f, 12f);
            Q7From(world, "b5_ledger", "b5_camp", 2.4f, -2f);
        }
    }
}
