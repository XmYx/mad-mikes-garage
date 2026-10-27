using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Binoculars: hold the right mouse button (or swing) to look. First / third person zoom right in;
    /// the top-down views pull back to see further. While looking, the HUD names people, vehicles and landmarks far away,
    /// the glasses are raised to the eyes (both hands; first person shows the two lenses on the HUD instead) and, with the
    /// sun ahead, the lenses flash: what you are watching may see it (<see cref="WastelandGame.BinocularTick"/>).</summary>
    public class BinocularsTool : HandTool
    {
        public static bool Looking { get; private set; }
        /// <summary>Automation: look through them without the mouse.</summary>
        public static bool ForceLook;
        float swingLook;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Looking = false; ForceLook = false; }

        /// <summary>Raised to the eyes: both hands up, elbows out, the head a little down into them.</summary>
        static readonly ToolPose Raised = new ToolPose { chestX = -3f, armRX = -112f, armRY = -6f, armRZ = -22f, foreR = -122f, handRZ = -10f, armLX = -112f, armLY = 6f, armLZ = 22f, foreL = -122f };

        /// <summary>Raised to the eyes whenever looking (not in first person: the lenses are the HUD there).</summary>
        public bool RaisedNow
        {
            get
            {
                if (!Looking || !enabled) return false;
                var g = WastelandGame.Instance;
                return !(g && g.cameraRig && g.cameraRig.mode == ViewMode.FirstPerson);
            }
        }

        public override string Hold => RaisedNow ? null : base.Hold;
        public override ToolPose? IdlePose => RaisedNow ? Raised : base.IdlePose;

        public override void Strike(PlayerCharacter user) => swingLook = Time.time + 3f;

        void Update()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            Looking = enabled && ((m != null && m.rightButton.isPressed) || Time.time < swingLook || ForceLook);
            var g = WastelandGame.Instance;
            if (Looking && g && g.Player && g.Player.Tool == this) g.BinocularTick(Time.deltaTime);
            else WastelandGame.GlintStrength = 0f;
        }

        void OnDisable() { Looking = false; WastelandGame.GlintStrength = 0f; }
    }
}
