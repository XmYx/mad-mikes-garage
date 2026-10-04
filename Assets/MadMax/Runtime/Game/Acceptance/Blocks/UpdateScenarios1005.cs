using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of 2026-10-05: the crafting and build catalogues (categories, icon grid on screen,
    /// the hover panel) and player text that tells only what the character could know.</summary>
    public static class UpdateScenarios1005
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new CatalogueMenus();
            yield return new NoSpoilers();
            yield return new PlayerStaysShown();
        }
    }

    /// <summary>The workbench opens a categorised icon grid that fits the screen in every category, with a panel that
    /// says what the recipe makes and takes; the build menu shows its pieces the same way, and picking one starts
    /// placing it.</summary>
    class CatalogueMenus : Scenario
    {
        public override string Id => "ui.catalogue";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var at = g.Player.transform.position + g.Player.transform.forward * 2f;
            at.y = MadMax.World.DeformableTerrain.Instance.Height(at.x, at.z);
            var bench = FurnitureLibrary.Spawn("workbench", g.Build.Structures, at, Quaternion.LookRotation(-g.Player.transform.forward), g.propMaterial);
            var st = bench ? bench.GetComponent<CraftingStation>() : null;
            if (!c.Check(st, "a workbench to craft at")) yield break;
            g.Inventory.Add(ResourceType.Scrap, 30); g.Inventory.Add(ResourceType.Wood, 30);
            c.Fixture("a workbench in front of the player; 30 scrap and 30 wood");
            g.Menus.OpenCrafting(st);
            yield return new WaitForSecondsRealtime(1.5f);                                       // icons rasterise a few per frame
            c.Check(g.Menus.Current == MenuSystem.Page.Crafting, "the workbench opens crafting");
            int cells = g.Menus.VisibleCells(out int cols);
            c.Metric("craft_cells", cells, ""); c.Metric("craft_columns", cols, "");
            c.Check(cells > 0 && cols >= 4, $"recipes as an icon grid ({cells} cells, {cols} columns)");
            var tip = g.Menus.TipLines();
            c.Check(tip.Count >= 3 && (tip.Contains("TAKES:") || tip.Exists(t => t.StartsWith("YOU DON'T KNOW"))), "the panel says what it makes and takes: " + string.Join(" | ", tip));
            c.Screenshot("crafting");
            yield return null;
            int bad = 0;
            for (int i = 0; i < 12; i++)
            {
                g.Menus.PickCategory(i);
                yield return null;
                int n = g.Menus.Labels(true).Count, shown = g.Menus.VisibleCells(out _);
                if (n > 0 && shown == 0) bad++;
            }
            c.Check(bad == 0, $"every category shows its cells on screen ({bad} empty)");
            g.Menus.Close();

            g.Menus.OpenBuild();
            yield return new WaitForSecondsRealtime(1.5f);
            c.Check(g.Menus.Current == MenuSystem.Page.Build, "B's menu: the build catalogue");
            cells = g.Menus.VisibleCells(out cols);
            c.Metric("build_cells", cells, "");
            c.Check(cells > 0, $"pieces as an icon grid ({cells} cells)");
            tip = g.Menus.TipLines();
            c.Check(tip.Count >= 3, "the panel says what the piece is for and what it takes: " + string.Join(" | ", tip));
            c.Screenshot("build");
            yield return null;
            g.Menus.PickCategory(System.Array.IndexOf(BuildMode.Categories, BuildCategory.Furniture));
            yield return null;
            if (g.Inventory.GetItem(ItemIds.ClawHammer) <= 0) g.Inventory.AddItem(ItemIds.ClawHammer);
            c.Check(g.Menus.Pick("WORKBENCH"), "pick the workbench from the grid");
            yield return null;
            c.Check(!g.Menus.IsOpen && g.Build.Active && g.Build.Current.id == "workbench", "picking starts placing it");
            c.Check(g.Player.Tool && g.Player.Tool.id == ItemIds.ClawHammer, "the claw hammer comes out");
            g.Build.SetActive(false);
            // ---- sorting, search, filters (at the workbench)
            g.Menus.OpenCrafting(st);
            yield return null;
            var names = g.Menus.Labels(true);
            var sorted = new List<string>(names); sorted.Sort(string.CompareOrdinal);
            c.Check(names.Count > 1 && string.Join("|", names) == string.Join("|", sorted), "recipes A-Z: " + string.Join(", ", names.GetRange(0, Mathf.Min(5, names.Count))));
            g.Menus.SetSearch("HAMMER");
            yield return null;
            var found = g.Menus.Labels(true);
            c.Check(found.Count > 0 && found.TrueForAll(n => n.Contains("HAMMER")), "search HAMMER finds only hammers: " + string.Join(", ", found));
            g.Menus.SetSearch("");
            g.Menus.SetFilter(2);
            yield return null;
            int makeable = g.Menus.Labels(true).Count, enabled = g.Menus.Labels().Count;
            c.Check(makeable > 0 && makeable == enabled, $"CAN MAKE shows only what can be made now ({makeable})");
            g.Menus.SetFilter(0);
            g.Menus.Close();
            // ---- handcraft: anywhere, by hand, timed
            g.Inventory.Add(ResourceType.Wood, 20); g.Inventory.Add(ResourceType.Cloth, 20); g.Inventory.Add(ResourceType.Stone, 20);
            g.Menus.OpenCrafting(null);
            yield return null;
            var hand = new List<string>();
            for (int i = 0; i < 12; i++) { g.Menus.PickCategory(i); yield return null; foreach (var n in g.Menus.Labels(true)) if (!hand.Contains(n)) hand.Add(n); }
            int notHand = 0; foreach (var n in hand) { foreach (var r in RecipeLibrary.All) if (r.name == n.TrimEnd(' ', '?') && !r.hand) { notHand++; break; } }
            c.Metric("hand_recipes", hand.Count, "");
            c.Note("by hand: " + string.Join(", ", hand));
            c.Check(hand.Count >= 3 && notHand == 0 && !hand.Exists(n => n.Contains("HELMET") || n.Contains("VEST")), $"HANDCRAFT lists {hand.Count} hand-made recipes, nothing that needs a bench or a sewing table");
            Recipe pick = null;
            foreach (var r in RecipeLibrary.All) if (r.hand && r.kind == OutputKind.Item && g.CanCraft(r, null) && (pick == null || RecipeLibrary.Seconds(r) < RecipeLibrary.Seconds(pick))) pick = r;
            if (!c.Check(pick != null, "something to make by hand with wood, cloth and stone")) yield break;
            int before = g.Inventory.GetItem(pick.output);
            c.Check(g.Handcraft(pick) && g.HandQueue.Count == 1, "it goes into your hands: " + pick.name);
            float t0 = Time.time, need = WastelandGame.HandSeconds(pick) / g.CraftSpeed(pick);
            while (g.Inventory.GetItem(pick.output) <= before && Time.time - t0 < need + 5f) yield return null;
            c.Metric("handcraft_seconds", Time.time - t0, "s");
            c.Check(g.Inventory.GetItem(pick.output) > before && g.HandQueue.Count == 0, $"done by hand in {Time.time - t0:0.0} s (no bench)");
            g.Menus.Close();
            // the map page (HD MAP draws it at screen detail)
            g.Menus.Open(MenuSystem.Page.Map);
            yield return new WaitForSecondsRealtime(0.8f);
            var hud = PixelHud.Canvas;
            c.Note($"settings: vector {GameSettings.Current.vector}, HD HUD {GameSettings.Current.hdHud}, HD MAP {GameSettings.Current.hdMap}; canvas {hud?.w}x{hud?.h} at x{hud?.res}");
            c.Check(hud != null && (!GameSettings.Current.hdMap || Screen.height / hud.h < 2 || hud.res > 1), "HD MAP: the map page draws at screen detail");
            c.Screenshot("map");
            yield return null;
            g.Menus.Close();
        }
    }

    /// <summary>Hidden values read as the character would tell them: a fuel blend by smell, not by share; strangers by
    /// what they look like until you have met; odds and conditions in words.</summary>
    class NoSpoilers : Scenario
    {
        public override string Id => "ui.no_spoilers";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var blend = FluidsScenarios.Mix((ResourceType.Diesel, 85f), (ResourceType.Fuel, 15f));
            var tainted = FluidsScenarios.Mix((ResourceType.Fuel, 95f), (ResourceType.Water, 5f));
            c.Check(!blend.Label().Contains("%") && blend.Label().StartsWith(ResourceInfo.Name(ResourceType.Diesel)), "a blend reads by smell: " + blend.Label());
            c.Check(!tainted.Label().Contains("%") && tainted.Label().Contains("SMELLS OFF"), "a little water in petrol smells off: " + tainted.Label());
            c.Check(blend.Assay().Contains("%"), "the assay still knows the shares: " + blend.Assay());
            c.Check(Words.Odds(0.5f) == "EVEN ODDS" && Words.Condition(0.4f) == "BATTERED", "odds and wear in words");
            MadMax.Npc.Npc stranger = null;
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.Alive && !n.proxy && !n.State.Has(MadMax.Npc.NpcSave.Met) && !n.companion) { stranger = n; break; }
            if (stranger)
            {
                c.Check(!stranger.KnownName.Contains(stranger.Profile.Name) && !stranger.Prompt(c.Game).Contains(stranger.Profile.Name), "a stranger has no name yet: " + stranger.Prompt(c.Game));
                stranger.State.Set(MadMax.Npc.NpcSave.Met);
                c.Check(stranger.KnownName == stranger.Profile.Name, "once met, the name: " + stranger.KnownName);
            }
            else c.Note("no stranger near the start to check");
            yield break;
        }
    }

    /// <summary>Line of sight hides vehicles out of view by their renderers, listed once. A player sitting in or on a
    /// vehicle when its list was taken must not vanish with it: hiding the vehicle leaves the player shown.</summary>
    class PlayerStaysShown : Scenario
    {
        public override string Id => "ui.player_stays_shown";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var los = g.GetComponent<LineOfSight>();
            MadMax.Vehicles.VehicleDriver v = null;
            foreach (var x in g.AllVehicles) if (x && x != g.Current && x.driveable) { v = x; break; }
            if (!c.Check(los && v, "line of sight and a vehicle")) yield break;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform; var parent0 = me.parent;
            me.SetParent(v.transform, true);                                                    // riding in it (a passenger seat, the roof)
            var t = typeof(LineOfSight);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            t.GetMethod("Rescan", flags).Invoke(los, null);
            me.SetParent(parent0, true);
            // the vehicle drops out of view: hide its listed renderers
            var targets = (System.Collections.IList)t.GetField("targets", flags).GetValue(los);
            object target = null;
            foreach (var o in targets) if ((GameObject)o.GetType().GetField("root").GetValue(o) == v.gameObject) { target = o; break; }
            if (!c.Check(target != null, "the vehicle is a line-of-sight target")) yield break;
            t.GetMethod("Show", flags).Invoke(los, new object[] { target, false });
            int hidden = 0, total = 0;
            foreach (var r in g.Player.GetComponentsInChildren<Renderer>(true)) { total++; if (r.forceRenderingOff && !MadMax.Rendering.HDVisual.IsHost(r)) hidden++; }
            c.Check(total > 0 && hidden == 0, $"the vehicle hid, the player didn't ({hidden} of {total} player renderers hidden)");
            t.GetMethod("Show", flags).Invoke(los, new object[] { target, true });
            yield return null;
        }
    }
}
