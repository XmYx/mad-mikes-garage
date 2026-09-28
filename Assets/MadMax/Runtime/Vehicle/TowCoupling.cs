using System.Collections;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Trailer side of a tow connection. Couples its "Coupler" point to any vehicle's "Hitch" with a
    /// ConfigurableJoint (free to yaw ±85°, pitch ±30°, roll ±20°). Trailer brakes follow the tow vehicle.
    /// Hitching is a short sequence so the joint never starts with an error: both bodies are frozen (kinematic),
    /// the trailer glides onto the hitch while its landing legs wind up, the joint is made, then physics comes back
    /// softly (high damping, slow depenetration, a soft joint that stiffens to locked). Unhitching lowers the legs
    /// before the joint lets go.</summary>
    [RequireComponent(typeof(VehicleDriver))]
    public class TowCoupling : MonoBehaviour
    {
        public VehicleDriver Tower { get; private set; }
        public Transform Coupler => transform.Find("Coupler");
        /// <summary>Hitch/unhitch sequence running (inputs are ignored meanwhile).</summary>
        public bool Busy { get; private set; }

        ConfigurableJoint joint;
        VehicleDriver self;
        Transform legs; Collider legCollider; Vector3 legsDown; float legT, legTop;
        const float RetractedScale = 0.06f;   // legs telescope up to 6 % of their length
        /// <summary>0 = legs down on the ground .. 1 = fully wound up.</summary>
        public float LegsUp => legT;

        const float GlideTime = 0.6f, SettleTime = 1.0f;
        float settleUntil;
        readonly System.Collections.Generic.List<(Collider, Collider)> ignored = new System.Collections.Generic.List<(Collider, Collider)>();

        /// <summary>Trailer and tow vehicle (including every mounted part: spare carriers, bumpers...) never collide
        /// while hitched; the joint alone connects them. Parts carry their own colliders, so the joint's
        /// enableCollision=false is not enough.</summary>
        void IgnoreTower(VehicleDriver tower, bool ignore)
        {
            if (ignore)
            {
                foreach (var a in GetComponentsInChildren<Collider>(true))
                    foreach (var b in tower.GetComponentsInChildren<Collider>(true))
                    { Physics.IgnoreCollision(a, b, true); ignored.Add((a, b)); }
            }
            else
            {
                foreach (var (a, b) in ignored) if (a && b) Physics.IgnoreCollision(a, b, false);
                ignored.Clear();
            }
        }

        void Awake()
        {
            self = GetComponent<VehicleDriver>();
            legs = transform.Find("Body/Legs");
            if (legs)
            {
                legsDown = legs.localPosition; legCollider = legs.GetComponent<Collider>();
                var mf = legs.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) legTop = mf.sharedMesh.bounds.max.y;   // legs telescope towards their top mount
            }
        }

        void SetLegs(float t)
        {
            legT = Mathf.Clamp01(t);
            if (!legs) return;
            // telescope: shrink along Y while keeping the top mount in place, so the foot rises fully under the body
            float sy = Mathf.Lerp(1f, RetractedScale, legT);
            legs.localScale = new Vector3(1f, sy, 1f);
            legs.localPosition = legsDown + Vector3.up * legTop * (1f - sy);
            if (legCollider) legCollider.enabled = legT < 0.05f;
        }

        public static Transform HitchOf(VehicleDriver v) => v ? v.transform.Find("Hitch") : null;

        /// <summary>Starts hitching to the tower. instant = restore from a save (no glide).</summary>
        public bool Couple(VehicleDriver tower, bool instant = false)
        {
            var hitch = HitchOf(tower);
            var coupler = Coupler;
            if (!hitch || !coupler || Tower || Busy) return false;
            Tower = tower;
            StopAllCoroutines();
            StartCoroutine(HitchSequence(tower, instant));
            return true;
        }

        IEnumerator HitchSequence(VehicleDriver tower, bool instant)
        {
            Busy = true;
            var rb = self.Body; var trb = tower.Body;
            bool towerWasKinematic = trb.isKinematic;
            Freeze(rb); Freeze(trb);
            IgnoreTower(tower, true);
            if (legCollider) legCollider.enabled = false;

            // target pose: yaw roughly with the tow vehicle, coupler exactly on the hitch
            var hitch = HitchOf(tower);
            var fwd = Vector3.ProjectOnPlane(tower.transform.position - transform.position, Vector3.up).normalized;
            var targetRot = fwd.sqrMagnitude > 0.1f && Vector3.Angle(fwd, transform.forward) > 60f ? Quaternion.LookRotation(fwd) : rb.rotation;
            var couplerLocal = transform.InverseTransformPoint(Coupler.position);
            Vector3 startPos = rb.position; Quaternion startRot = rb.rotation;
            float legStart = legT;
            for (float t = instant ? GlideTime : 0f; t < GlideTime; t += Time.fixedDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / GlideTime);
                var rot = Quaternion.Slerp(startRot, targetRot, k);
                var targetPos = hitch.position - rot * couplerLocal;
                rb.MovePosition(Vector3.Lerp(startPos, targetPos, k));
                rb.MoveRotation(rot);
                SetLegs(Mathf.Lerp(legStart, 1f, k));
                yield return new WaitForFixedUpdate();
            }
            rb.rotation = targetRot;
            rb.position = hitch.position - targetRot * couplerLocal;
            transform.SetPositionAndRotation(rb.position, rb.rotation);
            SetLegs(1f);

            // joint made while both are still frozen: zero initial error
            joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = trb;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = transform.InverseTransformPoint(Coupler.position);
            joint.connectedAnchor = tower.transform.InverseTransformPoint(hitch.position);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            joint.linearLimit = new SoftJointLimit { limit = 0.001f };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = rb.mass * 400f, damper = rb.mass * 12f };
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -30f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 30f };
            joint.angularYLimit = new SoftJointLimit { limit = 85f };
            joint.angularZLimit = new SoftJointLimit { limit = 20f };
            joint.enableCollision = false;

            // physics back, gently
            var rbState = Thaw(rb, false); var tState = Thaw(trb, towerWasKinematic);
            for (float t = 0f; t < SettleTime; t += Time.fixedDeltaTime)
            {
                float k = t / SettleTime;
                Ease(rb, rbState, k); Ease(trb, tState, k);
                if (joint) joint.linearLimitSpring = new SoftJointLimitSpring { spring = rb.mass * Mathf.Lerp(400f, 6000f, k), damper = rb.mass * 12f };
                yield return new WaitForFixedUpdate();
            }
            Ease(rb, rbState, 1f); Ease(trb, tState, 1f);
            if (joint)
            {
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
            }
            settleUntil = Time.time + 2f;
            Busy = false;
        }

        struct BodyState { public float linDamp, angDamp, depen; }

        static void Freeze(Rigidbody rb)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }

        static BodyState Thaw(Rigidbody rb, bool stayKinematic)
        {
            var s = new BodyState { linDamp = rb.linearDamping, angDamp = rb.angularDamping, depen = rb.maxDepenetrationVelocity };
            rb.isKinematic = stayKinematic;
            if (!stayKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.maxDepenetrationVelocity = 0.5f;
            rb.linearDamping = 3f; rb.angularDamping = 6f;
            return s;
        }

        static void Ease(Rigidbody rb, BodyState s, float k)
        {
            rb.linearDamping = Mathf.Lerp(3f, s.linDamp, k);
            rb.angularDamping = Mathf.Lerp(6f, s.angDamp, k);
            rb.maxDepenetrationVelocity = Mathf.Lerp(0.5f, s.depen, k);
        }

        public void Uncouple()
        {
            if (Busy) return;
            if (!Tower) { Release(); return; }
            StopAllCoroutines();
            StartCoroutine(UnhitchSequence());
        }

        IEnumerator UnhitchSequence()
        {
            Busy = true;
            float start = legT;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { SetLegs(Mathf.Lerp(start, 0f, t / 0.6f)); yield return null; }   // legs down first
            SetLegs(0f);
            Release();
            Busy = false;
        }

        void Release()
        {
            IgnoreTower(null, false);
            if (joint) Destroy(joint);
            joint = null;
            Tower = null;
        }

        void FixedUpdate()
        {
            if (Tower && !joint && !Busy) Tower = null;
            if (Tower)
            {
                self.handbrake = false;
                self.brakeInput = Tower.ForwardSpeed > 0.5f ? Tower.brakeInput : 0f;
                // rest together: a sleeping body drops out of the joint solve, so never let only one of the pair sleep
                if (Busy || Time.time < settleUntil) { Tower.KeepAwakeUntil = Time.time + 0.5f; self.KeepAwakeUntil = Time.time + 0.5f; }
                else if (Tower.Body.IsSleeping()) { if (!self.Body.IsSleeping()) self.Body.Sleep(); }
                else self.KeepAwakeUntil = Time.time + 0.5f;
            }
            else self.handbrake = true;
        }
    }
}
