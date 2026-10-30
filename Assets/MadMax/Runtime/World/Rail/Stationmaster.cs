using MadMax.Game;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The stationmaster (2026-10-30): at every passenger stop a Guild service makes, a stationmaster stands on
    /// the platform beside the loco (<see cref="TrainDirector.StopEvent"/>), turned to the train. With
    /// <see cref="WhistleAt"/> s of the dwell left they blow the whistle and hold the flag up (by night a lamp, swung
    /// side to side) until the train answers with its horn and pulls out; then the arm comes down. The arm is posed
    /// after the person's own animation (<see cref="LateUpdate"/>).</summary>
    public class Stationmaster : MonoBehaviour
    {
        public const float WhistleAt = 9f;
        public TrainService service;
        MadMax.Npc.Npc npc;
        Transform flag;
        bool whistled, lamp;
        float up;
        /// <summary>Whistles blown and the time of the last (tests).</summary>
        public static int Whistles;
        public static float LastWhistleAt = -100f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Whistles = 0; LastWhistleAt = -100f; }
        static Mesh flagMesh;

        /// <summary>The flag (or lamp) is raised (0..1).</summary>
        public float Raised => up;

        public static Stationmaster Attach(MadMax.Npc.Npc n, TrainService s, Material mat, bool night)
        {
            var m = n.gameObject.AddComponent<Stationmaster>();
            m.npc = n; m.service = s; m.lamp = night;
            if (!night && n.Rig)
            {
                if (!flagMesh) { flagMesh = MadMax.Voxel.VoxelMesher.ToMesh(TrainDesigns.Flag().mesh, "Rail_Flag"); flagMesh.RecalculateBounds(); }
                var go = new GameObject("Flag", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(n.Rig.RightHand, false);
                go.transform.localPosition = new Vector3(0f, -0.05f, 0.01f);
                go.GetComponent<MeshFilter>().sharedMesh = flagMesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                m.flag = go.transform;
            }
            return m;
        }

        void LateUpdate()
        {
            if (!npc || !npc.Alive || service == null) return;
            float dt = Time.deltaTime;
            bool standing = service.AtStop >= 0;
            float left = service.DwellLeft;
            bool raise = standing && left < WhistleAt && !npc.Hostile && npc.mode != MadMax.Npc.Npc.Mode.Flee;
            if (raise && !whistled)
            {
                whistled = true; Whistles++; LastWhistleAt = Time.time;
                MadMax.Audio.Sfx.Play("rail_whistle", transform.position + Vector3.up * 1.6f, 0.9f, Random.Range(0.97f, 1.03f), 90f);
            }
            if (!standing) whistled = false;
            up = Mathf.MoveTowards(up, raise ? 1f : 0f, dt * 2.5f);
            if (flag) flag.localScale = Vector3.one * (0.6f + 0.4f * up);
            if (up <= 0f || !npc.Rig) return;
            var rig = npc.Rig;
            float swing = lamp ? Mathf.Sin(Time.time * 3.2f) * 28f * up : Mathf.Sin(Time.time * 1.3f) * 4f;
            var ua = rig.Bone(MadMax.Game.BodyPart.UpperArmR);
            var fa = rig.Bone(MadMax.Game.BodyPart.ForearmR);
            ua.localRotation = Quaternion.Slerp(ua.localRotation, Quaternion.Euler(-150f, 0f, 18f + swing), up);
            fa.localRotation = Quaternion.Slerp(fa.localRotation, Quaternion.Euler(-12f, 0f, 0f), up);
        }
    }
}
