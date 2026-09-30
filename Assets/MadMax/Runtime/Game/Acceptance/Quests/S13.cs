using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S13(List<Scenario> into) => into.Add(new QuestS13Roof());
    }

    /// <summary>S13 ONE GOOD ROOF in a sandbox world: Margo on the flat roof (too high to mantle from the ground), the
    /// stair down, crates at the back; borrow Cal's kit, nail his rails into a ladder against the wall and walk into it
    /// to climb, strap her ankle with his bandage, bring her down the ladder, tell Cal. Checks the roof height, Margo
    /// kept up there, the way up read as the ladder, her move to the yard and the remembered routes.</summary>
    class QuestS13Roof : Scenario
    {
        public override string Id => "story.s13";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(StoryState.StateOf("S13") == StoryState.State.Open, "S13 is on offer in a sandbox world");
            if (!c.Check(StoryAnchors.Has("cal") && StoryAnchors.Has("s13_house") && StoryAnchors.Has("s13_yard"), "Cal's workshop is placed")) yield break;
            var a = StoryAnchors.Get("s13_house"); float yaw = StoryAnchors.Yaw("s13_house"); var rot = Quaternion.Euler(0f, yaw, 0f);
            var t = MadMax.World.DeformableTerrain.Instance;

            // ---- Cal
            yield return H.Walk(c, "cal", 2.5f);
            yield return H.Until(() => g.CastBody("cal") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("cal"), "YOU'RE STARING AT THAT ROOF"), "Cal asks for help with Margo")) yield break;
            yield return H.Until(() => StoryState.StateOf("S13") == StoryState.State.Active, 3f);

            // ---- assess
            yield return Q2T.WalkTo(c, a + rot * new Vector3(0f, 0f, 6f), yaw + 180f, "the workshop");
            yield return H.Until(() => StoryState.StepDone("S13", "look"), 4f);
            c.Check(StoryState.StepDone("S13", "look"), "the building is assessed");
            var roofs = Placeable.All.Where(p => p && p.id == "roof_flat" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, a) < 2.2f).ToList();
            float roof = roofs.Count > 0 ? roofs.Max(p => p.transform.position.y) + 0.04f : 0f;
            float ground = t.Height(a.x + 3.5f, a.z);
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }) { var q = a + rot * d * 3f; ground = Mathf.Max(ground, t.Height(q.x, q.z)); }
            c.Check(roofs.Count == 4 && roof - ground > 2.3f, $"a flat roof {roof - ground:0.00} m up: too high to mantle from the ground");
            c.Check(Placeable.All.Count(p => p && p.id == "crate" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, a + rot * new Vector3(-0.9f, 0f, -2.85f)) < 0.5f) == 3, "three crates stacked at the back wall");
            c.Check(Placeable.All.Any(p => p && p.id == "stairs" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, a) < 5f), "the outside stair lies where it fell");
            yield return H.Until(() => g.CastBody("s13_margo") != null && g.CastBody("s13_margo").transform.position.y > roof - 0.5f, 5f);
            var margo = g.CastBody("s13_margo");
            if (!c.Check(margo && margo.transform.position.y > roof - 0.5f, "Margo is up on the roof")) yield break;
            c.Screenshot("scene");
            yield return null;

            // ---- Cal's kit
            c.Check(H.Talk(g, g.CastBody("cal"), "LEND ME WHAT YOU'VE GOT"), "borrow Cal's access kit");
            yield return H.Until(() => StoryState.StepDone("S13", "gear"), 4f);
            c.Check(g.Inventory.GetItem("tool_grapple") > 0 && g.Inventory.GetItem("med_bandage") > 0 && g.Inventory.Get(ResourceType.Wood) >= 3, "a grappling hook, a bandage and ladder rails");

            // ---- up the ladder
            g.Inventory.TrySpend(ResourceType.Wood, 3);
            var lp = a + rot * new Vector3(1f, 0f, 2.3f); lp.y = t.Height(lp.x, lp.z);
            var ladder = FurnitureLibrary.Spawn("ladder", g.Build.Structures, lp, rot, g.propMaterial);
            c.Fixture("Cal's rails nailed into a ladder against the front wall (as the build tool would; 3 wood spent)");
            yield return Q2T.WalkTo(c, a + rot * new Vector3(1f, 0f, 3.1f), yaw + 180f, "the foot of the ladder");
            g.Player.moveInput = new Vector2(0f, 1f);
            yield return H.Until(() => g.Player.Traversing, 2f);
            g.Player.moveInput = Vector2.zero;
            yield return H.Until(() => StoryState.StepDone("S13", "up"), 5f);
            c.Check(ladder && StoryState.StepDone("S13", "up") && g.Player.transform.position.y > roof - 0.4f, "walked into the ladder and climbed onto the roof");
            c.Check(StoryState.Route("S13", "up") == "UP A LADDER", "the way up is remembered: " + StoryState.Route("S13", "up"));

            // ---- steady her, bring her down
            c.Check(H.Talk(g, g.CastBody("s13_margo"), "LET ME STRAP THAT ANKLE"), "strap Margo's ankle");
            yield return H.Until(() => StoryState.StepDone("S13", "steady"), 4f);
            yield return H.Until(() => g.Inventory.GetItem("med_bandage") == 0, 2f);
            c.Check(StoryState.StepDone("S13", "steady") && g.Inventory.GetItem("med_bandage") == 0, "the bandage is on her ankle");
            yield return H.Until(() => StoryState.StepDone("S13", "down"), 4f);
            c.Check(StoryState.Route("S13", "down") == "DOWN A LADDER", "she comes down the ladder: " + StoryState.Route("S13", "down"));
            var yard = StoryAnchors.Get("s13_yard");
            yield return H.Until(() => g.CastBody("s13_margo") != null && Q2T.Flat(g.CastBody("s13_margo").transform.position, yard) < 5f, 5f);
            c.Check(g.CastBody("s13_margo") && Q2T.Flat(g.CastBody("s13_margo").transform.position, yard) < 5f, "Margo sits in the yard");

            // ---- Cal
            g.Player.crouch = true;
            yield return H.Until(() => g.Player.Traversing, 2f);
            g.Player.crouch = false;
            yield return new WaitForSeconds(1.5f);
            c.Check(g.Player.transform.position.y < roof - 1.5f, "crouched at the ladder's top and climbed down");
            c.Check(H.Talk(g, g.CastBody("cal"), "SHE'S DOWN"), "tell Cal");
            yield return H.Until(() => StoryState.StateOf("S13") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("S13") == StoryState.State.Done && g.Inventory.GetItem("book_builder") > 0 && g.Inventory.GetItem("misc_rope") >= 2, "S13 ONE GOOD ROOF is done: Cal's handbook and rope");
            var payoff = StoryLibrary.Get("S13").payoff;
            c.Check(payoff.Contains("LADDER") && payoff.Contains("STRAPPED") && Journal.Entries.Any(e => e.text == payoff), "the closing line follows the rescue: " + payoff);
        }
    }
}
