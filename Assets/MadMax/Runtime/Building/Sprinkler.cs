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
        float sprayUntil, head = -Arc;
        public bool Spraying => Time.time < sprayUntil;
        public void Spray(float seconds) => sprayUntil = Time.time + seconds;

        MadMax.World.Droplets jet;
        float tickT, sweepDir = 1f;
        /// <summary>Impact-head arc: ticks forward <see cref="TickDeg"/> every <see cref="TickSeconds"/> across ±<see cref="Arc"/>°,
        /// then the spring throws it back fast (2026-10-16: it used to turn smoothly at 140°/s).</summary>
        public const float Arc = 135f, TickDeg = 5f, TickSeconds = 0.11f, ReturnDegPerSec = 260f;
        /// <summary>The jet's heading now (degrees, piece space; tests).</summary>
        public float Heading => head;

        void Spin()
        {
            if (jet) jet.emitting = Spraying && !drip;
            if (!Spraying || drip) return;
            if (!jet)
                jet = MadMax.World.Droplets.Attach(gameObject, 48, (int i, out Vector3 at, out Vector3 v) =>
                {
                    // each droplet leaves along the jet's heading at that moment; one in four breaks up at the nozzle
                    // (the arm's spoon chops the stream) and falls close in, the rest carry the jet out to the reach
                    var dir = Quaternion.Euler(0f, head + Random.Range(-3f, 3f), 0f) * Vector3.forward;
                    at = new Vector3(0f, 0.62f, 0f) + dir * 0.12f;
                    bool broken = (i & 3) == 0;
                    float range = broken ? Random.Range(1.2f, 2.6f) : Random.Range(4.4f, 5.2f) * Mathf.Clamp(reach / 3.5f, 0.6f, 1.4f);
                    v = dir * range + Vector3.up * (broken ? Random.Range(1.4f, 2.4f) : Random.Range(2.8f, 3.4f));
                }, new Color(0.72f, 0.86f, 1f), 0.05f, 1.4f);
            if (sweepDir > 0f)
            {
                if ((tickT -= Time.deltaTime) <= 0f)
                {
                    tickT += TickSeconds;
                    head += TickDeg;
                    if (head >= Arc) { head = Arc; sweepDir = -1f; }
                    MadMax.Audio.Sfx.Play("click", transform.position + Vector3.up * 0.6f, 0.08f, Random.Range(1.5f, 1.7f), 8f, 0.05f);
                }
            }
            else
            {
                head -= ReturnDegPerSec * Time.deltaTime;
                if (head <= -Arc) { head = -Arc; sweepDir = 1f; tickT = TickSeconds; }
            }
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
