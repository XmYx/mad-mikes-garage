using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Keeps the runtime dressing of the S01-S23 (Q5) story scenes on their saved props, whatever state their
    /// quest is in (the quest ticks only run while a quest is active; the pieces themselves are saved, but runtime parts
    /// such as Hollis's organ on its stage lamp are not). Looks every two seconds; created once per play session.</summary>
    public class Q5Dressing : MonoBehaviour
    {
        float t;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<Q5Dressing>()) return;
            var go = new GameObject("Q5 Story Dressing");
            DontDestroyOnLoad(go);
            go.AddComponent<Q5Dressing>();
        }

        void Update()
        {
            if ((t -= Time.deltaTime) > 0f) return;
            t = 2f;
            var g = WastelandGame.Instance;
            if (g && g.Player) g.Q5Dress();
        }
    }
}
