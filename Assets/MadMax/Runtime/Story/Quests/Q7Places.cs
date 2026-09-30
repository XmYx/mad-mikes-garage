using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // Shared helpers of the A5, A6, B3, B4 and B5 quest hooks. The Q7 prefix keeps them apart from other quests'
    // helpers in the same partial classes.

    public static partial class StoryLibrary
    {
        /// <summary>Anchor names these quests use (the contract checks them, <see cref="StoryAnchors.Validate"/> wants them bound).</summary>
        static void Q7Anchors(params string[] keys) { foreach (var k in keys) if (!Anchors.Contains(k)) Anchors.Add(k); }

        /// <summary>The last alternative of the step costs <paramref name="scrap"/> (a priced talk option).</summary>
        static StepDef Q7Price(this StepDef s, int scrap) { s.any[s.any.Count - 1].price = scrap; return s; }

        /// <summary>The step of quest <paramref name="q"/> with id <paramref name="step"/> (null when there is none).</summary>
        internal static StepDef Q7Step(string q, string step)
        {
            var def = Get(q);
            if (def == null) return null;
            foreach (var s in def.steps) if (s.id == step) return s;
            return null;
        }
    }

    /// <summary>Story-state questions the Q7 quests' cast and hooks ask every frame (cheap: no allocation).</summary>
    public static class Q7State
    {
        /// <summary>The current step of an active quest is one of <paramref name="steps"/>.</summary>
        public static bool At(string q, params string[] steps)
        {
            var def = StoryLibrary.Get(q);
            var s = def != null ? Story.Current(def) : null;
            if (s == null) return false;
            foreach (var id in steps) if (s.id == id) return true;
            return false;
        }

        public static bool Active(string q) => Story.StateOf(q) == Story.State.Active;
        public static bool Done(string q) => Story.StateOf(q) == Story.State.Done;
        public static bool Step(string q, string step) => Story.StepDone(q, step);
        public static string Route(string q, string step) => Story.Route(q, step);

        /// <summary>The first n (0..max) with flag <paramref name="prefix"/>n set, or -1 (numbers kept in the add-only flag set).</summary>
        public static int Number(string prefix, int max = 16)
        {
            for (int n = 0; n <= max; n++) if (Story.Flag(prefix + n)) return n;
            return -1;
        }
    }

    public static partial class StoryAnchors
    {
        static float Q7Dist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Bind a place (height from the generator).</summary>
        static void Q7Set(WorldGen w, string key, Vector3 p, float face)
        {
            p.y = w.Sample(p.x, p.z).height;
            at[key] = p; yaw[key] = face;
        }

        /// <summary>A place laid out from a bound one: <paramref name="x"/> right, <paramref name="z"/> towards its front.</summary>
        static void Q7From(WorldGen w, string key, string from, float x, float z, float turn = 0f)
        {
            if (!at.ContainsKey(from)) return;
            float y0 = yaw[from];
            Q7Set(w, key, at[from] + Quaternion.Euler(0f, y0, 0f) * new Vector3(x, 0f, z), y0 + turn);
        }

        /// <summary>At least <paramref name="d"/> m from every place bound so far and 50 m from the world's landmarks.</summary>
        static bool Q7Free(WorldGen w, Vector3 p, float d)
        {
            foreach (var kv in at) if (Q7Dist(kv.Value, p) < d) return false;
            var here = new Vector2(p.x, p.z);
            foreach (var m in BiomeProps.Landmarks(w)) if (Vector2.Distance(m.pos, here) < 50f) return false;
            return true;
        }

        /// <summary>Open, level ground beside a road between <paramref name="min"/> and <paramref name="max"/> m from
        /// <paramref name="from"/>, <paramref name="gap"/> m clear of every place bound so far; relaxes once, then falls
        /// back to a ring search so a seed never loses the place. Faces the road (or <paramref name="from"/>).</summary>
        static void Q7Place(WorldGen w, Settlement town, string key, Vector3 from, float min, float max, float gap, float half, float clearTown, float clear)
        {
            if (Roadside(w, from, min, max, 20f, p => Clear(town, p, clearTown) && Q7Free(w, p, gap) && Level(w, p, half, 1.6f), out var spot, out var face)
                || Roadside(w, from, min * 0.6f, max * 1.6f, 20f, p => Clear(town, p, clearTown * 0.6f) && Q7Free(w, p, gap * 0.5f) && Level(w, p, half * 0.6f, 2.2f), out spot, out face))
            {
                Q7Set(w, key, spot, face);
                Clearing(key, clear);
                return;
            }
            for (float r = min; r <= max * 2f; r += 20f)
                for (int k = 0; k < 24; k++)
                {
                    var p = from + Quaternion.Euler(0f, k * 15f + 11f, 0f) * Vector3.forward * r;
                    if (!Open(w, p) || !Clear(town, p, clearTown * 0.5f) || !Q7Free(w, p, gap * 0.4f)) continue;
                    var f = from - p;
                    Q7Set(w, key, p, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
                    Clearing(key, clear);
                    return;
                }
            Q7Set(w, key, from + new Vector3(min, 0f, 0f), -90f);
            Clearing(key, clear);
        }

        /// <summary>The first town's centre (the start yard without one).</summary>
        static Vector3 Q7Town(WorldGen w, Settlement town)
        {
            if (town != null) return new Vector3(town.pos.x, 0f, town.pos.y);
            w.Yard(out var o, out _, out _);
            return o;
        }
    }
}
