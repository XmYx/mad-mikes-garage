using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>The catalogue pages: crafting at a station, HANDCRAFT (no station: hand-made recipes anywhere, timed
    /// personal jobs) and the build menu share one layout — categories down the left; over the grid a search box and
    /// filter chips (crafting: ALL / KNOWN / CAN MAKE, build: ALL / CAN BUILD); the recipes / pieces A–Z as 3D icons
    /// with their names; recipes not learned yet greyed out; and a floating panel with what it is, what it does and
    /// what it takes while a cell is hovered (or picked with the keys). Search covers every category of the page.
    /// Mouse: hover, click a category / chip / cell / the search box, wheel scrolls. Keys: arrows / WASD move, the
    /// build category keys (also Q / Shift+Q, PgUp / PgDn) switch categories, / searches, F cycles the filter, Enter
    /// makes / picks.</summary>
    public partial class MenuSystem
    {
        const int CellW = 48, CellH = 50, IconSize = 26, CatWidth = 86, CatRow = 12, HeaderH = 14;
        /// <summary>New icons rasterised per frame (the rest show a placeholder until their turn).</summary>
        const int IconBudget = 6;
        int gridCols = 1, gridRows = 1, gridScroll, iconsThisFrame, iconFrame;
        readonly List<RectInt> catRects = new List<RectInt>(), chipRects = new List<RectInt>();
        RectInt searchRect;
        Vector2Int catalogueMouse = new Vector2Int(-1, -1);
        /// <summary>The selection moved by keys / pad (the panel follows it) rather than by the mouse.</summary>
        bool tipByKeys;
        string search = "";
        bool searchFocus;
        int craftFilter, buildFilter;
        static readonly string[] CraftFilters = { "ALL", "KNOWN", "CAN MAKE" }, BuildFilters = { "ALL", "CAN BUILD" };

        bool IsCatalogue => Current == Page.Crafting || Current == Page.Build;
        int Filter { get => Current == Page.Build ? buildFilter : craftFilter; set { if (Current == Page.Build) buildFilter = value; else craftFilter = value; } }
        string[] Filters => Current == Page.Build ? BuildFilters : CraftFilters;

        bool Matches(string name) => search.Length == 0 || name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ------------------------------------------------------------------ pages
        void BuildCraftPage()
        {
            var cats = StationCategories();
            category = Mathf.Clamp(category, 0, Mathf.Max(0, cats.Count - 1));
            var cat = cats.Count > 0 ? cats[category] : RecipeCategory.Tools;
            var list = new List<Recipe>();
            foreach (var r in RecipeLibrary.All)
            {
                if (!Offered(r) || (search.Length == 0 && r.category != cat) || !Matches(r.name)) continue;
                bool known = game.Stats.Knows(RecipeLibrary.KnowledgeFor(r));
                if (craftFilter == 1 && !known) continue;
                if (craftFilter == 2 && !(known && game.CanCraft(r, station))) continue;
                list.Add(r);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var r in list)
            {
                var rec = r;
                bool known = game.Stats.Knows(RecipeLibrary.KnowledgeFor(r));
                var it = new Item
                {
                    label = known ? r.name : r.name + " ?", recipe = r, known = known, iconKey = RecipeIconKey(r),
                    confirm = () => { if (station) game.Craft(rec, station); else game.Handcraft(rec); },
                    enabled = () => known && game.CanCraft(rec, station) && (station || game.HandQueue.Count < WastelandGame.HandQueueMax)
                };
                var gm = game; var cell = it;
                it.iconMesh = () => RecipeMesh(gm, rec, out cell.diagonal);
                if (r.kind == OutputKind.Item) WorldItemModels.IconMesh(r.output, out it.diagonal);
                items.Add(it);
            }
        }

        int buildCategory;

        /// <summary>The build menu (tap B): pick a piece, then place it with the claw hammer.</summary>
        public void OpenBuild()
        {
            if (game.Build) buildCategory = Mathf.Max(0, System.Array.IndexOf(BuildMode.Categories, game.Build.Category));
            gridScroll = 0; search = ""; searchFocus = false;
            Open(Page.Build);
            if (game.Build && game.Build.Active) { var cur = game.Build.Current; for (int i = 0; i < items.Count; i++) if (items[i].piece == cur) { cursor = i; CatalogueMove(0, 0); } }
        }

        void BuildBuildPage()
        {
            var b = game.Build;
            buildCategory = Mathf.Clamp(buildCategory, 0, BuildMode.Categories.Length - 1);
            var list = new List<FurnitureDef>();
            if (search.Length == 0) list.AddRange(FurnitureLibrary.InCategory(BuildMode.Categories[buildCategory]));
            else foreach (var c in BuildMode.Categories) foreach (var d in FurnitureLibrary.InCategory(c)) if (Matches(d.name)) list.Add(d);
            if (buildFilter == 1) list.RemoveAll(d => !(b && b.Affordable(d)));
            list.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            foreach (var d in list)
            {
                var def = d;
                items.Add(new Item
                {
                    label = d.name, piece = d, iconKey = "piece:" + d.id, iconMesh = () => PieceMesh(def),
                    enabled = () => b && b.Affordable(def),
                    confirm = () => PickPiece(def)
                });
            }
        }

        void PickPiece(FurnitureDef d)
        {
            var b = game.Build;
            if (!b) return;
            b.Select(d.id);
            b.SetActive(true);
            if ((!game.Player.Tool || game.Player.Tool.id != ItemIds.ClawHammer) && game.Inventory.GetItem(ItemIds.ClawHammer) > 0) game.UseItem(ItemIds.ClawHammer);   // the hammer comes out
            Close();
        }

        static Mesh PieceMesh(FurnitureDef d)
        {
            if (d.mesh) return d.mesh;
            return WorldItemModels.IconMesh(d.kit ?? "bp_plan", out _);                       // plans and link tools: a blueprint
        }

        static Mesh RecipeMesh(WastelandGame g, Recipe r, out bool diagonal)
        {
            diagonal = false;
            switch (r.kind)
            {
                case OutputKind.Item: return WorldItemModels.IconMesh(r.output, out diagonal);
                case OutputKind.Resource: return WorldItemModels.IconMesh("res:" + (int)r.outputResource, out diagonal);
                case OutputKind.Part: case OutputKind.Vehicle:
                {
                    var prefab = r.kind == OutputKind.Part ? g.PartPrefab(r.output) : g.VehiclePrefab(r.output);
                    Mesh best = null; int verts = 0;
                    if (prefab) foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh && mf.sharedMesh.isReadable && mf.sharedMesh.vertexCount > verts && !mf.name.StartsWith("Glass")) { best = mf.sharedMesh; verts = best.vertexCount; }
                    return best ? best : WorldItemModels.IconMesh("kit_crate", out diagonal);
                }
            }
            return null;
        }

        static string RecipeIconKey(Recipe r) => r.kind == OutputKind.Part ? "part:" + r.output : r.kind == OutputKind.Vehicle ? "vehicle:" + r.output
            : r.kind == OutputKind.Resource ? "res:" + (int)r.outputResource : r.output;

        // ------------------------------------------------------------------ input
        /// <summary>Catalogue keys and mouse (before the generic input). True when the frame's input was used.</summary>
        bool CatalogueTick(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            int cats = Current == Page.Build ? BuildMode.Categories.Length : Mathf.Max(1, StationCategories().Count);
            int dc = 0;
            bool shift = kb != null && kb.shiftKey.isPressed;
            if (Controls.Down(Controls.Act.BuildNextCategory) || (kb != null && ((kb.qKey.wasPressedThisFrame && shift) || kb.pageDownKey.wasPressedThisFrame)) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) dc = 1;
            if (Controls.Down(Controls.Act.BuildPrevCategory) || (kb != null && ((kb.qKey.wasPressedThisFrame && !shift) || kb.pageUpKey.wasPressedThisFrame)) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) dc = -1;
            if (kb != null && kb.slashKey.wasPressedThisFrame) { searchFocus = true; return true; }
            if (kb != null && kb.fKey.wasPressedThisFrame) { SetFilter((Filter + 1) % Filters.Length); return true; }
            if ((kb != null && (kb.wKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame
                 || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) || (pad != null && pad.dpad.ReadValue().sqrMagnitude > 0.1f)) tipByKeys = true;
            var canvas = PixelHud.Canvas;
            if (mouse != null && canvas != null)
            {
                var m = mouse.position.ReadValue();
                var p = new Vector2Int(Mathf.FloorToInt(m.x / Screen.width * canvas.w), Mathf.FloorToInt((1f - m.y / Screen.height) * canvas.h));
                if ((m - lastMouse).sqrMagnitude > 1f || mouse.leftButton.wasPressedThisFrame) { catalogueMouse = p; tipByKeys = false; }
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (searchRect.Contains(p)) { searchFocus = true; return true; }
                    for (int i = 0; i < chipRects.Count; i++) if (chipRects[i].Contains(p)) { SetFilter(i); MadMax.Audio.Sfx.Play2D("click", 0.5f); return true; }
                    for (int i = 0; i < catRects.Count; i++) if (catRects[i].Contains(p)) { search = ""; SetCatalogueCategory(i); MadMax.Audio.Sfx.Play2D("click", 0.5f); return true; }
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    int maxRow = Mathf.Max(0, (items.Count + gridCols - 1) / gridCols - gridRows);
                    gridScroll = Mathf.Clamp(gridScroll - (int)Mathf.Sign(wheel), 0, maxRow);
                }
            }
            if (dc != 0) { search = ""; SetCatalogueCategory(((Current == Page.Build ? buildCategory : category) + dc + cats) % cats); MadMax.Audio.Sfx.Play2D("click", 0.5f); return true; }
            return false;
        }

        /// <summary>Typing into the search box: letters filter as you type, Backspace deletes, Enter / Tab keep the search
        /// and hand the keys back to the grid, Esc clears it.</summary>
        void SearchTick(Keyboard kb, bool esc)
        {
            string before = search;
            foreach (char ch in typed) if (ch != '/' && search.Length < 24) search += char.ToUpperInvariant(ch);
            typed = "";
            if (kb != null && kb.backspaceKey.wasPressedThisFrame && search.Length > 0) search = search.Substring(0, search.Length - 1);
            if (esc) { search = ""; searchFocus = false; }
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) searchFocus = false;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !searchRect.Contains(catalogueMouse)) searchFocus = false;
            if (search != before) { cursor = 0; gridScroll = 0; Rebuild(); }
        }

        /// <summary>Automation and the chips: show only ALL / KNOWN / CAN MAKE (build: ALL / CAN BUILD).</summary>
        public void SetFilter(int f) { Filter = Mathf.Clamp(f, 0, Filters.Length - 1); cursor = 0; gridScroll = 0; Rebuild(); }

        /// <summary>Automation: search the open catalogue (empty = off).</summary>
        public void SetSearch(string s) { search = (s ?? "").ToUpperInvariant(); cursor = 0; gridScroll = 0; Rebuild(); }

        void SetCatalogueCategory(int i)
        {
            if (Current == Page.Build) buildCategory = i; else category = i;
            cursor = 0; gridScroll = 0;
            Rebuild();
        }

        /// <summary>Grid moves: left / right one cell, up / down one row (the selection stays in view).</summary>
        void CatalogueMove(int dx, int dy)
        {
            if (items.Count == 0) return;
            cursor = Mathf.Clamp(cursor + dx + dy * gridCols, 0, items.Count - 1);
            int row = cursor / gridCols;
            if (row < gridScroll) gridScroll = row;
            if (row >= gridScroll + gridRows) gridScroll = row - gridRows + 1;
        }

        // ------------------------------------------------------------------ drawing
        /// <summary>The catalogue frame: title, categories, search and filters, the labelled icon grid; returns the panel rect.</summary>
        RectInt DrawCatalogue(PixelCanvas c, string title, string sub, List<string> catNames, int catSel, int footer)
        {
            int w = Mathf.Min(c.w - 12, 580), h = Mathf.Min(c.h - 22, 330);
            int x = (c.w - w) / 2, y = Mathf.Max(6, (c.h - h) / 2 - 5);
            c.Panel(x, y, w, h);
            c.Text(x + 8, y + 7, title, Amber);
            if (sub != null) c.Text(x + 8 + PixelCanvas.TextWidth(title) + 10, y + 7, FitCraft(sub, w - PixelCanvas.TextWidth(title) - 26), Dim);
            catRects.Clear();
            int cy = y + 22;
            for (int i = 0; i < catNames.Count; i++)
            {
                var r = new RectInt(x + 4, cy - 2, CatWidth - 6, CatRow);
                catRects.Add(r);
                bool sel = i == catSel && search.Length == 0, hov = r.Contains(catalogueMouse);
                if (sel) { c.Rect(r.x, r.y, r.width, r.height, Hi); c.Rect(r.x, r.y, 2, r.height, Amber); }
                else if (hov) c.Rect(r.x, r.y, r.width, r.height, new Color32(Hi.r, Hi.g, Hi.b, 110));
                c.Text(r.x + 5, cy + 1, FitCraft(catNames[i], r.width - 8), sel ? Amber : search.Length > 0 ? Dim : Text);
                cy += CatRow + 1;
            }
            int gx = x + CatWidth + 4, gw = w - CatWidth - 14;
            c.Line(gx - 3, y + 20, gx - 3, y + h - footer - 4, MadMax.Voxel.Pal.PanelEdge);
            // search box and filter chips
            int hy = y + 20;
            searchRect = new RectInt(gx, hy, 150, HeaderH - 2);
            c.Rect(searchRect.x, searchRect.y, searchRect.width, searchRect.height, searchFocus ? Hi : new Color32(14, 10, 8, 200));
            c.Frame(searchRect.x, searchRect.y, searchRect.width, searchRect.height, searchFocus ? Amber : MadMax.Voxel.Pal.PanelEdge);
            string shown = search.Length > 0 ? search + (searchFocus && (int)(Time.unscaledTime * 2f) % 2 == 0 ? "_" : "") : searchFocus ? "_" : "/ SEARCH";
            c.Text(searchRect.x + 4, hy + 3, FitCraft(shown, searchRect.width - 8), search.Length > 0 || searchFocus ? Text : Dim);
            chipRects.Clear();
            int chx = gx + 158;
            var filters = Filters;
            for (int i = 0; i < filters.Length; i++)
            {
                int cw = PixelCanvas.TextWidth(filters[i]) + 8;
                var r = new RectInt(chx, hy, cw, HeaderH - 2);
                chipRects.Add(r);
                bool on = i == Filter;
                c.Rect(r.x, r.y, r.width, r.height, on ? Hi : new Color32(14, 10, 8, 160));
                c.Frame(r.x, r.y, r.width, r.height, on ? Amber : MadMax.Voxel.Pal.PanelEdge);
                c.Text(r.x + 4, hy + 3, filters[i], on ? Amber : Dim);
                chx += cw + 3;
            }
            c.Text(chx + 4, hy + 3, items.Count + (items.Count == 1 ? " THING" : " THINGS"), Dim);
            // the grid
            int gy = hy + HeaderH + 3, gh = y + h - footer - 6 - gy;
            gridCols = Mathf.Max(1, gw / CellW);
            gridRows = Mathf.Max(1, gh / CellH);
            int rowsTotal = (items.Count + gridCols - 1) / gridCols;
            gridScroll = Mathf.Clamp(gridScroll, 0, Mathf.Max(0, rowsTotal - gridRows));
            int ox = gx + (gw - gridCols * CellW) / 2;
            if (iconFrame != Time.frameCount) { iconFrame = Time.frameCount; iconsThisFrame = 0; }
            int chars = (CellW - 4) / 4;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int row = i / gridCols - gridScroll, col = i % gridCols;
                if (row < 0 || row >= gridRows) { it.rect = new RectInt(-99, -99, 0, 0); continue; }
                int cx = ox + col * CellW, cyy = gy + row * CellH;
                it.rect = new RectInt(cx, cyy, CellW - 2, CellH - 2);
                bool sel = i == cursor, ready = Enabled(it), hov = it.rect.Contains(catalogueMouse);
                c.Rect(cx, cyy, CellW - 2, CellH - 2, sel ? Hi : hov ? new Color32(Hi.r, Hi.g, Hi.b, 140) : new Color32(20, 14, 10, 150));
                c.Frame(cx, cyy, CellW - 2, CellH - 2, sel ? Amber : ready ? MadMax.Voxel.Pal.PanelEdge : new Color32(70, 30, 24, 255));
                var icon = Icon(it);
                int ix = cx + (CellW - 2 - IconSize) / 2, iy = cyy + 2;
                if (icon != null) c.Blit(ix, iy, IconSize, icon, !it.known);              // not learned yet: greyed out
                else c.Text(ix + 11, iy + 10, ".", Dim);
                if (!it.known) c.Text(cx + CellW - 9, cyy + 3, "?", Dim);
                else if (!ready) c.Rect(cx + CellW - 7, cyy + 2, 3, 3, Red);                // something missing
                // the name, on two lines under the icon
                string name = it.recipe != null ? it.recipe.name : it.piece != null ? it.piece.name : it.label;
                var lines = CellLines(name, chars);
                var col2 = !it.known ? new Color32(Dim.r, Dim.g, Dim.b, 170) : sel ? Amber : Text;
                for (int k = 0; k < lines.Count; k++) c.Text(cx + (CellW - 2 - PixelCanvas.TextWidth(lines[k])) / 2, cyy + IconSize + 5 + k * 7, lines[k], col2, 1, false);
            }
            if (items.Count == 0) c.Text(gx + 6, gy + 6, search.Length > 0 ? "NOTHING CALLED THAT" : "NOTHING HERE", Dim);
            if (rowsTotal > gridRows)
            {
                int track = gridRows * CellH, bar = Mathf.Max(8, track * gridRows / rowsTotal);
                int by = gy + (track - bar) * gridScroll / Mathf.Max(1, rowsTotal - gridRows);
                c.Rect(x + w - 7, gy, 2, track, new Color32(40, 28, 20, 200));
                c.Rect(x + w - 7, by, 2, bar, Amber);
            }
            return new RectInt(x, y, w, h);
        }

        /// <summary>A name in up to two lines of <paramref name="chars"/> characters (word wrapped; what doesn't fit is cut
        /// and marked with a dot).</summary>
        static List<string> CellLines(string name, int chars)
        {
            var l = new List<string>();
            string line = "";
            bool cut = false;
            foreach (var word in name.Split(' '))
            {
                string wd = word.Length > chars ? word.Substring(0, chars) : word;
                if (wd.Length < word.Length) cut = true;
                if (line.Length == 0) line = wd;
                else if (line.Length + 1 + wd.Length <= chars) line += " " + wd;
                else if (l.Count == 0) { l.Add(line); line = wd; }
                else { cut = true; break; }
            }
            if (line.Length > 0) l.Add(line);
            if (cut && l.Count > 0) { var last = l[l.Count - 1]; l[l.Count - 1] = (last.Length >= chars ? last.Substring(0, chars - 1) : last) + "."; }
            return l;
        }

        Color32[] Icon(Item it)
        {
            if (it.iconKey == null) return null;
            int size = IconSize * Mathf.Max(1, PixelHud.Canvas != null ? PixelHud.Canvas.res : 1);   // HD HUD: icons at screen detail
            string key = it.iconKey + "#" + size;
            if (!IconRenderer.Cached(key, size))
            {
                if (iconsThisFrame >= IconBudget) return null;
                iconsThisFrame++;
            }
            return IconRenderer.Get(key, it.iconMesh != null ? it.iconMesh() : null, size, it.diagonal);
        }

        /// <summary>The floating panel beside a cell: <paramref name="lines"/> (text, colour; null text = a gap) wrapped
        /// to its width.</summary>
        void DrawTip(PixelCanvas c, RectInt cell, List<(string text, Color32 col)> lines)
        {
            const int tw = 176;
            var wrapped = new List<(string, Color32)>();
            foreach (var (t, col) in lines)
            {
                if (t == null) { wrapped.Add(("", col)); continue; }
                foreach (var l in Wrap(t, tw - 10)) wrapped.Add((l, col));
            }
            int th = wrapped.Count * 8 + 8;
            int tx = cell.xMax + 4;
            if (tx + tw > c.w - 2) tx = cell.x - tw - 4;
            int ty = Mathf.Clamp(cell.y - 4, 2, Mathf.Max(2, c.h - th - 2));
            c.Panel(tx, ty, tw, th);
            int ly = ty + 5;
            foreach (var (t, col) in wrapped) { c.Text(tx + 5, ly, t, col); ly += 8; }
        }

        /// <summary>The cell the panel is for: the one under the mouse; the selected one only while moving by keys;
        /// otherwise none (the panel goes when the mouse leaves the cell).</summary>
        Item TipItem()
        {
            if (searchFocus) return null;
            if (!tipByKeys) { foreach (var it in items) if (it.rect.width > 0 && it.rect.Contains(catalogueMouse)) return it; return null; }
            return cursor >= 0 && cursor < items.Count ? items[cursor] : null;
        }

        // ------------------------------------------------------------------ crafting
        void DrawCrafting(PixelCanvas c)
        {
            var cats = StationCategories();
            var names = new List<string>();
            foreach (var k in cats) names.Add(k.ToString().ToUpperInvariant());
            const int footer = 44;
            var frame = DrawCatalogue(c, station ? station.title : "HANDCRAFT", station ? "PACK + STORAGE WITHIN 5 M" : "BY HAND, ANYWHERE, FROM YOUR PACK", names, Mathf.Clamp(category, 0, Mathf.Max(0, cats.Count - 1)), footer);
            int x = frame.x, y = frame.y, w = frame.width, h = frame.height;
            int qy = y + h - footer;
            c.Line(x + 7, qy, x + w - 8, qy, MadMax.Voxel.Pal.PanelEdge);
            string status = "READY WHEN YOU ARE";
            float progress = 0f;
            if (station && station.Current != null)
            {
                var job = station.Current;
                var recipe = RecipeLibrary.Get(job.recipe);
                progress = Mathf.Clamp01(job.progress);
                int seconds = recipe != null ? Mathf.CeilToInt((1f - progress) * RecipeLibrary.Seconds(recipe) / Mathf.Max(0.01f, job.speed)) : 0;
                status = (station.Powered ? "MAKING " : "PAUSED - NO POWER: ") + (recipe != null ? recipe.name : "...") + "  " + seconds + " S  [" + station.queue.Count + "/8]";
            }
            else if (station && station.TrayCount > 0) status = "FINISHED GOODS IN THE TRAY - COLLECT OUTSIDE THIS MENU";
            else if (!station && game.HandCurrent != null)
            {
                var j = game.HandCurrent;
                progress = Mathf.Clamp01(j.progress);
                status = "IN YOUR HANDS: " + j.recipe.name + "  " + Mathf.CeilToInt((1f - progress) * WastelandGame.HandSeconds(j.recipe) / Mathf.Max(0.01f, j.speed)) + " S  [" + game.HandQueue.Count + "/" + WastelandGame.HandQueueMax + "]";
            }
            c.Text(x + 8, qy + 5, FitCraft(status, w - 16), Amber);
            c.Rect(x + 8, qy + 14, w - 16, 3, Hi);
            c.Rect(x + 8, qy + 14, Mathf.RoundToInt((w - 16) * progress), 3, Green);
            c.Text(x + 8, qy + 23, FitCraft("CLICK / ENTER MAKE   ARROWS SELECT   " + CategoryKeys + " CATEGORY   / SEARCH   F FILTER   ESC LEAVE", w - 16), Text);
            c.Text(x + 8, qy + 33, FitCraft(station ? "R RESEARCH   T REPAIR   Y SALVAGE   X CANCEL LAST   JOBS KEEP WORKING WHILE YOU EXPLORE"
                : "X PUT DOWN THE LAST   HAND JOBS GO ON WHILE YOU WALK   A BENCH MAKES MORE, AND FASTER", w - 16), Dim);
            var tip = TipItem();
            if (tip != null && tip.recipe != null && tip.rect.width > 0) DrawTip(c, tip.rect, RecipeTip(tip.recipe, tip.known));
        }

        static string CategoryKeys => Controls.Name(Controls.Act.BuildPrevCategory) + " " + Controls.Name(Controls.Act.BuildNextCategory);

        List<(string, Color32)> RecipeTip(Recipe r, bool known)
        {
            var l = new List<(string, Color32)> { (r.name + (r.amount > 1 ? "  X" + r.amount : ""), Amber) };
            if (!known)
            {
                l.Add(("YOU DON'T KNOW HOW TO MAKE THIS YET.", Dim));
                l.Add(("A BOOK, A TAPE, A BLUEPRINT OR RESEARCH AT A WORKBENCH MIGHT TEACH IT.", Dim));
                return l;
            }
            if (!string.IsNullOrEmpty(r.description)) l.Add((r.description, Text));
            l.Add((null, Dim));
            l.Add(("TAKES:", Dim));
            foreach (var (t, n) in r.resources) if (t != ResourceType.None) { int have = PoolRes(t), need = RecipeLibrary.Amount(n); l.Add(("  " + ResourceInfo.Name(t) + "  " + have + "/" + need, have >= need ? Green : Red)); }
            foreach (var (id, n) in r.items) { int have = PoolItem(id); l.Add(("  " + ItemIds.Name(id) + "  " + have + "/" + n, have >= n ? Green : Red)); }
            if (r.fuel != ResourceType.None) { var fuel = game.CraftFuel(r, station); int have = PoolRes(fuel); l.Add(("  FUEL: " + ResourceInfo.Name(fuel) + "  " + have + "/" + r.fuelAmount, have >= r.fuelAmount ? Green : Red)); }
            float secs = (station ? RecipeLibrary.Seconds(r) : WastelandGame.HandSeconds(r)) / game.CraftSpeed(r);
            l.Add(("ABOUT " + Mathf.CeilToInt(secs) + " S" + (station ? "" : " BY HAND"), Dim));
            if (station && r.hand) l.Add(("ALSO MADE BY HAND (" + Controls.Name(Controls.Act.Craft) + ")", Dim));
            string blocked = game.CraftBlockReason(r, station);
            if (blocked == null && !station && game.HandQueue.Count >= WastelandGame.HandQueueMax) blocked = "YOUR HANDS ARE FULL";
            l.Add((blocked ?? "CLICK OR ENTER: MAKE IT", blocked == null ? Green : Amber));
            return l;
        }

        // ------------------------------------------------------------------ building
        void DrawBuildPage(PixelCanvas c)
        {
            var names = new List<string>();
            foreach (var k in BuildMode.Categories) names.Add(k.ToString().ToUpperInvariant());
            const int footer = 24;
            bool holding = game.Player.Tool && game.Player.Tool.id == ItemIds.ClawHammer;
            string sub = holding ? "MATERIALS FROM YOUR PACK" : game.Inventory.GetItem(ItemIds.ClawHammer) > 0 ? "THE CLAW HAMMER COMES OUT WHEN YOU PICK" : "YOU NEED A CLAW HAMMER TO BUILD";
            var frame = DrawCatalogue(c, "BUILD", sub, names, buildCategory, footer);
            int x = frame.x, y = frame.y, w = frame.width, h = frame.height;
            int qy = y + h - footer;
            c.Line(x + 7, qy, x + w - 8, qy, MadMax.Voxel.Pal.PanelEdge);
            c.Text(x + 8, qy + 5, FitCraft("CLICK / ENTER PICK   ARROWS SELECT   " + CategoryKeys + " CATEGORY   / SEARCH   F FILTER   ESC CLOSE", w - 16), Text);
            c.Text(x + 8, qy + 14, FitCraft("PLACING: LMB PLACE  " + Controls.Name(Controls.Act.BuildRotate) + " ROTATE  TAP " + Controls.Name(Controls.Act.Build) + " PUT THE HAMMER AWAY  HOLD " + Controls.Name(Controls.Act.Build) + " THIS MENU", w - 16), Dim);
            var tip = TipItem();
            if (tip != null && tip.piece != null && tip.rect.width > 0) DrawTip(c, tip.rect, PieceTip(tip.piece));
        }

        List<(string, Color32)> PieceTip(FurnitureDef d)
        {
            var b = game.Build;
            var l = new List<(string, Color32)> { (d.name, Amber) };
            foreach (var line in FurnitureLibrary.Describe(d)) l.Add((line, Text));
            l.Add((null, Dim));
            if (d.kit != null)
            {
                int have = game.Inventory.GetItem(d.kit);
                l.Add(("NEEDS: " + ItemIds.Name(d.kit) + "  " + have + "/1", have > 0 ? Green : Red));
                Recipe kitRecipe = null;
                foreach (var r in RecipeLibrary.All) if (r.output == d.kit) { kitRecipe = r; break; }
                if (kitRecipe != null) l.Add(("THE KIT IS MADE AT A " + kitRecipe.station.Replace('_', ' ').ToUpperInvariant(), Dim));
            }
            else if (d.link != UtilityKind.None && d.cost.Length > 0) l.Add(("TAKES 1 " + ResourceInfo.Name(d.cost[0].type) + " PER 5 M", Text));
            else if (d.cost.Length > 0)
            {
                l.Add(("TAKES:", Dim));
                foreach (var (t, n) in d.cost) { int need = b ? b.Cost(n) : n, have = game.Inventory.Get(t); l.Add(("  " + ResourceInfo.Name(t) + "  " + have + "/" + need, have >= need ? Green : Red)); }
            }
            if (d.needsItem != null) { int have = game.Inventory.GetItem(d.needsItem); l.Add(("ALSO: " + ItemIds.Name(d.needsItem) + "  " + have + "/1", have > 0 ? Green : Red)); }
            bool ok = b && b.Affordable(d);
            l.Add((ok ? "CLICK OR ENTER: PICK IT AND PLACE IT" : "MISSING MATERIALS", ok ? Green : Amber));
            return l;
        }
    }
}
