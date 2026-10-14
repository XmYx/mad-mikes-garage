using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Camera and handling feel (2026-10-12): the overhead view (default for new games: climbs with speed,
    /// leads the travel, turns with the heading), the arcade yaw assist, on-foot momentum.</summary>
    public static class FeelScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new OverheadCamera();
            yield return new YawAssist();
            yield return new WalkMomentum();
        }
    }

    /// <summary>OVERHEAD VIEW: a new game opens in it; parked, the camera hangs ~33 m up; driving it climbs, leads the
    /// car along its travel (the car stays well inside the frame) and turns behind the heading.</summary>
    class OverheadCamera : Scenario
    {
        public override string Id => "camera.overhead";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var rig = g.cameraRig;
            if (!rig) { c.Block("no camera rig"); yield break; }
            c.Check(rig.mode == ViewMode.Overhead, "a new game opens in the overhead view (" + rig.mode + ")");
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("no sedan in the start fleet"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level test pad with a clear 60 m lane"); yield break; }
            yield return TestWorld.Place(c, v, pad, fwd);
            if (g.Current != v) { if (g.Current) g.Exit(); g.Enter(v); }
            rig.mode = ViewMode.Overhead;
            yield return new WaitForSeconds(2.5f);
            if (!c.Check(g.Current == v, "in the sedan")) yield break;
            float rest = rig.OverheadHeight;
            c.Metric("height_parked", rest, "m");
            c.Check(rest > 20f && rest < 50f, $"parked: the camera hangs {rest:0} m up");
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            v.handbrake = false; v.throttleInput = 1f;
            float t0 = Time.time; float top = 0f;
            while (Time.time - t0 < 5f) { top = Mathf.Max(top, v.SpeedKmh); yield return null; }
            var cam = rig.pixel.GetComponent<Camera>();
            float high = rig.OverheadHeight;
            var lead = rig.OverheadLead;
            var vp = cam.WorldToViewportPoint(v.transform.position);
            c.Metric("top_kmh", top, "km/h"); c.Metric("height_driving", high, "m"); c.Metric("lead", lead.magnitude, "m");
            c.Check(high > rest + 3f, $"driving at {top:0} km/h the camera climbs ({rest:0} -> {high:0} m)");
            c.Check(Vector3.Dot(lead, v.Body.linearVelocity) > 0f && lead.magnitude > 2f, $"leads the travel ({lead.magnitude:0.0} m ahead)");
            c.Check(vp.z > 0f && vp.x > 0.15f && vp.x < 0.85f && vp.y > 0.12f && vp.y < 0.88f, $"the car stays well in frame ({vp.x:0.00}, {vp.y:0.00})");
            float heading = Mathf.Atan2(v.transform.forward.x, v.transform.forward.z) * Mathf.Rad2Deg;
            float off = Mathf.Abs(Mathf.DeltaAngle(rig.ViewYaw, heading));
            c.Metric("yaw_off_heading", off, "deg");
            c.Check(off < 45f, $"the view turned behind the heading ({off:0}° off)");
            c.Screenshot("overhead_driving");
            yield return null;
            v.throttleInput = 0f; v.brakeInput = 1f;
            yield return VehicleFixScenarios.Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 6f);
            v.brakeInput = 0f; v.handbrake = true;
        }
    }

    /// <summary>YAW ASSIST: at ~45 km/h a step of half lock turns the car within a third of a second (player driving),
    /// without spinning it; with the assist off the same step answers slower.</summary>
    class YawAssist : Scenario
    {
        public override string Id => "vehicle.yaw_assist";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("no sedan in the start fleet"); yield break; }
            float assisted = 0f, bare = 0f, peakBeta = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level test pad with a clear 60 m lane"); yield break; }
                yield return TestWorld.Place(c, v, pad, fwd);
                if (g.Current != v) { if (g.Current) g.Exit(); g.Enter(v); }
                yield return new WaitForSeconds(0.3f);
                if (!c.Check(g.Current == v, "in the sedan")) yield break;
                yield return TestWorld.StartEngine(c, v);
                if (c.Failed) yield break;
                v.yawAssist = pass == 0 ? 1f : 0f;
                v.handbrake = false; v.throttleInput = 1f;
                yield return VehicleFixScenarios.Until(() => v.SpeedKmh >= 45f, 8f);
                c.Metric(pass == 0 ? "speed_at_step_assist" : "speed_at_step_bare", v.SpeedKmh, "km/h");
                if (!c.Check(v.SpeedKmh > 35f, $"up to speed for the step ({v.SpeedKmh:0} km/h)")) { c.Note(TestWorld.State(v) + "; " + TestWorld.Contacts(v)); yield break; }
                v.throttleInput = 0.4f; v.steerInput = 0.5f;
                float t0 = Time.time;
                while (Time.time - t0 < 0.35f)
                {
                    var vel = v.Body.linearVelocity;
                    if (vel.magnitude > 3f) peakBeta = Mathf.Max(peakBeta, Mathf.Abs(Vector3.SignedAngle(v.transform.forward, Vector3.ProjectOnPlane(vel, Vector3.up), Vector3.up)));
                    yield return null;
                }
                float yaw = Mathf.Abs(Vector3.Dot(v.Body.angularVelocity, Vector3.up)) * Mathf.Rad2Deg;
                if (pass == 0) assisted = yaw; else bare = yaw;
                v.steerInput = 0f; v.throttleInput = 0f; v.brakeInput = 1f;
                yield return VehicleFixScenarios.Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 6f);
                v.brakeInput = 0f; v.handbrake = true;
            }
            v.yawAssist = 1f;
            c.Metric("yaw_rate_035s_assist", assisted, "deg/s"); c.Metric("yaw_rate_035s_bare", bare, "deg/s"); c.Metric("slip_angle_peak", peakBeta, "deg");
            c.Check(assisted > 20f, $"half lock at 45 km/h: {assisted:0}°/s yaw after 0.35 s");
            c.Check(assisted > bare * 1.1f, $"answers faster than the tyres alone ({assisted:0} vs {bare:0}°/s)");
            c.Check(peakBeta < 15f, $"no spin: slip angle stays {peakBeta:0.0}°");
        }
    }

    /// <summary>WALK MOMENTUM: on foot a run builds up over about a fifth of a second and stops a little quicker
    /// (no instant starts and stops).</summary>
    class WalkMomentum : Scenario
    {
        public override string Id => "player.momentum";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var p = g.Player;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.5f); }
            if (!TestWorld.Pad(3f, 20f, out var pad, out var fwd)) { c.Block("no clear pad"); yield break; }
            p.Teleport(pad + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            WastelandGame.ExternalInput = true;
            yield return new WaitForSeconds(0.6f);
            var dir = Quaternion.Inverse(Quaternion.Euler(0f, g.cameraRig.ViewYaw, 0f)) * fwd;
            p.moveInput = new Vector2(dir.x, dir.z).normalized; p.run = true;
            float tIn = Time.time;
            yield return null; yield return null;
            float early = new Vector2(p.Velocity.x, p.Velocity.z).magnitude, earlyT = Time.time - tIn;   // the editor may run slow frames
            yield return new WaitForSeconds(0.5f);
            float full = new Vector2(p.Velocity.x, p.Velocity.z).magnitude;
            p.moveInput = Vector2.zero; p.run = false;
            float t0 = Time.time;
            yield return VehicleFixScenarios.Until(() => new Vector2(p.Velocity.x, p.Velocity.z).magnitude < 0.2f, 1.5f);
            float stop = Time.time - t0;
            WastelandGame.ExternalInput = false;
            c.Metric("speed_2_frames", early, "m/s"); c.Metric("speed_0_5s", full, "m/s"); c.Metric("stop_time", stop, "s");
            c.Check(early <= 26f * earlyT * 1.25f + 0.2f, $"a run builds up: {early:0.0} m/s after {earlyT * 1000f:0} ms (ramp {26f * earlyT:0.0})");
            c.Check(full > p.runSpeed * 0.8f, $"and reaches running pace ({full:0.0} m/s)");
            // with the run_stop clip the body runs on to the next heel strike and takes the clip's braking step (root
            // motion, 2026-10-13 animation revamp); without it momentum stops it in under 0.45 s
            float stopLimit = HumanClips.Enabled && HumanClips.Get("run_stop") != null ? 1.0f : 0.45f;
            c.Check(stop < stopLimit, $"stops within {stop:0.00} s (limit {stopLimit:0.00})");
        }
    }
}
