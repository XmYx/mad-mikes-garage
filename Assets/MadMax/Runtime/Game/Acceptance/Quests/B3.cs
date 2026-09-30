using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_B3(List<Scenario> into) => into.Add(new QuestB3());
    }

    /// <summary>B3 LIGHTS WORTH COMING BACK TO in a STORY world: Nell's offer, a fuel generator cabled to a ceiling light
    /// and a fridge at the garage, a roof patch, a bed, a latrine; Nell's test (her compressor through a load breaker
    /// stalls the generator), the breaker set to LOW and the generator restarted so the fridge comes back; the refuge
    /// (lantern under the sign, recovery point). Checks the supply and test routes are remembered.</summary>
    class QuestB3 : Scenario
    {
        public override string Id => "story.b3";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            foreach (var q in new[] { "A1", "B1" }) MadMax.Story.Story.Complete(g, q);
            c.Fixture("A1 and B1 finished (chapter skip)");
            yield return new WaitForSeconds(1.5f);
            c.Check(MadMax.Story.Story.StateOf("B3") == MadMax.Story.Story.State.Open, "B3 is on offer (its systems are ready)");
            yield return Q7T.Meet(c, "nell", 3f, "nell");
            if (!c.Check(Q7T.Say(c, "nell", "WHAT WOULD MAKE THE GARAGE"), "Nell's offer")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("B3") == MadMax.Story.Story.State.Active, 3f);

            // ---- supply, lamp, service (as the build tool and the cable tool would)
            yield return H.Walk(c, "garage_yard", 5f);
            var gen = Q7T.Put(c, "garage", "generator", new Vector3(6.5f, 0f, -2f));
            var lamp = Q7T.Put(c, "garage", "light_ceiling", new Vector3(0f, 0f, 0f), 0f, 2.3f);
            var fridge = Q7T.Put(c, "garage", "fridge", new Vector3(-3f, 0f, -2.2f));
            if (!c.Check(gen && lamp && fridge, "generator, ceiling light and fridge built")) yield break;
            var gn = gen.GetComponent<UtilityNode>();
            gn.Link(fridge.GetComponent<UtilityNode>(), UtilityKind.Power);
            fridge.GetComponent<UtilityNode>().Link(lamp.GetComponent<UtilityNode>(), UtilityKind.Power);
            c.Fixture("cabled generator to fridge to light");
            var gc = gen.GetComponent<Generator>();
            gc.fuel = 20f; if (!gc.on) gc.Toggle();
            c.Fixture("20 L in the generator, switched on");
            yield return Q7T.Step("B3", "service", 10f);
            c.Check(MadMax.Story.Story.StepDone("B3", "supply") && MadMax.Story.Story.Route("B3", "supply") == StoryLibrary.B3Fuel, "supply: " + MadMax.Story.Story.Route("B3", "supply"));
            c.Check(MadMax.Story.Story.StepDone("B3", "lamp") && MadMax.Story.Story.StepDone("B3", "service"), "a lamp and the fridge on the generator's line");

            // ---- roof, bed, sanitation
            Q7T.Put(c, "garage", "roof_flat", new Vector3(-3f + (4 % 4) * 2f, 0f, -2f + (4 / 4) * 2f), 0f, 2.42f);   // one of the holes
            Q7T.Put(c, "garage", "bed", new Vector3(2.4f, 0f, -1.8f));
            Q7T.Put(c, "garage", "latrine", new Vector3(-7f, 0f, -4f));
            yield return Q7T.Step("B3", "sanitation", 8f);
            c.Check(MadMax.Story.Story.StepDone("B3", "roof") && MadMax.Story.Story.StepDone("B3", "bed") && MadMax.Story.Story.StepDone("B3", "sanitation"), "a patched roof, a bed and a latrine");

            // ---- Nell's test
            yield return Q7T.Meet(c, "garage_yard", 3f, "nell_garage");
            c.Screenshot("garage");
            yield return null;
            if (!c.Check(Q7T.Say(c, "nell_garage", "IT'S READY"), "tell Nell to test it")) yield break;
            yield return H.Until(() => MadMax.Story.Story.Flag("b3:test_on"), 3f);
            var brk = Q7T.Prop(c, "garage", "breaker", 12f);
            c.Check(brk && Q7T.Prop(c, "garage", "air_compressor", 12f), "Nell's compressor is on the line through a load breaker");
            yield return H.Until(() => gc.stalled, 10f);
            c.Check(gc.stalled && !gc.on, "the surge stalls the generator (outage)");
            yield return H.Until(() => MadMax.Story.Story.Flag("b3:outage") && !fridge.GetComponent<UtilityNode>().Powered, 3f);
            c.Check(MadMax.Story.Story.Flag("b3:outage") && !fridge.GetComponent<UtilityNode>().Powered, "the fridge went dark");
            var pb = brk ? brk.GetComponent<PowerBreaker>() : null;
            if (!c.Check(pb, "her breaker")) yield break;
            pb.Use(g, false);                                                                   // NORMAL → LOW
            c.Check(pb.priority == 2, "her breaker set to LOW (shed first)");
            gc.Use(g, false);                                                                   // [E] restart
            c.Fixture("breaker [E] to LOW, generator [E] restart");
            yield return Q7T.Step("B3", "restore", 12f);
            c.Check(MadMax.Story.Story.Route("B3", "restore") == StoryLibrary.B3Restored, "route: " + MadMax.Story.Story.Route("B3", "restore"));
            c.Check(fridge.GetComponent<UtilityNode>().Powered && gc.on, "the fridge is back on while the compressor stays plugged in (shed)");
            yield return Q7T.Done("B3", 6f);
            c.Check(MadMax.Story.Story.StateOf("B3") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("refuge"), "B3 LIGHTS WORTH COMING BACK TO is done: the garage is the refuge");
            c.Check(Q7T.Prop(c, "garage", "porch_lights", 10f) != null && Q7T.Prop(c, "garage", "air_compressor", 12f) == null, "a lantern under the sign; Nell took her compressor home");
            c.Check(Q7T.Wrote("KNOCKED IT FLAT") && Q7T.Wrote("WHATEVER YOU FEED IT"), "the payoff tells the supply and the test");
            c.Screenshot("refuge");
            yield return null;
        }
    }
}
