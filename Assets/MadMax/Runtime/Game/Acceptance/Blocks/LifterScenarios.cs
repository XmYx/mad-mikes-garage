using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Heavy lifters (smoke test): the car crusher presses a car laid in its bay into a bale and its grapple
    /// picks the bale up; the magnet crane switched on over a car holds it and lifts it.</summary>
    public static class LifterScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new LiftersSmoke();
        }
    }

    class LiftersSmoke : Scenario
    {
        public override string Id => "vehicle.lifters";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(14f, 30f, out var pad, out var fwd)) { c.Block("no level pad"); yield break; }
            if ((g.Player.transform.position - pad).sqrMagnitude > 30f * 30f)                   // a far pad: stand by it (bodies beyond 70 m are frozen)
                yield return PH.Go(c, pad - Vector3.Cross(Vector3.up, fwd) * 16f, PH.Yaw(Vector3.Cross(Vector3.up, fwd)), "the pad", 1.5f);

            // ---- crusher: a car laid in the bay (fixture), Shift+7 / E crushes it, the grapple lifts the bale
            var crusher = g.SpawnFound("CarCrusher", pad + Vector3.up * 1.2f, Quaternion.LookRotation(fwd));
            var car = g.SpawnFound("Trabant", pad + Vector3.right * 12f + Vector3.up, Quaternion.LookRotation(fwd));
            if (!crusher || !car) { c.Block("CarCrusher or Trabant prefab missing (rebuild Parts + Vehicles)"); yield break; }
            yield return TestWorld.Place(c, crusher, pad, fwd, 1.5f);
            var gc = crusher.GetComponent<GrappleCrusher>();
            if (!c.Check(gc && gc.Part && gc.HasBay, "the crusher has its grapple and bay")) yield break;
            car.Body.position = gc.BayFloor + Vector3.up * 0.4f;
            car.Body.rotation = crusher.transform.rotation;
            car.Body.linearVelocity = Vector3.zero;
            c.Fixture("Trabant laid straight into the crusher bay");
            yield return new WaitForSeconds(1.5f);
            float pool = car.GetComponent<VehicleDamage>().SalvageLeft;
            g.Enter(crusher); yield return new WaitForSeconds(0.3f);
            gc.Control(false, false, false, 0f, false, true);
            c.Check(gc.Crushing, $"the crush cycle starts: {gc.Status} (car at {crusher.transform.InverseTransformPoint(car.Body.worldCenterOfMass)} vs floor {crusher.transform.InverseTransformPoint(gc.BayFloor)}, kinematic {car.Body.isKinematic})");
            float t0 = Time.time;
            while (gc.Crushing && Time.time - t0 < 15f) { gc.Control(false, false, false, 0f); yield return null; }
            c.Screenshot("crushed");
            yield return null;
            c.Check(!car, "the car is gone");
            var bale = gc.LastBale;
            if (!c.Check(bale && bale.IsResource && bale.count >= Mathf.FloorToInt(pool), $"a bale of {(bale ? bale.count : 0)} scrap (pool {pool:0})")) yield break;
            if (c.Check(gc.Reach(bale.transform.position + Vector3.up * 0.2f), "the grapple reaches the bale"))
            {
                gc.Control(true, false, false, 0f);
                t0 = Time.time;
                while (!gc.Load && Time.time - t0 < 2f) { gc.Control(false, false, false, 0f); yield return null; }
                c.Check(gc.Load == bale.Body, "[7] grabs the bale");
                float y0 = bale.transform.position.y;
                t0 = Time.time;
                while (Time.time - t0 < 2f) { gc.Control(false, true, false, 0f); yield return null; }
                c.Metric("bale_lifted", bale.transform.position.y - y0, "m");
                gc.Control(true, false, false, 0f);
            }
            g.Exit();

            // ---- magnet: a car under the pole face, ON holds it, hoist lifts it
            var magnet = g.SpawnFound("MagnetCrane", pad + fwd * 20f + Vector3.up * 1.2f, Quaternion.LookRotation(fwd));
            var car2 = g.SpawnFound("Trabant", pad + fwd * 34f + Vector3.up, Quaternion.LookRotation(fwd));
            if (!magnet || !car2) { c.Block("MagnetCrane prefab missing"); yield break; }
            yield return TestWorld.Place(c, magnet, pad + fwd * 20f, fwd, 1.5f);
            var mc = magnet.GetComponent<MagnetCrane>();
            if (!c.Check(mc && mc.Part, "the magnet crane is mounted")) yield break;
            g.Enter(magnet); yield return new WaitForSeconds(0.3f);
            t0 = Time.time;
            while (Time.time - t0 < 3f) { mc.Control(false, true, false, 0f, true); yield return null; }        // boom up
            t0 = Time.time;
            while (Time.time - t0 < 4.6f) { mc.Control(false, false, false, 1f); yield return null; }           // swung out over the side
            t0 = Time.time;
            while (Time.time - t0 < 2f) { mc.Control(false, false, false, 0f); yield return null; }             // the disc settles
            var face = mc.PoleFace;
            yield return TestWorld.Place(c, car2, face - Vector3.up * face.y, magnet.transform.forward, 1f);
            t0 = Time.time;
            float roof = car2.Body.worldCenterOfMass.y + 0.8f;
            while (mc.PoleFace.y > roof + 0.6f && Time.time - t0 < 8f) { mc.Control(false, false, true, 0f); yield return null; }
            float rest = car2.Body.worldCenterOfMass.y;
            mc.Switch(true);
            yield return new WaitForSeconds(1f);
            c.Check(mc.Holds(car2.Body), "ON: the magnet holds the car (" + mc.Status + ")");
            t0 = Time.time;
            while (Time.time - t0 < 3f) { mc.Control(false, true, false, 0f); yield return null; }
            float lift = car2.Body.worldCenterOfMass.y - rest;
            c.Metric("magnet_lift", lift, "m");
            c.Check(lift > 1f, $"the car rises with the magnet ({lift:0.0} m)");
            c.Screenshot("magnet");
            yield return null;
            mc.Switch(false);
            yield return new WaitForSeconds(2.5f);
            c.Check(car2.Body.worldCenterOfMass.y - rest < 0.6f, "OFF: it drops back down");
            g.Exit();
        }
    }
}
