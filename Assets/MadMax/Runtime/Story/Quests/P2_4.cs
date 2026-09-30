using MadMax.RPG;

namespace MadMax.Story
{
    // P2.4 ONE SUPPLY RUN: take the hamlets' purse to town for lamp fuel and tinned food, bring it to the market, come
    // back for the opening evening (after 18:00; sleep or wait), then settle who keeps it going: the hamlets in turn,
    // a stall rent, or one night only. The market recurs (every third day) only if upkeep is agreed.
    public static partial class StoryLibrary
    {
        public const string P2Food = "food_can|food_can_stew|food_can_meat|food_can_fish|food_can_veg|food_can_fruit|food_ration|food_jam|food_bread|food_cheese";

        static partial void Author_P2_4(QuestDef q)
        {
            q.build = Build.Playable;
            q.offerSay = "WHAT DOES THE MARKET STILL NEED?";
            q.offerReply = "LAMP FUEL FOR THE OPENING EVENING AND SOMETHING TO FEED A CROWD. ONE SUPPLY RUN TO TOWN. TOWN PRICES, NOT YOUR PRICES.";
            q.hook = "THE HAMLETS' MARKET NEEDS ONE SUPPLY RUN BEFORE ITS OPENING EVENING.";
            Step(q, "purse", "TAKE THE HAMLETS' PURSE FROM ODA", "p2_market")
                .Says("hamlets", "p2_4_purse", "I'LL MAKE THE RUN.", "THIRTY SCRAP. TEN LITRES OF LAMP FUEL AND SIX TINS OR JARS. RECEIPTS OPTIONAL, HONESTY NOT.")
                .Pays(r => r.scrap = 30);
            Step(q, "fuel", "SUPPLY RUN: 10 L OF PETROL FOR THE LAMPS (A TOWN TRADER OR YOUR OWN)", "town1").When(Goal.Have, "res:" + (int)MadMax.Items.ResourceType.Fuel, 10f);
            Step(q, "food", "SUPPLY RUN: SIX TINS, JARS, LOAVES OR RATIONS", "town1").When(Goal.Have, P2Food, 6f);
            Step(q, "deliver", "BRING THE SUPPLIES TO THE MARKET FIELD", "p2_market")
                .Says("hamlets", "p2_4_deliver", "THE SUPPLIES ARE HERE: LAMP FUEL AND FOOD.", "GOOD. LAMPS ON THE POSTS, TINS ON THE TABLE. COME BACK THIS EVENING. WEAR SOMETHING CLEAN, IF YOU OWN IT.")
                .Pays(r => r.take.Add((P2Food, 6)));
            Step(q, "evening", "THE OPENING EVENING: BE AT THE MARKET FIELD AFTER 18:00 (SLEEP OR WAIT IF YOU LIKE)", "p2_market").When(Goal.Event, "p2_4:evening");
            Step(q, "upkeep", "WHO KEEPS IT GOING? SETTLE THE UPKEEP WITH ODA", "p2_market")
                .Says("hamlets", "p2_4_turns", "EACH HAMLET SWEEPS, WATERS AND MENDS IN TURN.", "THEN IT'S A MARKET. EVERY THIRD DAY, SAME FIELD, SAME ARGUMENTS.")
                .Says("hamlets", "p2_4_rent", "CHARGE A SCRAP A STALL AND PAY SOMEONE TO KEEP IT.", "RENT. BARNABY WILL TRY TO CHARGE HIS OWN GOATS. ...ALRIGHT: EVERY THIRD DAY, AND THE RENT KEEPS IT.")
                .Says("hamlets", "p2_4_once", "LET IT BE ONE NIGHT. PEOPLE WILL REMEMBER IT.", "...MAYBE THAT'S ENOUGH. ONE GOOD NIGHT IS MORE THAN MOST PLACES GET.");
            Step(q, "night", "SAY GOODNIGHT TO BARNABY", "p2_market")
                .Says("barnaby", "p2_4_night", "GOODNIGHT, BARNABY.", "GOODNIGHT. BRING A GOAT NEXT TIME. EVERYONE SHOULD BRING A GOAT.");
            q.reward.scrap = 20; q.reward.training.Add((Skill.Speech, 4f)); q.reward.training.Add((Skill.Construction, 3f)); q.reward.training.Add((Skill.Farming, 3f));
            q.payoff = "THE MARKET BETWEEN THE HAMLETS OPENED WITH LANTERNS AND GOATS.";
        }

        /// <summary>P2.4's upkeep: the market recurs (turns or rent), or it was one night.</summary>
        public static bool P2Recurs(string route) => route != null && !route.StartsWith("LET IT BE");
    }
}
