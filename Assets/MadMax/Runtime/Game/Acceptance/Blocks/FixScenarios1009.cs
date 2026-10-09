using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game.Acceptance
{
    /// <summary>User fixes (2026-10-08/09): parked cars sit on the ground, the dozer blade's range, the excavator's
    /// bucket on its pin, crash injuries by the change of speed, the mouse wheel on list pages.</summary>
    public static class FixScenarios1009
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new CrashHarm();
            yield return new ParkedSettle();
            yield return new DozerBlade();
            yield return new ExcavatorBucket();
            yield return new MenuWheel();
        }
    }

    /// <summary>A belted driver walks away from a 20 km/h knock into a wall (no wound, little health lost) and is hurt
    /// at 50 km/h; the harm curve: nothing under 25 km/h in a cab, growing steeply above.</summary>
    class CrashHarm : Scenario
    {
        public override string Id => "vehicle.crash_harm";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            c.Check(WastelandGame.CrashHarm(6.9f, false) == 0f && WastelandGame.CrashHarm(10f, false) < 10f && WastelandGame.CrashHarm(13.9f, false) > 22f && WastelandGame.CrashHarm(6f, true) > 0f,
                    $"harm by Δv: 25 km/h {WastelandGame.CrashHarm(6.9f, false):0}, 36 km/h {WastelandGame.CrashHarm(10f, false):0}, 50 km/h {WastelandGame.CrashHarm(13.9f, false):0}; open seat 22 km/h {WastelandGame.CrashHarm(6f, true):0}");
            if (!TestWorld.Pad(6f, 40f, out var pad, out var dir)) { c.Block("no pad with a lane"); yield break; }
            var car = VehicleFixScenarios.Find(c, "Sedan", pad);
            if (!car) { c.Block("no Sedan"); yield break; }
            foreach (var (kmh, hurt) in new[] { (20f, false), (50f, true) })
            {
                if (g.Current) { g.Exit(); yield return null; }
                yield return TestWorld.Place(c, car, pad, dir, 1.2f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "TestWall";
                var wp = pad + dir * 9f; wp.y = DeformableTerrain.Instance.Height(wp.x, wp.z) + 1.5f;
                wall.transform.SetPositionAndRotation(wp, Quaternion.LookRotation(dir));
                wall.transform.localScale = new Vector3(8f, 3f, 1f);
                var rb = wall.AddComponent<Rigidbody>(); rb.isKinematic = true;
                g.Stats.health = g.Stats.MaxHealth; g.Stats.injuries.Clear();
                g.Enter(car);
                yield return new WaitForSeconds(0.6f);
                car.handbrake = false; car.brakeInput = 0f;
                car.Body.linearVelocity = dir * (kmh / 3.6f);
                c.Fixture($"the Sedan sent into a wall at {kmh:0} km/h");
                yield return new WaitForSeconds(2f);
                int wounds = g.Stats.injuries.Count;
                float lost = g.Stats.MaxHealth - g.Stats.health;
                c.Metric($"crash_{kmh:0}_wounds", wounds, ""); c.Metric($"crash_{kmh:0}_health_lost", lost, "");
                if (hurt) c.Check(wounds > 0, $"at {kmh:0} km/h the driver is hurt ({wounds} wounds, {lost:0} health)");
                else c.Check(wounds == 0 && lost < 5f, $"at {kmh:0} km/h the belted driver walks away ({wounds} wounds, {lost:0} health)");
                Object.Destroy(wall);
                if (g.Current) g.Exit();
                yield return new WaitForSeconds(0.3f);
            }
            g.Stats.health = g.Stats.MaxHealth; g.Stats.injuries.Clear();
        }
    }

    /// <summary>Cars put to sleep far from the player rest on the ground: a car dropped from 1.5 m and frozen as the
    /// player leaves, and a road wreck spawned asleep, both sit on their wheels (no hover).</summary>
    class ParkedSettle : Scenario
    {
        public override string Id => "vehicle.parked_settle";
        public override float Timeout => 50f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(8f, 30f, out var pad, out var dir)) { c.Block("no pad"); yield break; }
            var car = VehicleFixScenarios.Find(c, "Pickup", pad);
            if (!car) { c.Block("no Pickup"); yield break; }
            var t = DeformableTerrain.Instance;
            car.Body.isKinematic = false;
            car.Body.position = new Vector3(pad.x, t.Height(pad.x, pad.z) + 1.5f + car.RestHeight, pad.z);
            car.Body.rotation = Quaternion.LookRotation(dir);
            car.Body.linearVelocity = Vector3.zero;
            c.Fixture("a pickup let go 1.5 m above the ground");
            yield return null;
            // the player drives off: 100 m away the sleeper freezes it mid-drop and must set it down
            g.Player.Teleport(pad + Vector3.Cross(Vector3.up, dir) * 100f + Vector3.up * 2f, 0f);
            c.Fixture("the player 100 m away");
            yield return VehicleFixScenarios.Until(() => car.Body.isKinematic, 3f);
            c.Check(car.Body.isKinematic, "far from the player the pickup is put to sleep (kinematic)");
            float hover = Hover(car);
            c.Metric("frozen_hover", hover, "m");
            c.Check(Mathf.Abs(hover) < 0.15f, $"asleep, it rests on its springs on the ground ({hover:+0.00;-0.00} m off the rest height)");
            // a road wreck spawned asleep
            var wreck = g.SpawnRoadWreck("Sedan", pad + dir * 14f, Quaternion.LookRotation(dir), 4242);
            if (c.Check(wreck, "a road wreck spawned"))
            {
                float wh = wreck.HasWheels ? Hover(wreck) : 0f;
                c.Metric("wreck_hover", wh, "m");
                c.Check(wreck.Body.isKinematic && Mathf.Abs(wh) < 0.2f, $"the wreck lies on the ground, not 0.8 m up ({wh:+0.00;-0.00} m off the rest height, wheels {wreck.HasWheels})");
                Object.Destroy(wreck.gameObject);
            }
            g.Player.Teleport(pad + Vector3.Cross(Vector3.up, dir) * 5f + Vector3.up * 0.5f, 0f);
            yield return new WaitForSeconds(1f);
        }

        /// <summary>Origin height over the terrain under it, less the rest height (0 = on its springs).</summary>
        static float Hover(VehicleDriver v)
        {
            var p = v.Body.position;
            var t = DeformableTerrain.Instance;
            var fwd = Vector3.ProjectOnPlane(v.transform.forward, Vector3.up).normalized; var right = Vector3.Cross(Vector3.up, fwd);
            float H(Vector3 a) => t.Height(a.x, a.z);
            float ground = (H(p + fwd * 1.4f) + H(p - fwd * 1.4f) + H(p + right * 0.75f) + H(p - right * 0.75f)) * 0.25f;
            return Vector3.Dot(p - new Vector3(p.x, ground, p.z), v.transform.up) - v.RestHeight;
        }
    }

    /// <summary>The bulldozer's blade lifts high for travel and drops below grade to cut: full up and full down reach
    /// the new limits (−34° / +22°) and the blade travels well over a metre between them.</summary>
    class DozerBlade : Scenario
    {
        public override string Id => "machines.dozer_blade";
        public override float Timeout => 50f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            var v = VehicleFixScenarios.Find(c, "Bulldozer", pad);
            if (!v) { c.Block("no Bulldozer"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1.5f);
            var m = v.GetComponent<Machine>();
            if (!c.Check(m, "a bulldozer with its blade")) yield break;
            g.Enter(v); yield return new WaitForSeconds(0.4f);
            WastelandGame.ExternalInput = true;
            Transform blade = null;
            foreach (var tr in v.GetComponentsInChildren<Transform>()) if (tr.name == "blade") { blade = tr; break; }
            WastelandGame.MachineInput = new MachineKeys { h2 = true };
            yield return new WaitForSeconds(4f);
            float up = m.BucketAngle, yUp = blade ? blade.position.y : 0f;
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward(Quaternion.Euler(0, 120f, 0) * v.transform.forward - Vector3.up * 0.2f); }
            yield return new WaitForSeconds(1f);
            c.Screenshot("blade_up");
            yield return null;
            WastelandGame.MachineInput = new MachineKeys { h1 = true };
            yield return new WaitForSeconds(5f);
            float down = m.BucketAngle, yDown = blade ? blade.position.y : 0f;
            c.Screenshot("blade_down");
            yield return null;
            WastelandGame.MachineInput = default;
            c.Metric("blade_up_deg", up, "deg"); c.Metric("blade_down_deg", down, "deg"); c.Metric("blade_travel", yUp - yDown, "m");
            c.Check(up <= -33f && down >= 21f, $"the blade reaches full up and full down ({up:0}° / {down:0}°)");
            c.Check(blade && yUp - yDown > 1.0f, $"the blade travels {yUp - yDown:0.00} m between them");
            if (g.cameraRig) g.cameraRig.mode = ViewMode.Isometric;
            g.Exit();
        }
    }

    /// <summary>The excavator's bucket hangs on the stick's pin: the digging edge (BucketTip) is on the bucket, the
    /// bucket reaches below its pin and holds it; a side view for the eye.</summary>
    class ExcavatorBucket : Scenario
    {
        public override string Id => "machines.excavator_bucket";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            var v = VehicleFixScenarios.Find(c, "Excavator", pad);
            if (!v) { c.Block("no Excavator"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1.5f);
            var m = v.GetComponent<Machine>();
            Transform bucket = null;
            foreach (var tr in v.GetComponentsInChildren<Transform>()) if (tr.name == "bucket") { bucket = tr; break; }
            var r = bucket ? bucket.GetComponentInChildren<Renderer>() : null;
            if (!c.Check(m && bucket && r, "an excavator with its bucket segment")) yield break;
            var b = r.bounds; b.Expand(0.2f);
            c.Check(b.Contains(m.BucketTip), $"the digging edge is on the bucket (tip {m.BucketTip}, bucket {r.bounds.min}..{r.bounds.max})");
            c.Check(r.bounds.min.y < bucket.position.y - 0.4f && r.bounds.SqrDistance(bucket.position) < 0.04f, "the bucket hangs from its pin (the pin on the bucket, the body below it)");
            g.Player.Teleport(v.transform.position + v.transform.right * 7f + v.transform.forward * 3f + Vector3.up * 0.3f, 0f);
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward(-v.transform.right - Vector3.up * 0.1f); }
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("excavator_side");
            yield return null;
            if (g.cameraRig) g.cameraRig.mode = ViewMode.Isometric;
        }
    }

    /// <summary>The mouse wheel scrolls list pages: down moves the selection on, up back, and it stops at the ends
    /// instead of wrapping round.</summary>
    class MenuWheel : Scenario
    {
        public override string Id => "ui.mouse_wheel";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var mouse = Mouse.current;
            if (mouse == null) { c.Block("no mouse device (headless)"); yield break; }
            foreach (var id in new[] { "food_ration", "med_bandage", "tool_wrench", "tool_knife", "tool_torch", "use_sponge", "tool_crowbar" }) g.Inventory.AddItem(id, 1);
            g.Menus.Open(MenuSystem.Page.Inventory);
            yield return null; yield return null;
            int rows = g.Menus.RowCount;
            if (!c.Check(rows > 4, $"the inventory page lists rows ({rows})")) { g.Menus.Close(); yield break; }
            int c0 = g.Menus.Cursor;
            yield return Wheel(mouse, -1f, 3);
            int down = g.Menus.Cursor;
            c.Check(down > c0, $"wheel down moves the selection on ({c0} -> {down})");
            yield return Wheel(mouse, 1f, 2);
            c.Check(g.Menus.Cursor < down, $"wheel up moves it back ({down} -> {g.Menus.Cursor})");
            yield return Wheel(mouse, 1f, rows + 3);
            c.Check(g.Menus.Cursor == 0, $"it stops at the top, no wrap-around ({g.Menus.Cursor})");
            g.Menus.Close();
        }

        static IEnumerator Wheel(Mouse mouse, float dir, int notches)
        {
            for (int i = 0; i < notches; i++)
            {
                InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0f, dir * 120f));
                yield return null;
                InputSystem.QueueDeltaStateEvent(mouse.scroll, Vector2.zero);
                yield return null;
            }
        }
    }
}
