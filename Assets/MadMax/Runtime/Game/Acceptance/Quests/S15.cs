using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S15(List<Scenario> into) => into.Add(new StoryS15());
    }

    /// <summary>S15 THE ORGAN RUNS ON DIESEL in a sandbox world: Hollis's cracked generator is repaired in build mode and
    /// fuelled; the three missing pipes come out of the chapel ruin (two picked up, one from a crate) and are fitted; the
    /// organ is cabled to the supply and the generator stalls under organ, lamps and heater together; the heater goes
    /// behind a LOW load breaker, the generator is restarted and the organ runs steady (balanced); the recital, started on
    /// the player's word, plays as a performance scene over the generator's noise; the route and closing line follow.</summary>
    class StoryS15 : Scenario
    {
        public override string Id => "story.s15";
        public override float Timeout => 300f;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            yield return Q5Test.Open(c, "S15", "hollis", "hollis", "IS THAT AN ORGAN");
            if (!c.Check(MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Active, "S15 is running")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "look"), 4f);
            var stage = StoryAnchors.Get("hollis");
            S15Organ organ = null;
            yield return H.Until(() => (organ = Object.FindObjectsByType<S15Organ>(FindObjectsSortMode.None).FirstOrDefault()) != null, 4f);
            var gen = Placeable.All.FirstOrDefault(p => p && p.id == "generator" && g.IsStoryProp(p) && Flat(p.transform.position, stage) < 12f);
            var heater = Placeable.All.FirstOrDefault(p => p && p.id == "heater" && g.IsStoryProp(p) && Flat(p.transform.position, stage) < 12f);
            if (!c.Check(organ && gen && heater, "the organ, Hollis's generator and the audience heater")) yield break;
            var gc = gen.GetComponent<Generator>();
            c.Check(gen.hits < gen.MaxHits && gc.fuel <= 0f && !organ.Powered, "the generator is cracked and dry; the organ has no wind");
            c.Screenshot("stage");
            yield return null;

            // ---- the generator: repaired in build mode, fuelled, started
            g.Inventory.Add(ResourceType.Iron, 2); g.Inventory.Add(ResourceType.Copper, 2); g.Inventory.Add(ResourceType.Aluminium, 2); g.Inventory.Add(ResourceType.Fuel, 12);
            c.Fixture("repair materials (2 iron, copper, aluminium) and 12 L petrol in the pack");
            g.Build.Repair(gen);
            gc.Use(g, true); gc.Use(g, false);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "gen"), 4f);
            c.Check(gen.hits == gen.MaxHits && gc.fuel > 0f && gc.on, "repaired (build mode R), fuelled ([T]) and started ([E])");
            c.Check(MadMax.Story.Story.Route("S15", "gen") == "HOLLIS'S GENERATOR, REPAIRED", "route: " + MadMax.Story.Story.Route("S15", "gen"));

            // ---- the pipes from the chapel ruin
            yield return H.Walk(c, "s15_ruin", 1.5f);
            var ruin = StoryAnchors.Get("s15_ruin");
            var pipes = WorldItem.All.Where(w => w && w.key == "story_organ_pipe" && Flat(w.transform.position, ruin) < 8f).ToList();
            c.Check(pipes.Count == 2, $"two pipes lie in the ruin ({pipes.Count})");
            foreach (var w in pipes) g.PickUpItem(w);
            var crate = Placeable.All.FirstOrDefault(p => p && p.id == "crate" && g.IsStoryProp(p) && Flat(p.transform.position, ruin) < 8f);
            var box = crate ? crate.GetComponent<Container>() : null;
            if (c.Check(box && box.inventory.GetItem("story_organ_pipe") > 0, "and one in a crate"))
            {
                box.inventory.TakeItem("story_organ_pipe"); g.Inventory.AddItem("story_organ_pipe");
                c.Fixture("the pipe moved from the crate to the pack (its container page)");
            }
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "pipes"), 4f);
            c.Check(g.Inventory.GetItem("story_organ_pipe") == 3 && MadMax.Story.Story.StepDone("S15", "pipes"), "three pipes recovered ([E] on the two, the crate for the third)");
            yield return H.Walk(c, "hollis", 3f);
            yield return H.Until(() => g.CastBody("hollis") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("hollis"), "HERE: THREE PIPES"), "Hollis fits the pipes");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "fit"), 4f);
            c.Check(g.Inventory.GetItem("story_organ_pipe") == 0, "the pipes went into the organ");

            // ---- power, the overload, the balance
            var node = organ.GetComponent<UtilityNode>(); var gn = gen.GetComponent<UtilityNode>();
            node.Link(gn, UtilityKind.Power);
            c.Fixture("a cable from the generator to the organ's lamp (as the build tool's cable does)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "power"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S15", "power"), "the organ is on the supply");
            yield return H.Until(() => gc.stalled, Generator.StallAfter + 4f);
            c.Check(gc.stalled, "organ, lamps and heater together are more than 1.5 kW: the generator stalls");
            var bp = heater.transform.position + heater.transform.right * 1.5f; bp.y = MadMax.World.DeformableTerrain.Instance.Height(bp.x, bp.z);
            var breaker = FurnitureLibrary.Spawn("breaker", g.Build.Structures, bp, heater.transform.rotation, g.propMaterial);
            yield return null;
            var bn = breaker ? breaker.GetComponent<UtilityNode>() : null;
            if (!c.Check(bn, "a load breaker")) yield break;
            var hn = heater.GetComponent<UtilityNode>();
            hn.Unlink(); bn.Link(gn, UtilityKind.Power); hn.Link(bn, UtilityKind.Power);
            breaker.GetComponent<PowerBreaker>().SetPriority(2);
            c.Fixture("a load breaker built between the generator and the heater, set LOW ([E])");
            gc.Toggle();
            c.Fixture("generator restarted ([E])");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "balance"), 20f);
            c.Check(MadMax.Story.Story.StepDone("S15", "balance") && organ.Powered, "the organ runs steady with the heater shedding first");
            c.Check(MadMax.Story.Story.Route("S15", "balance") == "THE HEATER SHEDS FIRST (LOAD BREAKER)", "how it was balanced: " + MadMax.Story.Story.Route("S15", "balance"));

            // ---- the recital
            c.Check(H.Talk(g, g.CastBody("hollis"), "THE ORGAN'S READY"), "tell Hollis to play");
            yield return H.Until(() => PerformanceScene.Current != null && PerformanceScene.Current.Beat >= 2, 20f);
            c.Check(PerformanceScene.Current && PerformanceScene.Current.Show.key == "s15:recital", "the recital plays");
            if (PerformanceScene.Current) c.Check(PerformanceScene.Current.Guests.Count >= 6, $"townsfolk gather ({PerformanceScene.Current.Guests.Count})");
            c.Screenshot("recital");
            yield return null;
            yield return H.Until(() => MadMax.Story.Story.StepDone("S15", "recital"), 80f);
            c.Check(MadMax.Story.Story.Route("S15", "recital") == "OVER THE GENERATOR", "with the generator beside the stage it is a noisy recital: " + MadMax.Story.Story.Route("S15", "recital"));
            c.Check(H.Talk(g, g.CastBody("hollis"), "THAT WAS SOMETHING"), "tell Hollis");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Done, 5f);
            c.Check(MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("hollis_recital"), "S15 THE ORGAN RUNS ON DIESEL is done");
            c.Check(g.Inventory.GetItem(ItemIds.Coil) >= 2 && g.Inventory.GetItem("story_recital_tape") == 1 && g.Inventory.GetItem("story_tin_whistle") == 1, "coils, the tape and a tin whistle");
            c.Check(StoryLibrary.Get("S15").payoff.Contains("GENERATOR"), "the closing line remembers the noise");
        }
    }
}
