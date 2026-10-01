using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Crash ejection: an unbelted occupant keeps the vehicle's travel when the vehicle stops. Bikes and
    /// aircraft throw their rider through <see cref="ThrowRider"/> (their own crash models); open vehicles (buggies,
    /// rollers, pavers) eject above <see cref="EjectOpenDv"/>, closed cars only in very hard crashes above
    /// <see cref="EjectClosedDv"/> (through the windscreen). Walk-in vehicles and boats never eject.</summary>
    public partial class WastelandGame
    {
        /// <summary>Collision Δv (m/s, impulse / mass as <see cref="VehicleDamage"/> measures it) that throws the driver out.</summary>
        public const float EjectOpenDv = 9f, EjectClosedDv = 22f;

        /// <summary>The last ejection (tests, the HUD): when, the launch velocity, the travel before the crash, why.</summary>
        public struct Ejection { public float time; public Vector3 launch, travel, from; public string why; public VehicleDriver vehicle; }
        public Ejection LastEjection { get; private set; }

        /// <summary>Does a crash of <paramref name="dv"/> throw the driver of <paramref name="car"/> out?</summary>
        public static bool Ejects(VehicleDriver car, float dv)
        {
            if (!car || car.aiDriven) return false;
            if (car.GetComponent<BikeBalance>() || car.GetComponent<FlightModel>() || car.GetComponent<BoatModel>() || car.GetComponent<InteriorSpace>()) return false;
            return dv >= (OpenVehicle(car) ? EjectOpenDv : EjectClosedDv);
        }

        /// <summary>No cabin around the driver (buggies, open machines): <see cref="VehicleClimate.Enclosed"/>.</summary>
        public static bool OpenVehicle(VehicleDriver car) => car.TryGetComponent<VehicleClimate>(out var cl) && !cl.Enclosed;

        /// <summary>The body's launch: the travel before the impact less a quarter of the vehicle's own change (seat,
        /// bars and dash drag on it), plus a lift that grows with the crash (over the bars / the bonnet).</summary>
        public static Vector3 EjectVelocity(Vector3 before, Vector3 after)
        {
            var change = after - before;
            var v = before + change * 0.25f;
            v.y = Mathf.Max(v.y, 0f) + Mathf.Clamp(change.magnitude * 0.22f, 1.5f, 6f);
            return v;
        }

        /// <summary>Called by <see cref="VehicleDamage"/> on every hard contact of the driven vehicle.</summary>
        public void CrashEject(VehicleDriver car, float dv, Vector3 before, Vector3 after)
        {
            if (Current != car || !Player || !Ejects(car, dv)) return;
            bool open = OpenVehicle(car);
            float severity = (dv - (open ? EjectOpenDv : EjectClosedDv)) * 2.5f + 6f;           // the landing, on top of the crash itself
            Eject(car, EjectVelocity(before, after), severity, open ? "THROWN OUT" : "THROUGH THE WINDSCREEN");
        }

        /// <summary>Out of the seat as a ragdoll flying at <paramref name="launch"/>, tumbling head first; it passes
        /// through its own vehicle, the camera follows the body, CRASH injuries, and up again 2.6 s later.</summary>
        public void Eject(VehicleDriver car, Vector3 launch, float severity, string why)
        {
            if (Current != car || !Player) return;
            var at = Player.transform.position;
            Exit();
            var cc = Player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            var flat = new Vector3(launch.x, 0f, launch.z);
            Player.transform.SetPositionAndRotation(at + Vector3.up * (OpenVehicle(car) || car.GetComponent<BikeBalance>() ? 0.1f : 0.35f),
                flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : Player.transform.rotation);
            Toast(why + "!");
            var rd = Ragdoll.For(Player.Rig);
            // the shove lands high and forward: the head leads, the legs trail on the seat / bars → a forward tumble
            var head = Player.Rig.Bone(BodyPart.Head);
            var hit = head ? head.position : Player.transform.position + Vector3.up * 1.5f;
            rd.Go(launch.normalized * Mathf.Min(launch.magnitude, 25f) * 2f, hit, launch);
            // its own vehicle doesn't catch the body (it starts inside the cab)
            var mine = Player.Rig.GetComponentsInChildren<Collider>();
            var theirs = car.GetComponentsInChildren<Collider>();
            foreach (var a in mine) foreach (var b in theirs) if (a && b) Physics.IgnoreCollision(a, b, true);
            if (cameraRig)
            {
                var pelvis = Player.Rig.Bone(BodyPart.Pelvis);
                if (pelvis) cameraRig.SetTarget(pelvis);
                cameraRig.Shake(Mathf.Min(8f, severity * 0.3f));
            }
            LastEjection = new Ejection { time = Time.time, launch = launch, travel = car.TryGetComponent<VehicleDamage>(out var vd) ? vd.PreImpactVelocity : launch, from = at, why = why, vehicle = car };
            Vitals.Hurt(severity, "CRASH");
            if (!Vitals.Dead) { CancelInvoke(nameof(GetUp)); Invoke(nameof(GetUp), 2.6f); }
        }
    }
}
