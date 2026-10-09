using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Climbable: a player touching it and pushing forward climbs up (backwards climbs down).</summary>
    public class Ladder : MonoBehaviour
    {
        public static readonly List<Ladder> All = new List<Ladder>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public Bounds localBounds;
        /// <summary>The ladder runs along local +Z (a frame ladder placed from its foot to its top), else local +Y.</summary>
        public bool alongZ;

        /// <summary>World direction up the ladder.</summary>
        public Vector3 Axis => alongZ ? transform.forward : transform.up;
        /// <summary>World points of its foot and its top (centre line).</summary>
        public Vector3 Foot => transform.TransformPoint(alongZ ? new Vector3(localBounds.center.x, localBounds.center.y, localBounds.min.z) : new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z));
        public Vector3 Top => transform.TransformPoint(alongZ ? new Vector3(localBounds.center.x, localBounds.center.y, localBounds.max.z) : new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z));

        public static Ladder At(Vector3 p, float margin = 0.45f)
        {
            foreach (var l in All)
            {
                if (!l) continue;
                var lp = l.transform.InverseTransformPoint(p);
                var b = l.localBounds; b.Expand(l.alongZ ? new Vector3(margin * 2f, margin * 2f + 0.5f, 0.2f) : new Vector3(margin * 2f, 0.2f, margin * 2f));   // around the rails, not past the ends
                if (b.Contains(lp)) return l;
            }
            return null;
        }

        public float TopY => Mathf.Max(Top.y, Foot.y);
    }
}
