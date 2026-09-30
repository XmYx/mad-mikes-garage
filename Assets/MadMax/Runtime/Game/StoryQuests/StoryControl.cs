using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A story-set control on a scene prop ([E] / [T] on foot): the player's explicit "we're ready" for an
    /// operation (A5's trailer door, B5's word to the gang). Prompt and use come from the quest's game hook, which
    /// re-attaches the control after a reload (components aren't saved; the prop is).</summary>
    public class StoryControl : MonoBehaviour, MadMax.Building.IInteractable
    {
        public string key;
        public System.Func<WastelandGame, string> prompt;
        public System.Action<WastelandGame, bool> use;

        public string Prompt(WastelandGame g) => prompt != null ? prompt(g) : null;
        public void Use(WastelandGame g, bool secondary) => use?.Invoke(g, secondary);

        /// <summary>The control on <paramref name="host"/> (added once; its delegates refreshed every call).</summary>
        public static StoryControl On(GameObject host, string key, System.Func<WastelandGame, string> prompt, System.Action<WastelandGame, bool> use)
        {
            if (!host) return null;
            var c = host.GetComponent<StoryControl>();
            if (!c) c = host.AddComponent<StoryControl>();
            c.key = key; c.prompt = prompt; c.use = use;
            return c;
        }
    }
}
