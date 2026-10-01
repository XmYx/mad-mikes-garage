using MadMax.Building;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>One storage compartment of a vehicle (<see cref="VehicleStorage"/>): a <see cref="Container"/> on a child
    /// object standing at its first access point (where the player stands: behind the boot, at the passenger door, at a
    /// rear door). It opens only from one of its access points on foot, or from a seat inside when
    /// <see cref="fromSeat"/>; never while someone else is at the wheel or the vehicle is moving. A cut boot lid or
    /// tailgate (<see cref="lid"/>) lifts while the compartment is open in the loot window.</summary>
    public class VehicleCompartment : MonoBehaviour
    {
        /// <summary>Feet-to-access-point distance (m, flat) that counts as standing there.</summary>
        public const float Reach = 1.35f;

        public VehicleStorage.Kind kind;
        public VehicleDriver vehicle;
        public Container container;
        /// <summary>Access points in the vehicle's local space (feet height + 0.35 m).</summary>
        public Vector3[] access;
        /// <summary>Reachable from a seat inside (glovebox, back seat).</summary>
        public bool fromSeat;
        /// <summary>A hinged panel that lifts while open (boot lid, tailgate), optional.</summary>
        public Transform lid;
        float lidAngle;
        bool wasOpen;

        /// <summary>The player sits in this vehicle (driving or on its passenger seat).</summary>
        public bool Seated(WastelandGame g) =>
            g && g.Player && vehicle && (g.Current == vehicle || (g.Player.SeatedOn && g.Player.SeatedOn.transform.IsChildOf(vehicle.transform)));

        /// <summary>Why it can't be opened right now (null = it can).</summary>
        public string Why(WastelandGame g)
        {
            if (!g || !g.Player || !vehicle || !container) return "OUT OF REACH";
            if (vehicle.aiDriven) return "SOMEONE IS AT THE WHEEL";
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.Online && vehicle.Occupied && vehicle.owner != net.LocalId && g.Current != vehicle) return "SOMEONE IS AT THE WHEEL";
            if (Seated(g)) return fromSeat ? null : kind == VehicleStorage.Kind.Trunk || kind == VehicleStorage.Kind.Bed ? "FROM THE BACK OF THE VEHICLE" : "GET OUT FIRST";
            if (g.Current || g.Player.Sitting) return "GET OUT FIRST";
            if (vehicle.Body && !vehicle.Body.isKinematic && vehicle.Body.linearVelocity.sqrMagnitude > 2.25f) return "IT IS MOVING";
            if (AtAccess(g.Player.transform.position)) return null;
            switch (kind)
            {
                case VehicleStorage.Kind.Trunk: case VehicleStorage.Kind.Bed: return "FROM THE BACK OF THE VEHICLE";
                case VehicleStorage.Kind.Glovebox: return "FROM THE PASSENGER DOOR";
                case VehicleStorage.Kind.Seats: return "FROM A DOOR";
                default: return "GET CLOSER";
            }
        }

        public bool CanOpen(WastelandGame g) => Why(g) == null;

        /// <summary>Feet at one of the access points.</summary>
        public bool AtAccess(Vector3 feet)
        {
            if (!vehicle || access == null) return false;
            foreach (var a in access)
            {
                var w = vehicle.transform.TransformPoint(a);
                var d = feet - w;
                if (Mathf.Abs(d.y + 0.35f) > 1.6f) continue;                                   // on the roof, in a pit
                d.y = 0f;
                if (d.sqrMagnitude <= Reach * Reach) return true;
            }
            return false;
        }

        /// <summary>Where to stand for it (world): the access point nearest to <paramref name="from"/>.</summary>
        public Vector3 StandAt(Vector3 from)
        {
            var best = transform.position; float bd = float.MaxValue;
            if (vehicle && access != null)
                foreach (var a in access)
                {
                    var w = vehicle.transform.TransformPoint(a);
                    float d = (w - from).sqrMagnitude;
                    if (d < bd) { bd = d; best = w; }
                }
            best.y -= 0.35f;
            return best;
        }

        /// <summary>Shown in the loot window right now.</summary>
        public bool IsOpen
        {
            get
            {
                var g = WastelandGame.Instance;
                if (!g || !g.Menus || !g.Menus.IsOpen || g.Menus.Current != MenuSystem.Page.Container) return false;
                var cur = g.Menus.LootCurrent;
                return cur != null && cur.box == container;
            }
        }

        void Update()
        {
            bool open = IsOpen;
            if (open != wasOpen)
            {
                wasOpen = open;
                var at = lid ? lid.position : transform.position;
                string key = kind == VehicleStorage.Kind.Trunk || kind == VehicleStorage.Kind.Bed || kind == VehicleStorage.Kind.Seats ? "car_door" : open ? "door_open" : "door_close";
                MadMax.Audio.Sfx.Play(key, at, open ? 0.45f : 0.55f, open ? 1.15f : 0.95f, 18f, 0.2f);
            }
            if (!lid) return;
            if (!lid.parent || !lid.parent.GetComponent<MountSocket>()) { lid = null; return; }   // knocked off
            float target = open ? 65f : 0f;
            if (Mathf.Approximately(lidAngle, target)) return;
            lidAngle = Mathf.MoveTowards(lidAngle, target, Time.unscaledDeltaTime * 160f);
            lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);                           // hinge at the panel's mount point, rear edge lifts
        }
    }
}
