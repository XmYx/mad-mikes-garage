using UnityEngine;

namespace MadMax.World
{
    /// <summary>Flying insects around the player (user additions), as pooled particles: fireflies blinking over grass
    /// after dark (forest, jungle, villages), butterflies by day where things bloom, dragonflies darting over water,
    /// gnat clouds by the shore at dusk, and flies buzzing over carcasses. None in rain, snow, cold or storms; the
    /// crawling bugs (scorpions, spiders, roaches, beetles) are animals (<see cref="MadMax.Animals.AnimalLibrary"/>).</summary>
    public class Insects : MonoBehaviour
    {
        ParticleSystem fireflies, butterflies, darters, gnats, flies;
        float tick;
        static readonly Color[] Wings = { new Color(1f, 0.85f, 0.2f), new Color(1f, 0.55f, 0.15f), new Color(0.95f, 0.95f, 0.9f), new Color(0.45f, 0.65f, 1f), new Color(0.9f, 0.4f, 0.6f) };

        void Start()
        {
            fireflies = Make("Fireflies", 160, 0.35f, 1.2f);
            var col = fireflies.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 1f, 0.3f), 0f), new GradientColorKey(new Color(0.8f, 1f, 0.3f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.1f, 0.25f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.05f, 0.6f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            butterflies = Make("Butterflies", 60, 1.2f, 1.6f);
            darters = Make("Dragonflies", 30, 2.5f, 3f);
            gnats = Make("Gnats", 400, 0.8f, 4f);
            flies = Make("Flies", 200, 1.5f, 5f);
        }

        ParticleSystem Make(string name, int max, float noise, float freq)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var n = ps.noise; n.enabled = true; n.strength = noise; n.frequency = freq; n.scrollSpeed = 0.6f; n.quality = ParticleSystemNoiseQuality.Low;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Fx.TransparentMaterial(null);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            var gm = MadMax.Game.WastelandGame.Instance;
            if (gm && gm.Player && !Weather.Raining)                                                   // loops are requested every frame
            {
                var me = gm.Player.transform.position;
                foreach (var c in MadMax.Animals.Carcass.All)
                    if (c && (c.transform.position - me).sqrMagnitude < 8f * 8f) { MadMax.Audio.Sfx.Loop(c, "insects", 0.25f, 1.6f, 10f); break; }
            }
            if ((tick -= Time.deltaTime) > 0f) return;
            tick = 0.25f;
            var g = MadMax.Game.WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !g.Player || !t || t.World == null || Application.isBatchMode) return;
            if (Weather.Raining || Weather.Temperature < 6f || Storms.Dust > 0.2f || MadMax.Game.OccluderFade.Underground) return;
            var focus = g.Current ? g.Current.transform.position : g.Player.transform.position;
            var b = g.CurrentBiome;
            bool green = b == Biome.Forest || b == Biome.Tropical || b == Biome.Village;
            float dark = DayNight.Darkness;
            float h = DayNight.Hours;
            for (int i = 0; i < 8; i++)
            {
                var off = Random.insideUnitCircle * 28f;
                float x = focus.x + off.x, z = focus.z + off.y;
                float gy = t.HeightNoLoad(x, z);
                float water = t.WaterDepthNoLoad(x, z);
                bool wet = water > 0.05f;
                if (dark > 0.5f && green && !wet && Random.value < 0.5f)
                    Emit(fireflies, new Vector3(x, gy + Random.Range(0.3f, 1.8f), z), 0.05f, Color.white, Random.Range(3f, 6f));
                else if (dark < 0.3f && green && !wet && Random.value < 0.12f)
                    Emit(butterflies, new Vector3(x, gy + Random.Range(0.4f, 1.6f), z), 0.07f, Wings[Random.Range(0, Wings.Length)], Random.Range(5f, 9f));
                else if (wet && dark < 0.35f && Random.value < 0.2f)
                    Emit(darters, new Vector3(x, gy + water + Random.Range(0.3f, 1.2f), z), 0.08f, Random.value < 0.5f ? new Color(0.25f, 0.55f, 0.9f) : new Color(0.3f, 0.7f, 0.4f), Random.Range(3f, 6f));
                else if (wet && (h > 17.5f && h < 21f || h > 5f && h < 7f))
                {
                    var c = new Vector3(x, gy + water + 1.2f, z);                                   // a gnat cloud
                    for (int k = 0; k < 12; k++) Emit(gnats, c + Random.insideUnitSphere * 0.5f, 0.02f, new Color(0.1f, 0.08f, 0.06f, 0.85f), Random.Range(2f, 4f));
                }
            }
            foreach (var c in MadMax.Animals.Carcass.All)
            {
                if (!c) continue;
                var p = c.transform.position;
                if ((p - focus).sqrMagnitude > 25f * 25f) continue;
                for (int k = 0; k < 4; k++) Emit(flies, p + Vector3.up * Random.Range(0.2f, 0.9f) + Random.insideUnitSphere * 0.5f, 0.025f, new Color(0.05f, 0.05f, 0.05f, 0.9f), Random.Range(1.5f, 3f));
            }
        }

        static void Emit(ParticleSystem ps, Vector3 p, float size, Color c, float life)
        {
            var e = new ParticleSystem.EmitParams { position = p, startSize = size, startLifetime = life, startColor = c, velocity = Random.insideUnitSphere * 0.3f };
            ps.Emit(e, 1);
        }
    }
}
