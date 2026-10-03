using System.Collections;
using System.Collections.Generic;
using MadMax.Audio;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the scheduled update 2026-10-03: fish prices by season, the home frequency on the
    /// radio, the HOME ledger, news wrecks that scavengers pick over, and townsfolk on the player's pantry.</summary>
    public static class UpdateScenarios1003
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new FishPrices();
            yield return new HomeRadio();
            yield return new HomeLedger();
            yield return new WreckScavenged();
            yield return new PantryTalk();
        }
    }

    /// <summary>Fresh fish is cheap in the summer runs and dear in winter (from the bite tables); preserved fish swings
    /// less; other food does not take the fish factor; the season flash names the fish market.</summary>
    class FishPrices : Scenario
    {
        public override string Id => "market.fish_seasons";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            float summer = Market.FishSeason("food_fish_raw", 0), winter = Market.FishSeason("food_fish_raw", 2);
            float smokedW = Market.FishSeason("food_fish_smoked", 2);
            c.Metric("fish_summer", summer, "x"); c.Metric("fish_winter", winter, "x");
            c.Check(summer < 0.95f && winter > 1.1f, $"fresh fish: x{summer:0.00} in summer, x{winter:0.00} in winter");
            c.Check(smokedW > 1f && smokedW < winter, $"smoked fish swings less in winter (x{smokedW:0.00})");
            c.Check(Market.FishSeason("food_meat_raw", 2) == 1f && Market.FishSeason("food_fish_glow", 2) == 1f, "meat and glowing fish are not fish-market goods");
            c.Check(Market.FishNews(0) != null && Market.FishNews(2) != null, "the summer and winter flashes name the fish market");
            var sts = c.Game.World.settlements;
            if (!c.Check(sts.Count > 0, "the world has settlements")) yield break;
            var lk = SeasonsScenarios.SeasonLock.Take(); var town = Trade.Town;
            try
            {
                Trade.Town = sts[0];
                lk.Set(0); int s = Trade.BuyPrice("food_fish_raw", 0f);
                lk.Set(2); int w = Trade.BuyPrice("food_fish_raw", 0f);
                c.Fixture("season held at SUMMER, then WINTER; buy price of raw fish at " + Market.TownName(sts[0]));
                c.Check(w > s, $"raw fish costs {s} scrap in summer and {w} in winter");
            }
            finally { lk.Restore(); Trade.Town = town; }
            yield break;
        }
    }

    /// <summary>A station of the player's away from them calls on the home frequency when it loses power, gets it back
    /// and finishes its queue; the call is kept off the town boards; near stations stay silent.</summary>
    class HomeRadio : Scenario
    {
        public override string Id => "base.home_radio";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            var q = pad; q.y = t.Height(q.x, q.z);
            var bench = FurnitureLibrary.Spawn("workbench", g.Build.Structures, q, Quaternion.identity, g.propMaterial);
            var st = bench ? bench.GetComponent<CraftingStation>() : null;
            if (!c.Check(st, "a workbench with a station")) yield break;
            bench.owner = g.Stats.name;
            Recipe r = null;
            foreach (var x in RecipeLibrary.All) if (x.station == "workbench") { r = x; break; }
            st.Enqueue(r, 0.0001f);
            var far = pad + new Vector3(0f, 0f, -70f); far.y = t.Height(far.x, far.z) + 0.3f;
            g.Player.Teleport(far, 0f);
            yield return new WaitForSeconds(1.5f);
            c.Fixture("the player's bench with a near-frozen job, the player 70 m away");
            int news = TownNews.Entries.Count;
            st.watts = 200f;
            yield return SeasonsScenarios.Until(() => g.LastHomeCall != null, 3f);
            c.Note(g.LastHomeCall);
            c.Check(g.LastHomeCall != null && g.LastHomeCall.Contains("LOST POWER"), "losing power calls the home frequency");
            c.Check(RadioNetwork.FlashText == g.LastHomeCall, "it goes out as a flash on the radio");
            c.Check(TownNews.Entries.Count == news, "and stays off the town boards");
            if (g.LastHomeCall == null) yield break;
            st.watts = 0f;
            yield return SeasonsScenarios.Until(() => g.LastHomeCall.Contains("BACK"), 3f);
            c.Check(g.LastHomeCall.Contains("POWER IS BACK"), "power back is called");
            st.CancelLast();
            st.tray.AddItem("food_bread", 2);
            st.Enqueue(r, 0.0001f);
            yield return new WaitForSeconds(1.3f);
            st.CancelLast();
            yield return SeasonsScenarios.Until(() => g.LastHomeCall.Contains("DONE"), 3f);
            c.Check(g.LastHomeCall.Contains("DONE") && g.LastHomeCall.Contains("ON THE TRAY"), "a finished queue with goods on the tray is called");
            bool journal = false;
            foreach (var e in Journal.Entries) if (e.kind == "HOME") journal = true;
            c.Check(journal, "the calls are in the journal under HOME");
            string last = g.LastHomeCall;
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -4f), 0f);
            yield return new WaitForSeconds(1.2f);
            st.watts = 200f; st.Enqueue(r, 0.0001f);
            yield return new WaitForSeconds(2.2f);
            c.Check(g.LastHomeCall == last, "standing at the bench, a stall is not called");
            st.CancelLast(); st.watts = 0f;
            Object.Destroy(bench.gameObject);
        }
    }

    /// <summary>The journal's HOME section sums up a claim: workshop jobs, power, water, weathered pieces, the pantry.</summary>
    class HomeLedger : Scenario
    {
        public override string Id => "base.home_ledger";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -6f), 0f);
            yield return new WaitForSeconds(0.3f);
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            var flag = FurnitureLibrary.Spawn("claim_flag", g.Build.Structures, P(-3f, 0f), Quaternion.identity, g.propMaterial);
            var bench = FurnitureLibrary.Spawn("workbench", g.Build.Structures, P(2f, 1f), Quaternion.identity, g.propMaterial);
            var crate = FurnitureLibrary.Spawn("crate", g.Build.Structures, P(2f, -2f), Quaternion.identity, g.propMaterial);
            yield return null;
            if (!c.Check(flag && bench && crate, "a claim flag, a workbench and a crate")) yield break;
            flag.owner = bench.owner = crate.owner = g.Stats.name;
            var st = bench.GetComponent<CraftingStation>();
            Recipe r = null;
            foreach (var x in RecipeLibrary.All) if (x.station == "workbench") { r = x; break; }
            st.Enqueue(r, 0.0001f);
            crate.GetComponent<Container>().inventory.AddItem("food_can_fish", 6);
            crate.hits = Mathf.Max(1, crate.MaxHits - 1);
            var lines = new List<string>();
            g.HomeLedger(lines);
            foreach (var l in lines) c.Note(l);
            c.Check(lines.Exists(l => l.StartsWith("HOME") && l.Contains("1 WORKING")), "the new claim's line counts the working bench");
            c.Check(lines.Exists(l => l.Contains("WEATHERED")), "worn pieces are listed");
            c.Check(lines[lines.Count - 1].StartsWith("PANTRY") && !lines[lines.Count - 1].StartsWith("PANTRY 0 "), "the pantry counts the tinned fish");
            g.Menus.Open(MenuSystem.Page.Journal);
            yield return null; yield return null;
            c.Screenshot("journal_home");
            yield return null;
            g.Menus.Close();
            st.CancelLast();
            Object.Destroy(flag.gameObject); Object.Destroy(bench.gameObject); Object.Destroy(crate.gameObject);
        }
    }

    /// <summary>A news wreck the player reaches late has been picked over (parts, storage, fuel) and a scavenger is still
    /// at it; one reached the same day is untouched; the record saves.</summary>
    class WreckScavenged : Scenario
    {
        public override string Id => "towns.wreck_scavenged";
        public override float Timeout => 50f;

        static int Parts(VehicleDriver v)
        {
            int n = 0;
            foreach (var s in v.GetComponent<VehicleChassis>().Sockets) if (s.Current) n++;
            return n;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            var far = pad + new Vector3(0f, 0f, -200f); far.y = t.Height(far.x, far.z) + 0.3f;
            g.Player.Teleport(far, 0f);
            yield return new WaitForSeconds(0.5f);
            var oldAt = pad + new Vector3(-20f, 0f, 0f); var newAt = pad + new Vector3(20f, 0f, 0f);
            var old = g.SpawnRoadWreck("Coupe", oldAt, Quaternion.identity, 11);
            var fresh = g.SpawnRoadWreck("Coupe", newAt, Quaternion.identity, 11);
            if (!c.Check(old && fresh, "two road wrecks (same seed)")) yield break;
            int p0 = Parts(old), f0 = Parts(fresh);
            g.RoadWrecks.Clear();
            g.RoadWrecks.Add(new Vector4(oldAt.x, oldAt.y, oldAt.z, DayNight.TotalDays - 3f));
            g.NoteRoadWreck(newAt);
            c.Fixture($"two skirmish wrecks with {p0} parts: one left 3 days ago, one today; the player arrives");
            var saved = new SaveData();
            g.RoadWrecks.ForEach(w => saved.roadWrecks.Add(w));
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -5f), 0f);
            yield return SeasonsScenarios.Until(() => g.RoadWrecks.TrueForAll(WastelandGame.Found), 4f);
            c.Check(g.RoadWrecks.TrueForAll(WastelandGame.Found), "both wrecks are marked found on arrival");
            yield return null;
            int p1 = Parts(old), f1 = Parts(fresh);
            c.Metric("parts_left_late", p1, ""); c.Metric("parts_left_fresh", f1, "");
            c.Check(p1 < p0, $"the 3-day-old wreck lost parts ({p0} -> {p1})");
            c.Check(f1 == f0, "the fresh one is untouched");
            var dir = NpcDirector.Instance;
            string id = WastelandGame.ScavengerId(oldAt);
            c.Check(dir && dir.Scavengers.ContainsKey(id), "a scavenger is placed at the old wreck");
            MadMax.Npc.Npc scav = null;
            yield return SeasonsScenarios.Until(() =>
            {
                foreach (var n in Object.FindObjectsByType<MadMax.Npc.Npc>(FindObjectsSortMode.None))
                    if (n && n.Profile != null && n.Profile.id == id) { scav = n; return true; }
                return false;
            }, 15f);
            c.Check(scav, "and walks there");
            c.Check(saved.roadWrecks.Count == 2 && JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)).roadWrecks.Count == 2, "the wreck records survive a save round trip");
            c.Check(WastelandGame.ScavengedShare(1f) == 0f && WastelandGame.ScavengedShare(10f) > 0.8f, "nothing goes the first day and a week-old wreck is stripped");
            if (dir) dir.Scavengers.Remove(id);
            g.RoadWrecks.Clear();
        }
    }

    /// <summary>In autumn and winter townsfolk answer what they think of the player's stores, by temper and the numbers.</summary>
    class PantryTalk : Scenario
    {
        public override string Id => "talk.pantry";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            string full = Dialogue.PantryVerdict(Temper.Friendly, 40f, 0f, 30f);
            string thin = Dialogue.PantryVerdict(Temper.Friendly, 5f, 3f, 30f);
            string joke = Dialogue.PantryVerdict(Temper.Joker, 5f, 0f, 30f);
            c.Note(full); c.Note(thin); c.Note(joke);
            c.Check(full.Contains("HOLD") && full.Contains("40 DAYS"), "a full larder holds");
            c.Check(thin.Contains("BOOT LEATHER") && thin.Contains("TURN"), "a thin one with rotting food gets the warning and the advice");
            c.Check(joke != thin, "tempers say it their own way");
            yield break;
        }
    }
}
