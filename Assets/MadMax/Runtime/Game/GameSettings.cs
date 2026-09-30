using System;
using MadMax.Rendering;
using MadMax.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.Game
{
    /// <summary>Player preferences (PlayerPrefs JSON). Apply() pushes them into cameras, lights, shaders, terrain and vehicles.</summary>
    [Serializable]
    public class GameSettings
    {
        public bool manualTransmission;
        public int pixelHeightIndex = 3;
        public int outline = 1;          // 0 off, 1 = 1 px, 2 = 2 px
        public int shadows = 2;          // 0 off, 1 low, 2 high
        public bool bloom = true;
        public float brightness = 1f;
        public int lod = 1;              // 0 low, 1 medium, 2 high, 3 ultra
        public int resolutionIndex = -1; // -1 = current
        public bool fullscreen = true;
        public bool vsync = true;
        public bool vector;              // full-resolution rendering instead of pixel art
        public bool dither;              // ordered dither between light bands
        public int lightDetail = 2;      // 0 vehicle lights only, 1 low, 2 high
        public bool lineOfSight = true;  // hide objects the character cannot see
        public bool intro = true;        // boot film + flyover; off = straight to the neon sign and menu
        public int version;              // settings format (see Load migration)
        const int CurrentVersion = 2;
        public float radioVolume = 0.8f; // master gain for all radios
        public float sfxVolume = 1f;     // sound effects
        public bool blood = true;        // blood bursts and stains on injuries
        // controls & camera
        public string keys = "";         // rebound keys (Controls): "Act=Key;..." (defaults omitted)
        public float mouseSensitivity = 1f;
        public bool invertY;
        public float fovFirst = 60f;     // vertical degrees (≈ 90° horizontal at 16:9)
        public float fovThird = 55f;
        public bool cameraShake = true;
        public int deformation = 2;      // CAR DEFORMATION: index into DeformationScales
        public static readonly float[] DeformationScales = { 0f, 0.5f, 1f, 1.6f, 2.4f };
        public static readonly string[] DeformationNames = { "OFF", "LIGHT", "NORMAL", "HEAVY", "EXTREME" };
        public float DeformationScale => DeformationScales[Mathf.Clamp(deformation, 0, DeformationScales.Length - 1)];
        // interface
        public int hudScale;             // 0 = with the pixel size, else the HUD's own height index into PixelHeights
        public bool colourBlind;         // blue/orange instead of red/green on bars and lamps
        public bool metric = true;       // km/h and °C (off: mph and °F)
        public bool radioCaptions;       // subtitles for DJ talk, news and weather
        public bool voiceCaptions = true;   // speech bubbles over talking NPCs
        public bool flatWorld;              // no horizon curve (the planet stays flat to the eye)
        public bool hints = true;        // context hints (the first times you meet something)
        public int autosaveMinutes = 10; // 0 off
        // audio
        public float ambientVolume = 1f;
        public float masterVolume = 1f, vehicleVolume = 1f, weaponVolume = 1f, voiceVolume = 1f;   // audio channels (user additions)
        public float uiVolume = 1f;
        // handling assists
        public bool simFlight;           // raw flight model: no rate commands, auto-level, bank or AoA limits
        public bool vintageSidecar;      // outfits pull to the chair and lift it like the real thing

        public static readonly string[] LightNames = { "VEHICLES ONLY", "LOW", "HIGH" };
        public static readonly int[] AutosaveChoices = { 0, 5, 10, 20, 30 };

        /// <summary>Speed for display: km/h or mph.</summary>
        public string Speed(float kmh) => metric ? Mathf.RoundToInt(kmh) + " KM/H" : Mathf.RoundToInt(kmh * 0.6214f) + " MPH";
        public float SpeedValue(float kmh) => metric ? kmh : kmh * 0.6214f;
        public string SpeedUnit => metric ? "KM/H" : "MPH";
        /// <summary>Temperature for display: °C or °F (the 3x5 font has no degree sign).</summary>
        public string Temp(float c) => metric ? Mathf.RoundToInt(c) + "C" : Mathf.RoundToInt(c * 1.8f + 32f) + "F";
        public int HudHeight => hudScale <= 0 ? PixelHeight : PixelHeights[Mathf.Clamp(hudScale - 1, 0, PixelHeights.Length - 1)];

        public static readonly int[] PixelHeights = { 180, 240, 270, 320, 360, 480, 540 };
        public static readonly string[] LodNames = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
        static readonly float[] LodRadius = { 48f, 72f, 96f, 128f };
        static readonly float[] ShadowDistance = { 0f, 80f, 190f };

        const string Key = "madmax.settings";
        static GameSettings current;
        public static GameSettings Current => current ??= Load();

        static GameSettings Load()
        {
            GameSettings s;
            try { s = Profile.HasKey(Key) ? JsonUtility.FromJson<GameSettings>(Profile.GetString(Key)) : new GameSettings { version = CurrentVersion }; }
            catch { s = new GameSettings { version = CurrentVersion }; }
            // v2: the intro became a pre-rendered boot film; old "intro off" choices predate it, so they reset once
            if (s.version < 2) { s.intro = true; s.version = CurrentVersion; s.Save(); }
            return s;
        }

        public void Save()
        {
            Profile.SetString(Key, JsonUtility.ToJson(this));
            Profile.Save();
        }

        public int PixelHeight => PixelHeights[Mathf.Clamp(pixelHeightIndex, 0, PixelHeights.Length - 1)];
        public float ViewRadius => LodRadius[Mathf.Clamp(lod, 0, LodRadius.Length - 1)];

        public static Resolution[] Resolutions
        {
            get
            {
                var list = new System.Collections.Generic.List<Resolution>();
                foreach (var r in Screen.resolutions)
                    if (!list.Exists(x => x.width == r.width && x.height == r.height)) list.Add(r);
                return list.ToArray();
            }
        }

        public string ResolutionName
        {
            get
            {
                var all = Resolutions;
                if (resolutionIndex < 0 || resolutionIndex >= all.Length) return $"{Screen.width}X{Screen.height}";
                return $"{all[resolutionIndex].width}X{all[resolutionIndex].height}";
            }
        }

        static Volume volume;

        public void Apply(WastelandGame game)
        {
            AudioListener.volume = Mathf.Clamp01(masterVolume);
            var rig = game ? game.cameraRig : null;
            if (rig && rig.pixel)
            {
                rig.pixel.pixelHeight = vector ? Mathf.Max(360, Screen.height) : PixelHeight;
                rig.pixel.snapToTexels = !vector;
            }
            Shader.SetGlobalFloat("_MadMaxDither", dither ? 1f : 0f);
            LightBudget.Detail = lightDetail;
            LineOfSight.Enabled = lineOfSight;

            // global shader controls (defaults are 0 so the editor look is unchanged)
            Shader.SetGlobalFloat("_MadMaxOutlineDelta", outline - 1);
            Shader.SetGlobalFloat("_MadMaxBrightnessDelta", brightness - 1f);

            if (game && game.sun)
            {
                game.sun.shadows = shadows == 0 ? LightShadows.None : LightShadows.Hard;
                game.sun.shadowBias = 0.6f;          // stable, acne-free voxel shadows
                game.sun.shadowNormalBias = 1.2f;
            }
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Min(ShadowDistance[Mathf.Clamp(shadows, 0, 2)], 60f + ViewRadius * 1.5f);

            if (rig)
            {
                rig.fpsFov = Mathf.Clamp(fovFirst, rig.fpsFovRange.x, rig.fpsFovRange.y);
                rig.thirdFov = Mathf.Clamp(fovThird, 35f, 90f);
                rig.fogEnd = ViewRadius - 4f;
                rig.fogStart = rig.fogEnd * 0.55f;
                var cam = rig.pixel.GetComponent<Camera>();
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = bloom;
                if (!volume)
                {
                    volume = new GameObject("PostFX").AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    var b = volume.profile.Add<Bloom>(true);
                    b.intensity.Override(0.9f);
                    b.threshold.Override(0.9f);
                    b.scatter.Override(0.55f);
                }
                volume.enabled = bloom;
            }
            if (DeformableTerrain.Instance) DeformableTerrain.Instance.SetViewRadius(ViewRadius);

            QualitySettings.vSyncCount = vsync ? 1 : 0;
            var all = Resolutions;
            var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (resolutionIndex >= 0 && resolutionIndex < all.Length) Screen.SetResolution(all[resolutionIndex].width, all[resolutionIndex].height, mode);
            else if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
        }
    }
}
