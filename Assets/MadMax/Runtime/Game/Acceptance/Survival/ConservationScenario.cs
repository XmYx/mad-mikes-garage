using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q4 (conservation): moving goods between the pack and a crate through the container page (real
    /// key presses: ENTER moves five, D moves all) never makes or loses anything, and a nearly full crate takes only what
    /// fits; a crate within 5 m of a workbench feeds its recipes and one at 6 m does not; a full queue says so; jobs
    /// queued before the crafter's skill rose are refunded exactly what they cost when cancelled.</summary>
    class SurvivalConservation : Scenario
    {
        public override string Id => "survival.conservation";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var inv = g.Inventory; var P = g.Player;
            var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 8f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var fwd = P.transform.forward; var side = P.transform.right;
            var crate = SurvivalKit.Piece(g, "crate", at[0] + fwd * 1.3f, -fwd);
            var box = crate ? crate.GetComponent<Container>() : null;
            if (!c.Check(box, "a crate stands in front of the player")) yield break;
            inv.Add(ResourceType.Scrap, 30); inv.AddItem(ItemIds.Paper, 4);
            c.Fixture("a crate in front of the player; granted 30 scrap and 4 paper");
            yield return SurvivalKit.GameSeconds(0.2f);

            // ---- container page through real key presses
            var w = new Waited();
            yield return VirtualKeys.Probe(w);
            if (!w.ok) c.Block("virtual key presses do not reach the game here (focus); container page not driven");
            else
            {
                var before = SurvivalKit.Ledger(inv, box.inventory);
                yield return SurvivalKit.Use(g, crate, false, w);
                c.Check(w.ok && g.Menus.Current == MenuSystem.Page.Container, "[E] at the crate opens it");
                if (g.Menus.Current == MenuSystem.Page.Container)
                {
                    int s0 = inv.Get(ResourceType.Scrap);
                    yield return VirtualKeys.Select(g.Menus, "SCRAP", w);
                    if (w.ok) yield return VirtualKeys.Tap(Key.Enter);
                    c.Check(w.ok && inv.Get(ResourceType.Scrap) == s0 - 5 && box.inventory.Get(ResourceType.Scrap) == 5, $"ENTER moves 5 scrap into the crate ({box.inventory.Get(ResourceType.Scrap)})");
                    yield return VirtualKeys.Select(g.Menus, ItemCatalog.Name(ItemIds.Paper), w);
                    if (w.ok) yield return VirtualKeys.Tap(Key.Enter);
                    c.Check(box.inventory.GetItem(ItemIds.Paper) == 1, "ENTER on an item moves one");
                    int header = g.Menus.Labels().FindIndex(l => l != null && l.StartsWith("- CRATE"));
                    yield return VirtualKeys.Select(g.Menus, "SCRAP", w, header + 1);
                    if (w.ok) yield return VirtualKeys.Tap(Key.D);
                    c.Check(box.inventory.Get(ResourceType.Scrap) == 0 && inv.Get(ResourceType.Scrap) == s0, "D on the crate's scrap takes it all back");
                    c.Check(SurvivalKit.Diff(before, SurvivalKit.Ledger(inv, box.inventory)) == null, "pack + crate balance: " + (SurvivalKit.Diff(before, SurvivalKit.Ledger(inv, box.inventory)) ?? "ok"));
                    // nearly full: only what fits goes in
                    box.inventory.Add(ResourceType.Stone, Mathf.FloorToInt(box.capacity - box.Weight - 2f));
                    c.Fixture($"the crate filled with stone to within 2 kg of its {box.capacity:0} kg");
                    before = SurvivalKit.Ledger(inv, box.inventory);
                    yield return VirtualKeys.Select(g.Menus, "SCRAP", w);
                    if (w.ok) yield return VirtualKeys.Tap(Key.D);
                    c.Check(box.Weight <= box.capacity + 0.001f && box.inventory.Get(ResourceType.Scrap) > 0 && box.inventory.Get(ResourceType.Scrap) <= 3, $"a full crate takes only what fits ({box.inventory.Get(ResourceType.Scrap)} scrap, {box.Weight:0.0}/{box.capacity:0} kg)");
                    c.Check(SurvivalKit.Diff(before, SurvivalKit.Ledger(inv, box.inventory)) == null, "and nothing is lost: " + (SurvivalKit.Diff(before, SurvivalKit.Ledger(inv, box.inventory)) ?? "ok"));
                    c.Screenshot("container");
                    yield return null;
                    yield return VirtualKeys.Tap(Key.Escape);
                    c.Check(!g.Menus.IsOpen && Time.timeScale == 1f, "Esc closes the page, the world runs");
                }
                VirtualKeys.Remove();
            }
            if (g.Menus.IsOpen) g.Menus.Close();
            Object.Destroy(crate.gameObject);
            yield return null;

            // ---- crafting: storage range, full queue, refund at the price paid
            var bench = SurvivalKit.Piece(g, "workbench", at[0] + fwd * 1.6f, -fwd);
            yield return null;
            var st = bench ? bench.GetComponentInChildren<CraftingStation>() : null;
            var r = RecipeLibrary.All.Where(x => x.station == "workbench" && x.fuel == ResourceType.None && x.items.Length == 0 && x.resources.Length > 0
                                                 && x.kind == OutputKind.Item && string.IsNullOrEmpty(RecipeLibrary.KnowledgeFor(x)) && x.resources.All(i => i.amount >= 2))
                                 .OrderBy(x => x.resources.Sum(i => i.amount)).FirstOrDefault();
            if (!st || r == null) { c.Block("no workbench or plain workbench recipe"); yield break; }
            c.Note("recipe " + r.id + ": " + string.Join(", ", r.resources.Select(i => i.amount + " " + i.type)));
            foreach (var (t, n) in r.resources) { int have = inv.Get(t); if (have > 0) inv.TrySpend(t, have); }
            var store = SurvivalKit.Piece(g, "crate", st.transform.position + side * 4f, -side).GetComponent<Container>();
            foreach (var (t, n) in r.resources) store.inventory.Add(t, RecipeLibrary.Amount(n));
            c.Fixture("the recipe's inputs only in a crate 4 m from the bench (none in the pack)");
            yield return null;
            c.Check(g.CraftBlockReason(r, st) == null, "a crate within 5 m feeds the bench: " + (g.CraftBlockReason(r, st) ?? "craftable"));
            store.transform.position = st.transform.position + side * 6f;
            c.Check((g.CraftBlockReason(r, st) ?? "").StartsWith("NEED"), "moved to 6 m it no longer does: " + g.CraftBlockReason(r, st));
            Object.Destroy(store.gameObject);

            foreach (var (t, n) in r.resources) inv.Add(t, RecipeLibrary.Amount(n) * (CraftingStation.MaxQueue + 1));
            c.Fixture($"granted inputs for {CraftingStation.MaxQueue + 1} jobs");
            var ledger0 = SurvivalKit.Ledger(inv);
            for (int i = 0; i < CraftingStation.MaxQueue; i++) g.Craft(r, st);
            foreach (var j in st.queue) j.speed = 0f;                                          // hold the queue still
            c.Check(st.queue.Count == CraftingStation.MaxQueue, $"{CraftingStation.MaxQueue} jobs queue up");
            c.Check((g.CraftBlockReason(r, st) ?? "").Contains("QUEUE FULL"), "the ninth says the queue is full: " + g.CraftBlockReason(r, st));
            float mult0 = RecipeLibrary.CostMult;
            g.Stats.skillXp[(int)Skill.Crafting] = CharacterStats.XpForLevel(10);
            c.Fixture("Crafting skill raised to 10 while the jobs wait");
            yield return null; yield return null;
            c.Check(RecipeLibrary.CostMult < mult0, $"recipes now cost less ({mult0:0.00} -> {RecipeLibrary.CostMult:0.00})");
            while (st.queue.Count > 0) g.CancelLastJob(st);
            var diff = SurvivalKit.Diff(ledger0, SurvivalKit.Ledger(inv));
            c.Check(diff == null, "cancelling every job refunds exactly what was paid: " + (diff ?? "balanced"));
            Object.Destroy(bench.gameObject);
        }
    }
}
