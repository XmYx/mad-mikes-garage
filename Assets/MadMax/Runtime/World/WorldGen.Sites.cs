using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public enum SiteKind { Bunker, Outcrop, Airfield }

    /// <summary>A generated landmark that reshapes the ground: a buried bunker (graded surface, floor pit under a
    /// voxel roof, entrance ramp) or a rock mesa with a natural tunnel running through it. Layouts are axis-aligned
    /// in quarter turns so voxel walls follow the terrain grid. The voxel structures come from <see cref="SiteBuilder"/>.</summary>
    public class Site
    {
        public SiteKind kind;
        public Vector2 pos;
        public int rot;                 // quarter turns (prop yaw = rot * 90)
        public float reach;             // radius of influence on the ground (m)
        public int seed;
        public Vector2Int cell;
        public string Key => $"{cell.x},{cell.y}";

        // bunker: gw x gh cells, room id per cell (-1 empty), doorways between neighbouring cells
        public const float CellSize = 4f, BunkerDepth = 3f, RampLen = 8f, RampHalf = 1.9f, PitMargin = 0.45f;
        public float top, floor;
        public int gw, gh, entrance;    // entrance: cell column on the j = 0 edge, the ramp runs out along local -z
        public int[] room;
        public bool[] doorX, doorZ;     // (i,j)-(i+1,j) and (i,j)-(i,j+1)

        // outcrop: mesa of halfLen x halfWid (local z = tunnel axis); airfield: runway of halfLen x halfWid along local z
        public float halfLen, halfWid, height;
        public const float ApronX = 22f, ApronHalfX = 14f, ApronHalfZ = 16f;   // airfield: hangar apron centre (local +x) and size
        public const float TunnelHalf = 3.4f, TunnelWall = 0.5f, TunnelRoof = 5.2f;

        public int Room(int i, int j) => i < 0 || j < 0 || i >= gw || j >= gh ? -1 : room[j * gw + i];
        public bool Door(int i, int j, bool alongX) => alongX ? i >= 0 && j >= 0 && i < gw - 1 && j < gh && doorX[j * gw + i]
                                                              : i >= 0 && j >= 0 && i < gw && j < gh - 1 && doorZ[j * gw + i];

        /// <summary>World xz → site-local frame (prop space).</summary>
        public Vector2 ToLocal(float x, float z)
        {
            float dx = x - pos.x, dz = z - pos.y;
            switch (rot & 3)
            {
                case 1: return new Vector2(-dz, dx);
                case 2: return new Vector2(-dx, -dz);
                case 3: return new Vector2(dz, -dx);
                default: return new Vector2(dx, dz);
            }
        }

        /// <summary>Site-local xz → world xz (same rotation as Quaternion.Euler(0, rot * 90, 0)).</summary>
        public Vector2 ToWorld(float lx, float lz)
        {
            switch (rot & 3)
            {
                case 1: return pos + new Vector2(lz, -lx);
                case 2: return pos + new Vector2(-lx, -lz);
                case 3: return pos + new Vector2(-lz, lx);
                default: return pos + new Vector2(lx, lz);
            }
        }

        /// <summary>Local centre of a bunker cell.</summary>
        public Vector2 CellCentre(int i, int j) => new Vector2((i - (gw - 1) * 0.5f) * CellSize, (j - (gh - 1) * 0.5f) * CellSize);
        public float EntranceX => (entrance - (gw - 1) * 0.5f) * CellSize;
        public float GridMinZ => -gh * 0.5f * CellSize;
    }

    public partial class WorldGen
    {
        const float SiteCell = 320f;
        readonly Dictionary<Vector2Int, Site> sites = new Dictionary<Vector2Int, Site>();

        /// <summary>The site generated for a 320 m cell (null = none). Deterministic, thread-safe.</summary>
        public Site SiteIn(Vector2Int cell)
        {
            lock (sites) { if (sites.TryGetValue(cell, out var cached)) return cached; }
            var site = MakeSite(cell);
            lock (sites) sites[cell] = site;
            return site;
        }

        /// <summary>Site whose area covers the point.</summary>
        public Site SiteAt(float x, float z)
        {
            var c = new Vector2Int(Mathf.FloorToInt(x / SiteCell), Mathf.FloorToInt(z / SiteCell));
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var s = SiteIn(new Vector2Int(c.x + dx, c.y + dz));
                if (s == null) continue;
                float ex = x - s.pos.x, ez = z - s.pos.y;
                if (ex * ex + ez * ez < s.reach * s.reach) return s;
            }
            return null;
        }

        /// <summary>Sites near a point (for prop spawning and template warm-up).</summary>
        public void SitesNear(Vector3 p, float radius, List<Site> into)
        {
            into.Clear();
            int r = Mathf.CeilToInt(radius / SiteCell) + 1;
            var c = new Vector2Int(Mathf.FloorToInt(p.x / SiteCell), Mathf.FloorToInt(p.z / SiteCell));
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                var s = SiteIn(new Vector2Int(c.x + dx, c.y + dz));
                if (s != null && (new Vector2(p.x, p.z) - s.pos).magnitude < radius + s.reach) into.Add(s);
            }
        }

        Site MakeSite(Vector2Int cell)
        {
            var rnd = new System.Random(Mix(cell.x, cell.y, seed));
            var p = new Vector2((cell.x + 0.2f + (float)rnd.NextDouble() * 0.6f) * SiteCell, (cell.y + 0.2f + (float)rnd.NextDouble() * 0.6f) * SiteCell);
            var b = NaturalBiome(p.x, p.y);
            double roll = rnd.NextDouble();
            SiteKind kind;
            if (roll < (b == Biome.Nuclear ? 0.55 : b == Biome.Desert ? 0.3 : 0.25)) kind = SiteKind.Bunker;
            else if (roll < (b == Biome.Desert ? 0.7 : b == Biome.Forest ? 0.5 : 0.4)) kind = SiteKind.Outcrop;
            else if (b != Biome.Nuclear && roll < (b == Biome.Desert ? 0.92 : b == Biome.Forest ? 0.62 : 0.52)) kind = SiteKind.Airfield;
            else return null;
            var s = new Site { kind = kind, pos = p, rot = rnd.Next(4), seed = rnd.Next(), cell = cell };
            if (kind == SiteKind.Bunker) PlanBunker(s, rnd);
            else if (kind == SiteKind.Airfield)
            {
                // an old airstrip: 220-300 m of runway, apron and hangar to one side; needs fairly level land
                s.halfLen = 95f + (float)rnd.NextDouble() * 35f;
                s.halfWid = 9f;
                s.top = BaseHeight(p.x, p.y);
                s.reach = s.halfLen + 30f;
                for (int i = -6; i <= 6; i++)
                {
                    var q = s.ToWorld(0f, s.halfLen * i / 6f);
                    if (Mathf.Abs(BaseHeight(q.x, q.y) - s.top) > 10f) return null;
                }
            }
            else
            {
                s.halfLen = 20f + (float)rnd.NextDouble() * 12f;
                s.halfWid = 11f + (float)rnd.NextDouble() * 5f;
                s.height = 7.5f + (float)rnd.NextDouble() * 4f;
                s.reach = s.halfLen + 4f;
            }
            // keep clear of towns, lakes, roads, the start area and the world edge
            if (p.magnitude < 150f || !Habitable(p.x, p.y, 0.6f)) return null;
            foreach (var st in settlements) if ((st.pos - p).magnitude < st.radius + (kind == SiteKind.Airfield ? s.halfLen * 0.6f : s.reach) + 40f) return null;
            var hits = new List<RoadHit>();
            for (int i = 0; i < 9 && kind != SiteKind.Airfield; i++)                                 // airfields may have a road nearby: only the strip is checked
            {
                float a = i * Mathf.PI * 2f / 8f;
                var q = i == 8 ? p : p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.reach;
                if (LakeAt(q.x, q.y, out float t) != null && t < 1.8f) return null;
                if (RiverAt(q.x, q.y, out _, out _, out _)) return null;
                roads.QueryAll(q.x, q.y, hits);
                if (hits.Count > 0) return null;
            }
            if (kind == SiteKind.Airfield)
                for (float z = -s.halfLen; z <= s.halfLen; z += 20f)                                   // the strip itself stays dry and off the roads
                {
                    var q = s.ToWorld(0f, z);
                    if (LakeAt(q.x, q.y, out float t) != null && t < 1.8f) return null;
                    if (RiverAt(q.x, q.y, out _, out _, out _)) return null;
                    roads.QueryAll(q.x, q.y, hits);
                    if (hits.Count > 0) return null;
                }
            return s;
        }

        /// <summary>Seed from a cell (System.Random takes |seed|, so plain xor products repeat across mirrored cells).</summary>
        static int Mix(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u;
                h ^= (uint)b * 0x85EBCA77u + (h << 6) + (h >> 2);
                h ^= (uint)c * 0xC2B2AE3Du + (h << 6) + (h >> 2);
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>Bunker layout: a tree of 4 m cells grown from the entrance, some merged into bigger rooms.</summary>
        void PlanBunker(Site s, System.Random rnd)
        {
            s.gw = 5 + rnd.Next(3); s.gh = 5 + rnd.Next(3);
            s.room = new int[s.gw * s.gh];
            s.doorX = new bool[s.gw * s.gh]; s.doorZ = new bool[s.gw * s.gh];
            for (int k = 0; k < s.room.Length; k++) s.room[k] = -1;
            s.entrance = s.gw / 2;
            var cells = new List<Vector2Int> { new Vector2Int(s.entrance, 0) };
            int next = 0;
            s.room[s.entrance] = next++;
            int target = 8 + rnd.Next(7);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            for (int guard = 0; cells.Count < target && guard < 400; guard++)
            {
                // corridors: mostly extend the newest cell, sometimes branch from an older one
                var from = rnd.NextDouble() < 0.65 ? cells[cells.Count - 1] : cells[rnd.Next(cells.Count)];
                var d = dirs[rnd.Next(4)];
                var to = from + d;
                if (to.x < 0 || to.y < 0 || to.x >= s.gw || to.y >= s.gh || s.Room(to.x, to.y) >= 0) continue;
                s.room[to.y * s.gw + to.x] = next++;
                cells.Add(to);
                var a = d.x + d.y < 0 ? to : from;
                if (d.x != 0) s.doorX[a.y * s.gw + a.x] = true; else s.doorZ[a.y * s.gw + a.x] = true;
            }
            // merge neighbours into halls (no wall between cells of one room)
            foreach (var c in cells)
            {
                if (rnd.NextDouble() > 0.35) continue;
                var n = c + (rnd.NextDouble() < 0.5 ? Vector2Int.right : Vector2Int.up);
                int rn = s.Room(n.x, n.y);
                if (rn < 0 || (n.x == s.entrance && n.y == 0) || (c.x == s.entrance && c.y == 0)) continue;
                int rc = s.Room(c.x, c.y);
                for (int k = 0; k < s.room.Length; k++) if (s.room[k] == rn) s.room[k] = rc;
            }
            s.top = BaseHeight(s.pos.x, s.pos.y);
            s.floor = s.top - Site.BunkerDepth;
            float half = Mathf.Max(s.gw, s.gh) * 0.5f * Site.CellSize;
            s.reach = half * 1.42f + Site.RampLen + 10f;
        }

        /// <summary>Mesa height added over the natural ground (ignores the tunnel cut).</summary>
        public float OutcropAdd(Site s, float x, float z)
        {
            var l = s.ToLocal(x, z);
            float e = Mathf.Sqrt(l.x * l.x / (s.halfWid * s.halfWid) + l.y * l.y / (s.halfLen * s.halfLen));
            e += (P(x, z, 0.09f, 3) - 0.5f) * 0.18f;                                           // ragged outline
            float m = 1f - Mathf.Clamp01((e - 0.6f) / 0.4f);
            m = m * m * (3f - 2f * m);
            float strata = Mathf.Round(m * 4f) / 4f;                                            // stepped ledges
            return s.height * Mathf.Lerp(m, strata, 0.35f) + (P(x, z, 0.23f, 2) - 0.5f) * 1.4f * m;
        }

        float ShapeSite(Site s, float x, float z, float h, ref GroundSample g, ref float wet)
        {
            var l = s.ToLocal(x, z);
            if (s.kind == SiteKind.Airfield)
            {
                // runway graded flat along local z, painted; apron and hangar floor on the +x side
                float rox = Mathf.Max(0f, Mathf.Abs(l.x) - s.halfWid), roz = Mathf.Max(0f, Mathf.Abs(l.y) - s.halfLen);
                float ax = Mathf.Max(0f, Mathf.Abs(l.x - Site.ApronX) - Site.ApronHalfX), az = Mathf.Max(0f, Mathf.Abs(l.y) - Site.ApronHalfZ);
                float rout = Mathf.Min(Mathf.Sqrt(rox * rox + roz * roz), Mathf.Sqrt(ax * ax + az * az));
                float rgrade = 1f - Mathf.Clamp01((rout - 1f) / 14f);
                h = Mathf.Lerp(h, s.top, rgrade * rgrade * (3f - 2f * rgrade));
                if (rox <= 0f && roz <= 0f)
                {
                    g.feature = 4; g.road = 1f; g.paved = true; wet *= 0.2f;
                    float ay = Mathf.Abs(l.y), axx = Mathf.Abs(l.x);
                    bool centre = axx < 0.35f && Mathf.Repeat(l.y, 24f) < 12f && ay < s.halfLen - 20f;
                    bool threshold = ay > s.halfLen - 10f && ay < s.halfLen - 4f && Mathf.Repeat(l.x + 0.75f, 1.5f) < 0.9f && axx < s.halfWid - 1.5f;
                    bool edge = axx > s.halfWid - 0.6f && axx < s.halfWid - 0.3f;
                    bool aim = Mathf.Abs(ay - (s.halfLen - 40f)) < 5f && Mathf.Abs(axx - 3.5f) < 1f;
                    if (centre || threshold || edge || aim) g.feature = 5;
                    return s.top;
                }
                if (ax <= 0f && az <= 0f) { g.feature = 2; g.road = 1f; g.paved = true; wet *= 0.2f; return s.top; }
                return h;
            }
            if (s.kind == SiteKind.Outcrop)
            {
                bool corridor = Mathf.Abs(l.x) < Site.TunnelHalf && Mathf.Abs(l.y) < s.halfLen + 2f;
                float add = OutcropAdd(s, x, z);
                if (corridor)
                {
                    if (add > 0.3f) { g.feature = 3; wet *= 0.3f; }
                    return h;
                }
                if (add > 0.7f) { g.feature = 1; wet *= 0.4f; }
                return h + add;
            }

            // bunker: grade the surroundings to the roof height, sink the floor under the cells and the ramp
            float hx = s.gw * 0.5f * Site.CellSize, hz = s.gh * 0.5f * Site.CellSize;
            float ox = Mathf.Max(0f, Mathf.Abs(l.x) - hx), oz = Mathf.Max(0f, Mathf.Max(l.y - hz, -hz - Site.RampLen - l.y));
            float outside = Mathf.Sqrt(ox * ox + oz * oz);
            float grade = 1f - Mathf.Clamp01((outside - 2f) / 9f);
            h = Mathf.Lerp(h, s.top, grade * grade * (3f - 2f * grade));

            float gx = l.x / Site.CellSize + s.gw * 0.5f, gz = l.y / Site.CellSize + s.gh * 0.5f;
            int ci = Mathf.FloorToInt(gx), cj = Mathf.FloorToInt(gz);
            float m = Site.PitMargin / Site.CellSize;
            for (int dj = -1; dj <= 1; dj++)
            for (int di = -1; di <= 1; di++)
            {
                int i = ci + di, j = cj + dj;
                if (s.Room(i, j) < 0) continue;
                if (gx > i - m && gx < i + 1 + m && gz > j - m && gz < j + 1 + m) { g.feature = 2; g.road = 1f; g.paved = true; wet = 0f; return s.floor; }
            }
            float rz0 = s.GridMinZ - Site.RampLen;
            if (Mathf.Abs(l.x - s.EntranceX) < Site.RampHalf && l.y > rz0 - 0.3f && l.y < s.GridMinZ + 0.1f)
            {
                float t = Mathf.Clamp01((l.y - rz0) / Site.RampLen);
                g.feature = 2; g.road = 1f; g.paved = true; wet = 0f;
                return Mathf.Lerp(s.top, s.floor, t);
            }
            return h;
        }
    }
}
