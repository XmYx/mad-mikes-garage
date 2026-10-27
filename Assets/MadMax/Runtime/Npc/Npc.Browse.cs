using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A customer at the player's stall (2026-10-27): someone idling nearby (a resident or wanderer, not hostile,
    /// not busy) is sent by <see cref="Browse"/> to stand in front of the counter, looks the wares over, the sale lands
    /// (<c>buy</c>), and they wander on. Anything else coming up (a fight, a fright, a visit) ends it without a sale.</summary>
    public partial class Npc
    {
        public const float BrowseGiveUp = 30f;
        MadMax.Building.PlayerStall browseStall;
        Vector3 browseAt;
        float browseLook, browseUntil = -1f, browseQuitAt;
        bool browseArrived;
        System.Action<Npc> onBuy, onQuit;

        /// <summary>On the way to (or standing at) a stall to buy (tests).</summary>
        public bool Browsing => browseStall;
        /// <summary>Standing at the counter looking the wares over (tests).</summary>
        public bool BrowseSettled => browseStall && browseArrived;

        /// <summary>Free to be a customer: idling, friendly, not someone's companion, keeper, courier or guest.</summary>
        public bool MayBrowse =>
            Alive && Available && !proxy && !Hostile && !Profile.Raider && !Profile.Cast && convoy == null && !raiding && !leaving && !companion
            && aboard == null && !Visiting && !tending && !fireTarget && !browseStall && Watching == null
            && (Profile.role == NpcRole.Resident || Profile.role == NpcRole.Wanderer) && (mode == Mode.Wander || mode == Mode.Stand);

        /// <summary>Walk to <paramref name="stand"/> (in front of <paramref name="stall"/>), look <paramref name="look"/>
        /// s, then <paramref name="buy"/>; <paramref name="quit"/> runs when it ends without a sale.</summary>
        public void Browse(MadMax.Building.PlayerStall stall, Vector3 stand, float look, System.Action<Npc> buy, System.Action<Npc> quit)
        {
            EndTv();
            EndChore();
            browseStall = stall; browseAt = stand; browseLook = look; browseArrived = false;
            browseUntil = -1f; browseQuitAt = Time.time + BrowseGiveUp;
            onBuy = buy; onQuit = quit;
        }

        /// <summary>From the think loop: the walk over and the look; false when not browsing.</summary>
        bool BrowseTick(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            if (!browseStall) { if (onQuit != null || onBuy != null) EndBrowse(false); return false; }
            if ((mode != Mode.Wander && mode != Mode.Stand) || Hostile || Time.time > browseQuitAt) { EndBrowse(false); return false; }
            var counter = browseStall.transform.position + Vector3.up * 0.9f;
            if (!browseArrived)
            {
                float off = Flat(browseAt - me).magnitude;
                if (off > 0.45f) { move = Toward(browseAt); speed = off > 6f ? 1.4f : 1.15f; return true; }
                browseArrived = true;
                browseUntil = Time.time + browseLook;
                hasGoal = false;
            }
            faceTarget = counter; faceUntil = Time.time + 0.5f;
            if (Time.time >= browseUntil) EndBrowse(true);
            return true;
        }

        /// <summary>Done at the stall: <paramref name="bought"/> lands the sale, else the stall is told it fell through.</summary>
        public void EndBrowse(bool bought)
        {
            var buy = onBuy; var quit = onQuit;
            browseStall = null; onBuy = null; onQuit = null; browseArrived = false; browseUntil = -1f;
            if (bought) buy?.Invoke(this); else quit?.Invoke(this);
            repath = Time.time + Random.Range(0.5f, 2f);                                       // then on their way
        }
    }
}
