using System.Collections.Generic;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.Profiling;
using G = MadMax.Rendering.HudGlyphs.Glyph;

namespace MadMax.Game
{
    /// <summary>HD HUD vehicle instruments: the driving cluster (rev dial with the speed inside, gear, fuel and
    /// temperature arcs, oil / coolant / mud / slip bars, drivetrain chips, warning lamps; boats: throttle and rudder;
    /// submarines: depth, ballast, battery, air), the flight panel (attitude ball, airspeed, altitude, climb, heading,
    /// throttle, rotor, stall) and the damage view (<see cref="HudVehicleView"/>: the vehicle tinted part by part, lost
    /// parts ghosted, hidden engines and empty sockets marked, a status glyph row).</summary>
    public partial class PixelHud
    {
        static readonly Color32 OilCol = new Color32(196, 150, 70, 255), CoolCol = new Color32(90, 200, 190, 255), MudCol = new Color32(150, 98, 60, 255);
        /// <summary>The damage view panel was drawn this frame (tests).</summary>
        public static int LastDamageViewFrame { get; private set; } = -1;
        /// <summary>The HD vehicle cluster was drawn this frame (tests).</summary>
        public static int LastClusterFrame { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHDHooks() { LastDamageViewFrame = -1; LastClusterFrame = -1; MinimapHz = 15f; foreach (var d in iconCache) d.Clear(); iconCacheAt = 0f; }

        /// <summary>The damage view (tests and the acceptance scenario).</summary>
        public HudVehicleView DamageView => hdView;

        // ------------------------------------------------------------------ driving cluster
        void DrawVehiclePanelHD(VehicleDriver car, int x, int yBase)
        {
            Profiler.BeginSample("MadMax.Hud.VehiclePanel");
            LastClusterFrame = Time.frameCount;
            var gs = GameSettings.Current;
            car.TryGetComponent<VehicleSystems>(out var sys);
            car.TryGetComponent<BoatModel>(out var boat);
            float w = 174f, h = 52f, px = x, py = canvas.h - 6f - h;
            canvas.VPanel(px, py, w, h, 3.5f);
            // rev dial with the speed inside
            float cx = px + 27f, cy = py + 26.5f, R = 21f;
            V.Circle(cx, cy, R + 1.6f, FaceDark);
            float maxK = car.Engine ? Mathf.Ceil(car.Engine.maxRpm / 1000f) : 7f;
            float rpm = car.Engine ? Mathf.Clamp01(car.Rpm / (maxK * 1000f)) : 0f, red = car.Engine ? car.Engine.maxRpm * 0.88f / (maxK * 1000f) : 1f;
            V.Arc(cx, cy, R - 1f, 2.4f, 225f, 270f, new Color32(40, 44, 42, 255));
            V.Arc(cx, cy, R - 1f, 2.4f, 225f + 270f * red, 270f * (1f - red), HudVector.Alpha(Red, 120));
            var rc = rpm > red ? Red : rpm > red * 0.78f ? Amber : Green;
            V.Arc(cx, cy, R - 1f, 2.4f, 225f, 270f * rpm, rc);
            int step = maxK > 9f ? 2 : 1;
            for (int k = 0; k <= (int)maxK; k++)
            {
                float a = 225f + 270f * k / maxK;
                var p0 = HudVector.Polar(cx, cy, R - 3.2f, a); var p1 = HudVector.Polar(cx, cy, R - 5.2f, a);
                bool hot = k / maxK > red;
                V.Line(p0.x, p0.y, p1.x, p1.y, 0.45f, hot ? Red : Text);
                if (k % step != 0) continue;
                var lp = HudVector.Polar(cx, cy, R - 8.3f, a);
                string s = NumText(k);
                canvas.TextF(lp.x - PixelCanvas.TextWidthF(s, 0.55f) * 0.5f, lp.y - 1.4f, s, hot ? Red : Dim, 0.55f, false);
            }
            var tip = HudVector.Polar(cx, cy, R - 2.2f, 225f + 270f * rpm); var tail = HudVector.Polar(cx, cy, 3f, 225f + 270f * rpm + 180f);
            V.Line(tail.x, tail.y, tip.x, tip.y, 0.7f, rpm > red ? Red : Amber);
            V.Circle(cx, cy, 1.4f, new Color32(30, 30, 28, 255));
            float kmh = gs.SpeedValue(Mathf.Abs(car.SpeedKmh));
            string sp = NumText(Mathf.RoundToInt(kmh));
            canvas.TextF(cx - PixelCanvas.TextWidthF(sp, 1.9f) * 0.5f, cy - 6.8f, sp, Text, 1.9f);
            canvas.TextF(cx - PixelCanvas.TextWidthF(gs.SpeedUnit, 0.55f) * 0.5f, cy + 4f, gs.SpeedUnit, Dim, 0.55f, false);
            string gear = !car.Engine ? "-" : car.Reversing ? "R" : car.manual && car.Gear == 0 ? "N" : NumText(car.Gear);
            float gy = cy + R - 5.5f;
            V.Circle(cx, gy, 4.3f, new Color32(16, 18, 18, 255));
            V.Ring(cx, gy, 4.3f, 0.5f, Amber);
            canvas.TextF(cx - PixelCanvas.TextWidthF(gear, 1.2f) * 0.5f, gy - 3f, gear, Amber, 1.2f, false);
            canvas.TextF(cx + 5.6f, gy - 1.3f, car.manual ? "M" : "A", Dim, 0.55f, false);

            // drivetrain chips
            float x1 = px + 54f, chipY = py + 3.5f, chx = x1;
            var off = new Color32(14, 18, 18, 220);
            chx += HChip(chx, chipY, car.FourWheelDrive ? "4WD" : "2WD", car.FourWheelDrive ? Text : Dim, car.FourWheelDrive ? HudVector.Alpha(HudVector.Darken(Green, 70), 230) : off, 0.62f) + 1.5f;
            if (car.hasDiffLock) chx += HChip(chx, chipY, "LOCK", car.diffLocked ? new Color32(20, 14, 6, 255) : Dim, car.diffLocked ? Amber : off, 0.62f) + 1.5f;
            if (car.TryGetComponent<VehicleTuning>(out var tn) && (tn.nitrous > 0 || tn.NitrousOn))
                chx += HChip(chx, chipY, tn.NitrousOn ? "NOS!" : "NOS X" + NumText(tn.nitrous), tn.NitrousOn ? new Color32(10, 20, 30, 255) : PaleBlue, tn.NitrousOn ? PaleBlue : off, 0.62f) + 1.5f;
            if (boat) chx += HChip(chx, chipY, boat.Afloat ? "AFLOAT" : "AGROUND", boat.Afloat ? PaleBlue : Amber, off, 0.62f) + 1.5f;

            // fuel and temperature arcs
            VehicleGauges.Reading rd = default;
            if (sys)
            {
                rd = VehicleGauges.Read(sys);
                VehicleGauges.LastHud = rd;
                if (rd.fuelShown) HalfArc(x1 + 11f, py + 24f, 8.5f, rd.fuelNeedle, rd.low ? Red : Amber, G.Fuel, "E", "F", VehicleGauges.LowFuel, true, rd.low && Blink(2f, 0.6f));
                if (rd.tempShown)
                {
                    var tcol = rd.zone == TempZone.Normal ? Amber : VehicleGauges.ZoneColour(rd.zone, Dim);
                    HalfArc(x1 + 33f, py + 24f, 8.5f, rd.tempNeedle, tcol, G.Thermo, "C", "H", VehicleGauges.TempNeedle(VehicleSystems.HotLimit), false, rd.zone == TempZone.Critical && Blink(3f, 0.4f));
                    string tt = TempText(rd.temperature);
                    canvas.TextF(x1 + 33f - PixelCanvas.TextWidthF(tt, 0.55f) * 0.5f, py + 28.6f, tt, Dim, 0.55f, false);
                }
            }
            // bars: oil, coolant, then mud / slip (boats: throttle / rudder)
            float bx = x1 + 48f, bw = 56f;
            if (sys)
            {
                MiniBar(bx, py + 12f, bw, G.Oil, sys.oilInFuel ? 1f : sys.OilFraction, OilCol);
                MiniBar(bx, py + 19f, bw, G.Drop, sys.usesCoolant ? sys.CoolantFraction : 1f, CoolCol);
            }
            if (boat)
            {
                MiniLabel(bx, py + 26f, "THR"); HBar(bx + 10f, py + 26.6f, bw - 10f, 2.4f, Mathf.Abs(car.throttleInput), boat.Reversing ? Red : Amber);
                MiniLabel(bx, py + 32.5f, "RUD");
                float rx = bx + 10f, rw = bw - 10f;
                V.Fill(rx, py + 33.1f, rw, 2.4f, 1.2f, Track);
                V.Line(rx + rw * 0.5f, py + 32.4f, rx + rw * 0.5f, py + 36.2f, 0.3f, Dim);
                V.Circle(rx + rw * (0.5f + 0.5f * Mathf.Clamp(car.steerInput, -1f, 1f)), py + 34.3f, 1.6f, Text);
            }
            else
            {
                MiniLabel(bx, py + 26f, "MUD"); HBar(bx + 10f, py + 26.6f, bw - 10f, 2.4f, car.Mud, MudCol);
                MiniLabel(bx, py + 32.5f, "SLIP"); HBar(bx + 10f, py + 33.1f, bw - 10f, 2.4f, car.WheelSlip, Amber);
            }
            // warning lamps
            float ly = py + 44.5f, lx = x1 + 3f;
            var f = sys ? sys.Faults : Fault.None;
            bool any(Fault m) => (f & m) != 0;
            LampG(ref lx, ly, G.Oil, sys && (sys.OilPressureLow || any(Fault.LowOil | Fault.NoOil)), Red);
            LampG(ref lx, ly, G.Thermo, any(Fault.Overheat) || rd.zone == TempZone.Hot || rd.zone == TempZone.Critical, Red);
            LampG(ref lx, ly, G.Fuel, any(Fault.LowFuel | Fault.NoFuel), Amber);
            LampG(ref lx, ly, G.Engine, any(Fault.Seized | Fault.Misfire | Fault.Clogged | Fault.RoughFuel | Fault.Labouring | Fault.WrongFuel | Fault.ServiceDue | Fault.Flooded), any(Fault.Seized | Fault.Flooded) ? Red : Amber);
            LampG(ref lx, ly, G.Drop, any(Fault.CoolantLeak | Fault.OilLeak | Fault.FuelLeak | Fault.LowCoolant), Amber);
            LampG(ref lx, ly, G.Coil, sys && (sys.ColdStart || sys.GlowPlugsLit), sys && sys.GlowPlugsLit ? Amber : PaleBlue);
            LampG(ref lx, ly, G.Fire, any(Fault.OnFire), Blink(4f, 0.5f) ? Red : Amber);
            LampG(ref lx, ly, G.Lamp, car.TryGetComponent<VehicleLights>(out var vl) && vl.On, Green);
            bool hb = car.handbrake;
            var pc = hb ? Red : Lamp0;
            V.Ring(lx + 3f, ly, 3f, 0.45f, pc);
            canvas.TextF(lx + 1.75f, ly - 1.65f, "P", pc, 0.66f, false);
            // the loudest fault, blinking above the cluster
            var fault = sys ? sys.FaultText() : null;
            if (fault != null && (Time.unscaledTime * 2f) % 2f > 0.6f)
            {
                float fw = PixelCanvas.TextWidthF(fault, 0.9f) + 12f;
                canvas.VPanel(px, py - 13f, fw, 11f, 5.5f, 235);
                HGlyph(G.Engine, px + 5.5f, py - 7.5f, 5f, Red);
                canvas.TextF(px + 9.5f, py - 9.7f, fault, Red, 0.9f, false);
            }
            if (car.TryGetComponent<Submarine>(out var sub)) DrawSubHD(sub, px + w + 4f, py);
            Profiler.EndSample();
        }

        static readonly Color32 PaleBlue = MadMax.Voxel.Pal.PaleBlue[3];

        void MiniLabel(float x, float y, string s) => canvas.TextF(x, y, s, Dim, 0.55f, false);

        void MiniBar(float x, float y, float w, G g, float t, Color32 col)
        {
            HGlyph(g, x + 3f, y + 1.2f, 4.6f, t < 0.15f ? Red : col);
            HBar(x + 10f, y, w - 10f, 2.4f, t, t < 0.15f ? Red : col);
        }

        void LampG(ref float x, float y, G g, bool lit, Color32 col)
        {
            HGlyph(g, x + 3f, y, 5.6f, lit ? col : Lamp0);
            x += 9.2f;
        }

        /// <summary>A small upper half-dial (left = empty) with a needle, end letters and a red band.</summary>
        void HalfArc(float cx, float cy, float r, float t, Color32 needle, G glyph, string lo, string hi, float band, bool bandLow, bool flash)
        {
            V.Arc(cx, cy, r, 1.5f, 270f, 180f, new Color32(40, 44, 42, 255));
            if (bandLow) V.Arc(cx, cy, r, 1.5f, 270f, 180f * band, HudVector.Alpha(Red, 170));
            else V.Arc(cx, cy, r, 1.5f, 270f + 180f * band, 180f * (1f - band), HudVector.Alpha(Red, 170));
            for (int k = 0; k <= 4; k++)
            {
                var a = HudVector.Polar(cx, cy, r - 1.2f, 270f + 45f * k); var b = HudVector.Polar(cx, cy, r - 2.6f, 270f + 45f * k);
                V.Line(a.x, a.y, b.x, b.y, 0.35f, Dim);
            }
            float ang = 270f + 180f * Mathf.Clamp01(t);
            var tip = HudVector.Polar(cx, cy, r - 0.6f, ang);
            V.Line(cx, cy, tip.x, tip.y, 0.6f, flash ? Text : needle);
            V.Circle(cx, cy, 1f, new Color32(30, 30, 28, 255));
            HGlyph(glyph, cx, cy - r * 0.42f, 3.6f, Dim);
            canvas.TextF(cx - r - 1f, cy + 1.2f, lo, Dim, 0.55f, false);
            canvas.TextF(cx + r - 1.2f, cy + 1.2f, hi, bandLow ? Dim : Red, 0.55f, false);
        }

        void DrawSubHD(Submarine sub, float x, float yBottom)
        {
            float w = 74f, h = 34f, y = canvas.h - 6f - h;
            canvas.VPanel(x, y, w, h, 3.5f);
            HGlyph(G.Anchor, x + 6f, y + 6f, 6f, PaleBlue);
            string d = NumText(Mathf.RoundToInt(sub.Depth)) + " M";
            canvas.TextF(x + 11f, y + 3f, d, Text, 1.1f);
            canvas.TextF(x + w - 4f - PixelCanvas.TextWidthF(sub.Snorkel ? "SNORKEL" : "BATTERY", 0.55f), y + 4.2f, sub.Snorkel ? "SNORKEL" : "BATTERY", sub.Snorkel ? Green : Amber, 0.55f, false);
            MiniLabel(x + 3f, y + 12f, "BALLAST"); HBar(x + 30f, y + 12.6f, w - 34f, 2.4f, sub.ballast, PaleBlue);
            MiniLabel(x + 3f, y + 18.5f, "BATT"); HBar(x + 30f, y + 19.1f, w - 34f, 2.4f, sub.BatteryFraction, sub.BatteryFraction < 0.15f ? Red : Green);
            MiniLabel(x + 3f, y + 25f, "AIR"); HBar(x + 30f, y + 25.6f, w - 34f, 2.4f, sub.air / 100f, sub.air < 25f ? Red : new Color32(200, 220, 240, 255));
        }

        // ------------------------------------------------------------------ flight
        void DrawFlightHD(VehicleDriver car, FlightModel f, int x, int yBase)
        {
            Profiler.BeginSample("MadMax.Hud.Flight");
            LastClusterFrame = Time.frameCount;
            var gs = GameSettings.Current;
            float w = 176f, h = 56f, px = x, py = canvas.h - 6f - h;
            canvas.VPanel(px, py, w, h, 3.5f);
            // attitude ball
            float cx = px + 27f, cy = py + 25f, R = 20.5f;
            var fwd = car.transform.forward;
            float pitch = -Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd, car.transform.right);
            var upProj = Vector3.ProjectOnPlane(Vector3.up, fwd);
            float roll = upProj.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(upProj.normalized, car.transform.up, fwd) : 0f;
            float ra = roll * Mathf.Deg2Rad;
            var n = new Vector2(Mathf.Sin(ra), Mathf.Cos(ra));                                     // towards the ground (y down)
            var u = new Vector2(n.y, -n.x);
            const float PxPerDeg = 0.45f;
            float d = Mathf.Clamp(pitch * PxPerDeg, -R, R);
            var c0 = new Vector2(cx, cy); var p0 = c0 + n * d;
            V.Circle(cx, cy, R, new Color32(48, 92, 140, 255));
            // ground: the disc on the far side of the horizon line (convex)
            const int N = 48;
            V.BeginPoly();
            for (int i = 0; i < N; i++)
            {
                var a = HudVector.Polar(cx, cy, R, i * 360f / N); var b = HudVector.Polar(cx, cy, R, (i + 1) * 360f / N);
                float sa = Vector2.Dot(a - p0, n), sb = Vector2.Dot(b - p0, n);
                if (sa >= 0f) V.P(a);
                if ((sa >= 0f) != (sb >= 0f)) V.P(Vector2.Lerp(a, b, sa / (sa - sb)));
            }
            V.EndPoly(new Color32(122, 88, 54, 255));
            if (Mathf.Abs(d) < R)
            {
                float half = Mathf.Sqrt(R * R - d * d);
                V.Line(p0.x - u.x * half, p0.y - u.y * half, p0.x + u.x * half, p0.y + u.y * half, 0.5f, Text);
            }
            for (int k = -2; k <= 2; k++)
            {
                if (k == 0) continue;
                float lo = d - k * 10f * PxPerDeg;
                if (Mathf.Abs(lo) > R - 3f) continue;
                var q = c0 + n * lo; float len = k % 2 == 0 ? 6f : 4f;
                V.Line(q.x - u.x * len, q.y - u.y * len, q.x + u.x * len, q.y + u.y * len, 0.3f, HudVector.Alpha(Text, 200));
            }
            V.Ring(cx, cy, R + 0.6f, 1.5f, new Color32(16, 16, 14, 255));
            for (int k = -2; k <= 2; k++)
            {
                var a = HudVector.Polar(cx, cy, R - 0.4f, k * 30f); var b = HudVector.Polar(cx, cy, R - 2.6f, k * 30f);
                V.Line(a.x, a.y, b.x, b.y, 0.4f, Text);
            }
            var bank = HudVector.Polar(cx, cy, R - 3.2f, -roll);
            V.Tri(bank.x, bank.y, bank.x - 1.4f, bank.y + 2.4f, bank.x + 1.4f, bank.y + 2.4f, Amber);
            V.Line(cx - 11f, cy, cx - 4f, cy, 0.8f, Amber); V.Line(cx + 4f, cy, cx + 11f, cy, 0.8f, Amber);
            V.Line(cx - 4f, cy, cx - 2f, cy + 2f, 0.8f, Amber); V.Line(cx + 4f, cy, cx + 2f, cy + 2f, 0.8f, Amber);
            V.Circle(cx, cy, 0.8f, Amber);
            // slip ball under the dial
            float sby = py + h - 6.3f;
            V.Fill(cx - 11f, sby, 22f, 3.8f, 1.9f, Track);
            V.Line(cx - 2.6f, sby, cx - 2.6f, sby + 3.8f, 0.3f, Dim); V.Line(cx + 2.6f, sby, cx + 2.6f, sby + 3.8f, 0.3f, Dim);
            float ball = Mathf.Clamp(-f.Sideslip * 0.5f, -9f, 9f);
            V.Circle(cx + ball, sby + 1.9f, 1.6f, Mathf.Abs(f.Sideslip) > 8f ? Amber : Text);

            // readouts
            float x1 = px + 54f, x2 = px + 114f;
            MiniLabel(x1, py + 4f, gs.metric ? "AIRSPEED KM/H" : "AIRSPEED MPH");
            canvas.TextF(x1, py + 9f, NumText(Mathf.RoundToInt(gs.SpeedValue(f.Airspeed * 3.6f))), Text, 1.6f);
            MiniLabel(x1, py + 20f, "ALTITUDE M");
            canvas.TextF(x1, py + 25f, NumText(Mathf.RoundToInt(f.Altitude)), f.Altitude < 15f && f.Airborne ? Amber : Text, 1.6f);
            float vs = f.VerticalSpeed;
            MiniLabel(x2, py + 4f, "CLIMB M/S");
            var vc = vs < -6f ? Red : Mathf.Abs(vs) < 0.3f ? Dim : vs > 0f ? Green : Amber;
            if (vs >= 0f) V.Tri(x2 + 2f, py + 13.5f, x2 + 6f, py + 13.5f, x2 + 4f, py + 10f, vc);
            else V.Tri(x2 + 2f, py + 10f, x2 + 6f, py + 10f, x2 + 4f, py + 13.5f, vc);
            canvas.TextF(x2 + 8f, py + 9.4f, ClimbText(vs), vc, 1.1f);
            int hdg = Mathf.RoundToInt(f.Heading) % 360;
            MiniLabel(x2, py + 20f, "HEADING");
            canvas.TextF(x2, py + 25.4f, HeadingText(hdg), Text, 1.1f);
            MiniLabel(x1, py + 39f, "THR");
            HBar(x1 + 11f, py + 39.4f, 50f, 2.8f, f.Throttle, f.Reversing ? Red : Amber);
            float chx = x1 + 64f;
            var off = new Color32(14, 18, 18, 220);
            if (f.Reversing) chx += HChip(chx, py + 38f, "REV", Text, Red, 0.62f) + 1.5f;
            if (f.kind == FlightModel.Kind.Gyro) chx += HChip(chx, py + 38f, "ROTOR " + NumText(Mathf.RoundToInt(f.RotorRpm)), f.RotorRpm < 200f && f.Airborne ? Red : Dim, off, 0.62f) + 1.5f;
            if (f.StallWarning && !f.Stalled && Blink(3f, 0.5f)) HChip(x1, py + 46f, "STALL WARNING", new Color32(20, 14, 6, 255), Amber, 0.66f);
            if (f.Stalled && Blink(3f, 0.65f))
            {
                const string s = "STALL";
                float sw = PixelCanvas.TextWidthF(s, 2f) + 14f;
                canvas.VPanel((canvas.w - sw) * 0.5f, 37f, sw, 15f, 7.5f, 240);
                V.Stroke((canvas.w - sw) * 0.5f, 37f, sw, 15f, 7.5f, 0.8f, Red);
                canvas.TextF((canvas.w - sw) * 0.5f + 7f, 39.5f, s, Red, 2f);
            }
            Profiler.EndSample();
        }

