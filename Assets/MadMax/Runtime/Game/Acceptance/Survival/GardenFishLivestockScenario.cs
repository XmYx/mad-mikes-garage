using System.Collections;
using System.Linq;
using System.Reflection;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q4 (gardens, pests, greenhouses, fishing, livestock): a bed is planted and watered with [E];
    /// in a frost the open bed's crop suffers while one under a greenhouse keeps growing; crows peck an unguarded
    /// ripening bed but not one beside a scarecrow; a ripe bed is harvested into the pack; a rod cast into the nearest
    /// running river gets a bite, is struck and reeled in (fish or junk lands in the pack); two chicks released at home,
    /// a filled trough and a nest box give eggs after the days pass. Timers that run minutes in play (the crow clock,
    /// growth, days) are moved on and disclosed.</summary>
    class SurvivalGardenFishLivestock : Scenario
    {
        public override string Id => "survival.garden_fish_livestock";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var inv = g.Inventory; var P = g.Player;
            var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 12f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var pad = at[0]; var fwd = P.transform.forward; var side = P.transform.right;
            DayNight.SetHours(12f);
            WeatherPin.Set(false, 20f, 0f);
            c.Fixture("noon, dry, 20 C held");
            var crop = FoodLibrary.Crops.Where(k => !k.tree && !k.dark && k.yields != null && k.yields.Length > 0).OrderBy(k => k.growMinutes).First();
            c.Note("crop " + crop.seed + " (" + crop.growMinutes + " min)");

            // ---- plant and water by hand
            var open = SurvivalKit.Piece(g, "garden_plot", pad + fwd * 1.6f, -fwd).GetComponent<GardenPlot>();
            var green = SurvivalKit.Piece(g, "greenhouse", pad - fwd * 5f, fwd);
            var inside = SurvivalKit.Piece(g, "garden_plot", pad - fwd * 5f, fwd).GetComponent<GardenPlot>();
            c.Fixture("a garden plot in the open, a greenhouse with a plot inside it");
            inv.AddItem(crop.seed, 3); inv.Add(ResourceType.Water, 3);
            c.Fixture("granted 3 " + crop.seed + " and 3 L water");
            yield return SurvivalKit.GameSeconds(0.3f);
            var w = new Waited();
            yield return SurvivalKit.Use(g, open, false, w);
            c.Check(w.ok && open.crop == crop.seed && inv.GetItem(crop.seed) == 2, "[E] at the plot plants a seed from the pack");
            open.water = 0f;
            yield return SurvivalKit.Use(g, open, false, w);
            c.Check(open.water >= 0.99f && inv.Get(ResourceType.Water) == 2, "[E] again waters it from the pack");
            c.Check(inside.Sow(crop.seed, 1f), "the greenhouse bed is sown");
            inside.Water(1f);
            yield return SurvivalKit.GameSeconds(3.5f);                                       // the roof checks run every 3 s

            // ---- frost: open against greenhouse
            WeatherPin.Set(false, -3f, 0f);
            c.Fixture("a frost held at -3 C");
            open.water = inside.water = 1f; open.health = inside.health = 1f;
            float g0 = inside.growth;
            yield return SurvivalKit.GameSeconds(4f);
            c.Metric("frost_open_health", open.health, ""); c.Metric("frost_greenhouse_health", inside.health, "");
            c.Check(open.health < 0.995f, $"frost harms the open bed (health {open.health:0.000})");
            c.Check(inside.health >= 0.999f && inside.growth > g0, $"under glass the crop is unharmed and grows ({g0:0.0000} -> {inside.growth:0.0000})");
            WeatherPin.Set(false, 20f, 0f);

            // ---- crows against a scarecrow
            var guarded = SurvivalKit.Piece(g, "garden_plot", pad + side * 6f, -side).GetComponent<GardenPlot>();
            SurvivalKit.Piece(g, "scarecrow", pad + side * 7.4f, -side);
            guarded.Sow(crop.seed, 1f);
            var pestT = typeof(GardenPlot).GetField("pestT", BindingFlags.NonPublic | BindingFlags.Instance);
            if (pestT == null) c.Block("crow timer not found");
            else
            {
                c.Fixture("crow clock moved to the edge (150 s) on two ripening beds, up to 14 times");
                bool pecked = false, guardPecked = false;
                for (int i = 0; i < 14 && !pecked; i++)
                {
                    foreach (var b in new[] { open, guarded }) { b.growth = 0.7f; b.health = 1f; b.water = 1f; pestT.SetValue(b, 149.9f); }
                    yield return SurvivalKit.Frames(3);
                    pecked |= open.growth < 0.69f; guardPecked |= guarded.growth < 0.69f;
                }
                c.Check(pecked, "crows peck the unguarded ripening bed");
                c.Check(!guardPecked, "the scarecrow keeps them off its bed");
            }

            // ---- harvest
            open.growth = 1f; open.health = 1f;
            c.Fixture("the open bed grown ripe");
            var ledger0 = SurvivalKit.Ledger(inv);
            yield return SurvivalKit.Use(g, open, false, w);
            var gained = crop.yields.Sum(y => inv.GetItem(y.item) - (ledger0.TryGetValue(y.item, out var h) ? h : 0));
            c.Check(open.crop == null && gained > 0, $"[E] harvests the ripe bed into the pack (+{gained})");

            // ---- livestock: chicks, trough, nest box, days pass
            var home = SurvivalKit.Ground(pad + fwd * 3f + side * 3f);
            P.Teleport(home - fwd * 1.2f + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            yield return SurvivalKit.GameSeconds(0.3f);
            var trough = SurvivalKit.Piece(g, "trough", home + side * 2f, -side).GetComponent<Trough>();
            var nest = SurvivalKit.Piece(g, "nest_box", home - side * 2f, side).GetComponent<Container>();
            inv.AddItem("animal_chick", 2); inv.Add(ResourceType.Hay, 8); inv.Add(ResourceType.Water, 10);
            c.Fixture("granted 2 chicks, 8 hay, 10 L water; a trough and a nest box set down");
            int kept0 = AnimalDirector.Instance ? AnimalDirector.Instance.kept.Count : 0;
            g.UseItem("animal_chick"); g.UseItem("animal_chick");
            yield return null;
            c.Check(AnimalDirector.Instance && AnimalDirector.Instance.kept.Count == kept0 + 2, "using the chicks releases them at home");
            trough.Use(g, false);
            float feed0 = trough.feed;
            c.Check(feed0 > 0f && trough.water > 0f, $"the trough is filled ({feed0:0} feed, {trough.water:0} L)");
            int eggs0 = nest.inventory.GetItem("food_egg");
            DayNight.SetDay(DayNight.Day + 3);
            c.Fixture("three days pass");
            yield return SurvivalKit.Until(() => nest.inventory.GetItem("food_egg") > eggs0, 5f, w);
            c.Check(w.ok, $"grown hens lay in the nest box ({nest.inventory.GetItem("food_egg") - eggs0} eggs)");
            c.Check(trough.feed < feed0, $"they eat from the trough ({feed0:0.00} -> {trough.feed:0.00})");

            // ---- fishing at the nearest running river
            if (!MedMineScenarios.RiverSpot(g, pad, out var stand, out var face, out _)) { c.Block("no running river within reach of the start"); WeatherPin.Release(); yield break; }
            stand = SurvivalKit.Ground(stand);
            P.Teleport(stand + Vector3.up * 0.1f, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
            c.Fixture($"stood at the river ({stand.x:0},{stand.z:0}), facing the channel");
            inv.AddItem("bait_worms", 4);
            var rod = ToolLibrary.Create("tool_fishing_rod", g.propMaterial) as FishingRodTool;
            if (!c.Check(rod, "the fishing rod is a tool")) { WeatherPin.Release(); yield break; }
            P.Equip(rod);
            yield return SurvivalKit.GameSeconds(1f);
            var before = SurvivalKit.Ledger(inv);
            P.Attack(false);
            yield return SurvivalKit.Until(() => rod.phase == FishingRodTool.Phase.Waiting, 5f, w);
            if (!c.Check(w.ok, "the line is cast into open water: " + (g.ToastText ?? rod.Status))) { WeatherPin.Release(); yield break; }
            float wait0 = Time.time;
            yield return SurvivalKit.Until(() => rod.phase == FishingRodTool.Phase.Bite, 90f, w);
            c.Metric("time_to_bite", Time.time - wait0, "s");
            if (!c.Check(w.ok, "a bite within 90 s")) { WeatherPin.Release(); yield break; }
            rod.Click();
            c.Check(rod.phase == FishingRodTool.Phase.Hooked, "a click on the bite strikes and hooks it");
            float fight0 = Time.time;
            yield return SurvivalKit.Until(() => { rod.reel = rod.Tension < 0.55f; return rod.phase == FishingRodTool.Phase.Idle; }, 60f, w);
            rod.reel = false;
            c.Metric("fight_time", Time.time - fight0, "s");
            var after = SurvivalKit.Ledger(inv);
            bool landed = after.Any(kv => !before.TryGetValue(kv.Key, out var b) || b < kv.Value);
            c.Check(landed, "reeling with the tension under the limit lands the catch: " + (g.ToastText ?? ""));
            P.Equip(null);
            WeatherPin.Release();
        }
    }
}
