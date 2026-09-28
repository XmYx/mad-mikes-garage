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

        public static Ladder At(Vector3 p, float margin = 0.45f)
        {
            foreach (var l in All)
            {
                if (!l) continue;
                var lp = l.transform.InverseTransformPoint(p);
                var b = l.localBounds; b.Expand(new Vector3(margin * 2f, 0.2f, margin * 2f));
                if (b.Contains(lp)) return l;
            }
            return null;
        }

        public float TopY => transform.TransformPoint(new Vector3(0, localBounds.max.y, 0)).y;
    }
}
