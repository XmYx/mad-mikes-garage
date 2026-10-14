using MadMax.Building;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Moving bases (<see cref="RigVehicle"/>): registering a new rig in the fleet, letting go of one taken apart,
    /// and the prompt beside one.</summary>
    public partial class WastelandGame
    {
        /// <summary>Start a moving base from a frame piece at a world pose (the frame's bottom centre).</summary>
        public RigVehicle StartRig(string frameId, Vector3 at, Quaternion rot, out Placeable frame)
        {
            var rig = RigVehicle.Create(frameId, at, rot, propMaterial, Stats != null ? Stats.name : null, out frame);
            Register(rig.Driver, fleet);
            MadMax.Net.NetSession.Instance?.SendRigSpawn(rig.Driver, frameId, frame);           // every peer starts the same base (NetSession.Tools)
            return rig;
        }

        /// <summary>A vehicle taken apart as a whole (a rig's last frame section came off).</summary>
        public void ForgetVehicle(VehicleDriver v)
        {
            if (!v) return;
            if (v == Current) Exit();
            cars.Remove(v); fleet.Remove(v); trailers.Remove(v); wrecks.Remove(v); vehicles.Remove(v);
        }

        /// <summary>Beside a moving base away from its seat: what it still needs, or where to drive it from.</summary>
        string RigPrompt(VehicleDriver v)
        {
            var r = v ? v.GetComponent<RigVehicle>() : null;
            if (!r) return null;
            return r.Commissioned ? "MOVING BASE: DRIVE FROM THE SEAT" : "MOVING BASE: " + r.Missing;
        }
    }
}
