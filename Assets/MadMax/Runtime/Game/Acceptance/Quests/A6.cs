using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_A6(List<Scenario> into) => into.Add(new QuestA6());
    }

    /// <summary>A6 TELL IT STRAIGHT in a STORY world: the records at Ivo Rusk's Remnant checkpoint, the town's deliveries
    /// cross-checked by Ivo's runner, the redacted release (the script waits for Wes's logbook), the rehearsal with
    /// captions, the broadcast on WasteTalk, and Mara's apology and her room at the garage. Checks the broadcast is
    /// remembered as redacted and the payoff says so.</summary>
    class QuestA6 : Scenario
    {
        public override string Id => "story.a6";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return Q7T.Open(c, "A6", new[] { "A1", "A2", "B1", "A3", "A4", "A5" });
            if (!c.Check(MadMax.Story.Story.StateOf("A6") == MadMax.Story.Story.State.Active, "A6 runs after A5")) yield break;
            c.Check(StoryAnchors.Has("a6_archive"), "the Remnant checkpoint is bound");
            c.Metric("archive_to_town", Vector3.Distance(StoryAnchors.Get("a6_archive"), StoryAnchors.Get("town1")), "m");

            // ---- Ivo and the records
            yield return Q7T.Meet(c, "a6_archive", 6f, "ivo_archive");
            if (!c.Check(g.CastBody("ivo_archive") && g.CastBody("ivo_archive").Profile.Name == "DR. IVO RUSK", "Dr. Ivo Rusk at his checkpoint")) yield break;
            c.Check(Q7T.Prop(c, "a6_archive", "bookshelf", 4f) != null && Q7T.Prop(c, "a6_archive", "doorway_concrete", 4f) != null, "a concrete hut of shelves by the road");
            c.Screenshot("archive");
            yield return null;
            c.Check(Q7T.Say(c, "ivo_archive", "HERE'S ALL OF IT"), "show him the records");
            yield return Q7T.Step("A6", "records");
            g.Inventory.Add(ResourceType.Scrap, 15);
            c.Fixture("15 scrap for the runner");
            c.Check(Q7T.Say(c, "ivo_archive", "SEND YOUR RUNNER"), "send Ivo's runner to the freight office");
            yield return Q7T.Step("A6", "crosscheck");
            c.Check(MadMax.Story.Story.Evidence("tally"), "one town's book, checked: eleven signed for, four arrived");

            // ---- the decision with June: redacted
            yield return Q7T.Meet(c, "relay", 4f, "june");
            c.Check(Q7T.Say(c, "june", "LOADS AND DATES"), "a redacted release");
            yield return Q7T.Step("A6", "decide");
            yield return new WaitForSeconds(0.6f);
            c.Check(g.Inventory.GetItem(StoryLibrary.A6Script) == 0, "no script yet: a redaction needs a second source");
            c.Check(!Q7T.Say(c, "june", "LET'S RUN IT ONCE"), "no run-through without the script");
            yield return Q7T.Meet(c, "q7_porch", 2.5f, "wes_home");
            c.Check(Q7T.Say(c, "wes_home", "WES, YOUR LOGBOOK"), "Wes's logbook: loads and dates, no names");
            yield return Q7T.Step("A6", "logbook");
            yield return H.Until(() => g.Inventory.GetItem(StoryLibrary.A6Script) > 0, 2f);
            c.Check(MadMax.Story.Story.Evidence("logbook") && g.Inventory.GetItem(StoryLibrary.A6Script) > 0, "the logbook is evidence; June's script follows");

            // ---- rehearsal and broadcast
            yield return Q7T.Meet(c, "relay", 4f, "june");
            c.Check(Q7T.Say(c, "june", "LET'S RUN IT ONCE"), "run it through once");
            yield return Q7T.Step("A6", "prepare");
            yield return H.Until(() => Broadcast.Playing, 2f);
            c.Check(Broadcast.Playing, "the rehearsal plays line by line (captions)");
            var lines = Broadcast.Script(Broadcast.Kind.Redacted);
            c.Check(lines.Exists(l => l.Contains("NAMES OF THE DRIVERS WHO HELPED ARE CUT")) && lines.Exists(l => l.Contains("FOUR ARRIVED")), "the script cuts the names and keeps the figures");
            c.Check(Q7T.Say(c, "june", "WE'RE LIVE"), "on air");
            yield return Q7T.Step("A6", "release");
            yield return H.Until(() => Broadcast.Aired != null, 2f);
            c.Check(Broadcast.Aired == Broadcast.Kind.Redacted && MadMax.Story.Story.Flag("broadcast:redacted"), "remembered: broadcast redacted");
            c.Check(MadMax.Audio.RadioNetwork.FlashText != null && MadMax.Audio.RadioNetwork.FlashText.Contains("WITHHELD"), "WasteTalk carries the flash");
            c.Check(Q7T.Wrote("BROADCAST (REDACTED)"), "the written summary is in the journal");
            c.Check(g.Inventory.GetItem(StoryLibrary.A6Script) == 0, "the script went on air");

            // ---- Mara
            yield return Q7T.Meet(c, "q7_porch", 2.5f, "mara_home");
            c.Check(Q7T.Say(c, "mara_home", "YOU WANTED TO SAY"), "hear Mara out");
            yield return Q7T.Step("A6", "apology");
            c.Check(Q7T.Say(c, "mara_home", "STAY."), "she may stay");
            yield return Q7T.Done("A6", 6f);
            c.Check(MadMax.Story.Story.StateOf("A6") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("a6_done"), "A6 TELL IT STRAIGHT is done");
            c.Check(MadMax.Story.Story.Flag("mara_stays") && MadMax.Story.Story.Route("A6", "mara") == StoryLibrary.A6Stay, "Mara's place is remembered: " + MadMax.Story.Story.Route("A6", "mara"));
            c.Check(Q7T.Wrote("HELPERS' NAMES CUT") && Q7T.Wrote("BACK ROOM"), "the payoff tells how it went out and where Mara is");
            yield return new WaitForSeconds(2.5f);
            c.Check(g.CastBody("mara_home") != null && g.CastBody("wes_home") == null, "Mara stays on at the garage; Wes goes home");
        }
    }
}
