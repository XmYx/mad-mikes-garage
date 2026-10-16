using System;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Visible water (2026-10-15): a small pool of pixel-sized droplets on ballistic paths — a shower rose, a
    /// sprinkler's turning jet. Smoke puffs were too faint to read at pixel resolution. Shared cube mesh and one Unlit
    /// material; only animated while <see cref="emitting"/> and within <see cref="Reach"/> m of the camera.
    /// 2026-10-16: paths are integrated in ≤ 1/120 s steps with the exact ballistic term (one big step at a low frame
    /// rate used to eat a jet's whole climb, so the sprinkler stayed under 0.7 m), optional air <see cref="drag"/>
    /// (sawdust drifts) and a colour per emitter.</summary>
    public class Droplets : MonoBehaviour
    {
        /// <summary>Emitter: (index, out local start, out local velocity) for a new droplet.</summary>
        public delegate void Source(int i, out Vector3 at, out Vector3 vel);
        public Source source;
        public bool emitting;
        public float size = 0.05f, life = 0.7f, floorY = 0.02f;
        /// <summary>Air drag (1/s): 0 = water drops, ~3 = sawdust that drifts on the air.</summary>
        public float drag;
        /// <summary>Droplets that reached the floor since the emitter was made (sawdust piles count them).</summary>
        public int Landed { get; private set; }
        /// <summary>Called with the local landing point of each droplet that reaches the floor.</summary>
        public Action<Vector3> landed;
        public const float Reach = 50f;
        /// <summary>Droplets in the air now (tests).</summary>
        public int Airborne { get; private set; }

        /// <summary>World position of the first droplet in the air (tests), or null.</summary>
        public Vector3? First { get { if (drops != null) foreach (var d in drops) if (d && d.gameObject.activeSelf) return d.position; return null; } }

        /// <summary>"lowest..highest m, farthest m" of the droplets in the air, local to the emitter (tests).</summary>
        public string Spread()
        {
            float lo = 99f, hi = -99f, far = 0f; int n = 0;
            if (drops != null) for (int i = 0; i < drops.Length; i++) if (drops[i] && drops[i].gameObject.activeSelf)
            { n++; lo = Mathf.Min(lo, pos[i].y); hi = Mathf.Max(hi, pos[i].y); far = Mathf.Max(far, new Vector2(pos[i].x, pos[i].z).magnitude); }
            return n + " drops, y " + lo.ToString("0.00") + ".." + hi.ToString("0.00") + " m, out to " + far.ToString("0.00") + " m";
        }

        Transform[] drops;
        Vector3[] pos, vel;
        float[] age;
        static Mesh cube;
        static readonly System.Collections.Generic.Dictionary<Color32, Material> mats = new System.Collections.Generic.Dictionary<Color32, Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => mats.Clear();
        Color32 tint = new Color32(184, 219, 255, 255);

        public static Droplets Attach(GameObject go, int count, Source source, float size = 0.05f, float life = 0.7f) =>
            Attach(go, count, source, new Color(0.72f, 0.86f, 1f), size, life);

        public static Droplets Attach(GameObject go, int count, Source source, Color color, float size, float life, float drag = 0f)
        {
            var d = go.AddComponent<Droplets>();
            d.source = source; d.size = size; d.life = life; d.drag = drag; d.tint = color;
            d.Build(count);
            return d;
        }

        /// <summary>The ballistic step with gravity <paramref name="g"/> over <paramref name="dt"/>, in sub-steps of at
        /// most 1/120 s (exact for drag 0 at any frame rate).</summary>
        public static void Step(ref Vector3 p, ref Vector3 v, Vector3 g, float drag, float dt)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 12);
            float h = dt / n, keep = drag > 0f ? Mathf.Exp(-drag * h) : 1f;
            for (int k = 0; k < n; k++) { p += v * h + g * (0.5f * h * h); v = (v + g * h) * keep; }
        }

        void Build(int count)
        {
            if (!cube) { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = t.GetComponent<MeshFilter>().sharedMesh; Destroy(t); }
            if (!mats.TryGetValue(tint, out var water) || !water)
            {
                water = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                water.SetColor("_BaseColor", tint);
                mats[tint] = water;
            }
            drops = new Transform[count]; pos = new Vector3[count]; vel = new Vector3[count]; age = new float[count];
            for (int i = 0; i < count; i++)
            {
                var o = new GameObject("Drop");
                o.transform.SetParent(transform, false);
                o.AddComponent<MeshFilter>().sharedMesh = cube;
                var r = o.AddComponent<MeshRenderer>();
                r.sharedMaterial = water;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                o.transform.localScale = Vector3.one * size;
                o.SetActive(false);
                drops[i] = o.transform;
                age[i] = life * i / count;                                                   // staggered so the stream is even
            }
        }

        void Update()
        {
            if (drops == null) return;
            var cam = Camera.main;
            bool near = cam && (cam.transform.position - transform.position).sqrMagnitude < Reach * Reach;
            float dt = Time.deltaTime;
            var g = transform.InverseTransformDirection(Vector3.down) * 9.81f;
            int air = 0;
            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                bool alive = d.gameObject.activeSelf;
                if (alive)
                {
                    age[i] += dt;
                    Step(ref pos[i], ref vel[i], g, drag, dt);
                    if (pos[i].y < floorY) { alive = false; Landed++; if (landed != null) landed(new Vector3(pos[i].x, floorY, pos[i].z)); }
                    else if (age[i] > life) alive = false;
                }
                else age[i] += dt;
                if (!alive && emitting && near && source != null && age[i] >= life)
                {
                    source(i, out pos[i], out vel[i]);
                    age[i] = 0f; alive = true;
                }
                if (d.gameObject.activeSelf != alive) d.gameObject.SetActive(alive);
                if (!alive) continue;
                d.localPosition = pos[i];
                air++;
            }
            Airborne = air;
        }
    }
}
