using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Physical projectile (roadmap 13): arrows, crossbow bolts, slingshot stones and flares fly with gravity and
    /// travel time; a segment raycast per frame finds what they hit (IDamageable takes the hit, NPCs bleed from arrows and
    /// bolts). Arrows and bolts stick where they land and can be picked up again (walk over them); flares burn bright for
    /// half a minute, set dry ground alight and are seen from far off (NPC noise).</summary>
    public class Projectile : MonoBehaviour
    {
        public enum Kind { Arrow, Bolt, Stone, Flare }
        public Kind kind;
        public Vector3 velocity;
        public float power, gravity = 9.81f, life = 6f;
        public string pickup;                 // item recovered where it sticks (null: none)
        public GameObject source;
        bool stuck;
        float stuckUntil, collectT;
        Light glow;
        static readonly Mesh[] meshes = new Mesh[4];
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public static Projectile Launch(Kind kind, Vector3 from, Vector3 velocity, float power, string pickup, GameObject source, Material mat)
        {
            var go = new GameObject(kind.ToString(), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(velocity));
            go.GetComponent<MeshFilter>().sharedMesh = MeshFor(kind);
            go.GetComponent<MeshRenderer>().sharedMaterial = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : mat;
            var p = go.AddComponent<Projectile>();
            p.kind = kind; p.velocity = velocity; p.power = power; p.pickup = pickup; p.source = source;
            if (kind == Kind.Stone) p.life = 4f;
            if (kind == Kind.Flare)
            {
                p.glow = new GameObject("Glow").AddComponent<Light>();
                p.glow.transform.SetParent(go.transform, false);
                p.glow.type = LightType.Point; p.glow.color = new Color(1f, 0.25f, 0.15f); p.glow.range = 16f; p.glow.intensity = 3f; p.glow.shadows = LightShadows.None;
            }
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (glow) glow.intensity = (stuck ? 3.5f : 2.5f) * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 12f, 0.3f));
            if (kind == Kind.Flare && Random.value < dt * 20f) Fx.Smoke(transform.position, Vector3.up * 0.8f + Random.insideUnitSphere * 0.3f, 0.15f, new Color(1f, 0.45f, 0.35f, 0.6f), 1.2f);
            if (stuck) { Stuck(dt); return; }
            if ((life -= dt) < 0f) { Destroy(gameObject); return; }
            var from = transform.position;
            velocity += Vector3.down * gravity * dt;
            var step = velocity * dt;
            float len = step.magnitude;
            if (len < 1e-5f) return;
            int n = Physics.RaycastNonAlloc(from, step / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (source && hits[i].collider.transform.IsChildOf(source.transform)) continue;
                if (hits[i].distance < bd) { bd = hits[i].distance; best = i; }
            }
            if (best >= 0) { Hit(hits[best]); return; }
            var to = from + step;
            var terrain = DeformableTerrain.Instance;
            if (terrain)
            {
                float ground = terrain.Height(to.x, to.z);
                float water = terrain.WaterLevel(to.x, to.z);
                if (!float.IsNaN(water) && to.y < water && water > ground)
                {
                    Fx.Smoke(to, Vector3.up, 0.12f, new Color(0.8f, 0.88f, 0.95f, 0.7f), 0.5f);
                    MadMax.Audio.Sfx.Play("splash", to, 0.3f, 1.8f, 15f);
                    Destroy(gameObject);                                                     // lost in the water
                    return;
                }
                if (to.y < ground) { Land(new Vector3(to.x, ground, to.z), null); return; }
            }
            transform.SetPositionAndRotation(to, Quaternion.LookRotation(velocity));
        }

        void Hit(RaycastHit h)
        {
            var dir = velocity.normalized;
            float speed = Mathf.Clamp01(velocity.magnitude / 40f);
            var target = h.collider.GetComponentInParent<MadMax.Items.IDamageable>();
            if (target != null) target.ApplyHit(h.point, dir, power * (0.5f + 0.5f * speed), kind == Kind.Stone ? 0.08f : 0.05f, source);
            var npc = h.collider.GetComponentInParent<MadMax.Npc.Npc>();
            if (npc && (kind == Kind.Arrow || kind == Kind.Bolt)) npc.Bleed(12f);
            if (h.rigidbody && !h.rigidbody.isKinematic) h.rigidbody.AddForceAtPosition(dir * power * 6f, h.point, ForceMode.Impulse);
            MadMax.Audio.Sfx.Play(npc ? "punch" : "hit_wood", h.point, 0.5f, kind == Kind.Stone ? 1.5f : 1.2f, 20f);
            Land(h.point, npc ? npc.transform : h.collider.transform);
        }

        void Land(Vector3 at, Transform onto)
        {
            stuck = true;
            var dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.down;
            transform.position = at - dir * (kind == Kind.Stone ? 0f : 0.12f);             // sunk in a little
            if (kind == Kind.Stone)
            {
                // a pebble: back on the ground as a stone to pick up
                PickupSystem.Instance?.Spawn(MadMax.Items.ResourceType.Stone, 1, at + Vector3.up * 0.1f, Vector3.up);
                Destroy(gameObject);
                return;
            }
            if (onto && !onto.GetComponentInParent<DeformableTerrain>()) transform.SetParent(onto, true);   // rides along with what it hit
            stuckUntil = Time.time + (kind == Kind.Flare ? 25f : 60f);
            if (kind == Kind.Flare)
            {
                var terrain = DeformableTerrain.Instance;
                bool dry = terrain && (terrain.BiomeAt(at.x, at.z) is Biome.Forest or Biome.Village or Biome.Tropical);
                Fire.Ignite(at, onto && onto.GetComponentInParent<Rigidbody>() ? onto : null, 10f, 0.4f, dry && !Weather.Raining);
                MadMax.Npc.NpcDirector.Instance?.Noise(at, 60f);                              // everyone sees a flare go up
            }
        }

        void Stuck(float dt)
        {
            if (Time.time > stuckUntil || (!transform.parent && transform.position.y < -200f)) { Destroy(gameObject); return; }
            if (pickup == null || (collectT -= dt) > 0f) return;
            collectT = 0.25f;
            var g = WastelandGame.Instance;
            if (!g || !g.Player || g.Current || (g.Player.transform.position - transform.position).sqrMagnitude > 1.6f * 1.6f) return;
            var n = transform.parent ? transform.parent.GetComponentInParent<MadMax.Npc.Npc>() : null;
            if (n && n.Alive) return;
            g.Inventory.AddItem(pickup);
            g.Toast("PICKED UP " + MadMax.Items.ItemCatalog.Name(pickup));
            Destroy(gameObject);
        }

        static Mesh MeshFor(Kind k)
        {
            int i = (int)k;
            if (meshes[i]) return meshes[i];
            if (MadMax.Rendering.HDBits.On) return meshes[i] = MadMax.Rendering.HDBits.Projectile(k == Kind.Arrow ? 0 : k == Kind.Bolt ? 1 : k == Kind.Stone ? 2 : 3);
            var g = new MadMax.Voxel.VoxelGrid();
            switch (k)
            {
                case Kind.Arrow:
                    g.Box(0, 0, -5, 0, 0, 4, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Wood, 3, 1301));
                    g.Set(0, 0, 5, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Chrome[2]));
                    g.Set(1, 0, -5, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Crimson[3])); g.Set(-1, 0, -5, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Crimson[3]));
                    break;
                case Kind.Bolt:
                    g.Box(0, 0, -3, 0, 0, 3, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Metal, 2));
                    g.Set(0, 0, 4, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Chrome[3]));
                    g.Set(0, 1, -3, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Black[1]));
                    break;
                case Kind.Stone:
                    g.Set(0, 0, 0, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Metal[3]));
                    break;
                default:
                    g.Box(0, 0, -1, 0, 0, 1, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Crimson[4]));
                    g.Set(0, 0, 2, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.LightY));
                    break;
            }
            g.Bevel();
            meshes[i] = MadMax.Voxel.VoxelMesher.Build(g, "Projectile_" + k, 0.04f);
            return meshes[i];
        }
    }
}
