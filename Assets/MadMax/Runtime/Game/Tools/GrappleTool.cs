using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Grappling hook (roadmaps 1, 22): throw it at a wall, roof edge, tree or rock within 24 m and reel in. A
    /// top surface lands you on it; a wall leaves you hanging below the hook, pulling yourself over the edge when there
    /// is one in reach. A loose part or crate (under 300 kg) is reeled in to your feet instead. Aim with RMB like a gun.
    /// People and animals give it nothing to bite.</summary>
    public class GrappleTool : HandTool
    {
        public const float Range = 24f;
        LineRenderer rope;
        Vector3 hookAt;
        PlayerCharacter holder;
        Rigidbody pulled;
        float pullUntil;
        static Material ropeMat;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => ropeMat = null;

        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (!g || user.Traversing) return;
            var origin = user.Eye ? user.Eye.position : user.transform.position + Vector3.up * 1.6f;
            var dir = g.AimDirection(user, origin, Range + 6f);
            MadMax.Audio.Sfx.Play("chain", origin, 0.3f, 1.4f);
            if (!Physics.Raycast(origin, dir, out var hit, Range, ~0, QueryTriggerInteraction.Ignore) || hit.collider.transform.IsChildOf(user.transform))
            {
                g.Toast("THE HOOK FALLS SHORT - " + Mathf.RoundToInt(Range) + " M OF ROPE");
                return;
            }
            var rb = hit.collider.attachedRigidbody;
            if (hit.collider.GetComponentInParent<MadMax.Npc.Npc>() || hit.collider.GetComponentInParent<MadMax.Animals.Animal>()) { g.Toast("NOTHING SOLID TO HOOK"); return; }
            if (rb && !rb.isKinematic && rb.mass < 300f && !rb.GetComponent<MadMax.Vehicles.VehicleDriver>())
            {
                // a loose part, a crate: reel it in instead
                pulled = rb; hookAt = hit.point; holder = user; pullUntil = Time.time + 1.6f;
                MadMax.Audio.Sfx.Play("hit_metal", hit.point, 0.5f, 1.4f);
                g.Toast("REELING IT IN");
                return;
            }
            if (!user.Zip(hit.point, hit.normal)) { g.Toast("THE HOOK SLIPS"); return; }
            hookAt = hit.point; holder = user;
            MadMax.Audio.Sfx.Play("hit_metal", hit.point, 0.5f, 1.7f);
            g.Stats?.Practice(Skill.Athletics, 1f);
        }

        void FixedUpdate()
        {
            if (!pulled || !holder) return;
            var to = holder.transform.position + Vector3.up * 1f + holder.transform.forward * 1.2f - pulled.worldCenterOfMass;
            if (Time.time > pullUntil || to.magnitude < 1.4f) { pulled.linearVelocity *= 0.3f; pulled = null; return; }
            pulled.WakeUp();
            pulled.linearVelocity = Vector3.Lerp(pulled.linearVelocity, to.normalized * Mathf.Min(9f, to.magnitude * 3f) + Vector3.up * 1.5f, 0.25f);
            hookAt = pulled.worldCenterOfMass;
        }

        void LateUpdate()
        {
            if (!holder || !(holder.Zipping || pulled)) { if (rope) rope.enabled = false; return; }
            if (!rope)
            {
                if (!ropeMat) ropeMat = Fx.TransparentMaterial(null);
                rope = new GameObject("GrappleRope").AddComponent<LineRenderer>();
                rope.sharedMaterial = ropeMat;
                rope.widthMultiplier = 0.025f; rope.positionCount = 2; rope.useWorldSpace = true;
                rope.startColor = rope.endColor = new Color(0.62f, 0.52f, 0.36f, 1f);
            }
            rope.enabled = true;
            rope.SetPosition(0, transform.position);
            rope.SetPosition(1, hookAt);
        }

        void OnDisable() { if (rope) rope.enabled = false; }
        void OnDestroy() { if (rope) Destroy(rope.gameObject); }
    }
}
