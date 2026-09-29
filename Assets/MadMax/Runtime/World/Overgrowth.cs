using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Decay on buildings: ivy climbs the walls from the ground, moss and weeds settle on roofs and ledges,
    /// flowers open on old growth, and ruins shed the odd chunk of masonry. Growth is a front over the building's outer
    /// surface (Dijkstra with hashed costs from ground seeds, climbing is cheap, hanging down is dear), computed once per
    /// template variant on a worker thread. The front advances with world age (<see cref="DayNight.TotalDays"/>) at a
    /// biome rate from a per-building starting level, so every building spawns part-overgrown and keeps growing.
    /// The overlay is a half-size voxel mesh laid against the wall faces; vines on knocked-out wall voxels drop away.</summary>
    public class Overgrowth : MonoBehaviour
    {
        class Plan
        {
            public Vector3Int[] host, cell;
            public byte[] face;           // index into Dirs: the host → cell direction
            public float[] birth;         // growth level at which the node appears (sorted ascending)
            public float span;            // level at which everything is covered
            public int topY;
        }

        static readonly Vector3Int[] Dirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
        static readonly Dictionary<string, Task<Plan>> plans = new Dictionary<string, Task<Plan>>();
        static float crumbleAt;
        static int lastBuildFrame;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lock (plans) plans.Clear(); crumbleAt = 0f; lastBuildFrame = 0; }

        DestructibleVoxels target;
        Task<Plan> job;
        Task<VoxelMesher.MeshData> meshJob;
        Plan plan;
        float size, level0, rate, factor, nextCheck;
        int seed, shownLevel = int.MinValue, shownKey = -1;
        bool dirty, ruin;
        Biome biome;
        GameObject overlay;
        Mesh mesh;
        readonly List<int> visible = new List<int>();

        static float BiomeFactor(Biome b) => b switch
        {
            Biome.Tropical => 1f, Biome.Forest => 0.9f, Biome.Village => 0.75f, Biome.City => 0.6f, Biome.Town => 0.5f, Biome.Nuclear => 0.45f, _ => 0.25f
        };

        /// <summary>Give a spawned building its overgrowth. The plan is shared per template and variant (3 per template).</summary>
        public static void Attach(DestructibleVoxels d, VoxelGrid template, float size, string key, Biome biome)
        {
            var t = DeformableTerrain.Instance;
            if (!d || template == null || !t || !t.overgrowthMaterial) return;
            var o = d.gameObject.AddComponent<Overgrowth>();
            o.target = d; o.size = size; o.biome = biome; o.factor = BiomeFactor(biome);
            o.seed = Stable(key);
            int variant = (o.seed & 0x7fffffff) % 3;
            string id = (d.TemplateId ?? d.name) + "#" + variant;
            o.ruin = id.StartsWith("Tower") || id.StartsWith("BrickHouse") || id.StartsWith("Shack") || id.StartsWith("Farmhouse");
            lock (plans)
            {
                if (!plans.TryGetValue(id, out o.job)) plans[id] = o.job = Task.Run(() => MakePlan(template, variant * 7919 + 17));
            }
            d.Carved += o.OnCarved;
            o.nextCheck = Time.time + Random.value;
        }

        static int Stable(string s) { unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; } }

        void OnCarved() => dirty = true;

        void OnDestroy()
        {
            if (target) target.Carved -= OnCarved;
            if (mesh) Destroy(mesh);
        }

        float Level() => level0 + rate * DayNight.TotalDays;

        void Update()
        {
            if (plan == null)
            {
                if (job == null || !job.IsCompleted) return;
                if (job.IsFaulted) { Debug.LogException(job.Exception); enabled = false; return; }
                plan = job.Result;
                // how far along this building is when the world begins, and how fast it spreads
                float h01 = (Stable(target.StateKey ?? name) & 0xffff) / 65535f;
                level0 = plan.span * factor * (0.08f + 0.85f * h01 * h01);
                rate = plan.span * factor / 30f;
            }
            if (meshJob != null)
            {
                if (!meshJob.IsCompleted) return;
                if (meshJob.IsFaulted) Debug.LogException(meshJob.Exception); else Apply(meshJob.Result);
                meshJob = null;
            }
            if (Time.time < nextCheck && !dirty) return;
            nextCheck = Time.time + 4f + Random.value;
            float level = Level();
            int li = Mathf.FloorToInt(level), key = (Weather.Temperature < 1f ? 1 : 0) | (Weather.LocalSnow > 0.4f ? 2 : 0);
            if (li != shownLevel || key != shownKey || dirty)
            {
                if (Time.frameCount == lastBuildFrame) { nextCheck = 0f; return; }     // one rebuild per frame, all buildings
                lastBuildFrame = Time.frameCount;
                shownLevel = li; shownKey = key; dirty = false;
                StartMesh(level, key);
            }
            if (ruin) Crumble(level);
        }

        // ------------------------------------------------------------------ plan (worker thread)

        static long K(int a, int b) => ((long)a << 32) ^ (uint)b;

        static Plan MakePlan(VoxelGrid g, int seed)
        {
            // outermost voxel of every row along each axis: only faces that see the open air grow
            var minX = new Dictionary<long, int>(); var maxX = new Dictionary<long, int>();
            var minZ = new Dictionary<long, int>(); var maxZ = new Dictionary<long, int>();
            var maxY = new Dictionary<long, int>();
            int groundY = int.MaxValue, topY = int.MinValue;
            foreach (var p in g.voxels.Keys)
            {
                long yz = K(p.y, p.z), xy = K(p.x, p.y), xz = K(p.x, p.z);
                minX[yz] = minX.TryGetValue(yz, out var a) ? Mathf.Min(a, p.x) : p.x;
                maxX[yz] = maxX.TryGetValue(yz, out a) ? Mathf.Max(a, p.x) : p.x;
                minZ[xy] = minZ.TryGetValue(xy, out a) ? Mathf.Min(a, p.z) : p.z;
                maxZ[xy] = maxZ.TryGetValue(xy, out a) ? Mathf.Max(a, p.z) : p.z;
                maxY[xz] = maxY.TryGetValue(xz, out a) ? Mathf.Max(a, p.y) : p.y;
                groundY = Mathf.Min(groundY, p.y); topY = Mathf.Max(topY, p.y);
            }
            var index = new Dictionary<Vector3Int, int>();
            var host = new List<Vector3Int>(); var cell = new List<Vector3Int>(); var face = new List<byte>();
            foreach (var p in g.voxels.Keys)
            {
                for (byte f = 0; f < 6; f++)
                {
                    if (f == 3) continue;                                              // no hanging growth
                    var q = p + Dirs[f];
                    if (index.ContainsKey(q) || g.voxels.ContainsKey(q)) continue;
                    bool open = f switch
                    {
                        0 => p.x == maxX[K(p.y, p.z)], 1 => p.x == minX[K(p.y, p.z)],
                        2 => p.y == maxY[K(p.x, p.z)],
                        4 => p.z == maxZ[K(p.x, p.y)], _ => p.z == minZ[K(p.x, p.y)]
                    };
                    if (!open) continue;
                    index[q] = host.Count;
                    host.Add(p); cell.Add(q); face.Add(f);
                }
            }

            int n = host.Count;
            var dist = new float[n];
            for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;
            var heap = new MinHeap(n);
            for (int i = 0; i < n; i++)
            {
                if (face[i] != 2 && host[i].y <= groundY + 1 && Pal.Hash(cell[i], seed) < 0.22f) dist[i] = Pal.Hash(cell[i], seed + 1) * 6f;
                else if (face[i] == 2 && Pal.Hash(cell[i], seed + 2) < 0.02f) dist[i] = 8f + Pal.Hash(cell[i], seed + 3) * 30f;   // wind-blown spores
                if (!float.IsInfinity(dist[i])) heap.Push(i, dist[i]);
            }
            while (heap.Count > 0)
            {
                heap.Pop(out int i, out float di);
                if (di > dist[i]) continue;
                var c = cell[i];
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int m = (dx != 0 ? 1 : 0) + (dy != 0 ? 1 : 0) + (dz != 0 ? 1 : 0);
                    if (m == 0 || m == 3) continue;
                    var q = new Vector3Int(c.x + dx, c.y + dy, c.z + dz);
                    if (!index.TryGetValue(q, out int j)) continue;
                    float cost = (1f + 1.6f * Pal.Hash(q, seed + 9)) * (dy > 0 ? 0.75f : dy < 0 ? 2.4f : 1.25f) * (m == 2 ? 1.4f : 1f);
                    float nd = di + cost;
                    if (nd < dist[j]) { dist[j] = nd; heap.Push(j, nd); }
                }
            }

            var order = new List<int>(n);
            for (int i = 0; i < n; i++) if (!float.IsInfinity(dist[i])) order.Add(i);
            order.Sort((a, b) => dist[a].CompareTo(dist[b]));
            var plan = new Plan { host = new Vector3Int[order.Count], cell = new Vector3Int[order.Count], face = new byte[order.Count], birth = new float[order.Count], topY = topY };
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                plan.host[k] = host[i]; plan.cell[k] = cell[i]; plan.face[k] = face[i]; plan.birth[k] = dist[i];
            }
            plan.span = order.Count > 0 ? plan.birth[order.Count - 1] + 6f : 1f;
            return plan;
        }

        class MinHeap
        {
            int[] id; float[] key; public int Count;
            public MinHeap(int cap) { id = new int[Mathf.Max(16, cap)]; key = new float[id.Length]; }
            public void Push(int i, float k)
            {
                if (Count == id.Length) { System.Array.Resize(ref id, Count * 2); System.Array.Resize(ref key, Count * 2); }
                int c = Count++;
                while (c > 0) { int p = (c - 1) >> 1; if (key[p] <= k) break; id[c] = id[p]; key[c] = key[p]; c = p; }
                id[c] = i; key[c] = k;
            }
            public void Pop(out int i, out float k)
            {
                i = id[0]; k = key[0];
                int li = id[--Count]; float lk = key[Count];
                int c = 0;
                while (true)
                {
                    int a = c * 2 + 1; if (a >= Count) break;
                    if (a + 1 < Count && key[a + 1] < key[a]) a++;
                    if (key[a] >= lk) break;
                    id[c] = id[a]; key[c] = key[a]; c = a;
                }
                id[c] = li; key[c] = lk;
            }
        }

        // ------------------------------------------------------------------ overlay

        static readonly Color32[] Ivy = { Pal.Hex("1e3a18"), Pal.Hex("284a1e"), Pal.Hex("345c24"), Pal.Hex("42702c") };
        static readonly Color32[] Jungle = { Pal.Hex("2a5a1c"), Pal.Hex("367024"), Pal.Hex("46862c"), Pal.Hex("5a9e36") };
        static readonly Color32[] DryVine = { Pal.Hex("5a4a24"), Pal.Hex("6e5a2c"), Pal.Hex("826a34"), Pal.Hex("967a3e") };
        static readonly Color32[] Sick = { Pal.Hex("4a5220"), Pal.Hex("5a6424"), Pal.Hex("6e7a2a"), Pal.Hex("8a9430") };
        static readonly Color32[] Moss = { Pal.Hex("33421c"), Pal.Hex("3e4e20"), Pal.Hex("4a5c26"), Pal.Hex("56682c") };
        static readonly Color32[] Autumn = { Pal.Hex("6a4424"), Pal.Hex("80542a"), Pal.Hex("946430"), Pal.Hex("a87436") };
        static readonly Color32[] Blossom = { Pal.Hex("eeeadc"), Pal.Hex("e8c440"), Pal.Hex("d070a0"), Pal.Hex("8a50b0"), Pal.Hex("b83020") };
        static readonly Color32 Spore = Pal.Hex("9cff3a"), SnowCap = Pal.Hex("c8cad4");

        Color32[] Leaves => biome switch
        {
            Biome.Tropical => Jungle, Biome.Forest => Jungle, Biome.Desert => DryVine, Biome.Nuclear => Sick, _ => Ivy
        };

        void StartMesh(float level, int key)
        {
            visible.Clear();
            var grid = target.Grid;
            for (int i = 0; i < plan.birth.Length && plan.birth[i] < level; i++)
                if (grid.voxels.ContainsKey(plan.host[i])) visible.Add(i);
            if (visible.Count == 0) { Apply(null); return; }
            var nodes = visible.ToArray();
            var p = plan; int sd = seed; var leaves = Leaves; bool frost = (key & 1) != 0, snowy = (key & 2) != 0;
            bool flowers = biome != Biome.Desert && biome != Biome.Nuclear && !frost;
            bool spores = biome == Biome.Nuclear;
            float sz = size;
            meshJob = Task.Run(() => BuildOverlay(p, nodes, level, sd, leaves, frost, snowy, flowers, spores, sz));
        }

        static VoxelMesher.MeshData BuildOverlay(Plan p, int[] nodes, float level, int seed, Color32[] leaves, bool frost, bool snowy, bool flowers, bool spores, float size)
        {
            var g = new VoxelGrid();
            byte wood = (byte)ResourceType.Wood;
            foreach (int i in nodes)
            {
                float age = level - p.birth[i];
                var q = p.cell[i];
                int f = p.face[i];
                var d = Dirs[f];
                int ax = d.x != 0 ? 0 : d.y != 0 ? 1 : 2, sgn = d.x + d.y + d.z;
                bool top = f == 2;
                float cover = Mathf.Clamp01(age / 4f) * 0.92f + 0.05f;
                // the half-size layer against the wall face: fine centres of cell q sit at 2q-1 and 2q
                int near = sgn > 0 ? -1 : 0;
                for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                {
                    Vector3Int fine;
                    if (ax == 0) fine = new Vector3Int(2 * q.x + near, 2 * q.y - 1 + a, 2 * q.z - 1 + b);
                    else if (ax == 1) fine = new Vector3Int(2 * q.x - 1 + a, 2 * q.y + near, 2 * q.z - 1 + b);
                    else fine = new Vector3Int(2 * q.x - 1 + a, 2 * q.y - 1 + b, 2 * q.z + near);
                    if (Pal.Hash(fine, seed) > cover) continue;
                    float h = Pal.Hash(fine, seed + 1);
                    Color32 col;
                    if (top) col = snowy ? SnowCap : Moss[Mathf.Min(3, (int)(h * 4f))];
                    else if (frost && h < 0.5f) col = Autumn[Mathf.Min(3, (int)(h * 8f))];
                    else col = leaves[Mathf.Min(3, (int)(h * 4f))];
                    if (!top && age > 9f && Pal.Hash(fine, seed + 2) < 0.05f)
                    {
                        if (flowers) col = Blossom[Mathf.Min(Blossom.Length - 1, (int)(Pal.Hash(fine, seed + 6) * Blossom.Length))];
                        else if (spores) col = Spore;
                    }
                    col.a = top ? (byte)255 : (byte)215;
                    g.voxels[fine] = new Vox { color = col, mat = wood };
                    // old growth bulges out from the wall
                    if (!top && age > 7f && Pal.Hash(fine, seed + 3) < 0.3f)
                    {
                        var oc = col; oc.a = 170;
                        g.voxels[fine + d] = new Vox { color = oc, mat = wood };
                    }
                }
                // weeds on ledges and roofs
                if (top && !snowy && age > 6f && Pal.Hash(q, seed + 4) < 0.14f)
                {
                    int h = 1 + (int)(Pal.Hash(q, seed + 5) * 3f);
                    var root = new Vector3Int(2 * q.x - 1, 2 * q.y, 2 * q.z - 1);
                    for (int y = 0; y < h; y++)
                    {
                        var c = (frost ? Autumn : leaves)[Mathf.Min(3, 1 + y)];
                        c.a = (byte)(230 - y * 50);
                        g.voxels[root + new Vector3Int(0, y, 0)] = new Vox { color = c, mat = wood };
                    }
                }
            }
            return VoxelMesher.BuildData(g, size * 0.5f);
        }

        void Apply(VoxelMesher.MeshData md)
        {
            if (!this || !target) return;
            if (mesh) Destroy(mesh);
            mesh = null;
            if (md == null || md.verts.Count == 0) { if (overlay) overlay.SetActive(false); return; }
            mesh = VoxelMesher.ToMesh(md, "Overgrowth");
            if (!overlay)
            {
                overlay = new GameObject("Overgrowth", typeof(MeshFilter), typeof(MeshRenderer));
                overlay.transform.SetParent(transform, false);
                overlay.transform.localPosition = Vector3.one * (size * 0.25f);   // half-size voxels: centres at q*s ± s/4
                var mr = overlay.GetComponent<MeshRenderer>();
                mr.sharedMaterial = DeformableTerrain.Instance ? DeformableTerrain.Instance.overgrowthMaterial : null;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            overlay.SetActive(true);
            overlay.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ decay

        /// <summary>Old ruins near the player now and then lose a chunk of masonry where the growth has taken hold.
        /// Authority only (the carve is broadcast), at most one crumble a minute across the world.</summary>
        void Crumble(float level)
        {
            if (Time.time < crumbleAt || plan.birth.Length == 0 || level < plan.span * 0.55f) return;
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return;
            var focus = g.Current ? g.Current.transform.position : g.Player ? g.Player.transform.position : transform.position;
            if ((focus - transform.position).sqrMagnitude > 35f * 35f) return;
            if (Random.value > 0.2f) { crumbleAt = Time.time + 20f; return; }
            crumbleAt = Time.time + Random.Range(60f, 160f);
            int pick = Random.Range(0, plan.birth.Length);
            for (int tries = 0; tries < 12; tries++, pick = Random.Range(0, plan.birth.Length))
            {
                var h = plan.host[pick];
                if (plan.birth[pick] > level || h.y < plan.topY * 0.5f || !target.Grid.voxels.ContainsKey(h)) continue;
                var world = transform.TransformPoint((Vector3)h * size);
                var dir = -transform.TransformDirection((Vector3)Dirs[plan.face[pick]]);
                target.ApplyHit(world, dir, 0.7f, 0.22f, null);
                Fx.Smoke(world, Vector3.down * 0.4f + Fx.Wind * 0.3f, 0.5f, new Color(0.45f, 0.4f, 0.34f, 0.6f), 2.5f);
                MadMax.Audio.Sfx.Play("crash_small", world, 0.5f, 0.6f, 40f);
                return;
            }
        }
    }
}
