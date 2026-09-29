using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Power from the weather (user additions): solar panels (400 W in full sun), the large wind turbine (a
    /// 15 m tower, 3 kW in a good wind; the medium one is the old WIND TURBINE) and the water wheel (1.2 kW in a
    /// running river). Built from kits made at the workbench out of solar cells (arc furnace: silica and copper),
    /// turbine blades (aluminium) and generator coils (copper and iron). The small turbine is a vehicle part.</summary>
    public static partial class FurnitureLibrary
    {
        static Mesh bigRotorMesh, wheelMesh;

        static IEnumerable<FurnitureDef> Power()
        {
            var U = BuildCategory.Utility;
            yield return Kit("solar_panel", "SOLAR PANEL", SolarPanelGrid(), ItemIds.SolarKit, U).With(go => { Node(go, UtilityKind.Power, 0.4f); go.AddComponent<SolarPanel>(); });
            var tg = LargeTurbineTower();
            tg.Bevel();
            yield return new FurnitureDef
            {
                id = "wind_turbine_large", name = "LARGE WIND TURBINE", category = U, grid = tg, kit = ItemIds.WindKit, hits = 20, voxel = 0.16f,
                mesh = VoxelMesher.Build(tg, "Furniture_wind_turbine_large", 0.16f),
                setup = go =>
                {
                    Node(go, UtilityKind.Power, 0.5f);
                    if (!bigRotorMesh) { var rg = LargeRotor(); rg.Bevel(); bigRotorMesh = VoxelMesher.Build(rg, "Furniture_big_rotor", 0.16f); }
                    var r = new GameObject("Rotor", typeof(MeshFilter), typeof(MeshRenderer));
                    r.transform.SetParent(go.transform, false);
                    r.transform.localPosition = new Vector3(0f, 93 * 0.16f, 0.9f);
                    r.GetComponent<MeshFilter>().sharedMesh = bigRotorMesh;
                    r.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
                    var w = go.AddComponent<Windmill>(); w.rotor = r.transform; w.rated = 3000f; w.height = 15f;
                }
            };
            yield return Kit("water_turbine", "WATER WHEEL", WaterWheelFrame(), ItemIds.WaterWheelKit, U).With(go =>
            {
                Node(go, UtilityKind.Power, 1.4f);
                if (!wheelMesh) { var wg = WaterWheel(); wg.Bevel(); wheelMesh = VoxelMesher.Build(wg, "Furniture_water_wheel"); }
                var w = new GameObject("Wheel", typeof(MeshFilter), typeof(MeshRenderer));
                w.transform.SetParent(go.transform, false);
                w.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                w.GetComponent<MeshFilter>().sharedMesh = wheelMesh;
                w.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
                go.AddComponent<WaterTurbine>().wheel = w.transform;
            });
        }

        /// <summary>A panel of cells on two legs, tilted ~30° to the sky: navy cells in a silver grid, an aluminium frame.</summary>
        static VoxelGrid SolarPanelGrid()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Aluminium);
            var frame = Pal.Ramp(Pal.Chrome, 1, 2201);
            foreach (int x in new[] { -9, 9 }) { g.Box(x, 0, -5, x, 6, -5, Pal.Ramp(Pal.Metal, 1, 2202)); g.Box(x, 0, 6, x, 12, 6, Pal.Ramp(Pal.Metal, 1, 2203)); }
            for (int z = -7; z <= 8; z++)
            {
                int y = 7 + Mathf.RoundToInt((z + 7) * 0.55f);
                bool edge = z == -7 || z == 8;
                g.Mat((byte)(edge ? ResourceType.Aluminium : ResourceType.Glass));
                if (edge) g.Box(-11, y, z, 11, y, z, frame);
                else g.Box(-11, y, z, 11, y, z, p => Mathf.Abs(p.x) == 11 ? Pal.Chrome[1] : (p.x % 5 == 0 || (z + 7) % 5 == 0) ? Pal.Chrome[2] : Pal.Navy[1]);
            }
            g.Mat((byte)ResourceType.Copper); g.Box(-1, 1, 0, 1, 3, 1, Pal.Ramp(Pal.Black, 1)); g.Set(0, 3, 2, Pal.Solid(Pal.Amber));   // junction box
            return g;
        }

        /// <summary>A 15 m tapered tower on a concrete pad with the nacelle on top (0.16 m voxels; the rotor is separate).</summary>
        static VoxelGrid LargeTurbineTower()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Concrete);
            g.Box(-5, 0, -5, 5, 1, 5, Pal.Ramp(Pal.Cream, 1, 2211));
            g.Mat((byte)ResourceType.Iron);
            for (int y = 2; y <= 90; y++) g.CylY(0, 0, Mathf.Lerp(2.6f, 1.3f, y / 90f), y, y, Pal.Weathered(Pal.Cream, 0.2f, 2212 + y / 12, 2, 0));
            for (int y = 20; y <= 80; y += 30) g.CylY(0, 0, Mathf.Lerp(2.7f, 1.4f, y / 90f), y, y, Pal.Ramp(Pal.Rust, 1, 2213));   // weathered seams
            g.Box(-2, 90, -4, 2, 95, 4, Pal.Weathered(Pal.Cream, 0.15f, 2214, 3, 0));                                                  // nacelle
            g.Box(-1, 96, -3, 1, 96, -2, Pal.Solid(Pal.Crimson[4]));                                                                    // warning lamp
            g.Box(-2, 1, 2, 2, 5, 2, Pal.Ramp(Pal.Metal, 1));                                                                           // door
            return g;
        }

        /// <summary>Three long blades on a hub, in the rotor's XY plane (0.16 m voxels, ~5 m blades).</summary>
        static VoxelGrid LargeRotor()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Aluminium);
            g.CylZ(0, 0, 1.8f, -1, 2, Pal.Ramp(Pal.Cream, 3));
            for (int b = 0; b < 3; b++)
            {
                float a = b * Mathf.PI * 2f / 3f + 0.3f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                g.Tube(d * 1.5f, d * 31f, 1.1f, Pal.Weathered(Pal.Cream, 0.2f, 2220 + b, 3, 0));
                g.Tube(d * 25f, d * 31f, 1.15f, Pal.Solid(Pal.Crimson[3]));                    // red tips
            }
            return g;
        }

        /// <summary>The water wheel's frame: two timber A-frames, the axle bearings and a generator box on one side.</summary>
        static VoxelGrid WaterWheelFrame()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            foreach (int x in new[] { -9, 9 })
            {
                g.Tube(new Vector3(x, 0, -8), new Vector3(x, 13, 0), 0.8f, Pal.Ramp(Pal.Wood, 1, 2231));
                g.Tube(new Vector3(x, 0, 8), new Vector3(x, 13, 0), 0.8f, Pal.Ramp(Pal.Wood, 1, 2232));
            }
            g.Mat((byte)ResourceType.Iron); g.Box(-10, 12, -1, 10, 13, 1, Pal.Ramp(Pal.Metal, 1, 2233));                                // axle beam
            g.Box(10, 10, -3, 14, 15, 3, Pal.Weathered(Pal.Olive, 0.3f, 2234, 2, 0)); g.Set(14, 15, 0, Pal.Solid(Pal.Amber));            // generator
            return g;
        }

        /// <summary>The wheel: a hub, spokes and twelve paddles around a 1.2 m radius, turning about local X.</summary>
        static VoxelGrid WaterWheel()
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
            g.CylX(0, 0, 1.6f, -8, 8, Pal.Ramp(Pal.Metal, 1));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var d = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                g.Tube(d * 1.5f, d * 13f, 0.5f, Pal.Ramp(Pal.Wood, 2, 2240 + i));                                                   // spoke
                var c = d * 14f;
                var t = new Vector3(0f, -d.z, d.y);                                                                                   // along the rim
                for (int x = -7; x <= 7; x++) g.Tube(new Vector3(x, c.y - t.y, c.z - t.z), new Vector3(x, c.y + t.y, c.z + t.z), 0.6f, Pal.Ramp(Pal.Wood, 1, 2250 + i));   // paddle
            }
            return g;
        }
    }
}
