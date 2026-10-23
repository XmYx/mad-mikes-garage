using MadMax.Building;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A call on the player (2026-10-18): someone sent to <see cref="Visit"/> walks up to the spot (the player's
    /// fire or porch), takes a free seat within <see cref="VisitSeatReach"/> m of it (else stands beside it), sits a
    /// while facing it, then <see cref="onLeave"/> runs and they head home (leaving: folded away out of sight).</summary>
    public partial class Npc
    {
        public const float VisitSeatReach = 6f;
        Vector3 visitAt;
        float visitUntil = -1f;
        Seat visitSeat;
        int visitSpot = -1;
        Vector3 visitStand;
        bool visitArrived;
        System.Action<Npc> onArrive, onLeave;

        /// <summary>On a visit right now (tests).</summary>
        public bool Visiting => visitUntil > 0f;
        /// <summary>Arrived and sitting (or standing) at the visit spot (tests).</summary>
        public bool VisitSettled => Visiting && visitArrived;

        /// <summary>Walk over to <paramref name="at"/> and stay <paramref name="seconds"/> once there.</summary>
        public void Visit(Vector3 at, float seconds, System.Action<Npc> arrived, System.Action<Npc> leave)
        {
            EndTv();
            visitAt = at; visitUntil = seconds; visitArrived = false;
            onArrive = arrived; onLeave = leave;
            visitSeat = null; visitSpot = -1;
            float best = float.MaxValue;
            foreach (var s in Object.FindObjectsByType<Seat>(FindObjectsSortMode.None))
            {
                if (!s || s.standing || s.hidden || s.ride != null || s.spots == null) continue;
                for (int i = 0; i < s.spots.Length; i++)
                {
                    float d = Flat(s.transform.TransformPoint(s.spots[i]) - at).magnitude;
                    if (d < VisitSeatReach && d < best && !tvClaimed.Contains((s, i))) { best = d; visitSeat = s; visitSpot = i; }
                }
            }
            if (visitSeat) tvClaimed.Add((visitSeat, visitSpot));
            var away = Flat(transform.position - at);
            visitStand = at + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 1.4f;
            home = at; homeRadius = 2f; leaving = false;
        }

        /// <summary>From the think loop: the walk over, the sit, and the leave; false when not visiting.</summary>
        bool VisitTick(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            if (visitUntil <= 0f) return false;
            if (!visitArrived)
            {
                Vector3 target = visitSeat ? visitSeat.transform.TransformPoint(visitSeat.spots[visitSpot]) + Flat(visitSeat.transform.forward).normalized * 0.5f : visitStand;
                float off = Flat(target - me).magnitude;
                if (off > 0.5f) { move = Toward(target); speed = off > 8f ? 1.6f : 1.25f; return true; }
                visitArrived = true;
                visitUntil = Time.time + visitUntil;
                if (visitSeat) { seated = true; cc.enabled = false; }
                onArrive?.Invoke(this);
            }
            if (Time.time >= visitUntil) { EndVisit(true); return false; }
            if (!visitSeat) { faceTarget = visitAt; faceUntil = Time.time + 0.5f; }
            return true;
        }

        /// <summary>Hips on the visit seat, facing the way it faces.</summary>
        void SitOnVisit()
        {
            if (!visitSeat || visitSpot < 0) { EndVisit(false); return; }
            var hip = visitSeat.transform.TransformPoint(visitSeat.spots[visitSpot]);
            var face = Flat(visitSeat.transform.forward);
            transform.SetPositionAndRotation(hip - Vector3.up * (0.94f * Profile.look.height), Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : transform.forward));
        }

        /// <summary>The visit is over: up, the farewell (<paramref name="done"/>), and off home.</summary>
        public void EndVisit(bool done)
        {
            if (visitUntil <= 0f) return;
            visitUntil = -1f;
            if (visitSeat)
            {
                tvClaimed.Remove((visitSeat, visitSpot));
                if (seated)
                {
                    seated = false;
                    var p = visitSeat.transform.TransformPoint(visitSeat.spots[visitSpot]) + Flat(visitSeat.transform.forward).normalized * 0.45f;
                    var t = MadMax.World.DeformableTerrain.Instance;
                    float floor = visitSeat.transform.position.y;
                    if (t) floor = Mathf.Max(floor, t.HeightNoLoad(p.x, p.z));
                    p.y = floor + 0.05f;
                    transform.position = p;
                    cc.enabled = !asleep;
                }
            }
            visitSeat = null; visitSpot = -1;
            var leave = onLeave; onArrive = null; onLeave = null;
            if (done) leave?.Invoke(this);
            leaving = true;
        }
    }
}
