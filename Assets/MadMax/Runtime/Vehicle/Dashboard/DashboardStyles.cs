using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>The instrument cluster catalogue: four generic styles — <see cref="Classic"/> (analog sedan / coupe:
    /// big speedo + tacho, fuel / temperature, chrome bezels, warm amber backlight), <see cref="Heavy"/> (truck, bus and
    /// machine panel: many round gauges, hour meter, switch row), <see cref="Sport"/> (central tacho with digital speed,
    /// shift lights; buggies, bikes) and <see cref="Rugged"/> (military / offroad: square gauges, toggles, green
    /// backlight) — plus vehicle-specific ones registered in <see cref="Customs"/> (partial files
    /// <c>DashboardStyles.&lt;Name&gt;.cs</c>). <see cref="Pick"/> chooses one per vehicle; a design can force one with
    /// <see cref="VehicleDesign.dashboard"/>.</summary>
    public static partial class DashboardStyles
    {
        public const string Classic = "classic", Heavy = "heavy", HeavySlow = "heavy_slow", Sport = "sport", Rugged = "rugged";

        static readonly Dictionary<string, DashboardStyle> built = new Dictionary<string, DashboardStyle>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => built.Clear();

        /// <summary>Vehicle-specific clusters by style id (usually the design name). Add an entry from a partial file.</summary>
        static readonly Dictionary<string, System.Func<DashboardStyle>> Customs = new Dictionary<string, System.Func<DashboardStyle>>();

        static DashboardStyles() { RegisterCustoms(Customs); }

        /// <summary>Implemented in <c>DashboardStyles.Custom.cs</c>: fills the custom table.</summary>
        static partial void RegisterCustoms(Dictionary<string, System.Func<DashboardStyle>> table);

        /// <summary>The style with this id (built once); unknown ids fall back to <see cref="Classic"/>.</summary>
        public static DashboardStyle Get(string id)
        {
            if (string.IsNullOrEmpty(id)) id = Classic;
            if (built.TryGetValue(id, out var s)) return s;
            switch (id)
            {
                case Classic: s = MakeClassic(); break;
                case Heavy: s = MakeHeavy(120f); break;
                case HeavySlow: s = MakeHeavy(60f); break;
                case Sport: s = MakeSport(); break;
                case Rugged: s = MakeRugged(); break;
                default: s = Customs.TryGetValue(id, out var f) ? f() : null; break;
            }
            if (s == null) return id == Classic ? null : Get(Classic);
            s.id = id;
            built[id] = s;
            return s;
        }

        public static bool Exists(string id) => id == Classic || id == Heavy || id == HeavySlow || id == Sport || id == Rugged || (id != null && Customs.ContainsKey(id));

        /// <summary>The design name of a spawned vehicle ("Sedan(Clone)" → "Sedan").</summary>
        public static string DesignName(string objectName)
        {
            int k = objectName.IndexOf('(');
            return (k > 0 ? objectName.Substring(0, k) : objectName).Trim();
        }

        /// <summary>The cluster for a vehicle: a custom one named after its design, else by roster name, then by what it
        /// is (machine, moving base, bike, boat, aircraft, mass). <paramref name="scale"/> shrinks it for bikes.</summary>
        public static string Pick(VehicleDriver v, out float scale)
        {
            scale = 1f;
            string n = DesignName(v.name);
            if (Customs.ContainsKey(n)) return n;
            if (v.GetComponent<BikeBalance>()) { scale = 0.62f; return Sport; }
            switch (n)
            {
                case "Interceptor": case "Coupe": case "DuneBuggy": case "MonsterTruck": return Sport;
                case "Ultralight": case "Gyrocopter": scale = 0.8f; return Sport;
                case "APC": case "Scavenger": return Rugged;
                case "Hauler": case "Semi": case "Bus": case "TowTruck": case "Wrecker": case "Ambulance":
                case "MagnetCrane": case "CarCrusher": case "Trawler": case "Houseboat": case "IronEel": return Heavy;
                case "Sedan": case "Wagon": case "Pickup": case "Trabant": return Classic;
            }
            if (v.GetComponent<Machine>() || v.GetComponent<RigVehicle>()) return HeavySlow;
            if (v.GetComponent<MagnetCrane>() || v.GetComponent<GrappleCrusher>() || v.GetComponent<BoatModel>()) return Heavy;
            if (v.GetComponent<FlightModel>()) { scale = 0.8f; return Sport; }
            var rb = v.GetComponent<Rigidbody>();
            if (rb && rb.mass > 3500f) return Heavy;
            return Classic;
        }

        static readonly DashLamp[] AllLamps =
        {
            DashLamp.Charge, DashLamp.Oil, DashLamp.Brake, DashLamp.Engine, DashLamp.Temp, DashLamp.Fuel, DashLamp.Cold,
            DashLamp.Beam, DashLamp.Service, DashLamp.Wheel, DashLamp.Mud, DashLamp.Fire, DashLamp.FourWD, DashLamp.DiffLock,
        };

        // ------------------------------------------------------------------ (a) classic analog
        static DashboardStyle MakeClassic()
        {
            var s = new DashboardStyle
            {
                size = new Vector2(0.34f, 0.135f), visor = 0.04f, housing = Pal.Black,
                face = Pal.Black[0], ink = Pal.Cream[3], minorInk = Pal.Cream[1], needle = Pal.Amber, red = Pal.Crimson[4], digit = Pal.LightY,
                bezel = BezelKind.Chrome, illumination = Pal.LightY,
            };
            var sp = s.Dial(GaugeSource.Speed, -0.088f, 0.006f, 0.052f, 0f, 200f); sp.sweep = 260f; sp.labels = 10;
            var rp = s.Dial(GaugeSource.Rpm, 0.088f, 0.006f, 0.052f, 0f, 0f); rp.sweep = 260f;
            var fu = s.Dial(GaugeSource.Fuel, 0f, 0.03f, 0.022f, 0f, 1f); fu.sweep = 100f; fu.ends = "EF"; fu.redBelow = VehicleGauges.LowFuel;
            var te = s.Dial(GaugeSource.Temp, 0f, -0.022f, 0.022f, VehicleGauges.ScaleMin, VehicleGauges.ScaleMax); te.sweep = 100f; te.ends = "CH"; te.redAbove = VehicleSystems.HotLimit;
            var od = s.Add(new GaugeDef(GaugeKind.Drum, GaugeSource.Odometer, -0.088f, -0.029f, 0.0055f)); od.digits = 6; od.decimals = 1;
            s.Add(new GaugeDef(GaugeKind.Digital, GaugeSource.Gear, 0.088f, -0.026f, 0.012f));
            s.LampGrid(AllLamps, -0.0813f, -0.058f, 0.0125f, 0f, 14);
            return s;
        }

        // ------------------------------------------------------------------ (b) heavy truck / bus / machine panel
        static DashboardStyle MakeHeavy(float topKmh)
        {
            var s = new DashboardStyle
            {
                size = new Vector2(0.52f, 0.2f), depth = 0.04f, housing = Pal.Metal,
                face = Pal.Black[0], ink = Pal.Cream[4], minorInk = Pal.Cream[2], needle = Pal.LightW, red = Pal.Crimson[4], digit = Pal.Amber,
                bezel = BezelKind.Black, illumination = Pal.LightW, rivets = true,
            };
            var sp = s.Dial(GaugeSource.Speed, -0.115f, 0.02f, 0.055f, 0f, topKmh); sp.sweep = 250f; sp.labels = topKmh > 80f ? 12 : 6;
            var rp = s.Dial(GaugeSource.Rpm, 0.115f, 0.02f, 0.055f, 0f, 0f); rp.sweep = 250f;
            var oil = s.Dial(GaugeSource.OilPressure, -0.215f, 0.045f, 0.026f, 0f, 6f); oil.sweep = 240f; oil.labels = 3; oil.redBelow = VehicleSystems.OilLampBelow; oil.caption = "OIL";
            var te = s.Dial(GaugeSource.Temp, -0.215f, -0.018f, 0.026f, 40f, 140f); te.sweep = 240f; te.labels = 2; te.redAbove = VehicleSystems.HotLimit; te.caption = "TEMP";
            var fu = s.Dial(GaugeSource.Fuel, 0.215f, 0.045f, 0.026f, 0f, 1f); fu.sweep = 240f; fu.ends = "EF"; fu.redBelow = VehicleGauges.LowFuel; fu.caption = "FUEL";
            var vo = s.Dial(GaugeSource.Volts, 0.215f, -0.018f, 0.026f, 16f, 32f); vo.sweep = 240f; vo.labels = 2; vo.redBelow = 22f; vo.redAbove = 30f; vo.caption = "VOLT";
            var air = s.Dial(GaugeSource.Air, 0f, 0.055f, 0.024f, 0f, 10f); air.sweep = 240f; air.labels = 2; air.redBelow = 5.5f; air.caption = "AIR";
            var bo = s.Dial(GaugeSource.Boost, 0f, -0.004f, 0.024f, 0f, 2f); bo.sweep = 240f; bo.labels = 2; bo.caption = "TURBO";
            var hr = s.Add(new GaugeDef(GaugeKind.Drum, GaugeSource.Hours, 0f, -0.044f, 0.0055f)); hr.digits = 6; hr.decimals = 1; hr.caption = "HOURS";
            var od = s.Add(new GaugeDef(GaugeKind.Drum, GaugeSource.Odometer, -0.115f, -0.017f, 0.005f)); od.digits = 6; od.decimals = 1;
            s.Add(new GaugeDef(GaugeKind.Digital, GaugeSource.Gear, 0.115f, -0.004f, 0.012f));
            var set = new DashLamp[AllLamps.Length + 1];
            AllLamps.CopyTo(set, 0); set[set.Length - 1] = DashLamp.Air;
            s.LampGrid(set, -0.168f, -0.064f, 0.024f, 0f, set.Length);
            string[] labels = { "LIGHTS", "BEACON", "WIPER", "FAN", "PTO", "EXH BRK", "DIFF", "HORN" };
            for (int i = 0; i < labels.Length; i++) s.Switch(-0.175f + i * 0.05f, -0.082f, i == 7 ? 3 : i == 3 ? 2 : 1, labels[i]);
            return s;
        }

        // ------------------------------------------------------------------ (c) sport / buggy / bike
        static DashboardStyle MakeSport()
        {
            var s = new DashboardStyle
            {
                size = new Vector2(0.30f, 0.15f), visor = 0.035f, housing = Pal.Black,
                face = Pal.Black[0], ink = Pal.Cream[4], minorInk = Pal.Cream[1], needle = Pal.Crimson[4], red = Pal.Crimson[4], digit = Pal.Amber,
                bezel = BezelKind.Chrome, illumination = Pal.Crimson[4], alwaysLit = true, sweep = true,
            };
            var rp = s.Dial(GaugeSource.Rpm, 0f, -0.008f, 0.056f, 0f, 0f); rp.sweep = 270f;
            rp.face = Pal.Cream[3]; rp.ink = Pal.Black[0];
            var sp = s.Add(new GaugeDef(GaugeKind.Digital, GaugeSource.Speed, 0f, -0.038f, 0.013f)); sp.digits = 3; sp.ink = Pal.Black[1];
            var sl = s.Add(new GaugeDef(GaugeKind.Leds, GaugeSource.ShiftLights, 0f, 0.064f, 0.09f)); sl.digits = 7;
            var fu = s.Add(new GaugeDef(GaugeKind.Bar, GaugeSource.Fuel, -0.115f, 0.016f, 0.05f)); fu.digits = 8; fu.min = 0f; fu.max = 1f; fu.redBelow = VehicleGauges.LowFuel; fu.caption = "F";
            var te = s.Add(new GaugeDef(GaugeKind.Bar, GaugeSource.Temp, -0.093f, 0.016f, 0.05f)); te.digits = 8; te.min = VehicleGauges.ScaleMin; te.max = VehicleGauges.ScaleMax; te.redAbove = VehicleSystems.HotLimit; te.caption = "T";
            s.Add(new GaugeDef(GaugeKind.Digital, GaugeSource.Gear, 0.104f, 0.025f, 0.024f));
            var od = s.Add(new GaugeDef(GaugeKind.Drum, GaugeSource.Odometer, 0.104f, -0.008f, 0.0048f)); od.digits = 6; od.decimals = 1;
            var left = new[] { DashLamp.Charge, DashLamp.Oil, DashLamp.Brake, DashLamp.Engine, DashLamp.Temp, DashLamp.Fuel, DashLamp.Cold };
            var right = new[] { DashLamp.Beam, DashLamp.Service, DashLamp.Wheel, DashLamp.Mud, DashLamp.Fire, DashLamp.FourWD, DashLamp.DiffLock };
            s.LampGrid(left, -0.137f, -0.034f, 0.0175f, 0.018f, 4);
            s.LampGrid(right, 0.0845f, -0.034f, 0.0175f, 0.018f, 4);
            return s;
        }

        // ------------------------------------------------------------------ (d) rugged military / offroad
        static DashboardStyle MakeRugged()
        {
            var s = new DashboardStyle
            {
                size = new Vector2(0.40f, 0.17f), depth = 0.045f, housing = Pal.RigGreen,
                face = Pal.Black[0], ink = Pal.Cream[2], minorInk = Pal.Cream[0], needle = Pal.Cream[4], red = Pal.Crimson[3], digit = Pal.Moss[4],
                bezel = BezelKind.Square, illumination = Pal.Moss[4], rivets = true, lampSize = 0.0095f,
            };
            var sp = s.Dial(GaugeSource.Speed, -0.13f, 0.025f, 0.045f, 0f, 140f); sp.square = true; sp.sweep = 240f; sp.labels = 7;
            var rp = s.Dial(GaugeSource.Rpm, -0.022f, 0.025f, 0.045f, 0f, 0f); rp.square = true; rp.sweep = 240f;
            var fu = s.Dial(GaugeSource.Fuel, 0.072f, 0.047f, 0.022f, 0f, 1f); fu.square = true; fu.sweep = 180f; fu.ends = "EF"; fu.redBelow = VehicleGauges.LowFuel; fu.caption = "FUEL";
            var te = s.Dial(GaugeSource.Temp, 0.128f, 0.047f, 0.022f, VehicleGauges.ScaleMin, VehicleGauges.ScaleMax); te.square = true; te.sweep = 180f; te.ends = "CH"; te.redAbove = VehicleSystems.HotLimit; te.caption = "TEMP";
            var oil = s.Dial(GaugeSource.OilPressure, 0.072f, 0f, 0.022f, 0f, 6f); oil.square = true; oil.sweep = 180f; oil.labels = 3; oil.redBelow = VehicleSystems.OilLampBelow; oil.caption = "OIL";
            var vo = s.Dial(GaugeSource.Volts, 0.128f, 0f, 0.022f, 16f, 32f); vo.square = true; vo.sweep = 180f; vo.labels = 2; vo.redBelow = 22f; vo.redAbove = 30f; vo.caption = "VOLT";
            var od = s.Add(new GaugeDef(GaugeKind.Drum, GaugeSource.Odometer, -0.13f, -0.006f, 0.005f)); od.digits = 6; od.decimals = 1;
            s.Add(new GaugeDef(GaugeKind.Digital, GaugeSource.Gear, -0.022f, -0.006f, 0.011f));
            s.LampGrid(AllLamps, -0.172f, -0.042f, 0.0146f, 0f, 14);
            string[] labels = { "MASTER", "BLACKOUT", "LIGHTS", "PUMP", "WINCH", "HEATER" };
            for (int i = 0; i < labels.Length; i++) s.Switch(-0.165f + i * 0.045f, -0.066f, 0, labels[i]);
            s.Switch(0.155f, -0.058f, 2, "START");
            s.Switch(0.11f, -0.066f, 3, "HORN");
            return s;
        }
    }
}
