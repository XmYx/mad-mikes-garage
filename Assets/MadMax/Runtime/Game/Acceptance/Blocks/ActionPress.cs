using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Presses a game action for one frame as the keyboard would: it fires <see cref="Controls.Inject"/> from an
    /// Update that runs before <see cref="WastelandGame"/>'s, so the game's own key handlers see it that frame (a
    /// coroutine resumes after Update, too late). Removes itself after the press.</summary>
    [DefaultExecutionOrder(-1000)]
    public class ActionPress : MonoBehaviour
    {
        Controls.Act act;
        /// <summary>The frame the press was delivered (-1 = not yet).</summary>
        public int Frame { get; private set; } = -1;

        public static ActionPress Press(Controls.Act a)
        {
            var p = new GameObject("ActionPress " + a).AddComponent<ActionPress>();
            p.act = a;
            return p;
        }

        void Update()
        {
            if (Frame >= 0) { Destroy(gameObject); return; }
            Controls.Inject(act);
            Frame = Time.frameCount;
        }
    }
}
