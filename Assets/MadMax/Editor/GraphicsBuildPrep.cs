using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.EditorTools
{
    /// <summary>The game switches ambient occlusion and screen-space reflections on and off at runtime
    /// (<see cref="MadMax.Rendering.GraphicsQuality"/>), which in the editor also flips the renderer assets. URP strips the
    /// shader variants of features that are inactive when a build starts, so every build first sets them active again
    /// (before URP gathers its shader features).</summary>
    public class GraphicsBuildPrep : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report) => ActivateFeatures();

        [MenuItem("MadMax/Dev/Reset Graphics Features")]
        public static void ActivateFeatures()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!data) continue;
                foreach (var f in data.rendererFeatures)
                {
                    if (!f) continue;
                    bool want = f is ScreenSpaceAmbientOcclusion
#if URP_SCREEN_SPACE_REFLECTION
                                || f is ScreenSpaceReflectionRendererFeature
#endif
                        ;
                    if (want && !f.isActive) { f.SetActive(true); EditorUtility.SetDirty(data); n++; }
                }
            }
            if (n > 0) AssetDatabase.SaveAssets();
            Debug.Log("[graphics] renderer features active for the build (" + n + " switched back on)");
        }
    }
}
