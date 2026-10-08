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
    /// <summary>Scheduled update 2026-10-08: the trip heater, the trail behind a stolen part, burned-out wrecks that
    /// tell what happened, rough companion dressings, binoculars that find news wrecks.</summary>
    public static class ScheduledScenarios1008
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new TripHeaterStart();
            yield return new StolenTrailScenario();
            yield return new BurnedStoryScenario();
            yield return new RoughDressing();
            yield return new BinocularWrecks();
        }

        /// <summary>A deterministic wanderer profile with (or without) the knack for wounds.</summary>
        internal static NpcProfile Medic(bool skilled)
        {
            for (int s = 1; s < 4000; s++)
            {
                var p = NpcProfile.Make("test:medic" + s, NpcRole.Wanderer, s);
                if (Companions.Patches(p) == skilled) return p;
            }
            return null;
        }
    }

    /// <summary>Below freezing, away from power: the trip heater set beside a frozen car with an iced tank burns a
    /// litre from a can in the pack and warms it like a block heater; without fuel in a can (the tank iced) it can't be
    /// lit, in mild weather it isn't needed; it is on the trip-kit list in the cold and at the parts stalls in winter.</summary>
    class TripHeaterStart : Scenario
    {
        public override string Id => "vehicle.trip_heater";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no open pad"); yield break; }
            var car = g.SpawnAiVehicle("Coupe", pad + Vector3.up * 0.5f, Quaternion.identity);
            if (!c.Check(car, "a parked coupe")) yield break;
            car.aiDriven = false; car.handbrake = true; car.Occupied = false;
            var sys = car.GetComponent<VehicleSystems>();
            sys.Stop();
            g.Player.Teleport(pad + new Vector3(2.5f, 0.3f, 0f), -90f);
            yield return new WaitForSeconds(1f);
            float base0 = Weather.BaseTemperature;
            float offset = Weather.Temperature - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, sys.FreezePoint - 12f - offset);
            sys.SetTemperature(Weather.Temperature);
            sys.IceTank();
            c.Fixture($"air {Weather.Temperature:0} C, a coupe frozen up with an iced tank, no power anywhere");
            c.Check(sys.tankIced && (!sys.usesCoolant || sys.Frozen), "it won't start: frozen and iced");
            foreach (var d in FluidContainers.All) { int n = g.Inventory.GetItem(d.id); if (n > 0) g.Inventory.TakeItem(d.id, n); }
            if (g.Inventory.GetItem(WastelandGame.TripHeater) == 0) g.Inventory.AddItem(WastelandGame.TripHeater);
            c.Check(!g.StartTripHeater(car), "no fuel in a can and the tank iced: it can't be lit (" + g.ToastText + ")");
            g.GiveFilledCan(FluidContainers.FuelCan, ResourceType.Fuel, 5f);
            float litres0 = g.CansOf(FluidContainers.FuelCan)[0].litres;
            var kit = new List<TripKit.Line>();
            TripKit.Check(car, g.Inventory, kit);
            c.Check(kit.Exists(l => l.label == "TRIP HEATER" && l.where == TripKit.Where.Pack), "in the cold the trip kit lists the heater (in the pack)");
            c.Check(g.StartTripHeater(car) && g.TripHeating == sys, "lit beside the car: " + g.ToastText);
            c.Check(Mathf.Abs(g.CansOf(FluidContainers.FuelCan)[0].litres - (litres0 - WastelandGame.TripHeatLitres)) < 0.01f, "a litre came out of the can");
            c.Check(g.Inventory.GetItem(WastelandGame.TripHeater) == 1, "the heater is kept");
            float temp0 = sys.Temperature;
            yield return new WaitForSeconds(3f);
            c.Check(sys.BlockWarm && !sys.Frozen, "the coolant thaws: it can start");
            c.Check(sys.Temperature > temp0 + 1f, $"the engine warms ({temp0:0.0} → {sys.Temperature:0.0} C)");
            c.Check(sys.ThawProgress > 0.02f, $"the iced tank thaws ({sys.ThawProgress:0.00})");
            c.Screenshot("trip_heater");
            yield return null;
            Weather.Restore(false, 0f, 0f, BlockHeater.ColdBelow + 10f - offset);
            kit.Clear(); TripKit.Check(car, g.Inventory, kit);
            c.Check(!kit.Exists(l => l.label == "TRIP HEATER"), $"in mild weather ({Weather.Temperature:0} C) the kit doesn't ask for it");
            var other = g.SpawnAiVehicle("Coupe", pad + new Vector3(0f, 0.5f, 8f), Quaternion.identity);
            if (other) { other.aiDriven = false; other.Occupied = false; other.GetComponent<VehicleSystems>().Stop(); }
            c.Check(other && !g.StartTripHeater(other), "not needed when it isn't cold: " + g.ToastText);
            c.Check(RecipeLibrary.All.Any(r => r.output == WastelandGame.TripHeater && r.station == "workbench"), "made at the workbench");
            c.Check(Trade.SeasonalStock("parts", 2).Any(e => e.id == WastelandGame.TripHeater), "parts stalls stock it in winter");
            Weather.Restore(false, 0f, 0f, base0);
            Object.Destroy(car.gameObject); if (other) Object.Destroy(other.gameObject);
        }
    }

    /// <summary>A part car breakers take remembers the gang; the salvage vendor whose stall has it, asked by a player
    /// they trust, names the gang and the waypoint goes on where they ride; one try a day; the gang is saved.</summary>
    class StolenTrailScenario : Scenario
    {
        public override string Id => "towns.stolen_trail";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var dir = NpcDirector.Instance;
            var raid = dir ? dir.Convoys.FirstOrDefault(x => x.raiders) : null;
            if (raid == null) { c.Block("no raider convoy in the world"); yield break; }
            string gang = raid.Gang;
            var v = ScheduledScenarios1007.FleetCar(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            WastelandGame.LastStolen = null;
            c.Check(g.BreakerSteals(v, v.transform.position, gang), "the " + gang + " take a part: " + g.ToastText);
            string id = WastelandGame.LastStolen;
            c.Check(id != null && Trade.StolenBy.TryGetValue(id, out var by) && by == gang, "the part remembers who took it");
            var home = g.NearestTown(v.transform.position);
            c.Check(Trade.StolenOnStall(home) == null, "not on the stall the same day");
            Trade.MarketHaulDay[home.index] = DayNight.Day;                                            // a day later
            c.Check(Trade.StolenOnStall(home) == id, "the next day it is on " + Market.TownName(home) + "'s stalls");
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { scavHauls = Trade.SaveHauls() }));
            Trade.LoadHauls(saved.scavHauls);
            c.Check(Trade.StolenBy.TryGetValue(id, out by) && by == gang, "the thieves are saved");
            var p0 = NpcProfile.Make("test:salvage3", NpcRole.Shopkeeper, 79, "salvage");
            var vendor = MadMax.Npc.Npc.Spawn(p0, g.Player.transform.position + g.Player.transform.forward * 2f, 180f, null, g.propMaterial);
            vendor.State.Set(NpcSave.Met); vendor.State.disposition = Dialogue.TrailTrust + 5;
            if (g.NearestTown(vendor.transform.position) != home) { c.Block("the vendor isn't in the stall's town"); Object.Destroy(vendor.gameObject); yield break; }
            var talk = new Dialogue(g, vendor);
            var ask = talk.choices.FirstOrDefault(x => x.label.Contains("WHO BROUGHT IT IN"));
            if (!c.Check(ask.label != null, "the hub asks who brought it in: " + ask.label)) { Object.Destroy(vendor.gameObject); yield break; }
            int journal = Journal.Entries.Count;
            ask.act();
            c.Note(talk.line);
            c.Check(talk.line.Contains(gang), "a trusting vendor names the gang");
            c.Check(g.HasWaypoint && Vector3.Distance(g.Waypoint, raid.Position) < 30f, "the waypoint goes on where they ride");
            c.Check(Journal.Entries.Count > journal && Journal.Entries.Any(e => e.text.Contains(gang)), "and the journal has it");
            talk = new Dialogue(g, vendor);
            c.Check(!talk.choices.Any(x => x.label.Contains("WHO BROUGHT IT IN")), "one try a day");
            Object.Destroy(vendor.gameObject);
        }
    }

    /// <summary>A raider car that burns out near the player: the nearest town drinks to it (news, standing up), and
    /// walking up to the landmark tells whose car it was and how long ago; the story survives a save.</summary>
    class BurnedStoryScenario : Scenario
    {
        public override string Id => "map.burned_story";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var dir = NpcDirector.Instance;
            var raid = dir ? dir.Convoys.FirstOrDefault(x => x.raiders) : null;
            if (raid == null) { c.Block("no raider convoy in the world"); yield break; }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            var car = g.SpawnAiVehicle("Pickup", pad + Vector3.up * 0.5f, Quaternion.identity);
            if (!c.Check(car, "a raider pickup")) yield break;
            if (!car.TryGetComponent<AiDriver>(out var ai)) ai = car.gameObject.AddComponent<AiDriver>();
            ai.enabled = false;
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -30f), 0f);
            yield return null;
            var town = g.NearestTown(pad);
            var tf = Factions.OfSettlement(town);
            int rep0 = Factions.Rep(tf);
            int news0 = TownNews.Entries.Count;
            raid.cars.Add(ai);
            g.NoteBurnedOut(car);                                                                     // what VehicleBurn.Char calls
            raid.cars.Remove(ai);
            var b = g.BurnedOuts.LastOrDefault();
            c.Check(b.whose != null && b.whose.Contains(raid.Gang), "the landmark knows whose it was: " + b.whose);
            bool near = Vector2.Distance(town.pos, new Vector2(pad.x, pad.z)) <= WastelandGame.BurnedTownReach;
            if (near)
            {
                c.Check(TownNews.Entries.Count > news0 && TownNews.Entries[0].text.Contains("DRINKS TO"), "the town news cheers it: " + (TownNews.Entries.Count > 0 ? TownNews.Entries[0].text : ""));
                c.Check(Factions.Rep(tf) > rep0, $"{Factions.Names[(int)tf]} think better of the player ({rep0} → {Factions.Rep(tf)})");
            }
            else c.Note("the pad is beyond any town's reach: no town reaction");
            var d = new SaveData();
            ScheduledScenarios1007.Call(g, "SaveRoadside", d);
            g.BurnedOuts.Clear();
            ScheduledScenarios1007.Call(g, "LoadRoadside", JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));
            c.Check(g.BurnedOuts.Count > 0 && g.BurnedOuts.Last().whose == b.whose, "whose it was is saved");
            WastelandGame.LastBurnedStory = null;
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -5f), 0f);
            float t0 = Time.time;
            while (WastelandGame.LastBurnedStory == null && Time.time - t0 < 4f) yield return null;
            c.Note("story: " + WastelandGame.LastBurnedStory);
            c.Check(WastelandGame.LastBurnedStory != null && WastelandGame.LastBurnedStory.Contains(raid.Gang) && WastelandGame.LastBurnedStory.Contains("NOT LONG AGO"), "walking up, it tells whose car it was and when");
            c.Screenshot("burned_story");
            yield return null;
            WastelandGame.LastBurnedStory = null;
            yield return new WaitForSeconds(1.5f);
            c.Check(WastelandGame.LastBurnedStory == null, "told once");
            g.BurnedOuts.Clear();
            Object.Destroy(car.gameObject);
        }
    }

    /// <summary>A companion without the knack dresses and splints roughly (the bandage soils sooner, the splinted
    /// bone knits at 0.8); one who was a nurse or a medic dresses like the player; the player's own care redoes a
    /// rough dressing.</summary>
    class RoughDressing : Scenario
    {
        public override string Id => "npc.rough_dressing";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var rough = ScheduledScenarios1008.Medic(false);
            var good = ScheduledScenarios1008.Medic(true);
            if (!c.Check(rough != null && good != null, "wanderers with and without the knack")) yield break;
            c.Note("skilled: " + NpcLore.Origin[good.origin] + " / " + NpcLore.Secret[good.secret]);
            g.Stats.injuries.Clear();
            var spot = g.Player.transform.position + g.Player.transform.forward * 2f;
            var a = MadMax.Npc.Npc.Spawn(rough, spot, 0f, null, g.propMaterial);
            var b = MadMax.Npc.Npc.Spawn(good, spot + g.Player.transform.right * 2f, 0f, null, g.propMaterial);
            Companions.Recruit(g, a); Companions.Recruit(g, b);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(a.pack && b.pack, "both carry packs")) yield break;
            a.pack.inventory.AddItem(Companions.FirstAidKit); b.pack.inventory.AddItem(Companions.FirstAidKit);
            Injury Arm() => new Injury { zone = BodyZone.ArmL, type = Wound.Laceration, severity = 0.8f };
            Injury Leg() => new Injury { zone = BodyZone.LegR, type = Wound.Fracture, severity = 0.9f };

            g.Stats.injuries.Clear(); g.Stats.injuries.Add(Arm()); g.Stats.injuries.Add(Leg());
            c.Check(g.CompanionTreat(a, null) == 2, "the unskilled companion treats both: " + g.ToastText);
            var arm = g.Stats.injuries[0]; var leg = g.Stats.injuries[1];
            c.Check(arm.rough && leg.rough && arm.Status.Contains("ROUGH") && g.ToastText.Contains("ROUGHLY"), "roughly: " + arm.Status + " / " + leg.Status);
            arm.bandageAge = 400f;
            c.Check(arm.BandageDirty, "a rough bandage is dirty after 400 s");
            var proper = new Injury { zone = BodyZone.LegL, type = Wound.Fracture, severity = 0.9f, splinted = true };
            float s0 = leg.severity, p0 = proper.severity;
            leg.Tick(60f, 100f, 1f); proper.Tick(60f, 100f, 1f);
            float ratio = (s0 - leg.severity) / Mathf.Max(1e-6f, p0 - proper.severity);
            c.Check(Mathf.Abs(ratio - Injury.RoughSplint) < 0.02f, $"a rough splint knits at {ratio:0.00} of a proper one");
            g.Inventory.AddItem("med_bandage"); g.Inventory.AddItem("med_splint");
            g.Treat(leg);
            g.Treat(arm);
            c.Check(!leg.rough && !arm.rough && !arm.BandageDirty, "the player's own care redoes them: " + arm.Status + " / " + leg.Status);

            g.Stats.injuries.Clear(); g.Stats.injuries.Add(Arm()); g.Stats.injuries.Add(Leg());
            c.Check(g.CompanionTreat(b, null) == 2 && !g.Stats.injuries[0].rough && !g.Stats.injuries[1].rough, "the one who was a medic dresses properly: " + g.ToastText);
            g.Stats.injuries.Clear();
            Companions.Dismiss(g, a); Companions.Dismiss(g, b);
            if (a) Object.Destroy(a.gameObject);
            if (b) Object.Destroy(b.gameObject);
        }
    }

    /// <summary>Binoculars find a wreck from the news within reach and in view: pinned on the map once; one too far
    /// off or behind is not.</summary>
    class BinocularWrecks : Scenario
    {
        public override string Id => "map.binocular_wrecks";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            yield return null;
            var me = g.Player.transform.position;
            g.RoadWrecks.Clear(); g.NewsPins.Clear();
            var near = me + new Vector3(140f, 0f, 0f);
            var far = me + new Vector3(0f, 0f, 400f);
            g.NoteRoadWreck(near); g.NoteRoadWreck(far);
            c.Check(g.SpotWrecks(me, null) == 1 && g.NewsPins.Count == 1 && Vector3.Distance(g.NewsPins[0], near) < 1f, "the wreck 140 m off is spotted and pinned; the one 400 m off isn't");
            c.Check(g.SpotWrecks(me, null) == 0, "pinned once");
            var pins = new List<WastelandGame.Pin>();
            g.JobPins(pins);
            c.Note(pins.Count + " job pins");
            c.Check(WastelandGame.InSight(me + Vector3.up * 30f, far, null), "from 30 m higher the far one is in reach");
            g.RoadWrecks.Clear(); g.NewsPins.Clear();
        }
    }
}
