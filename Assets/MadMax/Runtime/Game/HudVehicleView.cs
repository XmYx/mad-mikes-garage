using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.Game
{
    /// <summary>The HD HUD's vehicle damage view: a 3/4 render of the current vehicle into a RenderTexture, every part
    /// tinted by its condition (green → amber → red; tyres by tread wear, the body by frame damage), parts lost while
    /// watching left as a translucent red ghost. The vehicle's own meshes are shared by proxy renderers on a hidden stage
    /// far above the world (layer 31, active only for the render, own sun, neutral globals — like <see cref="MadMax.Rendering.HDIcons"/>);
    /// nothing is re-meshed. Renders at most 10 times a second while the vehicle moves or its state changes, once a
    /// second otherwise; the RT is 2x the panel's screen size (bilinear sampling = a 2x2 box filter).</summary>
    public class HudVehicleView
    {
        const int Layer = 31, Buckets = 12;
        static readonly Vector3 Stage = new Vector3(0f, 7200f, 0f);
        /// <summary>Camera orbit: pitch, yaw (135 = front-left three-quarter).</summary>
        public static float Pitch = 24f, Yaw = 135f;
        /// <summary>Degrees per second of slow turntable rotation (0 = fixed 3/4 view).</summary>
        public static float Spin;
        /// <summary>Renders so far (tests).</summary>
        public static int Renders { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Pitch = 24f; Yaw = 135f; Spin = 0f; Renders = 0; }

        class Proxy
        {
            public Renderer src; public GameObject go; public MeshRenderer mr; public MeshFilter mf;
            public VehiclePart part; public WheelStats wheel; public MountSocket socket;
            public bool glass, ghost, hadPart; public int bucket = -1;
            public Bounds bounds;                                                                    // local mesh bounds
        }

        /// <summary>A socket in the image (viewport 0..1, y up), for the HUD overlay.</summary>
        public struct Marker { public Vector2 view; public float damage; public bool missing; public PartCategory cat; }

        public readonly List<Marker> markers = new List<Marker>();
        readonly List<Proxy> proxies = new List<Proxy>();
        readonly List<MeshRenderer> scan = new List<MeshRenderer>();
        readonly List<MountSocket> sockets = new List<MountSocket>();
        Camera cam; Light sun; RenderTexture rt; GameObject root;
        Material[] tints; Material glassMat, ghostMat;
        bool failed;
        VehicleDriver target; VehicleChassis chassis; VehicleDamage damage;
        int signature, stateHash;
        bool dirty, rendered;
        float nextScan, nextRender, spinAngle;
        Vector3 viewCentre, sphereCentre; float viewSize = 1f, aspect = 1.4f, sphereRadius = 1f;

        public Texture Texture => rt;
        public VehicleDriver Target => target;
        /// <summary>The view has a picture of the current target.</summary>
        public bool Ready => rendered && target && !failed;
        public bool Available => !failed;
        public int Proxies => proxies.Count;
        public int Ghosts { get { int n = 0; foreach (var p in proxies) if (p.ghost) n++; return n; } }

        public static Color32 DamageColour(float d)
        {
            d = Mathf.Clamp01(d);
            var amber = MadMax.Voxel.Pal.Accent;
            return d < 0.5f ? Color32.Lerp(PixelHud.Good, amber, d * 2f) : Color32.Lerp(amber, PixelHud.Bad, (d - 0.5f) * 2f);
        }

        bool Setup()
        {
            if (failed) return false;
            if (cam && root && tints != null && tints[0]) return true;
            var baseMat = Resources.Load<Material>("RuntimeMaterials/HDLit");
            var shader = baseMat ? baseMat.shader : Shader.Find("MadMax/HDLit");
            if (!shader) { failed = true; return false; }
            tints = new Material[Buckets];
            for (int i = 0; i < Buckets; i++)
            {
                var m = baseMat ? new Material(baseMat) : new Material(shader);
                m.name = "DamageView" + i;
                m.SetFloat("_MaskStrength", 0f); m.SetFloat("_Smoothness", 0.32f); m.SetFloat("_SpecularScale", 0.5f);
                m.SetFloat("_VertexAlbedo", 0.3f); m.SetFloat("_NormalStrength", 0f); m.SetFloat("_OutlinePx", 2f);
                m.SetColor("_OutlineColor", new Color(0.03f, 0.03f, 0.03f, 1f));
                m.SetColor("_Tint", (Color)DamageColour(i / (float)(Buckets - 1)));
                tints[i] = m;
            }
            glassMat = new Material(tints[0]) { name = "DamageViewGlass" };
            glassMat.SetColor("_Tint", new Color(0.42f, 0.55f, 0.62f, 1f));
            glassMat.SetFloat("_VertexAlbedo", 0f);
            ghostMat = MadMax.World.Fx.TransparentMaterial(null);
            ghostMat.name = "DamageViewGhost";
            ghostMat.SetColor("_BaseColor", new Color(1f, 0.25f, 0.18f, 0.3f));
            ghostMat.SetColor("_Color", new Color(1f, 0.25f, 0.18f, 0.3f));

            // scene objects (gone with the scene; Release destroys them with the HUD)
            root = new GameObject("HudVehicleView") { hideFlags = HideFlags.HideInHierarchy };
            root.transform.position = Stage;
            root.SetActive(false);
            var cgo = new GameObject("HudVehicleViewCamera") { hideFlags = HideFlags.HideInHierarchy };
            cam = cgo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.075f, 0.075f, 1f);                       // opaque: the panel shows it as is
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
            cam.allowHDR = false; cam.allowMSAA = false;
            var data = cgo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.renderShadows = false; data.antialiasing = AntialiasingMode.None;
            var lg = new GameObject("HudVehicleViewSun") { hideFlags = HideFlags.HideInHierarchy };
            sun = lg.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, 0.97f, 0.92f); sun.intensity = 1.25f;
            sun.shadows = LightShadows.None; sun.cullingMask = 1 << Layer;
            sun.transform.rotation = Quaternion.Euler(52f, 160f, 0f);
            sun.enabled = false;
            return true;
        }

        /// <summary>Follow <paramref name="v"/> (null: nothing to show) and render when due. The panel is
        /// <paramref name="pxW"/> x <paramref name="pxH"/> screen pixels.</summary>
        public void Tick(VehicleDriver v, int pxW, int pxH)
        {
            if (!v) { if (target) Detach(); return; }
            if (!Setup()) return;
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Hud.DamageView");
            float now = Time.unscaledTime;
            if (v != target) Attach(v);
            if (now >= nextScan) { nextScan = now + 1f; if (Signature() != signature) { Rebuild(); Frame(); dirty = true; } }
            int w = Mathf.Clamp(pxW * 2, 32, 1024), h = Mathf.Clamp(pxH * 2, 32, 768);
            if (!rt || rt.width != w || rt.height != h)
            {
                if (rt) { rt.Release(); Object.Destroy(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "HudVehicleView", antiAliasing = 1, filterMode = FilterMode.Bilinear };
                rt.Create();
                aspect = w / (float)h;
                Frame();
                dirty = true;
            }
            int hash = StateHash();
            if (hash != stateHash) { stateHash = hash; dirty = true; }
            bool moving = v.Body && (v.Body.linearVelocity.sqrMagnitude > 0.25f || v.Body.angularVelocity.sqrMagnitude > 0.01f);
            if (Spin != 0f) { spinAngle += Spin * Time.unscaledDeltaTime; moving = true; }
            if ((dirty && now >= nextRender) || (moving && now >= nextRender) || now >= nextRender + 0.9f)
            {
                nextRender = now + 0.1f;
                dirty = false;
                Render();
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        void Attach(VehicleDriver v)
        {
            Detach();
            target = v;
            chassis = v.GetComponent<VehicleChassis>();
            damage = v.GetComponent<VehicleDamage>();
            if (chassis) chassis.Changed += OnChanged;
            Rebuild();
            Frame();
            dirty = true; nextRender = 0f;
        }

        void Detach()
        {
            if (chassis) chassis.Changed -= OnChanged;
            foreach (var p in proxies) if (p.go) Object.Destroy(p.go);
            proxies.Clear(); sockets.Clear(); markers.Clear();
            target = null; chassis = null; damage = null; rendered = false; signature = 0;
        }

        void OnChanged() { dirty = true; nextScan = 0f; }

        public void Release()
        {
            Detach();
            if (rt) { rt.Release(); Object.Destroy(rt); }
            if (root) Object.Destroy(root);
            if (cam) Object.Destroy(cam.gameObject);
            if (sun) Object.Destroy(sun.gameObject);
            if (tints != null) foreach (var m in tints) if (m) Object.Destroy(m);
            if (glassMat) Object.Destroy(glassMat);
            if (ghostMat) Object.Destroy(ghostMat);
            tints = null;
        }

        bool Include(MeshRenderer r)
        {
            if (!r || !r.enabled || MadMax.Rendering.HDVisual.IsHost(r)) return false;
            string n = r.name;
            if (n == "Driver" || n.EndsWith("__L1") || n.EndsWith("__L2")) return false;
            if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return false;
            if (r.GetComponentInParent<VehicleDriver>() != target) return false;                  // a trailer / loose thing parented for a while
            if (r.GetComponentInParent<MadMax.Building.Placeable>() || r.GetComponentInParent<HumanRig>() || r.GetComponentInParent<MadMax.Items.WorldItem>()) return false;
            return true;
        }

        int Signature()
        {
            if (!target) return 0;
            target.GetComponentsInChildren(false, scan);
            int sig = 17;
            foreach (var r in scan) if (Include(r)) sig = sig * 31 + r.GetEntityId().GetHashCode();
            return sig;
        }

        void Rebuild()
        {
            signature = Signature();
            // parts that left the vehicle while we watched become ghosts; ghosts of a socket that has a part again go
            for (int i = proxies.Count - 1; i >= 0; i--)
            {
                var p = proxies[i];
                bool gone = p.hadPart && (!p.part || !p.part.IsMounted || p.part.Vehicle != chassis);
                if (p.ghost && p.socket && p.socket.Current) { Object.Destroy(p.go); proxies.RemoveAt(i); continue; }
                if (gone && !p.ghost && p.socket) { p.ghost = true; p.src = null; p.part = null; p.wheel = null; SetMaterial(p, ghostMat); continue; }
                if (!p.ghost) { Object.Destroy(p.go); proxies.RemoveAt(i); }
            }
            foreach (var r in scan)
            {
                if (!Include(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                var go = new GameObject("P") { hideFlags = HideFlags.HideInHierarchy, layer = Layer };
                go.transform.SetParent(root.transform, false);
                var pmf = go.AddComponent<MeshFilter>(); pmf.sharedMesh = mf.sharedMesh;
                var pmr = go.AddComponent<MeshRenderer>();
                pmr.shadowCastingMode = ShadowCastingMode.Off; pmr.receiveShadows = false;
                pmr.lightProbeUsage = LightProbeUsage.Off; pmr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var part = r.GetComponentInParent<VehiclePart>();
                var p = new Proxy { src = r, go = go, mr = pmr, mf = pmf, part = part, hadPart = part, wheel = part ? part.GetComponent<WheelStats>() : null, socket = part ? part.Socket : null,
                    glass = r.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0, bounds = mf.sharedMesh.bounds };
                var mats = r.sharedMaterials;
                if (mats.Length > 1)
                {
                    var arr = new Material[mats.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = tints[0];
                    pmr.sharedMaterials = arr;
                }
                proxies.Add(p);
                Pose(p);
            }
            sockets.Clear();
            if (chassis) foreach (var s in chassis.Sockets) sockets.Add(s);
        }

        static void SetMaterial(Proxy p, Material mat)
        {
            if (p.mr.sharedMaterials.Length > 1)
            {
                var arr = p.mr.sharedMaterials;
                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                p.mr.sharedMaterials = arr;
            }
            else p.mr.sharedMaterial = mat;
        }

        void Pose(Proxy p)
        {
            if (!p.src || !target || (p.hadPart && (!p.part || !p.part.IsMounted))) return;   // just came off: the rescan ghosts it
            var vt = target.transform; var t = p.src.transform;
            p.go.transform.localPosition = vt.InverseTransformPoint(t.position);
            p.go.transform.localRotation = Quaternion.Inverse(vt.rotation) * t.rotation;
            var ls = t.lossyScale; var vs = vt.lossyScale;
            p.go.transform.localScale = new Vector3(ls.x / vs.x, ls.y / vs.y, ls.z / vs.z);
        }

        float Condition(Proxy p)
        {
            if (p.part) return Mathf.Max(p.part.damage, p.wheel ? Mathf.Min(1f, p.wheel.wear) : 0f);
            return damage ? damage.FrameDamage : 0f;
        }

        int StateHash()
        {
            int h = 7;
            foreach (var p in proxies) h = h * 31 + (p.ghost ? -1 : Mathf.RoundToInt(Condition(p) * (Buckets - 1)));
            return h;
        }

        /// <summary>Fit the orthographic camera around all proxies (on attach / rebuild / resize: steady while driving).</summary>
        void Frame()
        {
            if (!cam || proxies.Count == 0) return;
            var view = Quaternion.Euler(Pitch, Yaw, 0f);
            var toView = Quaternion.Inverse(view);
            bool any = false; var b = new Bounds();
            foreach (var p in proxies)
            {
                var m = Matrix4x4.TRS(p.go.transform.localPosition, p.go.transform.localRotation, p.go.transform.localScale);
                var mb = p.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = toView * m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                }
            }
            viewCentre = view * b.center;
            viewSize = Mathf.Max(b.extents.y, b.extents.x / Mathf.Max(0.2f, aspect)) * 1.1f;
            sphereCentre = viewCentre; sphereRadius = b.extents.magnitude;
        }

        void Render()
        {
            if (!target || !rt) return;
            foreach (var p in proxies)
            {
                if (p.ghost) continue;
                Pose(p);
                int bucket = p.glass ? -2 : Mathf.Clamp(Mathf.RoundToInt(Condition(p) * (Buckets - 1)), 0, Buckets - 1);
                if (bucket == p.bucket) continue;
                p.bucket = bucket;
                SetMaterial(p, bucket == -2 ? glassMat : tints[bucket]);
            }
            var view = Quaternion.Euler(Pitch, Yaw + spinAngle, 0f);
            root.transform.SetPositionAndRotation(Stage, Quaternion.identity);
            bool spin = Spin != 0f;                                                                  // turntable: frame the bounding sphere
            cam.orthographicSize = spin ? Mathf.Max(sphereRadius * 0.8f, sphereRadius * 0.8f / Mathf.Max(0.2f, aspect)) : viewSize;
            cam.transform.rotation = view;
            cam.transform.position = Stage + (spin ? sphereCentre : viewCentre) - view * Vector3.forward * 30f;
            cam.targetTexture = rt;
            cam.aspect = aspect;
            // markers: sockets in the picture (viewport)
            markers.Clear();
            var vt = target.transform;
            foreach (var s in sockets)
            {
                if (!s) continue;
                var local = vt.InverseTransformPoint(s.transform.position);
                var vp = cam.WorldToViewportPoint(Stage + local);
                markers.Add(new Marker { view = new Vector2(vp.x, vp.y), damage = s.Current ? Mathf.Max(s.Current.damage, s.Current.TryGetComponent<WheelStats>(out var ws) ? Mathf.Min(1f, ws.wear) : 0f) : 1f, missing = !s.Current, cat = s.accepts });
            }
            NeutralRender();
            rendered = true;
            Renders++;
        }

        static readonly string[] Floats = { "_MadMaxNight", "_MadMaxSnow", "_MadMaxUnderFill", "_MadMaxCurve", "_MadMaxAutumn" };
        static readonly string[] Vectors = { "_MadMaxClouds", "_MadMaxWaterHole", "_MadMaxCut", "_MadMaxSnowLat" };
        static readonly float[] fv = new float[5];
        static readonly Vector4[] vv = new Vector4[4];

        void NeutralRender()
        {
            for (int i = 0; i < Floats.Length; i++) { fv[i] = Shader.GetGlobalFloat(Floats[i]); Shader.SetGlobalFloat(Floats[i], 0f); }
            for (int i = 0; i < Vectors.Length; i++) { vv[i] = Shader.GetGlobalVector(Vectors[i]); Shader.SetGlobalVector(Vectors[i], Vector4.zero); }
            var oldSun = RenderSettings.sun;
            bool oldOn = oldSun && oldSun.enabled, fog = RenderSettings.fog;
            if (oldSun) oldSun.enabled = false;
            sun.enabled = true;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            root.SetActive(true);
            try
            {
                var req = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); }
                else cam.Render();
            }
            finally
            {
                root.SetActive(false);
                sun.enabled = false;
                RenderSettings.sun = oldSun;
                RenderSettings.fog = fog;
                if (oldSun) oldSun.enabled = oldOn;
                for (int i = 0; i < Floats.Length; i++) Shader.SetGlobalFloat(Floats[i], fv[i]);
                for (int i = 0; i < Vectors.Length; i++) Shader.SetGlobalVector(Vectors[i], vv[i]);
            }
        }
    }
}
