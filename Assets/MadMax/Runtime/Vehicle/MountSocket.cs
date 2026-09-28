using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Attachment point on a chassis. Mirrored sockets (left side) flip a right-hand part so one prefab serves both sides.</summary>
    [DisallowMultipleComponent]
    public class MountSocket : MonoBehaviour
    {
        public PartCategory accepts;
        [Range(1, 4)] public int maxSizeClass = 4;
        [SerializeField] bool mirrored;
        [SerializeField] VehiclePart current;

        public VehiclePart Current => current;
        public bool IsFree => current == null;
        public bool Mirrored => mirrored;
        public VehicleChassis Chassis => GetComponentInParent<VehicleChassis>();

        public void SetMirrored(bool value)
        {
            mirrored = value;
            var s = transform.localScale;
            transform.localScale = new Vector3(value ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        }

        public bool CanAccept(VehiclePart part) =>
            part && part.category == accepts && part.sizeClass <= maxSizeClass && (current == null || current == part);

        public bool Attach(VehiclePart part)
        {
            if (!CanAccept(part)) return false;
            if (part.Socket && part.Socket != this) part.Socket.Detach(false);

            var t = part.transform;
            t.SetParent(transform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var rb = part.GetComponent<Rigidbody>();
            if (rb) { if (Application.isPlaying) Destroy(rb); else DestroyImmediate(rb); }
            // Wheels are simulated by raycast suspension; their colliders would fight it.
            // Mirrored (negative-scale) sockets cannot host BoxColliders; the chassis body collider covers them.
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = part.category != PartCategory.Wheel && !mirrored;

            current = part;
            part.Socket = this;
            part.RaiseMounted(this);
            var chassis = Chassis;
            if (chassis) chassis.NotifyChanged();
            return true;
        }

        /// <summary>Unmount the current part. With <paramref name="drop"/> the part becomes a loose physics object.</summary>
        public VehiclePart Detach(bool drop = true)
        {
            var part = current;
            if (!part) return null;
            current = null;
            part.Socket = null;
            part.transform.SetParent(null, true);
            part.transform.localScale = Vector3.one;   // loose parts are never mirrored (BoxColliders reject negative scale)
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = true;
            if (drop && Application.isPlaying && !part.GetComponent<Rigidbody>())
            {
                var rb = part.gameObject.AddComponent<Rigidbody>();
                rb.mass = part.mass;
            }
            part.RaiseUnmounted(this);
            var chassis = Chassis;
            if (chassis) chassis.NotifyChanged();
            return part;
        }

        [ContextMenu("Detach Part")]
        void DetachFromMenu() => Detach();

        void OnDrawGizmos()
        {
            Gizmos.color = current ? new Color(0.3f, 1f, 0.4f, 0.8f) : new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.12f);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.2f);
        }
    }
}
