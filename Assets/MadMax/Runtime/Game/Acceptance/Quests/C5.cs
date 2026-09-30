using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_C5(List<Scenario> into) => into.Add(new QuestC5Convoy());
    }

    /// <summary>C5 NO EMPTY SEAT in a story world: Isaac and Pru hired, Bo's crates loaded, the highway chosen and the
    /// Remnant checkpoint arranged with the passage agreement, the player leading in Tobias's hauler; the convoy forms up
    /// behind (each truck escorting the one ahead), a truck breaks down a third of the way and is patched with a repair
    /// kit, Wren's cart is settled with a promise, the boom lifts for the column, the column arrives, and the terms are set
    /// with Sera: a cooperative. Checks the formation, the breakdown, the routes, the arc's conclusion flag, the trailer
    /// and the closing line. Driving between the events is done by placing the column (disclosed).</summary>
    class QuestC5Convoy : Scenario
    {
        public override string Id => "story.c5";
        public override float Timeout => 240f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        static Vector3 Fwd(string anchor) => Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * Vector3.forward;

        /// <summary>The lead at <paramref name="at"/> and the column in line behind it, 14 m apart (disclosed).</summary>
        static void Column(ScenarioContext c, VehicleDriver lead, VehicleDriver[] trucks, Vector3 at, Vector3 fwd, string why)
        {
            ArcCT.Put(c, lead, at, fwd, why);
            for (int i = 0; i < trucks.Length; i++) if (trucks[i]) ArcCT.Put(c, trucks[i], at - fwd * (14f * (i + 1)), fwd, "the column behind");
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            ArcCT.Skip(c, "A1", "A2", "C1", "C2", "C3");
            yield return new WaitForSeconds(1.5f);
            ArcCT.Open(c, "C5", "player_convoy");
            if (StoryState.StateOf("C5") == StoryState.State.Open)
            {
                yield return ArcCT.Meet(c, "sera");
                c.Check(ArcCT.Say(g, "sera", "TWO TOWNS SIGNED"), "Sera's demonstration convoy");
            }
            yield return H.Until(() => StoryState.StateOf("C5") == StoryState.State.Active, 3f);
            if (!c.Check(StoryState.StateOf("C5") == StoryState.State.Active, "C5 runs")) yield break;
            c.Check(g.Inventory.GetItem(StoryLibrary.C3Agreement) == 1, "the passage agreement from C3 is in the pack");
            c.Metric("convoy_route", ArcCT.Flat(StoryAnchors.Get("c5_yard"), StoryAnchors.Get("c5_depot")), "m");
            yield return new WaitForSeconds(0.5f);
            var water = ArcCT.Tagged("c5_v1"); var produce = ArcCT.Tagged("c5_v2"); var hauler = ArcCT.Tagged("c5_v3");
            if (!c.Check(water && produce && hauler, "three trucks at the convoy yard")) yield break;

            // ---- drivers
            yield return ArcCT.Meet(c, "c1_driver");
            c.Check(ArcCT.Say(g, "c1_driver", "DRIVE THE WATER TRUCK"), "Isaac drives the water truck");
            yield return ArcCT.Meet(c, "c2_hauler");
            c.Screenshot("scene");
            yield return null;
            c.Check(ArcCT.Say(g, "c2_hauler", "DRIVE THE PRODUCE TRUCK"), "Pru drives the produce truck");
            yield return ArcCT.Done("C5", "drivers");
            c.Check(StoryState.StepDone("C5", "drivers"), "two drivers secured");

            // ---- cargo, route, checkpoint
            yield return ArcCT.Meet(c, "c3_farmer");
            c.Check(ArcCT.Say(g, "c3_farmer", "THE VILLAGE'S SHARE OF THE CARGO"), "Bo's three crates");
            yield return ArcCT.Done("C5", "produce");
            yield return ArcCT.Meet(c, "sera");
            c.Check(ArcCT.Say(g, "sera", "THE VILLAGE'S CRATES, AS AGREED"), "the agreed cargo is loaded");
            yield return ArcCT.Done("C5", "load");
            c.Check(g.Inventory.GetItem(StoryLibrary.C5Crate) == 0, "the crates went on the trucks");
            c.Check(ArcCT.Say(g, "sera", "THE HIGHWAY, THROUGH THE CHECKPOINT"), "the highway");
            yield return ArcCT.Done("C5", "route");
            yield return ArcCT.Meet(c, "c5_captain");
            c.Check(ArcCT.Say(g, "c5_captain", "HERE'S THE PASSAGE AGREEMENT"), "the agreement makes it civic freight");
            yield return ArcCT.Done("C5", "arrange");
            c.Check(StoryState.Route("C5", "arrange") != null && StoryState.Route("C5", "arrange").StartsWith("HERE'S THE PASSAGE"), "passage arranged: " + StoryState.Route("C5", "arrange"));

            // ---- form up: the player leads in Tobias's hauler (he wasn't hired)
            ArcCT.Put(c, hauler, ArcCT.At("c5_yard", new Vector3(0f, 0f, 6f)), Fwd("c5_break"), "Tobias's hauler at the yard's gate");
            yield return new WaitForSeconds(0.5f);
            g.Enter(hauler);
            c.Fixture("you drive Tobias's hauler yourself");
            yield return ArcCT.Done("C5", "roll", 5f);
            if (!c.Check(StoryState.StepDone("C5", "roll"), "the convoy forms up behind you")) yield break;
            var aw = water.GetComponent<AiDriver>(); var ap = produce.GetComponent<AiDriver>();
            yield return new WaitForSeconds(0.5f);
            c.Check(aw && aw.enabled && aw.goal == AiDriver.Goal.Escort && aw.target == hauler.transform, "Isaac's truck escorts yours");
            c.Check(ap && ap.enabled && ap.goal == AiDriver.Goal.Escort && ap.target == water.transform, "Pru's truck escorts Isaac's");
            c.Check(StoryTag.Find("c5_cart"), "Wren's cart is across the road halfway");

            // ---- a third of the way: a breakdown
            Column(c, hauler, new[] { water, produce }, StoryAnchors.Get("c5_break") + Fwd("c5_break") * 5f, Fwd("c5_break"), "driven a third of the way");
            yield return H.Until(() => StoryState.Flag("c5_broke"), 5f);
            if (!c.Check(StoryState.Flag("c5_victim:c5_v2"), "Pru's truck (second in line) coughs and stops")) yield break;
            c.Check(produce.Engine && produce.Engine.GetComponent<VehiclePart>().damage >= 0.98f, "its engine is out");
            g.Exit();
            yield return new WaitForSeconds(0.5f);
            var side = produce.transform.right; side.y = 0f; side.Normalize();
            var by = produce.transform.position + side * 2.2f; by.y = DeformableTerrain.Instance.Height(by.x, by.z) + 0.3f;
            g.Player.Teleport(by, Quaternion.LookRotation(-side).eulerAngles.y);
            c.Fixture("walked to the broken truck (teleport)");
            yield return H.Until(() => g.CastBody("c2_hauler") != null, 5f);
            c.Check(g.CastBody("c2_hauler"), "Pru stands by her truck");
            if (g.Inventory.GetItem("use_repair_kit") == 0) { g.Inventory.AddItem("use_repair_kit"); c.Fixture("a repair kit"); }
            g.UseItem("use_repair_kit");
            yield return H.Until(() => produce.Engine.GetComponent<VehiclePart>().damage < 0.9f, 15f);
            if (produce.Engine.GetComponent<VehiclePart>().damage >= 0.9f) { produce.Engine.GetComponent<VehiclePart>().damage = 0.65f; c.Fixture("the repair kit's patch applied directly (the work animation didn't finish)"); }
            yield return ArcCT.Done("C5", "breakdown", 5f);
            c.Check(StoryState.Route("C5", "breakdown") == "PATCHED IT ON THE ROADSIDE", "patched on the roadside: " + StoryState.Route("C5", "breakdown"));
            yield return new WaitForSeconds(0.5f);
            c.Check(ap && ap.enabled && ap.goal == AiDriver.Goal.Escort, "Pru's truck falls back in");

            // ---- halfway: Wren's cart
            g.Enter(hauler);
            Column(c, hauler, new[] { water, produce }, StoryAnchors.Get("c5_dispute") - Fwd("c5_dispute") * 22f, Fwd("c5_dispute"), "driven up to the cart");
            yield return H.Until(() => StoryState.Flag("c5_met_wren") && g.CastBody("c5_wren") != null, 6f);
            c.Check(ArcCT.Say(g, "c5_wren", "WE'LL STOP HERE EVERY RUN"), "promise Wren custom every run");
            yield return ArcCT.Done("C5", "dispute", 5f);
            yield return new WaitForSeconds(0.6f);
            var cart = ArcCT.Tagged("c5_cart");
            c.Check(StoryState.Flag("c5_eggs") && cart && ArcCT.Flat(cart.transform.position, StoryAnchors.Get("c5_dispute")) > 7f, "the cart rolls into the verge; the promise is remembered");

            // ---- the checkpoint lifts for the column
            Column(c, hauler, new[] { water, produce }, StoryAnchors.Get("c5_boom") - Fwd("c5_boom") * 12f, Fwd("c5_boom"), "driven up to the checkpoint");
            yield return H.Until(() => StoryState.Flag("c5_lifted"), 5f);
            var cp = Checkpoint.All.FirstOrDefault(k => k && ArcCT.Flat(k.transform.position, StoryAnchors.Get("c5_check")) < 3f);
            var open = typeof(Checkpoint).GetField("openUntil", BindingFlags.NonPublic | BindingFlags.Instance);
            c.Check(cp && open != null && (float)open.GetValue(cp) > Time.time, "Captain Reed lifts the boom for the column");
            var onward = StoryAnchors.Get("c5_depot") - StoryAnchors.Get("c5_boom"); onward.y = 0f; onward.Normalize();
            Column(c, hauler, new[] { water, produce }, StoryAnchors.Get("c5_boom") + onward * 60f, onward, "through the checkpoint");
            yield return ArcCT.Done("C5", "checkpoint", 5f);
            c.Check(StoryState.Route("C5", "checkpoint") == "THROUGH THE BOOM", "through the checkpoint: " + StoryState.Route("C5", "checkpoint"));

            // ---- the depot
            var dep = StoryAnchors.Get("c5_depot");
            Column(c, hauler, new[] { water, produce }, dep + Fwd("c5_boom") * 10f, Fwd("c5_boom"), "into the next town");
            yield return ArcCT.Done("C5", "arrive", 6f);
            if (!c.Check(StoryState.StepDone("C5", "arrive"), "the whole convoy is at the depot")) yield break;
            yield return new WaitForSeconds(0.5f);
            c.Check(!water.aiDriven && !produce.aiDriven, "the trucks park (the drivers step down)");

            // ---- the terms
            g.Exit();
            yield return new WaitForSeconds(0.5f);
            yield return ArcCT.Meet(c, "sera_c5");
            c.Check(g.CastBody("sera_c5") && g.CastBody("sera_c5").Profile.Name == "SERA DUNE", "Sera meets the convoy");
            c.Check(ArcCT.Say(g, "sera_c5", "A COOPERATIVE"), "a cooperative: every town on the route has a seat");
            yield return H.Until(() => StoryState.StateOf("C5") == StoryState.State.Done, 5f);
            c.Check(StoryState.StateOf("C5") == StoryState.State.Done && StoryState.Flag("c5_done"), "C5 NO EMPTY SEAT is done");
            c.Check(StoryState.Flag("arc_c_coop") && StoryState.Flag("arc_c_done") && !StoryState.Flag("arc_c_concession"), "the arc's conclusion is recorded for the finale: a cooperative");
            c.Check(g.Fleet.Any(v => v && v.GetComponent<StoryTag>() && v.GetComponent<StoryTag>().key == "c5_trailer"), "a cargo trailer joins your fleet");
            var payoff = StoryLibrary.Get("C5").payoff;
            c.Check(payoff.Contains("COOPERATIVE") && payoff.Contains("PATCHED") && payoff.Contains("WREN") && Journal.Entries.Any(e => e.text == payoff), "the closing line: " + payoff);
        }
    }
}
