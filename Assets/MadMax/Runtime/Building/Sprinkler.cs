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

        /// <summary>After watering the head spins and throws arcs of water for a while (2026-10-15: used to be four puffs
        /// at the bed); a drip line weeps now and then while it feeds.</summary>
        public const float SpraySeconds = 7f;
        float sprayUntil, head;
        public bool Spraying => Time.time < sprayUntil;
        public void Spray(float seconds) => sprayUntil = Time.time + seconds;

        MadMax.World.Droplets jet;

        void Spin()
        {
            if (jet) jet.emitting = Spraying && !drip;
            if (!Spraying || drip) return;
            if (!jet)
                jet = MadMax.World.Droplets.Attach(gameObject, 40, (int i, out Vector3 at, out Vector3 v) =>
                {
                    // the impact head ticks round: each droplet leaves along the jet's heading at that moment
                    var dir = Quaternion.Euler(0f, head + Random.Range(-6f, 6f), 0f) * Vector3.forward;
                    at = new Vector3(0f, 0.62f, 0f) + dir * 0.12f;
                    v = dir * Random.Range(3.2f, 4.4f) + Vector3.up * Random.Range(2.6f, 3.4f);
                }, 0.05f, 1.2f);
            head += Time.deltaTime * 140f;
            MadMax.Audio.Sfx.Loop(this, "pour", Mathf.Clamp01((sprayUntil - Time.time) / 0.8f) * 0.25f, 1.6f, 9f);
        }

        void Update()
        {
            Spin();
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
                sprayUntil = Time.time + SpraySeconds;
            }
        }
    }
}
