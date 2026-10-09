using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Vehicle fixes (wave 3): fuel and temperature gauges, leaving the bus, crash ejection, dry boat hulls.</summary>
    public static class VehicleFixScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new GaugeReadings();
            yield return new BusExit();
            yield return new CrashEjection();
            yield return new BoatDryHull();
        }

        /// <summary>Wait until <paramref name="ok"/> holds or <paramref name="seconds"/> pass (frame by frame).</summary>
        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        internal static VehicleDriver Find(ScenarioContext c, string name, Vector3 near)
        {
            var v = TestWorld.Vehicle(name);
            if (v) return v;
            var t = DeformableTerrain.Instance;
            var p = near + Vector3.up * 2f;
            if (t) p.y = t.Height(p.x, p.z) + 1f;
            v = c.Game.SpawnFound(name, p, Quaternion.identity);
            if (v) c.Fixture("spawned a " + name + " (not in the start fleet)");
            return v;
        }

        internal static float Flat(Vector3 a) { a.y = 0f; return a.magnitude; }
    }

    /// <summary>FUEL AND TEMPERATURE GAUGES: the HUD vehicle panel reads the tank and the engine temperature of
    /// <see cref="VehicleSystems"/> through <see cref="VehicleGauges"/> (needle positions, the low-fuel warning under
    /// 10 %, cold / normal / hot / critical bands on the 110 °C and 125 °C limits); the in-cabin dashboard draws the
    /// same reading; °F follows the units setting; air-cooled engines read the head, pedal bikes show neither.</summary>
    class GaugeReadings : Scenario
    {
        public override string Id => "vehicle.gauges";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle("Sedan");
            if (!v) v = TestWorld.Vehicle("Pickup");
            var sys = v ? v.GetComponent<VehicleSystems>() : null;
            if (!sys) { c.Block("no sedan or pickup in the fleet"); yield break; }
            if (g.cameraRig && (g.cameraRig.mode == ViewMode.FirstPerson || g.cameraRig.mode == ViewMode.Hood || g.cameraRig.mode == ViewMode.Bumper))
            { g.cameraRig.mode = ViewMode.Isometric; c.Fixture("isometric view (the screen panel hides in first person)"); }
            if (g.Current != v) { if (g.Current) g.Exit(); g.Enter(v); }
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(g.Current == v, "in the driver's seat")) yield break;
            float fuel0 = sys.fuel;
            var s = GameSettings.Current; bool metric0 = s.metric;

            IEnumerator Drawn()
            {
                int f0 = Time.frameCount;
                yield return VehicleFixScenarios.Until(() => VehicleGauges.LastHud.frame > f0 + 1 && VehicleGauges.LastHud.sys == sys, 3f);
            }

            // ---- fuel: needle follows the tank, warning under 10 %
            sys.fuel = sys.fuelCapacity * 0.6f;
            c.Fixture("tank at 60 %");
            yield return Drawn();
            var rd = VehicleGauges.LastHud;
            if (!c.Check(rd.sys == sys && rd.fuelShown, "the HUD vehicle panel draws the fuel gauge")) yield break;
            c.Metric("fuel_needle_60", rd.fuelNeedle, "");
            c.Check(Mathf.Abs(rd.fuelNeedle - 0.6f) < 0.02f && !rd.low, $"needle at 60 % ({rd.fuelNeedle:0.00}), no warning");
            sys.fuel = sys.fuelCapacity * 0.05f;
            c.Fixture("tank at 5 %");
            yield return Drawn();
            rd = VehicleGauges.LastHud;
            c.Metric("fuel_needle_5", rd.fuelNeedle, "");
            c.Check(Mathf.Abs(rd.fuelNeedle - 0.05f) < 0.02f && rd.low, $"needle at 5 % ({rd.fuelNeedle:0.00}) with the low-fuel warning");

            // ---- temperature: needle and band on the overheat thresholds
            var cases = new[] { (30f, TempZone.Cold), (90f, TempZone.Normal), (VehicleSystems.HotLimit + 5f, TempZone.Hot), (VehicleSystems.CriticalLimit + 5f, TempZone.Critical) };
            foreach (var (deg, zone) in cases)
            {
                sys.SetTemperature(deg);
                yield return Drawn();
                rd = VehicleGauges.LastHud;
                c.Metric("temp_needle_" + Mathf.RoundToInt(deg), rd.tempNeedle, "");
                c.Check(rd.tempShown && rd.zone == zone && Mathf.Abs(rd.temperature - deg) < 1.5f && Mathf.Abs(rd.tempNeedle - VehicleGauges.TempNeedle(deg)) < 0.02f,
                    $"{deg:0} °C reads {rd.temperature:0} °C in the {rd.zone} band (want {zone}), needle {rd.tempNeedle:0.00}");
                if (zone == TempZone.Hot) { c.Screenshot("gauges_hot"); yield return null; }
            }
            c.Check(VehicleGauges.TempNeedle(VehicleSystems.HotLimit) < VehicleGauges.TempNeedle(VehicleSystems.CriticalLimit) && VehicleGauges.TempNeedle(VehicleGauges.ScaleMax) >= 1f,
                "the hot and critical marks sit in order on the scale");

            // ---- units: °F with the imperial setting
            s.metric = false;
            c.Fixture("units set to imperial");
            c.Check(s.Temp(90f) == "194F" && s.Temp(VehicleSystems.HotLimit) == "230F", $"90 °C reads {s.Temp(90f)}, the overheat limit {s.Temp(VehicleSystems.HotLimit)}");
            yield return Drawn();
            s.metric = metric0;

            // ---- the in-cabin dashboard shows the same reading
            var dash = v.GetComponent<VehicleDashboard>();
            if (dash && dash.enabled)
            {
                sys.fuel = sys.fuelCapacity * 0.3f; sys.SetTemperature(95f);
                yield return new WaitForSeconds(0.3f);
                var d = dash.Last;
                c.Check(d.sys == sys && Mathf.Abs(d.fuelNeedle - 0.3f) < 0.02f && d.zone == TempZone.Normal, $"the dashboard strips read fuel {d.fuelNeedle:0.00} and {d.temperature:0} °C ({d.zone})");
            }
            else c.Note("no dashboard quad (headless / no driver eye): dashboard check skipped");

            // ---- air-cooled engines read the head, pedal bikes show neither
            bool airSeen = false, pedalSeen = false;
            foreach (var other in Object.FindObjectsByType<VehicleSystems>(FindObjectsSortMode.None))
            {
                var r = VehicleGauges.Read(other);
                if (VehicleGauges.Pedals(other)) { pedalSeen = true; c.Check(!r.fuelShown && !r.tempShown, other.name + ": pedal power shows no fuel or temperature"); }
                else if (!other.usesCoolant && other.HasEngine && !airSeen) { airSeen = true; c.Check(r.tempShown && r.airCooled, other.name + ": an air-cooled engine reads its head temperature"); }
            }
            if (!airSeen) c.Note("no air-cooled engine in the world");
            if (!pedalSeen) c.Note("no pedal bike in the world");

            sys.fuel = fuel0; sys.SetTemperature(25f);
            g.Exit();
        }
    }

    /// <summary>THE BUS CAN BE LEFT: standing up from the driver seat puts the player on open floor that reaches a door
    /// (the table no longer walls off the aisle from the sink); at the front door the EXIT prompt wins over DRIVE and F
    /// steps out; furniture dropped where the driver stands up never traps them (the exit spot moves to open floor).</summary>
    class BusExit : Scenario
    {
        public override string Id => "vehicle.bus_exit";
        public override float Timeout => 60f;
        const float R = 0.26f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no level pad"); yield break; }
            var v = VehicleFixScenarios.Find(c, "Bus", pad);
            var space = v ? v.GetComponent<InteriorSpace>() : null;
            if (!space) { c.Block("no bus"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            g.Player.Teleport(pad + Vector3.right * 9f + Vector3.up * 0.3f, -90f);
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1.5f);
            yield return null;
            int pieces = v.GetComponentsInChildren<Placeable>().Length;
            c.Metric("furnishings", pieces, "pieces");

            // every door can be reached from where the driver stands up
            c.Check(space.Free(space.stand, R), "the stand-up spot is clear");
            for (int i = 0; i < space.doors.Length; i++)
            {
                c.Check(space.ReachableDoor(space.doors[i].inside, R) >= 0, $"door {i} has open floor in reach");
            }
            int reach = space.ReachableDoor(space.stand, R);
            c.Check(reach >= 0, $"from the seat the aisle reaches a door ({reach})");

            // seat → stand up → in the walk-in space on open floor
            g.Enter(v);
            yield return new WaitForSeconds(0.6f);
            if (!c.Check(g.Current == v, "in the driver's seat")) yield break;
            g.Exit();
            yield return null;
            var local = g.Player.transform.localPosition;
            c.Check(g.Player.Interior == space && space.Free(local, R - 0.01f), $"stood up inside on clear floor at {local}");
            c.Check(space.ReachableDoor(local, R) >= 0 && !space.Trapped(local, R), "a door can be walked to from there");
            c.Screenshot("bus_stood_up");
            yield return null;

            // at the front door: EXIT, not DRIVE; F steps out
            int front = 0; float fz = float.MinValue;
            for (int i = 0; i < space.doors.Length; i++) if (space.doors[i].inside.z > fz) { fz = space.doors[i].inside.z; front = i; }
            g.Player.EnterInterior(space, space.doors[front].inside);
            c.Fixture("player walked (teleported) to the front door inside");
            yield return VehicleFixScenarios.Until(() => g.Prompt != null && g.Prompt.Contains("EXIT"), 2f);
            c.Note("prompt: " + g.Prompt);
            c.Check(g.Prompt != null && g.Prompt.Contains("EXIT") && !g.Prompt.Contains("DRIVE"), "the door beside the seat offers EXIT, not DRIVE");
            ActionPress.Press(Controls.Act.Enter);
            yield return VehicleFixScenarios.Until(() => !g.Player.Interior, 2f);
            var outside = v.transform.InverseTransformPoint(g.Player.transform.position);
            c.Check(!g.Player.Interior && Mathf.Abs(outside.x) > 1.2f, $"F steps out of the bus ({outside.x:0.0} m to the side)");
            c.Screenshot("bus_outside");
            yield return null;

            // furniture where the driver stands up: the exit spot moves to open floor that still reaches a door
            var blocker = FurnitureLibrary.Spawn("table", v.transform, space.stand, Quaternion.identity, space.furnitureMaterial);
            c.Fixture("a table dropped on the stand-up spot");
            yield return null;
            g.Enter(v);
            yield return new WaitForSeconds(0.6f);
            g.Exit();
            yield return null;
            local = g.Player.transform.localPosition;
            bool inside = g.Player.Interior == space;
            c.Check(inside ? space.Free(local, R - 0.01f) && space.ReachableDoor(local, R) >= 0 : true,
                inside ? $"blocked stand-up spot: stood at {local} instead, clear and with a way out" : "blocked: stepped straight out of a door");
            c.Check(!inside || Vector3.Distance(local, space.stand) > 0.2f, "not inside the table");
            if (blocker) Object.Destroy(blocker.gameObject);
            if (g.Player.Interior) { g.Player.ExitInterior(pad + Vector3.right * 9f); }
        }
    }

    /// <summary>CRASH EJECTION: an open buggy and a dirt bike driven into a low wall throw the driver out along the
    /// travel before the impact (not the bounce): the body tumbles on as a ragdoll, CRASH injuries are added and the
    /// player gets up again. Closed cars only eject in very hard crashes; walk-in vehicles never.</summary>
    class CrashEjection : Scenario
    {
        public override string Id => "vehicle.crash_ejection";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            // the rule
            var sedan = TestWorld.Vehicle("Sedan") ?? TestWorld.Vehicle("Pickup");
            if (sedan)
            {
                c.Check(!WastelandGame.Ejects(sedan, 14f) && WastelandGame.Ejects(sedan, WastelandGame.EjectClosedDv + 1f), "a closed car keeps its driver at 14 m/s Δv, not in a very hard crash");
            }
            var head = WastelandGame.EjectVelocity(new Vector3(0f, 0f, 14f), new Vector3(0f, 0.5f, -1f));
            c.Check(head.z > 9f && head.y > 1.5f, $"a head-on stop launches forward and up ({head})");

            if (!TestWorld.Pad(6f, 40f, out var pad, out var dir)) { c.Block("no level pad with a 40 m lane"); yield break; }
            foreach (var name in new[] { "DuneBuggy", "DirtBike" })
            {
                var v = VehicleFixScenarios.Find(c, name, pad);
                if (!v) { c.Note(name + " missing: skipped"); continue; }
                c.Check(name != "DuneBuggy" || WastelandGame.OpenVehicle(v), name + " counts as an open vehicle");
                if (g.Current) { g.Exit(); yield return null; }
                g.Player.Teleport(pad - Vector3.Cross(Vector3.up, dir) * 6f + Vector3.up * 0.3f, 0f);
                yield return TestWorld.Place(c, v, pad, dir, 1.5f);
                // a low concrete barrier 12 m ahead, square across the lane
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "TestBarrier";
                var wp = pad + dir * 12f;
                wp.y = DeformableTerrain.Instance.Height(wp.x, wp.z) + 0.45f;
                wall.transform.SetPositionAndRotation(wp, Quaternion.LookRotation(dir));
                wall.transform.localScale = new Vector3(6f, 0.9f, 0.6f);
                c.Fixture("a 0.9 m barrier 12 m ahead of the " + name);
                g.Stats.health = g.Stats.MaxHealth;
                int injuries0 = g.Stats.injuries.Count;
                g.Enter(v);
                yield return new WaitForSeconds(0.8f);
                if (!c.Check(g.Current == v, "riding the " + name)) { Object.Destroy(wall); continue; }
                float t0 = Time.time;
                v.handbrake = false; v.brakeInput = 0f;
                v.Body.linearVelocity = dir * 12f + Vector3.up * v.Body.linearVelocity.y;
                c.Fixture(name + " launched at 12 m/s at the barrier");
                yield return VehicleFixScenarios.Until(() => g.LastEjection.time >= t0 || Time.time - t0 > 3f, 3.2f);
                var ej = g.LastEjection;
                if (!c.Check(ej.time >= t0 && ej.vehicle == v && g.Current != v, $"the crash throws the rider off the {name} ({ej.why})"))
                { c.Note(TestWorld.State(v)); Object.Destroy(wall); if (g.Current) g.Exit(); continue; }
                var rd = g.Player.GetComponent<Ragdoll>();
                c.Check(rd && rd.Active, "the body goes limp (ragdoll)");
                var launchDir = new Vector3(ej.launch.x, 0f, ej.launch.z).normalized;
                float along = Vector3.Dot(launchDir, dir);
                c.Metric(name + "_launch_along_travel", along, "cos");
                c.Metric(name + "_launch_speed", ej.launch.magnitude, "m/s");
                c.Check(along > 0.8f && ej.launch.y > 1f, $"launched along the travel (cos {along:0.00}) and up ({ej.launch.y:0.0} m/s), not with the bounce");
                var pelvis0 = rd ? rd.Pelvis : g.Player.transform.position;
                yield return new WaitForSeconds(0.25f);
                c.Screenshot("ejection_" + name);
                yield return null;
                var moved = (rd ? rd.Pelvis : g.Player.transform.position) - pelvis0;
                float fwd = Vector3.Dot(new Vector3(moved.x, 0f, moved.z), dir);
                c.Metric(name + "_body_forward_0.25s", fwd, "m");
                c.Check(fwd > 0.8f, $"the body flies on forward ({fwd:0.0} m in a quarter second)");
                c.Check(g.Stats.injuries.Count > injuries0, $"CRASH injuries ({g.Stats.injuries.Count - injuries0} new)");
                yield return VehicleFixScenarios.Until(() => !(rd && rd.Active) || g.Vitals.Dead, 5f);
                c.Check(g.Vitals.Dead || !(rd && rd.Active), "gets back up");
                c.Note("dead: " + g.Vitals.Dead);
                Object.Destroy(wall);
                if (g.Vitals.Dead) yield return new WaitForSeconds(4.5f);                            // respawn
                g.Stats.health = g.Stats.MaxHealth;
                c.Fixture("health restored");
            }
        }
    }

    /// <summary>DRY BOAT HULLS: every boat (raft, skiff, trawler, houseboat, Iron Eel) uploads a hull outline the water
    /// shader clips: the water plane inside the hull is covered at rest and under way, the waterline just outside the
    /// hull is not. Screenshots at rest and moving.</summary>
    class BoatDryHull : Scenario
    {
        public override string Id => "vehicle.boat_dry_hull";
        public override string[] Suites => new[] { "full", "mobility" };
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (!MobilityKit.FindWater(3f, 30f, 4000f, out var at, out var outward, out var shore)) { c.Block("no open water within 4 km of the start"); yield break; }
            yield return MobilityKit.Stream(c, shore);
            float level = t.WaterLevel(at.x, at.z);
            if (float.IsNaN(level)) { c.Block("the water there is not loaded"); yield break; }
            foreach (var name in new[] { "Raft", "Skiff", "Trawler", "Houseboat", "IronEel" })
            {
                var v = g.SpawnFound(name, new Vector3(at.x, level + 0.4f, at.z), Quaternion.LookRotation(outward));
                var boat = v ? v.GetComponent<BoatModel>() : null;
                if (!c.Check(boat, name + " spawns as a boat")) continue;
                c.Fixture($"a {name} on the water {MobilityKit.Flat(at, shore):0} m off the shore");
                g.Enter(v);
                var mask = v.GetComponent<HullMask>();
                yield return VehicleFixScenarios.Until(() => mask && mask.Slot >= 0, 2f);
                if (!c.Check(mask && mask.Slot >= 0 && HullMask.Uploaded >= 1 && Shader.GetGlobalFloat("_MadMaxHullCount") >= 1f, name + ": the hull outline is uploaded to the water shader"))
                { g.Exit(); Object.Destroy(v.gameObject); continue; }
                float len = mask.zMax - mask.zMin, mid = mask.WidthAt((mask.zMin + mask.zMax) * 0.5f);
                c.Metric(name + "_outline_length", len, "m"); c.Metric(name + "_outline_halfwidth", mid, "m");
                c.Check(len > boat.hull.z * 0.6f && mid > boat.hull.x * 0.3f && mid < boat.hull.x * 0.9f + 0.3f,
                    $"{name}: outline {len:0.0} m long, {mid * 2f:0.0} m wide (hull {boat.hull.z:0.0} × {boat.hull.x:0.0} m, from mesh {mask.FromMesh})");
                yield return new WaitForSeconds(2.5f);
                Probe(c, g, v, mask, name + " at rest");
                c.Screenshot("boat_" + name + "_rest");
                yield return null;

                // under way: shove it along (any propulsion), the bow pitches and the hull squats
                float t0 = Time.time;
                c.Fixture(name + " pushed at 5 m/s for 3 s");
                while (Time.time - t0 < 3f)
                {
                    var vel = v.Body.linearVelocity;
                    var f = v.transform.forward; f.y = 0f; f.Normalize();
                    v.Body.linearVelocity = new Vector3(f.x * 5f, vel.y, f.z * 5f);
                    yield return new WaitForFixedUpdate();
                }
                Probe(c, g, v, mask, name + " moving");
                c.Screenshot("boat_" + name + "_moving");
                yield return null;
                g.Exit();
                yield return null;
                if (g.Player && g.Player.transform.IsChildOf(v.transform)) g.Player.Teleport(shore + Vector3.up * 0.3f, 0f);   // stepped into a walk-in cabin: off the boat before it goes
                Object.Destroy(v.gameObject);
                yield return null;
            }
        }

        /// <summary>The water plane under the hull's outline is covered; the waterline just outside is not.</summary>
        static void Probe(ScenarioContext c, WastelandGame g, VehicleDriver v, HullMask mask, string what)
        {
            var t = DeformableTerrain.Instance;
            int inside = 0, covered = 0, outside = 0, wet = 0, band = 0;
            for (int i = 1; i < 10; i++)
            for (int j = -2; j <= 2; j++)
            {
                float z = Mathf.Lerp(mask.zMin, mask.zMax, i / 10f), x = mask.WidthAt(z) * 0.85f * j / 2f;
                var w = v.transform.TransformPoint(new Vector3(x, 0f, z));
                float lvl = t.WaterLevel(w.x, w.z);
                if (float.IsNaN(lvl)) continue;
                w.y = lvl;
                var lp = v.transform.InverseTransformPoint(w);
                if (lp.y < mask.yMin || lp.y > mask.yMax) { band++; continue; }               // the surface misses the hull here (above / below)
                inside++; if (mask.Covers(w)) covered++;
            }
            foreach (float side in new[] { -1f, 1f })
            foreach (float zf in new[] { 0.3f, 0.5f, 0.7f })
            {
                float z = Mathf.Lerp(mask.zMin, mask.zMax, zf);
                var w = v.transform.TransformPoint(new Vector3(side * (mask.WidthAt(z) + 0.25f), 0f, z));
                float lvl = t.WaterLevel(w.x, w.z);
                if (float.IsNaN(lvl)) continue;
                w.y = lvl; outside++; if (mask.Covers(w)) wet++;
            }
            foreach (float zEnd in new[] { mask.zMin - 0.35f, mask.zMax + 0.35f })
            {
                var w = v.transform.TransformPoint(new Vector3(0f, 0f, zEnd));
                float lvl = t.WaterLevel(w.x, w.z);
                if (float.IsNaN(lvl)) continue;
                w.y = lvl; outside++; if (mask.Covers(w)) wet++;
            }
            c.Metric(what.Replace(' ', '_') + "_inside_covered", inside > 0 ? covered / (float)inside : 0f, "frac");
            if (band > 0) c.Note($"{what}: {band} probe(s) where the surface is outside the keel-gunwale band");
            c.Check(inside > 0 && covered == inside, $"{what}: no water inside the hull ({covered}/{inside} surface points covered)");
            c.Check(outside > 0 && wet == 0, $"{what}: the waterline outside the hull stays ({outside - wet}/{outside} points open)");
        }
    }
}
