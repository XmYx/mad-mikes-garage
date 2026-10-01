using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Lighting block: the wall light switch (<see cref="LightSwitch"/>), indoor fixtures (wall sconce, cabin
    /// strip light, pendant) and five street lights that switch themselves on at dusk and off at dawn when fed: tall
    /// cobra-head highway lamp, old gas-style lantern post, wooden pole with a hanging bulb, floodlight mast and a solar
    /// post with its own panel and battery. Each lamp's glass is a separate "Bulb" mesh that glows with an unlit material
    /// (reads as lit beyond the point-light budget) and street lamps have a smashable "Head". The same fixtures are spawned
    /// into world buildings and along town roads (<see cref="WorldFixture"/>, <c>World/TownLights</c>) without a network
    /// node or a Placeable.</summary>
    public static partial class FurnitureLibrary
    {
        /// <summary>Where a lamp's light sits and what it gives off (local metres).</summary>
        public class LampSpec
        {
            public Vector3 light;
            public Color color;
            public float range, intensity, watts;
            public Vector3 headCenter, headSize;     // smashable head (zero: the whole fixture is the head)
            public float pole;                       // ground lamps: the post's height (collider), 0 = fixture
            public bool outdoor;
        }

        static readonly Dictionary<string, LampSpec> lampSpecs = new Dictionary<string, LampSpec>();
        static readonly Dictionary<string, Mesh> lampBulbs = new Dictionary<string, Mesh>();

        /// <summary>Street light pieces, tallest first.</summary>
        public static readonly string[] StreetLamps = { "floodlight_mast", "streetlamp_cobra", "streetlamp_pole", "streetlamp_solar", "streetlamp_gas" };

        public static LampSpec Lamp(string id) { Ensure(); return lampSpecs.TryGetValue(id, out var s) ? s : null; }

        static IEnumerable<FurnitureDef> LightsPieces()
        {
            var Fu = BuildCategory.Furniture; var U = BuildCategory.Utility;
            var S = ResourceType.Scrap; var G = ResourceType.Glass; var Cu = ResourceType.Copper;
            Color warm = new Color(1f, 0.82f, 0.58f), white = new Color(1f, 0.94f, 0.82f), sodium = new Color(1f, 0.7f, 0.38f), led = new Color(0.9f, 0.95f, 1f);

            yield return Kit("light_switch", "LIGHT SWITCH", SwitchGrid(), LightIds.SwitchKit, Fu).With(go =>
            {
                Node(go, UtilityKind.Power, 0f);
                LightSwitch.FitLever(go, go.GetComponent<MeshRenderer>().sharedMaterial);
                go.AddComponent<LightSwitch>();
            });

            // indoor fixtures: raw cost, built straight from the menu
            yield return LampPiece("light_wall", "WALL LAMP", Fu, SconceGrid(), null, 2, 0.1f,
                new LampSpec { light = new Vector3(0f, 0.26f, 0.12f), color = warm, range = 6f, intensity = 6f, watts = 40f }, (S, 1), (G, 1), (Cu, 1));
            yield return LampPiece("light_strip", "STRIP LIGHT", Fu, StripGrid(), null, 2, 0f,
                new LampSpec { light = new Vector3(0f, 0.2f, 0f), color = white, range = 6f, intensity = 6f, watts = 20f }, (S, 1), (G, 1), (Cu, 1));
            yield return LampPiece("light_pendant", "PENDANT LAMP", Fu, PendantGrid(), null, 2, 0f,
                new LampSpec { light = new Vector3(0f, 0.86f, 0f), color = warm, range = 7f, intensity = 8f, watts = 60f }, (S, 1), (G, 1), (Cu, 1));

            // street lights: kits from the workbench, dusk to dawn
            yield return LampPiece("streetlamp_cobra", "HIGHWAY LAMP", U, CobraGrid(), LightIds.CobraKit, 10, 0.3f,
                new LampSpec { light = new Vector3(-0.04f, 6.98f, 2.16f), color = sodium, range = 22f, intensity = 40f, watts = 150f, headCenter = new Vector3(-0.04f, 7.2f, 2.2f), headSize = new Vector3(0.5f, 0.42f, 1f), pole = 7f, outdoor = true });
            yield return LampPiece("streetlamp_gas", "LANTERN POST", U, LanternGrid(), LightIds.LanternKit, 8, 0.3f,
                new LampSpec { light = new Vector3(-0.04f, 2.76f, -0.04f), color = new Color(1f, 0.74f, 0.42f), range = 12f, intensity = 14f, watts = 60f, headCenter = new Vector3(-0.04f, 2.84f, -0.04f), headSize = new Vector3(0.6f, 0.9f, 0.6f), pole = 2.5f, outdoor = true });
            yield return LampPiece("streetlamp_pole", "POLE LAMP", U, PoleLampGrid(), LightIds.PoleKit, 6, 0.3f,
                new LampSpec { light = new Vector3(0f, 3.92f, 1.04f), color = warm, range = 14f, intensity = 18f, watts = 60f, headCenter = new Vector3(0f, 4.08f, 1.04f), headSize = new Vector3(0.45f, 0.35f, 0.45f), pole = 5.1f, outdoor = true });
            yield return LampPiece("floodlight_mast", "FLOODLIGHT MAST", U, MastGrid(), LightIds.MastKit, 16, 0.3f,
                new LampSpec { light = new Vector3(0f, 8.04f, 0.7f), color = white, range = 34f, intensity = 70f, watts = 400f, headCenter = new Vector3(0f, 8.05f, 0.34f), headSize = new Vector3(1.1f, 0.56f, 0.45f), pole = 7.9f, outdoor = true });
            yield return LampPiece("streetlamp_solar", "SOLAR LAMP", U, SolarPostGrid(), LightIds.SolarLampKit, 6, 0.3f,
                new LampSpec { light = new Vector3(-0.04f, 3.22f, 0.68f), color = led, range = 14f, intensity = 14f, watts = 25f, headCenter = new Vector3(-0.04f, 3.4f, 0.7f), headSize = new Vector3(0.4f, 0.3f, 0.6f), pole = 3.7f, outdoor = true },
                extra: go => { var n = go.GetComponent<UtilityNode>(); n.batteryWh = 400f; n.batteryCharge = 200f; go.AddComponent<SolarPost>(); });
        }

        /// <summary>A lamp piece: its "bulb" voxels become a separate glowing mesh, a power node at <paramref name="portY"/>.</summary>
        static FurnitureDef LampPiece(string id, string name, BuildCategory cat, VoxelGrid g, string kit, int hits, float portY, LampSpec spec, params (ResourceType, int)[] cost) =>
            LampPiece(id, name, cat, g, kit, hits, portY, spec, null, cost);

        static FurnitureDef LampPiece(string id, string name, BuildCategory cat, VoxelGrid g, string kit, int hits, float portY, LampSpec spec, System.Action<GameObject> extra, params (ResourceType, int)[] cost)
        {
            var bulb = g.Take("bulb", Vector3Int.zero);
            lampBulbs[id] = bulb.Count > 0 ? VoxelMesher.Build(bulb, "Furniture_" + id + "_bulb") : null;
            lampSpecs[id] = spec;
            var d = kit != null ? Kit(id, name, g, kit, cat) : Def(id, name, g, cost);
            d.category = cat; d.hits = hits;
            d.setup = go => { Node(go, UtilityKind.Power, portY); FitLamp(go, id, true); extra?.Invoke(go); };
            return d;
        }

        /// <summary>Light, glowing bulb, smashable head and a post-sized collider for lamp <paramref name="id"/>.</summary>
        static PoweredLight FitLamp(GameObject go, string id, bool powered)
        {
            var sp = lampSpecs[id];
            Glow(go, sp.light, sp.color, sp.range, sp.intensity, powered, sp.watts);
            var pl = go.GetComponent<PoweredLight>();
            pl.outdoor = sp.outdoor;
            if (lampBulbs.TryGetValue(id, out var bm) && bm)
            {
                var b = new GameObject("Bulb", typeof(MeshFilter), typeof(MeshRenderer));
                b.transform.SetParent(go.transform, false);
                b.GetComponent<MeshFilter>().sharedMesh = bm;
                var r = b.GetComponent<MeshRenderer>();
                r.sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pl.SetBulb(r);
            }
            if (sp.headSize != Vector3.zero) LampHead.Fit(go, sp.headCenter, sp.headSize);
            if (sp.pole > 0f && go.TryGetComponent<BoxCollider>(out var box)) { box.center = new Vector3(0f, sp.pole * 0.5f, 0f); box.size = new Vector3(0.32f, sp.pole, 0.32f); }
            return pl;
        }

        /// <summary>A lamp or fixture in the world (town street lamps, a house's lights): mesh, collider, light, bulb and
        /// head, fed from outside the network (<see cref="PoweredLight.external"/>), smashable, not a Placeable.</summary>
        public static PoweredLight WorldFixture(string id, Transform parent, Vector3 worldPos, Quaternion worldRot, Material mat)
        {
            var def = Get(id);
            if (def == null || !lampSpecs.ContainsKey(id)) return null;
            var go = new GameObject("Fixture_" + id, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var box = go.AddComponent<BoxCollider>();
            box.center = def.mesh.bounds.center; box.size = def.mesh.bounds.size;
            var pl = FitLamp(go, id, false);
            pl.external = true;
            if (lampSpecs[id].headSize == Vector3.zero) go.AddComponent<LampHead>().lamp = pl;    // a house fixture: any hit smashes it
            DressHD(go, id, MadMax.World.HDProp.FlagsOf(mat));
            return pl;
        }

        /// <summary>A world building's light switch (plate, lever, box collider; [E] through the part interactables).</summary>
        public static LightSwitch WorldSwitch(Transform parent, Vector3 worldPos, Quaternion worldRot, Material mat)
        {
            var def = Get("light_switch");
            var go = new GameObject("LightSwitch", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var box = go.AddComponent<BoxCollider>();
            box.center = def.mesh.bounds.center + new Vector3(0f, 0.04f, 0f); box.size = def.mesh.bounds.size + new Vector3(0.04f, 0.08f, 0.04f);
            LightSwitch.FitLever(go, mat);
            var sw = go.AddComponent<LightSwitch>();
            DressHD(go, "light_switch", MadMax.World.HDProp.FlagsOf(mat));
            return sw;
        }

        // ------------------------------------------------------------------ grids (wall pieces: XZ plane, +Y out, +Z up)

        static VoxelGrid SwitchGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-1, 0, -2, 1, 0, 2, Pal.Ramp(Pal.Cream, 3, 3101));
            g.Set(0, 0, 2, Pal.Solid(Pal.Chrome[1])); g.Set(0, 0, -2, Pal.Solid(Pal.Chrome[1]));          // screws
            return g;
        }

        static VoxelGrid SconceGrid()
        {
            var g = new VoxelGrid().Mat(Copper);
            g.Box(-1, 0, -1, 1, 0, 1, Pal.Ramp(Pal.Bronze, 1, 3111));                                      // back plate
            g.Box(0, 1, -1, 0, 2, -1, Pal.Ramp(Pal.Bronze, 2, 3112));                                       // arm
            g.Box(-2, 2, -1, 2, 4, -1, Pal.Ramp(Pal.Bronze, 2, 3113));                                      // cup
            g.Mat(Glass).Use("bulb");
            g.Box(-1, 2, 0, 1, 4, 2, Pal.Solid(Pal.LightW));
            g.Use("body");
            return g;
        }

        static VoxelGrid StripGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-2, 0, -7, 2, 0, 7, Pal.Weathered(Pal.Cream, 0.2f, 3121, 2, 0));
            g.Box(-2, 1, -7, 2, 1, -7, Pal.Ramp(Pal.Metal, 2)); g.Box(-2, 1, 7, 2, 1, 7, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Glass).Use("bulb");
            g.Box(-1, 1, -6, 1, 1, 6, Pal.Solid(Pal.LightW));
            g.Use("body");
            return g;
        }

        static VoxelGrid PendantGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-1, 0, -1, 1, 0, 1, Pal.Ramp(Pal.Metal, 2));                                              // ceiling rose
            g.Box(0, 1, 0, 0, 7, 0, Pal.Ramp(Pal.Black, 1));                                                // flex
            g.Box(-2, 8, -2, 2, 8, 2, Pal.Ramp(Pal.Moss, 2, 3131));                                         // enamel shade
            for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) == 3) g.Set(x, 9, z, Pal.Ramp(Pal.Moss, 3, 3132));
            g.Mat(Glass).Use("bulb");
            g.Box(-1, 9, -1, 1, 10, 1, Pal.Solid(Pal.LightY));
            g.Use("body");
            return g;
        }

        // ------------------------------------------------------------------ street lights (floor pieces, +Z toward the road)

        static VoxelGrid CobraGrid()
        {
            var g = new VoxelGrid().Mat(Concrete);
            g.Box(-3, 0, -2, 2, 2, 3, Pal.Weathered(Pal.Cream, 0.25f, 3141, 1, 0));                         // footing
            g.Mat(Iron);
            var steel = Pal.Weathered(Pal.Chrome, 0.35f, 3142, 1, 0);
            g.Box(-1, 3, -1, 0, 84, 0, steel);
            g.Box(-2, 3, -2, 1, 6, 1, steel);                                                               // base flange
            g.Box(-1, 40, 1, 0, 43, 1, Pal.Ramp(Pal.Metal, 2));                                             // inspection hatch
            g.Tube(new Vector3(-0.5f, 84, -0.5f), new Vector3(-0.5f, 90, 8), 0.7f, steel);                  // swan neck
            g.Tube(new Vector3(-0.5f, 90, 8), new Vector3(-0.5f, 91, 23), 0.7f, steel);
            g.Box(-2, 89, 22, 1, 91, 32, Pal.Ramp(Pal.Chrome, 1, 3143));                                    // cobra head
            g.Box(-1, 92, 24, 0, 92, 30, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Glass).Use("bulb");
            g.Box(-1, 88, 23, 0, 88, 31, Pal.Solid(Pal.LightY));
            g.Use("body");
            return g;
        }

        static VoxelGrid LanternGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-2, 0, -2, 1, 3, 1, Pal.Ramp(Pal.Black, 2, 3151));
            g.Box(-1, 4, -1, 0, 30, 0, p => (p.x + p.z + p.y / 2) % 2 == 0 ? Pal.Black[1] : Pal.Black[2]);  // fluted post
            g.Box(-2, 12, -2, 1, 12, 1, Pal.Ramp(Pal.Black, 3)); g.Box(-2, 28, -2, 1, 28, 1, Pal.Ramp(Pal.Black, 3));
            g.Box(-4, 30, -1, 3, 30, 0, Pal.Ramp(Pal.Black, 2));                                            // ladder bar
            g.Box(-3, 31, -3, 2, 31, 2, Pal.Ramp(Pal.Black, 2));
            foreach (int x in new[] { -3, 2 }) foreach (int z in new[] { -3, 2 }) g.Box(x, 32, z, x, 37, z, Pal.Ramp(Pal.Black, 1));
            g.Box(-4, 38, -4, 3, 38, 3, Pal.Ramp(Pal.Black, 2));                                            // roof
            g.Box(-3, 39, -3, 2, 39, 2, Pal.Ramp(Pal.Black, 1));
            g.Box(-2, 40, -2, 1, 40, 1, Pal.Ramp(Pal.Black, 2));
            g.Box(-1, 41, -1, 0, 42, 0, Pal.Ramp(Pal.Bronze, 2));                                           // finial
            g.Mat(Glass).Use("bulb");                                                                       // the panes glow
            g.Box(-2, 32, -3, 1, 37, -3, Pal.Solid(Pal.LightY)); g.Box(-2, 32, 2, 1, 37, 2, Pal.Solid(Pal.LightY));
            g.Box(-3, 32, -2, -3, 37, 1, Pal.Solid(Pal.LightY)); g.Box(2, 32, -2, 2, 37, 1, Pal.Solid(Pal.LightY));
            g.Use("body");
            return g;
        }

        static VoxelGrid PoleLampGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-1, 0, -1, 0, 64, 0, Pal.Stripe(Pal.Ramp(Pal.Wood, 1, 3161), Pal.Ramp(Pal.Wood, 0, 3162), 1, 7));
            g.Box(-1, 58, 1, 0, 59, 14, Pal.Ramp(Pal.Wood, 2, 3163));                                       // crossarm
            g.Tube(new Vector3(-0.5f, 50, 0.5f), new Vector3(-0.5f, 58, 8), 0.5f, Pal.Ramp(Pal.Wood, 1, 3164));   // brace
            g.Mat(Glass); g.Set(-1, 60, 4, Pal.Solid(Pal.Glass[3])); g.Set(0, 60, 10, Pal.Solid(Pal.Glass[3]));   // insulators
            g.Mat(Scrap);
            g.Box(0, 53, 13, 0, 57, 13, Pal.Ramp(Pal.Black, 0));                                            // flex
            g.Box(-1, 52, 12, 1, 52, 14, Pal.Ramp(Pal.Moss, 2, 3165));                                      // enamel shade
            for (int x = -2; x <= 2; x++) for (int z = 11; z <= 15; z++) if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z - 13)) == 2) g.Set(x, 51, z, Pal.Ramp(Pal.Moss, 3, 3166));
            g.Mat(Glass).Use("bulb");
            g.Box(-1, 50, 12, 1, 51, 14, Pal.Solid(Pal.LightY));
            g.Use("body");
            return g;
        }

        static VoxelGrid MastGrid()
        {
            var g = new VoxelGrid().Mat(Concrete);
            g.Box(-5, 0, -5, 5, 1, 5, Pal.Weathered(Pal.Cream, 0.3f, 3171, 1, 0));
            g.Mat(Iron);
            var leg = Pal.Weathered(Pal.Ochre, 0.4f, 3172, 2, 0);                                           // yellow paint, rust
            foreach (int x in new[] { -3, 3 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 2, z, x, 96, z, leg);
            for (int y = 8; y <= 92; y += 7)                                                                // lattice rings
            {
                g.Box(-3, y, -3, 3, y, -3, leg); g.Box(-3, y, 3, 3, y, 3, leg);
                g.Box(-3, y, -3, -3, y, 3, leg); g.Box(3, y, -3, 3, y, 3, leg);
            }
            g.Box(1, 2, -2, 1, 95, -2, Pal.Ramp(Pal.Black, 0));                                             // feed cable
            g.Box(-5, 97, -5, 5, 97, 5, Pal.Ramp(Pal.Metal, 2, 3173));                                      // platform
            foreach (int x in new[] { -5, 5 }) foreach (int z in new[] { -5, 5 }) g.Box(x, 98, z, x, 101, z, Pal.Ramp(Pal.Metal, 1));
            g.Box(-6, 98, 2, -1, 103, 5, Pal.Ramp(Pal.Chrome, 1, 3174)); g.Box(1, 98, 2, 6, 103, 5, Pal.Ramp(Pal.Chrome, 1, 3175));
            g.Mat(Glass).Use("bulb");
            g.Box(-5, 99, 6, -2, 102, 6, Pal.Solid(Pal.LightW)); g.Box(2, 99, 6, 5, 102, 6, Pal.Solid(Pal.LightW));
            g.Use("body");
            return g;
        }

        static VoxelGrid SolarPostGrid()
        {
            var g = new VoxelGrid().Mat(Concrete);
            g.Box(-2, 0, -2, 1, 1, 1, Pal.Weathered(Pal.Cream, 0.3f, 3181, 1, 0));
            g.Mat(Iron);
            g.Box(-1, 2, -1, 0, 46, 0, Pal.Ramp(Pal.Chrome, 1, 3182));
            g.Box(-3, 14, -4, 2, 20, -2, p => p.y % 2 == 0 ? Pal.Black[1] : Pal.Black[2]);                   // battery box
            g.Box(-1, 18, -5, 0, 18, -5, Pal.Solid(Pal.Amber));                                              // charge lamp
            g.Box(-1, 44, 1, 0, 44, 7, Pal.Ramp(Pal.Chrome, 2));
            g.Box(-2, 42, 6, 1, 43, 11, Pal.Ramp(Pal.Black, 3, 3183));
            g.Box(-1, 47, -1, 0, 48, 0, Pal.Ramp(Pal.Chrome, 1));
            for (int z = -7; z <= 5; z++)                                                                    // tilted panel
            {
                int y = 49 + (z + 7) / 3;
                g.Box(-6, y, z, 5, y, z, p => Mathf.Abs(p.x + 0.5f) > 5f || p.z == -7 || p.z == 5 ? Pal.Chrome[2] : (p.x + 20) % 3 == 0 || (p.z + 20) % 4 == 0 ? Pal.Navy[1] : Pal.Navy[3]);
            }
            g.Mat(Glass).Use("bulb");
            g.Box(-1, 41, 7, 0, 41, 10, Pal.Solid(Pal.LightW));
            g.Use("body");
            return g;
        }
    }
}
