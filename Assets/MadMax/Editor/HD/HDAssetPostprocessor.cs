using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.EditorTools
{
    /// <summary>Import rules for <c>Assets/MadMax/Models/HD/**</c> (tools/blender/hd/PIPELINE.md): FBX at scale 1 with the
    /// hierarchy kept, no imported materials (every renderer gets its atlas' <c>&lt;Atlas&gt;_HD.mat</c>), read/write where the
    /// game edits meshes on the CPU (dents, paint scrapes, armour shapes, glass), Mikk tangents, Generic rig for characters,
    /// LOD groups for models used as-is; textures by suffix (_Base sRGB, _Mask linear, _Normal normal map, _Emission sRGB).</summary>
    public class HDAssetPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 4;

        static bool IsHD(string path) => path.Replace('\\', '/').StartsWith(HDSidecar.Root + "/");

        void OnPreprocessModel()
        {
            if (!IsHD(assetPath) || !(assetImporter is ModelImporter mi)) return;
            var side = HDSidecar.Load(HDSidecar.PathFor(assetPath));
            context.DependsOnSourceAsset(HDSidecar.PathFor(assetPath));
            bool character = side != null && side.kind == "character";
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = side != null && side.bakeAxisConversion;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.preserveHierarchy = true;
            mi.sortHierarchyByName = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = side == null || side.readable || HDCatalogBuilder.WantsReadable(side);   // Structure pieces merge far away
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.generateSecondaryUV = false;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (character)
            {
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.optimizeGameObjects = false;
                mi.skinWeights = ModelImporterSkinWeights.Standard;
            }
        }

        void OnPreprocessTexture()
        {
            if (!IsHD(assetPath) || !(assetImporter is TextureImporter ti)) return;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.alphaIsTransparency = false;
            if (n.EndsWith("_Normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = n.EndsWith("_Base") || n.EndsWith("_Emission");
                ti.alphaSource = n.EndsWith("_Emission") ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            }
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsHD(assetPath)) return;
            var side = HDSidecar.Load(HDSidecar.PathFor(assetPath));
            if (side == null) { Debug.LogWarning("[HD] no sidecar for " + assetPath); return; }
            foreach (var a in side.atlases ?? new HDAtlas[0])
            {
                var mp = HDMaterials.PathFor(side, a.name);
                if (File.Exists(mp)) context.DependsOnSourceAsset(mp);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var o = side.Get(r.name);
                var m = HDMaterials.For(side, o);
                int subs = r is SkinnedMeshRenderer smr && smr.sharedMesh ? smr.sharedMesh.subMeshCount : r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh ? mf.sharedMesh.subMeshCount : 1;
                if (m) r.sharedMaterials = HDMaterials.Slots(m, subs);
                if (o != null && (o.role == "glass" || o.name == "Glass" || o.role.StartsWith("lamp"))) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            // models placed as they are (props, characters): one LOD group over the whole asset. Vehicles and parts get
            // per-part groups from their builder (parts come off the vehicle).
            if (side.kind != "vehicle" && side.kind != "part") AddRootLods(root, side);
        }

        static void AddRootLods(GameObject root, HDSidecar side)
        {
            var levels = new[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var o = side.Get(r.name);
                int lvl = o != null && o.role == "lod1" ? 1 : o != null && o.role == "lod2" ? 2 : 0;
                levels[lvl].Add(r);
            }
            if (levels[1].Count == 0) return;
            var lg = root.GetComponent<LODGroup>() ?? root.AddComponent<LODGroup>();
            var lods = HDLod.Levels(levels[0], levels[1], levels[2]);
            lg.SetLODs(lods);
            lg.RecalculateBounds();
        }

        static bool catalogQueued;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var dirs = new HashSet<string>();
            foreach (var p in imported)
            {
                if (!IsHD(p)) continue;
                if (p.EndsWith(".fbx") && !catalogQueued)
                {
                    // new or re-exported models: refresh the runtime catalog (Resources/HDGen) once the import settles
                    catalogQueued = true;
                    EditorApplication.delayCall += () => { catalogQueued = false; HDCatalogBuilder.Build(true); };
                }
                if (p.EndsWith(".hd.json") || p.EndsWith(".png")) dirs.Add(Path.GetDirectoryName(p)?.Replace('\\', '/'));
            }
            foreach (var d in dirs)
            {
                foreach (var json in Directory.GetFiles(d, "*.hd.json"))
                {
                    var side = HDSidecar.Load(json.Replace('\\', '/'));
                    if (side == null) continue;
                    bool created = HDMaterials.Ensure(side);
                    AssetDatabase.SaveAssets();
                    if (created && File.Exists(side.ModelPath)) AssetDatabase.ImportAsset(side.ModelPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }

    /// <summary>LOD thresholds shared by the importer and the builders (screen-relative height of the group's size).</summary>
    public static class HDLod
    {
        public static float[] Heights = { 0.2f, 0.07f, 0.01f };

        public static LOD[] Levels(List<Renderer> l0, List<Renderer> l1, List<Renderer> l2)
        {
            var list = new List<LOD> { new LOD(Heights[0], l0.ToArray()) };
            if (l1.Count > 0) list.Add(new LOD(l2.Count > 0 ? Heights[1] : Heights[2], l1.ToArray()));
            if (l2.Count > 0) list.Add(new LOD(Heights[2], l2.ToArray()));
            return list.ToArray();
        }
    }
}
