using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Water sources: rain collector (open sky) and electric pump (next to a lake). Filters clean network water.</summary>
    public class WaterSource : MonoBehaviour
    {
        public enum Mode { Rain, Pump, Filter, Well }
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
                case Mode.Well:
                {
                    // an electric pump down the well when powered; the rate depends on how deep the water table lies
                    var t = MadMax.World.DeformableTerrain.Instance;
                    float depth = t && t.World != null ? t.World.WaterTable(p.x, p.z) : 10f;
                    bool toxic = t && t.BiomeAt(p.x, p.z) == MadMax.World.Biome.Nuclear;
                    node.demand = 300f;
                    float rate = node.Powered ? Mathf.Clamp(1.2f / depth, 0.03f, 0.5f) : 0f;
                    node.sourceClean = toxic ? 0f : rate; node.sourceDirty = toxic ? rate : 0f;
                    break;
                }
            }
        }
    }
}
