using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-30 (1 Hz hook, from the 1028 chain). The railway remodel lives on its pieces:
    /// turning wheelsets, brake and flange sparks (<c>RailWheels</c>), derailing and re-railing (<c>Train.Derail</c>),
    /// the cab crew (<c>CabCrew</c>), the stationmaster (<c>Stationmaster</c>); the bonnet steaming in the cold is in
    /// <c>VehicleSystems.BonnetSteam</c>. Here: the RE-RAIL context option beside a derailed train.</summary>
    public partial class WastelandGame
    {
        public const float RerailReach = 6f;

        /// <summary>The derailed train with a car within reach of <paramref name="p"/> (null: none).</summary>
        public static Train DerailedNear(Vector3 p, float reach)
        {
            foreach (var t in Train.All)
            {
                if (!t || !t.Derailed) continue;
                foreach (var c in t.cars)
                    if (c && (c.transform.position - p).sqrMagnitude < (reach + c.def.Half) * (reach + c.def.Half)) return t;
            }
            return null;
        }

        void RerailOptions(ContextTarget t, List<ContextOption> into)
        {
            if (t.item != null || !Player || Current) return;
            var train = DerailedNear(Player.transform.position, RerailReach);
            if (!train) return;
            string blocked = train.Rerailing ? "UNDER WAY" : Mathf.Abs(train.speed) > 0.3f ? "STILL MOVING" : Inventory.GetItem("tool_jack") <= 0 ? "NEEDS A JACK" : null;
            into.Add(new ContextOption
            {
                label = "RE-RAIL THE " + train.trainName,
                blocked = blocked,
                run = () => { if (train && train.StartRerail(Player.transform)) Toast("JACKING THE " + train.trainName + " BACK ONTO THE RAILS - STAY BESIDE IT"); }
            });
        }

        void UpdateNightly1030()
        {
            if (!ContextActions.Has("rail.rerail")) ContextActions.Register("rail.rerail", (g, t, into) => g.RerailOptions(t, into));
        }
    }
}
