using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Breath in the cold (2026-10-29, <see cref="MadMax.World.ColdBreath"/>): every body breathes a cloud out of
    /// the mouth below 4 °C — at rest every few seconds, quicker running and out of breath, never inside a vehicle's cabin.</summary>
    public partial class HumanAnimator
    {
        float breathClock = -1f;
        Transform cabinRoot;
        bool inCabin;

        void Breathe(float dt, State s)
        {
            var eye = rig.Eye;
            if (!eye) return;
            if (breathClock < 0f) breathClock = Random.value;                                     // people out of step with each other
            var root = rig.transform.root;
            if (root != cabinRoot) { cabinRoot = root; inCabin = rig.GetComponentInParent<MadMax.Vehicles.VehicleDriver>(); }
            if (inCabin && s.sitting) return;
            float exertion = Mathf.Max(Mathf.InverseLerp(1.2f, 6f, s.speed), s.tired);
            var fwd = eye.forward;
            MadMax.World.ColdBreath.Tick(ref breathClock, eye.position - eye.up * 0.09f + fwd * 0.07f, fwd, exertion, 1f, dt);
        }
    }
}
