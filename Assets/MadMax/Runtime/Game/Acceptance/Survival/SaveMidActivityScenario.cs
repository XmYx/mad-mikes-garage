using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q5 (save / restart mid-activity): in the middle of things — a job 40 % through at a workbench
    /// (paid with a stand-in price), a bandaged cut and a torn shirt, a half-grown bed, rain falling with soaked clothes,
    /// a fire burning, hungry and thirsty — the game is saved to a slot of the isolated test profile and loaded. The
    /// same state comes back (by piece id and position, with float tolerances) and the interrupted job then finishes
    /// once with its goods.</summary>
    class SurvivalSaveMidActivity : Scenario
    {
        public override string Id => "survival.save_midactivity";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 240f;

        public override IEnumerator Run(ScenarioContext c)
        {
            if (!Profile.Isolated) { c.Block("needs an isolated profile (-profiledir) so real saves are never touched"); yield break; }
            var g = c.Game;
            var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 9f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var pad = at[0]; var fwd = g.Player.transform.forward; var side = g.Player.transform.right;

            // ---- set the scene
            var bench = SurvivalKit.Piece(g, "workbench", pad + fwd * 1.6f, -fwd);
            var bed = SurvivalKit.Piece(g, "garden_plot", pad + side * 3f, -side).GetComponent<GardenPlot>();
            yield return null;
            var st = bench.GetComponentInChildren<CraftingStation>();
            var r = RecipeLibrary.All.Where(x => x.station == "workbench" && x.fuel == ResourceType.None && x.items.Length == 0 && x.resources.Length > 0
                                                 && x.kind == OutputKind.Item && string.IsNullOrEmpty(RecipeLibrary.KnowledgeFor(x))).OrderBy(x => x.resources.Sum(i => i.amount)).FirstOrDefault();
            if (r == null || !st) { c.Block("no plain workbench recipe"); yield break; }
            foreach (var (t, n) in r.resources) g.Inventory.Add(t, RecipeLibrary.Amount(n) * 2);
            g.Craft(r, st);
            if (!c.Check(st.queue.Count == 1, "a job is queued at the workbench: " + r.id)) yield break;
            st.queue[0].progress = 0.4f; st.queue[0].speed = 0f;
            c.Fixture("job held at 40 % for the save");
            var crop = FoodLibrary.Crops.First(k => !k.tree);
            bed.Sow(crop.seed, 1f); bed.growth = 0.3f; bed.water = 1f;
            var s = g.Stats;
            s.injuries.Clear();
            s.injuries.Add(new Injury { zone = BodyZone.LegR, type = Wound.Laceration, severity = 0.6f, bandaged = true, disinfected = true });
            s.hunger = 42f; s.thirst = 37f; s.wetness = 0.6f;
            string shirt = g.Player.Rig.outfit.FirstOrDefault(id => ClothingLibrary.Get(id) != null);
            if (shirt != null) g.ClothWear[shirt] = 0.5f;
            Weather.Auto = false; Weather.Raining = true; Weather.Restore(true, 0.7f, 0f, 12f);
            var fire = Fire.Ignite(SurvivalKit.Ground(pad - fwd * 5f) + Vector3.up * 0.1f, null, 200f, 0.8f);
            int marker = 9;
            g.Inventory.AddItem(ItemIds.Paper, marker);
            c.Fixture($"half-grown {crop.seed} (30 %), a bandaged cut, {shirt} at half wear, hunger 42, thirst 37, soaked 60 %, rain, a fire, {marker} paper");
            yield return null;
            var benchPos = bench.transform.position; var bedPos = bed.transform.position; var firePos = fire ? fire.transform.position : Vector3.zero;
            var pack = SurvivalKit.Ledger(g.Inventory);
            int paper = g.Inventory.GetItem(ItemIds.Paper);
            float mult = st.queue[0].costMult;

            // ---- save, load
            g.SaveGame(2);
            Weather.Auto = true;
            c.Check(SaveSystem.Exists(2), "saved to slot 2 of the test profile");
            var old = g;
            g.LoadGame(2);
            while ((WastelandGame.Instance == old || !WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - c.startedAt < 200f) yield return null;
            g = WastelandGame.Instance;
            if (!c.Check(g && g != old && g.Ready, "the save loads")) yield break;
            WastelandGame.ExternalInput = true;
            yield return SurvivalKit.GameSeconds(1f, 10);
            s = g.Stats;

            // ---- compare
            var bench2 = SurvivalKit.Near("workbench", benchPos, 0.2f);
            var st2 = bench2 ? bench2.GetComponentInChildren<CraftingStation>() : null;
            c.Check(st2 && st2.queue.Count == 1 && st2.queue[0].recipe == r.id && st2.queue[0].progress >= 0.395f && st2.queue[0].progress < 0.4f + 1.5f / Mathf.Max(0.5f, RecipeLibrary.Seconds(r)) * 3f, $"the workbench job is back at 40 % and works on from there ({(st2 && st2.queue.Count > 0 ? st2.queue[0].progress : -1f):0.00})");
            c.Check(st2 && st2.queue.Count == 1 && Mathf.Abs(st2.queue[0].costMult - mult) < 0.001f, "with the price it was paid at");
            var bed2 = SurvivalKit.Near("garden_plot", bedPos, 0.2f);
            var plot2 = bed2 ? bed2.GetComponent<GardenPlot>() : null;
            c.Check(plot2 && plot2.crop == crop.seed && Mathf.Abs(plot2.growth - 0.3f) < 0.03f, $"the bed is still sown and 30 % grown ({(plot2 ? plot2.growth : -1f):0.000})");
            var cut = s.injuries.FirstOrDefault(i => i.zone == BodyZone.LegR && i.type == Wound.Laceration);
            c.Check(cut != null && cut.bandaged && cut.disinfected && Mathf.Abs(cut.severity - 0.6f) < 0.05f, "the bandaged cut is still there");
            c.Check(Mathf.Abs(s.hunger - 42f) < 1.5f && Mathf.Abs(s.thirst - 37f) < 1.5f, $"hunger and thirst kept ({s.hunger:0.0}, {s.thirst:0.0})");
            c.Check(Mathf.Abs(s.wetness - 0.6f) < 0.08f, $"soaked clothes stay soaked ({s.wetness:0.00})");
            if (shirt != null) c.Check(g.ClothWear.TryGetValue(shirt, out var wear) && Mathf.Abs(wear - 0.5f) < 0.02f, $"the {shirt} keeps its wear");
            c.Check(Weather.Raining, "it is still raining");
            c.Check(Fire.All.Any(f => f && (f.transform.position - firePos).sqrMagnitude < 1f), "the fire is still burning");
            c.Check(g.Inventory.GetItem(ItemIds.Paper) == paper, $"the pack is kept ({g.Inventory.GetItem(ItemIds.Paper)} of {paper} paper)");
            var diff = SurvivalKit.Diff(pack, SurvivalKit.Ledger(g.Inventory));
            c.Check(diff == null, "every pack count matches: " + (diff ?? "same"));

            // ---- resume
            if (st2 && st2.queue.Count == 1)
            {
                st2.queue[0].speed = 1f; st2.queue[0].progress = 0.97f;
                c.Fixture("the resumed job advanced to 97 %");
                g.Player.Teleport(SurvivalKit.Ground(benchPos - fwd * 1.5f) + Vector3.up * 0.1f, 0f);
                int out0 = g.Inventory.GetItem(r.output) + st2.tray.GetItem(r.output);
                var w = new Waited();
                yield return SurvivalKit.Until(() => st2.queue.Count == 0, 20f, w);
                yield return SurvivalKit.GameSeconds(0.5f);
                int made = g.Inventory.GetItem(r.output) + st2.tray.GetItem(r.output) - out0;
                c.Check(w.ok && made == r.amount, $"the job finishes after the reload and delivers once ({made} of {r.amount})");
            }
        }
    }
}
