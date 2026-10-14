using MadMax.Vehicles;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Heavy lifters on driveable vehicles: the lifting magnet (<see cref="MagnetCrane"/>) and the scrap grapple
    /// with its crusher bay (<see cref="GrappleCrusher"/>). Both sit beside the hook <see cref="Crane"/> on every vehicle
    /// and act only while their part is mounted; they share the crane keys (7 / 8 / 9 / 0, Shift; arrows + Q when no
    /// machine claims them; E runs the crusher).</summary>
    public partial class WastelandGame
    {
        static void AddLifters(VehicleDriver v)
        {
            if (!v.GetComponent<MagnetCrane>()) v.gameObject.AddComponent<MagnetCrane>();
            if (!v.GetComponent<GrappleCrusher>()) v.gameObject.AddComponent<GrappleCrusher>();
        }

        /// <summary>The vehicle carries a working magnet crane or grapple (the arrows and Q / E work it).</summary>
        static bool LifterTool(VehicleDriver v) =>
            v && ((v.TryGetComponent<MagnetCrane>(out var m) && m.Part) || (v.TryGetComponent<GrappleCrusher>(out var g) && g.Part));

        void LifterControls(Keyboard kb, bool machineHere, float toolUp, float toolSide, bool shift)
        {
            bool p7 = kb.digit7Key.wasPressedThisFrame;
            float slew = kb.digit0Key.isPressed ? (shift ? -1f : 1f) : 0f;
            if (Current.TryGetComponent<MagnetCrane>(out var mag) && mag.Part)
            {
                bool free = !machineHere;
                mag.Control(p7 || free && Controls.Down(Controls.Act.ToolA), kb.digit8Key.isPressed || free && toolUp > 0f,
                    kb.digit9Key.isPressed || free && toolUp < 0f, slew != 0f ? slew : free ? toolSide : 0f, shift);
            }
            if (Current.TryGetComponent<GrappleCrusher>(out var grab) && grab.Part)
            {
                bool free = !machineHere;
                grab.Control(p7 || free && !shift && Controls.Down(Controls.Act.ToolA), kb.digit8Key.isPressed || free && toolUp > 0f,
                    kb.digit9Key.isPressed || free && toolUp < 0f, slew != 0f ? slew : free ? toolSide : 0f, shift,
                    free && !shift && Controls.Down(Controls.Act.ToolB));
            }
        }
    }
}
