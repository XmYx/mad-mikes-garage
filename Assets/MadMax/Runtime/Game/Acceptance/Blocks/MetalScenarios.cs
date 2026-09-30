using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Metal block (depth stage D).</summary>
    public static class MetalScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MetalParts();
            yield return new MetalLadder();
        }
    }

    /// <summary>Every depth-stage-D part is in <c>PartLibrary.All()</c> with its category, size and mass, has a baked
    /// prefab and exactly one recipe at a buildable station (forge, machine shop or garage); armour has crash stats,
    /// radiators cool more than stock, every kit has a recipe, and steel gets faster and richer up the ladder.</summary>
    class MetalParts : Scenario
    {
        public static readonly string[] Keys =
        {
            "exhaust_twin_chrome", "exhaust_flame_stack", "radiator_bigcore", "radiator_oil_cooler", "lights_led_bar", "lights_fog", "lights_search_pod",
            "armor_window_mesh", "armor_skirt_spiked", "armor_sloped", "spoiler_ducktail", "engine_v8_forged",
        };

        public override string Id => "metal.parts";
        public override string[] Suites => new[] { "fast", "full", "catalogue" };
        public override bool NeedsWorld => false;

        static PartCategory Expected(string key) =>
            key.StartsWith("exhaust_") ? PartCategory.Exhaust : key.StartsWith("radiator_") ? PartCategory.Radiator : key.StartsWith("lights_") ? PartCategory.Lights
            : key.StartsWith("armor_") ? PartCategory.Armor : key.StartsWith("spoiler_") ? PartCategory.Spoiler : PartCategory.Engine;

        public override IEnumerator Run(ScenarioContext c)
        {
            while (!WastelandGame.Instance) yield return null;
            var g = WastelandGame.Instance;
            var designs = new Dictionary<string, MadMax.Designs.PartDesign>();
            foreach (var p in MadMax.Designs.PartLibrary.All()) designs[p.key] = p;
            var prefabs = new HashSet<string>((g.partPrefabs ?? new GameObject[0]).Where(p => p).Select(p => p.name));
            foreach (var st in new[] { "forge", "machine_shop", "garage", "furnace", "arc_furnace" }) c.Check(FurnitureLibrary.Get(st) != null, "piece '" + st + "' is buildable");
            int ok = 0;
            foreach (var key in Keys)
            {
                if (!c.Check(designs.TryGetValue(key, out var d), key + ": in PartLibrary.All()")) continue;
                bool good = c.Check(d.category == Expected(key) && d.mass > 0f && d.sizeClass >= 1 && d.sizeClass <= 4, $"{key}: {d.category}, size {d.sizeClass}, {d.mass:0} kg");
                var recipes = RecipeLibrary.All.Where(r => r.kind == OutputKind.Part && r.output == key).ToList();
                good &= c.Check(recipes.Count == 1, $"{key}: one recipe ({string.Join(", ", recipes.Select(r => r.id + "@" + r.station))})");
                if (recipes.Count > 0) good &= c.Check(FurnitureLibrary.Get(recipes[0].station) != null, $"{key}: made at a buildable '{recipes[0].station}'");
                good &= c.Check(prefabs.Contains(key), key + ": prefab baked (run MadMax/Build Game Scene after adding parts)");
                if (d.category == PartCategory.Armor) good &= c.Check(ArmorStats.Get(key, out var s) && s.absorb > 0f, key + ": crash stats");
                if (d.category == PartCategory.Radiator) good &= c.Check(MetalPartFunctions.Cooling(key) > 1f, $"{key}: cools {MetalPartFunctions.Cooling(key):0.00}x a stock core");
                if (d.category == PartCategory.Engine) good &= c.Check(d.torque > 0f && d.maxRpm > 0f, $"{key}: {d.torque:0} Nm to {d.maxRpm:0} rpm");
                if (good) ok++;
            }
            foreach (var slot in VehicleTuning.KitSlots)
                foreach (var kit in slot.kits.Where(k => k != null))
                    c.Check(RecipeLibrary.All.Any(r => r.kind == OutputKind.Item && r.output == kit), kit + ": has a recipe");
            var forge = RecipeLibrary.Get("forge_steel"); var furnace = RecipeLibrary.Get("s_furnace_steel"); var arc = RecipeLibrary.Get("s_arc_furnace_steel");
            if (c.Check(forge != null && furnace != null && arc != null, "steel at the forge, furnace and arc furnace"))
            {
                float Rate(Recipe r) => r.amount / RecipeLibrary.Seconds(r);
                c.Metric("steel_per_min_forge", Rate(forge) * 60f, ""); c.Metric("steel_per_min_furnace", Rate(furnace) * 60f, ""); c.Metric("steel_per_min_arc", Rate(arc) * 60f, "");
                c.Check(Rate(forge) < Rate(furnace) && Rate(furnace) < Rate(arc), "steel comes faster up the ladder (forge < furnace < arc furnace)");
                c.Check(forge.fuel == ResourceType.Charcoal && furnace.fuel == ResourceType.Charcoal && arc.fuel == ResourceType.None, "forge and furnace burn charcoal (coal stands in), the arc furnace runs on power");
            }
            c.Metric("metal_parts_ok", ok, "");
        }
    }

    /// <summary>Depth stage D in a live seed-7 world: smith an axe at the forge and anvil, make steel at the furnace and
    /// the arc furnace, sand-cast blanks, then machine a close-ratio gearbox at a powered machine shop; fit it on the
    /// Sedan through the tuning kits (then a wide-ratio one, which swaps the first back into the pack) and measure the
    /// gear ratios; fit brakes, lift kit, long-range tank and transfer case; round-trip the tuning save; shoe a horse.</summary>
    class MetalLadder : Scenario
    {
        public override string Id => "metal.ladder";
        public override float Timeout => 160f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.3f); }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            var fwd = g.Player.transform.forward; var right = g.Player.transform.right;
            var gen = FurnitureLibrary.Spawn("coal_generator", g.Build.Structures, g.Player.transform.position - fwd * 2f, Quaternion.identity, g.propMaterial);
            yield return null;
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null;
            var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(genNode && genComp, "generator spawns")) yield break;
            genComp.fuel = 60f; genComp.on = true;
            var bat = FurnitureLibrary.Spawn("battery", g.Build.Structures, g.Player.transform.position - fwd * 2f + right * 2f, Quaternion.identity, g.propMaterial);
            yield return null;
            var batNode = bat ? bat.GetComponent<UtilityNode>() : null;
            if (batNode) { batNode.Link(genNode, UtilityKind.Power); batNode.batteryCharge = batNode.batteryWh; }
            c.Fixture("fuelled steam generator (2.5 kW) behind the player with a charged battery bank (the arc furnace draws 3 kW), cabled to one powered station at a time");
            c.Fixture("each recipe's inputs and fuel are added to the pack; each job starts at 95 % progress");

            var steps = new[] { ("forge", "forge_axe"), ("furnace", "s_furnace_steel"), ("arc_furnace", "s_arc_furnace_steel"), ("furnace", "s_furnace_castings"), ("machine_shop", "ms_gearbox_close") };
            UtilityNode prev = null;
            int made = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                var (piece, id) = steps[i];
                var r = RecipeLibrary.Get(id);
                if (!c.Check(r != null, "recipe " + id + " exists")) continue;
                var at = g.Player.transform.position + fwd * 2.2f + right * ((i % 3) * 3f - 3f) + fwd * (i / 3) * 3f;
                var pl = FurnitureLibrary.Spawn(piece, g.Build.Structures, at, Quaternion.LookRotation(-fwd), g.propMaterial);
                yield return null;
                var st = pl ? pl.GetComponentInChildren<CraftingStation>() : null;
                if (!c.Check(st && st.type == r.station, $"{piece} offers {r.station} recipes")) continue;
                var node = pl.GetComponent<UtilityNode>();
                if (st.watts > 0f && node) { if (prev) prev.Unlink(); node.Link(genNode, UtilityKind.Power); prev = node; }
                if (st.watts > 0f) { float t1 = Time.time; while (!st.Powered && Time.time - t1 < 3f) yield return null; }
                if (!c.Check(st.Powered, piece + " has power")) continue;
                foreach (var (t, k) in r.resources) if (t != ResourceType.None) g.Inventory.Add(t, RecipeLibrary.Amount(k));
                foreach (var (it, k) in r.items) g.Inventory.AddItem(it, k);
                if (r.fuel != ResourceType.None) g.Inventory.Add(r.fuel, r.fuelAmount);
                string why = g.CraftBlockReason(r, st);
                if (!c.Check(why == null, $"{r.name} craftable at the {piece}" + (why != null ? ": " + why : ""))) continue;
                int before = Output(g, r, st);
                g.Craft(r, st);
                if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
                float t0 = Time.time;
                while (st.queue.Count > 0 && Time.time - t0 < 30f) yield return null;
                yield return new WaitForSeconds(0.3f);
                int got = Output(g, r, st) - before;
                if (c.Check(got == r.amount, $"{piece}: {r.name} delivers {got} of {r.amount}")) made++;
                if (id == "forge_axe") c.Note("forged axe make: " + g.QualityName("tool_axe"));
            }
            c.Metric("rungs_made", made, "");
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);
            yield return new WaitForSeconds(1.2f);
            c.Screenshot("metal_ladder_shops");
            yield return null;

            // ---- the kits on the Sedan
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("the Sedan is not in the start fleet"); yield break; }
            var tune = v.GetComponent<VehicleTuning>();
            var sys = v.GetComponent<VehicleSystems>();
            if (!c.Check(tune && sys, "the Sedan has tuning and systems")) yield break;
            tune.Apply();
            var stock = (float[])v.gears.Clone();
            float stockBrake = v.brakeForce, stockTravel = v.travel, stockRide = v.rideHeight, stockCap = sys.fuelCapacity;
            bool stockAwd = v.awdSelectable;
            tune.Card(out _, out _, out float stockTop);
            g.Stats.skillXp[(int)MadMax.RPG.Skill.Mechanics] = Mathf.Max(g.Stats.skillXp[(int)MadMax.RPG.Skill.Mechanics], 40f * 25f);
            c.Fixture("Mechanics raised to level 5 (kits need 1-4)");

            int n = stock.Length, kitsBefore = g.Inventory.GetItem("kit_gearbox_close");
            bool fitted = g.FitMetalKit(tune, VehicleTuning.SlotFor("kit_gearbox_close"));
            c.Check(fitted && tune.gearbox == 1, "the machined close-ratio gearbox fits through the tuning kits");
            c.Check(g.Inventory.GetItem("kit_gearbox_close") == kitsBefore - 1, "the kit left the pack");
            var close = (float[])v.gears.Clone();
            tune.Card(out _, out _, out float closeTop);
            c.Note("gears stock " + string.Join(" ", stock.Select(x => x.ToString("0.00"))) + " | close " + string.Join(" ", close.Select(x => x.ToString("0.00"))));
            c.Metric("top_ratio_close_vs_stock", close[n - 1] / stock[n - 1], "x");
            c.Metric("top_speed_stock", stockTop, "km/h"); c.Metric("top_speed_close", closeTop, "km/h");
            c.Check(Enumerable.Range(0, n).All(k => close[k] >= stock[k] * 0.999f), "close ratio: every gear at least as short as stock (pulls as hard or harder)");
            c.Check(close[n - 1] >= stock[n - 1] * 1.08f, $"close ratio: top gear {close[n - 1] / stock[n - 1] * 100f - 100f:0}% shorter");
            c.Check(close[0] / close[n - 1] < stock[0] / stock[n - 1] * 0.95f, "close ratio: the spread first/top closes up");

            g.Inventory.AddItem("kit_gearbox_wide");
            c.Fixture("a wide-ratio gearbox kit put in the pack");
            fitted = g.FitMetalKit(tune, VehicleTuning.SlotFor("kit_gearbox_wide"));
            c.Check(fitted && tune.gearbox == 2 && g.Inventory.GetItem("kit_gearbox_close") == kitsBefore, "the wide-ratio kit swaps in, the close-ratio kit comes back");
            var wide = (float[])v.gears.Clone();
            tune.Card(out _, out _, out float wideTop);
            c.Metric("first_ratio_wide_vs_stock", wide[0] / stock[0], "x"); c.Metric("top_speed_wide", wideTop, "km/h");
            c.Check(wide[0] >= stock[0] * 1.08f && wide[n - 1] <= stock[n - 1] * 0.92f, "wide ratio: a lower crawler first and a taller overdrive top");

            c.Check(tune.FitKit("kit_brakes_hd") && Mathf.Abs(v.brakeForce - stockBrake * 1.35f) < 1f, $"heavy-duty brakes: {stockBrake:0} → {v.brakeForce:0} N");
            c.Check(tune.FitKit("kit_lift") && Mathf.Abs(v.rideHeight - stockRide - 0.1f) < 0.001f && Mathf.Abs(v.travel - stockTravel * 1.25f) < 0.001f, $"lift kit: ride +{(v.rideHeight - stockRide) * 100f:0} cm, travel {stockTravel:0.00} → {v.travel:0.00} m");
            c.Check(tune.FitKit("kit_long_range_tank") && sys.fuelCapacity >= stockCap + 30f, $"long-range tank: {stockCap:0} → {sys.fuelCapacity:0} L");
            if (!stockAwd && v.drive != VehicleDriver.Drive.All)
            {
                c.Check(tune.FitKit("kit_transfer_case") && v.awdSelectable, "transfer case: the rear-drive Sedan gets selectable 4WD");
                v.ToggleFourWheelDrive();
                c.Check(v.FourWheelDrive, "4WD engages");
                v.ToggleFourWheelDrive();
            }
            else c.Note("the Sedan is already four-wheel drive: transfer case skipped");

            string saved = tune.SaveState();
            c.Check(!string.IsNullOrEmpty(saved) && saved.Split(';').Length >= 19, "kits are in the tuning save: " + saved);
            var kitted = (float[])v.gears.Clone(); float kittedCap = sys.fuelCapacity, kittedBrake = v.brakeForce;
            tune.gearbox = 0; tune.transferCase = tune.hdBrakes = tune.longRange = false; tune.suspension = 0; tune.Apply();
            c.Check(Enumerable.Range(0, n).All(k => Mathf.Abs(v.gears[k] - stock[k]) < 0.001f) && Mathf.Abs(v.brakeForce - stockBrake) < 1f && Mathf.Abs(sys.fuelCapacity - stockCap) < 0.01f && v.awdSelectable == stockAwd,
                "kits off: back to the design values exactly");
            tune.LoadState(saved);
            c.Check(tune.gearbox == 2 && Enumerable.Range(0, n).All(k => Mathf.Abs(v.gears[k] - kitted[k]) < 0.001f) && Mathf.Abs(sys.fuelCapacity - kittedCap) < 0.01f && Mathf.Abs(v.brakeForce - kittedBrake) < 1f,
                "loading the tuning save restores every kit");

            // ---- horseshoes from the forge on a kept horse
            var horseDef = MadMax.Animals.AnimalLibrary.Get("horse");
            if (horseDef == null) { c.Note("no horse species: shoeing skipped"); yield break; }
            var hp = g.Player.transform.position + right * 1.5f;
            hp.y = MadMax.World.DeformableTerrain.Instance.Height(hp.x, hp.z);
            var horse = MadMax.Animals.Animal.Spawn(horseDef, hp, 0f, g.propMaterial, "t:metal");
            horse.owned = true;
            c.Fixture("an owned horse beside the player");
            yield return new WaitForSeconds(0.3f);
            g.Inventory.AddItem(MetalItems.Horseshoes);
            g.UseItem(MetalItems.Horseshoes);
            c.Check(g.IsShod(horse) && g.Inventory.GetItem(MetalItems.Horseshoes) == 0, "horseshoes: used beside the horse, it is shod");

            // the kitted Sedan (lifted, long-range tank, wide-ratio box) parked in view
            if (TestWorld.Pad(5f, out var pad2))
            {
                yield return TestWorld.Place(c, v, pad2, fwd, 1.2f);
                if (rig) rig.SetTarget(v.transform);
                yield return new WaitForSeconds(1f);
                c.Screenshot("metal_ladder_sedan");
                yield return null;
                if (rig) rig.SetTarget(g.Player.transform);
            }
        }

        static int Output(WastelandGame g, Recipe r, CraftingStation st) =>
            r.kind == OutputKind.Resource ? g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) : g.Inventory.GetItem(r.output) + st.tray.GetItem(r.output);
    }
}
