using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Ground cover: grass tufts, flowers, shrubs, ferns, mushrooms, crop rows, reeds, desert scrub and
    /// mutant pods, one combined mesh per terrain chunk near the focus (6 cm voxels, no outline, no shadows).
    /// Tyres, ruts and digging flatten a cell (<see cref="Trample"/>); it grows back through sprout / short / full
    /// stages over game days at a biome rate. Snow buries the short growth, frost browns it, rain makes the desert bloom.
    /// Vertex alpha carries the wind bend weight (PixelVoxel <c>_SwayTip</c>).</summary>
    public partial class DeformableTerrain
    {
        public Material floraMaterial, overgrowthMaterial;
        public float floraRadius = 36f;
        public int floraBuildsPerFrame = 1;
        const float FV = 0.06f;                  // flora voxel (m)
        const float Never = -1e6f;
        const int MaxBoxes = 1600;

        static readonly List<Vector3> fv = new List<Vector3>(), fn = new List<Vector3>();
        static readonly List<Color32> fc = new List<Color32>();
        static readonly List<int> fi = new List<int>();
        static readonly List<Rect> blockers = new List<Rect>();
        int boxes;

        void InitFlora(Material propMat)
        {
            floraMaterial = new Material(propMat) { name = "GroundFlora" };
            floraMaterial.SetFloat("_OutlinePx", 0f);
            floraMaterial.SetFloat("_SwayTip", 0.045f);
            floraMaterial.SetFloat("_WorldCut", 1f);
            floraMaterial.SetFloat("_SnowMask", 0f);
            floraMaterial.SetShaderPassEnabled("SRPDefaultUnlit", false);
            overgrowthMaterial = new Material(floraMaterial) { name = "Overgrowth" };
            overgrowthMaterial.SetFloat("_SwayTip", 0.03f);
            overgrowthMaterial.SetFloat("_SnowMask", 1f);
        }

        /// <summary>Flatten the ground cover in a disc (tyres, feet of heavy machines).</summary>
        public void Trample(Vector3 p, float radius)
        {
            if (World == null) return;
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell), x1 = Mathf.FloorToInt((p.x + radius) / Cell);
            int z0 = Mathf.FloorToInt((p.z - radius) / Cell), z1 = Mathf.FloorToInt((p.z + radius) / Cell);
            for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                var ch = Locate(ix, iz, out int k);
                MarkTrampled(ch, k);
            }
        }

        void MarkTrampled(Chunk ch, int k)
        {
            if (ch.trampled == null)
            {
                ch.trampled = new float[V * V];
                for (int i = 0; i < ch.trampled.Length; i++) ch.trampled[i] = Never;
            }
            float now = DayNight.TotalDays;
            if (now - ch.trampled[k] < 0.02f) return;          // already flat
            ch.trampled[k] = now;
            ch.floraDirty = true;
        }

        /// <summary>Rebuild the ground cover of chunks overlapping a world rect (a building appeared or went away).</summary>
        public void FloraDirty(Rect r)
        {
            int cx0 = Mathf.FloorToInt(r.xMin / ChunkWorld), cx1 = Mathf.FloorToInt(r.xMax / ChunkWorld);
            int cz0 = Mathf.FloorToInt(r.yMin / ChunkWorld), cz1 = Mathf.FloorToInt(r.yMax / ChunkWorld);
            for (int x = cx0; x <= cx1; x++)
            for (int z = cz0; z <= cz1; z++)
                if (chunks.TryGetValue(new Vector2Int(x, z), out var ch) && ch.floraGo) { ch.floraDirty = true; ch.floraNext = 0f; }
        }

        void DropFlora(Chunk ch)
        {
            if (ch.floraGo) Destroy(ch.floraGo);
            if (ch.floraMesh) Destroy(ch.floraMesh);
            ch.floraGo = null; ch.floraMesh = null; ch.floraKey = -1;
        }

        static int FloraKey(float z)
        {
            int snow = Mathf.RoundToInt(Weather.SnowAt(z) * 3f);                                  // latitude snow (planet)
            bool frost = Weather.TemperatureAt(z) < 1f;
            bool bloom = Weather.Wetness > 0.35f;
            return snow | (frost ? 4 : 0) | (bloom ? 8 : 0);
        }

        void UpdateFlora()
        {
            if (!floraMaterial || focusChunks.Count == 0) return;
            float r = Mathf.Min(floraRadius, viewRadius);
            int reach = Mathf.CeilToInt(r / ChunkWorld) + 1;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var ch = active[i];
                if (!ch.floraGo) continue;
                bool near = false;
                foreach (var c in focusChunks) if (Mathf.Max(Mathf.Abs(ch.c.x - c.x), Mathf.Abs(ch.c.y - c.y)) <= reach + 1) { near = true; break; }
                if (!near) DropFlora(ch);
            }
            int built = 0;
            float now = Time.time;
            var fc0 = focusChunks[0];
            foreach (var o in offsets)
            {
                if (built >= floraBuildsPerFrame) break;
                if (o.magnitude * ChunkWorld > r + ChunkWorld) continue;
                if (!chunks.TryGetValue(fc0 + o, out var ch) || !ch.go) continue;
                int key = FloraKey((ch.c.y + 0.5f) * ChunkWorld);
                bool need = !ch.floraGo || ch.floraKey != key || ((ch.floraDirty || ch.floraNext > 0f) && now >= ch.floraNext);
                if (!need) continue;
                BuildFlora(ch, key);
                built++;
            }
        }

        static float RegrowDays(Biome b) => b switch
        {
            Biome.Tundra => 5f, Biome.Tropical => 0.8f, Biome.Forest => 1.2f, Biome.Village => 1f, Biome.Nuclear => 2f, Biome.Town => 2.5f, _ => 3f
        };

        static float BaseDensity(Biome b) => b switch
        {
            Biome.Tundra => 0.1f, Biome.Tropical => 0.62f, Biome.Forest => 0.46f, Biome.Village => 0.5f, Biome.Nuclear => 0.2f, Biome.Town => 0.12f, Biome.City => 0.035f, _ => 0.075f
        };

        void BuildFlora(Chunk ch, int key)
        {
            fv.Clear(); fn.Clear(); fc.Clear(); fi.Clear();
            boxes = 0;
            float now = DayNight.TotalDays;
            bool regrow = false;
            int snow = key & 3;
            bool frost = (key & 4) != 0, bloom = (key & 8) != 0;

            // buildings standing in this chunk
            blockers.Clear();
            var cr = new Rect(ch.c.x * ChunkWorld, ch.c.y * ChunkWorld, ChunkWorld, ChunkWorld);
            foreach (var b in FloraBlocker.All) if (b.rect.Overlaps(cr)) blockers.Add(new Rect(b.rect.x - cr.x, b.rect.y - cr.y, b.rect.width, b.rect.height));

            for (int j = 0; j < N && boxes < MaxBoxes; j++)
            for (int i = 0; i < N && boxes < MaxBoxes; i++)
            {
                int k = j * V + i;
                byte feat = ch.feature[k];
                if (feat >= 2 || ch.pave[k] != 0 || ch.road[k] > 0.25f) continue;
                float lx = (i + 0.5f) * Cell, lz = (j + 0.5f) * Cell;
                bool blocked = false;
                foreach (var b in blockers) if (b.Contains(new Vector2(lx, lz))) { blocked = true; break; }
                if (blocked) continue;
                int gi = ch.c.x * N + i, gj = ch.c.y * N + j;
                float gx = gi * Cell, gz = gj * Cell;
                var biome = (Biome)ch.biome[k];
                float wetBase = ch.wet[k];
                float yMin = Mathf.Min(Mathf.Min(ch.h[k] + ch.d[k], ch.h[k + 1] + ch.d[k + 1]), Mathf.Min(ch.h[k + V] + ch.d[k + V], ch.h[k + V + 1] + ch.d[k + V + 1]));
                float water = ch.water[k];
                bool sea = !float.IsNaN(water) && Mathf.Abs(water - WorldGen.SeaLevel) < 0.01f;           // salt shores: bare beach sand
                if (sea && ch.shore[k] > 0.15f) continue;
                if (sea && yMin < water - 0.8f)
                {
                    // the sea floor (user additions): sea grass, kelp in cool seas, coral in warm ones, urchins on rocks
                    if (Hash(gi * 5 + 11, gj * 3 + 7) > 0.3f * (0.5f + Mathf.PerlinNoise(gx * 0.09f + 4f, gz * 0.09f + 9f))) continue;
                    float sy00 = ch.h[k] + ch.d[k], sy10 = ch.h[k + 1] + ch.d[k + 1], sy01 = ch.h[k + V] + ch.d[k + V], sy11 = ch.h[k + V + 1] + ch.d[k + V + 1];
                    var sc = new CellInfo { i = i, j = j, gi = gi, gj = gj, y00 = sy00, y10 = sy10, y01 = sy01, y11 = sy11, stage = 3 };
                    Seabed(sc, water - yMin, Mathf.Abs(WorldGen.Latitude(gz)));
                    continue;
                }
                bool wetFoot = !float.IsNaN(water) && yMin < water + Weather.LakeRise + 0.05f;
                bool shore = !wetFoot && ch.shore[k] > 0.3f && biome != Biome.Desert;
                if (wetFoot && (yMin < water + Weather.LakeRise - 0.35f || biome == Biome.Desert || sea)) continue;   // open water

                // crop rows in village fields (same pattern as the ground colour)
                bool crop = false;
                if (biome == Biome.Village)
                {
                    float field = Mathf.PerlinNoise(gx * 0.02f + 40f, gz * 0.02f + 12f);
                    if (field > 0.55f) crop = !(Mathf.Repeat(gx + (field > 0.62f ? gz : 0f), 1.5f) < 0.6f) && Hash(gi, gj) > 0.3f;
                    if (field > 0.55f && !crop) continue;           // bare furrows
                }

                float density = feat == 1 ? 0.05f : BaseDensity(biome);
                density *= 0.45f + Mathf.PerlinNoise(gx * 0.13f + 7f, gz * 0.13f + 3f) * 1.1f;
                density *= 0.75f + wetBase * 0.5f;
                if (crop || shore || wetFoot) density = 0.9f;
                if (Hash(gi * 5 + 11, gj * 3 + 7) > density) continue;

                float since = ch.trampled != null ? now - ch.trampled[k] : 1e6f;
                float grow = Mathf.Clamp01(since / (RegrowDays(biome) / (0.6f + wetBase)));
                if (grow < 1f) regrow = true;
                int stage = Mathf.Min(3, Mathf.FloorToInt(grow * 4f));
                if (stage == 0) continue;

                float r1 = Hash(gi * 13 + 3, gj * 17 + 1), r2 = Hash(gi * 7 + 19, gj * 29 + 5);
                float y00 = ch.h[k] + ch.d[k], y10 = ch.h[k + 1] + ch.d[k + 1], y01 = ch.h[k + V] + ch.d[k + V], y11 = ch.h[k + V + 1] + ch.d[k + V + 1];
                var cell = new CellInfo { i = i, j = j, gi = gi, gj = gj, y00 = y00, y10 = y10, y01 = y01, y11 = y11, stage = stage, snow = snow, frost = frost };

                if (crop) { CropRow(cell); continue; }
                if (wetFoot || shore) { Reeds(cell, wetFoot); continue; }
                if (feat == 1) { Tuft(cell, DryGrass, 2, 0.1f); continue; }
                switch (biome)
                {
                    case Biome.Forest:
                        if (r1 < 0.62f) Tuft(cell, frost ? Straw : ForestGrass, 3, 0.16f);
                        else if (r1 < 0.78f) Fern(cell, frost ? Straw : FernGreen);
                        else if (r1 < 0.9f) Shrub(cell, frost ? Straw : ForestGrass, 0.3f);
                        else if (r1 < 0.96f && !frost) Flower(cell, r2 < 0.5f ? FlowerWhite : FlowerPurple);
                        else Mushroom(cell, r2 < 0.4f);
                        break;
                    case Biome.Tropical:
                        if (r1 < 0.5f) Tuft(cell, TropicGrass, 4, 0.24f);
                        else if (r1 < 0.7f) Fern(cell, TropicGrass);
                        else if (r1 < 0.85f) Shrub(cell, TropicGrass, 0.42f);
                        else Flower(cell, r2 < 0.33f ? FlowerRed : r2 < 0.66f ? FlowerPink : FlowerOrange);
                        break;
                    case Biome.Nuclear:
                        if (r1 < 0.7f) Tuft(cell, SickGrass, 2, 0.12f);
                        else if (r1 < 0.88f) Shrub(cell, DeadBush, 0.24f);
                        else Pod(cell);
                        break;
                    case Biome.Desert:
                        if (bloom && r1 < 0.25f && !frost) Flower(cell, r2 < 0.5f ? FlowerPink : FlowerYellow);
                        else if (r1 < 0.6f) Tuft(cell, DryGrass, 3, 0.14f);
                        else if (r1 < 0.85f) Twigs(cell);
                        else Sprout(cell);
                        break;
                    default:   // meadows, towns, cities: weeds and wild flowers
                        if (r1 < 0.72f) Tuft(cell, frost ? Straw : MeadowGrass, 3, biome == Biome.Village ? 0.2f : 0.12f);
                        else if (r1 < 0.84f) Shrub(cell, frost ? Straw : MeadowGrass, 0.26f);
                        else if (!frost) Flower(cell, r2 < 0.25f ? FlowerYellow : r2 < 0.5f ? FlowerWhite : r2 < 0.75f ? FlowerRed : FlowerBlue);
                        break;
                }
            }

            if (!ch.floraMesh)
            {
                ch.floraMesh = new Mesh { name = "Flora " + ch.c };
                ch.floraMesh.MarkDynamic();
            }
            var m = ch.floraMesh;
            m.Clear();
            m.indexFormat = fv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(fv); m.SetNormals(fn); m.SetColors(fc); m.SetTriangles(fi, 0);
            m.RecalculateBounds();
            var bb = m.bounds; bb.Expand(new Vector3(0.6f, 0.3f, 0.6f)); m.bounds = bb;       // wind bend
            if (!ch.floraGo)
            {
                ch.floraGo = new GameObject("Flora", typeof(MeshFilter), typeof(MeshRenderer));
                ch.floraGo.transform.SetParent(ch.go.transform, false);
                ch.floraGo.GetComponent<MeshFilter>().sharedMesh = m;
                var mr = ch.floraGo.GetComponent<MeshRenderer>();
                mr.sharedMaterial = floraMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ch.floraKey = key;
            ch.floraDirty = false;
            ch.floraNext = regrow ? Time.time + 15f : 0f;
        }

        // ------------------------------------------------------------------ plants

        struct CellInfo
        {
            public int i, j, gi, gj, stage, snow;
            public float y00, y10, y01, y11;
            public bool frost;
            public float Ground(float fx, float fz) => fz > fx ? y00 + (y11 - y01) * fx + (y01 - y00) * fz : y00 + (y10 - y00) * fx + (y11 - y10) * fz;
        }

        static readonly Color32[] ForestGrass = { C(0x2a381a), C(0x34441e), C(0x405224), C(0x4c602a) };
        static readonly Color32[] TropicGrass = { C(0x2a561c), C(0x366a24), C(0x42802c), C(0x549634) };
        static readonly Color32[] MeadowGrass = { C(0x46561e), C(0x566826), C(0x68782e), C(0x7a8836) };
        static readonly Color32[] DryGrass = { C(0x7a6038), C(0x8e7242), C(0xa2844e), C(0xb4965a) };
        static readonly Color32[] Straw = { C(0x6e5a34), C(0x806a3e), C(0x927a48), C(0xa48a52) };
        static readonly Color32[] SickGrass = { C(0x5a5a24), C(0x6a682a), C(0x7c7830), C(0x8e8836) };
        static readonly Color32[] FernGreen = { C(0x24461a), C(0x2e5620), C(0x3a6828), C(0x467a30) };
        static readonly Color32[] DeadBush = { C(0x3e3a34), C(0x4a453e), C(0x585248), C(0x665e52) };
        static readonly Color32[] Twig = { C(0x4a3420), C(0x5a4028), C(0x6c4e32) };
        static readonly Color32[] Wheat = { C(0x8a8a2c), C(0xa89a36), C(0xc4aa40), C(0xd8bc4c) };
        static readonly Color32[] Reed = { C(0x4a5a26), C(0x5a6a2e), C(0x6a7a36) };
        static readonly Color32 FlowerWhite = C(0xeeeadc), FlowerYellow = C(0xe8c440), FlowerRed = C(0xc03020), FlowerPink = C(0xd870a8),
            FlowerBlue = C(0x5a74d0), FlowerPurple = C(0x8a54b4), FlowerOrange = C(0xe07a28), Cattail = C(0x4a2e1a),
            CapRed = C(0xa82818), CapBrown = C(0x8a6a44), Stem = C(0xd8d0bc), PodGlow = C(0x9cff3a), CactusG = C(0x3a5c2e);

        /// <summary>Axis-aligned box in chunk space; 5 faces (no bottom). Vertex alpha = wind weight (255 rooted).</summary>
        void Box(float x0, float y0, float z0, float sx, float sy, float sz, Color32 col, byte aBottom, byte aTop)
        {
            if (boxes >= MaxBoxes) return;
            boxes++;
            float x1 = x0 + sx, y1 = y0 + sy, z1 = z0 + sz;
            Color32 cb = col, ct = col; cb.a = aBottom; ct.a = aTop;
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, cb, ct);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left, cb, ct);
            Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward, cb, ct);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, cb, ct);
            int b = fv.Count;
            fv.Add(new Vector3(x0, y1, z0)); fv.Add(new Vector3(x0, y1, z1)); fv.Add(new Vector3(x1, y1, z1)); fv.Add(new Vector3(x1, y1, z0));
            for (int t = 0; t < 4; t++) { fn.Add(Vector3.up); fc.Add(ct); }
            fi.Add(b); fi.Add(b + 1); fi.Add(b + 2); fi.Add(b); fi.Add(b + 2); fi.Add(b + 3);
        }

        // a, d on the ground; a→b up; clockwise seen from outside
        static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color32 bottom, Color32 top)
        {
            int s = fv.Count;
            fv.Add(a); fv.Add(b); fv.Add(c); fv.Add(d);
            fn.Add(n); fn.Add(n); fn.Add(n); fn.Add(n);
            fc.Add(bottom); fc.Add(top); fc.Add(top); fc.Add(bottom);
            fi.Add(s); fi.Add(s + 1); fi.Add(s + 2); fi.Add(s); fi.Add(s + 2); fi.Add(s + 3);
        }

        static byte Bend(float h) => (byte)Mathf.Clamp(255 - h * 520f, 20, 255);

        /// <summary>A vertical stalk one flora voxel wide at (fx, fz) inside the cell (0..1).</summary>
        void Stalk(in CellInfo c, float fx, float fz, float h, Color32 col, float w = FV)
        {
            if (c.snow >= 2) { h -= 0.06f * c.snow; if (h < 0.03f) return; }
            float x = c.i * Cell + Mathf.Round(fx * Cell / FV) * FV, z = c.j * Cell + Mathf.Round(fz * Cell / FV) * FV;
            float y = c.Ground(Mathf.Clamp01(fx), Mathf.Clamp01(fz)) - 0.02f + (c.snow >= 2 ? 0.06f * c.snow : 0f);
            Box(x, y, z, w, h + 0.02f, w, col, 255, Bend(h));
        }

        static float Rn(in CellInfo c, int n) => Hash(c.gi * 31 + n * 7, c.gj * 17 + n * 13);
        static float Grow(in CellInfo c) => c.stage / 3f;

        static readonly Color32[] SeaGrass = { C(0x1e4a2a), C(0x265a30), C(0x2e6a38), C(0x3a7a40) };
        static readonly Color32[] Kelp = { C(0x3a3a14), C(0x4a4a1a), C(0x5a5620), C(0x6a6428) };
        static readonly Color32[] Coral = { C(0xd8588a), C(0xe07a3a), C(0x8a4ab4), C(0xd8c040), C(0x4ac0b0) };
        static readonly Color32 Urchin = C(0x3a1a3a), SeaRock = C(0x3e4448);

        /// <summary>One sea-floor cell: coral heads and sea grass in the tropics, kelp strands reaching for the light in
        /// cooler water, a boulder with urchins now and then.</summary>
        void Seabed(in CellInfo c, float depth, float lat)
        {
            float r = Rn(c, 3);
            if (r > 0.93f)
            {
                float x = c.i * Cell + 0.04f, z = c.j * Cell + 0.04f, y = c.Ground(0.3f, 0.3f) - 0.05f;
                Box(x, y, z, 0.16f, 0.12f, 0.14f, SeaRock, 255, 255);
                Box(x + 0.04f, y + 0.12f, z + 0.04f, 0.06f, 0.05f, 0.06f, Urchin, 255, 255);
                return;
            }
            if (lat < 25f && r < 0.45f)
            {
                var col = Coral[Mathf.FloorToInt(Rn(c, 5) * Coral.Length) % Coral.Length];
                int branches = 3 + Mathf.FloorToInt(Rn(c, 6) * 3f);
                for (int b = 0; b < branches; b++)
                    Stalk(c, 0.25f + Rn(c, 50 + b) * 0.5f, 0.25f + Rn(c, 60 + b) * 0.5f, 0.12f + Rn(c, 70 + b) * 0.35f, col, FV * 1.5f);
                return;
            }
            if (lat >= 25f && r < 0.25f && depth > 1.6f)
            {
                // kelp: a strand up to near the surface, leaves every 30 cm, floppy (sways)
                float h = Mathf.Min(depth - 0.3f, 6f) * (0.6f + Rn(c, 8) * 0.4f);
                float fx = 0.3f + Rn(c, 9) * 0.4f, fz = 0.3f + Rn(c, 10) * 0.4f;
                Stalk(c, fx, fz, h, Kelp[1]);
                float x = c.i * Cell + fx * Cell, z = c.j * Cell + fz * Cell, y0 = c.Ground(fx, fz);
                for (float y = 0.4f; y < h; y += 0.3f) Box(x - FV, y0 + y, z, FV * 3f, FV, FV, Kelp[(int)(y * 3f) & 3], Bend(y), Bend(y + 0.1f));
                return;
            }
            Tuft(c, SeaGrass, 4, 0.3f);
        }

        void Tuft(in CellInfo c, Color32[] ramp, int maxBlades, float height)
        {
            int n = 1 + Mathf.FloorToInt(Rn(c, 1) * maxBlades);
            for (int b = 0; b < n; b++)
            {
                float h = (0.35f + Rn(c, 10 + b) * 0.65f) * height * Grow(c) + FV;
                Stalk(c, 0.15f + Rn(c, 20 + b) * 0.7f, 0.15f + Rn(c, 30 + b) * 0.7f, h, ramp[Mathf.Min(ramp.Length - 1, Mathf.FloorToInt(Rn(c, 40 + b) * ramp.Length))]);
            }
        }

        void Flower(in CellInfo c, Color32 petal)
        {
            Tuft(c, MeadowGrass, 1, 0.1f);
            float fx = 0.3f + Rn(c, 2) * 0.4f, fz = 0.3f + Rn(c, 3) * 0.4f, h = (0.1f + Rn(c, 4) * 0.12f) * Grow(c) + FV;
            Stalk(c, fx, fz, h, MeadowGrass[1]);
            if (c.stage < 3 || c.snow >= 2) return;
            float x = c.i * Cell + Mathf.Round(fx * Cell / FV) * FV - FV * 0.25f, z = c.j * Cell + Mathf.Round(fz * Cell / FV) * FV - FV * 0.25f;
            float y = c.Ground(fx, fz) + h;
            Box(x, y, z, FV * 1.5f, FV, FV * 1.5f, petal, Bend(h), Bend(h + FV));
        }

        void Shrub(in CellInfo c, Color32[] ramp, float size)
        {
            float s = size * (0.4f + 0.6f * Grow(c)) * (0.7f + Rn(c, 5) * 0.5f);
            if (c.snow >= 3) s *= 0.5f;
            float cx = c.i * Cell + Cell * 0.5f, cz = c.j * Cell + Cell * 0.5f, y = c.Ground(0.5f, 0.5f) - 0.03f;
            int lumps = 3 + Mathf.FloorToInt(Rn(c, 6) * 3f);
            for (int l = 0; l < lumps; l++)
            {
                float w = s * (0.45f + Rn(c, 50 + l) * 0.35f), h = s * (0.35f + Rn(c, 60 + l) * 0.45f);
                float ox = (Rn(c, 70 + l) - 0.5f) * s * 0.8f, oz = (Rn(c, 80 + l) - 0.5f) * s * 0.8f, oy = l == 0 ? 0f : Rn(c, 90 + l) * s * 0.35f;
                var col = ramp[Mathf.Min(ramp.Length - 1, 1 + Mathf.FloorToInt(Rn(c, 95 + l) * (ramp.Length - 1)))];
                Box(cx + ox - w * 0.5f, y + oy, cz + oz - w * 0.5f, w, h, w, col, Bend(oy), Bend(oy + h));
            }
        }

        void Fern(in CellInfo c, Color32[] ramp)
        {
            float len = (0.1f + Rn(c, 7) * 0.12f) * Grow(c) + FV, cx = c.i * Cell + Cell * 0.5f, cz = c.j * Cell + Cell * 0.5f, y = c.Ground(0.5f, 0.5f);
            if (c.snow >= 2) return;
            Box(cx - FV * 0.5f, y - 0.02f, cz - FV * 0.5f, FV, FV * 2f, FV, ramp[1], 255, Bend(FV * 2f));
            Box(cx, y + FV, cz - FV * 0.5f, len, FV * 0.6f, FV, ramp[2], Bend(FV), Bend(FV * 2f));
            Box(cx - len, y + FV * 0.8f, cz - FV * 0.5f, len, FV * 0.6f, FV, ramp[3], Bend(FV), Bend(FV * 2f));
            Box(cx - FV * 0.5f, y + FV * 0.9f, cz, FV, FV * 0.6f, len, ramp[2], Bend(FV), Bend(FV * 2f));
            Box(cx - FV * 0.5f, y + FV * 0.7f, cz - len, FV, FV * 0.6f, len, ramp[1], Bend(FV), Bend(FV * 2f));
        }

        void Mushroom(in CellInfo c, bool red)
        {
            if (c.stage < 2 || c.snow >= 2) { Tuft(c, ForestGrass, 2, 0.1f); return; }
            float cx = c.i * Cell + Cell * (0.3f + Rn(c, 8) * 0.4f), cz = c.j * Cell + Cell * (0.3f + Rn(c, 9) * 0.4f), y = c.Ground(0.5f, 0.5f);
            Box(cx - 0.02f, y - 0.02f, cz - 0.02f, 0.04f, 0.1f, 0.04f, Stem, 255, 255);
            Box(cx - FV * 1.2f, y + 0.08f, cz - FV * 1.2f, FV * 2.4f, 0.04f, FV * 2.4f, red ? CapRed : CapBrown, 255, 255);
        }

        void CropRow(in CellInfo c)
        {
            for (int b = 0; b < 3; b++)
            {
                float fx = 0.2f + b * 0.3f, fz = 0.2f + Rn(c, 100 + b) * 0.6f;
                float h = (0.12f + 0.3f * Grow(c)) * (0.85f + Rn(c, 110 + b) * 0.3f);
                var col = c.frost ? Straw[2] : Wheat[Mathf.Min(3, c.stage - 1 + (Rn(c, 120 + b) > 0.6f ? 1 : 0))];
                Stalk(c, fx, fz, h, col);
                if (c.stage == 3 && !c.frost && c.snow < 2)
                {
                    float x = c.i * Cell + Mathf.Round(fx * Cell / FV) * FV, z = c.j * Cell + Mathf.Round(fz * Cell / FV) * FV;
                    Box(x, c.Ground(fx, fz) + h, z, FV, FV * 1.6f, FV, Wheat[3], Bend(h), Bend(h + FV * 1.6f));
                }
            }
        }

        void Reeds(in CellInfo c, bool inWater)
        {
            int n = 2 + Mathf.FloorToInt(Rn(c, 11) * 3f);
            for (int b = 0; b < n; b++)
            {
                float fx = 0.15f + Rn(c, 130 + b) * 0.7f, fz = 0.15f + Rn(c, 140 + b) * 0.7f;
                float h = (0.3f + Rn(c, 150 + b) * 0.45f) * Grow(c) + (inWater ? 0.25f : 0f);
                Stalk(c, fx, fz, h, c.frost ? Straw[1] : Reed[b % 3]);
                if (c.stage == 3 && Rn(c, 160 + b) < 0.35f)
                {
                    float x = c.i * Cell + Mathf.Round(fx * Cell / FV) * FV, z = c.j * Cell + Mathf.Round(fz * Cell / FV) * FV;
                    Box(x - 0.005f, c.Ground(fx, fz) + h - FV * 2f, z - 0.005f, FV + 0.01f, FV * 1.8f, FV + 0.01f, Cattail, Bend(h - FV * 2f), Bend(h));
                }
            }
        }

        void Twigs(in CellInfo c)
        {
            float cx = c.i * Cell + Cell * 0.5f, cz = c.j * Cell + Cell * 0.5f, y = c.Ground(0.5f, 0.5f) - 0.02f;
            float h = (0.12f + Rn(c, 12) * 0.16f) * (0.5f + 0.5f * Grow(c));
            Box(cx - 0.02f, y, cz - 0.02f, 0.04f, h, 0.04f, Twig[0], 255, Bend(h));
            Box(cx - 0.02f, y + h * 0.6f, cz - 0.02f, 0.12f, 0.03f, 0.03f, Twig[1], Bend(h * 0.6f), Bend(h * 0.7f));
            Box(cx - 0.11f, y + h * 0.4f, cz, 0.1f, 0.03f, 0.03f, Twig[2], Bend(h * 0.4f), Bend(h * 0.5f));
            Box(cx, y + h * 0.8f, cz - 0.1f, 0.03f, 0.03f, 0.1f, Twig[1], Bend(h * 0.8f), Bend(h * 0.9f));
        }

        void Sprout(in CellInfo c)
        {
            float cx = c.i * Cell + Cell * 0.5f, cz = c.j * Cell + Cell * 0.5f, y = c.Ground(0.5f, 0.5f) - 0.02f;
            float h = 0.08f + 0.14f * Grow(c);
            Box(cx - 0.04f, y, cz - 0.04f, 0.08f, h, 0.08f, CactusG, 255, 255);
            if (c.stage == 3) Box(cx + 0.04f, y + h * 0.5f, cz - 0.02f, 0.05f, 0.04f, 0.04f, CactusG, 255, 255);
        }

        void Pod(in CellInfo c)
        {
            float fx = 0.5f, fz = 0.5f, h = 0.08f + 0.12f * Grow(c);
            Stalk(c, fx, fz, h, SickGrass[0]);
            if (c.stage < 3) return;
            float x = c.i * Cell + Mathf.Round(fx * Cell / FV) * FV - FV * 0.25f, z = c.j * Cell + Mathf.Round(fz * Cell / FV) * FV - FV * 0.25f;
            Box(x, c.Ground(fx, fz) + h, z, FV * 1.5f, FV * 1.5f, FV * 1.5f, PodGlow, Bend(h), Bend(h + FV));
        }
    }
}
