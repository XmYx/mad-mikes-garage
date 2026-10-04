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

        VehiclePart part;
        EngineSpec spec;

        /// <summary>What the engine is mechanically (cylinders, cycle, displacement...; <see cref="EngineSpec"/>).</summary>
        public EngineSpec Spec
        {
            get
            {
                if (spec == null) { if (!part) part = GetComponent<VehiclePart>(); spec = EngineSpec.For(part ? part.partId : null, maxTorque, maxRpm); }
                return spec;
            }
        }

        void Awake() { idleRpm = Spec.idleRpm; }                                                      // big engines idle low, small ones high

        public float TorqueAt(float rpm)
        {
            float x = (rpm / maxRpm - peakAt) / 0.75f;
            if (!part) part = GetComponent<VehiclePart>();
            float make = !part ? 1f : part.quality <= 0 ? 0.95f : part.quality >= 2 ? 1.06f : 1f;     // crude / fine builds
            return maxTorque * make * Mathf.Clamp(1f - x * x, 0.35f, 1f);
        }
    }
}
