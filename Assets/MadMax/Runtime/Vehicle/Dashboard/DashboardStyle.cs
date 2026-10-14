using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>What an instrument reads.</summary>
    public enum GaugeSource { Speed, Rpm, Fuel, Temp, OilPressure, Boost, Volts, Air, Odometer, Hours, Gear, ShiftLights }

    /// <summary>How an instrument shows it: a needle over a scale, a column of LED segments, seven-segment digits,
    /// rolling number drums, or a row of LEDs (shift lights).</summary>
    public enum GaugeKind { Dial, Bar, Digital, Drum, Leds }

    /// <summary>Ring around a dial: chrome, matte black, a square screwed frame (military), or none.</summary>
    public enum BezelKind { None, Chrome, Black, Square }

    /// <summary>Warning / indicator lamps of the cluster. The order is kept: 5 = OIL, 9 = COLD (as the old cluster).</summary>
    public enum DashLamp { Fuel, Brake, Engine, Wheel, Mud, Oil, Temp, Service, Charge, Cold, Beam, FourWD, DiffLock, Fire, Air }

    /// <summary>One instrument of a <see cref="DashboardStyle"/>: position on the face (m, cluster centre, +x right,
    /// +y up), size (dial radius / square half-size, digit height, bar height, LED row width), scale and red zones in
    /// the source's units (km/h, rpm, fuel 0..1, °C, bar, V).</summary>
    public sealed class GaugeDef
    {
        public GaugeKind kind;
        public GaugeSource source;
        public Vector2 at;
        public float size;
        public float min, max;                 // Speed: km/h (converted with the units setting); Rpm max 0 = from the engine
        public float redAbove = float.PositiveInfinity, redBelow = float.NegativeInfinity;
        public float sweep = 270f, centre;     // degrees of the scale and the direction of its middle (0 = up, clockwise)
        public int labels = 8;                 // about this many numerals round the scale (0 = none)
        public float labelDiv = 1f;            // numerals show value / labelDiv (rpm: x1000)
        public string ends;                    // two letters at the scale ends instead of numerals ("EF", "CH")
        public string caption;                 // under the hub (null: from the source)
        public int digits = 1;                 // digital / drum digits, bar segments, LEDs
        public int decimals;                   // drums: the last n wheels are tenths (inverted colours)
        public bool square;                    // square face (Square bezel)
        public Color32? face, ink;             // per-gauge face / ink (a white-faced tacho)
        public BezelKind? bezel;               // per-gauge bezel (null = the style's)

        public GaugeDef(GaugeKind kind, GaugeSource source, float x, float y, float size)
        { this.kind = kind; this.source = source; at = new Vector2(x, y); this.size = size; }
    }

    /// <summary>A warning lamp lens on the face (centre, square size).</summary>
    public sealed class LampDef
    {
        public DashLamp lamp;
        public Vector2 at;
        public float size;
    }

    /// <summary>A dummy switch on the panel: 0 toggle, 1 rocker, 2 rotary knob, 3 push button; with a label plate.</summary>
    public sealed class SwitchDef
    {
        public Vector2 at;
        public int kind;
        public string label;
    }

    /// <summary>Data for an instrument cluster (see <see cref="DashboardStyles"/>): panel size and housing, colours (Pal
    /// only), bezel, instruments, lamps and switches, backlight colour. <see cref="DashboardBuilder"/> turns it into
    /// real geometry; <see cref="VehicleDashboard"/> drives it.</summary>
    public sealed class DashboardStyle
    {
        public string id;
        public Vector2 size = new Vector2(0.34f, 0.135f);   // face panel (m)
        public float depth = 0.05f;                         // housing behind the faces
        public float visor;                                 // binnacle hood reaching towards the driver (0 = flat panel)
        public Color32[] housing = MadMax.Voxel.Pal.Black;  // ramp (dark .. light)
        public Color32 face, ink, minorInk, needle, red, digit;
        public BezelKind bezel = BezelKind.Chrome;
        public Color32 illumination;                        // backlight at night
        public bool alwaysLit;                              // backlight with the key on (else with the headlamps)
        public bool sweep;                                  // needles sweep the scale on key-on
        public float lampSize = 0.0085f;
        public bool rivets;                                 // rivets round the panel edge
        public readonly List<GaugeDef> gauges = new List<GaugeDef>();
        public readonly List<LampDef> lamps = new List<LampDef>();
        public readonly List<SwitchDef> switches = new List<SwitchDef>();

        public GaugeDef Add(GaugeDef g) { gauges.Add(g); return g; }

        public GaugeDef Dial(GaugeSource src, float x, float y, float r, float min, float max)
        { var g = Add(new GaugeDef(GaugeKind.Dial, src, x, y, r)); g.min = min; g.max = max; return g; }

        public void Lamp(DashLamp l, float x, float y, float size = 0f) => lamps.Add(new LampDef { lamp = l, at = new Vector2(x, y), size = size > 0f ? size : lampSize });

        /// <summary>Lamps in rows of <paramref name="cols"/> from (x0, y0), stepping dx right and dy down.</summary>
        public void LampGrid(DashLamp[] set, float x0, float y0, float dx, float dy, int cols)
        {
            for (int i = 0; i < set.Length; i++) Lamp(set[i], x0 + (i % cols) * dx, y0 - (i / cols) * dy);
        }

        public void Switch(float x, float y, int kind, string label) => switches.Add(new SwitchDef { at = new Vector2(x, y), kind = kind, label = label });
    }
}
