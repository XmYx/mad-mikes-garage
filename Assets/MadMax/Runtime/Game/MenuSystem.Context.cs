using System.Collections.Generic;
using MadMax.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Context menus: a small pixel popup of <see cref="ContextOption"/>s at the cursor, over the world (page
    /// <see cref="Page.Context"/> holds the input while it is up; the world keeps running) or over the pack page and the
    /// loot window (RMB on a row, the context key, pad X in the loot window). W/S or the mouse pick, Enter / click / 1-9
    /// choose, Esc / RMB / pad B close. Also the respawn chooser (<see cref="Page.Respawn"/>).</summary>
    public partial class MenuSystem
    {
        List<ContextOption> popOptions;
        string popTitle;
        Vector2Int popAnchor;
        int popCursor, popFrame;
        readonly List<RectInt> popRects = new List<RectInt>();
        RectInt popRect;
        Vector2 popMouse;
        bool popCloseNext;               // the world popup's page closes next frame: the click that chose stays in the menu

        public bool PopupOpen => popOptions != null;
        public string PopupTitle => popTitle;
        /// <summary>The open context menu's labels in order (automation).</summary>
        public List<string> PopupLabels() { var l = new List<string>(); if (popOptions != null) foreach (var o in popOptions) l.Add(o.label); return l; }

        /// <summary>Open a context menu at <paramref name="anchor"/> (canvas pixels). Over no page it holds the input on the
        /// blank <see cref="Page.Context"/> page.</summary>
        public void OpenPopup(string title, List<ContextOption> options, Vector2Int anchor)
        {
            if (options == null) return;
            if (!IsOpen) Open(Page.Context);
            popCloseNext = false;
            popTitle = title; popOptions = options; popAnchor = anchor; popCursor = 0; popFrame = Time.frameCount;
            if (Mouse.current != null) popMouse = Mouse.current.position.ReadValue();
            while (popCursor < options.Count - 1 && options[popCursor].blocked != null) popCursor++;
            MadMax.Audio.Sfx.Play2D("click", 0.4f);
        }

        public void ClosePopup()
        {
            if (popOptions == null) return;
            popOptions = null;
            if (Current == Page.Context) popCloseNext = true;
        }

        /// <summary>Choose the first option whose label starts with <paramref name="prefix"/> — exactly what a click on it
        /// does. False when there is none or it is greyed out.</summary>
        public bool ChoosePopup(string prefix)
        {
            if (popOptions == null) return false;
            int i = popOptions.FindIndex(o => o.label.StartsWith(prefix));
            return i >= 0 && Choose(i);
        }

        bool Choose(int i)
        {
            if (popOptions == null || i < 0 || i >= popOptions.Count) return false;
            var o = popOptions[i];
            if (o.blocked != null) { game.Toast(o.blocked); return false; }
            var back = Current;
            popOptions = null;
            game.RunContext(o);
            if (Current == Page.Context) popCloseNext = true;                                     // nothing else opened: back to the world next frame
            else if (Current == back && IsOpen) Rebuild();                                        // the page under it shows the change
            return true;
        }

        /// <summary>Input while a popup is up (all of it). True when a popup was open.</summary>
        bool PopupTick(Keyboard kb, Mouse mouse, Gamepad pad, bool esc)
        {
            if (popCloseNext) { popCloseNext = false; if (Current == Page.Context && popOptions == null) { Close(); return true; } }
            if (popOptions == null) { if (Current != Page.Context) return false; Close(); return true; }   // a blank host page holds nothing
            if (Current == Page.None) { popOptions = null; return false; }
            bool fresh = Time.frameCount == popFrame;
            if (esc || (pad != null && pad.buttonEast.wasPressedThisFrame) || (!fresh && mouse != null && mouse.rightButton.wasPressedThisFrame)
                || (!fresh && Controls.Down(Controls.Act.Context))) { ClosePopup(); return true; }
            int n = popOptions.Count;
            if (kb != null)
            {
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) popCursor = (popCursor + n - 1) % Mathf.Max(1, n);
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) popCursor = (popCursor + 1) % Mathf.Max(1, n);
                for (int i = 0; i < Mathf.Min(9, n); i++) if (kb[Key.Digit1 + i].wasPressedThisFrame) { Choose(i); return true; }
                if (!fresh && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) { Choose(popCursor); return true; }
            }
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) popCursor = (popCursor + n - 1) % Mathf.Max(1, n);
                if (pad.dpad.down.wasPressedThisFrame) popCursor = (popCursor + 1) % Mathf.Max(1, n);
                if (!fresh && pad.buttonSouth.wasPressedThisFrame) { Choose(popCursor); return true; }
            }
            var canvas = PixelHud.Canvas;
            if (mouse != null && canvas != null)
            {
                var m = mouse.position.ReadValue();
                var p = CanvasPoint(mouse, canvas);
                bool moved = (m - popMouse).sqrMagnitude > 1f;
                popMouse = m;
                for (int i = 0; i < popRects.Count && i < n; i++)
                    if (popRects[i].Contains(p))
                    {
                        if (moved) popCursor = i;
                        if (!fresh && mouse.leftButton.wasPressedThisFrame) { Choose(i); return true; }
                    }
                if (!fresh && mouse.leftButton.wasPressedThisFrame && !popRect.Contains(p)) { ClosePopup(); return true; }
            }
            return true;
        }

        /// <summary>RMB / the context key on a row of the pack page or the loot window: that row's menu.</summary>
        bool RowContextTick(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            if (Current != Page.Inventory && Current != Page.Container) return false;
            int row = -1; Vector2Int anchor = default;
            var canvas = PixelHud.Canvas;
            if (mouse != null && canvas != null && mouse.rightButton.wasPressedThisFrame)
            {
                var p = CanvasPoint(mouse, canvas);
                for (int i = 0; i < items.Count; i++) if (items[i].rect.Contains(p)) { row = i; anchor = p; }
                if (row < 0) return false;
                cursor = row;
            }
            else if (Controls.Down(Controls.Act.Context) || (Current == Page.Container && pad != null && pad.buttonWest.wasPressedThisFrame))
            {
                row = cursor;
                if (row >= 0 && row < items.Count) anchor = new Vector2Int(items[row].rect.x + items[row].rect.width / 2, items[row].rect.y + 8);
            }
            if (row < 0 || row >= items.Count) return false;
            return OpenRowMenu(row, anchor);
        }

        /// <summary>The context menu of a pack page / loot window row (automation: same as RMB on it).</summary>
        public bool OpenRowMenu(int row, Vector2Int anchor)
        {
            if (row < 0 || row >= items.Count) return false;
            List<ContextOption> opts = null; string key = null;
            if (Current == Page.Container) { key = items[row].loot; if (key != null) opts = LootRowOptions(row); }
            else if (Current == Page.Inventory) { key = items[row].drop; if (key != null) opts = game.ItemUseOptions(key); }
            if (key == null || opts == null || opts.Count == 0) return false;
            string title = key.StartsWith("res:") && int.TryParse(key.Substring(4), out int rt) ? MadMax.Items.ResourceInfo.Name((MadMax.Items.ResourceType)rt) : MadMax.Items.ItemCatalog.Name(key);
            OpenPopup(title, opts, anchor);
            return true;
        }

        void DrawPopup(PixelCanvas c)
        {
            popRects.Clear();
            if (popOptions == null) return;
            int w = PixelCanvas.TextWidth(popTitle ?? "") + 8;
            foreach (var o in popOptions) w = Mathf.Max(w, PixelCanvas.TextWidth(o.label) + 22 + (o.key != null ? PixelCanvas.TextWidth(o.key) + 8 : 0));
            int rows = Mathf.Max(1, popOptions.Count);
            int h = 14 + rows * 9 + 4;
            var sel = popCursor >= 0 && popCursor < popOptions.Count ? popOptions[popCursor] : null;
            string why = sel != null ? sel.blocked : null;
            if (why != null) { h += 9; w = Mathf.Max(w, PixelCanvas.TextWidth(why) + 8); }
            int x = Mathf.Clamp(popAnchor.x + 3, 2, Mathf.Max(2, c.w - w - 2)), y = Mathf.Clamp(popAnchor.y + 3, 2, Mathf.Max(2, c.h - h - 2));
            popRect = new RectInt(x, y, w, h);
            c.Panel(x, y, w, h);
            c.Text(x + 4, y + 4, popTitle ?? "", Amber, 1, false);
            c.Rect(x + 3, y + 11, w - 6, 1, Dim);
            if (popOptions.Count == 0) c.Text(x + 4, y + 15, "NOTHING TO DO", Dim, 1, false);
            for (int i = 0; i < popOptions.Count; i++)
            {
                var o = popOptions[i];
                int ry = y + 14 + i * 9;
                var r = new RectInt(x + 2, ry - 1, w - 4, 9);
                popRects.Add(r);
                bool on = i == popCursor;
                if (on) c.Rect(r.x, r.y, r.width, r.height, Hi);
                var col = o.blocked != null ? Dim : on ? Amber : Text;
                if (i < 9) c.Text(x + 4, ry + 1, (i + 1).ToString(), Dim, 1, false);
                c.Text(x + 12, ry + 1, o.label, col, 1, false);
                if (o.key != null) { string k = "[" + o.key + "]"; c.Text(x + w - 4 - PixelCanvas.TextWidth(k), ry + 1, k, Dim, 1, false); }
            }
            if (why != null) c.Text(x + 4, y + h - 9, why, Red, 1, false);
        }

        // ------------------------------------------------------------------ respawn chooser
        void BuildRespawn()
        {
            var pts = game.RespawnPoints();
            var me = game.Player ? game.Player.transform.position : Vector3.zero;
            foreach (var pt in pts)
            {
                var p = pt;
                float km = Vector3.Distance(new Vector3(p.at.x, 0f, p.at.z), new Vector3(me.x, 0f, me.z)) / 1000f;
                items.Add(new Item
                {
                    label = p.label, value = () => km < 1f ? Mathf.RoundToInt(km * 1000f) + " M" : km.ToString("0.0") + " KM",
                    confirm = () => { Close(); game.RespawnAt(p); },
                    hint = "ENTER WAKE UP HERE: HALF HEALTH, WOUNDS BANDAGED" + (p.kind == "bed" ? "" : "   (A BED OF YOUR OWN IS A BETTER PLACE)")
                });
            }
        }
    }
}
