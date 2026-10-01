using MadMax.Building;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Context block HUD: a small preview of what a storage holds while it is under the cursor (top-down
    /// views, drawn beside the cursor) or in front of you (the [E] focus, drawn on the right).</summary>
    public partial class PixelHud
    {
        const int PreviewRows = 6;

        partial void DrawContextUi()
        {
            if (game.Current || (game.Menus && game.Menus.IsOpen) || game.ShowHelp) return;
            var hover = game.ContextHover;
            if (!hover) return;
            Inventory inv; string title; string weight;
            if (hover is Container box) { inv = box.inventory; title = box.title; weight = box.Weight.ToString("0.0") + "/" + box.capacity.ToString("0") + " KG"; }
            else if (hover is Lootable spot) { inv = spot.Leftovers(false); title = spot.title; weight = inv != null ? ItemCatalog.TotalWeight(inv).ToString("0.0") + " KG" : ""; }
            else return;
            int lines = 0, more = 0;
            var rows = new System.Collections.Generic.List<string>();
            if (inv != null)
            {
                for (int t = 1; t < ResourceInfo.Count; t++)
                {
                    int n = inv.Get((ResourceType)t);
                    if (n <= 0) continue;
                    if (lines++ < PreviewRows) rows.Add(n + (ResourceInfo.IsFluid((ResourceType)t) ? "L " : " ") + ResourceInfo.Name((ResourceType)t)); else more++;
                }
                foreach (var kv in inv.Items)
                {
                    if (kv.Value <= 0) continue;
                    if (lines++ < PreviewRows) rows.Add((kv.Value > 1 ? kv.Value + " " : "") + ItemCatalog.Name(kv.Key)); else more++;
                }
            }
            if (rows.Count == 0) rows.Add("EMPTY");
            if (more > 0) rows.Add("+" + more + " MORE");
            int w = PixelCanvas.TextWidth(title + "  " + weight) + 8;
            foreach (var r in rows) w = Mathf.Max(w, PixelCanvas.TextWidth(r) + 8);
            int h = 13 + rows.Count * 8 + 2;
            int x, y;
            var mouse = Mouse.current;
            if (game.ContextHoverFromCursor && mouse != null)
            {
                var m = mouse.position.ReadValue();
                x = Mathf.RoundToInt(m.x / Screen.width * canvas.w) + 8; y = Mathf.RoundToInt((1f - m.y / Screen.height) * canvas.h) + 6;
            }
            else { x = canvas.w - w - 6; y = 112; }
            x = Mathf.Clamp(x, 2, canvas.w - w - 2); y = Mathf.Clamp(y, 2, canvas.h - h - 30);
            canvas.Panel(x, y, w, h);
            canvas.Text(x + 4, y + 3, title, Amber, 1, false);
            canvas.Text(x + w - 4 - PixelCanvas.TextWidth(weight), y + 3, weight, Dim, 1, false);
            for (int i = 0; i < rows.Count; i++) canvas.Text(x + 4, y + 13 + i * 8, rows[i], i == rows.Count - 1 && more > 0 ? Dim : Text, 1, false);
        }
    }
}
