using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Raised air intake: while mounted, the engine breathes at the scoop, so it only floods when the water
    /// rises above it (see VehicleSystems).</summary>
    public class Snorkel : MonoBehaviour
    {
        VehiclePart part;
        void Awake() => part = GetComponent<VehiclePart>();

        public bool Mounted => part && part.Socket;
        /// <summary>World position of the intake mouth (head of the pipe, facing forward).</summary>
        public Vector3 Intake => transform.TransformPoint(new Vector3(0.12f, 1.36f, 0.32f));
    }
}
