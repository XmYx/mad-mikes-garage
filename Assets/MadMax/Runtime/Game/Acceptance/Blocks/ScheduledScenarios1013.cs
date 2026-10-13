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
    /// <summary>Scheduled update 2026-10-13: stolen parts in the boss car, hitchers who remember, the crew medic's
    /// reputation, check-ins that carry the base, rumoured caches, frost on the gauges, convalescence at home.</summary>
    public static class ScheduledScenarios1013
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new BossCar();
            yield return new HitcherRemembers();
            yield return new MedicWord();
            yield return new CrewBase();
            yield return new CacheRumour();
            yield return new FrostGauges();
            yield return new LieUp();
        }

        internal static Vector3 TownCentre(Settlement t)
        {
            var p = new Vector3(t.pos.x, 0f, t.pos.y);
            p.y = DeformableTerrain.Instance.Height(p.x, p.z) + 0.2f;
            return p;
        }
    }

    /// <summary>A part stolen by a gang rides in their boss car until market day: opening its storage puts the part
    /// behind the car and off the market; a car of no gang gives nothing.</summary>
    class BossCar : Scenario
    {
        public override string Id => "towns.boss_car";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var dir = NpcDirector.Instance;
            var convoy = dir ? dir.Convoys.FirstOrDefault(k => k != null && k.raiders) : null;
            if (convoy == null) { c.Block("no raider convoy in this world"); yield break; }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            var car = g.SpawnAiVehicle("Interceptor", pad + Vector3.up * 0.6f, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(car, "a raider car")) yield break;
            car.handbrake = true;
            var ai = car.gameObject.AddComponent<AiDriver>(); ai.enabled = false;
            string gang = convoy.Gang, id = "part:lights_bar";
            var town = g.NearestTown(pad);
            Trade.AddStolen(town, id, pad, gang);
            c.Check(Trade.CarriedBy(gang).Contains(id), "the " + gang + " carry the stolen lamp bar to market");
            c.Check(g.OpenBossCar(car) == 0, "a car that isn't their boss's has nothing of it");
            convoy.cars.Insert(0, ai);
            WastelandGame.LastReclaimed = null;
            int back = g.OpenBossCar(car);
            convoy.cars.Remove(ai);
            c.Check(back == 1 && WastelandGame.LastReclaimed == id && !Trade.InTransit(id) && !Trade.Stolen.ContainsKey(id), "the boss car's storage has it: off the market (" + g.ToastText + ")");
            yield return new WaitForSeconds(0.3f);
            var behind = car.transform.position - car.transform.forward * 3f;
            bool lying = Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Any(p => p.partId == "lights_bar" && !p.GetComponentInParent<VehicleDriver>() && (p.transform.position - behind).sqrMagnitude < 36f);
            c.Check(lying, "the lamp bar lies behind the car");
            c.Check(g.LookTicked.Count > 0 && g.LookTicked[0].Contains(gang), "LOOK FOR ticks it off");
            foreach (var p in Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None)) if (p.partId == "lights_bar" && !p.GetComponentInParent<VehicleDriver>()) Object.Destroy(p.gameObject);
            Object.Destroy(car.gameObject);
            Trade.LoadHauls(null);
        }
    }

    /// <summary>A survivor brought in greets the player by name and trades cheaper for a week; one left on the road who
    /// walks into town makes the news.</summary>
    class HitcherRemembers : Scenario
    {
        public override string Id => "npc.hitcher_remembers";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            // left on the road: they walk in and the town talks
            var walker = ScheduledScenarios1012.Person(g, "test:walker", NpcRole.Wanderer, 43, ScheduledScenarios1012.Beside(g, 5f));
            yield return new WaitForSeconds(0.3f);
            g.Strand(walker, town);
            var inside = ScheduledScenarios1013.TownCentre(town) + new Vector3(3f, 0f, 3f);
            walker.transform.position = inside;
            WastelandGame.LastWalkedIn = null;
            float t0 = Time.time;
            while (Time.time - t0 < 4f && WastelandGame.LastWalkedIn == null) yield return null;
            c.Check(WastelandGame.LastWalkedIn != null && WastelandGame.LastWalkedIn.Contains(Market.TownName(town)), "nobody stopped: the news has it (" + WastelandGame.LastWalkedIn + ")");
            c.Check(TownNews.Entries.Any(e => WastelandGame.LastWalkedIn != null && e.text == WastelandGame.LastWalkedIn), "on the town's board");
            c.Check(!g.Hitchers.Contains(walker), "and they are off the road");
            if (walker) Object.Destroy(walker.gameObject);
            // brought in: by name, cheaper for a week
            var vendor = ScheduledScenarios1012.Person(g, "test:rescued", NpcRole.Trader, 47, ScheduledScenarios1012.Beside(g, 3f));
            yield return new WaitForSeconds(0.3f);
            var S = vendor.State;
            S.Set(NpcSave.Met); S.disposition = 20; S.rescuedDay = -1;
            float b0 = Trade.Bargain(g, S);
            S.rescuedDay = DayNight.Day;
            float b1 = Trade.Bargain(g, S);
            c.Check(b1 > b0 + 0.05f || b1 >= 0.45f, $"a rescued trader bargains in the player's favour ({b0:0.00} → {b1:0.00})");
            var talk = new Dialogue(g, vendor);
            c.Note(talk.line);
            c.Check(talk.line != null && talk.line.Contains(g.Stats.name) && talk.line.Contains("PICKED ME UP"), "they greet the player by name");
            S.rescuedDay = DayNight.Day - WastelandGame.RescueDays;
            c.Check(!WastelandGame.Rescued(S) && Mathf.Abs(Trade.Bargain(g, S) - b0) < 0.001f, "a week later, ordinary prices");
            Object.Destroy(vendor.gameObject);
        }
    }

    /// <summary>The crew medic patching up townsfolk is news in their town; after two, the town calls for the medic on a
    /// bad day, and bringing them in answers it (scrap, standing).</summary>
    class MedicWord : Scenario
    {
        public override string Id => "npc.medic_word";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            var centre = ScheduledScenarios1013.TownCentre(town);
            var medic = ScheduledScenarios1009.Recruit(g, true, centre + new Vector3(-2f, 0f, 0f));
            var patient = ScheduledScenarios1012.Person(g, "test:townsfolk", NpcRole.Resident, 53, centre + new Vector3(2f, 0f, 0f));
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(medic && medic.pack && patient, "a crew medic and a townsperson in " + Market.TownName(town))) yield break;
            medic.pack.inventory.AddItem("med_bandage", 3);
            g.MedicWord.Remove(town.index); g.MedicCalls.Remove(town.index);
            int news0 = TownNews.Entries.Count;
            g.CompanionTreatOther(medic, patient, null);
            g.CompanionTreatOther(medic, patient, null);
            c.Check(g.MedicWord.TryGetValue(town.index, out int w) && w == 2, "the town counts the crew's treatments (" + w + ")");
            c.Check(TownNews.Entries.Count == news0 + 1 && TownNews.Entries.Last().text.Contains(medic.KnownName), "word gets round once a day: " + (TownNews.Entries.Count > 0 ? TownNews.Entries.Last().text : "-"));
            c.Check(g.RollMedicCalls(true) >= 1 && g.MedicCalls.ContainsKey(town.index), "on a bad day the town calls for the medic: " + WastelandGame.LastMedicCall);
            var look = new List<WastelandGame.Pin>(); g.LookFor(look);
            c.Check(look.Any(p => p.label.Contains("NEEDS YOUR MEDIC")), "the call is on LOOK FOR");
            int scrap = g.Inventory.Get(ResourceType.Scrap);
            WastelandGame.LastMedicHelp = null;
            g.Player.Teleport(centre, 0f);
            medic.transform.position = centre + new Vector3(2f, 0f, 1f);
            float t0 = Time.time;
            while (Time.time - t0 < 5f && WastelandGame.LastMedicHelp == null) yield return null;
            c.Check(WastelandGame.LastMedicHelp == Market.TownName(town) && !g.MedicCalls.ContainsKey(town.index), "bringing the medic into town answers it: " + g.ToastText);
            c.Check(g.Inventory.Get(ResourceType.Scrap) == scrap + WastelandGame.MedicFee, "paid " + WastelandGame.MedicFee + " scrap");
            ScheduledScenarios1009.Drop(g, medic);
            if (patient) Object.Destroy(patient.gameObject);
            g.MedicWord.Remove(town.index);
        }
    }

    /// <summary>A companion on post at home reads the base into the dusk report (out of food) and voices the station
    /// calls from that claim.</summary>
    class CrewBase : Scenario
    {
        public override string Id => "npc.crew_base";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            var flag = SurvivalKit.Piece(g, "claim_flag", pad + new Vector3(-3f, 0f, 0f), Vector3.forward);
            yield return null;
            if (!c.Check(flag, "a claim flag")) yield break;
            var spot = pad + new Vector3(1f, 0f, 1f); spot.y = t.Height(spot.x, spot.z) + 0.1f;
            var n = ScheduledScenarios1009.Recruit(g, false, spot);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(n && n.pack, "a companion at home")) { Object.Destroy(flag.gameObject); yield break; }
            if (n.pack.inventory.GetItem(SafetyTools.HandRadio) == 0) n.pack.inventory.AddItem(SafetyTools.HandRadio);
            n.order = 1; n.home = n.transform.position; n.homeRadius = 1f;
            var food = new List<KeyValuePair<string, int>>();
            foreach (var kv in g.Inventory.Items.ToList()) if (FoodLibrary.Get(kv.Key) != null && kv.Value > 0) { food.Add(kv); g.Inventory.TakeItem(kv.Key, kv.Value); }
            var worries = new List<string>(); g.HomeWorries(n.transform.position, worries);
            c.Note("worries: " + string.Join("; ", worries));
            string call = Companions.CheckInNow(g, n);
            c.Note(call);
            c.Check(call.Contains("FOOD"), "the dusk report says the pantry is empty");
            c.Check(WastelandGame.HomeVoice(n.transform.position) == n, "they answer the home frequency for this claim");
            c.Check(WastelandGame.HomeVoice(n.transform.position + new Vector3(400f, 0f, 0f)) == null, "but not for somewhere else");
            foreach (var kv in food) g.Inventory.AddItem(kv.Key, kv.Value);
            ScheduledScenarios1009.Drop(g, n);
            Object.Destroy(flag.gameObject);
        }
    }

    /// <summary>Town talk names a buried stash: a search circle on LOOK FOR (told once a week); walking up to the post
    /// finds a half-buried crate with goods and ticks it off.</summary>
    class CacheRumour : Scenario
    {
        public override string Id => "ui.cache_rumour";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            g.Caches.Clear();
            var centre = ScheduledScenarios1013.TownCentre(town);
            string said = g.CacheRumour(centre);
            c.Note(said);
            c.Check(said != null && said.Contains("STASH") && g.Caches.Count == 1, "someone in town tells of a buried stash");
            c.Check(g.CacheRumour(centre) == null, "the same stash isn't told twice this week");
            if (g.Caches.Count == 0) yield break;
            var cache = g.Caches[0];
            var again = g.CacheNear(town, DayNight.Day / 7);
            c.Check(again != null && (again.at - cache.at).sqrMagnitude < 0.01f, "the spot is the same for the same week (deterministic)");
            var look = new List<WastelandGame.Pin>(); g.LookFor(look);
            var pin = look.FirstOrDefault(p => p.label == "A BURIED STASH");
            c.Check(pin.label != null && pin.radius == WastelandGame.CacheRadius && Vector3.Distance(new Vector3(pin.pos.x, 0f, pin.pos.z), new Vector3(cache.at.x, 0f, cache.at.z)) <= WastelandGame.CacheRadius, "a search circle on LOOK FOR");
            var at = cache.at + new Vector3(2f, 0f, 0f);
            at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.3f;
            g.Player.Teleport(at, 0f);
            float t0 = Time.time;
            while (Time.time - t0 < 15f && !cache.found) yield return null;
            c.Check(cache.found, "walking up to the post finds it: " + g.ToastText);
            var box = Object.FindObjectsByType<Container>(FindObjectsSortMode.None).FirstOrDefault(b => b.title == "BURIED STASH" && (b.transform.position - cache.at).sqrMagnitude < 25f);
            c.Check(box && ItemCatalog.TotalWeight(box.inventory) > 0f, "a half-buried crate with goods in it");
            c.Check(g.LookTicked.Count > 0 && g.LookTicked[0] == "A BURIED STASH", "LOOK FOR ticks it off");
            look.Clear(); g.LookFor(look);
            c.Check(!look.Any(p => p.label.StartsWith("A BURIED STASH")), "and it leaves the list");
            yield return null;
            c.Screenshot("cache_found");
            yield return null;
            if (box) Object.Destroy(box.gameObject);
            g.Caches.Clear();
        }
    }

    /// <summary>A frosty start: the OIL lamp stays amber a few seconds while the pressure builds and the idle runs fast
    /// until warm; a warm engine builds pressure at once and idles normally.</summary>
    class FrostGauges : Scenario
    {
        public override string Id => "vehicle.frost_gauges";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            float base0 = Weather.BaseTemperature, offset = Weather.Temperature - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, -18f - offset);
            var v = g.SpawnAiVehicle("Sedan", pad + Vector3.up * 0.5f, Quaternion.identity);
            if (!c.Check(v, "a sedan")) { Weather.Restore(false, 0f, 0f, base0); yield break; }
            yield return new WaitForSeconds(0.3f);
            var s = v.GetComponent<VehicleSystems>();
            var e = v.Engine;
            v.aiDriven = false; v.handbrake = true; s.Stop(); s.SetTemperature(Weather.Temperature); s.fuel = Mathf.Max(s.fuel, 10f);
            yield return new WaitForSeconds(0.3f);
            c.Fixture($"air {Weather.Temperature:0} C, engine {s.Temperature:0} C");
            c.Check(s.OilPressure < 0.05f && !s.OilPressureLow, "engine off: no pressure");
            s.ForceStart();
            yield return new WaitForSeconds(1f);
            c.Check(s.OilPressureLow, $"one second after a frosty start the OIL lamp is still lit ({s.OilPressure:0.00} bar, rise {s.OilRiseSeconds:0.0} s)");
            c.Check(e && e.IdleRpm > e.idleRpm * 1.3f, $"fast idle while cold ({(e ? e.IdleRpm : 0f):0} vs {(e ? e.idleRpm : 0f):0} rpm)");
            float t0 = Time.time;
            while (Time.time - t0 < 15f && s.OilPressureLow) yield return null;
            c.Metric("oil_rise_seconds", 1f + Time.time - t0, "s");
            c.Check(!s.OilPressureLow, "the pressure comes up and the lamp goes out");
            s.SetTemperature(85f);
            yield return new WaitForSeconds(0.3f);
            c.Check(e && Mathf.Abs(e.IdleRpm - e.idleRpm) < 1f, "warm: normal idle");
            s.Stop();
            Weather.Restore(false, 0f, 0f, 20f - offset);
            yield return new WaitForSeconds(0.8f);
            s.ForceStart();
            yield return new WaitForSeconds(1f);
            c.Check(!s.OilPressureLow, $"a warm start builds pressure at once ({s.OilPressure:0.00} bar)");
            Weather.Restore(false, 0f, 0f, base0);
            Object.Destroy(v.gameObject);
        }
    }

    /// <summary>A broken leg at home by day: the bed offers LIE UP; four hours in it mend the break faster than walking
    /// would, read the book in the pack, and a neighbour from town brings food.</summary>
    class LieUp : Scenario
    {
        public override string Id => "body.lie_up";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -2f), 0f);
            yield return new WaitForSeconds(0.3f);
            var flag = SurvivalKit.Piece(g, "claim_flag", pad + new Vector3(-3f, 0f, 0f), Vector3.forward);
            var bed = SurvivalKit.Piece(g, "bed", pad + new Vector3(1.5f, 0f, 0f), Vector3.forward);
            yield return null;
            if (!c.Check(flag && bed, "a claim flag and a bed")) yield break;
            var b = bed.GetComponent<Bed>();
            float h0 = DayNight.Hours;
            DayNight.SetHours(10f);
            g.Stats.injuries.Clear();
            c.Check(!g.CanLieUp(b), "whole: the bed only offers rest");
            var leg = new Injury { zone = BodyZone.LegL, type = Wound.Fracture, severity = 0.9f, splinted = true };
            g.Stats.injuries.Add(leg);
            c.Check(g.CanLieUp(b) && b.Prompt(g).Contains("LIE UP"), "with a broken leg, at home, by day: " + b.Prompt(g));
            g.Inventory.AddItem("book_first_aid");
            g.Stats.consumed.Remove("book_first_aid");
            int food0 = g.Inventory.Items.Where(kv => FoodLibrary.Get(kv.Key) != null).Sum(kv => kv.Value);
            float sev0 = leg.severity;
            WastelandGame.LastVisit = null;
            b.Use(g, false);
            c.Note(g.ToastText);
            c.Check(Mathf.Abs(DayNight.Hours - 14f) < 0.05f, $"four hours pass ({DayNight.Hours:0.0} h)");
            float healed = sev0 - leg.severity;
            var walk = new Injury { zone = BodyZone.LegR, type = Wound.Fracture, severity = 0.9f, splinted = true };
            walk.Rest(WastelandGame.LieUpHours * DayNight.DayMinutes * 60f / 24f, 1f);
            c.Check(healed > (0.9f - walk.severity) * 1.5f, $"the break mends faster than on its feet ({healed:0.000} vs {0.9f - walk.severity:0.000})");
            c.Check(g.LearningId == "book_first_aid" && g.LearningProgress > 0.05f, $"the book in the pack gets read ({g.LearningProgress:0.00})");
            string visit = WastelandGame.LastVisit ?? g.ForceNeighbourVisit(bed.transform.position);
            c.Check(visit != null && g.Inventory.Items.Where(kv => FoodLibrary.Get(kv.Key) != null).Sum(kv => kv.Value) > food0, "a neighbour from town brings food: " + visit);
            DayNight.SetHours(22f);
            c.Check(!g.CanLieUp(b), "at night the bed is for sleeping");
            DayNight.SetHours(h0);
            g.Stats.injuries.Clear();
            Object.Destroy(bed.gameObject); Object.Destroy(flag.gameObject);
        }
    }
}
