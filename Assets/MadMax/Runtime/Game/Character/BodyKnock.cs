using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A limp body (<see cref="Ragdoll"/>) against vehicles. A standing person hit by a car is thrown along the
    /// car's path (a little faster than the impact speed, up over the bonnet, off to the side they were struck on); a
    /// body lying in the road is shoved and run over. Either way the bones stop colliding with that vehicle until they
    /// are clear of it, so they never ride on the bumper or wedge into the compound and get dragged along. Bodies that
    /// froze after settling wake up when a vehicle comes at them.</summary>
    public class BodyKnock : MonoBehaviour
    {
        Ragdoll rd;
        HumanRig rig;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<Collider> cols = new List<Collider>();

        class Ignored { public VehicleDriver v; public Collider[] cols; public float minUntil; }
        readonly List<Ignored> ignored = new List<Ignored>();

        /// <summary>When a vehicle last struck this body (Time.time).</summary>
        public float KnockedAt { get; private set; } = -99f;
        /// <summary>The vehicle that last struck it.</summary>
        public VehicleDriver KnockedBy { get; private set; }

        void Awake() { rd = GetComponent<Ragdoll>(); rig = GetComponent<HumanRig>(); }

        bool Collect()
        {
            bool stale = bodies.Count == 0;
            foreach (var b in bodies) if (!b) { stale = true; break; }
            if (!stale) return true;
            bodies.Clear(); cols.Clear(); ignored.Clear();
            if (!rig) return false;
            foreach (var kv in rig.bones)
            {
                if (!kv.Value) continue;
                if (kv.Value.TryGetComponent<Rigidbody>(out var rb)) bodies.Add(rb);
                foreach (var c in kv.Value.GetComponents<Collider>()) if (c && !c.isTrigger) cols.Add(c);
            }
            return bodies.Count > 0;
        }

        /// <summary>The vehicle <paramref name="v"/> strikes the body at <paramref name="point"/>. <paramref name="lying"/>:
        /// a body already down (shoved and run over) rather than a standing person (thrown).</summary>
        public void Knock(VehicleDriver v, Vector3 point, bool lying)
        {
            if (!v || !v.Body || !rd || !rd.Active || !Collect()) return;
            var vb = v.Body;
            var vc = vb.GetPointVelocity(point);
            vc.y = Mathf.Max(0f, vc.y);
            float sp = vc.magnitude;
            Ignore(v);
            KnockedAt = Time.time; KnockedBy = v;
            rd.Wake();
            if (sp < 0.5f) return;
            var fwd = vc / sp;
            var side = Vector3.Cross(Vector3.up, fwd);
            if (side.sqrMagnitude < 0.01f) side = v.transform.right; else side.Normalize();
            var centre = Vector3.zero;
            foreach (var b in bodies) centre += b.worldCenterOfMass;
            centre /= bodies.Count;
            float off = Vector3.Dot(centre - vb.worldCenterOfMass, side);
            float sign = Mathf.Abs(off) > 0.12f ? Mathf.Sign(off) : (Vector3.Dot(centre - vb.worldCenterOfMass, v.transform.right) >= 0f ? 1f : -1f);
            float glance = Mathf.Clamp01(Mathf.Abs(off) / 1.2f);                                   // struck by a corner: deflected more
            Vector3 vel; Vector3 spin;
            if (lying)
            {
                vel = fwd * sp * 0.3f + side * sign * sp * 0.15f + Vector3.up * Mathf.Min(2f, 0.6f + sp * 0.08f);
                spin = fwd * sign * Mathf.Min(sp, 10f) * 0.4f;
            }
            else
            {
                // pedestrian impact: the body takes about the car's speed, wraps up over the bonnet and is thrown ahead
                vel = fwd * sp * 1.05f + side * sign * sp * (0.22f + 0.4f * glance) + Vector3.up * Mathf.Clamp(sp * 0.3f, 1.2f, 5f);
                spin = side * Mathf.Min(sp, 14f) * 0.7f + fwd * sign * Mathf.Min(sp, 14f) * 0.3f;
            }
            foreach (var b in bodies)
            {
                if (b.isKinematic) b.isKinematic = false;
                b.linearVelocity = vel;
                b.angularVelocity = spin;
            }
        }

        void Ignore(VehicleDriver v)
        {
            foreach (var e in ignored) if (e.v == v) { e.minUntil = Time.time + 0.5f; return; }
            var vcols = v.GetComponentsInChildren<Collider>();
            foreach (var a in cols) foreach (var b in vcols) if (a && b && !b.isTrigger) Physics.IgnoreCollision(a, b, true);
            ignored.Add(new Ignored { v = v, cols = vcols, minUntil = Time.time + 0.5f });
        }

        bool Overlaps(Collider[] vcols)
        {
            bool any = false; var box = new Bounds();
            foreach (var c in vcols)
            {
                if (!c || !c.enabled || c.isTrigger) continue;
                if (!any) { box = c.bounds; any = true; } else box.Encapsulate(c.bounds);
            }
            if (!any) return false;
            box.Expand(0.5f);
            foreach (var c in cols) if (c && box.Intersects(c.bounds)) return true;
            return false;
        }

        bool IsIgnored(VehicleDriver v) { foreach (var e in ignored) if (e.v == v) return true; return false; }

        float scanT;

        /// <summary>Is something <paramref name="gap"/> (from the vehicle's bounds to it) about to be struck by a vehicle
        /// moving at <paramref name="vel"/>: touching (within <paramref name="contact"/>), or straight ahead within
        /// <paramref name="lookahead"/> m (not merely passed close beside).</summary>
        public static bool InPath(Vector3 gap, Vector3 vel, float contact, float lookahead)
        {
            if (gap.sqrMagnitude <= contact * contact) return true;
            float sp = vel.magnitude;
            if (sp < 0.01f) return false;
            var dir = vel / sp;
            float along = Vector3.Dot(gap, dir);
            return along > 0f && along <= contact + lookahead && (gap - dir * along).sqrMagnitude <= contact * contact;
        }

        void FixedUpdate()
        {
            if (!rd || !rd.Active) { bodies.Clear(); cols.Clear(); ignored.Clear(); return; }
            if (!Collect()) return;
            // contacts with a vehicle come back once the body is clear of it
            for (int i = ignored.Count - 1; i >= 0; i--)
            {
                var e = ignored[i];
                if (!e.v) { ignored.RemoveAt(i); continue; }
                if (Time.time < e.minUntil || Overlaps(e.cols)) continue;
                foreach (var a in cols) foreach (var b in e.cols) if (a && b && !b.isTrigger) Physics.IgnoreCollision(a, b, false);
                ignored.RemoveAt(i);
            }
            bool frozen = rd.Frozen;
            if (frozen && (scanT -= Time.fixedDeltaTime) > 0f) return;
            scanT = 0.1f;
            var g = WastelandGame.Instance;
            if (!g) return;
            var pelvis = bodies[0].worldCenterOfMass;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic || IsIgnored(v)) continue;
                if ((v.transform.position - pelvis).sqrMagnitude > 14f * 14f) continue;
                var vel = v.Body.linearVelocity - (frozen ? Vector3.zero : bodies[0].linearVelocity);   // closing speed (an ejected rider flies with the car)
                float sp = vel.magnitude;
                if (sp < 2.5f) continue;
                float ahead = sp * (frozen ? 0.13f : Time.fixedDeltaTime * 2f);
                foreach (var b in bodies)
                {
                    var c = b.worldCenterOfMass;
                    var cp = VehicleStorage.Closest(v, c);
                    if (!InPath(c - cp, vel, 0.3f, ahead)) continue;
                    Knock(v, cp, true);
                    return;
                }
            }
        }
    }
}
