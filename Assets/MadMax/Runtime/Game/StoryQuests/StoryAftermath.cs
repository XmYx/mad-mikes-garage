using UnityEngine;

namespace MadMax.Game
{
    /// <summary>What the story leaves running after its quests close (their Tick hooks stop at completion): broadcast
    /// captions, the residents' meals and service (<see cref="MadMax.Story.Residents"/>), the convoy's agreement, Mara
    /// travelling with the player, follow-up radio news, the garage charter. One object for the whole session
    /// (survives scene reloads); it acts on whichever game is running.</summary>
    public class StoryAftermath : MonoBehaviour
    {
        static StoryAftermath instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("StoryAftermath");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<StoryAftermath>();
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Ready || !g.Player) return;
            MadMax.Story.Broadcast.Tick(g);
            if (!MadMax.Story.Story.Campaign) return;
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Game.StoryAftermath");
            g.Q7Aftermath();
            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}
