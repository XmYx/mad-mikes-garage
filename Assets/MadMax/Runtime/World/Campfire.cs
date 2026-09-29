using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A settlement's (or a wanderer's) campfire (roadmap 20 schedules): a stone ring with logs to sit on.
    /// Lit from dusk to dawn — flames, sparks, smoke, a warm flickering light and crackle — where residents gather in
    /// the evening and wanderers sit up through the night. Not a spreading <see cref="Fire"/>.</summary>
    public class Campfire : MonoBehaviour
    {
        public static readonly List<Campfire> All = new List<Campfire>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public const float BenchHeight = 0.36f;
        public int seats = 6;
        public float ring = 1.9f;
        readonly List<bool> taken = new List<bool>();
        Light glow;
        float fxT;
        static Mesh mesh;

        public bool Lit => DayNight.Hours >= 18f || DayNight.Hours < 6.5f;

        public static Campfire Spawn(Vector3 at, int seats, Material mat)
        {
            var go = new GameObject("Campfire", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = at;
            var c = go.AddComponent<Campfire>();
            c.seats = seats;
            if (!mesh) mesh = Build();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            FloraBlocker.Add(go);
            return c;
        }

        static Mesh Build()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Stone);
            for (int i = 0; i < 14; i++)                                                              // stone ring
            {
                float a = i / 14f * Mathf.PI * 2f;
                int x = Mathf.RoundToInt(Mathf.Cos(a) * 5f), z = Mathf.RoundToInt(Mathf.Sin(a) * 5f);
                g.Box(x, 0, z, x, (i & 1), z, Pal.Ramp(Pal.Metal, 2, 2001 + i));
            }
            g.Mat((byte)MadMax.Items.ResourceType.Wood);
            g.Tube(new Vector3(-3, 1, -2), new Vector3(3, 2, 2), 0.7f, Pal.Ramp(Pal.Wood, 0, 2011));            // the firewood
            g.Tube(new Vector3(-3, 1, 2), new Vector3(3, 2, -2), 0.7f, Pal.Ramp(Pal.Wood, 1, 2012));
            g.Box(-2, 0, -2, 2, 0, 2, Pal.Ramp(Pal.Black, 1, 2013));                                              // ash
            g.Set(0, 1, 0, Pal.Solid(Pal.Amber)); g.Set(1, 1, 0, Pal.Solid(Pal.LightY));                          // embers
            return VoxelMesher.Build(g, "Campfire");
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            for (int i = 0; i < seats; i++) taken.Add(false);
            // a log to sit on at each place round the fire
            var logs = new GameObject("Logs", typeof(MeshFilter), typeof(MeshRenderer));
            logs.transform.SetParent(transform, false);
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            for (int i = 0; i < seats; i++)
            {
                var s = transform.InverseTransformPoint(Seat(i)) / VoxelMesher.DefaultSize;
                var side = Quaternion.Euler(0f, i * 360f / seats, 0f) * Vector3.right * 5f;
                g.Tube(new Vector3(s.x, 2, s.z) - side, new Vector3(s.x, 2, s.z) + side, 2.2f, Pal.Ramp(Pal.Wood, 1, 2020 + i));
            }
            logs.GetComponent<MeshFilter>().sharedMesh = VoxelMesher.Build(g, "CampfireLogs");
            logs.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 0.6f;
            glow.type = LightType.Point; glow.color = new Color(1f, 0.55f, 0.22f); glow.range = 7f; glow.shadows = LightShadows.None;
        }

        void OnDestroy()
        {
            var logs = transform.Find("Logs");
            if (logs && logs.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) Destroy(mf.sharedMesh);
        }

        void Update()
        {
            bool lit = Lit && !(Weather.Raining && !Weather.Snowing && Weather.Wetness > 0.8f);
            if (glow)
            {
                glow.enabled = lit && LightBudget.Allowed(glow);
                glow.intensity = 2.4f * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 7f, transform.position.x));
            }
            if (!lit) return;
            MadMax.Audio.Sfx.Loop(this, "fire", 0.35f, 1.1f, 18f);
            if ((fxT -= Time.deltaTime) > 0f) return;
            fxT = 0.12f;
            var p = transform.position + Vector3.up * 0.25f;
            Fx.Smoke(p + Random.insideUnitSphere * 0.15f, Vector3.up * 1.2f + Fx.Wind * 0.3f, Random.Range(0.25f, 0.45f), new Color(1f, Random.Range(0.45f, 0.7f), 0.15f, 0.9f), 0.6f);
            if (Random.value < 0.3f) Fx.Smoke(p + Vector3.up * 0.6f, Vector3.up * 0.8f + Fx.Wind * 0.5f, 0.6f, new Color(0.35f, 0.33f, 0.32f, 0.35f), 3f);
            if (Random.value < 0.15f) Fx.Sparks(p, Vector3.up, 2, new Color(1f, 0.6f, 0.2f));
        }

        /// <summary>Where the i-th sitter sits (on the ground, at the log).</summary>
        public Vector3 Seat(int i)
        {
            float a = i * Mathf.PI * 2f / Mathf.Max(1, seats);
            var p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.Height(p.x, p.z);
            return p;
        }

        public int Claim()
        {
            for (int i = 0; i < taken.Count; i++) if (!taken[i]) { taken[i] = true; return i; }
            return -1;
        }

        public void Free(int i) { if (i >= 0 && i < taken.Count) taken[i] = false; }

        public static Campfire Nearest(Vector3 at, float radius)
        {
            Campfire best = null; float bd = radius * radius;
            foreach (var c in All)
            {
                if (!c) continue;
                float d = (c.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }
    }
}
