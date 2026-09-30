using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_A5(List<Scenario> into) => into.Add(new QuestA5());
    }

    /// <summary>A5 NO ONE RIDES IN THE BACK in a STORY world: the transfer yard and its box trailer, Mara at the door,
    /// Wes stabilised with a first aid kit, everyone at once, the transfer pump repaired and traded for the contracts,
    /// Mara's list read live (the stranded car's seats, water, medicine), the go at the trailer door, two trips through
    /// the staging point, and the survivors' choice at the garage. Checks the routes are remembered and the schedule
    /// becomes evidence.</summary>
    class QuestA5 : Scenario
    {
        public override string Id => "story.a5";
        public override float Timeout => 240f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return Q7T.Open(c, "A5", new[] { "A1", "A2", "B1", "A3", "A4" });
            if (!c.Check(MadMax.Story.Story.StateOf("A5") == MadMax.Story.Story.State.Active, "A5 runs after A4 (no giver: the survivor's message)")) yield break;
            c.Check(StoryAnchors.Has("a5_yard") && StoryAnchors.Has("a5_staging") && StoryAnchors.Has("q7_porch"), "the transfer yard, the staging point and the garage porch are bound");
            c.Metric("yard_to_town", Vector3.Distance(StoryAnchors.Get("a5_yard"), StoryAnchors.Get("town1")), "m");
            c.Metric("yard_to_staging", Vector3.Distance(StoryAnchors.Get("a5_yard"), StoryAnchors.Get("a5_staging")), "m");

            // ---- the yard
            yield return H.Walk(c, "a5_yard", 4f);
            yield return Q7T.Step("A5", "yard");
            c.Check(MadMax.Story.Story.StepDone("A5", "yard"), "found the transfer yard");
            c.Check(StoryTag.Find("a5_trailer") != null, "a box trailer at the yard (the people are in the back)");
            var door = Q7T.Prop(c, "a5_trailer", "post", 3f);
            c.Check(door && door.GetComponent<StoryControl>() != null, "the trailer door carries the go control");
            c.Check(door && door.GetComponent<StoryControl>().Prompt(g).Contains("LOCKED"), "the door is locked and watched until there's a way out");
            yield return Q7T.Meet(c, "a5_trailer", 2f, "mara_yard");
            c.Screenshot("yard");
            yield return null;
            if (!c.Check(Q7T.Say(c, "mara_yard", "MARA. IT'S ME"), "Mara Vale at the trailer door")) yield break;
            yield return Q7T.Step("A5", "mara");
            c.Check(g.CastBody("mara_yard").Profile.Name == "MARA VALE", "her name is her own");

            // ---- Wes: a first aid kit
            g.Inventory.AddItem("med_firstaid");
            c.Fixture("a first aid kit in the pack");
            yield return Q7T.Meet(c, "a5_hut", 2.5f, "a5_wes");
            yield return H.Until(() => g.CastBody("a5_wes") && g.CastBody("a5_wes").Berthed, 3f);
            c.Check(g.CastBody("a5_wes") && g.CastBody("a5_wes").Berthed, "Wes lies on the cot in the crew hut");
            c.Check(Q7T.Say(c, "a5_wes", "HOLD STILL"), "stabilise Wes with the kit");
            yield return Q7T.Step("A5", "wes");
            yield return new WaitForSeconds(0.6f);
            c.Check(MadMax.Story.Story.Route("A5", "wes") == StoryLibrary.A5Kit && g.Inventory.GetItem("med_firstaid") == 0, "route: " + MadMax.Story.Story.Route("A5", "wes") + "; the kit is used up");

            // ---- everyone at once
            yield return Q7T.Meet(c, "a5_trailer", 2f, "mara_yard");
            c.Check(Q7T.Say(c, "mara_yard", "EVERYONE."), "everyone at once (Mara says what it costs)");
            yield return Q7T.Step("A5", "plan");
            yield return H.Until(() => g.Inventory.GetItem(StoryLibrary.A5PlanAll) > 0, 2f);
            c.Check(g.Inventory.GetItem(StoryLibrary.A5PlanAll) > 0, "Mara's list: six seats, twelve litres, three dressings");

            // ---- the pump for the contracts (repair labour and what you know)
            var pump = Q7T.Prop(c, "a5_yard", "fuel_pump", 10f);
            if (!c.Check(pump && pump.hits < pump.MaxHits, "the yard's transfer pump is broken")) yield break;
            g.Inventory.Add(ResourceType.Iron, 4); g.Inventory.Add(ResourceType.Scrap, 4); g.Inventory.Add(ResourceType.Glass, 2);
            c.Fixture("repair materials in the pack (iron, scrap, glass)");
            g.Build.Repair(pump);
            yield return Q7T.Step("A5", "pump");
            c.Check(g.Inventory.GetItem(StoryLibrary.A5Worksheet) > 0, "a signed work sheet for the repair");
            yield return Q7T.Meet(c, "a5_office", 2f, "a5_overseer");
            c.Check(Q7T.Say(c, "a5_overseer", "YOUR PUMP WORKS AGAIN"), "trade the repair and what you know about the depot");
            yield return Q7T.Step("A5", "way");
            c.Check(MadMax.Story.Story.Route("A5", "way") == StoryLibrary.A5Labour, "route: " + MadMax.Story.Story.Route("A5", "way"));

            // ---- Mara's list: the stranded car at the yard, water, medicine
            var car = g.Fleet.Count > 0 ? g.Fleet[0] : null;
            if (!c.Check(car, "the player's own car")) yield break;
            var yard = StoryAnchors.Get("a5_yard"); var yr = Quaternion.Euler(0f, StoryAnchors.Yaw("a5_yard"), 0f);
            yield return TestWorld.Place(c, car, yard + yr * new Vector3(-6f, 0f, 7f), yr * Vector3.right);
            yield return new WaitForSeconds(1.2f);
            c.Check(!MadMax.Story.Story.StepDone("A5", "ready"), "not ready without water and medicine");
            g.Inventory.Add(ResourceType.Water, 12); g.Inventory.AddItem("med_bandage", 3);
            c.Fixture("12 L of water and 3 bandages in the pack");
            yield return Q7T.Step("A5", "ready", 5f);
            c.Check(MadMax.Story.Story.StepDone("A5", "ready"), "the list is met: " + StoryLibrary.Q7Step("A5", "ready").text);
            c.Metric("seats_in_car", WastelandGame.Q7Seats(car), "");

            // ---- the go
            door = Q7T.Prop(c, "a5_trailer", "post", 3f);
            var ctl = door ? door.GetComponent<StoryControl>() : null;
            if (!c.Check(ctl && ctl.Prompt(g).Contains("[E] GO"), "the door offers the go once the list is met")) yield break;
            int water0 = g.Inventory.Get(ResourceType.Water), dressings0 = g.Inventory.GetItem("med_bandage");
            ctl.Use(g, false);
            yield return Q7T.Step("A5", "go");
            c.Check(MadMax.Story.Story.Flag("a5:started") && StoryTag.Find("a5_riders") != null, "the transfer runs; the car carries them");
            c.Check(water0 - g.Inventory.Get(ResourceType.Water) == 12 && dressings0 - g.Inventory.GetItem("med_bandage") == 3, "the water was drunk and the cuts dressed on the way (12 L, 3 dressings)");
            c.Check(Q7State.Number("a5:tload:") == 3 && Q7State.Number("a5:rest:") == 3, "three in the car, three wait at the staging point");
            yield return new WaitForSeconds(1.5f);
            c.Check(g.CastBody("mara_yard") == null, "the trailer is empty");

            // ---- two trips
            yield return H.Walk(c, "garage_yard", 6f);
            var gy = StoryAnchors.Get("garage_yard"); var gr = Quaternion.Euler(0f, StoryAnchors.Yaw("garage_yard"), 0f);
            yield return TestWorld.Place(c, car, gy + gr * new Vector3(-8f, 0f, -2f), gr * Vector3.right);
            yield return H.Until(() => MadMax.Story.Story.Flag("a5:trip1"), 4f);
            c.Check(MadMax.Story.Story.Flag("a5:trip1") && !MadMax.Story.Story.StepDone("A5", "deliver"), "first trip in; three still waiting");
            yield return Q7T.Meet(c, "a5_staging", 3f, "a5_tally");
            c.Check(g.CastBody("a5_tally") != null, "Tally waits at the staging point");
            var st = StoryAnchors.Get("a5_staging"); var sr = Quaternion.Euler(0f, StoryAnchors.Yaw("a5_staging"), 0f);
            yield return TestWorld.Place(c, car, st + sr * new Vector3(5f, 0f, 5f), sr * Vector3.right);
            yield return H.Until(() => MadMax.Story.Story.Flag("a5:trip2_loaded"), 4f);
            c.Check(MadMax.Story.Story.Flag("a5:trip2_loaded"), "the rest climb in at the staging point");
            yield return H.Walk(c, "garage_yard", 6f);
            yield return TestWorld.Place(c, car, gy + gr * new Vector3(-8f, 0f, -2f), gr * Vector3.right);
            yield return Q7T.Step("A5", "deliver", 5f);
            c.Check(MadMax.Story.Story.StepDone("A5", "deliver"), "everyone is at the garage");

            // ---- where they go
            yield return Q7T.Meet(c, "q7_porch", 2.5f, "mara_home");
            c.Screenshot("porch");
            yield return null;
            c.Check(Q7T.Say(c, "mara_home", "YOU'RE OUT"), "ask them where they'll go");
            yield return Q7T.Done("A5");
            c.Check(MadMax.Story.Story.StateOf("A5") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("a5_done"), "A5 NO ONE RIDES IN THE BACK is done");
            c.Check(g.Inventory.GetItem(StoryLibrary.A5Horn) > 0 && g.Inventory.GetItem(StoryLibrary.A5Schedule) > 0, "the convoy horn and Ada's allocation schedule");
            c.Check(MadMax.Story.Story.Evidence("schedule"), "the schedule is evidence");
            c.Check(MadMax.Story.Story.Route("A5", "plan") == StoryLibrary.A5All, "the choice is remembered: " + MadMax.Story.Story.Route("A5", "plan"));
            c.Check(Q7T.Wrote("WORK AND SILENCE") && Q7T.Wrote("NOBODY WAS LEFT BEHIND"), "the payoff tells how it was done");
            yield return new WaitForSeconds(2.5f);
            c.Check(g.CastBody("mara_home") != null, "Mara stays at the garage until A6 settles where she goes");
        }
    }
}
