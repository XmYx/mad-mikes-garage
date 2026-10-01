using System.Collections;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Two-wheelers (roadmap 24 / 26 Q3): parked on the kickstand it stands; mounted it is held upright;
    /// it pulls away and runs straight without wobbling over; D at speed leans it right and turns it, A back the
    /// other way (a sidecar outfit steers flat instead); braking stops it upright with the feet down, and getting off
    /// leaves it on its stand. The rider is never thrown.</summary>
    class BikeRide : Scenario
    {
        readonly string name;
        public BikeRide(string name) { this.name = name; }
        public override string Id => "mobility.bike." + name;
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle(name);
            var bal = v ? v.GetComponent<BikeBalance>() : null;
            if (!bal) { c.Block(name + " is not in the start fleet"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a 60 m lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, v, pad, fwd, 2f);
            c.Metric("parked_lean", bal.Lean, "deg");
            c.Check(Mathf.Abs(bal.Lean) < 25f && v.transform.up.y > 0.85f, $"parked, it stands (lean {bal.Lean:0} deg)");

            g.Enter(v);
            yield return new WaitForSeconds(1.2f);
            if (!c.Check(g.Current == v, "mounted the " + name)) yield break;
            c.Check(Mathf.Abs(bal.Lean) < 12f, $"the rider holds it upright at rest ({bal.Lean:0} deg)");
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;

            // pull away and run straight
            var p0 = v.transform.position;
            v.handbrake = false; v.throttleInput = 1f; v.steerInput = 0f;
            float t0 = Time.time, worst = 0f, top = 0f;
            while (Time.time - t0 < 4f)
            {
                if (Time.time - t0 > 1f) worst = Mathf.Max(worst, Mathf.Abs(bal.Lean));
                top = Mathf.Max(top, v.SpeedKmh);
                yield return null;
            }
            float ahead = Vector3.Dot(v.transform.position - p0, fwd);
            c.Metric("pull_away_4s", ahead, "m");
            c.Metric("speed_4s", v.SpeedKmh, "km/h");
            c.Metric("straight_lean_max", worst, "deg");
            if (!c.Check(ahead > 8f && !bal.Crashed, $"pulls away ({ahead:0.0} m in 4 s, {v.SpeedKmh:0} km/h)")) { c.Note(TestWorld.State(v) + "; crash: " + bal.LastCrash); yield break; }
            c.Check(worst < 12f, $"runs straight upright (worst lean {worst:0} deg)");

            // lean right, then left (S-bend on the lane)
            v.throttleInput = 0.4f;
            float y0 = v.transform.eulerAngles.y, rightLean = 0f, leftLean = 0f;
            v.steerInput = 0.5f;
            t0 = Time.time;
            while (Time.time - t0 < 1.2f) { rightLean = Mathf.Min(rightLean, bal.Lean); yield return null; }
            float turnR = Mathf.DeltaAngle(y0, v.transform.eulerAngles.y);
            v.steerInput = -0.5f;
            t0 = Time.time;
            while (Time.time - t0 < 1.6f) { leftLean = Mathf.Max(leftLean, bal.Lean); yield return null; }
            float turnBack = Mathf.DeltaAngle(y0, v.transform.eulerAngles.y) - turnR;
            v.steerInput = 0f;
            c.Metric("lean_right", rightLean, "deg"); c.Metric("lean_left", leftLean, "deg");
            c.Metric("turn_right", turnR, "deg"); c.Metric("turn_back", turnBack, "deg");
            if (!bal.sidecar) c.Check(rightLean < -6f && leftLean > 4f, $"D leans it right, A left ({rightLean:0} / {leftLean:0} deg)");
            c.Check(turnR > 4f && turnBack < -4f, $"it turns right, then back left ({turnR:0} / {turnBack:0} deg)");
            if (!c.Check(!bal.Crashed && g.Current == v, "the rider stays on through the bends")) { c.Note("crash: " + bal.LastCrash); yield break; }

            // stop upright
            v.throttleInput = 0f; v.brakeInput = 1f;
            var pb = v.transform.position;
            yield return MobilityKit.Until(() => Mathf.Abs(v.ForwardSpeed) < 0.2f, 6f);
            c.Metric("stop_distance", MobilityKit.Flat(pb, v.transform.position), "m");
            yield return new WaitForSeconds(0.8f);
            c.Check(Mathf.Abs(v.ForwardSpeed) < 0.3f && !bal.Crashed && Mathf.Abs(bal.Lean) < 15f, $"brakes to a stop, feet down ({bal.Lean:0} deg)");
            v.brakeInput = 0f; v.handbrake = true;
            c.Screenshot("stopped");
            yield return null;

            // off: on the kickstand
            g.Exit();
            yield return new WaitForSeconds(2f);
            c.Metric("stand_lean", bal.Lean, "deg");
            c.Check(g.Current == null && Mathf.Abs(bal.Lean) < 25f && v.transform.up.y > 0.85f, $"left on its stand ({bal.Lean:0} deg)");
        }
    }
}
