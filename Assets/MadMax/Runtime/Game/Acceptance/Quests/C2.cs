using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_C2(List<Scenario> into) => into.Add(new QuestC2CheapRoad());
    }

    /// <summary>C2 THE CHEAP ROAD in a story world: Pru's trial truck with its crate of seed potatoes, one run past the
    /// warden's gate and one along the public road through the wash (each timed, measured and classified by the truck),
    /// the wash filled back to the road, tyres to match and the first haul contract, Sera told. Checks both runs recorded,
    /// the route remembered and the closing line.</summary>
    class QuestC2CheapRoad : Scenario
    {
        public override string Id => "story.c2";
        public override float Timeout => 180f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            ArcCT.Skip(c, "A1", "A2", "C1");
            yield return new WaitForSeconds(1.5f);
            if (!c.Check(StoryState.StateOf("C2") == StoryState.State.Open, "C2 is on offer after C1")) yield break;
            c.Metric("market_to_village_straight", ArcCT.Flat(StoryAnchors.Get("c_market"), StoryAnchors.Get("c_village")), "m");
            c.Metric("gate_off_road", ArcCT.Flat(StoryAnchors.Get("c2_gate"), StoryAnchors.Get("c2_public")), "m");
            yield return ArcCT.Meet(c, "sera");
            if (!c.Check(ArcCT.Say(g, "sera", "ISAAC SAYS YOU'LL KNOW"), "Sera wants the cheap road measured")) yield break;
            yield return H.Until(() => StoryState.StateOf("C2") == StoryState.State.Active, 3f);

            var truck = ArcCT.Tagged("c2_trial");
            if (!c.Check(truck, "the village's trial truck waits at the village")) yield break;
            var cargo = truck.GetComponent<VehicleChassis>().FindSocket("cargo");
            c.Check(cargo && cargo.Current && cargo.Current.partId == "cargo_crate", "with a crate of seed potatoes in the bed");
            var wash = StoryAnchors.Get("c2_crossing");
            var t = DeformableTerrain.Instance;
            c.Check(t.DugDepth(wash.x, wash.z) > 0.4f, $"a soft wash is dug across the public road ({t.DugDepth(wash.x, wash.z):0.00} m)");

            yield return H.Walk(c, "c2_start", 2f);
            yield return ArcCT.Done("C2", "village");
            yield return H.Until(() => g.CastBody("c2_hauler") != null, 5f);
            c.Screenshot("scene");
            yield return null;
            c.Check(ArcCT.Say(g, "c2_hauler", "WHAT ARE WE TESTING"), "Pru explains the trial");
            yield return ArcCT.Done("C2", "brief");

            // ---- run 1: the warden's track (village -> gate -> market)
            g.Enter(truck);
            c.Fixture("in the trial truck");
            yield return new WaitForSeconds(0.6f);
            var v = StoryAnchors.Get("c_village"); var m = StoryAnchors.Get("c_market"); var gate = StoryAnchors.Get("c2_gate");
            ArcCT.Put(c, truck, v, gate - v, "at the village's edge");
            yield return new WaitForSeconds(0.8f);
            ArcCT.Put(c, truck, gate, m - gate, "driven over the flats past the warden's gate");
            yield return new WaitForSeconds(0.8f);
            ArcCT.Put(c, truck, Vector3.Lerp(gate, m, 0.6f), m - gate, "on towards the market");
            yield return new WaitForSeconds(0.6f);
            ArcCT.Put(c, truck, m, m - gate, "at the market's edge");
            yield return ArcCT.Done("C2", "short", 4f);
            c.Check(StoryState.StepDone("C2", "short") && StoryLibrary.C2Run("short") != null, "the warden's track is surveyed: " + StoryLibrary.C2Run("short"));
            c.Note("the runs are placed, not driven: their minutes and litres are the fixture's, the distance is straight-line hops");

            // ---- run 2: the public road (market -> halfway -> the wash -> village)
            var pub = StoryAnchors.Get("c2_public");
            ArcCT.Put(c, truck, pub, wash - pub, "along the public road");
            yield return new WaitForSeconds(0.8f);
            ArcCT.Put(c, truck, wash, v - wash, "through the wash");
            yield return new WaitForSeconds(0.8f);
            ArcCT.Put(c, truck, v, v - wash, "at the village's edge again");
            yield return ArcCT.Done("C2", "survey", 4f);
            c.Check(StoryState.StepDone("C2", "public") && StoryState.StepDone("C2", "survey"), "the public road is surveyed: " + StoryLibrary.C2Run("public"));
            c.Check(Journal.Entries.Any(e => e.text.StartsWith("THE CHEAP ROAD, SIDE BY SIDE")), "the journal compares the two");

            // ---- improve the wash: fill it
            for (int i = 0; i < 8 && t.DugDepth(wash.x, wash.z) > 0.15f; i++)
                t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dump, new Vector3(wash.x, t.Height(wash.x, wash.z), wash.z), 3.5f, 0.3f, 0);
            c.Fixture("soil dumped into the wash (the excavator's dump op)");
            yield return ArcCT.Done("C2", "decide", 5f);
            c.Check(StoryState.Route("C2", "decide") == "FILLED THE WASH", "the wash is filled: " + StoryState.Route("C2", "decide"));
            // the tick that hands the tyres and the contract over is frame-throttled: wait for it rather than a fixed second
            int Tyres() => Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Count(p => p && p.partId == "wheel_offroad" && !p.Socket && ArcCT.Flat(p.transform.position, truck.transform.position) < 8f);
            yield return H.Until(() => StoryState.Flag("c2_paid") && Tyres() >= 2 && Contracts.Active.Any(k => k.id == "C2:HAUL"), 8f);
            c.Note($"paid flag {StoryState.Flag("c2_paid")}, village {(Market.Near(StoryAnchors.Get("c_village")) != null)}, market {(Market.Near(StoryAnchors.Get("town1")) != null)}, contracts [{string.Join(", ", Contracts.Active.Select(k => k.id))}], loose wheels near the truck {Tyres()}");
            var tyres = Tyres();
            c.Check(tyres >= 2, "a pair of all-terrain tyres by the trial truck (" + tyres + ")");
            c.Check(Contracts.Active.Any(k => k.id == "C2:HAUL"), "the village's first haul contract is taken");

            // ---- tell Sera
            g.Exit();
            yield return new WaitForSeconds(0.5f);
            yield return ArcCT.Meet(c, "sera");
            c.Check(ArcCT.Say(g, "sera", "HERE'S WHAT CHEAP COSTS"), "tell Sera");
            yield return H.Until(() => StoryState.StateOf("C2") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("C2") == StoryState.State.Done && StoryState.Flag("c2_done"), "C2 THE CHEAP ROAD is done");
            var payoff = StoryLibrary.Get("C2").payoff;
            c.Check(payoff.Contains("FILLED THE WASH") && payoff.Contains("PUBLIC ROAD") && Journal.Entries.Any(e => e.text == payoff), "the closing line remembers it: " + payoff);
            yield return H.Until(() => StoryState.StateOf("C4") == StoryState.State.Open, 3f);
            c.Check(StoryState.StateOf("C4") == StoryState.State.Open, "C4 A BRIDGE YOU CAN AFFORD opens next");
        }
    }
}
