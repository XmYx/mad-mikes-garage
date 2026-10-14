using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>GTA-style towns: mountable kerbs and islands, poles that snap, furniture that gives way.</summary>
    public static class DriveThroughScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new DriveThrough(); }
    }

    /// <summary>DRIVE THROUGH: a sedan at ~50 km/h runs straight off a sidewalk, over the kerb, across a roundabout's
    /// ring and island and out the far side without being stopped or launched (keeps &gt; 60 % of its speed); then it
    /// hits a street lamp at ~50 km/h: the post snaps and falls (a rigid body, tilted past 45° after 2 s), the lamp goes
    /// dark, the car keeps going with a moderate crunch (not a wall stop).</summary>
    class DriveThrough : Scenario
    {
        public override string Id => "towns.drive_through";
        public override float Timeout => 150f;
        const string LampKey = "test:drive_through";

        class Run_ { public float v0, vMin = 999f, lift, along; public bool done; }

        /// <summary>Hold ~<paramref name="kmh"/> along the line from <paramref name="from"/> (flat <paramref name="dir"/>)
        /// until <paramref name="until"/> metres along it (or the time runs out).</summary>
        static IEnumerator Drive(VehicleDriver v, Vector3 from, Vector3 dir, float until, float kmh, float seconds, Run_ r, System.Action<float> each)
        {
            var right = Vector3.Cross(Vector3.up, dir);
            float t0 = Time.time;
            v.handbrake = false; v.brakeInput = 0f;
            while (Time.time - t0 < seconds)
            {
                var p = v.transform.position;
                r.along = Vector3.Dot(p - from, dir);
                if (r.along >= until) { r.done = true; break; }
                var fwd = v.transform.forward; fwd.y = 0f;
                float ang = Vector3.SignedAngle(fwd, dir, Vector3.up);
                float lat = -Vector3.Dot(p - from, right);                                     // the line is this far to the right
                v.steerInput = Mathf.Clamp(ang / 20f + lat * 0.35f, -1f, 1f);
                v.throttleInput = Mathf.Clamp((kmh - v.SpeedKmh) / 10f + 0.3f, 0f, 1f);
                r.lift = Mathf.Max(r.lift, v.Body.linearVelocity.y);
                each?.Invoke(r.along);
                yield return null;
            }
            v.throttleInput = 0f; v.steerInput = 0f;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var w = g.World; var terrain = DeformableTerrain.Instance;
            var v = TestWorld.Vehicle("Sedan");
            if (!v || !terrain) { c.Block("no sedan in the start fleet"); yield break; }
            // the roundabout nearest the start, and a paved street leaving it
            TownPlan plan = null; TownJunction junc = null; TownStreet leg = null; float best = float.MaxValue;
            var focus = new Vector2(g.FocusPos.x, g.FocusPos.z);
            foreach (var st in w.settlements)
            {
                if (st.plan == null) continue;
                foreach (var j in st.plan.junctions)
                {
                    if (j.ring <= 0f) continue;
                    TownStreet s0 = null;
                    foreach (var s in st.plan.streets)
                        if ((s.juncA == j.id || s.juncB == j.id) && s.paved && s.kind != StreetKind.Pedestrian && s.walk > 1f && s.len > j.ring + 50f
                            && (s0 == null || s.kind != StreetKind.Local)) s0 = s;                     // boulevards first: no wrecks parked at their kerbs
                    if (s0 == null) continue;
                    float d = (st.plan.World(j.p) - focus).sqrMagnitude;
                    if (d < best) { best = d; plan = st.plan; junc = j; leg = s0; }
                }
            }
            if (plan == null) { c.Block("no roundabout with a long paved leg in this world"); yield break; }
            DayNight.SetHours(11f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }

            // ---- phase A: sidewalk -> kerb -> ring -> island -> ring -> far side, straight
            var outward = leg.juncA == junc.id ? leg.dir : -leg.dir;
            var nrm = new Vector2(-outward.y, outward.x);
            var startL = junc.p + outward * (junc.ring + 45f) + nrm * (leg.half + leg.walk * 0.5f);
            var aimL = junc.p + nrm * (junc.island * 0.6f);                                     // over the island, clear of its centrepiece
            var dirL = (aimL - startL).normalized;
            var endL = aimL + dirL * (junc.ring + junc.walk + 4f);
            Vector3 W(Vector2 l) { var p = plan.World(l); return new Vector3(p.x, terrain.Height(p.x, p.y), p.y); }
            var start = W(startL); var end = W(endL);
            var dir = end - start; dir.y = 0f; float total = dir.magnitude; dir /= total;
            // what the line crosses
            int island = 0, kerb = 0, walk = 0; float firstKerb = -1f;
            for (float s = 0f; s < total; s += 0.25f)
            {
                var q = start + dir * s;
                var f = w.Sample(q.x, q.z).feature;
                if (f == TownPlan.FIsland) island++;
                if (f == TownPlan.FSidewalk || f == TownPlan.FPlaza) walk++;
                if (f == TownPlan.FKerb) { kerb++; if (firstKerb < 0f && s > 3f) firstKerb = s; }
            }
            c.Metric("line_island_m", island * 0.25f, "m"); c.Metric("line_kerb_m", kerb * 0.25f, "m"); c.Metric("line_sidewalk_m", walk * 0.25f, "m");
            c.Check(island > 0 && kerb > 0 && walk > 0, $"the line runs over sidewalk ({walk * 0.25f:0} m), kerbs ({kerb * 0.25f:0.0} m) and the island ({island * 0.25f:0.0} m)");
            if (firstKerb < 0f) firstKerb = total * 0.5f;
            yield return TestWorld.Place(c, v, start, dir, 2.5f);                               // streams the town in
            // the lane itself is cleared of street furniture and lamps: this phase is about the ground
            int cleared = 0;
            var near = new List<Component>();
            near.AddRange(Object.FindObjectsByType<Breakaway>(FindObjectsSortMode.None));
            foreach (var b in near)
            {
                if (!b) continue;
                var p = b.transform.position - start; p.y = 0f;
                float a = Vector3.Dot(p, dir);
                if (a < -4f || a > total + 2f || Vector3.Cross(dir, p).magnitude > 2.2f) continue;
                Object.Destroy(b.gameObject); cleared++;
            }
            c.Fixture($"cleared {cleared} props from the lane");
            yield return TestWorld.Place(c, v, start, dir, 0.6f);
            if (g.Current != v) g.Enter(v);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(g.Current == v, "in the sedan")) yield break;
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var run = new Run_();
            bool sampled = false;
            v.handbrake = false; v.Body.linearVelocity = dir * (50f / 3.6f);                    // rolling at 50 km/h from the start
            yield return Drive(v, start, dir, total, 52f, 25f, run, along =>
            {
                if (!sampled && along >= firstKerb - 3f) { sampled = true; run.v0 = v.SpeedKmh; }
                if (sampled) run.vMin = Mathf.Min(run.vMin, v.SpeedKmh);
            });
            c.Metric("entry_kmh", run.v0, "km/h"); c.Metric("min_kmh", run.vMin, "km/h"); c.Metric("lift_ms", run.lift, "m/s");
            if (!c.Check(run.done, $"crossed the roundabout ({run.along:0}/{total:0} m)")) c.Note(TestWorld.State(v) + "; " + TestWorld.Contacts(v));
            c.Check(run.v0 > 35f, $"at speed at the first kerb ({run.v0:0} km/h)");
            c.Check(run.v0 > 0f && run.vMin > run.v0 * 0.6f, $"not stopped by kerbs or the island: kept {run.vMin:0} of {run.v0:0} km/h");
            c.Check(run.lift < 2.5f, $"not launched ({run.lift:0.0} m/s upward at most)");
            c.Screenshot("drive_through_island");
            yield return null;
            v.throttleInput = 0f; v.brakeInput = 1f;
            yield return Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 6f);
            v.brakeInput = 0f;

            // ---- phase B: a street lamp at ~50 km/h
            if (!TestWorld.Pad(9f, 70f, out var pad, out var fwd)) { c.Block("no level pad with a clear 70 m lane"); yield break; }
            fwd.y = 0f; fwd.Normalize();
            yield return TestWorld.Place(c, v, pad, fwd);
            var lampAt = pad + fwd * 48f + Vector3.Cross(Vector3.up, fwd) * 0.35f;
            lampAt.y = terrain.Height(lampAt.x, lampAt.z) - 0.04f;
            var lamp = FurnitureLibrary.WorldFixture("streetlamp_cobra", null, lampAt, Quaternion.LookRotation(-fwd), terrain.worldPropMaterial);
            if (!lamp) { c.Block("no street lamp piece"); yield break; }
            var pole = BreakablePole.ForLamp(lamp.gameObject, "streetlamp_cobra", LampKey);
            var wl = lamp.gameObject.AddComponent<WorldLamp>(); wl.key = LampKey; wl.town = 0;
            c.Fixture("a highway lamp 48 m ahead");
            yield return new WaitForSeconds(0.5f);
            var dmg = v.GetComponent<VehicleDamage>();
            float frame0 = dmg ? dmg.FrameDamage : 0f, vHit = 0f, vPrev = 0f, snapAt = -1f, vAfter = 999f;
            var run2 = new Run_();
            v.handbrake = false; v.Body.linearVelocity = fwd * (50f / 3.6f);
            yield return Drive(v, pad, fwd, 70f, 52f, 20f, run2, _ =>
            {
                if (snapAt < 0f && pole.state != BreakablePole.State.Standing) { snapAt = Time.time; vHit = vPrev; }
                if (snapAt > 0f && Time.time - snapAt < 0.4f) vAfter = Mathf.Min(vAfter, v.SpeedKmh);   // right after the hit
                vPrev = v.SpeedKmh;
            });
            if (vAfter > 900f) vAfter = 0f;
            v.throttleInput = 0f; v.brakeInput = 1f;
            if (snapAt > 0f) yield return Until(() => Time.time - snapAt > 2f, 3f);
            float tilt = pole ? Vector3.Angle(pole.transform.up, Vector3.up) : 0f;
            float worst = 0f;
            var ch = v.GetComponent<VehicleChassis>();
            if (ch) foreach (var sk in ch.Sockets) if (sk.Current) worst = Mathf.Max(worst, sk.Current.damage);
            float frame = dmg ? dmg.FrameDamage - frame0 : 0f;
            c.Metric("lamp_hit_kmh", vHit, "km/h"); c.Metric("lamp_after_kmh", vAfter, "km/h"); c.Metric("pole_tilt", tilt, "deg");
            c.Metric("frame_damage", frame, "share"); c.Metric("worst_part_damage", worst, "share");
            if (c.Check(snapAt > 0f, $"the post snapped ({(pole ? pole.state.ToString() : "gone")})"))
            {
                c.Check(pole && pole.Body, "it came off as a rigid body");
                c.Check(tilt > 45f, $"fell over: {tilt:0}° from upright 2 s later");
                c.Check(lamp && lamp.broken && !lamp.Glowing, "the lamp went dark");
                c.Check(vHit > 35f, $"hit it at speed ({vHit:0} km/h)");
                c.Check(vAfter > vHit * 0.5f && vAfter < vHit * 0.99f, $"kept going: {vAfter:0} of {vHit:0} km/h (lost some, no wall stop)");
                c.Check(worst < 0.8f && frame < 0.3f && (worst > 0f || frame > 0f), $"a moderate crunch (worst part {worst:0.00}, frame +{frame:0.00})");
            }
            c.Screenshot("drive_through_lamp");
            yield return null;
            v.brakeInput = 1f;
            yield return Until(() => Mathf.Abs(v.ForwardSpeed) < 0.3f, 6f);
            v.brakeInput = 0f; v.handbrake = true;
            if (lamp) Object.Destroy(lamp.gameObject);
            TownLights.Knocked.Remove(LampKey); TownLights.Smashed.Remove(LampKey); BreakablePole.Fallen.Remove(LampKey);
        }

        static IEnumerator Until(System.Func<bool> ok, float seconds) => VehicleFixScenarios.Until(ok, seconds);
    }
}
