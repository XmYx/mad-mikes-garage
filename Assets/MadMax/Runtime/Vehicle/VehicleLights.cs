using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Headlights (spots) and tail/brake lights, placed from the body bounds. Mode: auto (on at night while
    /// driven), on, off — N while driving. Brake lights flare with the brake.</summary>
    public class VehicleLights : MonoBehaviour
    {
        public int mode;                       // 0 auto, 1 on, 2 off
        public static readonly string[] ModeNames = { "LIGHTS AUTO", "LIGHTS ON", "LIGHTS OFF" };
        /// <summary>Headlights lit (light bars and searchlights follow).</summary>
        public bool On { get; private set; }
        int broken;                            // VehicleBreakables bits: 1 front-left, 2 front-right, 4 rear-left, 8 rear-right

        /// <summary>Lamps knocked out: a broken headlamp dims the beam (both gone: dark), a broken tail lamp stays off.</summary>
        public void SetBroken(int bits) { broken = bits; beamShown = -1f; }
        Light[] heads, tails;
        VehicleDriver driver;
        // HD models: lamp lenses glow through the material's emission (MadMax/HDLit _LampOn), per lamp kind
        Renderer[] lampHeads, lampTails, lampAmbers;
        MaterialPropertyBlock lampBlock;
        float shownHead = -1f, shownTail = -1f;
        // light beams: cones you see in rain, fog and dust (one mesh per vehicle, alpha by visibility)
        Transform[] beams;
        Mesh beamMesh;
        Color32[] beamCols;
        float beamShown = -1f;
        static Material beamMat;

        void Start()
        {
            driver = GetComponent<VehicleDriver>();
            var body = transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh || !driver || !driver.driveable) { enabled = false; return; }
            var b = mf.sharedMesh.bounds;
            float y = Mathf.Lerp(b.min.y, b.max.y, 0.3f), hx = b.extents.x * 0.7f;
            heads = new Light[2]; tails = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -hx : hx;
                heads[i] = Make("Headlight", body, new Vector3(x, y, b.max.z + 0.05f), Quaternion.Euler(8f, 0f, 0f), LightType.Spot, new Color(1f, 0.93f, 0.8f), 28f, 60f);
                tails[i] = Make("Taillight", body, new Vector3(x, y, b.min.z - 0.08f), Quaternion.identity, LightType.Point, new Color(1f, 0.1f, 0.05f), 2f, 0.4f);
            }
            heads[1].enabled = false;                       // one spot per vehicle carries the beam (light budget), both tail lamps glow
            heads[0].transform.localPosition = new Vector3(0f, y, b.max.z + 0.05f);
            heads[0].spotAngle = 75f;
            BuildBeams(body, y, hx, b.max.z);
            FindLamps(body);
        }

        void FindLamps(Transform body)
        {
            var h = new System.Collections.Generic.List<Renderer>(); var t = new System.Collections.Generic.List<Renderer>(); var a = new System.Collections.Generic.List<Renderer>();
            foreach (var r in (body.parent ? body.parent : body).GetComponentsInChildren<Renderer>(true))      // body lamps and HD lamp parts (light bars, beacons)
            {
                if (!MadMax.Rendering.HDModel.IsLamp(r.transform)) continue;
                (r.name == "Lamp_Tail" ? t : r.name == "Lamp_Amber" ? a : h).Add(r);
            }
            if (h.Count + t.Count + a.Count == 0) return;
            lampHeads = h.ToArray(); lampTails = t.ToArray(); lampAmbers = a.ToArray();
            lampBlock = new MaterialPropertyBlock();
            SetLamps(lampAmbers, 0f);
        }

        void SetLamps(Renderer[] rs, float v)
        {
            if (rs == null) return;
            foreach (var r in rs)
            {
                if (!r) continue;
                r.GetPropertyBlock(lampBlock);
                lampBlock.SetFloat(MadMax.Rendering.HDModel.LampOnId, v);
                r.SetPropertyBlock(lampBlock);
            }
        }

        void BuildBeams(Transform body, float y, float hx, float front)
        {
            const int seg = 12;
            var v = new Vector3[(seg + 1) * 2];
            beamCols = new Color32[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.55f, 0f);
                v[i * 2] = d * 0.12f; v[i * 2 + 1] = d * 2.4f + Vector3.forward * 10f;
                if (i < seg) { int t = i * 6, k = i * 2; tris[t] = k; tris[t + 1] = k + 2; tris[t + 2] = k + 1; tris[t + 3] = k + 1; tris[t + 4] = k + 2; tris[t + 5] = k + 3; }
            }
            beamMesh = new Mesh { name = "HeadlightBeam" };
            beamMesh.vertices = v; beamMesh.triangles = tris; beamMesh.colors32 = beamCols; beamMesh.RecalculateBounds();
            if (!beamMat) { beamMat = MadMax.World.Fx.TransparentMaterial(null); beamMat.SetFloat("_Cull", 0f); }
            beams = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(body, false);
                go.transform.localPosition = new Vector3(i == 0 ? -hx : hx, y, front + 0.1f);
                go.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
                go.GetComponent<MeshFilter>().sharedMesh = beamMesh;
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = beamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                go.SetActive(false);
                beams[i] = go.transform;
            }
        }

        /// <summary>How visible the beams are: rain, fog and the dust thrown up on dry ground, more so in the dark.</summary>
        void UpdateBeams(bool on)
        {
            if (beams == null) return;
            float dust = Mathf.Clamp01((Mathf.Abs(driver.ForwardSpeed) - 5f) / 15f) * (Weather.Raining ? 0f : 0.6f);
            float vis = on ? Mathf.Clamp01(Mathf.Max(Weather.Raining ? 0.8f : 0f, Mathf.Max(Atmosphere.Fog * 0.9f, dust)) * (0.35f + 0.65f * DayNight.Darkness)) : 0f;
            bool show = vis > 0.05f;
            for (int i = 0; i < beams.Length; i++)
            {
                bool lit = show && (broken & (i == 0 ? 1 : 2)) == 0;                                 // beam 0 is the left lamp (-x)
                if (beams[i].gameObject.activeSelf != lit) beams[i].gameObject.SetActive(lit);
            }
            if (!show || Mathf.Abs(vis - beamShown) < 0.04f) return;
            beamShown = vis;
            for (int i = 0; i < beamCols.Length; i++) beamCols[i] = new Color32(255, 238, 200, (byte)(i % 2 == 0 ? 38 * vis : 0));
            beamMesh.colors32 = beamCols;
        }

        static Light Make(string name, Transform parent, Vector3 pos, Quaternion rot, LightType type, Color c, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos; l.transform.localRotation = rot;
            l.type = type; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            if (type == LightType.Spot) { l.spotAngle = 60f; l.innerSpotAngle = 30f; }
            return l;
        }

        void Update()
        {
            if (heads == null) return;
            var g = MadMax.Game.WastelandGame.Instance;
            bool driven = driver.Occupied && (driver.aiDriven || (g && g.Current == driver));             // a stale occupied flag never lights a parked car
            bool on = mode == 1 || (mode == 0 && driven && DayNight.Darkness > 0.3f);
            On = on;
            int heads2 = ((broken & 1) == 0 ? 1 : 0) + ((broken & 2) == 0 ? 1 : 0);
            heads[0].enabled = on && heads2 > 0 && LightBudget.Allowed(heads[0], true);
            heads[0].intensity = heads2 == 2 ? 60f : 30f;
            UpdateBeams(on);
            float brake = driver.brakeInput > 0.1f || driver.handbrake && driver.Occupied ? 1f : 0f;
            for (int i = 0; i < tails.Length; i++)
            {
                var t = tails[i];
                t.enabled = (broken & (i == 0 ? 4 : 8)) == 0 && (on || brake > 0f && driver.Occupied) && LightBudget.Allowed(t, true);
                t.intensity = brake > 0f ? 0.6f : 0.25f;
            }
            if (lampBlock != null)
            {
                float head = on && heads2 > 0 ? 1f : 0f, tail = brake > 0f && driver.Occupied ? 1.6f : on ? 0.7f : 0f;
                if (head != shownHead) { shownHead = head; SetLamps(lampHeads, head); }       // broken lenses are gone from the mesh
                if (tail != shownTail) { shownTail = tail; SetLamps(lampTails, tail); }
            }
        }
    }
}
