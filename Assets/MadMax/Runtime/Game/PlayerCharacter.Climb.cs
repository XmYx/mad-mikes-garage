using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Climbing ladders (roadmap 29): walking into a ladder (a ladder piece, or a frame ladder of any length
    /// and lean) takes hold of it; forward / back climb up / down its axis, at the top the climber steps off onto
    /// what it leans on, at the foot backwards lets go; jump lets go anywhere.</summary>
    public partial class PlayerCharacter
    {
        /// <summary>The ladder being climbed (null = none).</summary>
        public Ladder Climbing { get; private set; }
        float climbAt, climbPhase;
        const float ClimbSpeed = 1.3f, Reach = 0.45f;

        /// <summary>One tick of climbing; true while on a ladder (the rest of the movement is skipped).</summary>
        bool ClimbTick(float dt)
        {
            if (Climbing == null)
            {
                if (moveInput.y < 0.3f || Carried || Swinging || !cc.enabled) return false;
                var lad = Ladder.At(transform.position + Vector3.up * 0.9f, Reach);
                if (!lad || Vector3.Dot(lad.Axis, Vector3.up) < 0.45f) return false;               // too flat: walk on it instead
                var to = Vector3.ProjectOnPlane(LadderPoint(lad, transform.position) - transform.position, Vector3.up);
                var push = Quaternion.Euler(0, viewYaw, 0) * Vector3.forward;
                if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, push) < 0.2f) return false;   // walking past it, not into it
                Climbing = lad;
                climbAt = Mathf.Clamp(Along(lad, transform.position), 0f, Length(lad) - 0.3f);
                vy = 0f;
            }
            var l = Climbing;
            if (!l || jump) { jump = false; LetGo(false); return false; }
            float len = Length(l);
            climbAt += moveInput.y * ClimbSpeed * dt / Mathf.Max(0.5f, Vector3.Dot(l.Axis, Vector3.up));
            climbPhase += Mathf.Abs(moveInput.y) * dt * 3.2f;
            if (climbAt >= len - 0.15f) { LetGo(true); return true; }                              // over the top
            if (climbAt <= 0f && moveInput.y < 0f) { LetGo(false); return true; }                 // down at the foot
            climbAt = Mathf.Max(0f, climbAt);
            // hang in front of the rungs (on the side the climber came from), facing them
            var side = Vector3.ProjectOnPlane(transform.position - l.Foot, l.Axis);
            var outward = Vector3.ProjectOnPlane(side, Vector3.up).sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(side, Vector3.up).normalized : -Vector3.ProjectOnPlane(l.transform.up, Vector3.up).normalized;
            var pos = l.Foot + l.Axis * climbAt + outward * 0.38f - Vector3.up * 0.9f;
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-outward));
            cc.enabled = true;
            Velocity = l.Axis * moveInput.y * ClimbSpeed;
            float swing = Mathf.Sin(climbPhase) * 25f;
            var pose = new ToolPose { armRX = -150f + swing, armLX = -150f - swing, armRZ = 8f, armLZ = -8f, foreR = -40f, foreL = -40f, chestX = 6f, knees = 20f + Mathf.Abs(swing) };
            anim.Tick(dt, new HumanAnimator.State { tool = pose, twoHanded = true, grounded = true, lookPitch = lookPitch });
            return true;
        }

        void LetGo(bool top)
        {
            var l = Climbing;
            Climbing = null;
            if (!l) return;
            if (top)
            {
                // step off over the top onto what it leans on (a span, a roof, a deck)
                var over = Vector3.ProjectOnPlane(l.Axis, Vector3.up);
                var fwd = over.sqrMagnitude > 0.01f ? over.normalized : transform.forward;
                var at = l.Top + fwd * 0.55f + Vector3.up * 0.2f;
                if (Physics.Raycast(at + Vector3.up * 1f, Vector3.down, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point + Vector3.up * 0.05f;
                Teleport(at, Quaternion.LookRotation(fwd).eulerAngles.y);
            }
            else
            {
                cc.enabled = false;
                transform.position -= transform.forward * 0.25f;
                cc.enabled = true;
            }
        }

        static float Length(Ladder l) => Vector3.Distance(l.Foot, l.Top);
        static float Along(Ladder l, Vector3 feet) => Vector3.Dot(feet + Vector3.up * 0.9f - l.Foot, l.Axis);
        static Vector3 LadderPoint(Ladder l, Vector3 feet) => l.Foot + l.Axis * Mathf.Clamp(Along(l, feet), 0f, Length(l));
    }
}
