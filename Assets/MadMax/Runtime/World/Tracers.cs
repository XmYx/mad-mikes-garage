using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Short-lived bright lines (MG tracers, harpoon flights): a small pool of LineRenderers that fade out.</summary>
    public class Tracers : MonoBehaviour
    {
        static Tracers instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => instance = null;

        class Line { public LineRenderer r; public float life, age; public Color c; }
        readonly List<Line> pool = new List<Line>();

        public static void Add(Vector3 a, Vector3 b, Color c, float life = 0.06f, float width = 0.04f)
        {
            if (!instance) instance = new GameObject("Tracers").AddComponent<Tracers>();
            instance.Spawn(a, b, c, life, width);
        }

        void Spawn(Vector3 a, Vector3 b, Color c, float life, float width)
        {
            Line l = null;
            foreach (var x in pool) if (!x.r.enabled) { l = x; break; }
            if (l == null)
            {
                if (pool.Count >= 48) l = pool[0];
                else
                {
                    var r = new GameObject("Tracer").AddComponent<LineRenderer>();
                    r.transform.SetParent(transform, false);
                    r.sharedMaterial = Fx.TransparentMaterial(null);
                    r.positionCount = 2; r.numCapVertices = 0;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    l = new Line { r = r };
                    pool.Add(l);
                }
            }
            l.r.SetPosition(0, a); l.r.SetPosition(1, b);
            l.r.widthMultiplier = width;
            l.c = c; l.life = life; l.age = 0f;
            l.r.startColor = l.r.endColor = c;
            l.r.enabled = true;
        }

        void Update()
        {
            foreach (var l in pool)
            {
                if (!l.r.enabled) continue;
                l.age += Time.deltaTime;
                if (l.age >= l.life) { l.r.enabled = false; continue; }
                var c = l.c; c.a *= 1f - l.age / l.life;
                l.r.startColor = l.r.endColor = c;
            }
        }
    }
}
