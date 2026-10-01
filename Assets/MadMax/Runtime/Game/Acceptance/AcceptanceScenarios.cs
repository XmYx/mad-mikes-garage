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
    /// <summary>The scenario registry: data checks, one drive test per registered land vehicle (generated from the
    /// game's prefab lists, so new vehicles are covered automatically), and regressions for fixed defects.</summary>
    public static class AcceptanceScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new RecipeCatalogue();
            yield return new VehicleCatalogue();
            yield return new ManifestContract();
            foreach (var h in HarnessScenarios.All()) yield return h;                  // suite "harness" only
            var g = WastelandGame.Instance;
            var names = new List<string>();
            if (g != null && g.vehiclePrefabs != null) foreach (var p in g.vehiclePrefabs) if (p) names.Add(p.name);
            else if (g == null) names.AddRange(MadMax.Designs.VehicleDesigns.ModelCarNames);
            foreach (var n in names) if (!MobilityScenarios.OwnSuite(n)) yield return new VehicleDrive(n);   // bikes, aircraft: mobility.*
            yield return new MachineSoilRoundTrip();
            yield return new HillHold("Sedan", 12f, 17f);
            yield return new CarsSeparate();
            yield return new CraftFuelRefund();
            yield return new SaveRoundTrip();
            yield return new GlassAndLamps();
            yield return new DeformationSetting();
            yield return new ExhaustSmoke();
            yield return new StartChance();
            yield return new CookingLadder();
            yield return new FarmingLadder();
            yield return new ScrapeMarks();
            yield return new SunAndMoon();
            yield return new CrawlerMud("Bulldozer");
            yield return new CrawlerMud("Excavator");
            yield return new Boarding();
            foreach (var s in StoryScenarios.All()) yield return s;
            foreach (var s in ItemsScenarios.All()) yield return s;
            foreach (var s in AnimScenarios.All()) yield return s;
            foreach (var s in RoadsScenarios.All()) yield return s;
            foreach (var s in MetalScenarios.All()) yield return s;
            foreach (var s in HusbandryScenarios.All()) yield return s;
            foreach (var s in UtilitiesScenarios.All()) yield return s;
            foreach (var s in MedMineScenarios.All()) yield return s;
            foreach (var s in DefenceScenarios.All()) yield return s;
            foreach (var s in OnlineScenarios.All()) yield return s;
            foreach (var s in SeasonsScenarios.All()) yield return s;
            foreach (var s in PeopleScenarios.All()) yield return s;
            foreach (var s in MobilityScenarios.All()) yield return s;
            foreach (var s in SurvivalScenarios.All()) yield return s;
        }

        public static IEnumerable<string> Ids() => All().Select(s => s.Id);
    }

    // ================================================================== Q0 catalogue checks (no world needed)

    /// <summary>Every recipe resolves (output, station, inputs) and every knowledge gate has an attainable source
    /// (blueprint in loot or trade, a book/tape that teaches it, or research).</summary>
    class RecipeCatalogue : Scenario
    {
        public override string Id => "catalogue.recipes";
        public override string[] Suites => new[] { "fast", "full", "catalogue" };
        public override bool NeedsWorld => false;

        public override IEnumerator Run(ScenarioContext c)
        {
            while (!WastelandGame.Instance) yield return null;
            var g = WastelandGame.Instance;
            var parts = new HashSet<string>((g.partPrefabs ?? new GameObject[0]).Where(p => p).Select(p => p.name));
            var vehicles = new HashSet<string>(g.vehiclePrefabs.Concat(g.boatPrefabs ?? new GameObject[0]).Concat(g.trailerPrefabs ?? new GameObject[0]).Where(p => p).Select(p => p.name));
            // station types come from each piece's setup: run it on an inactive scratch object and read the station back
            var stations = new HashSet<string> { "hand" };
            var scratch = new GameObject("CatalogueScratch");
            scratch.SetActive(false);
            foreach (var d in FurnitureLibrary.All)
            {
                if (d.setup == null) continue;
                var go = new GameObject(d.id);
                go.transform.SetParent(scratch.transform, false);
                try { d.setup(go); foreach (var st in go.GetComponents<CraftingStation>()) stations.Add(st.type); }
                catch (System.Exception e) { c.Note("setup of " + d.id + " needs a live piece: " + e.GetType().Name); }
            }
            Object.Destroy(scratch);
            c.Metric("station_types", stations.Count - 1, "");

            var dropped = new HashSet<string>(LootTables.AllIds().Concat(Trade.AllStockIds()));
            var crafted = new HashSet<string>(RecipeLibrary.All.Where(r => r.kind == OutputKind.Item).Select(r => r.output));
            var taught = new HashSet<string>();
            foreach (var m in MediaLibrary.All) if (dropped.Contains(m.id) || crafted.Contains(m.id)) foreach (var k in m.teaches) taught.Add(k);
            foreach (var r in MediaLibrary.Research) taught.Add(r.grants);

            int bad = 0, gates = 0, unsourced = 0;
            foreach (var r in RecipeLibrary.All)
            {
                string why = null;
                switch (r.kind)
                {
                    case OutputKind.Part: if (!parts.Contains(r.output)) why = "part " + r.output + " has no prefab"; break;
                    case OutputKind.Vehicle: if (!vehicles.Contains(r.output)) why = "vehicle " + r.output + " has no prefab"; break;
                    case OutputKind.Resource: if (r.outputResource == ResourceType.None) why = "resource output is None"; break;
                    case OutputKind.Item: if (string.IsNullOrEmpty(r.output)) why = "empty item output"; break;
                }
                if (why == null && !string.IsNullOrEmpty(r.station) && !stations.Contains(r.station)) why = "station '" + r.station + "' is not a buildable piece";
                if (why != null) { bad++; c.Check(false, r.id + ": " + why); continue; }

                var k = RecipeLibrary.KnowledgeFor(r);
                if (string.IsNullOrEmpty(k)) continue;
                gates++;
                bool ok = taught.Contains(k) || (k.StartsWith("bp_") && (dropped.Contains(k) || crafted.Contains(k))) || (k.StartsWith("read_") && (dropped.Contains(k.Substring(5)) || crafted.Contains(k.Substring(5))));
                if (!ok) { unsourced++; c.Check(false, r.id + ": knowledge '" + k + "' has no loot, trade, media or research source"); }
            }
            c.Metric("recipes", RecipeLibrary.All.Count, "");
            c.Metric("knowledge_gates", gates, "");
            c.Metric("broken_recipes", bad, "");
            c.Metric("unsourced_gates", unsourced, "");
            c.Check(bad == 0 && unsourced == 0, "every recipe resolves and every gate is attainable");
        }
    }

    /// <summary>Every registered vehicle prefab is complete: chassis, body, engine and wheels where it drives.</summary>
    class VehicleCatalogue : Scenario
    {
        public override string Id => "catalogue.vehicles";
        public override string[] Suites => new[] { "fast", "full", "catalogue" };
        public override bool NeedsWorld => false;

        public override IEnumerator Run(ScenarioContext c)
        {
            while (!WastelandGame.Instance) yield return null;
            var g = WastelandGame.Instance;
            var all = g.vehiclePrefabs.Concat(g.boatPrefabs ?? new GameObject[0]).Concat(g.trailerPrefabs ?? new GameObject[0]).ToList();
            c.Check(all.All(p => p), "no missing prefab references");
            var names = new HashSet<string>();
            foreach (var p in all.Where(p => p))
            {
                c.Check(names.Add(p.name), p.name + ": unique name");
                var ch = p.GetComponent<VehicleChassis>(); var d = p.GetComponent<VehicleDriver>();
                if (!c.Check(ch && d, p.name + ": chassis and driver")) continue;
                var body = p.transform.Find("Body");
                c.Check(body && body.GetComponent<MeshFilter>() && body.GetComponent<MeshFilter>().sharedMesh, p.name + ": body mesh");
                if (!d.driveable) continue;
                bool boat = p.GetComponent<BoatModel>();
                if (!boat) c.Check(p.GetComponentInChildren<EngineStats>(true), p.name + ": an engine is fitted");     // rafts are paddled
                if (!boat) c.Check(p.GetComponentsInChildren<WheelStats>(true).Length >= 2, p.name + ": at least two wheels fitted");
            }
            c.Metric("vehicles", all.Count, "");
        }
    }

    /// <summary>The feature manifest only names scenarios that exist.</summary>
    class ManifestContract : Scenario
    {
        public override string Id => "catalogue.manifest";
        public override string[] Suites => new[] { "fast", "full", "catalogue" };
        public override bool NeedsWorld => false;

        public override IEnumerator Run(ScenarioContext c)
        {
            while (!WastelandGame.Instance) yield return null;
            var feats = CoverageReport.Load();
            c.Check(feats.Length > 0, "manifest.json present with features");
            var dangling = CoverageReport.Dangling(AcceptanceScenarios.Ids());
            foreach (var d in dangling) c.Check(false, "manifest names a missing scenario: " + d);
            c.Metric("features", feats.Length, "");
            c.Metric("gaps", feats.Count(f => f.scenarios == null || f.scenarios.Length == 0), "");
        }
    }

    // ================================================================== Q3 vehicles

    /// <summary>Spawned fleet vehicle on a level pad: enter, start, drive forward, brake to a stop, reverse, exit.
    /// Bikes and aircraft get their own mobility.* scenarios instead (own control models).</summary>
    class VehicleDrive : Scenario
    {
        readonly string name;
        public VehicleDrive(string name) { this.name = name; }
        public override string Id => "vehicle.drive." + name;
        static readonly string[] FastSet = { "Sedan", "Fiat126p", "Bulldozer" };                  // one car, one real car, one crawler
        public override string[] Suites => System.Array.IndexOf(FastSet, name) >= 0 ? new[] { "full", "vehicles", "fast" } : new[] { "full", "vehicles" };
        public override float Timeout => 70f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle(name);
            if (!v) { c.Block(name + " is not in the start fleet"); yield break; }
            if (!v.driveable) { c.Block("not driveable (trailer)"); yield break; }
            if (v.aircraft || v.GetComponent<BoatModel>() || v.GetComponent<BikeBalance>()) { c.Block("own control model: dedicated mobility suite not written yet"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level test pad with a clear 60 m lane"); yield break; }
            yield return TestWorld.Place(c, v, pad, fwd);
            g.Enter(v);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(g.Current == v, "entered " + name)) yield break;
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;

            // forward from rest
            var p0 = v.transform.position;
            v.handbrake = false; v.throttleInput = 1f; v.brakeInput = 0f;
            float t0 = Time.time, to20 = -1f;
            while (Time.time - t0 < 5f)
            {
                if (to20 < 0f && v.SpeedKmh >= 20f) to20 = Time.time - t0;
                yield return null;
            }
            float ahead = Vector3.Dot(v.transform.position - p0, fwd);
            c.Metric("forward_5s", ahead, "m");
            if (to20 >= 0f) c.Metric("to_20_kmh", to20, "s");
            if (!c.Check(ahead > 4f, $"drives forward from rest ({ahead:0.0} m in 5 s)")) c.Note("touching: " + TestWorld.Contacts(v) + "; " + TestWorld.State(v));

            // brake to a stop
            v.throttleInput = 0f; v.brakeInput = 1f;
            var pb = v.transform.position; t0 = Time.time;
            while (Mathf.Abs(v.ForwardSpeed) > 0.3f && Time.time - t0 < 8f) yield return null;
            c.Metric("stop_distance", Vector3.Distance(pb, v.transform.position), "m");
            c.Check(Mathf.Abs(v.ForwardSpeed) <= 0.3f, "brakes to a standstill");

            // automatic: holding the brake at rest selects reverse
            var pr = v.transform.position; t0 = Time.time;
            float nextNote = 0f;
            while (Time.time - t0 < 4.5f)
            {
                if (Time.time - t0 > nextNote) { nextNote += 1f; c.Note($"t {Time.time - t0:0.0}: rev {v.Reversing}, gear {v.Gear}, rpm {v.Rpm:0}, drive {v.DriveForce:0} N, v {v.ForwardSpeed:0.00}, sleeping {v.Body.IsSleeping()}"); }
                yield return null;
            }
            float back = -Vector3.Dot(v.transform.position - pr, v.transform.forward);
            c.Metric("reverse_4s", back, "m");
            if (!c.Check(back > 1f, $"reverses from rest ({back:0.0} m)")) c.Note("touching: " + TestWorld.Contacts(v) + "; " + TestWorld.State(v));

            v.brakeInput = 0f; v.handbrake = true;
            yield return new WaitForSeconds(0.8f);
            g.Exit();
            yield return new WaitForSeconds(0.3f);
            c.Check(g.Current == null && g.Player && g.Player.gameObject.activeInHierarchy, "exits on foot");
        }
    }

    /// <summary>Regression (2026-09-30): machines rest on their wheels and move; excavator digs a visible load,
    /// dumps it into the tipper bed without losing soil, the tipper unloads onto the ground.</summary>
    class MachineSoilRoundTrip : Scenario
    {
        public override string Id => "regression.machine_soil";
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var ex = TestWorld.Vehicle("Excavator"); var truck = TestWorld.Vehicle("DumpTruck");
            if (!ex || !truck) { c.Block("excavator or dump truck missing from the fleet"); yield break; }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, ex, pad, Vector3.forward, 2f);
            var m = ex.GetComponent<Machine>();
            g.Enter(ex);
            yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, ex);
            if (c.Failed) yield break;

            // the machine drives (it used to rest on its body)
            var p0 = ex.transform.position;
            ex.handbrake = false; ex.throttleInput = 1f;
            yield return new WaitForSeconds(3f);
            ex.throttleInput = 0f; ex.brakeInput = 1f;
            float moved = Vector3.Distance(p0, ex.transform.position);
            c.Metric("excavator_3s", moved, "m");
            c.Check(moved > 1.5f, $"excavator drives ({moved:0.0} m in 3 s)");
            yield return new WaitForSeconds(1.5f);
            ex.brakeInput = 0f; ex.handbrake = true;

            // dig: boom down until the teeth are in the soil, then curl
            float t0 = Time.time;
            WastelandGame.MachineInput = new MachineKeys { h1 = true };
            while (!(m.Status ?? "").Contains("IN SOIL") && Time.time - t0 < 8f) yield return null;
            c.Check((m.Status ?? "").Contains("IN SOIL"), "bucket teeth reach the ground");
            t0 = Time.time;
            WastelandGame.MachineInput = new MachineKeys { h1 = true, h5 = true, h3 = true };
            while (m.load < m.Capacity * 0.5f && Time.time - t0 < 12f) yield return null;
            WastelandGame.MachineInput = default;
            c.Metric("bucket_load", m.load, "m3");
            if (!c.Check(m.load > 0.2f, $"bucket fills ({m.load:0.00} m3)")) yield break;
            var heap = ex.GetComponentsInChildren<SoilHeap>(false).FirstOrDefault();
            c.Check(heap && heap.gameObject.activeInHierarchy, "soil is visible in the bucket");

            // raise, then park the tipper under the bucket (disclosed)
            t0 = Time.time;
            WastelandGame.MachineInput = new MachineKeys { h2 = true };
            while (Time.time - t0 < 1.2f) yield return null;
            WastelandGame.MachineInput = default;
            WastelandGame.MachineInput = new MachineKeys { h4 = true };                        // stick out: reach past the tracks
            t0 = Time.time;
            while (Time.time - t0 < 1.5f) yield return null;
            WastelandGame.MachineInput = default;
            var tm = truck.GetComponent<Machine>();
            int before = tm.BedUnits;
            // park the tipper clear of the excavator, facing away, its bed as close to the bucket as the bodies allow
            var fwd = Vector3.ProjectOnPlane(ex.transform.forward, Vector3.up).normalized;
            float front = 0f;
            foreach (var col in ex.GetComponentsInChildren<Collider>()) if (col.gameObject.layer != LayerMask.NameToLayer("MachineTool")) front = Mathf.Max(front, Vector3.Dot(col.bounds.max - ex.transform.position, fwd));
            var tb = truck.transform.Find("Body").GetComponent<Renderer>().bounds;
            float rear = Vector3.Dot(truck.transform.position - tb.center, truck.transform.forward) + tb.extents.z;   // root to rear bumper
            yield return TestWorld.Place(c, truck, ex.transform.position + fwd * (front + 1.2f + rear), fwd, 1.5f);
            c.Fixture("tipper parked facing away, 1.2 m clear of the excavator's front");
            float gap = Vector3.Distance(m.BucketTip, tm.BedPoint);
            c.Note($"tip {m.BucketTip}, bed point {tm.BedPoint}, reach {gap:0.00} m (bucket reach 7 m)");
            if (gap > 6.5f) { c.Block($"the bed is out of the bucket's reach ({gap:0.0} m; touching: {TestWorld.Contacts(truck)})"); yield break; }
            c.Check(g.Current == ex, "still at the excavator's controls");
            float expected = m.load * Machine.UnitsPerM3;
            t0 = Time.time;
            WastelandGame.MachineInput = new MachineKeys { h6 = true };
            while (m.load > 0.01f && Time.time - t0 < 8f) yield return null;
            WastelandGame.MachineInput = default;
            yield return new WaitForSeconds(0.6f);
            c.Note($"after opening: load {m.load:0.00}, bucket angle {m.BucketAngle:0}, current {(g.Current ? g.Current.name : "none")}, status '{m.Status}', bed units {tm.BedUnits}");
            int added = tm.BedUnits - before;
            c.Metric("bed_units_added", added, "units");
            c.Check(Mathf.Abs(added - Mathf.RoundToInt(expected)) <= 1, $"bed received the bucket's soil ({added} of {expected:0.0} units)");
            var bedHeap = truck.GetComponentsInChildren<SoilHeap>(false).FirstOrDefault();
            c.Check(bedHeap && bedHeap.gameObject.activeInHierarchy, "soil is visible in the tipper bed");

            // tip it out behind the truck
            g.Exit(); yield return new WaitForSeconds(0.3f);
            g.Enter(truck); yield return new WaitForSeconds(0.3f);
            var t = DeformableTerrain.Instance;
            var behind = truck.transform.TransformPoint(new Vector3(0, 0, -56) * 0.08f);
            float h0 = t.Height(behind.x, behind.z);
            int loaded = tm.BedUnits;
            t0 = Time.time;
            WastelandGame.MachineInput = new MachineKeys { h2 = true };
            while (tm.BedUnits > 0 && Time.time - t0 < 14f) yield return null;
            WastelandGame.MachineInput = default;
            float rise = t.Height(behind.x, behind.z) - h0;
            c.Metric("ground_rise_behind", rise, "m");
            c.Check(tm.BedUnits < loaded, $"tipper unloads ({loaded} -> {tm.BedUnits} units)");
            c.Check(rise > 0.01f, $"the unloaded soil lands behind the truck (+{rise:0.00} m)");
            WastelandGame.MachineInput = new MachineKeys { h1 = true };
            yield return new WaitForSeconds(4f);
            WastelandGame.MachineInput = default;
        }
    }

    /// <summary>Regression (2026-09-30): pulling away uphill must not roll back (hill hold + launch traction).</summary>
    class HillHold : Scenario
    {
        readonly string car; readonly float min, max;
        public HillHold(string car, float min, float max) { this.car = car; this.min = min; this.max = max; }
        public override string Id => "regression.hill_hold." + car;
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var v = TestWorld.Vehicle(car);
            if (!v) { c.Block(car + " missing"); yield break; }
            if (!TestWorld.Slope(min, max, out var at, out var up)) { c.Block($"no {min:0}-{max:0}° slope near the start"); yield break; }
            yield return TestWorld.Place(c, v, at, up, 2f);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var p0 = v.transform.position; float worst = 0f, t0 = Time.time;
            v.handbrake = false; v.throttleInput = 1f;
            while (Time.time - t0 < 6f) { worst = Mathf.Min(worst, Vector3.Dot(v.transform.position - p0, up)); yield return null; }
            float gained = Vector3.Dot(v.transform.position - p0, up);
            v.throttleInput = 0f; v.handbrake = true;
            c.Metric("slope", Vector3.Angle(DeformableTerrain.Instance.Normal(at.x, at.z), Vector3.up), "deg");
            c.Metric("worst_rollback", -worst, "m");
            c.Metric("uphill_6s", gained, "m");
            c.Check(worst > -0.35f, $"does not roll back more than 0.35 m ({-worst:0.00} m)");
            c.Check(gained > 2f, $"climbs the slope from rest ({gained:0.0} m in 6 s)");
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>Regression (2026-09-30): a car driving along another it touches slides free instead of sticking.</summary>
    class CarsSeparate : Scenario
    {
        public override string Id => "regression.cars_separate";
        public override float Timeout => 50f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var a = TestWorld.Vehicle("Coupe"); var b = TestWorld.Vehicle("Wagon");
            if (!a || !b) { c.Block("coupe or wagon missing"); yield break; }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, b, pad + Vector3.right * 1.9f, Vector3.forward, 0.2f);
            yield return TestWorld.Place(c, a, pad, Vector3.forward, 1.5f);
            c.Fixture("cars parked flank to flank");
            g.Enter(a); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, a);
            if (c.Failed) yield break;
            var p0 = a.transform.position; var b0 = b.transform.position;
            a.handbrake = false; a.throttleInput = 1f; a.steerInput = 0.35f;              // steer into the other car
            yield return new WaitForSeconds(4f);
            a.throttleInput = 0f; a.brakeInput = 1f;
            float moved = Vector3.Distance(p0, a.transform.position);
            c.Metric("moved_4s", moved, "m");
            c.Metric("other_pushed", Vector3.Distance(b0, b.transform.position), "m");
            if (!c.Check(moved > 3f, $"slides along the other car ({moved:0.0} m in 4 s)")) c.Note(TestWorld.State(a) + "; touching: " + TestWorld.Contacts(a));
            yield return new WaitForSeconds(1f);
        }
    }

    // ================================================================== Q4 crafting

    /// <summary>A fuel-burning recipe paid with a stand-in fuel (charcoal for wood) refunds exactly what it took on
    /// cancel, then completes once with its output.</summary>
    class CraftFuelRefund : Scenario
    {
        public override string Id => "crafting.fuel_refund";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var r = RecipeLibrary.All.FirstOrDefault(x => x.fuel == ResourceType.Wood && x.kind != OutputKind.Part && x.kind != OutputKind.Vehicle
                                                          && string.IsNullOrEmpty(RecipeLibrary.KnowledgeFor(x)) && FurnitureLibrary.Get(x.station) != null
                                                          && x.resources.All(i => i.type != ResourceType.Wood && i.type != ResourceType.Charcoal));
            if (r == null) { c.Block("no wood-fired recipe without a knowledge gate"); yield break; }
            c.Note("recipe " + r.id + " at " + r.station);
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad away from storage"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            c.Fixture("crafting on an open pad: no containers within reach");
            yield return new WaitForSeconds(0.3f);
            var at = g.Player.transform.position + g.Player.transform.forward * 1.6f;
            var piece = FurnitureLibrary.Spawn(r.station, g.Build.Structures, at, Quaternion.identity, g.propMaterial);
            yield return null;
            var st = piece ? piece.GetComponentInChildren<CraftingStation>() : null;
            if (!c.Check(st, "station " + r.station + " spawns")) yield break;
            c.Fixture("placed a " + r.station + " beside the player");
            // pack holds exactly the inputs and charcoal (no wood): the job must burn the stand-in
            foreach (var (t, n) in r.resources) if (t != ResourceType.None) g.Inventory.Add(t, RecipeLibrary.Amount(n));
            foreach (var (i, n) in r.items) g.Inventory.AddItem(i, n);
            int wood = g.Inventory.Get(ResourceType.Wood);
            if (wood > 0) g.Inventory.TrySpend(ResourceType.Wood, wood);
            g.Inventory.Add(ResourceType.Charcoal, r.fuelAmount);
            c.Fixture($"granted {r.id} inputs and {r.fuelAmount} charcoal, removed {wood} wood");
            var before = Ledger(g.Inventory);

            c.Check(g.CraftBlockReason(r, st) == null, "recipe shows as craftable: " + (g.CraftBlockReason(r, st) ?? "ok"));
            g.Craft(r, st);
            c.Check(st.queue.Count == 1, "job queued");
            c.Check(g.Inventory.Get(ResourceType.Charcoal) == 0, "charcoal burnt as the stand-in fuel");
            g.CancelLastJob(st);
            var after = Ledger(g.Inventory);
            c.Check(Same(before, after, out var diff), "cancel refunds exactly what it took" + (diff != null ? " (" + diff + ")" : ""));

            g.Craft(r, st);
            if (st.queue.Count == 1) { st.queue[0].progress = 0.97f; c.Fixture("job advanced to 97 %"); }
            int outBefore = Output(g, r, st);
            float t0 = Time.time;
            while (st.queue.Count > 0 && Time.time - t0 < 30f) yield return null;
            yield return new WaitForSeconds(1f);
            int got = Output(g, r, st) - outBefore;
            c.Check(got == r.amount, $"the job delivers its output once ({got} of {r.amount})");
            if (piece) Object.Destroy(piece.gameObject);
        }

        static int Output(WastelandGame g, Recipe r, CraftingStation st) =>
            r.kind == OutputKind.Resource ? g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) : g.Inventory.GetItem(r.output) + st.tray.GetItem(r.output);

        static Dictionary<string, int> Ledger(Inventory inv)
        {
            var d = new Dictionary<string, int>();
            for (int t = 1; t < ResourceInfo.Count; t++) { int n = inv.Get((ResourceType)t); if (n != 0) d["res:" + t] = n; }
            foreach (var kv in inv.Items) if (kv.Value != 0) d[kv.Key] = kv.Value;
            return d;
        }

        static bool Same(Dictionary<string, int> a, Dictionary<string, int> b, out string diff)
        {
            diff = null;
            foreach (var k in a.Keys.Union(b.Keys))
            {
                a.TryGetValue(k, out int x); b.TryGetValue(k, out int y);
                if (x != y) { diff = k + " " + x + " -> " + y; return false; }
            }
            return true;
        }
    }

    // ================================================================== Q2 / Q5 persistence

    /// <summary>Save to a slot in the isolated profile, reload the scene from it, compare inventory, position and fleet.</summary>
    class SaveRoundTrip : Scenario
    {
        public override string Id => "save.roundtrip";
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            if (!Profile.Isolated) { c.Block("needs an isolated profile (-profiledir) so real saves are never touched"); yield break; }
            var g = c.Game;
            g.Inventory.AddItem(ItemIds.Paper, 7);
            c.Fixture("granted 7 paper as a marker");
            int paper = g.Inventory.GetItem(ItemIds.Paper), scrap = g.Inventory.Get(ResourceType.Scrap);
            var pos = g.Player.transform.position;
            int fleet = Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None).Count(v => v.driveable && !v.aiDriven);   // AI traffic is not saved
            g.SaveGame(2);
            c.Check(SaveSystem.Exists(2), "slot 2 written in the test profile");
            var old = g;
            g.LoadGame(2);
            float t0 = Time.time;
            while ((WastelandGame.Instance == old || !WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - c.startedAt < 200f) yield return null;
            g = WastelandGame.Instance;
            if (!c.Check(g && g != old && g.Ready, "the save loads")) yield break;
            WastelandGame.ExternalInput = true;
            yield return new WaitForSeconds(1f);
            c.Metric("load_time", Time.time - t0, "s");
            c.Check(g.Inventory.GetItem(ItemIds.Paper) == paper, $"inventory item kept ({g.Inventory.GetItem(ItemIds.Paper)} of {paper} paper)");
            c.Check(g.Inventory.Get(ResourceType.Scrap) == scrap, "scrap kept");
            float moved = Vector3.Distance(pos, g.Player.transform.position);
            c.Metric("player_offset", moved, "m");
            c.Check(moved < 1.5f, $"player back where they saved ({moved:0.00} m off)");
            int fleet2 = Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None).Count(v => v.driveable && !v.aiDriven);
            c.Check(fleet2 >= fleet, $"fleet kept ({fleet2} of {fleet} vehicles)");
        }
    }

    // ================================================================== damage feedback (2026-09-30 requests)

    /// <summary>A knock on the windscreen breaks glass (quads removed, shards on the ground); one at a front corner
    /// breaks that headlamp (lens dark, the light dims); the state survives <see cref="VehicleBreakables.SaveState"/>.</summary>
    class GlassAndLamps : Scenario
    {
        public override string Id => "vehicle.glass_and_lamps";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("sedan missing"); yield break; }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var wear = v.GetComponent<VehicleBreakables>();
            var glass = v.transform.Find("Body/Glass");
            if (!c.Check(wear && glass, "vehicle has breakables and a glass mesh")) yield break;
            var gb = glass.GetComponent<Renderer>().bounds;
            int trisBefore = Visible(glass.GetComponent<MeshFilter>().sharedMesh);
            int shardsBefore = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m => m.name == "Shards" && m.gameObject.activeInHierarchy);
            var windscreen = new Vector3(gb.center.x, gb.center.y, gb.max.z - 0.15f);
            v.GetComponent<VehicleDamage>().ApplyHit(windscreen, -v.transform.forward, 1.5f, 0.35f, null);
            c.Fixture("struck the windscreen (power 1.5, as a sledgehammer)");
            yield return null; yield return null;
            int trisAfter = Visible(glass.GetComponent<MeshFilter>().sharedMesh);
            int shardsAfter = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m => m.name == "Shards" && m.gameObject.activeInHierarchy);
            c.Metric("glass_quads_removed", (trisBefore - trisAfter) / 6f, "");
            c.Check(trisAfter < trisBefore, "glass breaks where it was hit");
            c.Check(shardsAfter > shardsBefore, "shards are left on the ground");

            var bb = v.transform.Find("Body").GetComponent<Renderer>().bounds;
            var corner = v.transform.TransformPoint(new Vector3(-0.6f, 0f, 0f)); corner.y = bb.min.y + bb.size.y * 0.35f;
            corner += v.transform.forward * (bb.extents.z - 0.05f);
            v.GetComponent<VehicleDamage>().ApplyHit(corner, -v.transform.forward, 1.5f, 0.45f, null);
            c.Fixture("struck the front-left corner");
            yield return null;
            c.Check((wear.BrokenLamps & 1) != 0, $"front-left lamp breaks (bits {wear.BrokenLamps})");
            var state = wear.SaveState();
            c.Check(!string.IsNullOrEmpty(state) && state.StartsWith(wear.BrokenLamps.ToString()), "broken glass and lamps are saved: " + (state ?? "null").Split('|')[0]);
        }

        static int Visible(Mesh m)
        {
            var t = m.triangles; int n = 0;
            for (int i = 0; i + 2 < t.Length; i += 3) if (t[i] != t[i + 1] || t[i] != t[i + 2]) n += 3;
            return n;
        }
    }

    /// <summary>CAR DEFORMATION OFF leaves the body undented after a crash; NORMAL dents it.</summary>
    class DeformationSetting : Scenario
    {
        public override string Id => "settings.car_deformation";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var v = TestWorld.Vehicle("Coupe");
            if (!v) { c.Block("coupe missing"); yield break; }
            var s = GameSettings.Current; int keep = s.deformation;
            var dmg = v.GetComponent<VehicleDamage>();
            var at = v.transform.Find("Body").GetComponent<Renderer>().bounds.center + v.transform.right * 0.9f;
            string State() => string.Join("#", v.GetComponentsInChildren<DeformableMesh>().Select(d => d.SaveState()).Where(x => x != null));
            s.deformation = 0;
            var s0 = State();
            dmg.DentAt(at, -v.transform.right, 0.2f, 0.6f);
            yield return null;
            bool dentedOff = State() != s0;
            s.deformation = 2;
            var s1 = State();
            dmg.DentAt(at, -v.transform.right, 0.2f, 0.6f);
            yield return null;
            bool dentedOn = State() != s1;
            s.deformation = keep;
            c.Fixture("dented the coupe's right side directly (0.2 m, 0.6 m radius)");
            c.Check(!dentedOff, "OFF: no dents");
            c.Check(dentedOn, "NORMAL: the same knock dents the body");
        }
    }

    /// <summary>Depth stage A: one dish up every rung of the cooking ladder — campfire, wood stove, electric oven,
    /// kitchen range (triple batch), cannery (tins that keep), still (brewing) — each placed, powered where it needs it,
    /// fed from the pack and worked off to its output.</summary>
    class CookingLadder : Scenario
    {
        public override string Id => "cooking.ladder";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.3f); }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            var steps = new[] { ("campfire", "fire_meat"), ("stove", "stove_meat_stew"), ("oven", "oven_apple_pie"), ("kitchen_range", "range_meat_stew"), ("cannery", "can_meat"), ("still", "brew_cider") };
            var fwd = g.Player.transform.forward; var right = g.Player.transform.right;
            var gen = FurnitureLibrary.Spawn("coal_generator", g.Build.Structures, g.Player.transform.position - fwd * 2f, Quaternion.identity, g.propMaterial);
            yield return null;
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null;
            var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(genNode && genComp, "generator spawns")) yield break;
            genComp.fuel = 60f; genComp.on = true;
            c.Fixture("fuelled steam generator (2.5 kW) behind the player, cabled to one powered station at a time");
            UtilityNode prev = null;
            int made = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                var (piece, id) = steps[i];
                var r = RecipeLibrary.Get(id);
                if (!c.Check(r != null, "recipe " + id + " exists")) continue;
                var at = g.Player.transform.position + fwd * 1.8f + right * ((i % 3) * 2.4f - 2.4f) + fwd * (i / 3) * 2.4f;
                var pl = FurnitureLibrary.Spawn(piece, g.Build.Structures, at, Quaternion.LookRotation(-fwd), g.propMaterial);
                yield return null;
                var st = pl ? pl.GetComponentInChildren<CraftingStation>() : null;
                if (!c.Check(st && st.type == r.station, $"{piece} offers {r.station} recipes")) continue;
                var node = pl.GetComponent<UtilityNode>();
                if (st.watts > 0f && node) { if (prev) prev.Unlink(); node.Link(genNode, UtilityKind.Power); prev = node; }
                if (st.watts > 0f) { float t1 = Time.time; while (!st.Powered && Time.time - t1 < 3f) yield return null; }
                if (!c.Check(st.Powered, piece + " has power")) continue;
                foreach (var (t, n) in r.resources) if (t != ResourceType.None) g.Inventory.Add(t, RecipeLibrary.Amount(n));
                foreach (var (it, n) in r.items) g.Inventory.AddItem(it, n);
                if (r.fuel != ResourceType.None) g.Inventory.Add(r.fuel, r.fuelAmount);
                string why = g.CraftBlockReason(r, st);
                if (!c.Check(why == null, $"{r.name} craftable at the {piece}" + (why != null ? ": " + why : ""))) continue;
                int before = Output(g, r, st);
                g.Craft(r, st);
                if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
                float t0 = Time.time;
                while (st.queue.Count > 0 && Time.time - t0 < 20f) yield return null;
                yield return new WaitForSeconds(0.3f);
                int got = Output(g, r, st) - before;
                if (c.Check(got == r.amount, $"{piece}: {r.name} delivers {got} of {r.amount}")) made++;
            }
            c.Metric("rungs_cooked", made, "");
            var stew = RecipeLibrary.Get("stove_meat_stew"); var batch = RecipeLibrary.Get("range_meat_stew");
            c.Check(stew != null && batch != null && batch.amount == stew.amount * 3 && RecipeLibrary.Seconds(batch) <= RecipeLibrary.Seconds(stew), "the range cooks three times the stove's batch in the same time");
            var tin = MadMax.Items.FoodLibrary.Get("food_can_meat");
            c.Check(tin != null && tin.spoilMinutes == 0f, "tinned food never spoils");
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);
            c.Note($"player at {g.Player.transform.position}, pad {pad}");
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("cooking_ladder");
            yield return null;
        }

        static int Output(WastelandGame g, Recipe r, CraftingStation st) =>
            r.kind == OutputKind.Resource ? g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) : g.Inventory.GetItem(r.output) + st.tray.GetItem(r.output);
    }

    /// <summary>Engine start odds by condition: a sound engine (75 %+) always catches, half condition still mostly
    /// (about 80 %), a wreck rarely.</summary>
    class StartChance : Scenario
    {
        public override string Id => "vehicle.start_chance";

        public override IEnumerator Run(ScenarioContext c)
        {
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("sedan missing"); yield break; }
            var sys = v.GetComponent<VehicleSystems>(); var ep = v.Engine.GetComponent<VehiclePart>();
            float keep = ep.damage;
            float At(float condition) { ep.damage = 1f - condition; return sys.StartChance; }
            float c97 = At(0.97f), c75 = At(0.75f), c50 = At(0.5f), c20 = At(0.2f);
            ep.damage = keep;
            c.Metric("start_97", c97, ""); c.Metric("start_50", c50, ""); c.Metric("start_20", c20, "");
            c.Check(c97 >= 0.99f && c75 >= 0.99f, $"a sound engine always catches (97 %: {c97:0.00}, 75 %: {c75:0.00})");
            c.Check(c50 >= 0.75f && c50 < 0.95f, $"half condition mostly catches ({c50:0.00})");
            c.Check(c20 < 0.5f, $"a wreck of an engine rarely does ({c20:0.00})");
            yield break;
        }
    }

    /// <summary>The tailpipe smokes while the engine runs; a worn engine smokes thicker and blacker than a healthy one.</summary>
    class ExhaustSmoke : Scenario
    {
        public override string Id => "vehicle.exhaust_smoke";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("sedan missing"); yield break; }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 0.5f);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;
            var sys = v.GetComponent<VehicleSystems>();
            var ep = v.Engine.GetComponent<VehiclePart>();
            v.handbrake = true; v.throttleInput = 0.6f;
            yield return new WaitForSeconds(1f);
            float cleanDark = sys.ExhaustDarkness, cleanRate = sys.ExhaustRate;
            ep.damage = 0.8f;
            yield return new WaitForSeconds(1f);
            float wornDark = sys.ExhaustDarkness, wornRate = sys.ExhaustRate;
            c.Metric("exhaust_dark_healthy", cleanDark, ""); c.Metric("exhaust_dark_worn", wornDark, "");
            c.Note("outlet (local) " + v.transform.InverseTransformPoint(sys.LastOutlet) + ", exhaust sockets: " + string.Join(", ", v.GetComponentsInChildren<MountSocket>().Where(k => k.accepts == PartCategory.Exhaust).Select(k => k.name + "=" + (k.Current ? k.Current.partId : "-"))));
            c.Screenshot("worn_engine_smoke");
            yield return null;
            // a holed radiator steams at the front
            var radSock = v.GetComponentsInChildren<MountSocket>().FirstOrDefault(k => k.accepts == PartCategory.Radiator);
            if (radSock && radSock.Current)
            {
                ep.damage = 0f; radSock.Current.damage = 0.9f; v.throttleInput = 0.2f;
                yield return new WaitForSeconds(1.2f);
                c.Check((sys.Faults & Fault.CoolantLeak) != 0, "a holed radiator leaks coolant");
                c.Screenshot("radiator_steam");
                yield return null;
                radSock.Current.damage = 0f;
            }
            else c.Note("no radiator socket on the sedan: steam not checked");
            ep.damage = 0f; v.throttleInput = 0f;
            c.Check(cleanRate > 0f, $"a running engine smokes ({cleanRate:0.0} puffs/s, darkness {cleanDark:0.00})");
            c.Check(wornDark > cleanDark + 0.4f && wornRate > cleanRate * 2f, $"a worn engine smokes blacker and thicker ({wornRate:0.0} puffs/s, darkness {wornDark:0.00})");
        }
    }

    /// <summary>Scraping along another car at speed bares metal on the side that rubbed.</summary>
    class ScrapeMarks : Scenario
    {
        public override string Id => "vehicle.scrape_marks";
        public override float Timeout => 50f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var a = TestWorld.Vehicle("Pickup"); var b = TestWorld.Vehicle("Sedan");
            if (!a || !b) { c.Block("pickup or sedan missing"); yield break; }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, b, pad + Vector3.right * 1.7f + Vector3.forward * 3f, Vector3.forward, 0.2f);   // flanks overlap ~0.2 m
            yield return TestWorld.Place(c, a, pad - Vector3.forward * 4f, Vector3.forward, 1.5f);
            c.Fixture("pickup lined up to run along the sedan's flank");
            var wear = a.GetComponent<VehicleBreakables>();
            string before = wear.SaveState() ?? "";
            g.Enter(a); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, a);
            if (c.Failed) yield break;
            a.handbrake = false; a.throttleInput = 1f; a.steerInput = 0f;
            var dmg = a.GetComponent<VehicleDamage>();
            int calls0 = wear.ScrapeCalls;
            yield return new WaitForSeconds(3f);
            bool touched = wear.ScrapeCalls > calls0;
            if (!c.Check(touched, "the cars rub (scrape contact registered)")) c.Note(TestWorld.State(a) + $"; moved {Vector3.Distance(pad - Vector3.forward * 4f, a.transform.position):0.0} m; touching: " + TestWorld.Contacts(a));
            a.throttleInput = 0f; a.brakeInput = 1f; a.steerInput = 0f;
            yield return new WaitForSeconds(1f);
            string after = wear.SaveState() ?? "";
            int marks = after.Split(';').Skip(1).Sum(x => x.Split('|')[0].Split(',').Count(t => t.Length > 0));
            c.Metric("scratched_faces", marks, "");
            c.Note($"scrape calls {wear.ScrapeCalls}, last at local {a.transform.InverseTransformPoint(wear.LastScrape)}");
            c.Check(after != before && marks > 0, "paint is scraped to bare metal where the cars rubbed");
        }
    }

    /// <summary>Q7 presentation: in a perspective view the sun disc stands in the sky by day and the moon when it is up;
    /// screenshots are kept as evidence (the golden image is not the assertion).</summary>
    class SunAndMoon : Scenario
    {
        public override string Id => "visuals.sun_moon";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (!rig) { c.Block("no camera rig"); yield break; }
            var keepMode = rig.mode; float keepHours = DayNight.Hours;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.3f); }
            rig.mode = ViewMode.ThirdPerson;
            c.Fixture("on foot, third-person view, clock set to 18:15 then to the moon's hour");
            DayNight.SetHours(18.25f);
            yield return new WaitForSeconds(0.3f);
            var sd = DayNight.SunDirection;
            rig.LookToward(sd);
            yield return new WaitForSeconds(1.8f);                                              // the camera glides over from the car
            var atmo = Object.FindAnyObjectByType<Atmosphere>();
            var sunT = atmo ? atmo.transform.Find("SunDisc") : null; var sun = sunT ? sunT.gameObject : null;
            if (Atmosphere.CloudCover >= 0.85f) c.Block($"overcast (cloud cover {Atmosphere.CloudCover:0.00}): the sun is hidden by design");
            else c.Check(sun && sun.activeInHierarchy, $"the sun disc is up at 18:15 (sun elevation {sd.y:0.00}, sky visible {Atmosphere.SkyVisible}, clouds {Atmosphere.CloudCover:0.00})");
            var cam = Camera.main;
            if (sun && cam)
            {
                var vp = cam.WorldToViewportPoint(sun.transform.position);
                c.Metric("sun_viewport_x", vp.x, ""); c.Metric("sun_viewport_y", vp.y, "");
                c.Note($"cameras: {string.Join(", ", Camera.allCameras.Select(k => k.name + (k == cam ? "*" : "") + " @" + k.transform.position.ToString("0")))}; disc @{sun.transform.position:0}; atmospheres {Object.FindObjectsByType<Atmosphere>(FindObjectsSortMode.None).Length}");
                var toDisc = sun.transform.position - cam.transform.position;
                float off = Vector3.Angle(toDisc, sd);
                c.Metric("disc_bearing_error", off, "deg");
                c.Check(off < 2f && Mathf.Abs(toDisc.magnitude - 104f) < 3f && toDisc.magnitude < cam.farClipPlane, $"the disc stands on the sun's bearing, {toDisc.magnitude:0} m out, inside the far clip ({cam.farClipPlane:0} m)");
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) c.Note($"the camera was not facing it for the screenshot (viewport {vp.x:0.00},{vp.y:0.00})");
            }
            c.Screenshot("sun");
            yield return null; yield return null;                                               // the capture is written at the end of the frame
            // find an hour with the moon above the horizon
            float moonHour = -1f;
            for (float h = 0f; h < 24f; h += 1f) { DayNight.SetHours(h); yield return null; if (DayNight.MoonDirection.y > 0.25f) { moonHour = h; break; } }
            if (moonHour < 0f) c.Note("the moon stays low this day");
            else
            {
                var md = DayNight.MoonDirection;
                rig.LookToward(md);
                yield return new WaitForSeconds(1.2f);
                var moonT = atmo ? atmo.transform.Find("Moon") : null; var moon = moonT ? moonT.gameObject : null;
                c.Check(moon && moon.activeInHierarchy || Atmosphere.CloudCover >= 0.9f, $"the moon is up at {moonHour:00}:00");
                c.Screenshot("moon");
                yield return null; yield return null;
            }
            DayNight.SetHours(keepHours);
            rig.mode = keepMode;
        }
    }

    /// <summary>Tracked machines (roadmap: modelled tracks): on soaked soft ground with a full load (dozer blade pile,
    /// excavator bucket) the machine drives on, turns on the move and pivots on the spot.</summary>
    class CrawlerMud : Scenario
    {
        readonly string name;
        public CrawlerMud(string name) { this.name = name; }
        public override string Id => "vehicle.crawler_mud." + name;
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var v = TestWorld.Vehicle(name);
            if (!v) { c.Block(name + " missing"); yield break; }
            if (!c.Check(v.Tracked, name + " runs on modelled tracks")) yield break;
            Weather.SetWetness(1f);
            c.Fixture("ground soaked (weather wetness 1)");
            yield return new WaitForSeconds(0.5f);
            if (!TestWorld.MudPad(10f, 30f, 0.5f, out var pad, out var dir, out float mud)) { c.Block("no soft muddy ground near the start"); yield break; }
            c.Metric("mud", mud, "");
            yield return TestWorld.Place(c, v, pad, dir, 1.5f);
            var m = v.GetComponent<Machine>();
            if (m) { m.load = m.kind == Machine.Kind.Dozer ? 1.1f : m.Capacity; m.loadType = MadMax.Items.ResourceType.Clay; c.Fixture($"full load: {m.load:0.0} m3 of clay"); }
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            if (c.Failed) yield break;

            var p0 = v.transform.position;
            v.handbrake = false; v.throttleInput = 1f;
            float slipSum = 0f; int slipN = 0;
            for (float t = 0f; t < 8f; t += Time.deltaTime) { if (t > 2f) { slipSum += v.WheelSlip; slipN++; } yield return null; }
            float ahead = Vector3.Dot(v.transform.position - p0, dir);
            float slip = slipN > 0 ? slipSum / slipN : 0f;
            c.Metric("mud_8s", ahead, "m");
            c.Metric("mud_slip", slip, "");
            c.Check(slip < 0.25f, $"the tracks bite in the mud (mean slip {slip:0.00} once rolling)");
            if (!c.Check(ahead > 6f, $"drives through the mud with a full load ({ahead:0.0} m in 8 s)")) c.Note(TestWorld.State(v) + "; touching: " + TestWorld.Contacts(v));

            float y0 = v.transform.eulerAngles.y;
            v.steerInput = 1f;
            yield return new WaitForSeconds(3f);
            float turned = Mathf.Abs(Mathf.DeltaAngle(y0, v.transform.eulerAngles.y));
            c.Metric("turn_3s", turned, "deg");
            c.Check(turned > 15f, $"steers on the move ({turned:0} deg in 3 s)");

            v.throttleInput = 0f; v.brakeInput = 0f;
            yield return new WaitForSeconds(1f);
            y0 = v.transform.eulerAngles.y;
            var ps = v.transform.position;
            yield return new WaitForSeconds(3f);
            float pivot = Mathf.Abs(Mathf.DeltaAngle(y0, v.transform.eulerAngles.y));
            c.Metric("pivot_3s", pivot, "deg");
            c.Metric("pivot_drift", Vector3.Distance(ps, v.transform.position), "m");
            c.Check(pivot > 15f, $"pivots on the spot ({pivot:0} deg in 3 s)");
            v.steerInput = 0f; v.handbrake = true;
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>GET IN / OUT ANIMATION: the player walks to the door, the door opens, they slide in (the tool is put
    /// away while driving) and get out the same way; with the setting off both are instant.</summary>
    class Boarding : Scenario
    {
        public override string Id => "vehicle.boarding";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("sedan missing"); yield break; }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no pad"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            g.Player.Teleport(pad + Vector3.right * 3.2f + Vector3.up * 0.3f, -90f);
            c.Fixture("player 3 m from the sedan's door");
            var s = GameSettings.Current; bool keep = s.boardingAnimation;
            s.boardingAnimation = true;
            WastelandGame.ExternalInput = false;                                            // the animation plays for the player's own presses
            g.EnterAnimated(v);
            c.Check(g.Boarding, "the get-in animation starts");
            float t0 = Time.time;
            while (g.Boarding && Time.time - t0 < 4f) yield return null;
            c.Metric("get_in", Time.time - t0, "s");
            c.Check(g.Current == v && g.Player.SeatedIn == v, "seated at the wheel after the animation");
            c.Check(!g.Player.Tool || !g.Player.Tool.gameObject.activeInHierarchy, "the tool is put away while driving");
            g.ExitAnimated();
            t0 = Time.time;
            while (g.Boarding && Time.time - t0 < 4f) yield return null;
            c.Metric("get_out", Time.time - t0, "s");
            c.Check(g.Current == null && !g.Player.SeatedIn, "on foot after the get-out animation");
            c.Check(!g.Player.Tool || g.Player.Tool.gameObject.activeInHierarchy, "the tool is back in hand");
            s.boardingAnimation = false;
            g.EnterAnimated(v);
            c.Check(!g.Boarding && g.Current == v, "setting off: getting in is instant");
            g.Exit();
            s.boardingAnimation = keep;
            WastelandGame.ExternalInput = true;
            yield return null;
        }
    }
}
