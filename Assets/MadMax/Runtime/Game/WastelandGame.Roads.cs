using System.Globalization;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Roads block (depth stage C) hooks: the held use button drives the line painter (like reeling a fishing
    /// line), and the painter's colour and part-used dye are saved in <see cref="SaveData.blockRoads"/>. The roads
    /// themselves live in the terrain's chunk edits.</summary>
    public partial class WastelandGame
    {
        partial void RoadsUpdate()
        {
            if (!Player || !(Player.Tool is LinePainterTool painter)) return;
            if (ExternalInput) return;                                                           // automation sets spraying itself
            var mouse = Mouse.current; var pad = Gamepad.current;
            bool held = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            painter.spraying = held && !Current && !(Menus && Menus.IsOpen) && !(Build && Build.Active);
        }

        partial void RoadsSave(SaveData d)
        {
            d.blockRoads.Clear();
            d.blockRoads.Add("paint " + LinePainterTool.Colour);
            d.blockRoads.Add("metres " + LinePainterTool.Metres.ToString("0.###", CultureInfo.InvariantCulture));
        }

        partial void RoadsLoad(SaveData d)
        {
            LinePainterTool.Colour = 1; LinePainterTool.Metres = 0f;
            if (d.blockRoads == null) return;
            foreach (var line in d.blockRoads)
            {
                var parts = line.Split(' ');
                if (parts.Length < 2) continue;
                if (parts[0] == "paint" && byte.TryParse(parts[1], out var c) && (c == 1 || c == 2)) LinePainterTool.Colour = c;
                else if (parts[0] == "metres" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) LinePainterTool.Metres = m;
            }
        }

        partial void RoadsNewGame() { LinePainterTool.Colour = 1; LinePainterTool.Metres = 0f; }
    }
}
