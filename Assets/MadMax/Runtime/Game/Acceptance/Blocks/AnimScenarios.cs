using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Anim block.</summary>
    public static class AnimScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new VehicleWork();
        }
    }

    /// <summary>WORK ANIMATION: a service walks the player round to the engine bay (hood lifted), takes over a second and
    /// changes the fluids only at the end; the wrench unbolts a bumper (or lamp) standing beside it and bolts it back on;
    /// G pressed for a second service and walking off cancels it without effect; with the setting off it is instant.</summary>
    class VehicleWork : Scenario
    {
        public override string Id => "anim.vehicle_work";
        public override float Timeout => 100f;

        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>The job and the hands in one line (failure evidence).</summary>
        static string State(WastelandGame g, MountSocket sock = null)
        {
            var carried = g.Player.Carried;
            return $"working {g.Working} {(g.WorkNow.HasValue ? g.WorkNow.Value.ToString() : "-")} approaching {g.WorkApproaching} progress {g.WorkProgress:0.00}; "
                 + $"last {g.LastWork} '{g.LastWorkNote}' ({g.LastWorkProgress:0.00}); carried {(carried ? carried.partId : "none")}"
                 + (sock ? "; socket " + (sock.Current ? sock.Current.partId : "free") : "") + $"; setup: {g.WorkDebug}";
        }

        static void PutPlayer(WastelandGame g, VehicleDriver v, Vector3 local)
        {
            var p = v.transform.TransformPoint(local);
            p.y = DeformableTerrain.Instance.Height(p.x, p.z) + 0.05f;
            var look = v.transform.position - p; look.y = 0f;
            g.Player.Teleport(p, Quaternion.LookRotation(look).eulerAngles.y);
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle("Sedan");
            if (!v) v = TestWorld.Vehicle("Pickup");
            var sys = v ? v.GetComponent<VehicleSystems>() : null;
            var ch = v ? v.GetComponent<VehicleChassis>() : null;
            if (!sys || !ch) { c.Block("no sedan or pickup in the fleet"); yield break; }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no level pad"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            g.Player.moveInput = Vector2.zero;
            g.Player.Teleport(pad + Vector3.right * 7f + Vector3.up * 0.3f, -90f);                  // clear of where the car lands
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1.5f);
            var s = GameSettings.Current; bool keep = s.workAnimation;
            s.workAnimation = true;
            var name = v.name.Replace("(Clone)", "");

            // ---- a service: walked round to the engine bay, fluids only at the end
            if (sys.disconnected) { sys.Reconnect(); c.Fixture("battery lead reconnected"); }      // G would tighten it first
            sys.fuel = sys.fuelCapacity;                                                          // a full tank: G services instead of pouring
            sys.oil = sys.oilCapacity * 0.2f; sys.coolant = sys.coolantCapacity * 0.25f;
            g.Inventory.Add(ResourceType.Oil, 12); g.Inventory.Add(ResourceType.Coolant, 12);
            c.Fixture(name + ": full tank, oil 20 %, coolant 25 %; +12 oil and +12 coolant in the pack");
            float oil0 = sys.oil, cool0 = sys.coolant;
            PutPlayer(g, v, new Vector3(2.6f, 0f, -0.8f));
            c.Fixture("player 2.6 m beside the " + name);
            yield return null;
            g.ServiceVehicle(v);
            c.Note("service: " + State(g));
            if (!c.Check(g.Working && g.WorkNow == WorkKind.Service, "the service is timed work, not instant")) { s.workAnimation = keep; yield break; }
            c.Check(Mathf.Abs(sys.oil - oil0) < 0.01f, "nothing changes when it starts");
            float t0 = Time.time;
            while (g.Working && g.WorkApproaching && Time.time - t0 < 8f) yield return null;
            c.Metric("walk_to_engine_bay", Time.time - t0, "s");
            if (!c.Check(g.Working && !g.WorkApproaching, "walks up to the engine bay and starts working")) { g.CancelWork(null); s.workAnimation = keep; yield break; }
            Transform engine = null, hood = null; float dBay = float.MaxValue;
            foreach (var so in ch.Sockets)
            {
                if (so.accepts == PartCategory.Engine) engine = so.transform;
                if (so.accepts == PartCategory.Engine || so.accepts == PartCategory.Radiator) dBay = Mathf.Min(dBay, Flat(g.Player.transform.position, so.transform.position));
                if (so.accepts == PartCategory.Hood && so.Current) hood = so.Current.transform;
            }
            if (engine)
            {
                float zPlayer = v.transform.InverseTransformPoint(g.Player.transform.position).z, zEngine = v.transform.InverseTransformPoint(engine.position).z;
                c.Metric("player_to_engine_bay", dBay, "m");
                c.Check(dBay < 2f && zPlayer > zEngine, $"stands at the front by the engine / radiator ({dBay:0.00} m)");
            }
            float dur = g.WorkDuration, tw = Time.time;
            c.Metric("service_duration", dur, "s");
            c.Check(dur > 1f, $"the service takes over a second ({dur:0.0} s)");
            while (g.Working && g.WorkProgress < 0.45f && Time.time - tw < 10f) yield return null;
            c.Check(Mathf.Abs(sys.oil - oil0) < 0.05f && Mathf.Abs(sys.coolant - cool0) < 0.05f, "oil and coolant unchanged mid-way");
            c.Check(g.Player.PoseOverride.HasValue, "a work pose plays");
            if (hood)
            {
                float open = Quaternion.Angle(Quaternion.identity, hood.localRotation);
                c.Metric("hood_open", open, "deg");
                c.Check(open > 20f, "the hood is lifted over the engine bay");
            }
            c.Screenshot("service");
            yield return null;
            while (g.Working && Time.time - tw < 15f) yield return null;
            c.Metric("service_elapsed", Time.time - tw, "s");
            c.Check(g.LastWork == WorkOutcome.Done && g.LastWorkProgress >= 1f, "the work runs to the end (progress 1)");
            c.Check(sys.oil > oil0 + 1f && sys.coolant > cool0 + 1f, $"topped up at the end: oil {oil0:0.0} > {sys.oil:0.0} L, coolant {cool0:0.0} > {sys.coolant:0.0} L");
            c.Check(!g.Player.PoseOverride.HasValue, "the pose ends with the work");
            yield return new WaitForSeconds(0.8f);
            if (hood) c.Check(Quaternion.Angle(Quaternion.identity, hood.localRotation) < 2f, "the hood is shut again");

            // ---- the wrench: unbolt a bumper (or a lamp) beside it, bolt it back on
            MountSocket sock = null;
            foreach (var so in ch.Sockets)
                if (so.Current && (so.accepts == PartCategory.FrontBumper || so.accepts == PartCategory.RearBumper || so.accepts == PartCategory.Lights) && (!sock || so.accepts == PartCategory.FrontBumper)) sock = so;
            if (!sock) c.Note("no bumper or lamp fitted: part checks skipped");
            else
            {
                var part = sock.Current;
                if (g.Inventory.GetItem(ItemIds.Wrench) <= 0) { g.Inventory.AddItem(ItemIds.Wrench); c.Fixture("granted a wrench"); }
                if (!g.Player.Tool || g.Player.Tool.id != ItemIds.Wrench) { g.Player.Equip(ToolLibrary.Create(ItemIds.Wrench, g.propMaterial)); c.Fixture("wrench in hand"); }
                PutPlayer(g, v, new Vector3(-2.6f, 0f, -1f));
                c.Fixture("player 2.6 m on the far side");
                yield return null;
                g.TakePart(part);
                c.Note("take: " + State(g, sock));
                if (c.Check(g.Working && g.WorkNow == WorkKind.Take, "taking " + part.partId + " is timed wrench work"))
                {
                    t0 = Time.time;
                    while (g.Working && g.WorkApproaching && Time.time - t0 < 10f) yield return null;
                    var centre = part.TryGetComponent<Renderer>(out var r) ? r.bounds.center : part.transform.position;
                    float d = Flat(g.Player.transform.position, centre);
                    c.Metric("player_to_part", d, "m");
                    c.Check(g.Working && d < 1.6f, $"walks round beside the {part.partId} ({d:0.00} m)");
                    while (g.Working && g.WorkProgress < 0.5f && Time.time - t0 < 20f) yield return null;
                    c.Check(part.Socket == sock, "still bolted on mid-way");
                    c.Screenshot("unbolt");
                    yield return null;
                    while (g.Working && Time.time - t0 < 25f) yield return null;
                    c.Note("after the take: " + State(g, sock));
                    c.Check(g.LastWork == WorkOutcome.Done && !part.Socket && g.Player.Carried == part, "unbolted at the end and carried");
                }
                if (g.Player.Carried == part)
                {
                    var carPos = v.transform.position;
                    g.MountCarried(sock);
                    c.Note("mount: " + State(g, sock));
                    c.Check(g.Working && g.WorkNow == WorkKind.Mount, "bolting it back on is timed too");
                    t0 = Time.time;
                    bool wasWorking = g.Working, wasApproaching = g.WorkApproaching;
                    while (g.Working && g.WorkProgress < 0.5f && Time.time - t0 < 15f)
                    {
                        yield return null;
                        if (g.Working == wasWorking && g.WorkApproaching == wasApproaching) continue;
                        wasWorking = g.Working; wasApproaching = g.WorkApproaching;
                        c.Note($"mount +{Time.time - t0:0.00} s: " + State(g, sock));
                    }
                    c.Note($"mount mid-way: {State(g, sock)}; the car moved {Vector3.Distance(carPos, v.transform.position):0.00} m, player {Flat(g.Player.transform.position, sock.transform.position):0.00} m from the socket");
                    c.Check(sock.IsFree && g.Player.Carried != part, "held up to the socket, not yet mounted mid-way");
                    while (g.Working && Time.time - t0 < 25f) yield return null;
                    c.Note("mount end: " + State(g, sock));
                    c.Check(sock.Current == part, "back on its socket at the end");
                }
                else c.Note("not carrying the part after the take: " + State(g, sock));
            }

            // ---- G for a second service, then walking off: cancelled without effect
            sys.oil = sys.oilCapacity * 0.2f;
            float oil1 = sys.oil;
            c.Fixture("oil drained to 20 % again");
            if (g.Working) { c.Fixture("stopped a job still running before G: " + State(g)); g.CancelWork(null); }
            if (g.Player.Carried) { c.Fixture("dropped the still-carried " + g.Player.Carried.partId); g.Player.DropCarried(); }
            PutPlayer(g, v, new Vector3(2.4f, 0f, 0f));
            yield return null;
            c.Note($"before G: nearby {(g.NearbyVehicle ? g.NearbyVehicle.name : "none")}, needs service {sys.NeedsService(g.Inventory)}, prompt '{g.Prompt}'");
            var press = ActionPress.Press(Controls.Act.Service);                                   // G as the keyboard gives it, before the game's Update
            for (int i = 0; i < 5 && press.Frame < 0; i++) yield return null;
            c.Note($"G pressed in frame {press.Frame}: prompt '{g.Prompt}'; " + State(g));
            c.Check(g.Working && g.WorkNow == WorkKind.Service, "pressing G at the car starts the timed service");
            if (!g.Working) g.ServiceVehicle(v);
            t0 = Time.time;
            while (g.Working && g.WorkProgress < 0.25f && Time.time - t0 < 10f) yield return null;
            c.Check(g.Working && !g.WorkApproaching, "the second service is under way");
            g.Player.moveInput = new Vector2(0f, -1f);
            for (int i = 0; i < 30 && g.Working; i++) yield return null;
            g.Player.moveInput = Vector2.zero;
            c.Note("walked off: " + State(g));
            c.Check(!g.Working && g.LastWork == WorkOutcome.Cancelled, "walking off cancels it");
            c.Metric("cancelled_at", g.LastWorkProgress, "");
            yield return new WaitForSeconds(0.3f);
            c.Check(Mathf.Abs(sys.oil - oil1) < 0.05f, "a cancelled service changes nothing");
            c.Check(!g.Player.PoseOverride.HasValue && !g.Player.AutoWalk.HasValue, "the player is free again");

            // ---- WORK ANIMATION off: instant as before
            s.workAnimation = false;
            g.ServiceVehicle(v);
            c.Check(!g.Working && sys.oil > oil1 + 1f, "setting off: the service is instant");
            s.workAnimation = keep;
            yield return null;
        }
    }
}
