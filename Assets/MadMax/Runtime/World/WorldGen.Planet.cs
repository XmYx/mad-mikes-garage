using UnityEngine;

namespace MadMax.World
{
    /// <summary>The world as a planet (user additions; the "planet illusion"): continents and oceans from a large-scale
    /// continent noise, biomes spread by latitude (wet equator, dry subtropics — the start sits at 31° N in the desert
    /// band —, temperate forests, tundra and polar ice), an ice wall at both poles, and the date line: x wraps every
    /// <see cref="Circumference"/> metres through an open-ocean band (<see cref="Meridian"/>), where the game moves you
    /// round to the other side (<see cref="MadMax.Game.WastelandGame"/> PlanetWrap). Nothing is built in that band, so
    /// roads, towns and sites never straddle the seam. The horizon curves slightly in the shaders (`_MadMaxCurve`).</summary>
    public partial class WorldGen
    {
        public const float Circumference = 9600f, HalfX = Circumference * 0.5f;
        /// <summary>z of the equator and the distance from it to either pole.</summary>
        public const float ZEquator = -1100f, PoleSpan = 3200f;
        public const float SeaLevel = -7f;
        /// <summary>Half-width of the open ocean along the date line (x = ±HalfX).</summary>
        public const float Meridian = 520f;
        /// <summary>Radius the shaders bend the horizon with (m): ~15 m drop at 600 m.</summary>
        public const float CurveRadius = 12000f;

        public static float Latitude(float z) => Mathf.Clamp((z - ZEquator) / PoleSpan * 90f, -90f, 90f);
        public static float ZOfLatitude(float lat) => ZEquator + lat / 90f * PoleSpan;
        public static float ZNorth => ZOfLatitude(90f);
        public static float ZSouth => ZOfLatitude(-90f);
        /// <summary>x folded into [-HalfX, HalfX).</summary>
        public static float WrapX(float x) => x - Mathf.Floor((x + HalfX) / Circumference) * Circumference;

        /// <summary>Mean air temperature offset of a latitude against the start's (°C): colder towards the poles,
        /// warmer at the equator.</summary>
        public static float ClimateOffset(float z)
        {
            float T(float lat) => 30f - 0.0085f * lat * lat;
            return T(Latitude(z)) - T(Latitude(0f));
        }

        /// <summary>Seasons flip south of the equator and fade near it (multiplier on the season's swing).</summary>
        public static float SeasonSign(float z) { float lat = Latitude(z); return Mathf.Sign(lat) * Mathf.Clamp01(Mathf.Abs(lat) / 30f); }

        /// <summary>0..1 continental noise; 0.5 is the coastline. The start continent is guaranteed, the date line is open
        /// sea, the polar caps are land (ice sheets).</summary>
        public float ContinentNoise(float x, float z)
        {
            x = WrapX(x);
            float c = Mathf.PerlinNoise(x * 0.00045f + o[5].y * 0.37f + 91f, z * 0.00045f + o[6].x * 0.41f + 13f) * 0.62f
                    + Mathf.PerlinNoise(x * 0.0012f + o[1].y + 7f, z * 0.0012f + o[2].x - 3f) * 0.26f
                    + Mathf.PerlinNoise(x * 0.0035f + o[3].y - 5f, z * 0.0035f + o[0].x + 11f) * 0.12f;
            float start = Mathf.Clamp01(1f - new Vector2(x, z).magnitude / 1100f);
            c += start * start * 0.35f;
            float seam = Mathf.Clamp01((HalfX - Mathf.Abs(x) - Meridian) / 600f);
            c = Mathf.Lerp(0.3f, c, seam * seam * (3f - 2f * seam));
            float lat = Mathf.Abs(Latitude(z));
            if (lat > 84f && seam > 0.5f) c = Mathf.Max(c, 0.5f + (lat - 84f) * 0.04f);
            return c;
        }

        /// <summary>Open sea here (below the coastline noise).</summary>
        public bool Ocean(float x, float z) => ContinentNoise(x, z) < 0.5f;

        /// <summary>Somewhere things may be placed: inland, off the ice and away from the date line.</summary>
        public bool Habitable(float x, float z, float inland = 0.58f)
        {
            float lat = Mathf.Abs(Latitude(z));
            return lat < 66f && Mathf.Abs(WrapX(x)) < HalfX - Meridian - 300f && ContinentNoise(x, z) > inland;
        }

        /// <summary>The coast profile: land relief above a beach, the shelf and the deep sea below it (continuous at the
        /// coastline), the ice wall at the poles.</summary>
        float PlanetHeight(float x, float z, float land)
        {
            float c = ContinentNoise(x, z);
            float h;
            if (c >= 0.5f)
            {
                float inland = Mathf.Clamp01((c - 0.5f) / 0.07f);
                inland = inland * inland * (3f - 2f * inland);
                float up = Mathf.Max(land, SeaLevel + 1.2f + (c - 0.5f) * 40f);                           // coasts rise out of the sea
                h = Mathf.Lerp(SeaLevel - 1f, up, inland);
            }
            else
            {
                float deep = Mathf.Clamp01((0.5f - c) / 0.18f);
                float seabed = SeaLevel - 2f - 38f * deep * deep * (3f - 2f * deep);
                float shelf = Mathf.Clamp01((0.5f - c) / 0.08f);
                h = Mathf.Lerp(SeaLevel - 1f, seabed, shelf * shelf * (3f - 2f * shelf));
            }
            // the ice wall at both poles (where the old world had its rim)
            float wall = Mathf.Max(z - (ZNorth - 140f), (ZSouth + 140f) - z);
            if (wall > 0f) h = Mathf.Max(h, SeaLevel + 4f) + wall * wall * 0.02f;
            return h;
        }

        /// <summary>Sea water and its shore band for <see cref="Sample"/> (inland lakes are separate).</summary>
        void SeaAt(float x, float z, float h, ref GroundSample s, ref float wet)
        {
            if (ContinentNoise(x, z) > 0.58f) return;
            if (h < SeaLevel + 1.8f)
            {
                s.water = SeaLevel;
                s.shore = Mathf.Max(s.shore, Mathf.Clamp01(1f - Mathf.Abs(h - SeaLevel) / 1.4f));
                wet = Mathf.Max(wet, Mathf.Clamp01(1f - (h - SeaLevel) / 1.8f));
            }
        }

        /// <summary>Climate biome by latitude: tundra near the poles, a wet equator, dry subtropics, wet temperate belts.</summary>
        Biome ClimateBiome(float x, float z, float moistNoise, float heatNoise, float start)
        {
            float lat = Mathf.Abs(Latitude(z));
            if (lat > 74f + (heatNoise - 0.5f) * 6f) return Biome.Tundra;
            float band = lat < 12f ? 0.18f
                       : lat < 35f ? -0.16f * Mathf.Clamp01((lat - 12f) / 8f) * Mathf.Clamp01((35f - lat) / 8f)
                       : lat < 60f ? 0.12f : 0.05f;
            float moist = moistNoise + band - start * 0.5f;
            float heat = heatNoise * 0.35f + Mathf.Clamp01(1f - lat / 60f) * 0.65f;
            if (moist > 0.56f) return heat > 0.55f ? Biome.Tropical : Biome.Forest;
            return Biome.Desert;
        }
    }
}
