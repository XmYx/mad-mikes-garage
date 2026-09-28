using System.IO;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Development tool: records the live title sequence frame by frame (fixed 30 fps game time) to PNGs,
    /// which tools/encode the boot film from. Start with IntroRecorder.Begin(folder) in play mode.</summary>
    public class IntroRecorder : MonoBehaviour
    {
        string folder;
        int frame;
        float length;
        public static bool Recording { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Recording = false;

        public static void Begin(string folder, float seconds = 26f)
        {
            Directory.CreateDirectory(folder);
            foreach (var f in Directory.GetFiles(folder, "*.png")) File.Delete(f);
            var r = new GameObject("IntroRecorder").AddComponent<IntroRecorder>();
            r.folder = folder; r.length = seconds;
            Time.captureFramerate = 30;
            Recording = true;
        }

        void LateUpdate()
        {
            if (!Recording) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"frame_{frame:D4}.png"));
            frame++;
            if (frame >= length * 30) { Recording = false; Time.captureFramerate = 0; Destroy(gameObject); }
        }
    }
}
