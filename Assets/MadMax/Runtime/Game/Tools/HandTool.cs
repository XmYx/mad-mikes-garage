using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Full-body pose for a tool action (degrees, bone-local Euler).</summary>
    public struct ToolPose
    {
        public float chestX, chestY, armRX, armRY, armRZ, foreR, armLX, armLY, armLZ, foreL, knees, handRZ;

        public static ToolPose Lerp(ToolPose a, ToolPose b, float t)
        {
            float L(float x, float y) => Mathf.LerpUnclamped(x, y, t);
            return new ToolPose
            {
                chestX = L(a.chestX, b.chestX), chestY = L(a.chestY, b.chestY),
                armRX = L(a.armRX, b.armRX), armRY = L(a.armRY, b.armRY), armRZ = L(a.armRZ, b.armRZ), foreR = L(a.foreR, b.foreR),
                armLX = L(a.armLX, b.armLX), armLY = L(a.armLY, b.armLY), armLZ = L(a.armLZ, b.armLZ), foreL = L(a.foreL, b.foreL),
                knees = L(a.knees, b.knees), handRZ = L(a.handRZ, b.handRZ)
            };
        }
    }

    public enum ToolStyle { Overhead, Slash, Twist, Grind, Gun, Thrust }

    /// <summary>Base for tools and weapons held by the on-foot player. PlayerCharacter plays <see cref="Pose"/> over
    /// the action and calls <see cref="Strike"/> once at <see cref="strikeAt"/> (0..1 of the action).</summary>
    public abstract class HandTool : MonoBehaviour
    {
        public string id;
        public string toolName = "TOOL";
        public ToolStyle style = ToolStyle.Overhead;
        public float swingDuration = 0.55f;
        [Range(0f, 1f)] public float strikeAt = 0.64f;

        public abstract void Strike(PlayerCharacter user);

        public bool TwoHanded => style == ToolStyle.Overhead || style == ToolStyle.Grind || style == ToolStyle.Gun || style == ToolStyle.Thrust;

        // ---- keyframes: (time, pose). Anticipation → strike → follow-through → recovery, whole body involved.
        static readonly (float t, ToolPose p)[] Overhead =
        {
            (0f,    new ToolPose { armRX = -25, armLX = -30, armLZ = 14, foreR = -30, foreL = -35 }),
            (0.38f, new ToolPose { chestX = -14, chestY = -8, armRX = 168, armRZ = 6, foreR = -45, armLX = 150, armLZ = 22, foreL = -55, knees = 8 }),
            (0.5f,  new ToolPose { chestX = -16, chestY = -8, armRX = 172, armRZ = 6, foreR = -50, armLX = 155, armLZ = 22, foreL = -60, knees = 8 }),   // hold at the top
            (0.62f, new ToolPose { chestX = 28, armRX = -62, foreR = -4, armLX = -52, armLZ = 16, foreL = -18, knees = 30 }),                           // impact
            (0.74f, new ToolPose { chestX = 34, armRX = -40, foreR = -8, armLX = -36, armLZ = 14, foreL = -22, knees = 34 }),                           // follow-through
            (1f,    new ToolPose { armRX = -25, armLX = -30, armLZ = 14, foreR = -30, foreL = -35 }),
        };
        static readonly (float t, ToolPose p)[] Slash =
        {
            (0f,    new ToolPose { armRX = -10, foreR = -20 }),
            (0.32f, new ToolPose { chestY = -38, chestX = -4, armRX = 118, armRY = -20, armRZ = 48, foreR = -85, armLX = -35, armLZ = -18, foreL = -40, knees = 6 }),   // cocked back
            (0.5f,  new ToolPose { chestY = 30, chestX = 10, armRX = -58, armRY = 25, armRZ = -24, foreR = -8, armLX = 18, armLZ = -10, foreL = -25, knees = 16 }),    // diagonal cut
            (0.66f, new ToolPose { chestY = 42, chestX = 14, armRX = -35, armRY = 30, armRZ = -38, foreR = -12, armLX = 22, foreL = -20, knees = 14 }),
            (1f,    new ToolPose { armRX = -10, foreR = -20 }),
        };
        static readonly (float t, ToolPose p)[] Twist =
        {
            (0f,    new ToolPose { armRX = -15, foreR = -25 }),
            (0.2f,  new ToolPose { chestX = 18, armRX = -72, foreR = -30, armLX = -40, armLZ = 12, foreL = -45, knees = 20, handRZ = -40 }),
            (0.4f,  new ToolPose { chestX = 20, armRX = -74, foreR = -26, armLX = -40, armLZ = 12, foreL = -45, knees = 22, handRZ = 45 }),     // ratchet
            (0.55f, new ToolPose { chestX = 20, armRX = -72, foreR = -30, armLX = -40, armLZ = 12, foreL = -45, knees = 22, handRZ = -40 }),
            (0.7f,  new ToolPose { chestX = 20, armRX = -74, foreR = -26, armLX = -40, armLZ = 12, foreL = -45, knees = 22, handRZ = 45 }),
            (1f,    new ToolPose { armRX = -15, foreR = -25 }),
        };
        static readonly (float t, ToolPose p)[] Grind =
        {
            (0f,    new ToolPose { armRX = -40, foreR = -45, armLX = -42, armLZ = 20, foreL = -55 }),
            (0.3f,  new ToolPose { chestX = 16, armRX = -62, foreR = -34, armLX = -60, armLZ = 24, foreL = -46, knees = 18 }),
            (0.45f, new ToolPose { chestX = 22, armRX = -70, foreR = -24, armLX = -66, armLZ = 26, foreL = -38, knees = 24 }),        // press into the cut
            (0.75f, new ToolPose { chestX = 20, armRX = -68, foreR = -26, armLX = -64, armLZ = 26, foreL = -40, knees = 24 }),
            (1f,    new ToolPose { armRX = -40, foreR = -45, armLX = -42, armLZ = 20, foreL = -55 }),
        };
        static readonly (float t, ToolPose p)[] Gun =
        {
            (0f,    new ToolPose { armRX = -84, foreR = -6, armLX = -78, armLZ = 26, foreL = -40 }),
            (0.06f, new ToolPose { chestX = -9, armRX = -112, foreR = -18, armLX = -100, armLZ = 26, foreL = -52 }),                // recoil kick
            (0.3f,  new ToolPose { chestX = -2, armRX = -88, foreR = -8, armLX = -80, armLZ = 26, foreL = -40 }),
            (0.45f, new ToolPose { armRX = -86, foreR = -6, armLX = -80, armLZ = 26, foreL = -70 }),                              // pump back
            (0.6f,  new ToolPose { armRX = -84, foreR = -6, armLX = -78, armLZ = 26, foreL = -38 }),                              // pump forward
            (1f,    new ToolPose { armRX = -84, foreR = -6, armLX = -78, armLZ = 26, foreL = -40 }),
        };

        static readonly (float t, ToolPose p)[] Thrust =
        {
            (0f,    new ToolPose { armRX = -40, foreR = -60, armLX = -45, armLZ = 18, foreL = -60 }),
            (0.35f, new ToolPose { chestY = -18, armRX = -35, armRY = -10, foreR = -110, armLX = -50, armLZ = 20, foreL = -80, knees = 10 }),   // drawn back
            (0.5f,  new ToolPose { chestY = 12, chestX = 10, armRX = -88, foreR = -4, armLX = -80, armLZ = 14, foreL = -20, knees = 22 }),      // lunge
            (0.7f,  new ToolPose { chestY = 8, chestX = 8, armRX = -84, foreR = -8, armLX = -74, armLZ = 14, foreL = -26, knees = 18 }),
            (1f,    new ToolPose { armRX = -40, foreR = -60, armLX = -45, armLZ = 18, foreL = -60 }),
        };

        (float t, ToolPose p)[] Keys => style switch
        {
            ToolStyle.Slash => Slash, ToolStyle.Twist => Twist, ToolStyle.Grind => Grind, ToolStyle.Gun => Gun, ToolStyle.Thrust => Thrust, _ => Overhead
        };

        /// <summary>Pose at normalised action time t (smooth-stepped between keys; grinding adds vibration).</summary>
        public virtual ToolPose Pose(float t)
        {
            var k = Keys;
            t = Mathf.Clamp01(t);
            int i = 0;
            while (i < k.Length - 2 && t > k[i + 1].t) i++;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k[i].t, k[i + 1].t, t));
            var p = ToolPose.Lerp(k[i].p, k[i + 1].p, f);
            if (style == ToolStyle.Grind && t > 0.3f && t < 0.8f)
            {
                float jitter = Mathf.Sin(Time.time * 90f) * 1.6f;
                p.armRX += jitter; p.armLX += jitter; p.chestX += jitter * 0.3f;
            }
            return p;
        }

        /// <summary>Pose while held and idle (guns stay shouldered); null = normal gait arms.</summary>
        public virtual ToolPose? IdlePose => style == ToolStyle.Gun ? Gun[0].p : (ToolPose?)null;
    }
}
