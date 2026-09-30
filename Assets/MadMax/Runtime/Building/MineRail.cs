using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A 2 m length of mine track (depth stage H). Lengths whose ends meet (within <see cref="JoinGap"/>) make a
    /// line that an <see cref="OreCart"/> runs along; the track follows the ground it is laid on (slopes, bends at the
    /// joints).</summary>
    public class MineRail : MonoBehaviour
    {
        public static readonly List<MineRail> All = new List<MineRail>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Half the length along local Z, and the height of the rail heads above the piece's origin (m).</summary>
        public const float HalfLength = 1f, Top = 0.2f, JoinGap = 0.45f;

        public Vector3 A => transform.TransformPoint(0f, Top, -HalfLength);
        public Vector3 B => transform.TransformPoint(0f, Top, HalfLength);

        /// <summary>The rail whose centre line passes within <paramref name="max"/> m of <paramref name="p"/>
        /// (<paramref name="t"/> = 0 at A .. 1 at B).</summary>
        public static MineRail Nearest(Vector3 p, float max, out float t)
        {
            MineRail best = null; t = 0f;
            float bd = max * max;
            foreach (var r in All)
            {
                if (!r) continue;
                var a = r.A; var ab = r.B - a;
                float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = (a + ab * u - p).sqrMagnitude;
                if (d < bd) { bd = d; best = r; t = u; }
            }
            return best;
        }

        /// <summary>Another rail (not in <paramref name="skip"/>) with an end at <paramref name="end"/>; its far end is returned.</summary>
        public static MineRail Joined(Vector3 end, HashSet<MineRail> skip, out Vector3 far)
        {
            far = end;
            MineRail best = null; float bd = JoinGap * JoinGap;
            foreach (var r in All)
            {
                if (!r || skip.Contains(r)) continue;
                float da = (r.A - end).sqrMagnitude, db = (r.B - end).sqrMagnitude;
                if (da < bd) { bd = da; best = r; far = r.B; }
                if (db < bd) { bd = db; best = r; far = r.A; }
            }
            return best;
        }
    }
}
