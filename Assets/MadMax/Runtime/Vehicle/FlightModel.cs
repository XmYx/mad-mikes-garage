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
    /// Space pulls up, Ctrl pushes down. Hitting anything hard throws the pilot out and wrecks the engine.</summary>
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
        public float Heading => transform.eulerAngles.y;

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
        Transform prop, rotor;
        float tip, propAngle, rotorAngle, crashCd, pilotPitch, pilotRoll;
        Vector3 rotorHub = new Vector3(0f, 2.7f, 0.1f);
        const float Rho = 1.225f, RotorR = 4f, RotorCt = 0.0075f;

        void Awake()
        {
            v = GetComponent<VehicleDriver>();
            rb = GetComponent<Rigidbody>();
            sys = GetComponent<VehicleSystems>();
            prop = transform.Find("Prop");
            rotor = transform.Find("Rotor");
            if (rotor) rotorHub = rotor.localPosition;
            if (v) { v.aircraft = true; v.drag = 0.05f; }
            if (kind == Kind.Trike)
            {
                surfaces = new[]
                {
                    // a high flex wing with a little dihedral, a reflexed "tail" for pitch stability, a keel fin
                    new Surface { pos = new Vector3(-2.4f, 2.3f, 0.35f), fwd = Vector3.forward, up = Quaternion.Euler(0f, 0f, -3f) * Vector3.up, area = 7.5f, aspect = 6.7f, stall = 15f, cd0 = 0.03f, control = 1 },
                    new Surface { pos = new Vector3(2.4f, 2.3f, 0.35f), fwd = Vector3.forward, up = Quaternion.Euler(0f, 0f, 3f) * Vector3.up, area = 7.5f, aspect = 6.7f, stall = 15f, cd0 = 0.03f, control = 2 },
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

            // the throttle is a lever: W/S move it; on the ground with the lever closed S brakes
            Throttle = pilot ? Mathf.Clamp01(Throttle + throttleAxis * 0.6f * dt) : 0f;
            v.throttleInput = Throttle;
            v.brakeInput = pilot && throttleAxis < -0.5f && Throttle < 0.02f ? 1f : 0f;
            v.handbrake = !pilot;
            v.steerInput = rollInput;
            pilotPitch = Mathf.MoveTowards(pilotPitch, pilot ? pitchInput : 0f, 4f * dt);
            pilotRoll = Mathf.MoveTowards(pilotRoll, pilot ? rollInput : 0f, 4f * dt);

            var terrain = MadMax.World.DeformableTerrain.Instance;
            var pos = transform.position;
            float ground = terrain ? terrain.Height(pos.x, pos.z) : 0f;
            Altitude = pos.y - ground;
            Airborne = Altitude > 1.2f;
            var wind = MadMax.World.Fx.Wind; wind.y = 0f;
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
            rb.AddForceAtPosition(transform.forward * thrust, transform.TransformPoint(propAt));

            // lifting surfaces
            Stalled = false;
            foreach (var s in surfaces) Aero(s, wind);
            if (kind == Kind.Gyro) Rotor(dt, air, pilot);

            // parasitic drag of the airframe
            rb.AddForce(-air * air.magnitude * 0.5f * Rho * 0.35f);
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
            float defl = s.control == 1 ? pilotRoll : s.control == 2 ? -pilotRoll : s.control == 3 ? -pilotPitch : s.control == 4 ? -pilotRoll * 0.45f : 0f;   // rudder into the turn
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
                if (s.control == 1 || s.control == 2 || (kind == Kind.Gyro && s.control == 3)) Stalled = true;
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
            float target = Mathf.Min(220f, horiz * 9f + Mathf.Max(0f, -Vector3.Dot(air, transform.up)) * 6f);
            if (pilot && !Airborne && Throttle > 0.3f) target = Mathf.Max(target, 70f);         // pre-rotator
            tip = Mathf.MoveTowards(tip, target, (target > tip ? 22f : 14f) * dt);
            RotorRpm = tip / RotorR * 60f / (2f * Mathf.PI);
            var right = transform.right; var fwd = transform.forward;
            var axis = Quaternion.AngleAxis(-(3f + pilotPitch * 7f), right) * Quaternion.AngleAxis(-pilotRoll * 7f, fwd) * transform.up;
            float thrust = 0.5f * Rho * Mathf.PI * RotorR * RotorR * tip * tip * RotorCt;
            rb.AddForce(axis * thrust);                                                                    // through the centre of mass: the hang point
            if (air.sqrMagnitude > 1f) rb.AddForce(-air.normalized * thrust * 0.05f);                      // rotor drag
            // tilting the disc swings the airframe under it: control authority grows with rotor speed; the disc damps
            float auth = Mathf.Clamp01(tip / 150f);
            var w = rb.angularVelocity;
            rb.AddTorque((right * (-pilotPitch * 1.5f - Vector3.Dot(w, right) * 1.2f) + fwd * (-pilotRoll * 1.8f - Vector3.Dot(w, fwd) * 1.2f)) * auth, ForceMode.Acceleration);
            if (Airborne && tip < 90f && Airspeed > 3f) Stalled = true;                                    // too slow to hold the rotor up
        }

        void Spin(float dt, float rpmFrac)
        {
            if (prop) { propAngle = (propAngle + Mathf.Min(1400f, rpmFrac * 6000f) * dt) % 360f; prop.localRotation = Quaternion.Euler(0f, 0f, propAngle); }
            if (rotor)
            {
                rotorAngle = (rotorAngle + Mathf.Min(900f, tip / RotorR * Mathf.Rad2Deg) * dt) % 360f;
                rotor.localRotation = Quaternion.Euler(-(6f + pilotPitch * 7f), 0f, -pilotRoll * 7f) * Quaternion.Euler(0f, rotorAngle, 0f);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time < crashCd) return;
            float hit = c.relativeVelocity.magnitude;
            if (hit < 11f) return;
            crashCd = Time.time + 2f;
            var engine = v ? v.Engine : null;
            if (engine && engine.TryGetComponent<VehiclePart>(out var ep)) ep.damage = Mathf.Min(1f, ep.damage + (hit - 9f) * 0.04f);
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current == v) g.ThrowRider(v, rb.linearVelocity, (hit - 8f) * 5f, hit > 20f ? "YOU CRASHED" : "HARD LANDING");
            if (hit > 26f) MadMax.World.Explosion.Blast(transform.position, 3f, 3f, 0.4f, gameObject, true);
            tip *= 0.2f;
        }
    }
}
