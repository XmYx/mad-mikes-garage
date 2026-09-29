using UnityEngine;

namespace MadMax.World
{
    /// <summary>A lit charge on a fuse (thrown dynamite, pipe bomb): hisses and sparks, then blows (Explosion.Blast).</summary>
    public class Explosive : MonoBehaviour
    {
        public float fuse = 4f, radius = 3f, power = 6f, crater = 1f;
        public bool smoke;                // smoke bomb: a cloud instead of a blast
        public bool authority = true;
        public GameObject source;
        Transform wick;

        void Start() { wick = transform.Find("Wick"); }

        void Update()
        {
            fuse -= Time.deltaTime;
            var spark = wick ? wick.position : transform.position + Vector3.up * 0.1f;
            if (Random.value < Time.deltaTime * 20f) Fx.Sparks(spark, Vector3.up, 1, new Color(1f, 0.8f, 0.4f));
            MadMax.Audio.Sfx.Loop(this, "sizzle", 0.35f, 1.8f, 15f);
            if (transform.position.y < -200f) { Destroy(gameObject); return; }
            if (fuse > 0f) return;
            MadMax.Audio.Sfx.Loop(this, "sizzle", 0f, 1f, 15f);
            if (smoke) { SmokeScreen.Pop(transform.position, 5f); MadMax.Audio.Sfx.Play("pop", transform.position, 0.8f, 0.7f, 30f); Destroy(gameObject); return; }
            Explosion.Blast(transform.position, radius, power, crater, source, authority);
            Destroy(gameObject);
        }
    }
}
