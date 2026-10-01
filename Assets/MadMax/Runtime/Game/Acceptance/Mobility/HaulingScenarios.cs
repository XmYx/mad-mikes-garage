using System.Collections;
using System.Linq;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    static class Hauling
    {
        /// <summary>Park <paramref name="trailer"/> behind <paramref name="tower"/> with its coupler <paramref name="gap"/> m
        /// behind the hitch (both on the same heading).</summary>
        public static IEnumerator Behind(ScenarioContext c, VehicleDriver tower, VehicleDriver trailer, Vector3 fwd, float gap)
        {
            var hitch = TowCoupling.HitchOf(tower);
            var tc = trailer.GetComponent<TowCoupling>();
            var coupler = tc ? tc.Coupler : null;
            if (!hitch || !coupler) yield break;
            var rot = Quaternion.LookRotation(fwd);
            var couplerLocal = trailer.transform.InverseTransformPoint(coupler.position);
            var at = hitch.position - fwd * gap - rot * new Vector3(couplerLocal.x, 0f, couplerLocal.z);
            yield return TestWorld.Place(c, trailer, at, fwd, 1.2f);
        }
    }

    /// <summary>Towing and the fuel tanker (roadmap 26 Q3 "towing, tanker transfer with conservation"): backed up to the
    /// small bowser, [J] in the cab hitches it (legs wind up), it follows on the road with the coupler on the hitch and
    /// brakes with the tow vehicle; on foot beside it [G] pumps the tanker into the tow vehicle and [K] drains it back:
    /// the litres leaving one tank arrive in the other. [J] again unhitches (legs down) and the tow vehicle drives off alone.</summary>
    class TowingTanker : Scenario
    {
        public override string Id => "mobility.towing";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var tower = TestWorld.Vehicle("Pickup") ?? TestWorld.Vehicle("Interceptor");
            var trailer = TestWorld.Vehicle("TankerSmall");
            if (!tower || !trailer) { c.Block("pickup or small bowser missing from the fleet"); yield break; }
            var tc = trailer.GetComponent<TowCoupling>(); var tanker = trailer.GetComponent<FuelTanker>();
            if (!c.Check(tc && tanker && TowCoupling.HitchOf(tower), "the bowser has a coupler and pump, the " + MobilityKit.N(tower) + " a hitch")) yield break;
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, tower, pad + fwd * 6f, fwd, 0.6f);
            yield return Hauling.Behind(c, tower, trailer, fwd, 0.9f);
            var hitch = TowCoupling.HitchOf(tower);
            c.Note($"coupler to hitch {Vector3.Distance(tc.Coupler.position, hitch.position):0.00} m");

            // ---- hitch from the cab
            g.Enter(tower); yield return new WaitForSeconds(0.3f);
            c.Check((g.Prompt ?? "").Contains("HITCH"), "the cab offers [J] HITCH: " + g.Prompt);
            ActionPress.Press(Controls.Act.Hitch);
            yield return MobilityKit.Until(() => tc.Tower == tower && !tc.Busy, 6f);
            if (!c.Check(tc.Tower == tower && !tc.Busy, "[J] hitches the bowser")) { c.Note(g.Prompt); yield break; }
            c.Check(tc.LegsUp > 0.9f, $"the landing legs wind up ({tc.LegsUp * 100f:0} %)");

            // ---- tow it
            yield return TestWorld.StartEngine(c, tower);
            var t0p = trailer.transform.position;
            float worstGap = 0f;
            tower.handbrake = false; tower.throttleInput = 0.6f;
            float t0 = Time.time;
            while (Time.time - t0 < 5f) { worstGap = Mathf.Max(worstGap, Vector3.Distance(tc.Coupler.position, hitch.position)); yield return null; }
            tower.throttleInput = 0f; tower.brakeInput = 1f;
            float pulled = Vector3.Dot(trailer.transform.position - t0p, fwd);
            yield return MobilityKit.Until(() => Mathf.Abs(tower.ForwardSpeed) < 0.3f, 6f);
            c.Metric("towed_5s", pulled, "m");
            c.Metric("coupler_gap_max", worstGap, "m");
            if (!c.Check(pulled > 6f, $"the bowser follows ({pulled:0.0} m in 5 s)")) c.Note(TestWorld.State(tower) + "; touching: " + TestWorld.Contacts(tower));
            c.Check(worstGap < 0.3f, $"the coupler stays on the hitch (worst {worstGap:0.00} m)");
            c.Check(trailer.Body.linearVelocity.magnitude < 0.5f, $"the trailer brakes with the tow vehicle ({trailer.Body.linearVelocity.magnitude:0.0} m/s)");
            tower.brakeInput = 0f; tower.handbrake = true;
            g.Exit(); yield return null;

            // ---- the pump, on foot beside the bowser
            var ts = trailer.GetComponent<VehicleSystems>(); var ps = tower.GetComponent<VehicleSystems>();
            var kind = ps.FuelKind;
            if (ts.fuel < 100f) { ts.fuel = 200f; c.Fixture("bowser holds 200 L"); }
            if (ts.tankKind != kind) { ts.tankKind = kind; c.Fixture("bowser holds " + ResourceInfo.Name(kind) + " like the " + MobilityKit.N(tower) + "'s engine burns"); }
            ps.fuel = ps.fuelCapacity * 0.2f; ps.tankKind = kind;
            c.Fixture($"{MobilityKit.N(tower)} tank at 20 % ({ps.fuel:0} L)");
            var stand = trailer.transform.TransformPoint(new Vector3(2.2f, 0f, -0.8f));
            stand.y = DeformableTerrain.Instance.Height(stand.x, stand.z) + 0.05f;
            g.Player.Teleport(stand, trailer.transform.eulerAngles.y - 90f);
            yield return null; yield return null;
            c.Check((g.Prompt ?? "").Contains("PUMP INTO"), "beside the bowser [G] offers to pump into the tow vehicle: " + g.Prompt);
            float sum0 = ts.fuel + ps.fuel, p0 = ps.fuel;
            ActionPress.Press(Controls.Act.Service);
            yield return MobilityKit.Until(() => tanker.Pumping == FuelTanker.Mode.Fill, 1f);
            if (c.Check(tanker.Pumping == FuelTanker.Mode.Fill && tanker.Partner == tower, "[G] starts the pump into the " + MobilityKit.N(tower)))
            {
                yield return new WaitForSeconds(2f);
                ActionPress.Press(Controls.Act.Service);
                yield return MobilityKit.Until(() => tanker.Pumping == FuelTanker.Mode.Off, 1f);
                float moved = ps.fuel - p0, sum1 = ts.fuel + ps.fuel;
                c.Metric("pumped_in", moved, "L");
                c.Check(tanker.Pumping == FuelTanker.Mode.Off, "[G] again stops the pump");
                c.Check(moved > 5f, $"fuel flows into the tank ({moved:0.0} L)");
                c.Check(Mathf.Abs(sum1 - sum0) < 0.05f, $"no fuel lost or made ({sum0:0.00} -> {sum1:0.00} L)");
            }
            float t1 = ts.fuel; sum0 = ts.fuel + ps.fuel;
            ActionPress.Press(Controls.Act.Siphon);
            yield return MobilityKit.Until(() => tanker.Pumping == FuelTanker.Mode.Drain, 1f);
            if (c.Check(tanker.Pumping == FuelTanker.Mode.Drain, "[K] drains the " + MobilityKit.N(tower) + " into the bowser"))
            {
                yield return new WaitForSeconds(1f);
                ActionPress.Press(Controls.Act.Siphon);
                yield return MobilityKit.Until(() => tanker.Pumping == FuelTanker.Mode.Off, 1f);
                c.Metric("drained_back", ts.fuel - t1, "L");
                c.Check(ts.fuel > t1 + 2f, $"fuel flows back ({ts.fuel - t1:0.0} L)");
                c.Check(Mathf.Abs(ts.fuel + ps.fuel - sum0) < 0.05f, "no fuel lost or made draining");
            }
            if (tanker.Pumping != FuelTanker.Mode.Off) tanker.Toggle(FuelTanker.Mode.Off);

            // ---- unhitch and drive away alone
            g.Enter(tower); yield return new WaitForSeconds(0.3f);
            c.Check((g.Prompt ?? "").Contains("UNHITCH"), "the cab offers [J] UNHITCH: " + g.Prompt);
            ActionPress.Press(Controls.Act.Hitch);
            yield return MobilityKit.Until(() => !tc.Tower && !tc.Busy, 4f);
            c.Check(!tc.Tower, "[J] unhitches");
            c.Check(tc.LegsUp < 0.05f, "the legs are down again");
            yield return TestWorld.StartEngine(c, tower);
            var parked = trailer.transform.position;
            tower.handbrake = false; tower.throttleInput = 0.6f;
            yield return new WaitForSeconds(3f);
            tower.throttleInput = 0f; tower.brakeInput = 1f;
            float drift = Vector3.Distance(parked, trailer.transform.position);
            c.Metric("unhitched_drift", drift, "m");
            c.Check(drift < 0.5f, $"the bowser stays where it was left ({drift:0.00} m)");
            c.Check(MobilityKit.Flat(tower.transform.position, parked) > 6f, "the tow vehicle drives off alone");
            yield return MobilityKit.Until(() => Mathf.Abs(tower.ForwardSpeed) < 0.3f, 5f);
            tower.brakeInput = 0f; tower.handbrake = true;
            g.Exit();
        }
    }

    /// <summary>Front winch: [4] hooks the nearest anchor ahead (a parked car 14 m off), holding [5] reels in and
    /// drags it (its parked brakes locked) towards the winch truck; [4] again releases.</summary>
    class WinchPull : Scenario
    {
        public override string Id => "mobility.winch";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var truck = TestWorld.Vehicle("Wrecker");
            if (!truck || !truck.GetComponent<Winch>() || !truck.GetComponent<Winch>().WinchPart)
                truck = g.Fleet.FirstOrDefault(v => v && v.driveable && v.TryGetComponent<Winch>(out var w) && w.WinchPart && !v.GetComponent<Machine>());
            var load = TestWorld.Vehicle("Sedan");
            if (!truck || !load) { c.Block("no fleet vehicle with a winch bumper, or no sedan"); yield break; }
            var winch = truck.GetComponent<Winch>();
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, truck, pad, fwd, 0.1f);
            yield return TestWorld.Place(c, load, pad + fwd * 16f, fwd, 1.2f);
            g.Enter(truck); yield return new WaitForSeconds(0.3f);
            winch.Control(false, false, false);
            c.Check((winch.Status ?? "").Contains("HOOK"), "the winch is ready: " + winch.Status);
            float gap0 = MobilityKit.Flat(truck.transform.position, load.transform.position);
            var l0 = load.transform.position;
            winch.Control(true, false, false);
            yield return null;
            if (!c.Check(winch.Hooked, "[4] hooks the car ahead: " + winch.Status)) yield break;
            float t0 = Time.time;
            c.Fixture("the winch truck stands on its brakes while reeling in (the player holds S + Space)");
            while (Time.time - t0 < 7f) { truck.handbrake = true; truck.brakeInput = 1f; winch.Control(false, true, false); yield return null; }
            truck.brakeInput = 0f;
            float gap1 = MobilityKit.Flat(truck.transform.position, load.transform.position);
            float dragged = Vector3.Dot(l0 - load.transform.position, fwd);
            c.Metric("gap_closed", gap0 - gap1, "m");
            c.Metric("load_dragged", dragged, "m");
            c.Check(gap0 - gap1 > 3f, $"reeling in closes the gap ({gap0:0.0} -> {gap1:0.0} m)");
            c.Check(dragged > 1f, $"the car is dragged towards the truck ({dragged:0.0} m)");
            c.Screenshot("winch");
            yield return null;
            winch.Control(true, false, false);
            c.Check(!winch.Hooked, "[4] again releases the cable");
            g.Exit();
        }
    }

    /// <summary>Crane truck: slewed out to the side, [7] grabs a small car under the hook, [8] hoists it clear of the
    /// ground, the boom slews it round, [9] lowers it and [7] lets go: the car sets down on its wheels, unharmed.</summary>
    class CraneLift : Scenario
    {
        public override string Id => "mobility.crane";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 110f;

        static IEnumerator Work(Crane cr, bool hoist, bool lower, float slew, float seconds)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seconds) { cr.Control(false, hoist, lower, slew); yield return null; }
            t0 = Time.time;
            while (Time.time - t0 < 0.8f) { cr.Control(false, false, false, 0f); yield return null; }        // ease the slew out
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var truck = g.Fleet.FirstOrDefault(v => v && v.TryGetComponent<Crane>(out var cr) && cr.CranePart);
            var car = TestWorld.Vehicle("Trabant") ?? TestWorld.Vehicle("Fiat126p");
            if (!truck || !car) { c.Block("no crane truck or small car in the fleet"); yield break; }
            var crane = truck.GetComponent<Crane>();
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no level pad"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, truck, pad, Vector3.forward, 1f);
            g.Enter(truck); yield return new WaitForSeconds(0.3f);
            truck.handbrake = true;

            // slew the boom out over the side
            var tip0 = crane.HookPoint;
            yield return Work(crane, false, false, 1f, 3.2f);
            var hook = crane.HookPoint;
            c.Metric("slewed_hook_moved", MobilityKit.Flat(tip0, hook), "m");
            c.Check(MobilityKit.Flat(tip0, hook) > 1f, "[0] slews the boom");
            var spot = new Vector3(hook.x, 0f, hook.z);
            if (MobilityKit.Flat(spot, truck.transform.position) < 3f) { c.Block($"the hook hangs over the truck ({MobilityKit.Flat(spot, truck.transform.position):0.0} m from its centre)"); yield break; }
            if (!MobilityKit.Clear(spot, 1.1f)) c.Note("under the hook: " + MobilityKit.Blockers(spot, 1.1f));
            yield return TestWorld.Place(c, car, spot, truck.transform.forward, 1.5f);
            float Above() => car.Body.worldCenterOfMass.y - DeformableTerrain.Instance.Height(car.transform.position.x, car.transform.position.z);
            float rest = Above();
            c.Note($"resting: COM {rest:0.00} m above the ground, touching {TestWorld.Contacts(car)}");
            c.Note($"hook {crane.HookPoint}, car {car.transform.position}, rope {crane.Rope:0.0} m, status {crane.Status}");

            crane.Control(true, false, false, 0f);
            yield return null;
            if (!c.Check(crane.Load == car.Body, "[7] grabs the " + MobilityKit.N(car) + ": " + crane.Status)) yield break;
            yield return Work(crane, true, false, 0f, 3f);
            float lift = Above() - rest;
            c.Note($"hoisted: rope {crane.Rope:0.0} m, hook {crane.HookPoint}, COM {car.Body.worldCenterOfMass}, touching {TestWorld.Contacts(car)}");
            c.Metric("hoisted", lift, "m");
            c.Check(lift > 0.4f, $"[8] hoists it off the ground ({lift:0.00} m)");
            c.Check(truck.transform.up.y > 0.9f, "the truck stays upright under the load");
            var held = car.transform.position;
            yield return Work(crane, false, false, -1f, 2f);
            float swung = MobilityKit.Flat(held, car.transform.position);
            c.Metric("slewed_load", swung, "m");
            c.Check(swung > 1f, $"the boom swings the load round ({swung:0.0} m)");
            c.Screenshot("crane");
            yield return null;
            yield return Work(crane, false, true, 0f, 3.5f);
            crane.Control(true, false, false, 0f);
            c.Check(!crane.Load, "[7] lets go");
            yield return new WaitForSeconds(2f);
            float above = car.transform.position.y - DeformableTerrain.Instance.Height(car.transform.position.x, car.transform.position.z);
            c.Metric("set_down_height", above, "m");
            c.Check(car && car.transform.up.y > 0.8f && above < 1.5f, $"the car sits on its wheels again ({above:0.00} m, up {car.transform.up.y:0.00})");
            c.Check(car.GetComponentsInChildren<WheelStats>().Length >= 4, "nothing fell off it");
            g.Exit();
        }
    }

    /// <summary>Car transporter (README "car transporters with a tilting upper deck"): a small car drives up the rear
    /// ramps onto the deck, on foot [E] straps it down, the trailer is towed with the car riding along, [E] releases it.
    /// The double deck's [T] tilts the upper deck down and raises it again.</summary>
    class TransporterLoad : Scenario
    {
        public override string Id => "mobility.transporter";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 150f;

        static bool OnDeck(TrailerDeck d, VehicleDriver v)
        {
            var l = d.transform.InverseTransformPoint(v.Body.worldCenterOfMass);
            return Mathf.Abs(l.x) <= 1.6f && Mathf.Abs(l.z) <= 3.6f && l.y >= 0.3f && l.y <= 4.5f;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var trailer = TestWorld.Vehicle("CarTrailer"); var car = TestWorld.Vehicle("Fiat126p") ?? TestWorld.Vehicle("Trabant");
            var tower = TestWorld.Vehicle("Pickup");
            if (!trailer || !car || !tower) { c.Block("car trailer, small car or pickup missing"); yield break; }
            var deck = trailer.GetComponent<TrailerDeck>(); var tc = trailer.GetComponent<TowCoupling>();
            if (!c.Check(deck && tc, "the car trailer has a deck and a coupler")) yield break;
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a lane"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, tower, pad + fwd * 14f, fwd, 0.2f);
            yield return Hauling.Behind(c, tower, trailer, fwd, 0.9f);
            var rampEnd = trailer.transform.TransformPoint(new Vector3(0f, 0f, -5.4f));
            yield return TestWorld.Place(c, car, rampEnd - fwd * 3.5f, fwd, 1.2f);

            // ---- drive up the ramps
            g.Enter(car); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, car);
            car.handbrake = false; car.throttleInput = 0.55f;
            float t0 = Time.time, highest = 0f;
            while (Time.time - t0 < 10f && !(OnDeck(deck, car) && Vector3.Dot(car.transform.position - trailer.transform.position, fwd) > -0.5f))
            {
                highest = Mathf.Max(highest, trailer.transform.InverseTransformPoint(car.Body.worldCenterOfMass).y);
                yield return null;
            }
            car.throttleInput = 0f; car.brakeInput = 1f;
            yield return MobilityKit.Until(() => Mathf.Abs(car.ForwardSpeed) < 0.2f, 4f);
            car.brakeInput = 0f; car.handbrake = true;
            yield return new WaitForSeconds(0.6f);
            bool drove = OnDeck(deck, car);
            c.Metric("highest_on_trailer", highest, "m");
            if (!c.Check(drove, "the car drives up the ramps onto the deck"))
            {
                c.Note(TestWorld.State(car) + "; touching: " + TestWorld.Contacts(car) + "; local " + trailer.transform.InverseTransformPoint(car.Body.worldCenterOfMass));
                g.Exit(); yield return null;
                var top = trailer.transform.Find("Deck");
                var on = (top ? top.position : trailer.transform.position) + Vector3.up * 1.2f;
                yield return MobilityKit.PlaceAt(c, car, on, fwd, 1.5f, "on the deck (to test the straps)");
            }
            else { g.Exit(); yield return null; }

            // ---- strap, tow, release
            deck.Use(g, false);
            c.Check(deck.Loaded == 1, $"[E] straps the car down ({deck.Loaded})");
            var rel = trailer.transform.InverseTransformPoint(car.transform.position);
            g.Enter(tower); yield return new WaitForSeconds(0.3f);
            ActionPress.Press(Controls.Act.Hitch);
            yield return MobilityKit.Until(() => tc.Tower == tower && !tc.Busy, 6f);
            if (!c.Check(tc.Tower == tower, "[J] hitches the transporter")) yield break;
            yield return TestWorld.StartEngine(c, tower);
            var start = trailer.transform.position;
            tower.handbrake = false; tower.throttleInput = 0.5f;
            yield return new WaitForSeconds(4f);
            tower.throttleInput = 0f; tower.brakeInput = 1f;
            yield return MobilityKit.Until(() => Mathf.Abs(tower.ForwardSpeed) < 0.3f, 6f);
            tower.brakeInput = 0f; tower.handbrake = true;
            float moved = MobilityKit.Flat(start, trailer.transform.position);
            float slip = Vector3.Distance(rel, trailer.transform.InverseTransformPoint(car.transform.position));
            c.Metric("transported", moved, "m");
            c.Metric("cargo_shift", slip, "m");
            c.Check(moved > 4f, $"the loaded transporter is towed ({moved:0.0} m)");
            c.Check(slip < 0.1f, $"the strapped car rides along ({slip:0.00} m shift)");
            ActionPress.Press(Controls.Act.Hitch);
            yield return MobilityKit.Until(() => !tc.Tower && !tc.Busy, 4f);
            g.Exit(); yield return null;
            deck.Use(g, false);
            c.Check(deck.Loaded == 0 && !car.Body.isKinematic && car.transform.parent == null, "[E] releases the car");
            yield return new WaitForSeconds(1.5f);
            c.Check(OnDeck(deck, car) && car.transform.up.y > 0.8f, "released, it rests on the deck");

            // ---- the double deck tilts
            var dbl = TestWorld.Vehicle("CarTrailerDouble");
            var dd = dbl ? dbl.GetComponent<TrailerDeck>() : null;
            var upper = dbl ? dbl.transform.Find("UpperDeck") : null;
            if (!dd || !upper) { c.Note("no double-deck transporter: tilt not checked"); yield break; }
            dd.Use(g, true);
            yield return MobilityKit.Until(() => Mathf.Abs(Mathf.DeltaAngle(0f, upper.localEulerAngles.x) - TrailerDeck.UpperTilt) < 0.5f, 7f);
            float tilt = Mathf.DeltaAngle(0f, upper.localEulerAngles.x);
            c.Metric("upper_deck_tilt", tilt, "deg");
            c.Check(Mathf.Abs(tilt - TrailerDeck.UpperTilt) < 1f, $"[T] tilts the upper deck down ({tilt:0} deg)");
            dd.Use(g, true);
            yield return MobilityKit.Until(() => Mathf.Abs(Mathf.DeltaAngle(0f, upper.localEulerAngles.x)) < 0.5f, 7f);
            c.Check(Mathf.Abs(Mathf.DeltaAngle(0f, upper.localEulerAngles.x)) < 1f, "[T] raises it again");
        }
    }
}
