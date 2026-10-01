using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>The IRON EEL's submarine systems (user additions) on top of its <see cref="BoatModel"/>: ballast tanks
    /// (Space blows them — up, Ctrl floods them — down; released, the trim holds the depth), an electric motor on a
    /// 30 kWh battery under water, the diesel on the surface (it breathes through the sail: submerged it can't run) which
    /// charges the battery, a headlamp (lights key), and the cabin air — used up by whoever is aboard, scrubbed slower
    /// while there is power, refreshed at the surface or from O2 bottles at the rack. A dock or base grid with power
    /// charges it (its <see cref="MadMax.Building.UtilityNode"/>). State saves with the vehicle.</summary>
    public class Submarine : MonoBehaviour
    {
        public const float Capacity = 30000f;                          // Wh
        public float battery = 24000f, air = 100f, ballast;            // Wh, % of a full cabin, 0 blown .. 1 flooded
        public bool lamp;
        /// <summary>Automation (<see cref="WastelandGame.ExternalInput"/>): -1 blows the tanks, +1 floods them.</summary>
        [System.NonSerialized] public float ballastInput;
        const float Neutral = 0.714f;                                  // ballast that holds the depth

        VehicleDriver v;
        BoatModel boat;
        VehicleSystems sys;
        Rigidbody rb;
        InteriorSpace space;
        Light head;
        Transform sail;
        MadMax.Building.UtilityNode node;
        float hurtT;

        public float Depth { get; private set; }
        public bool Submerged => boat && boat.Submersion > 0.97f;
        /// <summary>The top of the sail is out of the water: the diesel can breathe and the hatch opens.</summary>
        public bool Snorkel { get; private set; } = true;
        public float BatteryFraction => battery / Capacity;
        /// <summary>Someone is breathing in here.</summary>
        public bool Crewed { get; private set; }

        void Awake()
        {
            v = GetComponent<VehicleDriver>(); boat = GetComponent<BoatModel>(); sys = GetComponent<VehicleSystems>();
            rb = GetComponent<Rigidbody>(); space = GetComponent<InteriorSpace>();
            if (sys) sys.sealedHull = true;
            var lg = new GameObject("Headlamp").AddComponent<Light>();
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 1.8f, 6.1f);
            lg.type = LightType.Spot; lg.range = 34f; lg.spotAngle = 55f; lg.intensity = 4f; lg.color = new Color(1f, 0.95f, 0.8f); lg.shadows = LightShadows.None;
            lg.enabled = false;
            head = lg;
            sail = new GameObject("Snorkel").transform;
            sail.SetParent(transform, false);
            sail.localPosition = new Vector3(0f, 4.6f, 1.6f);
            node = gameObject.AddComponent<MadMax.Building.UtilityNode>();                       // pieces built aboard (solar panels, generators) charge it
            node.kinds = MadMax.Building.UtilityKind.Power;
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || !boat) return;
            bool driving = g.Current == v;
            if (driving && Controls.Down(Controls.Act.Lights)) { lamp = !lamp; g.Toast(lamp ? "HEADLAMP ON" : "HEADLAMP OFF"); }
            head.enabled = lamp && battery > 0f && (DayNight.Darkness > 0.3f || Submerged);
        }

        void FixedUpdate()
        {
            var g = WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !t || !boat || !rb || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            bool driving = g.Current == v;
            Crewed = driving || (g.Player && g.Player.Interior == space);

            // ballast: Space blows, Ctrl floods; hands off, the trim creeps to neutral and the planes hold the depth
            float input = !driving ? 0f : WastelandGame.ExternalInput ? Mathf.Clamp(ballastInput, -1f, 1f)
                        : (Controls.Held(Controls.Act.Jump) ? -1f : 0f) + (Controls.Held(Controls.Act.Crouch) ? 1f : 0f);
            if (input != 0f) ballast = Mathf.Clamp01(ballast + input * 0.18f * dt);
            else if (Submerged) ballast = Mathf.MoveTowards(ballast, Neutral, 0.05f * dt);
            boat.lift = Mathf.Lerp(1.25f, 0.88f, ballast);
            if (Submerged && input == 0f) rb.AddForce(Vector3.up * -rb.linearVelocity.y * rb.mass * 1.2f);
            // stay level: the dive planes trim pitch and roll
            var fwd = transform.forward;
            float pitch = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f));
            float roll = Mathf.Asin(Mathf.Clamp(transform.right.y, -1f, 1f));
            rb.AddTorque((transform.right * pitch * 2.5f + fwd * -roll * 3f) - rb.angularVelocity * 0.8f, ForceMode.Acceleration);

            var mid = transform.position;
            float lvl = t.WaterLevel(mid.x, mid.z);
            Depth = float.IsNaN(lvl) ? 0f : Mathf.Max(0f, lvl - (transform.position.y + boat.keelY));
            Snorkel = float.IsNaN(lvl) || sail.position.y > lvl + 0.15f;
            if (sys) sys.noAir = !Snorkel;

            // electric drive under water; the diesel charges the battery on the surface; a powered dock charges it too
            float throttle = driving ? v.throttleInput - v.brakeInput : 0f;
            bool diesel = sys && sys.Started;
            float draw = 0f;
            if (!diesel && battery > 0f && Mathf.Abs(throttle) > 0.05f)
            {
                boat.extraThrust = throttle * 7000f;
                draw += Mathf.Abs(throttle) * 9000f;
            }
            else boat.extraThrust = 0f;
            if (head.enabled) draw += 250f;
            if (Crewed) draw += 200f;                                                       // CO2 scrubber, fans
            if (diesel) battery = Mathf.Min(Capacity, battery + 6000f * dt / 3600f);
            if (node)
            {
                node.demand = battery < Capacity - 1f ? 3000f : 0f;
                if (node.Powered && node.demand > 0f) battery = Mathf.Min(Capacity, battery + 3000f * dt / 3600f);
            }
            if (dockUntil > Time.time) battery = Mathf.Min(Capacity, battery + dockWatts * dt / 3600f);
            battery = Mathf.Max(0f, battery - draw * dt / 3600f);

            // the cabin air: breathed down by the crew (slower with the scrubber running), fresh through the hatch
            if (Snorkel) air = Mathf.Min(100f, air + 4f * dt);
            else if (Crewed) air = Mathf.Max(0f, air - (battery > 0f ? 0.045f : 0.11f) * dt);
            if (Crewed && air < 6f && (hurtT -= dt) <= 0f)
            {
                hurtT = 1f;
                g.Vitals.Hurt(4f, "SUFFOCATED");
                g.Toast("NO AIR! SURFACE OR OPEN AN O2 BOTTLE");
            }
        }

        float dockUntil, dockWatts;
        /// <summary>A powered docking collar charges the battery and tops up the air (called each second).</summary>
        public void Dock(float watts) { dockWatts = watts; dockUntil = Time.time + 1.5f; if (watts > 0f) air = Mathf.Min(100f, air + 3f); }

        /// <summary>Crack an O2 bottle into the cabin (the rack, or the bottle item).</summary>
        public void AddAir(float percent) => air = Mathf.Min(100f, air + percent);

        /// <summary>HUD line for the driver.</summary>
        public string Status => "DEPTH " + Mathf.RoundToInt(Depth) + " M  BALLAST " + Mathf.RoundToInt(ballast * 100f) + "%  BATT " + Mathf.RoundToInt(BatteryFraction * 100f)
                                + "%  AIR " + Mathf.RoundToInt(air) + "%" + (Snorkel ? (sys && sys.Started ? "  DIESEL CHARGING" : "  SURFACED") : "  ON BATTERY");

        public string SaveState() => battery.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "|" + air.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                     + "|" + ballast.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "|" + (lamp ? 1 : 0);

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split('|');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (p.Length > 0) float.TryParse(p[0], System.Globalization.NumberStyles.Float, inv, out battery);
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, inv, out air);
            if (p.Length > 2) float.TryParse(p[2], System.Globalization.NumberStyles.Float, inv, out ballast);
            if (p.Length > 3) lamp = p[3] == "1";
        }
    }
}
