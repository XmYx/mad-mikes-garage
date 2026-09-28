using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Tyre data carried by a wheel part. Grip is a friction coefficient on dry firm ground; wetGrip the fraction kept
    /// on wet firm ground (rain tyres ≈ 1, sport tyres lose a lot); mudGrip applies on soaked ground. Footprint > 1 spreads the
    /// load (crawler tracks) so the wheel barely sinks; rolling scales rolling resistance; wearRate scales tread wear.</summary>
    [RequireComponent(typeof(VehiclePart))]
    public class WheelStats : MonoBehaviour
    {
        public float grip = 1.1f;
        public float mudGrip = 0.45f;
        public float width = 0.32f;         // m, also the rut width
        public float wetGrip = 0.8f, rolling = 1f, wearRate = 1f, footprint = 1f;
        public float wear;                  // tread 0 new .. 1 = popped (saved)
        [System.NonSerialized] public float heat;

        public bool Popped => wear >= 1f;
        public float GripFactor => Popped ? 0.35f : Mathf.Lerp(1f, 0.72f, wear);

        /// <summary>Blow the tyre out (crash, puncture, overheated bald tyre).</summary>
        public void Pop()
        {
            if (Popped) return;
            wear = 1f;
            Popped_?.Invoke(this);
        }
        public static event System.Action<WheelStats> Popped_;
    }
}
