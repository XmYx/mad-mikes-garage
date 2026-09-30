using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Stable (depth stage E): an open-fronted shed with two stalls and a manger (the piece's
    /// <see cref="MadMax.Animals.Trough"/>). Animals standing inside get their stamina back fast, heal a limp twice as
    /// quickly and stay calm through gunfire and horns; a horse that has rested is rested for half a day (winds more
    /// slowly). See <see cref="MadMax.Animals.Animal.InStable"/>.</summary>
    public class Stable : MonoBehaviour
    {
        public static readonly List<Stable> All = new List<Stable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        /// <summary>The stalls (local box: centre and half size, m).</summary>
        public Vector3 centre = new Vector3(0f, 1.2f, 0f), half = new Vector3(2f, 1.5f, 1.55f);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Inside(Vector3 p)
        {
            var l = transform.InverseTransformPoint(p) - centre;
            return Mathf.Abs(l.x) < half.x && Mathf.Abs(l.y) < half.y && Mathf.Abs(l.z) < half.z;
        }

        /// <summary>Is <paramref name="p"/> inside any stable?</summary>
        public static bool Holds(Vector3 p)
        {
            foreach (var s in All) if (s && (s.transform.position - p).sqrMagnitude < 36f && s.Inside(p)) return true;
            return false;
        }
    }
}
