using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S23 NO TEETH, STILL TROUBLE: Nia Cole keeps the market at the edge of town, and Brick Halloran, a prizefighter
    // with no teeth and a permanent grin, keeps "lending" to her stallholders. Hear two of them out, then settle it one
    // of three ways: a supervised bout in Nia's ring under explicit nonlethal rules (padded blows, three knockdowns, step
    // out to yield, the referee stops it if anyone is hurt; a loss is just a loss, and a rematch is always on), his own
    // debt book (it sits in his crate) read back to him, or a deal where you cover half the restitution. Melee or Speech
    // training, protective gloves and market credit. Beaten fair, Brick offers a rematch, not his loyalty.
    public static partial class StoryLibrary
    {
        static partial void Author_S23(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_s23_ledger", "BRICK'S DEBT BOOK");
            StoryAnchors.Q5Declare("nia", "s23_ring", "s23_stall1", "s23_stall2", "s23_stall3");
            q.offerSay = "YOUR MARKET LOOKS NERVOUS.";
            q.offerReply = "IT IS. BRICK HALLORAN: OLD PRIZEFIGHTER, NO TEETH, SMILES AT EVERYONE. HE LENDS MY STALLHOLDERS MONEY THEY DIDN'T ASK FOR AND COLLECTS MORE THAN THEY OWE. " +
                           "HE NEVER HITS ANYONE. HE JUST STANDS VERY CLOSE. I'M THE STEWARD; I CAN'T TAKE SIDES WITHOUT PROOF. YOU CAN.";
            q.hook = "NIA COLE'S STALLHOLDERS ARE BEING SQUEEZED BY BRICK HALLORAN, A TOOTHLESS PRIZEFIGHTER WITH A BOOK OF DEBTS.";

            Step(q, "pru", "PRU AT THE VEGETABLE STALL", "s23_stall1")
                .Says("s23_pru", "s23_pru", "WHAT DOES BRICK WANT FROM YOU?", "HIS 'PROTECTION'. TEN SCRAP A WEEK, AND HIS WEEKS KEEP GETTING SHORTER. I PAID HIM TWICE ON MONDAY.").Optional();
            Step(q, "dev", "DEV THE TINKER", "s23_stall2")
                .Says("s23_dev", "s23_dev", "DOES BRICK LEND MONEY?", "LENT ME TWENTY WHEN MY CART BROKE. I'VE PAID HIM FORTY. HE SAYS I STILL OWE THIRTY. HE WRITES IT ALL IN A LITTLE BOOK HE KEEPS IN HIS CRATE BY THE RING.").Optional();
            Step(q, "sal", "SALLY THE BAKER", "s23_stall3")
                .Says("s23_sal", "s23_sal", "IS HE DANGEROUS?", "BRICK? HASN'T GOT A TOOTH IN HIS HEAD AND HE STILL WON'T STOP SMILING. HE DOESN'T HIT PEOPLE. HE JUST STANDS NEAR THEM UNTIL THEY PAY. THAT'S WORSE.").Optional();
            Step(q, "testimony", "HEAR OUT THE STALLHOLDERS: PRU, DEV AND SALLY (ANY TWO)", "nia").When(Goal.Steps, "pru,dev,sal", 2f)
                .Pays(r => r.training.Add((Skill.Speech, 2f)));
            Step(q, "plan", "TELL NIA WHAT THE STALLHOLDERS SAID", "nia")
                .Says("nia", "s23_plan", "THE STALLHOLDERS HAVE TOLD ME ABOUT BRICK.",
                    "THEN WE DO IT PROPERLY. ONE: MY RING, MY RULES. PADDED BLOWS ONLY: BRING SOMETHING BLUNT AND I'LL WRAP IT IN SACKING. NO BLADES, NO GUNS, NOBODY JOINS IN. " +
                    "THREE KNOCKDOWNS WINS, STEP OUT OF THE RING TO YIELD, AND I STOP IT IF ANYONE'S HURT. HE'S NEVER TURNED DOWN A FIGHT. TWO: THAT LITTLE BOOK OF HIS. THREE: TALK HIM ROUND. YOUR CHOICE.");
            Step(q, "challenge", "CHALLENGE BRICK TO A BOUT IN NIA'S RING", "s23_ring")
                .Says("s23_brick", "s23_challenge", "YOU AND ME IN NIA'S RING, HER RULES. IF I WIN, YOU PAY THEM ALL BACK.",
                    "HA! NO TEETH, NO FEAR. STEP INTO THE RING WHEN YOU'RE READY. STEP OUT IF YOU WANT YOUR MOTHER.").Optional();
            Step(q, "settle", "SETTLE IT: WIN A BOUT IN NIA'S RING (STEP IN TO START, OUT TO YIELD), READ HIS DEBT BOOK BACK TO HIM, OR MAKE HIM A DEAL", "s23_ring")
                .When(Goal.Event, "s23:bout:won", label: "WON THE BOUT")
                .Says("s23_brick", "s23_expose", "YOUR BOOK HAS DEV'S LOAN IN IT THREE TIMES. SO DOES PRU'S.",
                    "...THAT'S MY BOOK. THAT'S... EVERYONE'S LOOKING, AREN'T THEY. FINE. FINE! I'LL PAY IT BACK. ALL OF IT. STOP READING IT OUT.").Needs("story_s23_ledger")
                .Says("s23_brick", "s23_deal", "PAY THEM BACK HALF, AND I'LL COVER THE OTHER HALF MYSELF.",
                    "YOU'D PAY MY DEBTS? ...NOBODY'S EVER BOUGHT ME OUT BEFORE. ALL RIGHT. HALF. AND I STOP 'LENDING'. A MAN CAN'T SMILE AT PEOPLE WHO WON'T PAY HIM ANYWAY.");
            q.steps[q.steps.Count - 1].any[2].price = 30;
            Step(q, "r_bout", "BRICK'S SPARRING GLOVES", "s23_ring").When(Goal.Event, "s23:bout:won").Optional()
                .Pays(r => r.training.Add((Skill.Melee, 6f)));
            Step(q, "r_book", "THE MARKET HEARD THE BOOK READ OUT", "nia").When(Goal.Event, "talk:s23_brick:s23_expose").Optional()
                .Pays(r => { r.training.Add((Skill.Speech, 6f)); r.take.Add(("story_s23_ledger", 1)); });
            Step(q, "r_deal", "A DEAL STRUCK IN PUBLIC", "nia").When(Goal.Event, "talk:s23_brick:s23_deal").Optional()
                .Pays(r => r.training.Add((Skill.Speech, 6f)));
            Step(q, "tell", "TELL NIA IT'S SETTLED", "nia")
                .Says("nia", "s23_done", "IT'S SETTLED. THE STALLS GET THEIR MONEY BACK.",
                    "I SAW. THE WHOLE MARKET SAW. HERE: GLOVES, BECAUSE YOUR HANDS ARE A MESS, AND CREDIT AT EVERY STALL ON THIS ROW. DON'T SPEND IT ALL ON SALLY'S BUNS. SPEND MOST OF IT ON SALLY'S BUNS.");
            q.reward.scrap = 25; q.reward.items.Add(("cloth_gloves", 1)); q.reward.items.Add(("food_bread", 2));
            q.reward.flag = "market_credit";
            q.payoff = "BRICK HALLORAN STOPPED 'LENDING' AT NIA COLE'S MARKET.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S23(List<Member> into)
        {
            into.Add(new Member { key = "nia", name = "NIA COLE", title = "MARKET STEWARD", anchor = "nia", temper = Temper.Gruff, female = true, outfit = new[] { "jacket", "jeans", "boots", "bandana" } });
            into.Add(new Member { key = "s23_brick", name = "BRICK HALLORAN", title = "PRIZEFIGHTER, RETIRED", anchor = "s23_ring", temper = Temper.Proud, outfit = new[] { "tank", "pants", "boots", "fingerless" } });
            into.Add(new Member { key = "s23_pru", name = "PRU ADEYEMI", title = "VEGETABLE STALL", anchor = "s23_stall1", temper = Temper.Nervous, female = true, outfit = new[] { "overalls", "boots", "sunhat" } });
            into.Add(new Member { key = "s23_dev", name = "DEV MARLOWE", title = "TINKER", anchor = "s23_stall2", temper = Temper.Joker, outfit = new[] { "vest", "jeans", "boots", "goggles" } });
            into.Add(new Member { key = "s23_sal", name = "SALLY FENN", title = "BAKER", anchor = "s23_stall3", temper = Temper.Friendly, female = true, outfit = new[] { "tshirt", "pants", "boots", "bandana" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Nia's market on level ground at the edge of the first town: three stalls behind her, the ring in front.</summary>
        static partial void Anchors_S23(WorldGen world, Settlement town)
        {
            Q5Edge(world, town, "nia", 14f, 100f, 300f, 9f, 40f);
            Q5Beside(world, "s23_stall1", "nia", new Vector3(-6.5f, 0f, -3f));
            Q5Beside(world, "s23_stall2", "nia", new Vector3(0f, 0f, -5.5f));
            Q5Beside(world, "s23_stall3", "nia", new Vector3(6.5f, 0f, -3f));
            Q5Beside(world, "s23_ring", "nia", new Vector3(0f, 0f, 7f));
            Clearing("nia", 15f);
        }
    }
}
