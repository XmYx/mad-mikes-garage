using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Shared world effects: pooled smoke/dust/steam particles, a ring-buffer skidmark mesh, fading tracks and
    /// footprints in snow, sand and mud, flash lights (muzzle flashes) and blast shockwave rings (roadmap 16).</summary>
    public class Fx : MonoBehaviour
    {
        static Fx instance;
        /// <summary>Wind drift for smoke (m/s).</summary>
        public static Vector3 Wind = new Vector3(1.2f, 0f, 0.6f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => instance = null;

        ParticleSystem smoke, sparks, streaks;
        Mesh skidMesh;
        const int MaxSkids = 1400;
        Vector3[] sv = new Vector3[MaxSkids * 4];
        Color32[] sc = new Color32[MaxSkids * 4];
        int skidHead;
        bool skidDirty;
        readonly Dictionary<int, (Vector3 p, Vector3 side, float t)> lastSkid = new Dictionary<int, (Vector3, Vector3, float)>();

        // tracks and footprints: their own ring buffer, each quad fading out over TrackLife seconds
        Mesh trackMesh;
        const int MaxTracks = 2400;
        const float TrackLife = 90f;
        readonly Vector3[] tv = new Vector3[MaxTracks * 4];
        readonly Color32[] tc = new Color32[MaxTracks * 4];
        readonly float[] tBorn = new float[MaxTracks];
        readonly byte[] tAlpha = new byte[MaxTracks];
        int trackHead;
        bool trackDirty;
        float fadeTimer;
        readonly Dictionary<int, (Vector3 p, Vector3 side, float t)> lastTrack = new Dictionary<int, (Vector3, Vector3, float)>();

        // flash lights (muzzle flashes, sparks) and shockwave rings
        readonly Light[] flashes = new Light[4];
        readonly float[] flashUntil = new float[4];
        int flashHead;
        readonly List<(Transform t, float born, float radius, Material m)> rings = new List<(Transform, float, float, Material)>();
        static Mesh ringMesh;

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

            // wind-driven streaks (blown sand, sleet): stretched along their velocity, fading in and out
            streaks = MakeSystem("Streaks", 1500, null);
            var stc = streaks.colorOverLifetime; stc.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            stc.color = sg;
            var str = streaks.GetComponent<ParticleSystemRenderer>(); str.renderMode = ParticleSystemRenderMode.Stretch; str.velocityScale = 0.06f; str.lengthScale = 1f;

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

            trackMesh = new Mesh { name = "Tracks" };
            trackMesh.MarkDynamic();
            trackMesh.vertices = tv; trackMesh.colors32 = tc;
            var tidx = new int[MaxTracks * 6];
            for (int i = 0; i < MaxTracks; i++) { int v = i * 4, t = i * 6; tidx[t] = v; tidx[t + 1] = v + 2; tidx[t + 2] = v + 1; tidx[t + 3] = v + 1; tidx[t + 4] = v + 2; tidx[t + 5] = v + 3; }
            trackMesh.triangles = tidx;
            trackMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            var tg = new GameObject("Tracks", typeof(MeshFilter), typeof(MeshRenderer));
            tg.transform.SetParent(transform, false);
            tg.GetComponent<MeshFilter>().sharedMesh = trackMesh;
            var tmr = tg.GetComponent<MeshRenderer>();
            tmr.sharedMaterial = TransparentMaterial(null);
            tmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Ground colour of a fresh track: snow, sand or mud (null alpha = leave none).</summary>
        public static Color32 TrackColor(Vector3 p, out bool any)
        {
            any = false;
            var t = DeformableTerrain.Instance;
            if (!t) return default;
            var s = t.SurfaceAt(p.x, p.z);
            if (Weather.Snow > 0.3f && s.road < 0.5f) { any = true; return new Color32(120, 128, 150, 150); }                  // pressed snow
            if (s.mud > 0.35f) { any = true; return new Color32(40, 26, 16, 150); }
            if (s.softness > 0.35f && t.BiomeAt(p.x, p.z) == Biome.Desert) { any = true; return new Color32(120, 70, 36, 120); } // churned sand
            return default;
        }

        /// <summary>Continue a tyre track (one key per wheel) in soft ground; it fades out over a minute and a half.</summary>
        public static void Track(int key, Vector3 p, Vector3 side, float width)
        {
            var fx = I;
            var col = TrackColor(p, out bool any);
            if (!any) { fx.lastTrack.Remove(key); return; }
            p += Vector3.up * 0.02f;
            if (fx.lastTrack.TryGetValue(key, out var last) && Time.time - last.t < 0.3f)
            {
                if ((p - last.p).sqrMagnitude < 0.2f * 0.2f) return;
                Vector3 h0 = last.side * (width * 0.5f), h1 = side * (width * 0.5f);
                fx.AddQuad(last.p - h0, last.p + h0, p - h1, p + h1, col);
            }
            fx.lastTrack[key] = (p, side, Time.time);
        }

        /// <summary>One footprint (a little offset to the side of the walking line).</summary>
        public static void Footprint(Vector3 p, Vector3 fwd, bool left)
        {
            var col = TrackColor(p, out bool any);
            if (!any) return;
            fwd.y = 0f; fwd.Normalize();
            var side = Vector3.Cross(Vector3.up, fwd);
            p += side * (left ? -0.1f : 0.1f) + Vector3.up * 0.02f;
            Vector3 f = fwd * 0.13f, s = side * 0.05f;
            I.AddQuad(p - f - s, p - f + s, p + f - s, p + f + s, col);
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col)
        {
            int v = trackHead * 4;
            tv[v] = a; tv[v + 1] = b; tv[v + 2] = c; tv[v + 3] = d;
            tc[v] = tc[v + 1] = tc[v + 2] = tc[v + 3] = col;
            tBorn[trackHead] = Time.time; tAlpha[trackHead] = col.a;
            trackHead = (trackHead + 1) % MaxTracks;
            trackDirty = true;
        }

        /// <summary>A short burst of light (muzzle flash, blast): one of four pooled point lights.</summary>
        public static void Flash(Vector3 p, Color c, float range, float intensity, float duration)
        {
            var fx = I;
            int i = fx.flashHead; fx.flashHead = (fx.flashHead + 1) % fx.flashes.Length;
            if (!fx.flashes[i])
            {
                fx.flashes[i] = new GameObject("Flash").AddComponent<Light>();
                fx.flashes[i].transform.SetParent(fx.transform, false);
                fx.flashes[i].type = LightType.Point; fx.flashes[i].shadows = LightShadows.None;
            }
            var l = fx.flashes[i];
            l.transform.position = p; l.color = c; l.range = range; l.intensity = intensity; l.enabled = true;
            fx.flashUntil[i] = Time.time + duration;
        }

        /// <summary>An expanding ring of dust and air along the ground from a blast.</summary>
        public static void Shockwave(Vector3 p, float radius)
        {
            var fx = I;
            if (!ringMesh)
            {
                // flat ring, 32 segments: inner edge clear, outer edge opaque (vertex alpha)
                ringMesh = new Mesh { name = "Shockwave" };
                var v = new List<Vector3>(); var c = new List<Color32>(); var tris = new List<int>();
                for (int k = 0; k <= 32; k++)
                {
                    float a = k / 32f * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d * 0.8f); v.Add(d);
                    c.Add(new Color32(230, 215, 190, 0)); c.Add(new Color32(230, 215, 190, 200));
                    if (k < 32) { int b = k * 2; tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 }); }
                }
                ringMesh.SetVertices(v); ringMesh.SetColors(c); ringMesh.SetTriangles(tris, 0); ringMesh.RecalculateBounds();
            }
            var go = new GameObject("Shockwave", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(fx.transform, false);
            go.transform.position = p + Vector3.up * 0.15f;
            go.GetComponent<MeshFilter>().sharedMesh = ringMesh;
            var m = TransparentMaterial(null);
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.rings.Add((go.transform, Time.time, radius, m));
            for (int i = 0; i < 16; i++)
            {
                var d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                Smoke(p + d * 0.5f + Vector3.up * 0.2f, d * radius * 2.5f + Vector3.up * 0.4f, 0.6f, new Color(0.75f, 0.65f, 0.52f, 0.7f), 1.2f);
            }
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

        /// <summary>One streak flying with the wind (sand in a dust storm).</summary>
        public static void Streak(Vector3 p, Vector3 v, float size, Color c, float life = 1.2f)
        {
            var e = new ParticleSystem.EmitParams { position = p, velocity = v, startSize = size, startLifetime = life * Random.Range(0.8f, 1.2f), startColor = c };
            I.streaks.Emit(e, 1);
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
            if (skidDirty) { skidDirty = false; skidMesh.vertices = sv; skidMesh.colors32 = sc; }
            // tracks fade with age (colours rewritten twice a second)
            if ((fadeTimer -= Time.deltaTime) <= 0f)
            {
                fadeTimer = 0.5f;
                float now = Time.time;
                for (int i = 0; i < MaxTracks; i++)
                {
                    if (tAlpha[i] == 0) continue;
                    float k = 1f - (now - tBorn[i]) / TrackLife;
                    byte a = (byte)(k <= 0f ? 0 : Mathf.RoundToInt(tAlpha[i] * k));
                    int v = i * 4;
                    if (tc[v].a == a) continue;
                    tc[v].a = tc[v + 1].a = tc[v + 2].a = tc[v + 3].a = a;
                    if (a == 0) tAlpha[i] = 0;
                    trackDirty = true;
                }
            }
            if (trackDirty) { trackDirty = false; trackMesh.vertices = tv; trackMesh.colors32 = tc; }
            for (int i = 0; i < flashes.Length; i++)
                if (flashes[i] && flashes[i].enabled && Time.time > flashUntil[i]) flashes[i].enabled = false;
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var (t, born, radius, m) = rings[i];
                float u = (Time.time - born) / 0.45f;
                if (u >= 1f || !t) { if (t) Destroy(t.gameObject); if (m) Destroy(m); rings.RemoveAt(i); continue; }
                t.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2.2f, Mathf.Sqrt(u));
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f - u));
            }
        }
    }
}
