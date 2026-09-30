using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S07(List<Scenario> into) => into.Add(new QuestS07());
    }

    /// <summary>S07 AN HONEST FISH in a sandbox world: Milt's rod, the reeds read, a real catch with the rod (cast, strike
    /// on the bite, reel against the tension), the weigh-in, the shim found by crouching at the table, the scale exposed
    /// in public, Milt told; the choice and the catch are in the payoff.</summary>
    class QuestS07 : Scenario
    {
        public override string Id => "story.s07";
        public override float Timeout => 260f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = MadMax.World.DeformableTerrain.Instance;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S07") == MadMax.Story.Story.State.Open, "S07 is on offer in a sandbox world");
            var milt = StoryAnchors.Get("milt"); var face = StoryAnchors.Yaw("milt");
            var fwd = Quaternion.Euler(0f, face, 0f) * Vector3.forward;
            c.Metric("pond_from_town", Vector3.Distance(milt, StoryAnchors.Get("town1")), "m");
            c.Check(t.WaterDepth(milt.x + fwd.x * 4.5f, milt.z + fwd.z * 4.5f) > 0.25f, "water deep enough to cast into in front of Milt's spot");
            yield return H.Walk(c, "milt", -2.5f);
            yield return H.Until(() => g.CastBody("milt") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("milt"), "YOU LOOK LIKE A MAN WITH A GRIEVANCE"), "Milt Crowe wants his name cleared")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("S07") == MadMax.Story.Story.State.Active, 3f);
            yield return H.Until(() => MadMax.Story.Story.Flag("s07_rod"), 3f);
            c.Check(g.Inventory.GetItem("tool_fishing_rod") > 0, "a rod: Milt's spare, or your own and his thanks");
            c.Screenshot("pond");
            yield return null;

            // ---- the reeds
            yield return H.Walk(c, "s07_reeds", -2f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S07", "read"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S07", "reeds") && g.Inventory.GetItem(MadMax.Items.FishLibrary.Insects) >= 4, "the reeds: insects for bait");

            // ---- the catch, with the rod
            var spot = milt + Quaternion.Euler(0f, face, 0f) * new Vector3(3.2f, 0f, 0.6f); spot.y = t.Height(spot.x, spot.z) + 0.3f;
            g.Player.Teleport(spot, face);
            c.Fixture("stood on the bank beside Milt (teleport)");
            if (!g.Player.Tool || g.Player.Tool.id != "tool_fishing_rod") g.UseItem("tool_fishing_rod");
            yield return new WaitForSeconds(0.5f);
            var rod = g.Player.Tool as FishingRodTool;
            if (!c.Check(rod, "the rod in hand")) yield break;
            int fish = g.Inventory.GetItem("food_fish_raw"), casts = 0;
            float t0 = Time.time;
            bool reel = true;
            while (!MadMax.Story.Story.StepDone("S07", "catch") && Time.time - t0 < 150f)
            {
                switch (rod.phase)
                {
                    case FishingRodTool.Phase.Idle:
                        rod.reel = false;
                        rod.Strike(g.Player); casts++;
                        if (rod.phase == FishingRodTool.Phase.Idle && casts == 1)
                        {
                            var p = milt; p.y = t.Height(p.x, p.z) + 0.3f;
                            g.Player.Teleport(p, face);
                            c.Fixture("moved to Milt's own spot to reach the water");
                        }
                        yield return new WaitForSeconds(1f);
                        break;
                    case FishingRodTool.Phase.Bite:
                        rod.Click();
                        break;
                    case FishingRodTool.Phase.Hooked:
                        if (rod.Tension > 0.7f) reel = false; else if (rod.Tension < 0.35f) reel = true;
                        rod.reel = reel;
                        break;
                }
                yield return null;
            }
            rod.reel = false;
            c.Metric("casts", casts, "");
            c.Metric("catch_seconds", Time.time - t0, "s");
            if (!c.Check(MadMax.Story.Story.StepDone("S07", "catch") && g.Inventory.GetItem("food_fish_raw") > fish, "a fish from Milt's pond, caught fair")) yield break;

            // ---- the weigh-in
            yield return H.Walk(c, "s07_weigh", -2f);
            yield return H.Until(() => g.CastBody("hal") != null, 5f);
            c.Check(!H.Talk(g, g.CastBody("hal"), "(TO EVERYONE)"), "no accusations before the scale has been looked at");
            c.Check(H.Talk(g, g.CastBody("hal"), "WEIGH MY CATCH"), "Hal weighs it: impossibly heavy");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S07", "weigh"), 4f);
            var sc = StoryAnchors.Get("s07_scale") - Quaternion.Euler(0f, face, 0f) * Vector3.forward * 1.1f; sc.y = t.Height(sc.x, sc.z) + 0.3f;
            g.Player.Teleport(sc, face);
            g.Player.crouch = true;
            c.Fixture("walked up to the table and crouched");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S07", "scale"), 5f);
            g.Player.crouch = false;
            c.Check(MadMax.Story.Story.Route("S07", "scale") == StoryLibrary.S07Shim, "the shim under the pan: " + MadMax.Story.Story.Route("S07", "scale"));
            c.Screenshot("weigh_in");
            yield return null;
            c.Check(H.Talk(g, g.CastBody("hal"), "(TO EVERYONE)"), "say it out loud");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S07", "choice"), 4f);

            // ---- Milt
            yield return H.Walk(c, "milt", -2.5f);
            yield return H.Until(() => g.CastBody("milt") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("milt"), "IT WAS THE SCALE"), "tell Milt");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S07") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("S07") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("s07_done"), "S07 AN HONEST FISH is done");
            c.Check(MadMax.Story.Story.Flag("s07_public") && !MadMax.Story.Story.Flag("s07_quiet"), "the public route is remembered for later banter");
            c.Check(g.Inventory.GetItem("story_s07_ornament") > 0, "Milt's carved fish");
            c.Check(Journal.Entries.Any(e => e.text.Contains("A NEW WORD FOR A HEAVY SCALE")), "the payoff tells how it ended");
        }
    }
}
