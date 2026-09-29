using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Workshops (roadmap 5): chemistry lab, tanning rack, smokehouse, loom and gunsmith bench. Tier = the
    /// quality bonus of the station's tools (the garage and gunsmith bench make finer goods).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> Workshops()
        {
            var In = BuildCategory.Industry;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var Fe = ResourceType.Iron; var Cu = ResourceType.Copper;
            yield return D("chemlab", "CHEMISTRY LAB", In, ChemLab(), 6, false, go =>
            {
                Station(go, "chemlab", "CHEMISTRY (LAB)", 0f).tier = 0.5f;
                Glow(go, new Vector3(-0.4f, 0.85f, 0f), new Color(0.6f, 0.9f, 0.5f), 2.5f, 0.6f, false, 0f);
            }, (Fe, 4), (G, 4), (Cu, 2));
            yield return D("tanning_rack", "TANNING RACK", In, TanningRack(), 4, false, go => Station(go, "tanning", "TAN HIDES (RACK)", 0f), (W, 6), (ResourceType.Lime, 1));
            yield return D("smokehouse", "SMOKEHOUSE", In, Smokehouse(), 8, false, go => Station(go, "smokehouse", "SMOKE MEAT AND FISH", 0f).output = new Vector3(0f, 0.9f, 0.7f), (W, 12), (S, 2), (ResourceType.Stone, 4));
            yield return D("loom", "LOOM", In, Loom(), 4, false, go => Station(go, "loom", "WEAVE (LOOM)", 0f), (W, 8));
            yield return D("gunsmith", "GUNSMITH BENCH", In, GunsmithBench(), 8, false, go => Station(go, "gunsmith", "GUNSMITH BENCH", 0f).tier = 1f, (Fe, 6), (W, 4), (S, 4));
            var hg = Hangar();
            hg.Bevel();
            yield return new FurnitureDef
            {
                id = "hangar", name = "HANGAR", category = In, grid = hg, cost = new[] { (Fe, 20), (S, 40), (W, 10) }, hits = 30, meshCollider = true, voxel = 0.16f,
                mesh = VoxelMesher.Build(hg, "Furniture_hangar", 0.16f),
                setup = go => { var st = Station(go, "hangar", "HANGAR (FLYING MACHINES)", 0f); st.tier = 0.5f; st.output = new Vector3(0f, 0.3f, 0.6f); }
            };
        }

        /// <summary>Quonset hangar (roadmap 25): a corrugated half-cylinder 8.8 m wide, open at the front, with ribs, a
        /// workbench along the back and a hoist beam. Flying machines are built in it. Coarse 0.16 m voxels.</summary>
        static VoxelGrid Hangar()
        {
            var g = new VoxelGrid().Mat(Iron);
            const int R = 27, L = 30;
            var tin = Pal.Weathered(Pal.Metal, 0.3f, 2611, 2, -100);
            var tinDark = Pal.Weathered(Pal.Metal, 0.45f, 2612, 1, -100);
            var rib = Pal.Ramp(Pal.Black, 1, 2613);
            for (int z = -L; z <= L; z++)
            for (int x = -R - 1; x <= R + 1; x++)
            for (int y = 0; y <= R + 1; y++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                bool isRib = (z + L) % 10 == 0;
                if (Mathf.Abs(d - R) > (isRib ? 1.1f : 0.6f)) continue;
                int arc = Mathf.FloorToInt(Mathf.Atan2(y, x) * R);
                g.Set(x, y, z, isRib ? rib : (arc & 2) == 0 ? tin : tinDark);
            }
            for (int x = -R; x <= R; x++)                                                      // back wall
            for (int y = 0; y <= R; y++)
                if (x * x + y * y <= R * R) g.Set(x, y, -L, (x & 3) == 0 ? tinDark : tin);
            g.Mat(Wood);
            g.Box(-10, 0, -L + 1, 10, 5, -L + 3, Pal.Ramp(Pal.Wood, 2, 2614));                   // workbench along the back
            g.Mat(Iron);
            g.Box(-1, 22, -L + 1, 1, 23, L - 1, Pal.Ramp(Pal.Ochre, 2, 2615));                    // hoist beam
            return g;
        }

        static VoxelGrid ChemLab()
        {
            var g = new VoxelGrid().Mat(Iron);
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 8, z, Pal.Ramp(Pal.Metal, 1, 1601));
            g.Box(-11, 9, -5, 11, 9, 5, Pal.Ramp(Pal.Metal, 2, 1602));
            g.Box(-6, 10, -1, -4, 10, 1, Pal.Ramp(Pal.Black, 1)); g.Set(-5, 10, 0, Pal.Solid(Pal.Amber));                 // burner
            g.Mat(Glass);
            g.CylY(-5, 0, 1.8f, 11, 13, p => p.y <= 12 ? Pal.Moss[3] : Pal.Glass[3]);                                        // flask
            g.Box(-5, 14, 0, -5, 15, 0, Pal.Ramp(Pal.Glass, 3));
            g.Tube(new Vector3(-5, 15, 0), new Vector3(3, 12, -1), 0.5f, Pal.Ramp(Pal.Glass, 3));                            // condenser
            g.CylY(4, -2, 1.2f, 10, 12, p => p.y <= 11 ? Pal.Ochre[3] : Pal.Glass[3]);                                      // jars
            g.CylY(7, 2, 1.2f, 10, 11, p => p.y <= 10 ? Pal.Crimson[3] : Pal.Glass[3]);
            g.Mat(Wood); g.Box(-1, 10, 2, 2, 10, 3, Pal.Ramp(Pal.Wood, 1, 1603));                                          // test tube rack
            g.Mat(Glass); for (int x = -1; x <= 2; x++) g.Box(x, 11, 3, x, 12, 3, Pal.Solid(x % 2 == 0 ? Pal.Moss[4] : Pal.Navy[4]));
            return g;
        }

        static VoxelGrid TanningRack()
        {
            var g = new VoxelGrid().Mat(Wood);
            var post = Pal.Ramp(Pal.Wood, 1, 1611);
            foreach (int x in new[] { -8, 8 }) { g.Box(x, 0, 0, x, 18, 0, post); g.Box(x, 0, -3, x, 0, 3, post); }                // posts and feet
            foreach (int y in new[] { 2, 17 }) g.Box(-8, y, 0, 8, y, 0, post);
            g.Mat(Cloth);
            for (int x = -6; x <= 6; x++)
            for (int y = 4; y <= 15; y++)
            {
                // a stretched hide: ragged outline, darker spine
                if (Mathf.Abs(x) + (y > 12 ? (y - 12) * 2 : y < 6 ? (6 - y) * 2 : 0) > 7) continue;
                g.Set(x, y, 0, Mathf.Abs(x) <= 1 ? Pal.Ramp(Pal.Bronze, 1, 1612) : Pal.Weathered(Pal.Sand, 0.1f, 1613, 2, 0));
            }
            g.Mat(Wood);
            for (int y = 5; y <= 14; y += 3) { g.Set(-7, y, 0, Pal.Solid(Pal.Wood[0])); g.Set(7, y, 0, Pal.Solid(Pal.Wood[0])); }   // lacing
            return g;
        }

        static VoxelGrid Smokehouse()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-7, 0, -6, 7, 16, 6, Pal.Stripe(Pal.Weathered(Pal.Wood, 0.1f, 1621, 2, 0), Pal.Ramp(Pal.Wood, 1, 1622), 0, 3));
            g.Repaint(-7, 12, -6, 7, 16, 6, p => Pal.Hash(p, 1623) > 0.4f ? Pal.Black[2] : Pal.Wood[1]);                   // soot
            g.Repaint(-3, 1, 6, 3, 11, 6, Pal.Stripe(Pal.Ramp(Pal.Wood, 1, 1624), Pal.Ramp(Pal.Wood, 0, 1625), 0, 2));       // door
            g.Mat(Iron); g.Set(2, 6, 7, Pal.Ramp(Pal.Chrome, 1));
            g.Mat(Scrap);
            for (int z = -7; z <= 7; z++) g.Box(-8, 17, z, 8, 17 + (7 - z) / 5, z, Pal.Weathered(Pal.Metal, 0.5f, 1626, 2, 0));
            g.Box(4, 18, -4, 5, 24, -3, Pal.Ramp(Pal.Metal, 0, 1627));                                                         // chimney
            g.Mat(Stone); g.Box(-8, 0, -7, 8, 0, 7, Pal.Ramp(Pal.Metal, 1, 1628));
            return g;
        }

        static VoxelGrid Loom()
        {
            var g = new VoxelGrid().Mat(Wood);
            var frame = Pal.Ramp(Pal.Wood, 1, 1631);
            foreach (int x in new[] { -9, 9 }) { g.Box(x, 0, 0, x, 16, 0, frame); g.Box(x, 0, -3, x, 0, 3, frame); }
            g.CylX(2, 0, 1f, -9, 9, Pal.Ramp(Pal.Wood, 2, 1632)); g.CylX(15, 0, 1f, -9, 9, Pal.Ramp(Pal.Wood, 2, 1633));           // beams
            g.Mat(Cloth);
            for (int x = -7; x <= 7; x += 2) g.Box(x, 9, 0, x, 14, 0, Pal.Solid(Pal.Cream[3]));                                     // warp threads
            g.Box(-7, 3, 0, 7, 8, 0, p => p.y % 2 == 0 ? Pal.Crimson[2] : Pal.Olive[2]);                                           // woven cloth
            g.Mat(Wood); g.Box(-8, 9, 1, 8, 9, 1, Pal.Ramp(Pal.Wood, 0, 1634));                                                    // beater bar
            return g;
        }

        static VoxelGrid GunsmithBench()
        {
            var g = new VoxelGrid().Mat(Iron);
            foreach (int x in new[] { -11, 11 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Metal, 0, 1641));
            g.Box(-12, 10, -5, 12, 10, 5, Pal.Ramp(Pal.Metal, 1, 1642));
            g.Box(-12, 11, -6, 12, 20, -6, p => (p.x + p.y) % 4 == 0 ? Pal.Metal[0] : Pal.Metal[1]);                               // pegboard
            foreach (int x in new[] { -9, -5, -1, 3 }) g.Box(x, 13, -5, x, 18, -5, Pal.Ramp(Pal.Chrome, 2));                        // hanging tools
            g.Box(8, 11, -1, 10, 13, 1, Pal.Ramp(Pal.Black, 2, 1643)); g.Box(9, 14, -1, 9, 14, 1, Pal.Ramp(Pal.Chrome, 2));          // vise
            g.Box(-8, 11, 1, 3, 11, 1, Pal.Ramp(Pal.Black, 1)); g.Box(-8, 11, 2, -8, 11, 2, Pal.Ramp(Pal.Black, 1));                // rifle barrel
            g.Mat(Wood); g.Box(3, 11, 0, 7, 12, 2, Pal.Ramp(Pal.Wood, 2, 1644));                                                    // stock
            g.Mat(Scrap); g.Box(-11, 11, 2, -9, 12, 4, Pal.Weathered(Pal.Olive, 0.2f, 1645, 2, 0));                                 // ammo can
            return g;
        }
    }
}
