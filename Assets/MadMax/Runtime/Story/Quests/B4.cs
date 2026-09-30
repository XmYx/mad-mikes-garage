using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;

namespace MadMax.Story
{
    // B4 WHO GETS A KEY? (storyline §6): Hester Quayle (a road cook with nowhere left) and Judd Kessler (who rode with a
    // gang and stopped) ask for space at the garage. Rooms with some privacy (two beds and a door) or lodging paid at
    // Mae's in town. Hester objects to Judd: his crew took her father's water pump. Find out what actually happened
    // (ask Judd straight, or ask Nell, who fixed the pump and still has it) and set rules instead of a verdict: the
    // pump comes home and Judd fits it, house rules for everyone, or Judd sleeps in town. Then one staffed service
    // (<see cref="Residents"/>: repair labour, tending crops, medical resupply) and a stocked pantry: they eat every
    // evening, and a hungry day is a day nobody works.
    public static partial class StoryLibrary
    {
        internal const string B4Built = "BUILT THEIR ROOMS", B4Lodging = "LODGING AT MAE'S IN TOWN";
        internal const string B4Pump = "THE PUMP COMES HOME", B4House = "HOUSE RULES FOR ALL", B4Town = "JUDD SLEEPS IN TOWN";
        internal const string B4Repair = "REPAIR LABOUR", B4Crops = "TENDING CROPS", B4Medical = "MEDICAL RESUPPLY";

        internal static string B4_Payoff(string rooms, string rules, string service)
        {
            string where = rooms == B4Lodging ? "HESTER AND JUDD SLEEP OVER MAE'S DINER AND WALK OUT TO THE GARAGE FOR WORK." : "HESTER AND JUDD HAVE ROOMS AT THE GARAGE WITH DOORS THAT LOCK.";
            string rule = rules == B4Pump ? "THE QUAYLE PUMP IS BACK IN THE GROUND BEHIND THE GARAGE; JUDD FITTED IT AND HESTER WATCHED EVERY BOLT."
                : rules == B4House ? "THE RULES ARE THE SAME FOR EVERYONE, AND HESTER KEEPS HER KEY ON A STRING ROUND HER NECK."
                : rules == B4Town ? "JUDD EATS HERE AND SLEEPS IN TOWN. NOBODY PRETENDS IT'S FRIENDSHIP YET." : "";
            string work = service == B4Repair ? "JUDD KEEPS THE YARD'S CARS RUNNING" : service == B4Crops ? "HESTER KEEPS THE BEDS WATERED AND THE PANTRY FED" : service == B4Medical ? "HESTER ROLLS BANDAGES FOR THE CABINET" : "THEY WORK";
            return where + " " + rule + " " + work + ", AS LONG AS THERE'S FOOD IN THE STORES. OTHER SERVICES ARE STILL YOURS TO EARN.";
        }

