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

        int holdFrames;
        /// <summary>The frame the action was released (holds only; -1 = still held).</summary>
        public int ReleasedFrame { get; private set; } = -1;

        /// <summary>Presses and holds the action for <paramref name="frames"/> frames, then releases it (Down, Held, Up).</summary>
        public static ActionPress Hold(Controls.Act a, int frames)
        {
            var p = Press(a);
            p.holdFrames = Mathf.Max(1, frames);
            return p;
        }

        void Update()
        {
            if (holdFrames > 0)
            {
                if (Frame < 0) { Controls.InjectHold(act, true); Frame = Time.frameCount; return; }
                if (ReleasedFrame >= 0) { Destroy(gameObject); return; }
                if (Time.frameCount - Frame >= holdFrames) { Controls.InjectHold(act, false); ReleasedFrame = Time.frameCount; }
                return;
            }
            if (Frame >= 0) { Destroy(gameObject); return; }
            Controls.Inject(act);
            Frame = Time.frameCount;
        }

        void OnDestroy() { if (holdFrames > 0 && ReleasedFrame < 0) Controls.InjectHold(act, false); }
    }
}
