using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Control on a network link (depth stage F): cable or pipe it in between the supply and the pieces it
    /// should control. Open, it conducts nothing of the kind it <see cref="cuts"/> (<see cref="UtilityNode.cut"/>), so
    /// whatever is reached only through it goes dark (or dry). Manual switch / valve ([E] flips), timer (on in a window
    /// of hours), light sensor (on at dusk and night, or the reverse) and float switch (a pump's power: on when the water
    /// network it sits on runs low, off when full). Automatic ones have a master [T] ON / OFF.</summary>
    public class UtilitySwitch : MonoBehaviour, IPlaceState, IInteractable
    {
        public enum Mode { Manual, Timer, Sensor, Float }
        public Mode mode;
        public UtilityKind cuts = UtilityKind.Power;
        /// <summary>Manual: the lever. Automatic: the master switch.</summary>
        public bool on = true;
        /// <summary>Timer window / sensor sense / float band (index into the mode's presets).</summary>
        public int setting;

        static readonly (float from, float to, string name)[] Windows =
        {
            (18f, 6f, "DUSK TO DAWN 18-06"), (17f, 23f, "EVENINGS 17-23"), (7f, 19f, "DAYTIME 07-19"), (22f, 6f, "NIGHT 22-06"), (5f, 9f, "MORNINGS 05-09"),
        };
        static readonly string[] Senses = { "ON IN THE DARK", "ON IN DAYLIGHT" };
        static readonly (float low, float high, string name)[] Bands = { (0.3f, 0.9f, "REFILL AT 30%, STOP AT 90%"), (0.6f, 0.98f, "KEEP ABOVE 60%"), (0.1f, 0.5f, "KEEP A RESERVE: 10-50%") };

        bool closed = true, started;
        float poll;
        UtilityNode node;

        void Awake() => node = GetComponent<UtilityNode>();
        void Start() { started = true; Apply(Want(), true); }

        /// <summary>Conducting right now.</summary>
        public bool Closed => closed;

        static bool InWindow(float h, float from, float to) => from <= to ? h >= from && h < to : h >= from || h < to;

        bool Want()
        {
            switch (mode)
            {
                case Mode.Timer: { var w = Windows[Mathf.Clamp(setting, 0, Windows.Length - 1)]; return on && InWindow(DayNight.Hours, w.from, w.to); }
                case Mode.Sensor: return on && (DayNight.Darkness > 0.25f) == (setting == 0);
                case Mode.Float:
                {
                    float cap = UtilityGrid.NetCapacity(node);
                    if (!on || cap <= 0f) return false;
                    float fill = UtilityGrid.NetWater(node, out _) / cap;
                    var b = Bands[Mathf.Clamp(setting, 0, Bands.Length - 1)];
                    return closed ? fill < b.high : fill < b.low;             // hysteresis: runs from low up to high
                }
                default: return on;
            }
        }

        void Update()
        {
            if ((poll -= Time.deltaTime) > 0f) return;
            poll = 0.5f;
            Apply(Want(), false);
        }

        void Apply(bool close, bool force)
        {
            if (!node || (close == closed && !force)) return;
            closed = close;
            node.cut = close ? UtilityKind.None : cuts;
            UtilityGrid.Invalidate();
        }

        /// <summary>Flip / set the lever (or master switch) and re-evaluate at once.</summary>
        public void Set(bool value)
        {
            on = value;
            if (started) Apply(Want(), false);
            GetComponent<Placeable>()?.Dirty();
        }

        string What => cuts == UtilityKind.Water ? "VALVE" : mode == Mode.Timer ? "TIMER" : mode == Mode.Sensor ? "LIGHT SENSOR" : mode == Mode.Float ? "FLOAT SWITCH" : "SWITCH";
        string State => cuts == UtilityKind.Water ? (closed ? "OPEN" : "SHUT") : closed ? "ON" : "OFF";

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string s = What + ": " + State;
            switch (mode)
            {
                case Mode.Manual: return s + (cuts == UtilityKind.Water ? (on ? "  [E] SHUT" : "  [E] OPEN") : on ? "  [E] SWITCH OFF" : "  [E] SWITCH ON");
                case Mode.Timer: s += "  " + Windows[Mathf.Clamp(setting, 0, Windows.Length - 1)].name; break;
                case Mode.Sensor: s += "  " + Senses[Mathf.Clamp(setting, 0, Senses.Length - 1)]; break;
                case Mode.Float: s += "  " + Bands[Mathf.Clamp(setting, 0, Bands.Length - 1)].name; break;
            }
            return s + "  [E] CHANGE" + (on ? "  [T] DISABLE" : "  [T] ENABLE (DISABLED)");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            MadMax.Audio.Sfx.Play("click", transform.position, 0.7f, secondary ? 0.9f : 1.1f);
            if (mode == Mode.Manual) { if (!secondary) Set(!on); return; }
            if (secondary) { Set(!on); g.Toast(What + (on ? " ENABLED" : " DISABLED")); return; }
            int n = mode == Mode.Timer ? Windows.Length : mode == Mode.Sensor ? Senses.Length : Bands.Length;
            setting = (setting + 1) % n;
            Apply(Want(), false);
            g.Toast(What + ": " + (mode == Mode.Timer ? Windows[setting].name : mode == Mode.Sensor ? Senses[setting] : Bands[setting].name));
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => (on ? "1" : "0") + ";" + setting;
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            on = p[0] != "0";
            if (p.Length > 1) int.TryParse(p[1], out setting);
            if (started) Apply(Want(), false);
        }
    }
}
