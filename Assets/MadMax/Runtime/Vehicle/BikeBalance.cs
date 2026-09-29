using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Two-wheeler handling (roadmap 24) on top of the raycast <see cref="VehicleDriver"/>: A/D asks for a lean
    /// (up to <see cref="maxLean"/> at speed) and the bars follow — a flick of counter-steer to tip in, then the
    /// steering angle that balances that lean at this speed; the rider's balance (a roll PD) holds it. Slow = feet
    /// down and direct steering; parked = on the kickstand. Shift at speed lifts a wheelie (held on the throttle).
    /// Falling past ~60°, looping a wheelie or hitting something hard throws the rider (<see cref="MadMax.Game.WastelandGame.ThrowRider"/>);
    /// a crashed bike lies there until somebody gets back on. Sidecar outfits don't lean: the chair holds them up and
    /// lifts on hard right-handers.</summary>
    [DefaultExecutionOrder(-15)]   // before VehicleDriver: the steering for this step
    public class BikeBalance : MonoBehaviour
    {
        public bool sidecar;
        public float maxLean = 42f;
        /// <summary>Lean back (Shift): wheelie.</summary>
        [System.NonSerialized] public bool leanBack;
        public bool Crashed { get; private set; }
        /// <summary>Current roll in degrees (+ = leaning left).</summary>
        public float Lean { get; private set; }
        public float Wheelie { get; private set; }
        /// <summary>Pedal phase for the rider's legs (bicycles), radians.</summary>
        public float PedalPhase { get; private set; }
        public bool Pedals { get; private set; }

        VehicleDriver v;
        Rigidbody rb;
        float wheelbase = 1.4f, fallenT, crashCd, pedalCheck;

        void Awake()
        {
            v = GetComponent<VehicleDriver>();
            rb = GetComponent<Rigidbody>();
            float zmin = 0f, zmax = 0f;
            foreach (var s in GetComponentsInChildren<MountSocket>(true))
            {
                if (s.accepts != PartCategory.Wheel || s.name.StartsWith("wheel_side")) continue;
                float z = s.transform.localPosition.z;
                zmin = Mathf.Min(zmin, z); zmax = Mathf.Max(zmax, z);
            }
            wheelbase = Mathf.Max(0.8f, zmax - zmin);
        }

        /// <summary>Back on the bike: pick it up.</summary>
        public void Remount() { Crashed = false; fallenT = 0f; crashCd = Time.time + 1.5f; }

        void FixedUpdate()
        {
            if (!v || !rb || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            var fwd = transform.forward;
            var upProj = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (upProj.sqrMagnitude < 1e-4f) return;
            float phi = Vector3.SignedAngle(upProj.normalized, transform.up, fwd);
            Lean = phi;
            float pitchUp = -Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd, transform.right);
            Wheelie = Mathf.Max(0f, pitchUp);
            float speed = v.ForwardSpeed, av = Mathf.Abs(speed);
            bool ridden = v.Occupied;
            if ((pedalCheck -= dt) <= 0f) { pedalCheck = 1f; Pedals = v.Engine && v.Engine.name.Contains("pedals"); }
            if (Pedals && ridden) PedalPhase += v.DriveCommand * (1.5f + av * 0.6f) * dt * 2f;

            var terrain = MadMax.World.DeformableTerrain.Instance;
            float ground = terrain ? terrain.Height(transform.position.x, transform.position.z) : transform.position.y;
            bool airborne = transform.position.y - ground > 1.1f;

            if (sidecar || Crashed)
            {
                v.steerOverride = float.NaN;
                if (!Crashed && ridden) CrashChecks(phi, pitchUp, dt, 70f);
                return;
            }

            // the lean asked for: right = negative (A/D at speed), upright when slow, on the stand when parked
            float target = !ridden ? 11f : av < 2.5f ? 0f : -v.steerInput * maxLean * Mathf.Clamp01((av - 2.5f) / 9f);
            float steerDeg;
            if (!ridden || av < 2.5f) steerDeg = v.steerInput * v.maxSteer;
            else
            {
                // the bars that balance the current lean at this speed, plus counter-steer towards the wanted lean
                float coord = Mathf.Atan(wheelbase * 9.81f * Mathf.Tan(-phi * Mathf.Deg2Rad) / Mathf.Max(4f, av * av)) * Mathf.Rad2Deg;
                steerDeg = Mathf.Clamp(coord * Mathf.Sign(speed) + (target - phi) * 0.12f, -v.maxSteer, v.maxSteer);
            }
            v.steerOverride = steerDeg;

            // the rider's balance: a roll PD towards the target (weak in the air — the wheels' gyro)
            float rollRate = Vector3.Dot(rb.angularVelocity, fwd);
            float kp = !ridden ? 30f : av < 2.5f ? 45f : 32f, kd = 9f;
            if (airborne) { kp *= 0.3f; kd *= 0.5f; }
            if (Mathf.Abs(phi) < 70f) rb.AddTorque(fwd * ((target - phi) * Mathf.Deg2Rad * kp - rollRate * kd), ForceMode.Acceleration);

            // wheelie: lean back on the throttle, balanced around 25°
            if (ridden && leanBack && av > 1.5f && v.DriveCommand > 0.4f && !airborne)
            {
                float pitchRate = Vector3.Dot(rb.angularVelocity, -transform.right);
                rb.AddTorque(-transform.right * ((25f - pitchUp) * Mathf.Deg2Rad * 16f - pitchRate * 4f), ForceMode.Acceleration);
            }
            if (ridden) CrashChecks(phi, pitchUp, dt, 60f);
        }

        void CrashChecks(float phi, float pitchUp, float dt, float fallAngle)
        {
            fallenT = Mathf.Abs(phi) > fallAngle ? fallenT + dt : 0f;
            if (fallenT > 0.35f) Crash(Mathf.Abs(v.ForwardSpeed) * 1.2f, "YOU LOST IT");
            else if (pitchUp > 65f) Crash(8f + Mathf.Abs(v.ForwardSpeed), "LOOPED THE WHEELIE");
        }

        void OnCollisionEnter(Collision c)
        {
            if (Crashed || !v || !v.Occupied || Time.time < crashCd) return;
            var n = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
            if (n.y > 0.7f) return;                                                           // landings and the ground
            float hit = Mathf.Abs(Vector3.Dot(c.relativeVelocity, n));
            if (hit > 7f) Crash(hit * 2.2f, "THROWN OVER THE BARS");
        }

        void Crash(float severity, string why)
        {
            if (Crashed || Time.time < crashCd) return;
            Crashed = true;
            v.steerOverride = float.NaN;
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current == v) g.ThrowRider(v, rb.linearVelocity, severity, why);
        }
    }
}
