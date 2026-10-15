using MadMax.Game;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Chase on the radio (2026-10-15): a boss car that has been running for <see cref="CallAfter"/> s calls its
    /// gang. Raider convoys of the same gang within <see cref="GangReach"/> m (any raider column within
    /// <see cref="OtherReach"/> m) hurry along their road toward the call and, once close, go straight to the attack —
    /// a long chase brings the rest of the gang, a short one doesn't.</summary>
    public partial class Convoy
    {
        public const float CallAfter = 20f, GangReach = 1500f, OtherReach = 700f, CallSeconds = 150f, CallHurry = 2.6f;
        float chaseT, callUntil;
        bool called;
        Vector3 callAt;
        /// <summary>Answering a gang call (heading to <see cref="CallPoint"/>).</summary>
        public bool Answering => Time.time < callUntil && phase == Phase.Travel;
        public Vector3 CallPoint => callAt;
        /// <summary>How many columns answered the last call (tests).</summary>
        public static int LastCallAnswered;

        /// <summary>From <see cref="RunTick"/>: count the chase, call the gang once it lasts.</summary>
        void ChaseRadio(WastelandGame g, Vector3 at, float dt)
        {
            chaseT += dt;
            if (called || chaseT < CallAfter) return;
            called = true;
            CallGang(g, at);
        }

        /// <summary>The boss car's call: returns how many columns answer.</summary>
        public int CallGang(WastelandGame g, Vector3 at)
        {
            int n = 0;
            var dir = NpcDirector.Instance;
            if (dir != null)
                foreach (var c in dir.Convoys)
                {
                    if (c == null || c == this || !c.raiders || c.Friendly || c.phase != Phase.Travel) continue;
                    float reach = c.Gang == Gang ? GangReach : OtherReach;
                    if (Flat(c.Position - at).magnitude > reach) continue;
                    c.Answer(at);
                    n++;
                }
            LastCallAnswered = n;
            if (g) g.Toast(n > 0 ? "THE " + Gang + "'S BOSS CAR IS ON THE RADIO: MORE OF THEM ARE COMING" : "THE " + Gang + "'S BOSS CAR CALLS FOR HELP - NOBODY ANSWERS");
            if (n > 0) MadMax.Game.Journal.Add("RAID", "THE " + Gang + " CALLED THEIR GANG IN ON THE CHASE");
            return n;
        }

        public void Answer(Vector3 at) { callAt = at; callUntil = Time.time + CallSeconds; }

        /// <summary>Folded-away travel while answering: along the road toward the call, faster than cruising.</summary>
        bool AnswerTravel(float dt)
        {
            if (!Answering || route == null || route.Count < 2) return false;
            var here = PointAt(travel, out _);
            float s = Project(callAt, callAt - here);
            float loop = Mathf.Repeat(travel, routeLen * 2f), alt = routeLen * 2f - s;
            float goal = Mathf.Abs(alt - loop) < Mathf.Abs(s - loop) ? alt : s;
            travel = Mathf.MoveTowards(loop, goal, speed * CallHurry * dt);
            return true;
        }

        void ResetChase() { chaseT = 0f; called = false; }
    }
}
