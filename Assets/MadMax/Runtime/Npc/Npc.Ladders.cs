using MadMax.Building;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>People use ladders (roadmap 29, "NPCs in NpcPath"): a goal well above or below the walker with a ladder
    /// linking the two levels (its top near the upper one) is reached by walking to the ladder's end, climbing it like the
    /// player does (hands over hands, facing the rungs) and stepping off at the other end.</summary>
    public partial class Npc
    {
        public const float ClimbSpeed = 1.1f, LadderLevel = 1.8f, LadderReach = 5f;
        Ladder climbLadder; float climbAt, climbPhase; bool climbUp;
        /// <summary>Ladders climbed (tests).</summary>
        [System.NonSerialized] public int laddersClimbed;
        public bool Climbing => climbLadder;

        static Vector3 LowEnd(Ladder l) => l.Foot.y <= l.Top.y ? l.Foot : l.Top;
        static Vector3 HighEnd(Ladder l) => l.Foot.y <= l.Top.y ? l.Top : l.Foot;

        /// <summary>A ladder between <paramref name="me"/> and <paramref name="goal"/> when they are on different levels:
        /// the step toward its near end, or zero when the climb just started. False: no ladder needed or none fits.</summary>
        bool LadderStep(Vector3 me, Vector3 goal, out Vector3 step)
        {
            step = Vector3.zero;
            float dy = goal.y - me.y;
            if (Mathf.Abs(dy) < LadderLevel || Ladder.All.Count == 0) return false;
            bool up = dy > 0f;
            var t = MadMax.World.DeformableTerrain.Instance;
            var raised = up ? goal : me;                                                             // only for a raised level (deck, span, roof), not a hillside
            if (t && raised.y - t.HeightNoLoad(raised.x, raised.z) < 1.5f) return false;
            Ladder best = null; float bd = float.MaxValue;
            foreach (var l in Ladder.All)
            {
                if (!l) continue;
                Vector3 lo = LowEnd(l), hi = HighEnd(l);
                var upper = up ? goal : me; var lower = up ? me : goal;
                if (Mathf.Abs(hi.y - upper.y) > 1.6f || Flat(hi - upper).magnitude > 40f) continue;   // its top on the upper level
                if (lo.y - 0.9f > lower.y + 1.2f) continue;                                          // its foot reachable below
                float d = Flat((up ? lo : hi) - me).magnitude + Flat((up ? hi : lo) - goal).magnitude;
                if (d < bd) { bd = d; best = l; }
            }
            if (!best) return false;
            var start = up ? LowEnd(best) : HighEnd(best);
            var to = Flat(start - me);
            if (to.magnitude < 0.7f) { StartClimb(best, up); return true; }
            step = to;
            return true;
        }

        void StartClimb(Ladder l, bool up)
        {
            climbLadder = l; climbUp = up; climbPhase = 0f;
            float len = Vector3.Distance(l.Foot, l.Top);
            bool footLow = l.Foot.y <= l.Top.y;
            climbAt = up == footLow ? 0f : len;                                                      // from the end we stand at
            routing = false;
        }

        /// <summary>One climbing step; false once off the ladder (the brain carries on).</summary>
        bool ClimbTick(float dt)
        {
            var l = climbLadder;
            if (!l || mode == Mode.Fight && foe) { climbLadder = null; cc.enabled = true; return false; }
            float len = Vector3.Distance(l.Foot, l.Top);
            bool footLow = l.Foot.y <= l.Top.y;
            float dir = climbUp == footLow ? 1f : -1f;                                               // along Foot → Top
            climbAt += dir * ClimbSpeed * dt / Mathf.Max(0.5f, Mathf.Abs(Vector3.Dot(l.Axis, Vector3.up)));
            climbPhase += dt * 3.2f;
            if (climbAt >= len - 0.15f || climbAt <= 0f)
            {
                var end = climbAt <= 0f ? l.Foot : l.Top;
                var over = Vector3.ProjectOnPlane(l.Axis, Vector3.up);
                var fwd = over.sqrMagnitude > 0.01f ? over.normalized * (climbAt <= 0f ? -1f : 1f) : transform.forward;
                var at = end + fwd * 0.55f + Vector3.up * 0.2f;
                if (Physics.Raycast(at + Vector3.up * 1f, Vector3.down, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point + Vector3.up * 0.05f;
                cc.enabled = false; transform.position = at; cc.enabled = true;
                climbLadder = null; laddersClimbed++;
                return false;
            }
            var side = Vector3.ProjectOnPlane(transform.position - l.Foot, l.Axis);
            var flatSide = Vector3.ProjectOnPlane(side, Vector3.up);
            var outward = flatSide.sqrMagnitude > 0.01f ? flatSide.normalized : -Vector3.ProjectOnPlane(l.transform.up, Vector3.up).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = -transform.forward;
            cc.enabled = false;
            transform.SetPositionAndRotation(l.Foot + l.Axis * climbAt + outward * 0.38f - Vector3.up * 0.9f, Quaternion.LookRotation(-outward));
            float swing = Mathf.Sin(climbPhase) * 25f;
            var pose = new ToolPose { armRX = -150f + swing, armLX = -150f - swing, armRZ = 8f, armLZ = -8f, foreR = -40f, foreL = -40f, chestX = 6f, knees = 20f + Mathf.Abs(swing) };
            anim.Tick(dt, new HumanAnimator.State { tool = pose, twoHanded = true, grounded = true });
            lastPos = transform.position;
            return true;
        }
    }
}
