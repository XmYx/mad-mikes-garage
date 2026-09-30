using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;

namespace MadMax.Story
{
    // P3.3 WHO OWNS A WRECK: Iris Marrow, granddaughter of the wrecks' owner, wants what was his. Pay her a share, give
    // back the personal things from the wreck (the photograph or the letters), or say nothing came up but tins: the
    // dishonest route is disclosed (the harbour talks: settlers think less of you and there is no mooring agreement).
    public static partial class StoryLibrary
    {
        static partial void Author_P3_3(QuestDef q)
        {
            q.build = Build.Playable;
            q.offerSay = "WHO'S THE WOMAN WATCHING THE BOAT?";
            q.offerReply = "IRIS MARROW. HER GRANDFATHER OWNED THE ALBA AND THE MERIDIAN BOTH. SHE SAYS WHAT CAME UP IS HERS. I SAY THE SEA DOESN'T KEEP RECEIPTS. " +
                           "YOU WENT DOWN THERE: YOU SETTLE IT.";
            q.hook = "IRIS MARROW SAYS THE SALVAGE FROM HER GRANDFATHER'S WRECKS IS HERS. HALVARD SAYS THE SEA DOESN'T KEEP RECEIPTS.";
            Step(q, "iris", "HEAR IRIS MARROW OUT", "p3_heir")
                .Says("p3_heir", "p3_3_hear", "YOUR FAMILY OWNED THE WRECKS?",
                    "MY GRANDFATHER'S BOATS. THE INSURANCE NEVER PAID AND THE SALVAGE NEVER CAME. I DON'T WANT THE TINS. I WANT WHAT WAS HIS.");
            Step(q, "settle", "SETTLE THE SALVAGE: PAY HER A SHARE, RETURN HIS THINGS, OR KEEP IT ALL AND SAY NOTHING (THE HARBOUR WILL TALK)", "p3_heir")
                .Says("p3_heir", "p3_3_share", "A THIRD OF WHAT IT'S WORTH IS YOURS.", "A THIRD. IT'S MORE THAN THE INSURANCE EVER GAVE US. THANK YOU.")
                .Says("p3_heir", "p3_3_letters", "THESE WERE IN THE MERIDIAN'S CHART LOCKER. THEY'RE HIS.",
                    "...HIS LETTERS. TO MY GRANDMOTHER. HE WROTE HER FROM EVERY PORT. KEEP THE BRASS; I HAVE WHAT I CAME FOR.").Needs("story_p3_letters")
                .Says("p3_heir", "p3_3_photo", "THIS WAS IN THE ALBA'S CRATE. IT'S HIS.",
                    "...THAT'S HIM, ON THE ALBA'S DECK. HE LOOKS SO YOUNG. KEEP THE TINS; I HAVE WHAT I CAME FOR.").Needs("story_p3_photo")
                .Says("p3_heir", "p3_3_lie", "NOTHING CAME UP BUT TINS. [DISHONEST: THE HARBOUR WILL TALK]",
                    "...IF YOU SAY SO. THE HARBOUR IS SMALL, THOUGH. PEOPLE TALK, AND I LISTEN.");
            q.steps[q.steps.Count - 1].any[0].price = 20;
            Step(q, "halvard", "TELL HALVARD IT'S SETTLED", "p3_harbour")
                .Says("trawler", "p3_3_done", "IT'S SETTLED.", "SETTLED. THE HARBOUR WILL HAVE ITS OWN VERSION BY TONIGHT. IT USUALLY DOES.");
            q.reward.scrap = 20; q.reward.items.Add(("use_o2_bottle", 1)); q.reward.training.Add((Skill.Speech, 4f)); q.reward.training.Add((Skill.Salvaging, 4f));
            q.payoff = "THE SALVAGE IS SETTLED.";
        }

        /// <summary>P3.3 went the honest way (a share or his things returned).</summary>
        public static bool P3Honest(string route) => route != null && !route.StartsWith("NOTHING CAME UP");
    }

    public static partial class StoryCast
    {
        static partial void Cast_P3_3(List<Member> into)
        {
            into.Add(new Member { key = "p3_heir", name = "IRIS MARROW", title = "THE OWNER'S GRANDDAUGHTER", anchor = "p3_heir", temper = Temper.Proud, female = true,
                outfit = new[] { "coat", "jeans", "boots", "scarf" } });
        }
    }
}
