using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S17 SOUP AGAINST THE WEATHER (storyline §10): Bea Holt's roadside kitchen and a coach stranded at the washout down
    // the road. Plan the meal from what the country grows (stew, fish soup, porridge, tins: all interchangeable), keep
    // her stove's woodbox full (or pay the charcoal burner), cook six portions on the cooking ladder (her wood stove
    // burns from the woodbox; any stove, range or cannery counts, and bought food too), carry them hot in her haybox.
    public static partial class StoryLibrary
    {
        public const string S17Haybox = "story_haybox", S17Card = "story_recipe_card";
        public const string S17Hot = "food_stew|food_soup|food_fish_soup|food_meat_stew|food_mushsoup|food_porridge";
        public const string S17Tins = "food_can_stew|food_can_veg|food_can_fish|food_can_meat|food_can";

        static partial void Author_S17(QuestDef q)
        {
            ItemIds.Register(S17Haybox, "BEA'S HAYBOX (SIX HOT PORTIONS)");
            ItemIds.Register(S17Card, "RECIPE CARD: BEA'S STORM SOUP");
            foreach (var k in new[] { "bea", "s17_camp", "s17_camp2" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "COOKING FOR AN ARMY?";
            q.offerReply = "FOR TWELVE TRAVELLERS STUCK AT THE WASHOUT, WHICH IS WORSE: AN ARMY BRINGS ITS OWN SPOONS. THE STORM TOOK THE ROAD AND THEIR " +
                           "COACH IS SITTING IN THE MUD. I'VE A STOVE AND NO PLAN. HELP ME MAKE ONE?";
            q.hook = "BEA HOLT'S ROADSIDE KITCHEN WANTS TO FEED A COACHLOAD STRANDED AT THE WASHOUT. SHE HAS A STOVE AND NO PLAN.";

            Step(q, "plan", "PLAN THE MEAL WITH BEA: WHAT DOES THIS COUNTRY ACTUALLY GROW?", "bea")
                .Says("bea", "s17_stew", "VEGETABLE STEW: POTATOES, CARROTS, CABBAGE. THE GARDENS HAVE THOSE.",
                      "STEW, THEN. IF THE GARDENS ARE THIN, ANY SOUP OR STEW STRETCHES. SIX PORTIONS: THE CHILDREN EAT LIKE HALF-ADULTS AND THE ADULTS LIKE CHILDREN.")
                .Says("bea", "s17_fish", "FISH SOUP. THERE'S WATER, SO THERE ARE FISH.",
                      "IF YOU CATCH IT, I'LL GUT IT. A POTATO AND SOME HERBS AND IT'S SUPPER. ANY SOUP OR STEW DOES; SIX PORTIONS.")
                .Says("bea", "s17_porridge", "PORRIDGE. WHEAT KEEPS AND IT'S HOT.",
                      "NOBODY EVER THANKED ANYONE FOR PORRIDGE, AND NOBODY EVER REFUSED IT IN THE RAIN. SIX PORTIONS; MIX IN WHATEVER SOUP YOU LIKE.")
                .Says("bea", "s17_tins", "TINS FROM A CANNERY. THEY TRAVEL AND NEVER SPOIL.",
                      "COLD COMFORT, BUT COMFORT. SIX TINS, OR SIX BOWLS, OR SOME OF EACH. I WON'T TELL.");
            Step(q, "sack", "OPTIONAL: BUY VEGETABLES FROM BEA'S SUPPLIER", "bea")
                .Says("bea", "s17_sack", "CAN YOUR SUPPLIER SELL ME A SACK OF VEGETABLES?",
                      "THREE EACH OF POTATOES, CARROTS AND CABBAGE. HE'LL MOAN ABOUT THE PRICE; IGNORE HIM, I DO.").Optional()
                .Pays(r => { r.items.Add(("food_potato", 3)); r.items.Add(("food_carrot", 3)); r.items.Add(("food_cabbage", 3)); });
            q.steps[q.steps.Count - 1].any[0].price = 15;
            Step(q, "fuel", "KEEP THE STOVE GOING: PUT 12 WOOD OR CHARCOAL IN BEA'S WOODBOX ([E] ON THE CRATE BY THE STOVE), OR PAY THE CHARCOAL BURNER", "bea")
                .When(Goal.Event, "s17:woodbox", label: "STOCKED THE WOODBOX")
                .Says("bea", "s17_burner", "PAY THE CHARCOAL BURNER TO DROP A LOAD.",
                      "HE'LL BE ROUND BEFORE THE KETTLE BOILS. DON'T MENTION HIS HAT.")
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            q.steps[q.steps.Count - 1].any[1].price = 15;
            Step(q, "cook", "COOK SIX PORTIONS: SOUP, STEW OR PORRIDGE (BEA'S STOVE BURNS FROM THE WOODBOX), OR TINS FROM A CANNERY", "bea")
                .When(Goal.Have, S17Hot, 6f, "HOT FROM THE POT")
                .When(Goal.Have, S17Tins, 6f, "TINNED")
                .When(Goal.Have, S17Hot + "|" + S17Tins, 6f, "SOME OF EACH")
                .Pays(r => r.training.Add((Skill.Crafting, 5f)));
            Step(q, "pot", "BRING THE PORTIONS TO BEA: SHE PACKS THEM IN A HAYBOX TO TRAVEL", "bea")
                .Says("bea", "s17_pack", "SIX PORTIONS, READY TO TRAVEL.",
                      "INTO THE HAYBOX: STRAW, A LID AND A PRAYER. IT STAYS HOT FOR HOURS IF YOU DON'T DRIVE LIKE A GOAT. THE WASHOUT'S DOWN THE ROAD.")
                .Pays(r => { r.take.Add((S17Hot + "|" + S17Tins, 6)); r.items.Add((S17Haybox, 1)); });
            Step(q, "gentle", "OPTIONAL: GET THE HAYBOX THERE WITHOUT A HARD KNOCK", "s17_camp").When(Goal.Event, "s17:gentle").Optional()
                .Pays(r => r.training.Add((Skill.Driving, 3f)));
            Step(q, "carry", "CARRY THE HAYBOX TO THE STRANDED COACH AT THE WASHOUT", "s17_camp")
                .Says("s17_otis", "s17_serve", "HOT FOOD FROM BEA'S KITCHEN, UP THE ROAD.",
                      "HOT? ...YOU HEAR THAT, EVERYONE? HOT. MAUD, GET THE CUPS. TELL WHOEVER BEA IS THAT TWELVE PEOPLE WILL REMEMBER HER KITCHEN.").Needs(S17Haybox)
                .Pays(r => { r.take.Add((S17Haybox, 1)); r.training.Add((Skill.Speech, 2f)); });
            Step(q, "back", "TELL BEA THEY ATE", "bea")
                .Says("bea", "s17_back", "THEY ATE EVERY DROP.",
                      "OF COURSE THEY DID. WHEN THE ROAD'S OPEN THEY'LL TELL THE NEXT LOT WHERE THE KITCHEN IS, AND THAT'S THE POINT: THE KITCHEN, " +
                      "NOT YOU OR ME. HERE, TAKE A CARD: STORM SOUP, THE WAY MY MOTHER DIDN'T MAKE IT.");
            q.reward.items.Add(("food_bread", 2)); q.reward.items.Add(("drink_tea", 2)); q.reward.items.Add((S17Card, 1));
            q.reward.training.Add((Skill.Crafting, 5f)); q.reward.training.Add((Skill.Survival, 4f)); q.reward.flag = "bea_kitchen";
            q.payoff = "BEA'S KITCHEN FED THE COACH AT THE WASHOUT. TRAVELLERS ON THAT ROAD NOW ASK FOR THE KITCHEN BY NAME.";
        }

        /// <summary>S17's closing journal line from the routes taken.</summary>
        public static string S17Payoff()
        {
            string plan = Story.Route("S17", "plan"), cook = Story.Route("S17", "cook");
            string dish = plan == null ? "SUPPER" : plan.StartsWith("VEGETABLE") ? "STEW" : plan.StartsWith("FISH") ? "FISH SOUP" : plan.StartsWith("PORRIDGE") ? "PORRIDGE" : "TINNED SUPPER";
            string form = cook == "TINNED" ? " IN TINS" : cook == "SOME OF EACH" ? ", HALF OF IT TINNED" : "";
            string trip = Story.StepDone("S17", "gentle") ? " STILL HOT" : " A LITTLE SLOSHED";
            return "SIX PORTIONS OF " + dish + form + " REACHED THE WASHOUT" + trip + ". TRAVELLERS ON THAT ROAD NOW ASK FOR BEA'S KITCHEN, NOT FOR YOU.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S17(List<Member> into)
        {
            into.Add(new Member { key = "bea", name = "BEA HOLT", title = "KITCHEN ORGANISER", anchor = "bea", temper = Temper.Friendly, female = true,
                                  outfit = new[] { "sweater", "pants", "boots", "bandana" } });
            into.Add(new Member { key = "s17_otis", name = "OTIS FERN", title = "COACH DRIVER", anchor = "s17_camp", temper = Temper.Gruff,
                                  outfit = new[] { "jacket", "jeans", "boots", "beanie" } });
            into.Add(new Member { key = "s17_maud", name = "MAUD PRICE", title = "PASSENGER", anchor = "s17_camp2", temper = Temper.Nervous, female = true,
                                  outfit = new[] { "coat", "pants", "boots", "scarf" },
                                  present = () => Story.StateOf("S17") == Story.State.Active });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S17: Bea's kitchen beside a road 120-450 m out of town; the stranded coach 450-1200 m further on.</summary>
        static partial void Anchors_S17(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            if (!Roadside(world, tc, town.radius + 120f, town.radius + 450f, 16f, p => Clear(town, p, 60f) && Level(world, p, 5f, 1f) && Q2.Free(world, p, 60f), out var bea, out var bf)
                && !Q2.Near(world, tc, town.radius + 120f, town.radius + 600f, 60f, 5f, 1.4f, out bea, out bf))
                bea = Q2.Anywhere(world, tc, town.radius + 180f, 60f, out bf);
            Q2.Put(world, "bea", bea, bf);
            Clearing("bea", 10f);

            if (!Roadside(world, bea, 450f, 1200f, 18f, p => Clear(town, p, 250f) && Level(world, p, 6f, 1.2f) && Q2.OpenAround(world, p, 6f) && Q2.Free(world, p, 80f), out var camp, out var cf)
                && !Q2.Near(world, bea, 450f, 1300f, 80f, 6f, 1.5f, out camp, out cf))
                camp = Q2.Anywhere(world, bea, 500f, 150f, out cf);
            Q2.Put(world, "s17_camp", camp, cf);
            Q2.Put(world, "s17_camp2", camp + Quaternion.Euler(0f, cf, 0f) * new Vector3(-3.5f, 0f, -1.5f), cf + 25f);
            Clearing("s17_camp", 13f);
        }
    }
}
