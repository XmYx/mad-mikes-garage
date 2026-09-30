using MadMax.Rendering;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Roads (depth stage C): the player's own paving on the minimap and the world map, from the terrain's
    /// 4 m road cells (<see cref="DeformableTerrain.PlayerRoadAt"/>): asphalt, concrete and cobbles dark, gravel light.</summary>
    public partial class PixelHud
    {
        static readonly Color32 PlayerAsphalt = new Color32(46, 42, 40, 255), PlayerGravel = new Color32(140, 126, 106, 255);

        /// <summary>The terrain when the player has laid any road, else null (nothing to overlay).</summary>
        static DeformableTerrain RoadOverlay() { var t = DeformableTerrain.Instance; return t && t.PlayerRoadCount > 0 ? t : null; }

        static Color32 PlayerRoad(DeformableTerrain t, float wx, float wz, Color32 c) => t.PlayerRoadAt(wx, wz, out bool gravel) ? (gravel ? PlayerGravel : PlayerAsphalt) : c;

        /// <summary>World map page: one block per road cell, clipped to the map frame.</summary>
        void DrawPlayerRoads(PixelCanvas c, int x, int y, int w, int h, Vector2 center, float mpp)
        {
            var t = RoadOverlay();
            if (!t) return;
            int s = Mathf.Max(1, Mathf.RoundToInt(DeformableTerrain.MarkCell / mpp));
            foreach (var (pos, gravel) in t.PlayerRoads())
            {
                int px = x + w / 2 + Mathf.RoundToInt((pos.x - center.x) / mpp) - s / 2, py = y + h / 2 - Mathf.RoundToInt((pos.z - center.y) / mpp) - s / 2;
                int x0 = Mathf.Max(px, x + 2), y0 = Mathf.Max(py, y + 2), x1 = Mathf.Min(px + s, x + w - 2), y1 = Mathf.Min(py + s, y + h - 2);
                if (x1 > x0 && y1 > y0) c.Rect(x0, y0, x1 - x0, y1 - y0, gravel ? PlayerGravel : PlayerAsphalt);
            }
        }
    }
}
