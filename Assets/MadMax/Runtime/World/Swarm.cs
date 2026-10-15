using System;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A few insects about a spot (2026-10-15): bees working in and out of a hive mouth, flies circling a
    /// latrine pit or a carcass. Specks are small shared-mesh cubes (a little oversized to read as a pixel or two) on deterministic flight
    /// paths — no physics, no particles; only animated within <see cref="Reach"/> m of the camera (overhead views sit high). How many fly follows
    /// <see cref="activity"/> (0..1, polled every second).</summary>
    public class Swarm : MonoBehaviour
    {
        public enum Kind { Bees, Flies }
        public Kind kind;
        /// <summary>Mouth / centre in local space and the radius they range over.</summary>
        public Vector3 centre;
        public float radius = 0.8f;
        public int max = 6;
        public Func<float> activity;
        public const float Reach = 50f;
        /// <summary>Specks flying now (tests).</summary>
        public int Flying { get; private set; }

        Transform[] specks;
        float[] seeds;
        float level, pollT;
        static Mesh cube;
        static Material bee, fly;

        public static Swarm Attach(GameObject go, Kind kind, Vector3 centre, float radius, int max, Func<float> activity)
        {
            var s = go.AddComponent<Swarm>();
            s.kind = kind; s.centre = centre; s.radius = radius; s.max = max; s.activity = activity;
            return s;
        }

        void Start()
        {
            if (!cube) { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = t.GetComponent<MeshFilter>().sharedMesh; Destroy(t); }
            var mat = kind == Kind.Bees ? (bee ? bee : bee = Make(new Color(0.55f, 0.42f, 0.08f))) : (fly ? fly : fly = Make(new Color(0.06f, 0.07f, 0.06f)));
            specks = new Transform[max];
            seeds = new float[max];
            var rnd = new System.Random(Mathf.RoundToInt(transform.position.x * 31f + transform.position.z * 17f));
            for (int i = 0; i < max; i++)
            {
                var o = new GameObject("Speck");
                o.transform.SetParent(transform, false);
                o.AddComponent<MeshFilter>().sharedMesh = cube;
                var r = o.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                o.transform.localScale = Vector3.one * (kind == Kind.Bees ? 0.05f : 0.04f);
                o.SetActive(false);
                specks[i] = o.transform;
                seeds[i] = (float)rnd.NextDouble() * 100f;
            }
        }

        static Material Make(Color c)
        {
            var m = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        void Update()
        {
            if (specks == null) return;
            var cam = Camera.main;
            bool near = cam && (cam.transform.position - transform.position).sqrMagnitude < Reach * Reach;
            if ((pollT -= Time.deltaTime) <= 0f) { pollT = 1f; level = activity != null ? Mathf.Clamp01(activity()) : 1f; }
            int want = near ? Mathf.RoundToInt(level * max) : 0;
            Flying = want;
            float t = Time.time;
            for (int i = 0; i < specks.Length; i++)
            {
                var s = specks[i];
                bool on = i < want;
                if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) continue;
                float k = seeds[i];
                Vector3 p;
                if (kind == Kind.Bees)
                {
                    // out from the mouth along a lazy loop and back: a trip every 4-7 s
                    float period = 4f + (k % 3f);
                    float ph = ((t + k) % period) / period;
                    float out1 = Mathf.Sin(ph * Mathf.PI);                                       // 0 at the mouth, 1 at the far end
                    float ang = k * 2.1f + Mathf.Floor((t + k) / period) * 1.7f;
                    var dir = new Vector3(Mathf.Sin(ang), 0.25f + 0.2f * Mathf.Sin(k), Mathf.Cos(ang));
                    p = centre + dir * (radius * out1) + new Vector3(Mathf.Sin(t * 9f + k), Mathf.Sin(t * 11f + k * 2f) * 0.6f, Mathf.Cos(t * 8f + k)) * 0.04f;
                }
                else
                {
                    // flies: tight erratic loops with sudden darts
                    float a = t * (2.5f + k % 1.5f) + k, b = t * (3.7f + k % 1.1f) + k * 0.5f;
                    float dart = Mathf.PerlinNoise(k, t * 0.8f);
                    p = centre + new Vector3(Mathf.Sin(a) * Mathf.Cos(b * 0.5f), 0.3f * Mathf.Sin(b) + 0.15f, Mathf.Cos(a * 1.3f)) * (radius * (0.35f + 0.65f * dart));
                }
                s.localPosition = p;
            }
        }
    }
}
