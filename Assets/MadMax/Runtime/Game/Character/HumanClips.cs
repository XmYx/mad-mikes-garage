using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>One keyframed clip for the human (authored in Blender on the HD rig: tools/blender/hd/character/clips.py +
    /// anims.py): per frame a local rotation for every <see cref="BodyPart"/> (identity rest, HumanAnimator convention)
    /// and the pelvis offset from its rest position (metres for height 1).</summary>
    public sealed class HumanClip
    {
        public const int Bones = 15;
        public enum Mask : byte { Full, Upper, Lower, Arms, ArmR }

        public string name;
        public int frames;
        public float length;
        public bool loop, gait;
        public Mask mask;
        /// <summary>Impact moment 0..1 (actions; -1 none), mapped onto the tool's strikeAt.</summary>
        public float hit = -1f;
        /// <summary>Speed the gait cycle was authored for (m/s).</summary>
        public float speed;
        Quaternion[] rot;      // frames x Bones (BodyPart order)
        Vector3[] pelvis;

        [Serializable]
        class Json { public string name, mask; public int fps, frames; public float length, hit, speed; public bool loop, gait; public string[] bones; public float[] rot, pelvis; }

        public static HumanClip Parse(string text)
        {
            var j = JsonUtility.FromJson<Json>(text);
            if (j == null || j.frames <= 0 || j.bones == null || j.rot == null) return null;
            var c = new HumanClip
            {
                name = j.name, frames = j.frames, length = Mathf.Max(0.01f, j.length), loop = j.loop, gait = j.gait, hit = j.hit, speed = j.speed,
                mask = j.mask == "upper" ? Mask.Upper : j.mask == "lower" ? Mask.Lower : j.mask == "arms" ? Mask.Arms : j.mask == "arm_r" ? Mask.ArmR : Mask.Full,
                rot = new Quaternion[j.frames * Bones], pelvis = new Vector3[j.frames],
            };
            for (int i = 0; i < c.rot.Length; i++) c.rot[i] = Quaternion.identity;
            var map = new int[j.bones.Length];
            for (int b = 0; b < map.Length; b++) map[b] = Enum.TryParse<BodyPart>(j.bones[b], out var bp) ? (int)bp : -1;
            int nb = j.bones.Length;
            for (int f = 0; f < j.frames; f++)
            {
                for (int b = 0; b < nb; b++)
                {
                    if (map[b] < 0) continue;
                    int o = (f * nb + b) * 4;
                    if (o + 3 >= j.rot.Length) break;
                    c.rot[f * Bones + map[b]] = new Quaternion(j.rot[o], j.rot[o + 1], j.rot[o + 2], j.rot[o + 3]);
                }
                if (j.pelvis != null && f * 3 + 2 < j.pelvis.Length) c.pelvis[f] = new Vector3(j.pelvis[f * 3], j.pelvis[f * 3 + 1], j.pelvis[f * 3 + 2]);
            }
            return c;
        }

        public bool Affects(BodyPart p) => mask switch
        {
            Mask.Full => true,
            Mask.Arms => p >= BodyPart.UpperArmL && p <= BodyPart.HandR,
            Mask.ArmR => p == BodyPart.UpperArmR || p == BodyPart.ForearmR || p == BodyPart.HandR,
            _ => (mask == Mask.Upper) == IsUpper(p)
        };

        public static bool IsUpper(BodyPart p) => p == BodyPart.Chest || p == BodyPart.Head || (p >= BodyPart.UpperArmL && p <= BodyPart.HandR);

        /// <summary>Pose at <paramref name="t"/> (0..1 of the clip; loops wrap, one-shots clamp).</summary>
        public void Sample(float t, Quaternion[] outRot, out Vector3 outPelvis)
        {
            float f;
            int i0, i1;
            if (loop)
            {
                t -= Mathf.Floor(t);
                f = t * frames;
                i0 = Mathf.FloorToInt(f) % frames; i1 = (i0 + 1) % frames;
            }
            else
            {
                f = Mathf.Clamp01(t) * (frames - 1);
                i0 = Mathf.Min(Mathf.FloorToInt(f), frames - 1); i1 = Mathf.Min(i0 + 1, frames - 1);
            }
            float u = f - Mathf.Floor(f);
            int a = i0 * Bones, b = i1 * Bones;
            for (int k = 0; k < Bones; k++) outRot[k] = Quaternion.Slerp(rot[a + k], rot[b + k], u);
            outPelvis = Vector3.Lerp(pelvis[i0], pelvis[i1], u);
        }
    }

    /// <summary>The clip library (Resources/CharacterAnims/*.json), loaded once. <see cref="Enabled"/> is false with
    /// --procedural-anim (MadMax > Dev > Procedural Animation) or when no clips are installed: HumanAnimator then keeps
    /// its procedural poses.</summary>
    public static class HumanClips
    {
        static Dictionary<string, HumanClip> clips;
        /// <summary>Tests: force the procedural animator.</summary>
        public static bool ForceProcedural;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { clips = null; ForceProcedural = false; }

        static void Load()
        {
            clips = new Dictionary<string, HumanClip>();
            foreach (var ta in Resources.LoadAll<TextAsset>("CharacterAnims"))
            {
                if (!ta || ta.name == "clips_index") continue;
                try { var c = HumanClip.Parse(ta.text); if (c != null) clips[c.name ?? ta.name] = c; }
                catch (Exception e) { Debug.LogWarning("[anim] clip " + ta.name + ": " + e.Message); }
            }
        }

        public static int Count { get { if (clips == null) Load(); return clips.Count; } }

        public static bool Enabled => !ForceProcedural && !LaunchOptions.ProceduralAnim && Count > 0;

        public static HumanClip Get(string name)
        {
            if (name == null) return null;
            if (clips == null) Load();
            return clips.TryGetValue(name, out var c) ? c : null;
        }

        public static IEnumerable<string> Names { get { if (clips == null) Load(); return clips.Keys; } }
    }
}
