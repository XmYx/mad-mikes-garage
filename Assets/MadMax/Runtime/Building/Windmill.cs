using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wind turbine: output follows the (gusty) wind; the rotor spins with it.</summary>
    public class Windmill : MonoBehaviour
    {
        public Transform rotor;
        public float rated = 800f;
        UtilityNode node;
        float angle;
        void Awake() => node = GetComponent<UtilityNode>();

        public static float WindStrength(Vector3 p) =>
            Mathf.Clamp01(0.25f + Mathf.PerlinNoise(Time.time * 0.02f, p.x * 0.001f) * 0.8f + (MadMax.World.Weather.Raining ? 0.25f : 0f));

        void Update()
        {
            float w = WindStrength(transform.position);
            if (node) node.produce = rated * w * w;
            angle += w * 420f * Time.deltaTime;
            if (rotor) rotor.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
