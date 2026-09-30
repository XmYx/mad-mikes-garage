using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The sliding leaf of a <see cref="MotorGate"/>: a kinematic body that stops people and vehicles. A vehicle
    /// ramming it faster than <see cref="RamSpeed"/> m/s knocks hits off the gate by its momentum (a car at 50 km/h takes
    /// about a third of a steel gate); [E]/[T] on the leaf work the gate like its drive.</summary>
    public class GateLeaf : MonoBehaviour, IInteractable
    {
        public const float RamSpeed = 3f;
        MotorGate gate;
        Placeable piece;
        float cooldown;

        MotorGate Gate => gate ? gate : gate = GetComponentInParent<MotorGate>();          // added after the leaf in the piece's setup
        void OnEnable() => PartFunctions.Interactables.Add(this);
        void OnDisable() => PartFunctions.Interactables.Remove(this);

        public string Prompt(MadMax.Game.WastelandGame g) => Gate ? Gate.Prompt(g) : null;
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (Gate) Gate.Use(g, secondary); }

        void OnCollisionEnter(Collision c)
        {
            var rb = c.rigidbody;
            if (!piece) piece = GetComponentInParent<Placeable>();
            if (!rb || rb.isKinematic || !piece || Time.time < cooldown || !rb.GetComponent<VehicleDriver>()) return;
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;
            float dv = c.relativeVelocity.magnitude;
            if (dv < RamSpeed) return;
            cooldown = Time.time + 0.5f;
            var at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            float blows = dv * rb.mass / 1000f * 0.6f;
            MadMax.Audio.Sfx.Play("crash_big", at, Mathf.Clamp01(0.4f + dv * 0.05f), 0.8f, 60f);
            MadMax.World.Fx.Sparks(at, -c.relativeVelocity.normalized, 6, new Color(1f, 0.72f, 0.3f));
            piece.ApplyHit(at, -c.relativeVelocity.normalized, blows, 0.6f, rb.gameObject);
        }
    }
}
