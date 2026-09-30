using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S21(List<Scenario> into) => into.Add(new StoryS21());
    }

    /// <summary>S21 THE LAST HONEST SAFE in a sandbox world: Ruth's offer, the seized safe (no lid to lift, damaged
    /// linkage), her order book from the old shop on the road out, the combination, an intact opening (blueprint and the
    /// bonus, the method's training), and the poem handed back unread; the method is remembered in the payoff.</summary>
    class StoryS21 : Scenario
    {
        public override string Id => "story.s21";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            yield return Q3T.OnFoot(g);
            if (!c.Check(Q3T.Open("S21"), "S21 is on offer in a sandbox world")) yield break;
            c.Metric("old_shop_to_town", Vector3.Distance(StoryAnchors.Get("s21_oldshop"), StoryAnchors.Get("town1")), "m");
            yield return Q3T.TalkAt(c, "ruth", "ruth", "A LOCKSMITH WITH A LOCKED SAFE");
            yield return H.Until(() => Q3T.Active("S21"), 3f);
            if (!c.Check(Q3T.Active("S21"), "Ruth's job is taken")) yield break;
            yield return H.Walk(c, "s21_safe", 1.6f);
            yield return Q3T.Step(c, "S21", "look", 4f, "looked the safe over");
            var safe = Q3T.Prop(g, "locker", "s21_safe", 4f);
            c.Check(safe && safe.hits < safe.MaxHits && !safe.GetComponent<Container>(), "the safe's linkage is seized and it has no lid to lift");
            c.Check(!H.Talk(g, g.CastBody("ruth"), "IT'S IN YOUR ORDER BOOK"), "no combination to offer yet");
            c.Screenshot("safe");
            yield return null;
            // the old shop on the road out
            yield return H.Walk(c, "s21_records", 1f);
            yield return Q3T.Step(c, "S21", "records", 4f, "searched the order books at the old shop");
            c.Check(g.Inventory.GetItem("story_order_book") > 0, "Ruth's old order book");
            int scrap = g.Inventory.Get(MadMax.Items.ResourceType.Scrap);
            yield return Q3T.TalkAt(c, "ruth", "ruth", "IT'S IN YOUR ORDER BOOK");
            yield return Q3T.Step(c, "S21", "open", 3f, "the safe is open");
            yield return Q3T.Step(c, "S21", "intact", 4f, "the envelopes came out whole");
            c.Check(Plot.StepDone("S21", "by_paper") && !Plot.StepDone("S21", "by_charge"), "the method's training: an answer from old papers");
            c.Check(g.Inventory.GetItem("bp_framepack") > 0 && g.Inventory.Get(MadMax.Items.ResourceType.Scrap) >= scrap + 25, "an intact opening pays the blueprint and the bonus");
            c.Check(safe, "the safe still stands");
            yield return Q3T.TalkAt(c, "ruth", "ruth", "THERE'S A POEM IN HERE. IT'S YOURS");
            yield return Q3T.Finished(c, "S21", "S21 THE LAST HONEST SAFE is done");
            c.Check(Plot.Route("S21", "open") != null && Plot.Route("S21", "open").StartsWith("IT'S IN YOUR ORDER BOOK") && Plot.Route("S21", "inside").Contains("HAVEN'T READ IT"), "the method and the poem are remembered");
            c.Check(StoryLibrary.Get("S21").payoff.Contains("ORDER BOOK"), "the payoff tells how it opened");
        }
    }
}
