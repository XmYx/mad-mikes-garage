using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MadMax.Game
{
    /// <summary>Boot scene: plays the pre-rendered intro film while the game scene loads in the background — the HD film
    /// (StreamingAssets/Intro/intro_hd.webm) with full-resolution rendering, the pixel one (intro.webm) otherwise, so
    /// the hand-over keeps the look. The film plays into a texture on an overlay that survives the scene change: its
    /// last frame holds while the game loads, then cross-fades into the live title, which picks up at exactly the
    /// film's last moment (<see cref="TitleSequence.FilmEndsAt"/>) with the menu fading in over it. Any key skips the
    /// film once loading allows it.</summary>
    public class BootLoader : MonoBehaviour
    {
        public string gameScene = "Wasteland_Game";
        VideoPlayer video;
        RenderTexture filmTexture;
        AsyncOperation load;
        bool finished, handedOver;
        float started;

        /// <summary>The film for the current render style (falls back to the other one).</summary>
        static string FilmPath()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Intro");
            string hd = Path.Combine(dir, "intro_hd.webm"), px = Path.Combine(dir, "intro.webm");
            if (GameSettings.Current.vector && File.Exists(hd)) return hd;
            return File.Exists(px) ? px : File.Exists(hd) ? hd : null;
        }

        void Start()
        {
            started = Time.realtimeSinceStartup;
            var cam = GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            string path = FilmPath();
            if (!Application.isBatchMode && GameSettings.Current.intro && !LaunchOptions.NoIntro && path != null)   // INTRO off: straight to the neon sign and menu
            {
                video = gameObject.AddComponent<VideoPlayer>();
                video.url = path;
                video.renderMode = VideoRenderMode.RenderTexture;
                filmTexture = new RenderTexture(Mathf.Max(16, Screen.width), Mathf.Max(16, Screen.height), 0) { name = "IntroFilm" };
                video.targetTexture = filmTexture;
                video.aspectRatio = VideoAspectRatio.FitInside;
                video.audioOutputMode = VideoAudioOutputMode.Direct;
                video.loopPointReached += _ => { finished = true; video.Pause(); };              // the last frame stays in the texture
                video.errorReceived += (_, msg) => { Debug.LogWarning("Intro video: " + msg); finished = true; };
                video.prepareCompleted += _ => Debug.Log("Intro video playing: " + path);
                FilmHold.Show(filmTexture);
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
            if (skip) { finished = true; if (video) video.Pause(); }
            ScreenFader.Spinner(load != null && load.progress < 0.9f && Time.realtimeSinceStartup - started > 2f);
            if (finished && load != null && load.progress >= 0.9f) HandOver();
        }

        void HandOver()
        {
            handedOver = true;
            TitleSequence.SkipToFinale = true;                       // the film covered the flyover shots
            if (video && FilmHold.Active)
            {
                // the film's last frame stays up over the load; FilmHold cross-fades into the live finale
                load.allowSceneActivation = true;
                return;
            }
            ScreenFader.FadeThrough(() =>
            {
                if (video) video.Stop();
                load.allowSceneActivation = true;
                ScreenFader.FadeInWhenReady();
            }, 0.05f);
        }
    }

    /// <summary>The intro film's picture over the scene change: holds the last frame (with the loading wheel) until the
    /// live title shows the same moment, then fades out.</summary>
    public class FilmHold : MonoBehaviour
    {
        public static bool Active => instance;
        static FilmHold instance;
        RawImage image;
        RenderTexture texture;
        float fade = -1f, shownAt;
        const float FadeSeconds = 0.6f, Timeout = 45f;

        public static void Show(RenderTexture rt)
        {
            if (instance) Destroy(instance.gameObject);
            var go = new GameObject("FilmHold", typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31999;                                                         // under the fader's loading wheel
            var img = new GameObject("Film", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RawImage>();
            img.transform.SetParent(go.transform, false);
            var rt2 = img.rectTransform; rt2.anchorMin = Vector2.zero; rt2.anchorMax = Vector2.one; rt2.offsetMin = rt2.offsetMax = Vector2.zero;
            img.texture = rt; img.raycastTarget = false;
            instance = go.AddComponent<FilmHold>();
            instance.image = img; instance.texture = rt; instance.shownAt = Time.realtimeSinceStartup;
        }

        void Update()
        {
            bool sceneSwapped = SceneManager.GetActiveScene().name != "Boot" && WastelandGame.Instance;
            if (fade < 0f)
            {
                bool ready = sceneSwapped && WastelandGame.Instance.Ready && TitleSequence.FinaleLive && !ScreenFader.Busy;
                if (sceneSwapped) ScreenFader.Spinner(!ready);
                if (ready || Time.realtimeSinceStartup - shownAt > Timeout) { fade = 0f; ScreenFader.Spinner(false); }
                return;
            }
            fade += Time.unscaledDeltaTime / FadeSeconds;
            image.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, fade));
            if (fade >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (texture) { texture.Release(); Destroy(texture); }
        }
    }
}