        static partial void Author_B4(QuestDef q)
        {
            q.build = Build.Playable;
            Q7Anchors("garage", "b4_rooms", "b4_work", "a2_diner", "nell");
            q.offerSay = "PEOPLE KEEP ASKING IF THE GARAGE HAS ROOM.";
            q.offerReply = "IT DOES IF YOU SAY IT DOES. TWO OF THEM ARE SITTING ON YOUR STEP RIGHT NOW. TALK TO THEM BEFORE YOU DECIDE ANYTHING: A KEY'S EASY TO GIVE AND HARD TO TAKE BACK.";
            q.hook = "SURVIVORS AND STRANGERS ARE ASKING FOR SPACE AT THE GARAGE: ROOMS, RULES, AND SOME PRIVACY.";
            Step(q, "hester", "HEAR OUT HESTER QUAYLE", "b4_rooms")
                .Says("b4_hester", "b4_hester", "YOU WANTED TO ASK ME SOMETHING?",
                    "HESTER QUAYLE. I COOKED FOR A ROAD CREW FOR SIX YEARS, UNTIL THE CREW STOPPED BEING A CREW. I CAN COOK FOR A GARAGE. I NEED A DOOR THAT LOCKS AND A BED THAT'S MINE. THAT'S ALL.").Optional();
            Step(q, "judd", "HEAR OUT JUDD KESSLER", "b4_rooms")
                .Says("b4_judd", "b4_judd", "AND WHAT ABOUT YOU?",
                    "JUDD KESSLER. I RODE WITH A GANG FOR THREE YEARS. I STOPPED. I'M GOOD WITH ENGINES AND I DON'T SLEEP MUCH. I'LL TAKE THE FLOOR IF THAT'S WHAT'S GOING. I'D RATHER NOT TAKE IT FROM ANYONE WHO MINDS.").Optional();
            Step(q, "heard", "HEAR THEM OUT: HESTER QUAYLE AND JUDD KESSLER WANT SPACE AT THE GARAGE", "b4_rooms").When(Goal.Steps, "hester,judd", 2f);
            Step(q, "rooms", "ROOMS WITH SOME PRIVACY: TWO BEDS AND A DOOR AT THE GARAGE, OR PAY MAE HOLLOWAY TO PUT THEM UP IN TOWN", "garage")
                .When(Goal.Event, "b4:rooms", label: B4Built)
                .Says("mae", "b4_lodging", "MAE, CAN YOU PUT TWO PEOPLE UP? I'LL PAY THE FIRST MONTH.",
                    "TWO BEDS OVER THE DINER, BREAKFAST IF THEY'RE UP IN TIME. THEY CAN WALK OUT TO YOUR GARAGE FOR WORK. NOBODY SLEEPS IN MY KITCHEN.").Q7Price(30).Q7Label(B4Lodging)
                .Pays(r => r.training.Add((Skill.Construction, 3f)));
            Step(q, "objection", "HESTER HAS SOMETHING TO SAY ABOUT JUDD", "b4_rooms")
                .Says("b4_hester", "b4_objection", "SOMETHING'S WRONG. WHAT IS IT?",
                    "HIM. JUDD RODE WITH THE CREW THAT TOOK MY FATHER'S WATER PUMP. WE CARRIED WATER TWO MILES A DAY FOR A YEAR. MY FATHER DIDN'T SEE THE END OF IT. YOU WANT ME SLEEPING DOWN THE HALL FROM THAT?");
            Step(q, "investigate", "FIND OUT WHAT ACTUALLY HAPPENED TO THE QUAYLES' PUMP: ASK JUDD STRAIGHT, OR ASK NELL (SHE'S FIXED MOST PUMPS ROUND HERE)", "b4_rooms")
                .Says("b4_judd", "b4_ask", "HESTER SAYS YOUR CREW TOOK HER FATHER'S PUMP.",
                    "WE DID. I WAS THERE. I WAS THE ONE WHO SAID LEAVE THEM THE BUCKET, WHICH IS NOT MUCH TO BE PROUD OF. A WEEK LATER I TOOK THE PUMP BACK OFF THE BOSS AND PAID A MECHANIC TO FIX IT, " +
                    "TO TAKE IT HOME. THE HOUSE WAS EMPTY. I NEVER TOLD ANYONE, BECAUSE IT SOUNDS LIKE A STORY.").Q7Label("ASKED JUDD STRAIGHT")
                .Says("nell", "b4_nell", "NELL, WHAT DO YOU KNOW ABOUT JUDD KESSLER?",
                    "JUDD? BROUGHT ME A STOLEN PUMP TWO SUMMERS BACK, PAID TO HAVE IT FIXED, SAID IT WAS GOING HOME. CAME BACK A MONTH LATER STILL CARRYING IT: THE HOUSE WAS EMPTY. " +
                    "IT'S IN MY SHED WITH A QUAYLE TAG ON IT. I'D HAVE SAID SOMETHING IF ANYONE HAD ASKED.").Q7Label("NELL REMEMBERS THE PUMP")
                .Pays(r => r.training.Add((Skill.Speech, 3f)));
            Step(q, "rules", "SET THE RULES FOR THE HOUSE (HESTER WILL SAY IF SHE CAN LIVE WITH THEM)", "b4_rooms")
                .Says("b4_hester", "b4_rule_pump", "JUDD FITS YOUR FATHER'S PUMP AT THE GARAGE. IT'S YOURS, AND HE WORKS IT OFF.", "...THE SAME PUMP? ...ALL RIGHT. HE FITS IT, I WATCH, AND WE SEE.").Q7Label(B4Pump)
                .Says("b4_hester", "b4_rule_house", "HOUSE RULES FOR EVERYONE: A LOCK ON EVERY DOOR, NO WEAPONS INSIDE, EVERYBODY PULLS A SHIFT.", "RULES FOR EVERYONE. I CAN LIVE WITH THAT. I'M KEEPING MY KEY.").Q7Label(B4House)
                .Says("b4_hester", "b4_rule_town", "JUDD SLEEPS IN TOWN. HE WORKS HERE BY DAY.", "THAT I CAN LIVE WITH. HE CAN EAT HERE. HE DOESN'T SLEEP HERE.").Q7Label(B4Town);
            Step(q, "service", "ONE STAFFED SERVICE TO START: REPAIR LABOUR, TENDING CROPS OR MEDICAL RESUPPLY", "b4_rooms")
                .Says("b4_hester", "b4_repair", "REPAIR LABOUR: KEEP THE CARS IN THE YARD RUNNING.", "JUDD'LL LIKE THAT. HE'LL NEED SCRAP IN A CHEST, AND HE'S NOT A MIRACLE.").Q7Label(B4Repair)
                .Says("b4_hester", "b4_crops", "TENDING CROPS: WATER, WEED, BRING IN THE HARVEST.", "BEDS I CAN DO. WHAT'S RIPE GOES IN THE PANTRY; WHAT'S DEAD I'LL TELL YOU ABOUT.").Q7Label(B4Crops)
                .Says("b4_hester", "b4_medical", "MEDICAL RESUPPLY: KEEP A CABINET STOCKED WITH BANDAGES.", "CLOTH IN A CHEST AND I'LL ROLL BANDAGES. A MEDICINE CABINET IF YOU WANT THEM TIDY.").Q7Label(B4Medical);
            Step(q, "pantry", "THEY EAT TOO: STOCK A CHEST OR FRIDGE AT THE GARAGE WITH AT LEAST 4 PORTIONS OF FOOD", "garage").When(Goal.Event, "b4:pantry");
            q.reward.scrap = 10; q.reward.training.Add((Skill.Speech, 5f)); q.reward.flag = "b4_done";
            q.payoff = B4_Payoff(null, null, null);
        }
    }

