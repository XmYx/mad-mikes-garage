using MadMax.Building;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>The seat beside the driver (roadmap 19), reached at the passenger door. [E] rides along when someone
    /// else drives: a trader convoy's car (for a fare — the convoy carries on along its road), another player's
    /// vehicle, a companion's. The passenger sits and rides with the vehicle; standing up (F) gets out at the door.</summary>
    public class PassengerSeat : MonoBehaviour, IInteractable
    {
        public const int Fare = 10;
        public VehicleDriver Vehicle { get; private set; }
        public Seat Seat { get; private set; }
        /// <summary>A person sitting here (a companion riding along).</summary>
        public MadMax.Npc.Npc Occupant;

        /// <summary>Give a drivable vehicle its passenger seat (once).</summary>
        public static PassengerSeat For(VehicleDriver v)
        {
            if (!v || !v.driveable) return null;
            var existing = v.GetComponentInChildren<PassengerSeat>(true);
            if (existing) return existing;
            var eye = v.transform.Find("DriverEye");
            if (!eye) return null;
            var e = eye.localPosition;
            float side = e.x < 0f ? 1f : -1f;                                                    // the other side of the cab
            float half = 1f;
            var body = v.transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            if (mf && mf.sharedMesh) half = Mathf.Max(-mf.sharedMesh.bounds.min.x, mf.sharedMesh.bounds.max.x);
            var door = new GameObject("PassengerDoor").transform;
            door.SetParent(v.transform, false);
            door.localPosition = new Vector3(side * (half + 0.45f), 0f, e.z);
            var ps = door.gameObject.AddComponent<PassengerSeat>();
            ps.Vehicle = v;
            var spot = new GameObject("PassengerSeat").transform;
            spot.SetParent(v.transform, false);
            spot.localPosition = new Vector3(-e.x, e.y - 0.62f, e.z - 0.06f);                   // hips on the cushion beside the driver
            var seat = spot.gameObject.AddComponent<Seat>();
            seat.hidden = true; seat.rest = 1.2f; seat.reading = 1f;
            seat.hasExit = true;
            seat.exitLocal = spot.InverseTransformPoint(door.position);
            ps.Seat = seat;
            return ps;
        }

        void OnEnable() => PartFunctions.Interactables.Add(this);
        void OnDisable() => PartFunctions.Interactables.Remove(this);

        /// <summary>The local player sits here.</summary>
        public bool Taken { get { var g = WastelandGame.Instance; return Seat && g && g.Player && g.Player.SeatedOn == Seat; } }

        /// <summary>Who drives: 0 nobody (or the local player), 1 a trader convoy, 2 raiders, 3 another player.</summary>
        int Driver(WastelandGame g)
        {
            if (!Vehicle || Vehicle == g.Current) return 0;
            if (Vehicle.aiDriven)
            {
                var c = MadMax.Npc.NpcDirector.Instance ? MadMax.Npc.NpcDirector.Instance.ConvoyOf(Vehicle) : null;
                return c == null ? 0 : c.raiders ? 2 : 1;
            }
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.Online && Vehicle.Occupied && Vehicle.owner != net.LocalId) return 3;
            return 0;
        }

        public string Prompt(WastelandGame g)
        {
            if (!g.Player || g.Current || Taken || Occupant) return null;
            int who = Driver(g);
            return who == 1 ? "[E] RIDE ALONG (" + Fare + " SCRAP)" : who == 3 ? "[E] RIDE AS PASSENGER" : null;
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary || !Seat) return;
            int who = Driver(g);
            if (who != 1 && who != 3) return;
            if (who == 1 && !g.Inventory.TrySpend(MadMax.Items.ResourceType.Scrap, Fare)) { g.Toast("THE DRIVER WANTS " + Fare + " SCRAP FOR THE RIDE"); return; }
            MadMax.Audio.Sfx.Play("car_door", transform.position, 0.6f);
            g.Player.SitOn(Seat, Vector3.zero);
            g.Toast(who == 1 ? "YOU RIDE ALONG WITH THE TRADER - F TO GET OUT" : "RIDING ALONG - F TO GET OUT");
        }
    }
}
