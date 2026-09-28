using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Shared world effects: pooled smoke/dust/steam particles and a ring-buffer skidmark mesh.</summary>
    public class Fx : MonoBehaviour
    {
        static Fx instance;
        /// <summary>Wind drift for smoke (m/s).</summary>
        public static Vector3 Wind = new Vector3(1.2f, 0f, 0.6f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => instance = null;

        ParticleSystem smoke, sparks;
        Mesh skidMesh;
        const int MaxSkids = 1400;
        Vector3[] sv = new Vector3[MaxSkids * 4];
        Color32[] sc = new Color32[MaxSkids * 4];
        int skidHead;
        bool skidDirty;
        readonly Dictionary<int, (Vector3 p, Vector3 side, float t)> lastSkid = new Dictionary<int, (Vector3, Vector3, float)>();

        static Fx I
        {
            get
            {
                if (!instance) instance = new GameObject("Fx").AddComponent<Fx>();
                return instance;
            }
        }

        /// <summary>Shader for runtime-created materials. Shader.Find alone fails in builds (unreferenced shaders are
        /// stripped), so each one is referenced by a material asset in Resources/RuntimeMaterials.</summary>
        public static Shader RuntimeShader(string resource, string fallbackName)
        {
            var m = Resources.Load<Material>("RuntimeMaterials/" + resource);
            return m ? m.shader : Shader.Find(fallbackName);
        }

        public static Material TransparentMaterial(Texture tex)
        {
            var sh = RuntimeShader("ParticlesUnlit", "Universal Render Pipeline/Particles/Unlit");
            var m = new Material(sh);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            if (tex) m.SetTexture("_BaseMap", tex);
            return m;
        }

        static Texture2D puff;
        static Texture2D Puff()
        {
            if (puff) return puff;
            puff = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8, 8)) / 8f;
                float a = d < 0.6f ? 1f : d < 0.85f ? ((x + y) % 2 == 0 ? 1f : 0.5f) : d < 1f ? ((x + y) % 2 == 0 ? 0.45f : 0f) : 0f;
                puff.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            puff.Apply();
            return puff;
        }

        void Awake()
        {
            smoke = MakeSystem("Smoke", 3000, Puff());
            var main = smoke.main;
            main.gravityModifier = -0.02f;
            var sol = smoke.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 1f));
            var col = smoke.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = smoke.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.4f;

            sparks = MakeSystem("Sparks", 800, null);
            var sm = sparks.main; sm.gravityModifier = 1f;
            var sr = sparks.GetComponent<ParticleSystemRenderer>(); sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.04f; sr.lengthScale = 1f;

            skidMesh = new Mesh { name = "Skidmarks" };
            skidMesh.MarkDynamic();
            skidMesh.vertices = sv; skidMesh.colors32 = sc;
            var idx = new int[MaxSkids * 6];
            for (int i = 0; i < MaxSkids; i++) { int v = i * 4, t = i * 6; idx[t] = v; idx[t + 1] = v + 2; idx[t + 2] = v + 1; idx[t + 3] = v + 1; idx[t + 4] = v + 2; idx[t + 5] = v + 3; }
            skidMesh.triangles = idx;
            skidMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            var go = new GameObject("Skidmarks", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = skidMesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = TransparentMaterial(null);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        ParticleSystem MakeSystem(string name, int max, Texture tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false; main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = TransparentMaterial(tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>One smoke / dust / steam puff.</summary>
        public static void Smoke(Vector3 p, Vector3 v, float size, Color c, float life = 2.5f)
        {
            var e = new ParticleSystem.EmitParams { position = p, velocity = v, startSize = size, startLifetime = life * Random.Range(0.8f, 1.2f), startColor = c, rotation = Random.Range(0f, 360f) };
            I.smoke.Emit(e, 1);
        }

        public static void Sparks(Vector3 p, Vector3 dir, int n, Color c)
        {
            for (int i = 0; i < n; i++)
            {
                var e = new ParticleSystem.EmitParams { position = p, velocity = (dir + Random.insideUnitSphere * 0.8f) * Random.Range(2f, 6f), startSize = 0.04f, startLifetime = Random.Range(0.2f, 0.6f), startColor = c };
                I.sparks.Emit(e, 1);
            }
        }

        /// <summary>Continue the skid trail identified by key (one per wheel). intensity 0 ends it.</summary>
        public static void Skid(int key, Vector3 p, Vector3 side, float width, float intensity)
        {
            var fx = I;
            if (intensity <= 0.02f) { fx.lastSkid.Remove(key); return; }
            p += Vector3.up * 0.025f;
            if (fx.lastSkid.TryGetValue(key, out var last) && Time.time - last.t < 0.2f)
            {
                if ((p - last.p).sqrMagnitude < 0.12f * 0.12f) return;
                int v = fx.skidHead * 4;
                Vector3 h0 = last.side * (width * 0.5f), h1 = side * (width * 0.5f);
                fx.sv[v] = last.p - h0; fx.sv[v + 1] = last.p + h0; fx.sv[v + 2] = p - h1; fx.sv[v + 3] = p + h1;
                var col = new Color32(18, 14, 12, (byte)(Mathf.Clamp01(intensity) * 170));
                fx.sc[v] = fx.sc[v + 1] = fx.sc[v + 2] = fx.sc[v + 3] = col;
                fx.skidHead = (fx.skidHead + 1) % MaxSkids;
                fx.skidDirty = true;
            }
            fx.lastSkid[key] = (p, side, Time.time);
        }

        void LateUpdate()
        {
            if (!skidDirty) return;
            skidDirty = false;
            skidMesh.vertices = sv; skidMesh.colors32 = sc;
        }
    }
}
