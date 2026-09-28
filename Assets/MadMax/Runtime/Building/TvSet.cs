using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A placed TV: plays VHS tapes (flickering screen light while running).</summary>
    public class TvSet : MonoBehaviour
    {
        public static readonly List<TvSet> All = new List<TvSet>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => All.Clear();

        public string Playing { get; private set; }
        Light screen;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static TvSet Nearest(Vector3 p, float max)
        {
            TvSet best = null; float bd = max;
            foreach (var t in All) { if (!t) continue; float d = Vector3.Distance(t.transform.position, p); if (d < bd) { bd = d; best = t; } }
            return best;
        }

        public void Play(string id)
        {
            Playing = id;
            if (!screen)
            {
                screen = new GameObject("Screen").AddComponent<Light>();
                screen.transform.SetParent(transform, false);
                screen.transform.localPosition = new Vector3(0f, 0.3f, 0.35f);
                screen.type = LightType.Point; screen.range = 4f; screen.shadows = LightShadows.None;
            }
            screen.enabled = true;
        }

        public void Stop() { Playing = null; if (screen) screen.enabled = false; }

        void Update()
        {
            if (!screen || !screen.enabled) return;
            float t = Time.time * 7f;
            screen.intensity = 1.2f + Mathf.PerlinNoise(t, 0.3f) * 1.4f;
            screen.color = Color.Lerp(new Color(0.55f, 0.7f, 1f), new Color(1f, 0.85f, 0.7f), Mathf.PerlinNoise(0.7f, t * 0.5f));
        }
    }
}
