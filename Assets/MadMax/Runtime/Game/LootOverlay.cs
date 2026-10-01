using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>The floating loot panels (Project Zomboid style): two small panels over the running game — LOOT (every
    /// source within reach as a collapsible section: the floor, each storage reached at its <see cref="Container.AccessAt"/>
    /// incl. vehicle compartments, searchable spots and bodies) and YOU (the pack and each worn bag in
    /// <see cref="WastelandGame.WornStorage"/>). They come up by themselves when a storage, a searchable spot or an item
    /// on the ground is hovered (cursor in top-down views, the [E] focus otherwise) and stay while something is in reach;
    /// the LOOT key pins them (and frees the cursor in first / third person). Title bars drag the panels, [-] collapses,
    /// [x] closes; section headers fold. Drag a row onto another section (Ctrl: one, Shift: half), off the panels to drop
    /// it on the floor; double-click moves it across (loot → pack, yours → the first storage); RMB opens its menu; hovering
    /// shows a tooltip. Every move is <see cref="MenuSystem.LootMove"/> (capacity, item feed, replication as the loot
    /// window). The world never pauses.</summary>
    public sealed class LootOverlay
    {
        /// <summary>The mouse is on a panel or dragging from one: clicks, wheel and RMB belong to the panels this frame.</summary>
        public static bool ConsumesMouse { get; private set; }
        /// <summary>Pinned: the camera leaves the cursor free (first / third person).</summary>
        public static bool WantsCursor { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { ConsumesMouse = WantsCursor = false; }

        public sealed class Panel
        {
            public string name;
            public Vector2Int pos = new Vector2Int(-1, -1);
            public bool collapsed, closed;
            public int scroll;
            public RectInt rect, bar, closeBtn, foldBtn;
        }

        /// <summary>One line of a panel: a section header (<see cref="key"/> null) or goods of a source.</summary>
        public sealed class Row
        {
            public int panel;
            public LootSource src;
            public string key, label;
            public bool header, search;
            public RectInt rect;
        }

        const int PanelW = 140, RowH = 8, BarH = 10, MaxRows = 16;
        readonly WastelandGame g;
        public readonly Panel loot = new Panel { name = "LOOT" }, you = new Panel { name = "YOU" };
        public readonly List<LootSource> LootSide = new List<LootSource>(), PlayerSide = new List<LootSource>();
        public readonly List<Row> Rows = new List<Row>();
        readonly HashSet<string> folded = new HashSet<string>();

        public bool Pinned { get; private set; }
        public bool Visible { get; private set; }
        bool autoShown;
        Component shownBy, dismissedFor;
        float refreshAt;

        // mouse
        Vector2Int mp; bool overPanel;
        Panel dragPanel; Vector2Int dragOffset;
        Row pressRow; Vector2Int pressAt; bool draggingItem;
        Row hoverRow; float hoverSince;
        Row lastClick; float lastClickAt;

        public LootOverlay(WastelandGame game) { g = game; }

        /// <summary>What brought the panels up (the hovered storage / item), null when pinned or hidden.</summary>
        public Component ShownBy => shownBy;

        public void SetPinned(bool on)
        {
            Pinned = on;
            if (on) { loot.closed = you.closed = false; }
            else autoShown = false;
        }

        bool CanShow => g.Player && g.Player.gameObject.activeSelf && !g.Current && !g.AwaitingRespawn && !g.PlacingItem && !TitleSequence.Playing
                        && !(g.Vitals && g.Vitals.Dead) && (!g.Menus.IsOpen || g.Menus.Current == MenuSystem.Page.Context);

        public void Tick(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            ConsumesMouse = false; WantsCursor = false;
            bool can = CanShow;
            if (can && !g.Menus.IsOpen && Controls.Down(Controls.Act.Loot))
            {
                SetPinned(!Pinned);
                g.Toast(Pinned ? "LOOT PANELS PINNED (" + Controls.Name(Controls.Act.Loot) + " TO UNPIN)" : "LOOT PANELS UNPINNED");
            }
            if (!can) { Visible = false; CancelDrag(); return; }
            if (!Visible && (Pinned || autoShown) && Time.unscaledTime >= refreshAt) Refresh();                            // while shown, Draw refreshes (rows keep their rects)
            // auto show: a storage / spot / ground item is hovered; stays while something is in reach
            var trig = g.ContextHover;
            if (trig && trig != dismissedFor && !autoShown) { autoShown = true; shownBy = trig; loot.closed = you.closed = false; Refresh(); }
            if (!trig) dismissedFor = null;
            if (autoShown && LootSide.Count == 0) { autoShown = false; shownBy = null; }
            Visible = (Pinned || autoShown) && !(loot.closed && you.closed);
            if (!Visible) { CancelDrag(); return; }
            WantsCursor = Pinned;
            if (g.Menus.IsOpen) return;                                                           // a row menu is up: it has the input
            MouseInput(mouse, kb);
        }

        void CancelDrag() { dragPanel = null; pressRow = null; draggingItem = false; }

        /// <summary>Re-read the sources in reach and rebuild the rows.</summary>
        public void Refresh()
        {
            refreshAt = Time.unscaledTime + 0.25f;
            var at = g.Player.transform.position;
            LootSide.Clear();
            bool floor = false;
            foreach (var s in g.Menus.NearbySources(at, true)) { if (s.floor) floor = true; else LootSide.Add(s); }
            if (floor) LootSide.Insert(0, LootSource.Floor);
            PlayerSide.Clear();
            PlayerSide.Add(LootSource.Pack);
            foreach (var c in g.WornStorage) if (c) PlayerSide.Add(LootSource.Of(c));
            Rows.Clear();
            Build(1, LootSide);
            Build(0, PlayerSide);
        }

        static string SectionId(LootSource s) => s.pack ? "pack" : s.floor ? "floor" : s.box ? "box" + s.box.GetEntityId() : s.spot ? "spot" + s.spot.key : "?";

        void Build(int panel, List<LootSource> srcs)
        {
            foreach (var s in srcs)
            {
                Rows.Add(new Row { panel = panel, src = s, header = true, label = s.Title });
                if (folded.Contains(SectionId(s))) continue;
                if (s.spot && !Lootable.Searched.Contains(s.spot.key)) { Rows.Add(new Row { panel = panel, src = s, search = true, label = s.spot.locked ? "PRY IT OPEN" : "SEARCH IT" }); continue; }
                if (s.floor)
                {
                    foreach (var kv in g.FloorStacks(g.Player.transform.position, MenuSystem.FloorReach)) Rows.Add(new Row { panel = panel, src = s, key = kv.Key, label = KeyName(kv.Key) });
                    continue;
                }
                var inv = s.Inv(g);
                if (inv == null) continue;
                int n0 = Rows.Count;
                for (int t = 1; t < ResourceInfo.Count; t++) if (inv.Get((ResourceType)t) > 0) Rows.Add(new Row { panel = panel, src = s, key = "res:" + t, label = ResourceInfo.Name((ResourceType)t) });
                foreach (var kv in inv.Items) if (kv.Value > 0) Rows.Add(new Row { panel = panel, src = s, key = kv.Key, label = ItemCatalog.Name(kv.Key) });
                if (Rows.Count == n0) Rows.Add(new Row { panel = panel, src = s, label = "EMPTY" });
            }
        }

        public static string KeyName(string key) => key != null && key.StartsWith("res:") && int.TryParse(key.Substring(4), out int t) ? ResourceInfo.Name((ResourceType)t) : ItemCatalog.Name(key);

        public int Count(Row r) => r.key == null ? 0 : g.Menus.LootCount(r.src, r.key);

        public void ToggleFold(LootSource s) { var id = SectionId(s); if (!folded.Remove(id)) folded.Add(id); Refresh(); }

        // ------------------------------------------------------------------ moving goods (the drag and drop call)
        /// <summary>Drag a goods row onto <paramref name="target"/>: mode 0 = all, 1 = one, 2 = half. Returns how many moved.</summary>
        public int DragRow(Row r, LootSource target, int mode)
        {
            if (r == null || r.key == null || target == null || target.Same(r.src)) return 0;
            int have = Count(r);
            int n = mode == 1 ? 1 : mode == 2 ? Mathf.Max(1, have / 2) : have;
            int moved = g.Menus.LootMove(r.src, target, r.key, n);
            Refresh();
            return moved;
        }

        /// <summary>The first goods row of a section with <paramref name="key"/> (automation).</summary>
        public Row RowOf(LootSource s, string key) => Rows.Find(x => x.key == key && x.src.Same(s));

        LootSource QuickTarget(Row r)
        {
            if (r.panel == 1) return LootSource.Pack;
            foreach (var s in LootSide) if (!s.floor && !s.spot) return s;
            return LootSource.Floor;
        }

        /// <summary>A row's right-click options.</summary>
        public List<ContextOption> RowOptions(Row r)
        {
            var l = new List<ContextOption>();
            if (r == null || r.key == null) return l;
            int have = Count(r);
            void Move(string label, LootSource to, int n) { var t = to; l.Add(new ContextOption { label = label, run = () => { g.Menus.LootMove(r.src, t, r.key, n); Refresh(); } }); }
            if (r.panel == 1)
            {
                Move("TAKE ONE", LootSource.Pack, 1);
                if (have > 2) Move("TAKE HALF (" + have / 2 + ")", LootSource.Pack, have / 2);
                if (have > 1) Move("TAKE ALL (" + have + ")", LootSource.Pack, have);
                foreach (var b in PlayerSide) if (b.box) Move("TAKE ALL INTO " + b.Title, b, have);
                var src = r.src;
                l.Add(new ContextOption { label = "TAKE EVERYTHING HERE", run = () => TakeEverything(src) });
                if (!r.key.StartsWith("res:") && ItemCatalog.Category(r.key) == ItemCategory.Food)
                    l.Add(new ContextOption { label = (r.key.StartsWith("drink") ? "DRINK" : "EAT") + " ONE", run = () => { if (g.Menus.LootMove(r.src, LootSource.Pack, r.key, 1) > 0) g.UseItem(r.key); Refresh(); } });
            }
            else
            {
                var dest = QuickTarget(r);
                if (!dest.floor)
                {
                    Move("PUT ONE IN " + dest.Title, dest, 1);
                    if (have > 2) Move("PUT HALF IN " + dest.Title, dest, have / 2);
                    if (have > 1) Move("PUT ALL IN " + dest.Title, dest, have);
                }
                foreach (var b in PlayerSide) if (!b.Same(r.src)) Move("MOVE ALL TO " + b.Title, b, have);
                Move("DROP ONE", LootSource.Floor, 1);
                if (have > 1) Move("DROP ALL (" + have + ")", LootSource.Floor, have);
                if (r.src.pack) foreach (var o in g.ItemUseOptions(r.key)) if (!o.label.StartsWith("DROP")) l.Add(o);
            }
            return l;
        }

        public int TakeEverything(LootSource s)
        {
            int moved = 0;
            foreach (var r in Rows.ToArray()) if (r.key != null && r.src.Same(s)) moved += g.Menus.LootMove(s, LootSource.Pack, r.key, int.MaxValue);
            Refresh();
            return moved;
        }

        // ------------------------------------------------------------------ mouse
        void MouseInput(Mouse mouse, Keyboard kb)
        {
            var canvas = PixelHud.Canvas;
            if (mouse == null || canvas == null || Cursor.lockState == CursorLockMode.Locked) { overPanel = false; hoverRow = null; return; }
            var m = mouse.position.ReadValue();
            mp = new Vector2Int(Mathf.FloorToInt(m.x / Screen.width * canvas.w), Mathf.FloorToInt((1f - m.y / Screen.height) * canvas.h));
            overPanel = (!loot.closed && loot.rect.Contains(mp)) || (!you.closed && you.rect.Contains(mp));
            ConsumesMouse = overPanel || dragPanel != null || pressRow != null;
            var row = RowAt(mp);
            if (!SameRow(row, hoverRow)) hoverSince = Time.unscaledTime;
            hoverRow = row;
            bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed), shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            float wheel = mouse.scroll.ReadValue().y;
            if (overPanel && Mathf.Abs(wheel) > 0.01f)
            {
                var p = loot.rect.Contains(mp) ? loot : you;
                p.scroll = Mathf.Max(0, p.scroll - (int)Mathf.Sign(wheel) * 2);
            }
            if (overPanel && mouse.rightButton.wasPressedThisFrame && row != null && row.key != null)
            {
                g.Menus.OpenPopup(row.label, RowOptions(row), mp);
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame && overPanel)
            {
                foreach (var p in new[] { loot, you })
                {
                    if (p.closed) continue;
                    if (p.closeBtn.Contains(mp)) { Close(p); return; }
                    if (p.foldBtn.Contains(mp)) { p.collapsed = !p.collapsed; return; }
                    if (p.bar.Contains(mp)) { dragPanel = p; dragOffset = mp - p.pos; return; }
                }
                if (row != null) { pressRow = row; pressAt = mp; draggingItem = false; }
                return;
            }
            if (dragPanel != null)
            {
                if (mouse.leftButton.isPressed) dragPanel.pos = new Vector2Int(Mathf.Clamp(mp.x - dragOffset.x, 0, canvas.w - PanelW), Mathf.Clamp(mp.y - dragOffset.y, 0, canvas.h - BarH));
                else dragPanel = null;
                return;
            }
            if (pressRow == null) return;
            if (mouse.leftButton.isPressed) { if (!draggingItem && pressRow.key != null && (mp - pressAt).sqrMagnitude > 9) draggingItem = true; return; }
            // released
            var pr = pressRow; pressRow = null;
            if (draggingItem)
            {
                draggingItem = false;
                var target = row != null ? row.src : you.bar.Contains(mp) && !you.closed ? LootSource.Pack : overPanel ? null : LootSource.Floor;   // off the panels: on the floor
                if (target != null) DragRow(pr, target, ctrl ? 1 : shift ? 2 : 0);
                return;
            }
            if (!SameRow(row, pr)) return;
            if (pr.header) { ToggleFold(pr.src); return; }
            if (pr.search) { g.SearchInto(pr.src.spot); Refresh(); return; }
            bool twice = SameRow(lastClick, pr) && Time.unscaledTime - lastClickAt < 0.35f;
            lastClick = pr; lastClickAt = Time.unscaledTime;
            if (twice && pr.key != null) { lastClick = null; DragRow(pr, QuickTarget(pr), ctrl ? 1 : shift ? 2 : 0); }
        }

        static bool SameRow(Row a, Row b) => a != null && b != null && a.header == b.header && a.search == b.search && a.key == b.key && a.panel == b.panel && a.src.Same(b.src);

        void Close(Panel p)
        {
            p.closed = true;
            if (loot.closed && you.closed) { Pinned = false; autoShown = false; dismissedFor = shownBy; shownBy = null; }
        }

        Row RowAt(Vector2Int p)
        {
            foreach (var r in Rows) if (r.rect.Contains(p)) return r;
            return null;
        }

        // ------------------------------------------------------------------ drawing
        static readonly Color32 Text = MadMax.Voxel.Pal.Ink, Dim = MadMax.Voxel.Pal.MutedInk, Amber = MadMax.Voxel.Pal.Accent, Hi = MadMax.Voxel.Pal.Selection;

        public void Draw(PixelCanvas c)
        {
            if (Visible && Time.unscaledTime >= refreshAt) Refresh();
            foreach (var r in Rows) r.rect = new RectInt(-100, -100, 0, 0);
            if (!Visible) return;
            if (loot.pos.x < 0) loot.pos = new Vector2Int(c.w - PanelW - 6, 112);
            if (you.pos.x < 0) you.pos = new Vector2Int(Mathf.Max(0, c.w - 2 * PanelW - 12), 112);
            DrawPanel(c, loot, 1, "LOOT", LootSide.Count == 0 ? "NOTHING IN REACH" : null);
            DrawPanel(c, you, 0, "YOU  " + g.CarriedWeight.ToString("0.0") + "/" + g.Stats.CarryCapacity.ToString("0") + " KG", null);
            if (draggingItem && pressRow != null)
            {
                string d = pressRow.label + " X" + Count(pressRow);
                int dw = PixelCanvas.TextWidth(d) + 6;
                c.Rect(mp.x + 4, mp.y + 2, dw, 9, new Color32(60, 34, 14, 230));
                c.Text(mp.x + 7, mp.y + 4, d, Amber, 1, false);
            }
            else if (hoverRow != null && hoverRow.key != null && Time.unscaledTime - hoverSince > 0.35f) Tooltip(c, hoverRow);
            if (!Pinned && Cursor.lockState == CursorLockMode.Locked)
            {
                string h = Controls.Name(Controls.Act.Loot) + ": USE THE PANELS";
                c.Text(loot.pos.x, loot.pos.y - 8, h, Dim);
            }
        }

        void DrawPanel(PixelCanvas c, Panel p, int panel, string title, string empty)
        {
            if (p.closed) { p.rect = p.bar = p.closeBtn = p.foldBtn = new RectInt(-100, -100, 0, 0); return; }
            var mine = Rows.FindAll(r => r.panel == panel);
            int rows = p.collapsed ? 0 : Mathf.Min(MaxRows, Mathf.Max(1, mine.Count));
            p.scroll = Mathf.Clamp(p.scroll, 0, Mathf.Max(0, mine.Count - rows));
            int h = BarH + rows * RowH + (p.collapsed ? 0 : 3);
            int x = p.pos.x, y = Mathf.Clamp(p.pos.y, 0, Mathf.Max(0, c.h - h));
            p.rect = new RectInt(x, y, PanelW, h);
            p.bar = new RectInt(x, y, PanelW - 18, BarH);
            p.foldBtn = new RectInt(x + PanelW - 18, y, 9, BarH);
            p.closeBtn = new RectInt(x + PanelW - 9, y, 9, BarH);
            c.Panel(x, y, PanelW, h);
            c.Rect(x + 1, y + 1, PanelW - 2, BarH - 1, new Color32(40, 24, 12, 230));
            c.Text(x + 3, y + 3, title.Length > 30 ? title.Substring(0, 30) : title, Amber, 1, false);
            c.Text(p.foldBtn.x + 3, y + 3, p.collapsed ? "+" : "-", Text, 1, false);
            c.Text(p.closeBtn.x + 3, y + 3, "X", Text, 1, false);
            if (p.collapsed) return;
            if (mine.Count == 0 && empty != null) { c.Text(x + 4, y + BarH + 2, empty, Dim, 1, false); return; }
            for (int k = 0; k < rows && p.scroll + k < mine.Count; k++)
            {
                var r = mine[p.scroll + k];
                int ry = y + BarH + 1 + k * RowH;
                r.rect = new RectInt(x + 1, ry, PanelW - 2, RowH);
                bool hover = SameRow(r, hoverRow) || (draggingItem && r.src != null && hoverRow != null && hoverRow.src != null && hoverRow.src.Same(r.src) && r.header);
                if (r.header)
                {
                    c.Rect(x + 1, ry, PanelW - 2, RowH, hover ? Hi : new Color32(28, 18, 10, 220));
                    bool fold = folded.Contains(SectionId(r.src));
                    c.Text(x + 3, ry + 2, (fold ? "> " : "v ") + Short(r.label, 16), Amber, 1, false);
                    string cap = Capacity(r.src);
                    c.Text(x + PanelW - 3 - PixelCanvas.TextWidth(cap), ry + 2, cap, Dim, 1, false);
                    continue;
                }
                if (hover) c.Rect(x + 1, ry, PanelW - 2, RowH, Hi);
                c.Text(x + 7, ry + 2, Short(r.label, 22), r.key == null && !r.search ? Dim : hover ? Amber : Text, 1, false);
                if (r.key != null) { string n = Count(r).ToString(); c.Text(x + PanelW - 3 - PixelCanvas.TextWidth(n), ry + 2, n, Dim, 1, false); }
            }
            if (mine.Count > rows) c.Text(x + PanelW - 40, y + h - 1, (p.scroll + 1) + "-" + Mathf.Min(mine.Count, p.scroll + rows) + "/" + mine.Count, Dim, 1, false);
        }

        static string Short(string s, int n) => s == null ? "" : s.Length > n ? s.Substring(0, n - 1) + "." : s;

        string Capacity(LootSource s)
        {
            if (s.box) return s.box.Weight.ToString("0.0") + "/" + s.box.capacity.ToString("0");
            if (s.pack) return ItemCatalog.TotalWeight(g.Inventory).ToString("0.0") + " KG";
            if (s.spot) { var inv = s.spot.Leftovers(false); return inv != null ? ItemCatalog.TotalWeight(inv).ToString("0.0") + " KG" : "?"; }
            return "";
        }

        void Tooltip(PixelCanvas c, Row r)
        {
            int n = Count(r);
            bool res = r.key.StartsWith("res:");
            float each = res && int.TryParse(r.key.Substring(4), out int t) ? ItemCatalog.ResourceWeight((ResourceType)t) : ItemCatalog.Weight(r.key);
            var lines = new List<string> { r.label + " X" + n, (each * n).ToString("0.00") + " KG (" + each.ToString("0.00") + " EACH)" };
            lines.Add(res ? "RESOURCE" : ItemCatalog.Category(r.key).ToString().ToUpperInvariant() + (g.HasMake(r.key) && r.src.pack ? "  " + g.QualityName(r.key) : ""));
            lines.Add("IN " + r.src.Title + "   DRAG / 2X CLICK / RMB");
            int w = 0; foreach (var l in lines) w = Mathf.Max(w, PixelCanvas.TextWidth(l));
            w += 8;
            int h = lines.Count * 8 + 4;
            int x = Mathf.Clamp(mp.x + 8, 2, c.w - w - 2), y = Mathf.Clamp(mp.y + 8, 2, c.h - h - 2);
            c.Panel(x, y, w, h);
            for (int i = 0; i < lines.Count; i++) c.Text(x + 4, y + 3 + i * 8, lines[i], i == 0 ? Amber : Text, 1, false);
        }
    }
}
