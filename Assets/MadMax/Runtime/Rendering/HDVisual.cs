using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.Rendering
{
    /// <summary>The HD look of a voxel object: the imported HD model (or several kit modules) as a child named <c>HD</c>,
    /// while the voxel mesh stays for colliders, bounds, carving and debris with its renderer kept dark
    /// (<see cref="IsHost"/>; <c>LineOfSight</c> respects it). Moving parts of the HD model (<c>mover</c> objects, bulbs)
    /// ride on the voxel object's child of the same name (rotors, leaves, stamps, heads), so the game's animation code
    /// moves them unchanged. Lamp children (<c>Lamp_*</c>) glow while <see cref="LampState"/> says so; a dye tints every
    /// material; <see cref="HDCarve"/> clips the model where voxels were carved away.</summary>
    public class HDVisual : MonoBehaviour
    {
        public HDAssetRef asset;
        [Tooltip("First module of a kit (sites), for reference.")] public HDAssetRef kitAsset;
        public Transform root;
        public Renderer host;
        [Tooltip("Show / hide with the voxel renderer's enabled flag (held tools, items).")] public bool followHost;
        /// <summary>Lamp children glow while this returns true (polled each frame).</summary>
        public System.Func<bool> LampState { get => lampState; set { lampState = value; enabled = NeedsUpdate; } }
        System.Func<bool> lampState;
        /// <summary>Extra material stage (carving): applied after the variant and the tint.</summary>
        public System.Func<Material, Material> materialHook;

        public struct Module
        {
            public string id;
            public Vector3 pos;
            public Quaternion rot;
            public bool mirror;
        }

        Renderer[] renderers = new Renderer[0];
        Material[][] baseMats = new Material[0][];
        readonly List<Renderer> lamps = new List<Renderer>();
        readonly List<(Transform hd, string name)> unbound = new List<(Transform, string)>();
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<Renderer> extra = new List<Renderer>();
        bool shown = true, lampsOn, hasTint;
        int flags, bindTries;
        Color32 tint;

        static readonly HashSet<Renderer> hosts = new HashSet<Renderer>();
        static readonly Dictionary<Material, Material> litMats = new Dictionary<Material, Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { hosts.Clear(); litMats.Clear(); }

        public IReadOnlyList<Renderer> Renderers => renderers;
        public bool Shown => shown;

        /// <summary>A voxel renderer replaced by an HD model: it stays enabled (bounds, batching) but never draws.</summary>
        public static bool IsHost(Renderer r) => r && hosts.Contains(r);

        void Hide(Renderer r)
        {
            if (!r || hosts.Contains(r)) return;
            r.forceRenderingOff = true;
            hosts.Add(r);
            hidden.Add(r);
        }

        /// <summary>Give <paramref name="host"/> the HD model of <paramref name="id"/> (null when there is none: the voxel
        /// visual stays). <paramref name="flags"/>: <see cref="HDAssets.WorldCut"/>, <see cref="HDAssets.Sway"/>,
        /// <see cref="HDAssets.NoShadow"/>.</summary>
        public static HDVisual Dress(GameObject host, HDDomain domain, string id, int flags = 0, bool followHost = false)
        {
            if (!host) return null;
            var a = HDAssets.Get(domain, id);
            return a ? Dress(host, a, flags, followHost) : null;
        }

        public static HDVisual Dress(GameObject host, HDAssetRef a, int flags = 0, bool followHost = false)
        {
            if (!host || !a || !a.model) return null;
            var v = host.GetComponent<HDVisual>();
            if (v && v.root) Destroy(v.root.gameObject);
            if (!v) v = host.AddComponent<HDVisual>();
            v.asset = a; v.flags = flags; v.followHost = followHost;
            v.root = Instantiate(a.model, host.transform, false).transform;
            v.root.name = "HD";
            v.root.localPosition = Vector3.zero; v.root.localRotation = Quaternion.identity; v.root.localScale = Vector3.one;
            v.Setup(host);
            return v;
        }

        /// <summary>A kit assembled from modules (bunkers, airfields): each module's HD model placed in the host's space.
        /// Modules without an export are skipped (the caller keeps voxels there). Returns null when none was placed.</summary>
        public static HDVisual DressModules(GameObject host, IList<Module> modules, HDDomain domain, int flags = 0)
        {
            if (!host || modules == null || modules.Count == 0 || !HDAssets.Enabled) return null;
            Transform kit = null;
            HDAssetRef first = null;
            foreach (var m in modules)
            {
                var a = HDAssets.Get(domain, m.id);
                if (!a) continue;
                if (!kit)
                {
                    kit = new GameObject("HD").transform;
                    kit.SetParent(host.transform, false);
                    first = a;
                }
                var t = Instantiate(a.model, kit, false).transform;
                t.name = m.id;
                t.localPosition = m.pos; t.localRotation = m.rot; t.localScale = m.mirror ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            }
            if (!kit) return null;
            var v = host.GetComponent<HDVisual>();
            if (!v) v = host.AddComponent<HDVisual>();
            v.asset = null;
            v.kitAsset = first; v.flags = flags; v.root = kit;
            v.Setup(host);
            return v;
        }

        void Setup(GameObject h)
        {
            host = h.GetComponent<Renderer>();
            HDAssets.SetLayer(root, h.layer);
            renderers = root.GetComponentsInChildren<Renderer>(true);
            baseMats = new Material[renderers.Length][];
            lamps.Clear();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                baseMats[i] = r.sharedMaterials;
                if ((flags & HDAssets.NoShadow) != 0) r.shadowCastingMode = ShadowCastingMode.Off;
                if (r.name.StartsWith("Lamp_")) lamps.Add(r);
            }
            RefreshMaterials();
            Hide(host);
            if (host && !host.enabled && followHost) SetShown(false);
            unbound.Clear();
            if (asset)
                foreach (var o in asset.objects)
                {
                    if (!(o.mover || o.part == "Bulb" || BaseName(o.name) == "Bulb")) continue;
                    string hostName = string.IsNullOrEmpty(o.part) ? BaseName(o.name) : o.part;
                    for (int i = 0; i < root.childCount; i++)
                    {
                        var c = root.GetChild(i);
                        if (c.name == o.name || c.name.StartsWith(o.name + "__")) unbound.Add((c, hostName));      // the object, its LODs, lamps, glass
                    }
                }
            bindTries = 0;
            Bind();
            enabled = NeedsUpdate;
        }

        bool NeedsUpdate => followHost || lampState != null || unbound.Count > 0;

        /// <summary>Hang HD movers on the voxel object's children of the same name (made by the piece's setup, sometimes a
        /// few frames later). Their own voxel meshes go dark.</summary>
        void Bind()
        {
            for (int i = unbound.Count - 1; i >= 0; i--)
            {
                var (hd, n) = unbound[i];
                if (!hd) { unbound.RemoveAt(i); continue; }
                var target = FindHost(transform, n);
                if (!target) continue;
                hd.SetParent(target, true);
                foreach (var r in target.GetComponents<Renderer>()) Hide(r);
                unbound.RemoveAt(i);
            }
        }

        /// <summary>Object name without Blender's ".001" suffix and the exporter's "__L1" / "__Glass" splits.</summary>
        public static string BaseName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int k = n.IndexOf("__", System.StringComparison.Ordinal); if (k > 0) n = n.Substring(0, k);
            k = n.IndexOf('.'); if (k > 0) n = n.Substring(0, k);
            return n;
        }

        Transform FindHost(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c == root) continue;
                if (c.name == name && c.GetComponent<Renderer>()) return c;
                var r = FindHost(c, name);
                if (r) return r;
            }
            return null;
        }

        /// <summary>An HD object by name (after binding it may sit under a voxel child).</summary>
        public Transform Object(string name)
        {
            foreach (var r in renderers) if (r && (r.name == name || r.name.StartsWith(name + "."))) return r.transform;
            return root ? HDAssets.FindDeep(root, name) : null;
        }

        void LateUpdate()
        {
            if (unbound.Count > 0 && ++bindTries < 60) Bind();
            else if (unbound.Count > 0) unbound.Clear();
            if (followHost && host && host.enabled != shown) SetShown(host.enabled);
            if (lampState != null)
            {
                bool on = lampState();
                if (on != lampsOn) { lampsOn = on; RefreshMaterials(); }
            }
            if (!NeedsUpdate) enabled = false;
        }

        public void SetShown(bool on)
        {
            shown = on;
            foreach (var r in renderers) if (r) r.enabled = on;
            foreach (var r in extra) if (r) r.enabled = on;
        }

        /// <summary>A renderer that belongs to the HD look without coming from the model (the carve rim): it is shown,
        /// hidden and cut away with the model.</summary>
        public void Adopt(Renderer r) { if (r && !extra.Contains(r)) extra.Add(r); }

        /// <summary>Dye: every material multiplied by <paramref name="c"/> (null = as exported).</summary>
        public void SetTint(Color32? c)
        {
            hasTint = c.HasValue;
            tint = c ?? new Color32(255, 255, 255, 255);
            RefreshMaterials();
        }

        /// <summary>Re-derive every renderer's materials: base, world variant, tint, lamp state, carve hook.</summary>
        public void RefreshMaterials()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var src = baseMats[i];
                var mats = new Material[src.Length];
                bool lamp = lampsOn && lamps.Contains(r);
                for (int k = 0; k < src.Length; k++)
                {
                    var m = HDAssets.Variant(src[k], flags);
                    if (hasTint) m = HDAssets.Tinted(m, tint);
                    if (lamp) m = Lit(m);
                    if (materialHook != null) m = materialHook(m);
                    mats[k] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        static Material Lit(Material src)
        {
            if (!src) return src;
            if (litMats.TryGetValue(src, out var m) && m) return m;
            m = new Material(src) { name = src.name + "_lamp" };
            m.SetFloat(HDAssets.LampOnId, 1f);
            return litMats[src] = m;
        }

        /// <summary>Copy a property block (cutaway <c>_CutY</c>) from the voxel renderer onto the HD renderers; null clears
        /// it (the HD renderers batch again).</summary>
        public static void CopyBlock(Renderer hostRenderer, MaterialPropertyBlock block)
        {
            if (!hostRenderer || !hosts.Contains(hostRenderer)) return;
            if (!hostRenderer.TryGetComponent<HDVisual>(out var v)) return;
            foreach (var r in v.renderers) if (r) r.SetPropertyBlock(block);
            foreach (var r in v.extra) if (r) r.SetPropertyBlock(block);
        }

        void OnDestroy() { foreach (var r in hidden) hosts.Remove(r); hidden.Clear(); }
    }
}
