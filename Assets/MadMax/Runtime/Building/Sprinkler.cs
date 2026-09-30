using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Waters every garden plot within reach from the water network. A drip line (<see cref="drip"/>) sips a
    /// fraction of the water and keeps its beds evenly moist, which they reward with faster growth.</summary>
    public class Sprinkler : MonoBehaviour
    {
        public float reach = 3.5f;
        public bool drip;
        UtilityNode node;
        float tick;
        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if ((tick -= Time.deltaTime) > 0f || !node) return;
            tick = 5f;
            foreach (var p in FindObjectsByType<GardenPlot>())
            {
                if (Vector3.Distance(p.transform.position, transform.position) > reach) continue;
                float cost = IrrigationTimer.Cost(node, p.water < 0.15f);
                if (cost <= 0f) continue;                                                   // a timer holds the network till its window
                if (drip)
                {
                    if (p.water >= 0.9f) { p.Irrigate(0f); continue; }
                    if (UtilityGrid.Draw(node, 0.05f * cost, out _) < 0.049f * cost) return;
                    p.Irrigate(1f);
                    continue;
                }
                if (p.water > 0.6f) continue;
                if (UtilityGrid.Draw(node, 0.3f * cost, out _) < 0.29f * cost) return;
                p.Water(1f);
                for (int i = 0; i < 4; i++) MadMax.World.Fx.Smoke(p.transform.position + Vector3.up * 0.4f, Random.insideUnitSphere + Vector3.up, 0.15f, new Color(0.7f, 0.8f, 0.9f, 0.5f), 0.5f);
            }
        }
    }
}
