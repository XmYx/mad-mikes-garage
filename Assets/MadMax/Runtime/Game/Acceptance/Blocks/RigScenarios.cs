using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Moving bases (<see cref="RigVehicle"/>): one smoke test from frame to driving.</summary>
    public static class RigScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new RigCommission(); }
    }

    /// <summary>RIG: an M frame on stands, two axle mounts with truck wheels, an engine bay with the truck diesel, a seat
    /// and a crate on the deck; it commissions itself, weighs the pieces, drives 5 s forward and keeps its layout in the save.</summary>
    class RigCommission : Scenario
    {
        public override string Id => "rig.commission";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(9f, 40f, out var pad, out var fwd)) { c.Block("no level pad with a 40 m lane"); yield break; }
            if ((g.Player.transform.position - pad).sqrMagnitude > 30f * 30f)                   // a far pad: stand by it (bodies beyond 70 m are frozen)
                yield return PH.Go(c, pad - Vector3.Cross(Vector3.up, fwd) * 16f, PH.Yaw(Vector3.Cross(Vector3.up, fwd)), "the pad", 1.5f);
            var frame = g.Build.PlaceRigPiece("rig_frame_M", pad, null, Quaternion.LookRotation(fwd).eulerAngles.y);
            var rig = frame ? frame.GetComponentInParent<RigVehicle>() : null;
            if (!c.Check(rig, "an M frame starts a moving base")) yield break;
            c.Fixture("rig_frame_M on stands at " + pad.x.ToString("0") + "," + pad.z.ToString("0"));
            yield return null;
            c.Check(!rig.Commissioned && rig.Driver.Body.isKinematic, "on its stands until the running gear is in");
            var t = rig.transform;
            g.Build.PlaceRigPiece("rig_axle_M", t.TransformPoint(0f, 0f, 2.2f), rig);
            g.Build.PlaceRigPiece("rig_axle_M", t.TransformPoint(0f, 0f, -2.2f), rig);
            g.Build.PlaceRigPiece("rig_engine_bay", t.TransformPoint(0f, 0.4f, 0.4f), rig);
            g.Build.PlaceRigPiece("rig_driver_seat", t.TransformPoint(0f, 0.4f, -1.4f), rig);
            g.Build.PlaceRigPiece("rig_fuel_tank", t.TransformPoint(0.9f, 0.4f, -2.4f), rig);
            yield return null;
            int wheels = 0; bool engine = false;
            foreach (var s in rig.GetComponent<VehicleChassis>().Sockets)
            {
                var part = g.SpawnPart(s.accepts == PartCategory.Wheel ? "wheel_truck" : "engine_truck_diesel", s.transform.position, s.transform.rotation);
                if (part && s.Attach(part)) { if (s.accepts == PartCategory.Wheel) wheels++; else engine = true; }
            }
            c.Check(wheels == 4 && engine, $"four truck wheels and the diesel mounted ({wheels} wheels, engine {engine})");
            yield return null; yield return null;
            c.Check(rig.Commissioned && !rig.Driver.Body.isKinematic, "commissioned onto its wheels" + (rig.Missing != null ? ": " + rig.Missing : ""));
            float mass = rig.Driver.Body.mass;
            c.Metric("rig_mass", mass, "kg");
            c.Check(rig.PieceMass > 950f + 2f * 320f + 110f + 45f && mass > rig.PieceMass + 4f * 90f + 900f - 1f, $"mass includes the pieces ({rig.PieceMass:0} kg of pieces, {mass:0} kg in all)");
            var sys = rig.GetComponent<VehicleSystems>();
            c.Check(sys && sys.fuelCapacity >= 260f, "the fuel tank piece adds 200 L");
            if (sys) { sys.fuel = 120f; sys.tankKind = ResourceType.Diesel; }
            yield return new WaitForSeconds(1.5f);
            var v = rig.Driver;
            g.Enter(v);
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(g.Current == v, "in the seat")) yield break;
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var from = v.transform.position;
            v.handbrake = false; v.throttleInput = 1f;
            yield return new WaitForSeconds(5f);
            float moved = Vector3.Distance(from, v.transform.position);
            c.Metric("moved_5s", moved, "m");
            c.Check(moved > 5f, $"drives: {moved:0.0} m in 5 s ({TestWorld.State(v)})");
            c.Screenshot("rig_driving");
            yield return null;
            v.throttleInput = 0f; v.brakeInput = 1f;
            yield return new WaitForSeconds(2f);
            v.brakeInput = 0f; v.handbrake = true;
            var layout = RigVehicle.SaveLayout(v);
            c.Check(layout != null && layout.StartsWith("1") && layout.Contains("rig_axle_M") && layout.Contains("rig_driver_seat"), "the save keeps the layout (" + (layout != null ? layout.Length : 0) + " chars)");
            var d = g.CaptureSave();
            var vs = d.vehicles.Find(x => x.netId == v.netId);
            c.Check(vs != null && vs.design == RigVehicle.Design && vs.sockets.Exists(x => x.part == "engine_truck_diesel"), "saved as a rig with its engine");
            c.Check(!d.placed.Exists(p => p.id.StartsWith("rig_")), "rig pieces are not duplicated as placed pieces");
            g.Exit();
        }
    }
}
