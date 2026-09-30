using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Water sources: rain collector (open sky) and electric pump (next to a lake; salty by the sea). Filters
    /// clean network water while their cartridge lasts. Wells draw ground water, fouled where the ground is (oil
    /// fields, fallout, a latrine or trough upstream, the shore: <see cref="WaterQuality.GroundTaint"/>).</summary>
    public class WaterSource : MonoBehaviour
    {
        public enum Mode { Rain, Pump, Filter, Well }
        public Mode mode;
        UtilityNode node;
        FilterCartridge cartridge;
        WaterTaint ground;
        float groundAt = -1f;
        void Awake() => node = GetComponent<UtilityNode>();
        void Start() => cartridge = GetComponent<FilterCartridge>();

        void Update()
        {
            if (!node) return;
            var p = transform.position;
            switch (mode)
            {
                case Mode.Rain:
                    node.sourceDirty = MadMax.World.Weather.Raining && !MadMax.World.Weather.Snowing ? 0.35f : 0f;
                    node.sourceTaint = WaterTaint.Silt;
                    break;
                case Mode.Pump:
                    node.demand = 400f;
                    var terrain = MadMax.World.DeformableTerrain.Instance;
                    bool wet = false;
                    var at = p;
                    if (terrain)
                        for (int i = 0; i < 8 && !wet; i++)
                        {
                            var q = p + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 3f;
                            wet = terrain.WaterDepth(q.x, q.z) > 0.2f || terrain.WaterDepth(p.x, p.z) > 0.2f;
                            if (wet) at = terrain.WaterDepth(q.x, q.z) > 0.2f ? q : p;
                        }
                    node.sourceDirty = wet && node.Powered ? 1.2f : 0f;
                    if (wet && (Time.time >= groundAt || groundAt < 0f))
                    {
                        groundAt = Time.time + 5f;
                        ground = WaterQuality.IsSea(at.x, at.z) ? WaterTaint.Salt : terrain.BiomeAt(at.x, at.z) == MadMax.World.Biome.Nuclear ? WaterTaint.Toxic : WaterTaint.Silt;
                    }
                    node.sourceTaint = ground;
                    break;
                case Mode.Filter:
                    node.filterRate = cartridge && cartridge.Spent ? 0f : 0.4f;
                    break;
                case Mode.Well:
                {
                    // an electric pump down the well when powered; the rate depends on how deep the water table lies
                    var t = MadMax.World.DeformableTerrain.Instance;
                    float depth = t && t.World != null ? t.World.WaterTable(p.x, p.z) : 10f;
                    if (Time.time >= groundAt || groundAt < 0f) { groundAt = Time.time + 5f; ground = WaterQuality.GroundTaint(p); }
                    node.demand = 300f;
                    float rate = node.Powered ? Mathf.Clamp(1.2f / depth, 0.03f, 0.5f) : 0f;
                    bool fouled = ground != WaterTaint.None;
                    node.sourceClean = fouled ? 0f : rate; node.sourceDirty = fouled ? rate : 0f;
                    node.sourceTaint = ground;
                    break;
                }
            }
        }
    }
}
