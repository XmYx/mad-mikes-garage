using MadMax.Npc;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The Remnant on duty at a checkpoint (2026-10-31; the hut was empty and the boom lifted by itself). A guard
    /// with a bolt rifle (a lantern by night) stands on the shoulder beside the boom post while someone is within
    /// <see cref="Reach"/> m (<see cref="Checkpoint"/> keeps them). A car coming at the lowered boom: they turn to it and
    /// hold a palm up (STOP); the toll paid or a friend: they wave it through, the arm sweeping along the road; the boom
    /// smashed: they shake a fist and shout. Decoration over a real <see cref="MadMax.Npc.Npc"/> (talk, trade, fight back
    /// when hurt); the arm is posed after the person's own animation.</summary>
    public class CheckpointGuard : MonoBehaviour
    {
        public const float Reach = 120f, FoldReach = 170f, StopReach = 45f;
        public enum Gesture { None, Stop, Wave, Fist }
        public Checkpoint post;
        MadMax.Npc.Npc npc;
        float lift, waveUntil, fistUntil;
        public Gesture Showing { get; private set; }
        public float Lift => lift;
        /// <summary>Stops signalled, cars waved through, fists shaken this session (tests).</summary>
        public static int Stops, Waves, Fists;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Stops = 0; Waves = 0; Fists = 0; }

        public static MadMax.Npc.Npc Spawn(Checkpoint post, Vector3 at, float yaw, Material mat, int seed)
        {
            bool night = DayNight.Hours < 6.5f || DayNight.Hours > 19.5f;
            var prof = NpcProfile.Make("cpguard:" + seed, NpcRole.Shopkeeper, seed);   // stands at the post (Routine keeps keepers standing), like the stationmaster
            prof.title = "REMNANT GUARD";
            prof.tool = night ? "tool_lantern" : "tool_bolt_rifle";
            var n = MadMax.Npc.Npc.Spawn(prof, at, yaw, null, mat);
            n.homeRadius = 0.5f;
            n.mode = MadMax.Npc.Npc.Mode.Stand;
            var gd = n.gameObject.AddComponent<CheckpointGuard>();
            gd.npc = n; gd.post = post;
            return n;
        }

        public void WaveThrough() { waveUntil = Time.time + 3.2f; Waves++; }
        public void ShakeFist()
        {
            fistUntil = Time.time + 4f; Fists++;
            if (npc) MadMax.Game.WastelandGame.Remark(npc, Random.value < 0.5f ? "HEY! THAT BOOM WAS REMNANT PROPERTY!" : "YOU'LL PAY FOR THAT, ROAD RAT!");
        }

        void LateUpdate()
        {
            if (!npc || !npc.Alive || !post) return;
            float dt = Time.deltaTime;
            var g = MadMax.Game.WastelandGame.Instance;
            var car = g ? g.Current : null;
            Gesture want = Gesture.None;
            if (Time.time < fistUntil) want = Gesture.Fist;
            else if (Time.time < waveUntil) want = Gesture.Wave;
            else if (car && !post.IsOpen && !post.Broken && !npc.Hostile && npc.mode == MadMax.Npc.Npc.Mode.Stand)
            {
                var to = car.transform.position - transform.position; to.y = 0f;
                var v = car.Body ? car.Body.linearVelocity : Vector3.zero; v.y = 0f;
                if (to.magnitude < StopReach && (v.sqrMagnitude < 1f || Vector3.Dot(v, -to) > 0f)) want = Gesture.Stop;
            }
            if (want != Gesture.None && car)
            {
                var to = car.transform.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.5f) npc.homeYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;   // Stand turns to face it
            }
            if (want == Gesture.Stop && Showing != Gesture.Stop) Stops++;
            if (want != Gesture.None) Showing = want;
            lift = Mathf.MoveTowards(lift, want != Gesture.None ? 1f : 0f, dt * 3f);
            if (lift <= 0f) { Showing = Gesture.None; return; }
            var rig = npc.Rig;
            if (!rig) return;
            var ua = rig.Bone(MadMax.Game.BodyPart.UpperArmR);
            var fa = rig.Bone(MadMax.Game.BodyPart.ForearmR);
            Quaternion u, f;
            switch (Showing)
            {
                case Gesture.Stop: u = Quaternion.Euler(-92f, 0f, 8f); f = Quaternion.Euler(-58f, 0f, 0f); break;              // palm up and out
                case Gesture.Wave:
                {
                    float s = Mathf.Sin((waveUntil - Time.time) * 5.5f);
                    u = Quaternion.Euler(-70f + s * 12f, 0f, 55f + s * 30f); f = Quaternion.Euler(-25f, 0f, 0f); break;      // sweeping along the road
                }
                default:
                {
                    float s = Mathf.Sin(Time.time * 14f);
                    u = Quaternion.Euler(-150f + s * 10f, 0f, 14f); f = Quaternion.Euler(-95f + s * 15f, 0f, 0f); break;   // fist shaken overhead
                }
            }
            ua.localRotation = Quaternion.Slerp(ua.localRotation, u, lift);
            fa.localRotation = Quaternion.Slerp(fa.localRotation, f, lift);
        }
    }
}
