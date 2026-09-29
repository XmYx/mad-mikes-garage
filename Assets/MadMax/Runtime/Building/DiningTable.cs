using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Eating a proper meal (hunger ≥ 15) within reach of a table makes you WELL FED for <see cref="hours"/>
    /// game hours (longer when seated): hunger and thirst drop slower and stamina comes back faster.</summary>
    public class DiningTable : MonoBehaviour
    {
        public float hours = 3f;

        /// <summary>Longest well-fed time offered by a table within 1.2 m of <paramref name="p"/> (0 = none).</summary>
        public static float Near(Vector3 p)
        {
            float best = 0f;
            foreach (var pl in Placeable.All)
            {
                if (!pl || (pl.transform.position - p).sqrMagnitude > 16f || !pl.TryGetComponent<DiningTable>(out var t)) continue;
                if (pl.TryGetComponent<Collider>(out var c) && c.bounds.SqrDistance(p) > 1.44f) continue;
                best = Mathf.Max(best, t.hours);
            }
            return best;
        }
    }
}
