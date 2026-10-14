using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The circuit of a rolling city (roadmap 28): a smooth closed loop through the land past the start yard,
    /// graded like a road (a 48 m crawlerway with a gentle profile), with docks at the stops — graded yards on the
    /// city's right where the pier stands. Built once per world, read-only afterwards (terrain workers query it).</summary>
    public class CityRoute
    {
        public const float Step = 8f;
        /// <summary>Half-width of the flat bed; the shoulder blends back to the land over <see cref="Shoulder"/>.</summary>
        public const float HalfBed = 24f, Shoulder = 14f;
        /// <summary>Dock yard on the right of the travel direction: from the deck edge out, and half its length along.</summary>
        public const float DockOut = 74f, DockHalf = 36f;
        /// <summary>The deck top above the bed (tracks under it), the deck's half-width and half-length.</summary>
        public const float DeckHeight = 6.5f, HalfWidth = 18f, HalfLength = 32f;

        public readonly string name;
        public readonly float halfWidth, halfLength;
        /// <summary>Deck top above the bed, half the distance between the front and rear bogies (the pose chord), half
        /// the graded bed's width and the gap from the deck edge to the pier (clear of the crawlers).</summary>
        public readonly float deckHeight, chord, bedHalf, pierGap;
        /// <summary>The generated tracked city riding this circuit (null: the voxel downtown designs).</summary>
        public readonly TrackedCity.Params tracked;
        public float BedWidth => bedHalf;
        public readonly Vector2[] pts;          // closed polyline (pts[n-1] → pts[0] closes it)
        public readonly float[] cum;            // distance along at each point (cum[n] = Length)
        public readonly float[] prof;           // graded bed height at each point
        public readonly float Length;
        public readonly float[] docks;          // distance along of each stop (the deck centre stops there)
        public readonly string[] dockNames;
        readonly Dictionary<long, int[]> grid;  // 64 m cells → segment indices within reach
        const float Cell = 64f, Reach = 200f;

        public int Count => pts.Length;

        public CityRoute(string name, Vector2[] pts, float[] prof, float[] docks, string[] dockNames, bool industrial = false, TrackedCity.Params tracked = null)
        {
            this.tracked = tracked;
            if (tracked != null)
            {
                halfWidth = tracked.HalfWidth; halfLength = tracked.HalfLength;
                deckHeight = tracked.ZD; chord = tracked.Chord;
                bedHalf = tracked.Reach + 6f; pierGap = tracked.Reach - tracked.HalfWidth + 3f;
            }
            else
            {
                halfWidth = industrial ? CityDesign.IndustrialHX * CityDesign.V : HalfWidth;
                halfLength = industrial ? CityDesign.IndustrialHZ * CityDesign.V : HalfLength;
                deckHeight = DeckHeight; chord = RollingCity.BogieZ;
                bedHalf = Mathf.Max(HalfBed, halfWidth + 6f); pierGap = CityDesign.PierGap;
            }
            this.name = name; this.pts = pts; this.prof = prof; this.docks = docks; this.dockNames = dockNames;
            int n = pts.Length;
            cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector2.Distance(pts[i], pts[(i + 1) % n]);
            Length = cum[n];
            var g = new Dictionary<long, List<int>>();
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - Reach) / Cell), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + Reach) / Cell);
                int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - Reach) / Cell), z1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + Reach) / Cell);
                for (int cx = x0; cx <= x1; cx++) for (int cz = z0; cz <= z1; cz++)
                {
                    long k = Key(cx, cz);
                    if (!g.TryGetValue(k, out var l)) g[k] = l = new List<int>();
                    l.Add(i);
                }
            }
            grid = new Dictionary<long, int[]>();
            foreach (var kv in g) grid[kv.Key] = kv.Value.ToArray();
        }

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public float Wrap(float s) { s %= Length; return s < 0f ? s + Length : s; }

        /// <summary>Shortest distance along the loop between two positions (either way round).</summary>
        public float Gap(float a, float b) { float d = Mathf.Abs(Wrap(a) - Wrap(b)); return Mathf.Min(d, Length - d); }

        /// <summary>Point, graded height and travel direction at distance s along the circuit.</summary>
        public Vector3 At(float s, out Vector3 fwd)
        {
            s = Wrap(s);
            int lo = 0, hi = pts.Length;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (cum[m] <= s) lo = m; else hi = m; }
            int i = lo, j = (i + 1) % pts.Length;
            float t = (s - cum[i]) / Mathf.Max(0.001f, cum[i + 1] - cum[i]);
            var p = Vector2.Lerp(pts[i], pts[j], t);
            var d = (pts[j] - pts[i]).normalized;
            fwd = new Vector3(d.x, 0f, d.y);
            return new Vector3(p.x, Mathf.Lerp(prof[i], prof[j], t), p.y);
        }

        /// <summary>The same chord-centred deck frame is used by the moving hull and its stationary docks.</summary>
        public void DeckPose(float s, out Vector3 position, out Quaternion rotation)
        {
            var front = At(s + chord, out _);
            var rear = At(s - chord, out _);
            var forward = front - rear;
            position = (front + rear) * .5f + Vector3.up * deckHeight;
            rotation = Quaternion.LookRotation(forward.sqrMagnitude > .01f ? forward.normalized : Vector3.forward, Vector3.up);
        }

        public float HeightAt(float s) => At(s, out _).y;

        /// <summary>Nearest point of the circuit within reach: distance along, signed lateral offset (+ = right of travel).</summary>
        public bool Nearest(float x, float z, out float along, out float lateral)
        {
            along = lateral = 0f;
            if (!grid.TryGetValue(Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell)), out var segs)) return false;
            float best = float.MaxValue;
            var q = new Vector2(x, z);
            foreach (int i in segs)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                var ab = b - a; float len2 = ab.sqrMagnitude;
                float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
                var c = a + ab * t;
                float d2 = (q - c).sqrMagnitude;
                if (d2 >= best) continue;
                best = d2;
                along = cum[i] + t * Mathf.Sqrt(len2);
                var dir = len2 > 0f ? ab / Mathf.Sqrt(len2) : Vector2.up;
                lateral = (q.x - c.x) * dir.y - (q.y - c.y) * dir.x;       // right of travel = (dir.y, -dir.x)
            }
            return best < Reach * Reach;
        }

        /// <summary>Grade the land toward the bed and the dock yards (WorldGen.Sample, thread-safe). Returns the weight.</summary>
        public float Grade(float x, float z, ref float h, ref GroundSample s)
        {
            if (!Nearest(x, z, out float along, out float lat)) return 0f;
            float bed = HeightAt(along);
            float sh = Mathf.Clamp(4f + Mathf.Abs(bed - h) * 3.2f, Shoulder, 60f);               // the shoulder widens with the cut / fill (~20°)
            float w = 1f - Mathf.Clamp01((Mathf.Abs(lat) - BedWidth) / sh);
            if (lat > 0f)
                for (int k = 0; k < docks.Length; k++)
                {
                    // the dock yard: flat out to the foot of the ramp on the right
                    float e = Mathf.Max(Gap(along, docks[k]) - DockHalf, lat - (halfWidth + DockOut));
                    float wd = 1f - Mathf.Clamp01(e / Mathf.Clamp(4f + Mathf.Abs(HeightAt(docks[k]) - h) * 3.2f, 10f, 50f));
                    if (wd > w) { w = wd; bed = HeightAt(docks[k]); }
                }
            if (w <= 0f) return 0f;
            w = Mathf.Lerp(w, w * w * (3f - 2f * w), 0.35f);                                    // mostly linear: the gentlest bank for its width
            h = Mathf.Lerp(h, bed, w);
            if (w > 0.85f && s.feature == 0) s.feature = 6;                                   // packed gravel: no props or cover
            s.roadDist = Mathf.Min(s.roadDist, Mathf.Max(0f, Mathf.Abs(lat) - BedWidth));
            return w;
        }
    }

    public partial class WorldGen
    {
        /// <summary>The rolling city's circuit (null: none in this world).</summary>
        public CityRoute city;
        /// <summary>A second independent circuit, carrying the industrial downtown crawler.</summary>
        public CityRoute city2;

        static readonly string[] CityNames = { "BRASSWELL", "OLD MERIDIAN", "GRANITE FALLS", "SAINT RUST", "HAVENWORTH", "IRON CALLOWAY" };

        /// <summary>A loop of radius 500-850 m whose nearest point passes ~170 m beside the start yard, off lakes, sites,
        /// towns, the sea and the yard; the first clean candidate wins (else the least bad). Three docks evenly round it,
        /// the first nearest the yard. The bed profile is the land smoothed over ~200 m with grades under 3 %.</summary>
        void BuildCity()
        {
            city = BuildCityRoute(0, null);
            city2 = BuildCityRoute(1, city);
        }

        CityRoute BuildCityRoute(int index, CityRoute primary)
        {
            var rnd = new System.Random(seed * 977 + 13 + index * 7919);
            int nameIndex = rnd.Next(CityNames.Length);
            if (index > 0 && primary != null && CityNames[nameIndex] == primary.name) nameIndex = (nameIndex + 1) % CityNames.Length;
            string nm = CityNames[nameIndex];
            Vector2 Y = new Vector2(yardP.x, yardP.z), S = new Vector2(yardSide.x, yardSide.z), A = new Vector2(yardDir.x, yardDir.z);
            List<Vector2> best = null; int bestBad = int.MaxValue; Vector2 bestC = default;
            float primarySide = 0f;
            if (primary != null)
            {
                Vector3 primaryNearest = primary.At(ClosestAlong(primary, Y), out _);
                primarySide = Mathf.Sign(Vector2.Dot(new Vector2(primaryNearest.x, primaryNearest.z) - Y, S));
                if (Mathf.Abs(primarySide) < 0.1f) primarySide = 1f;
            }
            for (int attempt = 0; attempt < 24 && bestBad > 0; attempt++)
            {
                float R = 500f + (float)rnd.NextDouble() * 350f;
                float sideSign = primary == null ? (attempt % 2 == 0 ? -1f : 1f) : -primarySide;
                var C = Y + S * sideSign * (R + 170f) + A * (((float)rnd.NextDouble() - 0.5f) * 300f);
                float th0 = Mathf.Atan2(Y.y - C.y, Y.x - C.x);
                var ctrl = new Vector2[12];
                for (int k = 0; k < 12; k++)
                {
                    float r = k % 4 == 0 ? R : R * (1f + ((float)rnd.NextDouble() - 0.5f) * 0.32f);
                    float th = th0 + k * Mathf.PI * 2f / 12f;
                    ctrl[k] = C + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r;
                }
                var loop = Spline(ctrl);
                int bad = 0;
                foreach (var p in loop)
                {
                    if (!Habitable(p.x, p.y, 0.6f) || SiteAt(p.x, p.y) != null) { bad += 3; continue; }
                    if (LakeAt(p.x, p.y, out float t) != null && t < 1.9f) { bad += 3; continue; }
                    foreach (var st in settlements) if (Vector2.Distance(p, st.pos) < st.radius + 45f) { bad++; break; }
                    if (Vector2.Distance(p, Y) < 110f) bad += 3;
                    if (primary != null && TooCloseToRoute(p, primary, 210f)) bad += 3;
                }
                if (bad < bestBad) { bestBad = bad; best = loop; bestC = C; }
            }
            if (best == null) return null;
            if (rnd.NextDouble() < 0.5) best.Reverse();                                        // clockwise or not, as rolled
            int first = 0; float near = float.MaxValue;
            for (int i = 0; i < best.Count; i++) { float d = Vector2.Distance(best[i], Y); if (d < near) { near = d; first = i; } }
            int n = best.Count;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++) pts[i] = best[(first + i) % n];
            // profile: the land under it, smoothed, flat at the docks, slopes limited
            var raw = new float[n];
            for (int i = 0; i < n; i++) raw[i] = Sample(pts[i].x, pts[i].y).height;
            var prof = new float[n];
            const int win = 13;
            for (int i = 0; i < n; i++)
            {
                float sum = 0f, wsum = 0f;
                for (int k = -win; k <= win; k++) { float wk = 1f - Mathf.Abs(k) / (win + 1f); sum += raw[((i + k) % n + n) % n] * wk; wsum += wk; }
                prof[i] = sum / wsum;
            }
            var cum = new float[n]; for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            float total = cum[n - 1] + Vector2.Distance(pts[n - 1], pts[0]);
            var docks = new float[3]; var names = new string[3];
            string[] compass = { "EAST", "NORTH-EAST", "NORTH", "NORTH-WEST", "WEST", "SOUTH-WEST", "SOUTH", "SOUTH-EAST" };
            for (int k = 0; k < 3; k++)
            {
                docks[k] = total * k / 3f;
                int di = 0; while (di < n - 1 && cum[di + 1] <= docks[k]) di++;
                float dh = prof[di];
                for (int j = -7; j <= 7; j++) prof[((di + j) % n + n) % n] = dh;                // flat ±56 m at the stop
                var dp = pts[di] - bestC;
                int ci = ((Mathf.RoundToInt(Mathf.Atan2(dp.y, dp.x) / (Mathf.PI / 4f)) % 8) + 8) % 8;
                names[k] = compass[ci] + " DOCK";
            }
            const float maxGrade = 0.028f;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 1; i < n; i++) { float lim = maxGrade * Vector2.Distance(pts[i - 1], pts[i]); prof[i] = Mathf.Clamp(prof[i], prof[i - 1] - lim, prof[i - 1] + lim); }
                for (int i = n - 2; i >= 0; i--) { float lim = maxGrade * Vector2.Distance(pts[i + 1], pts[i]); prof[i] = Mathf.Clamp(prof[i], prof[i + 1] - lim, prof[i + 1] + lim); }
            }
            for (int i = 0; i < n; i++) prof[i] = Mathf.Max(prof[i], SeaLevel + 1.5f);
            return new CityRoute(nm, pts, prof, docks, names, index == 1, TrackedCity.Profile(index, 0));
        }

        static float ClosestAlong(CityRoute route, Vector2 point)
        {
            float best = float.MaxValue, along = 0f;
            for (int i = 0; i < route.pts.Length; i++)
            {
                Vector2 a = route.pts[i], b = route.pts[(i + 1) % route.pts.Length], ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (point - (a + ab * t)).sqrMagnitude;
                if (d < best) { best = d; along = route.cum[i] + t * ab.magnitude; }
            }
            return along;
        }

        static bool TooCloseToRoute(Vector2 point, CityRoute route, float distance)
        {
            float d2 = distance * distance;
            for (int i = 0; i < route.pts.Length; i++)
            {
                Vector2 a = route.pts[i], b = route.pts[(i + 1) % route.pts.Length], ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                if ((point - (a + ab * t)).sqrMagnitude < d2) return true;
            }
            return false;
        }

        /// <summary>Closed Catmull-Rom through the control points, resampled every 8 m.</summary>
        static List<Vector2> Spline(Vector2[] c)
        {
            var dense = new List<Vector2>();
            int n = c.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = c[(i - 1 + n) % n], p1 = c[i], p2 = c[(i + 1) % n], p3 = c[(i + 2) % n];
                for (int k = 0; k < 40; k++)
                {
                    float t = k / 40f, t2 = t * t, t3 = t2 * t;
                    dense.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
            var outp = new List<Vector2> { dense[0] };
            float acc = 0f;
            for (int i = 1; i <= dense.Count; i++)
            {
                var a = dense[i - 1]; var b = dense[i % dense.Count];
                float d = Vector2.Distance(a, b);
                while (d > 0f && acc + d >= CityRoute.Step)
                {
                    float t = (CityRoute.Step - acc) / d;
                    a = Vector2.Lerp(a, b, t); d = Vector2.Distance(a, b); acc = 0f;
                    outp.Add(a);
                }
                acc += d;
            }
            if (outp.Count > 2 && Vector2.Distance(outp[outp.Count - 1], outp[0]) < CityRoute.Step * 0.5f) outp.RemoveAt(outp.Count - 1);
            return outp;
        }
    }
}
