using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The biogas generator's hose (depth stage F): once a second it tops the generator's gas bag up from the
    /// nearest <see cref="BiogasDigester"/> within <see cref="BiogasDigester.HoseReach"/> m.</summary>
    public class GasHose : MonoBehaviour
    {
        Generator gen;
        float t;

        void Awake() => gen = GetComponent<Generator>();

        /// <summary>The digester feeding it now (null: none in reach).</summary>
        public BiogasDigester Source { get; private set; }

        void Update()
        {
            if (!gen || (t -= Time.deltaTime) > 0f) return;
            t = 1f;
            Source = null;
            float best = BiogasDigester.HoseReach;
            foreach (var d in BiogasDigester.All)
            {
                if (!d) continue;
                float dist = Vector3.Distance(d.transform.position, transform.position);
                if (dist < best) { best = dist; Source = d; }
            }
            if (!Source || gen.fuel > gen.tankLitres - 1f) return;
            gen.fuel += Source.Draw(gen.tankLitres - gen.fuel);
        }
    }
}
