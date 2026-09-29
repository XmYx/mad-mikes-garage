using System.Collections.Generic;
using MadMax.Game;
using MadMax.Items;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Populates the wasteland around the player: shopkeepers behind the counters of town and city shops,
    /// residents strolling in settlements, a market stall in villages and towns, roadside stalls every few hundred
    /// metres of road, wanderers roaming the wilds (a few per 300 m cell), travelling traders driving between towns
    /// and raider hordes patrolling roads (<see cref="Convoy"/>). Everyone is deterministic from the world seed and a
    /// spawn key, so the same people are met again; spawned within ~110 m, folded away beyond ~170 m.
    /// Authority only (single player / host).</summary>
    public class NpcDirector : MonoBehaviour
    {
        public static NpcDirector Instance { get; private set; }

        struct Spot { public string id; public NpcRole role; public string kind; public Vector3 pos; public float yaw, radius; public int stall, town; }

        WastelandGame game;
        readonly List<Convoy> convoys = new List<Convoy>();
        readonly List<(Vector3 pos, float yaw, string kind)> roadStalls = new List<(Vector3, float, string)>();
        readonly List<Vector3> packers = new List<Vector3>();                                  // pack traders' beats along the roads (roadmap 20)
        readonly Dictionary<int, Campfire> townFires = new Dictionary<int, Campfire>();
        readonly Dictionary<string, Campfire> campFires = new Dictionary<string, Campfire>();
        readonly Dictionary<int, (Vector3 pos, float yaw)> bossSpots = new Dictionary<int, (Vector3, float)>();
        readonly HashSet<string> retired = new HashSet<string>();                             // left to live their own lives this session
        readonly Dictionary<string, Npc> live = new Dictionary<string, Npc>();
        readonly Dictionary<string, GameObject> stalls = new Dictionary<string, GameObject>();
        readonly List<Spot> wanted = new List<Spot>();
        readonly HashSet<string> wantedIds = new HashSet<string>();
        readonly List<string> drop = new List<string>();
        float scanAt;
        List<ConvoySave> pendingConvoys;

        const float SpawnRange = 110f, KeepRange = 170f, WanderCell = 300f;

        public void Init(WastelandGame g)
        {
            game = g; Instance = this;
            if (!GetComponent<BaseRaid>()) gameObject.AddComponent<BaseRaid>();
            NpcRegistry.Load(null, 0);                                       // a fresh world; a loaded game restores after this
            var world = g.World;
            var r = new System.Random(world.seed * 101 + 5);
            // travelling traders and raider hordes on the roads
            var roads = world.roads.roads;
            string[] kinds = { "fuel", "parts", "food", "scrap" };
            int traders = Mathf.Min(4, roads.Count), raiders = Mathf.Min(3, roads.Count);
            for (int i = 0; i < traders; i++)
                AddConvoy("trader" + i, false, kinds[i % kinds.Length], roads[(i * 7 + 3) % roads.Count].points, r.Next());
            for (int i = 0; i < raiders; i++)
            {
                // raiders prefer roads away from the start town
                int best = 0; float far = -1f;
                for (int k = 0; k < 6; k++)
                {
                    int ri = r.Next(roads.Count);
                    var mid = roads[ri].points[roads[ri].points.Count / 2];
                    if (mid.magnitude > far) { far = mid.magnitude; best = ri; }
                }
                AddConvoy("raiders" + i, true, null, roads[best].points, r.Next());
            }
            // pack traders walking a beat every ~900 m on about a third of the stretches
            foreach (var road in roads)
            {
                float acc = 300f;
                for (int i = 1; i < road.points.Count; i++)
                {
                    acc += Vector3.Distance(road.points[i - 1], road.points[i]);
                    if (acc < 900f) continue;
                    acc = 0f;
                    if (r.NextDouble() > 0.35) continue;
                    var p = road.points[i];
                    if (world.SettlementAt(p.x, p.z) != null) continue;
                    packers.Add(p);
                }
            }
            // roadside stalls every ~400 m on about half the stretches
            foreach (var road in roads)
            {
                float acc = 150f;
                for (int i = 1; i < road.points.Count; i++)
                {
                    var a = road.points[i - 1]; var b = road.points[i];
                    acc += Vector3.Distance(a, b);
                    if (acc < 400f) continue;
                    acc = 0f;
                    if (r.NextDouble() > 0.5) continue;
                    var dir = (b - a); dir.y = 0f; dir.Normalize();
                    var side = Vector3.Cross(Vector3.up, dir) * (r.NextDouble() < 0.5 ? 1f : -1f);
                    var p = b + side * (road.width * 0.5f + 5.5f);
                    var s = world.Sample(p.x, p.z);
                    if (world.SettlementAt(p.x, p.z) != null || !float.IsNaN(s.water) || s.feature != 0) continue;
                    roadStalls.Add((p, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg, kinds[r.Next(kinds.Length)]));
                }
            }
        }

        void AddConvoy(string id, bool raider, string kind, List<Vector3> road, int seed)
        {
            var save = new ConvoySave { id = id };
            if (pendingConvoys != null) foreach (var s in pendingConvoys) if (s.id == id) save = s;
            convoys.Add(new Convoy(id, raider, kind, road, seed, save));
        }

        public List<ConvoySave> SaveConvoys() => convoys.ConvertAll(c => c.save);

        /// <summary>Raider gangs on the roads right now (convoy id, gang name): bounty targets.</summary>
        public List<(string id, string gang)> RaiderGangs()
        {
            var l = new List<(string, string)>();
            foreach (var c in convoys) if (c.raiders && c.save.deadDay < 0) l.Add((c.id, c.Gang));
            return l;
        }

        // ------------------------------------------------------------------ bounty boards
        readonly Dictionary<int, GameObject> boards = new Dictionary<int, GameObject>();
        static Mesh boardMesh;

        void UpdateBoards(Vector3 focus)
        {
            var world = game.World;
            foreach (var st in world.settlements)
            {
                float d = Vector2.Distance(st.pos, new Vector2(focus.x, focus.z)) - st.radius;
                bool near = d < SpawnRange;
                boards.TryGetValue(st.index, out var go);
                if (near && !go)
                {
                    // near the middle, off the road and clear of buildings (the last try stands anyway)
                    var rnd = new System.Random(world.seed ^ st.index * 911);
                    for (int k = 0; k < 16; k++)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (6f + (float)rnd.NextDouble() * 10f);
                        var s = world.Sample(p.x, p.y);
                        bool last = k == 15;
                        if (!last && (s.roadDist < 3f || Physics.CheckSphere(Ground(p) + Vector3.up * 1.2f, 1.1f, ~0, QueryTriggerInteraction.Ignore))) continue;
                        boards[st.index] = SpawnBoard(st.index, Ground(p), Mathf.Atan2(st.pos.x - p.x, st.pos.y - p.y) * Mathf.Rad2Deg);
                        break;
                    }
                }
                else if (!near && go && d > KeepRange) { Destroy(go); boards.Remove(st.index); }
            }
        }

        GameObject SpawnBoard(int town, Vector3 pos, float yaw)
        {
            if (!boardMesh)
            {
                var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
                foreach (int x in new[] { -9, 9 }) g.Box(x, 0, 0, x, 26, 0, Pal.Ramp(Pal.Wood, 1, 1501));
                g.Box(-10, 12, 0, 10, 25, 0, Pal.Ramp(Pal.Wood, 2, 1502));                                         // board
                g.Box(-11, 26, -1, 11, 27, 1, Pal.Ramp(Pal.Rust, 2, 1503));                                        // tin roof
                var rr = new System.Random(1504);
                for (int i = 0; i < 7; i++)
                {
                    int x0 = rr.Next(-8, 5), y0 = rr.Next(13, 21);
                    var paper = i % 3 == 0 ? Pal.Ochre[4] : Pal.Cream[3];
                    g.Box(x0, y0, 1, x0 + 3, y0 + 3, 1, p => (p.y - y0) % 2 == 1 && p.x > x0 && p.x < x0 + 3 ? Pal.Black[2] : paper);   // notices
                }
                g.Set(-2, 24, 1, Pal.Solid(Pal.Crimson[3])); g.Set(2, 24, 1, Pal.Solid(Pal.Crimson[3]));        // tacks
                g.Bevel();
                boardMesh = VoxelMesher.Build(g, "BountyBoard");
            }
            var go = new GameObject("BountyBoard", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.GetComponent<MeshFilter>().sharedMesh = boardMesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = game.propMaterial;
            var box = go.GetComponent<BoxCollider>(); box.center = boardMesh.bounds.center; box.size = boardMesh.bounds.size;
            go.AddComponent<MadMax.Building.BountyBoard>().town = town;
            FloraBlocker.Add(go);
            return go;
        }

        public void LoadConvoys(List<ConvoySave> list)
        {
            pendingConvoys = list;
            if (list == null) return;
            foreach (var c in convoys) foreach (var s in list) if (s.id == c.id) { c.save.generation = s.generation; c.save.deadDay = s.deadDay; }
        }

        /// <summary>The convoy a vehicle drives with, or null.</summary>
        public Convoy ConvoyOf(MadMax.Vehicles.VehicleDriver v)
        {
            if (!v) return null;
            foreach (var c in convoys) foreach (var car in c.cars) if (car && car.gameObject == v.gameObject) return c;
            return null;
        }

        /// <summary>A horn blast from the player's vehicle (roadmap 19): people nearby look round and the nervous jump
        /// clear; a trader convoy pulls over, a raider gang blocking the road takes it as the call to parley, a friendly
        /// gang honks back.</summary>
        public void Horn(Vector3 at, MadMax.Vehicles.VehicleDriver car)
        {
            MadMax.Animals.AnimalDirector.Instance?.Horn(at);
            foreach (var n in Npc.All)
            {
                if (!n || !n.Alive) continue;
                float d = (n.transform.position - at).sqrMagnitude;
                if (d > 40f * 40f) continue;
                n.Attend(at);
                if (d < 14f * 14f && !n.Hostile && n.Profile.temper == Temper.Nervous) n.Scare(3f);
            }
            foreach (var c in convoys) c.Horn(game, at);
        }

        public Convoy NearestRaiders(Vector3 at, out float dist)
        {
            dist = float.MaxValue; Convoy best = null;
            foreach (var c in convoys)
            {
                if (!c.raiders || c.phase == Convoy.Phase.Gone) continue;
                var p = c.cars.Count > 0 && c.cars[0] ? c.cars[0].transform.position : c.PointAt(c.travel, out _);
                float d = Vector3.Distance(p, at);
                if (d < dist) { dist = d; best = c; }
            }
            return best;
        }

        /// <summary>A loud noise (lock forced, gunshot, explosion): people within earshot look; locals dislike
        /// break-ins in their town, the nervous run. <paramref name="suspicious"/> = the player did something shady.</summary>
        public void Noise(Vector3 at, float radius, bool suspicious = true)
        {
            MadMax.Animals.AnimalDirector.Instance?.Noise(at, radius);                         // animals bolt from bangs
            var town = game ? game.World.SettlementAt(at.x, at.z) : null;
            foreach (var n in Npc.All)
            {
                if (!n || !n.Alive || (n.transform.position - at).sqrMagnitude > radius * radius) continue;
                n.Attend(at);
                if (n.Profile.Raider) { n.convoy?.Provoked(); continue; }
                if (suspicious && town != null && game.World.SettlementAt(n.transform.position.x, n.transform.position.z) == town)
                {
                    n.State.disposition = Mathf.Max(-100, n.State.disposition - 8);
                    if (n.Profile.temper == Temper.Nervous) n.Scare(6f);
                }
            }
        }

        /// <summary>T while driving near a raider boss who stepped out to demand a toll: open the parley.</summary>
        public static bool TryParley()
        {
            var d = Instance;
            if (!d || !d.game || d.game.Menus.IsOpen) return false;
            var at = d.game.Current ? d.game.Current.transform.position : d.game.Player.transform.position;
            foreach (var c in d.convoys)
                if (c.raiders && c.phase == Convoy.Phase.Confront && c.Boss && c.Boss.Alive && Vector3.Distance(c.Boss.transform.position, at) < 45f)
                {
                    d.game.Menus.OpenTalk(c.Boss, false);
                    return true;
                }
            return false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var c in convoys) c.Destroy();
        }

        void Update()
        {
            if (!game || game.Player == null) return;
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;
            var focus = game.Current ? game.Current.transform.position : game.Player.transform.position;
            float dt = Time.deltaTime;
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Convoys");
            foreach (var c in convoys) c.Tick(game, focus, dt);
            UnityEngine.Profiling.Profiler.EndSample();
            if (game.Menus.IsOpen && game.Menus.TalkingTo) game.Menus.TalkingTo.Attend(game.Player.transform.position);

            if (Time.time >= scanAt)
            {
                scanAt = Time.time + 0.5f;
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Scan"); Scan(focus); UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Boards"); UpdateBoards(focus); UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Fires"); UpdateFires(focus); UnityEngine.Profiling.Profiler.EndSample();
                Contracts.Tick();
            }
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Ticks");
            Companions.Tick(game);
            TownQuests.Tick(game);
            GuildEscort.Tick(game);
            Factions.Tick(game);
            UnityEngine.Profiling.Profiler.EndSample();
            // spawn at most one person per frame; their body meshes are built on worker threads first (a few people ahead)
            int warming = 0;
            for (int i = 0; i < wanted.Count && warming < 6; i++)
            {
                var s = wanted[i];
                if (live.ContainsKey(s.id)) continue;
                if (NpcRegistry.IsDead(s.id) || retired.Contains(s.id) || Companions.Has(s.id)) { live[s.id] = null; continue; }
                var p = ProfileOf(s);
                if (!MadMax.Game.HumanRig.Prewarm(p.look, p.outfit)) { warming++; continue; }
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Spawn");
                SpawnSpot(s, p);
                UnityEngine.Profiling.Profiler.EndSample();
                break;
            }
        }

        readonly Dictionary<string, NpcProfile> profiles = new Dictionary<string, NpcProfile>();
        readonly Dictionary<int, List<(string id, Vector2 pos, float yaw)>> townBuildings = new Dictionary<int, List<(string id, Vector2 pos, float yaw)>>();

        NpcProfile ProfileOf(Spot s)
        {
            if (profiles.TryGetValue(s.id, out var p)) return p;
            if (profiles.Count > 400) profiles.Clear();
            return profiles[s.id] = NpcProfile.Make(s.id, s.role, Stable(s.id) ^ game.World.seed, s.kind);
        }

        void Scan(Vector3 focus)
        {
            wanted.Clear(); wantedIds.Clear();
            var world = game.World;
            // settlements
            foreach (var st in world.settlements)
            {
                float d = Vector2.Distance(st.pos, new Vector2(focus.x, focus.z)) - st.radius;
                if (d > SpawnRange) continue;
                var rnd = new System.Random(world.seed ^ st.index * 7727);
                if (!townBuildings.TryGetValue(st.index, out var buildings))
                {
                    townBuildings[st.index] = buildings = new List<(string id, Vector2 pos, float yaw)>();   // deterministic: laid out once
                    BiomeProps.Buildings(world, st, buildings);
                }
                int shop = 0;
                foreach (var b in buildings)
                {
                    if (!b.id.StartsWith("Shop")) continue;
                    // behind the counter at the back of the shop, facing the door
                    var q = Quaternion.Euler(0f, b.yaw, 0f);
                    var o = q * new Vector3(4.4f, 0f, 4.2f);
                    string kind = st.kind == Biome.City ? new[] { "salvage", "parts", "fuel" }[(st.index + shop) % 3] : new[] { "food", "salvage", "scrap", "build" }[(st.index + shop) % 4];
                    Want(new Spot { id = "k" + st.index + "," + shop, role = NpcRole.Shopkeeper, kind = kind, pos = Ground(b.pos + new Vector2(o.x, o.z)) + Vector3.up * 0.2f, yaw = b.yaw + 180f }, focus);
                    shop++;
                }
                int residents = st.kind == Biome.Village ? 4 : 5;
                for (int i = 0; i < residents; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = st.radius * (0.2f + 0.45f * (float)rnd.NextDouble());
                    var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    Want(new Spot { id = "r" + st.index + "," + i, role = NpcRole.Resident, pos = Ground(p), radius = 12f }, focus);
                }
                // the town boss at their post (roadmap 20 quest chains)
                if (bossSpots.TryGetValue(st.index, out var boss))
                    Want(new Spot { id = "L" + st.index, role = NpcRole.Leader, pos = boss.pos, yaw = boss.yaw, town = st.index }, focus);
                if (st.kind != Biome.City)
                {
                    // market stall near the middle, off the road
                    for (int k = 0; k < 8; k++)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (9f + (float)rnd.NextDouble() * 5f);
                        var s = world.Sample(p.x, p.y);
                        if (s.roadDist < 4.5f || s.roadDist > 12f) continue;
                        string kind = st.kind == Biome.Village ? "food" : new[] { "fuel", "parts", "scrap" }[st.index % 3];
                        Want(new Spot { id = "m" + st.index, role = NpcRole.Stallkeeper, kind = kind, pos = Ground(p), yaw = Mathf.Atan2(st.pos.x - p.x, st.pos.y - p.y) * Mathf.Rad2Deg, stall = 1 }, focus);
                        break;
                    }
                }
            }
            // roadside stalls
            for (int i = 0; i < roadStalls.Count; i++)
            {
                var (p, yaw, kind) = roadStalls[i];
                if ((p - focus).sqrMagnitude > SpawnRange * SpawnRange * 1.5f) continue;
                Want(new Spot { id = "s" + i, role = NpcRole.Stallkeeper, kind = kind, pos = Ground(new Vector2(p.x, p.z)), yaw = yaw, stall = 1 }, focus);
            }
            // pack traders on their beats
            for (int i = 0; i < packers.Count; i++)
            {
                var p = packers[i];
                if ((p - focus).sqrMagnitude > SpawnRange * SpawnRange * 1.5f) continue;
                Want(new Spot { id = "p" + i, role = NpcRole.Packer, kind = "pack", pos = Ground(new Vector2(p.x, p.z)), radius = 45f }, focus);
            }
            // wanderers of the nearby cells
            var fc = new Vector2Int(Mathf.FloorToInt(focus.x / WanderCell), Mathf.FloorToInt(focus.z / WanderCell));
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var c = new Vector2Int(fc.x + dx, fc.y + dz);
                var rnd = new System.Random(c.x * 73856093 ^ c.y * 19349663 ^ world.seed * 3);
                int n = rnd.Next(0, 3);
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2((c.x + (float)rnd.NextDouble()) * WanderCell, (c.y + (float)rnd.NextDouble()) * WanderCell);
                    var s = world.Sample(p.x, p.y);
                    if (world.SettlementAt(p.x, p.y) != null || !float.IsNaN(s.water) || s.feature >= 2) continue;
                    Want(new Spot { id = "w" + c.x + "," + c.y + "," + i, role = NpcRole.Wanderer, pos = Ground(p), radius = 25f }, focus);
                }
            }
            // fold away the far ones
            drop.Clear();
            foreach (var kv in live)
            {
                if (wantedIds.Contains(kv.Key)) continue;
                if (kv.Value && (kv.Value.transform.position - focus).sqrMagnitude < KeepRange * KeepRange) continue;
                drop.Add(kv.Key);
            }
            foreach (var id in drop)
            {
                if (live[id]) Destroy(live[id].gameObject);
                live.Remove(id);
                if (stalls.TryGetValue(id, out var sg)) { if (sg) Destroy(sg); stalls.Remove(id); }
            }
        }

        Vector3 Ground(Vector2 p) => new Vector3(p.x, DeformableTerrain.Instance.HeightNoLoad(p.x, p.y) + 0.05f, p.y);   // scans reach past the loaded terrain

        void Want(Spot s, Vector3 focus)
        {
            if ((s.pos - focus).sqrMagnitude > SpawnRange * SpawnRange) { if (live.ContainsKey(s.id)) wantedIds.Add(s.id); return; }
            wanted.Add(s); wantedIds.Add(s.id);
        }

        void SpawnSpot(Spot s, NpcProfile p)
        {
            var pos = s.pos;
            if (s.stall > 0)
            {
                var q = Quaternion.Euler(0f, s.yaw, 0f);
                if (!stalls.ContainsKey(s.id)) stalls[s.id] = SpawnStall(s.kind, pos, s.yaw, s.id);
                pos = pos + q * new Vector3(0f, 0f, -0.9f);                       // behind the table, facing the customer
            }
            p.town = s.role == NpcRole.Leader ? s.town : -1;
            var n = Npc.Spawn(p, pos, s.yaw, null, game.propMaterial);
            n.mode = s.role == NpcRole.Shopkeeper || s.role == NpcRole.Stallkeeper || s.role == NpcRole.Leader ? Npc.Mode.Stand : Npc.Mode.Wander;
            if (s.role == NpcRole.Packer) PackAnimal.Attach(n, game.propMaterial);
            n.homeRadius = s.radius > 0f ? s.radius : 8f;
            live[s.id] = n;
        }

        static int Stable(string s) { unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h; } }

        /// <summary>A recruited person: the director lets go of their spot (and never spawns a copy).</summary>
        public void Release(Npc n)
        {
            string key = null;
            foreach (var kv in live) if (kv.Value == n) { key = kv.Key; break; }
            if (key != null) live[key] = null;
            retired.Add(n.Profile.id);
        }

        /// <summary>Somewhere near a settlement's middle that is off the road and clear of buildings.</summary>
        Vector3? ClearSpot(Settlement st, int salt, float min, float max, float room)
        {
            var world = game.World;
            var rnd = new System.Random(world.seed ^ st.index * 4099 + salt);
            for (int k = 0; k < 14; k++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(min, max, (float)rnd.NextDouble());
                var s = world.Sample(p.x, p.y);
                if (s.roadDist < 2.5f || !float.IsNaN(s.water)) continue;
                var g = Ground(p);
                if (Physics.CheckSphere(g + Vector3.up * (room + 0.2f), room, ~0, QueryTriggerInteraction.Ignore)) continue;
                return g;
            }
            return null;
        }

        /// <summary>Each settlement near the player gets its boss post and an evening campfire; wanderers light their own
        /// at dusk (roadmap 20).</summary>
        void UpdateFires(Vector3 focus)
        {
            var world = game.World;
            foreach (var st in world.settlements)
            {
                float d = Vector2.Distance(st.pos, new Vector2(focus.x, focus.z)) - st.radius;
                townFires.TryGetValue(st.index, out var fire);
                if (d < 60f)
                {
                    if (!fire) { var at = ClearSpot(st, 11, 7f, 16f, 2.2f); if (at.HasValue) townFires[st.index] = Campfire.Spawn(at.Value, 7, game.propMaterial); }
                    if (!bossSpots.ContainsKey(st.index)) { var at = ClearSpot(st, 23, 3f, 9f, 0.8f); if (at.HasValue) bossSpots[st.index] = (at.Value, Mathf.Atan2(st.pos.x - at.Value.x, st.pos.y - at.Value.z) * Mathf.Rad2Deg); }
                }
                else if (fire && d > KeepRange) { Destroy(fire.gameObject); townFires.Remove(st.index); }
            }
            bool dusk = DayNight.Hours >= 18f || DayNight.Hours < 6.5f;
            foreach (var kv in live)
            {
                var n = kv.Value;
                if (!n || n.Profile.role != NpcRole.Wanderer || campFires.ContainsKey(kv.Key) || !dusk) continue;
                var r = new System.Random(Stable(kv.Key));
                var at = n.home + new Vector3((float)r.NextDouble() * 4f - 2f, 0f, (float)r.NextDouble() * 4f - 2f);
                campFires[kv.Key] = Campfire.Spawn(Ground(new Vector2(at.x, at.z)), 3, game.propMaterial);
            }
            drop.Clear();
            foreach (var kv in campFires) if (!kv.Value || !dusk || !live.TryGetValue(kv.Key, out var owner) || !owner) drop.Add(kv.Key);
            foreach (var id in drop) { if (campFires[id]) Destroy(campFires[id].gameObject); campFires.Remove(id); }
        }

        // ------------------------------------------------------------------ stalls

        static readonly Dictionary<string, (VoxelGrid grid, Mesh mesh)> stallTemplates = new Dictionary<string, (VoxelGrid, Mesh)>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => stallTemplates.Clear();

        /// <summary>Market stall voxels for a trade kind (template id "Stall_kind", for saved destruction).</summary>
        public static VoxelGrid StallTemplate(string id) => id != null && id.StartsWith("Stall_") ? Stall(id.Substring(6)).grid : null;

        static (VoxelGrid grid, Mesh mesh) Stall(string kind)
        {
            if (stallTemplates.TryGetValue(kind, out var t) && t.mesh) return t;
            var g = new VoxelGrid();
            byte wood = (byte)ResourceType.Wood, cloth = (byte)ResourceType.Cloth, scrap = (byte)ResourceType.Scrap, glass = (byte)ResourceType.Glass;
            var canvas = kind switch { "fuel" => Pal.Hex("a02818"), "parts" => Pal.Hex("2a4a6a"), "food" => Pal.Hex("d4b020"), _ => Pal.Hex("5a6a2a") };
            // table, posts, slanted awning with stripes
            g.Mat(wood);
            g.Box(-14, 10, -4, 14, 10, 4, Pal.Ramp(Pal.Wood, 3, 61));
            foreach (int x in new[] { -13, 13 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Wood, 1, 62));
            foreach (int x in new[] { -15, 15 }) { g.Box(x, 0, -14, x, 30, -14, Pal.Ramp(Pal.Wood, 1, 63)); g.Box(x, 0, 6, x, 26, 6, Pal.Ramp(Pal.Wood, 1, 64)); }
            g.Mat(cloth);
            for (int z = -16; z <= 8; z++)
            {
                int y = 30 - (z + 16) / 6;
                g.Box(-17, y, z, 17, y, z, p => (p.x + 34) / 4 % 2 == 0 ? canvas : Pal.Cream[2]);
            }
            // wares by trade
            switch (kind)
            {
                case "fuel":
                    g.Mat(scrap);
                    for (int i = 0; i < 4; i++) g.Box(-11 + i * 6, 11, -2, -8 + i * 6, 16, 1, p => p.y == 15 ? Pal.Ramp(Pal.Metal, 0)(p) : Pal.Solid(Pal.Hex("8a2a1a"))(p));
                    g.CylY(-18, 8, 3f, 0, 9, Pal.Ramp(Pal.Rust, 2, 65));
                    break;
                case "parts":
                    g.Mat(scrap);
                    g.CylX(14f, 0, 4f, -11, -8, Pal.Ramp(Pal.Tire, 1, 66));
                    g.Box(-2, 11, -2, 3, 14, 2, Pal.Ramp(Pal.Metal, 2, 67));
                    g.Box(6, 11, -1, 11, 12, 1, Pal.Ramp(Pal.Chrome, 1, 68));
                    g.CylZ(-18, 3.5f, 3.2f, -3, 3, Pal.Ramp(Pal.Tire, 1, 69), 1.5f);
                    break;
                case "food":
                    g.Mat(glass);
                    for (int i = 0; i < 6; i++) g.Box(-12 + i * 4, 11, -1, -11 + i * 4, 13, 0, Pal.Solid(i % 2 == 0 ? Pal.Hex("c83020") : Pal.Hex("d4b020")));
                    g.Mat(wood);
                    g.Box(-18, 0, 0, -16, 6, 4, Pal.Ramp(Pal.Wood, 2, 70));
                    break;
                default:
                    g.Mat(scrap);
                    for (int i = 0; i < 12; i++) g.Set(-12 + i * 2, 11 + (i % 3 == 0 ? 1 : 0), (i * 5) % 5 - 2, Pal.Ramp(Pal.Rust, 1 + i % 3, 71 + i));
                    break;
            }
            g.PruneUnsupported();
            g.Bevel();
            t = (g, VoxelMesher.Build(g, "Stall_" + kind));
            stallTemplates[kind] = t;
            return t;
        }

        GameObject SpawnStall(string kind, Vector3 pos, float yaw, string key)
        {
            var t = Stall(kind);
            var terrain = DeformableTerrain.Instance;
            var mat = terrain && terrain.worldPropMaterial ? terrain.worldPropMaterial : game.propMaterial;
            var d = DestructibleVoxels.Spawn("Stall", t.grid, t.mesh, mat, null, pos - Vector3.up * 0.05f, yaw, false, terrain ? terrain.DestructionState : null, "stall:" + key, VoxelMesher.DefaultSize, "Stall_" + kind);
            if (!d) return null;
            FloraBlocker.Add(d.gameObject);
            return d.gameObject;
        }
    }
}
