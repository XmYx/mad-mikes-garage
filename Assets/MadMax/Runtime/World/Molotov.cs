using UnityEngine;

namespace MadMax.World
{
    /// <summary>Thrown fire bottle: shatters on impact and starts a fire.</summary>
    public class Molotov : MonoBehaviour
    {
        public bool authority = true;
        float armed = 0.08f;
        void Update() { armed -= Time.deltaTime; if (transform.position.y < -200f) Destroy(gameObject); }
        void OnCollisionEnter(Collision c)
        {
            if (armed > 0f) return;
            if (!authority) { Fx.Sparks(transform.position, Vector3.up, 12, new Color(1f, 0.7f, 0.3f)); Destroy(gameObject); return; }
            var p = transform.position;
            MadMax.Audio.Sfx.Play("glass_break", p, 0.9f);
            MadMax.Audio.Sfx.Play("explosion", p, 0.5f, 1.3f, 60f);
            var terrain = DeformableTerrain.Instance;
            bool grass = terrain && (terrain.BiomeAt(p.x, p.z) is Biome.Forest or Biome.Village or Biome.Tropical);
            Fire.Ignite(p, c.rigidbody && c.rigidbody.isKinematic == false ? c.transform : null, 25f, 1f, grass);
            for (int i = 0; i < 4; i++)
            {
                var q = p + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                if (terrain) q.y = terrain.Height(q.x, q.z);
                Fire.Ignite(q, null, Random.Range(8f, 14f), 0.7f, grass);
            }
            Fx.Sparks(p, Vector3.up, 12, new Color(1f, 0.7f, 0.3f));
            Destroy(gameObject);
        }
    }
}
