using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A blast (dynamite, pipe bombs): blows a crater into the ground, breaks rock and walls (IDamageable),
    /// flings loose bodies, hurts whoever stands too close, and is heard far away.</summary>
    public static class Explosion
    {
        static readonly Collider[] hits = new Collider[64];
        static readonly HashSet<Object> done = new HashSet<Object>();

        /// <param name="radius">Destruction radius (m); people are hurt out to 1.6×.</param>
        /// <param name="power">Hits dealt at the centre (1 = a sledgehammer blow).</param>
        /// <param name="crater">Crater depth at the centre (m); 0 = none.</param>
        public static void Blast(Vector3 at, float radius, float power, float crater, GameObject source, bool authority)
        {
            MadMax.Audio.Sfx.Play("explosion", at, 1f, Random.Range(0.85f, 1.05f), 250f);
            var fx = DebrisSystem.Instance;
            for (int i = 0; i < 18; i++) Fx.Smoke(at + Random.insideUnitSphere * radius * 0.4f, Random.insideUnitSphere * 3f + Vector3.up * 3f, 0.9f, new Color(0.3f, 0.28f, 0.26f, 0.8f), 3.5f);
            Fx.Sparks(at, Vector3.up, 30, new Color(1f, 0.75f, 0.35f));
            Fx.Shockwave(at, radius);                                                          // the ring of air and dust
            Fx.Flash(at + Vector3.up, new Color(1f, 0.65f, 0.3f), radius * 5f, 8f, 0.15f);
            if (fx) for (int i = 0; i < 20; i++) fx.EmitPuff(at, new Color32(255, 200, 90, 255), 0.08f, Random.insideUnitSphere * 9f + Vector3.up * 4f, 0.25f);
            var game = MadMax.Game.WastelandGame.Instance;
            if (game)
            {
                float d = Vector3.Distance(game.Player.transform.position, at);
                if (game.cameraRig) game.cameraRig.Shake(Mathf.Clamp(30f - d, 0f, 25f));
                if (d < 25f) MadMax.Game.ScreenFader.Flash(new Color(1f, 0.85f, 0.6f), 0.12f + (25f - d) * 0.01f);
            }
            if (MadMax.Npc.NpcDirector.Instance) MadMax.Npc.NpcDirector.Instance.Noise(at, 160f);
            if (!authority) return;

            // the crater
            var terrain = DeformableTerrain.Instance;
            if (terrain && crater > 0f && at.y < terrain.Height(at.x, at.z) + radius)
            {
                terrain.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, at, radius * 0.9f, crater, 0);
                MadMax.Net.NetSession.Instance?.SendTerraform((byte)DeformableTerrain.TerraOp.Dig, at, radius * 0.9f, crater, 0);
            }
            // everything that can be broken, once per object, weaker with distance
            done.Clear();
            int n = Physics.OverlapSphereNonAlloc(at, radius * 1.6f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                var dmg = c.GetComponentInParent<IDamageable>();
                var key = dmg as Object;
                if (dmg != null && key && done.Add(key))
                {
                    var p = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(at) : c.ClosestPoint(at);
                    float fall = Mathf.Clamp01(1f - Vector3.Distance(p, at) / (radius * 1.6f));
                    if (fall > 0f) dmg.ApplyHit(p, (p - at).normalized, power * fall, radius * (0.4f + 0.6f * fall), source);
                }
                var rb = c.attachedRigidbody;
                // a shove that scales with the charge, lighter things flung further; never more than ~14 m/s of kick
                if (rb && !rb.isKinematic && done.Add(rb)) rb.AddExplosionForce(Mathf.Min(power * 90f * Mathf.Sqrt(rb.mass), 14f * rb.mass), at, radius * 2f, 0.6f, ForceMode.Impulse);
            }
            // the player
            if (game && game.Vitals && !game.Current)
            {
                float d = Vector3.Distance(game.Player.transform.position + Vector3.up, at);
                if (d < radius * 1.6f) game.Vitals.Hurt(power * 9f * (1f - d / (radius * 1.6f)), "BLAST");
            }
        }
    }
}
