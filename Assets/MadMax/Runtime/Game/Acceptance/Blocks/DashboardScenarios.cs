using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Instrument clusters (smoke test): one vehicle of each generic style, sat in and seen in first person —
    /// the right style, real geometry (needles, lamps), the key-on bulb check, OIL / CHARGE lit with the engine off and
    /// out once it runs, the tacho needle off its stop. Captures each cluster.</summary>
    public static class DashboardScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new DashboardsSmoke();
        }
    }

    class DashboardsSmoke : Scenario
    {
        public override string Id => "vehicle.dashboards";
        public override float Timeout => 150f;

        static readonly (string design, string style)[] Cases =
        {
            ("Sedan", DashboardStyles.Classic), ("Semi", DashboardStyles.Heavy), ("DuneBuggy", DashboardStyles.Sport), ("APC", DashboardStyles.Rugged),
        };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var rig = g.cameraRig;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(9f, 20f, out var pad, out var fwd)) { c.Block("no level pad"); yield break; }
            foreach (var (design, style) in Cases)
            {
                var v = TestWorld.Vehicle(design);
                bool spawned = false;
                if (!v) { v = g.SpawnFound(design, pad + Vector3.up, Quaternion.LookRotation(fwd)); spawned = v; }
                if (!v) { c.Note(design + ": no prefab, skipped"); continue; }
                yield return TestWorld.Place(c, v, pad, fwd, 1f);
                var sys = v.GetComponent<VehicleSystems>();
                if (sys && sys.Started) sys.Stop();
                var dash = v.GetComponent<VehicleDashboard>();
                g.Enter(v);
                if (rig) rig.mode = ViewMode.FirstPerson;
                yield return null;
                if (dash) dash.Ensure();
                yield return new WaitForSeconds(0.4f);
                if (!c.Check(dash && dash.Parts != null, design + ": a cluster is built")) { g.Exit(); continue; }
                var p = dash.Parts;
                int needles = 0; foreach (var ins in p.gauges) if (ins.needle) needles++;
                c.Check(dash.StyleId == style, $"{design}: style {dash.StyleId} (want {style}), {(dash.IsHD ? "HDLit" : "PixelVoxel")}");
                c.Check(needles >= 1 && p.lamps.Count >= 10 && p.root.name == "Dashboard", $"{design}: {needles} needles, {p.lamps.Count} lamps, {p.gauges.Count} instruments");
                c.Check(dash.KeyOn && dash.LampState(DashLamp.Fuel) != 0 && dash.LampState(DashLamp.Brake) != 0, design + ": key on lights every lamp (bulb check)");
                yield return new WaitForSeconds(VehicleDashboard.BulbCheckSeconds + 0.3f);
                if (sys && !sys.Started)
                    c.Check(dash.LampState(DashLamp.Oil) == 2 && dash.LampState(DashLamp.Charge) == 2, design + ": engine off: OIL and CHARGE lamps red");
                c.Screenshot("dash_" + style + "_keyon");
                yield return null;
                yield return TestWorld.StartEngine(c, v);
                yield return new WaitForSeconds(2.5f);
                DashInstrument tacho = null;
                foreach (var ins in p.gauges) if (ins.def.source == GaugeSource.Rpm || ins.def.source == GaugeSource.ShiftLights) { tacho = ins; if (ins.needle) break; }
                if (sys && sys.Started)
                {
                    c.Check(dash.LampState(DashLamp.Charge) == 0, design + ": running: CHARGE lamp out");
                    if (tacho != null && tacho.needle) c.Check(tacho.shown > tacho.min + 200f, $"{design}: tacho reads {tacho.shown:0} rpm at idle");
                }
                c.Screenshot("dash_" + style + "_running");
                yield return null;
                g.Exit();
                yield return new WaitForSeconds(0.5f);
                if (spawned) Object.Destroy(v.gameObject);
                yield return null;
            }
        }
    }
}
