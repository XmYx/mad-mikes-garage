using System.Collections;
using System.Collections.Generic;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S23(List<Scenario> into) => into.Add(new StoryS23());
    }

    /// <summary>S23 NO TEETH, STILL TROUBLE in a sandbox world: two stallholders give testimony, Nia sets out the rules,
    /// Brick accepts a bout; stepping into the ring starts it, stepping out is a real stop (a yield: nothing happens to
    /// anyone); back in, the bout is fought with the scored blows of the nonlethal rules (his punches cost the player wind,
    /// never health) to three knockdowns; Brick is unhurt and peaceable afterwards; the win settles it, pays Melee training,
    /// and the closing line offers a rematch.</summary>
    class StoryS23 : Scenario
    {
        public override string Id => "story.s23";
        public override float Timeout => 240f;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        IEnumerator StandAt(ScenarioContext c, Vector3 p, string what)
        {
            p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z) + 0.3f;
            c.Game.Player.Teleport(p, StoryAnchors.Yaw("s23_ring"));
            c.Fixture(what + " (teleport)");
            yield return new WaitForSeconds(0.8f);
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            yield return Q5Test.Open(c, "S23", "nia", "nia", "YOUR MARKET LOOKS NERVOUS");
            if (!c.Check(MadMax.Story.Story.StateOf("S23") == MadMax.Story.Story.State.Active, "S23 is running")) yield break;
            // ---- testimony
            yield return H.Until(() => g.CastBody("s23_pru") != null && g.CastBody("s23_dev") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s23_pru"), "WHAT DOES BRICK WANT"), "Pru's testimony");
            c.Check(H.Talk(g, g.CastBody("s23_dev"), "DOES BRICK LEND MONEY"), "Dev's testimony");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S23", "testimony"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S23", "testimony"), "two stallholders heard out");
            c.Check(H.Talk(g, g.CastBody("nia"), "THE STALLHOLDERS HAVE TOLD ME"), "Nia sets out the ring's rules");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S23", "plan"), 4f);
            yield return H.Until(() => g.CastBody("s23_brick") != null, 5f);
            var brick = g.CastBody("s23_brick");
            if (!c.Check(brick, "Brick Halloran waits by the ring")) yield break;
            c.Check(H.Talk(g, brick, "YOU AND ME IN NIA'S RING"), "Brick accepts the challenge");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S23", "challenge"), 4f);
            g.Player.Equip(null);
            c.Fixture("empty-handed (Nia's rules: blunt tools only; the test lands its blows through the hit path directly)");
            var ring = StoryAnchors.Get("s23_ring");
            var outside = ring + Quaternion.Euler(0f, StoryAnchors.Yaw("s23_ring"), 0f) * new Vector3(0f, 0f, -8f);
            yield return StandAt(c, outside, "stood outside the ring");
            float health0 = g.Stats.health;

            // ---- a real stop: step in, then out again
            yield return StandAt(c, ring, "stepped into the ring");
            yield return H.Until(() => BoutRing.Current != null, 3f);
            if (!c.Check(BoutRing.Current != null, "stepping into the ring starts the bout")) yield break;
            c.Check(brick.Hostile, "Brick fights (for the length of the bout)");
            yield return new WaitForSeconds(2.5f);
            yield return StandAt(c, outside, "stepped out of the ring");
            yield return H.Until(() => BoutRing.Current == null, 3f);
            c.Check(BoutRing.Finished != null && BoutRing.Finished.result == MadMax.Story.NonlethalBout.Result.Yielded, "stepping out is a yield: the bout stops (" + (BoutRing.Finished != null ? BoutRing.Finished.result.ToString() : "-") + ")");
            c.Check(!brick.Hostile && brick.Alive && !MadMax.Story.Story.StepDone("S23", "settle"), "Brick is peaceable again, nothing is settled");

            // ---- the bout, to three knockdowns
            yield return StandAt(c, ring, "stepped back into the ring");
            yield return H.Until(() => BoutRing.Current != null, 3f);
            var bout = BoutRing.Current ? BoutRing.Current.Bout : null;
            if (!c.Check(bout != null, "a rematch starts")) yield break;
            c.Screenshot("bout");
            yield return null;
            float t0 = Time.time; int blows = 0;
            while (BoutRing.Current && Time.time - t0 < 70f)
            {
                if (!bout.Down && brick)
                {
                    var p = brick.transform.position + Vector3.up * 1.1f;
                    brick.ApplyHit(p, (brick.transform.position - g.Player.transform.position).normalized, 0.9f, 0.3f, g.Player.gameObject);
                    blows++;
                }
                yield return new WaitForSeconds(0.35f);
            }
            c.Fixture($"{blows} blows landed through the melee hit path (as a wrapped club would)");
            c.Metric("blows_landed", bout.blowsLanded, ""); c.Metric("blows_taken", bout.blowsTaken, "");
            c.Check(bout.result == MadMax.Story.NonlethalBout.Result.Won && bout.fighterDowns == 3, $"three knockdowns: the bout is won ({bout.result}, {bout.fighterDowns} down)");
            c.Check(brick && brick.Alive && brick.Health >= brick.MaxHealth - 0.5f, $"Brick is unhurt ({(brick ? brick.Health : 0f):0}/{(brick ? brick.MaxHealth : 0f):0})");
            c.Check(!brick.Hostile, "and peaceable again");
            c.Check(g.Stats.health >= health0 - 0.5f, $"his blows cost wind, not health ({health0:0} → {g.Stats.health:0}; took {bout.blowsTaken})");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S23", "settle"), 4f);
            c.Check(MadMax.Story.Story.Route("S23", "settle") == "WON THE BOUT" && MadMax.Story.Story.StepDone("S23", "r_bout"), "settled in the ring (remembered; Melee training paid)");
            yield return H.Walk(c, "nia", 2.5f);
            c.Check(H.Talk(g, g.CastBody("nia"), "IT'S SETTLED"), "tell Nia");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S23") == MadMax.Story.Story.State.Done, 5f);
            c.Check(MadMax.Story.Story.StateOf("S23") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("market_credit"), "S23 NO TEETH, STILL TROUBLE is done");
            c.Check(g.Inventory.GetItem("cloth_gloves") > 0, "protective gloves and market credit");
            c.Check(StoryLibrary.Get("S23").payoff.Contains("REMATCH"), "the closing line offers a rematch");
        }
    }
}
