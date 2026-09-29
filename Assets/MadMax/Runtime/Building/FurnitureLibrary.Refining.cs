using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Refining (roadmap 6): pumpjack over an oil field, refinery (crude → petrol, diesel, oil, tar; power and
    /// heat) and the seed-oil press (sunflower / hemp seeds → seed oil for biodiesel).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> Refining()
        {
            var In = BuildCategory.Industry;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Co = ResourceType.Concrete;
            yield return D("pumpjack", "PUMPJACK", In, PumpjackGrid(), 20, false, go => { Node(go, UtilityKind.Power, 0.6f); Beam(go); go.AddComponent<Pumpjack>(); }, (Fe, 16), (S, 10), (Cu, 4));
            yield return D("refinery", "REFINERY", In, RefineryGrid(), 25, false, go =>
            {
                Node(go, UtilityKind.Power, 1.2f);
                Station(go, "refinery", "REFINE CRUDE (REFINERY)", 1500f).output = new Vector3(0f, 0.6f, 1.4f);
                Glow(go, new Vector3(1.1f, 4.1f, -0.8f), new Color(1f, 0.55f, 0.2f), 6f, 2f, false, 0f);     // flare stack
            }, (Fe, 20), (Cu, 8), (Co, 6), (G, 2));
            yield return D("oil_press", "OIL PRESS", In, OilPress(), 6, false, go => Station(go, "press", "PRESS SEED OIL", 0f), (W, 6), (Fe, 3));
        }

        static Mesh beamMesh;
        /// <summary>The walking beam and horse head, nodding on its own pivot at the top of the A-frame.</summary>
        static void Beam(GameObject go)
        {
            if (!beamMesh)
            {
                var g = new VoxelGrid();
                g.Box(-1, -1, -13, 1, 1, 12, Pal.Weathered(Pal.Crimson, 0.3f, 1701, 2, 0));
                for (int y = -7; y <= 2; y++) g.Box(-1, y, 12 + Mathf.Max(0, 2 - Mathf.Abs(y + 2) / 2), 1, y, 14, Pal.Ramp(Pal.Metal, 1, 1702));   // horse head
                g.Box(-2, -5, -15, 2, 0, -12, Pal.Ramp(Pal.Metal, 0, 1703));                                                 // counterweight
                g.Bevel();
                beamMesh = VoxelMesher.Build(g, "Furniture_pumpjack_beam");
            }
            var b = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
            b.transform.SetParent(go.transform, false);
            b.transform.localPosition = new Vector3(0f, 22 * VoxelMesher.DefaultSize, 0f);
            b.GetComponent<MeshFilter>().sharedMesh = beamMesh;
            b.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
        }

        static VoxelGrid PumpjackGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-6, 0, -15, 6, 1, 15, p => (p.x + p.z) % 5 == 0 ? Pal.Metal[0] : Pal.Metal[1]);                          // skid
            foreach (int x in new[] { -4, 4 }) g.Tube(new Vector3(x, 2, -3), new Vector3(0, 21, 0), 0.6f, Pal.Weathered(Pal.Crimson, 0.35f, 1711, 2, 0));   // samson post
            foreach (int x in new[] { -4, 4 }) g.Tube(new Vector3(x, 2, 3), new Vector3(0, 21, 0), 0.6f, Pal.Weathered(Pal.Crimson, 0.35f, 1712, 2, 0));
            g.Box(-3, 2, -13, 3, 8, -7, Pal.Weathered(Pal.Ochre, 0.3f, 1713, 2, 0));                                           // gearbox
            g.Box(-2, 2, -6, 2, 5, -4, Pal.Ramp(Pal.Metal, 2, 1714));                                                         // motor
            foreach (int x in new[] { -4, 4 }) g.Tube(new Vector3(x, 6, -10), new Vector3(x, 12, -12), 0.5f, Pal.Ramp(Pal.Metal, 0, 1715));   // crank arms
            g.CylY(0, 13, 1.5f, 2, 6, Pal.Ramp(Pal.Metal, 1, 1716)); g.Box(-2, 6, 12, 2, 6, 14, Pal.Ramp(Pal.Chrome, 2));   // wellhead
            g.Box(0, 7, 13, 0, 14, 13, Pal.Ramp(Pal.Chrome, 3));                                                              // polished rod
            return g;
        }

        static VoxelGrid RefineryGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-17, 0, -17, 17, 1, 17, Pal.Ramp(Pal.Metal, 0, 1721));                                                      // pad
            g.CylY(-6, -4, 4f, 2, 44, p => p.y % 8 == 0 ? Pal.Chrome[1] : Pal.Pick(Pal.Chrome, p, 1722, 2));               // distillation column
            g.CylY(6, -6, 2.6f, 2, 30, p => p.y % 6 == 0 ? Pal.Metal[0] : Pal.Pick(Pal.Metal, p, 1723, 2));                 // stripper
            foreach (int y in new[] { 12, 22, 32 }) g.Tube(new Vector3(-6, y, -4), new Vector3(6, y - 2, -6), 0.6f, Pal.Ramp(Pal.Metal, 1, 1724));   // draw-off pipes
            g.Box(-4, 2, 6, 8, 9, 14, Pal.Weathered(Pal.Metal, 0.5f, 1725, 2, 0));                                            // fired heater
            g.Box(-2, 3, 15, 6, 6, 15, p => (p.x + p.y) % 2 == 0 ? Pal.Amber : Pal.Rust[4]);                                   // burner window
            g.Tube(new Vector3(14, 2, -10), new Vector3(14, 50, -10), 0.9f, Pal.Ramp(Pal.Metal, 0, 1726));                    // flare stack
            foreach (int x in new[] { -14, -10 }) g.CylY(x, 10, 2.5f, 2, 8, Pal.Weathered(Pal.Cream, 0.3f, 1727 + x, 2, 0)); // product tanks
            return g;
        }

        static VoxelGrid OilPress()
        {
            var g = new VoxelGrid().Mat(Wood);
            var frame = Pal.Ramp(Pal.Wood, 1, 1731);
            foreach (int x in new[] { -6, 6 }) g.Box(x, 0, 0, x, 18, 0, frame);
            g.Box(-7, 18, -1, 7, 19, 1, frame);
            g.CylY(0, 0, 3.6f, 1, 8, p => p.y % 3 == 0 ? Pal.Metal[1] : Pal.Wood[2]);                                         // barrel of seeds
            g.Mat(Iron); g.Box(0, 9, 0, 0, 17, 0, Pal.Ramp(Pal.Chrome, 2)); g.Box(-5, 16, 0, 5, 16, 0, Pal.Ramp(Pal.Metal, 2));   // screw and bar
            g.Box(-3, 9, -3, 3, 9, 3, Pal.Ramp(Pal.Metal, 1, 1732));                                                          // pressing plate
            g.Box(0, 2, 4, 0, 2, 6, Pal.Ramp(Pal.Metal, 1)); g.Set(0, 1, 6, Pal.Solid(Pal.Ochre[4]));                           // spout, drip
            return g;
        }
    }
}
