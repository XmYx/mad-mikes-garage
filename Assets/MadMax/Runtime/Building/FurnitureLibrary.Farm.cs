using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Farm pieces (depth stage B): the irrigation timer and a grain silo.</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> FarmPieces()
        {
            var S = ResourceType.Scrap; var Cu = ResourceType.Copper; var Fe = ResourceType.Iron;
            yield return D("irrigation_timer", "IRRIGATION TIMER", BuildCategory.Utility, TimerBox(), 2, false, go =>
            {
                Node(go, UtilityKind.Water, 0.3f).waterCapacity = 1f;
                go.AddComponent<IrrigationTimer>();
            }, (S, 2), (Cu, 1));
            yield return D("grain_silo", "GRAIN SILO", BuildCategory.Garden, Silo(), 14, true, go => Box(go, "GRAIN SILO", 1200f, false), (Fe, 8), (S, 12));
        }

        /// <summary>A valve box on a stake: a brass dial, two taps, a pipe stub.</summary>
        static VoxelGrid TimerBox()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(0, 0, 0, 0, 7, 0, Pal.Ramp(Pal.Wood, 1, 3501));
            g.Mat(Scrap);
            g.Box(-2, 7, -1, 2, 11, 1, Pal.Weathered(Pal.Olive, 0.3f, 3502, 2, 0));
            g.CylZ(0, 9, 1.4f, 2, 2, Pal.Solid(Pal.Bronze[2])); g.Set(0, 10, 2, Pal.Solid(Pal.Black[0]));   // dial
            foreach (int x in new[] { -2, 2 }) g.Box(x, 5, 0, x, 6, 0, Pal.Ramp(Pal.Metal, 2));             // taps
            g.Box(-1, 3, 0, 1, 3, 0, Pal.Ramp(Pal.Rust, 2));
            return g;
        }

        /// <summary>Corrugated grain silo: a steel bin on legs, conical roof, ladder and a chute.</summary>
        static VoxelGrid Silo()
        {
            var g = new VoxelGrid().Mat(Iron);
            var tin = Pal.Weathered(Pal.Metal, 0.25f, 3511, 2, 0);
            foreach (int x in new[] { -9, 9 }) foreach (int z in new[] { -9, 9 }) g.Box(x, 0, z, x, 8, z, Pal.Ramp(Pal.Metal, 1));
            g.CylY(0, 0, 13f, 9, 40, p => (p.y % 3 == 0) ? Pal.Metal[1] : Pal.Metal[2], 11.6f);          // corrugated wall
            g.CylY(0, 0, 13f, 8, 8, Pal.Ramp(Pal.Metal, 0));                                             // floor
            for (int y = 41; y <= 50; y++) g.CylY(0, 0, 13f - (y - 40) * 1.25f, y, y, tin);              // roof cone
            for (int y = 10; y <= 40; y += 2) g.Box(13, y, -1, 14, y, 1, Pal.Solid(Pal.Metal[3]));       // ladder rungs
            g.Box(13, 10, -2, 14, 40, -2, Pal.Ramp(Pal.Metal, 2)); g.Box(13, 10, 2, 14, 40, 2, Pal.Ramp(Pal.Metal, 2));
            g.Box(-2, 4, -15, 2, 8, -12, Pal.Ramp(Pal.Rust, 2));                                         // chute
            return g;
        }
    }
}
