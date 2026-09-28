using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Keeps ground cover (<see cref="DeformableTerrain"/> flora) out of a building's footprint:
    /// the renderer's world bounds, taken once when the building spawns.</summary>
    public class FloraBlocker : MonoBehaviour
    {
        public static readonly List<FloraBlocker> All = new List<FloraBlocker>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public Rect rect;

        public static void Add(GameObject go, float shrink = 0.1f)
        {
            var r = go.GetComponent<Renderer>();
            if (!r) return;
            var b = r.bounds;
            var fb = go.AddComponent<FloraBlocker>();
            fb.rect = Rect.MinMaxRect(b.min.x + shrink, b.min.z + shrink, b.max.x - shrink, b.max.z - shrink);
            if (DeformableTerrain.Instance) DeformableTerrain.Instance.FloraDirty(fb.rect);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
    }
}
