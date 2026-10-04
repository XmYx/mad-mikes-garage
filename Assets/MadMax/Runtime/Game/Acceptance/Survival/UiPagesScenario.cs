using System.Collections;
using MadMax.Building;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q2 (navigation and comfort) through real key presses: I, P, O and M open the inventory,
    /// skills, health and map pages and the same key closes them; Tab flips map and journal; Esc opens the pause menu
    /// (the world stops) and backs out of everything (the world runs again); the arrow keys reach SETTINGS, every
    /// settings tab opens and Esc returns; [E] at a workbench opens crafting; H toggles the key sheet; on the CONTROLS
    /// page the inventory action is rebound to F7, after which F7 opens the inventory and I no longer does (the default
    /// is put back afterwards).</summary>
    class UiPages : Scenario
    {
        public override string Id => "ui.pages";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var m = g.Menus;
            var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 6f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var w = new Waited();
            yield return VirtualKeys.Probe(w);
            if (!w.ok) { c.Block("virtual key presses do not reach the game here (no keyboard focus)"); VirtualKeys.Remove(); yield break; }
            int presses = 0;
            IEnumerator Tap(Key k) { presses++; return VirtualKeys.Tap(k); }

            // ---- hotkey pages
            var pages = new (Key key, MenuSystem.Page page)[] { (UnityEngine.InputSystem.Key.I, MenuSystem.Page.Inventory), (UnityEngine.InputSystem.Key.P, MenuSystem.Page.Skills), (UnityEngine.InputSystem.Key.O, MenuSystem.Page.Health) };
            foreach (var (k, p) in pages)
            {
                yield return Tap(k);
                c.Check(m.Current == p, $"{k} opens {p} (now {m.Current})");
                yield return Tap(k);
                c.Check(!m.IsOpen, $"{k} again closes it");
            }
            yield return Tap(UnityEngine.InputSystem.Key.M);
            c.Check(m.Current == MenuSystem.Page.Map && Time.timeScale == 0f, "M opens the map (the world pauses)");
            c.Screenshot("map");
            yield return null;
            yield return Tap(UnityEngine.InputSystem.Key.Tab);
            c.Check(m.Current == MenuSystem.Page.Journal, "Tab turns to the journal");
            yield return Tap(UnityEngine.InputSystem.Key.Escape);
            c.Check(m.Current == MenuSystem.Page.Map, "Esc goes back to the map");
            yield return Tap(UnityEngine.InputSystem.Key.M);
            c.Check(!m.IsOpen && Time.timeScale == 1f, "M closes it and the world runs again");

            // ---- pause, settings tabs
            yield return Tap(UnityEngine.InputSystem.Key.Escape);
            c.Check(m.Current == MenuSystem.Page.Pause && Time.timeScale == 0f, "Esc opens the pause menu (paused)");
            yield return VirtualKeys.Select(m, "SETTINGS", w);
            if (w.ok) yield return Tap(UnityEngine.InputSystem.Key.Enter);
            c.Check(m.Current == MenuSystem.Page.Settings, "the arrows and Enter reach SETTINGS");
            foreach (var tab in new[] { "GAMEPLAY", "MOUSE & CAMERA", "GRAPHICS", "AUDIO", "INTERFACE" })
            {
                yield return VirtualKeys.Select(m, tab, w);
                if (w.ok) yield return Tap(UnityEngine.InputSystem.Key.Enter);
                var labels = m.Labels();
                bool opened = m.Current == MenuSystem.Page.Settings && labels.Count > 1 && !labels.Contains("GAMEPLAY");
                c.Check(opened, $"the {tab} tab opens ({labels.Count} entries)");
                if (tab == "GRAPHICS") { c.Screenshot("settings_graphics"); yield return null; }
                yield return Tap(UnityEngine.InputSystem.Key.Escape);
                c.Check(m.Labels().Contains("GAMEPLAY"), "Esc returns to the tab list");
            }

            // ---- rebind the inventory key on the CONTROLS page
            bool isolated = Profile.Isolated;
            if (!isolated) c.Note("not an isolated profile: the rebinding (saved settings) is skipped");
            else
            {
                yield return VirtualKeys.Select(m, "CONTROLS", w);
                if (w.ok) yield return Tap(UnityEngine.InputSystem.Key.Enter);
                c.Check(m.Current == MenuSystem.Page.Controls, "CONTROLS opens");
                yield return VirtualKeys.Select(m, "INVENTORY", w);
                if (w.ok) yield return Tap(UnityEngine.InputSystem.Key.Enter);
                yield return Tap(UnityEngine.InputSystem.Key.F7);
                c.Check(Controls.Of(Controls.Act.Inventory) == UnityEngine.InputSystem.Key.F7, $"pressing F7 rebinds INVENTORY (now {Controls.Name(Controls.Act.Inventory)})");
                c.Check(m.Current == MenuSystem.Page.Controls, "the page stays open after the rebind");
            }
            for (int i = 0; i < 4 && m.IsOpen; i++) yield return Tap(UnityEngine.InputSystem.Key.Escape);
            c.Check(!m.IsOpen && Time.timeScale == 1f, "Esc backs out of every page; the world runs");
            if (isolated)
            {
                yield return Tap(UnityEngine.InputSystem.Key.I);
                c.Check(!m.IsOpen, "the old key I no longer opens the inventory");
                yield return Tap(UnityEngine.InputSystem.Key.F7);
                c.Check(m.Current == MenuSystem.Page.Inventory, "F7 does");
                yield return Tap(UnityEngine.InputSystem.Key.F7);
                Controls.Set(Controls.Act.Inventory, Controls.Default(Controls.Act.Inventory));
                c.Fixture("the inventory key reset to its default afterwards");
            }

            // ---- crafting at a workbench, the key sheet
            var fwd = g.Player.transform.forward;
            var bench = SurvivalKit.Piece(g, "workbench", at[0] + fwd * 1.4f, -fwd);
            c.Fixture("a workbench in front of the player");
            yield return SurvivalKit.GameSeconds(0.3f);
            yield return SurvivalKit.Use(g, bench, false, w);
            if (!w.ok) c.Note("in focus instead: " + (g.Focused is Component fc && fc ? fc.name : "nothing") + "; prompt '" + g.Prompt + "'");
            c.Check(w.ok && m.Current == MenuSystem.Page.Crafting, "[E] at the workbench opens crafting");
            c.Check(m.Labels().Count > 0, $"with recipes listed ({m.Labels().Count})");
            yield return Tap(UnityEngine.InputSystem.Key.Escape);
            c.Check(!m.IsOpen, "Esc closes crafting");
            bool help = g.ShowHelp;
            yield return Tap(UnityEngine.InputSystem.Key.H);
            c.Check(g.ShowHelp != help, "H toggles the key sheet");
            c.Screenshot("help");
            yield return null;
            yield return Tap(UnityEngine.InputSystem.Key.H);
            c.Metric("key_presses", presses, "");
            VirtualKeys.Remove();
        }
    }
}
