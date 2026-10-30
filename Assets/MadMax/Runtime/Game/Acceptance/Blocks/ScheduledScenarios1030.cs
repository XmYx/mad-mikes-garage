using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-30, the railway remodel: wheelsets turn and brakes spark, a free train taken
    /// too fast into a tight scrap curve comes off the rails and is jacked back on, a service has a driver in the cab who
    /// pulls the horn cord, a stationmaster whistles and raises the flag before departure; a warm bonnet steams in the cold.</summary>
    public static class ScheduledScenarios1030
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new RailWheelsTurn();
            yield return new RailDerail();
            yield return new RailCrewAndStationmaster();
            yield return new BonnetSteamCold();
        }

        /// <summary>A long plain main line (no bridge at its middle), or null.</summary>
        internal static RailLine PlainLine(RailNetwork net)
        {
            foreach (var l in net.lines) if (!l.dead && l.kind == TrackKind.Main && l.station < 0 && l.Length > 400f && (l.flags[l.pts.Length / 2] & RailLine.Bridge) == 0) return l;
            return null;
        }

        /// <summary>Sit the player in the loco's cab with RAILWAYS 2.</summary>
        internal static IEnumerator IntoCab(ScenarioContext c, Train t)
        {
            var g = c.Game;
            g.Stats.skillXp[(int)MadMax.RPG.Skill.Rail] = MadMax.RPG.CharacterStats.XpForLevel(2);
            var cab = t.Loco;
            g.Player.Teleport(cab.transform.TransformPoint(cab.CabSeat.exitLocal) + Vector3.up * 0.3f, 0f);
            yield return null;
            g.Player.SitOn(cab.CabSeat, cab.def.seat);
            yield return SurvivalKit.GameSeconds(1.5f, 10);
            c.Fixture("player seated in the cab of " + t.trainName + " (RAILWAYS 2)");
        }

        internal static IEnumerator Director(ScenarioContext c)
        {
            var g = c.Game;
            yield return SurvivalKit.Until(() => g.Rail, 10f);
        }
    }

    /// <summary>A loco and a flatcar on a main line: every car has its wheelsets (4, separate meshes); driven 40 m they
    /// roll that far (the wheels' distance within 15 % of the head's) and turn; hard braking at speed throws sparks.</summary>
    class RailWheelsTurn : Scenario
    {
        public override string Id => "rail.wheels_turn";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            yield return ScheduledScenarios1030.Director(c);
            var net = g.World != null ? g.World.rail : null;
            if (!c.Check(net != null && g.Rail, "a railway with its director")) yield break;
            var line = ScheduledScenarios1030.PlainLine(net);
            if (line == null) { c.Block("no long main line"); yield break; }
            var t = g.Rail.SpawnFree(new[] { CarKind.MakeshiftLoco, CarKind.Flatcar }, line, line.Length * 0.5f, true, "TEST TRAIN", true, 120f);
            if (!c.Check(t, "a loco and a flatcar on the rails")) yield break;
            foreach (var car in t.cars)
            {
                var w = car.GetComponent<RailWheels>();
                c.Check(w && w.Count == 4 && car.transform.Find("Wheelset"), $"{car.def.name}: {(w ? w.Count : 0)} wheelsets of their own");
            }
            yield return ScheduledScenarios1030.IntoCab(c, t);
            WastelandGame.ExternalInput = true;
            var wheels = t.cars[1].GetComponent<RailWheels>();
            float h0 = t.head, r0 = wheels.Rolled, t0 = Time.time;
            while (Time.time - t0 < 30f && (Mathf.Abs(t.head - h0) < 40f || Mathf.Abs(t.speed) < 7f) && Mathf.Abs(t.head - h0) < 180f) { g.Player.moveInput = new Vector2(0f, 1f); yield return null; }
            float moved = Mathf.Abs(t.head - h0), rolled = Mathf.Abs(wheels.Rolled - r0);
            c.Metric("moved", moved, "m"); c.Metric("rolled", rolled, "m"); c.Metric("speed", Mathf.Abs(t.speed) * 3.6f, "km/h");
            c.Check(moved > 30f && Mathf.Abs(rolled - moved) < moved * 0.15f + 0.5f, $"the flatcar's wheels rolled {rolled:0.0} m for {moved:0.0} m of track");
            c.Check(wheels.Turn > 0.5f && wheels.Turn < 359.5f || rolled > 1f, $"the wheelsets turned ({wheels.Turn:0}°)");
            c.Screenshot("rail_wheels");
            yield return null;
            int sparks = RailWheels.SparkBursts;
            t0 = Time.time;
            while (Time.time - t0 < 12f && Mathf.Abs(t.speed) > 0.2f) { g.Player.moveInput = new Vector2(0f, -1f); yield return null; }
            g.Player.moveInput = Vector2.zero;
            WastelandGame.ExternalInput = false;
            c.Metric("brake_sparks", RailWheels.SparkBursts - sparks, "");
            c.Check(RailWheels.SparkBursts > sparks, $"braking hard threw sparks ({RailWheels.SparkBursts - sparks} bursts)");
            g.Player.StandUp();
            Object.Destroy(t.gameObject);
        }
    }

    /// <summary>Scrap track laid by the player: a 70 m straight into a 25 m-radius bend. Taken slowly (≤ 4 m/s) the loco
    /// stays on; taken at full throttle it comes off (the pull past the scrap limit), slews over and stops; it won't
    /// drive off the rails and saves as derailed; a jack and five seconds a car put it back on.</summary>
    class RailDerail : Scenario
    {
        public override string Id => "rail.derail";
        public override float Timeout => 320f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            yield return ScheduledScenarios1030.Director(c);
            var net = g.World != null ? g.World.rail : null;
            if (!c.Check(net != null && g.Rail, "a railway with its director")) yield break;
            if (!TestWorld.Pad(6f, out var pad)) { c.Block("no pad"); yield break; }
            // the test track: straight +Z, then a left-hand 25 m bend through 150°, all at the pad's height
            var pts = new List<Vector3>();
            var o = pad + Vector3.up * 0.3f;
            for (float z = 0f; z <= 70f; z += 5f) pts.Add(o + new Vector3(0f, 0f, z));
            var centre = o + new Vector3(-25f, 0f, 70f);
            for (int k = 1; k <= 15; k++) { float a = k * 10f * Mathf.Deg2Rad; pts.Add(centre + new Vector3(Mathf.Cos(a) * 25f, 0f, Mathf.Sin(a) * 25f)); }
            var line = net.AddBuilt(pts, true);
            c.Fixture($"scrap track laid at the pad: 70 m straight + 25 m radius bend ({line.Length:0} m)");
            // slow pass
            var t = g.Rail.SpawnFree(new[] { CarKind.MakeshiftLoco }, line, 2f, true, "TEST LOCO", true, 120f);
            if (!c.Check(t, "a loco at the start of it")) yield break;
            yield return ScheduledScenarios1030.IntoCab(c, t);
            WastelandGame.ExternalInput = true;
            int before = Train.Derailments;
            float t0 = Time.time, peakSlow = 0f;
            while (Time.time - t0 < 45f && t.head < line.Length - 8f && !t.Derailed)
            {
                g.Player.moveInput = new Vector2(0f, Mathf.Abs(t.speed) < 3.5f ? 1f : -0.3f);
                if (t.head > 72f) peakSlow = Mathf.Max(peakSlow, t.CurvePull);
                yield return null;
            }
            c.Metric("slow_pull", peakSlow, "m/s²");
            c.Check(!t.Derailed && t.head > 80f, $"slowly ({Mathf.Abs(t.speed) * 3.6f:0} km/h) round the bend it stays on (pull {peakSlow:0.0} m/s², head {t.head:0} m)");
            g.Player.StandUp();
            WastelandGame.ExternalInput = false;
            Object.Destroy(t.gameObject);
            yield return null;
            // fast pass
            t = g.Rail.SpawnFree(new[] { CarKind.MakeshiftLoco, CarKind.Hopper }, line, 2f, true, "FAST LOCO", true, 120f);
            yield return ScheduledScenarios1030.IntoCab(c, t);
            WastelandGame.ExternalInput = true;
            float peak = 0f, entry = 0f;
            t0 = Time.time;
            while (Time.time - t0 < 30f && !t.Derailed && t.head < line.Length - 8f) { g.Player.moveInput = new Vector2(0f, 1f); peak = Mathf.Max(peak, t.CurvePull); if (t.head < 72f) entry = Mathf.Abs(t.speed); yield return null; }
            c.Metric("entry_speed", entry * 3.6f, "km/h"); c.Metric("fast_pull", peak, "m/s²");
            if (!c.Check(t.Derailed && Train.Derailments > before && Train.LastDerailed == t, $"at {entry * 3.6f:0} km/h it came off the rails (pull {peak:0.0} m/s²)")) { WastelandGame.ExternalInput = false; yield break; }
            yield return SurvivalKit.GameSeconds(1f, 5);
            c.Screenshot("rail_derailed");
            yield return null;
            yield return SurvivalKit.Until(() => Mathf.Abs(t.speed) < 0.05f, 10f);
            c.Check(Mathf.Abs(t.speed) < 0.05f, "it ploughed to a stop");
            var loco = t.Loco;
            System.Func<Vector3> onTrack = () => (t.path.At(t.head - loco.def.Half + loco.def.bogie) + t.path.At(t.head - loco.def.Half - loco.def.bogie)) * 0.5f + Vector3.up * RailNetwork.RailTop;
            var off = loco.transform.position - onTrack();
            c.Metric("slew", off.magnitude, "m");
            c.Check(off.magnitude > 0.2f && Vector3.Angle(loco.transform.up, Vector3.up) > 3f, $"the loco sits off the track ({off.magnitude:0.00} m over, leaning {Vector3.Angle(loco.transform.up, Vector3.up):0}°)");
            float h1 = t.head;
            t0 = Time.time;
            while (Time.time - t0 < 3f) { g.Player.moveInput = new Vector2(0f, 1f); yield return null; }
            g.Player.moveInput = Vector2.zero;
            WastelandGame.ExternalInput = false;
            c.Check(Mathf.Abs(t.head - h1) < 0.2f && t.throttle <= 0f, "the throttle does nothing off the rails");
            var save = new List<string>();
            g.Rail.Save(save);
            c.Check(save.Exists(s => s.StartsWith("t|") && s.Contains("|FAST LOCO|1")), "it saves as derailed");
            // jack it back on
            g.Player.StandUp();
            yield return SurvivalKit.GameSeconds(0.5f, 5);
            g.Player.Teleport(loco.transform.position + loco.transform.right * 3f + Vector3.up * 0.3f, 0f);
            yield return null;
            var opts = ContextActions.Gather(g, new ContextTarget { ground = true, point = g.Player.transform.position, title = "HERE" });
            var opt = opts.Find(x => x.label.StartsWith("RE-RAIL"));
            c.Check(opt != null && opt.blocked == "NEEDS A JACK", "RE-RAIL is offered beside it, needing a jack: " + (opt != null ? opt.blocked : "none"));
            g.Inventory.AddItem("tool_jack", 1);
            opts = ContextActions.Gather(g, new ContextTarget { ground = true, point = g.Player.transform.position, title = "HERE" });
            opt = opts.Find(x => x.label.StartsWith("RE-RAIL"));
            if (!c.Check(opt != null && opt.blocked == null, "with a jack it can start")) yield break;
            opt.run();
            c.Check(t.Rerailing, "re-railing under way");
            yield return SurvivalKit.Until(() => !t.Derailed, t.cars.Count * Train.RerailSeconds * 4f + 10f);
            c.Check(!t.Derailed && t.RerailedCars == t.cars.Count, $"back on the rails ({t.RerailedCars}/{t.cars.Count} cars)");
            yield return SurvivalKit.GameSeconds(2f, 10);
            off = loco.transform.position - onTrack();
            c.Check(off.magnitude < 0.05f, $"and sits on the track again ({off.magnitude:0.00} m)");
            Object.Destroy(t.gameObject);
        }
    }

    /// <summary>A Guild service arriving at a passenger stop: a driver sits in the cab (shown), pulls the horn cord when it
    /// sounds; a stationmaster stands on the platform by the loco, whistles and raises the flag in the last seconds of the
    /// dwell, and the driver waves to the player standing on the platform.</summary>
    class RailCrewAndStationmaster : Scenario
    {
        public override string Id => "rail.crew_stationmaster";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            yield return ScheduledScenarios1030.Director(c);
            var net = g.World != null ? g.World.rail : null;
            if (!c.Check(net != null && g.Rail, "a railway with its director")) yield break;
            int waves = CabCrew.Waves;
            TrainService svc = null;
            foreach (var s in g.Rail.services) if (!s.armoured && !s.ended && s.stopStation.Count > 1 && net.stations[s.stopStation[0]].Passenger && net.stations[s.stopStation[0]].platforms.Count > 0) { svc = s; break; }
            if (svc == null) { c.Block("no Guild service with a passenger first stop"); yield break; }
            var st = net.stations[svc.stopStation[0]];
            // 6 s before it pulls in at its first stop, the player on the platform there
            svc.clock = Mathf.Floor((float)(svc.clock / svc.Cycle)) * svc.Cycle + svc.Cycle - 6f;
            var pf = st.platforms[0];
            g.Player.Teleport(st.World((pf.x + pf.y) * 0.5f, (pf.z + pf.w) * 0.5f, RailNetwork.PlatformTop + 0.1f), st.Yaw);
            c.Fixture($"the {svc.name} 6 s from {st.Name(g.World)}; the player on its platform");
            yield return SurvivalKit.Until(() => svc.live && svc.AtStop == 0, 40f);
            if (!c.Check(svc.live && svc.AtStop == 0, $"the {svc.name} stands at the platform")) yield break;
            var crew = svc.live.Loco ? svc.live.Loco.GetComponentInChildren<CabCrew>() : null;
            yield return SurvivalKit.GameSeconds(1f, 5);
            c.Check(crew && crew.Shown, "a driver sits in the cab");
            Stationmaster sm = null;
            foreach (var n in MadMax.Npc.Npc.All) { var m = n ? n.GetComponent<Stationmaster>() : null; if (m && m.service == svc) sm = m; }
            if (!c.Check(sm, "a stationmaster on the platform")) yield break;
            float d = Vector3.Distance(sm.transform.position, svc.live.Loco.transform.position);
            c.Metric("stationmaster_to_loco", d, "m");
            c.Check(d < 12f, $"beside the loco ({d:0} m)");
            // the player beside the cab: the driver waves
            var loco = svc.live.Loco;
            g.Player.Teleport(loco.transform.position + loco.transform.right * 6f + loco.transform.forward * 2f + Vector3.up * 0.2f, 0f);
            yield return SurvivalKit.Until(() => CabCrew.Waves > waves, 30f);
            c.Check(CabCrew.Waves > waves, "the driver waves (pulling in, or to the player by the cab)");
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((loco.transform.position - g.Player.transform.position).normalized + Vector3.up * 0.2f); }
            yield return SurvivalKit.GameSeconds(0.6f, 5);
            c.Screenshot("rail_crew_wave");
            yield return null;
            // run the dwell down to the whistle
            int whistles = Stationmaster.Whistles;
            if (svc.DwellLeft > Stationmaster.WhistleAt + 1f) svc.clock += svc.DwellLeft - (Stationmaster.WhistleAt + 1f);
            yield return SurvivalKit.Until(() => Stationmaster.Whistles > whistles && sm.Raised > 0.9f, 15f);
            c.Check(Stationmaster.Whistles > whistles, "the whistle before departure");
            c.Check(sm.Raised > 0.9f, $"the flag held up ({sm.Raised:0.00})");
            if (g.cameraRig) g.cameraRig.LookToward((sm.transform.position - g.Player.transform.position).normalized + Vector3.up * 0.1f);
            yield return SurvivalKit.GameSeconds(0.5f, 5);
            c.Screenshot("rail_stationmaster");
            yield return null;
            yield return SurvivalKit.Until(() => crew && crew.OnHorn, 12f);
            c.Check(crew && crew.OnHorn, "the driver pulls the horn cord as it sounds");
            yield return SurvivalKit.Until(() => svc.AtStop < 0, 12f);
            yield return SurvivalKit.GameSeconds(1.5f, 10);
            c.Check(sm && sm.Raised < 0.2f, "it pulled out and the flag came down");
        }
    }

    /// <summary>A sedan with a hot engine parked in −6 °C: wisps rise off the bonnet; in +20 °C none.</summary>
    class BonnetSteamCold : Scenario
    {
        public override string Id => "vehicle.bonnet_steam";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            if (!TestWorld.Pad(5f, out var pad)) { c.Block("no pad"); yield break; }
            var v = TestWorld.Vehicle("Sedan");
            if (!c.Check(v, "a sedan")) yield break;
            yield return TestWorld.Place(c, v, pad, Vector3.forward);
            var sys = v.GetComponent<VehicleSystems>();
            var temp = typeof(VehicleSystems).GetProperty("Temperature");
            ScheduledScenarios1029.Cold(c, -6f);
            temp.SetValue(sys, 85f);
            c.Fixture("engine at 85 °C, parked");
            g.Player.Teleport(pad + Vector3.right * 4f + Vector3.up * 0.2f, 0f);
            int n0 = VehicleSystems.BonnetSteams;
            yield return SurvivalKit.GameSeconds(3f, 20);
            int cold = VehicleSystems.BonnetSteams - n0;
            c.Metric("wisps_cold", cold, "");
            c.Check(cold >= 3, $"the bonnet steams in the frost ({cold} wisps in 3 s)");
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((v.transform.position - g.Player.transform.position).normalized + Vector3.up * 0.2f); }
            yield return SurvivalKit.GameSeconds(0.6f, 5);
            c.Screenshot("bonnet_steam");
            yield return null;
            ScheduledScenarios1029.Warm(c);
            temp.SetValue(sys, 85f);
            n0 = VehicleSystems.BonnetSteams;
            yield return SurvivalKit.GameSeconds(2f, 15);
            c.Check(VehicleSystems.BonnetSteams == n0, "none in warm air");
            Object.Destroy(v.gameObject);
        }
    }
}
