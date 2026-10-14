using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Optional blood (setting BLOOD): droplet bursts on injuries and persistent pixel splats on the ground or
    /// floor. Splats stay until rain washes them out (slowly) or a sponge scrubs them (<see cref="Clean"/>).
    /// Pooled quads, oldest recycled beyond <see cref="Max"/>.</summary>
    public class BloodStains : MonoBehaviour
    {
        const int Max = 160;
        static BloodStains instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { instance = null; }

        public static bool Enabled => MadMax.Game.GameSettings.Current.blood && !Application.isBatchMode;

        class Stain { public Transform t; public MeshRenderer r; public float amount; }
        readonly List<Stain> stains = new List<Stain>();
        readonly Material[] mats = new Material[4];
        Mesh quad;
        MaterialPropertyBlock block;
        int next;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        static BloodStains I
        {
            get
            {
                if (!instance) instance = new GameObject("BloodStains").AddComponent<BloodStains>();
                return instance;
            }
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            quad = new Mesh { name = "Splat" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            var rnd = new System.Random(77);
            for (int v = 0; v < mats.Length; v++)
            {
                bool hd = MadMax.Rendering.HDAssets.Enabled;                                   // HD: 8x the pixels, soft rims
                int n = hd ? 128 : 16; float k = n / 16f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, hd) { filterMode = hd ? FilterMode.Trilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Splat" + v };
                var px = new Color32[n * n];
                // a few overlapping blobs + flecks: reads as a splat at pixel resolution
                int blobs = 3 + v;
                var cx = new float[blobs]; var cy = new float[blobs]; var cr = new float[blobs];
                for (int b = 0; b < blobs; b++) { cx[b] = 8 + (float)(rnd.NextDouble() * 2 - 1) * (b == 0 ? 0 : 4.5f); cy[b] = 8 + (float)(rnd.NextDouble() * 2 - 1) * (b == 0 ? 0 : 4.5f); cr[b] = b == 0 ? 4.2f : 1.2f + (float)rnd.NextDouble() * 2f; }
                var fl = new System.Random(77 + v);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / k - 0.5f, fy = (y + 0.5f) / k - 0.5f;
                    bool on = false; float core = 0f, rim = 0f;
                    for (int b = 0; b < blobs; b++) { float d = Mathf.Sqrt((fx - cx[b]) * (fx - cx[b]) + (fy - cy[b]) * (fy - cy[b])); if (d < cr[b]) { on = true; core = Mathf.Max(core, 1f - d / cr[b]); } rim = Mathf.Max(rim, Mathf.Clamp01((cr[b] + 0.6f - d) / 0.6f)); }
                    if (!hd)
                    {
                        if (!on && rnd.NextDouble() < 0.025) on = true;                            // fleck
                        px[y * n + x] = on ? (core > 0.45f ? new Color32(96, 10, 12, 235) : new Color32(128, 18, 16, 215)) : new Color32(0, 0, 0, 0);
                        continue;
                    }
                    float a = on ? Mathf.Lerp(0.84f, 0.92f, core) : rim * 0.84f;
                    var col = Color32.Lerp(new Color32(128, 18, 16, 255), new Color32(96, 10, 12, 255), Mathf.Clamp01((core - 0.3f) * 3f));
                    col.a = (byte)(255f * a);
                    px[y * n + x] = col;
                }
                if (hd)                                                                              // flecks: small round drops
                    for (int f = 0; f < 6; f++)
                    {
                        float dx0 = (float)fl.NextDouble() * n, dy0 = (float)fl.NextDouble() * n, rr = k * (0.4f + (float)fl.NextDouble() * 0.5f);
                        for (int y = Mathf.Max(0, (int)(dy0 - rr - 1)); y < Mathf.Min(n, (int)(dy0 + rr + 2)); y++)
                        for (int x = Mathf.Max(0, (int)(dx0 - rr - 1)); x < Mathf.Min(n, (int)(dx0 + rr + 2)); x++)
                        {
                            float d = Mathf.Sqrt((x - dx0) * (x - dx0) + (y - dy0) * (y - dy0));
                            if (d < rr && px[y * n + x].a < 180) px[y * n + x] = new Color32(128, 18, 16, (byte)(215 * Mathf.Clamp01((rr - d) / 1.5f)));
                        }
                    }
                tex.SetPixels32(px); tex.Apply(hd);
                mats[v] = Fx.TransparentMaterial(tex);
                mats[v].renderQueue = 2460;                                                          // after opaque ground, before other transparents
            }
        }

        Stain Take()
        {
            if (stains.Count < Max)
            {
                var go = new GameObject("Stain", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                var s = new Stain { t = go.transform, r = r };
                stains.Add(s);
                return s;
            }
            var old = stains[next]; next = (next + 1) % Max;
            return old;
        }

        void Place(Vector3 pos, float size)
        {
            Vector3 p = pos, n = Vector3.up;
            if (Physics.Raycast(pos + Vector3.up * 0.8f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && !hit.rigidbody) { p = hit.point; n = hit.normal; }
            else if (DeformableTerrain.Instance) { p.y = DeformableTerrain.Instance.Height(p.x, p.z); n = DeformableTerrain.Instance.Normal(p.x, p.z); }
            var s = Take();
            s.amount = 1f;
            s.t.gameObject.SetActive(true);
            s.t.SetPositionAndRotation(p + n * 0.015f, Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            s.t.localScale = Vector3.one * size;
            s.r.sharedMaterial = mats[Random.Range(0, mats.Length)];
            Tint(s);
        }

        void Tint(Stain s)
        {
            block.SetColor(ColorId, new Color(1f, 1f, 1f, Mathf.Clamp01(s.amount)));
            s.r.SetPropertyBlock(block);
        }

        /// <summary>Injury: a burst of droplets and a splat (severity 0..1).</summary>
        public static void Splash(Vector3 pos, float severity)
        {
            if (!Enabled) return;
            severity = Mathf.Clamp01(severity);
            var fx = DebrisSystem.Instance;
            int drops = 3 + Mathf.RoundToInt(severity * 8f);
            if (fx) for (int i = 0; i < drops; i++)
                fx.EmitPuff(pos + Vector3.up * 1.1f, new Color32(140, 16, 14, 255), 0.04f, Random.insideUnitSphere * 1.6f + Vector3.up * 1.2f, 0.6f);
            I.Place(pos + Random.insideUnitSphere * 0.2f, 0.5f + severity * 0.8f);
            if (severity > 0.4f) I.Place(pos + Random.insideUnitSphere * 0.6f, 0.3f + severity * 0.4f);
        }

        /// <summary>Bleeding: a small drip under the character.</summary>
        public static void Drip(Vector3 pos)
        {
            if (!Enabled) return;
            I.Place(pos + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f)), Random.Range(0.1f, 0.2f));
        }

        /// <summary>Visible stains within the radius.</summary>
        public static int Count(Vector3 pos, float radius)
        {
            if (!instance) return 0;
            int n = 0;
            foreach (var s in instance.stains) if (s.t.gameObject.activeSelf && (s.t.position - pos).sqrMagnitude < radius * radius) n++;
            return n;
        }

        /// <summary>Scrubs stains within the radius; returns how many were removed.</summary>
        public static int Clean(Vector3 pos, float radius)
        {
            if (!instance) return 0;
            int n = 0;
            foreach (var s in instance.stains)
                if (s.t.gameObject.activeSelf && (s.t.position - pos).sqrMagnitude < radius * radius) { s.amount = 0f; s.t.gameObject.SetActive(false); n++; }
            return n;
        }

        void Update()
        {
            if (!Weather.Raining || Weather.Snowing) return;
            float wash = Time.deltaTime / 90f * (0.5f + Weather.Wetness);                          // a good downpour clears the ground in ~1-3 min
            foreach (var s in stains)
            {
                if (!s.t.gameObject.activeSelf) continue;
                s.amount -= wash;
                if (s.amount <= 0f) s.t.gameObject.SetActive(false); else Tint(s);
            }
        }

        void OnDestroy()
        {
            foreach (var m in mats) if (m) { Destroy(m.GetTexture("_BaseMap")); Destroy(m); }
            if (quad) Destroy(quad);
        }
    }
}
