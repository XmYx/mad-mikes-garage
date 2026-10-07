using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Engine block heater post: while powered it keeps the nearest vehicle parked within <see cref="Reach"/>
    /// warm in the cold (<see cref="VehicleSystems.KeepWarm"/>: no frozen coolant, a warm-engine start, an iced tank
    /// thawed). Draws <see cref="Watts"/> only while a cold vehicle is plugged in.</summary>
    public class BlockHeater : MonoBehaviour
    {
        public const float Reach = 5f, Watts = 400f, ColdBelow = 5f;
        UtilityNode node;
        /// <summary>The vehicle on the cord right now (null = none or no power).</summary>
        [System.NonSerialized] public VehicleSystems plugged;

        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            plugged = null;
            VehicleSystems best = null; float bd = Reach;
            if (MadMax.World.Weather.Temperature < ColdBelow)
                foreach (var v in VehicleSystems.All)
                {
                    if (!v || v.Started) continue;
                    float d = Vector3.Distance(v.transform.position, transform.position);
                    if (d < bd) { bd = d; best = v; }
                }
            if (node) node.demand = best ? Watts : 0f;
            if (!best || !node || !node.Powered) return;
            plugged = best;
            best.KeepWarm(Time.deltaTime);
        }
    }
}
