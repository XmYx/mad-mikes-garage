using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_C1(List<Scenario> into) => into.Add(new QuestC1Water());
    }

    /// <summary>C1 WATER HAS NO FLAG in a story world: Sera's offer, the toll post, the keeper's claim (the toll book as
    /// evidence) and Isaac's side (the test kit and a clamp), the kit's reading at the bowser's valve (litres and quality
    /// against the claim: the slip), the valve weeping until it is repaired in build mode, the measured toll with the
    /// slip, the bowser hitched to Isaac's pickup and brought to the village, Isaac's thanks; checks the route, the litres
    /// recorded, the spare tank, the route map and the closing line.</summary>
    class QuestC1Water : Scenario
    {
        public override string Id => "story.c1";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            ArcCT.Skip(c, "A1", "A2");
            yield return new WaitForSeconds(1.5f);
            if (!c.Check(StoryState.StateOf("C1") == StoryState.State.Open, "C1 is on offer after A2")) yield break;
            foreach (var k in new[] { "c1_toll", "c1_rig", "c1_salt", "c1_dest", "c_market", "c_village" }) c.Check(StoryAnchors.Has(k), "place " + k + " is bound");
            c.Metric("toll_to_village", ArcCT.Flat(StoryAnchors.Get("c1_toll"), StoryAnchors.Get("c1_dest")), "m");
            yield return ArcCT.Meet(c, "sera");
            if (!c.Check(ArcCT.Say(g, "sera", "YOU LOOK LIKE SOMEONE'S MAP"), "Sera Dune asks you to look at the detained water")) yield break;
            yield return H.Until(() => StoryState.StateOf("C1") == StoryState.State.Active, 3f);

            var bowser = ArcCT.Tagged("c1_bowser");
            var truck = ArcCT.Tagged("c1_truck");
            if (!c.Check(bowser && truck, "Isaac's bowser and pickup wait on the verge")) yield break;
            var valve = bowser.GetComponentsInChildren<Placeable>().FirstOrDefault(p => p.id == "water_valve");
            var node = valve ? valve.GetComponent<UtilityNode>() : null;
            if (!c.Check(valve && node && g.IsStoryProp(valve), "the bowser's outlet valve holds the shipment")) yield break;
            c.Check(node.Water > 370f && (node.taint & WaterTaint.Silt) != 0, $"380 L of silty water in it ({node.Water:0} L, {node.taint})");
            c.Check(valve.hits < FurnitureLibrary.Get("water_valve").hits, "the valve is damaged (weeping)");

            // ---- the toll post: both sides
            yield return H.Walk(c, "c1_toll", 3f);
            yield return ArcCT.Done("C1", "post");
            c.Check(StoryState.StepDone("C1", "post"), "at the toll post");
            yield return H.Until(() => g.CastBody("c1_keeper") != null, 5f);
            c.Screenshot("scene");
            yield return null;
            c.Check(ArcCT.Say(g, "c1_keeper", "WHY ARE YOU HOLDING"), "the keeper reads out the toll book's claim");
            yield return ArcCT.Done("C1", "claim");
            c.Check(StoryState.Evidence("toll book"), "the toll book is evidence");
            yield return ArcCT.Meet(c, "c1_driver", 2f);
            int iron0 = g.Inventory.Get(ResourceType.Iron);
            c.Check(ArcCT.Say(g, "c1_driver", "WHAT'S REALLY IN THE BOWSER"), "Isaac tells his side");
            yield return ArcCT.Done("C1", "hear");
            c.Check(g.Inventory.GetItem(UtilityIds.WaterTest) > 0 && g.Inventory.Get(ResourceType.Iron) > iron0, "Isaac hands over a test kit and a hose clamp (iron)");

            // ---- quantity and quality at the valve
            var back = -bowser.transform.forward; back.y = 0f; back.Normalize();
            var stand = valve.transform.position + back * 1.1f; stand.y = MadMax.World.DeformableTerrain.Instance.Height(stand.x, stand.z) + 0.3f;
            g.Player.Teleport(stand, Quaternion.LookRotation(-back).eulerAngles.y);
            c.Fixture("walked to the back of the bowser (teleport)");
            yield return new WaitForSeconds(0.6f);
            float w0 = node.Water;
            string reading = g.TestWater();
            c.Note("reading: " + reading);
            c.Check(reading != null && reading.Contains("SILTY") && reading.Contains(" L "), "the kit reads litres and silt at the valve");
            yield return ArcCT.Done("C1", "test");
            c.Check(StoryState.StepDone("C1", "test") && g.Inventory.GetItem(StoryLibrary.C1Slip) == 1, "the reading gives the test slip");
            yield return new WaitForSeconds(3f);
            c.Check(node.Water < w0, $"the valve weeps ({w0:0.0} -> {node.Water:0.0} L)");

            // ---- stop the leak: build mode repair
            g.Build.Repair(valve);
            yield return ArcCT.Done("C1", "check");
            c.Check(StoryState.StepDone("C1", "valve") && StoryState.StepDone("C1", "check"), "the valve is repaired (build mode R, the clamp's iron) and the shipment checked");
            float w1 = node.Water;
            yield return new WaitForSeconds(2f);
            c.Check(Mathf.Abs(node.Water - w1) < 0.01f, "no more weeping");

            // ---- settle it with the reading
            if (g.Inventory.Get(ResourceType.Scrap) < 19) { g.Inventory.Add(ResourceType.Scrap, 19); c.Fixture("19 scrap for the measured toll"); }
            yield return H.Walk(c, "c1_toll", 3f);
            yield return H.Until(() => g.CastBody("c1_keeper") != null, 5f);
            int scrap0 = g.Inventory.Get(ResourceType.Scrap);
            c.Check(ArcCT.Say(g, "c1_keeper", "YOUR BOOK SAYS 600 L"), "show the keeper the reading");
            yield return ArcCT.Done("C1", "resolve");
            c.Check(StoryState.Route("C1", "resolve") != null && StoryState.Route("C1", "resolve").StartsWith("YOUR BOOK"), "settled at the measured toll: " + StoryState.Route("C1", "resolve"));
            c.Check(g.Inventory.Get(ResourceType.Scrap) == scrap0 - 19, "nineteen scrap, not sixty");

            // ---- hitch it and bring it in
            var tow = bowser.GetComponent<TowCoupling>();
            var ahead = bowser.transform.forward; ahead.y = 0f; ahead.Normalize();
            ArcCT.Put(c, truck, bowser.transform.position + ahead * 5.2f, ahead, "backed up to the bowser's drawbar");
            yield return new WaitForSeconds(1f);
            bool coupled = tow && tow.Couple(truck, true);
            yield return H.Until(() => tow && tow.Tower == truck && !tow.Busy, 4f);
            c.Check(coupled && tow.Tower == truck, "the bowser hitches to Isaac's pickup ([J])");
            if (tow && tow.Tower) tow.Uncouple();
            yield return new WaitForSeconds(0.5f);
            var dest = StoryAnchors.Get("c1_dest");
            ArcCT.Put(c, bowser, ArcCT.At("c1_dest", new Vector3(-6f, 0f, 3f)), Quaternion.Euler(0f, StoryAnchors.Yaw("c1_dest") + 90f, 0f) * Vector3.forward, "towed to the village");
            ArcCT.Put(c, truck, ArcCT.At("c1_dest", new Vector3(0f, 0f, 3f)), Quaternion.Euler(0f, StoryAnchors.Yaw("c1_dest") + 90f, 0f) * Vector3.forward, "the tow car");
            yield return ArcCT.Done("C1", "deliver", 6f);
            if (!c.Check(StoryState.StepDone("C1", "deliver"), "the bowser is at the village")) yield break;
            yield return new WaitForSeconds(0.6f);
            int litres = ArcCRecord.Get("c1:litres");
            c.Metric("litres_delivered", litres, "L");
            c.Check(litres >= 230 && litres <= 380, "the litres that arrived are recorded (" + litres + " L)");
            c.Check(Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Any(p => p && p.partId == "cargo_water_tank" && !p.Socket && ArcCT.Flat(p.transform.position, dest) < 12f),
                    "Isaac's spare water tank waits by the cistern");
            c.Check(g.Discovered.Any(k => k.StartsWith("town:")), "the route map marks the towns");

            // ---- Isaac at the village
            yield return ArcCT.Meet(c, "c1_driver", 2f);
            c.Check(ArcCT.Say(g, "c1_driver", "THE WATER'S HERE"), "Isaac, and what the toll book leaves out");
            yield return H.Until(() => StoryState.StateOf("C1") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("C1") == StoryState.State.Done && StoryState.Flag("c1_done"), "C1 WATER HAS NO FLAG is done");
            var payoff = StoryLibrary.Get("C1").payoff;
            c.Check(payoff.Contains(litres + " L") && payoff.Contains("MEASURED TOLL") && Journal.Entries.Any(e => e.text == payoff), "the closing line remembers it: " + payoff);
            yield return H.Until(() => StoryState.StateOf("C2") == StoryState.State.Open, 3f);
            c.Check(StoryState.StateOf("C2") == StoryState.State.Open, "C2 THE CHEAP ROAD opens next");
        }
    }
}
