using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.Rendering
{
    /// <summary>The graphics options on top of the render style (settings GRAPHICS / EFFECTS): anti-aliasing (FXAA, SMAA,
    /// TAA or MSAA on the render target), upscaling with a render scale (URP's upscaler framework: STP, FSR 1, and DLSS on
    /// Windows with an NVIDIA card — the quality mode follows the render scale, DLAA at 100 %), shadow quality (map size,
    /// cascades, soft shadows, distance), ambient occlusion (the renderer's SSAO feature: low, medium, high), screen-space
    /// reflections (the renderer's SSR feature + its volume; HDLit surfaces sample them), bloom strength, motion blur and
    /// texture filtering. Anti-aliasing, upscaling, soft shadows, motion blur and reflections only act with
    /// full-resolution rendering (VECTOR); the pixel-art look keeps its hard texels.</summary>
    public static class GraphicsQuality
    {
        public static readonly string[] AntiAliasNames = { "OFF", "FXAA", "SMAA", "TAA", "MSAA 2X", "MSAA 4X", "MSAA 8X" };
        public static readonly string[] UpscalerNames = { "NATIVE", "STP", "FSR 1", "DLSS" };
        public static readonly float[] RenderScales = { 0.5f, 0.59f, 0.67f, 0.77f, 0.87f, 1f };
        public static readonly string[] ShadowNames = { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" };
        public static readonly string[] AoNames = { "OFF", "LOW", "MEDIUM", "HIGH" };
        public static readonly string[] ReflectionNames = { "SKY ONLY", "SCREEN SPACE", "SCREEN SPACE (HIGH)" };
        public static readonly string[] BloomNames = { "OFF", "SOFT", "STRONG" };
        public static readonly string[] FilterNames = { "BILINEAR", "ANISOTROPIC 4X", "ANISOTROPIC 16X" };

        static readonly int[] ShadowMap = { 512, 1024, 2048, 4096, 4096 };
        static readonly int[] Cascades = { 1, 2, 4, 4, 4 };
        public static readonly float[] ShadowDistance = { 0f, 60f, 110f, 160f, 240f };

        static Volume volume;
        static Bloom bloom;
        static MotionBlur blur;
#if URP_SCREEN_SPACE_REFLECTION
        static ScreenSpaceReflectionVolumeSettings ssrVolume;
#endif
        static int dlssWanted = -1;
        static bool? dlssOk;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            volume = null; bloom = null; blur = null; dlssWanted = -1; dlssOk = null;
#if URP_SCREEN_SPACE_REFLECTION
            ssrVolume = null;
#endif
        }

        /// <summary>DLSS can run here: a Windows build with the NVIDIA module, an NVIDIA card and its driver's NGX.</summary>
        public static bool DlssAvailable
        {
            get
            {
#if ENABLE_UPSCALER_FRAMEWORK && ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
                if (dlssOk == null)
                {
                    try
                    {
                        var d = UnityEngine.NVIDIA.NVUnityPlugin.IsLoaded() ? (UnityEngine.NVIDIA.GraphicsDevice.device ?? UnityEngine.NVIDIA.GraphicsDevice.CreateGraphicsDevice()) : null;
                        dlssOk = d != null && d.IsFeatureAvailable(UnityEngine.NVIDIA.GraphicsDeviceFeature.DLSS);
                    }
                    catch { dlssOk = false; }
                }
                return dlssOk.Value;
#else
                return false;
#endif
            }
        }

        /// <summary>The upscaler's registry name.</summary>
        static string UpscalerId(int upscaler)
        {
            switch (upscaler)
            {
                case 1: return "Spatial-Temporal Post-Processing";
                case 2: return "FidelityFX Super Resolution 1.0";
                case 3:
#if ENABLE_UPSCALER_FRAMEWORK && ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
                    return DLSSIUpscaler.upscalerName;
#else
                    return "Spatial-Temporal Post-Processing";
#endif
                default: return "Bilinear";
            }
        }

        public static void Apply(MadMax.Game.GameSettings s, Camera cam, PixelArtCamera pixel, Light sun)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool full = s.vector;

            // ---- anti-aliasing: post AA on the camera, MSAA on the render target
            if (cam)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;                                                // AA, bloom, blur all run as post
                int aa = full ? s.antiAliasing : 0;
                data.antialiasing = aa == 1 ? AntialiasingMode.FastApproximateAntialiasing : aa == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                                  : aa == 3 ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
                data.antialiasingQuality = AntialiasingQuality.High;
                if (pixel) pixel.msaa = aa >= 4 ? 1 << (aa - 3) : 1;
            }

            if (urp)
            {
                // ---- upscaling: render scale + upscaler (full resolution only)
                int up = full ? s.upscaler : 0;
                if (up == 3 && !DlssAvailable) up = 1;                                          // no DLSS here: the temporal STP instead
                float scale = up == 0 ? 1f : RenderScales[Mathf.Clamp(s.renderScale, 0, RenderScales.Length - 1)];
                urp.renderScale = scale;
#if ENABLE_UPSCALER_FRAMEWORK
                urp.upscalerName = UpscalerId(up);
#else
                urp.upscalingFilter = up == 1 || up == 3 ? UpscalingFilterSelection.STP : up == 2 ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
#endif
                dlssWanted = up == 3 ? QualityFor(scale) : -1;

                // ---- shadows
                int sh = Mathf.Clamp(s.shadows, 0, ShadowNames.Length - 1);
                urp.mainLightShadowmapResolution = ShadowMap[sh];
                urp.additionalLightsShadowmapResolution = Mathf.Min(ShadowMap[sh], 2048);
                urp.shadowCascadeCount = Cascades[sh];
                urp.shadowDistance = Mathf.Min(ShadowDistance[sh], 60f + s.ViewRadius * 1.5f);
                urp.msaaSampleCount = 1;                                                       // MSAA lives on our render target

                // ---- renderer features: ambient occlusion, screen-space reflections
                foreach (var rd in urp.rendererDataList)
                {
                    if (!rd) continue;
                    foreach (var f in rd.rendererFeatures)
                    {
                        if (!f) continue;
                        if (f is ScreenSpaceAmbientOcclusion ao) SetAo(ao, s.ambientOcclusion);
#if URP_SCREEN_SPACE_REFLECTION
                        else if (f is ScreenSpaceReflectionRendererFeature ssr) ssr.SetActive(full && s.reflections > 0);
#endif
                    }
                }
            }
            if (sun)
            {
                sun.shadows = s.shadows == 0 ? LightShadows.None : s.shadows >= 3 && full ? LightShadows.Soft : LightShadows.Hard;   // soft edges only at full resolution
                var ld = sun.GetComponent<UniversalAdditionalLightData>();
                if (ld) ld.softShadowQuality = s.shadows >= 4 ? SoftShadowQuality.High : SoftShadowQuality.Medium;
            }

            // ---- the global volume: bloom, motion blur, reflections
            if (!volume)
            {
                volume = new GameObject("PostFX").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                bloom = volume.profile.Add<Bloom>(true);
                bloom.threshold.Override(0.9f); bloom.scatter.Override(0.55f);
                blur = volume.profile.Add<MotionBlur>(true);
                blur.quality.Override(MotionBlurQuality.Medium);
#if URP_SCREEN_SPACE_REFLECTION
                ssrVolume = volume.profile.Add<ScreenSpaceReflectionVolumeSettings>(true);
#endif
            }
            bloom.active = s.bloomLevel > 0;
            bloom.intensity.Override(s.bloomLevel >= 2 ? 1.6f : 0.9f);
            blur.active = s.motionBlur && full;
            blur.intensity.Override(0.35f);
#if URP_SCREEN_SPACE_REFLECTION
            ssrVolume.active = full && s.reflections > 0;
            ssrVolume.mode.Override(ScreenSpaceReflectionVolumeSettings.ReflectionMode.OpaquesOnly);
            ssrVolume.reflectionStrength.Override(0.85f);
            ssrVolume.resolution.Override(s.reflections >= 2 ? ScreenSpaceReflectionVolumeSettings.Resolution.Full : ScreenSpaceReflectionVolumeSettings.Resolution.Half);
            ssrVolume.marchingMethod.Override(ScreenSpaceReflectionVolumeSettings.MarchingMethod.Hierarchical);
            ssrVolume.maxRaySteps.Override(s.reflections >= 2 ? 96 : 48);
            ssrVolume.temporalFiltering.Override(true);
            ssrVolume.minimumSmoothness.Override(0.3f);
            ssrVolume.smoothnessFadeStart.Override(0.45f);
#endif

            // ---- texture filtering
            QualitySettings.anisotropicFiltering = s.textureFilter == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;
            Texture.SetGlobalAnisotropicFilteringLimits(s.textureFilter >= 2 ? 16 : 4, s.textureFilter >= 2 ? 16 : 4);
        }

        /// <summary>DLSS quality mode for a render scale: 4 DLAA (100 %), 2 quality, 1 balanced, 0 performance, 3 ultra performance.</summary>
        static int QualityFor(float scale) => scale >= 0.95f ? 4 : scale >= 0.64f ? 2 : scale >= 0.56f ? 1 : scale >= 0.45f ? 0 : 3;

        /// <summary>Once DLSS is the active upscaler, set its quality mode from the render scale (its options exist only
        /// after the pipeline picked it up). Call every frame; nothing to do unless DLSS was just chosen.</summary>
        public static void Tick()
        {
#if ENABLE_UPSCALER_FRAMEWORK && ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
            if (dlssWanted < 0) return;
            var f = typeof(UniversalRenderPipeline).GetField("upscaling", BindingFlags.NonPublic | BindingFlags.Static);
            var up = f != null ? f.GetValue(null) as Upscaling : null;
            if (up == null || up.activeUpscaler == null || up.activeUpscaler.name != DLSSIUpscaler.upscalerName) return;
            if (up.GetGlobalOptions(up.activeUpscaler) is DLSSOptions o)
                o.dlssQualityMode = dlssWanted == 4 ? UnityEngine.NVIDIA.DLSSQuality.DLAA : dlssWanted == 2 ? UnityEngine.NVIDIA.DLSSQuality.MaximumQuality
                                  : dlssWanted == 1 ? UnityEngine.NVIDIA.DLSSQuality.Balanced : dlssWanted == 0 ? UnityEngine.NVIDIA.DLSSQuality.MaximumPerformance
                                  : UnityEngine.NVIDIA.DLSSQuality.UltraPerformance;
            dlssWanted = -1;
#endif
        }

        static FieldInfo aoSettings;

        /// <summary>SSAO feature on/off and quality: low (half resolution, 4 samples), medium (full resolution, 8), high (12, bilateral blur).</summary>
        static void SetAo(ScreenSpaceAmbientOcclusion ao, int level)
        {
            ao.SetActive(level > 0);
            if (level <= 0) return;
            aoSettings ??= typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance);
            var st = aoSettings != null ? aoSettings.GetValue(ao) : null;
            if (st == null) return;
            var t = st.GetType();
            void Set(string name, object v)
            {
                var fi = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (fi == null) return;
                fi.SetValue(st, fi.FieldType.IsEnum ? System.Enum.ToObject(fi.FieldType, v) : v);
            }
            Set("Downsample", level == 1);
            Set("Samples", level == 1 ? 2 : level == 2 ? 1 : 0);                                // AOSampleOption: High 0 (12), Medium 1 (8), Low 2 (4)
            Set("BlurQuality", level == 1 ? 2 : level == 2 ? 1 : 0);                            // BlurQualityOptions: High 0 (bilateral), Medium 1, Low 2
            ao.Create();
        }
    }
}
