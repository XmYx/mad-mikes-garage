using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S17(List<Scenario> into) => into.Add(new QuestS17Soup());
    }

    /// <summary>S17 SOUP AGAINST THE WEATHER in a sandbox world: plan a vegetable stew with Bea, stock her woodbox with
    /// 12 wood, cook three stove batches of stew at her stove (the fuel comes out of the woodbox), pack six portions in
    /// the haybox, carry it to the stranded coach without a knock, tell Bea. Checks the fuel burned from the box, the
    /// hand-overs, the routes and the closing line.</summary>
    class QuestS17Soup : Scenario
    {
        public override string Id => "story.s17";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(StoryState.StateOf("S17") == StoryState.State.Open, "S17 is on offer in a sandbox world");
            if (!c.Check(StoryAnchors.Has("bea") && StoryAnchors.Has("s17_camp"), "Bea's kitchen and the washout are placed")) yield break;
            c.Metric("kitchen_to_coach", Q2T.Flat(StoryAnchors.Get("bea"), StoryAnchors.Get("s17_camp")), "m");

            // ---- the plan
            yield return H.Walk(c, "bea", 2.5f);
            yield return H.Until(() => g.CastBody("bea") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("bea"), "COOKING FOR AN ARMY"), "Bea wants to feed the stranded coach")) yield break;
            yield return H.Until(() => StoryState.StateOf("S17") == StoryState.State.Active, 3f);
            c.Check(H.Talk(g, g.CastBody("bea"), "VEGETABLE STEW"), "plan a vegetable stew");
            yield return H.Until(() => StoryState.StepDone("S17", "plan"), 4f);
            var a = StoryAnchors.Get("bea");
            var box = Placeable.All.FirstOrDefault(p => p && p.id == "crate" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, a) < 4f);
            var stove = Placeable.All.FirstOrDefault(p => p && p.id == "stove" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, a) < 4f);
            var wood = box ? box.GetComponent<Container>() : null;
            var st = stove ? stove.GetComponentInChildren<CraftingStation>() : null;
            if (!c.Check(wood && st, "a wood stove and its woodbox under the awning")) yield break;
            c.Screenshot("scene");
            yield return null;

            // ---- the woodbox
            int packWood = g.Inventory.Get(ResourceType.Wood);
            if (packWood > 0) g.Inventory.TrySpend(ResourceType.Wood, packWood);
            wood.inventory.Add(ResourceType.Wood, 12);
            c.Fixture("12 wood put in the woodbox (the container page); the pack's own wood set aside so the stove must burn the box's");
            yield return H.Until(() => StoryState.StepDone("S17", "fuel"), 4f);
            c.Check(StoryState.Route("S17", "fuel") == "STOCKED THE WOODBOX", "the woodbox is stocked: " + StoryState.Route("S17", "fuel"));

            // ---- cook six portions at Bea's stove
            foreach (var id in new[] { "food_potato", "food_carrot", "food_cabbage" }) g.Inventory.AddItem(id, 3);
            c.Fixture("three potatoes, carrots and cabbages from the gardens");
            foreach (var id in StoryLibrary.S17Hot.Split('|').Concat(StoryLibrary.S17Tins.Split('|'))) { int n = g.Inventory.GetItem(id); if (n > 0) g.Inventory.TakeItem(id, n); }
            c.Fixture("the pack's own tins and soups set aside (only the stew cooked here counts)");
            var rec = RecipeLibrary.Get("stove_stew");
            if (!c.Check(rec != null && rec.station == "stove", "the stove's stew recipe")) yield break;
            for (int i = 0; i < 3; i++)
            {
                string why = g.CraftBlockReason(rec, st);
                if (!c.Check(why == null, "batch " + (i + 1) + " can cook: " + (why ?? "ok"))) yield break;
                g.Craft(rec, st);
                if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
                yield return H.Until(() => st.queue.Count == 0, 12f);
            }
            yield return H.Until(() => StoryState.StepDone("S17", "cook"), 4f);
            c.Check(g.Inventory.GetItem("food_stew") >= 6 && StoryState.Route("S17", "cook") == "HOT FROM THE POT", "six hot portions of stew: " + StoryState.Route("S17", "cook"));
            c.Check(wood.inventory.Get(ResourceType.Wood) == 9, $"the stove burned three wood from the woodbox ({wood.inventory.Get(ResourceType.Wood)} left)");

            // ---- the haybox
            c.Check(H.Talk(g, g.CastBody("bea"), "SIX PORTIONS, READY TO TRAVEL"), "Bea packs the haybox");
            yield return H.Until(() => StoryState.StepDone("S17", "pot"), 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S17Haybox) == 1 && g.Inventory.GetItem("food_stew") == 0, "six portions went into the haybox");

            // ---- the washout
            yield return H.Walk(c, "s17_camp", 3f);
            yield return H.Until(() => g.CastBody("s17_otis") != null && StoryState.StepDone("S17", "gentle"), 5f);
            c.Check(StoryState.StepDone("S17", "gentle"), "the haybox arrived without a knock");
            c.Check(StoryTag.Find("s17_coach"), "the coach sits at the washout");
            c.Check(H.Talk(g, g.CastBody("s17_otis"), "HOT FOOD FROM BEA'S KITCHEN"), "hand the haybox to the coach driver");
            yield return H.Until(() => StoryState.StepDone("S17", "carry"), 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S17Haybox) == 0, "the travellers have their supper");

            // ---- back to Bea
            yield return H.Walk(c, "bea", 2.5f);
            yield return H.Until(() => g.CastBody("bea") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("bea"), "THEY ATE EVERY DROP"), "tell Bea");
            yield return H.Until(() => StoryState.StateOf("S17") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("S17") == StoryState.State.Done && g.Inventory.GetItem(StoryLibrary.S17Card) == 1 && StoryState.Flag("bea_kitchen"), "S17 SOUP AGAINST THE WEATHER is done: Bea's recipe card");
            var payoff = StoryLibrary.Get("S17").payoff;
            c.Check(payoff.Contains("STEW") && payoff.Contains("STILL HOT") && Journal.Entries.Any(e => e.text == payoff), "the closing line remembers the meal: " + payoff);
        }
    }
}
