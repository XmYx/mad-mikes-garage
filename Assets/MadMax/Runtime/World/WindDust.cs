using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Wind: a slowly veering direction with gusts, calm by default and strong in storms. Drives
    /// <see cref="Fx.Wind"/> (smoke, clouds), the shader global <c>_MadMaxWind</c> (trees, grass and vines bend, see
    /// PixelVoxel <c>_Sway</c>/<c>_SwayTip</c>) and the ambient wind loop. Over dry ground it lifts drifting dust,
    /// spins up dust devils in the desert and rolls tumbleweeds past the player (vehicles smash them).</summary>
    public class WindDust : MonoBehaviour
    {
        public static float Strength { get; private set; } = 2f;   // m/s
        public static float Gust { get; private set; }             // 0..1
        static readonly int WindId = Shader.PropertyToID("_MadMaxWind");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Strength = 2f; Gust = 0f; }

        float dustAcc, devilTimer = 20f, sampleTimer;
        Biome biome;
        float dryness;
        bool devilOn;
        Vector3 devilPos, devilVel;
        float devilLife;
        readonly List<Rigidbody> weeds = new List<Rigidbody>();
        Mesh weedMesh;
        Material weedMat;

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            // direction drifts over minutes, strength over tens of seconds, gusts over seconds
            float angle = (Mathf.PerlinNoise(t * 0.004f, 3.1f) - 0.5f) * 540f * Mathf.Deg2Rad;
            float storm = Mathf.Max(Weather.Raining ? (Weather.Snowing ? 0.7f : 1f) : 0f, Storms.Dust * 2.2f);   // dust storms blow hardest
            float target = Mathf.Lerp(1.2f, 4.5f, Mathf.PerlinNoise(t * 0.02f, 7.7f)) + storm * 4f;
            Strength = Mathf.MoveTowards(Strength, target, dt * 0.5f);
            Gust = Mathf.Clamp01(Mathf.PerlinNoise(t * 0.45f, 1.3f) * 1.6f - 0.5f + storm * 0.2f);
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Fx.Wind = dir * (Strength * (0.75f + 0.5f * Gust));
            Shader.SetGlobalVector(WindId, new Vector4(Fx.Wind.x, Gust, Fx.Wind.z, t));

            var g = MadMax.Game.WastelandGame.Instance;
            var terrain = DeformableTerrain.Instance;
            if (!g || !terrain || terrain.World == null || Application.isBatchMode) return;
            Transform f = g.Current ? g.Current.transform : g.Player ? g.Player.transform : null;
            if (!f || MadMax.Game.OccluderFade.Underground) return;
            var at = f.position;
            if ((sampleTimer -= dt) <= 0f)
            {
                sampleTimer = 1f;
                biome = terrain.BiomeAt(at.x, at.z);
                float wet = terrain.SurfaceAt(at.x, at.z).wet;
                dryness = (biome == Biome.Desert ? 1f : biome == Biome.Town || biome == Biome.Nuclear ? 0.7f : biome == Biome.City ? 0.5f : 0.15f)
                          * Mathf.Clamp01(1f - wet * 1.6f) * (Weather.LocalSnow > 0.3f ? 0f : 1f);
            }

            Dust(terrain, at, dt);
            Devil(terrain, at, dt);
            Tumbleweeds(terrain, at, dt);
        }

        static readonly Color DustCol = new Color(0.72f, 0.56f, 0.38f, 0.28f);

        void Dust(DeformableTerrain terrain, Vector3 at, float dt)
        {
            float w = Fx.Wind.magnitude;
            if (w < 3f || dryness <= 0.05f) return;
            dustAcc += dt * (w - 3f) * (w - 3f) * 1.4f * dryness;
            while (dustAcc > 1f)
            {
                dustAcc -= 1f;
                var up = -Fx.Wind.normalized;
                var p = at + up * Random.Range(4f, 18f) + Vector3.Cross(up, Vector3.up) * Random.Range(-14f, 14f);
                p.y = terrain.Height(p.x, p.z) + Random.Range(0.1f, 0.5f);
                var c = DustCol; c.a *= Random.Range(0.6f, 1.2f);
                Fx.Smoke(p, Fx.Wind * Random.Range(0.8f, 1.2f) + Vector3.up * Random.Range(0f, 0.4f), Random.Range(0.4f, 1.1f), c, 3.5f);
            }
        }

        /// <summary>A whirling column of dust wandering downwind through the desert.</summary>
        void Devil(DeformableTerrain terrain, Vector3 at, float dt)
        {
            if (!devilOn)
            {
                if (biome != Biome.Desert || dryness < 0.5f || Weather.Raining) return;
                if ((devilTimer -= dt) > 0f) return;
                devilTimer = Random.Range(40f, 120f);
                var up = -Fx.Wind.normalized;
                devilPos = at + up * 28f + Vector3.Cross(up, Vector3.up) * Random.Range(-12f, 12f);
                devilVel = Fx.Wind * 0.7f;
                devilLife = Random.Range(20f, 35f);
                devilOn = true;
            }
            devilLife -= dt;
            devilVel = Vector3.Lerp(devilVel, Fx.Wind * 0.7f + new Vector3(Mathf.PerlinNoise(Time.time * 0.3f, 1f) - 0.5f, 0f, Mathf.PerlinNoise(Time.time * 0.3f, 5f) - 0.5f) * 3f, dt);
            devilPos += devilVel * dt;
            float ground = terrain.Height(devilPos.x, devilPos.z);
            if (devilLife <= 0f || (devilPos - at).sqrMagnitude > 70f * 70f) { devilOn = false; MadMax.Audio.Sfx.Loop(this, "wind", 0f); return; }
            float fade = Mathf.Clamp01(devilLife / 4f);
            int puffs = Mathf.CeilToInt(dt * 60f);
            for (int i = 0; i < puffs; i++)
            {
                float h = Random.value, a = Time.time * 7f + Random.value * Mathf.PI * 2f, r = 0.4f + h * 1.6f;
                var p = new Vector3(devilPos.x + Mathf.Cos(a) * r, ground + h * 7f, devilPos.z + Mathf.Sin(a) * r);
                var tangent = new Vector3(-Mathf.Sin(a), 0.6f, Mathf.Cos(a)) * (3f + h * 2f);
                var c = DustCol; c.a = 0.35f * fade * (1f - h * 0.6f);
                Fx.Smoke(p, tangent + devilVel, 0.5f + h * 0.8f, c, 1.2f);
            }
            MadMax.Audio.Sfx.Loop(this, "wind", 0.5f * fade, 1.6f, 25f);
        }

        void Tumbleweeds(DeformableTerrain terrain, Vector3 at, float dt)
        {
            for (int i = weeds.Count - 1; i >= 0; i--)
            {
                var rb = weeds[i];
                if (!rb) { weeds.RemoveAt(i); continue; }
                var d = rb.position - at;
                if (d.sqrMagnitude > 50f * 50f || rb.position.y < terrain.Height(rb.position.x, rb.position.z) - 2f) { Destroy(rb.gameObject); weeds.RemoveAt(i); continue; }
                // rolling push from the wind, hops on gusts
                rb.AddForce(Fx.Wind * 1.1f, ForceMode.Force);
                if (Random.value < dt * Gust * 1.5f) rb.AddForce(Vector3.up * Random.Range(4f, 9f) + Fx.Wind * 0.8f, ForceMode.Impulse);
            }
            int want = biome == Biome.Desert && dryness > 0.4f && Fx.Wind.magnitude > 2f ? 3 : 0;
            if (weeds.Count >= want || Random.value > dt * 0.3f) return;
            var up = -Fx.Wind.normalized;
            var p = at + up * Random.Range(26f, 34f) + Vector3.Cross(up, Vector3.up) * Random.Range(-15f, 15f);
            p.y = terrain.Height(p.x, p.z) + 0.6f;
            weeds.Add(MakeWeed(p));
        }

        Rigidbody MakeWeed(Vector3 p)
        {
            if (!weedMesh)
            {
                var g = new VoxelGrid().Mat((byte)ResourceType.Wood);
                var twig = new[] { Pal.Hex("6e5030"), Pal.Hex("84643c"), Pal.Hex("9a7848"), Pal.Hex("b08c58") };
                for (int x = -4; x <= 4; x++) for (int y = -4; y <= 4; y++) for (int z = -4; z <= 4; z++)
                {
                    var v = new Vector3Int(x, y, z);
                    float r = ((Vector3)v).magnitude;
                    if (r > 4.3f || (r < 2.6f && Pal.Hash(v, 5) > 0.2f) || Pal.Hash(v, 9) > 0.42f) continue;
                    g.Set(v, Pal.Solid(twig[Mathf.Min(3, (int)(Pal.Hash(v, 3) * 4f))]));
                }
                weedMesh = VoxelMesher.Build(g, "Tumbleweed");
                var t = DeformableTerrain.Instance;
                weedMat = t && t.worldPropMaterial ? t.worldPropMaterial : MadMax.Game.WastelandGame.Instance.propMaterial;
            }
            var go = new GameObject("Tumbleweed", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(p, Random.rotation);
            go.GetComponent<MeshFilter>().sharedMesh = weedMesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = weedMat;
            go.AddComponent<SphereCollider>().radius = 0.33f;
            MadMax.Rendering.HDVisual.Dress(go, MadMax.Rendering.HDDomain.World, "Tumbleweed", HDProp.FlagsOf(weedMat));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f; rb.linearDamping = 0.15f; rb.angularDamping = 0.2f;
            go.AddComponent<Tumbleweed>();
            return rb;
        }

        void OnDestroy()
        {
            foreach (var w in weeds) if (w) Destroy(w.gameObject);
            if (weedMesh) Destroy(weedMesh);
            Shader.SetGlobalVector(WindId, Vector4.zero);
        }
    }
}
