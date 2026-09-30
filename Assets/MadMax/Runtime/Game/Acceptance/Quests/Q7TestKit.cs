using System.Collections;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Test steps shared by the A5, A6, B3, B4 and B5 scenarios (on top of <see cref="H"/>).</summary>
    static class Q7T
    {
        /// <summary>Build a piece at an anchor's local point, as the build tool would (a disclosed fixture).</summary>
        public static Placeable Put(ScenarioContext c, string anchor, string id, Vector3 local, float turn = 0f, float up = 0f)
        {
            var g = c.Game;
            var r = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f);
            var p = StoryAnchors.Get(anchor) + r * local;
            p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z) + up;
            var pl = FurnitureLibrary.Spawn(id, g.Build.Structures, p, r * Quaternion.Euler(0f, turn, 0f), g.propMaterial);
            c.Fixture("built " + id + " at " + anchor + (pl ? "" : " (FAILED)"));
            return pl;
        }

        /// <summary>Walk to an anchor and wait for a cast member to be standing there.</summary>
        public static IEnumerator Meet(ScenarioContext c, string anchor, float off, string key)
        {
            yield return H.Walk(c, anchor, off);
            yield return H.Until(() => c.Game.CastBody(key) != null, 6f);
        }

        public static bool Say(ScenarioContext c, string key, string prefix) => H.Talk(c.Game, c.Game.CastBody(key), prefix);

        public static IEnumerator Step(string q, string step, float seconds = 4f) => H.Until(() => MadMax.Story.Story.StepDone(q, step), seconds);

        public static IEnumerator Done(string q, float seconds = 5f) => H.Until(() => MadMax.Story.Story.StateOf(q) == MadMax.Story.Story.State.Done, seconds);

        /// <summary>Finish the prerequisites (a disclosed chapter skip) and open the quest: through the giver's offer when it
        /// is on offer, else directly (its system is not flipped to ready yet).</summary>
        public static IEnumerator Open(ScenarioContext c, string q, string[] before)
        {
            var g = c.Game;
            foreach (var id in before) MadMax.Story.Story.Complete(g, id);
            c.Fixture(string.Join(", ", before) + " finished (chapter skip)");
            yield return new WaitForSeconds(1.5f);
            var st = MadMax.Story.Story.StateOf(q);
            if (st != MadMax.Story.Story.State.Active)
            {
                MadMax.Story.Story.Activate(g, q);
                c.Fixture(q + (st == MadMax.Story.Story.State.Open ? " accepted directly (the giver's offer skipped)" : " opened directly (its system is not switched on in the register yet)"));
            }
            yield return new WaitForSeconds(1f);
        }

        /// <summary>A story prop of <paramref name="id"/> within <paramref name="r"/> m of an anchor.</summary>
        public static Placeable Prop(ScenarioContext c, string anchor, string id, float r)
        {
            var a = StoryAnchors.Get(anchor);
            foreach (var p in Placeable.All)
                if (p && p.id == id && c.Game.IsStoryProp(p) && Vector2.Distance(new Vector2(p.transform.position.x, p.transform.position.z), new Vector2(a.x, a.z)) < r) return p;
            return null;
        }

        /// <summary>The journal has a line containing <paramref name="text"/>.</summary>
        public static bool Wrote(string text)
        {
            foreach (var e in Journal.Entries) if (e.text.Contains(text)) return true;
            return false;
        }
    }
}
