using UnityEngine;

namespace MadMax.Items
{
    /// <summary>How a liquid behaves out in the open (ground spills, open pools): how fast it evaporates (water = 1,
    /// oils never), how freely it runs and soaks in, the thinnest film it spreads to, whether it burns, when it freezes
    /// and what colour it lies on the ground. Blends weigh their components by share.</summary>
    public static class FluidProps
    {
        /// <summary>Evaporation relative to water (petrol much faster, diesel slower, oils never).</summary>
        public static float Volatility(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Water: case ResourceType.DirtyWater: return 1f;
                case ResourceType.SeaWater: return 0.9f;
                case ResourceType.Fuel: return 12f;
                case ResourceType.Ethanol: return 5f;
                case ResourceType.Diesel: return 0.25f;
                case ResourceType.CrudeOil: return 0.05f;
                case ResourceType.Coolant: return 0.5f;
                case ResourceType.Acid: return 0.6f;
                case ResourceType.Biogas: return 100f;
                default: return 0f;                                     // engine oil, seed oil
            }
        }

        /// <summary>How freely it runs and soaks in (water = 1; oils creep).</summary>
        public static float Fluidity(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Fuel: case ResourceType.Ethanol: return 1.2f;
                case ResourceType.Diesel: return 0.6f;
                case ResourceType.Oil: return 0.2f;
                case ResourceType.SeedOil: return 0.25f;
                case ResourceType.CrudeOil: return 0.1f;
                case ResourceType.Coolant: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>Thinnest layer (mm) it spreads to on flat ground before it stops running.</summary>
        public static float FilmMM(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Fuel: case ResourceType.Ethanol: return 1.5f;
                case ResourceType.Diesel: return 2f;
                case ResourceType.Oil: case ResourceType.SeedOil: return 3f;
                case ResourceType.CrudeOil: return 5f;
                default: return 1f;
            }
        }

        /// <summary>Litres per second a burning pool cell consumes (0 = does not burn).</summary>
        public static float BurnRate(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Fuel: return 1f;
                case ResourceType.Ethanol: return 0.8f;
                case ResourceType.Diesel: return 0.35f;
                case ResourceType.CrudeOil: return 0.3f;
                case ResourceType.Oil: case ResourceType.SeedOil: return 0.2f;
                default: return 0f;
            }
        }

        /// <summary>Freezing point, °C (sea water and coolant lower, fuels never in this climate).</summary>
        public static float FreezesAt(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Water: case ResourceType.DirtyWater: return 0f;
                case ResourceType.SeaWater: return -2f;
                case ResourceType.Coolant: return -30f;
                case ResourceType.Acid: return -15f;
                default: return -60f;
            }
        }

        /// <summary>Colour and opacity on the ground (sRGB).</summary>
        public static Color Colour(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Water: return new Color(0.36f, 0.48f, 0.56f, 0.55f);
                case ResourceType.DirtyWater: return new Color(0.42f, 0.38f, 0.28f, 0.65f);
                case ResourceType.SeaWater: return new Color(0.3f, 0.5f, 0.52f, 0.55f);
                case ResourceType.Fuel: return new Color(0.78f, 0.72f, 0.5f, 0.32f);
                case ResourceType.Ethanol: return new Color(0.8f, 0.82f, 0.8f, 0.25f);
                case ResourceType.Diesel: return new Color(0.55f, 0.42f, 0.16f, 0.5f);
                case ResourceType.Oil: return new Color(0.07f, 0.06f, 0.05f, 0.85f);
                case ResourceType.SeedOil: return new Color(0.75f, 0.6f, 0.2f, 0.6f);
                case ResourceType.CrudeOil: return new Color(0.05f, 0.04f, 0.03f, 0.92f);
                case ResourceType.Coolant: return new Color(0.3f, 0.85f, 0.45f, 0.6f);
                case ResourceType.Acid: return new Color(0.75f, 0.85f, 0.25f, 0.55f);
                default: return new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
        }

        // ------------------------------------------------------------------ blends

        static readonly System.Func<ResourceType, float> volatility = Volatility, fluidity = Fluidity, film = FilmMM, burn = BurnRate;
        public static float Volatility(FluidMix m) => Weighted(m, volatility);
        public static float Fluidity(FluidMix m) => Weighted(m, fluidity);
        public static float FilmMM(FluidMix m) => Weighted(m, film);
        public static float BurnRate(FluidMix m) => Weighted(m, burn);

        /// <summary>Frozen at this temperature (the watery share freezes; a blend that is mostly fuel stays liquid).</summary>
        public static bool Frozen(FluidMix m, float celsius)
        {
            if (m == null || m.Empty) return false;
            float solid = 0f;
            for (int i = 1; i < ResourceInfo.Count; i++) { var t = (ResourceType)i; if (m[t] > 0f && celsius < FreezesAt(t)) solid += m[t]; }
            return solid > 0.6f;
        }

        public static Color Colour(FluidMix m)
        {
            if (m == null || m.Empty) return Color.clear;
            var c = new Color(0, 0, 0, 0);
            for (int i = 1; i < ResourceInfo.Count; i++) { var t = (ResourceType)i; float f = m[t]; if (f > 0f) c += Colour(t) * f; }
            return c;
        }

        static float Weighted(FluidMix m, System.Func<ResourceType, float> f)
        {
            if (m == null || m.Empty) return 0f;
            float s = 0f;
            for (int i = 1; i < ResourceInfo.Count; i++) { var t = (ResourceType)i; if (m[t] > 0f) s += m[t] * f(t); }
            return s;
        }
    }
}
