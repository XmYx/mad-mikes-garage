using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // Shared helpers of the S19-S22 and L1-L5 quest hooks. The Q3 prefix keeps them apart from other quests' helpers in
    // the same partial classes.

    public static partial class StoryLibrary
    {
        /// <summary>The last alternative of the step costs <paramref name="scrap"/> (a priced talk option).</summary>
        static StepDef Q3Price(this StepDef s, int scrap) { s.any[s.any.Count - 1].price = scrap; return s; }

        /// <summary>Anchor names these quests use (the contract checks them, <see cref="StoryAnchors.Validate"/> binds them).</summary>
        static void Q3Anchors(params string[] keys) { foreach (var k in keys) if (!Anchors.Contains(k)) Anchors.Add(k); }
    }

    public static partial class StoryAnchors
    {
        /// <summary>The first town's centre (the start yard without one).</summary>
        static Vector3 Q3Town(WorldGen w, Settlement town)
        {
            if (town != null) return new Vector3(town.pos.x, 0f, town.pos.y);
            w.Yard(out var o, out _, out _);
            return o;
        }

        static void Q3Set(WorldGen w, string key, Vector3 p, float face)
        {
            p.y = w.Sample(p.x, p.z).height;
            at[key] = p; yaw[key] = face;
        }

        static float Q3Dist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>At least <paramref name="d"/> m from every place bound so far (the core scenes and earlier quests).</summary>
        static bool Q3Free(Vector3 p, float d)
        {
            foreach (var kv in at) if (Q3Dist(kv.Value, p) < d) return false;
            return true;
        }

        /// <summary>The ground within <paramref name="r"/> m varies by at most <paramref name="tol"/> m.</summary>
        static bool Q3Level(WorldGen w, Vector3 p, float r, float tol)
        {
            float h0 = w.Sample(p.x, p.z).height, spread = 0f;
            foreach (var c in new[] { new Vector3(r, 0f, r), new Vector3(-r, 0f, r), new Vector3(r, 0f, -r), new Vector3(-r, 0f, -r) })
                spread = Mathf.Max(spread, Mathf.Abs(w.Sample(p.x + c.x, p.z + c.z).height - h0));
            return spread <= tol;
        }

        /// <summary>Open, level ground on rings round <paramref name="c"/> from <paramref name="r0"/> to <paramref name="r1"/> m
        /// (deterministic order from <paramref name="startDeg"/>), <paramref name="keep"/> m clear of every place bound so far;
        /// faces the centre. Falls back to a point on the first ring (false) so a place is always bound.</summary>
        static bool Q3Ring(WorldGen w, Vector3 c, float r0, float r1, float startDeg, float keep, System.Func<Vector3, bool> ok, out Vector3 spot, out float face)
        {
            for (float r = r0; r <= r1; r += 8f)
                for (int k = 0; k < 24; k++)
                {
                    float a = (startDeg + k * 15f) * Mathf.Deg2Rad;
                    var p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    if (!Open(w, p) || !Q3Free(p, keep) || !Q3Level(w, p, 4f, 1.1f) || (ok != null && !ok(p))) continue;
                    spot = p;
                    var f = c - p;
                    face = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    return true;
                }
            float a0 = startDeg * Mathf.Deg2Rad;
            spot = c + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * r0;
            face = startDeg + 180f;
            return false;
        }

        /// <summary>Someone's spot on open ground just outside a settlement (or round <paramref name="fallback"/>).</summary>
        static void Q3Edge(WorldGen w, Settlement st, Vector3 fallback, string key, float startDeg, float keep = 45f)
        {
            var c = st != null ? new Vector3(st.pos.x, 0f, st.pos.y) : fallback;
            float r0 = st != null ? st.radius + 12f : 30f;
            Q3Ring(w, c, r0, r0 + 120f, startDeg, keep, null, out var p, out var f);
            Q3Set(w, key, p, f);
        }

        /// <summary>A roadside spot <paramref name="min"/>..<paramref name="max"/> m from <paramref name="from"/>, clear of
        /// every place so far; else open ground on a ring at that distance; always binds.</summary>
        static void Q3Roadside(WorldGen w, Vector3 from, float min, float max, Settlement town, string key, float keep, float startDeg)
        {
            if (Roadside(w, from, min, max, 14f, p => Clear(town, p, 60f) && Q3Free(p, keep), out var spot, out var face)) { Q3Set(w, key, spot, face); return; }
            Q3Ring(w, from, min, max, startDeg, keep * 0.5f, p => Clear(town, p, 40f), out spot, out face);
            Q3Set(w, key, spot, face);
        }
    }
}
