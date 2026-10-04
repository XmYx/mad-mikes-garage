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
        /// <summary>Mud caked on the tyre, 0 clean .. 1 (picked up in mud and on wet soft ground, shed by driving on dry
        /// firm ground, fastest on paving; washed off fording). Costs a little grip on firm ground; saved.</summary>
        public float mud;

        public bool Popped => wear >= 1f;
        /// <summary>Driven flat until the carcass tore off: running on the bare rim (wear 1.5, saved with the wear).</summary>
        public bool Shredded => wear >= 1.5f;
        /// <summary>Rolling radius share left: full, flat (0.78) or the bare rim (0.7).</summary>
        public float RadiusFactor => Shredded ? 0.7f : Popped ? 0.78f : 1f;
        public float GripFactor => (Popped ? 0.35f : Mathf.Lerp(1f, 0.72f, wear)) * (1f - mud * MudGripLoss);
        /// <summary>Grip lost to a fully caked tread.</summary>
        public const float MudGripLoss = 0.12f;

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
