using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Binoculars: hold the right mouse button (or swing) to look. First / third person zoom right in;
    /// the top-down views pull back to see further. While looking, the HUD names people, vehicles and landmarks far away.</summary>
    public class BinocularsTool : HandTool
    {
        public static bool Looking { get; private set; }
        float swingLook;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Looking = false;

        public override void Strike(PlayerCharacter user) => swingLook = Time.time + 3f;

        void Update()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            Looking = enabled && ((m != null && m.rightButton.isPressed) || Time.time < swingLook);
        }

        void OnDisable() => Looking = false;
    }
}
