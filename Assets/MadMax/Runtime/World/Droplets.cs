using System;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Visible water (2026-10-15): a small pool of pixel-sized droplets on ballistic paths — a shower rose, a
    /// sprinkler's turning jet. Smoke puffs were too faint to read at pixel resolution. Shared cube mesh and one Unlit
    /// material; only animated while <see cref="emitting"/> and within <see cref="Reach"/> m of the camera.</summary>
    public class Droplets : MonoBehaviour
    {
        /// <summary>Emitter: (index, out local start, out local velocity) for a new droplet.</summary>
        public delegate void Source(int i, out Vector3 at, out Vector3 vel);
        public Source source;
        public bool emitting;
        public float size = 0.05f, life = 0.7f, floorY = 0.02f;
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
        static Material water;

        public static Droplets Attach(GameObject go, int count, Source source, float size = 0.05f, float life = 0.7f)
        {
            var d = go.AddComponent<Droplets>();
            d.source = source; d.size = size; d.life = life;
            d.Build(count);
            return d;
        }

        void Build(int count)
        {
            if (!cube) { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = t.GetComponent<MeshFilter>().sharedMesh; Destroy(t); }
            if (!water)
            {
                water = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                water.SetColor("_BaseColor", new Color(0.72f, 0.86f, 1f));
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
                    vel[i] += g * dt;
                    pos[i] += vel[i] * dt;
                    if (age[i] > life || pos[i].y < floorY) alive = false;
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
