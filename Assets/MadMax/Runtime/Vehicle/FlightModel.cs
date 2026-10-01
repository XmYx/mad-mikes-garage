using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Flight (roadmap 25) on top of a <see cref="VehicleDriver"/> that only rolls the landing gear. Every
    /// lifting surface (wing halves with ailerons, a tailplane with the elevator, a fin with the rudder) takes its own
    /// airflow from the rigidbody's point velocity (so roll, pitch and yaw rates damp themselves), computes the angle of
    /// attack, lift with a linear slope up to the stall and a flat-plate curve beyond it, and induced + profile drag.
    /// A pusher propeller converts engine rpm into thrust that fades with airspeed. The gyrocopter's rotor spins up
    /// from the airflow (autorotation, pre-rotated on the ground) and lifts along its tilted axis; stick input tilts
    /// the disc. W/S move the throttle lever, A/D bank (with coordinated rudder, nose-wheel steering on the ground),
    /// Space pulls up, Ctrl pushes down. On the ground (user additions): S pulls the lever back fast and brakes, A/D
    /// steer the nose wheel with differential braking (tight taxi turns, gentler at speed), and holding S at a standstill
    /// with the lever closed reverses the propeller pitch to back up (<see cref="Reversing"/>, ≤ 3 m/s).
    /// Hitting anything hard throws the pilot out and wrecks the engine.</summary>
    [DefaultExecutionOrder(-15)]   // before VehicleDriver: throttle, brakes and nose wheel for this step
    public class FlightModel : MonoBehaviour
    {
        public enum Kind { Trike, Gyro }
        public Kind kind;
        public float maxThrust = 1300f, propSpeed = 45f;
        public Vector3 propAt = new Vector3(0f, 1.5f, -1.3f);

        [System.NonSerialized] public float pitchInput, rollInput, throttleAxis;
        public float Throttle { get; private set; }
        public float Airspeed { get; private set; }
        public float Altitude { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float RotorRpm { get; private set; }
        public bool Stalled { get; private set; }
        public bool Airborne { get; private set; }
        /// <summary>Main wing angle of attack (degrees).</summary>
        public float AoA { get; private set; }
        /// <summary>Sideslip in degrees (+ = air from the right): the slip ball.</summary>
        public float Sideslip { get; private set; }
        /// <summary>Close to the stall (wing AoA over ~13°, or the rotor slowing): the stall horn.</summary>
        public bool StallWarning => Airborne && (Stalled || (kind == Kind.Trike ? AoA > 13f : RotorRpm < 260f));
        public float Heading => transform.eulerAngles.y;
        /// <summary>Taxiing backwards on reversed propeller pitch.</summary>
        public bool Reversing { get; private set; }
        float reverseHold;

        struct Surface
        {
            public Vector3 pos, fwd, up;
            public float area, aspect, stall, cd0;
            public int control;       // 0 none, 1 aileron left, 2 aileron right, 3 elevator, 4 rudder
        }

        Surface[] surfaces;
        VehicleDriver v;
        Rigidbody rb;
        VehicleSystems sys;
        Transform prop, rotor, wing;
        Hinge wingRest;
        float tip, propAngle, rotorAngle, crashCd, pilotPitch, pilotRoll;
        Vector3 rotorHub = new Vector3(0f, 2.7f, 0.1f);
        const float Rho = 1.225f, RotorR = 4f, RotorCt = 0.005f;

        void Awake()
        {
            v = GetComponent<VehicleDriver>();
            rb = GetComponent<Rigidbody>();
            sys = GetComponent<VehicleSystems>();
            prop = transform.Find("Prop");
            rotor = transform.Find("Rotor");
            if (rotor) rotorHub = rotor.localPosition;
            wing = transform.Find("Wing");                                                      // HD trike: the wing tilts with the pilot's bar
            if (wing) Hinge.Rest(wing, out wingRest);
            if (v) { v.aircraft = true; v.drag = 0.05f; }
            // the wing / rotor carry no collider, so give the airframe the inertia it really has (pitch, yaw, roll)
            if (rb) { rb.inertiaTensor = kind == Kind.Trike ? new Vector3(500f, 900f, 700f) : new Vector3(600f, 700f, 450f); rb.inertiaTensorRotation = Quaternion.identity; }
            if (kind == Kind.Trike)
            {
                surfaces = new[]
                {
                    // a high flex wing with a little dihedral, a reflexed "tail" for pitch stability, a keel fin
                    new Surface { pos = new Vector3(-2.4f, 2.3f, 0.35f), fwd = Quaternion.Euler(-2f, 0f, 0f) * Vector3.forward, up = Quaternion.Euler(-2f, 0f, -3f) * Vector3.up, area = 8.8f, aspect = 6.2f, stall = 15f, cd0 = 0.03f, control = 1 },   // 2° incidence
                    new Surface { pos = new Vector3(2.4f, 2.3f, 0.35f), fwd = Quaternion.Euler(-2f, 0f, 0f) * Vector3.forward, up = Quaternion.Euler(-2f, 0f, 3f) * Vector3.up, area = 8.8f, aspect = 6.2f, stall = 15f, cd0 = 0.03f, control = 2 },
                    new Surface { pos = new Vector3(0f, 2.25f, -1.5f), fwd = Quaternion.Euler(3f, 0f, 0f) * Vector3.forward, up = Quaternion.Euler(3f, 0f, 0f) * Vector3.up, area = 2.2f, aspect = 3f, stall = 18f, cd0 = 0.02f, control = 3 },   // 3° down: trims at ~6° wing AoA
                    new Surface { pos = new Vector3(0f, 1.6f, -1.3f), fwd = Vector3.forward, up = Vector3.right, area = 0.9f, aspect = 1.5f, stall = 20f, cd0 = 0.03f, control = 4 },
                };
            }
            else
            {
                surfaces = new[]
                {
                    new Surface { pos = new Vector3(0f, 1.3f, -2.0f), fwd = Vector3.forward, up = Vector3.up, area = 0.9f, aspect = 3f, stall = 18f, cd0 = 0.02f, control = 3 },
                    new Surface { pos = new Vector3(0f, 1.7f, -2.1f), fwd = Vector3.forward, up = Vector3.right, area = 0.8f, aspect = 1.5f, stall = 20f, cd0 = 0.03f, control = 4 },
                };
            }
        }

        void FixedUpdate()
        {
            if (!v || !rb || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            bool pilot = v.Occupied;

            var terrain = MadMax.World.DeformableTerrain.Instance;
            var pos = transform.position;
            float ground = terrain ? terrain.Height(pos.x, pos.z) : 0f;
            Altitude = pos.y - ground;
            Airborne = Altitude > 1.2f;
            float rollSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            // the throttle is a lever: W/S move it. On the ground S snaps it back and brakes; held at a standstill with
            // the lever closed it reverses the propeller pitch
            bool pull = pilot && throttleAxis < -0.1f;
            Throttle = pilot ? Mathf.Clamp01(Throttle + throttleAxis * (pull && !Airborne ? 2f : 0.6f) * dt) : 0f;
            reverseHold = pull && !Airborne && Throttle < 0.02f ? reverseHold + dt : 0f;
            Reversing = pull && !Airborne && Throttle < 0.02f && (Reversing || (rollSpeed < 0.8f && reverseHold > 0.45f));
            v.throttleInput = Reversing ? 0.35f : Throttle;
            v.brakeInput = pull && !Reversing && (!Airborne || Throttle < 0.02f) ? Mathf.Clamp01(-throttleAxis) : 0f;
            v.handbrake = !pilot;
            v.steerInput = rollInput;
            pilotPitch = Mathf.MoveTowards(pilotPitch, pilot ? pitchInput : 0f, 4f * dt);
            pilotRoll = Mathf.MoveTowards(pilotRoll, pilot ? rollInput : 0f, 4f * dt);
            var wind = MadMax.World.Fx.Wind; wind.y = 0f;
            wind *= Mathf.Clamp01(0.25f + Altitude / 25f);                                             // the wind gradient: calmer near the ground
            var vel = rb.linearVelocity;
            VerticalSpeed = vel.y;
            var air = vel - wind;
            Airspeed = air.magnitude;
            float fwdAir = Vector3.Dot(air, transform.forward);

            // pusher propeller
            var engine = v.Engine;
            float rpmFrac = engine ? Mathf.Clamp01(v.Rpm / engine.maxRpm) : 0f;
            float power = sys ? sys.PowerFactor : 1f;
            float thrust = maxThrust * rpmFrac * rpmFrac * power * Mathf.Clamp01(1f - fwdAir / propSpeed) * (Throttle > 0.02f ? 1f : 0f);
            if (Reversing) thrust = -maxThrust * 0.3f * power * Mathf.Clamp01(-throttleAxis) * Mathf.Clamp01(1f + rollSpeed / 3f);   // backing up, ≤ 3 m/s
            rb.AddForceAtPosition(transform.forward * thrust, transform.TransformPoint(propAt));

            // lifting surfaces
            Stalled = false;
            foreach (var s in surfaces) Aero(s, wind);
            if (kind == Kind.Gyro) Rotor(dt, air, pilot);

            // parasitic drag of the airframe
            rb.AddForce(-air * air.magnitude * 0.5f * Rho * 0.35f);
            // handling help: the castering nose wheel keeps the take-off roll straight as the wing unloads the tyres;
            // aloft a little yaw / roll damping stands in for the pilot's feet
            var w = rb.angularVelocity;
            float yawRate = Vector3.Dot(w, transform.up), rollRate = Vector3.Dot(w, transform.forward), pitchRate = Vector3.Dot(w, transform.right);
            bool sim = MadMax.Game.GameSettings.Current.simFlight;                               // SIM FLIGHT: no assists aloft
            Sideslip = Airspeed > 3f ? Mathf.Atan2(Vector3.Dot(air, transform.right), Mathf.Max(1f, Vector3.Dot(air, transform.forward))) * Mathf.Rad2Deg : 0f;
            if (StallWarning && GetComponent<VehicleDriver>().Occupied) MadMax.Audio.Sfx.Loop(this, "beep", 0.5f, 1f, 20f);
            if (kind == Kind.Gyro && RotorRpm > 20f) MadMax.Audio.Sfx.Loop(transform, "rotor", Mathf.Clamp01(RotorRpm / 400f) * 0.7f, Mathf.Clamp(RotorRpm / 300f, 0.4f, 2f), 90f);   // blade slap
            if (!Airborne)
            {
                // taxiing: nose wheel + differential brakes turn it tightly when slow, gently on the take-off roll
                float maxRate = Mathf.Lerp(65f, 10f, Mathf.Clamp01(Mathf.Abs(rollSpeed) / 18f)) * Mathf.Deg2Rad;
                float want = (pilot ? rollInput : 0f) * maxRate * (rollSpeed < -0.2f ? -1f : 1f);
                rb.AddTorque(transform.up * (want - yawRate) * 5f, ForceMode.Acceleration);
            }
            else if (Airspeed > 6f && !sim)
            {
                // turn coordinator: the nose swings onto the flight path (what rudder work does), killing sideslip
                float beta = Mathf.Atan2(Vector3.Dot(air, transform.right), Mathf.Max(1f, Vector3.Dot(air, transform.forward)));
                rb.AddTorque(transform.up * (beta * 10f - yawRate * 2.5f), ForceMode.Acceleration);
            }
            // pitch: the trike pilot shoves the wing bar, the gyro pilot tilts the disc — authority grows with airspeed
            if (kind == Kind.Trike && sim)
            {
                // raw weight shift: the bar moves the wing, nothing holds the attitude or stops a stall
                float qs = Mathf.Clamp01(fwdAir * fwdAir / (16f * 16f));
                rb.AddTorque((-transform.right * (pilotPitch * 1.6f + pitchRate * 1.2f) - transform.forward * (pilotRoll * 2.2f + rollRate * 1.5f)) * qs, ForceMode.Acceleration);
            }
            else if (kind == Kind.Trike)
            {
                // pitch-rate command with an angle-of-attack limiter: pulling never takes the wing past ~11°
                float qScale = Mathf.Clamp01(fwdAir * fwdAir / (16f * 16f));
                float want = pilotPitch * 30f;
                if (fwdAir > 10f && AoA > 11f) want = Mathf.Min(want, -(AoA - 11f) * 6f);
                float upRate = -pitchRate * Mathf.Rad2Deg;
                rb.AddTorque(-transform.right * (want - upRate) * Mathf.Deg2Rad * 6f * qScale, ForceMode.Acceleration);
                // roll-rate command, bank limited to 50°, easing back to wings-level with the bar centred
                var fwd = transform.forward;
                var upProj = Vector3.ProjectOnPlane(Vector3.up, fwd);
                float bankRight = upProj.sqrMagnitude > 1e-4f ? -Vector3.SignedAngle(upProj.normalized, transform.up, fwd) : 0f;
                float wantRoll = Mathf.Abs(pilotRoll) > 0.05f ? pilotRoll * 45f : -bankRight * 0.8f;
                if (bankRight * Mathf.Sign(wantRoll) > 50f) wantRoll = 0f;
                float rollRight = -rollRate * Mathf.Rad2Deg;
                if (Airborne) rb.AddTorque(-fwd * (wantRoll - rollRight) * Mathf.Deg2Rad * 12f * qScale, ForceMode.Acceleration);
            }
            Spin(dt, rpmFrac);
        }

        void Aero(Surface s, Vector3 wind)
        {
            var p = transform.TransformPoint(s.pos);
            var fw = transform.TransformDirection(s.fwd);
            var up = transform.TransformDirection(s.up);
            var u = rb.GetPointVelocity(p) - wind;
            float vf = Vector3.Dot(u, fw), vn = Vector3.Dot(u, up);
            float q2 = vf * vf + vn * vn;
            if (q2 < 0.5f) return;
            float alpha = Mathf.Atan2(-vn, vf);
            if (s.control == 1) AoA = alpha * Mathf.Rad2Deg;
            // the trike shifts weight instead of flying ailerons / elevator (see FixedUpdate); the fin's rudder follows the bank
            float defl = kind == Kind.Trike ? (s.control == 4 ? -pilotRoll * 0.45f : 0f)
                       : s.control == 1 ? pilotRoll : s.control == 2 ? -pilotRoll : s.control == 3 ? -pilotPitch : s.control == 4 ? -pilotRoll * 0.45f : 0f;
            alpha += defl * (s.control == 3 ? 14f : 12f) * Mathf.Deg2Rad;
            float slope = 2f * Mathf.PI * s.aspect / (s.aspect + 2f);
            float stall = s.stall * Mathf.Deg2Rad;
            float cl, cd;
            if (Mathf.Abs(alpha) <= stall) { cl = slope * alpha; cd = s.cd0 + cl * cl / (Mathf.PI * s.aspect * 0.8f); }
            else
            {
                // past the stall: the lift collapses to a flat plate's, drag soars
                cl = 0.9f * Mathf.Sin(2f * alpha) * 0.65f;
                float sa = Mathf.Sin(alpha);
                cd = s.cd0 + 1.2f * sa * sa;
                if (s.control == 1 || s.control == 2) Stalled = true;
            }
            float q = 0.5f * Rho * q2 * s.area;
            var flow = -(fw * vf + up * vn).normalized;
            var span = Vector3.Cross(fw, up);
            var lift = Vector3.Cross(flow, span).normalized;
            rb.AddForceAtPosition(lift * cl * q + flow * cd * q, p);
        }

        /// <summary>Autogyro rotor: tip speed follows the airflow (a pre-rotator helps on the ground); thrust along the
        /// disc axis, which the stick tilts.</summary>
        void Rotor(float dt, Vector3 air, bool pilot)
        {
            float horiz = new Vector2(air.x, air.z).magnitude;
            var right = transform.right; var fwd = transform.forward;
            var axis = Quaternion.AngleAxis(-(3f + pilotPitch * 7f), right) * Quaternion.AngleAxis(-pilotRoll * 7f, fwd) * transform.up;
            // autorotation lives on the air coming up through the tilted disc (forward speed into it, or sinking);
            // climbing starves it, so the gyro settles into a modest climb instead of rocketing
            float through = Mathf.Clamp(-Vector3.Dot(air, axis), -8f, 20f);
            float target = Mathf.Clamp(horiz * 8f + through * 14f, 0f, 220f);
            if (pilot && !Airborne && Throttle > 0.3f) target = Mathf.Max(target, 70f);         // pre-rotator
            tip = Mathf.MoveTowards(tip, target, (target > tip ? 40f : 20f) * dt);
            RotorRpm = tip / RotorR * 60f / (2f * Mathf.PI);
            float thrust = 0.5f * Rho * Mathf.PI * RotorR * RotorR * tip * tip * RotorCt;
            rb.AddForce(axis * thrust);                                                                    // through the centre of mass: the hang point
            if (air.sqrMagnitude > 1f) rb.AddForce(-air.normalized * thrust * 0.05f);                      // rotor drag
            // tilting the disc swings the airframe under it: control authority grows with rotor speed; the disc damps
            float auth = Mathf.Clamp01(tip / 150f);
            var w = rb.angularVelocity;
            // stick = rate commands: pitch held within ±25° (easing to a 4° nose-up trim), bank within 45° (easing level)
            float pitchUp = -Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd, right);
            float wantPitch = Mathf.Abs(pilotPitch) > 0.05f ? pilotPitch * 20f : -(pitchUp - 4f) * 1.2f;
            if (pitchUp * Mathf.Sign(wantPitch) > 25f) wantPitch = 0f;
            float upRate = -Vector3.Dot(w, right) * Mathf.Rad2Deg;
            var upProj = Vector3.ProjectOnPlane(Vector3.up, fwd);
            float bankRight = upProj.sqrMagnitude > 1e-4f ? -Vector3.SignedAngle(upProj.normalized, transform.up, fwd) : 0f;
            float wantRoll = Mathf.Abs(pilotRoll) > 0.05f ? pilotRoll * 40f : -bankRight * 0.9f;
            if (bankRight * Mathf.Sign(wantRoll) > 45f) wantRoll = 0f;
            float rollRight = -Vector3.Dot(w, fwd) * Mathf.Rad2Deg;
            if (Airborne && MadMax.Game.GameSettings.Current.simFlight)
                rb.AddTorque((-right * (pilotPitch * 1.2f - upRate * Mathf.Deg2Rad * 0.8f) - fwd * (pilotRoll * 1.6f - rollRight * Mathf.Deg2Rad * 1.0f)) * auth, ForceMode.Acceleration);   // raw disc tilt
            else if (Airborne)
                rb.AddTorque((-right * (wantPitch - upRate) * 0.12f - fwd * (wantRoll - rollRight) * 0.14f) * auth, ForceMode.Acceleration);
            if (Airborne && tip < 90f && Airspeed > 3f) Stalled = true;                                    // too slow to hold the rotor up
        }

        void Spin(float dt, float rpmFrac)
        {
            if (wing) wingRest.Set(wing, Quaternion.Euler(pilotPitch * 6f, 0f, -pilotRoll * 9f));
            if (prop) { propAngle = (propAngle + Mathf.Min(1400f, rpmFrac * 6000f) * dt) % 360f; prop.localRotation = Quaternion.Euler(0f, 0f, propAngle); }
            if (rotor)
            {
                rotorAngle = (rotorAngle + Mathf.Min(900f, tip / RotorR * Mathf.Rad2Deg) * dt) % 360f;
                rotor.localRotation = Quaternion.Euler(-(6f + pilotPitch * 7f), 0f, -pilotRoll * 7f) * Quaternion.Euler(0f, rotorAngle, 0f);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time < crashCd || c.contactCount == 0) return;
            // the impact across the surface counts, not the slide along it (a tail scrape, a belly landing)
            var n = c.GetContact(0).normal;
            float hit = Mathf.Abs(Vector3.Dot(c.relativeVelocity, n));
            if (hit < 8f) return;
            crashCd = Time.time + 2f;
            var engine = v ? v.Engine : null;
            if (engine && engine.TryGetComponent<VehiclePart>(out var ep)) ep.damage = Mathf.Min(1f, ep.damage + (hit - 9f) * 0.04f);
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current == v) g.ThrowRider(v, rb.linearVelocity, (hit - 6f) * 5f, hit > 16f ? "YOU CRASHED" : "HARD LANDING");
            if (hit > 22f) MadMax.World.Explosion.Blast(transform.position, 3f, 3f, 0.4f, gameObject, true);
            tip *= 0.2f;
        }
    }
}
