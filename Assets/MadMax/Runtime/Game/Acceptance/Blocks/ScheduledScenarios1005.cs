using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-05: wreck scavengers sell what they stripped, wrecks age with the season,
    /// convoy drivers fight their engine fires, rumours tell of story givers, the trip kit under the LOOT panel.</summary>
    public static class ScheduledScenarios1005
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ScavengerTrade();
            yield return new WreckSeasons();
            yield return new ConvoyFirefight();
            yield return new GiverRumours();
            yield return new TripKitPanel();
        }

        internal static int Parts(VehicleDriver v)
        {
            int n = 0;
            foreach (var s in v.GetComponent<VehicleChassis>().Sockets) if (s.Current) n++;
            return n;
        }

        internal static IEnumerator Burn(VehicleSystems sys)
        {
            sys.fuel = 30f;
            float t0 = Time.time;
            while (!sys.Burning && Time.time - t0 < 4f) { sys.Heat(5f); yield return null; }
        }
    }

    /// <summary>A news wreck with a scavenger on site: what was stripped is the scavenger's stock, under the usual
    /// price; a bought part spawns beside them and leaves the haul; the haul survives a save round trip.</summary>
    class ScavengerTrade : Scenario
    {
        public override string Id => "towns.scavenger_trade";
        public override float Timeout => 50f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            int season = Weather.Season;
            Weather.Season = 0;                                                                  // summer: busy roads
            var far = pad + new Vector3(0f, 0f, -200f); far.y = t.Height(far.x, far.z) + 0.3f;
            g.Player.Teleport(far, 0f);
            yield return new WaitForSeconds(0.5f);
            var at = pad + new Vector3(-12f, 0f, 0f);
            var wreck = g.SpawnRoadWreck("Coupe", at, Quaternion.identity, 21);
            if (!c.Check(wreck, "a road wreck")) { Weather.Season = season; yield break; }
            int p0 = ScheduledScenarios1005.Parts(wreck);
            g.RoadWrecks.Clear();
            g.RoadWrecks.Add(new Vector4(at.x, at.y, at.z, DayNight.TotalDays - 2.6f));
            string id = WastelandGame.ScavengerId(at);
            Trade.Hauls.Remove(id);
            c.Fixture($"a skirmish wreck with {p0} parts left 2.6 days ago in summer; the player arrives");
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -5f), 0f);
            yield return ScheduledScenarios1004.Until(() => g.RoadWrecks.TrueForAll(WastelandGame.Found), 4f);
            int taken = p0 - ScheduledScenarios1005.Parts(wreck);
            int parts = 0;
            if (Trade.Hauls.TryGetValue(id, out var haul)) foreach (var kv in haul) if (kv.Key.StartsWith("part:")) parts += kv.Value;
            c.Metric("parts_taken", taken, ""); c.Metric("haul_parts", parts, "");
            c.Check(taken > 0, $"the wreck was picked over ({p0} -> {p0 - taken})");
            c.Check(parts >= taken, $"the scavenger kept what was taken ({parts} parts in the haul)");
            MadMax.Npc.Npc scav = null;
            yield return ScheduledScenarios1004.Until(() =>
            {
                foreach (var n in Object.FindObjectsByType<MadMax.Npc.Npc>(FindObjectsSortMode.None))
                    if (n && n.Profile != null && n.Profile.id == id) { scav = n; return true; }
                return false;
            }, 15f);
            if (!c.Check(scav, "the scavenger is there")) { Weather.Season = season; yield break; }
            c.Check(scav.Profile.Vendor && scav.Profile.Title == "SCAVENGER", "and trades (" + scav.Profile.Title + ")");
            var stock = Trade.Stock(scav.Profile, scav.State, 0f);
            c.Note(string.Join(", ", stock.ConvertAll(o => o.id + " x" + o.count + " @" + o.price)));
            var offer = stock.Find(o => o.id.StartsWith("part:"));
            if (!c.Check(offer.id != null, "the stripped parts are on offer")) { Weather.Season = season; yield break; }
            c.Check(offer.price < Trade.BuyPrice(offer.id, 0f), $"under the usual price ({offer.price} vs {Trade.BuyPrice(offer.id, 0f)})");
            var saved = new SaveData { scavHauls = Trade.SaveHauls() };
            int before = Trade.HaulCount(id), loose = VehiclePart.Registry.Count;
            g.Inventory.Add(ResourceType.Scrap, offer.price + 10);
            c.Check(Trade.Buy(g, scav, offer, 1), "bought one");
            c.Check(Trade.HaulCount(id) == before - 1, "it left the haul");
            c.Check(VehiclePart.Registry.Count == loose + 1, "and lies beside the scavenger");
            Trade.LoadHauls(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)).scavHauls);
            c.Check(Trade.HaulCount(id) == before, "the haul survives a save round trip");
            NpcDirector.Instance?.Scavengers.Remove(id);
            Trade.Hauls.Remove(id);
            g.RoadWrecks.Clear();
            Weather.Season = season;
        }
    }

    /// <summary>Wrecks age on the season's clock: stripped fastest in summer, slowest in winter; a winter wreck keeps
    /// its (frozen) fuel.</summary>
    class WreckSeasons : Scenario
    {
        public override string Id => "towns.wreck_seasons";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            float s0 = WastelandGame.ScavengedShare(2.5f, 0), s1 = WastelandGame.ScavengedShare(2.5f, 1), s2 = WastelandGame.ScavengedShare(2.5f, 2);
            c.Metric("share_summer", s0, ""); c.Metric("share_autumn", s1, ""); c.Metric("share_winter", s2, "");
            c.Check(s0 > s1 && s1 > s2 && s2 > 0f, $"2.5 days on: summer {s0:0.00} > autumn {s1:0.00} > winter {s2:0.00}");
            c.Check(WastelandGame.ScavengedShare(1f, 0) == 0f, "nothing goes the first day, even in summer");
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            int season = Weather.Season;
            var a = pad + new Vector3(-14f, 0f, 0f); var b = pad + new Vector3(14f, 0f, 0f);
            var winter = g.SpawnRoadWreck("Coupe", a, Quaternion.identity, 31);
            var summer = g.SpawnRoadWreck("Coupe", b, Quaternion.identity, 31);
            if (!c.Check(winter && summer, "two road wrecks")) yield break;
            var ws = winter.GetComponent<VehicleSystems>(); var ss = summer.GetComponent<VehicleSystems>();
            ws.fuel = ss.fuel = 20f;
            yield return null;
            Weather.Season = 2; int tw = g.Scavenge(a, 3f, 5);
            Weather.Season = 0; int tsum = g.Scavenge(b, 3f, 5);
            Weather.Season = season;
            c.Metric("taken_winter", tw, ""); c.Metric("taken_summer", tsum, "");
            c.Check(tw < tsum, $"3 days on, winter took fewer parts ({tw}) than summer ({tsum})");
            c.Check(ws.fuel > 19.5f && ws.fuel - ss.fuel > 1f, $"the winter tank is frozen and kept ({ws.fuel:0.0} L)");
            c.Check(ss.fuel < 20f, $"the summer one was drained ({ss.fuel:0.0} L)");
            Object.Destroy(winter.gameObject); Object.Destroy(summer.gameObject);
        }
    }

    /// <summary>A trader car's engine catches: it pulls up, the driver climbs out with the extinguisher (and the key),
    /// beats the fire, gets back in and drives on.</summary>
    class ConvoyFirefight : Scenario
    {
        public override string Id => "npc.fire_fight";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            int traders = 0, raiders = 0;
            for (int i = 0; i < 400; i++)
            {
                if (Convoy.CarriesExtinguisher(NpcProfile.Make("x" + i, NpcRole.Trader, i * 7919 + 13, "fuel"), false)) traders++;
                if (Convoy.CarriesExtinguisher(NpcProfile.Make("y" + i, NpcRole.Raider, i * 104729 + 7), true)) raiders++;
            }
            c.Metric("trader_bottles", traders / 400f, ""); c.Metric("raider_bottles", raiders / 400f, "");
            c.Check(traders > 260 && traders < 380 && raiders > 140 && raiders < 260, $"most traders ({traders}/400) and about half the raiders ({raiders}/400) carry a bottle");

            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var ign = v.GetComponent<VehicleIgnition>();
            if (ign) ign.key = VehicleIgnition.Where.Ignition;
            Convoy convoy = null;
            for (int s = 1; s < 200 && convoy == null; s++)
            {
                var cv = new Convoy("test:fire" + s, false, "fuel", new List<Vector3> { v.transform.position, v.transform.position + v.transform.forward * 200f }, s, new ConvoySave { id = "test:fire" + s });
                if (Convoy.CarriesExtinguisher(cv.crew[0], false)) convoy = cv;
            }
            if (!c.Check(convoy != null, "a trader convoy whose driver carries a bottle")) yield break;
            var ai = v.gameObject.AddComponent<AiDriver>();
            ai.goal = AiDriver.Goal.Park;
            convoy.cars.Add(ai);
            var watch = typeof(Convoy).GetMethod("FireWatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (!c.Check(watch != null, "Convoy.FireWatch exists")) yield break;
            var sys = v.GetComponent<VehicleSystems>();
            g.Player.Teleport(v.transform.position + v.transform.right * 9f + Vector3.up * 0.3f, 0f);
            yield return ScheduledScenarios1005.Burn(sys);
            if (!c.Check(sys.Burning, "the engine bay catches fire")) { Object.Destroy(ai); yield break; }
            c.Fixture("a parked trader car (AI-driven, key in) with 30 L aboard catches fire");
            watch.Invoke(convoy, new object[] { g });
            c.Check(!ai.enabled && convoy.fighting.ContainsKey(ai), "the car stops and the driver climbs out");
            var npc = convoy.fighting.TryGetValue(ai, out var w) ? w : null;
            if (!c.Check(npc && npc.fireTarget == sys, "the driver goes for the fire")) { Object.Destroy(ai); yield break; }
            c.Check(ign == null || ign.key == VehicleIgnition.Where.Carried, "with the key on their ring");
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("firefight");
            yield return null;
            float t0 = Time.time;
            while (sys.Burning && Time.time - t0 < 30f) { watch.Invoke(convoy, new object[] { g }); yield return null; }
            c.Metric("seconds_to_out", Time.time - t0, "s");
            c.Check(!sys.Burning, "the driver beat the fire");
            var burn = v.GetComponent<VehicleBurn>();
            c.Check(!burn || (!burn.exploded && !burn.charred), "the car is saved");
            yield return null;
            watch.Invoke(convoy, new object[] { g });
            c.Check(ai.enabled && v.aiDriven && !convoy.fighting.ContainsKey(ai), "the driver is back behind the wheel");
            c.Check(ign == null || ign.key == VehicleIgnition.Where.Ignition, "the key is back in the ignition");
            yield return null;
            c.Check(!npc, "the walker folded back into the car");
            ai.Release();
            Object.Destroy(ai);
            v.aiDriven = false;
        }
    }

    /// <summary>Town talk tells of a story giver beyond the three nearest strangers: the rumour names what they do and
    /// where, and the journal pins them from then on (saved).</summary>
    class GiverRumours : Scenario
    {
        public override string Id => "talk.giver_rumours";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var pins = new List<WastelandGame.Pin>();
            g.JobPins(pins);
            int before = pins.FindAll(p => p.label.StartsWith("? ")).Count;
            string line = g.GiverRumour(g.Player.transform.position);
            c.Note(line ?? "(none)");
            if (line == null) { c.Block("no unpinned story giver in reach of the start"); yield break; }
            c.Check(line.Contains("LOOKING FOR A HAND"), "the rumour tells of work: " + line);
            bool named = false;
            foreach (var m in MadMax.Story.StoryCast.All) if (line.Contains(m.name)) named = true;
            c.Check(!named, "by what they do, not their name");
            pins.Clear();
            g.JobPins(pins);
            int after = pins.FindAll(p => p.label.StartsWith("? ")).Count;
            c.Metric("offer_pins_before", before, ""); c.Metric("offer_pins_after", after, "");
            c.Check(after == before + 1, $"one more offer on the map ({before} -> {after})");
            string second = g.GiverRumour(g.Player.transform.position);
            c.Check(second != line, "the same giver isn't told of twice");
            var d = new SaveData { heardOf = new List<string>(g.HeardOf) };
            c.Check(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)).heardOf.Count == g.HeardOf.Count && g.HeardOf.Count > 0, "heard-of givers are saved");
        }
    }

    /// <summary>Opening a car's storage lists the trip kit under the LOOT panel: an extinguisher in a compartment counts
    /// as in the car, a first-aid kit in the pack as carried, the rest as missing.</summary>
    class TripKitPanel : Scenario
    {
        public override string Id => "ui.trip_kit";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var storage = VehicleStorage.For(v);
            if (!c.Check(storage && storage.compartments.Count > 0, "the car has storage")) yield break;
            var box = storage.compartments[0];
            box.container.inventory.AddItem("tool_extinguisher");
            if (g.Inventory.GetItem("med_firstaid") == 0) g.Inventory.AddItem("med_firstaid");
            var lines = new List<TripKit.Line>();
            int inCar = TripKit.Check(v, g.Inventory, lines);
            c.Note(string.Join(", ", lines.ConvertAll(l => l.label + "=" + l.where)));
            c.Check(lines.Exists(l => l.label == "EXTINGUISHER" && l.where == TripKit.Where.Car), "the extinguisher rides in the car");
            c.Check(lines.Exists(l => l.label == "FIRST-AID KIT" && l.where != TripKit.Where.Missing), "the first-aid kit is carried");
            c.Check(inCar >= 1 && inCar < lines.Count, $"{inCar}/{lines.Count} in the car");
            var stand = VehicleStorage.Closest(v, g.Player.transform.position);
            g.Player.Teleport(stand + Vector3.up * 0.2f, 0f);
            yield return new WaitForSeconds(0.4f);
            g.Loot.OpenVehicle(v);
            g.Loot.HoverExpand(3f);
            yield return new WaitForSeconds(0.5f);
            c.Fixture("the car's storage opened from beside it, an extinguisher in a compartment, a first-aid kit in the pack");
            c.Screenshot("trip_kit");
            yield return null;
            c.Check(g.Loot.KitLines.Count == lines.Count, $"the TRIP KIT rows are drawn ({g.Loot.KitLines.Count})");
            box.container.inventory.TakeItem("tool_extinguisher");
            g.Loot.SetPinned(false);
        }
    }
}
