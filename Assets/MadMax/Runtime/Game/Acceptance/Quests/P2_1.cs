using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_P2_1(List<Scenario> into) => into.Add(new StoryP2Chain());
    }

    /// <summary>P2 THE MARKET NOBODY WANTED in a sandbox world, all four stages: Barnaby's side and the three-field
    /// survey (P2.1); the rotation chosen (the market moves to Oda's field), stalls, latrine and water built (P2.2);
    /// opening day settled with the gate tally (P2.3); the purse, the supply run, the evening and the upkeep agreed, so
    /// the market recurs (P2.4).</summary>
    class StoryP2Chain : Scenario
    {
        public override string Id => "story.p2";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            DayNight.SetHours(10f);
            c.Fixture("clock set to 10:00");
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("P2.1") == MadMax.Story.Story.State.Open, "P2.1 is on offer in a sandbox world");
            foreach (var a in new[] { "p2_oda", "p2_barnaby", "p2_a", "p2_b", "p2_mid", "p2_market", "p2_tally" }) c.Check(StoryAnchors.Has(a), "anchor " + a + " is placed");
            c.Metric("hamlet_gap", Vector3.Distance(StoryAnchors.Get("p2_oda"), StoryAnchors.Get("p2_barnaby")), "m");

            // ---- P2.1
            yield return H.Walk(c, "p2_oda", 2.5f);
            yield return H.Until(() => g.CastBody("hamlets") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("hamlets"), "I HEARD SOMEONE WANTS"), "Oda Fenn wants a market (somewhere else)")) yield break;
            c.Screenshot("oda");
            yield return null;
            yield return H.Walk(c, "p2_barnaby", 2.5f);
            yield return H.Until(() => g.CastBody("barnaby") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("barnaby"), "ODA SAYS THE MARKET"), "Barnaby's side");
            foreach (var f in new[] { "p2_a", "p2_b", "p2_mid" }) { yield return H.Walk(c, f, 1f); yield return H.Until(() => false, 0.6f); }
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.1", "survey"), 4f);
            c.Check(MadMax.Story.Story.StepDone("P2.1", "survey") && MadMax.Story.Story.Flag("p2_read_field_mid"), "three fields surveyed, each with a reading");
            yield return H.Walk(c, "p2_oda", 2.5f);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "I WALKED ALL THREE"), "report to Oda");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.1") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("P2.1") == MadMax.Story.Story.State.Done && StoryLibrary.Get("P2.1").payoff.StartsWith("THE SURVEY IS IN:"), "P2.1 done: " + StoryLibrary.Get("P2.1").payoff);

            // ---- P2.2: take turns (Oda's field first)
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.2") == MadMax.Story.Story.State.Open, 3f);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "SO WHERE DOES IT GO"), "where does it go");
            int wood = g.Inventory.Get(ResourceType.Wood);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "TAKE TURNS"), "take turns, Oda's field first");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.2", "choose"), 4f);
            yield return H.Until(() => Vector3.Distance(StoryAnchors.Get("p2_market"), StoryAnchors.Get("p2_a")) < 1f, 2f);
            c.Check(Vector3.Distance(StoryAnchors.Get("p2_market"), StoryAnchors.Get("p2_a")) < 1f, "the market field is Oda's field");
            c.Check(g.Inventory.Get(ResourceType.Wood) >= wood + 26, "the hamlets send timber and canvas");
            yield return H.Walk(c, "p2_barnaby", 2.5f);
            c.Check(H.Talk(g, g.CastBody("barnaby"), "IT'S DECIDED"), "tell Barnaby");
            var m = StoryAnchors.Get("p2_market"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("p2_market"), 0f);
            foreach (var (id, off) in new[] { ("player_stall", new Vector3(-3f, 0f, 0f)), ("player_stall", new Vector3(3f, 0f, 0f)), ("latrine", new Vector3(8f, 0f, -6f)), ("rain_collector", new Vector3(-8f, 0f, -6f)) })
            {
                var p = m + r * off; p.y = DeformableTerrain.Instance.Height(p.x, p.z);
                FurnitureLibrary.Spawn(id, g.Build.Structures, p, r, g.propMaterial);
            }
            c.Fixture("two market stalls, a latrine and a rain collector built on the market field (as the build tool would)");
            yield return H.Walk(c, "p2_market", 4f);
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.2") == MadMax.Story.Story.State.Done, 6f);
            c.Check(MadMax.Story.Story.StateOf("P2.2") == MadMax.Story.Story.State.Done && StoryLibrary.P2Rotates(MadMax.Story.Story.Route("P2.2", "choose")), "P2.2 A THIRD FIELD is done; the rotation is remembered");

            // ---- P2.3: opening day, settled with the gate tally
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.3") == MadMax.Story.Story.State.Open, 3f);
            yield return H.Walk(c, "p2_oda", 2.5f);
            yield return H.Until(() => g.CastBody("hamlets") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "READY TO OPEN"), "opening day");
            yield return H.Walk(c, "p2_market", 4f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.3", "arrive") && g.CastBody("hamlets") && g.CastBody("barnaby"), 6f);
            c.Check(Vector3.Distance(g.CastBody("hamlets").transform.position, StoryAnchors.Get("p2_market")) < 12f, "the elders are at the market field");
            yield return H.Until(() => MadMax.Animals.Animal.All.Count(a => a && a.key != null && a.key.StartsWith("story:p2_goat")) >= 2, 4f);
            c.Check(MadMax.Animals.Animal.All.Any(a => a && a.key != null && a.key.StartsWith("story:p2_goat")), "Barnaby's goats among the stalls");
            c.Screenshot("opening_day");
            yield return null;
            yield return H.Walk(c, "p2_tally", 1f);
            yield return H.Until(() => g.Inventory.GetItem("story_p2_tally") > 0, 4f);
            c.Check(g.Inventory.GetItem("story_p2_tally") > 0, "the gate tally sheet");
            c.Check(H.Talk(g, g.CastBody("hamlets"), "THE GATE TALLY"), "the stock records settle it");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.3", "dispute"), 4f);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "SHAKE ON IT"), "they shake on it");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.3") == MadMax.Story.Story.State.Done, 4f);
            c.Note($"P2.3: state {MadMax.Story.Story.StateOf("P2.3")}, step {MadMax.Story.Story.Current(StoryLibrary.Get("P2.3"))?.id}, dispute route '{MadMax.Story.Story.Route("P2.3", "dispute")}', " +
                   $"tally {g.Inventory.GetItem("story_p2_tally")}, payoff '{StoryLibrary.Get("P2.3").payoff}'");
            c.Check(MadMax.Story.Story.StateOf("P2.3") == MadMax.Story.Story.State.Done && StoryLibrary.Get("P2.3").payoff.StartsWith("THE GATE TALLY"), "P2.3 OPENING DAY is done; settled by the records");

            // ---- P2.4: the supply run and the evening
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.4") == MadMax.Story.Story.State.Open, 3f);
            yield return H.Walk(c, "p2_oda", 2.5f);
            yield return H.Until(() => g.CastBody("hamlets") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "WHAT DOES THE MARKET STILL NEED"), "one supply run");
            yield return H.Walk(c, "p2_market", 4f);
            yield return H.Until(() => g.CastBody("hamlets") && Vector3.Distance(g.CastBody("hamlets").transform.position, StoryAnchors.Get("p2_market")) < 12f, 5f);
            int scrap = g.Inventory.Get(ResourceType.Scrap);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "I'LL MAKE THE RUN"), "take the purse");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.4", "purse"), 4f);
            c.Check(g.Inventory.Get(ResourceType.Scrap) >= scrap + 30, "thirty scrap for the run");
            g.Inventory.Add(ResourceType.Fuel, 10 - Mathf.Min(10, g.Inventory.Get(ResourceType.Fuel)));
            int food0 = g.Inventory.GetItem("food_can");
            g.Inventory.AddItem("food_can", 6);
            c.Fixture("10 L petrol and six tins bought in town");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.4", "food"), 4f);
            c.Check(H.Talk(g, g.CastBody("hamlets"), "THE SUPPLIES ARE HERE"), "supplies delivered");
            yield return H.Until(() => MadMax.Story.Story.Flag("p2_lamps"), 4f);
            c.Note($"P2.4 deliver: step {MadMax.Story.Story.Current(StoryLibrary.Get("P2.4"))?.id}, deliver done {MadMax.Story.Story.StepDone("P2.4", "deliver")}, tins {food0} + 6 -> {g.Inventory.GetItem("food_can")}, fuel {g.Inventory.Get(ResourceType.Fuel)}");
            c.Check(MadMax.Story.Story.StepDone("P2.4", "deliver") && g.Inventory.GetItem("food_can") <= food0 && MadMax.Story.Story.Flag("p2_lamps"), "six tins and the lamp fuel went to the market");
            DayNight.SetHours(19f);
            c.Fixture("waited until 19:00");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P2.4", "evening"), 4f);
            c.Check(MadMax.Story.Story.StepDone("P2.4", "evening"), "the opening evening");
            c.Screenshot("evening");
            yield return null;
            c.Check(H.Talk(g, g.CastBody("hamlets"), "EACH HAMLET SWEEPS"), "the hamlets keep it going in turn");
            yield return H.Until(() => MadMax.Story.Story.Flag("p2_market"), 4f);
            c.Check(H.Talk(g, g.CastBody("barnaby"), "GOODNIGHT"), "goodnight, Barnaby");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P2.4") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("P2.4") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("p2_market") && StoryLibrary.Get("P2.4").payoff.Contains("EVERY THIRD DAY"),
                "P2.4 ONE SUPPLY RUN is done; the market recurs");
        }
    }
}
