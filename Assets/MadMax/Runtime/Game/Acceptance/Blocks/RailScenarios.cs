using System.Collections;
using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The railway smoke test: every settlement has a station of its tier, the passenger stations hang
    /// together, a timetabled train runs between two stations, and the player drives a makeshift loco 50 m.</summary>
    public static class RailScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new RailNetworkSmoke(); }
    }

    class RailNetworkSmoke : Scenario
    {
        public override string Id => "rail.network";
        public override string[] Suites => new[] { "full" };
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);                                                          // the director follows the player, not a parked car
            var net = g.World != null ? g.World.rail : null;
            if (!c.Check(net != null, "the world has a railway")) yield break;
            int terminals = 0, through = 0, halts = 0, pass = 0;
            foreach (var st in net.stations) { if (st.tier == StationTier.Terminal) terminals++; if (st.tier == StationTier.Through) through++; if (st.tier == StationTier.Halt) halts++; if (st.Passenger) pass++; }
            c.Metric("stations", net.stations.Count, ""); c.Metric("terminals", terminals, ""); c.Metric("lines", net.lines.Count, "");
            c.Check(pass >= g.World.settlements.Count * 0.8f, $"{pass} passenger stations for {g.World.settlements.Count} settlements");
            c.Check(terminals > 0 && through + halts > 0, $"tiers: {terminals} terminals, {through} stations, {halts} halts");
            // connectivity: walk the node graph from the first passenger station
            // connected networks: flood each passenger station's node set, keep the biggest
            var netOf = new Dictionary<int, int>(); var open = new Stack<int>(); int nets = 0, reach = 0; RailStation first = null;
            foreach (var st in net.stations)
            {
                if (!st.Passenger || st.platformLines.Count == 0) continue;
                int seed0 = net.lines[st.platformLines[0]].a;
                if (netOf.ContainsKey(seed0)) continue;
                nets++; open.Push(seed0);
                while (open.Count > 0)
                {
                    int n = open.Pop();
                    if (netOf.ContainsKey(n)) continue;
                    netOf[n] = nets;
                    foreach (var e in net.nodes[n].ends) { var l = net.lines[e.x]; if (!l.dead) { open.Push(l.a); open.Push(l.b); } }
                }
                int count = 0;
                foreach (var o in net.stations) if (o.Passenger && o.platformLines.Count > 0 && netOf.TryGetValue(net.lines[o.platformLines[0]].a, out int id) && id == nets) count++;
                if (count > reach) { reach = count; first = st; }
            }
            if (first == null) { c.Block("no passenger station"); yield break; }
            c.Metric("reachable_share", reach / (float)Mathf.Max(1, pass), "");
            c.Metric("networks", nets, "");
            var lands = new HashSet<int>(); int onFirstLand = 0, firstLand = net.LandOf(first.pos);
            foreach (var o in net.stations) if (o.Passenger) { lands.Add(net.LandOf(o.pos)); if (net.LandOf(o.pos) == firstLand) onFirstLand++; }
            c.Metric("landmasses", lands.Count, "");
            c.Check(reach >= onFirstLand * 0.8f, $"{reach}/{onFirstLand} stations of its landmass on the biggest network ({first.Name(g.World)}; {nets} networks over {lands.Count} landmasses, {pass} stations)");
            // wait for the director
            float t0 = Time.time;
            while (!g.Rail && Time.time - t0 < 10f) yield return null;
            if (!c.Check(g.Rail, "the train director runs")) yield break;
            if (!c.Check(g.Rail.services.Count > 0, $"{g.Rail.services.Count} services on timetables")) yield break;
            // a train between two stations: stand at its first stop just before it leaves
            var svc = g.Rail.services[0];
            var stop0 = net.stations[svc.stopStation[0]];
            svc.Evaluate(svc.clock, out _, out _, out _, out _);
            svc.clock = Mathf.Floor((float)(svc.clock / svc.Cycle)) * svc.Cycle + svc.dwell - 8f;      // 8 s before it pulls out of stop 0
            var stand = stop0.World(0f, stop0.platforms.Count > 0 ? (stop0.platforms[0].z + stop0.platforms[0].w) * 0.5f : 6f, RailNetwork.PlatformTop + 0.1f);
            g.Player.Teleport(stand, 0f);
            t0 = Time.time;
            while (!svc.live && Time.time - t0 < 8f) yield return null;
            if (!c.Check(svc.live, $"the {svc.name} stands at {stop0.Name(g.World)} (train point {Vector3.Distance(svc.WorldPoint(), g.FocusPos):0} m from the focus, ended {svc.ended}, stops {svc.stopStation.Count})")) yield break;
            float h0 = svc.live.head;
            yield return new WaitForSeconds(16f);
            if (!c.Check(svc.live, "it is still about")) yield break;
            float moved = Mathf.Abs(svc.live.head - h0);
            c.Metric("service_moved", moved, "m");
            c.Check(moved > 5f, $"it pulled out ({moved:0} m in 16 s)");
            c.Screenshot("rail_service");
            yield return null;
            // drive a makeshift loco 50 m on a long plain line
            RailLine line = null;
            foreach (var l in net.lines) if (!l.dead && l.kind == TrackKind.Main && l.station < 0 && l.Length > 400f && (l.flags[l.pts.Length / 2] & RailLine.Bridge) == 0) { line = l; break; }
            if (line == null) { c.Block("no long main line"); yield break; }
            var loco = g.Rail.SpawnFree(new[] { CarKind.MakeshiftLoco }, line, line.Length * 0.5f, true, "TEST LOCO", true, 120f);
            if (!c.Check(loco, "a makeshift loco is on the rails")) yield break;
            g.Stats.skillXp[(int)MadMax.RPG.Skill.Rail] = MadMax.RPG.CharacterStats.XpForLevel(2);
            var cab = loco.Loco;
            g.Player.Teleport(cab.transform.TransformPoint(cab.CabSeat.exitLocal) + Vector3.up * 0.3f, 0f);
            yield return null;
            g.Player.SitOn(cab.CabSeat, cab.def.seat);
            yield return new WaitForSeconds(2f);
            if (!c.Check(g.CabTrain == loco, "in the cab")) yield break;
            WastelandGame.ExternalInput = true;
            float start = loco.head;
            t0 = Time.time;
            while (Time.time - t0 < 14f && Mathf.Abs(loco.head - start) < 50f) { g.Player.moveInput = new Vector2(0f, 1f); yield return null; }
            g.Player.moveInput = Vector2.zero;
            float driven = Mathf.Abs(loco.head - start);
            c.Metric("driven", driven, "m"); c.Metric("top_speed", Mathf.Abs(loco.speed) * 3.6f, "km/h");
            c.Check(driven >= 50f, $"drove the loco {driven:0} m");
            c.Screenshot("rail_cab");
            yield return null;
            t0 = Time.time;
            while (Time.time - t0 < 16f && Mathf.Abs(loco.speed) > 0.2f) { g.Player.moveInput = new Vector2(0f, -1f); yield return null; }   // ~10.5 s from 45 km/h at 1.4 m/s²
            g.Player.moveInput = Vector2.zero;
            WastelandGame.ExternalInput = false;
            c.Check(Mathf.Abs(loco.speed) <= 0.2f, "and brought it to a stand");
            g.Player.StandUp();
        }
    }
}
