using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>An animal (alive or a carcass) struck by a vehicle: its kinematic capsule is thrown along the vehicle's
    /// path by momentum (light animals fly, a bull barely moves), tumbles to rest on the ground, and ignores that
    /// vehicle's colliders until it is clear of it, so it is never pushed along on the bumper. A carcass lying in the
    /// road is shoved and run over instead of stopping the car like a wall.</summary>
    public class AnimalKnock : MonoBehaviour
    {
        Animal animal;
        Collider col;
        Vector3 vel;
        float scanT, flyUntil;

        class Ignored { public VehicleDriver v; public Collider[] cols; public float minUntil; }
        readonly List<Ignored> ignored = new List<Ignored>();

        /// <summary>Thrown and still moving (the animal's own movement waits).</summary>
        public bool Flying { get; private set; }
        public float KnockedAt { get; private set; } = -99f;

        public static AnimalKnock For(Animal a)
        {
            if (!a) return null;
            if (!a.TryGetComponent<AnimalKnock>(out var k)) { k = a.gameObject.AddComponent<AnimalKnock>(); k.animal = a; k.col = a.GetComponent<Collider>(); }
            return k;
        }

        /// <summary>Struck at <paramref name="point"/> by <paramref name="v"/>; <paramref name="lying"/> = a carcass on the ground.</summary>
        public void Hit(VehicleDriver v, Vector3 point, bool lying)
        {
            if (!v || !v.Body) return;
            Ignore(v);
            KnockedAt = Time.time;
            var vc = v.Body.GetPointVelocity(point); vc.y = 0f;
            float sp = vc.magnitude;
            if (sp < 0.5f) return;
            float m = animal && animal.Def != null ? Mathf.Max(1f, animal.Def.mass * animal.Size) : 40f;
            float share = v.Body.mass / (v.Body.mass + m);                                          // momentum: a rabbit takes the car's speed, a bull a fraction
            var fwd = vc / sp;
            var side = Vector3.Cross(Vector3.up, fwd).normalized;
            float off = Vector3.Dot(transform.position - v.Body.worldCenterOfMass, side);
            float sign = Mathf.Abs(off) > 0.1f ? Mathf.Sign(off) : (Vector3.Dot(transform.position - v.Body.worldCenterOfMass, v.transform.right) >= 0f ? 1f : -1f);
            vel = lying ? fwd * sp * 0.3f * share + side * sign * sp * 0.12f * share + Vector3.up * Mathf.Min(1.5f, 0.4f + sp * 0.05f) * share
                        : fwd * sp * 1.05f * share + side * sign * sp * 0.3f * share + Vector3.up * Mathf.Clamp(sp * 0.25f * share, 0.5f, 4f);
            Flying = true;
            flyUntil = Time.time + 6f;
        }

        void Ignore(VehicleDriver v)
        {
            foreach (var e in ignored) if (e.v == v) { e.minUntil = Time.time + 0.5f; return; }
            var vcols = v.GetComponentsInChildren<Collider>();
            if (col) foreach (var b in vcols) if (b && !b.isTrigger) Physics.IgnoreCollision(col, b, true);
            ignored.Add(new Ignored { v = v, cols = vcols, minUntil = Time.time + 0.5f });
        }

        public bool IsIgnoring(VehicleDriver v) { foreach (var e in ignored) if (e.v == v) return true; return false; }

        bool Overlaps(Collider[] vcols)
        {
            if (!col) return false;
            var mine = col.bounds;
            mine.Expand(0.5f);
            foreach (var c in vcols) if (c && c.enabled && !c.isTrigger && c.bounds.Intersects(mine)) return true;
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = ignored.Count - 1; i >= 0; i--)
            {
                var e = ignored[i];
                if (!e.v) { ignored.RemoveAt(i); continue; }
                if (Time.time < e.minUntil || Overlaps(e.cols)) continue;
                if (col) foreach (var b in e.cols) if (b && !b.isTrigger) Physics.IgnoreCollision(col, b, false);
                ignored.RemoveAt(i);
            }
            if (Flying)
            {
                var p = transform.position + vel * dt;
                vel += Physics.gravity * dt;
                var t = DeformableTerrain.Instance;
                float ground = t ? t.HeightNoLoad(p.x, p.z) : p.y;
                if (p.y <= ground)
                {
                    p.y = ground;
                    if (vel.y < -2.5f) vel.y *= -0.25f; else vel.y = 0f;                            // a bounce, then sliding
                    var flat = new Vector3(vel.x, 0f, vel.z);
                    flat *= Mathf.Exp(-5f * dt);
                    vel = new Vector3(flat.x, vel.y, flat.z);
                    if (flat.sqrMagnitude < 0.09f && Mathf.Abs(vel.y) < 0.3f) Flying = false;
                }
                transform.position = p;
                if (Time.time > flyUntil) Flying = false;
            }
            // a carcass in the road: shoved and run over, never a wall
            if (animal && animal.Alive) return;
            if ((scanT -= dt) > 0f) return;
            scanT = 0.05f;
            var g = WastelandGame.Instance;
            if (!g || !col) return;
            var c = col.bounds.center;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic || IsIgnoring(v)) continue;
                if ((v.transform.position - c).sqrMagnitude > 14f * 14f) continue;
                var vv = v.Body.linearVelocity;
                float sp = vv.magnitude;
                if (sp < 2.5f) continue;
                var cp = v.Body.ClosestPointOnBounds(c);
                var d = c - cp;
                float reach = col.bounds.extents.magnitude * 0.6f + 0.2f + sp * 0.08f;
                if (d.sqrMagnitude > reach * reach || Vector3.Dot(d, vv) < -0.05f * sp) continue;
                Hit(v, cp, true);
                return;
            }
        }
    }
}
