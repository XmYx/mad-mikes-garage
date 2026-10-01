using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary>One <c>MadMax/HDLit</c> material per texture atlas, next to the asset (<c>&lt;Atlas&gt;_HD.mat</c>), filled
    /// from the sidecar: base / mask / normal / emission maps, emission scale, the factory paint colour.</summary>
    public static class HDMaterials
    {
        public const string Template = "Assets/MadMax/Resources/RuntimeMaterials/HDLit.mat";

        public static string PathFor(HDSidecar s, string atlas) => s.dir + "/" + atlas + "_HD.mat";

        public static Shader Shader
        {
            get
            {
                var t = AssetDatabase.LoadAssetAtPath<Material>(Template);
                return t ? t.shader : Shader.Find("MadMax/HDLit");
            }
        }

        /// <summary>Create or refresh every atlas material of the asset. Returns true when one was created.</summary>
        public static bool Ensure(HDSidecar s)
        {
            if (s?.atlases == null) return false;
            bool created = false;
            foreach (var a in s.atlases)
            {
                string path = PathFor(s, a.name);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = !m;
                if (isNew)
                {
                    var sh = Shader;
                    if (!sh) { Debug.LogError("[HD] MadMax/HDLit shader not found"); return false; }
                    m = new Material(sh) { name = a.name + "_HD" };
                }
                Fill(m, s, a);
                if (isNew) { AssetDatabase.CreateAsset(m, path); created = true; }
                else EditorUtility.SetDirty(m);
            }
            return created;
        }

        static Texture2D Tex(HDSidecar s, string file) => string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(s.dir + "/" + file);

        static void Fill(Material m, HDSidecar s, HDAtlas a)
        {
            var baseMap = Tex(s, a.baseMap); var mask = Tex(s, a.maskMap); var normal = Tex(s, a.normalMap); var emit = Tex(s, a.emissionMap);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_MaskMap", mask);
            m.SetFloat("_MaskStrength", mask ? 1f : 0f);
            m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalStrength", normal ? 1f : 0f);
            m.SetTexture("_EmissionMap", emit);
            m.SetFloat("_EmissionScale", emit ? Mathf.Max(0.01f, a.emissionScale) : 0f);
            m.SetFloat("_OutlinePx", 0f);                       // the sheets have no outline
            m.SetFloat("_SwayTip", a.swayTip);                  // vegetation: Col.a = 1 - Sway (1 = rooted)
            if (a.paintRef != null && a.paintRef.Length >= 3)
                m.SetColor("_PaintRef", new Color(a.paintRef[0], a.paintRef[1], a.paintRef[2], 1f).gamma);   // stored linear; SetColor takes sRGB
            m.enableInstancing = false;
        }

        /// <summary>The material for an object of the sidecar (its atlas), or null.</summary>
        public static Material For(HDSidecar s, HDObject o)
        {
            if (s == null || o == null || string.IsNullOrEmpty(o.atlas)) return null;
            return AssetDatabase.LoadAssetAtPath<Material>(PathFor(s, o.atlas));
        }

        public static Material[] Slots(Material m, int count)
        {
            var arr = new Material[Mathf.Max(1, count)];
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            return arr;
        }

        public static readonly HashSet<string> TextureSuffixes = new HashSet<string> { "_Base", "_Mask", "_Normal", "_Emission" };
    }
}
