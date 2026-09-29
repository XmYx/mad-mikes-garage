using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Smoke clouds from dischargers: billow for ~25 s, drift with the wind and block line of sight
    /// (raider fire through a cloud mostly misses; see Npc.Blast).</summary>
    public class SmokeScreen : MonoBehaviour
    {
        struct Cloud { public Vector3 pos; public float radius, until, puff; }
        static readonly List<Cloud> clouds = new List<Cloud>();
        static SmokeScreen instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { clouds.Clear(); instance = null; }

        public static void Pop(Vector3 at, float radius)
        {
            if (!instance) instance = new GameObject("SmokeScreen").AddComponent<SmokeScreen>();
            clouds.Add(new Cloud { pos = at, radius = radius, until = Time.time + 25f });
            for (int i = 0; i < 24; i++) Fx.Smoke(at + Random.insideUnitSphere * radius * 0.6f, Random.insideUnitSphere * 1.5f + Vector3.up * 0.4f, 1.6f, new Color(0.78f, 0.78f, 0.74f, 0.85f), 6f);
        }

        /// <summary>A cloud lies across the line from a to b.</summary>
        public static bool Blocks(Vector3 a, Vector3 b)
        {
            foreach (var c in clouds)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(c.pos - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                if ((a + ab * t - c.pos).sqrMagnitude < c.radius * c.radius) return true;
            }
            return false;
        }

        void Update()
        {
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var c = clouds[i];
                if (Time.time > c.until) { clouds.RemoveAt(i); continue; }
                c.pos += Fx.Wind * 0.25f * Time.deltaTime;
                // keep it billowing (a few big puffs a second)
                c.puff += Time.deltaTime;
                if (c.puff > 0.3f) { c.puff = 0f; Fx.Smoke(c.pos + Random.insideUnitSphere * c.radius * 0.7f, Vector3.up * 0.3f + Fx.Wind * 0.2f, 1.8f, new Color(0.75f, 0.75f, 0.72f, 0.7f), 5f); }
                clouds[i] = c;
            }
        }
    }
}
