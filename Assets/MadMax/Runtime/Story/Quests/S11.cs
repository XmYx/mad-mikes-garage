using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S11 LETTERS NOBODY STOLE (storyline §10): three twenty-year-old letters down one road out of the first town. One
    // recipient moved (a note on his old door), one refuses hers (burn it unread or take it back), one address is an
    // orchard now (its keeper closes it without anyone opening the post). Reva gets told how each one ended.
    public static partial class StoryLibrary
    {
        public const string S11LetterHal = "story_letter_hal", S11LetterIda = "story_letter_ida", S11LetterPell = "story_letter_pell";

        static partial void Author_S11(QuestDef q)
        {
            ItemIds.Register(S11LetterHal, "LETTER: H. BRIGGS, LINESMAN'S COTTAGE");
            ItemIds.Register(S11LetterIda, "LETTER: I. MARSH, BEE HOLLOW");
            ItemIds.Register(S11LetterPell, "LETTER: THE PELL FAMILY, END HOUSE");
            foreach (var k in new[] { "reva", "s11_old", "s11_ida", "s11_hal", "s11_orchard" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "WHAT'S IN THE BAG?";
            q.offerReply = "POST. WHAT'S LEFT OF THE LAST ROUND BEFORE THE ROADS WENT: THREE LETTERS, TWENTY YEARS OLD. I KEPT THEM DRY AND I NEVER " +
                           "OPENED ONE. ALL THREE GO DOWN THE SAME ROAD OUT OF TOWN. MY KNEES DON'T. WOULD YOU?";
            q.hook = "REVA DUNN KEPT THREE UNDELIVERED LETTERS FOR TWENTY YEARS. ALL THREE ADDRESSES ARE DOWN ONE ROAD OUT OF TOWN.";

            Step(q, "bag", "TAKE THE THREE LETTERS FROM REVA'S BAG", "reva").When(Goal.Reach, "reva", 7f)
                .Pays(r => { r.items.Add((S11LetterHal, 1)); r.items.Add((S11LetterIda, 1)); r.items.Add((S11LetterPell, 1)); });
            Step(q, "old", "FIRST ADDRESS: H. BRIGGS, THE LINESMAN'S COTTAGE, DOWN THE ROAD", "s11_old").When(Goal.Reach, "s11_old", 6f);
            Step(q, "ida", "THE COTTAGE IS EMPTY. A NOTE ON THE DOOR: 'GONE TO MY SISTER'S, FURTHER ON. H.B.' ON THE WAY: I. MARSH AT BEE HOLLOW", "s11_ida")
                .Says("s11_ida", "s11_ida_offer", "A LETTER FOR YOU, MRS MARSH. IT'S TWENTY YEARS LATE.",
                      "...I KNOW THAT HAND. NO. I DON'T WANT IT. TWENTY YEARS I DIDN'T HAVE IT, AND I MANAGED.").Needs(S11LetterIda);
            Step(q, "ida_choice", "IDA MARSH WON'T TAKE HER LETTER. RESPECT THAT", "s11_ida")
                .Says("s11_ida", "s11_ida_return", "THEN IT GOES BACK TO REVA. UNOPENED.",
                      "GOOD. TELL HER THANK YOU FOR KEEPING IT DRY, AND FOR NOT READING IT. SHE WOULDN'T HAVE. SHE NEVER DID.")
                .Says("s11_ida", "s11_ida_burn", "WANT ME TO BURN IT? YOU CAN WATCH.",
                      "...YES. IN THE STOVE. DON'T LOOK AT IT. ...THERE. THAT'S THAT. YOU CAN GO NOW. THANK YOU.")
                .Says("s11_ida", "s11_ida_ask", "WHAT IF IT'S AN APOLOGY?",
                      "THEN IT'S TWENTY YEARS LATE AND I MADE MY PEACE WITHOUT IT. TAKE IT BACK TO REVA. PLEASE.")
                .Pays(r => r.training.Add((Skill.Speech, 3f)));
            Step(q, "hal", "HAL BRIGGS MOVED TO HIS SISTER'S PLACE FURTHER DOWN THE ROAD: DELIVER HIS LETTER THERE", "s11_hal")
                .Says("s11_hal", "s11_hal_give", "HAL BRIGGS? A LETTER FOR YOU. IT'S TWENTY YEARS LATE.",
                      "FROM DELL. MY BROTHER. HE DIED SIX WINTERS BACK. ...HE SAYS HE'S SORRY ABOUT THE TRUCK. I SOLD HIM THAT TRUCK! HA. " +
                      "TWENTY YEARS AND HE STILL GETS THE LAST WORD. THANK YOU. SIT A MINUTE IF YOU LIKE; MY SISTER MAKES TERRIBLE TEA.").Needs(S11LetterHal)
                .Pays(r => { r.take.Add((S11LetterHal, 1)); r.training.Add((Skill.Speech, 3f)); });
            Step(q, "pell", "LAST ONE: THE PELL FAMILY, THE END HOUSE. THE ADDRESS IS AN ORCHARD NOW; ASK WHOEVER KEEPS IT", "s11_orchard")
                .Says("s11_noor", "s11_pell_ask", "I'VE A LETTER FOR THE PELLS. THIS WAS THEIR HOUSE?",
                      "THE PELLS LEFT THE WINTER THEIR WELL WENT SALT. THEIR GIRL PLANTED THE FIRST APPLE BEFORE THEY WENT; I JUST KEPT PLANTING. " +
                      "NOBODY'S HEARD FROM THEM SINCE. I WON'T OPEN THEIR POST, AND I'D RATHER YOU DIDN'T EITHER.").Needs(S11LetterPell);
            Step(q, "pell_choice", "NOBODY KNOWS WHERE THE PELLS WENT. DECIDE WITH NOOR WHERE THEIR LETTER RESTS", "s11_orchard")
                .Says("s11_noor", "s11_pell_shed", "COULD IT HANG IN YOUR SHED, SEALED, IN CASE THEY COME BACK?",
                      "WITH THE PICKING LADDERS. IF A PELL EVER WALKS UP THIS ROAD, IT'S THE FIRST THING THEY GET. AFTER AN APPLE.")
                .Says("s11_noor", "s11_pell_tree", "BURY IT UNOPENED UNDER THE GIRL'S TREE?",
                      "UNDER THE FIRST TREE. ...YES. I THINK THAT'S WHERE IT WAS GOING ALL ALONG.")
                .Says("s11_noor", "s11_pell_reva", "I'LL TAKE IT BACK TO REVA. SHE'LL KNOW.",
                      "SHE WILL. SHE ALWAYS DID. TAKE SOME APPLES FOR THE ROAD; THE PELLS WOULD'VE.")
                .Pays(r => r.items.Add(("food_apple", 3)));
            Step(q, "home", "THE ROUND IS DONE. TELL REVA HOW EACH LETTER ENDED", "reva")
                .Says("reva", "s11_home", "THE LETTERS FOUND THEIR ENDS. NOT ALL THE WAY YOU'D WRITE IT.",
                      "THAT'S POST. YOU TAKE IT AS FAR AS IT GOES AND NOT A STEP FURTHER. HERE: MY OLD SHOULDER BAG. IT'S CARRIED WORSE NEWS THAN YOURS.")
                .Pays(r => r.take.Add((S11LetterHal + "|" + S11LetterIda + "|" + S11LetterPell, 3)));
            q.reward.scrap = 18; q.reward.items.Add(("cloth_schoolbag", 1)); q.reward.training.Add((Skill.Speech, 6f)); q.reward.flag = "s11_done";
            q.payoff = "REVA'S POSTBAG IS EMPTY FOR THE FIRST TIME IN TWENTY YEARS.";
        }

        /// <summary>S11's closing journal line from the routes taken (set by the game while the quest runs).</summary>
        public static string S11Payoff()
        {
            string ida = Story.Route("S11", "ida_choice"), pell = Story.Route("S11", "pell_choice");
            string a = ida != null && ida.StartsWith("WANT ME TO BURN") ? "IDA MARSH WATCHED HERS BURN UNREAD" : "IDA MARSH'S WENT BACK TO REVA UNOPENED";
            string b = pell == null ? "THE PELLS' LETTER WAITS" : pell.StartsWith("COULD IT HANG") ? "THE PELLS' LETTER HANGS SEALED IN NOOR'S SHED"
                     : pell.StartsWith("BURY") ? "THE PELLS' LETTER LIES UNDER THE FIRST APPLE TREE" : "THE PELLS' LETTER WENT BACK INTO REVA'S BAG";
            return "HAL BRIGGS GOT HIS BROTHER'S APOLOGY; " + a + "; " + b + ". NOBODY OPENED ANYONE ELSE'S POST.";
        }
    }

    public static partial class StoryCast
    {
        static bool S11Around() => Story.StateOf("S11") != Story.State.Locked;

        static partial void Cast_S11(List<Member> into)
        {
            into.Add(new Member { key = "reva", name = "REVA DUNN", title = "POSTAL CARETAKER", anchor = "reva", temper = Temper.Proud, female = true,
                                  outfit = new[] { "sweater", "pants", "boots", "scarf" } });
            into.Add(new Member { key = "s11_hal", name = "HAL BRIGGS", title = "RETIRED LINESMAN", anchor = "s11_hal", temper = Temper.Joker,
                                  outfit = new[] { "coat", "jeans", "boots", "beanie" }, present = S11Around });
            into.Add(new Member { key = "s11_ida", name = "IDA MARSH", title = "BEEKEEPER", anchor = "s11_ida", temper = Temper.Gruff, female = true,
                                  outfit = new[] { "sweater", "overalls", "boots", "sunhat" }, present = S11Around });
            into.Add(new Member { key = "s11_noor", name = "NOOR ASHBY", title = "ORCHARD KEEPER", anchor = "s11_orchard", temper = Temper.Friendly, female = true,
                                  outfit = new[] { "tshirt", "overalls", "boots", "sunhat", "gloves" }, tool = "tool_hoe", present = S11Around });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S11: Reva's post at the town's edge, then down the longest road out: Hal's old cottage, Bee Hollow,
        /// Hal's sister's place and the orchard where the Pells' house stood.</summary>
        static partial void Anchors_S11(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            var roads = Q2.RoadsOut(world, town);
            var road = roads.Count > 0 ? roads[0] : null;
            float edge = road != null ? Q2.EdgeAlong(road, tc, town.radius) : 0f;
            var plan = new (string key, float along, float half, float clear)[] { ("reva", 30f, 4f, 10f), ("s11_old", 170f, 4f, 9f), ("s11_ida", 340f, 5f, 11f), ("s11_hal", 520f, 5f, 11f), ("s11_orchard", 720f, 8f, 16f) };
            for (int i = 0; i < plan.Length; i++)
            {
                var (key, along, half, clear) = plan[i];
                Vector3 spot; float face;
                if (!(road != null && Q2.Beside(world, road, edge + along, 40f, half, 1.3f, out spot, out face))
                    && !Q2.Near(world, tc, town.radius + along * 0.7f, town.radius + along + 360f, 40f, half, 1.6f, out spot, out face))
                    spot = Q2.Anywhere(world, tc, town.radius + along, 25f + i * 9f, out face);
                Q2.Put(world, key, spot, face);
                Clearing(key, clear);
            }
        }
    }
}
