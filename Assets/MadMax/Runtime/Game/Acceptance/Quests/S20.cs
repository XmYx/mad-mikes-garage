using System.Collections;
using System.Collections.Generic;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S20(List<Scenario> into) => into.Add(new StoryS20());
    }

    /// <summary>S20 LOW CLOUDS, HIGH HOPES in a sandbox world: Oren's offer, the first logger fetched on foot, the second
    /// from his trike set down beside the station, the third from a fleet car; his reading, then Ida advised to wait. The
    /// way each logger came down is remembered; airstrips go on the map.</summary>
    class StoryS20 : Scenario
    {
        public override string Id => "story.s20";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            yield return Q3T.OnFoot(g);
            if (!c.Check(Q3T.Open("S20"), "S20 is on offer in a sandbox world")) yield break;
            var t = MadMax.World.DeformableTerrain.Instance;
            var town = StoryAnchors.Get("town1");
            c.Metric("ridge_rise", t.Height(StoryAnchors.Get("s20_i1").x, StoryAnchors.Get("s20_i1").z) - t.Height(town.x, town.z), "m");
            yield return Q3T.TalkAt(c, "oren", "oren", "WHAT ARE YOU WATCHING");
            yield return H.Until(() => Q3T.Active("S20"), 3f);
            if (!c.Check(Q3T.Active("S20"), "Oren's job is taken")) yield break;
            yield return H.Until(() => StoryTag.Find("s20_trike") != null, 4f);
            var trike = StoryTag.Find("s20_trike") ? StoryTag.Find("s20_trike").GetComponent<VehicleDriver>() : null;
            c.Check(trike && trike.GetComponent<FlightModel>(), "Oren's trike waits on the road");
            // 1: on foot
            yield return H.Walk(c, "s20_i1", 1.5f);
            yield return Q3T.Step(c, "S20", "i1", 4f, "the first logger");
            c.Check(Plot.Route("S20", "i1") == "ON FOOT", "fetched on foot");
            c.Screenshot("ridge");
            yield return null;
            // 2: by air, the trike set down beside the station
            if (trike)
            {
                var a2 = StoryAnchors.Get("s20_i2");
                yield return TestWorld.Place(c, trike, a2 + new Vector3(7f, 0f, 0f), Vector3.forward, 1f);
                g.Enter(trike);
                c.Fixture("flew Oren's trike to the second station (placed beside it)");
                yield return Q3T.Step(c, "S20", "i2", 5f, "the second logger");
                c.Check(Plot.Route("S20", "i2") == "BY AIR", "fetched from the cockpit");
                yield return Q3T.OnFoot(g);
            }
            // 3: by road
            var car = Q3T.GroundCar(g);
            if (!c.Check(car, "a ground vehicle in the fleet")) yield break;
            yield return TestWorld.Place(c, car, StoryAnchors.Get("s20_i3") + new Vector3(0f, 0f, 5f), Vector3.right, 1f);
            g.Enter(car);
            c.Fixture("drove the long way round to the third station (placed beside it)");
            yield return Q3T.Step(c, "S20", "i3", 5f, "the third logger");
            c.Check(Plot.Route("S20", "i3") == "BY ROAD", "fetched through the window");
            yield return Q3T.OnFoot(g);
            if (!trike) { yield return H.Walk(c, "s20_i2", 1.5f); yield return Q3T.Step(c, "S20", "i2", 4f, "the second logger (on foot: no trike)"); }
            c.Check(g.Inventory.GetItem("story_logger") == 3, "three loggers in the pack");
            // Oren's reading and Ida's run
            yield return Q3T.TalkAt(c, "oren", "oren", "THREE LOGGERS");
            yield return Q3T.Step(c, "S20", "read", 3f, "Oren reads the loggers");
            c.Check(g.Inventory.GetItem("story_logger") == 0, "the loggers went to Oren");
            yield return H.Until(() => Plot.Flag("s20_notes"), 3f);
            c.Check(Plot.Flag("s20_notes"), "Oren's navigation notes");
            int fuel = g.Inventory.Get(MadMax.Items.ResourceType.Fuel);
            yield return Q3T.TalkAt(c, "oren", "s20_ida", "WAIT TWO DAYS");
            yield return Q3T.Finished(c, "S20", "S20 LOW CLOUDS, HIGH HOPES is done");
            c.Check(g.Inventory.Get(MadMax.Items.ResourceType.Fuel) >= fuel + 20 && g.Inventory.GetItem("story_flight_booklet") > 0, "paid: fuel and the flight booklet");
            c.Check(Plot.Route("S20", "advise") != null && Plot.Route("S20", "advise").StartsWith("WAIT TWO DAYS"), "the advice to Ida is remembered");
            c.Check(StoryLibrary.Get("S20").payoff.Contains("WHATEVER WAS TO HAND"), "the payoff tells how the loggers came down");
        }
    }
}
