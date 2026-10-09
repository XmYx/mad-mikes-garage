using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-09: the COLD lamp and diesel glow plugs, companions who learn to dress wounds,
    /// the broken-leg recovery arc, walkie-talkie orders to the crew, the column's cold-morning heater.</summary>
    public static class ScheduledScenarios1009
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ColdStartLamp();
            yield return new CompanionKnack();
            yield return new LegRecovery();
            yield return new RadioOrders();
            yield return new MorningHeater();
        }

        /// <summary>Recruit a deterministic wanderer at <paramref name="at"/> (skilled with wounds or not).</summary>
        internal static MadMax.Npc.Npc Recruit(WastelandGame g, bool skilled, Vector3 at, int skip = 0)
        {
            NpcProfile p = null;
            for (int s = 1, found = 0; s < 4000 && p == null; s++)
            {
                var q = NpcProfile.Make("test:crew" + s, NpcRole.Wanderer, s);
                if (Companions.Patches(q) == skilled && found++ == skip) p = q;
            }
            if (p == null) return null;
            var n = MadMax.Npc.Npc.Spawn(p, at, 0f, null, g.propMaterial);
            Companions.Recruit(g, n);
            n.EnsurePack();
            return n;
        }

        internal static void Drop(WastelandGame g, MadMax.Npc.Npc n)
        {
            if (!n) return;
            if (n.Driving) n.LeaveWheel();
            Companions.Dismiss(g, n);
            if (n) Object.Destroy(n.gameObject);
        }
    }

    /// <summary>A cold stopped engine lights the COLD lamp (steady on petrol); a diesel's glow plugs blink it while
    /// the driver sits in, cranking then is held back with a toast, and once the lamp goes out the start chance is
    /// better than before the glow; a block heater clears the lamp.</summary>
    class ColdStartLamp : Scenario
    {
        public override string Id => "vehicle.cold_start_lamp";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no open pad"); yield break; }
            float base0 = Weather.BaseTemperature, offset = Weather.Temperature - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, -8f - offset);
            var petrol = g.SpawnAiVehicle("Coupe", pad + Vector3.up * 0.5f, Quaternion.identity);
            var diesel = g.SpawnAiVehicle("Peugeot406Break", pad + new Vector3(6f, 0.5f, 0f), Quaternion.identity);
            if (!c.Check(petrol && diesel, "a petrol coupe and a diesel estate")) { Weather.Restore(false, 0f, 0f, base0); yield break; }
            var ps = petrol.GetComponent<VehicleSystems>(); var ds = diesel.GetComponent<VehicleSystems>();
            foreach (var (v, s) in new[] { (petrol, ps), (diesel, ds) })
            {
                v.aiDriven = false; v.Occupied = false; v.handbrake = true;
                s.Stop(); s.SetTemperature(Weather.Temperature); s.fuel = Mathf.Max(s.fuel, 10f);
            }
            c.Fixture($"air {Weather.Temperature:0} C, both engines stone cold, nobody inside");
            c.Check(ds.FuelKind == ResourceType.Diesel && ps.FuelKind != ResourceType.Diesel, "the estate burns diesel, the coupe petrol");
            c.Check(ps.ColdStart && VehicleDashboard.ColdLamp(ps).r > 200, "petrol: COLD lamp lit steady");
            c.Check(ds.GlowPlugsLit, "diesel: the glow plugs have not warmed (nobody in the seat)");
            float before = ds.StartChance;
            diesel.Occupied = true;                                                                  // someone sits in: the plugs warm
            diesel.throttleInput = 1f;
            yield return new WaitForSeconds(0.5f);
            c.Check(!ds.Cranking && !ds.Started && (g.ToastText ?? "").Contains("GLOW"), "cranking while the lamp is lit is held back: " + g.ToastText);
            diesel.throttleInput = 0f;
            int lit = 0;
            for (int i = 0; i < 12; i++) { if (VehicleDashboard.ColdLamp(ds).r > 200) lit++; yield return new WaitForSeconds(0.1f); }
            c.Check(lit > 2 && lit < 12, $"the lamp blinks while the plugs warm ({lit}/12 samples lit)");
            float wait = VehicleSystems.GlowFor(Weather.Temperature);
            c.Metric("glow_seconds", wait, "s");
            yield return new WaitForSeconds(wait);
            c.Check(ds.GlowReady && !ds.GlowPlugsLit && VehicleDashboard.ColdLamp(ds).r < 100, $"after {wait:0.0} s the lamp goes out (glow {ds.GlowProgress:0.00})");
            c.Check(ds.StartChance > before + 0.2f, $"start chance with hot plugs {ds.StartChance:0.00} vs {before:0.00} before");
            ps.KeepWarm(0.1f);
            c.Check(!ps.ColdStart && VehicleDashboard.ColdLamp(ps).r < 100, "a block heater clears the COLD lamp");
            c.Screenshot("cold_start_lamp");
            yield return null;
            diesel.Occupied = false;
            Weather.Restore(false, 0f, 0f, base0);
            Object.Destroy(petrol.gameObject); Object.Destroy(diesel.gameObject);
        }
    }

    /// <summary>A companion without the knack dresses roughly until they have done it <see cref="Companions.KnackAfter"/>
    /// times (then properly, said in a toast); another one handed FIELD FIRST AID dresses properly at once; the count
    /// lives in the saved NPC state.</summary>
    class CompanionKnack : Scenario
    {
        public override string Id => "npc.companion_knack";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var spot = g.Player.transform.position + g.Player.transform.forward * 2f;
            var a = ScheduledScenarios1009.Recruit(g, false, spot);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(a && a.pack, "an untrained companion with a pack")) yield break;
            c.Check(!Companions.Patches(a.Profile), "starts without the knack");
            bool learned = false;
            for (int i = 0; i < Companions.KnackAfter; i++)
            {
                a.pack.inventory.AddItem(Companions.FirstAidKit);
                g.Stats.injuries.Clear(); g.Stats.injuries.Add(new Injury { zone = BodyZone.ArmL, type = Wound.Laceration, severity = 0.8f });
                g.CompanionTreat(a, null);
                c.Check(g.Stats.injuries[0].rough, $"dressing {i + 1} is rough");
                if (i == Companions.KnackAfter - 1) learned = (g.ToastText ?? "").Contains("KNACK");
            }
            c.Check(learned && Companions.Patches(a.Profile), $"after {Companions.KnackAfter} dressings they have the knack: " + g.ToastText);
            c.Check(NpcRegistry.Get(a.Profile).tended >= Companions.KnackAfter, "the count is in the saved NPC state");
            a.pack.inventory.AddItem(Companions.FirstAidKit);
            g.Stats.injuries.Clear(); g.Stats.injuries.Add(new Injury { zone = BodyZone.ArmL, type = Wound.Laceration, severity = 0.8f });
            g.CompanionTreat(a, null);
            c.Check(!g.Stats.injuries[0].rough, "the next dressing is proper");

            var b = ScheduledScenarios1009.Recruit(g, false, spot + g.Player.transform.right * 2f, 1);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(b && b.Profile.id != a.Profile.id, "a second untrained companion")) { ScheduledScenarios1009.Drop(g, a); yield break; }
            c.Check(MediaLibrary.Get(Companions.FirstAidBook) != null, "FIELD FIRST AID is a book");
            int had = g.Inventory.GetItem(Companions.FirstAidBook);
            if (had > 0) g.Inventory.TakeItem(Companions.FirstAidBook, had);
            c.Check(!Companions.Study(b.Profile, g.Inventory), "no book in the pack: nothing to hand over");
            g.Inventory.AddItem(Companions.FirstAidBook);
            c.Check(Companions.Study(b.Profile, g.Inventory) && Companions.Patches(b.Profile) && g.Inventory.GetItem(Companions.FirstAidBook) == 0, "handed the book, they read it: proper dressings, the book is theirs");
            g.Stats.injuries.Clear();
            ScheduledScenarios1009.Drop(g, a); ScheduledScenarios1009.Drop(g, b);
        }
    }

    /// <summary>A splinted broken leg knits faster resting (in a car) and on a crutch than walked on; nights in a bed
    /// knit it further; a broken left leg takes the clutch away until it is mostly healed.</summary>
    class LegRecovery : Scenario
    {
        public override string Id => "body.leg_recovery";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var leg = new Injury { zone = BodyZone.LegL, type = Wound.Fracture, severity = 0.9f, splinted = true };
            g.Stats.injuries.Clear(); g.Stats.injuries.Add(leg);
            c.Check(leg.BrokenLeg && g.LeftLegBroken && !g.HasClutchFoot, "a broken left leg: no clutch foot");
            bool ext = WastelandGame.ExternalInput;
            WastelandGame.ExternalInput = true;
            g.Player.moveInput = Vector2.zero;
            float still = g.RecoveryPace;
            g.Player.moveInput = Vector2.up;
            float walking = g.RecoveryPace;
            c.Check(Mathf.Approximately(still, 1f) && Mathf.Approximately(walking, WastelandGame.WalkOnBreak), $"standing {still:0.00}, walking on it {walking:0.00}");
            if (g.Inventory.GetItem(SafetyTools.Crutch) == 0) g.Inventory.AddItem(SafetyTools.Crutch);
            g.UseItem(SafetyTools.Crutch);
            yield return new WaitForSeconds(0.3f);
            g.Player.moveInput = Vector2.up;
            c.Check(g.OnCrutch && Mathf.Approximately(g.RecoveryPace, WastelandGame.CrutchPace), $"on the crutch {g.RecoveryPace:0.00} (crutch held: {g.OnCrutch})");
            g.Player.moveInput = Vector2.zero;
            WastelandGame.ExternalInput = ext;
            var car = ScheduledScenarios1007.FleetCar(g);
            if (car)
            {
                g.Enter(car);
                yield return new WaitForSeconds(0.5f);
                c.Check(Mathf.Approximately(g.RecoveryPace, WastelandGame.RestPace), $"driving rests it ({g.RecoveryPace:0.00})");
                g.Exit();
                yield return new WaitForSeconds(0.5f);
            }
            float s0 = leg.severity;
            leg.pace = WastelandGame.WalkOnBreak; leg.Tick(60f, 100f, 1f); float dWalk = s0 - leg.severity;
            s0 = leg.severity; leg.pace = WastelandGame.RestPace; leg.Tick(60f, 100f, 1f); float dRest = s0 - leg.severity;
            c.Check(dRest > dWalk * 2.5f, $"a minute rested knits {dRest / Mathf.Max(1e-6f, dWalk):0.0}x a minute walked on");
            leg.severity = 0.9f;
            g.HealWhileAsleep(10f, 6f);
            c.Check(leg.severity < 0.75f, $"a night in a good bed knits it (0.90 → {leg.severity:0.00})");
            for (int i = 0; i < 4 && g.LeftLegBroken; i++) g.HealWhileAsleep(10f, 6f);
            c.Check(!g.LeftLegBroken && g.HasClutchFoot, $"a few nights later the clutch is back (severity {(g.Stats.injuries.Contains(leg) ? leg.severity : 0f):0.00})");
            var arm = new Injury { zone = BodyZone.ArmR, type = Wound.Fracture, severity = 0.9f, splinted = true };
            g.Stats.injuries.Clear(); g.Stats.injuries.Add(arm);
            c.Check(!arm.BrokenLeg && g.HasClutchFoot, "a broken arm keeps the clutch");
            g.Stats.injuries.Clear();
        }
    }

    /// <summary>With a handheld radio in the pack, companions who carry one take COME / HOLD / BRING A CAR from the
    /// context menu at any distance; without a radio on their side there is only static.</summary>
    class RadioOrders : Scenario
    {
        public override string Id => "npc.radio_orders";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var car = ScheduledScenarios1007.FleetCar(g);
            if (!car) { c.Block("no car in the fleet"); yield break; }
            var spot = car.transform.position + car.transform.right * 4f;
            var t = DeformableTerrain.Instance; spot.y = t.Height(spot.x, spot.z) + 0.1f;
            var n = ScheduledScenarios1009.Recruit(g, false, spot);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(n && n.pack, "a companion beside the fleet car")) yield break;
            if (g.Inventory.GetItem(SafetyTools.HandRadio) == 0) g.Inventory.AddItem(SafetyTools.HandRadio);
            int theirs = n.pack.inventory.GetItem(SafetyTools.HandRadio);
            if (theirs > 0) n.pack.inventory.TakeItem(SafetyTools.HandRadio, theirs);
            c.Check(Companions.RadioOrder(g, Companions.Order.Hold) == 0 && (g.ToastText ?? "").Contains("STATIC"), "no radio on their side: static");
            var opts = g.ContextOptionsFor(n);
            c.Check(!opts.Exists(o => o.label.StartsWith("RADIO THE CREW")), "no crew orders in the context menu");
            n.pack.inventory.AddItem(SafetyTools.HandRadio);
            opts = g.ContextOptionsFor(n);
            c.Check(opts.Count(o => o.label.StartsWith("RADIO THE CREW")) == 3, "context menu: " + string.Join(", ", opts.Where(o => o.label.StartsWith("RADIO")).Select(o => o.label)));
            c.Check(Companions.RadioOrder(g, Companions.Order.Hold) == 1 && n.order == 1 && (n.home - n.transform.position).magnitude < 0.5f, "HOLD: they stay where they are: " + g.ToastText);
            var far = car.transform.position + car.transform.forward * 90f;
            far.y = t.Height(far.x, far.z);
            g.Player.Teleport(far + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(1f);
            float apart = (g.Player.transform.position - n.transform.position).magnitude;
            c.Fixture($"the player {apart:0} m from the companion");
            c.Check(apart > 60f && (n.transform.position - spot).magnitude < 3f, "holding, they stay by the car while the player is away");
            var pick = Companions.CarFor(g, n);
            c.Check(pick, "a spare fleet car is near them: " + WastelandGame.Name(pick));
            if (pick) car = pick;
            c.Check(Companions.RadioOrder(g, Companions.Order.BringCar) == 1 && n.Driving && car.aiDriven, "BRING A CAR from " + apart.ToString("0") + " m: they take the wheel: " + g.ToastText);
            float d0 = (car.transform.position - g.Player.transform.position).magnitude;
            yield return new WaitForSeconds(6f);
            float d1 = (car.transform.position - g.Player.transform.position).magnitude;
            c.Metric("car_closed_in_6s", d0 - d1, "m");
            c.Check(d1 < d0 - 3f, $"the car comes towards the player ({d0:0} → {d1:0} m)");
            c.Check(Companions.RadioOrder(g, Companions.Order.Hold) == 1 && !n.Driving && !car.aiDriven, "HOLD again: they park and get out");
            c.Check(Companions.RadioOrder(g, Companions.Order.Come) == 1 && n.order == 0, "COME: they follow again: " + g.ToastText);
            ScheduledScenarios1009.Drop(g, n);
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>On a cold dawn a companion with a trip heater lights it under the fleet car nearest the player: a litre
    /// from its tank, the engine warms and the COLD lamp goes out; once a day.</summary>
    class MorningHeater : Scenario
    {
        public override string Id => "npc.morning_heater";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var car = ScheduledScenarios1007.FleetCar(g);
            if (!car) { c.Block("no car in the fleet"); yield break; }
            var sys = car.GetComponent<VehicleSystems>();
            float base0 = Weather.BaseTemperature, offset = Weather.Temperature - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, -6f - offset);
            sys.Stop(); sys.SetTemperature(Weather.Temperature); sys.fuel = Mathf.Max(sys.fuel, 12f);
            var spot = car.transform.position + car.transform.right * 3.5f;
            var t = DeformableTerrain.Instance; spot.y = t.Height(spot.x, spot.z) + 0.1f;
            g.Player.Teleport(spot + car.transform.forward * 3f, 0f);
            var n = ScheduledScenarios1009.Recruit(g, false, spot);
            yield return new WaitForSeconds(0.4f);
            if (!c.Check(n && n.pack, "a companion beside the cold car")) { Weather.Restore(false, 0f, 0f, base0); yield break; }
            c.Fixture($"air {Weather.Temperature:0} C, {WastelandGame.Name(car)} stopped and cold, {sys.fuel:0.0} L in the tank");
            c.Check(sys.ColdStart, "the COLD lamp is lit");
            c.Check(!Companions.LightHeaterNow(g), "no heater in their pack: nothing happens");
            n.pack.inventory.AddItem(WastelandGame.TripHeater);
            float fuel0 = sys.fuel;
            c.Check(Companions.LightHeaterNow(g) && Companions.LastHeaterLit == n && Companions.Warming(sys), "they light it under the car: " + g.ToastText);
            c.Check(Mathf.Abs(sys.fuel - (fuel0 - WastelandGame.TripHeatLitres)) < 0.01f, "a litre from the tank");
            float temp0 = sys.Temperature;
            yield return new WaitForSeconds(3f);
            c.Check(sys.BlockWarm && !sys.ColdStart && sys.Temperature > temp0 + 1f, $"the engine warms ({temp0:0.0} → {sys.Temperature:0.0} C), the lamp is out");
            Companions.LastHeaterLit = null;
            float h0 = DayNight.Hours;
            DayNight.SetHours(6.5f);
            yield return new WaitForSeconds(0.5f);
            c.Check(Companions.LastHeaterLit == null, "once a day: the dawn tick doesn't light it again");
            DayNight.SetHours(h0);
            Weather.Restore(false, 0f, 0f, base0);
            ScheduledScenarios1009.Drop(g, n);
        }
    }
}
