using System.Collections;
using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    /// <summary>Shared steps for the arc C scenarios (story.c1 - story.c5): chapter skips, meeting a cast member where they
    /// stand now, putting a story vehicle somewhere (disclosed), waiting for a step.</summary>
    static class ArcCT
    {
        public static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Finish earlier chapters outright (a disclosed chapter skip).</summary>
        public static void Skip(ScenarioContext c, params string[] ids)
        {
            foreach (var id in ids) StoryState.Complete(c.Game, id);
            c.Fixture(string.Join(", ", ids) + " finished (chapter skip)");
        }

        /// <summary>A quest whose story system the integrator hasn't switched on yet: opened directly (disclosed).</summary>
        public static void Open(ScenarioContext c, string id, string system)
        {
            if (StoryState.StateOf(id) == StoryState.State.Locked) { StoryState.Activate(c.Game, id); c.Fixture(id + " activated directly (story system '" + system + "' not flipped to ready yet)"); }
        }

        /// <summary>Walk (teleport, disclosed) to where a cast member stands right now and wait for their body.</summary>
        public static IEnumerator Meet(ScenarioContext c, string key, float off = 3f)
        {
            var m = StoryCast.Find(key);
            string anchor = m == null ? null : m.Value.anchorNow != null ? m.Value.anchorNow() : m.Value.anchor;
            if (anchor != null && StoryAnchors.Has(anchor)) yield return H.Walk(c, anchor, off);
            yield return H.Until(() => c.Game.CastBody(key) != null, 6f);
        }

        public static bool Say(WastelandGame g, string key, string say) => H.Talk(g, g.CastBody(key), say);

        public static IEnumerator Done(string q, string step, float s = 5f) => H.Until(() => StoryState.StepDone(q, step), s);

        public static VehicleDriver Tagged(string tag) { var t = StoryTag.Find(tag); return t ? t.GetComponent<VehicleDriver>() : null; }

        /// <summary>Set a vehicle down at a point facing <paramref name="fwd"/> (a disclosed placement), resting.</summary>
        public static void Put(ScenarioContext c, VehicleDriver v, Vector3 at, Vector3 fwd, string why)
        {
            if (!v || !v.Body) return;
            var t = MadMax.World.DeformableTerrain.Instance;
            fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            v.Body.position = new Vector3(at.x, (t ? t.Height(at.x, at.z) : at.y) + 0.7f, at.z);
            v.Body.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
            v.Body.WakeUp();
            c.Fixture(v.name + ": " + why + " (placed)");
        }

        /// <summary>The anchor's position with an offset in its own frame (+Z toward the road for roadside places).</summary>
        public static Vector3 At(string anchor, Vector3 local) => StoryAnchors.Get(anchor) + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * local;

        public static Placeable Prop(WastelandGame g, string id, string anchor, float within)
        {
            var a = StoryAnchors.Get(anchor);
            foreach (var p in Placeable.All) if (p && p.id == id && g.IsStoryProp(p) && Flat(p.transform.position, a) < within) return p;
            return null;
        }
    }
}
