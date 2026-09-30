using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;

namespace MadMax.Story
{
    // P1.2 THE WRONG ORIGINAL: Tom's straight six sits on Otis Vane's shelf in town. Buy it back (80 scrap), trade him a
    // car trophy (the dead coupe in the ditch still has its hood ornament), or ask Nell about a practical substitute.
    // Authenticity is a preference: each route ends with an engine in the coupe and is remembered.
    public static partial class StoryLibrary
    {
        public const string P1Original = "engine_i6", P1Substitute = "engine_i4";

        static partial void Author_P1_2(QuestDef q)
        {
            q.build = Build.Playable;
            q.offerSay = "ABOUT THAT ENGINE...";
            q.offerReply = "TOM'S STRAIGHT SIX. WE SOLD IT TO OTIS VANE IN TOWN THE WINTER WE NEEDED MEDICINE. HE COLLECTS OLD IRON AND NEVER RUNS ANY OF IT. " +
                           "SEE WHAT HE WANTS FOR IT. OR ASK ME ABOUT SOMETHING ELSE THAT FITS.";
            q.hook = "OTIS VANE, A COLLECTOR AT THE EDGE OF TOWN, OWNS THE ENGINE THAT CAME WITH NELL'S COUPE.";
            Step(q, "otis", "ASK OTIS VANE ABOUT TOM'S ENGINE", "p1_collector")
                .Says("otis", "p1_2_ask", "YOU HAVE NELL MERCER'S STRAIGHT SIX.",
                    "I HAVE A STRAIGHT SIX. STAMPED BLOCK, NUMBERS MATCHING, THE LAST ONE BETWEEN HERE AND THE COAST. 80 SCRAP. " +
                    "OR TRADE ME SOMETHING I DON'T HAVE: A LICENCE PLATE, A CHROME HUBCAP, A HOOD ORNAMENT. THE DEAD COUPE IN THE DITCH OUT OF TOWN STILL HAS ITS ORNAMENT, IF NOBODY'S BEEN AT IT.")
                .Optional();
            Step(q, "deal", "AN ENGINE FOR THE COUPE: BUY TOM'S FROM OTIS (80 SCRAP), TRADE HIM A CAR TROPHY, OR ASK NELL ABOUT A SUBSTITUTE", "p1_collector")
                .Says("otis", "p1_2_pay", "80 SCRAP. DELIVER IT TO NELL'S STOP.", "DONE. MY BOY WILL WHEEL IT OVER ON THE HAND CART. IF SHE ASKS, I DRIVE A HARD BARGAIN.")
                .Says("otis", "p1_2_ornament", "A HOOD ORNAMENT FOR THE STRAIGHT SIX?", "...A SILVER LADY, WINGS AND ALL. DONE. THE ENGINE GOES TO NELL'S ON THE CART.").Needs("trophy_ornament")
                .Says("otis", "p1_2_hubcap", "A CHROME HUBCAP FOR THE STRAIGHT SIX?", "NOT A DENT IN IT. DONE. THE ENGINE GOES TO NELL'S ON THE CART.").Needs("trophy_hubcap")
                .Says("otis", "p1_2_plate", "AN OLD LICENCE PLATE FOR THE STRAIGHT SIX?", "PRE-WAR NUMBERS. YOU KNOW WHAT YOU'RE DOING. DONE: IT GOES TO NELL'S ON THE CART.").Needs("trophy_plate")
                .Says("nell", "p1_2_sub", "WHAT IF SHE GETS A DIFFERENT ENGINE? SOMETHING THAT RUNS.",
                    "...TOM WOULD HAVE HATED IT. TOM ALSO PUT DIESEL IN A PETROL CAR TWICE. SHE'S A CAR, NOT A SHRINE. FIT WHAT RUNS AND LOOK AFTER IT.");
            q.steps[q.steps.Count - 1].any[0].price = 80;
            Step(q, "engine", "MOUNT THE ENGINE IN NELL'S COUPE (HOOD OFF; CARRY IT OVER, [E] WITH THE WRENCH)", "p1_car").When(Goal.Event, "p1_2:engine")
                .Pays(r => r.training.Add((Skill.Mechanics, 5f)));
            Step(q, "show", "SHOW NELL", "nell").Says("nell", "p1_2_show", "SHE HAS AN ENGINE AGAIN.",
                "SO SHE HAS. LISTEN TO THAT. ...SUNDAY, THEN. IF YOU'LL COME.");
            q.reward.scrap = 10; q.reward.training.Add((Skill.Speech, 3f)); q.reward.flag = "p1_engine";
            q.payoff = "NELL'S COUPE HAS AN ENGINE AGAIN.";
        }

        /// <summary>P1.2's deal: bought or traded for Tom's own engine (not the substitute).</summary>
        public static bool P1Bought(string route) => route != null && !route.StartsWith("WHAT IF");

        /// <summary>The car trophy a P1.2 trade route hands over (null for money or the substitute).</summary>
        public static string P1Trophy(string route) =>
            route == null ? null : route.StartsWith("A HOOD") ? "trophy_ornament" : route.StartsWith("A CHROME") ? "trophy_hubcap" : route.StartsWith("AN OLD") ? "trophy_plate" : null;
    }

    public static partial class StoryCast
    {
        static partial void Cast_P1_2(List<Member> into)
        {
            into.Add(new Member { key = "otis", name = "OTIS VANE", title = "COLLECTOR", anchor = "p1_collector", temper = Temper.Greedy, outfit = new[] { "coat", "pants", "boots", "cowboy" } });
        }
    }
}
