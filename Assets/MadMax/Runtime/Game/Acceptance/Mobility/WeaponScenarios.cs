using System.Collections;
using System.Linq;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Vehicle weapons (roadmap 26 Q3): a fitted rear dropper scatters caltrops from the pack (a car following
    /// over them bursts a tyre) and, switched with Shift, pours an oil slick; the APC's roof machine gun trains on a
    /// car 22 m ahead, feeds belts from the pack and hits it; the smoke dischargers pop a screen that blocks sight lines.
    /// Driven through <see cref="VehicleWeapons.ControlArgs"/> (the game's LMB / B / Shift / U mapping).</summary>
    class WeaponsTest : Scenario
    {
        public override string Id => "mobility.weapons";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 170f;

        static IEnumerator Hold(VehicleWeapons w, bool fire, bool alt, bool drop, bool smoke, Vector3 aim, float seconds)
        {
            float t0 = Time.time; bool first = true;
            do
            {
                w.ControlArgs(fire, fire && first, alt && first, drop && first, smoke && first, aim, Time.deltaTime);
                first = false;
                yield return null;
            } while (Time.time - t0 < seconds);
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var apc = TestWorld.Vehicle("APC"); var target = TestWorld.Vehicle("Sedan"); var victim = TestWorld.Vehicle("Coupe");
            var dropCar = TestWorld.Vehicle("Interceptor") ?? TestWorld.Vehicle("Pickup");
            if (!apc || !target || !victim || !dropCar) { c.Block("APC, sedan, coupe or interceptor missing from the fleet"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a 60 m lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, apc, pad + fwd * 34f, fwd, 0.1f);
            yield return TestWorld.Place(c, target, pad + fwd * 56f, fwd, 0.1f);
            yield return TestWorld.Place(c, dropCar, pad + fwd * 6f, fwd, 0.1f);
            yield return TestWorld.Place(c, victim, pad - fwd * 6f, fwd, 1.2f);

            // ---- rear dropper: caltrops, then an oil slick
            var rear = dropCar.GetComponent<VehicleChassis>().Sockets.FirstOrDefault(k => k.accepts == PartCategory.RearBumper);
            if (!c.Check(rear, MobilityKit.N(dropCar) + " has a rear bumper socket")) yield break;
            var old = rear.Detach(false);
            if (old) Object.Destroy(old.gameObject);
            var dropper = g.SpawnPart("rear_dropper", rear.transform.position, rear.transform.rotation);
            if (!c.Check(dropper && rear.Attach(dropper), "a rear dropper bolts onto " + MobilityKit.N(dropCar) + "'s " + rear.name)) yield break;
            c.Fixture("rear dropper fitted in place of the rear bumper");
            MobilityKit.Grant(c, RearDropper.Caltrops, 2);
            MobilityKit.Grant(c, ResourceType.Oil, 4);
            g.Enter(dropCar); yield return new WaitForSeconds(0.3f);
            var guns = dropCar.GetComponent<VehicleWeapons>();
            if (!c.Check(guns, "every drivable vehicle carries its weapon controls")) yield break;
            int cal0 = g.Inventory.GetItem(RearDropper.Caltrops), hz0 = RoadHazards.Count;
            var patch = dropper.transform.TransformPoint(new Vector3(0f, 0f, -0.6f));
            yield return Hold(guns, false, false, true, false, dropCar.transform.position + fwd * 30f, 0.3f);
            c.Check(g.Inventory.GetItem(RearDropper.Caltrops) == cal0 - 1 && RoadHazards.Count == hz0 + 1, $"[B] drops one bag of caltrops ({cal0} -> {g.Inventory.GetItem(RearDropper.Caltrops)})");
            c.Check(RoadHazards.At(patch, out var k1) && k1 == RoadHazards.Kind.Caltrops, "caltrops lie behind the car");
            c.Note("dropper: " + guns.Status);
            yield return TestWorld.StartEngine(c, dropCar);
            dropCar.handbrake = false; dropCar.throttleInput = 0.6f;
            yield return new WaitForSeconds(2.2f);
            dropCar.throttleInput = 0f;
            yield return Hold(guns, false, true, true, false, dropCar.transform.position + fwd * 30f, 0.5f);          // Shift+B: switch to oil
            c.Check(dropper.GetComponent<RearDropper>().oil, "Shift+[B] switches the dropper to oil");
            int oil0 = g.Inventory.Get(ResourceType.Oil);
            var slick = dropper.transform.TransformPoint(new Vector3(0f, 0f, -0.6f));
            yield return Hold(guns, false, false, true, false, dropCar.transform.position + fwd * 30f, 0.3f);
            c.Check(g.Inventory.Get(ResourceType.Oil) == oil0 - 2 && RoadHazards.At(slick, out var k2) && k2 == RoadHazards.Kind.Oil, $"[B] pours an oil slick (2 L; oil {oil0} -> {g.Inventory.Get(ResourceType.Oil)} L)");
            dropCar.brakeInput = 1f;
            yield return MobilityKit.Until(() => Mathf.Abs(dropCar.ForwardSpeed) < 0.3f, 5f);
            dropCar.brakeInput = 0f; dropCar.handbrake = true;
            g.Exit(); yield return null;

            // a car following over the caltrops bursts a tyre
            g.Enter(victim); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, victim);
            victim.handbrake = false; victim.throttleInput = 1f;
            float t0 = Time.time;
            while (Vector3.Dot(victim.transform.position - patch, fwd) < 3.5f && Time.time - t0 < 7f) yield return null;
            victim.throttleInput = 0f; victim.brakeInput = 1f;
            c.Metric("speed_over_caltrops", victim.SpeedKmh, "km/h");
            yield return MobilityKit.Until(() => Mathf.Abs(victim.ForwardSpeed) < 0.3f, 5f);
            victim.brakeInput = 0f; victim.handbrake = true;
            int popped = victim.GetComponentsInChildren<WheelStats>().Count(w => w.Popped);
            c.Metric("tyres_burst", popped, "");
            if (!c.Check(popped > 0, $"driving over the caltrops bursts a tyre ({popped})")) c.Note(TestWorld.State(victim));
            g.Exit(); yield return null;

            // ---- roof machine gun
            var mg = apc.GetComponentInChildren<MountedGun>();
            if (!c.Check(mg && mg.Mounted, "the APC carries a roof machine gun")) yield break;
            MobilityKit.Grant(c, MountedGun.Ammo, 2);
            MobilityKit.Grant(c, VehicleWeapons.SmokeAmmo, 1);
            g.Enter(apc); yield return new WaitForSeconds(0.3f);
            var apcGuns = apc.GetComponent<VehicleWeapons>();
            int impacts = 0;
            var tdmg = target.GetComponent<VehicleDamage>();
            System.Action<float, Vector3> onHit = (p, at) => impacts++;
            tdmg.Impact += onHit;
            var aim = target.transform.Find("Body").GetComponent<Renderer>().bounds.center;
            yield return Hold(apcGuns, false, false, false, false, aim, 1.2f);                                        // train on the target
            c.Check(apcGuns.Armed, "the gun is armed: " + apcGuns.Status);
            int belts0 = g.Inventory.GetItem(MountedGun.Ammo), rounds0 = mg.rounds;
            yield return Hold(apcGuns, true, false, false, false, aim, 2f);
            yield return Hold(apcGuns, false, false, false, false, aim, 0.3f);
            tdmg.Impact -= onHit;
            int fired = (belts0 * MountedGun.PerBelt + rounds0) - (g.Inventory.GetItem(MountedGun.Ammo) * MountedGun.PerBelt + mg.rounds);
            c.Metric("rounds_fired_2s", fired, "");
            c.Metric("hits_on_target", impacts, "");
            c.Check(fired >= 8, $"holding fire feeds belts from the pack ({fired} rounds in 2 s)");
            c.Check(impacts >= 3, $"the rounds hit the car 22 m ahead ({impacts} of {fired})");
            c.Screenshot("mg");
            yield return null;

            // ---- smoke dischargers
            int sm0 = g.Inventory.GetItem(VehicleWeapons.SmokeAmmo);
            yield return Hold(apcGuns, false, false, false, true, aim, 0.5f);
            c.Check(g.Inventory.GetItem(VehicleWeapons.SmokeAmmo) == sm0 - 1, "[U] fires one smoke salvo from the pack");
            var mid = apc.transform.position + Vector3.up * 1.2f;
            c.Check(SmokeScreen.Blocks(mid + apc.transform.right * 25f, mid - apc.transform.right * 25f), "the screen blocks a sight line across the APC");
            yield return new WaitForSeconds(1f);
            c.Screenshot("smoke");
            yield return null;
            g.Exit();
        }
    }
}
