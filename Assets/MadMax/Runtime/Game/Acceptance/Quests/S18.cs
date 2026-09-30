using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S18(List<Scenario> into) => into.Add(new QuestS18Race());
    }

    /// <summary>S18 A PERFECTLY LEGAL RACE in a sandbox world: Tamsin talks the player round her loop, the race is on,
    /// the player takes her spare bicycle to the start flag, the count of three sends Tamsin off on her own AI bicycle,
    /// the player's bike is placed at each flag in turn (a disclosed shortcut for riding) without touching anyone at the
    /// crossing, and gets home first. Checks the lined-up start, Tamsin riding (not standing), the route (bike), the
    /// medal and the clean crossing, and the closing line.</summary>
    class QuestS18Race : Scenario
    {
        public override string Id => "story.s18";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(StoryState.StateOf("S18") == StoryState.State.Open, "S18 is on offer in a sandbox world");
            var gates = new[] { "s18_start", "s18_g1", "s18_g1b", "s18_g2", "s18_g3", "s18_market", "s18_marshal", "tamsin" };
            if (!c.Check(gates.All(StoryAnchors.Has), "the start, the flags, the wash and the market crossing are placed")) yield break;
            float loop = 0f; var order = new[] { "s18_start", "s18_g1", "s18_g2", "s18_g3", "s18_start" };
            for (int i = 1; i < order.Length; i++) loop += Q2T.Flat(StoryAnchors.Get(order[i - 1]), StoryAnchors.Get(order[i]));
            c.Metric("bike_loop", loop, "m");
            c.Check(MadMax.World.DeformableTerrain.Instance.World.Sample(StoryAnchors.Get("s18_g2").x, StoryAnchors.Get("s18_g2").z).roadDist > 30f, "the wash flag is off the road (mixed surface)");

            // ---- Tamsin's challenge
            yield return H.Walk(c, "tamsin", 2.5f);
            yield return H.Until(() => g.CastBody("tamsin") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("tamsin"), "NICE BIKE"), "Tamsin challenges anything with wheels")) yield break;
            yield return H.Until(() => StoryState.StateOf("S18") == StoryState.State.Active, 3f);
            c.Check(H.Talk(g, g.CastBody("tamsin"), "WALK ME THROUGH THE LOOP"), "Tamsin talks the loop through");
            yield return H.Until(() => StoryState.StepDone("S18", "scout"), 4f);
            c.Check(H.Talk(g, g.CastBody("tamsin"), "LET'S RACE"), "the race is on");
            yield return H.Until(() => StoryState.StepDone("S18", "ready"), 4f);
            var spare = StoryTag.Find("s18_spare") ? StoryTag.Find("s18_spare").GetComponent<VehicleDriver>() : null;
            if (!c.Check(spare && spare.GetComponent<BikeBalance>(), "Tamsin's spare bicycle leans by her sign")) yield break;
            c.Screenshot("scene");
            yield return null;

            // ---- line up on her spare bike
            var start = StoryAnchors.Get("s18_start");
            var toG1 = StoryAnchors.Get("s18_g1") - start; toG1.y = 0f;
            g.Enter(spare); yield return new WaitForSeconds(0.4f);
            yield return TestWorld.Place(c, spare, start, toG1.normalized, 0.8f);
            yield return H.Until(() => WastelandGame.S18RaceOn, 3f);
            c.Check(WastelandGame.S18RaceOn, "lined up at the start flag: the count begins");
            yield return new WaitForSeconds(4.5f);
            var rival = TestWorld.Vehicle("Tamsin's Bike");
            c.Check(rival && rival.GetComponent<MadMax.Npc.AiDriver>() && g.CastBody("tamsin") == null, "GO: Tamsin is out on her own bike, not standing at the line");

            // ---- the flags in order (placed at each: a disclosed shortcut for pedalling the loop)
            foreach (var gate in new[] { "s18_g1", "s18_g2", "s18_g3", "s18_start" })
            {
                var at = StoryAnchors.Get(gate);
                var dir = at - spare.transform.position; dir.y = 0f;
                yield return TestWorld.Place(c, spare, at, dir.sqrMagnitude > 1f ? dir.normalized : spare.transform.forward, 0.4f);
                if (gate != "s18_start") c.Check(!StoryState.Flag("s18_fouled"), "through " + gate + " without touching anyone");
            }
            yield return H.Until(() => StoryState.StepDone("S18", "race"), 5f);
            c.Check(StoryState.Route("S18", "race") == "ON A BIKE", "home on a bike: " + StoryState.Route("S18", "race"));
            c.Check(StoryState.StepDone("S18", "medal") && g.Inventory.GetItem(StoryLibrary.S18Medal) == 1, "home before Tamsin, nobody hurt: the tin medal");
            yield return H.Until(() => StoryState.StepDone("S18", "square"), 3f);
            c.Check(StoryState.Route("S18", "square") == "NOBODY HURT", "the crossing is square: " + StoryState.Route("S18", "square"));

            // ---- shake on it
            g.Exit(); yield return new WaitForSeconds(0.5f);
            yield return H.Until(() => g.CastBody("tamsin") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("tamsin"), "GOOD RACE"), "shake on it with Tamsin");
            yield return H.Until(() => StoryState.StateOf("S18") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("S18") == StoryState.State.Done && g.Inventory.GetItem("cloth_leather_satchel") > 0, "S18 A PERFECTLY LEGAL RACE is done: a courier's satchel");
            var payoff = StoryLibrary.Get("S18").payoff;
            c.Check(payoff.Contains("YOU BEAT TAMSIN") && payoff.Contains("TWO WHEELS") && Journal.Entries.Any(e => e.text == payoff), "the closing line remembers the race: " + payoff);
        }
    }
}
