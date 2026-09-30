using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_B4(List<Scenario> into) => into.Add(new QuestB4());
    }

    /// <summary>B4 WHO GETS A KEY? in a STORY world: Hester and Judd heard out, two beds and a door at the garage, Hester's
    /// objection, Nell remembers the pump, the pump comes home (a well behind the garage), tending crops as the service,
    /// a stocked pantry; then the residents at work: a dry bed watered by day, supper eaten from the stores at seven.</summary>
    class QuestB4 : Scenario
    {
        public override string Id => "story.b4";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return Q7T.Open(c, "B4", new[] { "A1", "B1", "B3" });
            if (!c.Check(MadMax.Story.Story.StateOf("B4") == MadMax.Story.Story.State.Active, "B4 runs after B3")) yield break;

            // ---- hear them out
            yield return Q7T.Meet(c, "b4_rooms", 3f, "b4_hester");
            yield return H.Until(() => g.CastBody("b4_judd") != null, 4f);
            c.Screenshot("step");
            yield return null;
            c.Check(Q7T.Say(c, "b4_hester", "YOU WANTED TO ASK ME"), "Hester Quayle asks for a door that locks");
            c.Check(Q7T.Say(c, "b4_judd", "AND WHAT ABOUT YOU"), "Judd Kessler, who used to ride with a gang");
            yield return Q7T.Step("B4", "heard");
            c.Check(MadMax.Story.Story.StepDone("B4", "heard"), "both heard out");

            // ---- rooms
            Q7T.Put(c, "garage", "bed", new Vector3(-2.6f, 0f, -1.8f));
            Q7T.Put(c, "garage", "bed", new Vector3(2.6f, 0f, -1.8f));
            Q7T.Put(c, "garage", "door_wood", new Vector3(4f, 0f, 0f), 90f);
            yield return Q7T.Step("B4", "rooms", 5f);
            c.Check(MadMax.Story.Story.Route("B4", "rooms") == StoryLibrary.B4Built, "rooms: " + MadMax.Story.Story.Route("B4", "rooms"));

            // ---- the objection and what really happened
            c.Check(Q7T.Say(c, "b4_hester", "SOMETHING'S WRONG"), "Hester objects to Judd");
            yield return Q7T.Step("B4", "objection");
            yield return Q7T.Meet(c, "nell", 3f, "nell");
            c.Check(Q7T.Say(c, "nell", "NELL, WHAT DO YOU KNOW ABOUT JUDD"), "Nell remembers the Quayle pump");
            yield return Q7T.Step("B4", "investigate");
            c.Check(MadMax.Story.Story.Route("B4", "investigate") == "NELL REMEMBERS THE PUMP", "investigated: " + MadMax.Story.Story.Route("B4", "investigate"));

            // ---- rules and a service
            yield return Q7T.Meet(c, "b4_rooms", 3f, "b4_hester");
            c.Check(Q7T.Say(c, "b4_hester", "JUDD FITS YOUR FATHER'S PUMP"), "the pump comes home and Judd fits it");
            yield return Q7T.Step("B4", "rules");
            yield return H.Until(() => MadMax.Story.Story.Flag("b4:pump"), 2f);
            c.Check(Q7T.Prop(c, "garage", "well", 12f) != null, "the Quayle pump on a well behind the garage");
            c.Check(Q7T.Say(c, "b4_hester", "TENDING CROPS"), "tending crops as the staffed service");
            yield return Q7T.Step("B4", "service");
            yield return new WaitForSeconds(0.6f);
            c.Check(Residents.Current == Residents.Service.Crops, "service: " + Residents.Name(Residents.Current));

            // ---- the pantry
            var chest = Q7T.Put(c, "garage", "chest", new Vector3(-3.2f, 0f, 1.8f));
            if (!c.Check(chest && chest.GetComponent<Container>(), "a chest in the garage")) yield break;
            chest.GetComponent<Container>().inventory.AddItem("food_can", 5);
            c.Fixture("5 tins in the chest");
            yield return Q7T.Done("B4", 6f);
            c.Check(MadMax.Story.Story.StateOf("B4") == MadMax.Story.Story.State.Done && Residents.Living, "B4 WHO GETS A KEY? is done: they live at the garage");
            c.Check(Q7T.Wrote("THE QUAYLE PUMP IS BACK") && Q7T.Wrote("PANTRY FED"), "the payoff tells the rules and the service");

            // ---- the residents at work and at supper
            var bed = Q7T.Put(c, "garage", "garden_plot", new Vector3(-6f, 0f, 5f));
            var plot = bed ? bed.GetComponent<GardenPlot>() : null;
            if (!c.Check(plot, "a garden bed by the garage")) yield break;
            plot.crop = "seed_corn"; plot.growth = 0.3f; plot.water = 0f; plot.health = 1f;
            c.Fixture("corn sown in a dry bed");
            DayNight.SetHours(9.5f);
            yield return null; yield return null;
            DayNight.SetHours(10.6f);
            c.Fixture("clock set to 09:30, then 10:36");
            yield return H.Until(() => plot.water > 0.5f, 3f);
            c.Check(plot.water > 0.5f, "Hester watered the dry bed (" + plot.water.ToString("0.00") + ")");
            yield return new WaitForSeconds(2.5f);
            c.Check(g.CastBody("b4_hester") != null && Vector3.Distance(g.CastBody("b4_hester").transform.position, StoryAnchors.Get("b4_work")) < 6f, "Hester is at work in the bay by day");
            int before = Residents.Portions();
            DayNight.SetHours(19.3f);
            c.Fixture("clock set to 19:18 (supper)");
            yield return H.Until(() => Residents.Portions() < before, 3f);
            c.Check(before - Residents.Portions() == Residents.Mouths, "supper: " + (before - Residents.Portions()) + " portions eaten from the stores");
        }
    }
}
