using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Desalinator (depth stage F, the powered tier of sea water): its crafting station turns carried sea water
    /// into drinking water and salt; piped into a water network it also pumps the network's dirty water — brine from a
    /// sea pump included — through its membranes (<see cref="rate"/> L/s, <see cref="watts"/> W while it works). The salt
    /// it scrapes out (1 per 10 L of brine) waits in the station's tray ([T]).</summary>
    public class Desalinator : MonoBehaviour
    {
        public float rate = 0.5f, watts = 1000f;
        public const float BrinePerSalt = 10f;
        float salt;
        UtilityNode node;
        CraftingStation station;

        void Awake() => node = GetComponent<UtilityNode>();
        void Start() => station = GetComponent<CraftingStation>();

        /// <summary>Pumping network water through right now.</summary>
        public bool Working => node && node.desalRate > 0f;

        void Update()
        {
            if (!node) return;
            float w = UtilityGrid.NetWater(node, out float clean);
            bool dirty = w * (1f - clean) > 0.05f;
            node.auxDemand = dirty ? watts : 0f;
            node.desalRate = dirty && node.Powered ? rate : 0f;
            if (node.converted <= 0f) return;
            if ((node.convertedTaint & WaterTaint.Salt) != 0) salt += node.converted / BrinePerSalt;
            node.converted = 0f; node.convertedTaint = WaterTaint.None;
            if (salt < 1f || !station) return;
            int n = Mathf.FloorToInt(salt);
            salt -= n;
            station.tray.Add(ResourceType.Salt, n);
            GetComponent<Placeable>()?.Dirty();
        }
    }
}
