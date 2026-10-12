using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A generated city people live in (a rolling city, a megacity): its plan (blocks, flats, shops), how plan
    /// points map to the world, where open spots and the housing clerk are. Quarters, door signs, build permission
    /// and <c>CityFolk</c> work on any host; <see cref="Hosts"/> lists them in creation order (saved leases refer to
    /// the index).</summary>
    public abstract class CityHost : MonoBehaviour
    {
        public static readonly List<CityHost> Hosts = new List<CityHost>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetHosts() => Hosts.Clear();

        public abstract string CityName { get; }
        public abstract TrackedCity.Plan CityPlan { get; }
        public abstract bool IsReady { get; }
        /// <summary>Is the world point within the city's volume (where its building rules apply)?</summary>
        public abstract bool InCity(Vector3 p);
        /// <summary>Generator space → world.</summary>
        public abstract Vector3 PlanPoint(Vector3 gen);
        /// <summary>World → generator space.</summary>
        public abstract Vector3 ToPlan(Vector3 world);
        /// <summary>Open spots where residents linger (generator space).</summary>
        public abstract List<Vector3> Spots();
        /// <summary>The housing clerk's post and facing (generator space).</summary>
        public abstract Vector3 ClerkSpot(out Vector3 facing);

        /// <summary>Extra reach of the city beyond its centre (CityFolk wakes people up within this plus its own radius).</summary>
        public virtual float FolkRadius => 0f;
        /// <summary>CityFolk spawns a person within this of the player (folds them 60 m further out).</summary>
        public virtual float PersonReach => 140f;

        /// <summary>The flat (or shop) at a world point, or null.</summary>
        public TrackedCity.Room RoomAt(Vector3 world)
        {
            var plan = CityPlan;
            if (plan == null) return null;
            var b = ToPlan(world);
            foreach (var r in plan.rooms)
                if (b.x >= r.x0 && b.x <= r.x1 && b.y >= r.y0 && b.y <= r.y1 && b.z >= r.z0 - 0.3f && b.z <= r.z1) return r;
            return null;
        }

        protected virtual void OnEnable() { if (!Hosts.Contains(this)) Hosts.Add(this); }
        protected virtual void OnDestroy() => Hosts.Remove(this);
    }
}
