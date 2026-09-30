using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Cooking ladder pieces (depth stage A): a campfire with a spit (first tier, no workshop needed), the
    /// powered kitchen range (triple batches, finer food) and the cannery (tins that never spoil).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> KitchenPieces()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var St = ResourceType.Stone;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var G = ResourceType.Glass;
            yield return D("campfire", "CAMPFIRE", BuildCategory.Furniture, CampfireGrid(), 3, false, go =>
            {
                Station(go, "campfire", "COOK (CAMPFIRE)", 0f).output = new Vector3(0f, 0.75f, 0f);
                Glow(go, new Vector3(0f, 0.4f, 0f), new Color(1f, 0.55f, 0.22f), 6f, 2.2f, false, 0f);
                var cl = go.AddComponent<Climate>(); cl.burnsWood = true; cl.heat = 10f; cl.radius = 5f; cl.on = false;
            }, (St, 6), (W, 4));
            yield return D("kitchen_range", "KITCHEN RANGE", BuildCategory.Furniture, RangeGrid(), 6, false, go =>
            {
                Node(go, UtilityKind.Power, 0.6f);
                var st = Station(go, "range", "COOK (KITCHEN RANGE)", 2400f);
                st.tier = 1f; st.output = new Vector3(0f, 1.0f, 0.1f);
            }, (Fe, 6), (Cu, 3), (G, 1), (S, 4));
            yield return D("cannery", "CANNERY", BuildCategory.Industry, CanneryGrid(), 8, false, go =>
            {
                Node(go, UtilityKind.Power, 0.7f);
                var st = Station(go, "cannery", "CANNERY (TINS AND BOTTLES)", 1500f);
                st.tier = 0.5f; st.output = new Vector3(0.9f, 0.95f, 0f);
            }, (Fe, 8), (Cu, 2), (S, 8), (G, 2));
        }

        /// <summary>Stone ring, crossed logs over embers, a green-wood spit on forked posts with a pot hanging.</summary>
        static VoxelGrid CampfireGrid()
        {
            var g = new VoxelGrid().Mat(Stone);
            var stone = Pal.Weathered(Pal.Sand, 0.1f, 3101, 1, 0);
            for (int a = 0; a < 14; a++)
            {
                float t = a / 14f * Mathf.PI * 2f;
                int x = Mathf.RoundToInt(Mathf.Cos(t) * 5.5f), z = Mathf.RoundToInt(Mathf.Sin(t) * 5.5f);
                g.Box(x, 0, z, x + (a % 3 == 0 ? 1 : 0), a % 2 == 0 ? 2 : 1, z, stone);
            }
            g.Box(-3, 0, -3, 3, 0, 3, Pal.Ramp(Pal.Black, 1, 3102));                              // ash bed
            g.Box(-1, 1, -1, 1, 1, 1, p => (p.x + p.z) % 2 == 0 ? Pal.Amber : Pal.LightY);         // embers
            g.Mat(Wood);
            var log = Pal.Ramp(Pal.Wood, 1, 3103);
            g.Box(-4, 1, 0, 4, 1, 0, log); g.Box(0, 2, -4, 0, 2, 4, log);                          // crossed logs
            g.Set(-4, 1, 0, Pal.Ramp(Pal.Wood, 3)); g.Set(0, 2, 4, Pal.Ramp(Pal.Wood, 3));         // cut ends
            g.Set(0, 3, 0, Pal.Solid(Pal.Amber)); g.Set(1, 3, 0, Pal.Solid(Pal.LightY)); g.Set(0, 4, 0, Pal.Solid(Pal.Amber));   // flames
            foreach (int x in new[] { -7, 7 })
            {
                g.Box(x, 0, 0, x, 8, 0, Pal.Ramp(Pal.Wood, 2, 3104));                             // forked posts
                g.Set(x, 9, -1, Pal.Ramp(Pal.Wood, 2)); g.Set(x, 9, 1, Pal.Ramp(Pal.Wood, 2));
            }
            g.Box(-8, 9, 0, 8, 9, 0, Pal.Ramp(Pal.Wood, 3, 3105));                                // spit
            g.Mat(Scrap);
            g.Box(2, 4, 0, 2, 8, 0, Pal.Solid(Pal.Chrome[1]));                                     // pot hook
            g.CylY(2, 0, 1.6f, 3, 5, p => p.y == 3 ? Pal.Black[1] : Pal.Metal[1]);                 // billy can
            g.Box(-3, 7, 0, -1, 8, 0, Pal.Ramp(Pal.Crimson, 1, 3106));                             // meat on the spit
            return g;
        }

        /// <summary>Steel range: four burners, a glass-doored oven, knobs, a splashback with a hood and a pan.</summary>
        static VoxelGrid RangeGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Chrome, 0.12f, 3111, 1, 0);
            g.Box(-7, 0, -4, 7, 10, 4, steel);
            g.Box(-7, 0, 4, 7, 0, 4, Pal.Solid(Pal.Black[0]));                                     // kick plate
            g.Mat(Glass); g.Box(-5, 2, 4, 5, 6, 4, p => p.y == 6 || p.x == -5 || p.x == 5 ? Pal.Chrome[1] : Pal.Glass[1]);  // oven door
            g.Set(0, 3, 4, Pal.Solid(Pal.Amber));                                                   // oven light
            g.Mat(Scrap);
            g.Box(-5, 8, 4, 5, 8, 4, Pal.Solid(Pal.Chrome[3]));                                    // handle rail
            for (int i = 0; i < 4; i++) g.Set(-5 + i * 3, 9, 4, Pal.Solid(Pal.Black[1]));           // knobs
            foreach (int x in new[] { -4, 4 }) foreach (int z in new[] { -2, 2 })
                g.CylY(x, z, 1.6f, 11, 11, p => (p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) <= 1 ? Pal.Black[2] : Pal.Black[0]);   // burners
            g.CylY(4, 2, 2.2f, 12, 13, Pal.Ramp(Pal.Metal, 2, 3112)); g.Box(7, 13, 2, 9, 13, 2, Pal.Solid(Pal.Black[1]));  // pan
            g.Box(-7, 11, -4, 7, 20, -4, Pal.Weathered(Pal.Cream, 0.2f, 3113, 2, 0));              // splashback
            g.Box(-7, 21, -4, 7, 23, -1, steel);                                                   // hood
            return g;
        }

        /// <summary>Canning line: a steel table with a can conveyor, a sealing press and a pressure retort.</summary>
        static VoxelGrid CanneryGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Metal, 0.3f, 3121, 2, 0);
            g.Box(-14, 8, -5, 8, 9, 5, steel);                                                     // table top
            foreach (int x in new[] { -13, 7 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 7, z, Pal.Ramp(Pal.Metal, 1));
            g.Box(-13, 10, -2, 7, 10, 2, p => (p.x & 1) == 0 ? Pal.Black[1] : Pal.Black[0]);       // conveyor belt
            for (int x = -12; x <= 5; x += 3) g.CylY(x, 0, 1.2f, 11, 13, p => p.y == 13 ? Pal.Chrome[3] : Pal.Chrome[1]);   // cans
            g.Box(-3, 11, -4, -1, 20, -4, Pal.Ramp(Pal.Metal, 2)); g.Box(-3, 20, -4, -1, 20, 2, Pal.Ramp(Pal.Metal, 2));   // press frame
            g.Box(-3, 16, -1, -1, 18, 1, Pal.Solid(Pal.Accent));                                   // press head
            g.CylY(13, 0, 4.5f, 0, 16, Pal.Weathered(Pal.Chrome, 0.2f, 3122, 1, 0));                // retort
            g.CylY(13, 0, 2f, 17, 18, Pal.Ramp(Pal.Metal, 1)); g.Set(13, 19, 0, Pal.Solid(Pal.Crimson[2]));   // valve
            g.Box(9, 12, -1, 12, 12, 1, Pal.Ramp(Pal.Rust, 2));                                    // steam pipe
            g.Box(10, 8, 4, 12, 10, 4, Pal.Solid(Pal.Black[0])); g.Set(11, 9, 5, Pal.Solid(Pal.LightY));   // gauge
            return g;
        }
    }
}
