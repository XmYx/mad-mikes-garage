using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>An airfield's runway lights, still fed by the old solar bank: amber edge lamps every 30 m along both
    /// sides that glow (unlit material) after dusk, and green threshold lights with real point lights at both ends.
    /// Spawned with the hangar piece; the flight HUD adds the approach aid (<see cref="Thresholds"/>).</summary>
    public class RunwayLights : MonoBehaviour
    {
        static readonly Dictionary<string, RunwayLights> bySite = new Dictionary<string, RunwayLights>();
        static Mesh lampMesh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => bySite.Clear();

        readonly List<MeshRenderer> lamps = new List<MeshRenderer>();
        readonly List<Light> glows = new List<Light>();
        Material day, night;
        float check;
        bool lit;

        /// <summary>The two runway thresholds (world, on the ground) and the landing directions over them.</summary>
        public static void Thresholds(Site s, out Vector3 a, out Vector3 b)
        {
            var t = DeformableTerrain.Instance;
            var pa = s.ToWorld(0f, -s.halfLen); var pb = s.ToWorld(0f, s.halfLen);
            a = new Vector3(pa.x, t ? t.HeightNoLoad(pa.x, pa.y) : 0f, pa.y);
            b = new Vector3(pb.x, t ? t.HeightNoLoad(pb.x, pb.y) : 0f, pb.y);
        }

        public static void For(Site s, Transform parent, Material mat)
        {
            if (bySite.TryGetValue(s.Key, out var have) && have) return;
            var go = new GameObject("RunwayLights");
            go.transform.SetParent(parent, true);
            var rl = go.AddComponent<RunwayLights>();
            rl.Build(s, mat);
            bySite[s.Key] = rl;
        }

        static Mesh Lamp()
        {
            if (lampMesh) return lampMesh;
            var g = new VoxelGrid();
            g.Box(0, 0, 0, 0, 2, 0, Pal.Ramp(Pal.Metal, 1));                                       // post
            g.Box(-1, 3, -1, 1, 3, 1, Pal.Ramp(Pal.Ochre, 3));                                      // lens
            g.Bevel();
            return lampMesh = VoxelMesher.Build(g, "RunwayLamp");
        }

        void Build(Site s, Material mat)
        {
            day = mat;
            night = new Material(mat) { name = "RunwayLampLit" };
            night.SetFloat("_Unlit", 1f);
            var t = DeformableTerrain.Instance;
            var mesh = Lamp();
            for (float z = -s.halfLen; z <= s.halfLen + 0.1f; z += 30f)
                foreach (float x in new[] { -s.halfWid - 0.6f, s.halfWid + 0.6f })
                {
                    var p = s.ToWorld(x, z);
                    var lamp = new GameObject("Lamp", typeof(MeshFilter), typeof(MeshRenderer));
                    lamp.transform.SetParent(transform, false);
                    lamp.transform.position = new Vector3(p.x, t ? t.HeightNoLoad(p.x, p.y) : 0f, p.y);
                    lamp.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var r = lamp.GetComponent<MeshRenderer>();
                    r.sharedMaterial = day; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lamps.Add(r);
                }
            Thresholds(s, out var a, out var b);
            foreach (var end in new[] { a, b })
            {
                var l = new GameObject("Threshold").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.position = end + Vector3.up * 1.2f;
                l.type = LightType.Point; l.color = new Color(0.45f, 1f, 0.5f); l.range = 16f; l.intensity = 3f; l.shadows = LightShadows.None;
                l.enabled = false;
                glows.Add(l);
            }
        }

        void Update()
        {
            if ((check -= Time.deltaTime) > 0f) return;
            check = 2f;
            bool want = DayNight.Darkness > 0.35f;
            if (want != lit)
            {
                lit = want;
                foreach (var r in lamps) if (r) r.sharedMaterial = lit ? night : day;
            }
            foreach (var l in glows) if (l) l.enabled = lit && LightBudget.Allowed(l);
        }

        void OnDestroy() { if (night) Destroy(night); }
    }
}
