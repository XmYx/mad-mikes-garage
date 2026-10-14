using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>HD HUD (2026-10-13): vector panels, glyphs and gauges at screen resolution in every mode, and the 3D
    /// damage view of the current vehicle.</summary>
    public static class HudScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new HdHudSmoke();
        }
    }

    /// <summary>HD HUD on: on foot the vector layers carry shapes; driving the sedan the cluster and the damage view
    /// draw (the view renders, at most ~10 Hz); a worn tyre, a hurt engine and a part knocked off show up in the view
    /// (the lost part as a ghost); in an aircraft the flight panel draws. Screenshots of each.</summary>
    class HdHudSmoke : Scenario
    {
        public override string Id => "ui.hd_hud";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var gs = GameSettings.Current;
            bool was = gs.hdHud;
            gs.hdHud = true;
            var hud = PixelHud.Instance;
            if (!hud) { c.Block("no PixelHud"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.5f); }
            yield return new WaitForSeconds(0.3f);
            var cv = PixelHud.Canvas;
            c.Check(cv != null && cv.vec != null, "HD HUD: the canvas records vector shapes");
            c.Check(cv != null && cv.vec != null && cv.vec.back.Count + cv.vec.front.Count > 20, $"on foot: vector shapes drawn ({(cv != null && cv.vec != null ? cv.vec.back.Count + cv.vec.front.Count : 0)})");
            c.Screenshot("on_foot");
            yield return null;

            // driving
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("no sedan in the start fleet"); gs.hdHud = was; yield break; }
            if (!TestWorld.Pad(8f, 30f, out var pad, out var fwd)) { c.Block("no level pad"); gs.hdHud = was; yield break; }
            yield return TestWorld.Place(c, v, pad, fwd);
            g.Enter(v);
            yield return new WaitForSeconds(0.6f);
            yield return TestWorld.StartEngine(c, v);
            v.handbrake = false; v.throttleInput = 0.6f;
            int r0 = HudVehicleView.Renders;
            yield return new WaitForSeconds(2f);
            v.throttleInput = 0f; v.brakeInput = 1f;
            int renders = HudVehicleView.Renders - r0;
            c.Metric("damage_view_renders_2s", renders, "renders");
            c.Check(PixelHud.LastClusterFrame >= Time.frameCount - 2, "driving: the HD cluster draws");
            c.Check(PixelHud.LastDamageViewFrame >= Time.frameCount - 2, "driving: the damage view panel draws");
            var view = hud.DamageView;
            c.Check(view != null && view.Ready && view.Target == v, "the damage view renders the sedan");
            c.Check(renders >= 3 && renders <= 24, $"the view renders at a capped rate while moving ({renders} in 2 s)");
            c.Check(view != null && view.Proxies > 3, $"proxies share the sedan's meshes ({(view != null ? view.Proxies : 0)})");
            c.Screenshot("driving");
            yield return null;

            // damage: a worn tyre, a hurt engine, a part knocked off
            var chassis = v.GetComponent<VehicleChassis>();
            MountSocket lose = null;
            foreach (var s in chassis.Sockets)
            {
                if (!s.Current) continue;
                if (s.accepts == PartCategory.Wheel && s.Current.TryGetComponent<WheelStats>(out var ws)) ws.wear = Mathf.Max(ws.wear, 0.8f);
                if (s.accepts == PartCategory.Engine) s.Current.damage = Mathf.Max(s.Current.damage, 0.6f);
                if (!lose && s.accepts != PartCategory.Wheel && s.accepts != PartCategory.Engine && s.Current.GetComponentInChildren<MeshRenderer>()) lose = s;
            }
            yield return new WaitForSeconds(1.2f);
            int ghosts0 = view != null ? view.Ghosts : 0;
            if (lose) { lose.Detach(); c.Fixture("knocked off " + lose.name); }
            yield return new WaitForSeconds(1.6f);
            c.Check(!lose || (view != null && view.Ghosts > ghosts0), $"the lost part stays in the view as a ghost ({(view != null ? view.Ghosts : 0)})");
            bool engineMarked = false;
            if (view != null) foreach (var m in view.markers) if (m.cat == PartCategory.Engine && m.damage >= 0.55f) engineMarked = true;
            c.Check(engineMarked, "the hurt engine is marked in the view");
            c.Screenshot("damage_view");
            yield return null;
            g.Exit();
            yield return new WaitForSeconds(0.5f);

            // aircraft
            var air = TestWorld.Vehicle("Ultralight") ?? TestWorld.Vehicle("Gyrocopter");
            if (air && TestWorld.Pad(10f, 30f, out var apad, out var afwd))
            {
                yield return TestWorld.Place(c, air, apad, afwd);
                g.Enter(air);
                yield return new WaitForSeconds(1f);
                c.Check(PixelHud.LastClusterFrame >= Time.frameCount - 2, "in the aircraft: the HD flight panel draws");
                c.Screenshot("aircraft");
                yield return null;
                g.Exit();
            }
            else c.Note("no aircraft in the fleet: flight panel not captured");
            gs.hdHud = was;
        }
    }
}
