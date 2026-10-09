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
        /// <summary>Lean back (Shift): wheelie; on a bicycle, out of the saddle (sprint).</summary>
        [System.NonSerialized] public bool leanBack;
        public bool Crashed { get; private set; }
        /// <summary>Current roll in degrees (+ = leaning left).</summary>
        public float Lean { get; private set; }
        public float Wheelie { get; private set; }
        /// <summary>Pedal phase for the rider's legs (bicycles), radians.</summary>
        public float PedalPhase { get; private set; }
        public bool Pedals { get; private set; }
        /// <summary>Why it last went down (debugging, tests).</summary>
        public string LastCrash { get; private set; }

        VehicleDriver v;
        Rigidbody rb;
        float wheelbase = 1.4f, fallenT, crashCd, pedalCheck, holdYaw, driveX;
        bool holding;

        void Awake()
        {
            v = GetComponent<VehicleDriver>();
            rb = GetComponent<Rigidbody>();
            float zmin = 0f, zmax = 0f;
            foreach (var s in GetComponentsInChildren<MountSocket>(true))
            {
                if (s.accepts != PartCategory.Wheel || s.name.StartsWith("wheel_side")) continue;
                float z = s.transform.localPosition.z;
                if (z < zmin) { var ws = s.Current ? s.Current.GetComponent<WheelStats>() : null; driveX = s.transform.localPosition.x + (ws ? ws.width * 0.5f : 0.05f); }
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
            v.tractionLimit = leanBack ? 0f : 1.1f;                                   // the rider feeds the throttle; Shift = everything (wheelie, roost)

            var terrain = MadMax.World.DeformableTerrain.Instance;
            float ground = terrain ? terrain.Height(transform.position.x, transform.position.z) : transform.position.y;
            bool airborne = transform.position.y - ground > 1.1f;

            if (sidecar || Crashed)
            {
                v.steerOverride = float.NaN;
                bool vintage = ridden && MadMax.Game.GameSettings.Current.vintageSidecar && !v.aiDriven;
                if (sidecar && !Crashed && ridden && vintage)
                {
                    // VINTAGE: straight bars, the pull towards the chair left to the rider, the chair lifts when it will
                    v.steerOverride = v.steerInput * v.maxSteer * Mathf.Lerp(1f, 0.35f, av / 25f);
                    rb.AddTorque(-transform.up * (v.DriveForce * (rb.centerOfMass.x - driveX) * 0.25f));
                }
                else if (sidecar && !Crashed && ridden)
                {
                    // no lean: the bars steer directly, up to what the tyres hold at this speed (~1 g), and the rider
                    // holds it straight against the chair (power drags an outfit towards its chair, braking away)
                    float lim = Mathf.Min(v.maxSteer, Mathf.Atan(wheelbase * 11f / Mathf.Max(1f, av * av)) * Mathf.Rad2Deg);
                    float yawRate = Vector3.Dot(rb.angularVelocity, transform.up), hold = 0f;
                    if (Mathf.Abs(v.steerInput) > 0.05f || av < 2f) holding = false;
                    else if (!holding) { holding = true; holdYaw = transform.eulerAngles.y; }
                    if (holding)
                    {
                        float err = Mathf.DeltaAngle(holdYaw, transform.eulerAngles.y);                          // + = pulled right
                        hold = Mathf.Clamp(-err * 0.6f - Mathf.Atan(yawRate * wheelbase / av) * Mathf.Rad2Deg, -6f, 6f);
                    }
                    v.steerOverride = v.steerInput * lim + hold * Mathf.Sign(speed);
                    // the drive sits beside the outfit's centre of mass: power yaws it towards the chair. The rig's lead
                    // and toe-in trim most of that out; the rest is the rider's job
                    rb.AddTorque(-transform.up * (v.DriveForce * (rb.centerOfMass.x - driveX) * 0.8f));
                }
                if (!Crashed && ridden) CrashChecks(phi, pitchUp, dt, 70f);
                return;
            }

            // the lean asked for: right = negative (A/D at speed), upright when slow, on the stand when parked
            float target = !ridden ? 11f : av < 2.5f ? 0f : -v.steerInput * maxLean * Mathf.Clamp01((av - 2.5f) / 5f);
            float steerDeg;
            if (!ridden || av < 2.5f) steerDeg = v.steerInput * v.maxSteer;
            else
            {
                // the bars that balance the current lean at this speed, plus counter-steer towards the wanted lean
                float coord = Mathf.Atan(wheelbase * 9.81f * Mathf.Tan(-phi * Mathf.Deg2Rad) / Mathf.Max(4f, av * av)) * Mathf.Rad2Deg;
                // the counter-steer's roll moment grows with v² (lateral acceleration = v²·δ / wheelbase): scale it back
                // above ~7 m/s so a quick lean change at speed doesn't overshoot past the asked-for lean
                float counter = 0.12f * Mathf.Clamp(49f / Mathf.Max(1f, av * av), 0.12f, 1f);
                steerDeg = Mathf.Clamp(coord * Mathf.Sign(speed) + (target - phi) * counter, -v.maxSteer, v.maxSteer);
            }
            v.steerOverride = steerDeg;

            // the rider's balance: a roll PD towards the target (weak in the air — the wheels' gyro). Slow, nothing
            // holds the bike up but the feet / kickstand: it pivots on the tyres (inertia + m·h²) and its weight tips it
            float rollRate = Vector3.Dot(rb.angularVelocity, fwd);
            float kp = !ridden ? 30f : av < 2.5f ? 45f : 32f, kd = 9f;
            if (airborne) { kp *= 0.3f; kd *= 0.5f; }
            var axis = Quaternion.Inverse(rb.inertiaTensorRotation) * Vector3.forward;
            float iRoll = Mathf.Max(0.5f, Vector3.Dot(Vector3.Scale(axis, axis), rb.inertiaTensor));
            float h = Mathf.Max(0.25f, rb.worldCenterOfMass.y - ground);
            float slow = airborne ? 0f : 1f - Mathf.Clamp01((av - 2.5f) / 5f);
            float gain = Mathf.Lerp(1f, 1f + rb.mass * h * h / iRoll, slow);
            float tip = slow * rb.mass * 9.81f * h * Mathf.Sin(phi * Mathf.Deg2Rad) / iRoll;
            if (Mathf.Abs(phi) < 70f) rb.AddTorque(fwd * (gain * ((target - phi) * Mathf.Deg2Rad * kp - rollRate * kd) - tip), ForceMode.Acceleration);

            // wheelie: lean back on the throttle, balanced around 25°
            if (ridden && leanBack && !Pedals && av > 1.5f && v.DriveCommand > 0.4f && !airborne)
            {
                float pitchRate = Vector3.Dot(rb.angularVelocity, -transform.right);
                rb.AddTorque(-transform.right * ((25f - pitchUp) * Mathf.Deg2Rad * 16f - pitchRate * 4f), ForceMode.Acceleration);
            }
            if (ridden) CrashChecks(phi, pitchUp, dt, 60f);
        }

        void CrashChecks(float phi, float pitchUp, float dt, float fallAngle)
        {
            fallenT = Mathf.Abs(phi) > fallAngle ? fallenT + dt : 0f;
            if (fallenT > 0.35f) { LastCrash = "fell " + phi.ToString("0"); Crash(Mathf.Abs(v.ForwardSpeed) * 1.2f, "YOU LOST IT"); }
            else if (pitchUp > 65f) { LastCrash = "wheelie " + pitchUp.ToString("0"); Crash(8f + Mathf.Abs(v.ForwardSpeed), "LOOPED THE WHEELIE"); }
        }

        void OnCollisionEnter(Collision c)
        {
            if (Crashed || !v || !v.Occupied) return;
            var n = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
            if (n.y > 0.7f) return;                                                           // landings and the ground
            if (c.rigidbody && !c.rigidbody.isKinematic && c.rigidbody.mass < 40f) return;       // pickups, debris, crates: ride over them
            float hit = Mathf.Abs(Vector3.Dot(c.relativeVelocity, n));
            if (Time.time < crashCd && hit < 10f) return;                                      // just mounted: knocks don't throw, a wall still does
            // shrubs, fences, cacti: the bike ploughs through (the prop carves) with a wobble instead of a spill
            var prop = c.collider.GetComponentInParent<MadMax.World.DestructibleVoxels>();
            if (prop && (prop.name == "Bush" || prop.name == "Fence" || prop.name == "Cactus" || prop.VoxelCount < 500) && hit < 16f)
            {
                rb.AddTorque(transform.forward * Random.Range(-1f, 1f) * Mathf.Min(3f, hit * 0.3f) + transform.up * Random.Range(-0.6f, 0.6f), ForceMode.VelocityChange);
                rb.linearVelocity *= 0.92f;
                return;
            }
            if (hit > 7f && TryGetComponent<VehicleDamage>(out var vd)) vd.LastStruck = c.collider;     // the rider tumbles over it
            if (hit > 7f) { LastCrash = "hit " + c.collider.name + " " + hit.ToString("0.0") + " n=" + n; Crash(hit * 2.2f, "THROWN OVER THE BARS"); }
        }

        void Crash(float severity, string why)
        {
            if (Crashed || (Time.time < crashCd && severity < 22f)) return;
            Crashed = true;
            v.steerOverride = float.NaN;
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current == v) g.ThrowRider(v, rb.linearVelocity, severity, why);
        }
    }
}
