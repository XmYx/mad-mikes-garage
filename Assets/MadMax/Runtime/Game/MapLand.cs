using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The HD land layer under the minimap and the map page: world-aligned tiles of <see cref="T"/>+1 texels at six
    /// levels (0.25 m … 8 m per texel), each sampled from <see cref="WorldGen.Sample"/> on a worker (smooth biome colours,
    /// hillshade from the NW, water by depth with a shoreline, contour lines, sites and town-plan ground, building
    /// footprints from the plan's spots). A <see cref="View"/> picks the level for its scale, requests what it lacks
    /// (nearest first, a coarse ancestor ahead of the rest) and falls back to the nearest ready ancestor, else the caller's
    /// base map. Built tiles are cached (LRU, arrays pooled); nothing here touches Unity objects off the main thread.</summary>
    public sealed class MapLand
    {
        public const int T = 128, N = T + 1, Levels = 6;
        /// <summary>Texel size of level 0 (m); level L is 2^L times that.</summary>
        public const float Texel0 = 0.25f;
        /// <summary>From this many metres per physical pixel on the base map does it alone (whole-world views).</summary>
        public const float CoarseFrom = 12f;
        const int Cap = 480, MaxViewTiles = 400, PoolMax = 48;

        public sealed class Tile
        {
            public int level, tx, tz, used;
            public float ox, oz, texel, prio;
            public Color32[] px;
            public volatile bool ready, running, failed;
            public bool queued;
        }

        /// <summary>One consumer's lookup (the minimap, the map page), prepared each frame by <see cref="Prepare"/>.</summary>
        public sealed class View
        {
            public bool coarse = true;
            public int level = -1, tx0, tz0, nx, nz;
            public float tileSize;
            public Tile[] src = new Tile[0];
        }

        readonly WorldGen world;
        readonly Dictionary<long, Tile> tiles = new Dictionary<long, Tile>();
        readonly List<Tile> queue = new List<Tile>();
        readonly Stack<Color32[]> pool = new Stack<Color32[]>();
        readonly List<Tile> evict = new List<Tile>();
        readonly Foot[] feet;
        readonly Vector3[] footTowns;                                                             // x, z, reach per settlement with footprints
        readonly int[] footStart;
        int running, finished, pumped = -1;
        volatile bool dead;

        /// <summary>Tiles finished so far (the map page recomposes when this moves).</summary>
        public int Finished => Volatile.Read(ref finished);
        public int Running => Volatile.Read(ref running);
        public int Cached => tiles.Count;
        public int Queued => queue.Count;

        public MapLand(WorldGen world)
        {
            this.world = world;
            var list = new List<Foot>();
            var towns = new List<Vector3>();
            var starts = new List<int>();
            foreach (var st in world.settlements)
            {
                if (st.plan == null) continue;
                int first = list.Count;
                foreach (var sp in st.plan.spots)
                {
                    if (sp.kind != SpotKind.Prop || sp.id == null || !FootSize(sp.id, out float hx, out float hz, out var roof, out float tall)) continue;
                    float a = sp.yaw * Mathf.Deg2Rad;
                    var az = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    list.Add(new Foot { c = sp.pos, az = az, ax = new Vector2(az.y, -az.x), hx = hx, hz = hz, roof = roof, shadow = tall, r = Mathf.Sqrt(hx * hx + hz * hz) + tall + 1f });
                }
                if (list.Count == first) continue;
                towns.Add(new Vector3(st.plan.centre.x, st.plan.centre.y, st.plan.Outer + 20f));
                starts.Add(first);
            }
            starts.Add(list.Count);
            feet = list.ToArray(); footTowns = towns.ToArray(); footStart = starts.ToArray();
        }

        /// <summary>Stops queued and running builds (the HUD is going away).</summary>
        public void Dispose() { dead = true; queue.Clear(); }

        static long Key(int level, int tx, int tz) => ((long)level << 48) | ((long)(tx & 0xFFFFFF) << 24) | (long)(tz & 0xFFFFFF);

        Tile Peek(int level, int tx, int tz) => tiles.TryGetValue(Key(level, tx, tz), out var t) ? t : null;

        Tile Get(int level, int tx, int tz)
        {
            long k = Key(level, tx, tz);
            if (tiles.TryGetValue(k, out var t)) return t;
            float texel = Texel0 * (1 << level);
            t = new Tile { level = level, tx = tx, tz = tz, texel = texel, ox = tx * T * texel, oz = tz * T * texel };
            tiles[k] = t;
            return t;
        }

        void Want(Tile t, float prio)
        {
            if (t.failed || t.running || t.ready) return;
            t.prio = prio;
            if (!t.queued) { t.queued = true; queue.Add(t); }
        }

        /// <summary>The level whose texel is at most <paramref name="fm"/> metres (one texel per pixel or finer).</summary>
        public static int LevelFor(float fm) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(fm, Texel0) / Texel0, 2f) + 1e-3f), 0, Levels - 1);

        /// <summary>Sets <paramref name="v"/> up for a view covering world x/z <paramref name="x0"/>..<paramref name="x1"/>,
        /// <paramref name="z0"/>..<paramref name="z1"/> at <paramref name="fm"/> metres per physical pixel: requests the
        /// missing tiles (nearest <paramref name="focus"/> first) and starts up to <paramref name="workers"/> builds.</summary>
        public void Prepare(View v, float x0, float z0, float x1, float z1, float fm, Vector2 focus, int workers)
        {
            Pump(workers);
            v.coarse = dead || fm >= CoarseFrom;
            if (v.coarse) return;
            int L = LevelFor(fm);
            float size = T * Texel0 * (1 << L);
            int tx0 = Mathf.FloorToInt(x0 / size), tx1 = Mathf.FloorToInt(x1 / size), tz0 = Mathf.FloorToInt(z0 / size), tz1 = Mathf.FloorToInt(z1 / size);
            int nx = tx1 - tx0 + 1, nz = tz1 - tz0 + 1;
            if (nx * nz > MaxViewTiles) { v.coarse = true; return; }
            if (v.src.Length < nx * nz) v.src = new Tile[nx * nz];
            v.level = L; v.tileSize = size; v.tx0 = tx0; v.tz0 = tz0; v.nx = nx; v.nz = nz;
            int frame = Time.frameCount;
            int up = Mathf.Min(L + 2, Levels - 1);                                                // the quick cover while zooming in
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                int tx = tx0 + ix, tz = tz0 + iz;
                var t = Get(L, tx, tz);
                t.used = frame;
                Tile s = null;
                if (t.ready) s = t;
                else
                {
                    float d = Vector2.Distance(focus, new Vector2((tx + 0.5f) * size, (tz + 0.5f) * size)) / size;
                    Want(t, d);
                    for (int k = 1; L + k < Levels && k <= 4; k++)
                    {
                        var a = Peek(L + k, tx >> k, tz >> k);
                        if (a != null && a.ready) { a.used = frame; s = a; break; }
                    }
                    if (s == null && up > L)
                    {
                        var a = Get(up, tx >> (up - L), tz >> (up - L));
                        a.used = frame;
                        Want(a, d / (1 << (up - L)) - 1000f);
                    }
                }
                v.src[iz * nx + ix] = s;
            }
        }

        /// <summary>Starts builds (best priority first, only what a view asked for lately), drops stale requests and
        /// evicts the least recently used tiles beyond the cache size. Once per frame.</summary>
        void Pump(int workers)
        {
            int frame = Time.frameCount;
            if (pumped == frame) return;
            pumped = frame;
            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i].used < frame - 30 || queue[i].ready) { queue[i].queued = false; queue[i] = queue[queue.Count - 1]; queue.RemoveAt(queue.Count - 1); }
            while (!dead && Running < workers && queue.Count > 0)
            {
                int best = -1;
                for (int i = 0; i < queue.Count; i++)
                    if (queue[i].used >= frame - 1 && (best < 0 || queue[i].prio < queue[best].prio)) best = i;
                if (best < 0) break;
                var t = queue[best];
                queue[best] = queue[queue.Count - 1]; queue.RemoveAt(queue.Count - 1);
                t.queued = false; t.running = true;
                if (t.px == null) t.px = pool.Count > 0 ? pool.Pop() : new Color32[N * N];
                Interlocked.Increment(ref running);
                Task.Run(() => Run(t));
            }
            if (tiles.Count <= Cap) return;
            evict.Clear();
            foreach (var t in tiles.Values) if (!t.running && !t.queued && t.used < frame - 2) evict.Add(t);
            evict.Sort((a, b) => a.used.CompareTo(b.used));
            for (int i = 0; i < evict.Count && tiles.Count > Cap * 3 / 4; i++)
            {
                var t = evict[i];
                tiles.Remove(Key(t.level, t.tx, t.tz));
                if (t.px != null && pool.Count < PoolMax) pool.Push(t.px);
                t.px = null; t.ready = false;
            }
        }

        void Run(Tile t)
        {
            try { if (!dead) { Build(t); t.ready = !dead; } }
            catch (System.Exception e) { t.failed = true; Debug.LogException(e); }
            finally { t.running = false; Interlocked.Decrement(ref running); Interlocked.Increment(ref finished); }
        }

        /// <summary>The land colour at a world point from the view's tiles (bilinear when <paramref name="smooth"/>);
        /// <paramref name="fine"/> false = not covered yet (use the base map). Thread-safe while no Prepare runs.</summary>
        public Color32 At(View v, float wx, float wz, bool smooth, out bool fine)
        {
            fine = false;
            if (v.coarse) return default;
            int ix = Mathf.FloorToInt(wx / v.tileSize) - v.tx0, iz = Mathf.FloorToInt(wz / v.tileSize) - v.tz0;
            if (ix < 0 || iz < 0 || ix >= v.nx || iz >= v.nz) return default;
            var t = v.src[iz * v.nx + ix];
            var p = t != null ? t.px : null;
            if (p == null) return default;
            fine = true;
            float gx = (wx - t.ox) / t.texel, gz = (wz - t.oz) / t.texel;
            if (!smooth)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(gx), 0, T), j = Mathf.Clamp(Mathf.RoundToInt(gz), 0, T);
                return p[j * N + i];
            }
            int i0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, T - 1), j0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, T - 1);
            float fx = Mathf.Clamp01(gx - i0), fz = Mathf.Clamp01(gz - j0);
            int k = j0 * N + i0;
            Color32 a = p[k], b = p[k + 1], c = p[k + N], d = p[k + N + 1];
            float w00 = (1f - fx) * (1f - fz), w10 = fx * (1f - fz), w01 = (1f - fx) * fz, w11 = fx * fz;
            return new Color32((byte)(a.r * w00 + b.r * w10 + c.r * w01 + d.r * w11 + 0.5f), (byte)(a.g * w00 + b.g * w10 + c.g * w01 + d.g * w11 + 0.5f),
                (byte)(a.b * w00 + b.b * w10 + c.b * w01 + d.b * w11 + 0.5f), 255);
        }

        // ------------------------------------------------------------------ building a tile (worker thread)
        const byte KLand = 0, KRough = 1, KPaved = 2, KWater = 3, KOut = 4;
        static readonly float[] Relief = { 1f, 1.3f, 1.8f, 2.5f, 3.2f, 4f };                       // hillshade exaggeration per level
        static readonly float[] Contour = { 1f, 1f, 2f, 5f, 10f, 10f };                             // contour interval (m) per level

        // map palette (HUD constants, like the minimap's): earthy land, cool water, pale streets
        static readonly Color32 OutsideCol = new Color32(22, 13, 9, 255),
            DesertCol = new Color32(178, 116, 70, 255), ForestCol = new Color32(66, 84, 46, 255), TropicCol = new Color32(60, 104, 48, 255),
            TundraCol = new Color32(118, 120, 106, 255), NukeCol = new Color32(104, 98, 54, 255), MeadowCol = new Color32(102, 112, 56, 255),
            VillageCol = new Color32(118, 116, 70, 255), TownCol = new Color32(150, 124, 96, 255), CityCol = new Color32(112, 108, 102, 255),
            MudCol = new Color32(84, 54, 36, 255), ShoreSand = new Color32(206, 172, 122, 255), SnowCol = new Color32(222, 226, 232, 255),
            LakeShallow = new Color32(92, 142, 150, 255), LakeDeep = new Color32(46, 88, 112, 255), ToxicShallow = new Color32(128, 148, 74, 255),
            ToxicDeep = new Color32(84, 108, 52, 255), SeaShallow = new Color32(60, 106, 120, 255), SeaDeep = new Color32(18, 38, 70, 255),
            ShoreLine = new Color32(26, 46, 58, 255),
            RockCol = new Color32(116, 96, 80, 255), BunkerFloor = new Color32(124, 122, 116, 255), TunnelGravel = new Color32(104, 92, 78, 255),
            Runway = new Color32(64, 62, 60, 255), RunwayPaint = new Color32(214, 208, 192, 255), PackedGravel = new Color32(142, 124, 100, 255),
            Sidewalk = new Color32(150, 144, 134, 255), Plaza = new Color32(162, 140, 112, 255), Park = new Color32(82, 118, 56, 255),
            YardLawn = new Color32(98, 116, 62, 255), CarPark = new Color32(132, 128, 122, 255), Island = new Color32(90, 128, 60, 255),
            Street = new Color32(184, 176, 160, 255), PaintW = new Color32(240, 236, 224, 255), Field = new Color32(134, 124, 60, 255),
            FieldRow = new Color32(104, 86, 50, 255), BareLot = new Color32(134, 110, 84, 255), KerbCol = new Color32(196, 192, 182, 255),
            PaintY = new Color32(226, 186, 70, 255);

        [System.ThreadStatic] static float[] hBuf;
        [System.ThreadStatic] static Color32[] cBuf;
        [System.ThreadStatic] static byte[] kBuf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { hBuf = null; cBuf = null; kBuf = null; }

        void Build(Tile t)
        {
            const int G = N + 2;
            var h = hBuf ??= new float[G * G];
            var col = cBuf ??= new Color32[G * G];
            var kind = kBuf ??= new byte[G * G];
            int L = t.level;
            float m = t.texel, zn = WorldGen.ZNorth, zs = WorldGen.ZSouth;
            for (int j = 0; j < G; j++)
            {
                if (dead) return;
                float z = t.oz + (j - 1) * m;
                for (int i = 0; i < G; i++)
                {
                    int k = j * G + i;
                    float x = t.ox + (i - 1) * m;
                    if (z > zn || z < zs) { h[k] = 0f; col[k] = OutsideCol; kind[k] = KOut; continue; }
                    float wx = WorldGen.WrapX(x);
                    var s = world.Sample(wx, z);
                    h[k] = s.height;
                    col[k] = Ground(in s, wx, z, L, out kind[k]);
                }
            }
            var px = t.px;
            float relief = Relief[L], step = Contour[L], inv2m = 1f / (2f * m);
            const float lx = -0.5f, lz = 0.5f, ly = 0.7071f;                                     // sun from the north-west, 45° up
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = (j + 1) * G + i + 1;
                var c = col[k];
                byte kd = kind[k];
                if (kd == KWater)
                {
                    bool edge = IsDry(kind[k - 1]) || IsDry(kind[k + 1]) || IsDry(kind[k - G]) || IsDry(kind[k + G]);
                    if (edge) c = Mix(c, ShoreLine, 0.55f);
                }
                else if (kd != KOut)
                {
                    float dx = (h[k + 1] - h[k - 1]) * inv2m * relief, dz = (h[k + G] - h[k - G]) * inv2m * relief;
                    float shade = (-dx * lx - dz * lz + ly) / Mathf.Sqrt(dx * dx + dz * dz + 1f);
                    float f = Mathf.Clamp(1f + (shade - ly) * (kd == KPaved ? 0.55f : 1.15f), 0.5f, 1.4f);
                    if (kd <= KRough && step > 0f)
                    {
                        int band = Mathf.FloorToInt(h[k] / step);
                        int bx = Mathf.FloorToInt(h[k + 1] / step), bz = Mathf.FloorToInt(h[k + G] / step);
                        if ((band != bx && kind[k + 1] <= KRough) || (band != bz && kind[k + G] <= KRough))
                        {
                            int line = Mathf.Max(band, Mathf.Max(bx, bz));
                            f *= line % 5 == 0 ? 0.78f : 0.88f;                                     // every fifth line stronger
                        }
                    }
                    c = new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), 255);
                    if (kind[k - 1] == KWater || kind[k + 1] == KWater || kind[k - G] == KWater || kind[k + G] == KWater)
                        c = Mix(c, ShoreSand, 0.3f);                                                // a pale beach line at the water
                }
                px[j * N + i] = c;
            }
            if (L <= 3 && feet.Length > 0) Footprints(t, px);
        }

        static bool IsDry(byte k) => k != KWater && k != KOut;

        static Color32 Mix(Color32 a, Color32 b, float t) => new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);

        static Color32 Scale(Color32 a, float f) => new Color32((byte)Mathf.Min(255f, a.r * f), (byte)Mathf.Min(255f, a.g * f), (byte)Mathf.Min(255f, a.b * f), 255);

        /// <summary>The flat map colour of one sample (before relief): water by depth, site and town ground, else the
        /// biome blended over ~30 m with wet ground, beaches and polar snow.</summary>
        Color32 Ground(in GroundSample s, float x, float z, int level, out byte kind)
        {
            if (!float.IsNaN(s.water) && s.height < s.water - 0.03f)
            {
                kind = KWater;
                float depth = s.water - s.height;
                if (Mathf.Abs(s.water - WorldGen.SeaLevel) < 0.01f) return Mix(SeaShallow, SeaDeep, Mathf.Clamp01(depth / 40f));
                var lake = world.LakeAt(x, z, out _);
                bool toxic = lake != null && lake.toxic;
                return Mix(toxic ? ToxicShallow : LakeShallow, toxic ? ToxicDeep : LakeDeep, Mathf.Clamp01(depth / 2.2f));
            }
            Color32 c;
            kind = KPaved;
            bool fine = level <= 1;
            switch (s.feature)
            {
                case 1: c = RockCol; kind = KRough; break;
                case 2: c = BunkerFloor; break;
                case 3: c = TunnelGravel; break;
                case 4: c = Runway; break;
                case 5: c = fine ? RunwayPaint : Runway; break;
                case 6: c = PackedGravel; kind = KRough; break;
                case TownPlan.FSidewalk: c = Sidewalk; break;
                case TownPlan.FPlaza: c = Plaza; break;
                case TownPlan.FPark: c = Park; kind = KRough; break;
                case TownPlan.FYard: c = YardLawn; kind = KRough; break;
                case TownPlan.FParking: c = CarPark; break;
                case TownPlan.FIsland: c = Island; break;
                case TownPlan.FStreet: c = Street; break;
                case TownPlan.FPaint: c = fine ? PaintW : Street; break;
                case TownPlan.FField: c = level <= 2 && Mathf.Repeat(x + z * 0.35f, 2f) < 0.7f ? FieldRow : Field; kind = KRough; break;
                case TownPlan.FLot: c = BareLot; kind = KRough; break;
                case TownPlan.FKerb: c = fine ? KerbCol : Sidewalk; break;
                case TownPlan.FYellow: c = fine ? PaintY : Street; break;
                default:
                {
                    kind = KLand;
                    c = s.biome == Biome.Village ? VillageCol : s.biome == Biome.Town ? TownCol : s.biome == Biome.City ? CityCol : Natural(x, z);
                    float tone = 0.95f + 0.1f * Mathf.PerlinNoise(x * 0.006f + 31f, z * 0.006f + 17f)
                                 + Mathf.Clamp(s.height, -10f, 40f) * 0.0025f;                       // a touch lighter up high
                    c = Scale(c, tone);
                    if (s.baseWet > 0.25f) c = Mix(c, MudCol, Mathf.Clamp01((s.baseWet - 0.25f) * 0.6f));
                    if (s.shore > 0.4f) c = Mix(c, ShoreSand, Mathf.Clamp01((s.shore - 0.4f) / 0.6f) * 0.8f);
                    break;
                }
            }
            float lat = Mathf.Abs(WorldGen.Latitude(z));
            if (lat > 64f) c = Mix(c, SnowCol, Mathf.Clamp01((lat - 64f) / 8f) * (kind == KPaved ? 0.5f : 1f));
            return c;
        }

        static Color32 BiomeCol(Biome b)
        {
            switch (b)
            {
                case Biome.Forest: return ForestCol;
                case Biome.Tropical: return TropicCol;
                case Biome.Tundra: return TundraCol;
                case Biome.Nuclear: return NukeCol;
                case Biome.Village: return MeadowCol;
                case Biome.Town: return TownCol;
                case Biome.City: return CityCol;
                default: return DesertCol;
            }
        }

        /// <summary>The wilderness colour averaged over a ring (smooth borders instead of the terrain's dithered seam).</summary>
        Color32 Natural(float x, float z)
        {
            const float R = 15f;
            float r = 0f, g = 0f, b = 0f;
            for (int k = 0; k < 7; k++)
            {
                float px = x, pz = z;
                if (k > 0) { float a = k * (Mathf.PI / 3f) + 0.3f; px += Mathf.Cos(a) * R; pz += Mathf.Sin(a) * R; }
                var c = BiomeCol(world.NaturalBiome(WorldGen.WrapX(px), pz));
                r += c.r; g += c.g; b += c.b;
            }
            return new Color32((byte)(r / 7f), (byte)(g / 7f), (byte)(b / 7f), 255);
        }

        // ------------------------------------------------------------------ building footprints (town plans)
        struct Foot { public Vector2 c, ax, az; public float hx, hz, r, shadow; public Color32 roof; }

        /// <summary>Half footprint (template half extents × voxel size), roof colour and shadow length of the plan's
        /// buildings (BiomeProps / PropLibrary templates).</summary>
        static bool FootSize(string id, out float hx, out float hz, out Color32 roof, out float tall)
        {
            hx = hz = tall = 0f; roof = default;
            if (id.StartsWith("BrickHouse")) { hx = id == "BrickHouse1" ? 4.32f : 3.84f; hz = 3.2f; roof = new Color32(124, 74, 60, 255); tall = 1.1f; }
            else if (id.StartsWith("Shop")) { hx = 6f; hz = 4.4f; roof = new Color32(112, 108, 102, 255); tall = 1f; }
            else if (id.StartsWith("Tower")) { hx = id == "Tower1" || id == "Tower3" ? 6.4f : 5.2f; hz = 4.4f; roof = new Color32(96, 94, 92, 255); tall = 2.4f; }
            else if (id.StartsWith("Warehouse")) { hx = 6.4f; hz = 4.6f; roof = new Color32(120, 104, 86, 255); tall = 1.3f; }
            else if (id.StartsWith("Farmhouse")) { hx = id == "Farmhouse1" ? 3.84f : 3.2f; hz = 2.56f; roof = new Color32(130, 92, 60, 255); tall = 1f; }
            else if (id.StartsWith("Shack")) { int v = id.Length > 5 ? id[5] - '0' : 0; v = Mathf.Clamp(v, 0, 2); hx = (16 + v * 3) * 0.08f; hz = (18 + v * 2) * 0.08f; roof = new Color32(126, 98, 72, 255); tall = 0.6f; }
            else if (id.StartsWith("Canopy")) { hx = 4.8f; hz = 3.2f; roof = new Color32(192, 182, 152, 255); tall = 0.8f; }
            else return false;
            return true;
        }

        /// <summary>Roofs (dark rim at fine levels) and their shadows to the south-east into a finished tile.</summary>
        void Footprints(Tile t, Color32[] px)
        {
            float m = t.texel, x0 = t.ox, z0 = t.oz, x1 = x0 + T * m, z1 = z0 + T * m;
            bool rim = t.level <= 2, shade = t.level <= 2;
            for (int ti = 0; ti < footTowns.Length; ti++)
            {
                var tw = footTowns[ti];
                if (tw.x + tw.z < x0 || tw.x - tw.z > x1 || tw.y + tw.z < z0 || tw.y - tw.z > z1) continue;
                for (int pass = shade ? 0 : 1; pass < 2; pass++)
                for (int fi = footStart[ti]; fi < footStart[ti + 1]; fi++)
                {
                    var f = feet[fi];
                    if (f.c.x + f.r < x0 || f.c.x - f.r > x1 || f.c.y + f.r < z0 || f.c.y - f.r > z1) continue;
                    var sh = pass == 0 ? new Vector2(f.shadow, -f.shadow) * 0.7f : Vector2.zero;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((f.c.x - f.r - x0) / m)), i1 = Mathf.Min(T, Mathf.CeilToInt((f.c.x + f.r - x0) / m));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((f.c.y - f.r - z0) / m)), j1 = Mathf.Min(T, Mathf.CeilToInt((f.c.y + f.r - z0) / m));
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        var d = new Vector2(x0 + i * m, z0 + j * m) - f.c - sh;
                        float u = Mathf.Abs(Vector2.Dot(d, f.ax)), v = Mathf.Abs(Vector2.Dot(d, f.az));
                        if (u > f.hx || v > f.hz) continue;
                        int k = j * N + i;
                        if (pass == 0) { px[k] = Scale(px[k], 0.72f); continue; }
                        float lit = (Vector2.Dot(d, f.az) > 0f ? 1f : -1f) * (f.az.y - f.az.x) * 0.7071f;   // the slope facing the sun lighter
                        px[k] = rim && (f.hx - u < m * 0.9f || f.hz - v < m * 0.9f) ? Scale(f.roof, 0.62f) : Scale(f.roof, 1f + 0.09f * lit);
                    }
                }
            }
        }
    }
}
