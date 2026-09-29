using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A wind turbine (medium and large build pieces, the small one on a vehicle's cargo bed): the rotor turns
    /// with the wind (<see cref="MadMax.World.WindDust"/>: stronger aloft, gusts, storms) and feeds its power node.
    /// On a moving vehicle the airstream adds to the wind.</summary>
    public class Windmill : MonoBehaviour, IInteractable
    {
        public Transform rotor;
        public float rated = 800f;
        public float height = 6f;          // hub height: the wind is stronger up there
        UtilityNode node;
        Rigidbody carrier;
        float angle, spin, recheck;
        void Awake() => node = GetComponent<UtilityNode>();

        /// <summary>Wind speed (m/s) felt by a rotor at <paramref name="hub"/> m.</summary>
        public float WindAt(float hub)
        {
            float v = MadMax.World.WindDust.Strength * (1f + 0.5f * MadMax.World.WindDust.Gust) * Mathf.Pow(Mathf.Max(1f, hub) / 6f, 0.2f);
            if (carrier && !carrier.isKinematic) v += Mathf.Min(12f, carrier.linearVelocity.magnitude * 0.5f);   // driving into the air
            return v;
        }

        /// <summary>Share of rated output at a wind speed: cut-in ~1 m/s, full from ~9 m/s.</summary>
        public static float Curve(float v) { float f = Mathf.Clamp01((v - 1f) / 8f); return f * Mathf.Sqrt(f); }

        void Update()
        {
            if ((recheck -= Time.deltaTime) <= 0f) { recheck = 1f; carrier = GetComponentInParent<Rigidbody>(); }   // mounted on a vehicle, or loose
            float v = WindAt(height);
            if (node) node.produce = rated * Curve(v);
            spin = Mathf.MoveTowards(spin, Mathf.Clamp(v * 45f, 0f, 520f), Time.deltaTime * 60f);
            angle += spin * Time.deltaTime;
            if (rotor) rotor.localRotation = Quaternion.Euler(0, 0, angle);
        }

        public string Prompt(MadMax.Game.WastelandGame g) => "WIND TURBINE: " + Mathf.RoundToInt(node ? node.produce : 0f) + " W OF " + Mathf.RoundToInt(rated) + " (WIND " + WindAt(height).ToString("0") + " M/S)";
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { }
    }
}
