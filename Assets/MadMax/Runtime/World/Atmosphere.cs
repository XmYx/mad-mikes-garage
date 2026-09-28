using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Fog and clouds. <see cref="Fog"/> (0..1) rises for morning mist, evening haze, rain/snow and damp places
    /// (lakes, forest, tropics); CameraRig turns it into render fog per view. Clouds: drifting cloud shadows on everything
    /// (PixelVoxel globals <c>_MadMaxClouds</c>/<c>_MadMaxCloudOffset</c>) — sparse fair-weather cover or a heavy dark deck
    /// when it rains — plus voxel cloud puffs in the sky for the perspective views.</summary>
    public class Atmosphere : MonoBehaviour
    {
        public static float Fog { get; private set; }
        public static float CloudCover { get; private set; }
        public static bool SkyVisible;             // set by CameraRig: perspective views show the puffs
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Fog = 0f; CloudCover = 0f; SkyVisible = false; }

        static readonly int CloudsId = Shader.PropertyToID("_MadMaxClouds"), OffsetId = Shader.PropertyToID("_MadMaxCloudOffset");
        const float CloudScale = 1f / 70f;         // noise cells per metre
        const int PuffCount = 14;

        public Transform focus;
        Vector2 offset;
        float localDamp, dampTimer;
        readonly List<Transform> puffs = new List<Transform>();
        Material puffMat;
        readonly List<Mesh> puffMeshes = new List<Mesh>();

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            var g = MadMax.Game.WastelandGame.Instance;
            var f = focus ? focus : g && g.Current ? g.Current.transform : g && g.Player ? g.Player.transform : null;
            Vector3 at = f ? f.position : cam ? cam.transform.position : Vector3.zero;

            // damp places: sampled twice a second around the focus
            if ((dampTimer -= dt) <= 0f)
            {
                dampTimer = 0.5f;
                localDamp = 0f;
                var t = DeformableTerrain.Instance;
                if (t)
                {
                    var b = t.BiomeAt(at.x, at.z);
                    localDamp = b == Biome.Tropical ? 0.45f : b == Biome.Forest ? 0.3f : b == Biome.Nuclear ? 0.2f : 0f;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        if (!float.IsNaN(t.WaterLevel(at.x + Mathf.Cos(a) * 18f, at.z + Mathf.Sin(a) * 18f))) { localDamp = Mathf.Max(localDamp, 0.4f); break; }
                    }
                }
            }

            float h = DayNight.Hours;
            float morning = Bell(h, 6.3f, 1.6f), evening = Bell(h, 19.6f, 1.4f) * 0.55f;
            float precip = Weather.Raining ? (Weather.Snowing ? 0.8f : 0.65f) : 0f;
            float target = Mathf.Clamp01(Mathf.Max(morning * (0.45f + localDamp), evening * (0.6f + localDamp)) + precip + Weather.Wetness * 0.15f + localDamp * 0.25f);
            Fog = Mathf.MoveTowards(Fog, target, dt * 0.05f);

            float cover = Weather.Raining ? 0.9f : 0.3f + 0.18f * Mathf.Sin(Time.time * 0.01f);
            CloudCover = Mathf.MoveTowards(CloudCover, cover, dt * 0.02f);
            var wind = Fx.Wind;
            offset += new Vector2(wind.x, wind.z) * (dt * 2.2f * CloudScale);
            Shader.SetGlobalVector(CloudsId, new Vector4(CloudCover, Weather.Raining ? 0.6f : 0.4f, CloudScale, 0f));
            Shader.SetGlobalVector(OffsetId, new Vector4(-offset.x, -offset.y, 0f, 0f));

            UpdatePuffs(cam, dt);
        }

        static float Bell(float x, float centre, float width) { float d = (x - centre) / width; return Mathf.Exp(-d * d); }

        void UpdatePuffs(Camera cam, float dt)
        {
            if (!cam || Application.isBatchMode) return;
            if (puffs.Count == 0) BuildPuffs();
            var c = cam.transform.position;
            var wind = Fx.Wind * 2.2f;
            var tint = Color.Lerp(new Color(1.15f, 1.12f, 1.08f), new Color(0.55f, 0.57f, 0.62f), Weather.Raining ? 1f : 0f);
            tint = DayNight.Tint(tint);
            if (puffMat) puffMat.SetColor("_Tint", new Color(tint.r, tint.g, tint.b, 1f));
            int visible = Mathf.RoundToInt(PuffCount * Mathf.Clamp01(CloudCover * 1.4f));
            for (int i = 0; i < puffs.Count; i++)
            {
                var p = puffs[i];
                bool on = SkyVisible && i < visible;
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
                if (!on) continue;
                var pos = p.position + wind * dt;
                var d = new Vector2(pos.x - c.x, pos.z - c.z);
                if (d.magnitude > 85f) { var r = Random.insideUnitCircle.normalized * 80f; pos = new Vector3(c.x - r.x, 0f, c.z - r.y); }   // recycle on the upwind side
                pos.y = c.y + 38f + (i % 4) * 6f;
                p.position = pos;
            }
        }

        void BuildPuffs()
        {
            var game = MadMax.Game.WastelandGame.Instance;
            if (!game || !game.propMaterial) return;
            puffMat = new Material(game.propMaterial);
            puffMat.SetFloat("_Unlit", 0.55f); puffMat.SetFloat("_OutlinePx", 0f); puffMat.SetFloat("_NoFog", 1f); puffMat.SetFloat("_SnowMask", 0f);
            var rnd = new System.Random(4242);
            for (int m = 0; m < 4; m++)
            {
                var g = new VoxelGrid();
                int blobs = 5 + m;
                for (int b = 0; b < blobs; b++)
                {
                    float cx = (float)(rnd.NextDouble() * 2 - 1) * (10 + m * 3), cz = (float)(rnd.NextDouble() * 2 - 1) * 6, rr = 4f + (float)rnd.NextDouble() * 4f;
                    for (int x = (int)(cx - rr); x <= cx + rr; x++)
                    for (int y = 0; y <= rr * 0.8f; y++)
                    for (int z = (int)(cz - rr); z <= cz + rr; z++)
                    {
                        float dx = x - cx, dy = y * 1.5f, dz = z - cz;
                        if (dx * dx + dy * dy + dz * dz <= rr * rr) g.Set(x, y, z, Pal.Solid(y > rr * 0.4f ? Pal.Cream[4] : Pal.Cream[2]));
                    }
                }
                puffMeshes.Add(VoxelMesher.Build(g, "CloudPuff" + m, 0.9f));
            }
            for (int i = 0; i < PuffCount; i++)
            {
                var go = new GameObject("Cloud", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = puffMeshes[i % puffMeshes.Count];
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = puffMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                var r = Random.insideUnitCircle * 80f;
                go.transform.position = new Vector3(r.x, 60f, r.y);
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.SetActive(false);
                puffs.Add(go.transform);
            }
        }

        void OnDestroy()
        {
            if (puffMat) Destroy(puffMat);
            foreach (var m in puffMeshes) if (m) Destroy(m);
            Shader.SetGlobalVector(CloudsId, Vector4.zero);
        }
    }
}
