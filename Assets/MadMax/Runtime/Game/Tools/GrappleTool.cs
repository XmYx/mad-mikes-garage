using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Grappling hook (roadmap 22): throw it at a wall, roof edge, tree or rock within 24 m and reel in. A
    /// top surface lands you on it; a wall leaves you hanging below the hook, pulling yourself over the edge when there
    /// is one in reach. Aim with RMB like a gun. People and loose light things give it nothing to bite.</summary>
    public class GrappleTool : HandTool
    {
        public const float Range = 24f;
        LineRenderer rope;
        Vector3 hookAt;
        PlayerCharacter holder;
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
            if (hit.collider.GetComponentInParent<MadMax.Npc.Npc>() || (rb && !rb.isKinematic && rb.mass < 300f))
            {
                g.Toast("NOTHING SOLID TO HOOK");
                return;
            }
            if (!user.Zip(hit.point, hit.normal)) { g.Toast("THE HOOK SLIPS"); return; }
            hookAt = hit.point; holder = user;
            MadMax.Audio.Sfx.Play("hit_metal", hit.point, 0.5f, 1.7f);
            g.Stats?.Practice(Skill.Athletics, 1f);
        }

        void LateUpdate()
        {
            if (!holder || !holder.Zipping) { if (rope) rope.enabled = false; return; }
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
