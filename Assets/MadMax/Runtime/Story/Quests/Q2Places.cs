using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    public static partial class StoryAnchors
    {
        /// <summary>Place-finding shared by S11, S13, S16, S17 and S18: the roads that leave the first town walked by
        /// distance, open spots beside them kept clear of every place bound so far and of the world's landmarks, and a
        /// last-resort ring search so a seed never loses a quest's place.</summary>
        static class Q2
        {
            public static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

            /// <summary>Bind a place (height from the generator).</summary>
            public static void Put(WorldGen w, string key, Vector3 p, float face)
            {
                p.y = w.Sample(p.x, p.z).height;
                at[key] = p; yaw[key] = face;
            }

            /// <summary>No bound place (other than <paramref name="mine"/>) and no landmark within <paramref name="d"/> m.</summary>
            public static bool Free(WorldGen w, Vector3 p, float d, params string[] mine)
            {
                foreach (var kv in at)
                {
                    if (Flat(kv.Value, p) >= d) continue;
                    bool own = false;
                    foreach (var m in mine) if (kv.Key == m) { own = true; break; }
                    if (!own) return false;
                }
                var here = new Vector2(p.x, p.z);
                foreach (var m in BiomeProps.Landmarks(w)) if (Vector2.Distance(m.pos, here) < 45f) return false;
                return true;
            }

            /// <summary>Open ground all round <paramref name="p"/> at <paramref name="r"/> m (no road, water, site or town).</summary>
            public static bool OpenAround(WorldGen w, Vector3 p, float r)
            {
                if (!Open(w, p)) return false;
                for (int k = 0; k < 8; k++)
                    if (!Open(w, p + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * r)) return false;
                return true;
            }

            /// <summary>The roads that leave <paramref name="town"/>, each as a polyline starting at the town end, the
            /// longest stretch before another settlement first (ties by road index: deterministic).</summary>
            public static List<List<Vector3>> RoadsOut(WorldGen w, Settlement town)
            {
                var found = new List<(float len, int idx, List<Vector3> pts)>();
                var tc = new Vector3(town.pos.x, 0f, town.pos.y);
                for (int i = 0; i < w.roads.roads.Count; i++)
                {
                    var r = w.roads.roads[i].points;
                    if (r.Count < 2) continue;
                    bool a = Flat(r[0], tc) < town.radius + 40f, b = Flat(r[r.Count - 1], tc) < town.radius + 40f;
                    if (a == b) continue;                                                         // not from here (or a loop)
                    var pts = new List<Vector3>(r);
                    if (!a) pts.Reverse();
                    float len = 0f;
                    for (int k = 1; k < pts.Count; k++)
                    {
                        var st = w.SettlementAt(pts[k].x, pts[k].z);
                        if (st != null && st != town) break;
                        len += Flat(pts[k], pts[k - 1]);
                    }
                    found.Add((len, i, pts));
                }
                found.Sort((x, y) => x.len != y.len ? y.len.CompareTo(x.len) : x.idx.CompareTo(y.idx));
                var list = new List<List<Vector3>>();
                foreach (var f in found) list.Add(f.pts);
                return list;
            }

            /// <summary>Distance along the road at which it leaves the town's radius.</summary>
            public static float EdgeAlong(List<Vector3> pts, Vector3 centre, float radius)
            {
                float acc = 0f;
                for (int k = 1; k < pts.Count; k++)
                {
                    float len = Flat(pts[k], pts[k - 1]);
                    if (Flat(pts[k], centre) > radius)
                    {
                        float d0 = Flat(pts[k - 1], centre), d1 = Flat(pts[k], centre);
                        return acc + len * Mathf.Clamp01((radius - d0) / Mathf.Max(0.01f, d1 - d0));
                    }
                    acc += len;
                }
                return acc;
            }

            public static float Length(List<Vector3> pts)
            {
                float acc = 0f;
                for (int k = 1; k < pts.Count; k++) acc += Flat(pts[k], pts[k - 1]);
                return acc;
            }

            /// <summary>The road point <paramref name="along"/> m from its start (y = 0) and the heading there.</summary>
            public static bool PointAt(List<Vector3> pts, float along, out Vector3 p, out Vector3 dir)
            {
                float acc = 0f;
                for (int k = 1; k < pts.Count; k++)
                {
                    var a = pts[k - 1]; var b = pts[k];
                    var seg = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    float len = seg.magnitude;
                    if (len < 0.01f) continue;
                    if (acc + len >= along && along >= 0f)
                    {
                        p = new Vector3(a.x, 0f, a.z) + seg * ((along - acc) / len);
                        dir = seg / len;
                        return true;
                    }
                    acc += len;
                }
                p = Vector3.zero; dir = Vector3.forward;
                return false;
            }

            static readonly float[] Shifts = { 0f, 12f, -12f, 24f, -24f, 36f, -36f, 48f, -48f, 64f, -64f, 80f, -80f };
            static readonly float[] Sides = { 12f, -12f, 16f, -16f, 20f, -20f };

            /// <summary>An open, level spot 12-20 m beside the road near <paramref name="along"/> (shifted up to 80 m
            /// either way), clear of other places by <paramref name="gap"/>; faces the road.</summary>
            public static bool Beside(WorldGen w, List<Vector3> pts, float along, float gap, float half, float tol, out Vector3 spot, out float face, params string[] mine)
            {
                foreach (float da in Shifts)
                {
                    if (!PointAt(pts, along + da, out var q, out var dir)) continue;
                    var across = new Vector3(dir.z, 0f, -dir.x);
                    foreach (float off in Sides)
                    {
                        var p = q + across * off;
                        if (!Open(w, p) || !Level(w, p, half, tol) || !Free(w, p, gap, mine)) continue;
                        spot = p;
                        var f = q - p;
                        face = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                        return true;
                    }
                }
                spot = Vector3.zero; face = 0f;
                return false;
            }

            /// <summary>Ring search round <paramref name="from"/> between <paramref name="min"/> and <paramref name="max"/>
            /// m: open, level and clear ground; faces <paramref name="from"/>.</summary>
            public static bool Near(WorldGen w, Vector3 from, float min, float max, float gap, float half, float tol, out Vector3 spot, out float face, params string[] mine)
            {
                for (float r = min; r <= max; r += 12f)
                    for (int k = 0; k < 24; k++)
                    {
                        var p = from + Quaternion.Euler(0f, k * 15f + 7f, 0f) * Vector3.forward * r;
                        if (!Open(w, p) || !Level(w, p, half, tol) || !Free(w, p, gap, mine)) continue;
                        spot = p;
                        var f = from - p;
                        face = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                        return true;
                    }
                spot = Vector3.zero; face = 0f;
                return false;
            }

            /// <summary>Somewhere, whatever the seed: the first open spot on a widening ring, else a fixed offset.</summary>
            public static Vector3 Anywhere(WorldGen w, Vector3 from, float r0, float angle, out float face)
            {
                for (float r = r0; r <= r0 + 600f; r += 20f)
                    for (int k = 0; k < 18; k++)
                    {
                        var p = from + Quaternion.Euler(0f, angle + k * 20f, 0f) * Vector3.forward * r;
                        if (!Open(w, p)) continue;
                        var f = from - p;
                        face = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                        return p;
                    }
                face = angle + 180f;
                return from + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * r0;
            }
        }
    }
}
