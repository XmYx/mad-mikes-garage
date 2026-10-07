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
    /// <summary>Scheduled update 2026-10-07: search circles that shrink with each telling (and binoculars that find
    /// the camp), stalls that remember where a haul came from and sell the player's stolen parts back at half,
    /// burned-out wrecks as landmarks, the companion medic, spare keys and locksmiths, the engine block heater.</summary>
    public static class ScheduledScenarios1007
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new SearchShrink();
            yield return new HaulMemory();
            yield return new BurnedLandmark();
            yield return new CompanionMedic();
            yield return new SpareKeys();
            yield return new BlockHeaterStart();
        }

        internal static object Call(object o, string method, params object[] args) =>
            o.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Invoke(o, args);

        internal static VehicleDriver FleetCar(WastelandGame g) =>
            g.Fleet.FirstOrDefault(v => v && v.driveable && !v.GetComponent<Machine>() && !v.GetComponent<BikeBalance>() && v.GetComponent<VehicleSystems>() && VehicleStorage.For(v));
    }

    /// <summary>Each telling by a new stranger halves a vague giver's search circle (the same teller twice doesn't),
    /// down to 60 m, and the circle still holds the place; the count is saved; binoculars from inside the circle pin
    /// the camp.</summary>
    class SearchShrink : Scenario
    {
        public override string Id => "talk.search_shrink";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var at = g.Player.transform.position;
            string first = g.GiverRumour(at, false, "test:a");
            if (first == null) { c.Block("no unpinned story giver in reach of the start"); yield break; }
            string key = g.HeardVague.First();
            var m = MadMax.Story.StoryCast.All.First(x => x.key == key);
            var real = MadMax.Story.StoryAnchors.Get(m.anchor);
            c.Check(Mathf.Approximately(g.VagueRadiusOf(key), WastelandGame.VagueRadius), $"the first telling: a {g.VagueRadiusOf(key):0} m circle");
            g.GiverRumour(at, false, "test:a");
            c.Check(Mathf.Approximately(g.VagueRadiusOf(key), WastelandGame.VagueRadius), "the same teller again doesn't narrow it");
            var radii = new List<float>();
            foreach (var t in new[] { "test:b", "test:c", "test:d", "test:e" })
            {
                string l = g.GiverRumour(at, false, t);
                radii.Add(g.VagueRadiusOf(key));
                c.Note(t + ": " + l);
            }
            c.Note("radii: " + string.Join(", ", radii.Select(r => r.ToString("0"))));
            c.Check(radii[0] < 230f && radii[1] < 115f, "each new teller halves the circle");
            c.Check(Mathf.Approximately(radii[3], WastelandGame.VagueMinRadius), $"down to {WastelandGame.VagueMinRadius:0} m and no further");
            var pins = new List<WastelandGame.Pin>();
            g.JobPins(pins);
            var circle = pins.FirstOrDefault(p => p.radius > 0f && p.label.Contains(m.title));
            float off = Vector2.Distance(new Vector2(circle.pos.x, circle.pos.z), new Vector2(real.x, real.z));
            c.Check(circle.label != null && Mathf.Approximately(circle.radius, WastelandGame.VagueMinRadius) && off < circle.radius, $"the map circle shrank and still holds the place ({off:0} m off, radius {circle.radius:0} m)");
            var d = new SaveData();
            ScheduledScenarios1007.Call(g, "SaveStory", d);
            c.Check(d.heardVague.Any(e => e.StartsWith(key + "|")), "the tellings are saved: " + string.Join(", ", d.heardVague));
            c.Check(!g.SpotCamp(key, real, real + new Vector3(2000f, 0f, 0f), null), "binoculars from outside the circle see nothing");
            var centre = WastelandGame.VagueCentre(key, real, g.VagueRadiusOf(key));
            c.Check(g.SpotCamp(key, real, centre, null) && g.HeardOf.Contains(key) && !g.HeardVague.Contains(key), "binoculars from inside the circle spot the camp: pinned");
        }
    }

    /// <summary>A haul on the stalls names where it came from; a part car breakers took off a fleet car turns up at
    /// the nearest town's salvage stall marked as the player's, at half the price, and buying it clears the mark.</summary>
    class HaulMemory : Scenario
    {
        public override string Id => "towns.haul_memory";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var at = g.Player.transform.position + new Vector3(600f, 0f, 0f);
            var town = g.NearestTown(at);
            if (!c.Check(town != null, "a town")) yield break;
            string scav = WastelandGame.ScavengerId(at);
            Trade.AddHaul(scav, "med_bandage", 2);
            g.HaulOff(scav, at);
            string note = Trade.HaulNote(town);
            c.Check(note.StartsWith(Trade.MarketHaulNote) && note.Length > Trade.MarketHaulNote.Length, "the stall names where the haul came from: " + note);
            Trade.MarketHauls.Remove(town.index); Trade.MarketHaulDay.Remove(town.index); Trade.MarketHaulFrom.Remove(town.index);

            var v = ScheduledScenarios1007.FleetCar(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            int mounted0 = v.GetComponentsInChildren<VehiclePart>().Count(p => p.Socket);
            WastelandGame.LastStolen = null;
            c.Check(g.BreakerSteals(v, v.transform.position), "a car breaker takes a part");
            string id = WastelandGame.LastStolen;
            yield return null;
            int mounted1 = v.GetComponentsInChildren<VehiclePart>().Count(p => p.Socket);
            c.Check(id != null && mounted1 == mounted0 - 1, $"{id} is gone off the {WastelandGame.Name(v)} ({mounted0} → {mounted1} parts)");
            c.Check(Trade.Stolen.TryGetValue(id ?? "", out int st) && st == 1, "the part is remembered as the player's");
            var home = g.NearestTown(v.transform.position);
            c.Check(Trade.MarketHaulCount(home) == 0, "not on the stalls the same day");
            Trade.MarketHaulDay[home.index] = DayNight.Day;
            var p0 = NpcProfile.Make("test:salvage2", NpcRole.Shopkeeper, 78, "salvage");
            var vendor = MadMax.Npc.Npc.Spawn(p0, g.Player.transform.position + g.Player.transform.forward * 2f, 180f, null, g.propMaterial);
            Trade.Town = home; Trade.Seller = Faction.None;
            var stock = Trade.Stock(p0, vendor.State, 0f);
            var line = stock.FirstOrDefault(o => o.id == id);
            c.Note(string.Join(", ", stock.Where(o => o.note != null).Select(o => o.id + " @" + o.price + " " + o.note)));
            if (!c.Check(line.id != null && line.note == Trade.StolenNote, "the stall sells it marked " + Trade.StolenNote)) { Object.Destroy(vendor.gameObject); yield break; }
            int full = Trade.BuyPrice(id, 0f);
            c.Check(line.price <= Mathf.CeilToInt(full * Trade.BuyBackFactor) + 1, $"at half the price ({line.price} vs {full})");
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { scavHauls = Trade.SaveHauls() }));
            Trade.LoadHauls(saved.scavHauls);
            c.Check(Trade.Stolen.ContainsKey(id) && Trade.MarketHaulCount(home) == 1, "the stolen mark survives a save");
            g.Inventory.Add(ResourceType.Scrap, line.price + 5);
            c.Check(Trade.Buy(g, vendor, line, 1), "bought it back");
            c.Check(!Trade.Stolen.ContainsKey(id), "and it's no longer out there");
            Object.Destroy(vendor.gameObject);
        }
    }

    /// <summary>A vehicle that burns out marks the map ("BURNED-OUT ..."), scavengers leave it alone for a week, and
    /// the landmark is saved.</summary>
    class BurnedLandmark : Scenario
    {
        public override string Id => "map.burned_landmark";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            var wreck = g.SpawnRoadWreck("Coupe", pad, Quaternion.identity, 43);
            if (!c.Check(wreck, "a road wreck")) yield break;
            var burn = wreck.GetComponent<VehicleBurn>();
            if (!burn) burn = wreck.gameObject.AddComponent<VehicleBurn>();
            yield return null;
            burn.Char(true);
            c.Check(g.BurnedOuts.Count == 1 && g.BurnedOuts[0].name.StartsWith("BURNED-OUT"), "the map notes it: " + (g.BurnedOuts.Count > 0 ? g.BurnedOuts[0].name : "(none)"));
            var pins = new List<WastelandGame.Pin>();
            g.JobPins(pins);
            c.Check(pins.Any(p => p.label != null && p.label.StartsWith("BURNED-OUT")), "a landmark pin on the map");
            int taken = g.Scavenge(pad, 4f, 7);
            c.Check(taken == 0 && g.BurnedNear(pad), "scavengers leave a fresh burned-out wreck alone");
            var d = new SaveData();
            ScheduledScenarios1007.Call(g, "SaveRoadside", d);
            g.BurnedOuts.Clear();
            ScheduledScenarios1007.Call(g, "LoadRoadside", JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));
            c.Check(g.BurnedOuts.Count == 1 && Vector3.Distance(g.BurnedOuts[0].pos, wreck.transform.position) < 1f, "the landmark is saved");
            var b = g.BurnedOuts[0]; b.day -= WastelandGame.BurnedDays + 1f; g.BurnedOuts[0] = b;
            c.Check(!g.BurnedNear(pad), "after a week it is fair game again");
            pins.Clear(); g.JobPins(pins);
            c.Check(!pins.Any(p => p.label != null && p.label.StartsWith("BURNED-OUT")), "and off the map");
            g.BurnedOuts.Clear();
            Object.Destroy(wreck.gameObject);
        }
    }

    /// <summary>Once the fight is over a companion fetches the first-aid kit from a fleet car's compartment, walks to the
    /// bleeding player and dresses and splints their wounds.</summary>
    class CompanionMedic : Scenario
    {
        public override string Id => "npc.companion_medic";
        public override float Timeout => 45f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = ScheduledScenarios1007.FleetCar(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var box = ScheduledScenarios1006.Box(v);
            if (!c.Check(box, "the car has storage")) yield break;
            int kits = box.inventory.GetItem(Companions.FirstAidKit);
            if (kits > 0) box.inventory.TakeItem(Companions.FirstAidKit, kits);
            box.inventory.AddItem(Companions.FirstAidKit);
            g.Player.Teleport(v.transform.position + v.transform.right * 4f + Vector3.up * 0.3f, -90f);
            var spot = v.transform.position - v.transform.right * 6f;
            spot.y = DeformableTerrain.Instance.Height(spot.x, spot.z) + 0.1f;
            var npc = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:medic", NpcRole.Wanderer, 4343), spot, 0f, null, g.propMaterial);
            Companions.Recruit(g, npc);
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(npc && npc.companion, "a companion by the car")) yield break;
            g.Stats.injuries.Clear();
            g.Stats.injuries.Add(new Injury { zone = BodyZone.ArmL, type = Wound.Laceration, severity = 0.8f });
            g.Stats.injuries.Add(new Injury { zone = BodyZone.LegR, type = Wound.Fracture, severity = 0.9f });
            c.Fixture("the player bleeds from the left arm and has a broken right leg; a first-aid kit in the " + box.title + ", the companion 10 m off");
            c.Check(g.NeedsTending, "something to treat");
            float t0 = Time.time;
            while (Companions.LastMedic != npc && Time.time - t0 < 10f) yield return null;
            c.Check(Companions.LastMedic == npc && npc.tending && npc.tendFrom == box, "the companion comes with the kit from the " + box.title);
            c.Note("toast: " + g.ToastText);
            while (npc.tending && Time.time - t0 < 30f) yield return null;
            c.Metric("seconds_to_treated", Time.time - t0, "s");
            c.Screenshot("companion_medic");
            yield return null;
            var arm = g.Stats.injuries[0]; var leg = g.Stats.injuries[1];
            c.Check(npc.tendOutcome == 2 && arm.bandaged && arm.disinfected && leg.splinted, $"dressed and splinted ({npc.tendOutcome} wounds)");
            c.Check(box.inventory.GetItem(Companions.FirstAidKit) == 0, "the kit was used up");
            c.Check(!g.NeedsTending, "nothing left to treat");
            g.Stats.injuries.Clear();
            Companions.Dismiss(g, npc);
            if (npc) Object.Destroy(npc.gameObject);
        }
    }

    /// <summary>A town mechanic cuts a key for a car whose key is lost (scrap now, the key tomorrow); a key and a blank
    /// at a workbench cut a spare; orders are saved.</summary>
    class SpareKeys : Scenario
    {
        public override string Id => "vehicle.spare_keys";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no open pad"); yield break; }
            var car = g.SpawnRoadWreck("Coupe", pad, Quaternion.identity, 47);
            if (!c.Check(car, "a found car")) yield break;
            var ign = car.GetComponent<VehicleIgnition>();
            if (!c.Check(ign && ign.NeedsKey, "it needs a key")) yield break;
            ign.key = VehicleIgnition.Where.Lost; ign.hotwired = false;
            string key = ign.KeyItem;
            if (g.Inventory.GetItem(key) > 0) g.Inventory.TakeItem(key, g.Inventory.GetItem(key));
            var spot = pad + new Vector3(4f, 0f, -4f); spot.y = DeformableTerrain.Instance.Height(spot.x, spot.z) + 0.1f;
            var p = NpcProfile.Make("test:mechanic", NpcRole.Shopkeeper, 91, "parts");
            var smith = MadMax.Npc.Npc.Spawn(p, spot, 0f, null, g.propMaterial);
            smith.State.Set(NpcSave.Met); smith.State.disposition = 20;
            g.Player.Teleport(spot + new Vector3(0f, 0.2f, -2f), 0f);
            yield return null;
            c.Fixture("a found coupe with its key lost, a parts vendor 6 m away, met and friendly");
            var talk = new Dialogue(g, smith);
            var order = talk.choices.FirstOrDefault(x => x.label.Contains("CUT A KEY FOR THE"));
            if (!c.Check(order.label != null, "the mechanic offers to cut a key: " + order.label)) { Object.Destroy(smith.gameObject); yield break; }
            int scrap = g.Inventory.Get(ResourceType.Scrap);
            if (scrap < WastelandGame.LocksmithScrap) g.Inventory.Add(ResourceType.Scrap, WastelandGame.LocksmithScrap - scrap);
            scrap = g.Inventory.Get(ResourceType.Scrap);
            order.act();
            c.Note(talk.line);
            c.Check(g.KeyOrders.Count == 1 && g.Inventory.Get(ResourceType.Scrap) == scrap - WastelandGame.LocksmithScrap, $"paid {WastelandGame.LocksmithScrap} scrap, the key is on order");
            var ask = talk.choices.FirstOrDefault(x => x.label.StartsWith("IS MY KEY READY"));
            if (!c.Check(ask.label != null, "the hub asks after the key")) yield break;
            ask.act();
            c.Check(g.Inventory.GetItem(key) == 0, "not ready the same day: " + talk.line);
            var d = new SaveData();
            ScheduledScenarios1007.Call(g, "SaveRoadside", d);
            g.KeyOrders.Clear();
            ScheduledScenarios1007.Call(g, "LoadRoadside", JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));
            c.Check(g.KeyOrders.Count == 1 && g.KeyOrders[0].key == key, "the order is saved");
            var o = g.KeyOrders[0]; o.ready = DayNight.Day; g.KeyOrders[0] = o;                     // a day later
            talk = new Dialogue(g, smith);
            ask = talk.choices.FirstOrDefault(x => x.label.StartsWith("IS MY KEY READY"));
            if (ask.act != null) ask.act();
            c.Check(g.Inventory.GetItem(key) == 1 && ign.CanStart(g.Inventory), "the next day the key is handed over and starts it: " + talk.line);

            var bench = MedMineScenarios.Piece(g, "workbench", g.Player.transform.position + g.Player.transform.forward * 1.5f, -g.Player.transform.forward);
            yield return null;
            if (!c.Check(bench, "a workbench")) yield break;
            int blanks = g.Inventory.GetItem(WastelandGame.KeyBlank);
            if (blanks > 0) g.Inventory.TakeItem(WastelandGame.KeyBlank, blanks);
            c.Check(!g.CutSpareKey(key), "no blank, no spare: " + g.ToastText);
            c.Check(RecipeLibrary.All.Any(r => r.output == WastelandGame.KeyBlank && r.station == "workbench"), "key blanks are made at the workbench");
            g.Inventory.AddItem(WastelandGame.KeyBlank);
            int keys = g.Inventory.GetItem(key);
            g.UseItem(key);
            c.Check(g.Inventory.GetItem(key) == keys + 1 && g.Inventory.GetItem(WastelandGame.KeyBlank) == 0, $"using the key at the bench cut a spare ({g.Inventory.GetItem(key)} keys): " + g.ToastText);
            Object.Destroy(bench.gameObject); Object.Destroy(smith.gameObject); Object.Destroy(car.gameObject);
        }
    }

    /// <summary>Below the coolant's freezing point a parked car is frozen up; a powered block heater beside it keeps it
    /// startable, holds the engine warm and starts thawing an iced tank; unpowered it does nothing.</summary>
    class BlockHeaterStart : Scenario
    {
        public override string Id => "vehicle.block_heater";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no open pad"); yield break; }
            var car = g.SpawnAiVehicle("Coupe", pad + Vector3.up * 0.5f, Quaternion.identity);
            if (!c.Check(car, "a parked coupe")) yield break;
            car.aiDriven = false; car.handbrake = true; car.Occupied = false;
            var sys = car.GetComponent<VehicleSystems>();
            sys.Stop();
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -9f), 0f);
            yield return new WaitForSeconds(1f);
            float base0 = Weather.BaseTemperature;
            float offset = Weather.Temperature - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, sys.FreezePoint - 12f - offset);
            sys.SetTemperature(Weather.Temperature);
            c.Fixture($"air {Weather.Temperature:0} C, below the coolant's freezing point ({sys.FreezePoint:0} C); engine cold");
            c.Check(!sys.usesCoolant || sys.Frozen, "parked in the cold the coolant freezes: no start");
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            var heater = FurnitureLibrary.Spawn("block_heater", g.Build.Structures, P(2.6f, 0.5f), Quaternion.identity, g.propMaterial);
            var gen = FurnitureLibrary.Spawn("generator", g.Build.Structures, P(5.5f, 0.5f), Quaternion.identity, g.propMaterial);
            yield return null;
            if (!c.Check(heater && gen, "a block heater and a generator")) yield break;
            heater.GetComponent<UtilityNode>().Link(gen.GetComponent<UtilityNode>(), UtilityKind.Power);
            var bh = heater.GetComponent<BlockHeater>();
            yield return new WaitForSeconds(1f);
            c.Check(!bh.plugged && (!sys.usesCoolant || sys.Frozen), "unpowered it does nothing");
            var gc = gen.GetComponent<Generator>(); gc.fuel = 20f; gc.on = true;
            sys.IceTank();
            float t0 = Time.time;
            while (bh.plugged != sys && Time.time - t0 < 6f) yield return null;
            c.Check(bh.plugged == sys, "the powered heater plugs into the car beside it");
            yield return new WaitForSeconds(2f);
            c.Check(sys.BlockWarm && !sys.Frozen, "the coolant stays liquid: it can start");
            c.Check(sys.Temperature > Weather.Temperature + 0.5f, $"the engine is warming ({sys.Temperature:0.0} C in {Weather.Temperature:0} C air)");
            c.Check(!sys.tankIced || sys.ThawProgress > 0f, $"an iced tank thaws ({sys.ThawProgress:0.00})");
            c.Screenshot("block_heater");
            yield return null;
            Weather.Restore(false, 0f, 0f, base0);
            Object.Destroy(heater.gameObject); Object.Destroy(gen.gameObject); Object.Destroy(car.gameObject);
        }
    }
}
