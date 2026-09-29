using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Driver input for mounted weapons, one frame: LMB (held / pressed), Shift, B (rear dropper), U (smoke),
    /// and the world point the driver aims at (mouse in top-down views, screen centre in third / first person).</summary>
    public struct WeaponInput
    {
        public bool fire, firePressed, alt, drop, smoke;
        public Vector3 aim;
        public float dt;
    }

    /// <summary>Base of a weapon on a vehicle part. Operated by <see cref="VehicleWeapons"/> while the player drives.</summary>
    public abstract class VehicleWeapon : MonoBehaviour
    {
        protected VehiclePart part;
        protected virtual void Awake() => part = GetComponent<VehiclePart>();

        public bool Mounted => part && part.Socket;
        public VehicleDriver Vehicle => Mounted ? part.GetComponentInParent<VehicleDriver>() : null;
        /// <summary>HUD line (ammo, key).</summary>
        public virtual string Status => null;

        public abstract void Operate(in WeaponInput input);

        /// <summary>Train a yaw segment and a pitch segment toward a world point at <paramref name="rate"/> °/s.</summary>
        protected static void Train(Transform yaw, Transform pitch, Vector3 aim, float rate, float down, float up, float dt)
        {
            if (yaw)
            {
                var p = yaw.parent.InverseTransformPoint(aim) - yaw.localPosition;
                float want = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                yaw.localRotation = Quaternion.RotateTowards(yaw.localRotation, Quaternion.Euler(0f, want, 0f), rate * dt);
            }
            if (pitch)
            {
                var q = pitch.parent.InverseTransformPoint(aim) - pitch.localPosition;
                float el = Mathf.Atan2(q.y, new Vector2(q.x, q.z).magnitude) * Mathf.Rad2Deg;
                el = Mathf.Clamp(el, -down, up);
                pitch.localRotation = Quaternion.RotateTowards(pitch.localRotation, Quaternion.Euler(-el, 0f, 0f), rate * dt);
            }
        }
    }
}
