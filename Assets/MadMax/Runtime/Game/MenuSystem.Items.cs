using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Items block on the pack page: DROP ONE (resources: 5) / DROP ALL with Shift, and PLACE (a preview in the
    /// world, see <see cref="WastelandGame.BeginPlaceItem"/>), for the row under the cursor. Keys are the player's
    /// bindings (Drop, Run, Build); pad: X drops, Y places.</summary>
    public partial class MenuSystem
    {
        string pendingPlace;       // PLACE chosen: the page closes once the key is let go (a released B would toggle build mode)

        /// <summary>The pack page's hint for dropping or placing an item id or "res:N".</summary>
        static string DropHint(string key)
        {
            bool res = key.StartsWith("res:");
            string q = Controls.Name(Controls.Act.Drop);
            return "   " + q + " DROP" + (res ? " 5  " : "  ") + Controls.Name(Controls.Act.Run) + "+" + q + " ALL  "
                   + Controls.Name(Controls.Act.Build) + " PLACE" + (res ? " 10" : "");
        }

        /// <summary>Drop / place keys on the pack page. True when they did something (the rest of the input waits).</summary>
        bool InventoryDropKeys(Gamepad pad)
        {
            if (pendingPlace != null)
            {
                if (Controls.Held(Controls.Act.Build) || Controls.Up(Controls.Act.Build) || (pad != null && (pad.buttonNorth.isPressed || pad.buttonNorth.wasReleasedThisFrame))) return true;
                var key = pendingPlace;
                pendingPlace = null;
                Close();
                game.BeginPlaceItem(key);
                return true;
            }
            if (items.Count == 0 || items[cursor].drop == null) return false;
            var drop = items[cursor].drop;
            if (Controls.Down(Controls.Act.Drop) || (pad != null && pad.buttonWest.wasPressedThisFrame))
            {
                int have = game.PackCount(drop);
                int n = Controls.Held(Controls.Act.Run) ? have : drop.StartsWith("res:") ? Mathf.Min(5, have) : 1;
                if (game.DropFromPack(drop, n)) MadMax.Audio.Sfx.Play2D("click", 0.5f);
                Rebuild();
                return true;
            }
            if (Controls.Down(Controls.Act.Build) || (pad != null && pad.buttonNorth.wasPressedThisFrame))
            {
                if (game.CanPlaceItem(drop, out var why)) pendingPlace = drop;
                else game.Toast(why);
                return true;
            }
            return false;
        }
    }
}
