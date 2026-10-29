using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Animals leave a mark and a breath (2026-10-29): each footfall in mud, snow or loose sand prints a hoof, paw
    /// or bird's foot under the leg that came down (diagonal pairs for four legs, sized by the animal's weight), and below
    /// 4 °C warm-blooded animals breathe clouds from the muzzle (a horse's or cow's from both nostrils, a hen's a wisp),
    /// quicker when they run (<see cref="ColdBreath"/>). Within 30 m of the camera; not while flying, swimming or dead.</summary>
    public partial class Animal
    {
        public const float PrintReach = 30f;
        /// <summary>Prints laid (tests).</summary>
        public static int Prints;
        float breathClock = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetPrintStatics() => Prints = 0;

        void StepAndBreathe(float dt, float s, int half, bool stepped)
        {
            if (state == State.Dead || Def.plan == BodyPlan.Snake || Def.plan == BodyPlan.Arthropod || Mesh.sprawl) return;   // cold-blooded: no clouds, no print worth drawing
            bool flying = Def.plan == BodyPlan.Bird && air > 0.2f;
            if (head && !flying)
            {
                if (breathClock < 0f) breathClock = Random.value;
                float exertion = Mathf.Clamp01(s / Mathf.Max(1f, Def.run));
                if (state == State.Flee || state == State.Charge) exertion = Mathf.Max(exertion, 0.7f);
                float muzzle = (Def.head + Def.snout) * 0.08f * Def.scale * (Def.plan == BodyPlan.Bird ? 0.6f : 0.75f);
                var fwd = head.forward;
                bool nostrils = Def.plan == BodyPlan.Quadruped && Def.mass >= 150f;
                ColdBreath.Tick(ref breathClock, head.position + fwd * muzzle, fwd, exertion, Mathf.Max(0.03f, Def.mass / 70f) * Size, dt, nostrils);
            }
            if (!stepped || s < 0.3f || flying || legs == null || legs.Length == 0 || !Building.PieceBits.Near(transform.position, PrintReach)) return;
            float len = Mathf.Clamp(0.04f + 0.02f * Mathf.Sqrt(Def.mass * Size / 10f), 0.04f, 0.2f);
            float wid = Def.plan == BodyPlan.Bird ? len * 0.7f : len * 0.8f;
            var dir = transform.forward;
            float ground = transform.position.y;
            if (Def.plan == BodyPlan.Bird) { Lay(legs[(half & 1) % legs.Length], dir, len, wid, ground); return; }
            if (legs.Length < 4) return;
            // diagonal pairs: front-left with back-right, then front-right with back-left
            if ((half & 1) == 0) { Lay(legs[0], dir, len, wid, ground); Lay(legs[3], dir, len * 0.95f, wid, ground); }
            else { Lay(legs[1], dir, len, wid, ground); Lay(legs[2], dir, len * 0.95f, wid, ground); }
        }

        static void Lay(Transform leg, Vector3 dir, float len, float wid, float ground)
        {
            if (!leg) return;
            var p = leg.position; p.y = ground;
            Fx.Print(p, dir, len, wid);
            Prints++;
        }
    }
}
