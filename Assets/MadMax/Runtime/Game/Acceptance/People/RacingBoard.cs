using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The daily race board: a town board offering today's road race ([T] on the bounty board) takes the entry
    /// fee once (signing up at another board meanwhile charges nothing), lays the course and sets the waypoint at the
    /// start; a car brought to the start gets the countdown and three rival drivers lined up beside it, who race off on
    /// GO; [T] again quits.</summary>
    class RacingBoard : Scenario
    {
        public override string Id => "racing.board";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            var race = Racing.Instance;
            if (!c.Check(race, "the race board is running")) yield break;
            g.World.Yard(out var origin, out _, out _);
            var towns = g.World.settlements.OrderBy(s => Vector2.Distance(s.pos, new Vector2(origin.x, origin.z))).ToList();
            var st = towns.FirstOrDefault(s => Racing.Offer(s.index)?.kind == Racing.Kind.Road);
            c.Note("today: " + string.Join(" | ", towns.Take(6).Select(s => Market.TownName(s) + ": " + (Racing.Offer(s.index)?.title ?? "-"))));
            if (st == null) { c.Block("no town offers a road race today"); yield break; }
            var offer = Racing.Offer(st.index);
            yield return PH.ToTown(c, st);
            yield return PH.Until(() => BountyBoard.All.Any(b => b && b.town == st.index), 20f);
            var board = BountyBoard.All.FirstOrDefault(b => b && b.town == st.index);
            if (!c.Check(board, "the board in " + Market.TownName(st))) yield break;
            c.Check(board.Prompt(g).Contains("[T] " + offer.title), "the board offers it: " + board.Prompt(g));

            if (PH.Scrap(g) < 100) PH.Grant(c, g, "res:" + (int)ResourceType.Scrap, 100 - PH.Scrap(g));
            int scrap = PH.Scrap(g);
            board.Use(g, true);
            var e = race.Active;
            if (!c.Check(e != null && e.kind == Racing.Kind.Road && e.town == st.index, "[T] signs up: " + (e != null ? e.title : "nothing"))) yield break;
            c.Check(PH.Scrap(g) == scrap - offer.fee, $"the entry fee is {offer.fee} scrap (paid {scrap - PH.Scrap(g)})");
            c.Check(e.gates.Count > 0 && e.path.Count > 1 && g.HasWaypoint && PH.Flat(g.Waypoint, e.start) < 1f, $"a course of {e.gates.Count} gates; the waypoint marks the start");
            var other = towns.FirstOrDefault(s => s != st);
            if (other != null) { race.SignUp(other.index); c.Check(race.Active == e && PH.Scrap(g) == scrap - offer.fee, "signing up elsewhere meanwhile charges nothing"); }

            // ---- to the start in a car
            var car = TestWorld.Vehicle("Sedan") ?? g.Fleet.FirstOrDefault(v => v && v.driveable && !v.GetComponent<MadMax.Vehicles.BikeBalance>() && !v.GetComponent<MadMax.Vehicles.FlightModel>());
            if (!c.Check(car, "a fleet car")) yield break;
            var to = e.gates[0] - e.start; to.y = 0f;
            yield return TestWorld.Place(c, car, e.start, to.sqrMagnitude > 1f ? to.normalized : Vector3.forward, 1f);
            g.Enter(car);
            yield return TestWorld.StartEngine(c, car);
            car.handbrake = true;
            yield return PH.Until(() => race.Status != null && race.Status.Contains("GATE"), 8f);
            var rivals = Object.FindObjectsByType<AiDriver>(FindObjectsSortMode.None).Where(a => a && a.Vehicle && a.Vehicle != car && PH.Flat(a.transform.position, e.start) < 40f).ToList();
            c.Metric("rivals", rivals.Count, "");
            c.Check(race.Status != null && race.Status.Contains("GATE 1/"), "countdown, then GO: " + race.Status);
            c.Check(rivals.Count == 3 && rivals.All(a => a.enabled && a.Vehicle.aiDriven), $"three rival drivers lined up and released ({rivals.Count})");
            c.Screenshot("start");
            yield return null;
            var at = rivals.ToDictionary(a => a, a => a.transform.position);
            yield return new WaitForSeconds(8f);
            float gone = rivals.Where(a => a).Select(a => PH.Flat(a.transform.position, at[a])).DefaultIfEmpty(0f).Max();
            c.Metric("rival_progress", gone, "m");
            c.Check(gone > 25f, $"the rivals race off down the road ({gone:0} m in 8 s)");

            // ---- quit
            board.Use(g, true);
            c.Check(race.Active == null && !g.HasWaypoint, "[T] at the board again quits the race");
            c.Check(PH.Scrap(g) == scrap - offer.fee, "the fee is not refunded, and nothing else is charged");
            g.Exit();
        }
    }
}
