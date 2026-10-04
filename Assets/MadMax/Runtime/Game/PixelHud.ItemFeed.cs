using MadMax.Items;
using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Items block HUD: the item feed (what came into and went out of the pack) under the resources on the left,
    /// kept clear of the prompt list, and the PLACE preview's status line above the hotbar.</summary>
    public partial class PixelHud
    {
        const int ItemFeedRow = 11, ItemFeedIcon = 9;

        partial void DrawItemFeed()
        {
            DrawItemPlaceStatus();
            float now = Time.unscaledTime;
            ItemFeed.Tick(now);
            var rows = ItemFeed.Rows;
            if (rows.Count == 0) return;
            var car = game.Current;
            bool fps = rig.mode == ViewMode.FirstPerson;
            int x = !car && game.Build && game.Build.Active ? 142 : 6;                               // right of the build panel
            int y = car ? 44 : 31;                                                                  // under the resources (and a car's vitals)
            int bottom = canvas.h - (car && !fps ? 76 : 84) - ItemFeedPromptHeight() - 2;
            int max = Mathf.Min(ItemFeed.MaxRows, (bottom - y) / ItemFeedRow);
            for (int i = 0; i < rows.Count && i < max; i++)
            {
                var r = rows[i];
                float gone = ItemFeed.Out(r, now), come = Mathf.Clamp01((now - r.born) / 0.12f);
                int tw = PixelCanvas.TextWidth(r.text), sw = r.source != null ? PixelCanvas.TextWidth(r.source) + 5 : 0;
                int w = ItemFeedIcon + 4 + tw + sw + 3;
                int dx = -Mathf.RoundToInt((w + x + 2) * Mathf.Max(gone, 1f - come));              // slides in, and out when done
                byte a = (byte)(255 * (1f - gone));
                var col = r.amount > 0 ? Green : Red;
                if (now - r.last < 0.15f && r.last > r.born) col = Text;                            // a merge flashes the row
                col.a = a;
                var dim = Dim; dim.a = a;
                canvas.Rect(x - 2 + dx, y - 1, w + 2, ItemFeedRow, new Color32(8, 10, 10, (byte)(140 * (1f - gone))));
                int isz = ItemFeedIcon * canvas.res;
                if (r.icon == null || r.icon.Length != isz * isz) r.icon = ItemFeedIconFor(r, canvas.res);   // HD HUD: icons at screen detail
                canvas.Blit(x + dx, y, ItemFeedIcon, r.icon);
                canvas.Text(x + dx + ItemFeedIcon + 3, y + 2, r.text, col);
                if (r.source != null) canvas.Text(x + dx + ItemFeedIcon + 3 + tw + 5, y + 2, r.source, dim);
                y += ItemFeedRow;
            }
        }

        static Color32[] ItemFeedIconFor(ItemFeed.Row r, int res)
        {
            string key = r.Key;
            var mesh = WorldItemModels.IconMesh(key, out bool diagonal);
            return IconRenderer.Get("feed:" + key + (res > 1 ? "@" + res : ""), mesh, ItemFeedIcon * res, diagonal);
        }

        /// <summary>Height of the lower-left prompt list this frame (same layout as HudKeys.DrawPromptList).</summary>
        int ItemFeedPromptHeight()
        {
            if (game.RadialOpen || game.ShowHelp) return 0;
            var p = game.Prompt;
            if (string.IsNullOrEmpty(p)) return 0;
            int rows = 0, first = -1;
            for (int i = 0; i < p.Length; i++) if (p[i] == '[') { rows++; if (first < 0) first = i; }
            bool head = false;
            for (int i = 0; i < (first < 0 ? p.Length : first) && !head; i++) head = p[i] != ' ';
            return rows * 10 + (head ? 9 : 0) + 4;
        }

        void DrawItemPlaceStatus()
        {
            var s = game.PlaceStatus;
            if (s == null || (game.Menus && game.Menus.IsOpen)) return;
            int w = PixelCanvas.TextWidth(s) + 10, x = (canvas.w - w) / 2, y = canvas.h - 42;
            canvas.Panel(x, y, w, 12);
            canvas.Text(x + 5, y + 3, s, game.PlaceOk ? Green : Amber);
        }
    }
}
