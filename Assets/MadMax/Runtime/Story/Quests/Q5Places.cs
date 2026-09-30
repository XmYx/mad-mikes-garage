using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // Shared place helpers of the S01, S08, S12, S14, S15 and S23 hooks (the Q5 prefix keeps them apart from other
    // quests' helpers). Everything is deterministic from the world seed; a place that can't be found where it should be
    // falls back to a looser search and finally to a fixed offset, so no quest is ever left without its scene.
    public static partial class StoryAnchors
    {
        static void Q5Set(WorldGen w, string key, Vector3 p, float yawDeg)
        {
            p.y = w.Sample(p.x, p.z).height;
            at[key] = p; yaw[key] = yawDeg;
        }

        /// <summary>No bound place (any quest's) within <paramref name="d"/> m.</summary>
        static bool Q5Free(Vector3 p, float d)
        {
            float d2 = d * d;
            foreach (var kv in at)
            {
                float dx = kv.Value.x - p.x, dz = kv.Value.z - p.z;
                if (dx * dx + dz * dz < d2) return false;
            }
            return true;
        }

        /// <summary>The first town's centre (the start yard when the world has no town).</summary>
        static Vector3 Q5Centre(WorldGen w, Settlement town)
        {
            if (town != null) return new Vector3(town.pos.x, 0f, town.pos.y);
            w.Yard(out var o, out _, out _);
            return new Vector3(o.x, 0f, o.z);
        }

        static float Q5Face(Vector3 from, Vector3 to) { var d = to - from; return d.sqrMagnitude < 0.01f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        /// <summary>A scene's square of <paramref name="half"/> m is open ground all over (no road, water, building, site).</summary>
        static bool Q5Dry(WorldGen w, Vector3 p, float half)
        {
            for (float x = -half; x <= half + 0.01f; x += half)
                for (float z = -half; z <= half + 0.01f; z += half)
                    if (!Open(w, p + new Vector3(x, 0f, z))) return false;
            return true;
        }

        /// <summary>Open, level ground on a ring outside the first town (its edge + <paramref name="from"/> .. + <paramref name="to"/> m,
        /// angles from <paramref name="startDeg"/>), the scene's square clear and <paramref name="spacing"/> m from every
        /// bound place; faces the town.</summary>
        static Vector3 Q5Edge(WorldGen w, Settlement town, string key, float from, float to, float startDeg, float half, float spacing)
        {
            var tc = Q5Centre(w, town);
            float r0 = town != null ? town.radius : 40f;
            if (Q5Ring(w, key, tc, r0 + from, r0 + to, startDeg, p => Q5Free(p, spacing) && Level(w, p, half, 1.3f) && Q5Dry(w, p, half) && Clear(town, p, 6f), true)) return at[key];
            if (Q5Ring(w, key, tc, r0 + from, r0 + to * 2f, startDeg, p => Q5Free(p, spacing * 0.5f) && Clear(town, p, 4f), true)) return at[key];
            var fb = tc + Quaternion.Euler(0f, startDeg, 0f) * Vector3.forward * (r0 + from);
            Q5Set(w, key, fb, Q5Face(fb, tc));
            return at[key];
        }

        /// <summary>The first open spot on rings around <paramref name="centre"/> (<paramref name="min"/>..<paramref name="max"/> m,
        /// every 8 m and 10°) that <paramref name="ok"/> accepts; faces the centre (or away from it).</summary>
        static bool Q5Ring(WorldGen w, string key, Vector3 centre, float min, float max, float startDeg, System.Func<Vector3, bool> ok, bool faceIn)
        {
            for (float rr = min; rr <= max; rr += 8f)
                for (int k = 0; k < 36; k++)
                {
                    float ang = (startDeg + k * 10f) * Mathf.Deg2Rad;
                    var p = new Vector3(centre.x + Mathf.Sin(ang) * rr, 0f, centre.z + Mathf.Cos(ang) * rr);
                    if (!Open(w, p) || !ok(p)) continue;
                    Q5Set(w, key, p, faceIn ? Q5Face(p, centre) : Q5Face(centre, p));
                    return true;
                }
            return false;
        }

        /// <summary>A roadside spot <paramref name="min"/>..<paramref name="max"/> m from <paramref name="from"/> (the engine's
        /// <see cref="Roadside"/>: open, fairly level, a few metres off the verge, facing the road), out of the first town
        /// by <paramref name="margin"/> and <paramref name="spacing"/> m from every bound place. Falls back to a looser
        /// search, then to open ground on a ring.</summary>
        static Vector3 Q5Road(WorldGen w, Settlement town, string key, Vector3 from, float min, float max, float spacing, float margin, System.Func<Vector3, bool> extra = null)
        {
            if (Roadside(w, from, min, max, 20f, p => Clear(town, p, margin) && Q5Free(p, spacing) && Q5Dry(w, p, 3f) && (extra == null || extra(p)), out var s, out var f)
                || Roadside(w, from, min * 0.5f, max * 2f, 20f, p => Clear(town, p, margin * 0.5f) && Q5Free(p, spacing * 0.5f), out s, out f))
            {
                Q5Set(w, key, s, f);
                return at[key];
            }
            if (Q5Ring(w, key, from, min, max * 2f, 17f, p => Clear(town, p, margin * 0.5f) && Q5Free(p, spacing * 0.5f), false)) return at[key];
            var fb = from + new Vector3(min, 0f, 0f);
            Q5Set(w, key, fb, 0f);
            return at[key];
        }

        /// <summary>A place relative to another (<paramref name="local"/> in the base place's frame).</summary>
        static void Q5Beside(WorldGen w, string key, string of, Vector3 local, float turn = 0f)
        {
            var b = Get(of); float y = Yaw(of);
            Q5Set(w, key, b + Quaternion.Euler(0f, y, 0f) * local, y + turn);
        }

        /// <summary>Move a place while the game runs (someone walking or riding with the player): cast bodies at it stay
        /// spawned around the player. Rebinding the world puts it back where the hook set it; the quest's tick restores
        /// where it should be from its flags.</summary>
        public static void Q5Move(string key, Vector3 p) { if (at.ContainsKey(key)) at[key] = p; }

        /// <summary>Keep a list of anchor names in the catalogue's list (the contract checks every waypoint is one).</summary>
        internal static void Q5Declare(params string[] keys) { foreach (var k in keys) if (!StoryLibrary.Anchors.Contains(k)) StoryLibrary.Anchors.Add(k); }
    }
}
