using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary>Command-line / CI player build (GameCI buildMethod). Arguments:
    /// -customBuildTarget StandaloneWindows64|StandaloneLinux64|StandaloneOSX, -customBuildPath path/to/player,
    /// -buildVersion 1.2.3, -regenerate true|false. Regenerates the generated content from the design code, then builds
    /// Boot + Wasteland_Game with Mono (macOS: universal Intel + Apple Silicon). Exits non-zero on failure.</summary>
    public static class CiBuild
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback = null)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }

            var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), Arg("-customBuildTarget", Arg("-buildTarget", "StandaloneLinux64")));
            bool ok = Run(target, Arg("-customBuildPath"), Arg("-buildVersion"), Arg("-regenerate", "true") != "false");
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>Builds one platform; returns success. Safe to call from an open editor (never exits it).</summary>
        public static bool Run(BuildTarget target, string path = null, string version = null, bool regenerate = true)
        {
            string ext = target == BuildTarget.StandaloneWindows64 ? ".exe" : target == BuildTarget.StandaloneOSX ? ".app" : ".x86_64";
            if (string.IsNullOrEmpty(path)) path = $"build/{target}/MadMikesGarage{ext}";
            if (!path.EndsWith(ext)) path += ext;
            // a Windows player cross-built off Windows gets ENABLE_NVIDIA without the NVIDIA module reference, and URP's
            // DLSS code doesn't compile: build it without the upscaler framework (STP / FSR / DLSS) and restore after
            string defines0 = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            bool stripUpscaler = target == BuildTarget.StandaloneWindows64 && Application.platform != RuntimePlatform.WindowsEditor && defines0.Contains("ENABLE_UPSCALER_FRAMEWORK");
            try
            {
                if (stripUpscaler)
                {
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.Join(";", defines0.Split(';').Where(d => d != "ENABLE_UPSCALER_FRAMEWORK")));
                    Debug.Log("[MadMax] CI build: Windows cross-build without ENABLE_UPSCALER_FRAMEWORK (no DLSS/STP/FSR in this player)");
                }
                if (regenerate)
                {
                    AssetDatabase.Refresh();                                      // a freshly rendered HD pack (tools/release.sh)
                    MadMaxBuilder.BuildGameScene();
                }
                if (!string.IsNullOrEmpty(version) && version != "none") BuildStamp.VersionOverride = version;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                if (target == BuildTarget.StandaloneLinux64)
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                    PlayerSettings.SetGraphicsAPIs(target, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore });
                }
                if (target == BuildTarget.StandaloneOSX) SetMacUniversal();

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/MadMax/Scenes/Boot.unity", "Assets/MadMax/Scenes/Wasteland_Game.unity" },
                    target = target,
                    targetGroup = BuildTargetGroup.Standalone,
                    locationPathName = path,
                    options = BuildOptions.None
                });
                Debug.Log($"[MadMax] CI build {target} v{PlayerSettings.bundleVersion}: {report.summary.result}, {report.summary.totalSize / 1048576} MB -> {path}");
                return report.summary.result == BuildResult.Succeeded;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                BuildStamp.VersionOverride = null;
                if (stripUpscaler) PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defines0);
            }
        }

        /// <summary>Universal (x64 + ARM64) macOS player. Uses reflection so the project compiles without Mac build support installed.</summary>
        static void SetMacUniversal()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings")).FirstOrDefault(t => t != null);
            var prop = type?.GetProperty("architecture");
            if (prop == null) { Debug.LogWarning("[MadMax] Mac build support not found: default architecture"); return; }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, "x64ARM64"));
        }
    }
}
