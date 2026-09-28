using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Marks a radiation / heat source that hurts players nearby.</summary>
    public class Hazard : MonoBehaviour
    {
        public static readonly List<Hazard> All = new List<Hazard>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => All.Clear();
        public float radiation, radius = 4f;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
