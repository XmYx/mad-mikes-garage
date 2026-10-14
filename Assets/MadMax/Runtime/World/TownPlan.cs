using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public enum StreetKind : byte { Arterial, Ring, Local, Alley, Pedestrian, Lane }
    public enum LotUse : byte { House, Shop, Apartment, Warehouse, Shack, Farm, Ruin, Empty, Park, Plaza, Market, Parking, GasStation, Green, Hangout }
    public enum SpotKind : byte { Prop, Decor, Lamp, Mast, Wreck, Stall, Birds, Rats, Dogs, Gang, Fire, Boss, Board }

    /// <summary>A street segment of a town plan, junction to junction, in the plan's local frame (metres).</summary>
    public sealed class TownStreet
    {
        public int id, juncA = -1, juncB = -1;
        public Vector2 a, b, dir;
        public float len, half, walk;
        public StreetKind kind;
        /// <summary>Paved (asphalt) or a dirt lane; <see cref="path"/> = a highway runs along it (no parked wrecks).</summary>
        public bool paved, path;
    }

    /// <summary>A crossing; <see cref="ring"/> &gt; 0 makes it a roundabout (island .. ring = the carriageway).</summary>
    public sealed class TownJunction
    {
        public int id;
        public Vector2 p;
        public float island, ring, box, walk;
        public bool zebra;
        public float Centre => (island + ring) * 0.5f;
    }

    /// <summary>A lot: an axis-aligned rectangle (plan frame) facing its street (<see cref="front"/> points at it).</summary>
    public sealed class TownLot
    {
        public int id, street = -1, variant;
        public Vector2 min, max, front;
        public LotUse use;
        public bool last;
        /// <summary>Distance of the building's front line from the lot's front edge (front paths, aprons).</summary>
        public float setback;
        public float Width => Mathf.Abs(front.x) > 0.5f ? max.y - min.y : max.x - min.x;
        public float Depth => Mathf.Abs(front.x) > 0.5f ? max.x - min.x : max.y - min.y;
        public Vector2 Across => new Vector2(front.y, -front.x);
        public Vector2 FrontMid => (min + max) * 0.5f + front * (Depth * 0.5f);
        /// <summary>Plan point at x across the frontage, y in from the front edge.</summary>
        public Vector2 At(float x, float y) => FrontMid + Across * x - front * y;
        public void Frame(Vector2 l, out float x, out float y) { var r = l - FrontMid; x = Vector2.Dot(r, Across); y = -Vector2.Dot(r, front); }
        public bool Contains(Vector2 l) => l.x >= min.x && l.x <= max.x && l.y >= min.y && l.y <= max.y;
        public bool Special => use == LotUse.Park || use == LotUse.Plaza || use == LotUse.Market || use == LotUse.Green;
    }

    /// <summary>Something the plan puts somewhere (world x, z): a prop template, batched decor, a lamp, a wreck, people, animals.</summary>
    public struct TownSpot
    {
        public SpotKind kind;
        public string id, table, visual;
        public Vector2 pos;
        public float yaw, y;
        public bool dynamic, crate;
    }

    /// <summary>What a plan point is: terrain feature, kerb raise and the road fields <see cref="WorldGen.Sample"/> reports.</summary>
    public struct TownHit
    {
        public byte feature;
        public float raise, dist, along, side;
        public bool road, paved;
        public int lot;
    }

    /// <summary>The street plan of one settlement (roadmap: coherent towns). Pure and immutable after the world is built,
    /// queried by terrain workers through <see cref="Grade"/>: a local frame rotated to the main highway, a street level
    /// plane (gentle slopes), streets with sidewalks and kerbs, junctions and roundabouts, lots facing their streets, and
    /// the spots of everything placed on it (buildings, street furniture, lamps, wrecks, people, birds). Built by
    /// <c>TownPlan.Build.cs</c>; highways are rerouted along its boulevards and round its roundabouts
    /// (<see cref="WorldGen"/>.BuildTownPlans). A 4 m cell index answers point queries in O(1).</summary>
    public sealed partial class TownPlan
    {
        public const float Cell = 4f, Kerb = 0.1f, Drive = 0.02f;
        /// <summary>Kerbs are mountable (GTA-style towns): the raise climbs over this width (m) from the carriageway
        /// edge, so tyres roll up it and bodies don't catch; the kerb band stays drawn crisply on top.</summary>
        public const float KerbRamp = 0.45f;
        /// <summary>Share of the full kerb raise <paramref name="into"/> metres in from the carriageway edge.</summary>
        public static float Ramp(float into) { float t = Mathf.Clamp01(into / KerbRamp); return t * t * (3f - 2f * t); }
        // terrain feature codes (GroundSample.feature, appended after the site codes 1-6)
        public const byte FSidewalk = 7, FPlaza = 8, FPark = 9, FYard = 10, FParking = 11, FIsland = 12, FStreet = 13, FPaint = 14,
            FField = 15, FLot = 16, FKerb = 17, FYellow = 18;

        public readonly Settlement st;
        public readonly Vector2 centre, U, V;
        public readonly float radius, shoulder, angle;
        public readonly int seed;
        readonly float h0, gu, gv;

        public readonly List<TownStreet> streets = new List<TownStreet>();
        public readonly List<TownJunction> junctions = new List<TownJunction>();
        public readonly List<TownLot> lots = new List<TownLot>();
        public readonly List<TownSpot> spots = new List<TownSpot>();
        /// <summary>Sidewalk loops (world x, z corners) residents walk.</summary>
        public readonly List<Vector2[]> beats = new List<Vector2[]>();

        public float Outer => radius + shoulder;
        public Biome Kind => st.kind;

        // ------------------------------------------------------------------ frame

        public Vector2 Local(float x, float z) { float dx = x - centre.x, dz = z - centre.y; return new Vector2(dx * U.x + dz * U.y, dx * V.x + dz * V.y); }
        public Vector2 World(Vector2 l) => centre + U * l.x + V * l.y;
        public Vector2 WorldDir(Vector2 l) => U * l.x + V * l.y;
        /// <summary>Yaw (degrees) that turns a template's +Z along the plan direction <paramref name="dirLocal"/>.</summary>
        public float Yaw(Vector2 dirLocal) { var w = WorldDir(dirLocal); return Mathf.Atan2(w.x, w.y) * Mathf.Rad2Deg; }
        /// <summary>Street level (carriageways); everything else stands <see cref="Kerb"/> higher.</summary>
        public float Level(Vector2 l) => h0 + gu * l.x + gv * l.y;
        public float LevelAt(float x, float z) => Level(Local(x, z));

        // ------------------------------------------------------------------ index

        int n;
        float org;
        int[][] cStreets, cJuncs, cLots;

        int CellOf(Vector2 l)
        {
            int i = Mathf.FloorToInt((l.x - org) / Cell), j = Mathf.FloorToInt((l.y - org) / Cell);
            return i < 0 || j < 0 || i >= n || j >= n ? -1 : j * n + i;
        }

        void BuildIndex()
        {
            float ext = radius + 12f;
            org = -ext; n = Mathf.CeilToInt(2f * ext / Cell);
            var ls = new List<int>[n * n]; var lj = new List<int>[n * n]; var ll = new List<int>[n * n];
            void Stamp(List<int>[] into, int id, Vector2 lo, Vector2 hi)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((lo.x - org) / Cell)), i1 = Mathf.Min(n - 1, Mathf.FloorToInt((hi.x - org) / Cell));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((lo.y - org) / Cell)), j1 = Mathf.Min(n - 1, Mathf.FloorToInt((hi.y - org) / Cell));
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) (into[j * n + i] ??= new List<int>()).Add(id);
            }
            foreach (var s in streets) { float r = s.half + s.walk + 12f; Stamp(ls, s.id, Vector2.Min(s.a, s.b) - Vector2.one * r, Vector2.Max(s.a, s.b) + Vector2.one * r); }
            foreach (var j in junctions) if (j.ring > 0f) { float r = j.ring + j.walk + 1f; Stamp(lj, j.id, j.p - Vector2.one * r, j.p + Vector2.one * r); }
            foreach (var l in lots) Stamp(ll, l.id, l.min - Vector2.one * 0.5f, l.max + Vector2.one * 0.5f);
            cStreets = Pack(ls); cJuncs = Pack(lj); cLots = Pack(ll);
        }

        static int[][] Pack(List<int>[] l)
        {
            var r = new int[l.Length][];
            for (int i = 0; i < l.Length; i++) r[i] = l[i] != null ? l[i].ToArray() : System.Array.Empty<int>();
            return r;
        }

        // ------------------------------------------------------------------ queries (thread-safe: read-only)

        /// <summary>The lot covering a plan point (null: none).</summary>
        public TownLot LotAt(Vector2 l)
        {
            int c = CellOf(l);
            if (c < 0) return null;
            foreach (int id in cLots[c]) if (lots[id].Contains(l)) return lots[id];
            return null;
        }

        /// <summary>Signed lateral (+ = left of a→b), distance along and from the centre line of a street.</summary>
        static float Project(TownStreet s, Vector2 l, out float t, out float lat)
        {
            var r = l - s.a;
            t = Mathf.Clamp(Vector2.Dot(r, s.dir), 0f, s.len);
            lat = s.dir.x * r.y - s.dir.y * r.x;
            return (l - (s.a + s.dir * t)).magnitude;
        }

        /// <summary>What the plan has at a local point. False: plain ground (still graded).</summary>
        public bool Query(Vector2 l, out TownHit hit)
        {
            hit = new TownHit { dist = 999f, lot = -1, raise = Kerb };
            int c = CellOf(l);
            if (c < 0) return false;
            // roundabouts: the island, then the ring carriageway
            foreach (int id in cJuncs[c])
            {
                var j = junctions[id];
                float r = (l - j.p).magnitude;
                if (r < j.island)
                {
                    hit.feature = r > j.island - 0.3f ? FKerb : FIsland; hit.raise = Kerb * Ramp(j.island - r); hit.dist = j.ring - r;   // mountable: drive straight over
                    return true;
                }
                if (r < j.ring)
                {
                    hit.feature = FStreet; hit.raise = 0f; hit.road = hit.paved = true;
                    hit.dist = Mathf.Abs(r - j.Centre); hit.along = Mathf.Atan2(l.y - j.p.y, l.x - j.p.x) * r;
                    return true;
                }
            }
            // streets: carriageway wins over any sidewalk; two carriageways = the junction box (no markings)
            TownStreet best = null, walkS = null;
            float bestT = 0f, bestLat = 0f, bestRatio = 99f, walkD = 9f, walkDist = 0f, nearest = 999f;
            int carr = 0;
            foreach (int id in cStreets[c])
            {
                var s = streets[id];
                float d = Project(s, l, out float t, out float lat);
                if (d < nearest) nearest = d;
                if (d <= s.half)
                {
                    carr++;
                    float ratio = d / s.half + (s.kind == StreetKind.Pedestrian ? 2f : 0f) + (s.paved ? 0f : 1f);   // a paved road wins the junction box
                    if (ratio < bestRatio) { bestRatio = ratio; best = s; bestT = t; bestLat = lat; }
                }
                else if (s.walk > 0f && d <= s.half + s.walk && d - s.half < walkD) { walkD = d - s.half; walkS = s; walkDist = d; }
            }
            hit.dist = nearest;
            if (best != null)
            {
                hit.road = true; hit.paved = best.paved; hit.dist = Mathf.Abs(bestLat); hit.along = bestT; hit.side = bestLat < 0f ? 1f : -1f;
                if (best.kind == StreetKind.Pedestrian) { hit.feature = FPlaza; hit.raise = Kerb * Ramp(Mathf.Min(bestT - EndBox(best.juncA), best.len - bestT - EndBox(best.juncB))); return true; }
                if (!best.paved) { hit.feature = 0; hit.raise = Kerb * 0.5f; return true; }    // a dirt lane: the ground's own track colours
                hit.raise = 0f;
                hit.feature = carr > 1 ? FStreet : Marking(best, l, bestT, bestLat);
                return true;
            }
            foreach (int id in cJuncs[c])
            {
                var j = junctions[id];
                float r = (l - j.p).magnitude;
                if (r < j.ring + j.walk) { hit.feature = r < j.ring + 0.3f ? FKerb : FSidewalk; hit.raise = Kerb * Ramp(r - j.ring); hit.road = hit.paved = true; hit.dist = r - j.Centre; return true; }
            }
            if (walkS != null)
            {
                hit.feature = walkD < 0.3f ? FKerb : FSidewalk; hit.road = hit.paved = true; hit.dist = walkDist;
                // driveways: the kerb drops in front of car parks, fuel stations and yards
                Project(walkS, l, out float wt, out float wlat);
                var nrm = new Vector2(-walkS.dir.y, walkS.dir.x) * (wlat < 0f ? -1f : 1f);
                var behind = LotAt(l + nrm * (walkS.half + walkS.walk + 0.6f - walkDist));
                hit.raise = behind != null && (behind.use == LotUse.Parking || behind.use == LotUse.GasStation || behind.use == LotUse.Warehouse) ? Drive : Kerb * Ramp(walkD);
                return true;
            }
            foreach (int id in cLots[c])
            {
                var lot = lots[id];
                if (!lot.Contains(l)) continue;
                hit.lot = id;
                LotGround(lot, l, ref hit);
                return true;
            }
            return false;
        }

        /// <summary>Where a pedestrian street's ramp starts at a junction (the crossing carriageway's edge); none: far away.</summary>
        float EndBox(int ji) => ji < 0 ? -99f : junctions[ji].ring > 0f ? junctions[ji].ring : junctions[ji].box;

        void LotGround(TownLot lot, Vector2 l, ref TownHit hit)
        {
            lot.Frame(l, out float x, out float y);
            float depth = lot.Depth, w = lot.Width;
            byte f;
            switch (lot.use)
            {
                case LotUse.House: f = Mathf.Abs(x) < 0.7f && y < lot.setback ? FPlaza : FYard; break;
                case LotUse.Shop: f = y < lot.setback + 0.3f ? FPlaza : FLot; break;
                case LotUse.Apartment: f = y < depth * 0.55f ? FPlaza : FYard; break;
                case LotUse.Farm: f = y < 16f ? (Mathf.Abs(x) < 0.7f && y < lot.setback ? FLot : FYard) : FField; break;
                case LotUse.Park:
                {
                    bool path = Mathf.Abs(x) < 1.1f || Mathf.Abs(y - depth * 0.5f) < 1.1f
                                || Mathf.Abs(new Vector2(x, y - depth * 0.5f).magnitude - Mathf.Min(w, depth) * 0.3f) < 0.9f;
                    f = path ? FPlaza : FPark;
                    break;
                }
                case LotUse.Green: f = Mathf.Abs(x) < 0.8f || Mathf.Abs(y - depth * 0.5f) < 0.8f ? FPlaza : FPark; break;
                case LotUse.Plaza: case LotUse.Market: f = FPlaza; break;
                case LotUse.Parking:
                {
                    hit.raise = Drive;
                    bool row = (y > 1f && y < 6f) || (y > depth - 6f && y < depth - 1f && depth > 15f);
                    bool bayLine = row && Mathf.Repeat(x + 100.35f, 2.7f) < 0.2f;
                    bool rowLine = Mathf.Abs(y - 6f) < 0.12f || (depth > 15f && Mathf.Abs(y - (depth - 6f)) < 0.12f);
                    f = (bayLine || rowLine) && Mathf.Abs(x) < w * 0.5f - 0.8f && Wear(l) > 0.2f ? FPaint : FParking;
                    break;
                }
                case LotUse.GasStation: hit.raise = Drive; f = y < depth - 5f ? FParking : FLot; break;
                case LotUse.Warehouse: hit.raise = Drive; f = FLot; break;
                default: f = FLot; break;
            }
            hit.feature = f;
            hit.road = hit.paved = f == FPlaza || f == FParking || f == FPaint;
        }

        static float Wear(Vector2 l)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(l.x * 4f) * 374761393u ^ (uint)Mathf.RoundToInt(l.y * 4f) * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Paint on a carriageway: zebra crossings and stop lines by junctions, give-way dashes at roundabouts,
        /// double yellow centre lines and lane dashes on arterials, dashed centre lines on local streets; a share worn off.</summary>
        byte Marking(TownStreet s, Vector2 l, float t, float lat)
        {
            float alat = Mathf.Abs(lat);
            byte paint = Wear(l) < (st.kind == Biome.City ? 0.18f : 0.28f) ? FStreet : FPaint;
            for (int end = 0; end < 2; end++)
            {
                int ji = end == 0 ? s.juncA : s.juncB;
                if (ji < 0) continue;
                var j = junctions[ji];
                float e = end == 0 ? t : s.len - t;
                bool incoming = end == 0 ? lat > 0f : lat < 0f;                                     // right-hand traffic
                float box = j.ring > 0f ? j.ring : j.box;
                float walk = Mathf.Max(2.2f, j.walk);
                if (j.ring > 0f && incoming && e > box + 0.15f && e < box + 0.5f && Mathf.Repeat(lat + 50f, 0.9f) < 0.45f) return paint;   // give way
                if (!j.zebra) continue;
                float z0 = box + (j.ring > 0f ? 1.6f : 0.3f), z1 = z0 + walk - 0.4f;
                if (e >= z0 && e <= z1) return Mathf.Repeat(lat + 50.25f, 1f) < 0.5f && alat < s.half - 0.2f ? paint : FStreet;
                if (incoming && e > z1 + 0.5f && e < z1 + 0.85f && alat < s.half - 0.3f) return paint;   // stop line
            }
            switch (s.kind)
            {
                case StreetKind.Arterial:
                    if (Mathf.Abs(alat - 0.25f) < 0.16f) return paint == FPaint ? FYellow : FStreet;
                    if (s.half >= 4.8f && Mathf.Abs(alat - s.half * 0.5f - 0.1f) < 0.16f && Mathf.Repeat(t, 9f) < 4f) return paint;
                    break;
                case StreetKind.Ring:
                    if (Mathf.Abs(alat - 0.25f) < 0.16f) return paint == FPaint ? FYellow : FStreet;
                    break;
                case StreetKind.Local:
                    if (st.kind != Biome.Village || s.path) { if (alat < 0.16f && Mathf.Repeat(t, 6f) < 3f) return paint; }
                    break;
            }
            return FStreet;
        }

        /// <summary>Grade the land to the plan (WorldGen.Sample, any thread): street level plus kerbs inside, a smooth
        /// shoulder back to the land outside; plan features and road fields where the plan has something.</summary>
        public void Grade(float x, float z, ref float h, ref GroundSample s, ref float wet, float yard)
        {
            float dx = x - centre.x, dz = z - centre.y, d2 = dx * dx + dz * dz, outer = Outer;
            if (d2 >= outer * outer) return;
            float d = Mathf.Sqrt(d2);
            var l = new Vector2(dx * U.x + dz * U.y, dx * V.x + dz * V.y);
            bool on = Query(l, out var q);
            float w = 1f;
            if (d > radius)
            {
                w = Mathf.Clamp01(1f - (d - radius) / shoulder);
                w = w * w * (3f - 2f * w) * (1f - yard);
            }
            float raise = on ? q.raise : Kerb * Mathf.Clamp01((0.6f - s.road) / 0.5f);       // off the plan: eased up from the highway's edge
            h = Mathf.Lerp(h, Level(l) + raise, w);
            if (d < radius) { s.water = float.NaN; s.shore = 0f; }
            if (!on || w < 0.5f) return;
            s.feature = q.feature;
            s.road = q.road ? 1f : 0f;
            s.paved = q.paved;
            s.roadDist = q.road ? q.dist : Mathf.Min(q.dist, s.roadDist);
            s.along = q.along; s.roadSide = q.side;
            if (q.road) wet *= 0.3f;
        }
    }
}
