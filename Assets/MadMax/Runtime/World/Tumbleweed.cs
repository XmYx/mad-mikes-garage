using UnityEngine;

namespace MadMax.World
{
    /// <summary>A rolling tumbleweed (spawned by <see cref="WindDust"/>): bursts into twigs when a vehicle or a
    /// swing hits it hard.</summary>
    public class Tumbleweed : MonoBehaviour, MadMax.Items.IDamageable
    {
        static readonly Color Twig = new Color(0.6f, 0.47f, 0.3f, 0.7f);

        void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody && c.rigidbody.mass > 100f && c.relativeVelocity.magnitude > 3f) Burst(c.relativeVelocity);
        }

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source) => Burst(direction * 3f);

        void Burst(Vector3 v)
        {
            for (int i = 0; i < 6; i++)
                Fx.Smoke(transform.position + Random.insideUnitSphere * 0.3f, v * 0.3f + Random.insideUnitSphere + Vector3.up, 0.25f, Twig, 1.2f);
            MadMax.Audio.Sfx.Play("hit_wood", transform.position, 0.4f, 1.6f, 25f);
            Destroy(gameObject);
        }
    }
}
