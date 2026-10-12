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
    /// <summary>Scheduled update 2026-10-12: the crew medic treats others, crew check-ins on the home frequency, the
    /// glow-plug relay and the frost-heavy starter, taking stolen parts back off a raider boss, the journal's LOOK FOR
    /// list, survivors of a burned trader who need a lift.</summary>
    public static class ScheduledScenarios1012
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MedicOthers();
            yield return new CrewCheckIn();
            yield return new GlowRelay();
            yield return new TakeItBack();
            yield return new LookForList();
            yield return new Hitcher();
        }

        internal static MadMax.Npc.Npc Person(WastelandGame g, string id, NpcRole role, int seed, Vector3 at)
        {
            var t = DeformableTerrain.Instance; at.y = t.Height(at.x, at.z) + 0.1f;
            return MadMax.Npc.Npc.Spawn(NpcProfile.Make(id, role, seed), at, 0f, null, g.propMaterial);
        }

        internal static Vector3 Beside(WastelandGame g, float right, float fwd = 0f)
        {
            var p = g.Player.transform.position + g.Player.transform.right * right + g.Player.transform.forward * fwd;
            p.y = DeformableTerrain.Instance.Height(p.x, p.z) + 0.1f;
            return p;
        }
    }

    /// <summary>The player is whole: an untrained companion leaves a bleeding townsperson be; one with the knack and a
    /// bandage walks over and dresses them — the bleeding stops, the bandage is used, the person warms to the player.</summary>
    class MedicOthers : Scenario
    {
        public override string Id => "npc.medic_others";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            g.Stats.injuries.Clear();
            var hurt = ScheduledScenarios1012.Person(g, "test:hurt_local", NpcRole.Resident, 31, ScheduledScenarios1012.Beside(g, 6f, 4f));
            var raw = ScheduledScenarios1009.Recruit(g, false, ScheduledScenarios1012.Beside(g, -3f));
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(hurt && raw && raw.pack, "a townsperson and an untrained companion")) yield break;
            raw.pack.inventory.AddItem("med_bandage", 2);
            hurt.Bleed(90f);
            int disp0 = hurt.State.disposition;
            Companions.LastPatient = null;
            c.Fixture("the townsperson bleeds; the player has no wounds");
            yield return new WaitForSeconds(4f);
            c.Check(Companions.LastPatient == null && hurt.Bleeding, "without the knack nobody goes to them");
            ScheduledScenarios1009.Drop(g, raw);
            var medic = ScheduledScenarios1009.Recruit(g, true, ScheduledScenarios1012.Beside(g, -3f));
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(medic && medic.pack && Companions.Patches(medic.Profile), "a companion who knows wounds")) { if (hurt) Object.Destroy(hurt.gameObject); yield break; }
            int kits = medic.pack.inventory.GetItem(Companions.FirstAidKit);
            if (kits > 0) medic.pack.inventory.TakeItem(Companions.FirstAidKit, kits);
            medic.pack.inventory.AddItem("med_bandage", 2);
            float t0 = Time.time;
            hurt.Bleed(90f);
            float logAt = 0f;
            while (Time.time - t0 < 20f && hurt.Bleeding)
            {
                if (Time.time > logAt) { logAt = Time.time + 2f; c.Note($"t {Time.time - t0:0.0}: medic tending {medic.tending} who {(medic.tendWho ? medic.tendWho.Profile.id : "-")} dist {Vector3.Distance(medic.transform.position, hurt.transform.position):0.0} m, patient hp {hurt.Health:0}, medic at {medic.transform.position}, patient at {hurt.transform.position}, mode {medic.mode}/{hurt.mode}"); }
                yield return null;
            }
            c.Metric("treat_seconds", Time.time - t0, "s");
            c.Check(Companions.LastPatient == hurt, "the medic goes to the hurt townsperson: " + g.ToastText);
            c.Check(!hurt.Bleeding && hurt.Alive, "the bleeding is stopped");
            c.Check(medic.pack.inventory.GetItem("med_bandage") == 1, "one bandage used");
            c.Check(hurt.State.disposition >= disp0 + 10 && hurt.State.Has(NpcSave.Helped), $"they remember it (disposition {disp0} → {hurt.State.disposition})");
            ScheduledScenarios1009.Drop(g, medic);
            if (hurt) Object.Destroy(hurt.gameObject);
        }
    }

    /// <summary>A companion holding a post with a handheld radio calls in at dusk over the home frequency (once a day,
    /// out of earshot only); the call reaches the journal and the player's handheld.</summary>
    class CrewCheckIn : Scenario
    {
        public override string Id => "npc.crew_checkin";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var spot = ScheduledScenarios1012.Beside(g, 3f);
            var n = ScheduledScenarios1009.Recruit(g, false, spot);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(n && n.pack, "a companion")) yield break;
            if (n.pack.inventory.GetItem(SafetyTools.HandRadio) == 0) n.pack.inventory.AddItem(SafetyTools.HandRadio);
            if (g.Inventory.GetItem(SafetyTools.HandRadio) == 0) g.Inventory.AddItem(SafetyTools.HandRadio);
            n.order = 1; n.home = n.transform.position; n.homeRadius = 1f;
            float h0 = DayNight.Hours;
            DayNight.SetHours(19.5f);
            Companions.LastCheckIn = null;
            yield return new WaitForSeconds(3f);
            c.Check(Companions.LastCheckIn == null, "standing beside the player: no radio call");
            var far = g.Player.transform.position + g.Player.transform.forward * 80f;
            far.y = DeformableTerrain.Instance.Height(far.x, far.z) + 0.3f;
            g.Player.Teleport(far, 0f);
            int calls = g.HandRadioCalls;
            yield return new WaitForSeconds(3f);
            c.Note(Companions.LastCheckIn);
            c.Check(Companions.LastCheckIn != null && Companions.LastCheckIn.Contains("ALL QUIET"), "at dusk, 80 m away, they call in");
            c.Check(g.LastHomeCall == Companions.LastCheckIn, "it goes out on the home frequency");
            c.Check(g.HandRadioCalls > calls || MadMax.Audio.RadioNetwork.Heard(), "the handheld in the pack picks it up");
            Companions.LastCheckIn = null;
            yield return new WaitForSeconds(3f);
            c.Check(Companions.LastCheckIn == null, "once a day");
            n.order = 2;
            c.Check(Companions.CheckInNow(g, n).Contains("GUARD"), "a guard says the guard is up");
            var dir = NpcDirector.Instance;
            float rd = float.MaxValue;
            var raiders = dir ? dir.NearestRaiders(n.transform.position, out rd) : null;
            c.Fixture(raiders != null ? $"nearest raider column {rd:0} m from the post" : "no raider column on the roads");
            c.Check(!Companions.RaidAlarm(g, n) || (raiders != null && rd <= Companions.AlarmReach), "no alarm without raiders near the post");
            DayNight.SetHours(h0);
            ScheduledScenarios1009.Drop(g, n);
        }
    }

    /// <summary>A diesel in a hard frost: the glow relay clicks out once when the plugs are hot, the starter is
    /// frost-heavy below 0 °C (oil drag) and free above it.</summary>
    class GlowRelay : Scenario
    {
        public override string Id => "vehicle.glow_relay";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            float base0 = Weather.BaseTemperature, offset = Weather.Temperature - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, -15f - offset);
            var v = g.SpawnAiVehicle("Peugeot406Break", pad + Vector3.up * 0.5f, Quaternion.identity);
            if (!c.Check(v, "a diesel estate")) { Weather.Restore(false, 0f, 0f, base0); yield break; }
            var s = v.GetComponent<VehicleSystems>();
            v.aiDriven = false; v.handbrake = true; s.Stop(); s.SetTemperature(Weather.Temperature); s.fuel = Mathf.Max(s.fuel, 10f);
            c.Fixture($"air {Weather.Temperature:0} C, engine {s.Temperature:0} C");
            c.Check(s.FrostDrag > 0.5f, $"frost-thick oil drags the starter ({s.FrostDrag:0.00})");
            int clicks = s.GlowClicks;
            v.Occupied = true;
            float t0 = Time.time;
            while (Time.time - t0 < 15f && !s.GlowReady) yield return null;
            yield return new WaitForSeconds(0.3f);
            c.Metric("glow_seconds", Time.time - t0, "s");
            c.Check(s.GlowReady && s.GlowClicks == clicks + 1, "the relay clicks out once when the plugs are hot");
            yield return new WaitForSeconds(1f);
            c.Check(s.GlowClicks == clicks + 1, "and only once");
            v.Occupied = false;
            Weather.Restore(false, 0f, 0f, 10f - offset);
            c.Check(s.FrostDrag == 0f, "above freezing the starter turns freely");
            Weather.Restore(false, 0f, 0f, base0);
            Object.Destroy(v.gameObject);
        }
    }

    /// <summary>A part stolen by a gang is carried by their boss until market day: killing the boss drops it beside the
    /// body and it never reaches the stall; once on the stall, the boss has nothing of the player's.</summary>
    class TakeItBack : Scenario
    {
        public override string Id => "towns.take_back";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var boss = ScheduledScenarios1012.Person(g, "test:boss", NpcRole.RaiderBoss, 77, ScheduledScenarios1012.Beside(g, 0f, 12f));
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(boss, "a raider boss")) yield break;
            string gang = boss.Profile.gang, id = "part:lights_bar";
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) { Object.Destroy(boss.gameObject); yield break; }
            Trade.AddStolen(town, id, g.Player.transform.position, gang);
            c.Check(Trade.InTransit(id) && Trade.CarriedBy(gang).Contains(id), "the stolen lamp bar rides with the " + gang + " until market day");
            var look = new List<WastelandGame.Pin>(); g.LookFor(look);
            c.Note("LOOK FOR: " + string.Join(", ", look.Select(p => p.label)));
            int loose0 = Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Count(p => !p.GetComponentInParent<VehicleDriver>() && (p.transform.position - boss.transform.position).sqrMagnitude < 25f);
            boss.ApplyHit(boss.transform.position + Vector3.up, Vector3.forward, 60f, 0.5f, null);
            yield return new WaitForSeconds(0.5f);
            c.Check(!boss.Alive, "the boss falls");
            c.Check(WastelandGame.LastReclaimed == id && !Trade.Stolen.ContainsKey(id) && !Trade.InTransit(id), "the part comes back and is off the market: " + g.ToastText);
            int loose1 = Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Count(p => !p.GetComponentInParent<VehicleDriver>() && (p.transform.position - boss.transform.position).sqrMagnitude < 25f);
            c.Check(loose1 > loose0, "it lies beside the body");
            c.Check(g.LookTicked.Count > 0 && g.LookTicked[0].Contains(gang), "LOOK FOR ticks it off");
            // after market day the boss carries nothing
            var boss2 = ScheduledScenarios1012.Person(g, "test:boss2", NpcRole.RaiderBoss, 77, ScheduledScenarios1012.Beside(g, 4f, 12f));
            Trade.AddStolen(town, id, g.Player.transform.position, boss2.Profile.gang);
            Trade.LoadHauls(Trade.SaveHauls().Select(l => l.StartsWith("@") ? Regex(l) : l).ToList());
            c.Check(!Trade.InTransit(id), "on the stall today: no longer in transit");
            WastelandGame.LastReclaimed = null;
            c.Check(g.ReclaimFromBoss(boss2.Profile.gang, boss2.transform.position) == 0 && WastelandGame.LastReclaimed == null, "the boss has nothing of the player's any more");
            Object.Destroy(boss2.gameObject);
            Trade.LoadHauls(null);
        }

        /// <summary>Move a saved market haul's arrival day to today ("@town|day|…").</summary>
        static string Regex(string line)
        {
            var f = line.Split('|');
            if (f.Length > 1) f[1] = DayNight.Day.ToString();
            return string.Join("|", f);
        }
    }

    /// <summary>A wreck in the news within reach that nobody marked is on the LOOK FOR list as a search circle; marking
    /// it (or spotting it) takes it off.</summary>
    class LookForList : Scenario
    {
        public override string Id => "ui.look_for";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var me = g.Player.transform.position;
            var wreck = me + new Vector3(300f, 0f, 120f);
            TownNews.Post("A TEST CONVOY WAS HIT ON THE ROAD", wreck + new Vector3(1f, 0f, 1f));
            TownNews.MarkWreck(wreck + new Vector3(1f, 0f, 1f), wreck);
            var look = new List<WastelandGame.Pin>(); g.LookFor(look);
            var pin = look.FirstOrDefault(p => p.label.StartsWith("A WRECK IN THE NEWS") && Vector3.Distance(new Vector3(p.pos.x, 0f, p.pos.z), new Vector3(wreck.x, 0f, wreck.z)) <= WastelandGame.NewsWreckRadius);
            c.Note("LOOK FOR: " + string.Join(", ", look.Select(p => p.label)));
            c.Check(pin.label != null && pin.radius == WastelandGame.NewsWreckRadius, "the news wreck is a search circle on the list");
            g.Menus.Open(MenuSystem.Page.Journal);
            yield return null;
            var labels = g.Menus.ItemLabels();
            c.Check(labels.Contains("- LOOK FOR -") && labels.Any(l => l.StartsWith("[ ] A WRECK IN THE NEWS")), "the journal shows it under LOOK FOR");
            g.Menus.Close();
            g.NewsPins.Add(wreck);
            look.Clear(); g.LookFor(look);
            c.Check(!look.Any(p => p.label.StartsWith("A WRECK IN THE NEWS") && Vector3.Distance(new Vector3(p.pos.x, 0f, p.pos.z), new Vector3(wreck.x, 0f, wreck.z)) <= WastelandGame.NewsWreckRadius), "marked on the map, it is off the list");
            g.NewsPins.Remove(wreck);
        }
    }

    /// <summary>A stranded trader walks toward town; the player stops beside them and they climb in; stopped inside the
    /// town they get out with thanks: a hot meal, the town's standing, the news.</summary>
    class Hitcher : Scenario
    {
        public override string Id => "npc.hitcher";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var car = ScheduledScenarios1007.FleetCar(g);
            if (!car) { c.Block("no car in the fleet"); yield break; }
            var town = g.NearestTown(car.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            var tp = new Vector3(town.pos.x, 0f, town.pos.y);
            var cp = new Vector3(car.transform.position.x, 0f, car.transform.position.z);
            if (Vector3.Distance(cp, tp) < town.radius + 20f)
            {
                var dirOut = (cp - tp).sqrMagnitude > 1f ? (cp - tp).normalized : Vector3.forward;
                var outside = tp + dirOut * (town.radius + 120f);
                outside.y = DeformableTerrain.Instance.Height(outside.x, outside.z) + 1.2f;
                car.Body.position = outside; car.Body.linearVelocity = Vector3.zero;
                g.Player.Teleport(outside + Vector3.right * 6f, 0f);
                yield return new WaitForSeconds(1.5f);
                c.Fixture("moved the fleet car " + Mathf.RoundToInt(town.radius + 120f) + " m out of town");
            }
            var n = ScheduledScenarios1012.Person(g, "test:hitch", NpcRole.Wanderer, 41, car.transform.position + car.transform.right * 5f);
            yield return new WaitForSeconds(0.3f);
            g.Strand(n, town);
            c.Check(g.Hitchers.Contains(n) && n.hitchTown == town.index, "a stranded survivor heading for " + Market.TownName(town));
            var h0 = n.home;
            yield return new WaitForSeconds(2f);
            c.Check((n.home - h0).magnitude > 5f, "they walk on toward town");
            g.Enter(car);
            car.handbrake = true;
            var p = n.transform.position - car.transform.right * 4f;
            p.y = DeformableTerrain.Instance.Height(p.x, p.z) + 1f;
            car.Body.position = p; car.Body.linearVelocity = Vector3.zero;
            float t0 = Time.time;
            while (Time.time - t0 < 6f && !n.Riding) yield return null;
            c.Check(n.Riding && n.RideVehicle == car, "stopping beside them, they climb in: " + g.ToastText);
            yield return new WaitForSeconds(1.5f);
            c.Check(n.Riding, "stopped out of town, they stay in the seat");
            int meals = g.Inventory.GetItem(WastelandGame.HitchMeal);
            var into = tp + new Vector3(4f, 0f, 4f);
            into.y = DeformableTerrain.Instance.Height(into.x, into.z) + 1.2f;
            car.Body.position = into; car.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1f);
            t0 = Time.time;
            while (Time.time - t0 < 8f && WastelandGame.LastDelivered != n.Profile.id) yield return null;
            c.Check(WastelandGame.LastDelivered == n.Profile.id && !n.Riding, "stopped in town, they get out: " + g.ToastText);
            c.Check(g.Inventory.GetItem(WastelandGame.HitchMeal) == meals + 1, "a hot meal for the driver");
            c.Check(n.State.Has(NpcSave.Helped), "they remember the lift");
            g.Exit();
            yield return new WaitForSeconds(0.4f);
            if (n) Object.Destroy(n.gameObject);
        }
    }
}
