using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 28, the rolling city.</summary>
    public static class CityScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new CityRide();
        }
    }

    /// <summary>START: ABOARD THE ROLLING CITY. The game begins on the deck, docked at the first stop with the gangway
    /// down onto the pier; a car parked on the main street is strapped down; the city leaves on its timetable and the
    /// player and the car ride along without sliding across the deck; at the next dock the gangway lands on the pier at
    /// deck height and the player walks off onto it.</summary>
    class CityRide : Scenario
    {
        public override string Id => "city.ride";
        public override float Timeout => 150f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, start = 1 };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.Until(() => g.City && g.City.Ready, 30f);
            var city = g.City;
            if (!c.Check(city && city.Ready, "the rolling city is built" + (city ? " (" + city.Name + ")" : ""))) yield break;
            var P = g.Player;
            yield return new WaitForSeconds(1f);
            c.Check(!g.Current && city.OnDeck(P.transform.position, 4f), $"the game starts aboard, on foot (deck-local {Local(city, P.transform.position)})");
            c.Check(city.Dock == 0, $"docked at the first stop ({(city.Dock >= 0 ? g.World.city.dockNames[city.Dock] : "travelling")}, {city.DwellLeft:0} s to departure)");
            yield return PH.Until(() => city.GangwayDown, 12f);
            c.Check(city.GangwayDown, "the gangway is down onto the pier");
            var rig = g.cameraRig;
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(city.transform.forward * -0.6f + city.transform.right * 0.8f - Vector3.up * 0.25f); }
            yield return new WaitForSeconds(1.2f);
            c.Screenshot("aboard_docked");
            yield return null;

            // a car parked on the main street, strapped down by the city
            var car = g.SpawnAiVehicle("Coupe", city.DeckPoint(new Vector3(-1.5f, 1.2f, 18f)), city.transform.rotation);
            if (!c.Check(car, "a coupe set down on the main street")) yield break;
            car.aiDriven = false; car.handbrake = true; car.Occupied = false;
            c.Fixture("a coupe parked on the deck's main street");
            yield return PH.Until(() => ((IList<VehicleDriver>)city.Strapped).Contains(car), 8f);
            c.Check(((IList<VehicleDriver>)city.Strapped).Contains(car), "the parked car is strapped to the deck");

            // jump to a few seconds before departure (the player kept where they stood on the deck), then ride out
            var keep = Local(city, P.transform.position);
            city.Clock += Mathf.Max(0f, city.DwellLeft - 4f);
            city.Snap();
            P.Teleport(city.DeckPoint(keep), P.transform.eulerAngles.y);
            c.Fixture("the clock moved to 4 s before departure (player kept in place on the deck)");
            var startCity = city.transform.position;
            var playerLocal0 = Local(city, P.transform.position);
            var carLocal0 = Local(city, car.transform.position);
            yield return new WaitForSeconds(28f);
            float travelled = Vector3.Distance(startCity, city.transform.position);
            float playerDrift = Vector3.Distance(Flat(playerLocal0), Flat(Local(city, P.transform.position)));
            float carDrift = Vector3.Distance(Flat(carLocal0), Flat(Local(city, car.transform.position)));
            c.Metric("city_travelled", travelled, "m"); c.Metric("player_drift", playerDrift, "m"); c.Metric("car_drift", carDrift, "m");
            c.Check(city.Dock < 0 && travelled > 8f, $"the city left the dock on its timetable ({travelled:0.0} m, {city.CurrentSpeed:0.0} m/s)");
            c.Check(playerDrift < 1.2f && city.OnDeck(P.transform.position, 4f), $"the player rides along on the deck ({playerDrift:0.00} m drift)");
            c.Check(carDrift < 0.5f, $"the strapped car rides along ({carDrift:0.00} m drift)");
            if (rig) rig.mode = ViewMode.Isometric;
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("under_way");
            yield return null;

            // at the next dock: the gangway lands on the pier; walk off onto it
            keep = Local(city, P.transform.position);
            city.Clock = System.Math.Floor(city.Clock / city.Cycle) * city.Cycle + city.ArrivalAt(1) + 2.0;
            city.Snap();
            P.Teleport(city.DeckPoint(keep), P.transform.eulerAngles.y);
            c.Fixture("the clock moved to the arrival at the second dock");
            float ft0 = Time.fixedTime, rt0 = Time.realtimeSinceStartup; double ck0 = city.Clock;
            yield return PH.Until(() => city.GangwayDown, 15f);
            c.Fixture($"waited {Time.realtimeSinceStartup - rt0:0.0} s real, {Time.fixedTime - ft0:0.0} s fixed, clock +{city.Clock - ck0:0.0} s, timeScale {Time.timeScale}, city active {city.isActiveAndEnabled}");
            var dock = g.CityDocks.Find(d => d.index == 1);
            if (!c.Check(dock && city.GangwayDown, $"docked at the second stop, gangway down (dock {city.Dock}, {city.DwellLeft:0} s left, gangway {(city.GangwayDown ? "down" : "up")}, docks {g.CityDocks.Count}, clock {city.Clock:0} of {city.Cycle:0})")) yield break;
            var tip = city.DeckPoint(new Vector3(CityRoute.HalfWidth + CityDesign.PierGap, 0f, 0f));
            var pier = dock.PierPoint(0f, 0f);
            float gap = Vector2.Distance(new Vector2(tip.x, tip.z), new Vector2(pier.x, pier.z)), step = Mathf.Abs(tip.y - pier.y);
            c.Metric("gangway_gap", gap, "m"); c.Metric("gangway_step", step, "m");
            c.Check(gap < CityDesign.GangLen * CityDesign.V - CityDesign.PierGap - 0.2f && step < 0.4f, $"the gangway rests on the pier ({gap:0.00} m off the line, it overlaps by {CityDesign.GangLen * CityDesign.V - CityDesign.PierGap - gap:0.00} m; {step:0.00} m step)");
            P.Teleport(city.DeckPoint(new Vector3(14f, 0.15f, 0f)), city.transform.eulerAngles.y + 90f);
            yield return new WaitForSeconds(0.4f);
            for (float t = 0f; t < 13f; t += Time.deltaTime) { P.AutoWalk = city.transform.right; yield return null; }   // the work system clears it between jobs (laden: ~1 m/s)
            P.AutoWalk = null;
            yield return new WaitForSeconds(0.5f);
            var pl = dock.transform.InverseTransformPoint(P.transform.position);
            c.Check(!city.OnDeck(P.transform.position, 4f) && pl.x > 0.3f && Mathf.Abs(pl.y) < 0.6f,
                    $"walked down the gangway onto the pier (dock-local {pl.x:0.0}, {pl.y:0.00}, {pl.z:0.0})");
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(-city.transform.right * 0.8f + city.transform.forward * 0.3f - Vector3.up * 0.15f); }
            yield return new WaitForSeconds(1.2f);
            c.Screenshot("from_the_pier");
            yield return null;
            // the whole city from the dock yard (the foot of the ramp), then from above
            var foot = dock.transform.TransformPoint(new Vector3((CityDesign.PierW + CityDesign.RampLen) * CityDesign.V + 14f, 0f, 22f));
            foot.y = g.World.Sample(foot.x, foot.z).height + 0.2f;
            P.Teleport(foot, 0f);
            var look = city.transform.position + Vector3.up * 8f - foot;
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(look.normalized); }
            yield return new WaitForSeconds(2.5f);
            c.Screenshot("city_from_the_yard");
            yield return null;
            // broadside from the far side (the generated skyline), and the tracks close up
            var broad = city.transform.position - city.transform.right * 70f + city.transform.forward * 6f;
            broad.y = g.World.Sample(broad.x, broad.z).height + 0.2f;
            P.Teleport(broad, 0f);
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward((city.transform.position + Vector3.up * 14f - broad).normalized); }
            yield return new WaitForSeconds(2.5f);
            c.Screenshot("city_broadside");
            yield return null;
            var near = city.transform.position - city.transform.right * 30f + city.transform.forward * 22f;
            near.y = g.World.Sample(near.x, near.z).height + 0.2f;
            P.Teleport(near, 0f);
            if (rig) rig.LookToward((city.transform.position + city.transform.forward * 22f - city.transform.right * 14f + Vector3.up * 2f - near).normalized);
            yield return new WaitForSeconds(2f);
            c.Screenshot("city_tracks");
            yield return null;
            if (rig) rig.mode = ViewMode.Isometric;
        }

        static Vector3 Local(RollingCity city, Vector3 p) => city.transform.InverseTransformPoint(p);
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
