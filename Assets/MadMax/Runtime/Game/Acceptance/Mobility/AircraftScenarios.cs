using System.Collections;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    static class Pilot
    {
        /// <summary>Move the throttle lever towards <paramref name="target"/> (the lever has a rate; on the ground a pull
        /// below -0.1 brakes, so on the ground the lever only creeps back).</summary>
        public static void Lever(FlightModel fm, float target, bool ground) =>
            fm.throttleAxis = Mathf.Clamp((target - fm.Throttle) * 3f, ground ? -0.09f : -1f, 1f);

        /// <summary>Start the engine: nudge the lever until it catches, then pull it back and hold the brakes.</summary>
        public static IEnumerator Start(ScenarioContext c, VehicleDriver v, FlightModel fm)
        {
            var sys = v.GetComponent<VehicleSystems>();
            float t0 = Time.time;
            while (sys && !sys.Started && Time.time - t0 < 14f)
            {
                fm.rollInput = 0f; fm.pitchInput = 0f;
                fm.throttleAxis = fm.Throttle < 0.2f ? 0.5f : 0f;
                yield return null;
            }
            c.Metric("engine_start", Time.time - t0, "s");
            c.Check(!sys || sys.Started, "the engine starts");
            t0 = Time.time;
            while (Time.time - t0 < 0.8f) { fm.throttleAxis = -1f; yield return null; }
            fm.throttleAxis = 0f;
        }

        /// <summary>Bank towards a heading (deg) and hold an attitude for an altitude (m above ground).</summary>
        public static void Fly(VehicleDriver v, FlightModel fm, float heading, float altTarget, float maxBank = 30f, float pitchTrim = 2f)
        {
            MobilityKit.Attitude(v.transform, out float bank, out float pitch);
            float err = Mathf.DeltaAngle(v.transform.eulerAngles.y, heading);
            float bankWant = Mathf.Clamp(err * 1.1f, -maxBank, maxBank);
            fm.rollInput = Mathf.Clamp((bankWant - bank) / 20f, -1f, 1f);
            float thetaWant = Mathf.Clamp((altTarget - fm.Altitude) * 0.6f - fm.VerticalSpeed * 1.2f, -7f, 10f) + pitchTrim;
            fm.pitchInput = Mathf.Clamp((thetaWant - pitch) / 15f, -0.6f, 0.6f);
        }

        /// <summary>Heading that intercepts and follows a line through <paramref name="p0"/> along <paramref name="dir"/>.</summary>
        public static float Track(Vector3 pos, Vector3 p0, Vector3 dir, float gain = 1f, float max = 40f)
        {
            var right = new Vector3(dir.z, 0f, -dir.x);
            float cross = Vector3.Dot(pos - p0, right);
            return MobilityKit.Yaw(dir) - Mathf.Clamp(cross * gain, -max, max);
        }
    }

    /// <summary>Aircraft on the ground (roadmap 25 user additions): engine start, taxi on a part lever, a tight taxi
    /// turn on the nose wheel and differential brakes, S brakes to a stop, held S at a standstill reverses the prop
    /// pitch and backs up (≤ 3 m/s). Driven through <see cref="FlightModel.throttleAxis"/> / <see cref="FlightModel.rollInput"/>.</summary>
    class AircraftGround : Scenario
    {
        readonly string name;
        public AircraftGround(string name) { this.name = name; }
        public override string Id => "mobility.aircraft_ground." + name;
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle(name);
            var fm = v ? v.GetComponent<FlightModel>() : null;
            if (!fm) { c.Block(name + " is not in the start fleet"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a 60 m lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, v, pad, fwd, 1.5f);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return Pilot.Start(c, v, fm);
            if (c.Failed) yield break;

            var p0 = v.transform.position;
            float t0 = Time.time;
            while (Time.time - t0 < 4f) { Pilot.Lever(fm, 0.4f, true); fm.rollInput = 0f; yield return null; }
            float taxi = Vector3.Dot(v.transform.position - p0, fwd);
            c.Metric("taxi_4s", taxi, "m");
            c.Metric("taxi_speed", v.ForwardSpeed, "m/s");
            if (!c.Check(taxi > 4f && !fm.Airborne, $"taxis on a part lever ({taxi:0.0} m in 4 s, {v.ForwardSpeed:0.0} m/s)")) c.Note(TestWorld.State(v));

            float y0 = v.transform.eulerAngles.y;
            t0 = Time.time;
            while (Time.time - t0 < 1.5f) { Pilot.Lever(fm, 0.3f, true); fm.rollInput = 1f; yield return null; }
            float turned = Mathf.DeltaAngle(y0, v.transform.eulerAngles.y);
            c.Metric("taxi_turn_1_5s", turned, "deg");
            c.Check(turned > 20f, $"D turns it tightly on the ground ({turned:0} deg in 1.5 s)");
            fm.rollInput = 0f;

            var pb = v.transform.position;
            t0 = Time.time;
            while (Mathf.Abs(v.ForwardSpeed) > 0.2f && Time.time - t0 < 6f) { fm.throttleAxis = -1f; yield return null; }
            c.Metric("taxi_stop", MobilityKit.Flat(pb, v.transform.position), "m");
            c.Check(Mathf.Abs(v.ForwardSpeed) <= 0.2f, "S brakes it to a stop");

            var pr = v.transform.position; var back = v.transform.forward;
            bool rev = false; float fastest = 0f;
            t0 = Time.time;
            while (Time.time - t0 < 4f) { fm.throttleAxis = -1f; rev |= fm.Reversing; fastest = Mathf.Max(fastest, -v.ForwardSpeed); yield return null; }
            fm.throttleAxis = 0f;
            float backed = -Vector3.Dot(v.transform.position - pr, back);
            c.Metric("reverse_4s", backed, "m");
            c.Metric("reverse_speed_max", fastest, "m/s");
            c.Check(rev && backed > 0.8f, $"held S at a standstill reverses the prop and backs up ({backed:0.0} m)");
            c.Check(fastest <= 3.3f, $"backing up stays slow ({fastest:0.0} m/s)");
            t0 = Time.time;
            while (Time.time - t0 < 0.5f) { fm.throttleAxis = 0f; yield return null; }
            g.Exit();
            yield return new WaitForSeconds(0.5f);
            c.Check(g.Current == null && !g.Player.Swimming, "the pilot climbs out");
        }
    }

    /// <summary>A flight from the nearest airfield (roadmap 26 Q3 "taxi / take-off / land"): full lever down the runway,
    /// rotate, lift off within the strip, climb to 30 m, fly a rectangular circuit away from the hangar, line up on the
    /// runway and land on it without a hard landing, then brake to a stop. A small autopilot works the production
    /// inputs (lever rate, roll bar, pitch) the way a pilot would; teleporting to the airfield is disclosed.</summary>
    class AircraftCircuit : Scenario
    {
        readonly string name;
        public AircraftCircuit(string name) { this.name = name; }
        public override string Id => "mobility.aircraft." + name;
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 320f;

        enum Leg { Roll, Climb, Turn1, Downwind, Turn2, Final, Flare, Landed }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle(name);
            var fm = v ? v.GetComponent<FlightModel>() : null;
            if (!fm) { c.Block(name + " is not in the start fleet"); yield break; }
            var site = MobilityKit.Airfield(4000f);
            if (site == null) { c.Block("no airfield within 4 km of the start"); yield break; }
            var w0 = site.ToWorld(0f, 0f); var w1 = site.ToWorld(0f, 1f); var wx = site.ToWorld(1f, 0f);
            var C = new Vector3(w0.x, 0f, w0.y);
            var A = new Vector3(w1.x - w0.x, 0f, w1.y - w0.y).normalized;
            var hangar = new Vector3(wx.x - w0.x, 0f, wx.y - w0.y).normalized;
            var side = -hangar;                                                           // the circuit flies away from the hangar
            float half = site.halfLen;
            c.Metric("runway_length", half * 2f, "m");
            var start = C + A * (-half + 12f);
            yield return MobilityKit.Stream(c, start);
            yield return TestWorld.Place(c, v, start, A, 1.5f);
            c.Fixture($"{name} moved to the runway threshold of the airfield at {C.x:0},{C.z:0}");
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return Pilot.Start(c, v, fm);
            if (c.Failed) yield break;
            var sys = v.GetComponent<VehicleSystems>();
            var ep = v.Engine ? v.Engine.GetComponent<VehiclePart>() : null;
            float dmg0 = ep ? ep.damage : 0f, fuel0 = sys ? sys.fuel : 0f;

            var leg = Leg.Roll;
            float vr = fm.kind == FlightModel.Kind.Gyro ? 13f : 15f, cruiseAlt = 30f, downwind = 160f;
            float liftS = float.NaN, liftV = 0f, maxAlt = 0f, tdVs = 0f, legT = Time.time, t0 = Time.time, landedAt = -1f;
            float lastVs = 0f;
            bool everAirborne = false, touched = false;
            var lineP = C; var lineD = A;
            while (Time.time - t0 < 260f && g.Current == v)
            {
                var p = v.transform.position; p.y = 0f;
                float s = Vector3.Dot(p - C, A);
                maxAlt = Mathf.Max(maxAlt, fm.Altitude);
                if (fm.Airborne) { everAirborne = true; lastVs = fm.VerticalSpeed; }
                switch (leg)
                {
                    case Leg.Roll:
                        Pilot.Lever(fm, 1f, true);
                        fm.rollInput = Mathf.Clamp(Mathf.DeltaAngle(v.transform.eulerAngles.y, Pilot.Track(p, C, A, 1.5f, 15f)) / 15f, -1f, 1f);
                        fm.pitchInput = fm.Airspeed > vr ? 0.5f : 0f;
                        if (fm.Airborne) { leg = Leg.Climb; liftS = s; liftV = fm.Airspeed; legT = Time.time; }
                        else if (s > half) { c.Check(false, $"lifts off within the runway (still rolling at its end, {fm.Airspeed:0.0} m/s)"); leg = Leg.Landed; }
                        break;
                    case Leg.Climb:
                        Pilot.Lever(fm, 1f, false);
                        Pilot.Fly(v, fm, Pilot.Track(p, C, A), Mathf.Min(cruiseAlt, 6f + (Time.time - legT) * 4f), 15f, 4f);
                        if (s > half + 120f) { leg = Leg.Turn1; legT = Time.time; }
                        break;
                    case Leg.Turn1:
                        Pilot.Lever(fm, 0.85f, false);
                        Pilot.Fly(v, fm, MobilityKit.Yaw(side), cruiseAlt);
                        if (Mathf.Abs(Mathf.DeltaAngle(v.transform.eulerAngles.y, MobilityKit.Yaw(side))) < 20f) { leg = Leg.Downwind; legT = Time.time; }
                        break;
                    case Leg.Downwind:
                        Pilot.Lever(fm, 0.8f, false);
                        Pilot.Fly(v, fm, Pilot.Track(p, C + side * downwind, -A), cruiseAlt);
                        if (s < -half - 260f) { leg = Leg.Turn2; legT = Time.time; }
                        break;
                    case Leg.Turn2:
                        Pilot.Lever(fm, 0.8f, false);
                        Pilot.Fly(v, fm, MobilityKit.Yaw(-side), cruiseAlt);
                        if (Mathf.Abs(Vector3.Dot(p - C, side)) < 70f) { leg = Leg.Final; legT = Time.time; }
                        break;
                    case Leg.Final:
                    {
                        float sTouch = -half + 40f;
                        float want = Mathf.Clamp((sTouch - s) * 0.09f + 1f, 1f, cruiseAlt);
                        Pilot.Lever(fm, Mathf.Clamp(0.45f + (want - fm.Altitude) * 0.05f + (14f - fm.Airspeed) * 0.05f, 0.05f, 1f), false);
                        Pilot.Fly(v, fm, Pilot.Track(p, C, A, 1.5f, 30f), want, 15f);
                        if (fm.Altitude < 3f && s > -half - 30f) { leg = Leg.Flare; legT = Time.time; }
                        break;
                    }
                    case Leg.Flare:
                        Pilot.Lever(fm, 0f, false);
                        Pilot.Fly(v, fm, Pilot.Track(p, C, A, 1.5f, 15f), 0f, 8f, 6f);
                        fm.pitchInput = Mathf.Max(fm.pitchInput, 0.1f);
                        if (!fm.Airborne) { tdVs = lastVs; leg = Leg.Landed; landedAt = s; touched = true; }
                        break;
                }
                if (leg == Leg.Landed) break;
                if (everAirborne && !fm.Airborne && leg != Leg.Roll && leg != Leg.Flare && leg != Leg.Final) { c.Check(false, $"stays airborne in the {leg} leg (alt {fm.Altitude:0.0} m)"); break; }
                if (Time.time - legT > 90f) { c.Check(false, $"the {leg} leg finishes within 90 s"); break; }
                yield return null;
            }
            c.Note($"ended in {leg}: s {Vector3.Dot(v.transform.position - C, A):0} m, side {Vector3.Dot(v.transform.position - C, side):0} m, alt {fm.Altitude:0.0} m, airspeed {fm.Airspeed:0.0} m/s, current {(g.Current ? g.Current.name : "none")}");
            if (!float.IsNaN(liftS))
            {
                c.Metric("takeoff_roll", liftS - (-half + 12f), "m");
                c.Metric("liftoff_speed", liftV, "m/s");
                c.Check(liftS < half, $"lifts off within the runway ({liftS - (-half + 12f):0} m roll at {liftV:0.0} m/s)");
            }
            c.Metric("max_altitude", maxAlt, "m");
            c.Check(maxAlt > 20f, $"climbs ({maxAlt:0} m above the ground)");
            if (!c.Check(touched && g.Current == v, "flies the circuit and lands")) { c.Screenshot("circuit_end"); yield return null; yield break; }
            c.Metric("touchdown_vs", tdVs, "m/s");
            c.Metric("touchdown_along_runway", landedAt + half, "m");
            c.Check(tdVs > -3f, $"a soft touchdown ({tdVs:0.0} m/s)");
            c.Check(landedAt > -half - 10f && landedAt < half, $"touches down on the runway ({landedAt + half:0} m along)");
            float tb = Time.time;
            while (Mathf.Abs(v.ForwardSpeed) > 0.3f && Time.time - tb < 20f) { fm.throttleAxis = -1f; fm.rollInput = 0f; fm.pitchInput = 0f; yield return null; }
            fm.throttleAxis = 0f;
            c.Check(Mathf.Abs(v.ForwardSpeed) <= 0.3f, "brakes to a stop after landing");
            c.Check(g.Current == v && (!ep || ep.damage <= dmg0 + 0.01f), "the pilot is still aboard and the engine unharmed");
            if (sys) c.Metric("fuel_used", fuel0 - sys.fuel, "L");
            c.Screenshot("landed");
            yield return null;
            g.Exit();
        }
    }
}
