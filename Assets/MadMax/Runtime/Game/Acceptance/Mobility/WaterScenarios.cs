using System.Collections;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>A boat trip (roadmap 26 Q3 "steer, moor"): a found skiff floats at its draft in open water off the
    /// nearest shore; it starts, runs out under power, turns on the rudder, slows and backs up astern, and getting off
    /// puts the skipper on the deck (not in the sea) while the idle boat stays put; back aboard from the deck.</summary>
    class BoatTrip : Scenario
    {
        readonly string design;
        public BoatTrip(string design) { this.design = design; }
        public override string Id => "mobility.boat." + design;
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (!MobilityKit.FindWater(2f, 30f, 4000f, out var at, out var outward, out var shore)) { c.Block("no open water within 4 km of the start"); yield break; }
            c.Metric("water_distance", MobilityKit.Flat(at, Vector3.zero), "m");
            yield return MobilityKit.Stream(c, shore);
            float level = t.WaterLevel(at.x, at.z);
            if (float.IsNaN(level)) { c.Block("the water there is not loaded"); yield break; }
            var v = g.SpawnFound(design, new Vector3(at.x, level + 0.3f, at.z), Quaternion.LookRotation(outward));
            var boat = v ? v.GetComponent<BoatModel>() : null;
            if (!c.Check(boat, design + " spawns as a boat")) yield break;
            c.Fixture($"a found {design} put on the water {MobilityKit.Flat(at, shore):0} m off the shore ({t.WaterDepth(at.x, at.z):0.0} m deep)");
            g.Enter(v);                                                                            // the driven boat is the streaming focus (never frozen far off)
            yield return new WaitForSeconds(3f);
            float draft = level - (v.transform.position.y + boat.keelY);
            c.Metric("draft", draft, "m");
            c.Check(boat.Afloat && boat.Submersion < 0.6f && v.transform.up.y > 0.9f, $"it floats upright at its draft ({draft:0.00} m, submersion {boat.Submersion:0.00})");

            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var p0 = v.transform.position;
            v.handbrake = false; v.throttleInput = 1f; v.steerInput = 0f;
            yield return new WaitForSeconds(6f);
            float run = MobilityKit.Flat(p0, v.transform.position);
            c.Metric("run_6s", run, "m"); c.Metric("speed", boat.Speed, "m/s");
            if (!c.Check(run > 8f && boat.Speed > 1.5f, $"runs out under power ({run:0} m in 6 s, {boat.Speed:0.0} m/s)")) c.Note(TestWorld.State(v));
            float y0 = v.transform.eulerAngles.y;
            v.steerInput = 1f;
            yield return new WaitForSeconds(3f);
            float turned = Mathf.Abs(Mathf.DeltaAngle(y0, v.transform.eulerAngles.y));
            c.Metric("rudder_turn_3s", turned, "deg");
            c.Check(turned > 25f, $"the rudder turns it ({turned:0} deg in 3 s)");
            v.steerInput = 0f; v.throttleInput = 0f; v.brakeInput = 1f;
            yield return MobilityKit.Until(() => boat.Reversing, 12f);
            var pr = v.transform.position; var fw = v.transform.forward;
            yield return new WaitForSeconds(4f);
            float astern = -Vector3.Dot(v.transform.position - pr, fw);
            c.Metric("astern_4s", astern, "m");
            c.Check(boat.Reversing && astern > 0.8f, $"S slows it and backs it up astern ({astern:0.0} m)");
            v.brakeInput = 0f;
            yield return new WaitForSeconds(2f);

            // off onto the deck: moored, not swimming
            g.Exit();
            yield return new WaitForSeconds(1f);
            var moored = v.transform.position;
            float lvl = t.WaterLevel(g.Player.transform.position.x, g.Player.transform.position.z);
            c.Check(!g.Player.Swimming && MobilityKit.Flat(g.Player.transform.position, v.transform.position) < 4f && (float.IsNaN(lvl) || g.Player.transform.position.y > lvl - 0.2f),
                $"getting off puts the skipper on the deck (swimming {g.Player.Swimming}, {MobilityKit.Flat(g.Player.transform.position, v.transform.position):0.0} m from the hull)");
            yield return new WaitForSeconds(5f);
            float drift = MobilityKit.Flat(moored, v.transform.position);
            c.Metric("idle_drift_5s", drift, "m");
            c.Check(drift < 3f, $"the idle boat stays put ({drift:0.0} m in 5 s)");
            c.Check(!g.Player.Swimming, "still dry on the deck");
            c.Screenshot("moored");
            yield return null;
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            c.Check(g.Current == v, "back at the helm from the deck");
            g.Exit();
        }
    }

    /// <summary>The Iron Eel (roadmap 26 Q3 "dive / surface, cabin oxygen and power"): it floats on the surface with the
    /// diesel running; flooding the ballast takes it under (the diesel can't breathe and stops), the trim holds the
    /// depth, the electric motor drives it on the battery while the crew uses up the cabin air; blowing the tanks
    /// brings it back up, the hatch air refreshes, the diesel restarts and charges; standing up leaves the skipper
    /// inside the dry hull. Ballast is driven through <see cref="Submarine.ballastInput"/>.</summary>
    class SubmarineDive : Scenario
    {
        public override string Id => "mobility.submarine";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 220f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (!MobilityKit.FindWater(12f, 40f, 6000f, out var at, out var outward, out var shore)) { c.Block("no water 12 m deep within 6 km of the start"); yield break; }
            yield return MobilityKit.Stream(c, shore);
            float level = t.WaterLevel(at.x, at.z);
            if (float.IsNaN(level)) { c.Block("the water there is not loaded"); yield break; }
            var v = g.SpawnFound("IronEel", new Vector3(at.x, level + 0.5f, at.z), Quaternion.LookRotation(outward));
            var sub = v ? v.GetComponent<Submarine>() : null; var boat = v ? v.GetComponent<BoatModel>() : null;
            if (!c.Check(sub && boat, "the Iron Eel spawns as a submarine")) yield break;
            var sys = v.GetComponent<VehicleSystems>();
            if (sys.fuel < 20f) { sys.fuel = Mathf.Min(sys.fuelCapacity, 60f); c.Fixture("60 L of diesel aboard"); }
            c.Fixture($"the Iron Eel put on {t.WaterDepth(at.x, at.z):0} m of water {MobilityKit.Flat(at, shore):0} m off the shore");
            g.Enter(v);                                                                            // the driven sub is the streaming focus
            yield return new WaitForSeconds(3f);
            c.Check(boat.Afloat && !sub.Submerged && sub.Snorkel, $"it floats on the surface (submersion {boat.Submersion:0.00})");
            yield return TestWorld.StartEngine(c, v);
            v.throttleInput = 0f;
            float air0 = sub.air;
            float t0 = Time.time;
            while (!(sub.Submerged && sub.Depth > 2f) && Time.time - t0 < 45f) { sub.ballastInput = 1f; yield return null; }
            sub.ballastInput = 0f;
            c.Metric("dive_time", Time.time - t0, "s");
            if (!c.Check(sub.Submerged && sub.Depth > 2f, $"flooding the ballast takes it under ({sub.Depth:0.0} m, ballast {sub.ballast * 100f:0} %)")) { c.Note(sub.Status); yield break; }
            c.Check(!sub.Snorkel && (!sys.Started), "submerged the diesel can't breathe and stops");
            float d0 = sub.Depth;
            yield return new WaitForSeconds(5f);
            c.Metric("depth_drift_5s", sub.Depth - d0, "m");
            c.Check(Mathf.Abs(sub.Depth - d0) < 1.5f && sub.Submerged, $"the trim holds the depth ({d0:0.0} -> {sub.Depth:0.0} m)");
            float batt0 = sub.battery; var p0 = v.transform.position;
            v.throttleInput = 1f;
            yield return new WaitForSeconds(6f);
            v.throttleInput = 0f;
            float run = MobilityKit.Flat(p0, v.transform.position);
            c.Metric("electric_run_6s", run, "m");
            c.Metric("battery_used", batt0 - sub.battery, "Wh");
            c.Metric("air_used", air0 - sub.air, "%");
            c.Check(run > 3f, $"the electric motor drives it under water ({run:0.0} m in 6 s)");
            c.Check(sub.battery < batt0, "driving draws the battery");
            c.Check(sub.air < air0, $"the crew breathes the cabin air down ({air0:0.0} -> {sub.air:0.0} %)");
            c.Screenshot("submerged");
            yield return null;

            t0 = Time.time;
            while (!sub.Snorkel && Time.time - t0 < 45f) { sub.ballastInput = -1f; yield return null; }
            c.Metric("surface_time", Time.time - t0, "s");
            if (!c.Check(sub.Snorkel, "blowing the tanks brings it up")) { sub.ballastInput = 0f; yield break; }
            float airUp = sub.air;
            yield return new WaitForSeconds(3f);
            sub.ballastInput = 0f;
            c.Check(sub.air > airUp || sub.air >= 99.9f, $"the hatch air refreshes ({airUp:0.0} -> {sub.air:0.0} %)");
            yield return TestWorld.StartEngine(c, v);
            v.throttleInput = 0f;
            float b1 = sub.battery;
            yield return new WaitForSeconds(3f);
            c.Check(sys.Started && sub.battery > b1, $"the diesel runs again on the surface and charges ({b1:0} -> {sub.battery:0} Wh)");
            var space = v.GetComponent<InteriorSpace>();
            g.Exit();
            yield return new WaitForSeconds(0.6f);
            c.Check(g.Current == null && (!space || g.Player.Interior == space) && !g.Player.Swimming, "standing up leaves the skipper inside the dry hull");
        }
    }
}
