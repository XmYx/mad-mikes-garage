using UnityEngine;

namespace MadMax.World
{
    /// <summary>A kinematic body people ride (the rolling city's deck, a dock lift): the player and NPCs standing on its
    /// colliders are moved with <see cref="PointVelocity"/> and turned with <see cref="YawRate"/>.</summary>
    public interface IDeckCarrier
    {
        Vector3 PointVelocity(Vector3 p);
        /// <summary>Turn rate about the vertical (degrees per second).</summary>
        float YawRate { get; }
    }
}
