using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Diegetic instrument cluster in front of the driver (child "Dashboard" at DriverEye +
    /// <see cref="offsetFromEye"/>, built when first sat in or seen from within <see cref="BuildReach"/>): real geometry from a <see cref="DashboardStyle"/> (<see cref="DashboardBuilder"/>),
    /// picked per vehicle (<see cref="DashboardStyles.Pick"/>) unless <see cref="style"/> names one. Needles turn with
    /// damping (speed / rpm quick, fuel / temperature slow, electrical gauges fall back with the key off), drums roll,
    /// seven-segment readouts and LED bars swap meshes, warning lamps glow through a MaterialPropertyBlock: bulb check on
    /// key-on, then OIL and CHARGE until the engine runs. The faces are backlit at night with the headlamps (or always,
    /// by style). Same geometry in both render paths: MadMax/PixelVoxel copies, or HDLit with the HD pack.</summary>
    [RequireComponent(typeof(VehicleDriver))]
    public class VehicleDashboard : MonoBehaviour
    {
        public Vector3 offsetFromEye = new Vector3(0f, -0.14f, 0.42f);
        [Tooltip("Cluster style id (DashboardStyles); empty = picked for the vehicle")] public string style;

        VehicleDriver driver;
        VehicleSystems sys;
        VehicleLights lights;
        DashParts parts;
        DashboardStyle def;
        DashScale scale;
        MaterialPropertyBlock mpb;
        readonly List<MountSocket> sockets = new List<MountSocket>();
        float keyOnAt = -100f, nextRead, nextScale, nextWheels, trip, odoBase, hoursBase, air = 6.5f, sizeScale = 1f;
        bool wasKeyOn, wheelMissing, wheelFlat, settled;
        Vector4 shownTint = new Vector4(-1f, 0f, 0f, 0f), shownSelf = new Vector4(-1f, 0f, 0f, 0f);

        public const float BulbCheckSeconds = 1.6f, SweepSeconds = 1.3f;

        /// <summary>The HD (HDLit) materials are in use.</summary>
        public bool IsHD => parts != null && parts.hd;
        /// <summary>The cluster style in use (null = none built).</summary>
        public string StyleId => def != null ? def.id : null;
        public DashboardStyle Style => def;
        /// <summary>The built cluster (tests).</summary>
        public DashParts Parts => parts;
        /// <summary>The fuel / temperature reading used last.</summary>
        public VehicleGauges.Reading Last { get; private set; }
        /// <summary>The key is on (someone in the seat, battery connected).</summary>
        public bool KeyOn => wasKeyOn;
        /// <summary>Kilometres on the odometer (miles shown with imperial units).</summary>
        public double OdometerKm => odoBase + trip;

        /// <summary>A transform of an instrument cluster (skipped by dents, grime, scrapes and paint).</summary>
        public static bool IsDash(Component c) => c && c.name.StartsWith("Dash", System.StringComparison.Ordinal);

        void Start()
        {
            driver = GetComponent<VehicleDriver>();
            sys = GetComponent<VehicleSystems>();
            if (!transform.Find("DriverEye") || Application.isBatchMode) { enabled = false; return; }
            mpb = new MaterialPropertyBlock();
            nextProbe = Time.time + Random.value;                                                            // spread the distance probes
        }

        /// <summary>Clusters are built lazily: when someone sits in or the focus comes this close (m).</summary>
        public const float BuildReach = 30f;
        float nextProbe;
        bool tried;

        /// <summary>Builds the cluster now if it is not there yet (tests, scenes without a game).</summary>
        public void Ensure() { if (!tried && enabled) { tried = true; Build(); if (parts == null) enabled = false; } }

        /// <summary>Builds (or rebuilds) the cluster for the current style / vehicle.</summary>
        public void Build()
        {
            if (parts != null && parts.root) Destroy(parts.root);
            parts = null; def = null;
            var eye = transform.Find("DriverEye");
            if (!eye || (sys && VehicleGauges.Pedals(sys))) return;                                 // pedal bikes: nothing to read
            string id = DashboardStyles.Pick(driver, out sizeScale);
            if (!string.IsNullOrEmpty(style) && DashboardStyles.Exists(style)) id = style;
            def = DashboardStyles.Get(id);
            if (def == null) return;
            bool hd = MadMax.Rendering.HDBits.On;
            var g = MadMax.Game.WastelandGame.Instance;
            Material src = g ? g.propMaterial : null;
            if (!src) { var body = transform.Find("Body"); if (body && body.TryGetComponent<Renderer>(out var br)) src = br.sharedMaterial; }
            if (!DashboardBuilder.Materials(hd, src, out var lit, out var glow)) { def = null; return; }
            scale = DashboardBuilder.ScaleFor(def, driver.Engine, MadMax.Game.GameSettings.Current.metric);
            parts = DashboardBuilder.Build(transform, def, scale, lit, glow, hd);
            Place(eye);
            uint h = 2166136261u;
            foreach (char ch in DashboardStyles.DesignName(name)) h = (h ^ ch) * 16777619u;
            hoursBase = 600f + h % 5400u + (h >> 12) % 10u * 0.1f;
            odoBase = 30000f + (h >> 4) % 170000u + (sys ? sys.hours * 35f : 0f);
            shownTint.x = shownSelf.x = -1f;
            foreach (var l in parts.lamps) l.shown = -1;
            wasKeyOn = false;
            Lamps(false, false);                                                                     // dark lenses, unlit faces until the key turns
            Illuminate(false);
            settled = false;
        }

        void Place(Transform eye)
        {
            var t = parts.root.transform;
            t.localPosition = eye.localPosition + offsetFromEye;
            float tilt = Mathf.Atan2(-offsetFromEye.y, Mathf.Max(0.05f, offsetFromEye.z)) * Mathf.Rad2Deg;
            t.localRotation = Quaternion.Euler(Mathf.Clamp(tilt, 0f, 45f) + 4f, 0f, 0f);         // faces the eye, top leaning away
            t.localScale = Vector3.one * sizeScale;
        }

        void Update()
        {
            float dt = Time.deltaTime, now = Time.time;
            if (!tried)
            {
                if (now < nextProbe) return;
                nextProbe = now + 1f;
                var g = MadMax.Game.WastelandGame.Instance;
                if (!driver.Occupied && g && (g.FocusPos - transform.position).sqrMagnitude > BuildReach * BuildReach) return;
                Ensure();
            }
            if (parts == null || !parts.root) return;
            bool occupied = driver.Occupied;
            bool keyOn = occupied && !(sys && sys.disconnected) && driver.driveable;
            if (keyOn && !wasKeyOn) keyOnAt = now;
            if (keyOn != wasKeyOn) settled = false;
            wasKeyOn = keyOn;
            float speedAbs = Mathf.Abs(driver.SpeedKmh);
            if (speedAbs > 0.5f) trip += speedAbs / 3.6f * dt / 1000f;

            if (sys && now >= nextRead) { nextRead = now + (occupied ? 0.1f : 0.5f); Last = VehicleGauges.Read(sys); }
            if (!occupied && settled) return;
            if (occupied && now >= nextScale) { nextScale = now + 1f; CheckScale(); }
            if (!parts.faceRenderer.isVisible && !(occupied && !driver.aiDriven)) return;          // the player's own car stays live

            if (now >= nextWheels) { nextWheels = now + 0.25f; ScanWheels(); }
            bool running = sys ? sys.Started : keyOn;
            if (running) air = Mathf.MoveTowards(air, 8.2f, 0.35f * dt);
            else air = Mathf.MoveTowards(air, 0f, 0.004f * dt);
            if (driver.brakeInput > 0.05f) air = Mathf.Max(0f, air - driver.brakeInput * 0.25f * dt);

            float since = now - keyOnAt;
            bool bulb = keyOn && since < BulbCheckSeconds;
            float sweep = keyOn && def.sweep && since < SweepSeconds ? Mathf.Sin(Mathf.PI * since / SweepSeconds) : -1f;
            bool moving = false;
            foreach (var ins in parts.gauges) moving |= Drive(ins, keyOn, running, bulb, sweep, dt);
            bool lampsLit = Lamps(keyOn, bulb);
            Illuminate(keyOn);
            settled = !keyOn && !moving && !lampsLit;
        }

        // ------------------------------------------------------------------ instruments
        float Value(DashInstrument ins, bool keyOn, bool running)
        {
            var e = driver.Engine;
            float rpmFrac = e && e.maxRpm > 0f ? Mathf.Clamp01(driver.Rpm / e.maxRpm) : 0f;
            switch (ins.def.source)
            {
                case GaugeSource.Speed: return Mathf.Abs(driver.SpeedKmh) * (scale.metric ? 1f : 0.6214f);
                case GaugeSource.Rpm:
                case GaugeSource.ShiftLights:
                    if (running) return driver.Rpm;
                    return sys && sys.Cranking ? 180f + 90f * Mathf.Abs(Mathf.Sin(Time.time * 19f)) : 0f;
                case GaugeSource.Fuel: return keyOn && Last.fuelShown ? Mathf.Lerp(ins.min, ins.max, Last.fuelNeedle) : ins.min;
                case GaugeSource.Temp: return keyOn && Last.tempShown ? Last.temperature : ins.min;
                case GaugeSource.OilPressure: return keyOn && sys ? sys.OilPressure : 0f;
                case GaugeSource.Boost:
                    if (!running) return 0f;
                    bool diesel = sys && sys.FuelKind == MadMax.Items.ResourceType.Diesel;
                    return Mathf.Clamp01(driver.throttleInput) * rpmFrac * (diesel ? 1.8f : 0.9f);
                case GaugeSource.Volts:
                    if (!keyOn) return 0f;
                    return running ? 27.8f + 0.5f * rpmFrac : sys && sys.Cranking ? 20.5f : 24.6f;
                case GaugeSource.Air: return air;
                case GaugeSource.Odometer: return (float)(OdometerKm * (scale.metric ? 1.0 : 0.6214));
                case GaugeSource.Hours: return hoursBase + (sys ? sys.hours : 0f);
            }
            return 0f;
        }

        /// <summary>Moves one instrument; true while it is still travelling.</summary>
        bool Drive(DashInstrument ins, bool keyOn, bool running, bool bulb, float sweep, float dt)
        {
            var g = ins.def;
            switch (g.kind)
            {
                case GaugeKind.Dial:
                    {
                        float target = Value(ins, keyOn, running);
                        bool fast = g.source == GaugeSource.Speed || g.source == GaugeSource.Rpm;
                        if (sweep >= 0f && fast) target = Mathf.Lerp(ins.min, ins.max, sweep);
                        if (fast) ins.shown = Mathf.SmoothDamp(ins.shown, target, ref ins.vel, sweep >= 0f ? 0.03f : g.source == GaugeSource.Rpm ? 0.05f : 0.08f, Mathf.Infinity, dt);
                        else if (g.source == GaugeSource.Fuel || g.source == GaugeSource.Temp) ins.shown = Mathf.MoveTowards(Mathf.Lerp(ins.shown, target, 1f - Mathf.Exp(-dt / 1.4f)), target, (ins.max - ins.min) * 0.02f * dt);
                        else ins.shown = Mathf.SmoothDamp(ins.shown, target, ref ins.vel, 0.25f, Mathf.Infinity, dt);
                        float frac = Mathf.Clamp((ins.shown - ins.min) / (ins.max - ins.min), -0.012f, 1.03f);
                        float deg = Mathf.LerpUnclamped(ins.sweepFrom, ins.sweepTo, frac);
                        if (Mathf.Abs(deg - ins.drawn) > 0.04f) { ins.drawn = deg; ins.needle.localRotation = Quaternion.Euler(0f, 0f, -deg); }
                        return Mathf.Abs(ins.shown - target) > (ins.max - ins.min) * 0.002f;
                    }
                case GaugeKind.Digital:
                    {
                        if (g.source == GaugeSource.Gear) SetChar(ins, 0, !keyOn ? ' ' : bulb ? '8' : GearChar());
                        else
                        {
                            int v = Mathf.Min(999, Mathf.RoundToInt(Value(ins, keyOn, running)));
                            for (int k = 0; k < ins.digits.Length; k++)
                            {
                                int place = ins.digits.Length - 1 - k, div = place == 0 ? 1 : place == 1 ? 10 : place == 2 ? 100 : 1000;
                                char ch = !keyOn ? ' ' : bulb ? '8' : (v >= div || place == 0) ? (char)('0' + v / div % 10) : ' ';
                                SetChar(ins, k, ch);
                            }
                        }
                        return false;
                    }
                case GaugeKind.Drum:
                    {
                        double val = Value(ins, keyOn, running);
                        if (g.source == GaugeSource.Odometer) val = OdometerKm * (scale.metric ? 1.0 : 0.6214);
                        int n = ins.wheels.Length;
                        val *= System.Math.Pow(10, g.decimals);
                        for (int k = 0; k < n; k++)
                        {
                            int place = n - 1 - k;
                            double x = val / System.Math.Pow(10, place);
                            double whole = System.Math.Floor(x);
                            float digit = (float)(whole % 10.0), f = (float)(val - System.Math.Floor(val));
                            float roll;
                            if (place == 0) roll = f;
                            else
                            {
                                double below = val - System.Math.Floor(val / System.Math.Pow(10, place)) * System.Math.Pow(10, place);   // what the lower wheels show
                                double top = System.Math.Pow(10, place) - 1.0;
                                roll = below > top ? (float)(below - top) : 0f;
                            }
                            float ang = 36f * (digit + roll);
                            if (Mathf.Abs(Mathf.DeltaAngle(ang, ins.wheelAngle[k])) > 0.05f)
                            {
                                ins.wheelAngle[k] = ang;
                                ins.wheels[k].localRotation = Quaternion.AngleAxis(ang, Vector3.right);
                            }
                        }
                        return false;
                    }
                case GaugeKind.Bar:
                    {
                        float target = Value(ins, keyOn, running);
                        ins.shown = Mathf.MoveTowards(Mathf.Lerp(ins.shown, target, 1f - Mathf.Exp(-dt / 1.2f)), target, (ins.max - ins.min) * 0.02f * dt);
                        int n = ins.levels.Length - 1;
                        int lv = !keyOn ? 0 : bulb ? n : Mathf.Clamp(Mathf.CeilToInt((ins.shown - ins.min) / (ins.max - ins.min) * n - 0.25f), 0, n);
                        SetLevel(ins, lv);
                        return Mathf.Abs(ins.shown - target) > (ins.max - ins.min) * 0.002f;
                    }
                case GaugeKind.Leds:
                    {
                        int n = ins.levels.Length - 1, lv = 0;
                        if (keyOn)
                        {
                            float rpm = Value(ins, keyOn, running), red = scale.rpmRed, start = red * 0.72f;
                            lv = bulb || sweep >= 0f ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, sweep) * n), 0, n)
                               : rpm >= red * 0.98f ? (Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? n : 0)
                               : Mathf.Clamp(Mathf.FloorToInt((rpm - start) / (red - start) * n), 0, n);
                        }
                        SetLevel(ins, lv);
                        return false;
                    }
            }
            return false;
        }

        char GearChar()
        {
            if (!driver.Engine) return '-';
            if (driver.Reversing) return 'R';
            if (driver.manual && driver.Gear == 0) return 'N';
            if (!driver.manual && driver.handbrake && Mathf.Abs(driver.SpeedKmh) < 1f) return 'P';
            return (char)('0' + Mathf.Clamp(driver.Gear, 0, 9));
        }

        static void SetChar(DashInstrument ins, int k, char ch)
        {
            if (ins.shownChars[k] == ch) return;
            ins.shownChars[k] = ch;
            int i = DashMesh.SegChars.IndexOf(ch);
            ins.digits[k].sharedMesh = ins.glyphs[i < 0 ? DashMesh.SegChars.Length - 1 : i];
        }

        static void SetLevel(DashInstrument ins, int lv)
        {
            if (ins.shownLevel == lv) return;
            ins.shownLevel = lv;
            ins.level.sharedMesh = ins.levels[lv];
        }

        void CheckScale()
        {
            var sc = DashboardBuilder.ScaleFor(def, driver.Engine, MadMax.Game.GameSettings.Current.metric);
            if (sc.metric == scale.metric && sc.rpmMax == scale.rpmMax && sc.rpmRed == scale.rpmRed && sc.speedMax == scale.speedMax) return;
            scale = sc;
            parts.face.sharedMesh = DashboardBuilder.FaceFor(def, sc, out parts.faceKey);              // numerals for the new units / engine
            foreach (var ins in parts.gauges)
                if (ins.def.source == GaugeSource.Speed) ins.max = sc.speedMax;
                else if (ins.def.source == GaugeSource.Rpm || ins.def.source == GaugeSource.ShiftLights) ins.max = sc.rpmMax;
        }

        void ScanWheels()
        {
            GetComponentsInChildren(true, sockets);
            wheelMissing = wheelFlat = false;
            foreach (var sk in sockets)
            {
                if (sk.accepts != PartCategory.Wheel) continue;
                if (!sk.Current) { wheelMissing = true; continue; }
                if (sk.Current.TryGetComponent<WheelStats>(out var ws) && ws.Popped) wheelFlat = true;
            }
        }

        // ------------------------------------------------------------------ lamps and backlight
        static readonly Color32[] LampCols = { Pal.Amber, Pal.Crimson[4], Pal.Moss[4] };
        const int Amber = 1, Red = 2, Green = 3;

        static int Default(DashLamp l) =>
            l == DashLamp.Beam || l == DashLamp.FourWD ? Green :
            l == DashLamp.Brake || l == DashLamp.Wheel || l == DashLamp.Oil || l == DashLamp.Temp || l == DashLamp.Charge || l == DashLamp.Fire || l == DashLamp.Air ? Red : Amber;

        /// <summary>What a lamp shows now: 0 off, 1 amber, 2 red, 3 green.</summary>
        public int LampState(DashLamp l)
        {
            if (!wasKeyOn) return 0;
            if (Time.time - keyOnAt < BulbCheckSeconds) return Default(l);
            var e = driver.Engine;
            var f = sys ? sys.Faults : default(Fault);
            bool running = sys ? sys.Started : true;
            switch (l)
            {
                case DashLamp.Fuel: return (f & (Fault.NoFuel | Fault.LowFuel | Fault.FuelLeak)) != 0 ? Amber : 0;
                case DashLamp.Brake: return driver.handbrake ? Red : 0;
                case DashLamp.Engine:
                    if (!e) return Red;
                    return (e.TryGetComponent<VehiclePart>(out var ep) && ep.damage > 0.5f) || (f & (Fault.WrongFuel | Fault.Flooded | Fault.Frozen)) != 0 ? Amber : 0;
                case DashLamp.Wheel: return wheelMissing ? Red : wheelFlat ? Amber : 0;
                case DashLamp.Mud: return driver.Mud > 0.5f ? Amber : 0;
                case DashLamp.Oil:
                    if (!sys) return 0;
                    if ((f & (Fault.NoOil | Fault.LowOil | Fault.OilLeak | Fault.Seized)) != 0 || !running) return Red;
                    return sys.OilPressureLow ? Amber : 0;
                case DashLamp.Temp: return (f & Fault.Overheat) != 0 ? Red : (f & (Fault.LowCoolant | Fault.CoolantLeak)) != 0 ? Amber : 0;
                case DashLamp.Service: return (f & (Fault.ServiceDue | Fault.Clogged | Fault.Misfire)) != 0 ? Amber : 0;
                case DashLamp.Charge: return sys && !running ? Red : 0;
                case DashLamp.Cold: return sys && ColdLamp(sys).r > 200 ? Amber : 0;
                case DashLamp.Beam: return lights && lights.On ? Green : 0;
                case DashLamp.FourWD: return driver.awdSelectable && driver.FourWheelDrive ? Green : 0;
                case DashLamp.DiffLock: return driver.diffLocked ? Amber : 0;
                case DashLamp.Fire: return sys && ((f & Fault.OnFire) != 0 || sys.Burning) && Mathf.Repeat(Time.time, 0.5f) < 0.3f ? Red : 0;
                case DashLamp.Air: return air < 5.5f ? Red : 0;
            }
            return 0;
        }

        /// <summary>Any lamp lit.</summary>
        bool Lamps(bool keyOn, bool bulb)
        {
            if (!lights) lights = GetComponent<VehicleLights>();
            bool any = false;
            foreach (var lv in parts.lamps)
            {
                int st = keyOn ? LampState(lv.lamp) : 0;
                any |= st != 0;
                if (st == lv.shown) continue;
                lv.shown = st;
                var col = ((Color)LampCols[(st != 0 ? st : Default(lv.lamp)) - 1]).linear;
                float k = st != 0 ? 2.2f : 0.06f;
                mpb.Clear();
                mpb.SetVector(TintId, new Vector4(col.r * k, col.g * k, col.b * k, 1f));
                lv.renderer.SetPropertyBlock(mpb);
            }
            return any;
        }

        static readonly int TintId = Shader.PropertyToID("_Tint");

        void Illuminate(bool keyOn)
        {
            if (!lights) lights = GetComponent<VehicleLights>();
            float night = MadMax.World.DayNight.Darkness;
            bool back = keyOn && (def.alwaysLit || (lights && lights.On));
            float day = Mathf.Lerp(1f, 0.08f, night);
            var ill = ((Color)def.illumination).linear;
            float b = back ? Mathf.Max(night, 0.15f) * 1.3f : 0f;
            var tint = new Vector4(day + ill.r * b, day + ill.g * b, day + ill.b * b, 1f);
            if ((tint - shownTint).sqrMagnitude > 1e-4f)
            {
                shownTint = tint;
                mpb.Clear(); mpb.SetVector(TintId, tint);
                foreach (var r in parts.illuminated) if (r) r.SetPropertyBlock(mpb);
            }
            float self = keyOn ? Mathf.Lerp(1.5f, 1f, night) : 0.3f;
            var st = new Vector4(self, self, self, 1f);
            if ((st - shownSelf).sqrMagnitude > 1e-4f)
            {
                shownSelf = st;
                mpb.Clear(); mpb.SetVector(TintId, st);
                foreach (var r in parts.selfLit) if (r) r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>COLD lamp: steady while the engine is cold, blinking while diesel glow plugs warm, off once ready.</summary>
        public static Color32 ColdLamp(VehicleSystems sys)
        {
            if (!sys.ColdStart) return LampOff;
            if (sys.GlowPlugsLit) return Mathf.Repeat(Time.time, 0.6f) < 0.35f ? Pal.Amber : LampOff;
            return sys.FuelKind == MadMax.Items.ResourceType.Diesel ? LampOff : Pal.Amber;
        }

        static Color32 LampOff => Pal.Black[2];
    }
}
