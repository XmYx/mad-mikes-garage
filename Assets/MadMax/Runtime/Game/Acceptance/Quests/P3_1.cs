using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_P3_1(List<Scenario> into) => into.Add(new StoryP3Chain());
    }

    /// <summary>P3 THE SEA DOES NOT KEEP RECEIPTS in a sandbox world, all three stages: the trawler's split seam found in
    /// the hold, patched with a repair kit, proved with a catch (P3.1); Halvard's helmet and tank, both wrecks surveyed,
    /// the deep Meridian's chart locker salvaged on foot on the sea floor (P3.2); Iris Marrow gets her grandfather's
    /// letters back, the honest route earns the mooring (P3.3).</summary>
    class StoryP3Chain : Scenario
    {
        public override string Id => "story.p3";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            DayNight.SetHours(10f);
            c.Fixture("clock set to 10:00");
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("P3.1") == MadMax.Story.Story.State.Open, "P3.1 is on offer in a sandbox world");
            foreach (var a in new[] { "p3_harbour", "p3_trawler", "p3_shallow", "p3_deep", "p3_heir" }) c.Check(StoryAnchors.Has(a), "anchor " + a + " is placed");
            c.Metric("harbour_from_town", Vector3.Distance(StoryAnchors.Get("p3_harbour"), StoryAnchors.Get("town1")), "m");
            var w = g.World; var hs = StoryAnchors.Get("p3_shallow"); var hd = StoryAnchors.Get("p3_deep");
            c.Metric("alba_depth", WorldGen.SeaLevel - w.Sample(hs.x, hs.z).height, "m");
            c.Metric("meridian_depth", WorldGen.SeaLevel - w.Sample(hd.x, hd.z).height, "m");

            // ---- P3.1
            yield return H.Walk(c, "p3_harbour", 2.5f);
            yield return H.Until(() => g.CastBody("trawler") != null && g.P3Boat() != null, 5f);
            if (!c.Check(g.CastBody("trawler"), "Halvard Ness on the shore")) yield break;
            var boat = g.P3Boat();
            if (!c.Check(boat && boat.GetComponent<VehicleDamage>().FrameDamage >= 0.4f, "his trawler is moored off the shore with a split seam")) yield break;
            c.Screenshot("harbour");
            yield return null;
            if (g.CastBody("p3_peg")) c.Check(H.Talk(g, g.CastBody("p3_peg"), "TELL ME ABOUT THE MONSTER"), "Peg's monster (before the job)");
            c.Check(H.Talk(g, g.CastBody("trawler"), "YOUR CREW LOOKS"), "Halvard's lost catch");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.1") == MadMax.Story.Story.State.Active, 3f);
            var sp = boat.transform.position + boat.transform.right * 4f; sp.y = WorldGen.SeaLevel - 1.2f;
            g.Player.Teleport(sp, 0f);
            c.Fixture("swam out to the trawler (teleport)");
            yield return H.Until(() => MadMax.Story.Story.Flag("p3_diagnosed"), 5f);
            c.Check(MadMax.Story.Story.Flag("p3_diagnosed"), "the hold: a split seam, no monster");
            yield return H.Walk(c, "p3_harbour", 2.5f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "IT'S NOT A MONSTER"), "tell Halvard");
            g.Inventory.AddItem("use_repair_kit", 1);
            c.Fixture("a repair kit made at a workbench");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P3.1", "diagnose"), 4f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "HERE'S A REPAIR KIT"), "hand Halvard the repair kit");
            yield return H.Until(() => MadMax.Story.Story.Flag("p3_seam_fixed"), 4f);
            c.Check(g.Inventory.GetItem("use_repair_kit") == 0 && boat.GetComponent<VehicleDamage>().FrameDamage < 0.1f, "the seam is patched; the kit is used");
            var hold = boat.GetComponent<Container>();
            if (hold) hold.inventory.AddItem("food_fish_raw", 3);
            c.Fixture("three fish trawled into the hold (the net at 4-14 km/h over deep water)");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.1") == MadMax.Story.Story.State.Done, 5f);
            c.Check(MadMax.Story.Story.StateOf("P3.1") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Route("P3.1", "proof") == "TRAWLED A CATCH", "P3.1 done: the catch stays in the hold");
            c.Check(StoryLibrary.Get("P3.1").payoff.StartsWith("A REPAIR KIT"), "the repair route is remembered");

            // ---- P3.2: deep
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.2") == MadMax.Story.Story.State.Open, 3f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "YOU SAID SOMETHING ABOUT WRECKS"), "two wrecks");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.2") == MadMax.Story.Story.State.Active, 3f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "I'LL TAKE THE HELMET"), "borrow the helmet and tank");
            yield return H.Until(() => g.Inventory.GetItem("cloth_dive_helmet") > 0, 4f);
            g.Player.Rig.Wear("dive_helmet"); g.Player.Rig.Wear("air_tank");
            c.Fixture("put on the helmet and the tank");
            c.Check(g.DiveReady, "ready to walk the bottom");
            yield return H.Until(() => MadMax.Story.Story.Flag("hook:P3.2"), 3f);
            yield return H.Walk(c, "p3_shallow", 1.5f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("P3.2", "alba"), 4f);
            c.Screenshot("alba");
            yield return null;
            yield return H.Walk(c, "p3_deep", 1.5f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("P3.2", "survey"), 4f);
            c.Check(MadMax.Story.Story.StepDone("P3.2", "survey"), "both wrecks surveyed");
            c.Screenshot("meridian");
            yield return null;
            var locker = Placeable.All.Select(p => p ? p.GetComponent<Container>() : null).FirstOrDefault(b => b && b.title == "MERIDIAN'S CHART LOCKER");
            if (!c.Check(locker && locker.inventory.GetItem("story_p3_instruments") > 0, "the Meridian's chart locker on the sea floor")) yield break;
            foreach (var id in new[] { "story_p3_instruments", "story_p3_letters" }) if (locker.inventory.TakeItem(id)) g.Inventory.AddItem(id);
            c.Fixture("taken from the chart locker ([E] under water)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P3.2", "salvage"), 4f);
            c.Check(StoryLibrary.P3Deep(MadMax.Story.Story.Route("P3.2", "salvage")), "the deep route is remembered: " + MadMax.Story.Story.Route("P3.2", "salvage"));
            yield return H.Walk(c, "p3_harbour", 2.5f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "THE WRECKS ARE SURVEYED"), "bring word to Halvard");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.2") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("P3.2") == MadMax.Story.Story.State.Done, "P3.2 SHALLOW OR DEEP is done");

            // ---- P3.3: the letters go back
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.3") == MadMax.Story.Story.State.Open, 3f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "WHO'S THE WOMAN"), "who owns a wreck");
            yield return H.Walk(c, "p3_heir", 2.5f);
            yield return H.Until(() => g.CastBody("p3_heir") != null, 5f);
            if (!c.Check(g.CastBody("p3_heir"), "Iris Marrow above the harbour")) yield break;
            c.Check(H.Talk(g, g.CastBody("p3_heir"), "YOUR FAMILY OWNED"), "hear Iris out");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P3.3", "iris"), 4f);
            c.Check(H.Talk(g, g.CastBody("p3_heir"), "THESE WERE IN THE MERIDIAN"), "return her grandfather's letters");
            yield return H.Until(() => MadMax.Story.Story.Flag("p3_settled"), 4f);
            c.Check(g.Inventory.GetItem("story_p3_letters") == 0 && MadMax.Story.Story.Flag("p3_mooring") && !MadMax.Story.Story.Flag("p3_dishonest"), "the letters went back; the mooring is yours");
            yield return H.Walk(c, "p3_harbour", 2.5f);
            c.Check(H.Talk(g, g.CastBody("trawler"), "IT'S SETTLED"), "tell Halvard");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P3.3") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("P3.3") == MadMax.Story.Story.State.Done && StoryLibrary.Get("P3.3").payoff.StartsWith("IRIS MARROW HAS"), "P3.3 WHO OWNS A WRECK is done; the settlement is remembered");
        }
    }
}
