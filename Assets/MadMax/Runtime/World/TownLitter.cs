using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Paper litter blowing down a planned town's streets (<see cref="TownPlan"/>): sheets of paper, newspaper
    /// and paper bags tumble and hop downwind past the player, more and higher in gusts; none in still air, out of
    /// town or underground. Pooled, purely visual.</summary>
    public class TownLitter : MonoBehaviour
    {
        const int Max = 14;
        class Sheet { public Transform t; public Vector3 pos; public float phase, spin, life; public bool on; }
        readonly List<Sheet> sheets = new List<Sheet>();
        Mesh[] meshes;
        float spawnT;

        void Build()
        {
            meshes = new Mesh[3];
            if (MadMax.Rendering.HDBits.On)                                                     // HD pack: thin bevelled sheets, a creased paper bag
            {
                for (int v = 0; v < 3; v++)
                {
                    var hb = new MadMax.Rendering.HDShapes();
                    var c = v == 0 ? Pal.Cream[3] : v == 1 ? Pal.Cream[1] : Pal.Ochre[2];
                    if (v == 2)
                    {
                        hb.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.14f, 0.08f, 0.22f), c, 0.01f, Quaternion.Euler(0f, 0f, 6f));
                        hb.Box(new Vector3(0f, 0.085f, -0.08f), new Vector3(0.13f, 0.012f, 0.06f), Pal.Ochre[1], 0.004f, Quaternion.Euler(-25f, 0f, 0f));
                    }
                    else
                    {
                        hb.Box(Vector3.zero, new Vector3(0.17f, 0.004f, 0.27f), c, 0.0015f, Quaternion.Euler(0f, 0f, 4f));
                        hb.Box(new Vector3(0.02f, 0.006f, 0.06f), new Vector3(0.15f, 0.004f, 0.12f), c, 0.0015f, Quaternion.Euler(14f, 8f, 0f));   // folded corner
                        if (v == 1)
                            for (int k = 0; k < 3; k++)
                                hb.Box(new Vector3(-0.05f + k * 0.05f, 0.0025f, -0.02f), new Vector3(0.03f, 0.001f, 0.2f), Pal.Black[2], 0f);   // print columns
                    }
                    meshes[v] = hb.ToMesh("LitterHD" + v);
                }
                return;
            }
            for (int v = 0; v < 3; v++)
            {
                var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Cloth);
                var c = v == 0 ? Pal.Cream[3] : v == 1 ? Pal.Cream[1] : Pal.Ochre[2];
                g.Box(-1, 0, -2, 1, 0, 2, p => v == 1 && (p.z & 1) == 0 && p.x != 0 ? Pal.Black[2] : c);
                if (v == 2) g.Box(-1, 1, -2, 1, 1, -1, Pal.Solid(Pal.Ochre[1]));
                meshes[v] = VoxelMesher.Build(g, "Litter" + v, 0.06f);
            }
        }

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            var terrain = DeformableTerrain.Instance;
            if (!g || !terrain || terrain.World == null || Application.isBatchMode) return;
            Transform f = g.Current ? g.Current.transform : g.Player ? g.Player.transform : null;
            if (!f) return;
            if (meshes == null || !meshes[0]) Build();
            var at = f.position;
            float dt = Time.deltaTime, wind = Fx.Wind.magnitude, gust = WindDust.Gust;
            bool town = terrain.World.InTown(at.x, at.z) && !MadMax.Game.OccluderFade.Underground && Weather.LocalSnow < 0.3f;
            int active = 0;
            foreach (var s in sheets)
            {
                if (!s.on) continue;
                var drift = new Vector3(Mathf.PerlinNoise(s.phase * 0.3f, 3f) - 0.5f, 0f, Mathf.PerlinNoise(s.phase * 0.3f, 7f) - 0.5f) * 1.5f;
                s.pos += (Fx.Wind * (0.5f + gust * 0.7f) + drift) * dt;
                s.phase += dt * (2f + gust * 5f);
                float lift = Mathf.Clamp01((wind - 1f) / 4f);
                float hop = Mathf.Abs(Mathf.Sin(s.phase)) * (0.08f + gust * 0.9f) * lift;
                s.pos.y = terrain.Height(s.pos.x, s.pos.z) + 0.02f + hop;
                s.spin += dt * (40f + gust * 200f) * lift;
                s.t.SetPositionAndRotation(s.pos, Quaternion.Euler(Mathf.Sin(s.phase * 1.3f) * 70f * lift, s.spin, Mathf.Cos(s.phase) * 50f * lift));
                s.life -= dt;
                if (s.life <= 0f || (s.pos - at).sqrMagnitude > 45f * 45f) { s.on = false; s.t.gameObject.SetActive(false); continue; }
                active++;
            }
            if (!town || wind < 1.2f || active >= Max || (spawnT -= dt) > 0f) return;
            spawnT = Random.Range(0.25f, 0.8f) / (0.5f + gust);
            var up = -Fx.Wind.normalized;
            var p = at + up * Random.Range(8f, 26f) + Vector3.Cross(up, Vector3.up) * Random.Range(-18f, 18f);
            var plan = terrain.World.PlanAt(p.x, p.z);
            if (plan == null || !plan.Query(plan.Local(p.x, p.z), out var hit) || !WorldGen.HardTown(hit.feature)) return;   // off the paving
            Sheet sh = null;
            foreach (var s in sheets) if (!s.on) { sh = s; break; }
            if (sh == null)
            {
                var go = new GameObject("Litter", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : terrain.worldPropMaterial ? terrain.worldPropMaterial : g.propMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sh = new Sheet { t = go.transform };
                sheets.Add(sh);
            }
            sh.t.GetComponent<MeshFilter>().sharedMesh = meshes[Random.Range(0, meshes.Length)];
            sh.pos = p; sh.phase = Random.value * 6f; sh.spin = Random.value * 360f; sh.life = Random.Range(8f, 18f); sh.on = true;
            sh.t.gameObject.SetActive(true);
        }

        void OnDestroy() { if (meshes != null) foreach (var m in meshes) if (m) Destroy(m); }
    }
}
