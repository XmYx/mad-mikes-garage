using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Companions drive on their own key (roadmap 10-07 suggestion): a fleet car is theirs to take only when
    /// its key is in the ignition, in the glovebox, or a key for it (a spare cut at a workbench) is in their pack. Over the
    /// radio, DRIVE A CAR TO MY WAYPOINT sends a companion (already driving, or with a startable fleet car within
    /// <see cref="CarReach"/>) along the roads to the player's waypoint (<see cref="AiDriver.DriveTo"/>); there they park,
    /// climb out and hold beside it.</summary>
    public static partial class Companions
    {
        /// <summary>Companion → the car they are delivering to the waypoint.</summary>
        static readonly Dictionary<Npc, VehicleDriver> delivering = new Dictionary<Npc, VehicleDriver>();
        /// <summary>The last car delivered to a waypoint (tests).</summary>
        public static VehicleDriver LastDelivered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetKeys() { delivering.Clear(); LastDelivered = null; }

        /// <summary>Can <paramref name="n"/> start <paramref name="v"/>: no key needed, the key in it or in the glovebox,
        /// hotwired, or a key for it in their pack.</summary>
        public static bool CanStart(Npc n, VehicleDriver v)
        {
            if (!v || !v.TryGetComponent<VehicleIgnition>(out var ign)) return true;
            if (ign.key == VehicleIgnition.Where.Glovebox) return true;
            return ign.CanStart(n && n.pack ? n.pack.inventory : null);
        }

        /// <summary>A companion is at the wheel of a fleet car (the waypoint order can use it).</summary>
        public static bool AnyDriving() { foreach (var n in Live) if (n && n.Alive && n.Driving) return true; return false; }

        static bool SendToWaypoint(WastelandGame g, Npc n)
        {
            var car = n.Driving ? n.DrivenCar : CarFor(g, n);
            if (!car) return false;
            if (!n.Driving) { n.order = 0; n.TakeWheel(car); }
            if (!car.TryGetComponent<AiDriver>(out var ai)) return false;
            ai.DriveTo(g.Waypoint);
            delivering[n] = car;
            return true;
        }

        static void Deliveries(WastelandGame g)
        {
            if (delivering.Count == 0) return;
            List<Npc> done = null;
            foreach (var kv in delivering)
            {
                var n = kv.Key; var car = kv.Value;
                if (!n || !n.Alive || !car || !n.Driving || n.DrivenCar != car) { (done ??= new List<Npc>()).Add(n); continue; }
                if (!car.TryGetComponent<AiDriver>(out var ai) || !ai.Arrived || Mathf.Abs(car.ForwardSpeed) > 0.5f) continue;
                n.LeaveWheel();
                n.order = 1; n.home = n.transform.position; n.homeYaw = n.transform.eulerAngles.y; n.homeRadius = 1.5f;
                LastDelivered = car;
                Call(g, n, "THE " + WastelandGame.Name(car) + " IS AT YOUR MARK. I'LL WAIT BY IT");
                (done ??= new List<Npc>()).Add(n);
            }
            if (done != null) foreach (var n in done) delivering.Remove(n);
        }
    }
}
