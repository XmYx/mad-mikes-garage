using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Scarecrow: keeps crows off the garden plots within 12 m.</summary>
    public class Scarecrow : MonoBehaviour
    {
        public const float Reach = 12f;
        static readonly List<Scarecrow> all = new List<Scarecrow>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => all.Clear();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static bool Near(Vector3 p)
        {
            foreach (var s in all) if (s && (s.transform.position - p).sqrMagnitude < Reach * Reach) return true;
            return false;
        }
    }
}
