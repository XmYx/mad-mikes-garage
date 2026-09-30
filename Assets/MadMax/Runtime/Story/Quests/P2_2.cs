using MadMax.Items;
using MadMax.RPG;

namespace MadMax.Story
{
    // P2.2 A THIRD FIELD: choose the field between the hamlets or a rotation (Oda's field first), tell Barnaby, then
    // build two market stalls, a latrine and a water point on the market field, the water well clear of the latrine
    // (WaterQuality: a used pit fouls a well within 12 m). The hamlets chip in timber and canvas.
    public static partial class StoryLibrary
    {
        static partial void Author_P2_2(QuestDef q)
        {
            q.build = Build.Playable;
            q.offerSay = "SO WHERE DOES IT GO?";
            q.offerReply = "YOU TELL US. THE FIELD BETWEEN, OR WE TAKE TURNS. BARNABY WILL SULK EITHER WAY, BUT HE SULKS PRODUCTIVELY.";
            q.hook = "THE MARKET NEEDS A SITE BOTH HAMLETS CAN LIVE WITH, THEN STALLS, A LATRINE AND WATER.";
            Step(q, "choose", "DECIDE WITH ODA: THE FIELD BETWEEN, OR TAKE TURNS", "p2_oda")
                .Says("hamlets", "p2_2_mid", "THE FIELD BETWEEN. NEITHER OF YOU OWNS IT.", "NEUTRAL GROUND. BARNABY WILL HATE THAT HE LIKES IT. WE'LL SEND TIMBER AND CANVAS.")
                .Says("hamlets", "p2_2_turns", "TAKE TURNS: YOUR FIELD THIS MONTH, HIS THE NEXT.", "TURNS. LIKE CHILDREN. ...IT'S FAIR. WE START IN MY FIELD. WE'LL SEND TIMBER AND CANVAS.")
                .Pays(r => { r.resources.Add((ResourceType.Wood, 26)); r.resources.Add((ResourceType.Cloth, 8)); r.resources.Add((ResourceType.Scrap, 8)); });
            Step(q, "barnaby", "TELL BARNABY WHAT WAS DECIDED", "p2_barnaby")
                .Says("barnaby", "p2_2_tell", "IT'S DECIDED. HERE'S WHERE THE MARKET GOES.", "DECIDED, IS IT. WELL. I'LL BRING THE GOATS AND AN OPINION.");
            Step(q, "stalls", "BUILD TWO MARKET STALLS ON THE MARKET FIELD ([B], FURNITURE)", "p2_market").When(Goal.Event, "p2_2:stalls")
                .Pays(r => r.training.Add((Skill.Construction, 4f)));
            Step(q, "latrine", "SANITATION: BUILD A LATRINE BY THE MARKET FIELD", "p2_market").When(Goal.Build, "latrine", 22f);
            Step(q, "water", "WATER FOR THE MARKET: A WELL, RAIN COLLECTOR OR WATER TANK, 12 M OR MORE FROM THE LATRINE (A USED PIT FOULS A WELL)", "p2_market").When(Goal.Event, "p2_2:water");
            q.reward.scrap = 25; q.reward.training.Add((Skill.Construction, 6f));
            q.payoff = "THE MARKET FIELD HAS STALLS, A LATRINE AND WATER.";
        }
    }
}
