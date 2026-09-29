using System;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Procedural body animation: stride-locked gait with knee/ankle/elbow bends, pelvis bob and sway,
    /// counter-rotating torso, run lean, breathing idle, jump/fall, sitting (hands on the wheel), carrying,
    /// tool swings (one or two handed) and aiming. Pure pose math on HumanRig bones.</summary>
    public class HumanAnimator
    {
        public struct State
        {
            public float speed, verticalSpeed, lookPitch, steer, turnRate;
            public bool grounded, sitting, carrying, twoHanded, aiming;
            public bool lounging;            // sitting on furniture: hands in the lap instead of on a wheel
            public bool riding;              // astride a horse or a bike: legs apart and down, hands on the reins / grips
            public bool pedaling; public float pedal;   // a bicycle: the legs turn the cranks (phase, radians)
            public float limpL, limpR, armHurtL, armHurtR;   // 0..1 injuries: a limping leg swings less, a hurt arm is held in
            public ToolPose? tool;           // pose of an action in progress / held stance (null = none)
        }

        readonly HumanRig rig;
        float phase, time, landing, pelvisBase;
        public event Action<bool> Footstep;   // true = left foot

        public HumanAnimator(HumanRig rig)
        {
            this.rig = rig;
            pelvisBase = rig.Bone(BodyPart.Pelvis).localPosition.y;
        }

        public void Rebound() => pelvisBase = rig.Bone(BodyPart.Pelvis).localPosition.y;

        public void Tick(float dt, State s)
        {
            time += dt;
            float k = 1f - Mathf.Exp(-16f * dt);
            if (s.sitting) { Sit(s, k); return; }

            float run = Mathf.InverseLerp(2.6f, 5.5f, s.speed);
            float move = Mathf.Clamp01(s.speed / 1.2f);
            float stride = Mathf.Lerp(1.35f, 2.3f, run);
            float prev = phase;
            phase += s.speed / stride * Mathf.PI * 2f * dt;
            if (Mathf.Sign(Mathf.Sin(prev)) != Mathf.Sign(Mathf.Sin(phase)) && s.grounded && move > 0.2f) Footstep?.Invoke(Mathf.Sin(phase) > 0f);
            float sp = Mathf.Sin(phase), cp = Mathf.Cos(phase);

            float A = Mathf.Lerp(26f, 44f, run) * move;
            float Kn = Mathf.Lerp(55f, 100f, run) * move;
            float Aa = Mathf.Lerp(20f, 48f, run) * move;
            float elbow = Mathf.Lerp(10f, 80f, run) * move + 8f;
            float lean = run * 14f + Mathf.Clamp(s.verticalSpeed * -1f, -6f, 6f) * 0f;

            float thL = -A * sp, thR = A * sp;
            float knL = Kn * Mathf.Max(0f, cp) + 4f, knR = Kn * Mathf.Max(0f, -cp) + 4f;
            // limp: the hurt leg barely swings or bends; the pelvis drops and the body leans away when it takes the weight
            thL *= 1f - 0.6f * s.limpL; knL *= 1f - 0.55f * s.limpL;
            thR *= 1f - 0.6f * s.limpR; knR *= 1f - 0.55f * s.limpR;
            float hitch = (s.limpL * Mathf.Max(0f, sp) + s.limpR * Mathf.Max(0f, -sp)) * 0.045f * move;
            float limpRoll = (s.limpL - s.limpR) * 7f * move;
            if (!s.grounded)
            {
                thL = -38f; thR = -12f; knL = 70f; knR = 35f;
                landing = 1f;
            }
            landing = Mathf.MoveTowards(landing, 0f, dt * 4f);
            if (s.grounded && landing > 0f) { thL -= landing * 25f; thR -= landing * 25f; knL += landing * 45f; knR += landing * 45f; }

            Set(BodyPart.ThighL, thL - lean * 0.4f, 0, -2f, k);
            Set(BodyPart.ThighR, thR - lean * 0.4f, 0, 2f, k);
            Set(BodyPart.ShinL, knL, 0, 0, k);
            Set(BodyPart.ShinR, knR, 0, 0, k);
            Set(BodyPart.FootL, -(thL + knL) * 0.35f, 0, 0, k);
            Set(BodyPart.FootR, -(thR + knR) * 0.35f, 0, 0, k);

            var pelvis = rig.Bone(BodyPart.Pelvis);
            float bob = (1f - Mathf.Abs(cp)) * Mathf.Lerp(0.025f, 0.05f, run) * move + landing * 0.08f;
            pelvis.localPosition = new Vector3(0, pelvisBase - bob - hitch, 0);
            Set(BodyPart.Pelvis, 0, sp * 6f * move, cp * 3f * move + limpRoll, k);

            float breathe = Mathf.Sin(time * 1.7f) * 1.4f * (1f - move);
            float bank = Mathf.Clamp(s.turnRate * 0.05f, -8f, 8f) * move;
            Set(BodyPart.Chest, lean + breathe, -sp * 8f * move, -bank, k);
            Set(BodyPart.Head, -lean * 0.6f + Mathf.Clamp(s.lookPitch * 0.5f, -25f, 30f), sp * 3f * move, bank * 0.5f, k);

            // arms: gait swing, then carry / aim / tool override
            float armL = Aa * sp, armR = -Aa * sp, foreL = -elbow, foreR = -elbow, rollL = -6f, rollR = 6f;
            if (!s.grounded) { armL = -50f; armR = -40f; foreL = foreR = -30f; rollL = -25f; rollR = 25f; }
            if (s.carrying) { armL = armR = -62f; foreL = foreR = -28f; rollL = -10f; rollR = 10f; }
            if (s.aiming) { armR = -80f; armL = -75f; foreR = -10f; foreL = -35f; rollL = 20f; rollR = -4f; }
            // a badly hurt arm is held against the chest, a sore one swings less
            if (s.armHurtL > 0.6f) { armL = -22f; foreL = -100f; rollL = -18f; } else armL *= 1f - 0.5f * s.armHurtL;
            if (s.armHurtR > 0.6f) { armR = -22f; foreR = -100f; rollR = 18f; } else armR *= 1f - 0.5f * s.armHurtR;
            bool acting = s.tool.HasValue;
            float armLy = 0f, armRy = 0f, handR = 0f;
            if (acting)
            {
                var tp = s.tool.Value;
                armR = tp.armRX; armRy = tp.armRY; rollR = tp.armRZ; foreR = tp.foreR; handR = tp.handRZ;
                // left arm: + roll brings it across the body to the handle (two-handed), - holds it out for balance
                if (s.twoHanded || tp.armLX != 0f) { armL = tp.armLX; armLy = tp.armLY; rollL = tp.armLZ; foreL = tp.foreL; }
                float kk = 1f - Mathf.Exp(-40f * dt);
                Set(BodyPart.Chest, lean + tp.chestX, tp.chestY, 0, kk);
                if (tp.knees > 0f)
                {
                    Set(BodyPart.ThighL, thL - tp.knees * 0.6f, 0, -2f, kk); Set(BodyPart.ThighR, thR - tp.knees * 0.6f, 0, 2f, kk);
                    Set(BodyPart.ShinL, knL + tp.knees, 0, 0, kk); Set(BodyPart.ShinR, knR + tp.knees, 0, 0, kk);
                    // bent knees lower the hips so the feet stay planted (crouch, slide, braced swings)
                    if (s.grounded)
                    {
                        float kr = tp.knees * Mathf.Deg2Rad;
                        pelvis.localPosition -= Vector3.up * (pelvisBase * (1f - 0.5f * (Mathf.Cos(kr * 0.6f) + Mathf.Cos(kr * 0.4f))));
                    }
                }
            }
            float ks = acting ? 1f - Mathf.Exp(-40f * dt) : k;   // actions follow their keyframes tightly
            Set(BodyPart.UpperArmL, armL, armLy, rollL, ks);
            Set(BodyPart.UpperArmR, armR, armRy, rollR, ks);
            Set(BodyPart.ForearmL, foreL, 0, 0, ks);
            Set(BodyPart.ForearmR, foreR, 0, 0, ks);
            Set(BodyPart.HandL, 0, 0, 0, k);
            Set(BodyPart.HandR, 0, 0, handR, ks);
        }

        void Sit(State s, float k)
        {
            var pelvis = rig.Bone(BodyPart.Pelvis);
            pelvis.localPosition = new Vector3(0, pelvisBase, 0);
            Set(BodyPart.Pelvis, 0, 0, 0, k);
            Set(BodyPart.ThighL, -84f, 0, -4f, k);
            Set(BodyPart.ThighR, -84f, 0, 4f, k);
            Set(BodyPart.ShinL, 78f, 0, 0, k);
            Set(BodyPart.ShinR, 72f, 0, 0, k);
            Set(BodyPart.FootL, 4f, 0, 0, k);
            Set(BodyPart.FootR, 10f, 0, 0, k);
            Set(BodyPart.Chest, -6f + Mathf.Sin(time * 1.6f) * 0.8f, s.steer * 4f, 0, k);
            Set(BodyPart.Head, Mathf.Clamp(s.lookPitch * 0.5f, -25f, 30f), s.steer * 8f, 0, k);
            if (s.riding)
            {
                // astride: thighs down and apart, heels back, upright with a little bounce, hands forward on the reins
                float bounce = Mathf.Sin(time * 9f) * 2f;
                float pl = s.pedaling ? Mathf.Sin(s.pedal) * 22f : 0f, pk = s.pedaling ? Mathf.Cos(s.pedal) * 20f : 0f;
                float spread = s.pedaling ? 8f : 24f;
                Set(BodyPart.ThighL, -38f - (s.pedaling ? 20f : 0f) + pl, 0, -spread, k);
                Set(BodyPart.ThighR, -38f - (s.pedaling ? 20f : 0f) - pl, 0, spread, k);
                Set(BodyPart.ShinL, 52f + (s.pedaling ? 20f : 0f) + pk, 0, 0, k);
                Set(BodyPart.ShinR, 52f + (s.pedaling ? 20f : 0f) - pk, 0, 0, k);
                Set(BodyPart.FootL, -10f, 0, 0, k);
                Set(BodyPart.FootR, -10f, 0, 0, k);
                Set(BodyPart.Chest, 4f + bounce, s.steer * 6f, 0, k);
                Set(BodyPart.UpperArmL, -40f, 0, -6f, k);
                Set(BodyPart.UpperArmR, -40f, 0, 6f, k);
                Set(BodyPart.ForearmL, -50f + s.steer * 10f, 0, 0, k);
                Set(BodyPart.ForearmR, -50f - s.steer * 10f, 0, 0, k);
                return;
            }
            if (s.lounging)
            {
                // leaning back a little, hands resting on the thighs
                Set(BodyPart.Chest, 6f + Mathf.Sin(time * 1.4f) * 0.8f, 0, 0, k);
                Set(BodyPart.UpperArmL, -26f, 0, -10f, k);
                Set(BodyPart.UpperArmR, -26f, 0, 10f, k);
                Set(BodyPart.ForearmL, -58f, 0, 0, k);
                Set(BodyPart.ForearmR, -58f, 0, 0, k);
                return;
            }
            // hands on the wheel, rotated with the steering
            Set(BodyPart.UpperArmL, -72f + s.steer * 10f, 0, -6f + s.steer * 12f, k);
            Set(BodyPart.UpperArmR, -72f - s.steer * 10f, 0, 6f + s.steer * 12f, k);
            Set(BodyPart.ForearmL, -24f, 0, 0, k);
            Set(BodyPart.ForearmR, -24f, 0, 0, k);
        }

        void Set(BodyPart p, float x, float y, float z, float k)
        {
            var t = rig.Bone(p);
            t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(x, y, z), k);
        }
    }
}
