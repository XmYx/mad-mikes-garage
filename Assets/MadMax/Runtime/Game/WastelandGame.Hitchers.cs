using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Survivors of a burned trader (2026-10-12): the trader crew who got clear of a car that burned out walk
    /// the road toward the nearest town; stop beside them and they climb into the passenger seat. Delivered (stopped
    /// inside the town), the town that mourned the car remembers it: its standing, their disposition, a hot meal and
    /// the news. Not saved (a reload leaves them to the road).</summary>
    public partial class WastelandGame
    {
        public readonly List<MadMax.Npc.Npc> Hitchers = new List<MadMax.Npc.Npc>();
        public const float HitchReach = 40f, PickUpReach = 9f, HitchStep = 22f;
        public const string HitchMeal = "food_stew";
        /// <summary>The last survivor delivered (tests).</summary>
        public static string LastDelivered;
        float hitchNext;

        /// <summary>A trader's car burned out at <paramref name="p"/>: its crew standing near become hitchers.</summary>
        public int StrandSurvivors(Vector3 p)
        {
            var town = NearestTown(p);
            if (town == null) return 0;
            int k = 0;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || n.proxy || n.companion || n.Hostile || n.Profile.Raider || n.hitchTown >= 0 || n.Driving || n.Riding) continue;
                if (n.convoy == null || n.convoy.raiders || Flat(n.transform.position - p) > HitchReach) continue;
                Strand(n, town);
                k++;
            }
            if (k > 0) Journal.Add("ROAD", "THE TRADER CREW OF THE BURNED CAR WALK TOWARD " + Market.TownName(town) + ". THEY COULD USE A LIFT");
            return k;
        }

        /// <summary>Make <paramref name="n"/> a hitcher walking to <paramref name="town"/> (tests use it directly).</summary>
        public void Strand(MadMax.Npc.Npc n, Settlement town)
        {
            n.convoy?.walkers.Remove(n);
            n.convoy = null;
            n.leaving = false;
            n.hitchTown = town.index;
            n.home = n.transform.position; n.homeRadius = 2f;
            if (!Hitchers.Contains(n)) Hitchers.Add(n);
        }

        void UpdateHitchers()
        {
            if (Hitchers.Count == 0 || Time.time < hitchNext || World == null) return;
            hitchNext = Time.time + 0.5f;
            for (int i = Hitchers.Count - 1; i >= 0; i--)
            {
                var n = Hitchers[i];
                if (!n || !n.Alive || n.hitchTown < 0 || n.hitchTown >= World.settlements.Count || n.Hostile) { if (n) n.hitchTown = -1; Hitchers.RemoveAt(i); continue; }
                var town = World.settlements[n.hitchTown];
                var tp = new Vector3(town.pos.x, 0f, town.pos.y);
                if (n.Riding)
                {
                    var v = n.RideVehicle;
                    if (v && Flat(v.transform.position - tp) < town.radius && Mathf.Abs(v.ForwardSpeed) < 2f) Deliver(n, town);
                    continue;
                }
                var me = n.transform.position;
                if (Flat(me - tp) < town.radius) { n.hitchTown = -1; Hitchers.RemoveAt(i); n.LetGo(); continue; }   // walked it themselves
                if (Current && Current.driveable && Mathf.Abs(Current.ForwardSpeed) < 1.5f && Flat(Current.transform.position - me) < PickUpReach)
                {
                    var seat = PassengerSeat.For(Current);
                    if (seat && !seat.Occupant)
                    {
                        n.Board(seat);
                        SetWaypoint(tp, Market.TownName(town), true);
                        Toast(n.KnownName + " CLIMBS IN: \"" + Market.TownName(town) + ", IF YOU'RE GOING THAT WAY\" (WAYPOINT SET)");
                        continue;
                    }
                }
                if (Flat(n.home - me) < 3f)                                                       // walk on: the next stretch toward town
                {
                    var step = me + (tp - new Vector3(me.x, 0f, me.z)).normalized * HitchStep;
                    step.y = me.y;
                    n.home = step; n.homeRadius = 2f;
                }
            }
        }

        void Deliver(MadMax.Npc.Npc n, Settlement town)
        {
            n.Unboard();
            n.hitchTown = -1;
            Hitchers.Remove(n);
            string tn = Market.TownName(town);
            n.State.disposition = Mathf.Min(100, n.State.disposition + 20);
            n.State.Set(NpcSave.Helped);
            var f = Factions.OfSettlement(town);
            Factions.Shift(f != Faction.None ? f : Faction.Settlers, 3);
            Factions.Shift(Faction.FuelGuild, 2);
            Inventory.AddItem(HitchMeal);
            TownNews.Post("A DRIVER BROUGHT IN THE CREW OF A BURNED TRADER CAR", new Vector3(town.pos.x, 0f, town.pos.y));
            Toast(n.KnownName + ": \"I OWE YOU ONE.\" " + tn + " WON'T FORGET IT (A HOT MEAL FROM THE STALL)");
            Journal.Add("ROAD", "BROUGHT " + n.KnownName + " SAFE TO " + tn);
            LastDelivered = n.Profile.id;
            n.LetGo();
        }
    }
}
