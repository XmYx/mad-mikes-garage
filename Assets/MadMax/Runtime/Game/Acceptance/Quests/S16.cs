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
        static partial void Tests_S16(List<Scenario> into) => into.Add(new QuestS16Bus());
    }

    /// <summary>S16 RUST IN PEACE in a sandbox world: Mo's contract, the bus on the verge (loose lead, dry tank, tired
    /// engine), the camp behind it, Hanne Varga; the repair-for-spares deal: lead reconnected, fuelled, a repair kit,
    /// the engine catches with the player at the wheel; Hanne's note and the spares; Mo told. Checks that nothing is
    /// stripped, no standing is lost, and the closing line and the safe stop remember the choice.</summary>
    class QuestS16Bus : Scenario
    {
        public override string Id => "story.s16";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(StoryState.StateOf("S16") == StoryState.State.Open, "S16 is on offer in a sandbox world");
            if (!c.Check(new[] { "mo", "s16_bus", "s16_camp", "s16_hanne" }.All(StoryAnchors.Has), "Mo's cart, the bus, its camp and Hanne's shelter are placed")) yield break;
            int standing = MadMax.Npc.NpcRegistry.Reputation;

            // ---- Mo's contract
            yield return H.Walk(c, "mo", 2.5f);
            yield return H.Until(() => g.CastBody("mo") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("mo"), "BUSY DAY IN THE SCRAP TRADE"), "Mo offers the bus contract")) yield break;
            yield return H.Until(() => StoryState.StateOf("S16") == StoryState.State.Active && StoryTag.Find("s16_bus") != null, 4f);
            var bus = StoryTag.Find("s16_bus") ? StoryTag.Find("s16_bus").GetComponent<VehicleDriver>() : null;
            if (!c.Check(bus, "the bus stands on the verge")) yield break;
            var sys = bus.GetComponent<VehicleSystems>();
            var engine = bus.Engine ? bus.Engine.GetComponent<VehiclePart>() : null;
            c.Check(sys && sys.disconnected && sys.fuel < 0.5f && engine && engine.damage > 0.7f, "a loose battery lead, a dry tank and a tired engine");

            // ---- look it over, the camp, the owner
            yield return Q2T.WalkTo(c, bus.transform.position + bus.transform.right * 4f, bus.transform.eulerAngles.y - 90f, "the bus");
            yield return H.Until(() => StoryState.StepDone("S16", "look"), 4f);
            c.Check(StoryState.StepDone("S16", "look"), "the bus is looked over");
            yield return H.Walk(c, "s16_camp", 1.5f);
            yield return H.Until(() => StoryState.StepDone("S16", "signs"), 4f);
            c.Check(StoryState.StepDone("S16", "signs") && Placeable.All.Any(p => p && p.id == "bed" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, StoryAnchors.Get("s16_camp")) < 4f),
                    "bedding and ashes behind the bus: someone lives here");
            c.Screenshot("scene");
            yield return null;
            yield return H.Walk(c, "s16_hanne", 2.5f);
            yield return H.Until(() => g.CastBody("s16_hanne") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s16_hanne"), "IS THAT BUS YOURS"), "ask Hanne about the bus");
            yield return H.Until(() => StoryState.StepDone("S16", "owners"), 4f);
            c.Check(H.Talk(g, g.CastBody("s16_hanne"), "LET ME GET IT RUNNING"), "offer to repair it for the spares");
            yield return H.Until(() => StoryState.StepDone("S16", "deal"), 4f);
            c.Check(StoryState.Route("S16", "deal").StartsWith("LET ME GET IT RUNNING"), "the deal is remembered");

            // ---- get it running
            yield return Q2T.WalkTo(c, bus.transform.position + bus.transform.right * 3f, bus.transform.eulerAngles.y - 90f, "the bus");
            sys.Reconnect();
            c.Fixture("battery lead reconnected (the service key's wrench action)");
            g.Inventory.Add(ResourceType.Diesel, 30); g.Inventory.Add(ResourceType.Fuel, 30);
            c.Fixture("30 L diesel and 30 L petrol in the pack");
            int moved = sys.Service(g.Inventory);
            c.Check(sys.fuel >= 5f, $"fuel poured in ({moved} L)");
            engine.damage = 0.5f;
            c.Fixture("the engine patched with a repair kit (+30 %)");
            g.Enter(bus); yield return new WaitForSeconds(0.5f);
            yield return TestWorld.StartEngine(c, bus, 25f);
            yield return H.Until(() => StoryState.StepDone("S16", "work") && StoryState.StepDone("S16", "spares"), 5f);
            c.Check(StoryState.Route("S16", "work") == "GOT IT RUNNING", "the Vargas' bus runs: " + StoryState.Route("S16", "work"));
            c.Check(g.Inventory.GetItem(StoryLibrary.S16Note) == 1 && g.Inventory.GetItem("use_battery") > 0, "Hanne's note and the spares from the back");
            bus.throttleInput = 0f; bus.handbrake = true;
            g.Exit(); yield return new WaitForSeconds(0.5f);

            // ---- tell Mo
            yield return H.Walk(c, "mo", 2.5f);
            yield return H.Until(() => g.CastBody("mo") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("mo"), "THE BUS RUNS"), "tell Mo the bus is a home, not scrap");
            yield return H.Until(() => StoryState.StateOf("S16") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("S16") == StoryState.State.Done && g.Inventory.GetItem(StoryLibrary.S16Note) == 0, "S16 RUST IN PEACE is done; Mo keeps Hanne's note with the contract");
            c.Check(StoryTag.Find("s16_bus") && StoryState.Flag("s16_stop") && !StoryState.Flag("s16_trust") && MadMax.Npc.NpcRegistry.Reputation >= standing,
                    "the bus stands whole, a safe stop beside it, no standing lost");
            var payoff = StoryLibrary.Get("S16").payoff;
            c.Check(payoff.Contains("RUNS AGAIN") && Journal.Entries.Any(e => e.text == payoff), "the closing line remembers the deal: " + payoff);
        }
    }
}
