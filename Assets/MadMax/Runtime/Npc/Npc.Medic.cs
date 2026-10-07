using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A companion tending the player: walks over once the fight is done, kneels for <see cref="TendSeconds"/>
    /// and treats with the first-aid kit (their pack or a fleet car's compartment, <see cref="tendFrom"/>) or loose
    /// bandages and splints from their pack (<c>WastelandGame.CompanionTreat</c>). Set by <see cref="Companions"/>; a
    /// foe interrupts it.</summary>
    public partial class Npc
    {
        public const float TendSeconds = 3f, TendReach = 1.4f;
        /// <summary>Who this NPC is fighting (null = nobody).</summary>
        public Npc Foe => foe;
        /// <summary>Walking over to treat the player.</summary>
        [System.NonSerialized] public bool tending;
        /// <summary>The compartment the kit comes from (null = their own pack).</summary>
        [System.NonSerialized] public MadMax.Building.Container tendFrom;
        /// <summary>Wounds treated by the last tend (−1 = gave up, 0 none / still going).</summary>
        [System.NonSerialized] public int tendOutcome;
        float tendT, tendGiveUp;

        public void Tend(MadMax.Building.Container from)
        {
            tending = true; tendFrom = from; tendOutcome = 0; tendT = TendSeconds; tendGiveUp = Time.time + 40f;
        }

        void EndTend(int outcome) { tending = false; tendFrom = null; tendOutcome = outcome; }

        /// <summary>The tending step; false once there is nothing left to do.</summary>
        bool TendTick(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || g.Current || g.Vitals.Dead || Time.time > tendGiveUp || !g.NeedsTending) { EndTend(-1); return false; }
            var p = g.Player.transform.position;
            var to = Flat(p - me);
            if (to.magnitude > TendReach) { move = Toward(p - to.normalized * (TendReach - 0.4f)); speed = to.magnitude > 6f ? 3.4f : 1.6f; tendT = TendSeconds; return true; }
            faceTarget = p; faceUntil = Time.time + 0.5f;
            if ((tendT -= dt) > 0f) return true;
            EndTend(Mathf.Max(-1, g.CompanionTreat(this, tendFrom)));
            return false;
        }
    }
}
