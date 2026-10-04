using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Physics ragdoll for a <see cref="HumanRig"/>: every bone gets a rigidbody (mass by body part), a
    /// collider fitted to its voxel shape and a CharacterJoint to its parent with anatomical-ish limits (knees and
    /// elbows bend one way, neck and spine swing a little). <see cref="Go"/> goes limp and takes the killing blow's
    /// impulse; bodies freeze once settled to save physics. <see cref="Restore"/> removes the physics again (respawn).</summary>
    public class Ragdoll : MonoBehaviour
    {
        HumanRig rig;
        readonly List<Component> added = new List<Component>();
        readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> rest = new Dictionary<Transform, (Vector3, Quaternion)>();
        public bool Active { get; private set; }
        float settleAt;

        struct Spec { public float mass, radius; public Vector3 center, size; public int shape; public float twistLo, twistHi, swing; }   // shape 0 box, 1 capsule, 2 sphere

        static Spec For(BodyPart p, Appearance a)
        {
            float h = a.height, w = a.build;
            switch (p)
            {
                case BodyPart.Pelvis: return new Spec { mass = 12f, shape = 0, center = new Vector3(0, 0.02f, 0), size = new Vector3(0.3f * w, 0.2f, 0.2f) };
                case BodyPart.Chest: return new Spec { mass = 18f, shape = 0, center = new Vector3(0, 0.22f * h, 0), size = new Vector3(0.34f * w, 0.44f * h, 0.21f), twistLo = -15f, twistHi = 15f, swing = 20f };
                case BodyPart.Head: return new Spec { mass = 5f, shape = 2, center = new Vector3(0, 0.18f * h, 0.01f), radius = 0.105f, twistLo = -40f, twistHi = 40f, swing = 35f };
                case BodyPart.UpperArmL: case BodyPart.UpperArmR: return Limb(2.5f, 0.29f * h, 0.045f * w, -60f, 60f, 80f);
                case BodyPart.ForearmL: case BodyPart.ForearmR: return Limb(1.6f, 0.25f * h, 0.035f * w, -5f, 140f, 10f);
                case BodyPart.HandL: case BodyPart.HandR: return new Spec { mass = 0.5f, shape = 0, center = new Vector3(0, -0.045f * h, 0), size = new Vector3(0.05f, 0.09f * h, 0.04f), twistLo = -40f, twistHi = 40f, swing = 30f };
                case BodyPart.ThighL: case BodyPart.ThighR: return Limb(8f, 0.43f * h, 0.065f * w, -30f, 90f, 35f);
                case BodyPart.ShinL: case BodyPart.ShinR: return Limb(4f, 0.43f * h, 0.045f * w, -130f, 0f, 5f);
                default: return new Spec { mass = 1f, shape = 0, center = new Vector3(0, -0.035f * h, 0.07f), size = new Vector3(0.09f, 0.07f, 0.25f), twistLo = -30f, twistHi = 30f, swing = 20f };
            }
        }

        static Spec Limb(float mass, float len, float r, float lo, float hi, float swing) =>
            new Spec { mass = mass, shape = 1, center = new Vector3(0, -len * 0.5f, 0), size = new Vector3(0, len, 0), radius = r, twistLo = lo, twistHi = hi, swing = swing };

        public static Ragdoll For(HumanRig r) { var rd = r.GetComponent<Ragdoll>(); if (!rd) { rd = r.gameObject.AddComponent<Ragdoll>(); rd.rig = r; } return rd; }

        /// <summary>Go limp. <paramref name="impulse"/> (N·s) is applied at <paramref name="point"/> on the nearest bone.</summary>
        public void Go(Vector3 impulse, Vector3 point, Vector3 inheritVelocity = default)
        {
            if (Active || !rig) return;
            Active = true;
            added.Clear(); rest.Clear();
            var bodies = new Dictionary<BodyPart, Rigidbody>();
            foreach (var kv in rig.bones)
            {
                var t = kv.Value;
                if (!t || rig.BoneGone(kv.Key)) continue;                                        // a lost limb has no body
                rest[t] = (t.localPosition, t.localRotation);
                var sp = For(kv.Key, rig.appearance);
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = sp.mass; rb.linearDamping = 0.05f; rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.linearVelocity = inheritVelocity;
                added.Add(rb);
                Collider c;
                switch (sp.shape)
                {
                    case 1: { var cc = t.gameObject.AddComponent<CapsuleCollider>(); cc.direction = 1; cc.center = sp.center; cc.height = sp.size.y + sp.radius * 2f; cc.radius = sp.radius; c = cc; break; }
                    case 2: { var sc = t.gameObject.AddComponent<SphereCollider>(); sc.center = sp.center; sc.radius = sp.radius; c = sc; break; }
                    default: { var bc = t.gameObject.AddComponent<BoxCollider>(); bc.center = sp.center; bc.size = sp.size; c = bc; break; }
                }
                added.Add(c);
                bodies[kv.Key] = rb;
            }
            // joints to the parent bone; parts of one body never collide with each other
            foreach (var bone in HumanDesign.Skeleton(rig.appearance))
            {
                if (!bone.parent.HasValue || !bodies.TryGetValue(bone.part, out var rb) || !bodies.TryGetValue(bone.parent.Value, out var parent)) continue;
                var sp = For(bone.part, rig.appearance);
                var j = rb.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                j.axis = Vector3.right; j.swingAxis = Vector3.forward;
                j.lowTwistLimit = new SoftJointLimit { limit = sp.twistLo };
                j.highTwistLimit = new SoftJointLimit { limit = sp.twistHi };
                j.swing1Limit = new SoftJointLimit { limit = sp.swing };
                j.swing2Limit = new SoftJointLimit { limit = sp.swing * 0.6f };
                j.enableProjection = true;
                added.Insert(0, j);                                       // joints go first when removing
            }
            var cols = new List<Collider>();
            foreach (var a in added) if (a is Collider col) cols.Add(col);
            for (int i = 0; i < cols.Count; i++) for (int k = i + 1; k < cols.Count; k++) Physics.IgnoreCollision(cols[i], cols[k]);
            // the blow: on the bone nearest to the hit
            Rigidbody hit = null; float best = float.MaxValue;
            foreach (var rb in bodies.Values) { float d = (rb.worldCenterOfMass - point).sqrMagnitude; if (d < best) { best = d; hit = rb; } }
            if (hit) hit.AddForceAtPosition(impulse, point, ForceMode.Impulse);
            if (bodies.TryGetValue(BodyPart.Chest, out var chest)) chest.AddForce(impulse * 0.4f, ForceMode.Impulse);
            settleAt = Time.time + 10f;
            if (!GetComponent<BodyKnock>()) gameObject.AddComponent<BodyKnock>();   // vehicles knock the body away, never carry it
        }

        /// <summary>Back to live physics (a frozen body about to be hit again); it settles and freezes once more.</summary>
        public void Wake()
        {
            if (!Active) return;
            foreach (var a in added) if (a is Rigidbody rb && rb.isKinematic) rb.isKinematic = false;
            settleAt = Time.time + 3f;
        }

        /// <summary>Settled and frozen (kinematic bones).</summary>
        public bool Frozen => Active && settleAt == float.MaxValue;

        void FixedUpdate()
        {
            if (!Active || Time.time < settleAt) return;
            // settled: freeze the pose (no more physics cost), the body stays where it fell
            bool calm = true;
            foreach (var a in added) if (a is Rigidbody rb && !rb.isKinematic && rb.linearVelocity.sqrMagnitude > 0.01f) { calm = false; break; }
            if (!calm) { settleAt = Time.time + 2f; return; }
            foreach (var a in added) if (a is Rigidbody rb) rb.isKinematic = true;
            settleAt = float.MaxValue;
        }

        /// <summary>Back to an animated body (respawn): remove the physics and reset the bones.</summary>
        public void Restore()
        {
            if (!Active) return;
            foreach (var a in added) if (a is Joint) DestroyImmediate(a);           // joints before the bodies they depend on
            foreach (var a in added) if (a) DestroyImmediate(a);
            added.Clear();
            foreach (var kv in rest) if (kv.Key) { kv.Key.localPosition = kv.Value.pos; kv.Key.localRotation = kv.Value.rot; }
            Active = false;
        }

        /// <summary>World position of the pelvis (where the body lies).</summary>
        public Vector3 Pelvis => rig && rig.bones.TryGetValue(BodyPart.Pelvis, out var t) && t ? t.position : transform.position;
    }
}
