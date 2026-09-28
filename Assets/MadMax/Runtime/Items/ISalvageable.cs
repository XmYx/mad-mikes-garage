using UnityEngine;

namespace MadMax.Items
{
    /// <summary>Objects a salvage tool can strip for materials (wrecks, vehicles).</summary>
    public interface ISalvageable
    {
        /// <returns>False when salvage is not allowed (e.g. occupied vehicle).</returns>
        bool Salvage(Vector3 point, float amount, GameObject source);
    }
}
