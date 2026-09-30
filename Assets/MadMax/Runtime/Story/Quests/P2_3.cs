using MadMax.Items;
using MadMax.RPG;

namespace MadMax.Story
{
    // P2.3 OPENING DAY: Barnaby's goats ate Oda's cabbages; she wants twenty scrap, he says six heads. Settle it with the
    // gate tally (the stock records), a manure-for-cabbages deal, or by covering half; optionally pen the goats. Then
    // they shake on it.
    public static partial class StoryLibrary
    {
        static partial void Author_P2_3(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_p2_tally", "GATE TALLY SHEET");
            q.offerSay = "READY TO OPEN?";
            q.offerReply = "AS READY AS WE'LL EVER BE. COME TO THE MARKET FIELD. BRING PATIENCE.";
            q.hook = "OPENING DAY AT THE HAMLETS' MARKET. SOMEONE WILL ARGUE; IT'S TRADITION.";
            Step(q, "arrive", "OPENING DAY AT THE MARKET FIELD", "p2_market").When(Goal.Reach, "p2_market", 14f);
            Step(q, "records", "OPTIONAL: READ THE GATE TALLY (THE STOCK RECORDS) BY THE STALLS", "p2_tally").When(Goal.Reach, "p2_tally", 2.6f).Optional()
                .Pays(r => r.items.Add(("story_p2_tally", 1)));
            Step(q, "dispute", "BARNABY'S GOATS ATE ODA'S CABBAGES. SHE WANTS 20 SCRAP; HE SAYS SIX HEADS. SETTLE IT", "p2_market")
                .Says("hamlets", "p2_3_records", "THE GATE TALLY: TWELVE CABBAGES IN, SIX SOLD. SIX WERE EATEN, NOT TWENTY.",
                    "...THE TALLY'S IN MY OWN HAND. FINE. SIX HEADS, TWO SCRAP EACH. BARNABY, PAY UP.").Needs("story_p2_tally")
                .Says("barnaby", "p2_3_manure", "PAY HER IN GOAT MANURE: A SACK A WEEK FOR HER BEDS TILL HARVEST.",
                    "MANURE FOR CABBAGES. THE GOATS WILL THINK IT WAS THEIR IDEA. ODA? ...SHE'S NODDING. THAT'S A YES.")
                .Says("barnaby", "p2_3_cover", "I'LL COVER HALF. TEN SCRAP, AND YOU SHAKE HANDS.", "HALF'S FAIR. DON'T TELL HER I SAID FAIR.");
            q.steps[q.steps.Count - 1].any[2].price = 10;
            Step(q, "pen", "OPTIONAL: PEN THE GOATS: A FENCE OR A TROUGH BY THE STALLS", "p2_market").When(Goal.Build, "fence_wood|fence_wire|trough", 18f).Optional()
                .Pays(r => r.training.Add((Skill.Farming, 4f)));
            Step(q, "shake", "SEE THEM SHAKE ON IT", "p2_market")
                .Says("hamlets", "p2_3_shake", "SHAKE ON IT, BOTH OF YOU.", "...THERE. BARNABY'S HANDS SMELL OF GOAT. DON'T MAKE A SPEECH ABOUT IT.")
                .Pays(r => r.take.Add(("story_p2_tally", 1)));
            q.reward.scrap = 15; q.reward.training.Add((Skill.Speech, 6f)); q.reward.training.Add((Skill.Farming, 3f));
            q.payoff = "OPENING DAY ENDED WITH A HANDSHAKE AND ONLY ONE GOAT IN THE CABBAGES.";
        }
    }
}
