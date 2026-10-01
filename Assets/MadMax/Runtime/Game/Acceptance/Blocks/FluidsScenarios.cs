using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the fluids block: hand containers siphon one fluid up to their capacity and pour
    /// it through the K / G radial, blends by volume run (or don't) by the mixing table, blends survive a save.</summary>
    public static class FluidsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new FluidsSiphonRefill();
            yield return new FluidsMixing();
            yield return new FluidsSave();
        }

        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        /// <summary>Diesel-engined vehicles of the fleet (no tankers), biggest tank first.</summary>
        internal static List<VehicleDriver> Diesels(WastelandGame g) =>
            g.AllVehicles.Where(v => v && v.driveable && !v.aiDriven && v.TryGetComponent<VehicleSystems>(out var s) && s.HasEngine && s.FuelKind == ResourceType.Diesel
                                     && !v.GetComponent<FuelTanker>() && !v.GetComponent<BoatModel>())
                         .OrderByDescending(v => v.GetComponent<VehicleSystems>().fuelCapacity).ToList();

        /// <summary>Stand the player 1 m off the vehicle's right flank (by its body mesh), facing it.</summary>
        internal static void Beside(WastelandGame g, VehicleDriver v)
        {
            var body = v.transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            float half = mf && mf.sharedMesh ? mf.sharedMesh.bounds.max.x * Mathf.Abs(body.lossyScale.x) : 1.2f;
            var p = v.transform.TransformPoint(new Vector3(half + 1f, 0f, -0.6f));
            p.y = DeformableTerrain.Instance.Height(p.x, p.z) + 0.05f;
            var look = v.transform.position - p; look.y = 0f;
            g.Player.Teleport(p, Quaternion.LookRotation(look).eulerAngles.y);
        }

        internal static FluidMix Mix(params (ResourceType t, float f)[] parts)
        {
            var m = new FluidMix();
            float have = 0f;
            foreach (var (t, f) in parts) { m.Blend(have, t, f); have += f; }
            return m;
        }

        internal static string Slices(WastelandGame g) => string.Join(" | ", g.RadialActions.Select(a => a.label + " (" + a.detail + ")"));
    }

    /// <summary>Equip a 20 L jerry can, siphon diesel from a truck through the K radial: exactly 20 L of one fluid; a
    /// full can offers nothing more; pour it into a diesel car through the G radial: the car's tank gains what the can
    /// loses and no litre is made or lost.</summary>
    class FluidsSiphonRefill : Scenario
    {
        public override string Id => "fluids.siphon_refill";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var diesels = FluidsScenarios.Diesels(g);
            if (diesels.Count < 2) { c.Block("needs two diesel vehicles in the fleet (found " + diesels.Count + ")"); yield break; }
            var truck = diesels[0];
            var car = diesels.FirstOrDefault(v => v != truck && !v.GetComponent<Machine>()) ?? diesels[1];
            var ts = truck.GetComponent<VehicleSystems>(); var cs = car.GetComponent<VehicleSystems>();
            string tn = truck.name.Replace("(Clone)", ""), cn = car.name.Replace("(Clone)", "");
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no level pad"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            WastelandGame.ExternalInput = true;
            g.Player.moveInput = Vector2.zero;

            // ---- the can in hand
            if (g.Inventory.GetItem(FluidContainers.JerryCan) <= 0) { g.Inventory.AddItem(FluidContainers.JerryCan); c.Fixture("granted a jerry can"); }
            var cans = g.CansOf(FluidContainers.JerryCan);
            if (!cans[0].Empty) { cans[0].Clear(); c.Fixture("emptied the jerry can"); }
            if (!(g.Player.Tool && g.Player.Tool.id == FluidContainers.JerryCan)) g.UseItem(FluidContainers.JerryCan);
            yield return null;
            if (!c.Check(g.Player.Tool is FluidCanTool && g.HeldCan != null && g.HeldCanDef.litres == 20f, "the 20 L jerry can is in hand: " + g.HeldCanText)) yield break;

            // ---- siphon from the truck
            var truckHome = truck.transform.position; var truckFwd = truck.transform.forward;
            g.Player.Teleport(pad + Vector3.right * 9f + Vector3.up * 0.3f, -90f);
            yield return TestWorld.Place(c, truck, pad, Vector3.forward, 1.5f);
            if (ts.fuel < 40f) ts.fuel = Mathf.Min(ts.fuelCapacity, 60f);
            ts.fuelMix.Set(ResourceType.Diesel);
            c.Fixture($"{tn}: {ts.fuel:0.0} L of pure diesel");
            FluidsScenarios.Beside(g, truck);
            yield return null; yield return null;
            c.Note("prompt: " + g.Prompt);
            float truck0 = ts.fuel;
            ActionPress.Press(Controls.Act.Siphon);
            yield return FluidsScenarios.Until(() => g.FluidChoiceOpen || g.Working, 2f);
            c.Note("K radial: " + FluidsScenarios.Slices(g));
            if (!c.Check(g.FluidChoiceOpen, "[K] opens the siphon radial")) yield break;
            var tank = g.RadialActions.FirstOrDefault(a => a.label.StartsWith("FUEL TANK"));
            c.Check(tank.label != null && tank.detail.Contains("DIESEL") && tank.detail.Contains("+20 L"), "the truck's fuel tank is offered: DIESEL +20 L (" + tank.detail + ")");
            c.Check(g.RadialActions.Any(a => a.label.StartsWith("ENGINE OIL")) && g.RadialActions.Any(a => a.label.StartsWith("COOLANT")), "an empty can may take the oil or the coolant instead");
            c.Check(g.PickRadial("FUEL TANK"), "picked the fuel tank");
            yield return FluidsScenarios.Until(() => !g.Working, 30f);
            var can = g.HeldCan;
            c.Note("after: " + g.LastTransferNote + "; work " + g.LastWork + " '" + g.LastWorkNote + "'");
            c.Metric("siphoned", can.litres, "L");
            c.Check(Mathf.Abs(can.litres - 20f) < 0.001f, $"the can holds exactly its 20 L ({can.litres:0.###} L)");
            c.Check(can.mix.IsPure && can.mix.Main == ResourceType.Diesel, "one fluid: " + can.mix.Label());
            c.Check(Mathf.Abs(truck0 - ts.fuel - 20f) < 0.01f, $"the truck lost the same 20 L ({truck0:0.00} -> {ts.fuel:0.00} L)");

            yield return null;
            ActionPress.Press(Controls.Act.Siphon);
            yield return null; yield return null;
            c.Check(!g.FluidChoiceOpen && !g.Working, "a full can offers nothing more");
            if (g.FluidChoiceOpen) g.CancelFluidChoice();

            // ---- pour into the car
            yield return TestWorld.Place(c, truck, truckHome, truckFwd, 0.5f);
            g.Player.Teleport(pad + Vector3.right * 9f + Vector3.up * 0.3f, -90f);
            yield return TestWorld.Place(c, car, pad, Vector3.forward, 1.5f);
            cs.fuel = Mathf.Min(10f, cs.fuelCapacity * 0.2f);
            cs.fuelMix.Set(ResourceType.Diesel);
            c.Fixture($"{cn}: {cs.fuel:0.0} of {cs.fuelCapacity:0} L of diesel");
            FluidsScenarios.Beside(g, car);
            yield return null; yield return null;
            float car0 = cs.fuel, total0 = ts.fuel + cs.fuel + can.litres;
            ActionPress.Press(Controls.Act.Service);
            yield return FluidsScenarios.Until(() => g.FluidChoiceOpen || g.Working, 2f);
            c.Note("G radial: " + FluidsScenarios.Slices(g));
            if (!c.Check(g.FluidChoiceOpen, "[G] opens the pour radial")) yield break;
            var into = g.RadialActions.FirstOrDefault(a => a.label.StartsWith("FUEL TANK"));
            c.Check(into.label != null && into.detail == "DIESEL", "the car's tank is offered and stays pure diesel: " + into.detail);
            c.Check(g.RadialActions.Any(a => a.label == "POUR OUT"), "pouring out on the ground is offered");
            c.Check(g.PickRadial("FUEL TANK"), "picked the car's fuel tank");
            yield return FluidsScenarios.Until(() => !g.Working, 30f);
            float poured = cs.fuel - car0;
            c.Metric("poured", poured, "L");
            c.Check(Mathf.Abs(poured - Mathf.Min(20f, cs.fuelCapacity - car0)) < 0.01f, $"the car's tank gains the can ({car0:0.00} -> {cs.fuel:0.00} L)");
            float total1 = ts.fuel + cs.fuel + can.litres;
            c.Check(Mathf.Abs(total1 - total0) < 0.01f, $"litres conserved ({total0:0.000} -> {total1:0.000} L)");
            c.Check(cs.fuelMix.IsPure && cs.fuelMix.Main == ResourceType.Diesel && !cs.WrongFuel, "the car holds diesel: " + cs.fuelMix.Label());
            if (can.Empty) c.Check(g.HeldCanText.Contains("EMPTY"), "the HUD shows the can empty: " + g.HeldCanText);
            g.Player.Equip(null);
        }
    }

    /// <summary>The mixing table, row by row (FuelBlend.Evaluate), then live in a diesel vehicle: 15 % petrol runs with
    /// a small power loss, 40 % stalls and won't restart, 8 % water runs; oil and coolant blends rate as documented.</summary>
    class FluidsMixing : Scenario
    {
        public override string Id => "fluids.mixing";
        public override float Timeout => 90f;

        struct Case { public string what; public EngineFuel engine; public FluidMix mix; public bool runs; public bool? rough; public float minPower, maxPower, minBurn, maxBurn; }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            const ResourceType P = ResourceType.Fuel, D = ResourceType.Diesel, E = ResourceType.Ethanol, W = ResourceType.Water, O = ResourceType.Oil;
            var cases = new[]
            {
                new Case { what = "diesel + 15 % petrol", engine = EngineFuel.Diesel, mix = FluidsScenarios.Mix((D, 85f), (P, 15f)), runs = true, rough = false, minPower = 0.9f, maxPower = 0.99f, minBurn = 0.99f, maxBurn = 1.01f },
                new Case { what = "diesel + 25 % petrol", engine = EngineFuel.Diesel, mix = FluidsScenarios.Mix((D, 75f), (P, 25f)), runs = true, rough = true, minPower = 0.5f, maxPower = 0.93f, minBurn = 0.99f, maxBurn = 1.01f },
                new Case { what = "diesel + 40 % petrol", engine = EngineFuel.Diesel, mix = FluidsScenarios.Mix((D, 60f), (P, 40f)), runs = false },
                new Case { what = "diesel + 8 % water", engine = EngineFuel.Diesel, mix = FluidsScenarios.Mix((D, 92f), (W, 8f)), runs = true, rough = false, minPower = 0.8f, maxPower = 0.95f, minBurn = 1.08f, maxBurn = 1.2f },
                new Case { what = "petrol + 8 % water", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((P, 92f), (W, 8f)), runs = true, rough = false, minPower = 0.8f, maxPower = 0.95f, minBurn = 1.08f, maxBurn = 1.2f },
                new Case { what = "petrol + 15 % water", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((P, 85f), (W, 15f)), runs = false },
                new Case { what = "E85 in petrol", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((E, 85f), (P, 15f)), runs = true, rough = false, minPower = 0.93f, maxPower = 0.99f, minBurn = 1.2f, maxBurn = 1.4f },
                new Case { what = "petrol + 5 % diesel", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((P, 95f), (D, 5f)), runs = true, rough = false, minPower = 0.9f, maxPower = 0.99f, minBurn = 0.99f, maxBurn = 1.01f },
                new Case { what = "petrol + 15 % diesel", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((P, 85f), (D, 15f)), runs = true, rough = true, minPower = 0.5f, maxPower = 0.9f, minBurn = 0.99f, maxBurn = 1.01f },
                new Case { what = "petrol + 30 % diesel", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((P, 70f), (D, 30f)), runs = false },
                new Case { what = "2-stroke mix in a two-stroke", engine = EngineFuel.TwoStroke, mix = FluidsScenarios.Mix((P, 96f), (O, 4f)), runs = true, rough = false, minPower = 0.99f, maxPower = 1.01f, minBurn = 0.99f, maxBurn = 1.01f },
                new Case { what = "2-stroke mix in a four-stroke", engine = EngineFuel.Petrol, mix = FluidsScenarios.Mix((P, 96f), (O, 4f)), runs = true, rough = false, minPower = 0.95f, maxPower = 0.99f, minBurn = 0.99f, maxBurn = 1.01f },
                new Case { what = "ethanol in diesel 30 %", engine = EngineFuel.Diesel, mix = FluidsScenarios.Mix((D, 70f), (E, 30f)), runs = false },
                new Case { what = "diesel + 15 % petrol + 8 % water", engine = EngineFuel.Diesel, mix = FluidsScenarios.Mix((D, 77f), (P, 15f), (W, 8f)), runs = false },
            };
            c.Fixture("blends built directly for the table rows");
            foreach (var k in cases)
            {
                var e = FuelBlend.Evaluate(k.mix, k.engine);
                string got = $"{k.what} ({k.mix.Label()}, {k.engine}): runs {e.runs} rough {e.rough} power {e.power:0.000} burn {e.burn:0.000} misfire {e.misfire:0.00} smoke {e.smoke:0.00}";
                bool ok = e.runs == k.runs && (!k.rough.HasValue || e.rough == k.rough.Value)
                          && (!k.runs || (e.power >= k.minPower && e.power <= k.maxPower && e.burn >= k.minBurn && e.burn <= k.maxBurn));
                c.Check(ok, got);
            }
            var two = FuelBlend.Evaluate(cases[10].mix, EngineFuel.TwoStroke); var four = FuelBlend.Evaluate(cases[11].mix, EngineFuel.Petrol);
            c.Check(four.smoke > two.smoke + 0.1f, $"2-stroke mix smokes in a four-stroke ({four.smoke:0.00}) more than in a two-stroke ({two.smoke:0.00})");
            c.Check(FuelBlend.OilProtection(new FluidMix(ResourceType.Oil)) > 0.99f && FuelBlend.OilProtection(FluidsScenarios.Mix((O, 90f), (W, 10f))) < 0.8f,
                "clean oil protects; 10 % water in the sump does not (" + FuelBlend.OilProtection(FluidsScenarios.Mix((O, 90f), (W, 10f))).ToString("0.00") + ")");
            float fp100 = FuelBlend.FreezePoint(new FluidMix(ResourceType.Coolant)), fp50 = FuelBlend.FreezePoint(FluidsScenarios.Mix((ResourceType.Coolant, 50f), (W, 50f))), fp0 = FuelBlend.FreezePoint(new FluidMix(W));
            c.Check(fp100 < -30f && fp50 > fp100 && fp50 < -10f && Mathf.Abs(fp0) < 0.01f, $"coolant freezing points: ready mix {fp100:0} C, half water {fp50:0} C, water {fp0:0} C");

            // ---- live: a diesel vehicle
            var diesels = FluidsScenarios.Diesels(g);
            var v = diesels.FirstOrDefault(x => !x.GetComponent<Machine>()) ?? diesels.FirstOrDefault();
            if (!v) { c.Note("no diesel vehicle: live checks skipped"); yield break; }
            var sys = v.GetComponent<VehicleSystems>();
            string vn = v.name.Replace("(Clone)", "");
            if (!TestWorld.Pad(8f, out var pad)) { c.Note("no level pad: live checks skipped"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            WastelandGame.ExternalInput = true;
            g.Player.Teleport(pad + Vector3.right * 8f + Vector3.up * 0.3f, -90f);
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1.5f);
            var ep = v.Engine ? v.Engine.GetComponent<VehiclePart>() : null;
            if (ep && ep.damage > 0f) { ep.damage = 0f; c.Fixture("engine repaired"); }
            sys.fuel = Mathf.Min(sys.fuelCapacity, 40f); sys.fuelMix.Set(ResourceType.Diesel);
            sys.oil = sys.oilCapacity; sys.coolant = sys.coolantCapacity; sys.oilMix.Set(ResourceType.Oil); sys.coolantMix.Set(ResourceType.Coolant);
            sys.plugs = sys.airFilter = sys.oilLife = 1f;
            c.Fixture($"{vn}: 40 L pure diesel, full fresh oil and coolant, new filters");
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            float clean = 0f; yield return Power(v, sys, 1.5f, x => clean = x);
            c.Metric("power_clean", clean, "");

            sys.fuelMix.CopyFrom(FluidsScenarios.Mix((ResourceType.Diesel, 85f), (ResourceType.Fuel, 15f)));
            c.Fixture("tank blend set to diesel 85 % petrol 15 %");
            float p15 = 0f; yield return Power(v, sys, 2f, x => p15 = x);
            c.Metric("power_15pct_petrol", p15, "");
            c.Check(sys.Started && !sys.WrongFuel, "15 % petrol in diesel keeps running");
            c.Check(p15 < clean - 0.01f && p15 > clean * 0.85f, $"with a small power loss ({clean:0.000} -> {p15:0.000})");

            sys.fuelMix.CopyFrom(FluidsScenarios.Mix((ResourceType.Diesel, 60f), (ResourceType.Fuel, 40f)));
            c.Fixture("tank blend set to diesel 60 % petrol 40 %");
            yield return FluidsScenarios.Until(() => !sys.Started, 3f);
            c.Check(!sys.Started && (sys.Faults & Fault.WrongFuel) != 0, "40 % petrol stalls the diesel: " + sys.FaultText());
            float t0 = Time.time; bool caught = false;
            while (Time.time - t0 < 4f) { v.handbrake = true; v.throttleInput = 0.4f; if (sys.Started) caught = true; yield return new WaitForSeconds(0.25f); if (!sys.Cranking) { v.throttleInput = 0f; yield return new WaitForSeconds(0.3f); } }
            v.throttleInput = 0f;
            c.Check(!caught, "and won't restart on it");

            sys.fuelMix.CopyFrom(FluidsScenarios.Mix((ResourceType.Diesel, 92f), (ResourceType.Water, 8f)));
            if (ep) ep.damage = 0f;
            c.Fixture("tank blend set to diesel 92 % water 8 % (engine damage from the cranks reset)");
            yield return TestWorld.StartEngine(c, v);
            float pw = 0f; yield return Power(v, sys, 2f, x => pw = x);
            c.Metric("power_8pct_water", pw, "");
            c.Check(sys.Started && pw > 0.5f && pw < clean - 0.02f, $"8 % water runs, weaker ({pw:0.000})");
            c.Check(sys.Blend.burn > 1.05f, $"and burns more fuel for the work (x{sys.Blend.burn:0.00})");
            sys.fuelMix.Set(ResourceType.Diesel);
            v.throttleInput = 0f; v.handbrake = true;
            g.Exit(); yield return null;
        }

        /// <summary>Average PowerFactor over <paramref name="seconds"/> with the throttle held against the handbrake.</summary>
        static IEnumerator Power(VehicleDriver v, VehicleSystems sys, float seconds, System.Action<float> result)
        {
            float t0 = Time.time, sum = 0f; int n = 0;
            while (Time.time - t0 < seconds)
            {
                v.handbrake = true; v.throttleInput = 0.3f;
                yield return new WaitForFixedUpdate();
                sum += sys.PowerFactor; n++;
            }
            v.throttleInput = 0f;
            result(n > 0 ? sum / n : 0f);
        }
    }

    /// <summary>Blends in a vehicle's tank, sump and radiator and in a jerry can in the pack go into the save and come
    /// back (JSON round trip; a slot reload with an isolated profile); an old save line without blends loads as the pure
    /// tank kind.</summary>
    class FluidsSave : Scenario
    {
        public override string Id => "fluids.save";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = g.AllVehicles.FirstOrDefault(x => x && x.driveable && !x.aiDriven && x.TryGetComponent<VehicleSystems>(out var s) && s.HasEngine && s.usesCoolant && !s.oilInFuel && !x.GetComponent<BoatModel>());
            if (!v) { c.Block("no vehicle with oil and coolant"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            var sys = v.GetComponent<VehicleSystems>();
            sys.fuel = Mathf.Min(sys.fuelCapacity, 30f);
            sys.fuelMix.CopyFrom(FluidsScenarios.Mix((ResourceType.Diesel, 85f), (ResourceType.Fuel, 15f)));
            sys.oilMix.CopyFrom(FluidsScenarios.Mix((ResourceType.Oil, 90f), (ResourceType.Water, 10f)));
            sys.coolantMix.CopyFrom(FluidsScenarios.Mix((ResourceType.Coolant, 60f), (ResourceType.Water, 40f)));
            if (g.Inventory.GetItem(FluidContainers.JerryCan) <= 0) g.Inventory.AddItem(FluidContainers.JerryCan);
            var can = g.CansOf(FluidContainers.JerryCan)[0];
            can.mix.CopyFrom(FluidsScenarios.Mix((ResourceType.Ethanol, 85f), (ResourceType.Fuel, 15f))); can.litres = 12.5f;
            c.Fixture("blends set directly: tank diesel 85/petrol 15, sump oil 90/water 10, coolant 60/water 40, jerry can 12.5 L E85");
            string fuel0 = sys.fuelMix.Save(), oil0 = sys.oilMix.Save(), cool0 = sys.coolantMix.Save(), canMix0 = can.mix.Save();
            ushort id = v.netId;

            var d = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.CaptureSave()));
            var vs = d.vehicles.FirstOrDefault(x => x.netId == id);
            c.Check(vs != null && !string.IsNullOrEmpty(vs.fluids), "the vehicle's save line carries its blends: " + (vs != null ? vs.fluids : "vehicle missing"));
            c.Check(d.blockFluids != null && d.blockFluids.Any(l => l.Contains(FluidContainers.JerryCan) && l.Contains("12.5")), "the save carries the jerry can's 12.5 L");
            if (vs != null)
            {
                var probe = new FluidMix(); var oilProbe = new FluidMix();
                foreach (var part in vs.fluids.Split('|')) { if (part.StartsWith("f=")) probe.Load(part.Substring(2)); if (part.StartsWith("o=")) oilProbe.Load(part.Substring(2)); }
                c.Check(probe.Like(sys.fuelMix, 0.001f) && oilProbe.Like(sys.oilMix, 0.001f), "the saved blends read back the same: " + probe.Label() + " / " + oilProbe.Label());
            }
            // an old save line: no blends, tank kind only
            var old = new FluidMix(); old.CopyFrom(sys.fuelMix);
            sys.fuelMix.Clear(); sys.LoadFluidState(null); sys.tankKind = ResourceType.Diesel;
            c.Check(sys.fuelMix.IsPure && sys.fuelMix.Main == ResourceType.Diesel, "an old save (no blends, tank DIESEL) loads as pure diesel");
            sys.fuelMix.CopyFrom(old);

            if (!Profile.Isolated) { c.Note("not an isolated profile (-profiledir): reload round trip skipped"); yield break; }
            g.SaveGame(3);
            var before = g;
            g.LoadGame(3);
            while ((WastelandGame.Instance == before || !WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - c.startedAt < Timeout - 20f) yield return null;
            g = WastelandGame.Instance;
            if (!c.Check(g && g != before && g.Ready, "slot 3 loads")) yield break;
            WastelandGame.ExternalInput = true;
            yield return new WaitForSeconds(1f);
            var v2 = g.AllVehicles.FirstOrDefault(x => x && x.netId == id);
            if (!c.Check(v2, "the vehicle is back")) yield break;
            var s2 = v2.GetComponent<VehicleSystems>();
            c.Check(s2.fuelMix.Save() == fuel0, "tank blend back: " + s2.fuelMix.Label());
            c.Check(s2.oilMix.Save() == oil0, "sump blend back: " + s2.oilMix.Label());
            c.Check(s2.coolantMix.Save() == cool0, "coolant blend back: " + s2.coolantMix.Label());
            var can2 = g.CansOf(FluidContainers.JerryCan);
            c.Check(can2.Count > 0 && Mathf.Abs(can2[0].litres - 12.5f) < 0.001f && can2[0].mix.Save() == canMix0, "the jerry can holds its 12.5 L of " + (can2.Count > 0 ? can2[0].mix.Label() : "?"));
        }
    }
}
