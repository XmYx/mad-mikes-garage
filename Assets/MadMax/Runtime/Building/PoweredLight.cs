using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A light that needs power (and a switch). Lights itself only at night when set to auto. Floodlights
    /// (<see cref="canSense"/>) have a motion SENSOR mode ([T]): dark until something moves within
    /// <see cref="SenseRange"/> m, then on for half a minute; raiders keep out of lit floodlights when they can.</summary>
    public class PoweredLight : MonoBehaviour, IPlaceState, IInteractable
    {
        public const float SenseRange = 18f;
        public static readonly System.Collections.Generic.List<PoweredLight> All = new System.Collections.Generic.List<PoweredLight>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt(MadMax.Game.WastelandGame g) => (on ? "[E] SWITCH OFF" : "[E] SWITCH ON") + (canSense ? (sensor ? "  [T] SENSOR: ON" : "  [T] SENSOR: OFF") : "") + (needsPower && node && !node.Powered ? "  NO POWER" : "");
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary) Toggle();
            else if (canSense) { sensor = !sensor; GetComponent<Placeable>()?.Dirty(); g.Toast(sensor ? "FLOODLIGHT: MOTION SENSOR" : "FLOODLIGHT: ALWAYS ON AT NIGHT"); }
        }

        public float watts = 60f;
        public bool on = true;
        public bool needsPower = true;
        public bool canSense, sensor;
        Light lamp;
        UtilityNode node;
        float scan, trippedUntil;
        void Awake() { lamp = GetComponentInChildren<Light>(); node = GetComponent<UtilityNode>(); }

        /// <summary>Lit right now (a raider in its beam is seen).</summary>
        public bool Lit => lamp && lamp.enabled;

        void Update()
        {
            bool dark = MadMax.World.DayNight.Darkness > 0.25f;
            if (sensor && dark && on && (scan -= Time.deltaTime) <= 0f) { scan = 0.5f; if (Motion()) trippedUntil = Time.time + 30f; }
            bool want = on && dark && (!sensor || Time.time < trippedUntil);
            if (node) node.demand = want ? watts : 0f;
            bool lit = want && (!needsPower || (node && node.Powered));
            if (lamp) lamp.enabled = lit && MadMax.World.LightBudget.Allowed(lamp);
        }

        /// <summary>Someone or something moving within range: people, animals, a vehicle rolling.</summary>
        bool Motion()
        {
            var at = transform.position;
            float r2 = SenseRange * SenseRange;
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.Alive && (n.transform.position - at).sqrMagnitude < r2) return true;
            foreach (var a in MadMax.Animals.Animal.All) if (a && (a.transform.position - at).sqrMagnitude < r2) return true;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return false;
            if (g.Player && !g.Current && (g.Player.transform.position - at).sqrMagnitude < r2) return true;
            foreach (var v in g.AllVehicles) if (v && v.Body && v.Body.linearVelocity.sqrMagnitude > 0.5f && (v.transform.position - at).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>A lit floodlight within range of <paramref name="p"/> (raiders pick other targets).</summary>
        public static bool FloodlitAt(Vector3 p)
        {
            foreach (var l in All) if (l && l.canSense && l.Lit && (l.transform.position - p).sqrMagnitude < SenseRange * SenseRange) return true;
            return false;
        }

        public void Toggle() { on = !on; GetComponent<Placeable>()?.Dirty(); }
        public string SaveState() => (on ? "1" : "0") + (sensor ? "s" : "");
        public void LoadState(string s) { on = !string.IsNullOrEmpty(s) && s[0] != '0'; sensor = s != null && s.EndsWith("s"); }
    }
}
