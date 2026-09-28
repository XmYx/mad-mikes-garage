using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Vehicle root: owns the body mesh and a set of MountSockets. Any VehiclePart can be moved between chassis.</summary>
    [DisallowMultipleComponent]
    public class VehicleChassis : MonoBehaviour
    {
        public string vehicleName;
        public float bodyMass = 900f;

        public event Action Changed;

        public MountSocket[] Sockets => GetComponentsInChildren<MountSocket>(true);

        public IEnumerable<VehiclePart> Parts
        {
            get { foreach (var s in Sockets) if (s.Current) yield return s.Current; }
        }

        public float TotalMass
        {
            get { float m = bodyMass; foreach (var p in Parts) m += p.mass; return m; }
        }

        public MountSocket FindSocket(string socketName)
        {
            foreach (var s in Sockets) if (s.name == socketName) return s;
            return null;
        }

        public MountSocket FindFreeSocket(VehiclePart part)
        {
            foreach (var s in Sockets) if (s.IsFree && s.CanAccept(part)) return s;
            return null;
        }

        public bool TryMount(VehiclePart part)
        {
            var s = FindFreeSocket(part);
            return s && s.Attach(part);
        }

        public bool TryMount(VehiclePart part, string socketName)
        {
            var s = FindSocket(socketName);
            return s && s.Attach(part);
        }

        public VehiclePart Unmount(string socketName, bool drop = true)
        {
            var s = FindSocket(socketName);
            return s ? s.Detach(drop) : null;
        }

        /// <summary>Move a part from this vehicle to a compatible socket on another.</summary>
        public bool Transfer(string socketName, VehicleChassis target, string targetSocket = null)
        {
            var s = FindSocket(socketName);
            if (!s || !s.Current || !target) return false;
            var part = s.Current;
            var dest = targetSocket != null ? target.FindSocket(targetSocket) : target.FindFreeSocket(part);
            if (!dest || !dest.CanAccept(part)) return false;
            s.Detach(false);
            return dest.Attach(part);
        }

        internal void NotifyChanged()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.mass = TotalMass;
            Changed?.Invoke();
        }

        [ContextMenu("Detach All Parts")]
        void DetachAll() { foreach (var s in Sockets) s.Detach(); }
    }
}
