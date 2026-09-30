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
        static partial void Tests_F1(List<Scenario> into) { into.Add(new QuestF1LongWayHome()); into.Add(new QuestF1Routes()); }
    }

    /// <summary>Shared steps for the F1 scenarios.</summary>
    static class F1T
    {
        /// <summary>The campaign up to the finale (A1-A6, B1, B3, C1-C3) finished outright (a disclosed chapter skip).</summary>
        public static void Prereqs(ScenarioContext c) => ArcCT.Skip(c, "A1", "A2", "A3", "A4", "A5", "A6", "B1", "B3", "C1", "C2", "C3");

        /// <summary>The story control on a story prop at an anchor's local point.</summary>
        public static StoryControl Control(WastelandGame g, string anchor, string id, Vector3 local)
        {
            var p = g.Q7Prop(anchor, id, local, 2.5f);
            return p ? p.GetComponent<StoryControl>() : null;
        }

        public static bool Logged(string start) => Journal.Entries.Any(e => e.text.StartsWith(start));
    }

    /// <summary>F1 THE LONG WAY HOME in a story world, after a chapter skip to the finale: Ada's offer at the dispatch
    /// yard; the fuel week posted by need with Sera (live numbers, fixed when posted, the journal's can-be-met line);
    /// published terms put to Ada at her table, refused with a reason on one voice of support and signed once the earlier
    /// decisions stand behind the player (disclosed flags); the repaired road; the consequences shown and the autosave
    /// written before committing; the first load moved to the place with the biggest need (disclosed teleport); the
    /// ending published; home to the refuge, where the radio plays the driver's question and the player answers.</summary>
    class QuestF1LongWayHome : Scenario
    {
        public override string Id => "story.f1";
        public override float Timeout => 300f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            F1T.Prereqs(c);
            yield return new WaitForSeconds(1.5f);
            yield return H.Until(() => StoryState.StateOf("F1") != StoryState.State.Locked, 4f);
            if (!c.Check(StoryState.StateOf("F1") == StoryState.State.Open, "F1 opens once A6, B3 and C3 are done, and waits for the player")) yield break;
            c.Check(StoryAnchors.Has("f1_yard") && StoryAnchors.Has("dispatch"), "the dispatch yard is bound at " + StoryAnchors.Get("f1_yard"));

            // ---- the offer
            yield return ArcCT.Meet(c, "ada", 3f);
            c.Screenshot("yard");
            yield return null;
            c.Check(StoryLibrary.Get("F1").offerReply.Contains("EIGHT RUNNING TRUCKS AND TWELVE TOWNS"), "Ada's offer names her ugly answer");
            c.Check(ArcCT.Say(g, "ada", "YOU WANTED TO SEE ME"), "hear Ada's offer at the yard");
            yield return H.Until(() => StoryState.StateOf("F1") == StoryState.State.Active, 3f);
            if (!c.Check(StoryState.StateOf("F1") == StoryState.State.Active, "F1 runs")) yield break;
            yield return new WaitForSeconds(0.6f);
            c.Check(ArcCT.Prop(g, "fuel_pump", "f1_yard", 16f) && ArcCT.Prop(g, "water_tank", "f1_yard", 16f), "the yard's pumps and tanks stand");

            // ---- keep people supplied
            yield return ArcCT.Meet(c, "sera_f1", 3f);
            yield return new WaitForSeconds(0.6f);
            var byNeed = StoryLibrary.Q7Step("F1", "plan").any.First(a => a.topic == "f1_byneed").reply;
            c.Note("Sera: " + byNeed);
            c.Check(byNeed.Contains("LOADS THIS WEEK") && byNeed.Contains("THE CLINIC 3 OF 3") && byNeed.Contains("CAN BE MET"), "Sera's by-need answer carries today's numbers, the clinic first");
            c.Check(ArcCT.Say(g, "sera_f1", "BY NEED"), "post the week by need");
            yield return H.Until(() => ArcCRecord.Has("f1:supply"), 4f);
            var shares = StoryLibrary.F1Shares();
            int supply = ArcCRecord.Get("f1:supply");
            c.Check(shares != null && shares.Sum() <= supply && shares[2] == 3, "posted: " + (shares != null ? string.Join("/", shares) : "-") + " of " + supply + " loads");
            c.Check(F1T.Logged("CAN BE MET"), "the journal says which commitments can actually be met");
            c.Check(g.Inventory.GetItem(StoryLibrary.F1Posted) == 1, "a copy of the posted week");
            int target = StoryLibrary.F1Target();
            c.Note("the first load goes to " + StoryLibrary.F1TargetName());

            // ---- take control of the decision: published terms at Ada's table
            StoryControl table = null;
            yield return H.Until(() => (table = F1T.Control(g, "f1_yard", "table", WastelandGame.F1TableAt)) != null, 3f);
            if (!c.Check(table, "Ada's table carries the negotiation")) yield break;
            string prompt = table.Prompt(g);
            c.Check(prompt != null && prompt.Contains("[E]"), "table: " + prompt);
            table.Use(g, false);
            yield return null;
            c.Check(StoryState.Flag("f1_neg_failed") && !StoryState.StepDone("F1", "control") && F1T.Logged("THE TABLE: ADA COUNTS"),
                    "on one voice Ada says no and the journal says why; the stoppage and the yard stay open");
            StoryState.SetFlag("mara_stays"); StoryState.SetFlag("broadcast:redacted"); StoryState.SetFlag("refuge");
            c.Fixture("earlier decisions as flags: Mara stayed, June aired the redacted records, the garage is a refuge");
            table.Use(g, false);
            yield return ArcCT.Done("F1", "control");
            c.Check(StoryState.Route("F1", "control") == StoryLibrary.F1Terms, "control: " + StoryState.Route("F1", "control"));

            // ---- the route
            yield return ArcCT.Meet(c, "sera_f1", 3f);
            c.Check(ArcCT.Say(g, "sera_f1", "THE ROAD"), "the repaired road");
            yield return ArcCT.Done("F1", "route");
            c.Check(StoryState.Route("F1", "route") == StoryLibrary.F1Road, "route: " + StoryState.Route("F1", "route"));

            // ---- commit: consequences, then a save, then the run
            StoryControl board = null;
            yield return H.Until(() => (board = F1T.Control(g, "f1_yard", "sign", WastelandGame.F1BoardAt)) != null, 3f);
            if (!c.Check(board, "the dispatch board")) yield break;
            c.Note("board: " + board.Prompt(g));
            board.Use(g, false);
            yield return null;
            c.Check(F1T.Logged("IF YOU COMMIT: A BETTER BARGAIN") && F1T.Logged("A LOST LOAD IS REPLACED"), "the consequences are shown before committing");
            var before = System.DateTime.Now.AddSeconds(-2);
            board.Use(g, true);
            yield return ArcCT.Done("F1", "commit");
            c.Check(StoryState.StepDone("F1", "commit"), "committed");
            c.Check(SaveSystem.Exists(0) && System.IO.File.GetLastWriteTime(SaveSystem.PathOf(0)) >= before, "a recoverable save was written before the run (autosave slot)");

            // ---- make the run
            yield return H.Until(() => ArcCT.Tagged("f1_load") != null, 5f);
            var load = ArcCT.Tagged("f1_load");
            if (!c.Check(load, "the first load waits in the yard")) yield break;
            var drop = StoryAnchors.Get("f1_drop");
            c.Check(ArcCT.Flat(drop, StoryAnchors.Get(StoryLibrary.F1DropAnchor(target))) < 1f, "the drop is at " + StoryLibrary.F1TargetName());
            yield return H.Walk(c, "f1_drop", 8f);
            ArcCT.Put(c, load, drop + new Vector3(3f, 0f, 3f), Vector3.forward, "driven to " + StoryLibrary.F1TargetName() + " by the road");
            yield return ArcCT.Done("F1", "run", 6f);
            c.Check(StoryState.StepDone("F1", "run") && StoryState.Route("F1", "route") == StoryLibrary.F1Road, "the first load is in, by the road");
            yield return H.Until(() => StoryState.Flag("campaign_done"), 5f);
            c.Check(StoryState.Flag("f1_ending_better_bargain") && (StoryState.Flag("f1_strong") || StoryState.Flag("f1_weak")), "A BETTER BARGAIN (" + (StoryState.Flag("f1_strong") ? "strong" : "weak") + ")");
            c.Check(F1T.Logged("A BETTER BARGAIN") && F1T.Logged("WHAT STAYS HARD") && F1T.Logged("HOW IT WAS WEIGHED"), "the consequences are published in the journal");

            // ---- the last scene
            yield return H.Walk(c, "garage_yard", 3f);
            yield return ArcCT.Done("F1", "home", 5f);
            StoryControl radio = null;
            yield return H.Until(() => (radio = F1T.Control(g, "garage_yard", "table", WastelandGame.F1RadioTableAt)) != null, 4f);
            if (!c.Check(radio, "the radio table at the refuge")) yield break;
            yield return H.Until(() => StoryState.Flag("f1_call_played"), 3f);
            c.Screenshot("radio");
            yield return null;
            c.Check(Journal.Entries.Any(e => e.text.Contains("CAN STOP FOR THE NIGHT")), "the receiver plays the driver's question");
            radio.Use(g, false);
            yield return H.Until(() => StoryState.StateOf("F1") == StoryState.State.Done, 5f);
            c.Check(StoryState.StateOf("F1") == StoryState.State.Done && StoryState.Flag("f1_done") && StoryState.Route("F1", "answer") == StoryLibrary.F1Answer,
                    "THE LONG WAY HOME is done: " + StoryState.Route("F1", "answer"));
            var payoff = StoryLibrary.Get("F1").payoff;
            c.Check(payoff.Contains("A BETTER BARGAIN") && payoff.Contains("YOU TOLD HER YES") && Journal.Entries.Any(e => e.text == payoff), "the closing line: " + payoff);
        }
    }

    /// <summary>F1's other two control routes resolve to their endings: after the week is posted (noted directly), a work
    /// stoppage called at the dispatch board (arc C's outcome set as flags) leads to OPEN ROADS; the story state is then
    /// rewound to the posted week (a snapshot, disclosed) and the yard is taken: a pump smashed in the fight fails the
    /// approach with a recoverable message, repairing it takes the yard, and that leads to BREAK THE LOCKS. The run
    /// itself is noted directly (story.f1 drives it).</summary>
    class QuestF1Routes : Scenario
    {
        public override string Id => "story.f1_routes";
        public override float Timeout => 240f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        static IEnumerator RunOut(ScenarioContext c)
        {
            StoryState.Note("talk:sera_f1:f1_road");
            yield return ArcCT.Done("F1", "route");
            StoryState.Note("f1:committed");
            yield return ArcCT.Done("F1", "commit");
            StoryState.Note("f1:delivered");
            c.Fixture("route, commitment and delivery noted directly");
            yield return H.Until(() => StoryState.Flag("campaign_done"), 6f);
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            F1T.Prereqs(c);
            foreach (var f in new[] { "arc_c_done", "c5_route_open", "mara_travels", "broadcast:full", "refuge" }) StoryState.SetFlag(f);
            c.Fixture("earlier outcomes as flags: arc C concluded (Sera's drivers, the convoy route), Mara travels, a full broadcast, the refuge");
            yield return new WaitForSeconds(1.5f);
            yield return H.Until(() => StoryState.StateOf("F1") != StoryState.State.Locked, 4f);
            StoryState.Activate(g, "F1");
            c.Fixture("Ada's offer taken (F1 activated directly)");
            yield return new WaitForSeconds(0.6f);
            StoryState.Note("talk:sera_f1:f1_byneed");
            c.Fixture("the week posted by need (noted directly)");
            yield return H.Until(() => ArcCRecord.Has("f1:supply"), 4f);
            if (!c.Check(ArcCRecord.Has("f1:supply"), "the week is posted: " + ArcCRecord.Get("f1:supply") + " loads")) yield break;
            var snapshot = StoryState.Save();

            // ---- a work stoppage
            yield return H.Walk(c, "f1_yard", 2f);
            StoryControl board = null;
            yield return H.Until(() => (board = F1T.Control(g, "f1_yard", "sign", WastelandGame.F1BoardAt)) != null, 3f);
            if (!c.Check(board, "the dispatch board")) yield break;
            board.Use(g, false);
            yield return ArcCT.Done("F1", "control");
            c.Check(StoryState.Route("F1", "control") == StoryLibrary.F1Stop, "control: " + StoryState.Route("F1", "control"));
            yield return RunOut(c);
            c.Check(StoryState.Flag("f1_ending_open_roads") && !StoryState.Flag("f1_ending_better_bargain") && !StoryState.Flag("f1_ending_break_locks"), "a work stoppage ends in OPEN ROADS");

            // ---- rewind and take the yard
            StoryState.Load(snapshot);
            c.Fixture("story state rewound to the posted week (Story.Load of a snapshot)");
            c.Check(!StoryState.Flag("campaign_done") && !StoryState.StepDone("F1", "control"), "rewound");
            yield return ArcCT.Meet(c, "f1_guard1", 4f);
            yield return H.Until(() => g.CastBody("f1_guard2") != null, 4f);
            var pump = ArcCT.Prop(g, "fuel_pump", "f1_yard", 20f);
            if (!c.Check(pump, "a fuel pump at the yard")) yield break;
            pump.ApplyHit(pump.transform.position + Vector3.up, Vector3.down, 100f, 1f, null);
            c.Fixture("a fuel pump smashed in the fight");
            yield return new WaitForSeconds(0.5f);
            foreach (var k in new[] { "f1_guard1", "f1_guard2" })
            {
                var b = g.CastBody(k);
                if (b) b.ApplyHit(b.transform.position + Vector3.up, Vector3.forward, 50f, 0.5f, g.Player.gameObject);
            }
            c.Fixture("the yard guards knocked down");
            yield return H.Until(() => StoryState.Flag("f1_seize_blocked"), 5f);
            c.Check(StoryState.Flag("f1_seize_blocked") && !StoryState.StepDone("F1", "control") && F1T.Logged("TAKING THE YARD FAILS"),
                    "a wrecked pump fails the approach with a recoverable message");
            foreach (var t in new[] { ResourceType.Iron, ResourceType.Scrap, ResourceType.Glass, ResourceType.Copper, ResourceType.Aluminium }) g.Inventory.Add(t, 30);
            c.Fixture("materials for the repairs");
            var yard = StoryAnchors.Get("f1_yard");
            foreach (var p in Placeable.All.ToList())
            {
                if (!p || !g.IsStoryProp(p) || (p.id != "fuel_pump" && p.id != "water_tank") || ArcCT.Flat(p.transform.position, yard) > 20f) continue;
                var def = FurnitureLibrary.Get(p.id);
                if (def != null && p.hits < def.hits) g.Build.Repair(p);
            }
            yield return ArcCT.Done("F1", "control", 5f);
            c.Check(StoryState.Route("F1", "control") == StoryLibrary.F1Seize, "repaired, the yard is taken: " + StoryState.Route("F1", "control"));
            yield return RunOut(c);
            c.Check(StoryState.Flag("f1_ending_break_locks") && !StoryState.Flag("f1_ending_open_roads"), "taking the yard ends in BREAK THE LOCKS");
        }
    }
}
