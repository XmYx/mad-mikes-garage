using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    public struct Surface
    {
        public float wet;       // 0 dry .. 1 soaked
        public float road;      // 0 off-road .. 1 on road
        public float softness;  // how easily the ground deforms
        public float mud;       // grip blend towards tyre mud grip
        public float rut;       // current rut depth (m)
        public float ice;       // 0 .. 1 frozen surface (snow / black ice)
    }

    /// <summary>Chunk-streamed heightfield with a persistent deformation layer. Wheels press ruts, spinning wheels dig,
    /// displaced soil builds berms. Wet ground deforms far more; roads barely at all.</summary>
    [DefaultExecutionOrder(-50)]
    public partial class DeformableTerrain : MonoBehaviour
    {
        public const int N = 32;              // cells per chunk side
        public const float Cell = 0.25f;      // metres per cell
        public const float ChunkWorld = N * Cell;
        const int V = N + 1;

        public static DeformableTerrain Instance { get; private set; }

        /// <summary>Modified destructible grids by prop key, so damage survives chunks streaming out and back in.</summary>
        public readonly Dictionary<string, MadMax.Voxel.VoxelGrid> DestructionState = new Dictionary<string, MadMax.Voxel.VoxelGrid>();
        /// <summary>Template id of each modified prop (for saving the difference).</summary>
        public readonly Dictionary<string, string> DestructionTemplates = new Dictionary<string, string>();
        public WorldGen World { get; private set; }

        public Material material;
        public Material propMaterial;
        /// <summary>Instances for world-spawned props (underground cutaway) and swaying vegetation.</summary>
        public Material worldPropMaterial, vegetationMaterial;
        public Transform focus;
        /// <summary>Additional streaming centres (server: every connected player).</summary>
        public readonly System.Collections.Generic.List<Transform> extraFoci = new System.Collections.Generic.List<Transform>();
        public float viewRadius = 56f;
        public int buildsPerFrame = 3;
        public int rebuildsPerFrame = 6;
        public float colliderInterval = 1.0f;

        [Header("Mud")]
        public float sinkPerNewton = 1.4e-5f;
        public float maxSink = 0.09f;
        public float digRate = 0.16f;
        public float maxDepth = 0.3f;
        public float bermShare = 0.45f;
        public float maxBerm = 0.08f;

        class Chunk
        {
            public Vector2Int c;
            public float lastUse;          // Time.time of the last query (queried data is kept, see cleanup)
            public float[] h, d, wet, road, dist, along, water, shore;
            public bool[] paved;
            public byte[] biome, feature;
            public float[] trampled;          // game day the ground cover was flattened (ruts, tyres, digging)
            public GameObject floraGo;
            public Mesh floraMesh;
            public bool floraDirty;
            public float floraNext;
            public int floraKey = -1;
            public GameObject waterGo;
            public byte[] pave, compact;      // 0 none, 1 asphalt, 2 concrete; compacted by a roller
            public float[] cure;              // 0 wet .. 1 set
            public bool terraformed;
            public float[] rut;               // wheel / foot deformation that heals (not terraform), null until rutted
            public float rutHealAt;
            public GameObject go;
            public Mesh mesh;
            public MeshCollider col;
            public bool meshDirty, colDirty, deformed;
            public float colTime, colorWet = -1f, colorSnow = -1f;
        }

        readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        readonly List<Chunk> active = new List<Chunk>();
        readonly List<Vector2Int> offsets = new List<Vector2Int>();
        readonly List<Vector2Int> berm = new List<Vector2Int>();
        float cleanupTime;

        static Vector3[] vb; static Vector3[] nb; static Color32[] cb; static int[] ib;

        public void Init(WorldGen world, Material terrainMat, Material propMat)
        {
            World = world; propMaterial = propMat; Instance = this;
            material = terrainMat ? new Material(terrainMat) : null;
            if (material) { material.SetFloat("_SnowMask", 0f); material.SetFloat("_WorldCut", 1f); }   // terrain paints its own snow
            if (propMat)
            {
                worldPropMaterial = new Material(propMat) { name = "WorldProps" };
                worldPropMaterial.SetFloat("_WorldCut", 1f);
                vegetationMaterial = new Material(worldPropMaterial) { name = "Vegetation" };
                vegetationMaterial.SetFloat("_Sway", 0.0016f);
                InitFlora(propMat);
            }
            SetViewRadius(viewRadius);
            if (vb == null)
            {
                vb = new Vector3[N * N * 6]; nb = new Vector3[vb.Length]; cb = new Color32[vb.Length]; ib = new int[vb.Length];
                for (int i = 0; i < ib.Length; i++) ib[i] = i;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Change streaming distance (LOD setting). Chunks beyond it unload on the next Update.</summary>
        public void SetViewRadius(float radius)
        {
            viewRadius = radius;
            offsets.Clear();
            int R = Mathf.CeilToInt(viewRadius / ChunkWorld) + 3;   // +2 rings are only prefetched (data, no objects)
            for (int x = -R; x <= R; x++) for (int z = -R; z <= R; z++) offsets.Add(new Vector2Int(x, z));
            offsets.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
        }

        // ------------------------------------------------------------------ data
        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
        public static Vector2Int ChunkOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / ChunkWorld), Mathf.FloorToInt(p.z / ChunkWorld));

        Chunk Data(Vector2Int c)
        {
            if (chunks.TryGetValue(c, out var ch)) { ch.lastUse = Time.time; return ch; }
            return Insert(Generate(World, c));
        }

        /// <summary>Samples the generated ground for a chunk. Pure (no terrain state), so it also runs on worker threads.</summary>
        static Chunk Generate(WorldGen world, Vector2Int c)
        {
            var ch = new Chunk
            {
                c = c, h = new float[V * V], d = new float[V * V], wet = new float[V * V], road = new float[V * V],
                dist = new float[V * V], along = new float[V * V], paved = new bool[V * V],
                water = new float[V * V], shore = new float[V * V], biome = new byte[V * V], feature = new byte[V * V],
                pave = new byte[V * V], compact = new byte[V * V], cure = new float[V * V]
            };
            for (int j = 0; j < V; j++)
            for (int i = 0; i < V; i++)
            {
                int k = j * V + i;
                var s = world.Sample((c.x * N + i) * Cell, (c.y * N + j) * Cell);
                ch.h[k] = s.height; ch.wet[k] = s.baseWet; ch.road[k] = s.road;
                ch.dist[k] = s.roadDist; ch.along[k] = s.along; ch.paved[k] = s.paved;
                ch.water[k] = s.water; ch.shore[k] = s.shore; ch.biome[k] = (byte)s.biome; ch.feature[k] = s.feature;
            }
            return ch;
        }

        Chunk Insert(Chunk ch)
        {
            bool edited = false;
            if (savedEdits.TryGetValue(ch.c, out var edit)) { ApplySaved(ch, edit); savedEdits.Remove(ch.c); edited = true; }
            ch.lastUse = Time.time;
            chunks[ch.c] = ch;
            // channels dug earlier: flood when the trench or the lake it leads from streams in
            if (edited || HasWater(ch)) FloodAround(new Vector3((ch.c.x + 0.5f) * ChunkWorld, 0f, (ch.c.y + 0.5f) * ChunkWorld), ChunkWorld * 0.6f);
            return ch;
        }

        // background generation of chunk data ahead of the focus: the main thread only builds meshes
        readonly System.Collections.Concurrent.ConcurrentQueue<Chunk> prefetched = new System.Collections.Concurrent.ConcurrentQueue<Chunk>();
        readonly HashSet<Vector2Int> pending = new HashSet<Vector2Int>();
        const int MaxPrefetch = 6;

        void Prefetch(Vector2Int center)
        {
            while (prefetched.TryDequeue(out var done))
            {
                pending.Remove(done.c);
                if (done.h != null && !chunks.ContainsKey(done.c)) Insert(done);
            }
            if (pending.Count >= MaxPrefetch) return;
            var world = World;
            foreach (var o in offsets)
            {
                var c = center + o;
                if (chunks.ContainsKey(c) || pending.Contains(c)) continue;
                pending.Add(c);
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { prefetched.Enqueue(Generate(world, c)); }
                    catch (System.Exception e) { Debug.LogException(e); prefetched.Enqueue(new Chunk { c = c }); }
                });
                if (pending.Count >= MaxPrefetch) break;
            }
        }

        readonly List<Vector2Int> drop = new List<Vector2Int>();
        readonly System.Diagnostics.Stopwatch buildClock = new System.Diagnostics.Stopwatch();

        Chunk Locate(int ix, int iz, out int k)
        {
            var c = new Vector2Int(FloorDiv(ix, N), FloorDiv(iz, N));
            var ch = Data(c);
            k = (iz - c.y * N) * V + (ix - c.x * N);
            return ch;
        }

        public float Height(float x, float z)
        {
            float gx = x / Cell, gz = z / Cell;
            int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float fx = gx - ix, fz = gz - iz;
            var ch = Locate(ix, iz, out int k);
            float h00 = ch.h[k] + ch.d[k], h10 = ch.h[k + 1] + ch.d[k + 1];
            float h01 = ch.h[k + V] + ch.d[k + V], h11 = ch.h[k + V + 1] + ch.d[k + V + 1];
            // same diagonal split as the render mesh
            return fz > fx ? h00 + (h11 - h01) * fx + (h01 - h00) * fz
                           : h00 + (h10 - h00) * fx + (h11 - h10) * fz;
        }

        public Vector3 Normal(float x, float z)
        {
            const float e = Cell * 0.5f;
            float dx = Height(x + e, z) - Height(x - e, z);
            float dz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        public Surface SurfaceAt(float x, float z)
        {
            var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k);
            return MakeSurface(ch, k);
        }

        static Surface MakeSurface(Chunk ch, int k)
        {
            float road = ch.road[k], d = ch.d[k];
            bool paved = ch.paved[k];
            // rain soaks the ground less than standing water: rain-only mud stays drivable, basins stay nasty
            float w = Weather.Wetness * 0.75f;
            float wet = Mathf.Clamp01(ch.wet[k] + w * (1f - road * 0.75f) + (d < -0.02f ? w * 0.4f : 0f));
            float soak = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(ch.wet[k] * 1.6f));
            float hard = road * (paved ? 0.97f : 0.6f);
            if (ch.pave[k] > 0)
            {
                float set = ch.cure[k];
                return new Surface { wet = w * 0.3f, road = 1f, rut = 0f, softness = (1f - set) * 0.35f, mud = (1f - set) * 0.4f, ice = Weather.Ice * 0.8f };
            }
            return new Surface
            {
                wet = wet, road = road, rut = Mathf.Max(0f, -d),
                softness = (0.1f + 0.9f * wet * soak) * (1f - hard) + Weather.Snow * 0.25f,
                mud = wet * soak * (1f - road * (paved ? 1f : 0.5f)) * (Weather.Temperature < 0f ? 0.3f : 1f),
                ice = Mathf.Clamp01(Weather.Ice * (0.6f + hard * 0.5f) + Mathf.Clamp01(-d * 8f) * Weather.Snow * 0.3f * (Weather.Temperature < 0f ? 1f : 0f))
            };
        }

        // ------------------------------------------------------------------ deformation
        float GetD(int ix, int iz) { var ch = Locate(ix, iz, out int k); return ch.d[k]; }

        void SetD(int ix, int iz, float v)
        {
            int cx = FloorDiv(ix, N), cz = FloorDiv(iz, N);
            int li = ix - cx * N, lj = iz - cz * N;
            Put(cx, cz, li, lj, v);
            if (li == 0) Put(cx - 1, cz, N, lj, v);
            if (lj == 0) Put(cx, cz - 1, li, N, v);
            if (li == 0 && lj == 0) Put(cx - 1, cz - 1, N, N, v);
        }

        void Put(int cx, int cz, int li, int lj, float v)
        {
            var ch = Data(new Vector2Int(cx, cz));
            int rk = lj * V + li;
            // ruts and berms are tracked apart so they can heal; terraforming makes a cell's shape permanent
            if (terraforming) { if (ch.rut != null) ch.rut[rk] = 0f; }
            else
            {
                if (ch.rut == null) { ch.rut = new float[V * V]; ch.rutHealAt = Time.time; rutted.Add(ch); }
                ch.rut[rk] += v - ch.d[rk];
            }
            ch.d[lj * V + li] = v;
            ch.meshDirty = ch.colDirty = ch.deformed = true;
            MarkTrampled(ch, lj * V + li);
            if (terraforming) ch.terraformed = true;
        }
        bool terraforming;

        // ------------------------------------------------------------------ ruts heal (roadmap 17)
        readonly List<Chunk> rutted = new List<Chunk>();
        int rutCursor;
        float rutTimer;

        /// <summary>Ruts, berms and footprints settle back over the days: about half a day, faster in rain, fastest
        /// when a dust storm drifts sand into them. A few chunks per second, round robin.</summary>
        void HealRuts(float dt)
        {
            if ((rutTimer -= dt) > 0f || rutted.Count == 0) return;
            rutTimer = 0.33f;
            float daySeconds = (DayNight.DayMinutes > 0f ? DayNight.DayMinutes : 24f) * 60f;
            float mult = (Weather.Raining && !Weather.Snowing ? 3f : 1f) * (1f + Storms.Dust * 5f);
            for (int n = 0; n < 2 && rutted.Count > 0; n++)
            {
                rutCursor = (rutCursor + 1) % rutted.Count;
                var ch = rutted[rutCursor];
                if (!chunks.TryGetValue(ch.c, out var live) || live != ch) { rutted.RemoveAt(rutCursor); continue; }   // streamed out
                float elapsed = Time.time - ch.rutHealAt;
                if (elapsed < 20f) continue;
                ch.rutHealAt = Time.time;
                float f = 1f - Mathf.Exp(-0.693f * elapsed / daySeconds * mult);                  // halves in a day
                bool any = false, changed = false;
                for (int k = 0; k < ch.rut.Length; k++)
                {
                    float r = ch.rut[k];
                    if (r > -0.001f && r < 0.001f) { ch.rut[k] = 0f; continue; }
                    float h = r * f;
                    ch.d[k] -= h; ch.rut[k] -= h;
                    any = true; changed |= Mathf.Abs(h) > 0.0005f;
                }
                if (changed) { ch.meshDirty = true; ch.colDirty = true; }
                if (!any) { ch.rut = null; rutted.RemoveAt(rutCursor); }
            }
        }

        /// <summary>Press a tyre footprint into the ground. <paramref name="dig"/> (0..1) is wheel-spin that excavates.</summary>
        public void Deform(Vector3 contact, Vector3 fwd, Vector3 side, float width, float load, float dig, float dt)
        {
            Trample(contact, width * 0.5f);
            var s = SurfaceAt(contact.x, contact.z);
            float sink = Mathf.Min(maxSink, s.softness * load * sinkPerNewton);
            float extra = dig * s.softness * digRate * dt;
            if (sink < 0.004f && extra <= 0f) return;

            float halfW = width * 0.5f, reach = halfW + 0.35f;
            int x0 = Mathf.FloorToInt((contact.x - reach) / Cell), x1 = Mathf.CeilToInt((contact.x + reach) / Cell);
            int z0 = Mathf.FloorToInt((contact.z - reach) / Cell), z1 = Mathf.CeilToInt((contact.z + reach) / Cell);
            float removed = 0f;
            berm.Clear();
            for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                float rx = ix * Cell - contact.x, rz = iz * Cell - contact.z;
                float a = rx * fwd.x + rz * fwd.z;
                float lat = Mathf.Abs(rx * side.x + rz * side.z);
                if (Mathf.Abs(a) > 0.2f) continue;
                if (lat <= halfW)
                {
                    float cur = GetD(ix, iz);
                    float nv = Mathf.Min(cur, Mathf.Max(-maxDepth, Mathf.Min(cur, -sink) - extra));   // never refill dug-out ground
                    if (nv < cur - 0.001f) { SetD(ix, iz, nv); removed += cur - nv; }
                }
                else if (lat <= reach) berm.Add(new Vector2Int(ix, iz));
            }
            if (removed > 0f && berm.Count > 0)
            {
                float add = removed * bermShare / berm.Count;
                foreach (var b in berm)
                {
                    float cur = GetD(b.x, b.y);
                    if (cur < maxBerm) SetD(b.x, b.y, Mathf.Min(maxBerm, cur + add));
                }
            }
        }

        // ------------------------------------------------------------------ streaming
        public void BuildAllNow(Vector3 around)
        {
            var fc = ChunkOf(around);
            foreach (var o in offsets)
            {
                if (o.magnitude * ChunkWorld > viewRadius + ChunkWorld) continue;
                var ch = Data(fc + o);
                if (!ch.go) BuildObject(ch);
            }
        }

        readonly List<Vector2Int> focusChunks = new List<Vector2Int>();

        void Update()
        {
            if (World == null || !focus) return;
            HealRuts(Time.deltaTime);
            focusChunks.Clear();
            focusChunks.Add(ChunkOf(focus.position));
            foreach (var f in extraFoci) if (f) focusChunks.Add(ChunkOf(f.position));
            var fc = focusChunks[0];
            int built = 0;
            Prefetch(fc);
            buildClock.Restart();
            foreach (var center in focusChunks)
            {
                foreach (var o in offsets)
                {
                    if (built >= buildsPerFrame || (built > 0 && buildClock.Elapsed.TotalMilliseconds > 4.0)) break;   // time budget per frame
                    if (o.magnitude * ChunkWorld > viewRadius + ChunkWorld) continue;
                    var ch = Data(center + o);
                    if (ch.go) continue;
                    BuildObject(ch);
                    built++;
                }
            }

            int R = Mathf.CeilToInt(viewRadius / ChunkWorld) + 2;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var ch = active[i];
                bool near = false;
                foreach (var c in focusChunks) if (Mathf.Max(Mathf.Abs(ch.c.x - c.x), Mathf.Abs(ch.c.y - c.y)) <= R) { near = true; break; }
                if (near) continue;
                Destroy(ch.go); Destroy(ch.mesh);
                ch.go = null; ch.mesh = null; ch.col = null;
                DropFlora(ch);
                active.RemoveAt(i);
            }

            UpdateCuring(Time.deltaTime);
            UpdateFlora();
            SiteBuilder.Prewarm(World, focus.position, viewRadius + 60f);
            int rebuilt = 0;
            foreach (var ch in active)
            {
                if (ch.waterGo) ch.waterGo.transform.localPosition = new Vector3(0f, Weather.LakeRise, 0f);
                if (rebuilt < rebuildsPerFrame && (ch.meshDirty || Mathf.Abs(ch.colorWet - Weather.Wetness) > 0.05f || Mathf.Abs(ch.colorSnow - Weather.Snow) > 0.06f))
                {
                    FillMesh(ch);
                    rebuilt++;
                }
                if (ch.colDirty && Time.time >= ch.colTime)
                {
                    ch.col.sharedMesh = null;
                    ch.col.sharedMesh = ch.mesh;
                    ch.colDirty = false;
                    ch.colTime = Time.time + colliderInterval;
                }
            }

            if (Time.time > cleanupTime)
            {
                cleanupTime = Time.time + 3f;
                drop.Clear();
                foreach (var kv in chunks)
                {
                    if (kv.Value.go || kv.Value.deformed || Time.time - kv.Value.lastUse < 20f) continue;   // still queried (far vehicles, props)
                    bool near = false;
                    foreach (var c in focusChunks) if (Mathf.Max(Mathf.Abs(kv.Key.x - c.x), Mathf.Abs(kv.Key.y - c.y)) <= R + 2) { near = true; break; }
                    if (!near) drop.Add(kv.Key);
                }
                foreach (var k in drop) chunks.Remove(k);
            }
        }

        void BuildObject(Chunk ch)
        {
            ch.go = new GameObject($"Chunk {ch.c.x},{ch.c.y}");
            ch.go.transform.SetParent(transform, false);
            ch.go.transform.position = new Vector3(ch.c.x * ChunkWorld, 0, ch.c.y * ChunkWorld);
            ch.go.isStatic = true;
            ch.go.layer = Layers.Terrain;
            ch.mesh = new Mesh { name = ch.go.name };
            ch.mesh.MarkDynamic();
            FillMesh(ch);
            ch.go.AddComponent<MeshFilter>().sharedMesh = ch.mesh;
            var mr = ch.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            // faceted terrain shadowing itself produced acne that blinked as the shadow cascades moved with the camera:
            // terrain only receives shadows (from props and vehicles)
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ch.col = ch.go.AddComponent<MeshCollider>();
            ch.col.sharedMesh = ch.mesh;
            ch.colDirty = false;
            active.Add(ch);
            BuildWater(ch);
            PropLibrary.Populate(this, ch.c, ch.go.transform, worldPropMaterial ? worldPropMaterial : propMaterial);
            SiteBuilder.Populate(this, ch.c, ch.go.transform, worldPropMaterial ? worldPropMaterial : propMaterial);
        }

        // ------------------------------------------------------------------ terraforming (excavators, pavers, rollers)
        public enum TerraOp : byte { Dig, Dump, Flatten, Pave, Roll }
        public const float MaxDig = 8f, MaxFill = 6f;

        /// <summary>Soil a dig produces here: biome topsoil, stone deeper down.</summary>
        public ResourceType SoilAt(float x, float z, float depthBelowBase)
        {
            // deep over an ore deposit, some scoops come up as ore (richer ground, more often)
            if (depthBelowBase > 0.6f && World != null)
            {
                float ore = World.OreAt(x, z, out var kind);
                if (ore > 0.3f && Hash(Mathf.FloorToInt(x * 3f), Mathf.FloorToInt(z * 3f) + Mathf.FloorToInt(depthBelowBase * 4f)) < ore * 0.6f) return kind;
            }
            if (depthBelowBase > 1.6f) return ResourceType.Stone;
            switch (BiomeAt(x, z))
            {
                case Biome.Desert: return ResourceType.Sand;
                case Biome.Tropical: return ResourceType.Laterite;
                case Biome.Nuclear:
                    // crater floors: yellow sulfur crusts in the slag, richer towards the hot cores
                    return depthBelowBase > 0.25f && Hash(Mathf.FloorToInt(x * 2f), Mathf.FloorToInt(z * 2f)) < 0.2f + (World != null ? World.Radiation(x, z) * 0.3f : 0f)
                        ? ResourceType.Sulfur : ResourceType.Slag;
                case Biome.Town: case Biome.City: return ResourceType.Rubble;
                default: return ResourceType.Clay;
            }
        }

        /// <summary>Base-relative deformation at a point (negative = dug).</summary>
        public float DugDepth(float x, float z) { var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k); return -ch.d[k]; }

        /// <summary>Apply a terraform operation (deterministic; replicated over the network). Returns m³ moved for dig/dump.</summary>
        public float ApplyTerraform(byte op, Vector3 p, float radius, float amount, byte extra)
        {
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell), x1 = Mathf.CeilToInt((p.x + radius) / Cell);
            int z0 = Mathf.FloorToInt((p.z - radius) / Cell), z1 = Mathf.CeilToInt((p.z + radius) / Cell);
            float moved = 0f;
            terraforming = true;
            for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                float dx = ix * Cell - p.x, dz = iz * Cell - p.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > radius) continue;
                float fall = 1f - dist / radius; fall = fall * fall * (3f - 2f * fall);
                var ch = Locate(ix, iz, out int k);
                float cur = ch.d[k];
                switch ((TerraOp)op)
                {
                    case TerraOp.Dig:
                    {
                        float nv = Mathf.Max(-MaxDig, cur - amount * fall);
                        if (nv < cur) { SetD(ix, iz, nv); moved += (cur - nv) * Cell * Cell; ClearPave(ix, iz); }
                        break;
                    }
                    case TerraOp.Dump:
                    {
                        float nv = Mathf.Min(MaxFill, cur + amount * fall);
                        if (nv > cur) { SetD(ix, iz, nv); moved += (nv - cur) * Cell * Cell; ClearPave(ix, iz); }
                        break;
                    }
                    case TerraOp.Flatten:
                    {
                        float target = amount - ch.h[k];                       // amount = world height of the blade
                        float nv = Mathf.Lerp(cur, Mathf.Clamp(target, -MaxDig, MaxFill), fall * 0.5f);
                        moved += (cur - nv) * Cell * Cell;
                        if (Mathf.Abs(nv - cur) > 0.001f) SetD(ix, iz, nv);
                        break;
                    }
                    case TerraOp.Pave:
                        if (ch.pave[k] == 0) { SetPave(ix, iz, extra, 0f, 0); SetD(ix, iz, cur + (Hash(ix, iz) - 0.5f) * 0.03f); moved += Cell * Cell; }
                        break;
                    case TerraOp.Roll:
                        if (ch.pave[k] > 0 && ch.cure[k] < 1f)
                        {
                            float avg = (GetD(ix - 1, iz) + GetD(ix + 1, iz) + GetD(ix, iz - 1) + GetD(ix, iz + 1)) * 0.25f;
                            SetD(ix, iz, Mathf.Lerp(cur, avg, 0.6f));
                            SetPave(ix, iz, ch.pave[k], ch.cure[k], 1);
                        }
                        break;
                }
            }
            terraforming = false;
            if ((TerraOp)op == TerraOp.Dig && moved > 0f) FloodAround(p, radius);   // a trench from a lake fills up
            return moved;
        }

        void ClearPave(int ix, int iz) { var ch = Locate(ix, iz, out int k); if (ch.pave[k] != 0) SetPave(ix, iz, 0, 0f, 0); }

        void SetPave(int ix, int iz, byte kind, float cure, byte compact)
        {
            int cx = FloorDiv(ix, N), cz = FloorDiv(iz, N);
            int li = ix - cx * N, lj = iz - cz * N;
            void P(int ccx, int ccz, int i, int j)
            {
                var ch = Data(new Vector2Int(ccx, ccz));
                int k = j * V + i;
                ch.pave[k] = kind; ch.cure[k] = cure; ch.compact[k] = compact;
                ch.terraformed = true; ch.deformed = true; ch.meshDirty = true;
                if (kind > 0 && !curing.Contains(ch)) curing.Add(ch);
            }
            P(cx, cz, li, lj);
            if (li == 0) P(cx - 1, cz, N, lj);
            if (lj == 0) P(cx, cz - 1, li, N);
            if (li == 0 && lj == 0) P(cx - 1, cz - 1, N, N);
        }

        readonly List<Chunk> curing = new List<Chunk>();
        float cureTick;

        /// <summary>Wet asphalt / concrete sets over time (faster when rolled).</summary>
        void UpdateCuring(float dt)
        {
            if ((cureTick += dt) < 2f) return;
            float step = cureTick; cureTick = 0f;
            for (int i = curing.Count - 1; i >= 0; i--)
            {
                var ch = curing[i];
                bool any = false;
                for (int k = 0; k < ch.pave.Length; k++)
                {
                    if (ch.pave[k] == 0 || ch.cure[k] >= 1f) continue;
                    float time = ch.pave[k] == 1 ? 90f : 180f;
                    ch.cure[k] = Mathf.Min(1f, ch.cure[k] + step / time * (ch.compact[k] > 0 ? 2f : 1f));
                    any = true;
                }
                ch.meshDirty = true;
                if (!any) curing.RemoveAt(i);
            }
        }

        // ---- persistence of terraformed chunks
        [System.Serializable]
        public class ChunkEdit { public int x, z; public string d, pave, cure, compact; }
        readonly Dictionary<Vector2Int, ChunkEdit> savedEdits = new Dictionary<Vector2Int, ChunkEdit>();

        public List<ChunkEdit> SaveEdits()
        {
            var list = new List<ChunkEdit>();
            foreach (var kv in chunks)
            {
                var ch = kv.Value;
                if (!ch.terraformed) continue;
                var db = new byte[ch.d.Length * 2]; var cb2 = new byte[ch.cure.Length];
                for (int i = 0; i < ch.d.Length; i++) { short v = (short)Mathf.Clamp(Mathf.RoundToInt(ch.d[i] * 1000f), short.MinValue, short.MaxValue); db[i * 2] = (byte)v; db[i * 2 + 1] = (byte)(v >> 8); cb2[i] = (byte)Mathf.RoundToInt(ch.cure[i] * 255f); }
                list.Add(new ChunkEdit { x = kv.Key.x, z = kv.Key.y, d = System.Convert.ToBase64String(db), pave = System.Convert.ToBase64String(ch.pave), cure = System.Convert.ToBase64String(cb2), compact = System.Convert.ToBase64String(ch.compact) });
            }
            foreach (var e in savedEdits.Values) list.Add(e);                  // never streamed in this session
            return list;
        }

        public void LoadEdits(List<ChunkEdit> edits)
        {
            savedEdits.Clear();
            if (edits == null) return;
            foreach (var e in edits)
            {
                var c = new Vector2Int(e.x, e.z);
                if (chunks.TryGetValue(c, out var ch)) ApplySaved(ch, e); else savedEdits[c] = e;
            }
        }

        void ApplySaved(Chunk ch, ChunkEdit e)
        {
            var db = System.Convert.FromBase64String(e.d);
            var pv = System.Convert.FromBase64String(e.pave); var cu = System.Convert.FromBase64String(e.cure); var cp = System.Convert.FromBase64String(e.compact);
            for (int i = 0; i < ch.d.Length && i * 2 + 1 < db.Length; i++)
            {
                ch.d[i] = (short)(db[i * 2] | (db[i * 2 + 1] << 8)) / 1000f;
                if (i < pv.Length) ch.pave[i] = pv[i];
                if (i < cu.Length) ch.cure[i] = cu[i] / 255f;
                if (i < cp.Length) ch.compact[i] = cp[i];
            }
            ch.terraformed = true; ch.deformed = true; ch.meshDirty = true; ch.colDirty = true;
            bool wet = false; foreach (var v in ch.cure) if (v < 1f) { wet = true; break; }
            if (wet && !curing.Contains(ch)) curing.Add(ch);
        }

        /// <summary>Biome at a world point (as sampled by the terrain).</summary>
        public Biome BiomeAt(float x, float z)
        {
            var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k);
            return (Biome)ch.biome[k];
        }

        /// <summary>Lake surface height at a point, NaN when dry.</summary>
        public float WaterLevel(float x, float z)
        {
            var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k);
            return ch.water[k] + Weather.LakeRise;
        }

        /// <summary>Depth of water above the ground at a point (0 when dry).</summary>
        public float WaterDepth(float x, float z)
        {
            float lvl = WaterLevel(x, z);
            return float.IsNaN(lvl) ? 0f : Mathf.Max(0f, lvl - Height(x, z));
        }

        static Material waterMat;

        void BuildWater(Chunk ch)
        {
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = j * V + i;
                float lvl = float.NaN;
                if (!float.IsNaN(ch.water[k])) lvl = ch.water[k];
                if (!float.IsNaN(ch.water[k + 1])) lvl = ch.water[k + 1];
                if (!float.IsNaN(ch.water[k + V])) lvl = ch.water[k + V];
                if (!float.IsNaN(ch.water[k + V + 1])) lvl = ch.water[k + V + 1];
                if (float.IsNaN(lvl)) continue;
                float low = Mathf.Min(Mathf.Min(ch.h[k] + ch.d[k], ch.h[k + 1] + ch.d[k + 1]), Mathf.Min(ch.h[k + V] + ch.d[k + V], ch.h[k + V + 1] + ch.d[k + V + 1]));
                if (low > lvl + Weather.MaxLakeRise) continue;
                float depth = Mathf.Max(0.05f, lvl - low);
                var b = (Biome)ch.biome[k];
                int gi = ch.c.x * N + i, gj = ch.c.y * N + j;
                float hs = Hash(gi + 11, gj + 5);
                Color32 deep = b == Biome.Nuclear ? C(0x1e3a0c) : b == Biome.Tropical ? C(0x0c3c46) : C(0x122a3a);
                Color32 shallow = b == Biome.Nuclear ? C(0x3e6a14) : b == Biome.Tropical ? C(0x1e6a6a) : C(0x284a5a);
                var col = Color32.Lerp(shallow, deep, Mathf.Round(Mathf.Clamp01(depth / 1.5f) * 3f) / 3f);
                if (hs > 0.97f) col = Color32.Lerp(col, C(0x9ab8c0), 0.4f);                  // glints
                col.a = (byte)(depth < 0.15f ? 170 : 225);
                int v = verts.Count;
                float x0 = i * Cell, z0 = j * Cell;
                verts.Add(new Vector3(x0, lvl, z0)); verts.Add(new Vector3(x0, lvl, z0 + Cell)); verts.Add(new Vector3(x0 + Cell, lvl, z0 + Cell)); verts.Add(new Vector3(x0 + Cell, lvl, z0));
                for (int t = 0; t < 4; t++) cols.Add(col);
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
            if (verts.Count == 0) return;
            if (!waterMat) waterMat = Fx.TransparentMaterial(null);
            var m = new Mesh { name = "Water " + ch.c };
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            ch.waterGo = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer));
            ch.waterGo.transform.SetParent(ch.go.transform, false);
            ch.waterGo.GetComponent<MeshFilter>().sharedMesh = m;
            var mr = ch.waterGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void FillMesh(Chunk ch)
        {
            int n = 0;
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = j * V + i;
                float x0 = i * Cell, z0 = j * Cell, x1 = x0 + Cell, z1 = z0 + Cell;
                var v00 = new Vector3(x0, ch.h[k] + ch.d[k], z0);
                var v10 = new Vector3(x1, ch.h[k + 1] + ch.d[k + 1], z0);
                var v01 = new Vector3(x0, ch.h[k + V] + ch.d[k + V], z1);
                var v11 = new Vector3(x1, ch.h[k + V + 1] + ch.d[k + V + 1], z1);
                var col = CellColor(ch, i, j);
                var na = Vector3.Cross(v01 - v00, v11 - v00).normalized;
                var nbv = Vector3.Cross(v11 - v00, v10 - v00).normalized;
                vb[n] = v00; vb[n + 1] = v01; vb[n + 2] = v11;
                vb[n + 3] = v00; vb[n + 4] = v11; vb[n + 5] = v10;
                for (int t = 0; t < 3; t++) { nb[n + t] = na; nb[n + 3 + t] = nbv; }
                for (int t = 0; t < 6; t++) cb[n + t] = col;
                n += 6;
            }
            ch.mesh.SetVertices(vb);
            ch.mesh.SetNormals(nb);
            ch.mesh.SetColors(cb);
            ch.mesh.SetTriangles(ib, 0);
            ch.mesh.RecalculateBounds();
            ch.meshDirty = false;
            ch.colorWet = Weather.Wetness;
            ch.colorSnow = Weather.Snow;
        }

        // ------------------------------------------------------------------ colours
        static readonly Color32[] Sand = { C(0x8a4a24), C(0xa65c2e), C(0xbb6c36), C(0xcf8044), C(0xe09a58) };
        static readonly Color32[] Asphalt = { C(0x2f2a28), C(0x3a3432), C(0x443d39), C(0x4e4540) };
        static readonly Color32[] Dirt = { C(0x6b3a1e), C(0x7d4524), C(0x8f522b) };
        static readonly Color32 Mud = C(0x3e2416), MudLight = C(0x52301d), Water = C(0x6e5a52), WaterHi = C(0xa08c80);
        static readonly Color32 Line = C(0xc49a44), Crack = C(0x221e1c), Tar = C(0x16130f), TarSheen = C(0x3a3842);
        static readonly Color32 SnowCol = C(0xe8e6ee), SnowShade = C(0xc4c6d8), IceCol = C(0x8aa0b8);
        static readonly Color32[] ForestG = { C(0x26301a), C(0x303c1e), C(0x3c4824), C(0x4a5428), C(0x5a6030) };
        static readonly Color32[] TropicG = { C(0x2a4e1e), C(0x346026), C(0x40742c), C(0x508834), C(0x649c3c) };
        static readonly Color32[] NukeG = { C(0x3e3c26), C(0x4c4a2c), C(0x5c5830), C(0x6c6636), C(0x7e763c) };
        static readonly Color32[] MeadowG = { C(0x44501e), C(0x505e24), C(0x5e6a2a), C(0x6e7832), C(0x80883a) };
        static readonly Color32[] Gravel = { C(0x5a4636), C(0x6a5440), C(0x7a624a), C(0x8a7056), C(0x9a7e62) };
        static readonly Color32[] Concrete = { C(0x4a4846), C(0x565452), C(0x625f5c), C(0x6e6b66), C(0x7a7670) };
        static readonly Color32 Litter = C(0x5a3a1e), Glow = C(0x9cff3a), Soil = C(0x4a2e1a), Crop = C(0x7a8a2a), Joint = C(0x34322f), LakeBed = C(0x2e2a22);
        static Color32 C(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static readonly Color32[] Sandstone = { C(0x6a3e24), C(0x7e4c2c), C(0x925a34), C(0xa66a3e), C(0xb87c4a) };
        static readonly Color32[] RockGrey = { C(0x34302c), C(0x403b36), C(0x4c4640), C(0x5a534b), C(0x686056) };

        /// <summary>Site ground: mesa rock (strata by height), bunker concrete, tunnel gravel.</summary>
        Color32 FeatureColor(Chunk ch, int k, byte feat, float hs, float gx, float gz)
        {
            float y = ch.h[k] + ch.d[k];
            switch (feat)
            {
                case 1:
                {
                    bool sand = (Biome)ch.biome[k] == Biome.Desert;
                    var ramp = sand ? Sandstone : RockGrey;
                    int band = Mathf.FloorToInt(y * 1.6f + Mathf.PerlinNoise(gx * 0.2f, gz * 0.2f) * 0.8f);
                    int b = Mathf.Clamp(1 + (band % 3 + 3) % 3 + (hs > 0.85f ? 1 : hs < 0.1f ? -1 : 0), 0, 4);
                    return ramp[b];
                }
                case 2:
                    if (Mathf.Repeat(gx, 2f) < Cell * 0.99f || Mathf.Repeat(gz, 2f) < Cell * 0.99f) return Joint;
                    return Concrete[hs < 0.1f ? 0 : hs < 0.75f ? 1 : 2];
                default:
                    return hs > 0.9f ? RockGrey[0] : Gravel[hs < 0.3f ? 0 : 1];
            }
        }

        static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) ^ (uint)(z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        Color32 CellColor(Chunk ch, int i, int j)
        {
            int k = j * V + i;
            int gi = ch.c.x * N + i, gj = ch.c.y * N + j;
            float road = (ch.road[k] + ch.road[k + 1] + ch.road[k + V] + ch.road[k + V + 1]) * 0.25f;
            float d = (ch.d[k] + ch.d[k + 1] + ch.d[k + V] + ch.d[k + V + 1]) * 0.25f;
            float dist = Mathf.Min(Mathf.Min(ch.dist[k], ch.dist[k + 1]), Mathf.Min(ch.dist[k + V], ch.dist[k + V + 1]));
            bool paved = ch.paved[k];
            float hs = Hash(gi, gj);
            float gx = gi * Cell, gz = gj * Cell;

            Color32 col;
            byte feat = ch.feature[k];
            if (feat != 0)
            {
                col = FeatureColor(ch, k, feat, hs, gx, gz);
                if (feat >= 2) return col;                        // under a roof: no puddles or snow
            }
            else if (road > hs * 0.6f + 0.3f)
            {
                if (paved)
                {
                    col = Asphalt[hs < 0.15f ? 0 : hs < 0.8f ? 1 : 2 + (hs > 0.95f ? 1 : 0)];
                    float cr = Mathf.PerlinNoise(gx * 0.9f + 17f, gz * 0.9f + 3f);
                    if (Mathf.Abs(cr - 0.5f) < 0.02f) col = Crack;
                    if (dist < 0.14f && Mathf.Repeat(ch.along[k], 6f) < 3f && Hash(gi + 7, gj) > 0.25f) col = Line;
                }
                else
                {
                    col = Dirt[hs < 0.2f ? 0 : hs < 0.85f ? 1 : 2];
                    if (dist > 0.5f && dist < 1.2f) col = Color32.Lerp(col, Mud, 0.35f); // worn wheel tracks
                }
            }
            else
            {
                float tone = Mathf.PerlinNoise(gx * 0.03f + 5f, gz * 0.03f + 9f) + (Mathf.PerlinNoise(gx * 0.4f, gz * 0.4f) - 0.5f) * 0.25f;
                int b = tone > 0.62f ? 3 : tone > 0.42f ? 2 : 1;
                int bi = Mathf.Clamp(b + (hs < 0.12f ? -1 : hs > 0.9f ? 1 : 0), 0, 4);
                switch ((Biome)ch.biome[k])
                {
                    case Biome.Forest: col = hs > 0.93f ? Litter : ForestG[bi]; break;
                    case Biome.Tropical: col = TropicG[bi]; break;
                    case Biome.Nuclear:
                        col = NukeG[bi];
                        if (hs > 0.988f) col = Glow;
                        else if (Mathf.Abs(Mathf.PerlinNoise(gx * 0.5f + 3f, gz * 0.5f) - 0.5f) < 0.025f) col = Crack;
                        break;
                    case Biome.Village:
                    {
                        col = MeadowG[bi];
                        float field = Mathf.PerlinNoise(gx * 0.02f + 40f, gz * 0.02f + 12f);
                        if (field > 0.55f) col = Mathf.Repeat(gx + (field > 0.62f ? gz : 0f), 1.5f) < 0.6f ? Soil : (hs > 0.3f ? Crop : Soil);
                        break;
                    }
                    case Biome.Town: col = hs > 0.55f ? Gravel[bi] : Sand[bi]; break;
                    case Biome.City:
                        col = Concrete[bi];
                        if (Mathf.Repeat(gx, 4f) < Cell * 0.99f || Mathf.Repeat(gz, 4f) < Cell * 0.99f) col = Joint;
                        else if (hs > 0.965f) col = Gravel[1];
                        break;
                    default: col = Sand[bi]; break;
                }
                // oil fields: tar seeps stain the ground in blotches, stronger over rich oil
                float oil = World != null ? World.OilAt(gx, gz) : 0f;
                if (oil > 0.25f)
                {
                    float seep = Mathf.PerlinNoise(gx * 0.35f + 71f, gz * 0.35f - 13f);
                    if (seep < oil * 0.75f - 0.05f) col = hs > 0.94f ? TarSheen : Color32.Lerp(col, Tar, 0.8f);
                    else if (seep < oil * 0.75f + 0.08f) col = Color32.Lerp(col, Tar, 0.35f);
                }
                float sh = ch.shore[k];
                if (sh > 0.45f && (Biome)ch.biome[k] != Biome.Nuclear) col = Sand[hs > 0.5f ? 4 : 3];
                if (!float.IsNaN(ch.water[k]) && ch.h[k] + ch.d[k] < ch.water[k] - 0.05f) col = Color32.Lerp(LakeBed, col, 0.25f);
            }

            if (ch.pave[k] > 0)
            {
                float set = ch.cure[k];
                bool asphalt = ch.pave[k] == 1;
                var baseC = asphalt ? Asphalt[hs < 0.2f ? 0 : hs < 0.85f ? 1 : 2] : Concrete[hs < 0.3f ? 1 : hs < 0.9f ? 2 : 3];
                if (ch.compact[k] == 0 && set >= 1f && hs > 0.8f) baseC = asphalt ? Asphalt[3] : Concrete[0];   // rough, unrolled finish
                col = set < 1f ? Color32.Lerp(asphalt ? C(0x1a1614) : Concrete[0], baseC, set * 0.7f) : baseC;
                if (Weather.Snow > 0.3f && set >= 1f) col = Color32.Lerp(col, SnowCol, Mathf.Round(Weather.Snow * 2f) / 3f);
                return col;
            }
            var s = MakeSurface(ch, k);
            float wq = Mathf.Round(s.wet * 4f) / 4f;
            if (paved && road > 0.5f) col = Color32.Lerp(col, Crack, wq * 0.35f);
            else col = Color32.Lerp(col, hs > 0.5f ? Mud : MudLight, wq * 0.7f);
            if (s.wet > 0.85f && (d < -0.03f || ch.wet[k] > 0.8f) && Weather.Wetness > 0.25f && road < 0.5f)
                col = hs > 0.88f ? WaterHi : Water;
            if (d < -0.015f) col = Color32.Lerp(col, Mud, Mathf.Clamp(-d * 3f, 0.12f, 0.45f));
            else if (d > 0.015f) col = Color32.Lerp(col, Sand[4], 0.12f);
            // snow cover: patchy at first, thinner on roads, ruts cut through to the ground
            float snow = Weather.Snow;
            if (snow > 0.02f)
            {
                float patch = Mathf.PerlinNoise(gx * 0.25f + 31f, gz * 0.25f + 11f) * 0.6f + hs * 0.4f;
                float cover = Mathf.Clamp01((snow * 1.6f - patch) * 3f) * (1f - road * 0.45f) * (d < -0.02f ? 0.25f : 1f);
                cover = Mathf.Round(cover * 3f) / 3f;
                if (Weather.Ice > 0.4f && paved && road > 0.5f) col = Color32.Lerp(col, IceCol, 0.4f);
                col = Color32.Lerp(col, hs > 0.93f ? SnowShade : SnowCol, cover);
            }
            return col;
        }
    }
}
