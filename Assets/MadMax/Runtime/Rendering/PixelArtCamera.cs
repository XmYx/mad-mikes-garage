using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MadMax.Rendering
{
    /// <summary>Renders the camera into a low-resolution, point-filtered target and upscales it to the screen
    /// with hard pixels. Pair with the MadMax/PixelVoxel shader (banded light + 1px outline).</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public class PixelArtCamera : MonoBehaviour
    {
        [Min(32)] public int pixelHeight = 270;
        [Tooltip("Snap the camera to whole texels to stop pixel crawl while it moves (orthographic only).")]
        public bool snapToTexels = true;
        /// <summary>MSAA samples of the render target (1 = off; full-resolution rendering only, set by the graphics settings).</summary>
        public int msaa = 1;

        [Tooltip("Optional post pass applied to the low-res image (e.g. tilt-shift). Null = none.")]
        public Material postMaterial;

        /// <summary>Always-on last pass (colour grade, heat haze, lightning flash; set by World.Atmosphere in play mode).</summary>
        public static Material Grade;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Grade = null;

        public RenderTexture Target { get; private set; }
        /// <summary>The texture actually shown on screen (post-processed when a post material is active, then graded).</summary>
        public RenderTexture Output => Grade && graded && Application.isPlaying ? graded : postMaterial && post ? post : Target;
        RenderTexture post, graded;

        Camera cam;
        GameObject display;
        Camera displayCam;
        RawImage image;
        Vector3 truePosition, lastSnap;
        Quaternion lastRotation;
        bool hasSnap;
        bool hasTruePosition;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            BuildDisplay();
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam || !Target) return;
            var src = Target;
            if (postMaterial)
            {
                Ensure(ref post);
                Graphics.Blit(Target, post, postMaterial);
                src = post;
            }
            if (Grade && Application.isPlaying)
            {
                Ensure(ref graded);
                Graphics.Blit(src, graded, Grade);
            }
        }

        void Ensure(ref RenderTexture rt)
        {
            if (rt && rt.width == Target.width && rt.height == Target.height) return;
            if (rt) { rt.Release(); DestroyImmediate(rt); }
            rt = new RenderTexture(Target.width, Target.height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            rt.Create();
        }

        void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (post) { post.Release(); DestroyImmediate(post); post = null; }
            if (graded) { graded.Release(); DestroyImmediate(graded); graded = null; }
            if (cam) cam.targetTexture = null;
            if (Target) { Target.Release(); DestroyImmediate(Target); Target = null; }
            if (display) DestroyImmediate(display);
            hasTruePosition = false;
        }

        void BuildDisplay()
        {
            if (display) return;
            display = new GameObject("PixelArtDisplay") { hideFlags = HideFlags.HideAndDontSave };
            displayCam = display.AddComponent<Camera>();
            displayCam.cullingMask = 0;
            displayCam.clearFlags = CameraClearFlags.SolidColor;
            displayCam.backgroundColor = Color.black;
            displayCam.depth = cam.depth + 10;
            displayCam.orthographic = true;

            var canvas = display.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1000;
            var imgGo = new GameObject("Image") { hideFlags = HideFlags.HideAndDontSave };
            imgGo.transform.SetParent(display.transform, false);
            image = imgGo.AddComponent<RawImage>();
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void LateUpdate() => Refresh();
        void OnValidate() { if (isActiveAndEnabled) Refresh(); }

        public void Refresh()
        {
            if (!cam) cam = GetComponent<Camera>();
            if (!display) BuildDisplay();

            float aspect = displayCam && displayCam.pixelHeight > 0 ? displayCam.pixelWidth / (float)displayCam.pixelHeight : 16f / 9f;
            int h = pixelHeight;
            int w = Mathf.Max(1, Mathf.RoundToInt(h * aspect));
            int samples = Mathf.ClosestPowerOfTwo(Mathf.Clamp(msaa, 1, 8));
            if (!Target || Target.width != w || Target.height != h || Target.antiAliasing != samples)
            {
                if (Target) { cam.targetTexture = null; Target.Release(); DestroyImmediate(Target); }
                Target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                {
                    name = "PixelArtTarget",
                    filterMode = FilterMode.Point,
                    antiAliasing = samples,
                    useMipMap = false,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Target.Create();
            }
            cam.targetTexture = Target;
            cam.allowMSAA = samples > 1;
            if (image) image.texture = Output;

            if (snapToTexels && cam.orthographic && Application.isPlaying) Snap();
        }

        void Snap()
        {
            if (!hasTruePosition) truePosition = transform.position;
            hasTruePosition = false;
            float texel = 2f * cam.orthographicSize / pixelHeight;
            var inv = Quaternion.Inverse(transform.rotation);
            var local = inv * truePosition;
            // Hysteresis: re-snap only after moving well past a texel. Plain rounding flips between two texels when the
            // true position sits on a boundary, shifting the whole image by a pixel every frame (a flickering double image).
            bool sameRotation = Quaternion.Angle(transform.rotation, lastRotation) < 0.01f;
            float sx = Mathf.Round(local.x / texel) * texel, sy = Mathf.Round(local.y / texel) * texel;
            if (sameRotation && hasSnap)
            {
                if (Mathf.Abs(local.x - lastSnap.x) < texel * 0.8f) sx = lastSnap.x;
                if (Mathf.Abs(local.y - lastSnap.y) < texel * 0.8f) sy = lastSnap.y;
            }
            local.x = sx; local.y = sy;
            lastSnap = local; lastRotation = transform.rotation; hasSnap = true;
            transform.position = transform.rotation * local;
        }

        /// <summary>Un-snapped camera position for this frame (call from follow scripts before LateUpdate snapping).</summary>
        public void SetTruePosition(Vector3 p) { truePosition = p; hasTruePosition = true; }
    }
}
