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
        /// <summary>How far the view carries (fog, far clip): Aerial in the air; ~0.7 in the ground-level perspective
        /// views (first / third person, hood, bumper), where the far terrain fills the distance beyond the chunks.</summary>
        public static float Reach { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Aerial = 0f; Reach = 0f; }

        GameObject go;
        Mesh mesh;
        Vector3 centre = new Vector3(float.MaxValue, 0f, 0f);
        /// <summary>A built sheet; HD: shared vertices (smooth) with the HD terrain's class weights and no road (uv1-3).</summary>
        sealed class Sheet { public Vector3[] v; public Color32[] c; public int[] t; public Vector2[] u1; public Vector4[] u2, u3; }
        Task<Sheet> job;
        Vector3 jobCentre;

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            var car = g ? g.Current : null;
            var flight = car ? car.GetComponent<MadMax.Vehicles.FlightModel>() : null;
            float want = flight ? Mathf.Clamp01((flight.Altitude - 20f) / 100f) : 0f;
            Aerial = Mathf.MoveTowards(Aerial, want, Time.deltaTime * 0.5f);
            var rig = g ? g.cameraRig : null;
            bool ground = rig && !MadMax.Game.TitleSequence.OnMoon && !MadMax.Game.OccluderFade.Underground &&
                          (rig.mode == MadMax.Game.ViewMode.FirstPerson || rig.mode == MadMax.Game.ViewMode.ThirdPerson || rig.mode == MadMax.Game.ViewMode.Hood || rig.mode == MadMax.Game.ViewMode.Bumper);
            Reach = Mathf.MoveTowards(Reach, Mathf.Max(want, ground ? 0.7f : 0f), Time.deltaTime * 0.5f);
            if (go) go.SetActive(Reach > 0.01f);
            var focus = car ? car.transform : g && g.Player ? g.Player.transform : null;
            if (!focus || !t || t.World == null) return;
            if (job != null)
            {
                if (!job.IsCompleted) return;
                if (!job.IsFaulted) Apply(job.Result, jobCentre, t);
                job = null;
                return;
            }
            if (Reach <= 0.01f) return;
            var p = focus.position;
            var snapped = new Vector3(Mathf.Round(p.x / Snap) * Snap, 0f, Mathf.Round(p.z / Snap) * Snap);
            if (go && (snapped - centre).sqrMagnitude < 150f * 150f) return;
            var world = t.World;
            float snow = Weather.Snow;
            jobCentre = snapped;
            bool hd = t.material && t.material.IsKeywordEnabled("_TERRAIN");
            job = Task.Run(() => Build(world, snapped, snow, hd));
        }

        static Sheet Build(WorldGen world, Vector3 c, float snow, bool hd)
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
            if (hd) return BuildHD(h, col, half, Inside);
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
            return new Sheet { v = v, c = cc, t = t };
        }

        /// <summary>HD sheet: one vertex per sample (smooth normals), classes guessed from the ground colour (green →
        /// grass, pale → snow, grey → rock, dark → dirt, else sand).</summary>
        static Sheet BuildHD(float[] h, Color32[] col, float half, System.Func<int, int, bool> inside)
        {
            int n = Grid * Grid;
            var v = new Vector3[n]; var u1 = new Vector2[n]; var u2 = new Vector4[n]; var u3 = new Vector4[n];
            for (int j = 0; j < Grid; j++)
            for (int i = 0; i < Grid; i++)
            {
                int k = j * Grid + i;
                v[k] = new Vector3(-half + i * Step, h[k], -half + j * Step);
                u1[k] = new Vector2(99f, 0f);
                var q = col[k];
                float r = q.r, g = q.g, b = q.b, mx = Mathf.Max(r, Mathf.Max(g, b)), mn = Mathf.Min(r, Mathf.Min(g, b));
                var w0 = Vector4.zero; var w1 = Vector4.zero;
                if (mn > 175f) { }                                                                  // snow: the rest
                else if (g > r * 0.95f && g > b) w0.z = 1f;                                         // grass
                else if (mx - mn < 22f) w0.w = 1f;                                                  // rock / grey
                else if (mx < 110f) w0.y = 1f;                                                      // dirt
                else w0.x = 1f;                                                                     // sand
                u2[k] = w0; u3[k] = w1;
            }
            var tl = new System.Collections.Generic.List<int>();
            for (int j = 0; j < Grid - 1; j++)
            for (int i = 0; i < Grid - 1; i++)
            {
                if (!inside(i, j) || !inside(i + 1, j + 1)) continue;
                int k = j * Grid + i;
                tl.Add(k); tl.Add(k + Grid); tl.Add(k + Grid + 1); tl.Add(k); tl.Add(k + Grid + 1); tl.Add(k + 1);
            }
            return new Sheet { v = v, c = col, t = tl.ToArray(), u1 = u1, u2 = u2, u3 = u3 };
        }

        void Apply(Sheet r, Vector3 at, DeformableTerrain terrain)
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
            mesh.SetVertices(r.v); mesh.SetColors(r.c);
            if (r.u1 != null) { mesh.SetUVs(1, r.u1); mesh.SetUVs(2, r.u2); mesh.SetUVs(3, r.u3); }
            mesh.SetTriangles(r.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.transform.position = at;
            centre = at;
        }

        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
