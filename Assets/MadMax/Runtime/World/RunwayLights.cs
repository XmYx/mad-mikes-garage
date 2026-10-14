using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>An airfield's runway lights, still fed by the old solar bank: amber edge lamps every 30 m along both
    /// sides that glow (unlit material) after dusk, and green threshold lights with real point lights at both ends.
    /// Kept for airfields within a kilometre of the player (<see cref="Tick"/>), not tied to terrain streaming; the flight
    /// HUD adds the approach aid (<see cref="Thresholds"/>).</summary>
    public class RunwayLights : MonoBehaviour
    {
        static readonly Dictionary<string, RunwayLights> bySite = new Dictionary<string, RunwayLights>();
        static Mesh lampMesh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { bySite.Clear(); nextScan = 0f; }

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
            if (parent) go.transform.SetParent(parent, true);
            var rl = go.AddComponent<RunwayLights>();
            rl.Build(s, mat);
            bySite[s.Key] = rl;
        }

        static float nextScan;
        static readonly List<Site> near = new List<Site>();
        static readonly List<string> drop = new List<string>();

        /// <summary>Lights for every airfield within 1 km of <paramref name="focus"/>, whatever the terrain streaming does
        /// (a pilot sees them from far out); sets further than 1.3 km go away.</summary>
        public static void Tick(WorldGen world, Vector3 focus, Material mat)
        {
            if (world == null || !mat || Time.time < nextScan) return;
            nextScan = Time.time + 2f;
            world.SitesNear(focus, 1000f, near);
            foreach (var s in near) if (s.kind == SiteKind.Airfield) For(s, null, mat);
            drop.Clear();
            foreach (var kv in bySite)
                if (!kv.Value || Vector3.Distance(kv.Value.anchor, new Vector3(focus.x, kv.Value.anchor.y, focus.z)) > 1300f) drop.Add(kv.Key);
            foreach (var k in drop) { if (bySite[k]) Destroy(bySite[k].gameObject); bySite.Remove(k); }
        }

        Vector3 anchor;

        static Mesh Lamp()
        {
            if (MadMax.Rendering.HDBits.On) return MadMax.Rendering.HDBits.RunwayLamp();
            if (lampMesh) return lampMesh;
            var g = new VoxelGrid();
            g.Box(0, 0, 0, 0, 2, 0, Pal.Ramp(Pal.Metal, 1));                                       // post
            g.Box(-1, 3, -1, 1, 3, 1, Pal.Ramp(Pal.Ochre, 3));                                      // lens
            g.Bevel();
            return lampMesh = VoxelMesher.Build(g, "RunwayLamp");
        }

        void Build(Site s, Material mat)
        {
            anchor = new Vector3(s.pos.x, 0f, s.pos.y);
            day = mat;
            if (MadMax.Rendering.HDBits.On) { mat = MadMax.Rendering.HDShapes.Solid; day = mat; }
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
                    lamp.transform.localScale = Vector3.one * 3f;                                   // big enough to read as a dot from the air
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
