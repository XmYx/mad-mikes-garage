using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Audit of roadmap 6-10 (2026-10-26): the refinery's flare and burner at work, the fish trap's catch on
    /// show, the water tower's float gauge / the rain barrel's gauge glass with overflow and weeping staves, the hand
    /// pump's gush, the metal detector's signal lamp.</summary>
    public static class ScheduledScenarios1026Audit
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new RefineryAtWork();
            yield return new TrapCatchShows();
            yield return new TankGauges();
            yield return new PumpGush();
            yield return new DetectorLamp();
        }

        internal static Vector3 Side(WastelandGame g) => Vector3.Cross(Vector3.up, ScheduledScenarios1016.Fwd(g));
    }

    /// <summary>Idle without power the flare is out; a run of crude lights a tall flame on the stack with soot going
    /// up; when the run stops it dies back.</summary>
    class RefineryAtWork : Scenario
    {
        public override string Id => "refining.refinery_flare";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var p = PH.Piece(g, "refinery", me + fwd * 9f, -fwd);
            if (!c.Check(p, "a refinery")) yield break;
            var flare = p.GetComponent<RefineryFlare>();
            var st = p.GetComponent<CraftingStation>();
            if (!c.Check(flare && st, "it has its flare")) { Object.Destroy(p.gameObject); yield break; }
            yield return new WaitForSeconds(0.5f);
            c.Check(flare.Flame < 0.05f && !flare.Working, $"no power, nothing to do: the flare is out ({flare.Flame:0.00})");
            st.watts = 0f;
            c.Fixture("refinery runs without a power net (watts 0)");
            var r = RecipeLibrary.Get("crack_petrol");
            if (!c.Check(r != null, "the petrol cut recipe")) { Object.Destroy(p.gameObject); yield break; }
            st.Enqueue(r, 0.05f);
            c.Fixture("a petrol cut queued at a twentieth of the speed (stays running)");
            yield return PH.Until(() => flare.Flame > 0.9f, 5f);
            c.Check(flare.Working && flare.Flame > 0.9f, $"a run lights the flare ({flare.Flame:0.00})");
            c.Check(flare.FlameHeight > 0.5f && p.transform.Find("Flare"), $"a tall flame stands on the stack ({flare.FlameHeight:0.00} m)");
            int soot = flare.SootPuffs;
            float h0 = flare.FlameHeight;
            yield return new WaitForSeconds(1f);
            c.Check(flare.SootPuffs > soot, $"soot goes up ({flare.SootPuffs - soot} puffs a second)");
            c.Check(Mathf.Abs(flare.FlameHeight - h0) > 0.005f, "the flame flickers");
            c.Check(p.transform.Find("BurnerLight"), "the fired heater has its burner glow");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.TransformPoint(new Vector3(0.5f, 2.5f, 0f)), ScheduledScenarios1026Audit.Side(g), 11f, "refinery_flare");
            st.queue.Clear();
            yield return PH.Until(() => flare.Flame < 0.05f, 6f);
            c.Check(!flare.Working && flare.Flame < 0.05f, $"the run stops: the flare dies back ({flare.Flame:0.00})");
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>Fish caught in the trap show inside the wire drum (mutants as glowing fish), the bait hangs in it while
    /// there is some, and a stranded catch flops.</summary>
    class TrapCatchShows : Scenario
    {
        public override string Id => "fishing.trap_catch";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var p = PH.Piece(g, "fish_trap", me + fwd * 3f, -fwd);
            if (!c.Check(p, "a fish trap")) yield break;
            var trap = p.GetComponent<FishTrap>();
            trap.bait = 2;
            c.Fixture("the trap baited with 2 baits");
            yield return null; yield return null;
            c.Check(trap.BaitShown, "the bait hangs inside");
            trap.Catch(false); trap.Catch(false); trap.Catch(true);
            c.Fixture("three catches made (one from a toxic lake)");
            yield return null;
            c.Check(trap.CatchShown == 3, $"three fish in the drum ({trap.CatchShown})");
            c.Check(trap.bait == 0 && !trap.BaitShown, "the baits are eaten");
            var f0 = trap.CatchVisual(0);
            var local = f0 ? f0.localPosition : Vector3.zero;
            c.Check(f0 && Mathf.Abs(local.z) < 0.6f && Mathf.Abs(local.x) < 0.3f && local.y > 0f && local.y < 0.7f, $"inside the wire ({local.x:0.00}, {local.y:0.00}, {local.z:0.00})");
            float t0 = Time.time;
            var r0 = new Quaternion[3];
            for (int i = 0; i < 3; i++) r0[i] = trap.CatchVisual(i) ? trap.CatchVisual(i).localRotation : Quaternion.identity;
            yield return PH.Until(() => trap.LastThrashAt > t0, 12f);
            c.Check(trap.LastThrashAt > t0, "out of the water the catch flops");
            float turned = 0f;
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                for (int i = 0; i < 3; i++) if (trap.CatchVisual(i)) turned = Mathf.Max(turned, Quaternion.Angle(r0[i], trap.CatchVisual(i).localRotation));
                yield return null;
            }
            c.Check(turned > 1f, $"a fish moves ({turned:0} deg)");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 0.35f, ScheduledScenarios1026Audit.Side(g), 1.6f, "trap_catch");
            trap.Use(g, false);
            yield return null;
            c.Check(trap.CatchShown == 0, "[E] empties it: the drum is empty");
            g.Inventory.TakeItem("food_fish_raw", 2); g.Inventory.TakeItem("food_fish_glow", 1);
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>The water tower's target rides with the water (a quarter full low, full at the top), it overflows while
    /// water still comes in, the staves weep; a rain barrel's gauge glass stands as high as its water.</summary>
    class TankGauges : Scenario
    {
        public override string Id => "irrigation.tank_gauges";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var side = ScheduledScenarios1026Audit.Side(g);
            var p = PH.Piece(g, "water_tower", me + fwd * 7f, -fwd);
            if (!c.Check(p, "a water tower")) yield break;
            var gauge = p.GetComponent<TankGauge>();
            var node = p.GetComponent<UtilityNode>();
            if (!c.Check(gauge && node, "it carries a gauge")) { Object.Destroy(p.gameObject); yield break; }
            node.clean = node.waterCapacity * 0.25f; node.dirty = 0f;
            c.Fixture("tower filled a quarter (750 L)");
            yield return PH.Until(() => Mathf.Abs(gauge.Level - 0.25f) < 0.03f && p.transform.Find("GaugeTarget"), 8f);
            float low = gauge.MarkerHeight;
            c.Check(Mathf.Abs(gauge.Level - 0.25f) < 0.03f, $"the float settles at a quarter ({gauge.Level:0.00})");
            c.Check(!gauge.Overflowing && !gauge.Weeping, "a quarter full: no overflow, the staves are dry");
            node.clean = node.waterCapacity;
            node.sourceClean = 0.5f;
            c.Fixture("tower filled to the brim with 0.5 L/s still coming in");
            yield return PH.Until(() => gauge.Level > 0.97f, 10f);
            c.Check(gauge.MarkerHeight > low + 1f, $"the target climbs to the top ({low:0.00} → {gauge.MarkerHeight:0.00} m)");
            yield return PH.Until(() => gauge.Overflowing, 3f);
            c.Check(gauge.Overflowing, "full with water coming in: the overflow runs");
            c.Check(gauge.Weeping, "the staves weep");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 4f, -fwd, 8f, "tower_gauge");
            node.sourceClean = 0f;
            yield return PH.Until(() => !gauge.Overflowing, 3f);
            c.Check(!gauge.Overflowing, "nothing coming in: the overflow stops");
            Object.Destroy(p.gameObject);

            var b = PH.Piece(g, "rain_collector", me + fwd * 3f, -fwd);
            if (!c.Check(b, "a rain barrel")) yield break;
            var bg = b.GetComponent<TankGauge>(); var bn = b.GetComponent<UtilityNode>();
            bn.clean = bn.waterCapacity * 0.5f;
            c.Fixture("rain barrel half filled (100 L)");
            yield return PH.Until(() => Mathf.Abs(bg.Level - 0.5f) < 0.03f && b.transform.Find("GaugeWater"), 8f);
            c.Check(Mathf.Abs(bg.MarkerHeight - bg.height * 0.5f) < 0.06f, $"the gauge glass shows half ({bg.MarkerHeight:0.00} of {bg.height:0.00} m)");
            yield return ScheduledScenarios1015.Shoot(c, g, b.transform.position + Vector3.up * 0.5f, side, 2f, "rain_gauge");
            Object.Destroy(b.gameObject);
        }
    }

    /// <summary>Working the well's hand pump gushes water from the spout with the strokes; it stops after them.</summary>
    class PumpGush : Scenario
    {
        public override string Id => "irrigation.pump_gush";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var p = PH.Piece(g, "well", me + fwd * 2.5f, -fwd);
            if (!c.Check(p, "a well")) yield break;
            var pump = p.GetComponent<HandPump>();
            yield return null; yield return null;
            pump.Use(g, false);
            c.Check(pump.LastStroke > 0f && pump.Gushing, "a stroke: water runs");
            yield return PH.Until(() => pump.GushDrops > 3, 1f);
            c.Check(pump.GushDrops > 3, $"it gushes from the spout ({pump.GushDrops} drops in the air)");
            var spout = p.transform.Find("Spout");
            c.Check(spout && spout.GetComponent<MadMax.World.Droplets>().First is Vector3 d && d.y < spout.position.y + 0.05f, "falling from the spout");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 0.9f, ScheduledScenarios1026Audit.Side(g), 1.8f, "pump_gush");
            yield return PH.Until(() => !pump.Gushing, 3f);
            c.Check(!pump.Gushing, "after the strokes it stops");
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>Over buried ore the detector's signal lamp flashes with its beeps (over nothing it stays dark).</summary>
    class DetectorLamp : Scenario
    {
        public override string Id => "mining.detector_lamp";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var tool = ToolLibrary.Create("tool_detector", g.propMaterial);
            if (!c.Check(tool is MetalDetectorTool, "a metal detector")) yield break;
            var det = (MetalDetectorTool)tool;
            tool.transform.position = g.Player.transform.position + Vector3.up;
            yield return null; yield return null;
            c.Check(tool.transform.Find("SignalLamp"), "a signal lamp on the control box");
            // the nearest buried ore within 300 m of the player
            var me = g.Player.transform.position;
            Vector3 best = Vector3.zero; float bs = 0f;
            for (int k = 0; k < 2400 && bs < 0.5f; k++)
            {
                float a = k * 2.39996f, r = 3f + k * 0.12f;
                var q = me + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                float s = g.World.OreAt(q.x, q.z, out _);
                if (s > bs) { bs = s; best = q; }
            }
            if (bs < 0.2f) { c.Block("no buried ore within 300 m of the start"); Object.Destroy(tool.gameObject); yield break; }
            var stand = best - g.Player.transform.forward * 1f;
            g.Player.Teleport(PH.Ground(stand) + Vector3.up * 0.1f, g.Player.transform.eulerAngles.y);
            c.Fixture($"player moved beside buried ore (strength {bs:0.00})");
            float peak = 0f;
            for (float t = 0f; t < 4f; t += Time.deltaTime) { peak = Mathf.Max(peak, det.LampLevel); yield return null; }
            c.Check(MetalDetectorTool.Reading > 0.05f, $"it reads the ore ({MetalDetectorTool.Reading:0.00})");
            c.Check(peak > 0.5f, $"the lamp flashes with the beeps (peak {peak:0.00})");
            Object.Destroy(tool.gameObject);
        }
    }
}
