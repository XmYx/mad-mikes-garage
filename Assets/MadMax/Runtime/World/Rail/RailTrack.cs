using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Building;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Streams the railway's look around the player: 64 m track tiles (<see cref="RailTrackBuilder.Track"/>,
    /// built on workers, colliders cooked on workers, one tile turned into objects per frame), stations (platforms and
    /// buildings, search spots, the terminal's loading ramp as a drivable deck), and the working props of each tile —
    /// lever stands at the points, signals at station throats, barriers and lamps at level crossings. Tiles touched by
    /// new track rebuild.</summary>
    public class RailTrack : MonoBehaviour
    {
        public static RailTrack Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Instance = null;

        public float reach = 300f, keep = 380f;
        RailNetwork net;
        WorldGen world;
        Material mat, glow;
        Transform focus;
        int seed;

        class TileObj { public GameObject go; public Task<RailKit> job; public Task bake; public Mesh mesh, glowMesh, solid; public MeshCollider col; public bool dirty; public readonly List<GameObject> props = new List<GameObject>(); public readonly List<Mesh> poleMeshes = new List<Mesh>(); }
        class StationObj { public GameObject go; public Task<RailKit> job; public List<RailTrackBuilder.Spot> spots; public Task bake; public Mesh mesh, glowMesh, solid; public MeshCollider col; }
        readonly Dictionary<Vector2Int, TileObj> tiles = new Dictionary<Vector2Int, TileObj>();
        readonly Dictionary<int, StationObj> stations = new Dictionary<int, StationObj>();
        readonly List<RailLine> lineBuf = new List<RailLine>();
        readonly Dictionary<string, BreakablePole> telegraph = new Dictionary<string, BreakablePole>();
        float scanAt;

        public static RailTrack Create(RailNetwork net, WorldGen world, Material mat, Material glow, int seed)
        {
            var t = new GameObject("RailTrack").AddComponent<RailTrack>();
            t.net = net; t.world = world; t.mat = mat; t.glow = glow; t.seed = seed;
            net.Changed += t.OnChanged;
            Instance = t;
            return t;
        }

        void OnChanged(Bounds b)
        {
            for (int x = Mathf.FloorToInt(b.min.x / RailTrackBuilder.Tile); x <= Mathf.FloorToInt(b.max.x / RailTrackBuilder.Tile); x++)
            for (int z = Mathf.FloorToInt(b.min.z / RailTrackBuilder.Tile); z <= Mathf.FloorToInt(b.max.z / RailTrackBuilder.Tile); z++)
                if (tiles.TryGetValue(new Vector2Int(x, z), out var t)) t.dirty = true;
        }

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || net == null) return;
            var p = g.FocusPos;
            if (Time.time >= scanAt)
            {
                scanAt = Time.time + 0.4f;
                Want(p);
            }
            Finish();
        }

        void Want(Vector3 p)
        {
            float T = RailTrackBuilder.Tile;
            int r = Mathf.CeilToInt(reach / T);
            var c = new Vector2Int(Mathf.FloorToInt(p.x / T), Mathf.FloorToInt(p.z / T));
            int running = 0;
            foreach (var kv in tiles) if (kv.Value.job != null) running++;
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                var k = new Vector2Int(c.x + dx, c.y + dz);
                var centre = new Vector3((k.x + 0.5f) * T, 0f, (k.y + 0.5f) * T);
                if ((new Vector2(centre.x - p.x, centre.z - p.z)).magnitude > reach + T * 0.7f) continue;
                tiles.TryGetValue(k, out var t);
                if (t != null && !t.dirty) continue;
                if (t != null && t.job != null) continue;
                if (running >= 4) continue;
                var box = new Bounds(centre, new Vector3(T + 8f, 2000f, T + 8f));
                net.LinesIn(box, lineBuf);
                var nodes = new List<RailTrackBuilder.NodeInfo>();
                foreach (var n in net.nodes)
                {
                    if (n.ends.Count == 0 || n.pos.x < k.x * T || n.pos.x >= (k.x + 1) * T || n.pos.z < k.y * T || n.pos.z >= (k.y + 1) * T) continue;
                    var e = n.ends[0];
                    nodes.Add(new RailTrackBuilder.NodeInfo { pos = n.pos, degree = n.ends.Count, outward = net.lines[e.x].Outward(e.y) });
                }
                if (t == null) { if (lineBuf.Count == 0 && nodes.Count == 0) continue; tiles[k] = t = new TileObj(); }
                t.dirty = false;
                var lines = new List<RailLine>(lineBuf);
                var w = world; var key = k; int sd = seed;
                t.job = Task.Run(() => RailTrackBuilder.Track(w, lines, nodes, key, sd));
                running++;
            }
            // fold far tiles
            var drop = new List<Vector2Int>();
            foreach (var kv in tiles)
            {
                var centre = new Vector2((kv.Key.x + 0.5f) * T, (kv.Key.y + 0.5f) * T);
                if ((centre - new Vector2(p.x, p.z)).magnitude > keep + T && kv.Value.job == null) drop.Add(kv.Key);
            }
            foreach (var k in drop) { Clear(tiles[k]); tiles.Remove(k); }
            // stations
            foreach (var st in net.stations)
            {
                float d = (st.pos - new Vector2(p.x, p.z)).magnitude;
                stations.TryGetValue(st.id, out var so);
                if (so == null && d < reach + st.half)
                {
                    so = stations[st.id] = new StationObj { spots = new List<RailTrackBuilder.Spot>() };
                    var w = world; var s = st; var spots = so.spots; int sd = seed;
                    so.job = Task.Run(() => RailTrackBuilder.Station(w, s, spots, sd));
                }
                else if (so != null && so.job == null && d > keep + st.half + 40f) { ClearStation(so); stations.Remove(st.id); }
            }
        }

        /// <summary>Turn at most one finished tile or station into objects per frame (cooking happens on workers).</summary>
        void Finish()
        {
            foreach (var kv in tiles)
            {
                var t = kv.Value;
                if (t.bake != null)
                {
                    if (!t.bake.IsCompleted) continue;
                    t.bake = null;
                    if (t.col && t.solid) { t.col.sharedMesh = t.solid; t.col.enabled = true; }
                    continue;
                }
                if (t.job == null || !t.job.IsCompleted) continue;
                var job = t.job; t.job = null;
                if (job.IsFaulted) { Debug.LogException(job.Exception); return; }
                ClearMeshes(t.go, ref t.mesh, ref t.glowMesh, ref t.solid);
                foreach (var pr in t.props) if (pr) Destroy(pr);
                t.props.Clear();
                ClearPoleMeshes(t);
                var k = job.Result;
                var origin = new Vector3(kv.Key.x * RailTrackBuilder.Tile, 0f, kv.Key.y * RailTrackBuilder.Tile);
                t.go = Build("RailTile_" + kv.Key.x + "_" + kv.Key.y, k, origin, Quaternion.identity, out t.mesh, out t.glowMesh, out t.solid, out t.col, out t.bake);
                Props(t, kv.Key);
                Poles(t, k);
                return;
            }
            foreach (var kv in stations)
            {
                var so = kv.Value;
                if (so.bake != null)
                {
                    if (!so.bake.IsCompleted) continue;
                    so.bake = null;
                    if (so.col && so.solid) { so.col.sharedMesh = so.solid; so.col.enabled = true; }
                    continue;
                }
                if (so.job == null || !so.job.IsCompleted) continue;
                var job = so.job; so.job = null;
                if (job.IsFaulted) { Debug.LogException(job.Exception); return; }
                var st = net.stations[kv.Key];
                so.go = Build("Station_" + st.id, job.Result, st.World(0f, 0f), Quaternion.identity, out so.mesh, out so.glowMesh, out so.solid, out so.col, out so.bake);
                StationProps(so, st);
                return;
            }
        }

        GameObject Build(string name, RailKit k, Vector3 at, Quaternion rot, out Mesh mesh, out Mesh glowMesh, out Mesh solid, out MeshCollider col, out Task bake)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(at, rot);
            mesh = MadMax.Voxel.VoxelMesher.ToMesh(k.mesh, name);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            glowMesh = null; solid = null; col = null; bake = null;
            if (k.glow.verts.Count > 0)
            {
                var g = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(go.transform, false);
                glowMesh = MadMax.Voxel.VoxelMesher.ToMesh(k.glow, name + "_glow");
                g.GetComponent<MeshFilter>().sharedMesh = glowMesh;
                g.GetComponent<MeshRenderer>().sharedMaterial = glow;
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (k.solid.verts.Count > 0)
            {
                solid = MadMax.Voxel.VoxelMesher.ToMesh(k.solid, name + "_solid");
                col = go.AddComponent<MeshCollider>();
                col.enabled = false;
                var id = solid.GetEntityId();
                bake = Task.Run(() => Physics.BakeMesh(id, false));
            }
            return go;
        }

        static void ClearMeshes(GameObject go, ref Mesh a, ref Mesh b, ref Mesh c)
        {
            if (go) Destroy(go);
            if (a) Destroy(a); if (b) Destroy(b); if (c) Destroy(c);
            a = b = c = null;
        }

        void Clear(TileObj t)
        {
            ClearMeshes(t.go, ref t.mesh, ref t.glowMesh, ref t.solid);
            foreach (var p in t.props) if (p) Destroy(p);
            t.props.Clear();
            ClearPoleMeshes(t);
        }

        static void ClearPoleMeshes(TileObj t) { foreach (var m in t.poleMeshes) if (m) Destroy(m); t.poleMeshes.Clear(); }

        /// <summary>The tile's telegraph poles: each a body of its own that snaps when a car hits it, its wire span to
        /// the next pole a child that comes down with it (and with the next pole).</summary>
        void Poles(TileObj t, RailKit k)
        {
            foreach (var p in k.poles)
            {
                var go = new GameObject("TelegraphPole", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(p.foot, Quaternion.identity);
                var m = MadMax.Voxel.VoxelMesher.ToMesh(p.post.mesh, "TelegraphPole");
                t.poleMeshes.Add(m);
                go.GetComponent<MeshFilter>().sharedMesh = m;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1; cap.radius = 0.12f; cap.height = 6.5f; cap.center = new Vector3(0f, 3.25f, 0f);
                var pole = BreakablePole.ForTelegraph(go, p.key);
                if (p.wires.mesh.verts.Count > 0)
                {
                    var wg = new GameObject("Wires", typeof(MeshFilter), typeof(MeshRenderer));
                    wg.transform.SetParent(go.transform, false);
                    var wm = MadMax.Voxel.VoxelMesher.ToMesh(p.wires.mesh, "TelegraphWires");
                    t.poleMeshes.Add(wm);
                    wg.GetComponent<MeshFilter>().sharedMesh = wm;
                    var wr = wg.GetComponent<MeshRenderer>();
                    wr.sharedMaterial = mat; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pole.wires.Add(wr);
                }
                t.props.Add(go);
                // wires between neighbours: the span into a pole belongs to the one before it
                telegraph[p.key] = pole;
                if (p.prev != null && telegraph.TryGetValue(p.prev, out var before) && before) Link(before, pole);
                if (p.next != null && telegraph.TryGetValue(p.next, out var after) && after) Link(pole, after);
            }
        }

        static void Link(BreakablePole from, BreakablePole to)
        {
            to.feeds = from;
            if (to.state != BreakablePole.State.Standing || from.state != BreakablePole.State.Standing)
                foreach (var w in from.wires) if (w) w.enabled = false;                       // one of them is down: no span
        }

        void ClearStation(StationObj so) => ClearMeshes(so.go, ref so.mesh, ref so.glowMesh, ref so.solid);

        // ------------------------------------------------------------------ working props

        /// <summary>Lever stands at points, crossing barriers, signals at station throats in this tile.</summary>
        void Props(TileObj t, Vector2Int key)
        {
            float T = RailTrackBuilder.Tile, x0 = key.x * T, z0 = key.y * T;
            bool In(Vector3 p) => p.x >= x0 && p.x < x0 + T && p.z >= z0 && p.z < z0 + T;
            foreach (var n in net.nodes)
            {
                if (!n.Switch || !In(n.pos)) continue;
                t.props.Add(RailSwitch.Create(net, n, mat, transform).gameObject);
            }
            net.LinesIn(new Bounds(new Vector3(x0 + T * 0.5f, 0f, z0 + T * 0.5f), new Vector3(T, 4000f, T)), lineBuf);
            foreach (var l in lineBuf)
            {
                // a crossing gets one set of barriers where its flagged run starts
                for (int i = 0; i < l.pts.Length; i++)
                {
                    if ((l.flags[i] & RailLine.Crossing) == 0 || (i > 0 && (l.flags[i - 1] & RailLine.Crossing) != 0)) continue;
                    int j = i; while (j < l.pts.Length - 1 && (l.flags[j + 1] & RailLine.Crossing) != 0) j++;
                    var mid = Vector3.Lerp(l.pts[i], l.pts[j], 0.5f);
                    if (!In(mid)) continue;
                    t.props.Add(RailCrossing.Create(net, l, (l.cum[i] + l.cum[j]) * 0.5f, l.cum[j] - l.cum[i] + 4f, mat, glow, transform).gameObject);
                }
            }
            foreach (var st in net.stations)
            {
                if (st.tier != StationTier.Through && st.tier != StationTier.Terminal && st.tier != StationTier.Junction) continue;
                float[] us = st.tier == StationTier.Terminal ? new[] { -st.half - 30f } : st.tier == StationTier.Through ? new[] { -st.half - 32f, st.half + 32f } : new float[0];
                foreach (float u in us)
                {
                    var at = st.World(u, -3.2f * (u < 0 ? 1f : -1f));
                    if (!In(at)) continue;
                    t.props.Add(RailSignal.Create(st, at, u < 0f ? st.Yaw + 180f : st.Yaw, mat, glow, transform).gameObject);
                }
            }
        }

        void StationProps(StationObj so, RailStation st)
        {
            foreach (var s in so.spots)
            {
                switch (s.kind)
                {
                    case "loot":
                    {
                        var go = new GameObject("Search_" + s.title);
                        go.transform.SetParent(so.go.transform, true);
                        go.transform.SetPositionAndRotation(s.at, Quaternion.Euler(0f, s.yaw, 0f));
                        var b = go.AddComponent<BoxCollider>(); b.isTrigger = true; b.size = s.half * 2f;
                        var l = go.AddComponent<Lootable>();
                        l.key = "rail:st:" + st.id + ":" + s.title; l.table = s.table; l.title = s.title;
                        break;
                    }
                    case "ramp":
                    case "ramp_slope":
                    {
                        // the end-loading ramp: a drivable deck (flat landing, then the slope down to the ground)
                        var go = new GameObject("LoadingRamp");
                        go.transform.SetParent(so.go.transform, true);
                        var dir = Quaternion.Euler(0f, s.yaw, 0f);
                        go.transform.SetPositionAndRotation(s.kind == "ramp" ? s.at : new Vector3(s.at.x, st.height, s.at.z), dir);
                        var b = go.AddComponent<BoxCollider>(); b.isTrigger = true; b.size = new Vector3(s.half.x * 2f, 0.4f, s.half.z * 2f); b.center = new Vector3(0f, -0.2f, 0f);
                        // StructureGround size: half width x, half length z, top at -z and +z (local y)
                        float top = RailNetwork.RailTop + 0.75f;
                        var size = s.kind == "ramp" ? new Vector4(s.half.x, s.half.z, 0f, 0f) : new Vector4(s.half.x, s.half.z, -top * 0f + 0.02f, 0f);
                        if (s.kind == "ramp_slope") size = new Vector4(s.half.x, s.half.z, top, 0.02f);       // -z end at the landing (toward the buffers), +z at the ground
                        StructureGround.AddDeck(go, go.transform, b, size);
                        break;
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (net != null) net.Changed -= OnChanged;
            foreach (var t in tiles.Values) Clear(t);
            foreach (var s in stations.Values) ClearStation(s);
            if (Instance == this) Instance = null;
        }
    }
}
