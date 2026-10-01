using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>Runtime lookup of the HD asset pack (tools/blender/hd/PIPELINE.md) for everything outside vehicles and
    /// characters: world props and buildings, furniture, items, tools, animals. Assets load on first use from
    /// <c>Resources/HDGen/&lt;Domain&gt;/&lt;id&gt;</c> (written by MadMax/HD/Build HD Catalog); a missing asset keeps the
    /// voxel visual. <see cref="Enabled"/> is off with the player flag <c>--no-hd</c> or MadMax &gt; Dev &gt; Voxel Visuals.
    /// Material variants (underground cutaway, wind sway, dye tint) are shared copies of the atlas materials, so the SRP
    /// batcher keeps batching them.</summary>
    public static class HDAssets
    {
        static readonly Dictionary<(HDDomain, string), HDAssetRef> cache = new Dictionary<(HDDomain, string), HDAssetRef>();
        static readonly HashSet<(HDDomain, string)> missing = new HashSet<(HDDomain, string)>();
        static readonly Dictionary<(Material, int), Material> variants = new Dictionary<(Material, int), Material>();
        static readonly Dictionary<(Material, Color32), Material> tinted = new Dictionary<(Material, Color32), Material>();
        static int enabled = -1;

        /// <summary>Runtime switch (tests, settings): false shows the voxel visuals for everything spawned afterwards.</summary>
        public static bool Enabled
        {
            get { if (enabled < 0) enabled = MadMax.Game.LaunchOptions.NoHD ? 0 : 1; return enabled == 1; }
            set => enabled = value ? 1 : 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); missing.Clear(); variants.Clear(); tinted.Clear(); enabled = -1; }

        public const int WorldCut = 1, Sway = 2, NoShadow = 4;
        public static readonly int CutYId = Shader.PropertyToID("_CutY"), TintId = Shader.PropertyToID("_Tint"), LampOnId = Shader.PropertyToID("_LampOn");

        /// <summary>The HD asset for a game id, or null (HD off, not exported, catalog not built).</summary>
        public static HDAssetRef Get(HDDomain domain, string id)
        {
            if (string.IsNullOrEmpty(id) || !Enabled) return null;
            var key = (domain, id);
            if (cache.TryGetValue(key, out var a) && a && a.model) return a;
            if (missing.Contains(key)) return null;
            a = Resources.Load<HDAssetRef>("HDGen/" + domain + "/" + id);
            if (!a || !a.model) { missing.Add(key); return null; }
            cache[key] = a;
            return a;
        }

        public static bool Has(HDDomain domain, string id) => Get(domain, id) != null;

        /// <summary>Forget misses (after the catalog was rebuilt while playing).</summary>
        public static void Refresh() { missing.Clear(); cache.Clear(); }

        /// <summary>Copy of an HD material with world flags (<see cref="WorldCut"/>: clipped under bunker roofs like the
        /// terrain; <see cref="Sway"/>: wind sway by height like the voxel vegetation).</summary>
        public static Material Variant(Material src, int flags)
        {
            flags &= WorldCut | Sway;
            if (!src || flags == 0) return src;
            if (variants.TryGetValue((src, flags), out var m) && m) return m;
            m = new Material(src) { name = src.name + "_v" + flags };
            if ((flags & WorldCut) != 0) m.SetFloat("_WorldCut", 1f);
            if ((flags & Sway) != 0) { m.SetFloat("_Sway", 0.0016f); m.SetFloat("_SwayTip", 0.05f); }
            return variants[(src, flags)] = m;
        }

        /// <summary>Copy of an HD material multiplied by a tint (painted furniture).</summary>
        public static Material Tinted(Material src, Color32 tint)
        {
            if (!src) return src;
            if (tinted.TryGetValue((src, tint), out var m) && m) return m;
            m = new Material(src) { name = src.name + "_dye" };
            m.SetColor(TintId, tint);
            return tinted[(src, tint)] = m;
        }

        /// <summary>A transform anywhere under <paramref name="root"/> by exact name (depth first).</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r) return r;
            }
            return null;
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// <summary>The LOD0 mesh and materials of an HD object (by its sidecar name) in the imported model.</summary>
        public static bool MeshOf(HDAssetRef a, string objectName, out Mesh mesh, out Material[] mats)
        {
            mesh = null; mats = null;
            var t = a && a.model ? FindDeep(a.model.transform, objectName) : null;
            if (!t) return false;
            if (t.TryGetComponent<MeshFilter>(out var mf) && t.TryGetComponent<MeshRenderer>(out var mr)) { mesh = mf.sharedMesh; mats = mr.sharedMaterials; }
            else if (t.TryGetComponent<SkinnedMeshRenderer>(out var sk)) { mesh = sk.sharedMesh; mats = sk.sharedMaterials; }
            return mesh;
        }
    }
}
