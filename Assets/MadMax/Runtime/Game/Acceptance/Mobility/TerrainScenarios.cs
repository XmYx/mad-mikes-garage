using System.Collections;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Mud and ruts (README "mud, ruts ... change how much grip you have"): on soaked soft ground a pickup
    /// sinks in (the wheels report mud), loses grip compared with firm ground and leaves ruts pressed into the terrain
    /// along its track.</summary>
    class MudRuts : Scenario
    {
        public override string Id => "mobility.terrain.ruts";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            var v = TestWorld.Vehicle("Pickup") ?? TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("no pickup or sedan"); yield break; }
            Weather.SetWetness(1f);
            c.Fixture("ground soaked (weather wetness 1)");
            yield return new WaitForSeconds(0.5f);
            if (!TestWorld.MudPad(8f, 26f, 0.45f, out var pad, out var dir, out float mud)) { c.Block("no soft muddy ground near the start"); yield break; }
            c.Metric("mud", mud, "");
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, v, pad, dir, 1.5f);
            var right = Vector3.Cross(Vector3.up, dir);
            // the ground along the lane before (a strip as wide as the car)
            const int nx = 13, nz = 18;
            var before = new float[nx, nz];
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            {
                var q = pad + right * ((i - nx / 2) * 0.25f) + dir * (5f + j);
                before[i, j] = t.Height(q.x, q.z);
            }
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var p0 = v.transform.position;
            v.handbrake = false; v.throttleInput = 0.7f;
            float t0 = Time.time, mudSeen = 0f, slip = 0f; int n = 0;
            while (Time.time - t0 < 7f && Vector3.Dot(v.transform.position - p0, dir) < 25f)
            {
                mudSeen = Mathf.Max(mudSeen, v.Mud);
                if (Time.time - t0 > 1.5f) { slip += v.WheelSlip; n++; }
                yield return null;
            }
            v.throttleInput = 0f; v.brakeInput = 1f;
            yield return MobilityKit.Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 5f);
            v.brakeInput = 0f; v.handbrake = true;
            float ahead = Vector3.Dot(v.transform.position - p0, dir);
            float deepest = 0f; int sunk = 0;
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            {
                var q = pad + right * ((i - nx / 2) * 0.25f) + dir * (5f + j);
                if (Vector3.Dot(q - p0, dir) > ahead - 2.5f) continue;                             // only where the car has passed
                float dd = before[i, j] - t.Height(q.x, q.z);
                deepest = Mathf.Max(deepest, dd);
                if (dd > 0.02f) sunk++;
            }
            c.Metric("mud_driven", ahead, "m");
            c.Metric("wheel_mud", mudSeen, "");
            c.Metric("mean_slip", n > 0 ? slip / n : 0f, "");
            c.Metric("rut_depth", deepest, "m");
            c.Metric("rut_cells", sunk, "");
            c.Check(ahead > 6f, $"the pickup gets through the mud ({ahead:0.0} m)");
            c.Check(mudSeen > 0.2f, $"the tyres are in mud ({mudSeen:0.00})");
            c.Check(deepest > 0.03f && sunk >= 4, $"it leaves ruts ({deepest * 100f:0} cm deep, {sunk} cells)");
            c.Screenshot("ruts");
            yield return null;
            g.Exit();
            Weather.SetWetness(0f);
        }
    }

    /// <summary>Ice (README "snow, ice"): the same sedan braking from the same speed on the same road needs far
    /// longer once the road is frozen (sub-zero, wet, snow cover) than when it is dry.</summary>
    class IceGrip : Scenario
    {
        public override string Id => "mobility.terrain.ice";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 120f;

        static IEnumerator Stop(ScenarioContext c, VehicleDriver v, Vector3 pad, Vector3 fwd, string tag, float[] result)
        {
            yield return TestWorld.Place(c, v, pad, fwd, 1f);
            yield return TestWorld.StartEngine(c, v);
            v.handbrake = false; v.throttleInput = 1f;
            float t0 = Time.time;
            while (v.SpeedKmh < 32f && Time.time - t0 < 12f && Vector3.Dot(v.transform.position - pad, fwd) < 34f) yield return null;
            float v0 = v.ForwardSpeed;
            var pb = v.transform.position;
            v.throttleInput = 0f; v.brakeInput = 1f;
            yield return MobilityKit.Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 12f);
            float d = MobilityKit.Flat(pb, v.transform.position);
            v.brakeInput = 0f; v.handbrake = true;
            result[0] = v0; result[1] = d; result[2] = d > 0.1f ? v0 * v0 / (2f * d) : 0f;
            c.Metric(tag + "_brake_from", v0 * 3.6f, "km/h");
            c.Metric(tag + "_stop_distance", d, "m");
            c.Metric(tag + "_deceleration", result[2], "m/s2");
            yield return new WaitForSeconds(0.5f);
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("sedan missing"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a 60 m lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            Weather.Restore(false, 0f, 0f, 22f);
            c.Fixture("dry road, 22 °C");
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            var dry = new float[3];
            yield return Stop(c, v, pad, fwd, "dry", dry);
            float offset = Weather.Temperature - Weather.BaseTemperature;                      // latitude
            Weather.Restore(false, 1f, 1f, -14f - offset);
            c.Fixture("frozen road: -14 °C, wet, snow cover");
            yield return new WaitForSeconds(0.5f);
            c.Metric("ice", Weather.Ice, "");
            if (!c.Check(Weather.Ice > 0.5f, $"the road freezes (ice {Weather.Ice:0.00})")) yield break;
            var ice = new float[3];
            yield return Stop(c, v, pad, fwd, "ice", ice);
            c.Check(dry[2] > 2f, $"brakes bite on a dry road ({dry[2]:0.0} m/s2)");
            c.Check(ice[2] > 0.1f && ice[2] < dry[2] * 0.7f, $"ice cuts the braking ({ice[2]:0.0} vs {dry[2]:0.0} m/s2; {ice[1]:0.0} vs {dry[1]:0.0} m)");
            g.Exit();
            Weather.Restore(false, 0f, 0f, 22f);
        }
    }

    /// <summary>Fords (README "fords"): where a dirt track crosses a river the track dips into shallow water; a 4x4
    /// drives through without flooding its engine, wades (buoyancy and drag felt) and climbs out on the far bank.</summary>
    class RiverFord : Scenario
    {
        public override string Id => "mobility.terrain.ford";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            var v = TestWorld.Vehicle("Scavenger") ?? TestWorld.Vehicle("Pickup");
            if (!v) { c.Block("no scavenger or pickup"); yield break; }
            var fords = MobilityKit.Fords(6000f);
            c.Metric("fords_found", fords.Count, "");
            if (fords.Count == 0) { c.Block("no dirt-track ford within 6 km of the start"); yield break; }
            var f = fords[0];
            float reach = f.half + 14f;
            var from = f.centre - f.across * reach; var to = f.centre + f.across * reach;
            yield return MobilityKit.Stream(c, from);
            float depth = t.WaterDepth(f.centre.x, f.centre.z);
            c.Metric("ford_depth", depth, "m");
            c.Note($"ford at {f.centre.x:0},{f.centre.z:0}: {depth:0.00} m deep, river half-width {f.half:0.0} m, {MobilityKit.Flat(f.centre, Vector3.zero):0} m from the origin; blockers at the start: {MobilityKit.Blockers(from, 2f)}");
            yield return TestWorld.Place(c, v, from, f.across, 1.5f);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var sys = v.GetComponent<VehicleSystems>();
            v.handbrake = false; v.throttleInput = 0.6f;
            float t0 = Time.time, wet = 0f; bool flooded = false;
            float total = MobilityKit.Flat(from, to);
            while (Time.time - t0 < 30f && Vector3.Dot(v.transform.position - from, f.across) < total)
            {
                wet = Mathf.Max(wet, v.InWater);
                flooded |= (sys.Faults & Fault.Flooded) != 0;
                v.steerInput = Mathf.Clamp(Mathf.DeltaAngle(v.transform.eulerAngles.y, MobilityKit.Yaw(f.across)) / 20f, -1f, 1f);
                yield return null;
            }
            float crossed = Vector3.Dot(v.transform.position - from, f.across);
            v.throttleInput = 0f; v.brakeInput = 1f; v.steerInput = 0f;
            c.Metric("ford_time", Time.time - t0, "s");
            c.Metric("wading", wet, "");
            c.Check(wet > 0.05f, $"the wheels go through water ({wet:0.00})");
            c.Check(!flooded && sys.Started, "the engine keeps breathing (no flood)");
            if (!c.Check(crossed >= total - 1f, $"climbs out on the far bank ({crossed:0} of {total:0} m)")) c.Note(TestWorld.State(v) + "; touching: " + TestWorld.Contacts(v));
            yield return MobilityKit.Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 5f);
            v.brakeInput = 0f; v.handbrake = true;
            c.Screenshot("ford");
            yield return null;
            g.Exit();
        }
    }
}
