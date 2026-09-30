using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_C4(List<Scenario> into) => into.Add(new QuestC4Bridge());
    }

    /// <summary>C4 A BRIDGE YOU CAN AFFORD in a story world: the washout across the public road, Greta's terms, bridge
    /// timber bought from the crew and a timber deck built over the gap, the crew's loaded tipper driven across it (near
    /// side, the deck's middle, far side), both machines back at the yard on Greta's terms, and the road-work sign's credit.
    /// Checks the trench, the route, the returned machines, the sign and the closing line.</summary>
    class QuestC4Bridge : Scenario
    {
        public override string Id => "story.c4";
        public override float Timeout => 180f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            ArcCT.Skip(c, "A1", "A2", "C1", "C2");
            yield return new WaitForSeconds(1.5f);
            if (!c.Check(StoryState.StateOf("C4") == StoryState.State.Open, "C4 is on offer after C2")) yield break;
            c.Check(StoryAnchors.Has("c4_crossing") && StoryAnchors.Has("c4_yard"), "the crossing and the crew's yard are placed" + (StoryAnchors.Has("c4_ferry") ? " (a ford: the ferry is an option)" : " (a dry culvert)"));
            yield return ArcCT.Meet(c, "sera");
            if (!c.Check(ArcCT.Say(g, "sera", "THE PUBLIC ROAD'S THE CHEAP ONE"), "Sera: the storm took the crossing")) yield break;
            yield return H.Until(() => StoryState.StateOf("C4") == StoryState.State.Active, 3f);
            yield return new WaitForSeconds(0.5f);

            var cx = StoryAnchors.Get("c4_crossing");
            float dug = t.DugDepth(cx.x, cx.z);
            c.Metric("washout_depth", dug, "m");
            c.Check(dug > 1f, $"the washout is dug across the road ({dug:0.00} m)");
            var dig = ArcCT.Tagged("c4_digger"); var tip = ArcCT.Tagged("c4_tipper");
            var tm = tip ? tip.GetComponent<Machine>() : null;
            if (!c.Check(dig && tip && tm && tm.BedUnits >= 8, "Greta's digger and a loaded tipper wait at the yard (" + (tm ? tm.BedUnits : 0) + " units in the bed)")) yield break;
            var tipHome = tip.transform.position;

            yield return H.Walk(c, "c4_crossing", 8f);
            yield return ArcCT.Done("C4", "look");
            c.Screenshot("scene");
            yield return null;
            yield return ArcCT.Meet(c, "c4_foreman");
            c.Check(ArcCT.Say(g, "c4_foreman", "WHAT WILL IT TAKE"), "Greta's three ways and her terms");
            yield return ArcCT.Done("C4", "crew");

            // ---- the bridge: timber from the crew, a deck over the gap
            if (g.Inventory.Get(ResourceType.Scrap) < 45) { g.Inventory.Add(ResourceType.Scrap, 45); c.Fixture("45 scrap for the timber"); }
            int wood0 = g.Inventory.Get(ResourceType.Wood);
            c.Check(ArcCT.Say(g, "c4_foreman", "SELL ME BRIDGE TIMBER"), "buy bridge timber");
            yield return ArcCT.Done("C4", "timber");
            c.Check(g.Inventory.Get(ResourceType.Wood) >= wood0 + 40 && g.Inventory.Get(ResourceType.Iron) >= 4, "forty lengths and four bolts");
            var dir = Quaternion.Euler(0f, StoryAnchors.Yaw("c4_crossing"), 0f) * Vector3.forward;
            var bank = cx - dir * 4f; bank.y = t.Height(bank.x, bank.z);
            g.Inventory.TrySpend(ResourceType.Wood, 40); g.Inventory.TrySpend(ResourceType.Iron, 4);
            var bridge = FurnitureLibrary.Spawn("bridge_timber", g.Build.Structures, bank, Quaternion.LookRotation(-dir), g.propMaterial);
            c.Fixture("a timber bridge built from the near bank across the washout (its cost paid from the pack)");
            yield return ArcCT.Done("C4", "fix", 5f);
            c.Check(StoryState.Route("C4", "fix") == "BRIDGED IT", "the crossing is bridged: " + StoryState.Route("C4", "fix"));

            // ---- the test: the loaded tipper over the deck
            var near = StoryAnchors.Get("c4_near"); var far = StoryAnchors.Get("c4_far");
            ArcCT.Put(c, tip, near, dir, "driven to the near side");
            yield return new WaitForSeconds(1f);
            var rb = bridge ? bridge.GetComponent<RoadBridge>() : null;
            if (rb)
            {
                var mid = rb.Middle + dir * 1.5f + Vector3.up * 1f;
                tip.Body.position = mid; tip.Body.rotation = Quaternion.LookRotation(dir); tip.Body.linearVelocity = Vector3.zero; tip.Body.WakeUp();
                c.Fixture("the tipper on the deck's middle (placed)");
                yield return new WaitForSeconds(1.2f);
                c.Metric("tipper_over_deck", tip.transform.position.y - t.Height(tip.transform.position.x, tip.transform.position.z), "m");
            }
            ArcCT.Put(c, tip, far, dir, "on across to the far side");
            yield return ArcCT.Done("C4", "test", 5f);
            c.Check(StoryState.Route("C4", "test") == "THE LOADED TIPPER CROSSED", "a loaded tipper crossed: " + StoryState.Route("C4", "test"));

            // ---- the machines back on Greta's terms
            ArcCT.Put(c, tip, tipHome, tip.transform.forward, "driven back to the yard");
            yield return ArcCT.Done("C4", "return", 6f);
            c.Check(StoryState.Route("C4", "return") == "RETURNED AS AGREED", "both machines back as agreed: " + StoryState.Route("C4", "return"));

            // ---- the sign
            yield return ArcCT.Meet(c, "c4_foreman");
            c.Check(ArcCT.Say(g, "c4_foreman", "CREDIT EVERYONE WHO HELPED"), "the sign credits everyone who helped");
            yield return H.Until(() => StoryState.StateOf("C4") == StoryState.State.Done, 5f);
            c.Check(StoryState.StateOf("C4") == StoryState.State.Done && StoryState.Flag("c4_done") && StoryState.Flag("c4_rental"), "C4 A BRIDGE YOU CAN AFFORD is done; the crew lends its machines");
            c.Check(ArcCT.Prop(g, "sign_direction", "c4_near", 10f), "the road-work sign is up");
            var payoff = StoryLibrary.Get("C4").payoff;
            c.Check(payoff.Contains("BRIDGED") && payoff.Contains("EVERYONE") && Journal.Entries.Any(e => e.text == payoff), "the closing line: " + payoff);
        }
    }
}
