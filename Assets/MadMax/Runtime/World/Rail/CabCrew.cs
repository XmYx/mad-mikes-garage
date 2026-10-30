using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The driver in a timetabled train's cab (2026-10-30; services only — a free train's cab is empty, the
    /// player's seat). A seated figure in the cab seat (Guild overalls and cap, a raider crew in black with a scarf)
    /// with a head that watches the line ahead, glances at crossings and turns to follow someone near the track, and a
    /// right arm on the throttle that reaches up for the horn cord whenever the horn sounds. A Guild driver waves to the
    /// player standing or driving within <see cref="WaveReach"/> m when the train is slow; raiders only stare.</summary>
    public class CabCrew : MonoBehaviour
    {
        public const float WaveReach = 25f, WaveEvery = 45f, ShowReach = 140f;
        RailCar car;
        bool raider;
        Transform body, head, arm;
        Renderer[] rends;
        float headYaw, waveUntil, waveReady, bob;
        /// <summary>Waves given and the time of the last (tests).</summary>
        public static int Waves;
        public static float LastWaveAt = -100f;
        public bool Shown { get; private set; }
        public bool Waving => Time.time < waveUntil;
        public bool OnHorn => car && car.train && Time.time - car.train.LastHornTime < 1.3f;
        public float HeadYaw => headYaw;

        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { meshes.Clear(); Waves = 0; LastWaveAt = -100f; }

        static Mesh M(int key, RailKit k, string name)
        {
            if (meshes.TryGetValue(key, out var m) && m) return m;
            m = MadMax.Voxel.VoxelMesher.ToMesh(k.mesh, name);
            m.RecalculateBounds();
            meshes[key] = m;
            return m;
        }

        static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 at, string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        public static CabCrew Add(RailCar car, Material mat)
        {
            bool raider = car.def.kind == CarKind.ArmoredLoco;
            int b = raider ? 10 : 0;
            var root = new GameObject("CabCrew").transform;
            root.SetParent(car.transform, false);
            root.localPosition = car.def.seat;
            var c = root.gameObject.AddComponent<CabCrew>();
            c.car = car; c.raider = raider;
            c.body = Part(root, M(b + 1, TrainDesigns.CrewBody(raider), "Rail_CrewBody"), mat, Vector3.zero, "Body");
            c.head = Part(c.body, M(b + 2, TrainDesigns.CrewHead(raider), "Rail_CrewHead"), mat, new Vector3(0f, 0.52f, -0.02f), "Head");
            c.arm = Part(c.body, M(b + 3, TrainDesigns.CrewArm(raider), "Rail_CrewArm"), mat, new Vector3(0.23f, 0.48f, -0.02f), "ArmR");
            c.rends = root.GetComponentsInChildren<Renderer>();
            c.waveReady = Random.Range(0f, 20f);
            c.Shown = true;
            c.Show(false);
            return c;
        }

        void Show(bool on)
        {
            if (Shown == on && rends != null) return;
            Shown = on;
            foreach (var r in rends) if (r) r.enabled = on;
        }

        void LateUpdate()
        {
            var t = car ? car.train : null;
            var g = MadMax.Game.WastelandGame.Instance;
            bool want = t && t.service != null && !car.Broken && MadMax.Building.PieceBits.Near(transform.position, ShowReach);
            Show(want);
            if (!want) return;
            float dt = Time.deltaTime;
            float sp = Mathf.Abs(t.speed);
            // the body rides the cab: a little sway and bob with the speed
            bob += dt * (4f + sp * 0.6f);
            body.localPosition = new Vector3(Mathf.Sin(bob * 0.5f) * 0.006f * sp, Mathf.Abs(Mathf.Sin(bob)) * 0.004f * Mathf.Min(sp, 10f), 0f);
            // the head: the line ahead, someone near the track, a sway
            Vector3 me = g && g.Player ? (g.Current ? g.Current.transform.position : g.Player.transform.position) : Vector3.one * 1e6f;
            var local = transform.InverseTransformPoint(me + Vector3.up * 1.3f);
            float wantYaw = Mathf.Sin(Time.time * 0.23f + car.variant) * 14f;
            bool near = local.sqrMagnitude < 32f * 32f;
            if (near)
            {
                float y = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(y) < 115f) wantYaw = Mathf.Clamp(y, -95f, 95f);
            }
            headYaw = Mathf.MoveTowardsAngle(headYaw, wantYaw, dt * 120f);
            head.localRotation = Quaternion.Euler(near ? -4f : 2f, headYaw, 0f);
            // wave to the player when slow and close (Guild drivers)
            if (!raider && near && local.magnitude < WaveReach && sp < 9f && Time.time >= waveReady && !t.service.Hostile && Mathf.Abs(headYaw) < 100f)
            {
                waveUntil = Time.time + 2.6f; waveReady = Time.time + WaveEvery;
                Waves++; LastWaveAt = Time.time;
            }
            Quaternion armWant;
            if (OnHorn)
            {
                float tug = Mathf.Sin((Time.time - t.LastHornTime) * 14f) * 6f;
                armWant = Quaternion.Euler(-168f + tug, 0f, -8f);
            }
            else if (Waving)
            {
                float w = Mathf.Sin((waveUntil - Time.time) * 9f);
                armWant = Quaternion.Euler(-160f, 0f, 22f + w * 22f);
            }
            else armWant = Quaternion.Euler(-72f + (t.service.Braking ? -6f : 0f), 0f, 4f);
            arm.localRotation = Quaternion.Slerp(arm.localRotation, armWant, 1f - Mathf.Exp(-dt * 10f));
        }
    }
}