    public static partial class StoryCast
    {
        static bool B4Running => Story.StateOf("B4") == Story.State.Active;
        static bool B4Lodged => Story.Route("B4", "rooms") == StoryLibrary.B4Lodging;
        static bool B4Night => DayNight.Hours < 7f || DayNight.Hours >= 21f;
        static string B4Place(string key) => Residents.Living && Residents.Working && Residents.Worker(Residents.Current) == key ? "b4_work" : "b4_rooms";

        static partial void Cast_B4(List<Member> into)
        {
            into.Add(new Member { key = "b4_hester", name = "HESTER QUAYLE", title = "COOK, LOOKING FOR A ROOM", anchor = "b4_rooms", temper = Temper.Proud, female = true,
                outfit = new[] { "sweater", "pants", "boots", "scarf" },
                present = () => B4Running || (Residents.Living && !(B4Lodged && B4Night)), anchorNow = () => B4Place("b4_hester") });
            into.Add(new Member { key = "b4_judd", name = "JUDD KESSLER", title = "USED TO RIDE WITH A GANG", anchor = "b4_rooms", temper = Temper.Gruff,
                outfit = new[] { "jacket", "jeans", "boots", "bandana" },
                present = () => B4Running || (Residents.Living && !((B4Lodged || Story.Route("B4", "rules") == StoryLibrary.B4Town) && B4Night)), anchorNow = () => B4Place("b4_judd") });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>B4: the residents' side door at the garage (outside its right-hand doorway) and the work bay inside.</summary>
        static partial void Anchors_B4(WorldGen world, Settlement town)
        {
            Q7From(world, "b4_rooms", "garage", 6.8f, 0.5f, -90f);
            Q7From(world, "b4_work", "garage", -1.4f, 0.4f, 0f);
        }
    }
}
