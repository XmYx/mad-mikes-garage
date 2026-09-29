using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Biome hazards: radiation zones and waste barrels, toxic lakes.</summary>
    public partial class WastelandGame
    {
        public Biome CurrentBiome { get; private set; }
        public float RadiationLevel { get; private set; }

        void UpdateEnvironment()
        {
            if (!terrain || !Player) return;
            var p = Current ? Current.transform.position : Player.transform.position;
            CurrentBiome = terrain.BiomeAt(p.x, p.z);
            float rad = terrain.World.Radiation(p.x, p.z) * 0.6f;
            foreach (var h in Hazard.All)
            {
                if (!h) continue;
                float d = Vector3.Distance(h.transform.position, p);
                if (d < h.radius) rad += h.radiation * (1f - d / h.radius);
            }
            // a submarine's pressure hull (at the helm or walking its cabin) or a sea-base air pocket keeps the sea out
            bool pressureHull = Current ? Current.TryGetComponent<MadMax.Vehicles.VehicleSystems>(out var hull) && hull.sealedHull
                              : (Player.Interior && Player.Interior.airtight) || MadMax.Building.AirPocket.Contains(p + Vector3.up * 1.5f);
            float lvl = terrain.WaterLevel(p.x, p.z);
            if (!float.IsNaN(lvl) && CurrentBiome == Biome.Nuclear && p.y < lvl && !pressureHull) rad += 0.8f;   // contaminated water on the skin
            if (Storms.Rad > 0.01f) rad += Storms.Rad * 1.2f * (Sheltered || Player.Interior ? 0.15f : 1f);   // radiation storm: under a roof or out
            if (pressureHull) rad *= 0.08f;                                            // steel and sea shield a lot
            else if (Current) rad *= 0.35f;                                            // the cab a little
            rad *= 1f - Stats.Level(Skill.Survival) * 0.05f;
            rad *= RadiationPassed;                                                   // hazmat suit, gas mask
            RadiationLevel = rad;
            if (rad > 0.02f && Vitals)
            {
                Vitals.Hurt(rad * 2.2f * Time.deltaTime, "RADIATION");
                Stats.Practice(Skill.Survival, rad * Time.deltaTime * 0.6f);
            }
        }
    }
}
