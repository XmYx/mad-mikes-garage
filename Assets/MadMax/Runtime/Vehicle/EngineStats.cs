using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Performance data carried by an engine part; read by VehicleDriver of whatever chassis it is mounted on.</summary>
    [RequireComponent(typeof(VehiclePart))]
    public class EngineStats : MonoBehaviour
    {
        public float maxTorque = 500f;      // Nm
        public float maxRpm = 6000f;
        public float idleRpm = 900f;
        [Range(0.3f, 0.9f)] public float peakAt = 0.6f;

        public float TorqueAt(float rpm)
        {
            float x = (rpm / maxRpm - peakAt) / 0.75f;
            return maxTorque * Mathf.Clamp(1f - x * x, 0.35f, 1f);
        }
    }
}
