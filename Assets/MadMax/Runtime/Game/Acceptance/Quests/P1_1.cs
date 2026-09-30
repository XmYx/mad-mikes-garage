using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_P1_1(List<Scenario> into) => into.Add(new StoryP1Chain());
    }

    /// <summary>P1 NELL'S UNFINISHED CAR in a story world (A1 and B1 finished as disclosed fixtures), all three stages in
    /// order: catalogue the coupe and bolt on wheels and a radiator (P1.1); the ditch glovebox's hood ornament traded to
    /// Otis for Tom's straight six, mounted (P1.2); the service, Nell at the wheel with the player in the passenger
    /// seat to the lookout, a conversation, the drive home; the coupe stays Nell's (P1.3).</summary>
    class StoryP1Chain : Scenario
    {
        public override string Id => "story.p1";
        public override float Timeout => 330f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            MadMax.Story.Story.Complete(g, "A1"); MadMax.Story.Story.Complete(g, "B1");
            DayNight.SetHours(9f);
            c.Fixture("A1 and B1 finished (chapter skip); clock set to 9:00 (Nell sleeps at night and won't drive the lookout road in the dark)");
            yield return new WaitForSeconds(1.5f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("P1.1") == MadMax.Story.Story.State.Open, "P1.1 is on offer after B1");
            foreach (var a in new[] { "p1_car", "p1_pile", "p1_collector", "p1_ditch", "p1_view" }) c.Check(StoryAnchors.Has(a), "anchor " + a + " is placed");
            c.Metric("nell_to_lookout", Vector3.Distance(StoryAnchors.Get("nell"), StoryAnchors.Get("p1_view")), "m");

            // ---- P1.1 A SECOND PAIR OF HANDS
            yield return H.Walk(c, "nell", 3f);
            yield return H.Until(() => g.CastBody("nell") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("nell"), "WHAT'S UNDER THE TARP"), "Nell tells you about the coupe")) yield break;
            yield return H.Until(() => g.P1Car() != null, 4f);
            var car = g.P1Car();
            if (!c.Check(car, "Nell's coupe stands behind her stop")) yield break;
            var ch = car.GetComponent<VehicleChassis>();
            c.Check(!ch.FindSocket("wheel_rear_R").Current && !ch.FindSocket("wheel_rear_L").Current && !ch.FindSocket("engine").Current && !ch.FindSocket("radiator").Current,
                "a shell: no rear wheels, no radiator, no engine");
            c.Check(!g.Fleet.Contains(car), "the coupe is Nell's, not in your fleet");
            yield return H.Walk(c, "p1_car", 3f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("P1.1", "look"), 4f);
            c.Screenshot("coupe");
            yield return null;
            yield return H.Walk(c, "nell", 3f);
            c.Check(H.Talk(g, g.CastBody("nell"), "LET'S WRITE HER UP"), "catalogue the coupe with Nell");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P1.1", "catalogue"), 4f);
            c.Check(g.Inventory.GetItem("story_p1_list") > 0, "Nell's parts list");
            var pileWheel = VehiclePart.Registry.FirstOrDefault(p => p && !p.Socket && p.partId == "wheel_street" && Vector3.Distance(p.transform.position, StoryAnchors.Get("p1_pile")) < 8f);
            c.Check(pileWheel, "a worn wheel lies in Nell's pile");
            if (pileWheel) ch.FindSocket("wheel_rear_R").Attach(pileWheel);
            var w2 = g.SpawnPart("wheel_street", car.transform.position, Quaternion.identity); ch.FindSocket("wheel_rear_L").Attach(w2);
            var rad = g.SpawnPart("radiator_car", car.transform.position, Quaternion.identity); ch.FindSocket("radiator").Attach(rad);
            c.Fixture("pile wheel mounted; a second wheel and a radiator bought and mounted (the wrench's [E] mount)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P1.1", "radiator"), 5f);
            c.Check(MadMax.Story.Story.StepDone("P1.1", "wheels") && MadMax.Story.Story.StepDone("P1.1", "radiator"), "wheels and radiator noticed in their sockets");
            c.Check(H.Talk(g, g.CastBody("nell"), "SHE'S ON FOUR WHEELS"), "tell Nell");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P1.1") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("P1.1") == MadMax.Story.Story.State.Done, "P1.1 A SECOND PAIR OF HANDS is done");

            // ---- P1.2 THE WRONG ORIGINAL: trade Otis the ditch coupe's hood ornament
            yield return H.Until(() => MadMax.Story.Story.StateOf("P1.2") == MadMax.Story.Story.State.Open, 3f);
            c.Check(H.Talk(g, g.CastBody("nell"), "ABOUT THAT ENGINE"), "Nell names the collector");
            yield return H.Until(() => MadMax.Story.Story.Flag("hook:P1.2") && WastelandGame.LootSpots.Any(l => l && l.key == "story:p1_glovebox"), 4f);
            yield return H.Walk(c, "p1_ditch", 2.5f);
            var glove = WastelandGame.LootSpots.FirstOrDefault(l => l && l.key == "story:p1_glovebox");
            if (!c.Check(glove, "the dead coupe in the ditch has a glovebox")) yield break;
            glove.Use(g, false);
            c.Check(g.Inventory.GetItem("trophy_ornament") > 0, "a hood ornament from the glovebox");
            yield return H.Walk(c, "p1_collector", 2.5f);
            yield return H.Until(() => g.CastBody("otis") != null, 5f);
            if (!c.Check(g.CastBody("otis"), "Otis Vane at his yard")) yield break;
            c.Check(H.Talk(g, g.CastBody("otis"), "YOU HAVE NELL MERCER'S"), "Otis names his price");
            c.Check(H.Talk(g, g.CastBody("otis"), "A HOOD ORNAMENT"), "trade the ornament for the straight six");
            yield return H.Until(() => MadMax.Story.Story.Flag("p1_2_dealt"), 4f);
            c.Check(g.Inventory.GetItem("trophy_ornament") == 0, "the ornament went to Otis");
            var engine = VehiclePart.Registry.FirstOrDefault(p => p && !p.Socket && p.partId == StoryLibrary.P1Original);
            if (!c.Check(engine, "Tom's straight six arrives at Nell's pile")) yield break;
            ch.FindSocket("engine").Attach(engine);
            c.Fixture("the straight six carried to the coupe and mounted");
            yield return H.Until(() => MadMax.Story.Story.StepDone("P1.2", "engine"), 4f);
            yield return H.Walk(c, "nell", 3f);
            c.Check(H.Talk(g, g.CastBody("nell"), "SHE HAS AN ENGINE"), "show Nell");
            yield return H.Until(() => MadMax.Story.Story.StateOf("P1.2") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("P1.2") == MadMax.Story.Story.State.Done, "P1.2 THE WRONG ORIGINAL is done");
            c.Check(StoryLibrary.P1Trophy(MadMax.Story.Story.Route("P1.2", "deal")) == "trophy_ornament" && StoryLibrary.Get("P1.2").payoff.StartsWith("TOM'S STRAIGHT SIX"),
                "the trade is remembered: " + MadMax.Story.Story.Route("P1.2", "deal"));

            // ---- P1.3 ONE MORE SUNDAY: Nell drives
            DayNight.SetHours(9f);
            yield return H.Until(() => MadMax.Story.Story.StateOf("P1.3") == MadMax.Story.Story.State.Open, 3f);
            c.Check(H.Talk(g, g.CastBody("nell"), "IT'S SUNDAY SOMEWHERE"), "Nell wants one more Sunday");
            yield return H.Until(() => MadMax.Story.Story.Flag("hook:P1.3"), 3f);
            var barrel = Placeable.All.FirstOrDefault(p => p && p.id == "barrel" && g.IsStoryProp(p) && Vector3.Distance(p.transform.position, StoryAnchors.Get("nell")) < 9f);
            var box = barrel ? barrel.GetComponent<Container>() : null;
            c.Check(box && box.inventory.Get(ResourceType.Oil) >= 5 && box.inventory.Get(ResourceType.Coolant) >= 8, "oil and coolant in Nell's barrel");
            if (box) { g.Inventory.Add(ResourceType.Oil, box.inventory.Get(ResourceType.Oil)); g.Inventory.Add(ResourceType.Coolant, box.inventory.Get(ResourceType.Coolant)); box.inventory.TrySpend(ResourceType.Oil, box.inventory.Get(ResourceType.Oil)); box.inventory.TrySpend(ResourceType.Coolant, box.inventory.Get(ResourceType.Coolant)); }
            g.Inventory.Add(ResourceType.Fuel, 15);
            c.Fixture("oil and coolant taken from the barrel ([E]); 15 L petrol in the pack");
            yield return H.Walk(c, "p1_car", 3f);
            car.GetComponent<VehicleSystems>().Service(g.Inventory);
            yield return H.Until(() => MadMax.Story.Story.StepDone("P1.3", "ready"), 4f);
            c.Check(MadMax.Story.Story.StepDone("P1.3", "ready"), "serviced ([G]): fuel, oil, coolant");
            yield return H.Walk(c, "nell", 3f);
            c.Check(H.Talk(g, g.CastBody("nell"), "YOU DRIVE, NELL"), "Nell drives; you ride");
            var nell = g.CastBody("nell");
            yield return H.Until(() => nell && nell.Driving, 20f);
            if (!(nell && nell.Driving))
            {
                var cq = MadMax.Story.Story.Current(StoryLibrary.Get("P1.3"));
                c.Note($"step {(cq != null ? cq.id : "-")}, route '{MadMax.Story.Story.Route("P1.3", "who")}', body {(nell ? "yes" : "no")}, listed {(g.CastBody("nell") == nell)}, " +
                       (nell ? $"mode {nell.mode}, available {nell.Available}, riding {nell.Riding}, companion {nell.companion}, to car {Vector3.Distance(nell.transform.position, car.transform.position):0.0} m, home {Vector3.Distance(nell.home, car.transform.position):0.0} m from car, " : "") +
                       $"car aiDriven {car.aiDriven}, hours {DayNight.Hours:0.0}");
            }
            if (!c.Check(nell && nell.Driving, "Nell takes the wheel")) yield break;
            var seat = car.GetComponentInChildren<PassengerSeat>();
            if (!c.Check(seat && seat.Prompt(g) != null, "the passenger door offers a ride: " + (seat ? seat.Prompt(g) : "-"))) yield break;
            seat.Use(g, false);
            c.Check(seat.Taken, "riding in the passenger seat");
            var p0 = car.transform.position;
            yield return H.Until(() => MadMax.Story.Story.StepDone("P1.3", "lookout"), 60f);
            c.Metric("nell_drove", Vector3.Distance(p0, car.transform.position), "m");
            c.Check(Vector3.Distance(p0, car.transform.position) > 15f, "Nell drives off along the road");
            if (!MadMax.Story.Story.StepDone("P1.3", "lookout"))
            {
                var v = StoryAnchors.Get("p1_view");
                yield return TestWorld.Place(c, car, v + (car.transform.position - v).normalized * 12f, v - car.transform.position, 1f);
                c.Fixture("the rest of the lookout road (the coupe placed near the lookout)");
                yield return H.Until(() => MadMax.Story.Story.StepDone("P1.3", "lookout"), 10f);
            }
            c.Check(MadMax.Story.Story.StepDone("P1.3", "lookout") && !nell.Driving, "at the lookout; Nell gets out");
            g.Player.StandUp();
            yield return new WaitForSeconds(1f);
            c.Screenshot("lookout");
            yield return null;
            c.Check(H.Talk(g, nell, "WHAT WAS TOM LIKE"), "an ordinary conversation at the view");
            yield return H.Until(() => nell.Driving, 25f);
            if (!nell.Driving) c.Note($"home leg: step {MadMax.Story.Story.Current(StoryLibrary.Get("P1.3"))?.id}, to car {Vector3.Distance(nell.transform.position, car.transform.position):0.0} m, available {nell.Available}");
            c.Check(nell.Driving, "Nell takes the wheel for home");
            seat.Use(g, false);
            yield return H.Until(() => MadMax.Story.Story.StateOf("P1.3") == MadMax.Story.Story.State.Done, 60f);
            if (MadMax.Story.Story.StateOf("P1.3") != MadMax.Story.Story.State.Done)
            {
                var hp = StoryAnchors.Get("p1_car");
                yield return TestWorld.Place(c, car, hp + (car.transform.position - hp).normalized * 5f, hp - car.transform.position, 1f);
                c.Fixture("the road home (the coupe placed by Nell's stop)");
                yield return H.Until(() => MadMax.Story.Story.StateOf("P1.3") == MadMax.Story.Story.State.Done, 10f);
            }
            c.Check(MadMax.Story.Story.StateOf("P1.3") == MadMax.Story.Story.State.Done, "P1.3 ONE MORE SUNDAY is done");
            c.Check(StoryLibrary.P1NellDrives(MadMax.Story.Story.Route("P1.3", "who")) && StoryLibrary.Get("P1.3").payoff.StartsWith("NELL DROVE"), "who drove is remembered");
            c.Check(!g.Fleet.Contains(car) && MadMax.Story.Story.Flag("paint_nell") && g.Inventory.GetItem("tool_jack") > 0, "the coupe stays Nell's; Tom's jack and paint code are yours");
            c.Check(!nell.Driving && !nell.Riding && !nell.companion, "Nell is out of the car and back to herself");
        }
    }
}
