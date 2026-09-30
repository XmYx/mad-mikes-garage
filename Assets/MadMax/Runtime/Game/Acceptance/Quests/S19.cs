using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S19(List<Scenario> into) => into.Add(new StoryS19());
    }

    /// <summary>S19 THE BELL BENEATH THE WATER in a sandbox world: Ester's offer, her diving gear, a dive over the drowned
    /// chapel, the slow-and-level plan, rigging the bell under water (Bram rigs it where the lake is too shallow to go
    /// under), the rise on the bags, hauling it in on the shore line, and hanging it in town; the plan, the rigging and the
    /// hanging place are remembered and written into the payoff.</summary>
    class StoryS19 : Scenario
    {
        public override string Id => "story.s19";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            yield return Q3T.OnFoot(g);
            if (!c.Check(Q3T.Open("S19"), "S19 is on offer in a sandbox world")) yield break;
            var ruin = StoryAnchors.Get("s19_ruin");
            float lvl = g.World.WaterLevel(ruin.x, ruin.z);
            if (!c.Check(!float.IsNaN(lvl), "the drowned chapel lies in a lake")) yield break;
            c.Metric("ruin_depth", lvl - g.World.Sample(ruin.x, ruin.z).height, "m");
            c.Metric("yard_to_town", Vector3.Distance(StoryAnchors.Get("ester"), StoryAnchors.Get("town1")), "m");
            yield return Q3T.TalkAt(c, "ester", "ester", "THAT'S A LOT OF ROPE");
            yield return H.Until(() => Q3T.Active("S19"), 3f);
            if (!c.Check(Q3T.Active("S19"), "Ester's job is taken")) yield break;
            yield return H.Until(() => StoryTag.Find("s19_bell") != null, 4f);
            c.Check(StoryTag.Find("s19_bell") != null, "the bell stands in its frame on the lake bed");
            c.Check(StoryTag.Find("s19_skiff") != null, "Ester's skiff is afloat by the yard");
            // her husband's hard hat
            yield return Q3T.TalkAt(c, "ester", "ester", "CAN I BORROW");
            yield return Q3T.Step(c, "S19", "loan", 3f, "borrowed the diving gear");
            c.Check(g.Inventory.GetItem("cloth_dive_helmet") > 0 && g.Inventory.GetItem("cloth_air_tank") > 0, "a brass helmet and an air tank in the pack");
            g.Player.Rig.Wear("dive_helmet"); g.Player.Rig.Wear("air_tank");
            g.FillTank(WastelandGame.TankSize);
            c.Fixture("put on the helmet and tank; tank full");
            // the survey: down to the ruin
            var t = MadMax.World.DeformableTerrain.Instance;
            var sp = ruin + new Vector3(0f, 0f, -1.5f); sp.y = t.Height(sp.x, sp.z) + 0.2f;
            g.Player.Teleport(sp, 0f);
            c.Fixture("walked into the lake over the ruin (teleport)");
            yield return Q3T.Step(c, "S19", "survey", 6f, "surveyed the drowned chapel");
            c.Screenshot("ruin");
            yield return null;
            // the plan: slow and level
            yield return Q3T.TalkAt(c, "ester", "ester", "FOUR SMALL BAGS");
            yield return Q3T.Step(c, "S19", "plan", 3f, "planned the lift");
            c.Check(g.Inventory.GetItem("story_lift_bags") > 0, "lift bags and a sling");
            // rigging: under water beside the bell, else Bram
            var bell = StoryTag.Find("s19_bell").transform.position;
            var bp = bell + new Vector3(1.2f, 0f, 0f); bp.y = t.Height(bp.x, bp.z) + 0.2f;
            g.Player.Teleport(bp, 0f);
            g.FillTank(WastelandGame.TankSize);
            c.Fixture("dived to the bell with a full tank (teleport)");
            yield return H.Until(() => Plot.StepDone("S19", "rig"), 5f);
            if (!Plot.StepDone("S19", "rig"))
            {
                c.Note("the lake is too shallow at the bell to work with the head under (" + (g.Player.HeadUnder ? "head under" : "head above") + "): Bram rigs it");
                if (g.Inventory.Get(ResourceType.Scrap) < 25) { g.Inventory.Add(ResourceType.Scrap, 25); c.Fixture("25 scrap for Bram"); }
                yield return Q3T.TalkAt(c, "ester", "s19_bram", "BRAM, RIG IT");
            }
            yield return Q3T.Step(c, "S19", "rig", 3f, "the bell is rigged");
            c.Check(g.Inventory.GetItem("story_lift_bags") == 0, "the bags went on the bell");
            yield return Q3T.Step(c, "S19", "raise", 45f, "she rose level on the bags and floats");
            c.Check(!Plot.Flag("s19_fouled"), "a slow lift doesn't foul");
            // ashore on the shore line
            yield return Q3T.TalkAt(c, "ester", "ester", "HAUL HER IN");
            yield return Q3T.Step(c, "S19", "ashore", 3f, "hauling her in");
            yield return Q3T.Step(c, "S19", "landed", 70f, "the bell is on the shingle");
            yield return H.Walk(c, "s19_shore", 3f);
            c.Screenshot("bell_ashore");
            yield return null;
            // where she hangs
            yield return Q3T.TalkAt(c, "ester", "ester", "IN THE TOWN");
            yield return H.Until(() => Plot.Flag("s19_hung"), 4f);
            yield return new WaitForSeconds(0.2f);
            var hung = Q3T.Prop(g, "alarm_bell", "s19_townbell", 6f);
            c.Check(hung && StoryTag.Find("s19_bell") && StoryTag.Find("s19_bell").gameObject == hung.gameObject, "the bell hangs at the edge of town");
            yield return H.Walk(c, "s19_townbell", 1.5f);
            yield return Q3T.Finished(c, "S19", "S19 THE BELL BENEATH THE WATER is done");
            c.Check(Q3T.Done("S19") && Plot.Flag("ester_boat_service") && g.Inventory.GetItem("use_o2_bottle") >= 2, "paid: O2 bottles and Ester's boat service");
            c.Check(Plot.Route("S19", "plan") != null && Plot.Route("S19", "plan").StartsWith("FOUR SMALL BAGS") && Plot.Route("S19", "hang").StartsWith("IN THE TOWN"), "the plan and the hanging place are remembered");
            var payoff = StoryLibrary.Get("S19").payoff;
            c.Check(payoff.Contains("SLOW AND LEVEL") && payoff.Contains("EDGE OF TOWN") && payoff.Contains("HAULED"), "the payoff tells how it was done");
        }
    }
}
