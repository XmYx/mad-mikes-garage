using System.Globalization;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Tuning (roadmap 14), per vehicle and saved, as offsets from its design: engine map (power ↔ economy ↔
    /// reliability), turbo (spools up high in the rev range) and supercharger (all the way) kits, nitrous bottles (5 s of
    /// +60 % torque each), gearing (short ↔ long) and final drive, ride height / spring stiffness / damping, brake bias
    /// and upgrades, tyre pressure (low: grips soft ground, rolls heavier, wears faster), ballast or a stripped interior.
    /// Written into VehicleDriver / VehicleChassis by <see cref="Apply"/>; the driver, systems and tyres read the factors.</summary>
    public partial class VehicleTuning : MonoBehaviour
    {
        public float map;                    // -1 economy .. 0 stock .. +1 power
        public float gearing;                // -1 short .. +1 long
        public float finalDrive;             // -1 .. +1 → ±15 %
        public float ride;                   // -1 slammed .. +1 lifted
        public float stiffness;              // -1 soft .. +1 stiff
        public float damping;                // -1 .. +1
        public float brakeBias = 0.6f;       // share of braking on the front axle
        public int brakeLevel;               // 0 stock, 1 vented discs, 2 racing
        public float pressure = 1f;          // 0.6 (soft ground) .. 1.25 (hard road)
        public float ballast;                // kg added
        public bool stripped;                // interior out: body 8 % lighter
        public bool turbo, supercharger;
        public int nitrous;                  // bottles fitted
        float nitrousUntil;

        VehicleDriver driver;
        VehicleChassis chassis;
        float[] baseGears;
        float baseFinal, baseRide, baseFreq, baseDamp, baseBrake, baseBody;
        bool captured;

        void Awake() { driver = GetComponent<VehicleDriver>(); chassis = GetComponent<VehicleChassis>(); }
        void Start() { Capture(); Apply(); }

        void Update()
        {
            if (!NitrousOn) return;
            var back = transform.position - transform.forward * 2.2f + transform.up * 0.5f;                  // blue flame out of the back
            if (Random.value < Time.deltaTime * 30f) MadMax.World.Fx.Sparks(back, -transform.forward * 3f, 2, new Color(0.4f, 0.6f, 1f));
        }

        void Capture()
        {
            if (captured || !driver) return;
            captured = true;
            baseGears = (float[])driver.gears.Clone(); baseFinal = driver.finalDrive; baseRide = driver.rideHeight;
            baseFreq = driver.frequency; baseDamp = driver.damping; baseBrake = driver.brakeForce; baseBody = chassis ? chassis.bodyMass : 0f;
        }

        public bool Stock => map == 0f && gearing == 0f && finalDrive == 0f && ride == 0f && stiffness == 0f && damping == 0f && Mathf.Abs(brakeBias - 0.6f) < 0.001f
                             && brakeLevel == 0 && pressure == 1f && ballast == 0f && !stripped && !turbo && !supercharger && nitrous == 0 && KitsStock;

        /// <summary>Write the settings into the driver and chassis.</summary>
        public void Apply()
        {
            Capture();
            if (!driver) return;
            float gs = gearing < 0f ? 1f - gearing * 0.15f : 1f - gearing * 0.13f;       // short ×1.15 .. long ×0.87
            for (int i = 0; i < baseGears.Length && i < driver.gears.Length; i++) driver.gears[i] = baseGears[i] * gs;
            driver.finalDrive = baseFinal * (1f + finalDrive * 0.15f);
            driver.rideHeight = baseRide + ride * (ride < 0f ? 0.05f : 0.12f);
            driver.frequency = baseFreq * (1f + stiffness * (stiffness < 0f ? 0.25f : 0.35f));
            driver.damping = Mathf.Clamp(baseDamp * (1f + damping * 0.35f), 0.1f, 1.5f);
            driver.brakeForce = baseBrake * (1f + brakeLevel * 0.25f);
            ApplyKits();                                                                    // gearbox, brake, suspension, tank kits
            if (chassis)
            {
                chassis.bodyMass = baseBody * (stripped ? 0.92f : 1f);
                chassis.tuneMass = ballast;
                chassis.NotifyChanged();
            }
        }

        // ------------------------------------------------------------------ what the driver and systems read
        public bool NitrousOn => Time.time < nitrousUntil;

        /// <summary>Engine torque multiplier at a fraction of max rpm.</summary>
        public float TorqueFactor(float rpmFrac)
        {
            float f = 1f + map * 0.15f;
            if (turbo) f *= 1f + 0.35f * Mathf.Clamp01((rpmFrac - 0.45f) / 0.3f);        // lag, then the rush
            if (supercharger) f *= 1.2f;
            if (NitrousOn) f *= 1.6f;
            return f;
        }

        public float FuelFactor => (1f + map * 0.3f) * (turbo ? 1.1f : 1f) * (supercharger ? 1.2f : 1f) * (NitrousOn ? 1.5f : 1f);
        public float HeatFactor => (1f + Mathf.Max(0f, map) * 0.5f) * (turbo ? 1.25f : 1f) * (supercharger ? 1.15f : 1f) * (NitrousOn ? 2f : 1f);

        /// <summary>Tyre grip from pressure: low pressure spreads the tread on soft ground, a little worse on hard road.</summary>
        public float GripFactor(float softness)
        {
            float d = 1f - pressure;
            return 1f + d * (softness * 0.9f - (1f - softness) * 0.12f);
        }

        public float RollingFactor => 1f + Mathf.Max(0f, 1f - pressure) * 0.8f - Mathf.Max(0f, pressure - 1f) * 0.3f;
        public float TyreWearFactor => 1f + Mathf.Abs(1f - pressure) * 1.5f;

        /// <summary>Per-wheel brake multiplier for the bias (1 = even split).</summary>
        public float BrakeShare(bool front, int nFront, int nTotal)
        {
            int nRear = nTotal - nFront;
            if (nFront == 0 || nRear == 0) return 1f;
            return front ? brakeBias * nTotal / nFront : (1f - brakeBias) * nTotal / nRear;
        }

        /// <summary>Fire a nitrous bottle (5 s).</summary>
        public bool FireNitrous()
        {
            if (nitrous <= 0 || NitrousOn) return false;
            nitrous--; nitrousUntil = Time.time + 5f;
            return true;
        }

        // ------------------------------------------------------------------ bench readouts
        /// <summary>Peak torque (Nm), peak power (kW) and top speed (km/h, gearing or drag limited).</summary>
        public void Card(out float torque, out float kw, out float topKmh)
        {
            torque = 0f; kw = 0f; topKmh = 0f;
            var e = driver ? driver.GetComponentInChildren<EngineStats>() : null;
            if (!e || !driver) return;
            for (int i = 1; i <= 40; i++)
            {
                float rpm = e.maxRpm * i / 40f, t = e.TorqueAt(rpm) * TorqueFactor(i / 40f);
                torque = Mathf.Max(torque, t);
                kw = Mathf.Max(kw, t * rpm * 2f * Mathf.PI / 60f / 1000f);
            }
            float r = 0.35f, rolling = 0f; int nw = 0;
            foreach (var w in driver.GetComponentsInChildren<WheelStats>())
            {
                var p = w.GetComponent<VehiclePart>();
                if (p && p.radius > 0f && nw == 0) r = p.radius;
                rolling += w.rolling; nw++;
            }
            rolling = nw > 0 ? rolling / nw : 1f;
            // flat hard ground: the fastest speed where some gear's pull at the tyres still beats air drag + rolling
            // resistance (the same forces VehicleDriver applies), the rev limiter included
            var rb = GetComponent<Rigidbody>();
            float weight = (rb ? rb.mass : 1500f) * 9.81f, crr = 0.015f * rolling * RollingFactor, best = 0f;
            foreach (float g in driver.gears)
            {
                float ratio = g * driver.finalDrive;
                for (float v = 1f; v < 150f; v += 0.25f)
                {
                    float rpm = v / r * 60f / (2f * Mathf.PI) * ratio;
                    if (rpm >= e.maxRpm * 0.995f) break;
                    float pull = e.TorqueAt(rpm) * TorqueFactor(rpm / e.maxRpm) * ratio * driver.efficiency / r;
                    if (pull < driver.drag * v * v + crr * weight) break;
                    best = Mathf.Max(best, v);
                }
            }
            topKmh = best * 3.6f;
        }

        // ------------------------------------------------------------------ save
        public string SaveState()
        {
            if (Stock) return null;
            var ci = CultureInfo.InvariantCulture;
            return string.Join(";", new[]
            {
                map.ToString("0.##", ci), gearing.ToString("0.##", ci), finalDrive.ToString("0.##", ci), ride.ToString("0.##", ci), stiffness.ToString("0.##", ci),
                damping.ToString("0.##", ci), brakeBias.ToString("0.##", ci), brakeLevel.ToString(ci), pressure.ToString("0.##", ci), ballast.ToString("0", ci),
                stripped ? "1" : "0", turbo ? "1" : "0", supercharger ? "1" : "0", nitrous.ToString(ci)
            }) + KitsState();
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            var ci = CultureInfo.InvariantCulture; var st = NumberStyles.Float;
            float F(int i, float def) => i < p.Length && float.TryParse(p[i], st, ci, out var v) ? v : def;
            map = F(0, 0f); gearing = F(1, 0f); finalDrive = F(2, 0f); ride = F(3, 0f); stiffness = F(4, 0f); damping = F(5, 0f);
            brakeBias = F(6, 0.6f); brakeLevel = (int)F(7, 0f); pressure = F(8, 1f); ballast = F(9, 0f);
            stripped = F(10, 0f) > 0.5f; turbo = F(11, 0f) > 0.5f; supercharger = F(12, 0f) > 0.5f; nitrous = (int)F(13, 0f);
            LoadKits(p);
            Apply();
        }
    }
}
