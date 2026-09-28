using System;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>A removable component. Its pivot is its mount point, so it snaps onto any compatible socket on any chassis.</summary>
    [DisallowMultipleComponent]
    public class VehiclePart : MonoBehaviour
    {
        public string partId;
        public PartCategory category;
        [Tooltip("Physical size bracket; a socket only accepts parts up to its maxSizeClass.")]
        [Range(1, 4)] public int sizeClass = 1;
        public float mass = 25f;
        [Tooltip("Wheels: rolling radius in metres (for future drive/physics code).")]
        public float radius;
        [Tooltip("0 = pristine, 1 = destroyed (loose parts fall off). Travels with the part.")]
        public float damage;
        [System.NonSerialized] public uint netId;   // loose parts only (network identity)

        MountSocket socket;
        public MountSocket Socket
        {
            get
            {
                if (!socket && transform.parent && transform.parent.TryGetComponent<MountSocket>(out var s) && s.Current == this) socket = s;
                return socket;
            }
            internal set => socket = value;
        }
        public VehicleChassis Vehicle => Socket ? Socket.Chassis : null;
        public bool IsMounted => Socket != null;

        /// <summary>Every enabled part (mounted, loose or carried) for proximity queries.</summary>
        public static readonly System.Collections.Generic.List<VehiclePart> Registry = new System.Collections.Generic.List<VehiclePart>();
        void OnEnable() => Registry.Add(this);
        void OnDisable() => Registry.Remove(this);

        public event Action<VehiclePart, MountSocket> Mounted;
        public event Action<VehiclePart, MountSocket> Unmounted;

        internal void RaiseMounted(MountSocket s) => Mounted?.Invoke(this, s);
        internal void RaiseUnmounted(MountSocket s) => Unmounted?.Invoke(this, s);

        [ContextMenu("Detach From Vehicle")]
        public void Detach()
        {
            if (Socket) Socket.Detach();
        }

        /// <summary>Mount on the first free compatible socket of a vehicle.</summary>
        public bool MountOn(VehicleChassis chassis) => chassis && chassis.TryMount(this);
    }
}
