using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Watercraft (user additions) on top of a wheel-less <see cref="VehicleDriver"/> (which keeps the engine,
    /// fuel, oil and faults): the hull floats on a grid of points along its bottom (spring to the design draft, with
    /// damping), sea waves rock it, water drag is low fore and aft and high across (the keel), the propeller pushes
    /// only while it is wet (engine rpm², reverse at 40 % when braking from a stop), the rudder turns with speed and
    /// prop wash, fast hulls lift their bow. Rafts are paddled (stamina). Rivers carry the hull along. Submarines add
    /// <see cref="Submarine"/> (ballast, battery, air).</summary>
    [DefaultExecutionOrder(-15)]
    public class BoatModel : MonoBehaviour
    {
        public enum Kind { Raft, Skiff, Trawler, Houseboat, Submarine }
        public Kind kind;
        /// <summary>Hull width, depth (keel to gunwale) and length (m); the keel sits at local y = <see cref="keelY"/>.</summary>
        public Vector3 hull = new Vector3(1.6f, 0.8f, 4f);
        public float keelY = 0.2f, draft = 0.3f;
        public float maxThrust = 3000f, rudder = 1f;
        public Vector3 propAt = new Vector3(0f, 0.1f, -2f);
        /// <summary>Where the driver stands up to when getting off (local, m).</summary>
        public Vector3 deckAt;
        /// <summary>Buoyancy multiplier (ballast; 1 = floats at the design draft).</summary>
        [System.NonSerialized] public float lift = 1f;
        /// <summary>Extra thrust from outside (the submarine's electric motor), N along the hull.</summary>
        [System.NonSerialized] public float extraThrust;

        public bool Afloat { get; private set; }
        public float Speed { get; private set; }
        public bool Reversing { get; private set; }
        /// <summary>0..1 how much of the hull is under water (1 = submerged).</summary>
        public float Submersion { get; private set; }
        /// <summary>Water surface over the hull's middle (NaN on land).</summary>
        public float WaterAt { get; private set; } = float.NaN;

        VehicleDriver v;
        Rigidbody rb;
        VehicleSystems sys;
        Vector3[] points;
        float k, wake, paddleT;

        void Awake()
        {
            v = GetComponent<VehicleDriver>();
            rb = GetComponent<Rigidbody>();
            sys = GetComponent<VehicleSystems>();
            if (!GetComponent<HullMask>()) gameObject.AddComponent<HullMask>();               // no water surface inside the hull
            if (v) { v.aircraft = true; v.drag = 0.01f; }                                   // no wheels: the engine revs with the lever, the hull does the rest
            // float points: across the beam and along the keel, a little in from the ends
            int nx = hull.x > 2.5f ? 3 : 2, nz = hull.z > 6f ? 6 : 4;
            points = new Vector3[nx * nz];
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                points[i * nz + j] = new Vector3((i / (float)(nx - 1) - 0.5f) * hull.x * 0.8f, keelY, (j / (float)(nz - 1) - 0.5f) * hull.z * 0.85f);
            if (rb)
            {
                // a spring per point that carries the hull at its draft; submarines are rated for full submersion
                float depthRef = kind == Kind.Submarine ? hull.y : draft;
                k = rb.mass * 9.81f / (points.Length * Mathf.Max(0.05f, depthRef));
                rb.angularDamping = 0.6f;
            }
        }

        /// <summary>Surface height with sea swell (lakes and rivers stay flat).</summary>
        public static float Surface(DeformableTerrain t, float x, float z, float time)
        {
            float lvl = t.WaterLevel(x, z);
            if (float.IsNaN(lvl) || Mathf.Abs(lvl - (WorldGen.SeaLevel + Weather.LakeRise)) > 0.05f) return lvl;
            float storm = 1f + (Weather.Raining ? 0.8f : 0f) + Fx.Wind.magnitude * 0.05f;
            return lvl + (Mathf.Sin(x * 0.21f + time * 1.1f) * 0.12f + Mathf.Sin(z * 0.17f - time * 0.8f + 1.3f) * 0.09f + Mathf.Sin((x + z) * 0.43f + time * 1.7f) * 0.04f) * storm;
        }

        void FixedUpdate()
        {
            var t = DeformableTerrain.Instance;
            if (!rb || rb.isKinematic || !t) return;
            float dt = Time.fixedDeltaTime, now = Time.time;
            var mid = transform.TransformPoint(new Vector3(0f, keelY, 0f));
            WaterAt = t.WaterLevel(mid.x, mid.z);
            int wet = 0; float sub = 0f;
            float cap = hull.y + (kind == Kind.Submarine ? 0f : 0.15f);                         // above the gunwale it can't displace more
            // a submerged hull has no waterplane to right it: its buoyancy acts at the hull's middle, above the heavy
            // keel (pushing at the keel points would stand it on its side)
            var buoy = kind == Kind.Submarine ? transform.up * (Mathf.Max(0f, rb.centerOfMass.y - keelY) + 0.6f) : Vector3.zero;
            foreach (var lp in points)
            {
                var p = transform.TransformPoint(lp);
                float lvl = Surface(t, p.x, p.z, now);
                if (float.IsNaN(lvl)) continue;
                float depth = lvl - p.y;
                if (depth <= 0f) continue;
                wet++;
                float d = Mathf.Min(depth, cap);
                sub += d / Mathf.Max(0.05f, hull.y);
                var pv = rb.GetPointVelocity(p);
                rb.AddForceAtPosition(Vector3.up * (k * d * lift - pv.y * rb.mass * 0.9f / points.Length), p + buoy);
            }
            Afloat = wet > 0;
            Submersion = points.Length > 0 ? Mathf.Clamp01(sub / points.Length) : 0f;
            var vel = rb.linearVelocity;
            Speed = Vector3.Dot(vel, transform.forward);
            if (!Afloat) { Reversing = false; return; }
            float wetFrac = wet / (float)points.Length;

            // water drag: slippery along the hull, the keel bites across it; rivers carry the boat
            var flow2 = t.World != null ? t.World.RiverFlow(mid.x, mid.z) : Vector2.zero;
            var rel = vel - new Vector3(flow2.x, 0f, flow2.y);
            var lv = transform.InverseTransformDirection(rel);
            float m = rb.mass;
            var drag = new Vector3(lv.x * Mathf.Abs(lv.x) * m * 0.35f + lv.x * m * 0.6f, lv.y * m * 0.4f, lv.z * Mathf.Abs(lv.z) * m * (kind == Kind.Skiff ? 0.012f : 0.022f) + lv.z * m * 0.05f);
            rb.AddForce(-transform.TransformDirection(drag) * wetFrac);
            rb.AddTorque(-rb.angularVelocity * m * 0.5f * wetFrac);

            // propeller: only while it is in the water
            bool pilot = v && v.Occupied;
            float throttle = pilot ? v.throttleInput : 0f, brake = pilot ? v.brakeInput : 0f;
            if (!Reversing && brake > 0.1f && throttle < 0.1f && Speed < 0.6f) Reversing = true;
            if (Reversing && (throttle > 0.1f || brake < 0.05f)) Reversing = false;
            var prop = transform.TransformPoint(propAt);
            float propLvl = Surface(t, prop.x, prop.z, now);
            bool propWet = !float.IsNaN(propLvl) && propLvl > prop.y - 0.05f;
            float thrust = 0f;
            if (kind == Kind.Raft && !(v && v.Engine))                                          // an outboard on the transom beats paddling
            {
                // paddling: steady pulls while W / S is held, paid in stamina
                float stroke = throttle - brake;
                if (Mathf.Abs(stroke) > 0.1f && (paddleT -= dt) <= 0f)
                {
                    paddleT = 0.9f;
                    var g = MadMax.Game.WastelandGame.Instance;
                    if (g && g.Current == v && g.Stats.stamina > 5f) { g.Stats.stamina -= 3f; rb.AddForce(transform.forward * Mathf.Sign(stroke) * m * 0.9f, ForceMode.Impulse); }
                }
            }
            else if (propWet && v && v.Engine && (sys == null || sys.Started))
            {
                float rpmFrac = Mathf.Clamp01(v.Rpm / Mathf.Max(1f, v.Engine.maxRpm));
                float power = sys ? sys.PowerFactor : 1f;
                thrust = Reversing ? -maxThrust * 0.4f * brake * rpmFrac * power
                       : maxThrust * rpmFrac * rpmFrac * power * (throttle > 0.02f ? 1f : 0f) * Mathf.Clamp01(1f - Speed / 22f);
            }
            if (propWet) thrust += extraThrust;
            if (thrust != 0f) rb.AddForceAtPosition(transform.forward * thrust, prop);
            if (v) v.throttleInput = Reversing ? Mathf.Max(throttle, brake * 0.6f) : throttle;   // the engine revs astern too

            // rudder: bites with speed through the water and with the prop's wash; paddles turn a raft slowly
            float steer = pilot ? v.steerInput : 0f;
            float bite = Mathf.Abs(lv.z) * 0.35f + (thrust > 0f ? thrust / Mathf.Max(1f, maxThrust) * 1.2f : 0f) + (kind == Kind.Raft ? 0.6f : 0f);
            float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
            float want = steer * rudder * Mathf.Clamp(bite, 0f, 3f) * (lv.z < -0.3f ? -1f : 1f) * 0.45f;
            rb.AddTorque(transform.up * (want - yawRate * 0.8f) * 2f * wetFrac, ForceMode.Acceleration);
            rb.AddTorque(-transform.forward * steer * Mathf.Clamp01(Speed / 10f) * 0.6f, ForceMode.Acceleration);   // lean into the turn
            if (kind == Kind.Skiff && Speed > 4f) rb.AddTorque(-transform.right * Mathf.Clamp01((Speed - 4f) / 8f) * 0.8f * wetFrac, ForceMode.Acceleration);   // on the plane: bow up

            // wake and spray
            if ((wake -= dt) <= 0f && Mathf.Abs(Speed) > 1.5f && !float.IsNaN(propLvl))
            {
                wake = 0.06f;
                var stern = transform.TransformPoint(new Vector3(Random.Range(-hull.x, hull.x) * 0.4f, 0f, -hull.z * 0.5f));
                Fx.Foam(new Vector3(stern.x, propLvl + 0.04f, stern.z), -transform.forward * 0.5f + Random.insideUnitSphere * 0.3f, 0.25f + Mathf.Abs(Speed) * 0.03f, new Color(0.9f, 0.95f, 0.97f, 0.8f), 3f + Mathf.Abs(Speed) * 0.2f);
                if (Speed > 6f)
                {
                    var bow = transform.TransformPoint(new Vector3(Random.Range(-1f, 1f) * hull.x * 0.5f, 0f, hull.z * 0.45f));
                    Fx.Smoke(new Vector3(bow.x, propLvl + 0.1f, bow.z), Vector3.up * 1.5f + transform.right * Random.Range(-2f, 2f), 0.25f, new Color(0.85f, 0.92f, 0.95f, 0.6f), 0.5f);
                }
            }
        }
    }
}
