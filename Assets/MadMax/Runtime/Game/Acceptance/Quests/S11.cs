using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S11(List<Scenario> into) => into.Add(new QuestS11Letters());
    }

    /// <summary>S11 LETTERS NOBODY STOLE in a sandbox world: Reva's three letters down one road; the cottage note sends
    /// the first one on, Ida refuses hers (burned unread at her word), Hal gets his brother's apology, Noor keeps the Pells'
    /// letter sealed in her shed; Reva hears how each ended. Checks the route order, the letters leaving the pack, the
    /// choices and the closing line that reflects them.</summary>
    class QuestS11Letters : Scenario
    {
        public override string Id => "story.s11";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(StoryState.StateOf("S11") == StoryState.State.Open, "S11 is on offer in a sandbox world");
            var keys = new[] { "reva", "s11_old", "s11_ida", "s11_hal", "s11_orchard" };
            if (!c.Check(keys.All(StoryAnchors.Has), "Reva's post, the cottage, Bee Hollow, Hal's new place and the orchard are placed")) yield break;
            var town = StoryAnchors.Get("town1");
            float prev = 0f; bool outward = true;
            foreach (var k in keys) { float d = Q2T.Flat(StoryAnchors.Get(k), town); if (d + 60f < prev) outward = false; prev = d; }
            c.Check(outward, "the addresses lie one after another down the road out of town");
            c.Metric("round_trip", 2f * Q2T.Flat(StoryAnchors.Get("s11_orchard"), StoryAnchors.Get("reva")), "m");

            // ---- Reva's bag
            yield return H.Walk(c, "reva", 2.5f);
            yield return H.Until(() => g.CastBody("reva") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("reva"), "WHAT'S IN THE BAG"), "ask Reva about her bag")) yield break;
            yield return H.Until(() => StoryState.StepDone("S11", "bag"), 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S11LetterHal) == 1 && g.Inventory.GetItem(StoryLibrary.S11LetterIda) == 1 && g.Inventory.GetItem(StoryLibrary.S11LetterPell) == 1,
                    "three sealed letters in the pack");
            c.Screenshot("scene");
            yield return null;

            // ---- the first address: Hal moved
            yield return H.Walk(c, "s11_old", 3f);
            yield return H.Until(() => StoryState.StepDone("S11", "old"), 4f);
            c.Check(StoryState.StepDone("S11", "old") && Placeable.All.Any(p => p && p.id == "doorway_wood" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, StoryAnchors.Get("s11_old")) < 6f),
                    "Hal's old cottage stands empty");

            // ---- Ida refuses; burned unread at her word
            yield return H.Walk(c, "s11_ida", 2.5f);
            yield return H.Until(() => g.CastBody("s11_ida") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s11_ida"), "A LETTER FOR YOU, MRS MARSH"), "offer Ida her letter");
            yield return H.Until(() => StoryState.StepDone("S11", "ida"), 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S11LetterIda) == 1, "she refuses: the letter stays sealed in the pack");
            c.Check(H.Talk(g, g.CastBody("s11_ida"), "WANT ME TO BURN IT"), "offer to burn it where she can watch");
            yield return H.Until(() => StoryState.StepDone("S11", "ida_choice") && g.Inventory.GetItem(StoryLibrary.S11LetterIda) == 0, 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S11LetterIda) == 0, "Ida's letter burned unread");

            // ---- Hal at his sister's
            yield return H.Walk(c, "s11_hal", 2.5f);
            yield return H.Until(() => g.CastBody("s11_hal") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s11_hal"), "HAL BRIGGS?"), "hand Hal his letter");
            yield return H.Until(() => StoryState.StepDone("S11", "hal"), 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S11LetterHal) == 0, "Hal has his brother's letter");

            // ---- the orchard where the Pells lived
            yield return H.Walk(c, "s11_orchard", 3f);
            yield return H.Until(() => g.CastBody("s11_noor") != null, 5f);
            c.Check(Placeable.All.Count(p => p && p.id == "tree_planted" && g.IsStoryProp(p) && Q2T.Flat(p.transform.position, StoryAnchors.Get("s11_orchard")) < 16f) == 9,
                    "the address is an orchard: nine apple trees");
            c.Check(H.Talk(g, g.CastBody("s11_noor"), "I'VE A LETTER FOR THE PELLS"), "ask the orchard keeper about the Pells");
            yield return H.Until(() => StoryState.StepDone("S11", "pell"), 4f);
            int apples = g.Inventory.GetItem("food_apple");
            c.Check(H.Talk(g, g.CastBody("s11_noor"), "COULD IT HANG IN YOUR SHED"), "leave it sealed in Noor's shed");
            yield return H.Until(() => StoryState.StepDone("S11", "pell_choice") && g.Inventory.GetItem(StoryLibrary.S11LetterPell) == 0, 4f);
            c.Check(g.Inventory.GetItem(StoryLibrary.S11LetterPell) == 0 && g.Inventory.GetItem("food_apple") >= apples + 3, "the Pells' letter stays at the orchard; apples for the road");

            // ---- back to Reva
            yield return H.Walk(c, "reva", 2.5f);
            yield return H.Until(() => g.CastBody("reva") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("reva"), "THE LETTERS FOUND THEIR ENDS"), "tell Reva how it went");
            yield return H.Until(() => StoryState.StateOf("S11") == StoryState.State.Done, 4f);
            c.Check(StoryState.StateOf("S11") == StoryState.State.Done && g.Inventory.GetItem("cloth_schoolbag") > 0, "S11 LETTERS NOBODY STOLE is done: Reva's old bag");
            c.Check(StoryState.Route("S11", "ida_choice").StartsWith("WANT ME TO BURN") && StoryState.Route("S11", "pell_choice").StartsWith("COULD IT HANG"), "the choices are remembered");
            var payoff = StoryLibrary.Get("S11").payoff;
            c.Check(payoff.Contains("BURN") && payoff.Contains("NOOR'S SHED") && Journal.Entries.Any(e => e.text == payoff), "the closing line tells the three endings: " + payoff);
        }
    }

    /// <summary>Small test helpers for the q2 quests (S11, S13, S16, S17, S18).</summary>
    static class Q2T
    {
        public static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Teleport the player on foot to a point (disclosed).</summary>
        public static IEnumerator WalkTo(ScenarioContext c, Vector3 p, float yaw, string what)
        {
            p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z) + 0.3f;
            c.Game.Player.Teleport(p, yaw);
            c.Fixture("walked to " + what + " (teleport)");
            yield return new WaitForSeconds(1.2f);
        }
    }
}
