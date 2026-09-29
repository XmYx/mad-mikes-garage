using System.Threading.Tasks;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The far ground for flying (gaps list: flying sees a small world): while an aircraft is up, a coarse
    /// sheet of terrain out to ~570 m around it — heights and ground colours only, no props, no colliders — fills the
    /// gap between the streamed chunks and the horizon. Sampled on a worker thread, rebuilt as the aircraft travels,
    /// sunk 1.5 m so the real chunks always cover it where they exist. <see cref="Aerial"/> (0..1 with altitude) lets the
    /// camera push its fog out.</summary>
    public class FarTerrain : MonoBehaviour
    {
        const int Grid = 96;
        const float Step = 12f, Snap = 48f, Sink = 1.5f;

        /// <summary>0 on the ground .. 1 at 120 m up in an aircraft.</summary>
        public static float Aerial { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Aerial = 0f;

        GameObject go;
        Mesh mesh;
        Vector3 centre = new Vector3(float.MaxValue, 0f, 0f);
        Task<(Vector3[] v, Color32[] c, int[] t)> job;
        Vector3 jobCentre;

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            var car = g ? g.Current : null;
            var flight = car ? car.GetComponent<MadMax.Vehicles.FlightModel>() : null;
            float want = flight ? Mathf.Clamp01((flight.Altitude - 20f) / 100f) : 0f;
            Aerial = Mathf.MoveTowards(Aerial, want, Time.deltaTime * 0.5f);
            if (go) go.SetActive(Aerial > 0.01f);
            if (!flight || !t || t.World == null) return;
            if (job != null)
            {
                if (!job.IsCompleted) return;
                if (!job.IsFaulted) Apply(job.Result, jobCentre, t);
                job = null;
                return;
            }
            if (Aerial <= 0.01f) return;
            var p = car.transform.position;
            var snapped = new Vector3(Mathf.Round(p.x / Snap) * Snap, 0f, Mathf.Round(p.z / Snap) * Snap);
            if (go && (snapped - centre).sqrMagnitude < 150f * 150f) return;
            var world = t.World;
            float snow = Weather.Snow;
            jobCentre = snapped;
            job = Task.Run(() => Build(world, snapped, snow));
        }

        static (Vector3[] v, Color32[] c, int[] t) Build(WorldGen world, Vector3 c, float snow)
        {
            var h = new float[Grid * Grid];
            var col = new Color32[Grid * Grid];
            float half = (Grid - 1) * Step * 0.5f;
            for (int j = 0; j < Grid; j++)
            for (int i = 0; i < Grid; i++)
            {
                float x = c.x - half + i * Step, z = c.z - half + j * Step;
                var s = world.Sample(x, z);
                bool wet = !float.IsNaN(s.water) && s.water > s.height;
                h[j * Grid + i] = (wet ? s.water : s.height) - Sink;
                col[j * Grid + i] = DeformableTerrain.FarColor(s, x, z, wet, Mathf.Max(snow, Weather.SnowAt(z)));
            }
            // flat-shaded quads like the near terrain; nothing past the ice walls at the poles (they climb steeply)
            bool Inside(int i, int j) { float z = c.z - half + j * Step; return z <= WorldGen.ZNorth - 40f && z >= WorldGen.ZSouth + 40f; }
            int quads = 0;
            for (int j = 0; j < Grid - 1; j++) for (int i = 0; i < Grid - 1; i++) if (Inside(i, j) && Inside(i + 1, j + 1)) quads++;
            var v = new Vector3[quads * 6]; var cc = new Color32[quads * 6]; var t = new int[quads * 6];
            int n = 0;
            for (int j = 0; j < Grid - 1; j++)
            for (int i = 0; i < Grid - 1; i++)
            {
                if (!Inside(i, j) || !Inside(i + 1, j + 1)) continue;
                int k = j * Grid + i;
                var v00 = new Vector3(-half + i * Step, h[k], -half + j * Step);
                var v10 = new Vector3(v00.x + Step, h[k + 1], v00.z);
                var v01 = new Vector3(v00.x, h[k + Grid], v00.z + Step);
                var v11 = new Vector3(v00.x + Step, h[k + Grid + 1], v00.z + Step);
                v[n] = v00; v[n + 1] = v01; v[n + 2] = v11; v[n + 3] = v00; v[n + 4] = v11; v[n + 5] = v10;
                var q = col[k];
                for (int s = 0; s < 6; s++) { cc[n + s] = q; t[n + s] = n + s; }
                n += 6;
            }
            return (v, cc, t);
        }

        void Apply((Vector3[] v, Color32[] c, int[] t) r, Vector3 at, DeformableTerrain terrain)
        {
            if (!go)
            {
                go = new GameObject("FarTerrain", typeof(MeshFilter), typeof(MeshRenderer));
                mesh = new Mesh { name = "FarTerrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = terrain.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            mesh.Clear();
            mesh.SetVertices(r.v); mesh.SetColors(r.c); mesh.SetTriangles(r.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.transform.position = at;
            centre = at;
        }

        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
