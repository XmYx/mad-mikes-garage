using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public struct GroundSample
    {
        public float height, baseWet, road, roadDist, along;
        public bool paved;
        public Biome biome;
        public float water;          // lake surface height (NaN = none)
        public float shore;          // 0..1, 1 = at the waterline
        public byte feature;         // Site ground: 0 none, 1 rock (mesa slopes), 2 concrete floor (bunker), 3 gravel (tunnel floor)
    }

    public enum Biome { Desert, Forest, Tropical, Nuclear, Village, Town, City, Tundra }

    public class Settlement
    {
        public Vector2 pos;
        public Biome kind;           // Village, Town or City
        public float radius, height;
        public int index;
    }

    public class Lake
    {
        public Vector2 pos;
        public float radius, level, depth;
        public bool toxic;
        public int seed;
    }

    /// <summary>Deterministic wasteland: rolling dunes, ridges, low mud basins and a graded road network.</summary>
    public partial class WorldGen
    {
        public readonly int seed;
        public readonly float halfSize;
        public readonly RoadNetwork roads;
        readonly Vector2[] o = new Vector2[8];

        public WorldGen(int seed, float halfSize = HalfX)
        {
            this.seed = seed;
            this.halfSize = halfSize;
            var r = new System.Random(seed);
            for (int i = 0; i < o.Length; i++) o[i] = new Vector2((float)r.NextDouble() * 5000f, (float)r.NextDouble() * 5000f);
            biomeScale = MadMax.Game.GameRules.Current != null ? Mathf.Max(0.3f, MadMax.Game.GameRules.Current.biomeScale) : 1f;
            roads = new RoadNetwork(this, r, 40);
            // settlements on the road network's towns: the start town is a town, at least one city
            for (int i = 0; i < roads.towns.Count; i++)
            {
                var p = roads.towns[i];
                double roll = r.NextDouble();
                var kind = i == 0 ? Biome.Town : i == 1 ? Biome.City : roll < 0.4 ? Biome.Village : roll < 0.75 ? Biome.Town : Biome.City;
                float radius = kind == Biome.Village ? 42f : kind == Biome.Town ? 58f : 95f;
                settlements.Add(new Settlement { pos = p, kind = kind, radius = radius, height = BaseHeight(p.x, p.y), index = i });
            }
            roads.SpawnPoint(out yardP, out yardDir);
            yardSide = Vector3.Cross(Vector3.up, yardDir);
            BuildRivers(new System.Random(seed * 31 + 7));
        }

        // ------------------------------------------------------------------ the start yard
        // A graded gravel lot on both sides of the first highway where the game starts: the starting fleet parks on the
        // -side, the machines on the +side. No props or ground cover there (the start town is tight enough).
        public const float YardAlong0 = -14f, YardAlong1 = 74f, YardAcross0 = -40f, YardAcross1 = 36f;
        Vector3 yardP, yardDir, yardSide;

        /// <summary>Spawn point, road direction and right-hand side of the start yard (metres, world).</summary>
        public void Yard(out Vector3 origin, out Vector3 along, out Vector3 side) { origin = yardP; along = yardDir; side = yardSide; }

        /// <summary>1 inside the start yard, fading to 0 over 12 m outside it.</summary>
        volatile Vector3[] clearings = new Vector3[0];

        /// <summary>Keep wild props off a circle (x, z, radius): the campaign's scenes (the convoy wreck, Nell's stop,
        /// the garage). Set before those chunks generate; read on worker threads (the array is swapped, never edited).</summary>
        public void Reserve(Vector3 circle)
        {
            var old = clearings;
            var n = new Vector3[old.Length + 1];
            old.CopyTo(n, 0); n[old.Length] = circle;
            clearings = n;
        }

        public bool Reserved(float x, float z)
        {
            var c = clearings;
            for (int i = 0; i < c.Length; i++) { float dx = x - c[i].x, dz = z - c[i].y; if (dx * dx + dz * dz < c[i].z * c[i].z) return true; }
            return false;
        }

        public float YardWeight(float x, float z)
        {
            float dx = x - yardP.x, dz = z - yardP.z;
            float a = dx * yardDir.x + dz * yardDir.z, c = dx * yardSide.x + dz * yardSide.z;
            float e = Mathf.Max(Mathf.Max(YardAlong0 - a, a - YardAlong1), Mathf.Max(YardAcross0 - c, c - YardAcross1));
            float w = Mathf.Clamp01(1f - (e + 2f) / 12f);
            return w * w * (3f - 2f * w);
        }

        public readonly float biomeScale;
        public readonly List<Settlement> settlements = new List<Settlement>();
        const float LakeCell = 200f;
        readonly Dictionary<Vector2Int, Lake> lakes = new Dictionary<Vector2Int, Lake>();

        // ------------------------------------------------------------------ biomes

        /// <summary>Wilderness biome from large-scale moisture / heat / fallout noise.</summary>
        public Biome NaturalBiome(float x, float z)
        {
            float f = 0.0014f / biomeScale;
            // keep the start area desert so the opening scene is the classic wasteland
            float start = Mathf.Clamp01(1f - new Vector2(x, z).magnitude / 160f);
            float fallout = Mathf.PerlinNoise(x * f * 1.3f + o[4].x + 311f, z * f * 1.3f + o[4].y);
            if (fallout > 0.73f && start <= 0f && Mathf.Abs(Latitude(z)) < 60f) return Biome.Nuclear;
            float moist = Mathf.PerlinNoise(x * f + o[7].x, z * f + o[7].y);
            float heat = Mathf.PerlinNoise(x * f * 0.7f + o[4].x, z * f * 0.7f + o[4].y + 97f);
            return ClimateBiome(x, z, moist, heat, start);                                      // latitude bands (planet)
        }

        /// <summary>Settlement covering this point (null = wilderness). Edge is jittered.</summary>
        public Settlement SettlementAt(float x, float z)
        {
            foreach (var st in settlements)
            {
                float dx = x - st.pos.x, dz = z - st.pos.y;
                float rr = st.radius * (0.85f + 0.3f * P(x, z, 0.05f, 6));
                if (dx * dx + dz * dz < rr * rr) return st;
            }
            return null;
        }

        public Biome BiomeAt(float x, float z)
        {
            var st = SettlementAt(x, z);
            return st != null ? st.kind : NaturalBiome(x, z);
        }

        /// <summary>0..1 radiation dose rate (nuclear zones, stronger towards their core).</summary>
        public float Radiation(float x, float z)
        {
            float f = 0.0014f / biomeScale;
            float fallout = Mathf.PerlinNoise(x * f * 1.3f + o[4].x + 311f, z * f * 1.3f + o[4].y);
            return Mathf.Clamp01((fallout - 0.73f) / 0.12f);
        }

        /// <summary>0..1 crude oil under the ground: big patchy fields in the dry lands (tar-stained ground marks them;
        /// thin under forest and jungle). A pumpjack's output scales with it.</summary>
        public float OilAt(float x, float z)
        {
            float f = 0.0019f / biomeScale;
            float n = Mathf.PerlinNoise(x * f + o[2].x + 57f, z * f + o[2].y - 131f) * 0.8f + Mathf.PerlinNoise(x * f * 4f + o[3].x - 19f, z * f * 4f + o[3].y + 71f) * 0.2f;
            float oil = Mathf.Clamp01((n - 0.6f) / 0.15f);
            if (oil <= 0f) return 0f;
            var b = NaturalBiome(x, z);
            return b == Biome.Forest || b == Biome.Tropical ? oil * 0.25f : oil;
        }

        // ------------------------------------------------------------------ lakes

        Lake LakeIn(Vector2Int cell)
        {
            lock (lakes) { if (lakes.TryGetValue(cell, out var cached)) return cached; }
            Lake lake;
            var rnd = new System.Random(cell.x * 92821 ^ cell.y * 68917 ^ seed * 31);
            var p = new Vector2((cell.x + 0.2f + (float)rnd.NextDouble() * 0.6f) * LakeCell, (cell.y + 0.2f + (float)rnd.NextDouble() * 0.6f) * LakeCell);
            var b = NaturalBiome(p.x, p.y);
            float chance = b == Biome.Tropical ? 0.8f : b == Biome.Forest ? 0.6f : b == Biome.Nuclear ? 0.45f : 0.1f;
            lake = null;
            bool clear = SettlementAt(p.x, p.y) == null && p.magnitude > 90f && Habitable(p.x, p.y, 0.6f);
            if (clear && rnd.NextDouble() < chance)
            {
                float radius = b == Biome.Desert ? 12f + (float)rnd.NextDouble() * 10f : 18f + (float)rnd.NextDouble() * 30f;
                foreach (var st in settlements) if ((st.pos - p).magnitude < st.radius + radius + 20f) { radius = 0f; break; }
                if (radius > 0f)
                    lake = new Lake { pos = p, radius = radius, level = BaseHeight(p.x, p.y) - 0.4f, depth = 1.6f + (float)rnd.NextDouble() * 1.6f, toxic = b == Biome.Nuclear, seed = rnd.Next() };
            }
            lock (lakes) lakes[cell] = lake;
            return lake;
        }

        /// <summary>Nearest lake whose area (with shore) covers the point.</summary>
        public Lake LakeAt(float x, float z, out float t)
        {
            t = 99f;
            var c = new Vector2Int(Mathf.FloorToInt(x / LakeCell), Mathf.FloorToInt(z / LakeCell));
            Lake best = null;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var lake = LakeIn(new Vector2Int(c.x + dx, c.y + dz));
                if (lake == null) continue;
                float wob = 0.82f + 0.36f * P(x, z, 0.04f, 3);
                float d = Vector2.Distance(new Vector2(x, z), lake.pos) / (lake.radius * wob);
                if (d < t) { t = d; best = lake; }
            }
            return t < 1.6f ? best : null;
        }

        /// <summary>Water surface height at a point, or NaN.</summary>
        public float WaterLevel(float x, float z)
        {
            var lake = LakeAt(x, z, out float t);
            return lake != null && t < 1.05f ? lake.level : float.NaN;
        }

        [System.ThreadStatic] static List<RoadHit> threadHits;   // Sample runs on terrain worker threads too

        float P(float x, float z, float f, int i) => Mathf.PerlinNoise(x * f + o[i].x, z * f + o[i].y);

        /// <summary>0..1, 1 = centre of a low mud flat.</summary>
        public float Basin(float x, float z)
        {
            float b = Mathf.Clamp01(Mathf.InverseLerp(0.36f, 0.22f, P(x, z, 0.0055f, 5)));
            return b * b * (3f - 2f * b);
        }

        public float BaseHeight(float x, float z)
        {
            float h = (P(x, z, 0.0022f, 0) - 0.5f) * 46f;
            h += (P(x, z, 0.009f, 1) - 0.5f) * 10f;
            float r = 1f - Mathf.Abs(P(x, z, 0.018f, 2) * 2f - 1f);
            h += r * r * r * 4f;
            float b = Basin(x, z);
            h += (P(x, z, 0.11f, 3) - 0.5f) * 0.45f * (1f - b);
            h -= b * 3.5f;
            return PlanetHeight(x, z, h);                                                       // coasts, sea floor, polar walls
        }

        public float BaseWetness(float x, float z)
        {
            float n = P(x, z, 0.035f, 6);
            return Mathf.Clamp01(Basin(x, z) * 1.25f + (n - 0.64f) * 1.6f);
        }

        static float Hash01(float x, float z)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(x * 4f) * 374761393u ^ (uint)Mathf.RoundToInt(z * 4f) * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        public GroundSample Sample(float x, float z)
        {
            var s = new GroundSample { roadDist = 999f };
            float h = BaseHeight(x, z);
            float wet = BaseWetness(x, z);
            s.water = float.NaN;
            float ford = 0f, fordLevel = float.NaN;
            var settle = SettlementAt(x, z);
            // jittered lookup dithers biome borders instead of drawing straight seams
            float jx = (P(x, z, 0.12f, 1) - 0.5f) * 10f + (Hash01(x, z) - 0.5f) * 3f;
            float jz = (P(x, z, 0.12f, 2) - 0.5f) * 10f + (Hash01(z, x) - 0.5f) * 3f;
            s.biome = settle != null ? settle.kind : NaturalBiome(x + jx, z + jz);
            if (settle != null)
            {
                // settlements sit on graded ground (cities flat, villages follow the land a little)
                float d = Vector2.Distance(new Vector2(x, z), settle.pos) / settle.radius;
                float flat = Mathf.Clamp01((1.15f - d) / 0.4f) * (settle.kind == Biome.Village ? 0.6f : 0.95f);
                h = Mathf.Lerp(h, settle.height, flat);
                wet *= settle.kind == Biome.City ? 0.2f : 0.6f;
            }
            else
            {
                var lake = LakeAt(x, z, out float t);
                if (lake != null)
                {
                    // bowl: shore slopes down to the waterline at t = 1, then to the lake bed
                    float bed = lake.level - lake.depth * Mathf.Clamp01(1f - t * t);
                    float shoreTop = Mathf.Min(h, lake.level + (t - 1f) * 2.5f);
                    float blend = Mathf.Clamp01((t - 1f) / 0.6f);
                    float target = t < 1f ? bed : shoreTop;
                    h = Mathf.Lerp(Mathf.Min(h, target), h, blend * blend);
                    if (t < 1.45f) s.water = lake.level;          // shore band too: it floods when the lake rises
                    s.shore = Mathf.Clamp01(1f - Mathf.Abs(t - 1f) / 0.25f);
                    wet = Mathf.Max(wet, Mathf.Clamp01(1.3f - t) * 0.9f);
                }
                SeaAt(x, z, h, ref s, ref wet);
                if (s.biome == Biome.Tropical) wet = Mathf.Clamp01(wet + 0.15f);
                if (rivers.Count > 0) ford = ShapeRiver(x, z, ref h, ref s, ref wet, out fordLevel);
                var site = SiteAt(x, z);
                if (site != null) h = ShapeSite(site, x, z, h, ref s, ref wet);
            }
            float yard = YardWeight(x, z);
            if (yard > 0f) { h = Mathf.Lerp(h, yardP.y, yard); wet *= 1f - 0.6f * yard; }
            var hits = threadHits ??= new List<RoadHit>();
            roads.QueryAll(x, z, hits);
            if (hits.Count > 0)
            {
                // weighted average of all nearby roads' graded heights: continuous at junctions
                float roadMax = 0f, wSum = 0f, hSum = 0f, blend = 0f;
                foreach (var q in hits)
                {
                    float half = q.width * 0.5f;
                    float hb = 1f - Mathf.Clamp01((q.dist - half - 0.5f) / (RoadNetwork.Reach - 1f));
                    hb = hb * hb * (3f - 2f * hb);
                    wSum += hb; hSum += hb * q.height; blend = Mathf.Max(blend, hb);
                    float onRoad = 1f - Mathf.Clamp01((q.dist - half) / 1.2f);
                    // paved wins on overlap so asphalt markings stay on top
                    if (onRoad > roadMax + 0.01f || (onRoad > 0.5f && q.paved && !s.paved))
                    {
                        roadMax = Mathf.Max(roadMax, onRoad);
                        s.roadDist = q.dist; s.along = q.along; s.paved = q.paved;
                    }
                }
                if (wSum > 0f) h = Mathf.Lerp(h, hSum / wSum, blend);
                // dirt tracks ford rivers (the road dips into the water); highways keep their graded causeway
                // (the river level, not the water band: that ends 1.5 m up the bank and left a wall where the dip stopped)
                if (ford > 0f && !s.paved && !float.IsNaN(fordLevel)) h = Mathf.Lerp(h, Mathf.Min(h, fordLevel - 0.35f), ford * blend);
                s.road = roadMax;
                wet *= 1f - s.road * 0.85f;
            }
            if (yard > 0.8f && s.road < 0.3f && s.feature == 0) s.feature = 6;                 // yard gravel
            s.height = h;
            s.baseWet = wet;
            return s;
        }
    }
}
