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
    /// <summary>Scheduled update 2026-10-14: neighbours by name, cache rivals, the boss car chase, the medic's kit
    /// running dry, the hitcher network, the oil-pressure sag of a worn engine.</summary>
    public static class ScheduledScenarios1014
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new NeighbourByName();
            yield return new CacheRivals();
            yield return new BossCarChase();
            yield return new MedicKitDry();
            yield return new HitcherTip();
            yield return new OilSag();
        }
    }

    /// <summary>The neighbour who looks in on the laid-up player is a real resident of the town: met, warmer by
    /// <see cref="WastelandGame.NeighbourWarmth"/>, and asks after the leg the next time they talk (once).</summary>
    class NeighbourByName : Scenario
    {
        public override string Id => "npc.neighbour_by_name";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var home = g.Player.transform.position;
            var town = g.NearestTown(home);
            if (town == null || Vector2.Distance(town.pos, new Vector2(home.x, home.z)) > WastelandGame.VisitReach) { c.Block("no town within visiting reach"); yield break; }
            WastelandGame.LastNeighbour = null;
            string visit = g.ForceNeighbourVisit(home);
            c.Note(visit);
            string id = WastelandGame.LastNeighbour;
            if (!c.Check(id != null && id.StartsWith("r" + town.index + ","), "a resident of " + Market.TownName(town) + " came by: " + visit)) yield break;
            var prof = NpcProfile.Make(id, NpcRole.Resident, NpcDirector.StableKey(id) ^ g.World.seed);
            var s = NpcRegistry.Get(prof);
            c.Check(visit.Contains(prof.Name), "the visit names them (" + prof.Name + ")");
            c.Check(s.Has(NpcSave.Met) && g.NeighbourVisits.ContainsKey(id), "they are met and remembered as a neighbour");
            int disp = s.disposition;
            g.Stats.injuries.Clear();
            g.Stats.injuries.Add(new Injury { zone = BodyZone.LegL, type = Wound.Fracture, severity = 0.8f, splinted = true });
            var n = MadMax.Npc.Npc.Spawn(prof, ScheduledScenarios1012.Beside(g, 2.5f), 180f, null, g.propMaterial);
            yield return new WaitForSeconds(0.3f);
            var talk = new Dialogue(g, n);
            c.Note(talk.line);
            c.Check(talk.line != null && talk.line.Contains("LEG") && talk.line.Contains(g.Stats.name), "next time they meet they ask after the leg");
            c.Check(s.disposition > disp, "and warm to the player for it");
            var again = new Dialogue(g, n);
            c.Check(again.line == null || !again.line.Contains("LEG"), "only once");
            g.Stats.injuries.Clear();
            Object.Destroy(n.gameObject);
        }
    }

    /// <summary>A cache left past <see cref="WastelandGame.CacheRivalDays"/> may be dug up first: the crate is empty,
    /// fresh tracks lead to a digger walking toward town (on LOOK FOR); caught up with on foot, they drop half and run.</summary>
    class CacheRivals : Scenario
    {
        public override string Id => "ui.cache_rivals";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            g.Caches.Clear();
            var cache = g.CacheNear(town, DayNight.Day / 7);
            if (!c.Check(cache != null, "this week's cache near " + Market.TownName(town))) yield break;
            c.Check(!g.RivalBeatYou(cache), "a cache just heard of is still there");
            int dug = 0;
            for (int i = 0; i < 200; i++) { var k = new WastelandGame.Cache { key = "cache:test:" + i, heard = DayNight.TotalDays - 5f }; if (g.RivalBeatYou(k)) dug++; }
            c.Metric("dug_share", dug / 200f, "");
            c.Check(dug > 90 && dug < 170, $"left five days, about two in three are dug up first ({dug}/200)");
            cache.heard = DayNight.TotalDays - 5f;
            for (int i = 0; i < 200 && !g.RivalBeatYou(cache); i++) cache.key = "cache:test:" + i; // one the roll says was dug up
            g.Caches.Add(cache);
            var near = cache.at + new Vector3(30f, 0f, 0f);
            near.y = DeformableTerrain.Instance.Height(near.x, near.z) + 0.3f;
            g.Player.Teleport(near, 0f);
            float t0 = Time.time;
            while (Time.time - t0 < 10f && !g.Diggers.ContainsKey(cache.key)) yield return null;
            var digger = g.Diggers.TryGetValue(cache.key, out var d) ? d : null;
            if (!c.Check(digger && digger.pack && Lootable.HasAny(digger.pack.inventory), "a digger walks off with the stash")) { g.Caches.Clear(); yield break; }
            var box = Object.FindObjectsByType<Container>(FindObjectsSortMode.None).FirstOrDefault(b => b.title.StartsWith("BURIED STASH") && (b.transform.position - cache.at).sqrMagnitude < 25f);
            c.Check(box && !Lootable.HasAny(box.inventory), "the crate is empty");
            var look = new List<WastelandGame.Pin>(); g.LookFor(look);
            c.Check(look.Any(p => p.label == "WHOEVER DUG UP THE STASH"), "the digger is on LOOK FOR");
            var at = cache.at + new Vector3(1.5f, 0f, 0f);
            at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.3f;
            g.Player.Teleport(at, 0f);
            t0 = Time.time;
            while (Time.time - t0 < 10f && !cache.found) yield return null;
            c.Check(cache.found && WastelandGame.LastDugUp != null && WastelandGame.LastDugUp.Contains("TRACKS LEAD"), "the hole tells the story: " + WastelandGame.LastDugUp);
            yield return null;
            c.Screenshot("cache_dug_up");
            yield return null;
            int before = Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None).Length;
            var by = digger.transform.position - digger.transform.forward * 2f;
            by.y = DeformableTerrain.Instance.Height(by.x, by.z) + 0.3f;
            g.Player.Teleport(by, 0f);
            t0 = Time.time;
            while (Time.time - t0 < 6f && g.Diggers.ContainsKey(cache.key)) yield return null;
            int after = Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None).Length;
            c.Check(!g.Diggers.ContainsKey(cache.key) && after > before, $"caught up with, they drop half and run ({after - before} dropped): " + g.ToastText);
            c.Check(digger && digger.pack && Lootable.HasAny(digger.pack.inventory), "and keep the rest");
            if (digger) Object.Destroy(digger.gameObject);
            if (box) Object.Destroy(box.gameObject);
            g.Caches.Clear();
        }
    }

    /// <summary>A gang told the player wants a part back runs: the boss car takes the road away from the player
    /// instead of blocking it; boxed in, the crew come out fighting.</summary>
    class BossCarChase : Scenario
    {
        public override string Id => "towns.boss_car_chase";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var t = DeformableTerrain.Instance;
            Vector3 pad = default, fwd = Vector3.forward; bool found = false;
            var from = g.Player.transform.position;
            for (int k = 0; k < 160 && !found; k++)                                                // open country: raiders keep out of towns
            {
                float a = k * 2.39996f, r = 140f + k * 4f;
                var p = from + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                var f = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bool ok = true;
                for (float d = -40f; d <= 60f && ok; d += 10f)
                {
                    var q = p + f * d;
                    ok = g.World.SettlementAt(q.x, q.z) == null && t.WaterDepthNoLoad(q.x, q.z) < 0.05f && Mathf.Abs(t.HeightNoLoad(q.x, q.z) - t.HeightNoLoad(p.x, p.z)) < 4f;
                }
                if (ok) { pad = p; fwd = f; found = true; }
            }
            if (!found) { c.Block("no open country out of town"); yield break; }
            pad.y = t.Height(pad.x, pad.z);
            var ahead = pad + fwd * 30f;
            g.Player.Teleport(ahead + Vector3.up * 0.5f, 0f);
            yield return new WaitForSeconds(2f);                                                   // the ground streams in
            pad.y = t.Height(pad.x, pad.z);
            var road = new List<Vector3>();
            for (int i = -10; i <= 10; i++) { var p = pad + fwd * (i * 20f); p.y = DeformableTerrain.Instance.Height(p.x, p.z); road.Add(p); }
            var convoy = new Convoy("test:chase", true, null, road, 7, new ConvoySave { id = "test:chase" });
            var car = g.SpawnAiVehicle("Interceptor", pad + Vector3.up * 0.6f, Quaternion.LookRotation(fwd));
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(car, "a raider boss car")) yield break;
            if (car.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = sys.fuelCapacity; sys.oil = sys.oilCapacity; sys.coolant = sys.coolantCapacity; }
            var ai = car.gameObject.AddComponent<AiDriver>();
            ai.SetPath(convoy.route, 1);
            convoy.cars.Add(ai);
            string gang = convoy.Gang, id = "part:lights_bar";
            Trade.LoadHauls(null);
            Trade.AddStolen(g.NearestTown(pad), id, pad, gang);
            c.Check(!WastelandGame.GangRuns(gang), "before the salvager talks, the gang doesn't run");
            g.MarkWary(gang);
            c.Check(WastelandGame.GangRuns(gang), "once word gets back, it does");
            var player = ahead + Vector3.Cross(Vector3.up, fwd) * 4f;                              // ahead of the car, beside its road
            player.y = DeformableTerrain.Instance.Height(player.x, player.z) + 0.3f;
            g.Player.Teleport(player, Quaternion.LookRotation(-fwd).eulerAngles.y);
            WastelandGame.LastRun = null;
            convoy.Tick(g, player, 0.1f);
            c.Check(convoy.running && convoy.phase == Convoy.Phase.Flee && WastelandGame.LastRun == gang, "seen, the boss car runs instead of blocking the road: " + g.ToastText);
            c.Check(ai.dir == -1 && ai.cruise >= Convoy.RunSpeed, $"along the road away from the player (dir {ai.dir}, {ai.cruise:0} m/s)");
            var start = car.transform.position;
            float t0 = Time.time;
            while (Time.time - t0 < 9f) { convoy.Tick(g, g.Player.transform.position, Time.deltaTime); yield return null; }
            float gain = Vector3.Distance(car.transform.position, g.Player.transform.position) - Vector3.Distance(start, g.Player.transform.position);
            c.Metric("pulled_away_m", gain, "m");
            c.Check(gain > 15f, $"it turns round and pulls away ({gain:0.0} m in 9 s)");
            // boxed in: held still with the player beside it
            var rb = car.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero; rb.isKinematic = true;
            var beside = car.transform.position + car.transform.right * 6f;
            beside.y = DeformableTerrain.Instance.Height(beside.x, beside.z) + 0.3f;
            g.Player.Teleport(beside, 0f);
            t0 = Time.time;
            while (Time.time - t0 < 6f && convoy.phase == Convoy.Phase.Flee) { convoy.Tick(g, g.Player.transform.position, Time.deltaTime); yield return null; }
            c.Check(!convoy.running && convoy.phase == Convoy.Phase.Attack, "boxed in, they come out fighting: " + g.ToastText);
            foreach (var w in convoy.walkers) if (w) Object.Destroy(w.gameObject);
            convoy.cars.Clear();
            if (car) Object.Destroy(car.gameObject);
            g.GangWary.Remove(gang);
            Trade.LoadHauls(null);
        }
    }

    /// <summary>A town call uses the medic's dressings and pays two bandages back; a medic with nothing left says so
    /// (on the radio when posted away), once a day.</summary>
    class MedicKitDry : Scenario
    {
        public override string Id => "npc.medic_kit";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            var medic = ScheduledScenarios1009.Recruit(g, true, ScheduledScenarios1012.Beside(g, 3f));
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(medic && medic.pack, "a crew medic")) yield break;
            foreach (var id in WastelandGame.MedicStock) medic.pack.inventory.TakeItem(id, medic.pack.inventory.GetItem(id));
            medic.pack.inventory.AddItem(Companions.FirstAidKit, 1);
            g.MedicCalls[town.index] = DayNight.Day + 1;
            c.Check(g.AnswerMedicCall(town, medic), "the medic answers the town's call");
            c.Check(WastelandGame.LastMedicUsed == "A FIRST-AID KIT" && medic.pack.inventory.GetItem(Companions.FirstAidKit) == 0, "using their first-aid kit");
            c.Check(medic.pack.inventory.GetItem("med_bandage") == WastelandGame.TownPaysBandages, "the town pays part in bandages");
            foreach (var id in WastelandGame.MedicStock) medic.pack.inventory.TakeItem(id, medic.pack.inventory.GetItem(id));
            WastelandGame.LastKitDry = null;
            g.UpdateMedicKit(true);
            c.Check(WastelandGame.LastKitDry != null && WastelandGame.LastKitDry.Contains("OUT OF DRESSINGS"), "an empty kit, they say so: " + WastelandGame.LastKitDry);
            WastelandGame.LastKitDry = null;
            g.UpdateMedicKit();
            c.Check(WastelandGame.LastKitDry == null, "once a day");
            ScheduledScenarios1009.Drop(g, medic);
        }
    }

    /// <summary>A survivor brought into town gets on the home frequency once within the week with a tip: a stash
    /// near their town (onto LOOK FOR) or where a raider gang rides.</summary>
    class HitcherTip : Scenario
    {
        public override string Id => "npc.hitcher_tip";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            var n = ScheduledScenarios1012.Person(g, "test:tipper", NpcRole.Trader, 61, ScheduledScenarios1012.Beside(g, 3f));
            yield return new WaitForSeconds(0.3f);
            g.Rescues.Clear(); g.Caches.Clear();
            n.State.rescuedDay = DayNight.Day;
            g.Rescues[n.Profile.id] = new WastelandGame.Rescue { town = town.index, name = n.Profile.Name };
            int day = WastelandGame.TipDay(n.State);
            c.Check(day > DayNight.Day && day < DayNight.Day + WastelandGame.RescueDays, $"they will call within the week (day {day}, rescued {DayNight.Day})");
            WastelandGame.LastTip = null;
            string tip = g.HitcherTip(n.Profile.id);
            c.Note(tip);
            c.Check(tip != null && tip.Contains(n.Profile.Name) && (tip.Contains("STASH") || tip.Contains("RIDING")), "a tip in their voice");
            c.Check(tip == null || !tip.Contains("STASH") || g.Caches.Count == 1, "a stash tip goes onto LOOK FOR");
            c.Check(g.Rescues[n.Profile.id].tipped, "one tip each");
            var d = new SaveData();
            var save = typeof(WastelandGame).GetMethod("SaveNightly1013", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var load = typeof(WastelandGame).GetMethod("LoadNightly1013", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            save.Invoke(g, new object[] { d });
            g.Rescues.Clear();
            load.Invoke(g, new object[] { d });
            c.Check(g.Rescues.TryGetValue(n.Profile.id, out var r) && r.tipped && r.name == n.Profile.Name && r.town == town.index, "saved and restored");
            g.Rescues.Clear(); g.Caches.Clear();
            Object.Destroy(n.gameObject);
        }
    }

    /// <summary>A worn engine's oil pressure sags at a hot idle (the lamp may flicker) where a healthy one holds;
    /// revs bring some of it back.</summary>
    class OilSag : Scenario
    {
        public override string Id => "vehicle.oil_sag";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            var v = g.SpawnAiVehicle("Sedan", pad + Vector3.up * 0.5f, Quaternion.identity);
            if (!c.Check(v, "a sedan")) yield break;
            yield return new WaitForSeconds(0.3f);
            var s = v.GetComponent<VehicleSystems>();
            var ep = v.Engine ? v.Engine.GetComponent<VehiclePart>() : null;
            if (!c.Check(s && ep, "with an engine")) { Object.Destroy(v.gameObject); yield break; }
            v.aiDriven = false; v.handbrake = true; s.fuel = Mathf.Max(s.fuel, 10f); s.oil = s.oilCapacity; s.oilLife = 1f;
            ep.damage = 0f;
            s.SetTemperature(90f);
            s.ForceStart();
            yield return new WaitForSeconds(2f);
            float healthy = s.OilPressure;
            ep.damage = 0.85f; s.oilLife = 0.2f;
            yield return new WaitForSeconds(2.5f);
            float worn = s.OilPressure;
            c.Metric("idle_bar_healthy", healthy, "bar"); c.Metric("idle_bar_worn", worn, "bar");
            c.Check(worn < healthy * 0.6f, $"the worn engine's needle sags at a hot idle ({healthy:0.00} → {worn:0.00} bar)");
            c.Check(s.OilSagAt(1f) > s.OilSagAt(0f) + 0.2f, $"revs bring some back ({s.OilSagAt(0f):0.00} idle vs {s.OilSagAt(1f):0.00} at the top)");
            ep.damage = 0f; s.oilLife = 1f;
            s.Stop();
            Object.Destroy(v.gameObject);
        }
    }
}
