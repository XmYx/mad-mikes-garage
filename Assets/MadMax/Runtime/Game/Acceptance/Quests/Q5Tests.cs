using System.Collections;
using System.Linq;
using MadMax.Story;

namespace MadMax.Game.Acceptance
{
    /// <summary>Shared checks of the S01, S08, S12, S14, S15 and S23 scenarios.</summary>
    static class Q5Test
    {
        public static bool Route(string quest, string step, string prefix) { var r = MadMax.Story.Story.Route(quest, step); return r != null && r.StartsWith(prefix); }

        /// <summary>Start a quest whose systems are still switched off in the register: through the giver when it is on
        /// offer, else as a disclosed fixture.</summary>
        public static IEnumerator Open(ScenarioContext c, string quest, string giver, string anchor, string offer)
        {
            var g = c.Game;
            yield return H.Walk(c, anchor, 2.5f);
            if (MadMax.Story.Story.StateOf(quest) == MadMax.Story.Story.State.Open)
            {
                yield return H.Until(() => g.CastBody(giver) != null, 5f);
                c.Check(H.Talk(g, g.CastBody(giver), offer), quest + ": the giver's offer");
            }
            else
            {
                var q = StoryLibrary.Get(quest);
                c.Note(quest + " is " + MadMax.Story.Story.StateOf(quest) + "; waiting on: " + string.Join(", ", q.needs.Where(n => !Systems.Ready(n))));
                MadMax.Story.Story.Activate(g, quest);
                c.Fixture(quest + " activated directly (its system is not switched on in Systems.cs yet)");
            }
            yield return H.Until(() => MadMax.Story.Story.StateOf(quest) == MadMax.Story.Story.State.Active, 3f);
            yield return H.Until(() => g.CastBody(giver) != null, 5f);
        }
    }
}
