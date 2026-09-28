using UnityEngine;

namespace MadMax.Items
{
    /// <summary>Anything tools, vehicles or explosions can hit: destructible structures, crates, vehicles.</summary>
    public interface IDamageable
    {
        /// <param name="point">World hit point.</param>
        /// <param name="direction">World direction of the blow (into the target).</param>
        /// <param name="power">1 = one sledgehammer blow.</param>
        /// <param name="radius">Affected radius in metres.</param>
        void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source);
    }
}
