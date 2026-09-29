using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The sea (user additions): a SLIPWAY to build boats on (lay it down the beach into the water), the diver's
    /// AIR COMPRESSOR and the submarine's O2 RACK, and the underwater base — riveted SEA DOMES and SEA TUNNELS that hold
    /// air (<see cref="AirPocket"/>), the SHORE ENTRANCE (a sloping tube from a hut on the beach down to the sea floor:
    /// walk in from dry land) and the DOCKING COLLAR a submarine locks its sail hatch to (charge, air, board).
    /// Coarse 0.16 m voxels for the big pieces.</summary>
    public static partial class FurnitureLibrary
    {
        const float Coarse = 0.16f;

        static FurnitureDef Big(string id, string name, BuildCategory cat, VoxelGrid g, int hits, System.Action<GameObject> setup, params (ResourceType, int)[] cost)
        {
            g.Bevel();
            return new FurnitureDef { id = id, name = name, category = cat, grid = g, cost = cost, hits = hits, meshCollider = true, voxel = Coarse, mesh = VoxelMesher.Build(g, "Furniture_" + id, Coarse), setup = setup };
        }

        static IEnumerable<FurnitureDef> Sea()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var Fe = ResourceType.Iron; var G = ResourceType.Glass; var Cu = ResourceType.Copper; var Rb = ResourceType.Rubber;
            yield return Big("slipway", "SLIPWAY", BuildCategory.Industry, Slipway(), 20, go =>
            {
                var st = Station(go, "slipway", "SLIPWAY (BOATS)", 0f);
                st.tier = 0.5f; st.output = new Vector3(0f, 0.8f, 10.5f);                               // the boat slides off the low end
            }, (W, 40), (Fe, 12), (S, 10));
            yield return D("air_compressor", "AIR COMPRESSOR", BuildCategory.Utility, Compressor(), 6, false, go =>
            {
                Node(go, UtilityKind.Power, 0.4f);
                go.AddComponent<AirCompressor>();
            }, (Fe, 8), (Cu, 6), (Rb, 3));
            yield return D("o2_rack", "O2 RACK", BuildCategory.Utility, O2RackGrid(), 4, false, go => go.AddComponent<O2Rack>(), (Fe, 4), (S, 2));
            yield return Big("sea_dome", "SEA DOME", BuildCategory.Structure, SeaDome(), 40, go =>
            {
                go.AddComponent<AirPocket>().boxes = new[] { new Bounds(new Vector3(0f, 1.8f, 0f), new Vector3(6.4f, 3.4f, 6.4f)) };
                Node(go, UtilityKind.Power, 0.3f);
                Glow(go, new Vector3(0f, 3.1f, 0f), new Color(1f, 0.85f, 0.6f), 9f, 2f, true, 60f);
            }, (Fe, 60), (S, 40), (G, 8), (ResourceType.Concrete, 20));
            yield return Big("sea_tunnel", "SEA TUNNEL", BuildCategory.Structure, SeaTunnel(), 24, go =>
            {
                go.AddComponent<AirPocket>().boxes = new[] { new Bounds(new Vector3(0f, 1.6f, 0f), new Vector3(3f, 3f, 6.3f)) };
                Node(go, UtilityKind.Power, 0.3f);
                Glow(go, new Vector3(0f, 2.9f, 0f), new Color(1f, 0.85f, 0.6f), 6f, 1.5f, true, 30f);
            }, (Fe, 24), (S, 12), (G, 2));
            yield return Big("shore_entrance", "SHORE ENTRANCE", BuildCategory.Structure, ShoreEntrance(), 36, go =>
            {
                // dry all the way down the sloping tube: boxes stepping down with it
                var boxes = new List<Bounds> { new Bounds(new Vector3(0f, 1.3f, -0.6f), new Vector3(3.2f, 2.6f, 1.6f)) };
                for (int i = 0; i < 6; i++)
                {
                    float z0 = i * 17f, zc = (z0 + 8.5f) * Coarse, yc = (EntranceAxis(z0 + 8.5f) - 8f) * Coarse + 1.4f;
                    boxes.Add(new Bounds(new Vector3(0f, yc, zc), new Vector3(3f, 3.4f, 17.5f * Coarse)));
                }
                go.AddComponent<AirPocket>().boxes = boxes.ToArray();
                Node(go, UtilityKind.Power, 0.3f);
                Glow(go, new Vector3(0f, 2f, 4f), new Color(1f, 0.85f, 0.6f), 8f, 1.5f, true, 40f);
            }, (Fe, 40), (S, 30), (ResourceType.Concrete, 16), (W, 8));
            yield return D("docking_collar", "DOCKING COLLAR", BuildCategory.Structure, Collar(), 12, false, go =>
            {
                Node(go, UtilityKind.Power, 0.5f);
                go.AddComponent<DockingCollar>();
            }, (Fe, 16), (Cu, 4), (Rb, 4));
        }

        /// <summary>A riveted steel hemisphere 7 m across: plated floor, a porthole ring, a door opening, hazard stripes.</summary>
        static VoxelGrid SeaDome()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            const int R = 22;
            var steel = Pal.Weathered(Pal.Metal, 0.45f, 2801, 2, 0);
            for (int x = -R; x <= R; x++)
            for (int z = -R; z <= R; z++)
                if (x * x + z * z <= (R - 1) * (R - 1)) g.Set(x, 0, z, (x % 6 == 0 || z % 6 == 0) ? Pal.Solid(Pal.Metal[1]) : Pal.Weathered(Pal.Metal, 0.25f, 2802, 1, 0));
            for (int x = -R; x <= R; x++)
            for (int y = 1; y <= R; y++)
            for (int z = -R; z <= R; z++)
            {
                float r = Mathf.Sqrt(x * x + y * y + z * z);
                if (Mathf.Abs(r - R) > 0.7f) continue;
                if (z > 0 && Mathf.Abs(x) <= 4 && y <= 14) continue;                                        // the doorway
                float ang = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
                bool port = y >= 9 && y <= 11 && Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 45f) * 45f + 22.5f)) < 4f;
                bool frame = z > 0 && Mathf.Abs(x) <= 5 && y <= 15;
                g.Set(x, y, z, port ? Pal.Ramp(Pal.Glass, 3, 2803) : frame ? Pal.Solid(Pal.Ochre[2])
                    : y <= 2 ? ((((x + z) / 3) & 1) == 0 ? Pal.Solid(Pal.Ochre[3]) : Pal.Solid(Pal.Black[1]))
                    : (y % 5 == 0 && ((x + z) & 3) == 0) ? Pal.Solid(Pal.Chrome[2]) : steel);
            }
            return g;
        }

        /// <summary>A 6 m pressure tube with a walkway, ribs and side portholes; open at both ends to join domes.</summary>
        static VoxelGrid SeaTunnel()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            const int R = 10, C = 10;
            var steel = Pal.Weathered(Pal.Metal, 0.45f, 2811, 2, 0);
            for (int z = -19; z <= 19; z++)
            {
                for (int x = -7; x <= 7; x++) g.Set(x, 1, z, (z & 3) == 0 ? Pal.Solid(Pal.Metal[1]) : Pal.Weathered(Pal.Metal, 0.3f, 2812, 1, 0));   // walkway
                for (int x = -R - 1; x <= R + 1; x++)
                for (int y = 0; y <= C + R + 1; y++)
                {
                    float r = Mathf.Sqrt(x * x + (y - C) * (y - C));
                    bool rib = (z + 19) % 8 == 0;
                    if (Mathf.Abs(r - R) > (rib ? 1.1f : 0.7f) || y < 1) continue;
                    bool port = !rib && (z + 19) % 8 == 4 && Mathf.Abs(y - C - 1) <= 1 && Mathf.Abs(x) >= R - 1;
                    g.Set(x, y, z, port ? Pal.Ramp(Pal.Glass, 3, 2813) : rib ? Pal.Solid(Pal.Metal[0]) : steel);
                }
            }
            return g;
        }

        /// <summary>Axis height of the shore entrance's tube at z (voxels): level through the hut, then down ~19°.</summary>
        static float EntranceAxis(float z) => 10f - Mathf.Max(0f, z) * 0.34f;

        /// <summary>A shed on the beach and a tube sloping 16 m down from it into the sea: walk in from dry land.</summary>
        static VoxelGrid ShoreEntrance()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            const int R = 10;
            var steel = Pal.Weathered(Pal.Metal, 0.5f, 2821, 2, 0);
            for (int z = 0; z <= 100; z++)
            {
                float c = EntranceAxis(z);
                int floorY = Mathf.RoundToInt(c - 8f);
                for (int x = -7; x <= 7; x++) g.Set(x, floorY, z, (z & 3) == 0 ? Pal.Solid(Pal.Ochre[2]) : Pal.Weathered(Pal.Metal, 0.3f, 2822, 1, 0));   // ramp, grip strips
                for (int x = -R - 1; x <= R + 1; x++)
                for (int y = Mathf.FloorToInt(c - R - 1); y <= Mathf.CeilToInt(c + R + 1); y++)
                {
                    float r = Mathf.Sqrt(x * x + (y - c) * (y - c));
                    bool rib = z % 10 == 0;
                    if (Mathf.Abs(r - R) > (rib ? 1.1f : 0.7f) || y < floorY) continue;
                    g.Set(x, y, z, rib ? Pal.Solid(Pal.Metal[0]) : steel);
                }
            }
            // the hut on the beach: concrete walls, a tin roof, the door facing inland
            g.Mat((byte)ResourceType.Concrete);
            for (int y = 0; y <= 18; y++)
            for (int z = -10; z <= 0; z++)
            for (int x = -11; x <= 11; x++)
            {
                bool wall = Mathf.Abs(x) == 11 || z == -10;
                if (y == 0) { g.Set(x, y, z, Pal.Ramp(Pal.Cream, 1, 2823)); continue; }
                if (!wall) continue;
                if (z == -10 && Mathf.Abs(x) <= 4 && y <= 14) continue;                                    // the door
                g.Set(x, y, z, y == 12 ? Pal.Solid(Pal.Ochre[2]) : Pal.Weathered(Pal.Cream, 0.35f, 2824, 2, 0));
            }
            g.Mat((byte)ResourceType.Scrap);
            g.Box(-12, 19, -11, 12, 19, 1, p => (p.x & 1) == 0 ? Pal.Metal[2] : Pal.Metal[1]);            // roof
            g.Box(-3, 16, -11, 3, 17, -11, Pal.Solid(Pal.Crimson[3]));                                    // sign board
            return g;
        }

        /// <summary>A collar ring with a hatch wheel and bolts (normal voxels), mounted on a dome or tunnel roof.</summary>
        static VoxelGrid Collar()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            for (int x = -12; x <= 12; x++)
            for (int z = -12; z <= 12; z++)
            for (int y = 0; y <= 10; y++)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                if (r > 12.5f || (r < 9.5f && y > 1)) continue;
                bool bolt = y == 10 && ((x + z) & 3) == 0 && r > 10.5f;
                g.Set(x, y, z, bolt ? Pal.Solid(Pal.Chrome[2]) : y >= 9 ? Pal.Solid(Pal.Ochre[2]) : Pal.Weathered(Pal.Metal, 0.4f, 2831, 2, 0));
            }
            g.CylY(0, 0, 3.5f, 11, 11, Pal.Ramp(Pal.Crimson, 2, 2832));                                   // hatch wheel
            g.Box(-4, 11, 0, 4, 11, 0, Pal.Ramp(Pal.Crimson, 2, 2832)); g.Box(0, 11, -4, 0, 11, 4, Pal.Ramp(Pal.Crimson, 2, 2832));
            return g;
        }

        /// <summary>A 12 m timber ramp with steel rails, sleepers and a winch post, sloping gently down its length.</summary>
        static VoxelGrid Slipway()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            for (int z = 0; z <= 75; z++)
            {
                int y = Mathf.RoundToInt(6f - z * 0.08f);
                if (z % 5 == 0) g.Box(-15, y, z, 15, y, z + 1, Pal.Ramp(Pal.Wood, 1, 2841 + z));            // sleepers
                g.Mat((byte)ResourceType.Iron);
                foreach (int x in new[] { -9, 9 }) g.Box(x, y + 1, z, x + 1, y + 1, z, Pal.Ramp(Pal.Metal, 2, 2842));   // rails
                g.Mat((byte)ResourceType.Wood);
                foreach (int x in new[] { -15, 15 }) if (z % 10 == 0) g.Box(x, y - 6, z, x, y + 2, z, Pal.Ramp(Pal.Wood, 0, 2843));   // piles
            }
            g.Box(-3, 7, -4, 3, 16, -1, Pal.Weathered(Pal.Wood, 0.2f, 2844, 2, 0));                          // winch post
            g.Mat((byte)ResourceType.Iron);
            g.CylX(12, -2, 2.5f, -5, 5, Pal.Ramp(Pal.Ochre, 2, 2845));                                        // cable drum
            g.Box(0, 8, 0, 0, 8, 30, Pal.Ramp(Pal.Black, 1, 2846));                                           // the cable down the ramp
            return g;
        }

        static VoxelGrid Compressor()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            g.Box(-7, 0, -5, 7, 1, 5, Pal.Ramp(Pal.Metal, 1, 2851));                                          // skid
            g.CylX(6, 0, 4.5f, -6, 4, Pal.Weathered(Pal.Crimson, 0.35f, 2852, 2, 0));                          // receiver tank
            g.CylY(-4, 0, 2.5f, 11, 16, Pal.Ramp(Pal.RigGreen, 2, 2853));                                     // motor
            g.CylZ(4, 12, 1.8f, -1, 1, p => p.z == 1 ? Pal.Cream[4] : Pal.Metal[2]);                         // gauge
            g.Set(4, 12, 2, Pal.Solid(Pal.Black[0]));
            g.Mat((byte)ResourceType.Rubber);
            g.Tube(new Vector3(6, 6, 4), new Vector3(9, 2, 6), 0.6f, Pal.Ramp(Pal.Black, 1, 2854));             // hose
            return g;
        }

        static VoxelGrid O2RackGrid()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            foreach (int x in new[] { -8, 8 }) g.Box(x, 0, -2, x, 18, -2, Pal.Ramp(Pal.Metal, 2, 2861));
            foreach (int y in new[] { 1, 12 }) g.Box(-8, y, -2, 8, y, -2, Pal.Ramp(Pal.Metal, 2, 2861));
            foreach (int x in new[] { -5, -1, 3, 7 })
                g.CylY(x, 0, 1.6f, 2, 16, p => p.y >= 15 ? Pal.Chrome[3] : p.y % 6 == 0 ? Pal.Cream[4] : Pal.Moss[2]);   // O2 bottles
            return g;
        }
    }
}
