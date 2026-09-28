using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Tyre data carried by a wheel part. Grip is a friction coefficient; mudGrip applies on soaked ground.</summary>
    [RequireComponent(typeof(VehiclePart))]
    public class WheelStats : MonoBehaviour
    {
        public float grip = 1.1f;
        public float mudGrip = 0.45f;
        public float width = 0.32f;         // m, also the rut width
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
