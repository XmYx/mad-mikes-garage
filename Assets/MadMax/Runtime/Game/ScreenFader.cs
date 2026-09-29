using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MadMax.Game
{
    /// <summary>Fades to and from black around scene changes and game start, with a spinning chained-tyre loader.
    /// Lives across scene loads (DontDestroyOnLoad).</summary>
    public class ScreenFader : MonoBehaviour
    {
        static ScreenFader instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => instance = null;

        RawImage black, wheel, flash;
        float alpha, target, spin, flashT, flashLen = 1f;
        Color flashColor = Color.white;
        bool spinning;
        public static bool Busy { get; private set; }

        public static ScreenFader I
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("ScreenFader");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ScreenFader>();
                instance.Build();
                return instance;
            }
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            black = new GameObject("Black").AddComponent<RawImage>();
            black.transform.SetParent(transform, false);
            black.color = new Color(0, 0, 0, 0);
            var rt = black.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            black.raycastTarget = false;
            flash = new GameObject("Flash").AddComponent<RawImage>();
            flash.transform.SetParent(transform, false);
            flash.color = new Color(1, 1, 1, 0);
            var fr = flash.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            flash.raycastTarget = false;
            wheel = new GameObject("LoadingWheel").AddComponent<RawImage>();
            wheel.transform.SetParent(transform, false);
            wheel.texture = WheelTexture();
            wheel.raycastTarget = false;
            var wr = wheel.rectTransform; wr.anchorMin = wr.anchorMax = new Vector2(1f, 0f); wr.pivot = new Vector2(0.5f, 0.5f);
            wr.sizeDelta = new Vector2(96, 96); wr.anchoredPosition = new Vector2(-80, 80);
            wheel.enabled = false;
        }

        /// <summary>Pixel-art tyre wrapped in snow chains (48 px, point filtered).</summary>
        static Texture2D WheelTexture()
        {
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                Color32 col = clear;
                if (r <= 22f && r > 14f)
                {
                    bool tread = Mathf.Repeat(a, 22.5f) < 9f && r > 19.5f;             // tread blocks
                    col = tread ? new Color32(22, 20, 20, 255) : (r > 20.5f ? new Color32(48, 44, 42, 255) : new Color32(36, 33, 32, 255));
                }
                else if (r <= 14f && r > 12f) col = new Color32(18, 16, 16, 255);        // bead
                else if (r <= 12f)
                {
                    bool spoke = Mathf.Repeat(a, 72f) < 10f || r < 4f;
                    col = spoke ? new Color32(170, 170, 176, 255) : (r > 10.5f ? new Color32(120, 120, 128, 255) : clear);
                    if (r < 2.5f) col = new Color32(60, 60, 64, 255);
                }
                // chain: cross links over the tread every 45 degrees, oval links around the sidewall
                bool crossLink = Mathf.Repeat(a + 8f, 45f) < 5f && r > 14f && r <= 23f;
                bool ringLink = Mathf.Abs(r - 17f) < 1.1f && Mathf.Repeat(a, 15f) < 10f;
                if (crossLink || ringLink) col = ((x + y) & 1) == 0 ? new Color32(210, 200, 170, 255) : new Color32(150, 140, 110, 255);
                px[y * n + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, target, dt * 2.2f);
            black.color = new Color(0, 0, 0, alpha);
            if (flashT > 0f) { flashT -= Time.unscaledDeltaTime; flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Clamp01(flashT / flashLen)); }
            wheel.enabled = spinning;
            if (spinning)
            {
                spin += dt * 360f;
                wheel.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Round(spin / 22.5f) * 22.5f);   // stepped, pixel-art rotation
                wheel.color = new Color(1, 1, 1, Mathf.Clamp01(alpha * 1.5f));
            }
        }

        public static void Spinner(bool on) => I.spinning = on;

        /// <summary>A full-screen flash that fades out over <paramref name="seconds"/> (welding arc, explosions).</summary>
        public static void Flash(Color c, float seconds) { var f = I; f.flashColor = c; f.flashLen = f.flashT = Mathf.Max(0.05f, seconds); }

        /// <summary>Fade to black, run an action (e.g. start the game), fade back in.</summary>
        public static void FadeThrough(Action middle, float hold = 0.15f) => I.StartCoroutine(I.Through(middle, hold));

        IEnumerator Through(Action middle, float hold)
        {
            Busy = true;
            target = 1f;
            while (alpha < 0.999f) yield return null;
            middle?.Invoke();
            yield return new WaitForSecondsRealtime(hold);
            target = 0f;
            Busy = false;
        }

        /// <summary>Fade out, show the spinning wheel, load the scene in the background, fade in once the game is ready.</summary>
        public static void LoadScene(string scene) => I.StartCoroutine(I.Load(scene));

        IEnumerator Load(string scene)
        {
            Busy = true;
            target = 1f;
            while (alpha < 0.999f) yield return null;
            spinning = true;
            yield return null;
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            // world generation runs in the game's Start: wait for it, then a couple of frames so everything is drawn
            float timeout = Time.realtimeSinceStartup + 30f;
            while ((!WastelandGame.Instance || !WastelandGame.Instance.Ready) && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null; yield return null;
            spinning = false;
            target = 0f;
            Busy = false;
        }

        /// <summary>Fade out and quit.</summary>
        public static void Quit(Action quit) => I.StartCoroutine(I.QuitRoutine(quit));
        IEnumerator QuitRoutine(Action quit) { target = 1f; while (alpha < 0.999f) yield return null; quit(); }

        /// <summary>Start black (boot hand-over) and fade in when the game is ready.</summary>
        public static void FadeInWhenReady() => I.StartCoroutine(I.InWhenReady());
        IEnumerator InWhenReady()
        {
            alpha = target = 1f; spinning = true;
            float timeout = Time.realtimeSinceStartup + 30f;
            while ((!WastelandGame.Instance || !WastelandGame.Instance.Ready) && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            spinning = false; target = 0f;
        }
    }
}
