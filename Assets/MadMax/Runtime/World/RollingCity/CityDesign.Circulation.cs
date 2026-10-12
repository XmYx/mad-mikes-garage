using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    public static partial class CityDesign
    {
        /// <summary>Two continuous elevated streets, crossed at three plazas. Their branches terminate at openings
        /// in the lots' stair-served floors. Maintenance galleries have stairs at both ends, including both sides
        /// of the right-hand docking gap. Coordinates and all collision are in metres after meshing, not a scaled city.</summary>
        static VoxelGrid IndustrialCirculation(Plan plan, int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Metal, 0.25f, seed, 2, -30f);
            var rust = Pal.Ramp(Pal.Rust, 1, seed);
            foreach (int y in new[] { 3 * Storey, 6 * Storey })
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int xa = side < 0 ? -24 : 12, xb = side < 0 ? -12 : 24;
                    g.Box(xa, y, -plan.hz + 40, xb, y, plan.hz - 28, steel);
                    // Inner rail has openings at the cross-street plazas.
                    for (int z = -plan.hz + 40; z <= plan.hz - 28; z++)
                    {
                        bool crossing = Mathf.Abs(z) <= 7 || Mathf.Abs(z - 144) <= 7 || Mathf.Abs(z + 144) <= 7;
                        foreach (var bridge in plan.bridges)
                            if (bridge.y == y && bridge.xL < -24 && bridge.xR > 24 && z >= bridge.z0 && z <= bridge.z1) crossing = true;
                        if (!crossing)
                        {
                            int x = side * 12;
                            g.Set(x, y + 4, z, Pal.Ramp(Pal.Ochre, 2));
                            if (z % 8 == 0) g.Box(x, y + 1, z, x, y + 3, z, rust);
                        }
                        bool door = false;
                        foreach (var b in plan.bridges)
                            if (b.y == y && z >= b.z0 && z <= b.z1 && (side < 0 ? b.xL < -24 : b.xR > 24)) { door = true; break; }
                        if (!door)
                        {
                            g.Set(side * 24, y + 4, z, Pal.Ramp(Pal.Ochre, 2));
                            if (z % 8 == 0) g.Box(side * 24, y + 1, z, side * 24, y + 3, z, rust);
                        }
                    }
                    for (int z = -plan.hz + 48; z < plan.hz - 28; z += 48)
                    {
                        g.Box(side * 23, 1, z, side * 23, y - 1, z + 1, rust);
                        g.Tube(new Vector3(side * 23, y - 2, z + 14), new Vector3(side * 23, y - 12, z), 0.65f, rust);
                    }
                }
                foreach (int z in new[] { -144, 0, 144 })
                {
                    g.Box(-12, y, z - 6, 12, y, z + 6, steel);
                    foreach (int edge in new[] { z - 6, z + 6 })
                    {
                        g.Box(-12, y + 4, edge, 12, y + 4, edge, Pal.Ramp(Pal.Ochre, 2));
                        for (int x = -12; x <= 12; x += 6) g.Box(x, y + 1, edge, x, y + 3, edge, rust);
                        g.Box(-12, y - 2, edge, 12, y - 1, edge, rust);
                    }
                }
            }
            foreach (int side in new[] { -1, 1 })
            foreach (int z0 in new[] { -plan.hz + 12, plan.hz - 44 })
            {
                int a = side < 0 ? -plan.hx - 10 : plan.hx + 3;
                Flight(g, a, a + 6, z0, -Storey, Scrap, steel);
                g.Box(Mathf.Min(side * (plan.hx - 2), a), 0, z0 + 24,
                    Mathf.Max(side * (plan.hx - 2), a + 6), 0, z0 + 34, steel);
                foreach (int z in new[] { z0 + 34 })
                    g.Box(Mathf.Min(side * plan.hx, a), 4, z, Mathf.Max(side * plan.hx, a + 6), 4, z, rust);
            }
            return g;
        }
    }
}
