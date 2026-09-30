using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_B5(List<Scenario> into)
        {
            into.Add(new QuestB5());
            into.Add(new QuestB5Defence());
        }
    }

    /// <summary>B5 THE NIGHT WE STAYED in a STORY world, the talking way: Nell's warning, Silas's demand at the gate, Moth's
    /// ledger at the crew's camp, the double books read to the crew, a shared meal (keepsakes for the wall) and the
    /// cooperative charter. Checks the answer and the charter are remembered and the keepsake wall is up.</summary>
    class QuestB5 : Scenario
    {
        public override string Id => "story.b5";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            foreach (var q in new[] { "A1", "B1", "B3" }) MadMax.Story.Story.Complete(g, q);
            c.Fixture("A1, B1 and B3 finished (chapter skip)");
            yield return new WaitForSeconds(1.5f);
            c.Check(MadMax.Story.Story.StateOf("B5") == MadMax.Story.Story.State.Open, "B5 is on offer");
            yield return Q7T.Meet(c, "nell", 3f, "nell");
            if (!c.Check(Q7T.Say(c, "nell", "YOU LOOK WORRIED"), "Nell's warning")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("B5") == MadMax.Story.Story.State.Active, 3f);

            yield return Q7T.Meet(c, "b5_gate", 5f, "b5_boss");
            c.Check(g.CastBody("b5_boss") && g.CastBody("b5_heavy"), "Silas Vance and a rider at the gate");
            c.Screenshot("gate");
            yield return null;
            c.Check(Q7T.Say(c, "b5_boss", "YOU'RE BLOCKING MY ROAD"), "hear what he wants");
            yield return Q7T.Step("B5", "demand");
            c.Check(!Q7T.Say(c, "b5_boss", "READ THIS TO YOUR CREW"), "nothing to expose him with yet");

            yield return Q7T.Meet(c, "b5_camp", 4f, "b5_moth");
            c.Check(Q7T.Say(c, "b5_moth", "YOU DON'T LOOK LIKE"), "Moth at the crew's camp is fed up");
            yield return Q7T.Step("B5", "camp");
            c.Check(g.Inventory.GetItem(StoryLibrary.B5Ledger) > 0, "Silas's double ledger");

            yield return Q7T.Meet(c, "b5_gate", 5f, "b5_boss");
            c.Check(Q7T.Say(c, "b5_boss", "READ THIS TO YOUR CREW"), "read his double books to his crew");
            yield return Q7T.Step("B5", "answer");
            yield return new WaitForSeconds(0.6f);
            c.Check(MadMax.Story.Story.Route("B5", "answer") == StoryLibrary.B5Expose && MadMax.Story.Story.Flag("b5_exposed"), "answer: " + MadMax.Story.Story.Route("B5", "answer"));
            c.Check(g.Inventory.GetItem(StoryLibrary.B5Ledger) == 0, "the ledger stays with the crew");

            g.Inventory.AddItem("food_stew", 4);
            c.Fixture("four stews cooked");
            yield return Q7T.Step("B5", "meal");
            yield return Q7T.Meet(c, "garage_yard", 3f, "nell_garage");
            c.Check(Q7T.Say(c, "nell_garage", "EVERYONE, SIT DOWN"), "a shared meal");
            yield return Q7T.Step("B5", "supper");
            c.Check(g.Inventory.GetItem("food_stew") == 0 && g.Inventory.GetItem("trophy_plate") > 0 && g.Inventory.GetItem("trophy_hubcap") > 0, "four plates eaten; a plate and a hubcap for the wall");
            c.Check(Q7T.Say(c, "nell_garage", "A REFUGE. WE RUN IT TOGETHER"), "a cooperative refuge");
            yield return Q7T.Done("B5", 6f);
            c.Check(MadMax.Story.Story.StateOf("B5") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("charter_coop"), "B5 THE NIGHT WE STAYED is done; the charter is remembered");
            int mounts = 0; TrophyMount filled = null;
            foreach (var p in Placeable.All)
                if (p && p.id == "trophy_mount" && g.IsStoryProp(p)) { mounts++; if (p.TryGetComponent<TrophyMount>(out var tm) && !string.IsNullOrEmpty(tm.trophy)) filled = tm; }
            c.Check(mounts == 3 && filled != null, "the keepsake wall: " + mounts + " mounts, one holding " + (filled ? filled.trophy : "nothing"));
            c.Check(Q7T.Wrote("DOUBLE BOOKS") && Q7T.Wrote("COOPERATIVE REFUGE"), "the payoff tells the answer and the charter");
            c.Screenshot("wall");
            yield return null;
        }
    }

    /// <summary>B5 the defence way: three defence works and a claim at the garage, the flag at the gate raised when
    /// ready, and Silas's crew thinking twice (no shot fired). Proves the defence route resolves without forced
    /// combat when the defences are strong enough.</summary>
    class QuestB5Defence : Scenario
    {
        public override string Id => "story.b5_defence";
        public override float Timeout => 150f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return Q7T.Open(c, "B5", new[] { "A1", "B1", "B3" });
            if (!c.Check(MadMax.Story.Story.StateOf("B5") == MadMax.Story.Story.State.Active, "B5 runs")) yield break;
            yield return Q7T.Meet(c, "b5_gate", 5f, "b5_boss");
            c.Check(Q7T.Say(c, "b5_boss", "YOU'RE BLOCKING MY ROAD"), "hear what he wants");
            yield return Q7T.Step("B5", "demand");
            yield return H.Walk(c, "garage_yard", 4f);
            var flag = Q7T.Prop(c, "garage", "flag", 16f);
            var ctl = flag ? flag.GetComponent<StoryControl>() : null;
            if (!c.Check(ctl != null, "the flag at the gate")) yield break;
            c.Check(ctl.Prompt(g).Contains("CLAIM FLAG"), "no claim yet: the flag says so");
            Q7T.Put(c, "garage", "claim_flag", new Vector3(-5f, 0f, 8f));
            yield return null;
            c.Check(!ctl.Prompt(g).Contains("[E]"), "not ready with no defences built: " + ctl.Prompt(g));
            ctl.Use(g, false);
            c.Check(!MadMax.Story.Story.Flag("b5:confront"), "raising the flag early does nothing");
            Q7T.Put(c, "garage", "watchtower", new Vector3(-9f, 0f, 9f));
            Q7T.Put(c, "garage", "mg_nest", new Vector3(8f, 0f, 9f));
            foreach (float x in new[] { -4f, 4f }) Q7T.Put(c, "garage", "spike_wall", new Vector3(x, 0f, 13f));
            foreach (float x in new[] { -8f, 8f }) Q7T.Put(c, "garage", "barbed_wire", new Vector3(x, 0f, 13f));
            foreach (float x in new[] { -6f, 6f }) Q7T.Put(c, "garage", "sandbag_wall", new Vector3(x, 0f, 11f));
            Q7T.Put(c, "garage", "alarm_bell", new Vector3(-5f, 0f, 6f));
            yield return null;
            var claim = ClaimFlag.Near(StoryAnchors.Get("garage"));
            c.Metric("defence", claim ? claim.Defence() : -1, "");
            c.Check(claim && claim.Defence() >= StoryLibrary.B5Deter, "defence " + (claim ? claim.Defence() : -1) + " (" + StoryLibrary.B5Deter + " to make them think twice)");
            if (!c.Check(ctl.Prompt(g).Contains("[E] RAISE THE FLAG"), "ready: " + ctl.Prompt(g))) yield break;
            c.Screenshot("defences");
            yield return null;
            ctl.Use(g, false);
            c.Check(MadMax.Story.Story.Flag("b5:confront"), "the flag is up: the confrontation starts when you say");
            yield return Q7T.Step("B5", "answer", 10f);
            c.Check(MadMax.Story.Story.Route("B5", "answer") == StoryLibrary.B5Stood && MadMax.Story.Story.Flag("b5_deterred") && !MadMax.Story.Story.Flag("b5:raid"), "they looked and rode on; no raid (" + MadMax.Story.Story.Route("B5", "answer") + ")");
            c.Check(!(MadMax.Npc.BaseRaid.Instance && MadMax.Npc.BaseRaid.Instance.Live), "no fight at the garage");
            c.Check(Q7T.Wrote("NOBODY FIRED A SHOT"), "the journal remembers the night");
        }
    }
}
