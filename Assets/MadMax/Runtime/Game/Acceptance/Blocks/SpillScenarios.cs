using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Liquids out in the open (<see cref="Spills"/>, <see cref="FluidStore"/>, taps, pumps, one-way pipes): soil
    /// soaks a spill up to its capacity and only then a pool stands, asphalt takes nothing; evaporation by liquid,
    /// temperature, sun, air; pools run downhill into a lined hole; fuel trails burn, water puts flames out; built stores
    /// fill, refuse a second liquid, drain and spill when broken; one-way pipes run one way and not uphill; taps run onto
    /// the ground, a transfer pump draws a puddle in and pumps it out; beds drink standing water; leaks drip; saves keep it.</summary>
    public static class SpillScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new SpillSoak();
            yield return new SpillEvaporation();
            yield return new SpillPond();
            yield return new SpillFire();
            yield return new SpillStores();
            yield return new SpillOneWay();
            yield return new SpillTapPump();
            yield return new SpillGarden();
            yield return new SpillLeak();
            yield return new SpillSave();
        }

        internal static float Sum(Vector3 at, float r, System.Func<Spills.Cell, float> f)
        {
            float s = 0f;
            foreach (var c in Spills.Cells)
            {
                float dx = (c.ix + 0.5f) * Spills.CellSize - at.x, dz = (c.iz + 0.5f) * Spills.CellSize - at.z;
                if (dx * dx + dz * dz <= r * r) s += f(c);
            }
            return s;
        }

        internal static float Capacity(Vector3 p) { DeformableTerrain.Instance.SeepAt(p.x, p.z, out float cap, out _); return cap; }

        /// <summary>A level spot of the paved start yard (nothing soaks in), away from <paramref name="not"/>.</summary>
        internal static bool Paved(out Vector3 at, Vector3? not = null, float apart = 12f)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            g.World.Yard(out var origin, out var along, out var side);
            for (int k = 0; k < 40; k++)
            foreach (float s in new[] { 0f, 3f, -3f })
            {
                var p = origin + along * ((k % 2 == 0 ? 1 : -1) * (k / 2) * 3f) + side * s;
                if (Capacity(p) > 0f || t.WaterDepth(p.x, p.z) > 0f) continue;
                if (not.HasValue && Vector3.Distance(p, not.Value) < apart) continue;
                if (Vector3.Angle(t.Normal(p.x, p.z), Vector3.up) > 2.5f) continue;
                at = new Vector3(p.x, t.Height(p.x, p.z), p.z);
                return true;
            }
            at = default; return false;
        }

        /// <summary>A level, clear spot of open soil (it soaks liquid up), away from <paramref name="not"/>.</summary>
        internal static bool Soil(float radius, out Vector3 at, Vector3? not = null)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            var focus = g.Player.transform.position;
            for (int k = 0; k < 300; k++)
            {
                float a = k * 2.39996f, r = 8f + k * 0.4f;
                var p = focus + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (Capacity(p) < 3f || (not.HasValue && Vector3.Distance(p, not.Value) < 14f)) continue;
                if (Vector3.Angle(t.Normal(p.x, p.z), Vector3.up) > 2.5f || t.WaterDepth(p.x, p.z) > 0f) continue;
                if (Physics.CheckSphere(new Vector3(p.x, t.Height(p.x, p.z) + radius + 0.2f, p.z), radius, ~0, QueryTriggerInteraction.Ignore)) continue;
                at = new Vector3(p.x, t.Height(p.x, p.z), p.z);
                return true;
            }
            at = default; return false;
        }

        internal static Placeable Place(WastelandGame g, string id, Vector3 at, float yaw = 0f)
        {
            at.y = DeformableTerrain.Instance.Height(at.x, at.z);
            return FurnitureLibrary.Spawn(id, g.Build.Structures, at, Quaternion.Euler(0f, yaw, 0f), g.propMaterial);
        }

        internal static IEnumerator Wait(float seconds) { float t0 = Time.time; while (Time.time - t0 < seconds) yield return null; }
    }

    /// <summary>10 L of water on the paved yard spreads into a thin film and none of it soaks in; on open soil it soaks
    /// in; 60 L on soil leaves a pool only where the soil under it is full.</summary>
    class SpillSoak : Scenario
    {
        public override string Id => "spill.soak";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            Spills.Clear();
            if (!SpillScenarios.Paved(out var hard)) { c.Block("no paved spot in the yard"); yield break; }
            if (!SpillScenarios.Soil(1f, out var soft, hard)) { c.Block("no open soil near the start"); yield break; }
            c.Fixture($"asphalt at {hard.x:0},{hard.z:0} (soaks {SpillScenarios.Capacity(hard):0.#} L/m²), soil at {soft.x:0},{soft.z:0} (soaks {SpillScenarios.Capacity(soft):0.#} L/m²)");
            Spills.Pour(hard, ResourceType.Water, 10f);
            Spills.Pour(soft, ResourceType.Water, 10f);
            Spills.FastForward(0f, 20f);
            float hPool = SpillScenarios.Sum(hard, 8f, x => x.pool), hSoak = SpillScenarios.Sum(hard, 8f, x => x.Soaked);
            int hCells = Spills.Cells.Count(x => x.pool > 0.05f && Vector3.Distance(x.Centre, hard) < 8f);
            float sPool = SpillScenarios.Sum(soft, 8f, x => x.pool), sSoak = SpillScenarios.Sum(soft, 8f, x => x.Soaked);
            c.Metric("asphalt_cells", hCells, ""); c.Metric("soil_soaked", sSoak, "L");
            c.Check(hSoak < 0.01f && hPool > 9.9f, $"asphalt takes nothing: {hPool:0.00} L standing, {hSoak:0.00} L soaked");
            c.Check(hCells >= 12, $"on the flat it runs out into a thin film ({hCells} cells of 0.5 m)");
            c.Check(sSoak > 2f && Mathf.Abs(sPool + sSoak - 10f) < 0.2f, $"soil drinks it: {sSoak:0.0} L soaked, {sPool:0.0} L standing (no litre lost)");
            // a hole dug in the soil (unlined) and filled: the soil under it fills first, then the water stands
            Spills.Clear();
            DeformableTerrain.Instance.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, soft, 1.2f, 0.5f, 0);
            Spills.RefreshAround(soft, 3f);
            float perCell = SpillScenarios.Capacity(soft) * Spills.Area * 1.5f;
            float poured = perCell * 24f + 40f;
            Spills.Pour(soft, ResourceType.Water, poured);
            Spills.FastForward(0f, 240f);
            var pooled = Spills.Cells.Where(x => x.pool > 1.2f).ToList();
            int full = pooled.Count(x => x.Soaked >= x.capacity * 0.95f);
            c.Check(pooled.Count > 0 && full == pooled.Count, $"{poured:0} L in a dug hole: a pool stands on {pooled.Count} cells, the soil under {full} of them is full ({SpillScenarios.Sum(soft, 4f, x => x.Soaked):0} L soaked)");
            c.Screenshot("soak");
            yield return null;
        }
    }

    /// <summary>The same 10 L left for half an hour of game time: petrol is gone first, water partly, engine oil not at all;
    /// a 2-stroke blend loses its petrol and keeps its oil; hot, sunny, dry air dries faster, rain and shade hardly.</summary>
    class SpillEvaporation : Scenario
    {
        public override string Id => "spill.evaporation";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            if (!SpillScenarios.Paved(out var at)) { c.Block("no paved spot in the yard"); yield break; }
            float Left(FluidMix m, out FluidMix after)
            {
                Spills.Clear();
                Spills.Pour(at, m, 10f);
                Spills.FastForward(0f, 15f);
                Spills.FastForward(0.5f, 1f);
                after = null;
                float best = 0f;
                foreach (var cell in Spills.Cells) if (cell.pool > best) { best = cell.pool; after = cell.mix.Clone(); }
                return SpillScenarios.Sum(at, 10f, x => x.pool);
            }
            float water = Left(new FluidMix(ResourceType.Water), out _), petrol = Left(new FluidMix(ResourceType.Fuel), out _), oil = Left(new FluidMix(ResourceType.Oil), out _);
            float twoStroke = Left(FluidsScenarios.Mix((ResourceType.Fuel, 0.9f), (ResourceType.Oil, 0.1f)), out var rest);
            c.Metric("water_left", water, "L"); c.Metric("petrol_left", petrol, "L"); c.Metric("oil_left", oil, "L");
            c.Note($"evaporation now: {Spills.EvapMM(at, false):0.00} mm/h for water (temp {Weather.TemperatureAt(at.z):0} C, hour {DayNight.Hours:0.0})");
            c.Check(oil > 9.95f, $"engine oil does not evaporate ({oil:0.00} of 10 L)");
            c.Check(water < 9.9f && water > 0.5f, $"water dries slowly ({water:0.00} of 10 L after half an hour)");
            c.Check(petrol < water * 0.5f, $"petrol goes much faster ({petrol:0.00} L vs {water:0.00} L water)");
            c.Check(rest != null && rest[ResourceType.Oil] > 0.5f, $"2-stroke mix: the petrol goes, the oil stays ({twoStroke:0.00} L left, {(rest != null ? rest.Assay() : "none")})");
            var t = DeformableTerrain.Instance;
            float hot = Spills.EvapMM(at.x, at.z, 35f, 1f, 0f, 1f, false, false, t), cold = Spills.EvapMM(at.x, at.z, 5f, 1f, 0f, 1f, false, false, t);
            float shade = Spills.EvapMM(at.x, at.z, 25f, 1f, 0f, 1f, true, false, t), sun = Spills.EvapMM(at.x, at.z, 25f, 1f, 0f, 1f, false, false, t);
            float night = Spills.EvapMM(at.x, at.z, 25f, 0f, 0f, 1f, false, false, t), rain = Spills.EvapMM(at.x, at.z, 25f, 1f, 1f, 1f, false, true, t);
            c.Check(hot > cold * 2.5f, $"heat: {hot:0.00} mm/h at 35 C vs {cold:0.00} at 5 C");
            c.Check(sun > shade * 2f && sun > night * 2f, $"sun: {sun:0.00} mm/h in the open vs {shade:0.00} under a roof, {night:0.00} at night");
            c.Check(rain < sun * 0.15f, $"rain: {rain:0.000} mm/h");
            Spills.Clear();
            yield return null;
        }
    }

    /// <summary>A hole dug in the soil and lined with a pond liner: water poured at its rim runs down into it and stays
    /// (nothing soaks away).</summary>
    class SpillPond : Scenario
    {
        public override string Id => "spill.pond";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            Spills.Clear();
            if (!SpillScenarios.Soil(2f, out var at)) { c.Block("no open soil near the start"); yield break; }
            t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, at, 1.3f, 0.6f, 0);
            yield return null;
            var liner = SpillScenarios.Place(g, "pond_liner", at);
            if (!c.Check(liner && liner.GetComponent<PondLiner>(), "lay a pond liner in the hole")) yield break;
            c.Fixture($"a hole 0.6 m deep at {at.x:0},{at.z:0}, lined");
            yield return null; yield return null;
            var rim = at + new Vector3(1.2f, 0f, 0f);
            Spills.Pour(rim, ResourceType.Water, 40f);
            Spills.FastForward(0f, 40f);
            float inner = SpillScenarios.Sum(at, 0.8f, x => x.pool), all = SpillScenarios.Sum(at, 4f, x => x.pool), soaked = SpillScenarios.Sum(at, 1.4f, x => x.Soaked);
            float dCentre = Spills.DepthAt(at.x, at.z), dRim = Spills.DepthAt(rim.x, rim.z);
            c.Metric("pond_depth", dCentre * 1000f, "mm");
            c.Check(inner > all * 0.4f && dCentre > dRim + 0.02f, $"it ran down into the hole: {inner:0.0} of {all:0.0} L in the middle, {dCentre * 1000f:0} mm deep there vs {dRim * 1000f:0} mm at the rim");
            c.Check(soaked < 0.5f, $"the liner keeps it ({soaked:0.00} L soaked inside)");
            g.Player.Teleport(at + new Vector3(0f, 0f, -4f), 0f);
            yield return SpillScenarios.Wait(0.8f);
            c.Screenshot("pond");
            yield return null;
        }
    }

    /// <summary>A petrol trail lit at one end burns along to the other; a flame in a water puddle goes out.</summary>
    class SpillFire : Scenario
    {
        public override string Id => "spill.fire";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            Spills.Clear();
            if (!SpillScenarios.Paved(out var a)) { c.Block("no paved spot in the yard"); yield break; }
            var dir = Vector3.right;
            for (int i = 0; i <= 8; i++) Spills.Pour(a + dir * (i * 0.5f), ResourceType.Fuel, 0.6f);
            Spills.FastForward(0f, 3f);
            var end = a + dir * 4f;
            float before = SpillScenarios.Sum(end, 0.8f, x => x.pool);
            Fire.Ignite(a + Vector3.up * 0.1f, null, 4f, 0.6f);
            int nearEnd = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 8f)
            {
                foreach (var f in Fire.All) if (f && Vector3.Distance(f.transform.position, end) < 1.3f) nearEnd++;
                yield return null;
            }
            float after = SpillScenarios.Sum(end, 0.8f, x => x.pool);
            c.Check(nearEnd > 0 || after < before * 0.5f, $"the flame ran 4 m along the trail ({before:0.00} L at the far end before, {after:0.00} L after; fire seen there in {nearEnd} frames)");
            c.Screenshot("trail");
            yield return null;
            // water on the ground puts a fire in it out
            if (!SpillScenarios.Paved(out var w, a, 10f)) { c.Note("no second paved spot for the water check"); yield break; }
            Spills.Pour(w, ResourceType.Water, 8f);
            Spills.FastForward(0f, 2f);
            var fire = Fire.Ignite(w + Vector3.up * 0.05f, null, 6f, 0.5f);
            yield return SpillScenarios.Wait(4f);
            c.Check(!fire || fire.fuel <= 0.5f, "a fire in a water puddle goes out" + (fire ? $" (fuel {fire.fuel:0.0} s)" : ""));
            Spills.Clear();
        }
    }

    /// <summary>Built stores: a bucket poured into the makeshift pool (G radial) joins its water; a drum takes petrol and
    /// refuses water on top; the pool's drain runs it onto the ground; a pool left a day in the sun loses some; a broken
    /// drum spills its petrol.</summary>
    class SpillStores : Scenario
    {
        public override string Id => "spill.stores";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            Spills.Clear();
            if (g.Current) { g.Exit(); yield return SpillScenarios.Wait(0.4f); }
            if (!TestWorld.Pad(3f, out var pad)) { c.Block("no level pad"); yield break; }
            var pool = SpillScenarios.Place(g, "pool_tarp", pad)?.GetComponent<FluidStore>();
            var drum = SpillScenarios.Place(g, "storage_drum", pad + new Vector3(3f, 0f, 0f))?.GetComponent<FluidStore>();
            if (!c.Check(pool && drum, "a makeshift pool and a storage drum")) yield break;
            c.Fixture("a makeshift pool and a drum on a pad");
            yield return SpillScenarios.Wait(0.6f);
            // a bucket of water through the pour radial
            if (g.Inventory.GetItem(FluidContainers.Bucket) <= 0) g.Inventory.AddItem(FluidContainers.Bucket);
            g.Player.Teleport(pad + new Vector3(0f, 0f, -1.9f), 0f);
            yield return null;
            c.Check(g.EquipCan(FluidContainers.Bucket), "a bucket in hand");
            var can = g.HeldCan; can.mix.Set(ResourceType.Water); can.litres = 10f;
            yield return null;
            g.OpenFluidChoice(false);
            c.Note("pour radial: " + FluidsScenarios.Slices(g));
            c.Check(g.PickRadial("POOL"), "the radial offers the pool");
            yield return null;
            c.Check(Mathf.Abs(pool.Contents - 10f) < 0.2f && pool.OnNet, $"the pool holds the bucket's 10 L as network water ({pool.Contents:0.0} L, {pool.Mix.Label()})");
            // the drum: petrol, then water refused
            c.Check(drum.Pour(new FluidMix(ResourceType.Fuel), 50f) > 49.9f && !drum.OnNet, $"the drum takes 50 L of petrol ({drum.Contents:0} L {drum.Mix.Label()})");
            string why = drum.Refuse(new FluidMix(ResourceType.Water));
            c.Check(why != null, "water on top of petrol is refused: " + why);
            // drain the pool onto the ground
            pool.Pour(new FluidMix(ResourceType.Water), 20f);
            float had = pool.Contents;
            pool.Use(g, true);
            yield return SpillScenarios.Wait(6f);
            float ground = SpillScenarios.Sum(pool.transform.TransformPoint(pool.drain), 6f, x => x.pool + x.Soaked);
            c.Check(pool.Contents < had - 8f && ground > 8f, $"the drain runs it out: {had:0} → {pool.Contents:0} L, {ground:0.0} L on the ground");
            if (pool.draining) pool.Use(g, true);
            // a day in the open
            pool.Pour(new FluidMix(ResourceType.Water), 500f - pool.Contents);
            yield return SpillScenarios.Wait(0.6f);
            float full = pool.Contents;
            pool.Weathering(24f);
            c.Check(pool.Contents < full - 1f && pool.Contents > full * 0.7f, $"a day in the open: {full:0} → {pool.Contents:0} L");
            // break the drum: its petrol goes on the ground
            var dp = drum.transform.position;
            var pl = drum.GetComponent<Placeable>();
            for (int i = 0; i < 20 && pl; i++) { pl.ApplyHit(dp + Vector3.up * 0.4f, Vector3.forward, 3f, 0.3f, null); yield return null; }
            yield return SpillScenarios.Wait(0.5f);
            float petrol = SpillScenarios.Sum(dp, 6f, x => x.pool * x.mix[ResourceType.Fuel] + x.soakFuel);
            c.Check(!pl && petrol > 30f, $"the broken drum spills its petrol ({petrol:0} L on and in the ground)");
            c.Screenshot("stores");
            yield return null;
        }
    }

    /// <summary>A one-way pipe between two tanks: water runs from the first to the second, never back, and not uphill
    /// without a pump.</summary>
    class SpillOneWay : Scenario
    {
        public override string Id => "spill.oneway";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!TestWorld.Pad(4f, out var pad)) { c.Block("no level pad"); yield break; }
            var a = SpillScenarios.Place(g, "water_tank", pad + new Vector3(-2f, 0f, 0f))?.GetComponent<UtilityNode>();
            var b = SpillScenarios.Place(g, "water_tank", pad + new Vector3(2f, 0f, 0f))?.GetComponent<UtilityNode>();
            if (!c.Check(a && b, "two water tanks")) yield break;
            yield return null;
            a.Link(b, UtilityKind.Water | UtilityKind.OneWay);
            a.clean = 400f; b.clean = 0f;
            yield return SpillScenarios.Wait(3f);
            c.Metric("oneway_litres", b.Water, "L");
            c.Check(b.Water > 2f && a.Water < 400f - 2f, $"downstream it runs: {b.Water:0.0} L arrived, the first tank holds {a.Water:0.0} L");
            a.clean = 0f; a.dirty = 0f; b.clean = 300f;
            yield return SpillScenarios.Wait(2.5f);
            c.Check(a.Water < 0.5f, $"never back: the first tank stays empty ({a.Water:0.00} L)");
            // lift the second tank 2 m: gravity can't push water up
            b.transform.position += Vector3.up * 2f;
            a.clean = 400f; b.clean = 0f; b.dirty = 0f;
            yield return SpillScenarios.Wait(2.5f);
            c.Check(b.Water < 0.5f, $"not uphill without a pump ({b.Water:0.00} L arrived)");
            b.transform.position -= Vector3.up * 2f;
            c.Screenshot("oneway");
            yield return null;
        }
    }

    /// <summary>A tap on a tank left running makes a puddle under its spout; a transfer pump cranked by hand draws a
    /// puddle into its pipes, then (OUT) pumps them back onto the ground.</summary>
    class SpillTapPump : Scenario
    {
        public override string Id => "spill.tap_pump";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            Spills.Clear();
            if (!SpillScenarios.Paved(out var pad)) { c.Block("no paved spot in the yard"); yield break; }
            var tank = SpillScenarios.Place(g, "water_tank", pad + new Vector3(-2.5f, 0f, 0f))?.GetComponent<UtilityNode>();
            var tapP = SpillScenarios.Place(g, "water_tap", pad);
            var tap = tapP ? tapP.GetComponent<WaterTap>() : null;
            if (!c.Check(tank && tap, "a water tank and a tap")) yield break;
            yield return null;
            tank.Link(tap.GetComponent<UtilityNode>(), UtilityKind.Water);
            tank.clean = 200f;
            yield return SpillScenarios.Wait(1f);
            tap.Use(g, true);
            yield return SpillScenarios.Wait(5f);
            var spout = tap.transform.TransformPoint(tap.spout);
            float under = SpillScenarios.Sum(spout, 3f, x => x.pool + x.Soaked);
            c.Check(tap.running && under > 0.8f, $"the running tap makes a puddle ({under:0.0} L under the spout)");
            tap.Use(g, true);
            // the transfer pump: hand crank, IN from a puddle, OUT onto the ground
            Spills.Clear();
            if (!SpillScenarios.Paved(out var pad2, pad, 8f)) { c.Block("no second paved spot"); yield break; }
            var pumpP = SpillScenarios.Place(g, "transfer_pump", pad2);
            var tank2 = SpillScenarios.Place(g, "water_tank", pad2 + new Vector3(-2.5f, 0f, 0f))?.GetComponent<UtilityNode>();
            var pump = pumpP ? pumpP.GetComponent<TransferPump>() : null;
            if (!c.Check(pump && tank2, "a transfer pump and an empty tank")) yield break;
            yield return null;
            pump.GetComponent<UtilityNode>().Link(tank2, UtilityKind.Water);
            yield return SpillScenarios.Wait(1f);
            Spills.Pour(pump.HoseEnd, ResourceType.Water, 20f);
            Spills.FastForward(0f, 2f);
            for (int i = 0; i < 3; i++) { pump.Use(g, false); yield return SpillScenarios.Wait(0.6f); }
            float inPipes = UtilityGrid.NetWater(tank2, out _);
            c.Check(inPipes > 5f, $"cranked IN: {inPipes:0.0} L drawn from the puddle into the pipes ({pump.lastSource})");
            pump.Use(g, true);
            c.Check(pump.mode == TransferPump.Mode.Out, "[T] switches it to OUT");
            float ground0 = Spills.TotalPool + Spills.TotalSoaked;
            pump.Use(g, false);
            yield return SpillScenarios.Wait(0.6f);
            float ground1 = Spills.TotalPool + Spills.TotalSoaked;
            c.Check(ground1 > ground0 + 2f, $"cranked OUT: {ground1 - ground0:0.0} L back onto the ground ({pump.lastSource})");
            yield return null;
        }
    }

    /// <summary>A dry garden bed under a puddle of water drinks it; petrol poured on a bed hurts the crop.</summary>
    class SpillGarden : Scenario
    {
        public override string Id => "spill.garden";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            Spills.Clear();
            if (!SpillScenarios.Paved(out var pad)) { c.Block("no paved spot in the yard"); yield break; }
            var wet = SpillScenarios.Place(g, "planter", pad)?.GetComponent<GardenPlot>();
            var oily = SpillScenarios.Place(g, "planter", pad + new Vector3(4f, 0f, 0f))?.GetComponent<GardenPlot>();
            if (!c.Check(wet && oily, "two planters")) yield break;
            yield return null;
            wet.water = 0.05f; oily.health = 1f;
            Spills.Pour(wet.transform.position, ResourceType.Water, 6f);
            Spills.Pour(oily.transform.position, ResourceType.Fuel, 3f);
            yield return SpillScenarios.Wait(5f);
            c.Check(wet.water > 0.15f, $"the bed drinks the standing water (water {wet.water:0.00})");
            c.Check(oily.health < 1f, $"petrol hurts the bed (health {oily.health:0.00})");
            yield return null;
        }
    }

    /// <summary>A car without its radiator pours coolant on the ground under its nose.</summary>
    class SpillLeak : Scenario
    {
        public override string Id => "spill.leak";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            Spills.Clear();
            VehicleSystems sys = null; MountSocket radiator = null;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.driveable || v.aiDriven || !v.TryGetComponent<VehicleSystems>(out var s) || !s.usesCoolant || s.coolant < 2f) continue;
                foreach (var m in v.GetComponentsInChildren<MountSocket>()) if (m.accepts == PartCategory.Radiator && m.Current) { radiator = m; break; }
                if (radiator) { sys = s; break; }
            }
            if (!sys) { c.Block("no car with a radiator in the fleet"); yield break; }
            c.Fixture(sys.name + " with its radiator taken off");
            radiator.Detach(true);
            yield return SpillScenarios.Wait(4f);
            float coolant = SpillScenarios.Sum(sys.transform.position, 5f, x => x.pool * x.mix[ResourceType.Coolant] + x.soakToxic);
            c.Check(coolant > 0.5f, $"coolant on the ground under it ({coolant:0.0} L)");
            yield return null;
        }
    }

    /// <summary>Spills survive a save: the same litres, soak and liquids come back.</summary>
    class SpillSave : Scenario
    {
        public override string Id => "spill.save";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            Spills.Clear();
            if (!SpillScenarios.Paved(out var at)) { c.Block("no paved spot in the yard"); yield break; }
            Spills.Pour(at, ResourceType.Oil, 5f);
            Spills.Pour(at + Vector3.right * 6f, ResourceType.Coolant, 3f);
            Spills.FastForward(0f, 5f);
            float pool = Spills.TotalPool, soak = Spills.TotalSoaked; int cells = Spills.Count;
            var lines = new List<string>();
            Spills.Save(lines, "s\u001f");
            Spills.Clear();
            foreach (var l in lines) Spills.Load(l.Substring(2));
            c.Check(Mathf.Abs(Spills.TotalPool - pool) < 0.05f && Mathf.Abs(Spills.TotalSoaked - soak) < 0.05f && Spills.Count == cells,
                    $"{lines.Count} lines: {Spills.TotalPool:0.00}/{pool:0.00} L standing, {Spills.TotalSoaked:0.00}/{soak:0.00} L soaked, {Spills.Count}/{cells} cells");
            var oil = Spills.CellAt(at.x, at.z);
            c.Check(oil != null && oil.mix[ResourceType.Oil] > 0.99f, "the oil is still oil: " + (oil != null ? oil.mix.Label() : "none"));
            Spills.Clear();
            yield return null;
        }
    }
}
