using UnityEngine;

namespace MadMax.World
{
    /// <summary>How the ground takes a spill (<see cref="Spills"/>): litres per m² the top layer can soak up and how fast
    /// it takes them in, by surface and soil. Asphalt, concrete, runways, rock and bunker floors take nothing; sand
    /// drinks fast and deep, clay slowly, packed dirt roads little; wet ground has less room left, frozen ground almost
    /// none, freshly dug ground takes it faster. Rates are per real second at game pace (a game hour is a minute): clay
    /// takes ~3 mm a game hour, sand ~100.</summary>
    public partial class DeformableTerrain
    {
        /// <summary>Extra wetness from spills at a point (0..1; set by <see cref="Spills"/>): standing or soaked water makes mud.</summary>
        public static System.Func<float, float, float> SpillWet;

        /// <summary>Soak capacity (L/m²) and intake rate (L/m²/s) of the ground at a point, wetness already counted.</summary>
        public void SeepAt(float x, float z, out float capacity, out float rate)
        {
            var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k);
            byte pave = ch.pave != null ? ch.pave[k] : (byte)0, feat = ch.feature != null ? ch.feature[k] : (byte)0;
            capacity = 0f; rate = 0f;
            switch (pave)
            {
                case PaveGravel: capacity = 15f; rate = 1f; break;
                case PaveCobbles: capacity = 3f; rate = 0.02f; break;
                case PavePothole: capacity = 10f; rate = 0.3f; break;
                case 0: break;
                default: return;                                                       // asphalt, concrete, painted
            }
            if (pave == 0)
            {
                if (feat == 1 || feat == 2 || feat == 4 || feat == 5) return;           // rock, bunker floor, runway, runway paint
                if (ch.paved[k] && ch.road[k] > 0.4f) return;                         // the old highways' asphalt
                if (feat == 3) { capacity = 15f; rate = 1f; }                         // tunnel gravel
                else
                    switch ((Biome)ch.biome[k])
                    {
                        case Biome.Desert: capacity = 40f; rate = 1.5f; break;          // sand
                        case Biome.Tropical: capacity = 20f; rate = 0.1f; break;        // laterite
                        case Biome.Nuclear: capacity = 10f; rate = 0.2f; break;         // slag
                        case Biome.Town: case Biome.City: capacity = 15f; rate = 0.3f; break;   // rubble
                        case Biome.Tundra: capacity = 10f; rate = 0.03f; break;
                        default: capacity = 12f; rate = 0.05f; break;                   // clay soils
                    }
                if (ch.road[k] > 0.4f) { capacity *= 0.4f; rate *= 0.3f; }            // packed dirt track
                if (ch.d[k] < -0.05f) rate *= 1.5f;                                   // dug, loose
            }
            float wet = Mathf.Clamp01(ch.wet[k] + Weather.Wetness * 0.75f);
            capacity *= 1f - wet * 0.85f;
            if (Weather.TemperatureAt(z) < -1f) { capacity *= 0.2f; rate *= 0.05f; }  // frozen ground
        }
    }
}
