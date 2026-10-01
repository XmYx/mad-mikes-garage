using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A river: a polyline running downhill from high ground into a lake or a basin, with the water level and
    /// channel half-width per point (it widens downstream).</summary>
    public class River
    {
        public Vector2[] pts;
        public float[] level, half;
        public int index;
    }

    /// <summary>Rivers (gaps list: variety for driving). Each starts on high ground and walks down the slope with a
    /// meander until it reaches a lake, a basin it cannot leave, a settlement or the rim. The bed is carved into the
    /// terrain with sloping banks; the water level steps down along the course (never up). In the desert the beds are
    /// dry wadis. Dirt tracks cross at fords (the road dips into the water: snorkels matter); highways cross on graded
    /// causeways. Built once in the constructor, read-only afterwards (Sample runs on worker threads).</summary>
    public partial class WorldGen
    {
        public readonly List<River> rivers = new List<River>();
        const float RiverCell = 48f, RiverStep = 16f, BankWidth = 7f;
        readonly Dictionary<Vector2Int, List<Vector2Int>> riverGrid = new Dictionary<Vector2Int, List<Vector2Int>>();   // (river, segment)

        void BuildRivers(System.Random r)
        {
            int want = 14;
            float rx = HalfX - Meridian - 500f, rz0 = ZOfLatitude(-60f), rz1 = ZOfLatitude(62f);
            for (int attempt = 0; attempt < 2500 && rivers.Count < want; attempt++)
            {
                // a high start on a continent, away from towns and the start
                var start = new Vector2(((float)r.NextDouble() * 2f - 1f) * rx, Mathf.Lerp(rz0, rz1, (float)r.NextDouble()));
                if (!Habitable(start.x, start.y, 0.64f) || BaseHeight(start.x, start.y) < 2f || NearTown(start, 80f) || (start - new Vector2(yardP.x, yardP.z)).magnitude < 200f) continue;
                var pts = new List<Vector2> { start };
                var dir = -Gradient(start);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
                dir.Normalize();
                int uphill = 0;
                float phase = (float)r.NextDouble() * 100f;
                while (pts.Count < 150)
                {
                    var p = pts[pts.Count - 1];
                    var g = -Gradient(p);
                    if (g.sqrMagnitude > 1e-6f) dir = Vector2.Lerp(dir, g.normalized, 0.35f).normalized;
                    var side = new Vector2(-dir.y, dir.x);
                    float meander = (Mathf.PerlinNoise(phase + pts.Count * 0.12f, 7.3f) - 0.5f) * 1.1f;
                    var q = p + (dir + side * meander).normalized * RiverStep;
                    if (BaseHeight(q.x, q.y) < SeaLevel + 0.3f) { pts.Add(q); break; }                   // out to sea
                    if (!Habitable(q.x, q.y, 0.5f) || NearTown(q, 45f) || (q - new Vector2(yardP.x, yardP.z)).magnitude < 110f) break;
                    uphill = BaseHeight(q.x, q.y) > BaseHeight(p.x, p.y) + 0.2f ? uphill + 1 : 0;
                    if (uphill >= 9) break;                                                         // a basin it cannot leave
                    pts.Add(q);
                    if (LakeAt(q.x, q.y, out float t) != null && t < 0.9f) break;                  // into the lake
                    dir = (q - p).normalized;
                }
                if (pts.Count < 22) continue;                                                       // at least 350 m
                bool crossesOther = false;
                foreach (var o in rivers) foreach (var op in o.pts) if ((op - pts[pts.Count / 2]).sqrMagnitude < 150f * 150f) crossesOther = true;
                if (crossesOther) continue;
                var river = new River { pts = pts.ToArray(), level = new float[pts.Count], half = new float[pts.Count], index = rivers.Count };
                float prev = float.MaxValue;
                for (int i = 0; i < pts.Count; i++)
                {
                    // the ground smoothed along the course, the water a little below it and never climbing
                    float sum = 0f; int n = 0;
                    for (int k = Mathf.Max(0, i - 2); k <= Mathf.Min(pts.Count - 1, i + 2); k++) { sum += BaseHeight(pts[k].x, pts[k].y); n++; }
                    float lvl = Mathf.Max(SeaLevel, Mathf.Min(prev, sum / n - 0.9f));
                    river.level[i] = prev = lvl;
                    river.half[i] = 3f + 3f * i / (pts.Count - 1f);
                }
                rivers.Add(river);
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    float reach = river.half[i + 1] + BankWidth + 2f;
                    var a = pts[i]; var b = pts[i + 1];
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach) / RiverCell), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + reach) / RiverCell);
                    int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach) / RiverCell), z1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + reach) / RiverCell);
                    for (int cx = x0; cx <= x1; cx++)
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        var key = new Vector2Int(cx, cz);
                        if (!riverGrid.TryGetValue(key, out var list)) riverGrid[key] = list = new List<Vector2Int>();
                        list.Add(new Vector2Int(river.index, i));
                    }
                }
            }
        }

        bool NearTown(Vector2 p, float margin)
        {
            foreach (var st in settlements) if ((st.pos - p).magnitude < st.radius + margin) return true;
            return false;
        }

        Vector2 Gradient(Vector2 p)
        {
            const float e = 30f;                                                                   // the lie of the land, not the dunes
            return new Vector2(BaseHeight(p.x + e, p.y) - BaseHeight(p.x - e, p.y), BaseHeight(p.x, p.y + e) - BaseHeight(p.x, p.y - e)) / (2f * e);
        }

        /// <summary>Nearest river course at a point: water level, channel half-width and the distance from the centre line
        /// (false when no river is within its bank).</summary>
        public bool RiverAt(float x, float z, out float level, out float half, out float dist)
        {
            level = half = 0f; dist = float.MaxValue;
            if (!riverGrid.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / RiverCell), Mathf.FloorToInt(z / RiverCell)), out var list)) return false;
            var p = new Vector2(x, z);
            foreach (var rs in list)
            {
                var rv = rivers[rs.x];
                var a = rv.pts[rs.y]; var b = rv.pts[rs.y + 1];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = (a + ab * t - p).magnitude;
                if (d >= dist) continue;
                dist = d;
                level = Mathf.Lerp(rv.level[rs.y], rv.level[rs.y + 1], t);
                half = Mathf.Lerp(rv.half[rs.y], rv.half[rs.y + 1], t);
            }
            return dist < half + BankWidth;
        }

        /// <summary>The current at a point (xz, m/s): downstream along the course, fastest mid-channel and where the bed
        /// drops; zero outside the channel and in dry wadis. Thread-safe.</summary>
        public Vector2 RiverFlow(float x, float z)
        {
            if (!riverGrid.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / RiverCell), Mathf.FloorToInt(z / RiverCell)), out var list)) return Vector2.zero;
            var p = new Vector2(x, z);
            float best = float.MaxValue; River rv = null; int seg = 0; float tt = 0f;
            foreach (var rs in list)
            {
                var r = rivers[rs.x];
                var a = r.pts[rs.y]; var ab = r.pts[rs.y + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = (a + ab * t - p).magnitude;
                if (d < best) { best = d; rv = r; seg = rs.y; tt = t; }
            }
            if (rv == null) return Vector2.zero;
            float half = Mathf.Lerp(rv.half[seg], rv.half[seg + 1], tt);
            if (best > half || NaturalBiome(x, z) == Biome.Desert) return Vector2.zero;
            var dir = (rv.pts[seg + 1] - rv.pts[seg]).normalized;
            float drop = rv.level[seg] - rv.level[seg + 1];
            float speed = Mathf.Clamp(0.5f + drop / RiverStep * 50f, 0.4f, 2.2f);
            float across = best / half;
            return dir * speed * (1f - across * across);
        }

        /// <summary>Carve the river bed and banks into <paramref name="h"/>, set the water (not in the desert: dry wadis).
        /// Returns the ford weight for roads (1 in the channel, fading up the bank).</summary>
        float ShapeRiver(float x, float z, ref float h, ref GroundSample s, ref float wet, out float fordLevel)
        {
            fordLevel = float.NaN;
            if (!RiverAt(x, z, out float level, out float half, out float dist)) return 0f;
            float depth = 0.7f + half * 0.08f;
            if (dist < half)
            {
                float u = dist / half;
                h = Mathf.Min(h, level - depth * (1f - u * u) - 0.05f);
            }
            else
            {
                float u = Mathf.Clamp01((dist - half) / BankWidth);
                u = u * u * (3f - 2f * u);
                h = Mathf.Min(h, Mathf.Lerp(level + 0.1f, h, u));
            }
            bool dry = NaturalBiome(x, z) == Biome.Desert;
            if (!dry) fordLevel = level;                                                          // the track dips towards it across the whole bank
            if (!dry && dist < half + 1.5f && (float.IsNaN(s.water) || s.water < level)) s.water = level;
            if (!dry) { s.shore = Mathf.Max(s.shore, Mathf.Clamp01(1f - Mathf.Abs(dist - half) / 2.5f)); wet = Mathf.Max(wet, Mathf.Clamp01(1.4f - dist / (half + BankWidth))); }
            else wet = Mathf.Max(wet, 0.25f * Mathf.Clamp01(1f - dist / (half + BankWidth)));
            float f = Mathf.Clamp01(1f - (dist - half) / BankWidth);
            return f * f * (3f - 2f * f);
        }
    }
}
