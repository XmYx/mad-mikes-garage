using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Watching TV (2026-10-16): someone idling (standing about or wandering, not a keeper at their counter,
    /// not hostile, not on a convoy) within <see cref="WatchReach"/> m of a playing set goes over and watches — on a
    /// free seat in front of it (a chair, sofa or bench within <see cref="SeatReach"/> m, facing the screen) or standing
    /// a couple of metres off, turned to the picture — until the tape stops or something else comes up.</summary>
    public partial class Npc
    {
        public const float WatchReach = 12f, SeatReach = 4.5f;
        TvSet tvSet;
        Seat tvSeat;
        int tvSpot = -1;
        Vector3 tvStand;
        float tvLookT, tvSitBy;
        static readonly HashSet<(Seat, int)> tvClaimed = new HashSet<(Seat, int)>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetTv() => tvClaimed.Clear();

        /// <summary>The set this person is watching (null: none).</summary>
        public TvSet Watching => tvSet && tvSet.Playing != null ? tvSet : null;
        /// <summary>Watching from a seat (tests).</summary>
        public bool WatchingSeated => Watching && seated && tvSeat;

        bool MayWatch =>
            !Hostile && !Profile.Raider && convoy == null && !raiding && !leaving && aboard == null && !asleep && !Riding && !Down
            && Profile.role != NpcRole.Shopkeeper && Profile.role != NpcRole.Stallkeeper && Profile.role != NpcRole.Leader && Profile.role != NpcRole.Packer
            && !(companion && order == 0);

        /// <summary>Walk to a spot in front of a playing TV and watch; false when there is nothing to watch.</summary>
        bool WatchTv(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            if (!MayWatch) { EndTv(); return false; }
            if ((tvLookT -= dt) <= 0f)
            {
                tvLookT = 2f + Random.value;
                var near = TvSet.Nearest(me, WatchReach);
                if (near && near.Playing != null && Mathf.Abs(near.transform.position.y - me.y) < 2.2f)
                {
                    if (near != tvSet) { EndTv(); tvSet = near; PickTvSpot(); }
                }
                else if (tvSet) EndTv();
            }
            if (!tvSet) return false;
            if (tvSet.Playing == null) { EndTv(); return false; }
            var screen = tvSet.transform.position + Vector3.up * 0.5f;
            if (seated) { faceTarget = screen; faceUntil = Time.time + 0.5f; return true; }
            // a seat: walk to just in front of it (its own collider keeps the body off the hip spot), then sit down
            Vector3 target = tvSeat ? tvSeat.transform.TransformPoint(tvSeat.spots[tvSpot]) + Flat(tvSeat.transform.forward).normalized * 0.5f : tvStand;
            float off = Flat(target - me).magnitude;
            if (tvSeat && off < 1.1f && (off < 0.45f || Time.time > tvSitBy)) { seated = true; cc.enabled = false; return true; }
            if (off > 0.45f) { move = Toward(target); speed = 1.3f; if (off > 1.1f) tvSitBy = Time.time + 1.5f; return true; }
            faceTarget = screen; faceUntil = Time.time + 0.5f;
            return true;
        }

        /// <summary>A free seat facing the screen within reach, else a standing spot ~2 m in front (spread by person).</summary>
        void PickTvSpot()
        {
            tvSeat = null; tvSpot = -1;
            var tp = tvSet.transform.position;
            var fwd = Flat(tvSet.transform.forward).normalized;
            var g = MadMax.Game.WastelandGame.Instance;
            var playerAt = g && g.Player ? g.Player.transform.position : Vector3.one * 1e6f;
            float best = float.MaxValue;
            foreach (var s in Object.FindObjectsByType<Seat>(FindObjectsSortMode.None))
            {
                if (!s || s.standing || s.hidden || s.ride != null || s.spots == null) continue;
                for (int i = 0; i < s.spots.Length; i++)
                {
                    var hip = s.transform.TransformPoint(s.spots[i]);
                    var off = Flat(hip - tp);
                    float d = off.magnitude;
                    if (d > SeatReach || d < 0.8f || Vector3.Dot(off / d, fwd) < 0.5f) continue;                 // in front of the screen
                    if (Vector3.Dot(Flat(s.transform.forward).normalized, -off / d) < 0.3f) continue;          // and turned toward it
                    if (tvClaimed.Contains((s, i)) || Flat(hip - playerAt).sqrMagnitude < 0.5f * 0.5f) continue;
                    if (d < best) { best = d; tvSeat = s; tvSpot = i; }
                }
            }
            if (tvSeat) { tvClaimed.Add((tvSeat, tvSpot)); return; }
            int h = (Profile.id.GetHashCode() & 0x7fffffff) % 5;
            var right = Vector3.Cross(Vector3.up, fwd);
            tvStand = tp + fwd * (1.9f + 0.25f * (h % 2)) + right * ((h - 2) * 0.55f);
        }

        /// <summary>On the chosen seat: hips on its spot, facing the way the seat faces.</summary>
        void SitOnTv()
        {
            if (!tvSeat || tvSpot < 0) { EndTv(); return; }
            var hip = tvSeat.transform.TransformPoint(tvSeat.spots[tvSpot]);
            var face = Flat(tvSeat.transform.forward);
            transform.SetPositionAndRotation(hip - Vector3.up * (0.94f * Profile.look.height), Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : transform.forward));
        }

        /// <summary>Stop watching: up off the seat and free it.</summary>
        void EndTv()
        {
            if (!tvSet && !tvSeat) return;
            if (tvSeat)
            {
                tvClaimed.Remove((tvSeat, tvSpot));
                if (seated)
                {
                    seated = false;
                    var p = tvSeat.transform.TransformPoint(tvSeat.spots[tvSpot]) + Flat(tvSeat.transform.forward).normalized * 0.45f;
                    var t = MadMax.World.DeformableTerrain.Instance;
                    float floor = tvSeat.transform.position.y;
                    if (t) floor = Mathf.Max(floor, t.HeightNoLoad(p.x, p.z));
                    p.y = floor + 0.05f;
                    transform.position = p;
                    cc.enabled = !asleep;
                }
            }
            tvSet = null; tvSeat = null; tvSpot = -1;
        }
    }
}
