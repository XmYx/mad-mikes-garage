using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Game
{
    public partial class PixelHud
    {
        /// <summary>Timed work at a vehicle (Anim block): what is being done and a bar that fills while working, just under
        /// the character (top-down views) or the crosshair. Walking up to the spot shows the name dimmed over an empty bar.</summary>
        partial void DrawWorkProgress()
        {
            if (!game || !game.Working || game.Current || (game.Menus && game.Menus.IsOpen)) return;
            string label = game.WorkLabel ?? "WORKING";
            bool walking = game.WorkApproaching;
            int lw = PixelCanvas.TextWidth(label);
            int w = Mathf.Max(60, lw + 10), h = 17;
            int x = (canvas.w - w) / 2, y = canvas.h / 2 + (rig.CrosshairView ? 9 : 16);
            canvas.Panel(x, y, w, h);
            canvas.Text(x + (w - lw) / 2, y + 3, label, walking ? Dim : Text);
            int bx = x + 4, bw = w - 8;
            canvas.Rect(bx, y + 11, bw, 3, new Color32(20, 12, 8, 220));
            int fill = Mathf.RoundToInt(bw * Mathf.Clamp01(game.WorkProgress));
            if (fill > 0) canvas.Rect(bx, y + 11, fill, 3, Amber);
            else if (walking) canvas.Rect(bx + Mathf.RoundToInt((Mathf.Sin(Time.time * 4f) * 0.5f + 0.5f) * (bw - 4)), y + 11, 4, 3, Dim);   // on the way
        }
    }
}
