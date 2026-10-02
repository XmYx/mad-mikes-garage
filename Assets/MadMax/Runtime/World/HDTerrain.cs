using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MadMax.World
{
    /// <summary>Ground classes the HD terrain splats (index = texture array slice, tools/blender/hd/terrain/make_textures.py).</summary>
    public enum TerrainClass : byte { Sand, Dirt, Grass, Rock, Gravel, Asphalt, Concrete, Mud, Snow }

    /// <summary>HD terrain: with the HD pack on, terrain chunks get smooth normals, per-corner colours blended from the
    /// neighbouring cells and per-corner weights of the nine <see cref="TerrainClass"/>es (mesh uv2 = classes 0-3, uv3 =
    /// classes 4-7, snow = the rest); the <c>_TERRAIN</c> variant of MadMax/HDLit tints tileable detail albedo (A =
    /// smoothness) and normal maps from <c>Resources/HDTerrain</c> (rendered at release time, not in git) with the vertex
    /// colour, so the biome palette stays the game's own. Without the textures the voxel terrain look stays.</summary>
    public static class HDTerrain
    {
        public const int Classes = 9;
        static readonly string[] Names = { "sand", "dirt", "grass", "rock", "gravel", "asphalt", "concrete", "mud", "snow" };
        /// <summary>World metres one texture tile covers, per class.</summary>
        static readonly float[] TileMetres = { 2.4f, 2.2f, 1.8f, 4.5f, 1.6f, 2.5f, 3.5f, 3.0f, 3.5f };
        static Texture2DArray albedo, normal;
        static int state = -1;                    // -1 unknown, 0 off, 1 ready

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { state = -1; albedo = normal = null; }

        /// <summary>The HD pack is on and the terrain textures load into arrays.</summary>
        public static bool Enabled
        {
            get
            {
                if (state >= 0 && (state == 0 || (albedo && normal))) return state == 1;
                state = MadMax.Rendering.HDAssets.Enabled && Build() ? 1 : 0;
                return state == 1;
            }
        }

        static bool Build()
        {
            if (SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None) return false;
            albedo = Array("A", false);
            normal = albedo ? Array("N", true) : null;
            if (!albedo || !normal) { Debug.Log("[HD] terrain textures not installed (Resources/HDTerrain): voxel terrain"); return false; }
            return true;
        }

        static Texture2DArray Array(string kind, bool linear)
        {
            Texture2D first = null;
            var tex = new Texture2D[Classes];
            for (int k = 0; k < Classes; k++)
            {
                tex[k] = Resources.Load<Texture2D>("HDTerrain/T" + k + "_" + Names[k] + "_" + kind);
                if (!tex[k]) return null;
                if (!first) first = tex[k];
                if (tex[k].width != first.width || tex[k].height != first.height || tex[k].graphicsFormat != first.graphicsFormat || tex[k].mipmapCount != first.mipmapCount)
                { Debug.LogWarning("[HD] terrain texture " + tex[k].name + " differs in size or format"); return null; }
            }
            var arr = new Texture2DArray(first.width, first.height, Classes, first.graphicsFormat, TextureCreationFlags.MipChain, first.mipmapCount)
            { name = "HDTerrain_" + kind, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            for (int k = 0; k < Classes; k++)
                for (int m = 0; m < first.mipmapCount; m++) Graphics.CopyTexture(tex[k], 0, m, arr, k, m);
            return arr;
        }

        /// <summary>The HD terrain material (HDLit, keyword <c>_TERRAIN</c>), or null when HD terrain is off.</summary>
        public static Material MakeMaterial()
        {
            if (!Enabled) return null;
            var src = Resources.Load<Material>("RuntimeMaterials/HDTerrain");                     // keeps the _TERRAIN variant in builds
            if (!src) return null;
            var m = new Material(src) { name = "HDTerrain" };
            m.EnableKeyword("_TERRAIN");
            m.SetTexture("_TerrainAlbedo", albedo);
            m.SetTexture("_TerrainNormal", normal);
            var scales = new Vector4[3];
            for (int k = 0; k < Classes; k++) scales[k / 4][k % 4] = 1f / TileMetres[k];
            m.SetVector("_TerrainScale0", scales[0]); m.SetVector("_TerrainScale1", scales[1]); m.SetVector("_TerrainScale2", scales[2]);
            m.SetFloat("_VertexAlbedo", 1f);
            m.SetFloat("_OutlinePx", 0f);
            m.SetFloat("_SnowMask", 0f);                                                     // the terrain paints its own snow
            m.SetFloat("_WorldCut", 1f);
            m.SetFloat("_MaskStrength", 0f);
            m.SetFloat("_NormalStrength", 1f);
            return m;
        }
    }
}
