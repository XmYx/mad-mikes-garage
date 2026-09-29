using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A dry, breathable volume under water (user additions): the inside of a sea dome, a tunnel or the shore
    /// entrance. The player inside one walks instead of swimming and breathes normally. Boxes are in the piece's local
    /// space so they follow it however it was placed.</summary>
    public class AirPocket : MonoBehaviour
    {
        public static readonly List<AirPocket> All = new List<AirPocket>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public Bounds[] boxes = new Bounds[0];

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Is this world point inside any dry volume?</summary>
        public static bool Contains(Vector3 p)
        {
            foreach (var a in All)
            {
                if (!a) continue;
                var l = a.transform.InverseTransformPoint(p);
                foreach (var b in a.boxes) if (b.Contains(l)) return true;
            }
            return false;
        }
    }
}
