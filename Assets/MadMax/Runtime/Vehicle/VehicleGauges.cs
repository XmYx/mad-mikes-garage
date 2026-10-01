using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Engine temperature band on the gauge: cold (below working heat), normal, hot (over
    /// <see cref="VehicleSystems.HotLimit"/>: power cut) and critical (over <see cref="VehicleSystems.CriticalLimit"/>:
    /// the head gasket goes).</summary>
    public enum TempZone { None, Cold, Normal, Hot, Critical }

    /// <summary>Fuel and engine temperature gauges shared by the screen HUD (<see cref="MadMax.Game.PixelHud"/> vehicle
    /// panel) and the in-cabin <see cref="VehicleDashboard"/>: readings from <see cref="VehicleSystems"/>, zones on its
    /// thresholds, pixel drawing in the colour-blind aware palette. Air-cooled engines read their cylinder head; pedal
    /// bikes show neither. The last reading the HUD drew is kept for the acceptance tests.</summary>
    public static class VehicleGauges
    {
        /// <summary>Temperature scale of the needle (°C) and where the cold band ends.</summary>
        public const float ScaleMin = 40f, ScaleMax = 140f, ColdBelow = 60f;
        /// <summary>Tank fraction under which the low-fuel warning shows (matches <see cref="Fault.LowFuel"/>).</summary>
        public const float LowFuel = 0.1f;

        public struct Reading
        {
            public int frame;
            public VehicleSystems sys;
            public bool fuelShown, tempShown, airCooled, low;
            public float fuel, temperature, fuelNeedle, tempNeedle;
            public TempZone zone;
        }

        /// <summary>What the screen HUD drew last (frame stamped).</summary>
        public static Reading LastHud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LastHud = default;

        /// <summary>Pedal power: no tank, no heat.</summary>
        public static bool Pedals(VehicleSystems s)
        {
            var d = s ? s.GetComponent<VehicleDriver>() : null;
            var e = d ? d.Engine : null;
            return e && e.TryGetComponent<VehiclePart>(out var p) && p.partId == "engine_pedals";
        }

        public static float TempNeedle(float celsius) => Mathf.Clamp01(Mathf.InverseLerp(ScaleMin, ScaleMax, celsius));

        public static TempZone Zone(float celsius) =>
            celsius < ColdBelow ? TempZone.Cold : celsius <= VehicleSystems.HotLimit ? TempZone.Normal : celsius <= VehicleSystems.CriticalLimit ? TempZone.Hot : TempZone.Critical;

        public static Reading Read(VehicleSystems s)
        {
            var r = new Reading { frame = Time.frameCount, sys = s, zone = TempZone.None };
            if (!s) return r;
            bool pedals = Pedals(s);
            r.fuelShown = s.fuelCapacity > 0f && !pedals;
            r.tempShown = s.HasEngine && !pedals;
            r.airCooled = !s.usesCoolant;
            r.fuel = Mathf.Clamp01(s.FuelFraction);
            r.fuelNeedle = r.fuel;
            r.low = r.fuelShown && r.fuel < LowFuel;
            r.temperature = s.Temperature;
            r.tempNeedle = TempNeedle(s.Temperature);
            if (r.tempShown) r.zone = Zone(s.Temperature);
            return r;
        }

        static Color32 Cold(Color32 tick) => MadMax.Game.GameSettings.Current.colourBlind ? tick : new Color32(110, 160, 220, 255);

        /// <summary>Colour of a temperature band (cold = blue, or the plain tick colour in the colour-blind palette).</summary>
        public static Color32 ZoneColour(TempZone z, Color32 tick) =>
            z == TempZone.Cold ? Cold(tick) : z == TempZone.Normal ? MadMax.Game.PixelHud.Good : z == TempZone.Hot ? MadMax.Voxel.Pal.Accent : z == TempZone.Critical ? MadMax.Game.PixelHud.Bad : tick;

        static Color32 TempScale(float f, Color32 tick) => ZoneColour(Zone(Mathf.Lerp(ScaleMin, ScaleMax, f)), tick);

        /// <summary>A half-dial (180°, left = empty / cold, right = full / hot) centred on its hub.</summary>
        static void HalfDial(PixelCanvas c, int cx, int cy, int r, float t, bool temp, Color32 tick, Color32 needle)
        {
            int steps = Mathf.Max(10, r * 3);
            for (int k = 0; k <= steps; k++)
            {
                float f = k / (float)steps, a = Mathf.PI * (1f - f);
                var col = temp ? TempScale(f, tick) : f < LowFuel ? MadMax.Game.PixelHud.Bad : tick;
                int px = cx + Mathf.RoundToInt(Mathf.Cos(a) * r), py = cy - Mathf.RoundToInt(Mathf.Sin(a) * r);
                c.Set(px, py, col);
                if (temp && Zone(Mathf.Lerp(ScaleMin, ScaleMax, f)) == TempZone.Critical)               // the danger band is thicker (shape, not only hue)
                    c.Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * (r - 1)), cy - Mathf.RoundToInt(Mathf.Sin(a) * (r - 1)), col);
            }
            for (int q = 0; q <= 4; q++)                                                                      // E ¼ ½ ¾ F
            {
                float f = q / 4f, a = Mathf.PI * (1f - f);
                c.Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * (r - 1)), cy - Mathf.RoundToInt(Mathf.Sin(a) * (r - 1)), tick);
            }
            float na = Mathf.PI * (1f - Mathf.Clamp01(t));
            int tx = cx + Mathf.RoundToInt(Mathf.Cos(na) * (r - 2)), ty = cy - Mathf.RoundToInt(Mathf.Sin(na) * (r - 2));
            c.Line(cx + 1, cy + 1, tx + 1, ty + 1, new Color32(0, 0, 0, 255));
            c.Line(cx, cy, tx, ty, needle);
            c.Rect(cx - 1, cy, 3, 1, tick);
        }

        /// <summary>Fuel half-dial with E / F under its ends; the needle turns red (blinking) on the reserve.</summary>
        public static void DrawFuel(PixelCanvas c, int cx, int cy, int r, Reading rd, Color32 tick, Color32 needle)
        {
            if (!rd.fuelShown) return;
            bool blink = (Time.unscaledTime * 2f) % 1f > 0.4f;
            HalfDial(c, cx, cy, r, rd.fuelNeedle, false, tick, rd.low && blink ? MadMax.Game.PixelHud.Bad : needle);
            c.Text(cx - r - 1, cy + 2, "E", rd.low ? MadMax.Game.PixelHud.Bad : tick, 1, false);
            c.Text(cx + r - 1, cy + 2, "F", tick, 1, false);
        }

        /// <summary>Temperature half-dial with C / H under its ends, banded cold / normal / hot / critical.</summary>
        public static void DrawTemp(PixelCanvas c, int cx, int cy, int r, Reading rd, Color32 tick, Color32 needle)
        {
            if (!rd.tempShown) return;
            var n = rd.zone == TempZone.Hot || rd.zone == TempZone.Critical ? ZoneColour(rd.zone, tick) : needle;
            if (rd.zone == TempZone.Critical && (Time.unscaledTime * 3f) % 1f < 0.4f) n = tick;
            HalfDial(c, cx, cy, r, rd.tempNeedle, true, tick, n);
            c.Text(cx - r - 1, cy + 2, "C", Cold(tick), 1, false);
            c.Text(cx + r - 1, cy + 2, "H", MadMax.Game.PixelHud.Bad, 1, false);
        }

        /// <summary>Upright fuel strip for small dials: the scale (reserve band red) beside the level.</summary>
        public static void DrawFuelBar(PixelCanvas c, int x, int top, int h, Reading rd, Color32 off, Color32 fill)
        {
            if (!rd.fuelShown) return;
            for (int j = 0; j < h; j++)
            {
                float f = 1f - j / (float)(h - 1);
                c.Set(x, top + j, f < LowFuel + 0.02f ? MadMax.Game.PixelHud.Bad : (j % 4 == 0 ? fill : off));
                bool lit = f <= rd.fuelNeedle + 0.001f && rd.fuelNeedle > 0.005f;
                c.Set(x + 1, top + j, lit ? (rd.low ? MadMax.Game.PixelHud.Bad : fill) : off);
            }
        }

        /// <summary>Upright temperature strip: banded scale beside a moving marker.</summary>
        public static void DrawTempBar(PixelCanvas c, int x, int top, int h, Reading rd, Color32 off, Color32 tick)
        {
            if (!rd.tempShown) return;
            int mark = Mathf.RoundToInt((1f - rd.tempNeedle) * (h - 1));
            for (int j = 0; j < h; j++)
            {
                float f = 1f - j / (float)(h - 1);
                c.Set(x, top + j, TempScale(f, tick));
                c.Set(x + 1, top + j, Mathf.Abs(j - mark) <= 0 ? (rd.zone == TempZone.Normal || rd.zone == TempZone.Cold ? tick : ZoneColour(rd.zone, tick)) : off);
            }
            c.Set(x - 1, top + mark, tick);
        }
    }
}
