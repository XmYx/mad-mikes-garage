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
        // extras (CLIPS.md): root motion in the start frame (height-1 metres, degrees + = right), door open share,
        // ground share (car clips: 1 while the feet carry the body outside), named events at t 0..1
        float[] rootX, rootZ, rootYaw, door, ground;
        string[] eventNames;
        float[] eventTimes;

        [Serializable]
        class Json
        {
            public string name, mask; public int fps, frames; public float length, hit, speed; public bool loop, gait; public string[] bones; public float[] rot, pelvis;
            public float[] rootX, rootZ, rootYaw, door, ground; public string[] eventNames; public float[] eventTimes;
        }

        /// <summary>The clip moves its root (rootX / rootZ / rootYaw): the runtime moves the body by it.</summary>
        public bool HasRoot => rootZ != null || rootX != null || rootYaw != null;
        public bool HasDoor => door != null;
        public bool HasGround => ground != null;
        public int EventCount => eventNames != null ? eventNames.Length : 0;
        public string EventName(int i) => eventNames[i];
        public float EventTime(int i) => eventTimes[i];

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
            c.rootX = Frames(j.rootX, j.frames); c.rootZ = Frames(j.rootZ, j.frames); c.rootYaw = Frames(j.rootYaw, j.frames);
            c.door = Frames(j.door, j.frames); c.ground = Frames(j.ground, j.frames);
            if (j.eventNames != null && j.eventTimes != null && j.eventNames.Length > 0)
            {
                int n = Mathf.Min(j.eventNames.Length, j.eventTimes.Length);
                c.eventNames = new string[n]; c.eventTimes = new float[n];
                Array.Copy(j.eventNames, c.eventNames, n); Array.Copy(j.eventTimes, c.eventTimes, n);
            }
            return c;
        }

        /// <summary>A per-frame array, or null when absent / the wrong size (JsonUtility leaves missing arrays empty).</summary>
        static float[] Frames(float[] a, int frames) => a != null && a.Length >= frames && frames > 0 ? a : null;

        // ------------------------------------------------------------------ extras
        void Frame(float t, out int i0, out int i1, out float u)
        {
            float f;
            if (loop) { t -= Mathf.Floor(t); f = t * frames; i0 = Mathf.FloorToInt(f) % frames; i1 = (i0 + 1) % frames; }
            else { f = Mathf.Clamp01(t) * (frames - 1); i0 = Mathf.Min(Mathf.FloorToInt(f), frames - 1); i1 = Mathf.Min(i0 + 1, frames - 1); }
            u = f - Mathf.Floor(f);
        }

        float Curve(float[] a, float t, float none)
        {
            if (a == null) return none;
            Frame(t, out int i0, out int i1, out float u);
            return Mathf.Lerp(a[i0], a[i1], u);
        }

        /// <summary>Root offset (height-1 metres, the clip's start frame: +z = the start facing) and yaw (degrees, + right) at t.</summary>
        public Vector3 RootAt(float t) => new Vector3(Curve(rootX, t, 0f), 0f, Curve(rootZ, t, 0f));
        public float RootYawAt(float t) => Curve(rootYaw, t, 0f);
        /// <summary>Forward root speed at t (height-1 m per clip second).</summary>
        public float RootSpeedAt(float t)
        {
            if (rootZ == null || frames < 2) return 0f;
            float step = 1f / (frames - 1), a = Mathf.Clamp01(t - step), b = Mathf.Clamp01(t + step);
            return (Curve(rootZ, b, 0f) - Curve(rootZ, a, 0f)) / Mathf.Max(1e-4f, (b - a) * length);
        }
        /// <summary>The clip time (0..1) at which the root has come <paramref name="z"/> forward (height-1 metres).</summary>
        public float TimeAtRootZ(float z)
        {
            if (rootZ == null) return 1f;
            if (z <= rootZ[0]) return 0f;
            for (int f = 1; f < frames; f++)
                if (rootZ[f] >= z) { float u = Mathf.InverseLerp(rootZ[f - 1], rootZ[f], z); return (f - 1 + u) / (frames - 1); }
            return 1f;
        }
        /// <summary>The door's open share at t (-1 = the clip doesn't drive a door).</summary>
        public float DoorAt(float t) => Curve(door, t, -1f);
        /// <summary>Car clips: 1 while the feet carry the body on the ground outside, 0 seated (-1 = none).</summary>
        public float GroundAt(float t) => Curve(ground, t, -1f);
        /// <summary>First time of the named event (-1 = none).</summary>
        public float EventAt(string n)
        {
            for (int i = 0; i < EventCount; i++) if (eventNames[i] == n) return eventTimes[i];
            return -1f;
        }
        /// <summary>The named event falls in (t0, t1] (t0 &lt; 0: from the start, inclusive).</summary>
        public bool Crossed(string n, float t0, float t1)
        {
            for (int i = 0; i < EventCount; i++)
                if (eventNames[i] == n && (t0 < 0f ? eventTimes[i] >= 0f : eventTimes[i] > t0) && eventTimes[i] <= t1) return true;
            return false;
        }

        /// <summary>The same motion on the other side of the body (left and right bones swapped, rotations reflected across
        /// the body's centre plane): a "_r" clip that was not authored is made from its left-hand original.</summary>
        public HumanClip Mirror(string newName)
        {
            var c = new HumanClip { name = newName, frames = frames, length = length, loop = loop, gait = gait, hit = hit, speed = speed, mask = mask,
                                    rot = new Quaternion[rot.Length], pelvis = new Vector3[pelvis.Length],
                                    rootZ = rootZ, door = door, ground = ground, eventTimes = eventTimes };
            if (rootX != null) { c.rootX = new float[rootX.Length]; for (int i = 0; i < rootX.Length; i++) c.rootX[i] = -rootX[i]; }
            if (rootYaw != null) { c.rootYaw = new float[rootYaw.Length]; for (int i = 0; i < rootYaw.Length; i++) c.rootYaw[i] = -rootYaw[i]; }
            if (eventNames != null)
            {
                c.eventNames = new string[eventNames.Length];
                for (int i = 0; i < eventNames.Length; i++)
                {
                    var e = eventNames[i];
                    c.eventNames[i] = e.EndsWith("L") ? e.Substring(0, e.Length - 1) + "R" : e.EndsWith("R") ? e.Substring(0, e.Length - 1) + "L" : e;
                }
            }
            for (int f = 0; f < frames; f++)
            {
                for (int b = 0; b < Bones; b++)
                {
                    var q = rot[f * Bones + b];
                    c.rot[f * Bones + MirrorBone(b)] = new Quaternion(q.x, -q.y, -q.z, q.w);
                }
                var p = pelvis[f];
                c.pelvis[f] = new Vector3(-p.x, p.y, p.z);
            }
            return c;
        }

        /// <summary>The bone on the other side (BodyPart order: the L/R pairs sit next to each other from UpperArmL on).</summary>
        static int MirrorBone(int b) => b < (int)BodyPart.UpperArmL ? b : (b - (int)BodyPart.UpperArmL) % 2 == 0 ? b + 1 : b - 1;

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
        static readonly HashSet<string> unmirrored = new HashSet<string>();
        /// <summary>Tests: force the procedural animator.</summary>
        public static bool ForceProcedural;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { clips = null; ForceProcedural = false; unmirrored.Clear(); }

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
            if (clips.TryGetValue(name, out var c)) return c;
            // a right-side clip ("car_enter_r") nobody authored: the left-side one mirrored, made once
            if (name.Length > 2 && name[name.Length - 2] == '_' && name[name.Length - 1] == 'r' && !unmirrored.Contains(name))
            {
                if (clips.TryGetValue(name.Substring(0, name.Length - 2), out var left)) { c = left.Mirror(name); clips[name] = c; return c; }
                unmirrored.Add(name);
            }
            return null;
        }

        public static IEnumerable<string> Names { get { if (clips == null) Load(); return clips.Keys; } }
    }
}
