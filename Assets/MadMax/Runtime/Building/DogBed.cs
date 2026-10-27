using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A dog blanket (2026-10-26): where the yard dog naps in the afternoon and, laid by a stable mouth, where
    /// it keeps the door at night (<c>Animal.Home</c>, <c>Animal.Roost</c>).</summary>
    public class DogBed : MonoBehaviour
    {
        public static readonly List<DogBed> All = new List<DogBed>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>A blanket within this of a stable's door spot is where the door dog lies; within NapReach of its home it naps there.</summary>
        public const float DoorReach = 6f, NapReach = 24f;

        /// <summary>The middle of the blanket (world).</summary>
        public Vector3 LieSpot => transform.TransformPoint(new Vector3(0f, 0.06f, 0f));

        public static DogBed Near(Vector3 p, float radius)
        {
            DogBed best = null; float bd = radius * radius;
            foreach (var b in All)
            {
                if (!b) continue;
                float d = (b.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }
    }
}
