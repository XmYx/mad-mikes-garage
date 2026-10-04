using UnityEngine;

namespace MadMax.Vehicles
{
    public enum Valvetrain { Ports, Ohv, Sohc, Dohc }
    public enum Aspiration { Natural, Turbo, Blower }

    /// <summary>What an engine is mechanically (read by the engine sound and the idle speed): cylinders and the crank
    /// angle each one fires at, two- or four-stroke, diesel or spark, displacement, valvetrain, aspiration, cooling, idle
    /// and flywheel. Big engines idle and rev lower and spin up and down slower.</summary>
    public sealed class EngineSpec
    {
        public string id;
        public int cylinders = 4;
        public bool twoStroke, diesel, airCooled;
        /// <summary>Crank angle (degrees, within the 720° four-stroke or 360° two-stroke cycle) each cylinder fires at.</summary>
        public float[] fireDeg;
        /// <summary>Exhaust bank per cylinder (V engines: 0 / 1; the banks meet at a Y-pipe).</summary>
        public int[] bank;
        public float displacement = 2f;          // litres
        public Valvetrain valves = Valvetrain.Sohc;
        public Aspiration air = Aspiration.Natural;
        public float idleRpm = 850f;
        /// <summary>Rotating mass relative to a 2 L four (rpm follows the throttle slower).</summary>
        public float flywheel = 1f;
        /// <summary>Camshaft wildness 0..1: overlap makes a lumpy, loping idle.</summary>
        public float cam;
        /// <summary>Exhaust system from the manifold to the tailpipe (m) and the stock muffler's corner (Hz).</summary>
        public float pipeLength = 2.4f, mufflerHz = 900f;

        public float Cycle => twoStroke ? 360f : 720f;
        public float PerCylinder => displacement / Mathf.Max(1, cylinders);

        static float[] Even(int n, float cycle) { var f = new float[n]; for (int i = 0; i < n; i++) f[i] = i * cycle / n; return f; }
        static int[] Alternate(int n) { var b = new int[n]; for (int i = 0; i < n; i++) b[i] = i & 1; return b; }

