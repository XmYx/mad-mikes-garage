using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A water wheel: set in a running river it turns with the current (<see cref="WorldGen.RiverFlow"/>) and
    /// makes steady power day and night; in still water or on dry land it stands.</summary>
    public class WaterTurbine : MonoBehaviour, IInteractable
    {
        public float rated = 1200f;
        public Transform wheel;
        UtilityNode node;
        float angle, check, flow;
        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if ((check -= Time.deltaTime) <= 0f)
            {
                check = 1f;
                var t = DeformableTerrain.Instance;
                var p = transform.position;
                flow = t && t.World != null && t.WaterDepthNoLoad(p.x, p.z) > 0.25f ? t.World.RiverFlow(p.x, p.z).magnitude : 0f;
            }
            if (node) node.produce = rated * Mathf.Clamp01(flow / 1.6f);
            angle += flow * 40f * Time.deltaTime;
            if (wheel) wheel.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        public string Prompt(MadMax.Game.WastelandGame g) => flow < 0.1f ? "WATER WHEEL: NEEDS A RUNNING RIVER" : "WATER WHEEL: " + Mathf.RoundToInt(node ? node.produce : 0f) + " W (CURRENT " + flow.ToString("0.0") + " M/S)";
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { }
    }
}
