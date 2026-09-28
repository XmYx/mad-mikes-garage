using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace MadMax.Game
{
    /// <summary>Boot scene: plays the pre-rendered intro film (StreamingAssets/Intro/intro.webm) while the game scene
    /// loads in the background, then fades into the live title (neon sign, burnout, menu). Any key skips the film once
    /// loading allows it.</summary>
    public class BootLoader : MonoBehaviour
    {
        public string gameScene = "Wasteland_Game";
        VideoPlayer video;
        AsyncOperation load;
        bool finished, handedOver;
        float started;

        void Start()
        {
            started = Time.realtimeSinceStartup;
            var cam = GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            string path = Path.Combine(Application.streamingAssetsPath, "Intro", "intro.webm");
            if (!Application.isBatchMode && GameSettings.Current.intro && File.Exists(path))   // INTRO off: straight to the neon sign and menu
            {
                video = gameObject.AddComponent<VideoPlayer>();
                video.url = path;
                video.renderMode = VideoRenderMode.CameraFarPlane;
                video.targetCamera = cam;
                video.aspectRatio = VideoAspectRatio.FitInside;
                video.audioOutputMode = VideoAudioOutputMode.Direct;
                video.loopPointReached += _ => finished = true;
                video.errorReceived += (_, msg) => { Debug.LogWarning("Intro video: " + msg); finished = true; };
                video.prepareCompleted += _ => Debug.Log("Intro video playing: " + path);
                video.Play();
            }
            else finished = true;
            load = SceneManager.LoadSceneAsync(gameScene);
            load.allowSceneActivation = false;
        }

        void Update()
        {
            if (handedOver) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool skip = Time.realtimeSinceStartup - started > 1f && ((kb != null && kb.anyKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame));
            if (skip) finished = true;
            ScreenFader.Spinner(load != null && load.progress < 0.9f && Time.realtimeSinceStartup - started > 2f);
            if (finished && load != null && load.progress >= 0.9f) HandOver();
        }

        void HandOver()
        {
            handedOver = true;
            TitleSequence.SkipToFinale = true;                       // the film covered the flyover shots
            ScreenFader.FadeThrough(() =>
            {
                if (video) video.Stop();
                load.allowSceneActivation = true;
                ScreenFader.FadeInWhenReady();
            }, 0.05f);
        }
    }
}
