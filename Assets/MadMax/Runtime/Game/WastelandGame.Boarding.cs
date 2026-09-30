using System.Collections;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Getting in and out on foot (setting GET IN / OUT ANIMATION): walk to the door, it swings open on its
    /// front hinge, slide into the seat, it shuts; out the same way backwards. Only for the player's own key presses;
    /// scripted enters and exits (fleet cycling, crashes, tests) stay instant. Bikes, aircraft, boats and walk-in
    /// cabins keep their own ways in.</summary>
    public partial class WastelandGame
    {
        /// <summary>The driven vehicle has a working tool (machine arm, blade, tipper, paver, crane) or flies: the arrows
        /// and Q/E work it (Controls ToolUp..ToolB) instead of doubling as driving keys.</summary>
        public bool ToolKeys
        {
            get
            {
                if (!Current) return false;
                if (Current.GetComponent<FlightModel>()) return true;
                if (Current.TryGetComponent<Machine>(out var m) && m.enabled && m.kind != Machine.Kind.Roller) return true;
                return Current.TryGetComponent<Crane>(out var cr) && cr.CranePart;
            }
        }

        /// <summary>A get-in / get-out animation is playing (the vehicle takes no input, F is ignored).</summary>
        public bool Boarding { get; private set; }
        const float DoorOpen = -68f;

        bool Animated(VehicleDriver car) => GameSettings.Current.boardingAnimation && !ExternalInput && car && Player && !Player.Interior
            && !car.GetComponent<BikeBalance>() && !car.aircraft && !car.GetComponent<BoatModel>() && !car.GetComponent<InteriorSpace>();

        public void EnterAnimated(VehicleDriver car)
        {
            var net = MadMax.Net.NetSession.Instance;
            if (!Animated(car) || (net && net.Online)) { Enter(car); return; }
            StartCoroutine(BoardRoutine(car));
        }

        public void ExitAnimated()
        {
            var car = Current;
            var net = MadMax.Net.NetSession.Instance;
            if (!Animated(car) || (net && net.Online) || Mathf.Abs(car.ForwardSpeed) > 1.5f) { Exit(); return; }
            StartCoroutine(AlightRoutine(car));
        }

        /// <summary>The front door on the side you get in (the part in the door socket), or null.</summary>
        static Transform DoorOn(VehicleDriver car, float side)
        {
            if (!car.TryGetComponent<VehicleChassis>(out var ch)) return null;
            Transform best = null; float bz = float.MinValue;
            foreach (var s in ch.Sockets)
            {
                if (s.accepts != PartCategory.Door || !s.Current) continue;
                var lp = car.transform.InverseTransformPoint(s.transform.position);
                if (Mathf.Sign(lp.x) != side || lp.z < bz) continue;
                bz = lp.z; best = s.Current.transform;
            }
            return best;
        }

        static IEnumerator Swing(Transform door, float from, float to, float time)
        {
            if (!door) yield break;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                if (!door) yield break;
                door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)), 0f);
                yield return null;
            }
            if (door) door.localRotation = Quaternion.Euler(0f, to, 0f);
        }

        IEnumerator BoardRoutine(VehicleDriver car)
        {
            Boarding = true;
            var exit = ExitPoint(car);
            float side = Mathf.Sign(car.transform.InverseTransformPoint(exit).x);
            var door = DoorOn(car, side);
            // walk up to the door, facing the car
            var cc = Player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            var from = Player.transform.position;
            var face = Quaternion.LookRotation(Vector3.ProjectOnPlane(car.transform.position - exit, Vector3.up).normalized + Vector3.forward * 1e-4f);
            if ((from - exit).sqrMagnitude < 16f)
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
                {
                    Player.transform.SetPositionAndRotation(Vector3.Lerp(from, exit, t), Quaternion.Slerp(Player.transform.rotation, face, t));
                    yield return null;
                }
            MadMax.Audio.Sfx.Play("door_open", car.transform.position, 0.5f, 1f, 20f, 0.2f);
            yield return Swing(door, 0f, DoorOpen, 0.25f);
            if (!car) { Boarding = false; if (cc) cc.enabled = true; yield break; }
            Enter(car);                                                                  // seats the player (parents to the car)
            if (Current == car && Player.SeatedIn == car)
            {
                var seat = Player.transform.localPosition;
                var start = car.transform.InverseTransformPoint(exit);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
                {
                    Player.transform.localPosition = Vector3.Lerp(start, seat, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                Player.transform.localPosition = seat;
            }
            yield return Swing(door, DoorOpen, 0f, 0.25f);
            Boarding = false;
        }

        IEnumerator AlightRoutine(VehicleDriver car)
        {
            Boarding = true;
            car.throttleInput = car.brakeInput = car.steerInput = 0f; car.handbrake = true;
            var exit = ExitPoint(car);
            float side = Mathf.Sign(car.transform.InverseTransformPoint(exit).x);
            var door = DoorOn(car, side);
            MadMax.Audio.Sfx.Play("door_open", car.transform.position, 0.5f, 1f, 20f, 0.2f);
            yield return Swing(door, 0f, DoorOpen, 0.25f);
            if (car && Player.SeatedIn == car)
            {
                var seat = Player.transform.localPosition;
                var end = car.transform.InverseTransformPoint(exit);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
                {
                    if (!car) break;
                    Player.transform.localPosition = Vector3.Lerp(seat, end, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
            }
            if (Current == car) Exit();
            yield return Swing(door, DoorOpen, 0f, 0.25f);
            Boarding = false;
        }
    }
}
