using System.IO;
using UnityEditor;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary>Import settings for Resources/Sfx: mono (all effects are 3D), background loading, short clips decompressed
    /// on load (cheap to trigger), long loops kept compressed in memory.</summary>
    public class SfxImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Sfx/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            imp.loadInBackground = true;
            var s = imp.defaultSampleSettings;
            long bytes = new FileInfo(assetPath).Length;
            s.loadType = bytes > 200_000 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
        }

        [MenuItem("MadMax/Reimport Sfx")]
        static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/MadMax/Resources/Sfx" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("Sfx reimported");
        }
    }
}
