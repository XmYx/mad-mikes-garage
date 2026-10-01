using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>One side of the loot window: the pack, a storage (<see cref="Container"/>: chest, crate, fridge, vehicle
    /// cargo, saddlebags, a death stash), a searched spot (<see cref="Lootable"/>: cupboards, bodies) or the floor around
    /// the player (<see cref="WorldItem"/>s).</summary>
    public sealed class LootSource
    {
        public static readonly LootSource Pack = new LootSource { pack = true };
        public static readonly LootSource Floor = new LootSource { floor = true };

        public bool pack, floor;
        public Container box;
        public Lootable spot;

        public static LootSource Of(Container c) => new LootSource { box = c };
        public static LootSource Of(Lootable l) => new LootSource { spot = l };

        public bool Valid => pack || floor || box || spot;
        public string Title => pack ? "YOUR PACK" : floor ? "FLOOR" : box ? box.title : spot ? spot.title : "-";
        /// <summary>The inventory behind it (null for the floor; a spot's leftovers are made on demand).</summary>
        public Inventory Inv(WastelandGame g) => pack ? g.Inventory : box ? box.inventory : spot ? spot.Leftovers(true) : null;
        public bool Same(LootSource o) => o != null && pack == o.pack && floor == o.floor && box == o.box && spot == o.spot;
    }

    /// <summary>The loot window (Project Zomboid style, page <see cref="Page.Container"/>): YOUR PACK on the left, the
    /// storage on the right with tabs for the opened one, other storage and searchable spots within reach and the FLOOR.
    /// Drag a row across (Ctrl: one), double-click / Enter moves 5 resources or 1 item, A/D moves the stack, Q/E switch
    /// storage, Tab jumps sides, T takes everything, RMB (pad X) opens the row's context menu. Every move goes through
    /// <see cref="LootMove"/>: the same inventory calls as before (containers replicate through <c>Placeable.Dirty</c>),
    /// the floor through <see cref="WastelandGame.DropFromPack"/> / <see cref="WastelandGame.TakeFromFloor"/>; a full
    /// storage takes what fits and nothing is made or lost.</summary>
    public partial class MenuSystem
    {
        public const float LootReach = 3f, FloorReach = 2.5f;

        readonly List<LootSource> lootSources = new List<LootSource>();
        readonly List<RectInt> lootTabRects = new List<RectInt>();
        readonly List<Container> lootPool = new List<Container>();
        LootSource lootOpened;
        int lootTab, lootHeader = -1;
        readonly int[] paneFirst = new int[2];
        RectInt[] paneRects = new RectInt[2];
        int dragRow = -1, clickRow = -1; bool dragging; Vector2Int dragFrom, lootMouse; float clickAt;

        public IReadOnlyList<LootSource> LootSources => lootSources;
        public LootSource LootCurrent => lootTab >= 0 && lootTab < lootSources.Count ? lootSources[lootTab] : null;
        /// <summary>Index of the storage side's header row (rows after it are the storage's).</summary>
        public int LootHeader => lootHeader;
        /// <summary>Loot window row: its item id / "res:N" and side (0 pack, 1 storage); null when not a goods row.</summary>
        public string LootKeyAt(int row, out int pane) { pane = row >= 0 && row < items.Count ? items[row].pane : -1; return row >= 0 && row < items.Count ? items[row].loot : null; }
        /// <summary>The first goods row of a side whose key is <paramref name="key"/> (-1 = none).</summary>
        public int LootRow(int pane, string key) => items.FindIndex(i => i.loot == key && i.pane == pane);

        public void OpenLoot(LootSource s)
        {
            if (s == null || !s.Valid) return;
            lootOpened = s; lootTab = 0; paneFirst[0] = paneFirst[1] = 0;
            dragRow = -1; dragging = false;
            if (s.box) container = s.box;
            TrySearch(s);
            Open(Page.Container);
        }

        public void OpenLoot(Lootable l) => OpenLoot(LootSource.Of(l));
        public void OpenFloor() => OpenLoot(LootSource.Floor);

        /// <summary>Show storage tab <paramref name="i"/> (an unsearched spot is searched as you look into it).</summary>
        public void SelectLootTab(int i)
        {
            if (lootSources.Count == 0) return;
            lootTab = (i % lootSources.Count + lootSources.Count) % lootSources.Count;
            paneFirst[1] = 0;
            TrySearch(LootCurrent);
            Rebuild();
            if (lootHeader >= 0 && cursor > lootHeader) cursor = Mathf.Min(items.Count - 1, lootHeader + 1);
        }

        void TrySearch(LootSource s)
        {
            if (s == null || !s.spot || Lootable.Searched.Contains(s.spot.key)) return;
            game.SearchInto(s.spot);
        }

        Vector3 LootCentre => game.Current ? game.Current.transform.position : game.Player.transform.position;

        bool LockedForMe(Container c)
        {
            var door = c.GetComponent<Door>();
            return door && door.locked && !game.OwnsPiece(c.GetComponent<Placeable>());
        }

        void RefreshLootSources()
        {
            var cur = LootCurrent;
            lootSources.Clear();
            if (lootOpened != null && lootOpened.Valid) lootSources.Add(lootOpened);
            var at = LootCentre;
            Container.Near(at, LootReach, lootPool);
            foreach (var c in lootPool)
                if (c && !LockedForMe(c) && (lootOpened == null || lootOpened.box != c)) lootSources.Add(LootSource.Of(c));
            foreach (var l in WastelandGame.LootSpots)
            {
                if (!l || (lootOpened != null && lootOpened.spot == l) || (l.transform.position - at).sqrMagnitude > LootReach * LootReach) continue;
                if (!Lootable.Searched.Contains(l.key) || l.HasLeftovers) lootSources.Add(LootSource.Of(l));
            }
            if (lootOpened == null || !lootOpened.floor) lootSources.Add(LootSource.Floor);
            lootTab = 0;
            if (cur != null) for (int i = 0; i < lootSources.Count; i++) if (lootSources[i].Same(cur)) lootTab = i;
        }

        static string TabName(LootSource s)
        {
            string t = s.spot && !Lootable.Searched.Contains(s.spot.key) ? "? " + s.Title : s.Title;
            return t.Length > 10 ? t.Substring(0, 10) : t;
        }

        // ------------------------------------------------------------------ rows
        void BuildLoot()
        {
            RefreshLootSources();
            if (lootOpened == null || !lootOpened.Valid || lootSources.Count == 0) { lootHeader = -1; Close(); return; }
            var src = LootCurrent;
            items.Add(new Item { label = "- YOUR PACK -", enabled = () => false, pane = 0 });
            LootRows(LootSource.Pack, src, 0);
            lootHeader = items.Count;
            items.Add(new Item { label = "- " + src.Title + " -", enabled = () => false, pane = 1 });
            int n0 = items.Count;
            LootRows(src, LootSource.Pack, 1);
            if (items.Count == n0) items.Add(new Item { label = src.spot && !Lootable.Searched.Contains(src.spot.key) ? "LOCKED" : "NOTHING HERE", enabled = () => false, pane = 1 });
        }

        void LootRows(LootSource from, LootSource to, int pane)
        {
            string verb = pane == 0 ? "PUT" : "TAKE";
            void Row(string key, string label, System.Func<string> value, bool res)
            {
                items.Add(new Item
                {
                    label = label, value = value, loot = key, pane = pane, id = pane == 0 && !res ? key : null,
                    confirm = () => { LootMove(from, pane == 0 ? LootCurrent : LootSource.Pack, key, res ? 5 : 1); Rebuild(); },
                    adjust = d => { LootMove(from, pane == 0 ? LootCurrent : LootSource.Pack, key, int.MaxValue); Rebuild(); },
                    hint = "ENTER " + verb + (res ? " 5" : " 1") + "   A/D " + verb + " ALL   DRAG ACROSS   RMB MORE"
                });
            }
            if (from.floor)
            {
                foreach (var kv in game.FloorStacks(LootCentre, FloorReach))
                {
                    var key = kv.Key;
                    bool res = key.StartsWith("res:");
                    string name = res && int.TryParse(key.Substring(4), out int rt) ? ResourceInfo.Name((ResourceType)rt) : ItemCatalog.Name(key);
                    Row(key, name, () => (res ? "" : "X") + game.FloorCount(key, LootCentre, FloorReach), res);
                }
                return;
            }
            var inv = from.Inv(game);
            if (inv == null) return;
            for (int t = 1; t < ResourceInfo.Count; t++)
            {
                var rt = (ResourceType)t;
                if (inv.Get(rt) <= 0) continue;
                Row("res:" + t, ResourceInfo.Name(rt), () => inv.Get(rt) + (ResourceInfo.IsFluid(rt) ? "L" : ""), true);
            }
            foreach (var kv in new List<KeyValuePair<string, int>>(inv.Items))
            {
                if (kv.Value <= 0) continue;
                var id = kv.Key;
                Row(id, ItemCatalog.Name(id), () => "X" + inv.GetItem(id), false);
            }
        }

        // ------------------------------------------------------------------ moving goods
        static bool IsRes(string key, out ResourceType t)
        {
            t = ResourceType.None;
            if (key == null || !key.StartsWith("res:") || !int.TryParse(key.Substring(4), out int i) || i <= 0 || i >= ResourceInfo.Count) return false;
            t = (ResourceType)i; return true;
        }

        static float UnitWeight(string key) => IsRes(key, out var t) ? ItemCatalog.ResourceWeight(t) : ItemCatalog.Weight(key);

        /// <summary>How many of <paramref name="key"/> a side holds.</summary>
        public int LootCount(LootSource s, string key)
        {
            if (s == null || key == null) return 0;
            if (s.floor) return game.FloorCount(key, LootCentre, FloorReach);
            var inv = s.Inv(game);
            return inv == null ? 0 : IsRes(key, out var t) ? inv.Get(t) : inv.GetItem(key);
        }

        /// <summary>Move up to <paramref name="n"/> of an item id or "res:N" between two sides of the loot window (the call
        /// behind drag and drop, the keys and the row menus). A storage takes only what fits. Returns how many moved.</summary>
        public int LootMove(LootSource from, LootSource to, string key, int n)
        {
            if (from == null || to == null || !from.Valid || !to.Valid || from.Same(to) || key == null) return 0;
            n = Mathf.Min(n, LootCount(from, key));
            if (n <= 0) return 0;
            if (to.box)
            {
                float w = UnitWeight(key), room = to.box.capacity - to.box.Weight;
                if (w > 0f) n = Mathf.Min(n, Mathf.FloorToInt(room / w));
                if (n <= 0) { game.Toast(to.box.title + " IS FULL"); return 0; }
                n = to.box.Fits(key, n);                                                           // bags: what they take, their slots
                if (n <= 0) { game.Toast(to.box.title + (to.box.accepts != null && !to.box.accepts(key) ? " DOESN'T TAKE THAT" : " HAS NO ROOM")); return 0; }
            }
            int moved;
            if (from.floor)
            {
                moved = game.TakeFromFloor(key, n, LootCentre, FloorReach);                       // into the pack first
                if (!to.pack && moved > 0) moved = MoveInv(LootSource.Pack, to, key, moved);
            }
            else if (to.floor)
            {
                moved = from.pack ? n : MoveInv(from, LootSource.Pack, key, n);                   // out through the hands
                moved = moved > 0 && game.DropFromPack(key, moved) ? moved : 0;
            }
            else moved = MoveInv(from, to, key, n);
            if (moved > 0) MadMax.Audio.Sfx.Play2D("click", 0.4f);
            return moved;
        }

        int MoveInv(LootSource from, LootSource to, string key, int n)
        {
            var a = from.Inv(game); var b = to.Inv(game);
            if (a == null || b == null || n <= 0) return 0;
            bool ok;
            using (Inventory.Source("TAKEN", "STORED"))
            {
                if (IsRes(key, out var t)) { ok = a.TrySpend(t, n); if (ok) b.Add(t, n); }
                else { ok = a.TakeItem(key, n); if (ok) b.AddItem(key, n); }
            }
            if (!ok) return 0;
            if (from.pack) game.PackReleased(key);
            if (to.pack && key.StartsWith("tool_")) game.UpdateHotbarNow();
            return n;
        }

        /// <summary>Drag and drop: the row <paramref name="row"/> dropped on <paramref name="target"/> (all of it, or one).</summary>
        public int LootDropRow(int row, LootSource target, bool one)
        {
            var key = LootKeyAt(row, out int pane);
            if (key == null || target == null) return 0;
            var from = pane == 0 ? LootSource.Pack : LootCurrent;
            int moved = LootMove(from, target, key, one ? 1 : int.MaxValue);
            Rebuild();
            return moved;
        }

        /// <summary>Everything in the shown storage into the pack (what fits nothing stops: the pack has no hard limit).</summary>
        public int LootTakeAll()
        {
            var src = LootCurrent;
            if (src == null) return 0;
            var keys = new List<string>();
            foreach (var it in items) if (it.pane == 1 && it.loot != null) keys.Add(it.loot);
            int moved = 0;
            foreach (var k in keys) moved += LootMove(src, LootSource.Pack, k, int.MaxValue);
            Rebuild();
            return moved;
        }

        // ------------------------------------------------------------------ input
        Vector2Int CanvasPoint(Mouse mouse, PixelCanvas canvas)
        {
            var m = mouse.position.ReadValue();
            return new Vector2Int(Mathf.FloorToInt(m.x / Screen.width * canvas.w), Mathf.FloorToInt((1f - m.y / Screen.height) * canvas.h));
        }

        int RowAt(Vector2Int p) { for (int i = 0; i < items.Count; i++) if (items[i].loot != null && items[i].rect.Contains(p)) return i; return -1; }

        /// <summary>Loot window input before the list's own keys; true when it used the frame.</summary>
        bool LootTick(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            if (kb != null && kb.qKey.wasPressedThisFrame || pad != null && pad.leftShoulder.wasPressedThisFrame) { SelectLootTab(lootTab - 1); return true; }
            if (kb != null && kb.eKey.wasPressedThisFrame || pad != null && pad.rightShoulder.wasPressedThisFrame) { SelectLootTab(lootTab + 1); return true; }
            if (kb != null && kb.tKey.wasPressedThisFrame || pad != null && pad.buttonNorth.wasPressedThisFrame) { LootTakeAll(); return true; }
            if (kb != null && kb.tabKey.wasPressedThisFrame && items.Count > 0)
            {
                int side = items[cursor].pane == 0 ? 1 : 0;
                int i = items.FindIndex(x => x.pane == side && x.loot != null);
                if (i >= 0) cursor = i;
                return true;
            }
            var canvas = PixelHud.Canvas;
            if (mouse == null || canvas == null) return false;
            var p = CanvasPoint(mouse, canvas);
            bool moved = p != lootMouse;
            lootMouse = p;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int pane = paneRects[1].Contains(p) ? 1 : 0;
                paneFirst[pane] = Mathf.Max(0, paneFirst[pane] - (int)Mathf.Sign(wheel) * 2);
                return true;
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                for (int i = 0; i < lootTabRects.Count; i++) if (lootTabRects[i].Contains(p)) { SelectLootTab(i); return true; }
                int r = RowAt(p);
                if (r >= 0) { cursor = r; dragRow = r; dragFrom = p; dragging = false; }
                return true;
            }
            if (dragRow >= 0 && mouse.leftButton.isPressed)
            {
                if (!dragging && (p - dragFrom).sqrMagnitude > 9) dragging = true;
                return true;
            }
            if (dragRow >= 0 && mouse.leftButton.wasReleasedThisFrame)
            {
                int row = dragRow; dragRow = -1;
                if (dragging)
                {
                    dragging = false;
                    LootSource target = null;
                    for (int i = 0; i < lootTabRects.Count; i++) if (lootTabRects[i].Contains(p)) target = lootSources[i];
                    if (target == null) target = paneRects[0].Contains(p) ? LootSource.Pack : paneRects[1].Contains(p) ? LootCurrent : LootSource.Floor;   // off both sides: on the floor
                    bool one = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
                    LootDropRow(row, target, one);
                    return true;
                }
                bool twice = clickRow == row && Time.unscaledTime - clickAt < 0.35f;
                clickRow = row; clickAt = Time.unscaledTime;
                if (twice && row < items.Count) { clickRow = -1; items[row].confirm?.Invoke(); }
                else if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) && row < items.Count) items[row].adjust?.Invoke(1);
                return true;
            }
            if (moved && dragRow < 0) { int r = RowAt(p); if (r >= 0) cursor = r; }
            return false;
        }

        /// <summary>Options for a loot window row (its context menu).</summary>
        public List<ContextOption> LootRowOptions(int row)
        {
            var list = new List<ContextOption>();
            var key = LootKeyAt(row, out int pane);
            var src = LootCurrent;
            if (key == null || src == null) return list;
            var from = pane == 0 ? LootSource.Pack : src;
            var to = pane == 0 ? src : LootSource.Pack;
            int have = LootCount(from, key);
            string verb = pane == 0 ? (src.floor ? "DROP" : "PUT") : "TAKE";
            string where = pane == 0 && !src.floor ? " IN " + src.Title : "";
            void Move(string label, int n) => list.Add(new ContextOption { label = label, run = () => { LootMove(from, to, key, n); Rebuild(); } });
            Move(verb + " ONE" + where, 1);
            if (have > 2) Move(verb + " HALF (" + (have / 2) + ")" + where, have / 2);
            if (have > 1) Move(verb + " ALL (" + have + ")" + where, have);
            if (pane == 1) list.Add(new ContextOption { label = "TAKE EVERYTHING", run = () => LootTakeAll() });
            if (pane == 0 && !src.floor)
            {
                list.Add(new ContextOption { label = "DROP ONE ON THE FLOOR", run = () => { LootMove(LootSource.Pack, LootSource.Floor, key, 1); Rebuild(); } });
                if (have > 1) list.Add(new ContextOption { label = "DROP ALL ON THE FLOOR", run = () => { LootMove(LootSource.Pack, LootSource.Floor, key, have); Rebuild(); } });
            }
            if (pane == 0) foreach (var o in game.ItemUseOptions(key)) list.Add(o);
            else if (!key.StartsWith("res:") && ItemCatalog.Category(key) == ItemCategory.Food)
                list.Add(new ContextOption { label = (key.StartsWith("drink") ? "DRINK" : "EAT") + " ONE", run = () => { if (LootMove(from, LootSource.Pack, key, 1) > 0) game.UseItem(key); Rebuild(); } });
            return list;
        }

        // ------------------------------------------------------------------ drawing
        void DrawLoot(PixelCanvas c)
        {
            c.Rect(0, 0, c.w, c.h, new Color32(10, 5, 3, 150));
            var src = LootCurrent;
            if (src == null) return;
            int gap = 10, pw = Mathf.Min(230, (c.w - 16 - gap) / 2), lx = (c.w - (2 * pw + gap)) / 2, rx = lx + pw + gap;
            int top = 14, ph = c.h - top - 26;
            paneRects[0] = new RectInt(lx, top, pw, ph); paneRects[1] = new RectInt(rx, top, pw, ph);
            c.Panel(lx, top, pw, ph);
            c.Panel(rx, top, pw, ph);
            float packKg = game.CarriedWeight, cap = game.Stats.CarryCapacity;
            c.Text(lx + 5, top + 15, "YOUR PACK", Amber);
            string pk = packKg.ToString("0.0") + "/" + cap.ToString("0") + " KG";
            c.Text(lx + pw - 5 - PixelCanvas.TextWidth(pk), top + 15, pk, packKg > cap ? Red : Dim);
            // storage tabs
            lootTabRects.Clear();
            int tx = rx + 4, ty = top + 3;
            for (int i = 0; i < lootSources.Count; i++)
            {
                string t = TabName(lootSources[i]);
                int tw = PixelCanvas.TextWidth(t) + 6;
                if (tx + tw > rx + pw - 3) { lootTabRects.Add(new RectInt(-100, -100, 0, 0)); continue; }
                var r = new RectInt(tx, ty, tw, 9);
                lootTabRects.Add(r);
                c.Rect(r.x, r.y, r.width, r.height, i == lootTab ? Hi : new Color32(20, 14, 10, 220));
                c.Text(r.x + 3, r.y + 2, t, i == lootTab ? Amber : Dim, 1, false);
                tx += tw + 2;
            }
            string st = src.Title;
            c.Text(rx + 5, top + 15, st, Amber);
            string sk = src.box ? src.box.Weight.ToString("0.0") + "/" + src.box.capacity.ToString("0") + " KG" + (src.box.fridge ? (src.box.Cooling ? " COLD" : " NO POWER") : "")
                      : src.floor ? "WITHIN " + FloorReach.ToString("0.#") + " M" : src.spot ? ItemCatalog.TotalWeight(src.Inv(game)).ToString("0.0") + " KG" : "";
            c.Text(rx + pw - 5 - PixelCanvas.TextWidth(sk), top + 15, sk, Dim);
            c.Rect(lx + 3, top + 23, pw - 6, 1, Dim); c.Rect(rx + 3, top + 23, pw - 6, 1, Dim);
            int rows = Mathf.Max(1, (ph - 30) / 9);
            DrawPane(c, 0, lx, top + 27, pw, rows);
            DrawPane(c, 1, rx, top + 27, pw, rows);
            if (dragging && dragRow >= 0 && dragRow < items.Count)
            {
                string d = items[dragRow].label + " " + (items[dragRow].value != null ? items[dragRow].value() : "");
                int dw = PixelCanvas.TextWidth(d) + 6;
                c.Rect(lootMouse.x + 4, lootMouse.y + 2, dw, 9, new Color32(60, 34, 14, 230));
                c.Text(lootMouse.x + 7, lootMouse.y + 4, d, Amber, 1, false);
            }
            string help = "DRAG ACROSS (CTRL ONE, OFF THE SIDES: FLOOR)  2X CLICK/ENTER MOVE  A/D ALL  Q/E STORAGE  TAB SIDE  T TAKE ALL  RMB MORE";
            if (PixelCanvas.TextWidth(help) > c.w - 8) help = "DRAG  2X CLICK  A/D ALL  Q/E STORAGE  T TAKE ALL  RMB MORE";
            c.Text((c.w - PixelCanvas.TextWidth(help)) / 2, c.h - 10, help, Dim);
        }

        void DrawPane(PixelCanvas c, int pane, int x, int y, int w, int rows)
        {
            var idx = new List<int>();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.pane != pane || (it.loot == null && it.label.StartsWith("- "))) { if (it.pane == pane) it.rect = new RectInt(-100, -100, 0, 0); continue; }
                idx.Add(i);
            }
            int at = idx.IndexOf(cursor);
            int first = paneFirst[pane];
            if (at >= 0) first = Mathf.Clamp(first, Mathf.Max(0, at - rows + 1), at);
            first = Mathf.Clamp(first, 0, Mathf.Max(0, idx.Count - rows));
            paneFirst[pane] = first;
            for (int k = 0; k < idx.Count; k++)
            {
                var it = items[idx[k]];
                if (k < first || k >= first + rows) { it.rect = new RectInt(-100, -100, 0, 0); continue; }
                int ly = y + (k - first) * 9;
                it.rect = new RectInt(x + 3, ly - 1, w - 6, 9);
                bool sel = idx[k] == cursor;
                if (sel) c.Rect(x + 3, ly - 1, w - 6, 9, Hi);
                var col = !Enabled(it) ? Dim : sel ? Amber : Text;
                c.Text(x + 6, ly + 1, it.label, col, 1, false);
                if (it.value != null) { string v = it.value(); c.Text(x + w - 6 - PixelCanvas.TextWidth(v), ly + 1, v, sel ? Amber : Dim, 1, false); }
            }
            if (idx.Count > rows) c.Text(x + w - 26, y - 12, (first + 1) + "-" + Mathf.Min(idx.Count, first + rows), Dim, 1, false);
        }
    }
}