        /// <summary>The spec of a mounted engine part (by id; unknown ids are guessed from torque and rev range).</summary>
        public static EngineSpec For(string id, float torque = 300f, float maxRpm = 6000f)
        {
            id ??= "";
            var s = new EngineSpec { id = id };
            switch (id)
            {
                case "engine_i4":
                    s.cylinders = 4; s.fireDeg = Even(4, 720f); s.displacement = 2.0f; s.valves = Valvetrain.Sohc; s.idleRpm = 850f; s.pipeLength = 2.6f; s.mufflerHz = 1000f;
                    break;
                case "engine_i6":
                    s.cylinders = 6; s.fireDeg = Even(6, 720f); s.displacement = 4.0f; s.valves = Valvetrain.Ohv; s.idleRpm = 650f; s.flywheel = 1.5f; s.pipeLength = 3f; s.mufflerHz = 800f;
                    break;
                case "engine_diesel_i6":
                    s.cylinders = 6; s.fireDeg = Even(6, 720f); s.displacement = 6.7f; s.diesel = true; s.valves = Valvetrain.Ohv; s.air = Aspiration.Turbo;
                    s.idleRpm = 700f; s.flywheel = 2.2f; s.pipeLength = 3.2f; s.mufflerHz = 650f;
                    break;
                case "engine_truck_diesel":
                    s.cylinders = 6; s.fireDeg = Even(6, 720f); s.displacement = 14.8f; s.diesel = true; s.valves = Valvetrain.Ohv; s.air = Aspiration.Turbo;
                    s.idleRpm = 600f; s.flywheel = 4.5f; s.pipeLength = 4.2f; s.mufflerHz = 450f;
                    break;
                case "engine_marine_diesel":
                    s.cylinders = 6; s.fireDeg = Even(6, 720f); s.displacement = 8.3f; s.diesel = true; s.valves = Valvetrain.Ohv; s.air = Aspiration.Turbo;
                    s.idleRpm = 650f; s.flywheel = 3f; s.pipeLength = 2.2f; s.mufflerHz = 520f;
                    break;
                case "engine_v8_blower":
                    // cross-plane V8, firing order 1-8-4-3-6-5-7-2: the banks take L R R L R L L R, uneven pulses per pipe burble
                    s.cylinders = 8; s.fireDeg = Even(8, 720f); s.bank = new[] { 0, 1, 1, 0, 1, 0, 0, 1 }; s.displacement = 6.4f; s.valves = Valvetrain.Ohv;
                    s.air = Aspiration.Blower; s.idleRpm = 750f; s.flywheel = 1.8f; s.cam = 0.8f; s.pipeLength = 2.6f; s.mufflerHz = 800f;
                    break;
                case "engine_v8_forged":
                    // flat-plane V8: the banks alternate evenly, a high-revving scream rather than a burble
                    s.cylinders = 8; s.fireDeg = Even(8, 720f); s.bank = Alternate(8); s.displacement = 5.0f; s.valves = Valvetrain.Dohc;
                    s.idleRpm = 1000f; s.flywheel = 1.1f; s.cam = 0.35f; s.pipeLength = 2.2f; s.mufflerHz = 1150f;
                    break;
                case "engine_v12_last":
                    s.cylinders = 12; s.fireDeg = Even(12, 720f); s.bank = Alternate(12); s.displacement = 6.0f; s.valves = Valvetrain.Dohc; s.air = Aspiration.Blower;
                    s.idleRpm = 800f; s.flywheel = 1.6f; s.cam = 0.2f; s.pipeLength = 2.4f; s.mufflerHz = 1100f;
                    break;
                case "engine_2stroke":
                    // a Trabant-style air-cooled parallel twin: each cylinder fires every turn
                    s.cylinders = 2; s.twoStroke = true; s.fireDeg = Even(2, 360f); s.displacement = 0.6f; s.valves = Valvetrain.Ports; s.airCooled = true;
                    s.idleRpm = 950f; s.flywheel = 0.6f; s.pipeLength = 1.4f; s.mufflerHz = 1300f;
                    break;
                case "engine_2stroke_aero":
                    s.cylinders = 2; s.twoStroke = true; s.fireDeg = Even(2, 360f); s.displacement = 0.58f; s.valves = Valvetrain.Ports;
                    s.idleRpm = 1600f; s.flywheel = 0.5f; s.pipeLength = 1.1f; s.mufflerHz = 1800f;
                    break;
                case "engine_outboard":
                    s.cylinders = 2; s.twoStroke = true; s.fireDeg = Even(2, 360f); s.displacement = 0.75f; s.valves = Valvetrain.Ports;
                    s.idleRpm = 1000f; s.flywheel = 0.5f; s.pipeLength = 0.8f; s.mufflerHz = 900f;
                    break;
                case "engine_pedals":
                    s.cylinders = 1; s.fireDeg = new[] { 0f }; s.displacement = 0.1f; s.idleRpm = 900f;                   // legs: modelled above a 900 rpm "idle"
                    break;
                case "engine_single":
                    s.cylinders = 1; s.fireDeg = new[] { 0f }; s.displacement = 0.45f; s.valves = Valvetrain.Sohc; s.airCooled = true;
                    s.idleRpm = 1300f; s.flywheel = 0.35f; s.pipeLength = 1.3f; s.mufflerHz = 1400f;
                    break;
                case "engine_vtwin":
                    // 45° V-twin on a shared crankpin: fires at 0° and 315°, then a long 405° gap (the potato-potato)
                    s.cylinders = 2; s.fireDeg = new[] { 0f, 315f }; s.bank = new[] { 0, 1 }; s.displacement = 1.6f; s.valves = Valvetrain.Ohv; s.airCooled = true;
                    s.idleRpm = 950f; s.flywheel = 1f; s.cam = 0.3f; s.pipeLength = 1.8f; s.mufflerHz = 700f;
                    break;
                default:
                {
                    // unknown: guess from torque per litre and the rev range
                    bool diesel = id.Contains("diesel");
                    s.diesel = diesel;
                    s.displacement = Mathf.Clamp(torque / (diesel ? 125f : 95f), 0.3f, 16f);
                    s.cylinders = s.displacement < 0.8f ? 2 : s.displacement < 2.6f ? 4 : s.displacement < 5f ? 6 : 8;
                    s.fireDeg = Even(s.cylinders, 720f);
                    if (s.cylinders == 8) s.bank = new[] { 0, 1, 1, 0, 1, 0, 0, 1 };
                    s.valves = diesel || s.cylinders >= 6 ? Valvetrain.Ohv : Valvetrain.Sohc;
                    s.idleRpm = Mathf.Clamp(1250f - 55f * s.displacement, 600f, 1300f);
                    s.flywheel = Mathf.Clamp(s.displacement / 2.5f, 0.4f, 4f);
                    s.mufflerHz = Mathf.Clamp(1200f - 40f * s.displacement, 450f, 1200f);
                    break;
                }
            }
            if (s.bank == null) s.bank = new int[s.cylinders];
            return s;
        }
    }
}
