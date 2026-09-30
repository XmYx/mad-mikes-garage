using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S02(List<Scenario> into) => into.Add(new QuestS02());
    }

    /// <summary>S02 THE GOAT HAS A LAWYER in a sandbox world: Bess's broken pen, the chewed beds, Horace found and walked
    /// home on a cabbage, the gap fenced, the burnt rail in Oswin's fire, and the "even" settlement remembered.</summary>
    class QuestS02 : Scenario
    {
        public override string Id => "story.s02";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S02") == MadMax.Story.Story.State.Open, "S02 is on offer in a sandbox world");
            yield return H.Walk(c, "bess", 2.5f);
            yield return H.Until(() => g.CastBody("bess") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("bess"), "YOU LOOK LIKE YOU'VE LOST"), "Bess Rill has lost a goat")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("S02") == MadMax.Story.Story.State.Active, 3f);
            var gap = StoryAnchors.Get("s02_gap");
            c.Check(!Placeable.All.Any(p => p && p.id == "fence_wood" && Vector3.Distance(p.transform.position, gap) < 0.6f), "the pen has a gap on the far side");

            yield return H.Walk(c, "s02_pen", 2f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "pen"), 4f);
            c.Check(g.Inventory.GetItem("food_cabbage") >= 2, "Bess hands over two cabbages (Horace's weakness)");
            c.Screenshot("pen");
            yield return null;
            yield return H.Walk(c, "s02_trail1", 1.5f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "chew1"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S02", "chew1"), "a chewed cabbage bed on the trail");

            // ---- Horace
            yield return H.Until(() => StoryTag.Find("s02_goat") != null, 4f);
            var tag = StoryTag.Find("s02_goat");
            var goat = tag ? tag.GetComponent<Animal>() : null;
            if (!c.Check(goat && goat.Def.id == "goat", "Horace is out there")) yield break;
            var gp = goat.transform.position + (StoryAnchors.Get("s02_pen") - goat.transform.position).normalized * 4f;
            gp.y = MadMax.World.DeformableTerrain.Instance.Height(gp.x, gp.z) + 0.3f;
            g.Player.Teleport(gp, 0f);
            c.Fixture("walked up to Horace (teleport)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "find"), 5f);
            c.Check(MadMax.Story.Story.StepDone("S02", "find"), "tracked him down");
            yield return H.Until(() => goat.owned && goat.order == 1, 3f);
            c.Check(goat.owned && goat.order == 1, "he smells the cabbage and follows");
            c.Screenshot("horace");
            yield return null;
            // walk home at a steady pace (1.2 m every 0.4 s); he trails behind
            var pen = StoryAnchors.Get("s02_pen");
            for (int i = 0; i < 90; i++)
            {
                var me = g.Player.transform.position;
                var to = pen - me; to.y = 0f;
                if (to.magnitude < 0.8f) break;
                var step = me + to.normalized * Mathf.Min(1.2f, to.magnitude);
                step.y = MadMax.World.DeformableTerrain.Instance.Height(step.x, step.z) + 0.2f;
                g.Player.Teleport(step, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                yield return new WaitForSeconds(0.4f);
            }
            c.Fixture("walked back to the pen with the cabbage (1.2 m steps)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "lure"), 20f);
            c.Metric("goat_to_pen", Vector3.Distance(goat.transform.position, pen), "m");
            if (!c.Check(MadMax.Story.Story.StepDone("S02", "lure") && MadMax.Story.Story.Route("S02", "lure") == "HORACE WALKED HOME", "Horace walked home behind the cabbage")) yield break;

            // ---- the gap
            var r = Quaternion.Euler(0f, StoryAnchors.Yaw("s02_pen"), 0f);
            var fp = gap; fp.y = MadMax.World.DeformableTerrain.Instance.Height(fp.x, fp.z);
            FurnitureLibrary.Spawn("fence_wood", g.Build.Structures, fp, r, g.propMaterial);
            c.Fixture("a fence built in the gap (as the build tool would)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "mend"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S02", "mend"), "the pen is whole again");

            // ---- the rail and the settlement
            c.Check(!H.Talk(g, g.CastBody("oswin"), "OSWIN, THAT'S BESS'S"), "no talk of rails before you've seen one");
            yield return H.Walk(c, "s02_fire", 1.5f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "rail"), 4f);
            c.Check(g.Inventory.GetItem("story_s02_rail") > 0, "a half-burnt fence rail in Oswin's fire");
            yield return H.Walk(c, "oswin", 2.5f);
            yield return H.Until(() => g.CastBody("oswin") != null, 5f);
            c.Screenshot("oswin");
            yield return null;
            c.Check(H.Talk(g, g.CastBody("oswin"), "OSWIN, THAT'S BESS'S"), "the rail for the shirts: call it even");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S02", "settle"), 4f);
            yield return H.Walk(c, "bess", 2.5f);
            yield return H.Until(() => g.CastBody("bess") != null, 5f);
            int cheese = g.Inventory.GetItem("food_cheese");
            c.Check(H.Talk(g, g.CastBody("bess"), "IT'S SETTLED"), "tell Bess");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S02") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("S02") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("s02_done"), "S02 THE GOAT HAS A LAWYER is done");
            c.Check(g.Inventory.GetItem("food_cheese") >= cheese + 2 && g.Inventory.GetItem("drink_milk") >= 2, "paid in milk and cheese");
            c.Check(MadMax.Story.Story.Route("S02", "settle") == StoryLibrary.S02Even, "the settlement is remembered: " + MadMax.Story.Story.Route("S02", "settle"));
            c.Check(Journal.Entries.Any(e => e.text.Contains("CANCELLED OUT")), "the payoff tells how it was settled");
        }
    }
}
