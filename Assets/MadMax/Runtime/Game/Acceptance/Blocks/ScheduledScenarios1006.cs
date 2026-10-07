using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-06: frozen wreck tanks, vague rumours from strangers, fire spreading through a
    /// halted column, the trip kit named in emergencies, companions with a bottle, scavenger hauls carried to market.</summary>
    public static class ScheduledScenarios1006
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new FrozenTank();
            yield return new VagueRumours();
            yield return new FireSpread();
            yield return new KitAlarm();
            yield return new CompanionFire();
            yield return new HaulToMarket();
        }

        /// <summary>Put the extinguishers of the pack away for a test; returns how many there were.</summary>
        internal static int StashBottles(WastelandGame g)
        {
            int n = g.Inventory.GetItem(Companions.Extinguisher);
            if (n > 0) g.Inventory.TakeItem(Companions.Extinguisher, n);
            return n;
        }

        internal static Building.Container Box(VehicleDriver v)
        {
            var s = VehicleStorage.For(v);
            return s && s.compartments.Count > 0 ? s.compartments[0].container : null;
        }
    }

    /// <summary>A winter wreck's tank is iced: the siphon finds nothing to draw from it, warmth thaws it, and the ice
    /// survives a save; scavengers in winter leave the tank iced.</summary>
    class FrozenTank : Scenario
    {
        public override string Id => "vehicle.frozen_tank";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            int season = Weather.Season;
            var wreck = g.SpawnRoadWreck("Coupe", pad, Quaternion.identity, 41);
            if (!c.Check(wreck, "a road wreck")) yield break;
            var sys = wreck.GetComponent<VehicleSystems>();
            sys.fuel = 12f; sys.fuelMix.Set(ResourceType.Fuel);
            Weather.Season = 2;
            int taken = g.Scavenge(pad, 3f, 7);
            Weather.Season = season;
            c.Check(sys.tankIced && sys.fuel >= 11.9f, $"winter scavengers left the tank full and iced ({sys.fuel:0.0} L, took {taken} parts)");
            yield return null;

            if (g.Inventory.GetItem(FluidContainers.JerryCan) <= 0) g.Inventory.AddItem(FluidContainers.JerryCan);
            var cans = g.CansOf(FluidContainers.JerryCan);
            if (!cans[0].Empty) cans[0].Clear();
            if (!(g.Player.Tool && g.Player.Tool.id == FluidContainers.JerryCan)) g.UseItem(FluidContainers.JerryCan);
            yield return null;
            FluidsScenarios.Beside(g, wreck);
            yield return null; yield return null;
            c.Fixture("a winter road wreck with 12 L of petrol, an empty jerry can in hand beside it");
            g.OpenFluidChoice(true);
            c.Note("siphon radial: " + FluidsScenarios.Slices(g) + " / toast: " + g.ToastText);
            c.Check(!g.RadialActions.Any(a => a.label.StartsWith("FUEL TANK")), "the iced tank is not offered");
            c.Check(g.RadialActions.Count > 0 || g.ToastText == WastelandGame.IcedNote, "nothing else to draw says why: " + (g.ToastText ?? "(other ends offered)"));
            g.CancelFluidChoice();
            c.Check(sys.fuel >= 11.9f, "nothing left the tank");

            var save = JsonUtility.FromJson<VehicleSave>(JsonUtility.ToJson(new VehicleSave { iced = sys.tankIced }));
            c.Check(save.iced, "the ice is saved with the vehicle");

            int heats = 0;
            while (sys.tankIced && heats < 10) { sys.Heat(0.25f); heats++; }
            c.Metric("heat_to_thaw", heats * 0.25f, "");
            c.Check(!sys.tankIced && !sys.Burning, $"fire heat thawed it before it caught ({heats * 0.25f:0.00} heat)");
            yield return null;
            g.OpenFluidChoice(true);
            c.Note("after the thaw: " + FluidsScenarios.Slices(g));
            bool offered = g.RadialActions.Any(a => a.label.StartsWith("FUEL TANK")) || g.Working || cans[0].litres > 0f;
            c.Check(offered, "the thawed tank can be siphoned");
            if (g.FluidChoiceOpen) g.CancelFluidChoice();
            if (g.Working) g.CancelWork("TEST");
            float t = VehicleSystems.AirThawSeconds;
            c.Check(t >= 60f && t <= 600f, $"above freezing the air thaws it in {t:0} s");
            Object.Destroy(wreck.gameObject);
        }
    }

    /// <summary>A stranger who doesn't trust the player only gives a direction: the giver is pinned as a search circle
    /// that holds the real place; a trusted teller sharpens it to a point. Both are saved.</summary>
    class VagueRumours : Scenario
    {
        public override string Id => "talk.vague_rumours";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var at = g.Player.transform.position;
            string line = g.GiverRumour(at, false);
            c.Note(line ?? "(none)");
            if (line == null) { c.Block("no unpinned story giver in reach of the start"); yield break; }
            c.Check(line.Contains("SOMEWHERE") && !line.Contains(" KM") && !line.Contains("METRES"), "a direction, no distance: " + line);
            c.Check(g.HeardVague.Count == 1 && g.HeardOf.Count == 0, "heard of vaguely, not pinned exactly");
            string key = g.HeardVague.First();
            var pins = new List<WastelandGame.Pin>();
            g.JobPins(pins);
            var circle = pins.FirstOrDefault(p => p.radius > 0f);
            if (!c.Check(circle.label != null, "a search circle on the map: " + circle.label)) yield break;
            var m = MadMax.Story.StoryCast.All.FirstOrDefault(x => x.key == key);
            var real = MadMax.Story.StoryAnchors.Get(m.anchor);
            float off = Vector2.Distance(new Vector2(circle.pos.x, circle.pos.z), new Vector2(real.x, real.z));
            c.Metric("circle_offset", off, "m");
            c.Check(off > 30f && off < circle.radius, $"the circle holds the place without centring on it ({off:0} m off, radius {circle.radius:0} m)");
            c.Check(g.GiverRumour(at, false) != line, "a stranger doesn't repeat the same giver");
            var d = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { heardVague = new List<string>(g.HeardVague) }));
            c.Check(d.heardVague.Contains(key), "vague givers are saved");
            g.HeardVague.Clear(); g.HeardVague.Add(key);
            string sure = g.GiverRumour(at, true);
            c.Note(sure ?? "(none)");
            c.Check(sure != null && sure.Contains(m.title) && g.HeardOf.Contains(key) && !g.HeardVague.Contains(key), "a trusted teller sharpens it to a point: " + sure);
            pins.Clear(); g.JobPins(pins);
            c.Check(!pins.Any(p => p.radius > 0f && p.label.Contains(m.title)), "the circle became a pin");
            c.Check(Dialogue.TrustForWay > 0, $"the way is given from disposition {Dialogue.TrustForWay} or a friendly faction");
        }
    }

    /// <summary>A burning car heats the vehicle parked beside it until that catches too; one parked well clear stays
    /// cold. In a halted convoy the cars near a burning one drive off first.</summary>
    class FireSpread : Scenario
    {
        public override string Id => "vehicle.fire_spread";
        public override float Timeout => 80f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            c.Check(VehicleSystems.SpreadRate(1f, 0.5f) > 0.2f, $"a taken-hold fire 1 m away out-heats the cooling ({VehicleSystems.SpreadRate(1f, 0.5f):0.00}/s > 0.2/s)");
            c.Check(VehicleSystems.SpreadRate(2.6f, 1f) < 0.2f && VehicleSystems.SpreadRate(3.2f, 1f) == 0f, "a car's width away it never catches");
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(14f, out var pad)) { c.Block("no open pad"); yield break; }
            var a = g.SpawnAiVehicle("Coupe", pad + Vector3.up * 0.5f, Quaternion.identity);
            var b = g.SpawnAiVehicle("Coupe", pad + new Vector3(2.3f, 0.5f, 1.5f), Quaternion.identity);       // alongside, by the burning bay
            var far = g.SpawnAiVehicle("Coupe", pad + new Vector3(-9f, 0.5f, 0f), Quaternion.identity);
            if (!c.Check(a && b && far, "three parked cars")) yield break;
            foreach (var v in new[] { a, b, far }) { v.handbrake = true; v.Occupied = false; v.aiDriven = false; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -16f), 0f);
            yield return new WaitForSeconds(1.5f);
            var sa = a.GetComponent<VehicleSystems>(); var sb = b.GetComponent<VehicleSystems>(); var sf = far.GetComponent<VehicleSystems>();
            float gap = Vector3.Distance(b.Body.ClosestPointOnBounds(sa.FirePos), sa.FirePos);
            c.Fixture($"two coupes side by side, a third 9 m off; the first's engine is set alight ({gap:0.0} m from its bay to the neighbour)");
            yield return ScheduledScenarios1005.Burn(sa);
            if (!c.Check(sa.Burning, "the first car burns")) yield break;
            sa.fuel = 4f;                                                                        // no tank roll in the test
            var burnA = a.GetComponent<VehicleBurn>();
            if (burnA) burnA.burn = Mathf.Max(burnA.burn, 0.3f);                                   // a fire that has taken hold
            gap = Vector3.Distance(b.Body.ClosestPointOnBounds(sa.FirePos), sa.FirePos);
            c.Metric("flames_to_neighbour", gap, "m");
            float t0 = Time.time;
            while (!sb.Burning && Time.time - t0 < 45f) yield return null;
            c.Metric("seconds_to_spread", Time.time - t0, "s");
            c.Check(sb.Burning, $"the neighbour caught after {Time.time - t0:0} s");
            c.Screenshot("fire_spread");
            yield return null;
            c.Check(!sf.Burning, "the car parked clear stays cold");
            foreach (var s in new[] { sa, sb }) { int i = 0; while (s && s.Burning && i++ < 20) s.Extinguish(1f); }

            // the column: a burning car makes the one parked beside it drive off
            var ai1 = a.gameObject.AddComponent<AiDriver>(); var ai2 = b.gameObject.AddComponent<AiDriver>();
            ai1.goal = ai2.goal = AiDriver.Goal.Park;
            var convoy = new Convoy("test:spread", false, "fuel", new List<Vector3> { pad, pad + Vector3.forward * 200f }, 3, new ConvoySave { id = "test:spread" });
            convoy.cars.Add(ai1); convoy.cars.Add(ai2);
            var clear = typeof(Convoy).GetMethod("ClearOfFire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (!c.Check(clear != null, "Convoy.ClearOfFire exists")) yield break;
            typeof(VehicleSystems).GetField("smotheredUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sa, 0f);   // the powder holds 90 s
            yield return ScheduledScenarios1005.Burn(sa);
            if (!c.Check(sa.Burning, "the lead car burns again")) yield break;
            ai1.Release();
            clear.Invoke(convoy, new object[] { g });
            c.Check(convoy.clearing.ContainsKey(ai2) && ai2.goal == AiDriver.Goal.Path && ai2.oneWay, "the car beside it pulls away (a one-way hop out of reach)");
            var to = ai2.path != null && ai2.path.Count > 1 ? ai2.path[1] : b.transform.position;
            float clearD = Vector3.Distance(new Vector3(to.x, 0f, to.z), new Vector3(sa.FirePos.x, 0f, sa.FirePos.z));
            c.Check(clearD > VehicleSystems.SpreadReach + 3f, $"to {clearD:0} m from the flames");
            int n = 0; while (sa.Burning && n++ < 20) sa.Extinguish(1f);
            foreach (var v in new[] { a, b, far }) if (v) Object.Destroy(v.gameObject);
        }
    }

    /// <summary>Emergencies name where the remedy is: an engine fire with the extinguisher in a compartment, a blowout
    /// with the jack in the pack, a wound with dressings in the glovebox, or that it didn't come along.</summary>
    class KitAlarm : Scenario
    {
        public override string Id => "ui.kit_alarm";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var box = ScheduledScenarios1006.Box(v);
            if (!c.Check(box, "the car has storage")) yield break;
            int stashed = ScheduledScenarios1006.StashBottles(g);
            int jacks = g.Inventory.GetItem("tool_jack");
            box.inventory.AddItem(Companions.Extinguisher);
            string p0 = TripKit.Prompt(TripKit.Need.Fire, v, g.Inventory);
            c.Check(p0.Contains("EXTINGUISHER") && p0.Contains(box.title), "the fire prompt names the compartment: " + p0);
            g.Enter(v);
            yield return new WaitForSeconds(0.4f);
            var sys = v.GetComponent<VehicleSystems>();
            TripKit.LastAlarm = null;
            yield return ScheduledScenarios1005.Burn(sys);
            c.Note("alarm: " + TripKit.LastAlarm);
            c.Check(TripKit.LastAlarm != null && TripKit.LastAlarm.StartsWith("ENGINE FIRE!") && TripKit.LastAlarm.Contains(box.title), "the fire alarm says where the bottle is: " + TripKit.LastAlarm);
            c.Screenshot("kit_alarm");
            yield return null;
            int k = 0; while (sys.Burning && k++ < 20) sys.Extinguish(1f);
            box.inventory.TakeItem(Companions.Extinguisher);
            c.Check(TripKit.Prompt(TripKit.Need.Fire, v, g.Inventory).StartsWith("NO EXTINGUISHER"), "without one: " + TripKit.Prompt(TripKit.Need.Fire, v, g.Inventory));
            if (jacks == 0) g.Inventory.AddItem("tool_jack");
            TripKit.LastAlarm = null;
            var wheel = v.GetComponentsInChildren<WheelStats>().FirstOrDefault(w => !w.Popped);
            if (wheel) wheel.Pop();
            c.Check(TripKit.LastAlarm != null && TripKit.LastAlarm.StartsWith("TYRE BLOWOUT!") && TripKit.LastAlarm.Contains("JACK IN YOUR PACK"), "a blowout names the jack: " + TripKit.LastAlarm);
            if (wheel) wheel.wear = 0f;
            if (jacks == 0) g.Inventory.TakeItem("tool_jack");
            int bandages = g.Inventory.GetItem("med_bandage"), kits = g.Inventory.GetItem("med_firstaid");
            if (bandages > 0) g.Inventory.TakeItem("med_bandage", bandages);
            if (kits > 0) g.Inventory.TakeItem("med_firstaid", kits);
            var glove = VehicleStorage.For(v).compartments.Select(x => x.container).FirstOrDefault(x => x && x.title == "GLOVEBOX") ?? box;
            glove.inventory.AddItem("med_bandage");
            string pb = TripKit.Prompt(TripKit.Need.Bleeding, v, g.Inventory);
            c.Check(pb.StartsWith("DRESSINGS") && VehicleStorage.For(v).compartments.Any(x => x && x.container && pb.Contains(x.container.title)), "a bleeding wound names where the dressings are: " + pb);
            glove.inventory.TakeItem("med_bandage");
            if (bandages > 0) g.Inventory.AddItem("med_bandage", bandages);
            if (kits > 0) g.Inventory.AddItem("med_firstaid", kits);
            if (stashed > 0) g.Inventory.AddItem(Companions.Extinguisher, stashed);
            g.Exit();
        }
    }

    /// <summary>A companion near a fleet car whose engine catches takes the extinguisher out of its compartment, beats
    /// the fire and puts the bottle back.</summary>
    class CompanionFire : Scenario
    {
        public override string Id => "npc.companion_fire";
        public override float Timeout => 50f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var box = ScheduledScenarios1006.Box(v);
            if (!c.Check(box, "the car has storage")) yield break;
            if (box.inventory.GetItem(Companions.Extinguisher) == 0) box.inventory.AddItem(Companions.Extinguisher);
            g.Player.Teleport(v.transform.position + v.transform.right * 7f + Vector3.up * 0.3f, -90f);
            var spot = v.transform.position - v.transform.right * 5f;
            spot.y = DeformableTerrain.Instance.Height(spot.x, spot.z) + 0.1f;
            var npc = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:mate", NpcRole.Wanderer, 4242), spot, 0f, null, g.propMaterial);
            Companions.Recruit(g, npc);
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(npc && npc.companion, "a companion beside the car")) yield break;
            c.Fixture("a fleet car with an extinguisher in its " + box.title + ", a companion 5 m away, the player 7 m off");
            var sys = v.GetComponent<VehicleSystems>();
            yield return ScheduledScenarios1005.Burn(sys);
            if (!c.Check(sys.Burning, "the engine catches")) yield break;
            sys.fuel = 4f;
            yield return null; yield return null;
            c.Check(Companions.LastFireFighter == npc && npc.fireTarget == sys, "the companion goes for the fire");
            c.Check(box.inventory.GetItem(Companions.Extinguisher) == 0, "with the bottle out of the " + box.title);
            c.Note("toast: " + g.ToastText);
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("companion_fire");
            yield return null;
            float t0 = Time.time;
            while (sys.Burning && Time.time - t0 < 30f) yield return null;
            c.Metric("seconds_to_out", Time.time - t0, "s");
            c.Check(!sys.Burning, "the companion beat the fire");
            yield return null; yield return null;
            c.Check(box.inventory.GetItem(Companions.Extinguisher) == 1, "and put the bottle back");
            Companions.Dismiss(g, npc);
            if (npc) Object.Destroy(npc.gameObject);
        }
    }

    /// <summary>A scavenger's unbought haul goes to the nearest town: on the salvage stalls from the next day, marked
    /// as off a wreck, under the usual price; buying takes it from the haul (not the vendor's daily stock); saved.</summary>
    class HaulToMarket : Scenario
    {
        public override string Id => "towns.haul_market";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var at = g.Player.transform.position + new Vector3(40f, 0f, 0f);
            string scav = WastelandGame.ScavengerId(at);
            Trade.Hauls.Remove(scav);
            Trade.AddHaul(scav, "part:wheel_offroad", 2);
            Trade.AddHaul(scav, "med_bandage", 3);
            int moved = g.HaulOff(scav, at);
            c.Check(moved == 5 && !Trade.Hauls.ContainsKey(scav), $"the scavenger left with {moved} goods");
            Settlement town = null; float td = float.MaxValue;
            foreach (var st in g.World.settlements) { float d = Vector2.Distance(st.pos, new Vector2(at.x, at.z)); if (d < td) { td = d; town = st; } }
            if (!c.Check(town != null, "a town to sell in")) yield break;
            c.Note("to " + Market.TownName(town) + " (" + td.ToString("0") + " m)");
            c.Check(Trade.MarketHaulCount(town) == 0, "not on the stalls the same day");
            Trade.MarketHaulDay[town.index] = DayNight.Day;                                    // a day later
            c.Check(Trade.MarketHaulCount(town) == 5, "on the stalls the next day");
            var p = NpcProfile.Make("test:salvage", NpcRole.Shopkeeper, 77, "salvage");
            var spot = g.Player.transform.position + g.Player.transform.forward * 2f;
            var vendor = MadMax.Npc.Npc.Spawn(p, spot, 180f, null, g.propMaterial);
            Trade.Town = town; Trade.Seller = Faction.None;
            var stock = Trade.Stock(p, vendor.State, 0f);
            c.Note(string.Join(", ", stock.Select(o => o.id + " x" + o.count + " @" + o.price + (o.note != null ? " " + o.note : ""))));
            var line = stock.FirstOrDefault(o => Trade.IsHaulNote(o.note) && o.id == "med_bandage");
            if (!c.Check(line.id != null && line.count == 3, "the salvage vendor sells the bandages off the wreck")) { Object.Destroy(vendor.gameObject); yield break; }
            c.Check(line.price <= Trade.BuyPrice(line.id, 0f), $"at no more than the usual price ({line.price} vs {Trade.BuyPrice(line.id, 0f)})");
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { scavHauls = Trade.SaveHauls() }));
            g.Inventory.Add(ResourceType.Scrap, line.price + 5);
            int bought0 = vendor.State.Bought("med_bandage", DayNight.Day);
            c.Check(Trade.Buy(g, vendor, line, 1), "bought one");
            c.Check(Trade.MarketHaulCount(town) == 4 && vendor.State.Bought("med_bandage", DayNight.Day) == bought0, "it came off the haul, not the daily stock");
            Trade.LoadHauls(saved.scavHauls);
            c.Check(Trade.MarketHaulCount(town) == 5, "the market haul survives a save round trip");
            Trade.MarketHauls.Remove(town.index); Trade.MarketHaulDay.Remove(town.index);
            Object.Destroy(vendor.gameObject);
        }
    }
}
