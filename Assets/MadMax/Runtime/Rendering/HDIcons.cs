using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.Rendering
{
    /// <summary>Hotbar / inventory / item-feed icons rendered from the HD models (the same 3/4 view as the voxel icons of
    /// <see cref="IconRenderer"/>): the model is posed on a hidden stage far above the world, lit by its own sun (the time
    /// of day, clouds, snow and the underwater tint are switched off for the shot), rendered by an off-screen camera at 4x
    /// the icon size, box-filtered down and outlined like the voxel icons. Requests are queued and rendered a few per
    /// frame outside the HUD drawing; until then <see cref="TryGet"/> says no and the voxel icon shows.</summary>
    public static class HDIcons
    {
        struct Request { public string key; public HDAssetRef asset; public int size; public bool diagonal; }

        static readonly Dictionary<string, Color32[]> done = new Dictionary<string, Color32[]>();
        static readonly Dictionary<string, Request> queued = new Dictionary<string, Request>();
        static readonly List<string> order = new List<string>();
        static readonly HashSet<string> failed = new HashSet<string>();
        static Camera cam;
        static Light sun;
        static RenderTexture rt;
        static Texture2D read;
        static Baker baker;
        const int Layer = 31;
        static readonly Vector3 Stage = new Vector3(0f, 6000f, 0f);
        static readonly Quaternion View = Quaternion.Euler(28f, -35f, 0f);
        static readonly Color Key = new Color(1f, 0f, 1f, 0f);                   // background: transparent, or keyed out by colour
        static bool Empty(Color32 c) => c.a < 128 || (c.r > 240 && c.g < 16 && c.b > 240);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { done.Clear(); queued.Clear(); order.Clear(); failed.Clear(); cam = null; sun = null; rt = null; read = null; baker = null; }

        /// <summary>The HD icon of an item key ("feed:" prefixes are ignored) when it is ready; queues it otherwise.</summary>
        public static bool TryGet(string key, int size, bool diagonal, out Color32[] px)
        {
            px = null;
            if (!HDAssets.Enabled || string.IsNullOrEmpty(key)) return false;
            string ck = key + "#" + size + (diagonal ? "d" : "");
            if (done.TryGetValue(ck, out px)) return true;
            if (failed.Contains(ck) || queued.ContainsKey(ck)) return false;
            var a = Resolve(key);
            if (!a) { failed.Add(ck); return false; }
            queued[ck] = new Request { key = ck, asset = a, size = size, diagonal = diagonal };
            order.Add(ck);
            EnsureBaker();
            return false;
        }

        public static void Invalidate() { done.Clear(); failed.Clear(); }

        /// <summary>The HD icon for <paramref name="key"/> at <paramref name="size"/> is done.</summary>
        public static bool Ready(string key, int size) => done.ContainsKey(key + "#" + size) || done.ContainsKey(key + "#" + size + "d");

        static HDAssetRef Resolve(string key)
        {
            int hash = key.IndexOf('#');
            if (hash > 0) key = key.Substring(0, hash);                                         // catalogue keys carry their size
            if (key.StartsWith("feed:")) key = key.Substring(5);
            if (key.StartsWith("piece:")) return HDAssets.Get(HDDomain.Furniture, key.Substring(6));
            if (key.StartsWith("part:")) return HDAssets.Get(HDDomain.Part, key.Substring(5));
            if (key.StartsWith("vehicle:")) return null;
            string id = MadMax.Items.WorldItemModels.HDId(key, out var domain);
            var a = HDAssets.Get(domain, id);
            if (a) return a;
            // build kits: the piece they make
            foreach (var d in MadMax.Building.FurnitureLibrary.All)
                if (d.kit == key) return HDAssets.Get(HDDomain.Furniture, d.id);
            return null;
        }

        static void EnsureBaker()
        {
            if (baker) return;
            var go = new GameObject("HDIconBaker") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            baker = go.AddComponent<Baker>();
        }

        class Baker : MonoBehaviour
        {
            void LateUpdate()
            {
                for (int n = 0; n < 3 && order.Count > 0; n++)
                {
                    string k = order[0];
                    order.RemoveAt(0);
                    if (!queued.TryGetValue(k, out var r)) continue;
                    queued.Remove(k);
                    var px = Render(r);
                    if (px != null) done[k] = px; else failed.Add(k);
                }
            }
        }

        static bool Setup()
        {
            if (cam) return true;
            var go = new GameObject("HDIconCamera") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Key;
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 50f;
            cam.allowHDR = false; cam.allowMSAA = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            var lg = new GameObject("HDIconSun") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(lg);
            sun = lg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1f;
            sun.shadows = LightShadows.None;
            sun.cullingMask = 1 << Layer;
            sun.transform.rotation = Quaternion.Euler(50f, -60f, 0f);
            sun.enabled = false;
            return cam;
        }

        static readonly string[] Floats = { "_MadMaxNight", "_MadMaxSnow", "_MadMaxUnderFill", "_MadMaxCurve", "_MadMaxAutumn" };
        static readonly string[] Vectors = { "_MadMaxClouds", "_MadMaxWaterHole", "_MadMaxCut", "_MadMaxSnowLat" };

        static Color32[] Render(Request r)
        {
            if (!Setup() || !r.asset || !r.asset.model) return null;
            int s = r.size * 4;
            if (!rt || rt.width != s)
            {
                if (rt) { rt.Release(); Object.Destroy(rt); }
                rt = new RenderTexture(s, s, 24, RenderTextureFormat.ARGB32) { name = "HDIcon", antiAliasing = 1 };
                rt.Create();
                read = new Texture2D(s, s, TextureFormat.RGBA32, false);
            }
            var inst = Object.Instantiate(r.asset.model);
            inst.hideFlags = HideFlags.HideAndDontSave;
            HDAssets.SetLayer(inst.transform, Layer);
            foreach (var lg in inst.GetComponentsInChildren<LODGroup>(true)) lg.enabled = false;
            var rends = inst.GetComponentsInChildren<Renderer>(true);
            // the voxel icons' pose: the camera looks along View, long tools lie diagonally (IconRenderer)
            var rot = r.diagonal ? Quaternion.Euler(0f, 0f, -45f) * Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            inst.transform.SetPositionAndRotation(Stage, rot);
            // LOD0 only, no shadows; frame the camera on the model's box in view space
            var b = new Bounds();
            bool any = false;
            var toView = Matrix4x4.Rotate(Quaternion.Inverse(View));
            foreach (var x in rends)
            {
                bool lod = x.name.EndsWith("__L1") || x.name.EndsWith("__L2");
                x.enabled = !lod;
                x.shadowCastingMode = ShadowCastingMode.Off;
                if (lod || !(x is MeshRenderer) || !x.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                var m = toView * Matrix4x4.Translate(-Stage) * x.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                }
            }
            if (!any) { Object.Destroy(inst); return null; }
            float span = Mathf.Max(b.size.x, b.size.y) * (r.size / (float)Mathf.Max(1, r.size - 2));
            cam.orthographicSize = span * 0.5f;
            cam.transform.rotation = View;
            cam.transform.position = Stage + View * new Vector3(b.center.x, b.center.y, b.min.z - 5f);
            cam.targetTexture = rt;

            // a neutral stage: midday light, no weather, no underwater tint
            var fv = new float[Floats.Length]; var vv = new Vector4[Vectors.Length];
            for (int i = 0; i < Floats.Length; i++) { fv[i] = Shader.GetGlobalFloat(Floats[i]); Shader.SetGlobalFloat(Floats[i], 0f); }
            for (int i = 0; i < Vectors.Length; i++) { vv[i] = Shader.GetGlobalVector(Vectors[i]); Shader.SetGlobalVector(Vectors[i], Vector4.zero); }
            var oldSun = RenderSettings.sun;
            bool oldOn = oldSun && oldSun.enabled;
            if (oldSun) oldSun.enabled = false;
            sun.enabled = true;
            RenderSettings.sun = sun;
            try
            {
                var req = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); }
                else cam.Render();
            }
            finally
            {
                sun.enabled = false;
                RenderSettings.sun = oldSun;
                if (oldSun) oldSun.enabled = oldOn;
                for (int i = 0; i < Floats.Length; i++) Shader.SetGlobalFloat(Floats[i], fv[i]);
                for (int i = 0; i < Vectors.Length; i++) Shader.SetGlobalVector(Vectors[i], vv[i]);
                inst.SetActive(false);
                Object.Destroy(inst);
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, s, s), 0, 0, false);
            RenderTexture.active = prev;
            var big = read.GetPixels32();
            return Downsample(big, s, r.size);
        }

        /// <summary>4x4 box filter with alpha coverage (a pixel is drawn when half of it is covered), then the voxel icons'
        /// 1 px dark outline.</summary>
        static Color32[] Downsample(Color32[] big, int s, int size)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int rr = 0, gg = 0, bb = 0, n = 0;
                for (int j = 0; j < 4; j++)
                for (int i = 0; i < 4; i++)
                {
                    var c = big[(y * 4 + j) * s + x * 4 + i];
                    if (Empty(c)) continue;
                    rr += c.r; gg += c.g; bb += c.b; n++;
                }
                if (n < 8) continue;
                px[y * size + x] = new Color32((byte)(rr / n), (byte)(gg / n), (byte)(bb / n), 255);
            }
            var outlined = (Color32[])px.Clone();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (px[y * size + x].a > 0) continue;
                bool edge = (x > 0 && px[y * size + x - 1].a > 0) || (x < size - 1 && px[y * size + x + 1].a > 0) || (y > 0 && px[(y - 1) * size + x].a > 0) || (y < size - 1 && px[(y + 1) * size + x].a > 0);
                if (edge) outlined[y * size + x] = new Color32(18, 10, 8, 255);
            }
            return outlined;
        }
    }
}
