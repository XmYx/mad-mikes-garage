using MadMax.Building;
using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Animals at the bedding (2026-10-18): at nightfall kept livestock walk into a bedded stable near their
    /// home, each to its own spot in the straw, and lie down there until morning. A night on fresh bedding mends them:
    /// a limp heals half again as fast and an owned animal gets health back (out in the open it only gets it from
    /// treatment). See <see cref="Stable.BedSpot"/>.</summary>
    public partial class Animal
    {
        public const float BeddingReach = 30f;
        Stable bedStable;
        bool onBedding;
        float bedBest = float.MaxValue, bedProgressAt;
        int bedAlt;
        /// <summary>Why the last bedding think did what it did (tests).</summary>
        public string BeddingDebug => "dark " + DayNight.Darkness.ToString("0.00") + " stable " + (bedStable ? "y" : "n") + " state " + state + " best " + bedBest.ToString("0.0");

        /// <summary>Lying on fresh bedding right now (tests).</summary>
        public bool OnBedding => onBedding && bedStable && bedStable.Bedded;

        bool BeddingAnimal => Def != null && owned && !Def.flies && Def.plan != BodyPlan.Snake && Def.plan != BodyPlan.Arthropod
            && !Def.guard && state != State.Ridden && !Def.nocturnal;

        /// <summary>From <c>ThinkKept</c>: after dark, to the bedding; true while that is what the animal is doing.</summary>
        bool BeddingThink(WastelandGame g)
        {
            if (!BeddingAnimal || DayNight.Darkness < 0.6f) { onBedding = false; bedStable = null; return false; }
            if (!bedStable || !bedStable.Bedded) { bedStable = Stable.BeddedNear(home, BeddingReach); onBedding = false; bedBest = float.MaxValue; bedProgressAt = Time.time; bedAlt = 0; }
            if (!bedStable) return false;
            var spot = bedStable.BedSpot(BedIndex + bedAlt);
            var me = transform.position;
            float d = Flat(spot - me).magnitude;
            if (d < bedBest - 0.1f) { bedBest = d; bedProgressAt = Time.time; }
            bool stuck = !onBedding && Time.time - bedProgressAt > 2.5f;
            if (stuck && !bedStable.Inside(me + Vector3.up * 0.4f) && bedAlt < 3)                 // a stall wall or the manger in the way: another spot
            { bedAlt++; bedBest = float.MaxValue; bedProgressAt = Time.time; stuck = false; }
            // in the straw: the spot, inside the stalls once it stops getting closer, or as close as it gets after a few tries
            bool settle = d < 0.9f || (stuck && (bedStable.Inside(me + Vector3.up * 0.4f) || d < 2.5f));
            if (!settle && !onBedding)
            {
                state = State.Wander; goal = spot; stateUntil = Time.time + 4f;
                SetMove(spot - me, Def.walk);
                return true;
            }
            onBedding = true;
            state = State.Sleep; stateUntil = Time.time + 30f;
            SetMove(Vector3.zero, 0f);
            return true;
        }

        /// <summary>A stable spot per animal, spread by its key so a pen's animals don't pile up on one spot.</summary>
        int BedIndex => ((key ?? "").GetHashCode() & 0x7fffffff) % 8;

        /// <summary>From <c>CareTick</c>: what a night on the straw mends over <paramref name="days"/>.</summary>
        void BeddingCare(float days)
        {
            if (!OnBedding) return;
            if (limp > 0f) limp = Mathf.Max(0f, limp - days * 0.3f);                              // on top of the stable's own 0.6 a day
            if (owned && bleed <= 0f && sick <= 0f) health = Mathf.Min(Def.health, health + Def.health * 0.25f * days);
        }
    }
}
