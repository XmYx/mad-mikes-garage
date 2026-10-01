using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Vehicle storage (wave 4): compartments reached from the right places, saved, wreck loot; and vehicles
    /// knocking people away instead of carrying them.</summary>
    public static class VehicleStorageScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new VehicleStorages();
            yield return new NpcKnockaway();
        }

        internal static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        internal static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    /// <summary>VEHICLE STORAGES: a Sedan has TRUNK, GLOVEBOX and BACK SEAT containers; the trunk opens from behind
    /// ([E] OPEN TRUNK) but not from the front or the passenger door; the glovebox opens at the passenger door and from
    /// the driver's seat (context menu), the trunk not from the seat; contents survive the save; a Pickup carries a bed
    /// reached over the side; wreck loot is deterministic.</summary>
    class VehicleStorages : Scenario
    {
        public override string Id => "vehicle.storages";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            WastelandGame.ExternalInput = true;
            if (!TestWorld.Pad(6f, 0f, out var pad, out var dir)) { c.Block("no level pad"); yield break; }
            var car = VehicleFixScenarios.Find(c, "Sedan", pad);
            if (!car) { c.Block("no Sedan"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            g.Player.Teleport(pad + Vector3.Cross(Vector3.up, dir) * 8f + Vector3.up * 0.3f, 0f);
            yield return TestWorld.Place(c, car, pad, dir, 1.5f);
            car.Body.isKinematic = false;
            var st = car.GetComponent<VehicleStorage>();
            if (!c.Check(st, "the Sedan has VehicleStorage")) yield break;
            var trunk = st.Compartment(VehicleStorage.Kind.Trunk);
            var glove = st.Compartment(VehicleStorage.Kind.Glovebox);
            var seat = st.Compartment(VehicleStorage.Kind.Seats);
            if (!c.Check(trunk && glove && seat, "TRUNK, GLOVEBOX and BACK SEAT compartments")) yield break;
            c.Metric("trunk_kg", trunk.container.capacity, "kg"); c.Metric("glovebox_kg", glove.container.capacity, "kg"); c.Metric("seat_kg", seat.container.capacity, "kg");
            c.Check(trunk.container.capacity > seat.container.capacity && seat.container.capacity > glove.container.capacity, "trunk > back seat > glovebox");
            c.Check(Container.All.Contains(trunk.container) && trunk.container.accessPoint, "compartments are Containers with an access point (loot overlay)");

            // ---- behind the car: the trunk, not the glovebox
            var behind = trunk.StandAt(car.transform.position);
            g.Player.Teleport(behind, VehicleStorageScenarios.Yaw(car.transform.position - behind));
            yield return VehicleStorageScenarios.Frames(5);
            c.Check(trunk.CanOpen(g), $"the trunk opens from behind ({trunk.Why(g) ?? "ok"})");
            c.Check(!glove.CanOpen(g), "the glovebox does not open from behind");
            var p = g.Prompt ?? "";
            c.Check(p.Contains("OPEN TRUNK"), $"[E] OPEN TRUNK prompt behind the car (\"{p}\")");
            c.Screenshot("storage_trunk");
            yield return null;

            // ---- in front: nothing
            var front = car.transform.position + car.transform.forward * 3.4f;
            front.y = DeformableTerrain.Instance.Height(front.x, front.z) + 0.05f;
            g.Player.Teleport(front, VehicleStorageScenarios.Yaw(car.transform.position - front));
            yield return VehicleStorageScenarios.Frames(5);
            c.Check(!trunk.CanOpen(g), $"the trunk does not open from the front ({trunk.Why(g)})");
            p = g.Prompt ?? "";
            c.Check(!p.Contains("OPEN TRUNK"), "no trunk prompt at the front");

            // ---- the passenger door: glovebox, not trunk
            var door = glove.StandAt(car.transform.position);
            g.Player.Teleport(door, VehicleStorageScenarios.Yaw(car.transform.position - door));
            yield return VehicleStorageScenarios.Frames(5);
            c.Check(glove.CanOpen(g), $"the glovebox opens at the passenger door ({glove.Why(g) ?? "ok"})");
            c.Check(!trunk.CanOpen(g), "the trunk does not open from the passenger door");
            var rearDoor = seat.StandAt(car.transform.position);
            g.Player.Teleport(rearDoor, VehicleStorageScenarios.Yaw(car.transform.position - rearDoor));
            yield return VehicleStorageScenarios.Frames(3);
            c.Check(seat.CanOpen(g), "the back seat opens at a rear door");

            // ---- seated: glovebox (and back seat) yes, trunk no
            g.Enter(car);
            yield return new WaitForSeconds(0.5f);
            if (c.Check(g.Current == car, "in the driver's seat"))
            {
                c.Check(glove.CanOpen(g), "the glovebox opens from the driver's seat");
                c.Check(!trunk.CanOpen(g), "the trunk does not open from the driver's seat");
                var opts = g.ContextOptionsFor(car);
                var gl = opts.Find(o => o.label == "OPEN GLOVEBOX");
                c.Check(gl != null && gl.blocked == null, "the vehicle's context menu offers OPEN GLOVEBOX while driving");
                c.Check(opts.Find(o => o.label == "OPEN TRUNK") == null, "and no OPEN TRUNK");
                if (gl != null && gl.blocked == null)
                {
                    gl.run();
                    yield return VehicleStorageScenarios.Frames(3);
                    c.Check(g.Menus.IsOpen && g.Menus.LootCurrent != null && g.Menus.LootCurrent.box == glove.container, "the loot window shows the glovebox");
                    g.Menus.Close();
                    yield return null;
                }
                g.Exit();
                yield return new WaitForSeconds(0.3f);
            }

            // ---- contents survive the save
            trunk.container.inventory.AddItem(ItemIds.Paper, 3);
            glove.container.inventory.AddItem("med_pills", 2);
            seat.container.inventory.Add(ResourceType.Cloth, 4);
            c.Fixture("3 paper in the trunk, 2 pills in the glovebox, 4 cloth on the back seat");
            var saved = g.CaptureSave();
            VehicleSave vs = null; float bd = float.MaxValue;
            foreach (var x in saved.vehicles) { if (x.design != "Sedan") continue; float d = (x.position - car.transform.position).sqrMagnitude; if (d < bd) { bd = d; vs = x; } }
            if (c.Check(vs != null && !string.IsNullOrEmpty(vs.storage), "VehicleSave.storage written"))
            {
                st.LoadState(null);
                c.Check(trunk.container.inventory.GetItem(ItemIds.Paper) == 0, "emptied");
                st.LoadState(vs.storage);
                c.Check(trunk.container.inventory.GetItem(ItemIds.Paper) == 3 && glove.container.inventory.GetItem("med_pills") == 2 && seat.container.inventory.Get(ResourceType.Cloth) == 4,
                    "restored from the save state (trunk 3 paper, glovebox 2 pills, seat 4 cloth)");
            }
            var legacy = new VehicleSave();
            c.Check(legacy.storage == null, "older saves without storage load (field defaults to null)");
            if (Profile.Isolated)
            {
                var at = car.transform.position;
                g.SaveGame(2);
                var old = g;
                g.LoadGame(2);
                while ((WastelandGame.Instance == old || !WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - c.startedAt < 200f) yield return null;
                g = WastelandGame.Instance;
                if (c.Check(g && g != old && g.Ready, "the save loads"))
                {
                    WastelandGame.ExternalInput = true;
                    yield return new WaitForSeconds(0.5f);
                    VehicleDriver back = null; bd = float.MaxValue;
                    foreach (var v in Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None))
                        if (v.GetComponent<VehicleChassis>() && v.GetComponent<VehicleChassis>().vehicleName == "Sedan") { float d = (v.transform.position - at).sqrMagnitude; if (d < bd) { bd = d; back = v; } }
                    var st2 = back ? back.GetComponent<VehicleStorage>() : null;
                    var t2 = st2 ? st2.Get(VehicleStorage.Kind.Trunk) : null;
                    var g2 = st2 ? st2.Get(VehicleStorage.Kind.Glovebox) : null;
                    c.Check(t2 && g2 && t2.inventory.GetItem(ItemIds.Paper) == 3 && g2.inventory.GetItem("med_pills") == 2, "trunk and glovebox contents survive save / load");
                }
            }
            else c.Note("not an isolated profile: full scene reload skipped (state round trip checked above)");

            // ---- a pickup: the bed, reached over the side
            if (!TestWorld.Pad(6f, 0f, out var pad2, out var dir2)) yield break;
            var pickup = VehicleFixScenarios.Find(c, "Pickup", pad2 + Vector3.Cross(Vector3.up, dir2) * 7f);
            if (pickup)
            {
                if (g.Current) { g.Exit(); yield return null; }
                yield return TestWorld.Place(c, pickup, pad2 + Vector3.Cross(Vector3.up, dir2) * 7f, dir2, 1.2f);
                var pst = pickup.GetComponent<VehicleStorage>();
                var bed = pst ? pst.Compartment(VehicleStorage.Kind.Bed) : null;
                if (c.Check(bed, "the Pickup has a PICKUP BED"))
                {
                    c.Metric("pickup_bed_kg", bed.container.capacity, "kg");
                    c.Check(bed.container.capacity >= 200f, "the bed holds at least 200 kg");
                    var side = pickup.transform.TransformPoint(bed.access[1]) - Vector3.up * 0.35f;
                    g.Player.Teleport(side, VehicleStorageScenarios.Yaw(pickup.transform.position - side));
                    yield return VehicleStorageScenarios.Frames(4);
                    c.Check(bed.CanOpen(g), "the bed opens over the side");
                    c.Screenshot("storage_pickup_bed");
                    yield return null;
                }
                // ---- wreck loot: deterministic from the seed
                if (pst)
                {
                    int filled = 0; bool same = true;
                    for (int seed = 1; seed <= 6; seed++)
                    {
                        pst.LoadState(null); pst.FillWreck(seed * 7919); var a = pst.SaveState();
                        pst.LoadState(null); pst.FillWreck(seed * 7919); var b = pst.SaveState();
                        if (a != b) same = false;
                        if (!string.IsNullOrEmpty(a)) filled++;
                    }
                    pst.LoadState(null);
                    c.Metric("wreck_fills_with_loot", filled, "of 6");
                    c.Check(same, "wreck compartments fill the same way from the same seed");
                    c.Check(filled >= 2, $"most wrecks have something in their compartments ({filled} of 6)");
                }
            }
            else c.Note("no Pickup: bed check skipped");
            WastelandGame.ExternalInput = false;
        }
    }

    /// <summary>KNOCKED AWAY, NOT CARRIED: a car at 40 km/h into a standing person throws the body; 1.5 s later the
    /// body is ≥ 2 m from the car and not moving with it. The same for a body already lying in the road (run over).</summary>
    class NpcKnockaway : Scenario
    {
        public override string Id => "vehicle.npc_knockaway";
        public override float Timeout => 120f;
        const float Speed = 40f / 3.6f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            WastelandGame.ExternalInput = true;
            if (!TestWorld.Pad(5f, 50f, out var pad, out var dir)) { c.Block("no level pad with a 50 m lane"); yield break; }
            var car = VehicleFixScenarios.Find(c, "Sedan", pad);
            if (!car) { c.Block("no Sedan"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            g.Player.Teleport(pad + Vector3.Cross(Vector3.up, dir) * 8f + Vector3.up * 0.3f, 0f);

            // ---- a standing person
            var at = pad + dir * 18f;
            at.y = DeformableTerrain.Instance.Height(at.x, at.z);
            var npc = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:knock1", NpcRole.Wanderer, 6161), at + Vector3.up * 0.05f, VehicleStorageScenarios.Yaw(-dir), null, g.propMaterial);
            c.Fixture("a wanderer standing 18 m down the lane");
            yield return Drive(c, g, car, pad, dir, npc, null);

            // ---- a body lying in the road
            if (!TestWorld.Pad(5f, 50f, out pad, out dir)) yield break;
            if (g.Current) { g.Exit(); yield return null; }
            at = pad + dir * 18f;
            at.y = DeformableTerrain.Instance.Height(at.x, at.z);
            var dead = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:knock2", NpcRole.Wanderer, 6262), at + Vector3.up * 0.05f, VehicleStorageScenarios.Yaw(dir), null, g.propMaterial);
            yield return null;
            dead.ApplyHit(at + Vector3.up, Vector3.down, 50f, 0.3f, null);
            c.Fixture("a second wanderer killed where they stood (a body in the road)");
            var rd = dead.GetComponent<Ragdoll>();
            yield return VehicleFixScenarios.Until(() => rd && rd.Frozen, 15f);
            c.Check(rd && rd.Active, $"the body lies limp ({(rd && rd.Frozen ? "settled" : "still moving")})");
            yield return Drive(c, g, car, pad, dir, dead, rd);
            WastelandGame.ExternalInput = false;
        }

        IEnumerator Drive(ScenarioContext c, WastelandGame g, VehicleDriver car, Vector3 pad, Vector3 dir, MadMax.Npc.Npc target, Ragdoll lying)
        {
            string what = lying ? "body" : "standing";
            yield return TestWorld.Place(c, car, pad, dir, 1.2f);
            g.Enter(car);
            yield return new WaitForSeconds(0.4f);
            if (!c.Check(g.Current == car, "driving the Sedan")) yield break;
            car.handbrake = false; car.brakeInput = 0f; car.throttleInput = 0.5f;
            Vector3 Pelvis() { var r = target ? target.GetComponent<Ragdoll>() : null; return r && r.Active ? r.Pelvis : target ? target.transform.position + Vector3.up : Vector3.zero; }
            bool Struck() { if (!target) return true; var k = target.GetComponent<BodyKnock>(); return lying ? k && k.KnockedAt > 0f && k.KnockedBy == car : !target.Alive || target.Down; }
            float t0 = Time.time;
            c.Fixture($"the Sedan held at {Speed * 3.6f:0} km/h towards the {what}");
            while (!Struck() && Time.time - t0 < 6f)
            {
                var to = Pelvis() - car.Body.position; to.y = 0f;
                var aim = Vector3.Dot(to, dir) > 1.5f ? to.normalized : dir;                   // straight at them
                car.Body.linearVelocity = aim * Speed + Vector3.up * car.Body.linearVelocity.y;
                yield return new WaitForFixedUpdate();
            }
            if (!c.Check(Struck(), $"the car hits the {what} ({Time.time - t0:0.0} s)")) { car.throttleInput = 0f; g.Exit(); yield break; }
            c.Metric(what + "_impact_speed", car.Body.linearVelocity.magnitude * 3.6f, "km/h");
            if (lying) c.Fixture("the Sedan drives on over the body");
            else { car.throttleInput = 0f; car.brakeInput = 1f; c.Fixture("the driver stands on the brake after the hit"); }
            yield return new WaitForSeconds(0.25f);
            c.Screenshot("knockaway_" + what);
            yield return null;
            yield return new WaitForSeconds(1.25f);
            var pel = Pelvis();
            var cp = car.Body.ClosestPointOnBounds(pel);
            float gap = Vector3.Distance(pel, cp);
            var r2 = target ? target.GetComponent<Ragdoll>() : null;
            var prb = r2 && r2.Active ? target.GetComponent<HumanRig>().Bone(MadMax.Game.BodyPart.Pelvis).GetComponent<Rigidbody>() : null;
            var bodyVel = prb && !prb.isKinematic ? prb.linearVelocity : Vector3.zero;
            var carVel = car.Body.linearVelocity;
            bool carried = gap < 0.6f && carVel.magnitude > 1f && (bodyVel - carVel).magnitude < 1f;
            c.Metric(what + "_gap_after_1.5s", gap, "m");
            c.Metric(what + "_car_speed_after_1.5s", carVel.magnitude, "m/s");
            c.Metric(what + "_relative_speed", (bodyVel - carVel).magnitude, "m/s");
            c.Check(gap >= 2f, $"1.5 s later the {what} lies {gap:0.0} m from the car (≥ 2 m)");
            c.Check(!carried && (prb == null || prb.transform.parent == null || !prb.transform.IsChildOf(car.transform)), $"not moving with the car (body {bodyVel.magnitude:0.0} m/s, car {carVel.magnitude:0.0} m/s)");
            car.brakeInput = 0f; car.handbrake = true;
            g.Exit();
            yield return new WaitForSeconds(0.3f);
        }
    }
}
