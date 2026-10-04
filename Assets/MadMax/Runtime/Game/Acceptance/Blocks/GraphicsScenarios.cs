using System.Collections;
using System.Collections.Generic;
using MadMax.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.Game.Acceptance
{
    /// <summary>The graphics options (<see cref="GraphicsQuality"/>) reach the renderer: full-resolution rendering in the
    /// third-person view, each preset (low / high / ultra / DLSS) sets the URP asset (render scale, upscaler, shadow map,
    /// cascades), the camera's anti-aliasing, the render target's MSAA, the SSAO / SSR features and the volume, renders
    /// frames without errors; DLSS falls back to STP where it can't run. The player's settings come back afterwards.</summary>
    public static class GraphicsScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new GraphicsOptions(); }
    }

    class GraphicsOptions : Scenario
    {
        public override string Id => "graphics.options";
        public override float Timeout => 120f;

        static void Features(UniversalRenderPipelineAsset urp, out bool ao, out bool ssr)
        {
            ao = ssr = false;
            foreach (var rd in urp.rendererDataList)
            {
                if (!rd) continue;
                foreach (var f in rd.rendererFeatures)
                {
                    if (f is ScreenSpaceAmbientOcclusion) ao |= f.isActive;
#if URP_SCREEN_SPACE_REFLECTION
                    if (f is ScreenSpaceReflectionRendererFeature) ssr |= f.isActive;
#endif
                }
            }
        }

        struct Preset { public string name; public int aa, up, scale, shadows, ao, refl, bloom; public bool blur; }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var s = GameSettings.Current;
            string saved = JsonUtility.ToJson(s);
            var view = g.cameraRig.mode;
            var errors = new List<string>();
            void Log(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception) errors.Add(msg); }
            Application.logMessageReceived += Log;
            try
            {
                if (g.Current) g.Exit();
                s.vector = true;
                g.cameraRig.mode = ViewMode.ThirdPerson;
                var presets = new[]
                {
                    new Preset { name = "low", aa = 1, up = 2, scale = 2, shadows = 1, ao = 1, refl = 0, bloom = 1 },
                    new Preset { name = "high", aa = 3, up = 1, scale = 3, shadows = 3, ao = 2, refl = 1, bloom = 1 },
                    new Preset { name = "ultra", aa = 5, up = 0, scale = 5, shadows = 4, ao = 3, refl = 2, bloom = 2, blur = true },
                    new Preset { name = "dlss", aa = 2, up = 3, scale = 3, shadows = 3, ao = 2, refl = 1, bloom = 1 },
                };
                var cam = g.cameraRig.pixel.GetComponent<Camera>();
                foreach (var p in presets)
                {
                    s.antiAliasing = p.aa; s.upscaler = p.up; s.renderScale = p.scale; s.shadows = p.shadows; s.ambientOcclusion = p.ao;
                    s.reflections = p.refl; s.bloomLevel = p.bloom; s.motionBlur = p.blur;
                    s.Apply(g);
                    float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
                    for (int i = 0; i < 30; i++) yield return null;
                    float fps = (Time.frameCount - f0) / Mathf.Max(0.001f, Time.realtimeSinceStartup - t0);
                    var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                    var data = cam.GetUniversalAdditionalCameraData();
                    Features(urp, out bool aoOn, out bool ssrOn);
                    int up = p.up == 3 && !GraphicsQuality.DlssAvailable ? 1 : p.up;
                    float wantScale = up == 0 ? 1f : GraphicsQuality.RenderScales[p.scale];
                    var wantAa = p.aa == 1 ? AntialiasingMode.FastApproximateAntialiasing : p.aa == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : p.aa == 3 ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
                    int wantMsaa = p.aa >= 4 ? 1 << (p.aa - 3) : 1;
                    var target = g.cameraRig.pixel.Target;
                    c.Note($"{p.name}: scale {urp.renderScale:0.00} upscaler '{urp.upscalerName}', shadow map {urp.mainLightShadowmapResolution} x{urp.shadowCascadeCount}, AA {data.antialiasing}, MSAA {target.antiAliasing}, SSAO {aoOn}, SSR {ssrOn}, sun {g.sun.shadows}, {fps:0} fps (editor)");
                    c.Check(Mathf.Abs(urp.renderScale - wantScale) < 0.01f, $"{p.name}: render scale {urp.renderScale:0.00}");
                    c.Check(data.antialiasing == wantAa && target.antiAliasing == wantMsaa, $"{p.name}: anti-aliasing {data.antialiasing}, MSAA {target.antiAliasing}");
                    c.Check(urp.mainLightShadowmapResolution == new[] { 512, 1024, 2048, 4096, 4096 }[p.shadows], $"{p.name}: shadow map {urp.mainLightShadowmapResolution}");
                    c.Check(aoOn == p.ao > 0 && ssrOn == p.refl > 0, $"{p.name}: SSAO {aoOn}, SSR {ssrOn}");
                    if (p.up == 3) c.Check(GraphicsQuality.DlssAvailable || urp.upscalerName == "Spatial-Temporal Post-Processing", $"DLSS where it can run, else STP ('{urp.upscalerName}'; DLSS {(GraphicsQuality.DlssAvailable ? "available" : "not available here")})");
                    c.Screenshot(p.name);
                    yield return null;
                }
                c.Check(errors.Count == 0, errors.Count == 0 ? "no render errors" : "errors: " + string.Join(" | ", errors.GetRange(0, Mathf.Min(3, errors.Count))));
            }
            finally
            {
                Application.logMessageReceived -= Log;
                JsonUtility.FromJsonOverwrite(saved, s);
                g.cameraRig.mode = view;
                s.Apply(g);
            }
        }
    }
}
