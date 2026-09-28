using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Waters every garden plot within reach from the water network.</summary>
    public class Sprinkler : MonoBehaviour
    {
        public float reach = 3.5f;
        UtilityNode node;
        float tick;
        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if ((tick -= Time.deltaTime) > 0f || !node) return;
            tick = 5f;
            foreach (var p in FindObjectsByType<GardenPlot>())
            {
                if (p.water > 0.6f || Vector3.Distance(p.transform.position, transform.position) > reach) continue;
                if (UtilityGrid.Draw(node, 0.3f, out _) < 0.29f) return;
                p.Water(1f);
                for (int i = 0; i < 4; i++) MadMax.World.Fx.Smoke(p.transform.position + Vector3.up * 0.4f, Random.insideUnitSphere + Vector3.up, 0.15f, new Color(0.7f, 0.8f, 0.9f, 0.5f), 0.5f);
            }
        }
    }
}
