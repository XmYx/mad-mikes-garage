using System.Collections.Generic;
using MadMax.Building;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Settlement lighting (lights block), spawned with each chunk's props (<see cref="Populate"/>):
    /// <para>Street lamps along the roads through and into every settlement, deterministic from the road network: inside
    /// a town every 24 m on alternating sides (villages: wooden pole lamps; towns: lantern posts in the old centre,
    /// cobra-head lamps further out; cities keep their own streetlights and get cobra heads), on the approaches every 40 m
    /// out to 80 m past the edge (paved: cobra heads, dirt: pole lamps; beyond the town grid settlers put up solar posts),
    /// and a floodlight mast at the junction in the middle of every town and city.</para>
    /// <para>Building fixtures (<see cref="BuildingLights"/>): farmhouses a wall lamp, brick houses a pendant per floor,
    /// shops two strip lights, shacks a pendant, ruined towers a strip light; each with a switch inside by the door.</para>
    /// Inhabited settlements are on their town grid (<see cref="TownPowered"/>; <see cref="Blackout"/> darkens one);
    /// ruins are dead. Lamps smashed or knocked over are remembered (<see cref="Smashed"/>, <see cref="Knocked"/>; saved
    /// in <c>SaveData.blockLights</c>).</summary>
    public static class TownLights
    {
        public struct Spot { public Vector2 pos; public float yaw; public string piece; public int town; public string key; }

        public static readonly HashSet<string> Smashed = new HashSet<string>();
        public static readonly HashSet<string> Knocked = new HashSet<string>();
        /// <summary>Settlements whose grid is down (story, tests): their lamps and houses stay dark.</summary>
        public static readonly HashSet<int> Blackout = new HashSet<int>();

        static readonly List<Spot> spots = new List<Spot>();
        static readonly Dictionary<Vector2Int, List<int>> byChunk = new Dictionary<Vector2Int, List<int>>();
        static int spotSeed = int.MinValue;
        static Mesh cityBulb;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Smashed.Clear(); Knocked.Clear(); Blackout.Clear(); spots.Clear(); byChunk.Clear(); spotSeed = int.MinValue; cityBulb = null; }

        public static bool TownPowered(int town) => town >= 0 && !Blackout.Contains(town);

        /// <summary>Every planned street lamp of the world (built on first use).</summary>
        public static IReadOnlyList<Spot> Spots(WorldGen world) { Plan(world); return spots; }

        // ------------------------------------------------------------------ save

        public static void Save(List<string> into)
        {
            foreach (var k in Smashed) into.Add("x:" + k);
            foreach (var k in Knocked) into.Add("k:" + k);
        }

        public static void Load(List<string> from)
        {
            Smashed.Clear(); Knocked.Clear();
            if (from == null) return;
            foreach (var s in from)
            {
                if (s == null || s.Length < 3) continue;
                if (s.StartsWith("x:")) Smashed.Add(s.Substring(2));
                else if (s.StartsWith("k:")) Knocked.Add(s.Substring(2));
            }
        }

        public static void NewGame() { Smashed.Clear(); Knocked.Clear(); Blackout.Clear(); }

        // ------------------------------------------------------------------ plan

        static void Plan(WorldGen world)
        {
            if (world == null || spotSeed == world.seed) return;
            spotSeed = world.seed;
            spots.Clear(); byChunk.Clear();
            var hits = new List<RoadHit>();
            var buildings = new List<(string id, Vector2 pos, float yaw)>();
            var anchors = new Dictionary<int, List<Vector2>>();
            List<Vector2> Anchors(Settlement st)
            {
                if (anchors.TryGetValue(st.index, out var l)) return l;
                BiomeProps.Buildings(world, st, buildings);
                l = new List<Vector2>();
                foreach (var b in buildings) l.Add(b.pos);
                return anchors[st.index] = l;
            }
            bool Usable(Vector2 p, Settlement st)
            {
                if (world.Reserved(p.x, p.y) || world.YardWeight(p.x, p.y) > 0.05f) return false;
                var s = world.Sample(p.x, p.y);
                if (!float.IsNaN(s.water) || s.feature != 0) return false;
                hits.Clear();
                world.roads.QueryAll(p.x, p.y, hits);
                foreach (var h in hits) if (h.dist < h.width * 0.5f + 0.7f) return false;
                foreach (var a in Anchors(st)) if ((a - p).sqrMagnitude < 6.5f * 6.5f) return false;
                foreach (var o in spots) if (o.town == st.index && (o.pos - p).sqrMagnitude < 8f * 8f) return false;
                return true;
            }
            void Add(Vector2 p, float yaw, string piece, Settlement st)
            {
                var sp = new Spot { pos = p, yaw = yaw, piece = piece, town = st.index, key = "lamp:" + Mathf.RoundToInt(p.x * 2f) + "," + Mathf.RoundToInt(p.y * 2f) };
                var c = DeformableTerrain.ChunkOf(new Vector3(p.x, 0f, p.y));
                if (!byChunk.TryGetValue(c, out var l)) byChunk[c] = l = new List<int>();
                l.Add(spots.Count);
                spots.Add(sp);
            }

            // junction masts in the middle of towns and cities
            foreach (var st in world.settlements)
            {
                if (st.kind == Biome.Village) continue;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f + st.index * 0.7f;
                    var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9f;
                    if (!Usable(p, st)) continue;
                    var to = st.pos - p;
                    Add(p, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg, "floodlight_mast", st);
                    break;
                }
            }

            // lamps along every road where it runs through or into a settlement
            for (int ri = 0; ri < world.roads.roads.Count; ri++)
            {
                var road = world.roads.roads[ri];
                float half = road.width * 0.5f;
                float s = 0f, next = 0f;
                int side = 1;
                for (int i = 1; i < road.points.Count; i++)
                {
                    var a = new Vector2(road.points[i - 1].x, road.points[i - 1].z);
                    var b = new Vector2(road.points[i].x, road.points[i].z);
                    float len = Vector2.Distance(a, b);
                    if (len < 0.01f) continue;
                    var t = (b - a) / len;
                    var n = new Vector2(-t.y, t.x);
                    for (float u = 0f; u < len; u += 4f)
                    {
                        float at = s + u;
                        if (at < next) continue;
                        var p = a + t * u;
                        Settlement st = null; float d = float.MaxValue;
                        foreach (var cand in world.settlements)
                        {
                            float dd = Vector2.Distance(cand.pos, p);
                            if (dd < cand.radius + 80f && dd - cand.radius < d) { d = dd - cand.radius; st = cand; }
                        }
                        if (st == null) continue;
                        bool inside = d < -st.radius * 0.05f;
                        if (inside && st.kind == Biome.City && d < -st.radius * 0.15f) continue;     // the city's own streetlights
                        string piece;
                        if (inside)
                            piece = st.kind == Biome.Village ? "streetlamp_pole" : st.kind == Biome.Town && d + st.radius < st.radius * 0.6f ? "streetlamp_gas" : "streetlamp_cobra";
                        else if (d > 30f) piece = "streetlamp_solar";
                        else piece = road.paved ? "streetlamp_cobra" : "streetlamp_pole";
                        float spacing = inside ? 24f : 40f;
                        int sd = inside ? side : 1;
                        var q = p + n * (sd * (half + 1.2f));
                        if (!Usable(q, st)) { next = at + 4f; continue; }
                        var face = -n * sd;
                        Add(q, Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg, piece, st);
                        next = at + spacing;
                        side = -side;
                    }
                    s += len;
                }
            }
        }

        // ------------------------------------------------------------------ spawn

        /// <summary>Street lamps and building fixtures whose anchor lies in chunk <paramref name="c"/> (called after the
        /// chunk's settlement pieces are spawned).</summary>
        public static void Populate(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat)
        {
            var world = terrain ? terrain.World : null;
            if (world == null || !mat) return;
            Plan(world);
            if (byChunk.TryGetValue(c, out var list))
                foreach (int i in list)
                {
                    var sp = spots[i];
                    var pos = new Vector3(sp.pos.x, terrain.Height(sp.pos.x, sp.pos.y) - 0.04f, sp.pos.y);
                    var pl = FurnitureLibrary.WorldFixture(sp.piece, parent, pos, Quaternion.Euler(0f, sp.yaw, 0f), mat);
                    if (!pl) continue;
                    pl.gameObject.name = "StreetLamp";
                    var wl = pl.gameObject.AddComponent<WorldLamp>();
                    wl.key = sp.key; wl.town = sp.town;
                    FloraBlocker.Add(pl.gameObject, 0.02f);
                }
            Buildings(world, c, parent, mat);
        }

        static readonly List<(string id, Vector2 pos, float yaw)> placed = new List<(string, Vector2, float)>();

        static void Buildings(WorldGen world, Vector2Int c, Transform parent, Material mat)
        {
            DestructibleVoxels[] here = null;
            foreach (var st in world.settlements)
            {
                float cx = (c.x + 0.5f) * DeformableTerrain.ChunkWorld, cz = (c.y + 0.5f) * DeformableTerrain.ChunkWorld;
                if (Mathf.Abs(st.pos.x - cx) > st.radius + 20f || Mathf.Abs(st.pos.y - cz) > st.radius + 20f) continue;
                BiomeProps.Buildings(world, st, placed);
                foreach (var b in placed)
                {
                    if (DeformableTerrain.ChunkOf(new Vector3(b.pos.x, 0f, b.pos.y)) != c) continue;
                    if (!HasFixtures(b.id)) continue;
                    if (here == null) here = parent.GetComponentsInChildren<DestructibleVoxels>();
                    foreach (var dv in here)
                    {
                        if (!dv || dv.TemplateId != b.id) continue;
                        var p = dv.transform.position;
                        if ((new Vector2(p.x, p.z) - b.pos).sqrMagnitude > 0.05f) continue;
                        Fit(dv, b.id, st.index, mat);
                        break;
                    }
                }
            }
        }

        static bool HasFixtures(string id) => id.StartsWith("Farmhouse") || id.StartsWith("BrickHouse") || id.StartsWith("Shop") || id.StartsWith("Shack") || id.StartsWith("Tower");

        struct Fix { public string piece; public Vector3Int v; public Vector3 normal; }

        /// <summary>Fixture layout of a building template (voxel coordinates of the surface voxel each one hangs on, and the
        /// normal pointing into the room), its switch, and whether it is a ruin.</summary>
        static bool Layout(string id, List<Fix> fixes, out Fix sw, out bool ruin)
        {
            fixes.Clear(); ruin = false; sw = default;
            int s = id.Length > 0 && char.IsDigit(id[id.Length - 1]) ? id[id.Length - 1] - '0' : 0;
            var down = Vector3.down;
            if (id.StartsWith("Farmhouse"))
            {
                int l = 16;
                fixes.Add(new Fix { piece = "light_wall", v = new Vector3Int(0, 14, l), normal = Vector3.back });
                sw = new Fix { piece = "light_switch", v = new Vector3Int(5, 8, -l), normal = Vector3.forward };
            }
            else if (id.StartsWith("BrickHouse"))
            {
                int l = 20, storey = 18;
                fixes.Add(new Fix { piece = "light_pendant", v = new Vector3Int(-10, storey + 1, 0), normal = down });
                fixes.Add(new Fix { piece = "light_pendant", v = new Vector3Int(-10, storey * 2 + 2, 0), normal = down });
                sw = new Fix { piece = "light_switch", v = new Vector3Int(5, 8, -l), normal = Vector3.forward };
            }
            else if (id.StartsWith("Shop"))
            {
                int w = 30, l = 22, h = 16;
                fixes.Add(new Fix { piece = "light_strip", v = new Vector3Int(-12, h + 1, 0), normal = down });
                fixes.Add(new Fix { piece = "light_strip", v = new Vector3Int(12, h + 1, 0), normal = down });
                sw = new Fix { piece = "light_switch", v = new Vector3Int(-w, 6, -l + 4), normal = Vector3.right };
            }
            else if (id.StartsWith("Shack"))
            {
                int w = 16 + s * 3, l = 18 + s * 2, h = 34;
                fixes.Add(new Fix { piece = "light_pendant", v = new Vector3Int(0, h + 1 + (w + 2) / 6, 0), normal = down });
                sw = new Fix { piece = "light_switch", v = new Vector3Int(8, 15, -l), normal = Vector3.forward };
            }
            else if (id.StartsWith("Tower"))
            {
                int w = 26 + (s % 2) * 6, l = 22, storey = 15;
                ruin = true;
                fixes.Add(new Fix { piece = "light_strip", v = new Vector3Int(-Mathf.RoundToInt(w * 0.4f), storey + 1, 0), normal = down });
                sw = new Fix { piece = "light_switch", v = new Vector3Int(5, 6, -l), normal = Vector3.forward };
            }
            else return false;
            return true;
        }

        static readonly List<Fix> fixes = new List<Fix>();

        static void Fit(DestructibleVoxels dv, string id, int town, Material mat)
        {
            if (dv.GetComponent<BuildingLights>() || !Layout(id, fixes, out var sw, out bool ruin)) return;
            var grid = dv.Grid;
            if (grid == null || !grid.Has(sw.v)) return;                                    // no wall by the door any more
            float size = dv.voxelSize;
            var bl = dv.gameObject.AddComponent<BuildingLights>();
            bl.town = town; bl.ruin = ruin;
            bl.bedtime = 22f + (float)new System.Random(Mathf.RoundToInt(dv.transform.position.x * 7f) * 7919 ^ Mathf.RoundToInt(dv.transform.position.z * 13f)).NextDouble() * 2f;
            var t = dv.transform;
            foreach (var f in fixes)
            {
                if (!grid.Has(f.v)) continue;
                var local = ((Vector3)f.v + f.normal * 0.5f) * size;                           // on the voxel's face
                var rot = f.normal == Vector3.down ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.LookRotation(Vector3.up, f.normal);
                var pl = FurnitureLibrary.WorldFixture(f.piece, t, t.TransformPoint(local), t.rotation * rot, mat);
                if (!pl) continue;
                bl.lights.Add(pl);
                bl.Mount(pl.transform, f.v);
            }
            var swLocal = ((Vector3)sw.v + sw.normal * 0.5f) * size;
            var ls = FurnitureLibrary.WorldSwitch(t, t.TransformPoint(swLocal), t.rotation * Quaternion.LookRotation(Vector3.up, sw.normal), mat);
            ls.group = t;
            ls.on = false;
            bl.lightSwitch = ls;
            bl.Mount(ls.transform, sw.v);
        }

        // ------------------------------------------------------------------ city streetlights (BiomeProps "Light" street spots)

        /// <summary>Turn a city streetlight's lamp spot (<paramref name="go"/> at the head of a <c>Streetlight0</c> prop)
        /// into a town-grid lamp: point light, glowing bulb under the head, smashable head (key from the placement).</summary>
        public static void CityLamp(GameObject go, int town, int index)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.type = LightType.Point; l.shadows = LightShadows.None;
            l.color = new Color(1f, 0.78f, 0.45f); l.range = 16f; l.intensity = 30f; l.enabled = false;
            var pl = go.AddComponent<PoweredLight>();
            pl.needsPower = false; pl.outdoor = true; pl.external = true; pl.watts = 150f;
            var mat = DeformableTerrain.Instance ? DeformableTerrain.Instance.worldPropMaterial : null;
            if (mat)
            {
                if (!cityBulb)
                {
                    var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Glass);
                    g.Box(-1, 0, -1, 1, 0, 1, Pal.Solid(Pal.LightY));
                    cityBulb = VoxelMesher.Build(g, "CityLampBulb");
                }
                var b = new GameObject("Bulb", typeof(MeshFilter), typeof(MeshRenderer));
                b.transform.SetParent(go.transform, false);
                b.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                b.GetComponent<MeshFilter>().sharedMesh = cityBulb;
                var r = b.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pl.SetBulb(r);
            }
            var head = go.AddComponent<SphereCollider>();
            head.radius = 0.3f; head.center = new Vector3(0f, -0.05f, 0f);
            go.AddComponent<LampHead>().lamp = pl;
            var wl = go.AddComponent<WorldLamp>();
            wl.key = "city:" + town + "," + index; wl.town = town;
        }
    }
}
