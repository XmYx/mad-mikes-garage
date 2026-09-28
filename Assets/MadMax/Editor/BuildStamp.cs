using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary>Runs before every player build (File > Build, Build And Run, MadMax > Build Linux Player):
    /// stamps the version shown on the main menu (0.1.yyMMdd.HHmm) and refuses to build when the generated
    /// content (vehicle/part prefabs, game scene) is older than the design code that produces it.</summary>
    public class BuildStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        /// <summary>Release version from CI (CiBuild -buildVersion); otherwise a date stamp is used.</summary>
        public static string VersionOverride;
        static string previous;   // restored after the build so ProjectSettings does not change with every build

        const string GameScene = "Assets/MadMax/Scenes/Wasteland_Game.unity";
        static readonly string[] Sources = { "Assets/MadMax/Runtime/Designs", "Assets/MadMax/Editor/MadMaxBuilder.cs" };

        public void OnPreprocessBuild(BuildReport report)
        {
            // CI checkouts have arbitrary file times (and CiBuild regenerates the content anyway)
            if (Environment.GetEnvironmentVariable("CI") == null && VersionOverride == null && File.Exists(GameScene))
            {
                var built = File.GetLastWriteTimeUtc(GameScene);
                string newer = null;
                foreach (var src in Sources)
                {
                    var files = Directory.Exists(src) ? Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories) : new[] { src };
                    foreach (var f in files) if (File.Exists(f) && File.GetLastWriteTimeUtc(f) > built.AddSeconds(5)) { newer = f; break; }
                    if (newer != null) break;
                }
                if (newer != null)
                    throw new BuildFailedException($"Generated content is older than {newer}. Run MadMax > Build Game Scene first " +
                                                   "(MadMax > Build Linux Player does this automatically).");
            }
            previous = PlayerSettings.bundleVersion;
            PlayerSettings.bundleVersion = VersionOverride ?? "0.1." + DateTime.Now.ToString("yyMMdd.HHmm");
            Debug.Log("[MadMax] build version " + PlayerSettings.bundleVersion);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (previous != null) PlayerSettings.bundleVersion = previous;
            previous = null;
        }
    }
}
