using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A burning spot: flames, drifting smoke, light, heat damage and spread to flammable things nearby.
    /// Rain, snow and water put it out; it burns out when its fuel is used up.</summary>
    public class Fire : MonoBehaviour
    {
        public static readonly List<Fire> All = new List<Fire>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { All.Clear(); }
        public const int MaxFires = 48;

        public float intensity = 1f;     // 0..1 (size of the flames)
        public float fuel = 30f;         // seconds of burning at full size
        public float radius = 1.2f;
        public bool ground;              // grass fire (spreads along dry vegetation biomes)
        static bool Authority = true;

        Light glow;
        float tick, emit;
        readonly Collider[] near = new Collider[24];

        /// <summary>Start a fire (or feed an existing one close by).</summary>
        public static Fire Ignite(Vector3 pos, Transform attach, float fuel, float intensity = 0.6f, bool ground = false)
        {
            foreach (var f in All)
                if (f && (f.transform.position - pos).sqrMagnitude < 1.2f * 1.2f) { f.fuel = Mathf.Max(f.fuel, fuel); f.intensity = Mathf.Max(f.intensity, intensity); return f; }
            if (All.Count >= MaxFires) return null;
            var terrain = DeformableTerrain.Instance;
            if (terrain && terrain.WaterDepth(pos.x, pos.z) > 0.2f && pos.y < terrain.WaterLevel(pos.x, pos.z) + 0.2f) return null;
            var go = new GameObject("Fire");
            go.transform.position = pos;
            if (attach) go.transform.SetParent(attach, true);
            var fire = go.AddComponent<Fire>();
            fire.fuel = fuel; fire.intensity = intensity; fire.ground = ground;
            var net = MadMax.Net.NetSession.Instance;
            if (net && !MadMax.Net.NetSession.Applying) net.SendFire(pos, fuel, intensity, ground);
            return fire;
        }

        /// <summary>Pour water on fires within a radius (watering can, bucket): each litre knocks out ~8 s of fuel.
        /// Returns how many fires it reached.</summary>
        public static int Douse(Vector3 pos, float radius, float litres)
        {
            int n = 0;
            foreach (var f in All)
            {
                if (!f || (f.transform.position - pos).sqrMagnitude > radius * radius) continue;
                f.fuel = Mathf.Max(0f, f.fuel - litres * 8f);
                f.intensity *= 0.5f;
                n++;
            }
            return n;
        }

        public static bool Flammable(GameObject go)
        {
            string n = go.name;
            return n.StartsWith("Tree") || n.StartsWith("Bush") || n.StartsWith("Log") || n.StartsWith("Haystack") || n.StartsWith("Farmhouse")
                || n.StartsWith("Shack") || n.StartsWith("Crate") || n.StartsWith("Table") || n.StartsWith("Fence") || n.StartsWith("Placed_");
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 0.6f;
            glow.type = LightType.Point; glow.shadows = LightShadows.None;
            glow.color = new Color(1f, 0.55f, 0.2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            MadMax.Audio.Sfx.Loop(this, "fire", Mathf.Clamp01(0.3f + intensity * 0.6f), 1f, 25f);
            float wet = Weather.Wetness * (Weather.Raining ? 1f : 0.4f) + Weather.Snow * 0.5f;
            fuel -= dt * (0.4f + intensity) * (1f + wet * 3f);
            intensity = Mathf.MoveTowards(intensity, fuel > 0f ? Mathf.Clamp01(fuel / 10f + 0.3f) * (1f - wet * 0.6f) : 0f, dt * 0.3f);
            if (intensity <= 0.02f && fuel <= 0f) { Destroy(gameObject); return; }

            var p = transform.position;
            float s = 0.4f + intensity;
            emit += dt * (6f + intensity * 20f);
            while (emit > 1f)
            {
                emit -= 1f;
                var off = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * radius * 0.5f * s;
                var flame = Color.Lerp(new Color(1f, 0.85f, 0.3f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.9f), Random.value);
                Fx.Smoke(p + off + Vector3.up * 0.1f, Vector3.up * Random.Range(1.2f, 2.4f) * s + Fx.Wind * 0.2f, Random.Range(0.25f, 0.5f) * s, flame, 0.45f);
                if (Random.value < 0.45f)
                {
                    float g = Random.Range(0.12f, 0.26f);
                    Fx.Smoke(p + off + Vector3.up * (0.8f + s), Vector3.up * 1.4f + Fx.Wind * 0.8f, Random.Range(0.8f, 1.6f) * s, new Color(g, g, g, 0.55f), 5f);
                }
            }
            if (glow)
            {
                glow.range = 4f + intensity * 8f;
                glow.intensity = (1.2f + intensity * 3f) * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 9f, p.x));
            }

            tick -= dt;
            if (tick > 0f) return;
            tick = 0.5f;
            var net = MadMax.Net.NetSession.Instance;
            Authority = !(net && net.IsClient);
            var terrain = DeformableTerrain.Instance;
            if (terrain && terrain.WaterDepth(p.x, p.z) > 0.3f && p.y < terrain.WaterLevel(p.x, p.z)) { fuel = 0f; intensity *= 0.3f; }
            Burn(p, 0.5f);
        }

        void Burn(Vector3 p, float dt)
        {
            float reach = radius * (0.8f + intensity);
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.5f, reach, near, ~0, QueryTriggerInteraction.Ignore);
            var game = MadMax.Game.WastelandGame.Instance;
            for (int i = 0; i < n; i++)
            {
                var c = near[i];
                // players
                var pc = c.GetComponentInParent<MadMax.Game.PlayerCharacter>();
                if (pc && game && pc == game.Player && game.Vitals && !game.Current) { game.Vitals.Hurt(9f * intensity * dt * 2f, "BURNED"); continue; }
                if (!Authority) continue;                     // clients mirror fires; the server burns and spreads
                // props: char them away and spread
                var d = c.GetComponentInParent<DestructibleVoxels>();
                if (d && Flammable(d.gameObject))
                {
                    var hit = c.ClosestPoint(p);
                    if (Random.value < 0.35f * intensity) d.ApplyHit(hit, Vector3.down, 0.25f * intensity, 0.18f, gameObject);
                    if (Random.value < 0.08f * intensity * (1f - Weather.Wetness)) Ignite(hit, d.transform, 20f, 0.4f);
                    continue;
                }
                var placed = c.GetComponentInParent<MadMax.Building.Placeable>();
                if (placed && Random.value < 0.06f * intensity * (1f - Weather.Wetness)) { Ignite(c.bounds.center, placed.transform, 15f, 0.4f); continue; }
                // vehicles: heat the parts, cook off the fuel
                var sys = c.GetComponentInParent<MadMax.Vehicles.VehicleSystems>();
                if (sys) sys.Heat(intensity * dt);
            }
            // ground fire: creeps across dry forest / meadow / tropical ground
            if (Authority && ground && intensity > 0.4f && Weather.Wetness < 0.25f && Weather.Snow < 0.1f && Random.value < 0.25f)
            {
                var terrain = DeformableTerrain.Instance;
                var q = p + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(1.2f, 2.2f) + Fx.Wind * 0.3f;
                if (terrain)
                {
                    var b = terrain.BiomeAt(q.x, q.z);
                    if ((b == Biome.Forest || b == Biome.Village || b == Biome.Tropical) && terrain.WaterDepth(q.x, q.z) <= 0f && terrain.SurfaceAt(q.x, q.z).road < 0.3f)
                    {
                        q.y = terrain.Height(q.x, q.z);
                        Ignite(q, null, Random.Range(8f, 16f), 0.5f, true);
                    }
                }
            }
        }
    }
}
