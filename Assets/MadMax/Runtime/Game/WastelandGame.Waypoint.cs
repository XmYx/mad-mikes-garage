using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Waypoint helpers for the HUD (round minimap readout, on-screen marker, map page): how far it is along the
    /// route, and how close counts as arrived.</summary>
    public partial class WastelandGame
    {
        /// <summary>Within this (m, flat) the waypoint counts as reached and clears with a toast.</summary>
        public const float WaypointReach = 15f;

        /// <summary>Metres to the waypoint along its road route (straight line when there is no route); 0 without one.</summary>
        public float WaypointDistance
        {
            get
            {
                if (!HasWaypoint) return 0f;
                var at = FocusPos;
                if (Route.Count < 2) return Flat(Waypoint - at);
                float d = Flat(Route[1] - at);
                for (int i = 2; i < Route.Count; i++) d += Flat(Route[i] - Route[i - 1]);
                return Mathf.Max(d, Flat(Waypoint - at));
            }
        }
    }
}
