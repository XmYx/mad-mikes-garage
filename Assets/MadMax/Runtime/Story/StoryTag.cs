using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Story
{
    /// <summary>Marks a vehicle (or anything) the campaign cares about ("hearse"): Bring goals look for it. Vehicles save
    /// the key (<c>VehicleSave.storyTag</c>).</summary>
    public class StoryTag : MonoBehaviour
    {
        public static readonly List<StoryTag> All = new List<StoryTag>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public string key;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static StoryTag Find(string key) { foreach (var t in All) if (t && t.key == key) return t; return null; }
        public static void Set(GameObject go, string key) { var t = go.GetComponent<StoryTag>(); if (!t) t = go.AddComponent<StoryTag>(); t.key = key; }
    }
}
