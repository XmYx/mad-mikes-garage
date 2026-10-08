using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A salvager with one of the player's stolen parts on the stall can be asked who brought it in: a vendor
    /// who trusts the player (disposition ≥ <see cref="TrailTrust"/>) says so, anyone else takes a [CHA 6] check (one
    /// try a day). The answer names the gang and sets a waypoint on where they ride
    /// (<c>WastelandGame.FollowStolenTrail</c>).</summary>
    public partial class Dialogue
    {
        public const int TrailTrust = 20;
        static readonly Dictionary<string, int> trailAsked = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTrail() => trailAsked.Clear();

        void StolenTrail()
        {
            if (P.kind != "salvage" || P.Raider || S.disposition <= -20) return;
            if (trailAsked.TryGetValue(P.id, out int day) && day == Day) return;
            var town = g.NearestTown(npc.transform.position);
            string id = Trade.StolenOnStall(town);
            if (id == null || !Trade.StolenBy.TryGetValue(id, out string gang)) return;
            string what = id.StartsWith("part:") ? id.Substring(5).Replace('_', ' ').ToUpperInvariant() : MadMax.Items.ItemIds.Name(id);
            bool trusts = S.disposition >= TrailTrust;
            Add((trusts ? "" : "[CHA 6] ") + "THAT " + what + " IS MINE. WHO BROUGHT IT IN?", () =>
            {
                asked = true;
                trailAsked[P.id] = Day;
                if (trusts || Check(6f))
                {
                    g.FollowStolenTrail(id, gang, npc.transform.position);
                    line = "THE " + gang + ". DIDN'T HEAR IT FROM ME. " + (g.GangWhereabouts(gang, npc.transform.position, out var at)
                        ? "LAST I SAW THEM THEY WERE HEADED " + NpcLore.Compass(at.x - npc.transform.position.x, at.z - npc.transform.position.z) + "."
                        : "NOBODY'S SEEN THEM ON THE ROADS SINCE.");
                }
                else { Change(-1); line = "I DON'T ASK WHERE THINGS COME FROM. YOU WANT IT BACK, IT'S FOR SALE."; }
                Hub(false);
            }, trusts ? "THEY TRUST YOU ENOUGH TO SAY" : "ONE TRY A DAY");
        }
    }
}
