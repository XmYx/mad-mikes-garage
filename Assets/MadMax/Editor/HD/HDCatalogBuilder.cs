using System.Collections.Generic;
using System.IO;
using MadMax.Rendering;
using UnityEditor;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary>MadMax/HD/Build HD Catalog: one <see cref="HDAssetRef"/> per exported HD asset outside vehicles and
    /// characters (<c>Assets/MadMax/Resources/HDGen/&lt;Domain&gt;/&lt;game id&gt;.asset</c>, gitignored like the models it
    /// points at), so the runtime finds world props, furniture, items, tools and animals by their game id. The domain
    /// comes from the asset's root props (<c>category</c>, <c>kind</c>) written by the Blender scripts. Also run by
    /// MadMax/Build Game Scene. Stale entries (assets no longer exported) are deleted.</summary>
    public static class HDCatalogBuilder
    {
        public const string Root = "Assets/MadMax/Resources/HDGen";

        static readonly HashSet<string> WorldCats = new HashSet<string> { "Prop", "Building", "Vegetation", "Landmark", "Stall", "Site" };
        static readonly HashSet<string> FurnitureCats = new HashSet<string> { "Structure", "Furniture", "Utility", "Garden", "Industry", "Decor", "Hidden", "Defence" };

        static string RootProp(HDSidecar s, string k)
        {
            if (s.rootProps != null) foreach (var p in s.rootProps) if (p.k == k) return p.s;
            return null;
        }

        static float RootNum(HDSidecar s, string k, float fallback)
        {
            if (s.rootProps != null) foreach (var p in s.rootProps) if (p.k == k) return p.n;
            return fallback;
        }

        /// <summary>The game domain of an exported asset, or null for vehicles and characters (their own builders).</summary>
        public static HDDomain? DomainOf(HDSidecar s)
        {
            if (s == null || s.kind == "vehicle" || s.kind == "character") return null;
            string cat = RootProp(s, "category") ?? "", kind = RootProp(s, "kind") ?? "", group = (s.group ?? "").ToLowerInvariant();
            if (cat == "Animal" || kind == "animal" || kind == "critter" || group == "animals") return HDDomain.Animal;
            if (kind == "tool" || cat == "Tool" || cat == "Weapon" || group == "tools") return HDDomain.Tool;
            if (kind == "item" || kind == "cloth" || group == "items") return HDDomain.Item;
            if (kind == "part" || s.kind == "part" || group.StartsWith("parts")) return HDDomain.Part;
            if (WorldCats.Contains(cat)) return HDDomain.World;
            if (FurnitureCats.Contains(cat) || group == "furniture") return HDDomain.Furniture;
            return HDDomain.World;
        }

        /// <summary>Meshes the game reads on the CPU: Structure pieces (merged far away by StructureBatcher).</summary>
        public static bool WantsReadable(HDSidecar s) => DomainOf(s) == HDDomain.Furniture && RootProp(s, "category") == "Structure";

        [MenuItem("MadMax/HD/Build HD Catalog")]
        public static void BuildMenu() => Build(true);

        public static int Build(bool log)
        {
            if (!Directory.Exists(HDSidecar.Root)) { if (log) Debug.Log("[HD] no " + HDSidecar.Root + ": catalog empty"); return 0; }
            var keep = new HashSet<string>();
            var counts = new Dictionary<HDDomain, int>();
            int skipped = 0;
            foreach (var json in Directory.GetFiles(HDSidecar.Root, "*.hd.json", SearchOption.AllDirectories))
            {
                var side = HDSidecar.Load(json.Replace('\\', '/'));
                var domain = DomainOf(side);
                if (domain == null) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(side.ModelPath);
                if (!model) { skipped++; if (log) Debug.LogWarning("[HD] " + side.asset + ": model not imported yet (" + side.ModelPath + ")"); continue; }
                string id = RootProp(side, "game_id");
                if (string.IsNullOrEmpty(id)) id = side.asset;
                string dir = Root + "/" + domain.Value;
                Directory.CreateDirectory(dir);
                string path = dir + "/" + id + ".asset";
                var a = AssetDatabase.LoadAssetAtPath<HDAssetRef>(path);
                bool isNew = !a;
                if (isNew) a = ScriptableObject.CreateInstance<HDAssetRef>();
                Fill(a, side, domain.Value, id, model);
                if (isNew) AssetDatabase.CreateAsset(a, path);
                else EditorUtility.SetDirty(a);
                keep.Add(path);
                counts[domain.Value] = counts.TryGetValue(domain.Value, out var n) ? n + 1 : 1;
            }
            // entries whose export is gone
            if (Directory.Exists(Root))
                foreach (var f in Directory.GetFiles(Root, "*.asset", SearchOption.AllDirectories))
                {
                    var p = f.Replace('\\', '/');
                    if (!keep.Contains(p)) AssetDatabase.DeleteAsset(p);
                }
            AssetDatabase.SaveAssets();
            HDAssets.Refresh();
            if (log)
            {
                var sb = new System.Text.StringBuilder("[HD] catalog: ");
                foreach (var kv in counts) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append("  ");
                if (skipped > 0) sb.Append("(").Append(skipped).Append(" not imported yet)");
                Debug.Log(sb.ToString());
            }
            return keep.Count;
        }

        static void Fill(HDAssetRef a, HDSidecar s, HDDomain domain, string id, GameObject model)
        {
            a.id = id; a.group = s.group; a.kind = RootProp(s, "kind") ?? s.kind; a.category = RootProp(s, "category") ?? "";
            a.domain = domain; a.model = model;
            a.boundsMin = HDSidecar.V(s.boundsMin); a.boundsMax = HDSidecar.V(s.boundsMax);
            a.voxel = RootNum(s, "voxel", 0f);
            a.worldScale = RootNum(s, "world_scale", 1f);
            if (a.worldScale <= 0f) a.worldScale = 1f;
            a.origin = RootProp(s, "origin") ?? "";
            a.sway = a.category == "Vegetation";
            var list = new List<HDAssetRef.ObjectInfo>();
            if (s.objects != null)
                foreach (var o in s.objects)
                {
                    var info = new HDAssetRef.ObjectInfo
                    {
                        name = o.name, parent = o.parent, role = o.role,
                        part = o.Prop("part") ?? "", rigPart = o.Prop("rig_part") ?? "",
                        index = o.Has("index") ? Mathf.RoundToInt(o.PropFloat("index", -1f)) : -1,
                        mover = o.Prop("mover") == "true", shell = o.Prop("shell") == "true" || (o.name ?? "").StartsWith("Shell"),
                        marker = o.Has("marker") || (o.type != "MESH" && (o.name ?? "").Contains(":")),
                        rootMatrix = o.RootMatrix,
                        boundsMin = HDSidecar.V(o.boundsMin), boundsMax = HDSidecar.V(o.boundsMax),
                    };
                    list.Add(info);
                }
            a.objects = list.ToArray();
        }
    }
}
