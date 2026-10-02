using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Hand containers for liquids (roadmap "Fluids"): equipped like tools (<c>tool_*</c> ids), each holds one
    /// blend at a time up to its capacity. There is no hand drum: a 200 L drum weighs ~190 kg full, so bulk liquid
    /// travels in tankers, pumps and vehicle tanks.</summary>
    public static class FluidContainers
    {
        public sealed class Def
        {
            public string id, name, blurb;
            public float litres, emptyKg;
            /// <summary>What it is meant for (orders the radial); anything still pours into anything.</summary>
            public FluidFamily meant;
        }

        public const string JerryCan = "tool_jerrycan", FuelCan = "tool_fuel_can", Bottle = "tool_bottle", Bucket = "tool_bucket", OilJug = "tool_oil_jug";

        public static readonly Def[] All =
        {
            new Def { id = JerryCan, name = "JERRY CAN 20L", litres = 20f, emptyKg = 4f, meant = FluidFamily.Fuel, blurb = "20 L OF ONE FLUID: SIPHON (K) AND POUR (G) AT TANKS" },
            new Def { id = FuelCan, name = "FUEL CAN 10L", litres = 10f, emptyKg = 1.6f, meant = FluidFamily.Fuel, blurb = "10 L SPOUTED CAN: TOP-UPS, GENERATORS, 2-STROKE MIX" },
            new Def { id = Bottle, name = "WATER BOTTLE 1L", litres = 1f, emptyKg = 0.2f, meant = FluidFamily.Aqueous, blurb = "1 L: DRINK (LMB), FILL AT WATER" },
            new Def { id = Bucket, name = "BUCKET 10L", litres = 10f, emptyKg = 1.2f, meant = FluidFamily.Aqueous, blurb = "10 L OPEN BUCKET: DIP AT WATER (LMB), RADIATORS, FIRES" },
            new Def { id = OilJug, name = "OIL JUG 4L", litres = 4f, emptyKg = 0.4f, meant = FluidFamily.Lube, blurb = "4 L JUG: ENGINE OIL, OIL CHANGES" },
        };

        static Dictionary<string, Def> byId;

        public static Def Get(string id)
        {
            if (id == null) return null;
            if (byId == null) { byId = new Dictionary<string, Def>(); foreach (var d in All) byId[d.id] = d; }
            return byId.TryGetValue(id, out var r) ? r : null;
        }

        public static bool Is(string id) => Get(id) != null;

        /// <summary>kg per litre of a blend (pack weights).</summary>
        public static float Density(FluidMix m)
        {
            if (m == null || m.Empty) return 0f;
            float kg = 0f;
            for (int i = 1; i < ResourceInfo.Count; i++) { var t = (ResourceType)i; if (m[t] > 0f) kg += m[t] * ItemCatalog.ResourceWeight(t); }
            return kg;
        }
    }

    /// <summary>What one container in the pack holds.</summary>
    public sealed class CanContents
    {
        public float litres;
        public readonly FluidMix mix = new FluidMix();
        public bool Empty => litres < 0.01f || mix.Empty;
        public void Clear() { litres = 0f; mix.Clear(); }
        public string Describe(float capacity) => Empty ? "EMPTY /" + capacity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " L"
            : litres.ToString(litres < 10f ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + "/" + capacity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " L " + mix.Label();
    }
}
