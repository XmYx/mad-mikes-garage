using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Voxel structures of <see cref="Site"/>s, built on worker threads ahead of the player and spawned as
    /// destructible props when their anchor chunk loads. Bunker: one piece (0.2 m voxels) with concrete walls, doorways,
    /// merged halls, pillars, a roof slab under a sod layer flush with the graded ground, ramp retaining walls, a blast
    /// door, pipes, surface vents and sandbags; plus lamps and loot spots. Rock tunnel: 8 m segments (0.25 m voxels)
    /// of rock walls up to the mesa surface, an arched roof where the mesa is high enough, solid portal faces, drips and
    /// boulders. Template ids are <c>site:{cell}:{piece}</c> so saved destruction restores through
    /// <see cref="PropLibrary.TemplateGrid"/>.</summary>
    public static class SiteBuilder
    {
        class Extra { public string kind, table, visual; public Vector3 local; public float yaw; public bool dying; }

        class Piece
        {
            public string id;
            public Site site;
            public int index;
            public Vector3 pos;
            public float yaw, size;
            public Task<(VoxelGrid grid, VoxelMesher.MeshData data, List<Extra> extras)> job;
            public VoxelGrid grid;
            public Mesh mesh;
            public List<Extra> extras;
        }

        static readonly Dictionary<string, Piece> pieces = new Dictionary<string, Piece>();
        static readonly List<Site> near = new List<Site>();
        static int worldSeed = int.MinValue;
        static float nextPrewarm;
        const float BunkerVoxel = 0.2f, TunnelVoxel = 0.25f, SegmentLen = 8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pieces.Clear(); worldSeed = int.MinValue; nextPrewarm = 0f; }

        static void CheckWorld(WorldGen world)
        {
            if (world.seed == worldSeed) return;
            foreach (var p in pieces.Values) if (p.mesh) Object.Destroy(p.mesh);
            pieces.Clear();
            worldSeed = world.seed;
        }

        static int PieceCount(Site s) => s.kind == SiteKind.Bunker || s.kind == SiteKind.Airfield ? 1 : Mathf.CeilToInt((s.halfLen * 2f + 4f) / SegmentLen);

        static VoxelGrid Build(WorldGen world, Site s, int index, Vector3 pos, List<Extra> extras) =>
            s.kind == SiteKind.Bunker ? Bunker(world, s, extras) : s.kind == SiteKind.Airfield ? Airfield(s, extras) : Tunnel(world, s, index, pos, extras);

        static Piece Get(WorldGen world, Site s, int index)
        {
            string id = $"site:{s.Key}:{index}";
            if (pieces.TryGetValue(id, out var p)) return p;
            p = new Piece { id = id, site = s, index = index };
            if (s.kind == SiteKind.Bunker)
            {
                var w = s.pos;
                p.pos = new Vector3(w.x, s.floor - BunkerVoxel * 0.5f, w.y);
                p.size = BunkerVoxel;
            }
            else if (s.kind == SiteKind.Airfield)
            {
                var w = s.ToWorld(Site.ApronX, 0f);
                p.pos = new Vector3(w.x, s.top + BunkerVoxel * 0.5f, w.y);
                p.size = BunkerVoxel;
            }
            else
            {
                float zc = -s.halfLen - 2f + (index + 0.5f) * SegmentLen;
                var w = s.ToWorld(0f, zc);
                p.pos = new Vector3(w.x, world.BaseHeight(w.x, w.y), w.y);
                p.size = TunnelVoxel;
            }
            p.yaw = s.rot * 90f;
            pieces[id] = p;
            return p;
        }

        static void Start(WorldGen world, Piece p)
        {
            if (p.job != null || p.grid != null) return;
            var s = p.site; int index = p.index; var pos = p.pos;
            p.job = Task.Run(() =>
            {
                var extras = new List<Extra>();
                var g = Build(world, s, index, pos, extras);
                g.Bevel();
                return (g, VoxelMesher.BuildData(g, s.kind == SiteKind.Outcrop ? TunnelVoxel : BunkerVoxel), extras);
            });
        }

        /// <summary>Start building the structures of sites around the focus; forget far ones.</summary>
        public static void Prewarm(WorldGen world, Vector3 focus, float radius)
        {
            if (world == null || Time.time < nextPrewarm) return;
            nextPrewarm = Time.time + 1f;
            CheckWorld(world);
            world.SitesNear(focus, radius, near);
            foreach (var s in near)
                for (int i = 0; i < PieceCount(s); i++) Start(world, Get(world, s, i));
            List<string> drop = null;
            foreach (var kv in pieces)
            {
                if ((kv.Value.site.pos - new Vector2(focus.x, focus.z)).magnitude < radius + kv.Value.site.reach + 150f) continue;
                if (kv.Value.job != null && !kv.Value.job.IsCompleted) continue;
                (drop ??= new List<string>()).Add(kv.Key);
            }
            if (drop != null) foreach (var k in drop) { if (pieces[k].mesh) Object.Destroy(pieces[k].mesh); pieces.Remove(k); }
        }

        /// <summary>Spawn the site pieces anchored in chunk <paramref name="c"/> (now, or as soon as they are built).</summary>
        public static void Populate(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat)
        {
            var world = terrain.World;
            if (world == null || !mat) return;
            CheckWorld(world);
            var centre = new Vector3((c.x + 0.5f) * DeformableTerrain.ChunkWorld, 0f, (c.y + 0.5f) * DeformableTerrain.ChunkWorld);
            world.SitesNear(centre, 8f, near);
            foreach (var s in near)
                for (int i = 0; i < PieceCount(s); i++)
                {
                    var p = Get(world, s, i);
                    if (DeformableTerrain.ChunkOf(p.pos) != c) continue;
                    Start(world, p);
                    if (Ready(p)) Spawn(terrain, p, parent, mat);
                    else parent.gameObject.AddComponent<SitePending>().Init(p.id, mat);
                }
        }

        /// <summary>Waits on the chunk object for a piece that was still being built when the chunk loaded.</summary>
        public class SitePending : MonoBehaviour
        {
            string id; Material mat;
            public void Init(string pieceId, Material m) { id = pieceId; mat = m; }
            void Update()
            {
                if (!pieces.TryGetValue(id, out var p)) { Destroy(this); return; }
                if (!Ready(p)) return;
                if (DeformableTerrain.Instance) Spawn(DeformableTerrain.Instance, p, transform, mat);
                Destroy(this);
            }
        }

        static bool Ready(Piece p)
        {
            if (p.grid != null && p.mesh) return true;
            if (p.job == null || !p.job.IsCompleted) return false;
            if (p.job.IsFaulted) { Debug.LogException(p.job.Exception); p.job = null; return false; }
            var r = p.job.Result;
            p.grid = r.grid; p.extras = r.extras;
            p.mesh = VoxelMesher.ToMesh(r.data, p.id);
            p.job = null;
            return true;
        }

        static void Spawn(DeformableTerrain terrain, Piece p, Transform parent, Material mat)
        {
            if (p.grid == null || p.grid.Count == 0) return;
            var d = DestructibleVoxels.Spawn(p.site.kind == SiteKind.Bunker ? "Bunker" : p.site.kind == SiteKind.Airfield ? "Hangar" : "RockTunnel", p.grid, p.mesh, mat, parent, p.pos, p.yaw, false,
                terrain.DestructionState, p.id, p.size, p.id);
            if (!d) return;
            if (p.site.kind != SiteKind.Airfield) d.gameObject.AddComponent<Subterranean>();
            else RunwayLights.For(p.site, parent, mat);                                      // edge and threshold lights for night landings
            if (p.extras == null) return;
            var q = Quaternion.Euler(0f, p.yaw, 0f);
            int n = 0;
            foreach (var e in p.extras)
            {
                var at = p.pos + q * e.local;
                if (e.kind == "bats")
                {
                    var go = new GameObject("BatColony");
                    go.transform.SetParent(parent, true);
                    go.transform.SetPositionAndRotation(at, q);
                    go.AddComponent<BatColony>();
                }
                else if (e.kind == "oil_lamp")
                {
                    var def = MadMax.Building.FurnitureLibrary.Get("lamp");
                    var go = new GameObject("CampLamp", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(parent, true);
                    go.transform.SetPositionAndRotation(at, q);
                    if (def != null) { go.GetComponent<MeshFilter>().sharedMesh = def.mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat; }
                    var glow = new GameObject("Light"); glow.transform.SetParent(go.transform, false); glow.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                    var l = glow.AddComponent<BunkerLamp>();
                    l.dying = e.dying; l.hum = false; l.color = new Color(1f, 0.72f, 0.4f); l.range = 6f; l.brightness = 2.6f;
                }
                else if (e.kind == "aircraft")
                {
                    // the one flying machine left in the hangar (once per airfield; after that it is a saved vehicle)
                    var g = MadMax.Game.WastelandGame.Instance;
                    if (g && g.FoundAircraft.Add(p.site.Key)) g.SpawnFound(e.visual, at + Vector3.up * 0.4f, q * Quaternion.Euler(0f, e.yaw, 0f));
                }
                else if (e.kind == "lamp")
                {
                    var go = new GameObject("BunkerLamp");
                    go.transform.SetParent(parent, true);
                    go.transform.position = at;
                    go.AddComponent<BunkerLamp>().dying = e.dying;
                }
                else if (e.kind == "loot")
                {
                    var def = MadMax.Building.FurnitureLibrary.Get(e.visual);
                    if (def == null) continue;
                    var lg = new GameObject("LootSpot", typeof(MeshFilter), typeof(MeshRenderer));
                    lg.transform.SetParent(parent, true);
                    lg.transform.SetPositionAndRotation(at, q * Quaternion.Euler(0f, e.yaw, 0f));
                    lg.GetComponent<MeshFilter>().sharedMesh = def.mesh;
                    lg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    var box = lg.AddComponent<BoxCollider>(); box.center = def.mesh.bounds.center; box.size = def.mesh.bounds.size;
                    var loot = lg.AddComponent<Lootable>();
                    loot.key = "S" + p.site.Key + "," + n++; loot.table = e.table;
                    loot.locked = Lootable.RollLocked(loot.key, e.visual);
                    loot.title = e.visual == "locker" ? "LOCKER" : e.visual == "crate" ? "CRATE" : "SHELF";
                }
            }
        }

        /// <summary>Pristine grid of a site piece (save/restore of destroyed voxels).</summary>
        public static VoxelGrid TemplateGrid(string id)
        {
            var world = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            if (world == null || id == null || !id.StartsWith("site:")) return null;
            CheckWorld(world);
            var parts = id.Substring(5).Split(':');
            if (parts.Length != 2) return null;
            var xy = parts[0].Split(',');
            var s = world.SiteIn(new Vector2Int(int.Parse(xy[0]), int.Parse(xy[1])));
            if (s == null) return null;
            var p = Get(world, s, int.Parse(parts[1]));
            if (p.grid != null) return p.grid;
            if (p.job != null) { p.job.Wait(); Ready(p); return p.grid; }
            var extras = new List<Extra>();
            var g = Build(world, s, p.index, p.pos, extras);
            g.Bevel();
            p.grid = g; p.extras = extras;
            p.mesh = VoxelMesher.Build(g, p.id, p.size);
            return g;
        }

        // ------------------------------------------------------------------ airfield (worker thread)

        /// <summary>Hangar complex beside an old airstrip (0.2 m voxels, local +x away from the runway): a rusty
        /// corrugated Quonset hangar open towards the strip with a flying machine inside, a concrete radio hut with a
        /// locker, a windsock and fuel drums.</summary>
        static VoxelGrid Airfield(Site s, List<Extra> extras)
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Scrap);
            var rnd = new System.Random(s.seed);
            const int R = 35, L = 45;
            var tin = Pal.Weathered(Pal.Metal, 0.45f, s.seed, 2, -100);
            var tinDark = Pal.Weathered(Pal.Metal, 0.6f, s.seed + 1, 1, -100);
            var rib = Pal.Ramp(Pal.Black, 1, 2601);
            for (int x = -L; x <= L; x++)
            for (int z = -R - 1; z <= R + 1; z++)
            for (int y = 0; y <= R + 1; y++)
            {
                float d = Mathf.Sqrt(z * z + y * y);
                bool isRib = (x + L) % 15 == 0 || x == -L;
                if (Mathf.Abs(d - R) > (isRib ? 1.1f : 0.6f)) continue;
                if (!isRib && Pal.Hash(x, y, z, s.seed) < 0.035f) continue;                      // rusted through
                int arc = Mathf.FloorToInt(Mathf.Atan2(y, z) * R);
                g.Set(x, y, z, isRib ? rib : (arc & 2) == 0 ? tin : tinDark);
            }
            for (int z = -R; z <= R; z++)                                                         // back wall with a man door
            for (int y = 0; y <= R; y++)
                if (z * z + y * y <= R * R && !(Mathf.Abs(z) < 4 && y < 11)) g.Set(L, y, z, (z & 3) == 0 ? tinDark : tin);
            // radio hut off the hangar's corner
            g.Mat((byte)ResourceType.Concrete);
            var conc = Pal.Weathered(Pal.Cream, 0.3f, s.seed + 2, 0, -100);
            int hx0 = -L + 6, hx1 = -L + 18, hz0 = R + 6, hz1 = R + 18;
            for (int x = hx0; x <= hx1; x++)
            for (int z = hz0; z <= hz1; z++)
            for (int y = 0; y <= 13; y++)
            {
                bool wall = x == hx0 || x == hx1 || z == hz0 || z == hz1;
                bool roof = y == 13;
                if (!wall && !roof) continue;
                if (x == hx0 && z > hz0 + 4 && z < hz0 + 9 && y < 10) continue;                   // door towards the strip
                if (z == hz0 && x > hx0 + 3 && x < hx1 - 3 && y > 5 && y < 10) { g.Set(x, y, z, Pal.Ramp(Pal.Glass, 2, 2602)); continue; }
                g.Set(x, y, z, conc);
            }
            g.Mat((byte)ResourceType.Iron);
            g.Box(hx1 - 2, 14, hz1 - 2, hx1 - 2, 30, hz1 - 2, Pal.Ramp(Pal.Metal, 2, 2603));             // radio mast
            g.Box(hx1 - 4, 28, hz1 - 2, hx1, 28, hz1 - 2, Pal.Ramp(Pal.Metal, 2, 2603));
            // windsock by the strip
            int wx = -L - 12, wz = -R - 10;
            g.Box(wx, 0, wz, wx, 26, wz, Pal.Ramp(Pal.Metal, 2, 2604));
            for (int i = 0; i < 9; i++)
            {
                var band = (i / 2) % 2 == 0 ? Pal.Ramp(Pal.Ochre, 3, 2605) : Pal.Ramp(Pal.Cream, 3, 2606);
                int r = i < 3 ? 1 : 0;
                g.Box(wx - r, 25 - i / 3 - r, wz + 1 + i, wx + r, 25 - i / 3 + r, wz + 1 + i, band);
            }
            // fuel drums by the door
            g.Mat((byte)ResourceType.Scrap);
            for (int i = 0; i < 4; i++)
            {
                float dx = -L - 4 - (i % 2) * 4, dz = -R + 6 + (i / 2) * 4;
                g.CylY(dx, dz, 1.6f, 0, 4, (i + rnd.Next(2)) % 2 == 0 ? Pal.Ramp(Pal.Crimson, 1, 2607) : Pal.Ramp(Pal.RigGreen, 2, 2608));
            }
            float V = BunkerVoxel;
            string plane = rnd.NextDouble() < 0.5 ? "Ultralight" : "Gyrocopter";
            extras.Add(new Extra { kind = "aircraft", visual = plane, local = new Vector3(2f, 0f, 0f) * 1f, yaw = -90f });
            extras.Add(new Extra { kind = "loot", table = "airfield", visual = "locker", local = new Vector3((hx1 - 2) * V, 0f, (hz0 + 3) * V), yaw = 180f });
            extras.Add(new Extra { kind = "loot", table = "garage", visual = "crate", local = new Vector3((L - 4) * V, 0f, -(R - 8) * V), yaw = 0f });
            return g;
        }

        // ------------------------------------------------------------------ bunker (worker thread)

        static readonly Color32[] Conc = { Pal.Hex("4a4846"), Pal.Hex("5c5a56"), Pal.Hex("6e6b66"), Pal.Hex("807c76") };
        static readonly Color32[] Army = { Pal.Hex("2a3228"), Pal.Hex("343e30"), Pal.Hex("3e4a38"), Pal.Hex("4a5842") };
        static readonly Color32[] Sandbag = { Pal.Hex("6a5a3a"), Pal.Hex("7c6a44"), Pal.Hex("8e7a4e"), Pal.Hex("a08a58") };
        static readonly Color32[] SodGreen = { Pal.Hex("303c1e"), Pal.Hex("3c4824"), Pal.Hex("4a5428"), Pal.Hex("5a6030") };
        static readonly Color32[] SodSand = { Pal.Hex("a65c2e"), Pal.Hex("bb6c36"), Pal.Hex("cf8044"), Pal.Hex("e09a58") };
        static readonly Color32[] SodNuke = { Pal.Hex("4c4a2c"), Pal.Hex("5c5830"), Pal.Hex("6c6636"), Pal.Hex("7e763c") };
        static readonly Color32 Hazard = Pal.Hex("d4b020"), PipeRed = Pal.Hex("8a2a1a");

        static VoxelGrid Bunker(WorldGen world, Site s, List<Extra> extras)
        {
            var g = new VoxelGrid();
            const int H = 10;                                  // half a cell in voxels
            const int Wall = 12, Slab0 = 13, Slab1 = 14, Sod = 15;
            var rnd = new System.Random(s.seed);
            byte stone = (byte)ResourceType.Stone, scrap = (byte)ResourceType.Scrap, cloth = (byte)ResourceType.Cloth, glass = (byte)ResourceType.Glass;
            var biome = world.NaturalBiome(s.pos.x, s.pos.y);
            var sod = biome == Biome.Desert ? SodSand : biome == Biome.Nuclear ? SodNuke : SodGreen;
            VoxMat wallPaint = p => p.y <= 5 ? (p.y == 5 ? Pal.Pick(Pal.Metal, p, 3, 1) : Pal.Pick(Army, p, s.seed, 2)) : Pal.Pick(Conc, p, s.seed + 1, 2);
            var frame = Pal.Stripe(Pal.Solid(Hazard), Pal.Ramp(Pal.Black, 1), 1, 4, 2);
            int Cx(int i) => Mathf.RoundToInt((i - (s.gw - 1) * 0.5f) * 20f);
            int Cz(int j) => Mathf.RoundToInt((j - (s.gh - 1) * 0.5f) * 20f);

            for (int j = 0; j < s.gh; j++)
            for (int i = 0; i < s.gw; i++)
            {
                int room = s.Room(i, j);
                if (room < 0) continue;
                int cx = Cx(i), cz = Cz(j);
                bool entryCell = i == s.entrance && j == 0;
                // roof slab and sod over the cell, overhanging where the neighbour is solid ground
                int x0 = cx - H - (s.Room(i - 1, j) < 0 ? 3 : 0), x1 = cx + H + (s.Room(i + 1, j) < 0 ? 3 : 0);
                int z0 = cz - H - (s.Room(i, j - 1) < 0 && !entryCell ? 3 : 0), z1 = cz + H + (s.Room(i, j + 1) < 0 ? 3 : 0);
                g.Mat(stone);
                g.Box(x0, Slab0, z0, x1, Slab1, z1, Pal.Ramp(Conc, 0, s.seed + 5));
                g.Mat((byte)ResourceType.Sand);
                g.Box(x0, Sod, z0, x1, Sod, z1, Pal.Ramp(sod, 1, s.seed + 6));

                // walls on the four edges (edges shared with a neighbour are written by both: the grid dedups)
                for (int e = 0; e < 4; e++)
                {
                    int ni = i + (e == 0 ? 1 : e == 1 ? -1 : 0), nj = j + (e == 2 ? 1 : e == 3 ? -1 : 0);
                    int nroom = s.Room(ni, nj);
                    if (nroom == room) continue;
                    bool door = e == 0 ? s.Door(i, j, true) : e == 1 ? s.Door(i - 1, j, true) : e == 2 ? s.Door(i, j, false) : s.Door(i, j - 1, false);
                    bool entry = e == 3 && entryCell;
                    bool outer = nroom < 0 && !entry;
                    int half = entry ? 7 : 4;
                    var outward = e == 0 ? Vector3Int.right : e == 1 ? Vector3Int.left : e == 2 ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 0, -1);
                    g.Mat(stone);
                    for (int t = -H; t <= H; t++)
                    for (int y = 0; y <= Wall; y++)
                    {
                        if ((door || entry) && Mathf.Abs(t) <= half && y >= 1 && y <= 11) continue;
                        var p = e < 2 ? new Vector3Int(cx + (e == 0 ? H : -H), y, cz + t) : new Vector3Int(cx + t, y, cz + (e == 2 ? H : -H));
                        g.Set(p, (door || entry) && Mathf.Abs(t) == half + 1 ? frame : wallPaint);
                        if (outer) g.Set(p + outward, Pal.Ramp(Conc, 0, s.seed + 7));   // double thickness against the earth
                    }
                    // pipe run along outer walls
                    if (outer && rnd.NextDouble() < 0.6)
                    {
                        g.Mat(scrap);
                        for (int t = -H + 1; t <= H - 1; t++)
                        {
                            var p = e < 2 ? new Vector3Int(cx + (e == 0 ? H : -H), 11, cz + t) : new Vector3Int(cx + t, 11, cz + (e == 2 ? H : -H));
                            g.Set(p - outward, t % 7 == 0 ? Pal.Solid(PipeRed) : Pal.Ramp(Pal.Rust, 2, s.seed + t));
                        }
                    }
                }

                // pillar where four cells of one hall meet
                if (s.Room(i + 1, j) == room && s.Room(i, j + 1) == room && s.Room(i + 1, j + 1) == room)
                {
                    g.Mat(stone);
                    g.Box(cx + H - 1, 0, cz + H - 1, cx + H + 1, Wall, cz + H + 1, Pal.Ramp(Conc, 1, s.seed + 8));
                }

                // lamp fixture + light
                if (rnd.NextDouble() < 0.65)
                {
                    g.Mat(glass); g.Box(cx - 1, Wall, cz, cx + 1, Wall, cz, Pal.Solid(Pal.LightW));
                    g.Mat(scrap); g.Box(cx - 1, Wall, cz - 1, cx + 1, Wall, cz - 1, Pal.Ramp(Pal.Metal, 1)); g.Box(cx - 1, Wall, cz + 1, cx + 1, Wall, cz + 1, Pal.Ramp(Pal.Metal, 1));
                    extras.Add(new Extra { kind = "lamp", local = new Vector3(cx * BunkerVoxel, 2.1f, cz * BunkerVoxel), dying = rnd.NextDouble() < 0.3 });
                }

                // loot against a wall of this cell
                if (!entryCell && rnd.NextDouble() < 0.55)
                {
                    int e = rnd.Next(4);
                    for (int k = 0; k < 4; k++, e = (e + 1) % 4)
                    {
                        int ni = i + (e == 0 ? 1 : e == 1 ? -1 : 0), nj = j + (e == 2 ? 1 : e == 3 ? -1 : 0);
                        if (s.Room(ni, nj) >= 0) continue;
                        var off = new Vector3(e == 0 ? 1.45f : e == 1 ? -1.45f : 0f, 0.1f, e == 2 ? 1.45f : e == 3 ? -1.45f : 0f);
                        float yaw = e == 0 ? -90f : e == 1 ? 90f : e == 2 ? 180f : 0f;
                        double r = rnd.NextDouble();
                        string table = r < 0.45 ? "bunker" : r < 0.65 ? "tools" : r < 0.85 ? "kitchen" : "office";
                        string visual = table == "kitchen" ? "shelf" : rnd.NextDouble() < 0.5 ? "locker" : "crate";
                        extras.Add(new Extra { kind = "loot", table = table, visual = visual, yaw = yaw, local = new Vector3(cx * BunkerVoxel + off.x, off.y, cz * BunkerVoxel + off.z) });
                        break;
                    }
                }

                // rubble in a corner
                if (rnd.NextDouble() < 0.3)
                {
                    g.Mat(stone);
                    int rx = cx + (rnd.Next(2) == 0 ? -H + 2 : H - 4), rz = cz + (rnd.Next(2) == 0 ? -H + 2 : H - 4);
                    for (int k = 0; k < 14; k++) g.Set(rx + rnd.Next(3), 1 + rnd.Next(k < 8 ? 1 : 2), rz + rnd.Next(3), Pal.Ramp(Conc, rnd.Next(4), k));
                }

                // surface: vents and hatches poke out of the sod
                double sr = rnd.NextDouble();
                if (sr < 0.25)
                {
                    g.Mat(scrap);
                    g.CylY(cx + 4, cz - 3, 1.6f, Sod + 1, Sod + 5, Pal.Ramp(Pal.Rust, 2, s.seed + i));
                    g.Box(cx + 2, Sod + 6, cz - 5, cx + 6, Sod + 6, cz - 1, Pal.Ramp(Pal.Metal, 1));
                    g.Box(cx + 3, Sod + 5, cz - 4, cx + 5, Sod + 5, cz - 2, Pal.Ramp(Pal.Metal, 0));
                }
                else if (sr < 0.4)
                {
                    g.Mat(scrap);
                    g.Box(cx - 3, Sod + 1, cz - 3, cx + 3, Sod + 1, cz + 3, Pal.Stripe(Pal.Ramp(Pal.Metal, 2), Pal.Ramp(Pal.Rust, 1), 0, 3));
                    g.Box(cx + 2, Sod + 2, cz - 1, cx + 2, Sod + 2, cz + 1, Pal.Ramp(Pal.Rust, 3));
                }
            }

            // entrance: ramp retaining walls with a coping, sandbags at the top, blast door swung open inside
            int ex = Mathf.RoundToInt(s.EntranceX / BunkerVoxel), zg = Mathf.RoundToInt(s.GridMinZ / BunkerVoxel), zr = Mathf.RoundToInt((s.GridMinZ - Site.RampLen) / BunkerVoxel);
            int hw = Mathf.RoundToInt(Site.RampHalf / BunkerVoxel);
            var ramp = Pal.Weathered(Conc, 0.25f, s.seed + 10, 2, 0);
            g.Mat(stone);
            for (int z = zr; z <= zg; z++)
            {
                float t = Mathf.Clamp01((z - zr) / (float)(zg - zr));
                int yFloor = Mathf.FloorToInt((1f - t) * Site.BunkerDepth / BunkerVoxel);
                foreach (int side in new[] { -1, 1 })
                {
                    int x = ex + side * hw;
                    for (int y = yFloor - 1; y <= Sod + 1; y++) g.Set(x, y, z, y > Sod ? Pal.Ramp(Conc, 3, s.seed + 9) : ramp);
                    g.Set(x + side, Sod + 1, z, Pal.Ramp(Conc, 2, s.seed + 11));
                }
            }
            g.Mat(cloth);
            for (int z = zr; z <= zr + 12; z++)
                foreach (int side in new[] { -1, 1 })
                    for (int k = 0; k < 3; k++) g.Set(ex + side * (hw + 2 + k), Sod + 1 + (k == 1 ? 1 : 0), z, Pal.Ramp(Sandbag, (z + k) % 2 == 0 ? 2 : 1, z));
            g.Mat(scrap);
            int dz0 = zg + 2;
            for (int z = dz0; z <= dz0 + 12; z++)
            for (int y = 1; y <= 11; y++)
                g.Set(ex + 9, y, z, (z - dz0 + y) % 6 < 2 ? Pal.Solid(Hazard) : Pal.Ramp(Pal.Rust, 2, s.seed + 12));
            return g;
        }

        // ------------------------------------------------------------------ rock tunnel (worker thread)

        static readonly Color32[] Sandstone = { Pal.Hex("6a3e24"), Pal.Hex("7e4c2c"), Pal.Hex("925a34"), Pal.Hex("a66a3e"), Pal.Hex("b87c4a") };
        static readonly Color32[] RockGrey = { Pal.Hex("34302c"), Pal.Hex("403b36"), Pal.Hex("4c4640"), Pal.Hex("5a534b"), Pal.Hex("686056") };

        static VoxelGrid Tunnel(WorldGen world, Site s, int index, Vector3 origin, List<Extra> extras)
        {
            var g = new VoxelGrid().Mat((byte)ResourceType.Stone);
            const float v = TunnelVoxel;
            float zc = -s.halfLen - 2f + (index + 0.5f) * SegmentLen;
            // caves (roadmap 17): the mesa's own ore shows in the tunnel walls — veins to pick at
            float oreStrength = world.OreAt(s.pos.x, s.pos.y, out var oreKind);
            if (oreKind == ResourceType.None)
            {
                // no deposit mapped here: most mesas still carry a seam of something
                var r = new System.Random(s.seed * 7919 + 17);
                if (r.NextDouble() < 0.75)
                {
                    oreKind = CaveOres[r.Next(CaveOres.Length)];
                    oreStrength = 0.3f + (float)r.NextDouble() * 0.4f;
                }
            }
            float veinChance = oreKind == ResourceType.None ? 0f : 0.05f + 0.18f * oreStrength;
            var vein = OreColour(oreKind);
            var biome = world.NaturalBiome(s.pos.x, s.pos.y);
            var shade = biome == Biome.Desert ? Sandstone : RockGrey;
            // strata by world height, like the mesa slopes on the terrain
            VoxMat rock = p =>
            {
                int band = Mathf.FloorToInt((origin.y + p.y * v) * 1.6f);
                return shade[Mathf.Clamp(1 + (band % 3 + 3) % 3 + (Pal.Hash(p, s.seed) > 0.85f ? 1 : 0) - (Pal.Hash(p, s.seed + 1) < 0.08f ? 1 : 0), 0, 4)];
            };
            float Floor(float lx, float lz) { var w = s.ToWorld(lx, lz); return world.BaseHeight(w.x, w.y); }
            float Cap(float lx, float lz) { var w = s.ToWorld(lx, lz); return world.BaseHeight(w.x, w.y) + world.OutcropAdd(s, w.x, w.y); }
            float EdgeAdd(float side, float lz) { var w = s.ToWorld(side * Site.TunnelHalf, lz); return world.OutcropAdd(s, w.x, w.y); }
            bool Roofed(float lz) => Mathf.Min(EdgeAdd(-1f, lz), EdgeAdd(1f, lz)) > Site.TunnelRoof;
            int J(float y) => Mathf.RoundToInt((y - origin.y) / v);

            int halfK = Mathf.RoundToInt(SegmentLen / v * 0.5f);
            for (int k = -halfK; k < halfK; k++)
            {
                float lz = zc + k * v;
                if (Mathf.Abs(lz) > s.halfLen + 2f) continue;
                bool roofed = Roofed(lz);
                bool portal = roofed && (!Roofed(lz - v) || !Roofed(lz + v));
                float floorMid = Floor(0f, lz);
                float addL = EdgeAdd(-1f, lz), addR = EdgeAdd(1f, lz);
                for (int i = -15; i <= 15; i++)
                {
                    float lx = i * v, ax = Mathf.Abs(lx);
                    if ((lx < 0f ? addL : addR) < 0.3f) continue;               // outside the mesa: open ground
                    int j0 = J(Floor(lx, lz)) - 1, jc = J(Cap(lx, lz));
                    if (ax >= 2.7f)
                    {
                        // rock walls: two voxels at the inner face, further out only a lip over the terrain seam
                        int from = ax < 3.2f ? j0 : Mathf.Max(j0, jc - 1);
                        for (int j = from; j <= jc; j++)
                        {
                            bool ore = ax < 3.2f && veinChance > 0f && Pal.Noise(new Vector3(i * 0.35f, j * 0.35f, k * 0.35f), s.seed + 12) < veinChance;
                            if (ore) { g.Mat((byte)oreKind); g.Set(i, j, k, vein); g.Mat((byte)ResourceType.Stone); }
                            else g.Set(i, j, k, rock);
                        }
                        continue;
                    }
                    if (!roofed) continue;
                    // arched roof, cap skin at the mesa surface, solid portal faces
                    int ja = J(floorMid + 4.6f - lx * lx * 0.1f);
                    if (portal) { for (int j = ja; j <= jc; j++) g.Set(i, j, k, rock); continue; }
                    g.Set(i, ja, k, rock); g.Set(i, ja + 1, k, rock);
                    if (jc > ja + 2) { g.Set(i, jc, k, rock); g.Set(i, jc - 1, k, rock); }
                    if (Pal.Hash(i, k, s.seed + 3) < 0.04f)                    // drips
                        for (int d = 1; d <= 1 + (int)(Pal.Hash(i, k, s.seed + 4) * 3f); d++) g.Set(i, ja - d, k, rock);
                }
                // boulders along the wall feet
                if (Pal.Hash(0, k, s.seed + 5) < 0.06f && Mathf.Min(addL, addR) > 1f)
                {
                    int side = Pal.Hash(1, k, s.seed + 6) < 0.5f ? -1 : 1;
                    int j = J(Floor(side * 2.4f, lz));
                    for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++) for (int dz = 0; dz < 2; dz++)
                        if (Pal.Hash(dx, dy + k, dz, s.seed + 7) < 0.8f) g.Set(side * (10 - dx), j + dy, k + dz, rock);
                }
            }
            // the roost and an old camp in the deepest roofed stretch
            if (index == PieceCount(s) / 2 && Roofed(zc))
            {
                float floor = Floor(0f, zc) - origin.y;
                extras.Add(new Extra { kind = "bats", local = new Vector3(0f, floor + 4.3f, 0f) });                  // BatColony hangs them on the arch
                extras.Add(new Extra { kind = "loot", table = "cave", visual = "crate", yaw = 90f, local = new Vector3(1.9f, Floor(1.9f, zc + 1.5f) - origin.y, 1.5f) });
                extras.Add(new Extra { kind = "oil_lamp", dying = true, local = new Vector3(1.5f, Floor(1.5f, zc + 0.4f) - origin.y, 0.4f) });   // guttering, low on oil
            }
            return g;
        }

        static readonly ResourceType[] CaveOres = { ResourceType.IronOre, ResourceType.IronOre, ResourceType.IronOre, ResourceType.CopperOre, ResourceType.CopperOre, ResourceType.Coal, ResourceType.Coal, ResourceType.TinOre, ResourceType.Sulfur, ResourceType.LeadOre };

        /// <summary>Ore vein paint: dark haematite for iron, verdigris for copper, soot for coal, pale for tin and bauxite.</summary>
        static VoxMat OreColour(ResourceType ore) => ore switch
        {
            ResourceType.CopperOre => Pal.Ramp(new[] { Pal.Hex("1e5a4a"), Pal.Hex("2a7a5e"), Pal.Hex("3e9a72") }, 1, 1801),
            ResourceType.Coal => Pal.Ramp(Pal.Black, 1, 1802),
            ResourceType.TinOre or ResourceType.Bauxite => Pal.Ramp(new[] { Pal.Hex("8a8278"), Pal.Hex("a39888"), Pal.Hex("c0b29c") }, 1, 1803),
            ResourceType.Sulfur => Pal.Ramp(new[] { Pal.Hex("a08a1e"), Pal.Hex("c4aa2a"), Pal.Hex("e0c840") }, 1, 1804),
            ResourceType.UraniumOre => Pal.Ramp(new[] { Pal.Hex("3a5a1e"), Pal.Hex("5a8a2a"), Pal.Hex("8ac03e") }, 1, 1805),
            ResourceType.LeadOre => Pal.Ramp(Pal.Metal, 2, 1806),
            _ => Pal.Ramp(new[] { Pal.Hex("2e1c1e"), Pal.Hex("48282a"), Pal.Hex("6a3430"), Pal.Hex("a4582a") }, 1, 1807),   // haematite, rust glints
        };
    }
}
