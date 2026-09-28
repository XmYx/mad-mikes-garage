using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Water sources: rain collector (open sky) and electric pump (next to a lake). Filters clean network water.</summary>
    public class WaterSource : MonoBehaviour
    {
        public enum Mode { Rain, Pump, Filter }
        public Mode mode;
        UtilityNode node;
        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if (!node) return;
            var p = transform.position;
            switch (mode)
            {
                case Mode.Rain:
                    node.sourceDirty = MadMax.World.Weather.Raining && !MadMax.World.Weather.Snowing ? 0.35f : 0f;
                    break;
                case Mode.Pump:
                    node.demand = 400f;
                    var terrain = MadMax.World.DeformableTerrain.Instance;
                    bool wet = false;
                    if (terrain)
                        for (int i = 0; i < 8 && !wet; i++)
                        {
                            var q = p + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 3f;
                            wet = terrain.WaterDepth(q.x, q.z) > 0.2f || terrain.WaterDepth(p.x, p.z) > 0.2f;
                        }
                    node.sourceDirty = wet && node.Powered ? 1.2f : 0f;
                    break;
                case Mode.Filter:
                    node.filterRate = 0.4f;
                    break;
            }
        }
    }
}
