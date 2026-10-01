using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.Rendering
{
    /// <summary>Marks a prefab built from an HD asset (tools/blender/hd/PIPELINE.md) and holds the helpers the vehicle
    /// systems share: HD meshes are textured (UV0) triangle meshes drawn with <c>MadMax/HDLit</c>; LOD meshes are children
    /// named <c>LOD1</c>/<c>LOD2</c>; lamp meshes are children named <c>Lamp_Head</c>/<c>Lamp_Tail</c>/<c>Lamp_Amber</c>/
    /// <c>Lamp_Other</c>; glazing children are named <c>Glass</c>.</summary>
    public class HDModel : MonoBehaviour
    {
        [Tooltip("Asset name in Models/HD (sidecar <asset>.hd.json).")] public string asset;
        [Tooltip("Asset group folder (cars, heavy, misc, character).")] public string group;

        public static readonly int PaintColorId = Shader.PropertyToID("_PaintColor");
        public static readonly int PaintRefId = Shader.PropertyToID("_PaintRef");
        public static readonly int LampOnId = Shader.PropertyToID("_LampOn");

        /// <summary>A textured HD mesh (voxel meshes have no UV0).</summary>
        public static bool IsHDMesh(Mesh m) => m && m.HasVertexAttribute(VertexAttribute.TexCoord0);

        /// <summary>A renderer drawn with an HD material.</summary>
        public static bool IsHD(Renderer r)
        {
            if (!r) return false;
            var m = r.sharedMaterial;
            return m && m.HasProperty(PaintRefId) && m.HasProperty(LampOnId);
        }

        /// <summary>LOD children (and the meshes under them) never take dents, paint scrapes or armour shapes.</summary>
        public static bool IsLod(Transform t) => t && (t.name == "LOD1" || t.name == "LOD2");

        public static bool IsLamp(Transform t) => t && t.name.StartsWith("Lamp_");

        static Material voxel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { voxel = null; }

        /// <summary>HDLit with vertex-colour albedo: voxel meshes (armour plates, decals) drawn in the HD look.</summary>
        public static Material VoxelMaterial
        {
            get
            {
                if (!voxel) voxel = Resources.Load<Material>("RuntimeMaterials/HDLitVoxel");
                return voxel;
            }
        }

        /// <summary>The material to give a voxel mesh added onto <paramref name="host"/> (the host's own unless it is HD).</summary>
        public static Material VoxelMaterialFor(Renderer host)
        {
            if (!host) return null;
            return IsHD(host) && VoxelMaterial ? VoxelMaterial : host.sharedMaterial;
        }
    }
}