        readonly Dictionary<int, string> climbTexts = new Dictionary<int, string>(), headingTexts = new Dictionary<int, string>();
        string ClimbText(float vs)
        {
            int k = Mathf.RoundToInt(Mathf.Abs(vs) * 10f);
            if (!climbTexts.TryGetValue(k, out var s)) climbTexts[k] = s = (k / 10f).ToString("0.0");
            return s;
        }
        string HeadingText(int hdg)
        {
            if (!headingTexts.TryGetValue(hdg, out var s)) headingTexts[hdg] = s = hdg.ToString("000") + " " + CompassNames[Mathf.RoundToInt(hdg / 45f) % 8];
            return s;
        }

        // ------------------------------------------------------------------ damage view
        float hdViewLastShown;
        VehicleDriver hdNameOf; string hdName;

        /// <summary>On foot: the vehicle you could drive (the damage view of what you are about to take).</summary>
        void DrawNearbyDamageHD()
        {
            if (game.ShowHelp || game.RadialOpen || (game.Menus && game.Menus.IsOpen) || (game.Build && game.Build.Active)) return;
            var v = game.NearbyVehicle;
            if (v) DrawDamageHD(v);
        }

        /// <summary>The damage view panel (bottom right). False when the view can't render (no HD shader).</summary>
        bool DrawDamageHD(VehicleDriver v)
        {
            if (hdView == null || !hdView.Available || !v) return false;
            Profiler.BeginSample("MadMax.Hud.DamageView");
            float W = 104f, H = Mathf.Clamp(canvas.h * 0.27f, 62f, 86f), x = canvas.w - 6f - W, y = canvas.h - 6f - H;
            canvas.VPanel(x, y, W, H, 3.5f);
            float scale = Screen.height / (float)Mathf.Max(1, canvas.h);
            var img = new Rect(x + 3f, y + 11f, W - 6f, H - 30f);
            hdView.Tick(v, Mathf.RoundToInt(img.width * scale), Mathf.RoundToInt(img.height * scale));
            hdViewLastShown = Time.unscaledTime;
            LastDamageViewFrame = Time.frameCount;
            V.Fill(img.x, img.y, img.width, img.height, 2.2f, new Color32(18, 23, 23, 255), new Color32(10, 13, 13, 255), true);
            if (hdView.Ready && hdView.Target == v) { hdViewShown = true; hdViewRect = img; V.hole = img; }
            // header: name and overall condition
            if (hdNameOf != v) { hdNameOf = v; hdName = WastelandGame.Name(v); int max = Mathf.FloorToInt((W - 30f) / (4f * 0.82f)); if (hdName.Length > max) hdName = hdName.Substring(0, max); }
            canvas.TextF(x + 4f, y + 3.3f, hdName, Text, 0.82f, false);
            float sum = 0f, worstTyre = 0f, engine = -1f; int count = 0, missing = 0;
            foreach (var m in hdView.markers)
            {
                sum += m.damage; count++;
                if (m.missing) missing++;
                if (m.cat == PartCategory.Wheel) worstTyre = Mathf.Max(worstTyre, m.damage);
                if (m.cat == PartCategory.Engine) engine = m.missing ? 1f : m.damage;
            }
            v.TryGetComponent<VehicleDamage>(out var vd);
            float frame = vd ? vd.FrameDamage : 0f;
            float cond = 1f - (count > 0 ? (sum + frame) / (count + 1) : frame);
            string ct = Pct(cond);
            canvas.TextF(x + W - 4f - PixelCanvas.TextWidthF(ct, 0.82f), y + 3.3f, ct, HudVehicleView.DamageColour(1f - cond), 0.82f, false);
            // markers: empty sockets ringed, the engine (hidden under the body) as a dot
            foreach (var m in hdView.markers)
            {
                if (m.view.x < 0f || m.view.x > 1f || m.view.y < 0f || m.view.y > 1f) continue;
                float mx = img.x + m.view.x * img.width, my = img.y + (1f - m.view.y) * img.height;
                if (m.missing)
                {
                    if (m.cat != PartCategory.Wheel && m.cat != PartCategory.Engine) continue;     // empty swap targets stay quiet
                    V.Ring(mx, my, 2.4f, 0.5f, Red);
                    V.Line(mx - 1.2f, my - 1.2f, mx + 1.2f, my + 1.2f, 0.4f, Red); V.Line(mx - 1.2f, my + 1.2f, mx + 1.2f, my - 1.2f, 0.4f, Red);
                }
                else if (m.cat == PartCategory.Engine)
                {
                    V.Circle(mx, my, 2.2f, new Color32(10, 10, 8, 230));
                    HGlyph(G.Engine, mx, my, 3.4f, HudVehicleView.DamageColour(m.damage));
                }
            }
            // armour zones (welded plates): F L R B T chips by condition
            if (v.TryGetComponent<VehicleArmor>(out var arm))
            {
                float ax = img.x + 1.5f;
                for (int z = 0; z < 5; z++)
                {
                    if (arm.mat[z] == ArmorMat.None) continue;
                    V.Fill(ax, img.y + img.height - 4f, 6f, 2.6f, 1.3f, HudVehicleView.DamageColour(1f - arm.condition[z]));
                    canvas.TextF(ax + 1.4f, img.y + img.height - 8.6f, ArmourLetters[z], Dim, 0.5f, false);
                    ax += 7.5f;
                }
            }
            // status row
            v.TryGetComponent<VehicleSystems>(out var sys);
            var faults = sys ? sys.Faults : Fault.None;
            float sx = x + 8f, sy = y + H - 13f;
            StatusGlyph(ref sx, sy, G.Engine, engine < 0f ? -1f : engine, (faults & (Fault.Seized | Fault.OnFire)) != 0);
            StatusGlyph(ref sx, sy, G.Tyre, worstTyre, false);
            StatusGlyph(ref sx, sy, G.Frame, frame, false);
            if (sys)
            {
                StatusGlyph(ref sx, sy, G.Fuel, sys.fuelCapacity > 0f ? 1f - sys.FuelFraction : -1f, (faults & Fault.FuelLeak) != 0, true);
                StatusGlyph(ref sx, sy, G.Oil, sys.oilInFuel ? -1f : 1f - sys.OilFraction, (faults & Fault.OilLeak) != 0, true);
                StatusGlyph(ref sx, sy, G.Thermo, sys.usesCoolant ? 1f - sys.CoolantFraction : -1f, (faults & (Fault.Overheat | Fault.CoolantLeak)) != 0, true);
                if ((faults & Fault.OnFire) != 0) StatusGlyph(ref sx, sy, G.Fire, 1f, true);
            }
            if (missing > 0)
            {
                string ms = NumText(missing) + " MISSING";
                canvas.TextF(x + W - 4f - PixelCanvas.TextWidthF(ms, 0.55f), y + 13f, ms, Red, 0.55f, false);
            }
            Profiler.EndSample();
            return true;
        }

        static readonly string[] ArmourLetters = { "F", "L", "R", "B", "T" };

        /// <summary>A status glyph coloured by a 0..1 badness (−1: not fitted), with its value under it; alarm blinks.</summary>
        void StatusGlyph(ref float x, float y, G g, float bad, bool alarm, bool level = false)
        {
            var col = bad < 0f ? Lamp0 : HudVehicleView.DamageColour(level ? Mathf.Clamp01((bad - 0.5f) * 2f) : bad);
            if (alarm && Blink(3f, 0.5f)) col = Red;
            HGlyph(g, x, y, 6.2f, col);
            if (bad >= 0f)
            {
                string s = Pct(1f - bad);
                canvas.TextF(x - PixelCanvas.TextWidthF(s, 0.5f) * 0.5f, y + 4.6f, s, Dim, 0.5f, false);
            }
            x += 14.5f;
        }
    }
}
