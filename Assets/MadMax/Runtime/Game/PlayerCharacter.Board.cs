using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The body driven from outside for a sequence (getting in and out of a vehicle, WastelandGame.Boarding):
    /// while <see cref="Puppeted"/> the player's own Update leaves position, rotation and animation to the caller, who
    /// poses it every frame with <see cref="PuppetPose"/>. A caller that stops posing (destroyed, scene change) lets go
    /// automatically after a few frames.</summary>
    public partial class PlayerCharacter
    {
        public bool Puppeted { get; private set; }
        int puppetFrame;

        /// <summary>Body height factor (1 = 1.78 m) of the rig.</summary>
        public float BodyHeight => H;

        /// <summary>The root (feet) of a body sitting in <paramref name="car"/>'s driver seat, car-local: hips from the
        /// DriverEye (eye 0.74 h above the hips, 0.1 m behind).</summary>
        public static Vector3 SeatRootLocal(VehicleDriver car, float h)
        {
            var eye = car ? car.transform.Find("DriverEye") : null;
            var eyeLocal = eye ? eye.localPosition : new Vector3(0.3f, 1.1f, 0f);
            return eyeLocal + new Vector3(0f, -(0.94f + 0.74f) * h, -0.1f);
        }

        public void BeginPuppet()
        {
            if (!Puppeted) { ClearLow(); if (InTransit) ClearTransit(); }
            Puppeted = true; puppetFrame = Time.frameCount;
            swingT = -1f; AutoWalk = null; vy = 0f; Velocity = Vector3.zero; planar = Vector3.zero;
            if (cc) cc.enabled = false;
        }

        /// <summary>Let go; <paramref name="standing"/> = on the feet again (the controller comes back unless seated).</summary>
        public void EndPuppet(bool standing)
        {
            Puppeted = false;
            if (standing && cc && !SeatedIn && !Sitting && !Interior && !InTransit) cc.enabled = true;
        }

        /// <summary>One frame of the sequence: put the root at <paramref name="pos"/> / <paramref name="rot"/> (local to the
        /// parent when <paramref name="local"/>, i.e. seated in the vehicle) and run the animator with <paramref name="st"/>.</summary>
        public void PuppetPose(Vector3 pos, Quaternion rot, bool local, HumanAnimator.State st, float dt)
        {
            if (!Puppeted || anim == null) return;
            puppetFrame = Time.frameCount;
            if (local) transform.SetLocalPositionAndRotation(pos, rot); else transform.SetPositionAndRotation(pos, rot);
            Velocity = Vector3.zero;
            Ambient(ref st);
            st.lookPitch = lookPitch;
            TickAnim(dt, st);
            lastYaw = transform.eulerAngles.y;
        }

        /// <summary>Blend the next pose in at once (a clip change at a matched pose).</summary>
        public void PuppetSnap() => anim?.Snap();

        void PuppetWatch()
        {
            if (Time.frameCount - puppetFrame <= 10) return;
            EndPuppet(!SeatedIn);                                                               // the sequence went away: back to normal
        }
    }
}
