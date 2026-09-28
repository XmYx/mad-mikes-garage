using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Net
{
    /// <summary>Another player's body: HumanRig + HumanAnimator driven by interpolated network state.
    /// Handles walking (world space), walking inside a vehicle interior (vehicle-local) and sitting in a seat.</summary>
    public class RemoteAvatar : MonoBehaviour
    {
        public struct State
        {
            public float time;
            public Vector3 pos;          // world, or vehicle-local when inside
            public float yaw, speed, vertical, lookPitch, swing;
            public bool grounded, carrying, inside, seated;
            public ushort vehicle;       // seat or interior vehicle (0 = none)
            public byte tool;
        }

        public ushort playerId;
        public string playerName;
        public HumanRig Rig { get; private set; }
        public Transform CarryPoint { get; private set; }
        HumanAnimator anim;
        readonly State[] buf = new State[16];
        int count, head;
        HandTool toolVisual;
        byte toolIndex = 255;
        Material material;

        public static RemoteAvatar Create(ushort id, string name, Appearance look, List<string> outfit, Material mat)
        {
            var go = new GameObject("Player " + name);
            var a = go.AddComponent<RemoteAvatar>();
            a.playerId = id; a.playerName = name; a.material = mat;
            a.Rig = go.AddComponent<HumanRig>();
            a.Rig.material = mat;
            a.SetLook(look, outfit);
            a.CarryPoint = new GameObject("Carry").transform;
            a.CarryPoint.SetParent(go.transform, false);
            a.CarryPoint.localPosition = new Vector3(0, 1.05f, 0.5f);
            // a collider so vehicles and tools can hit other players
            var cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f; cap.radius = 0.28f; cap.center = new Vector3(0, 0.9f, 0);
            return a;
        }

        public void SetLook(Appearance look, List<string> outfit)
        {
            if (look != null) Rig.appearance = look.Clone();
            Rig.outfit.Clear();
            if (outfit != null) Rig.outfit.AddRange(outfit);
            if (toolVisual) toolVisual.transform.SetParent(transform, false);
            Rig.Rebuild();
            anim = new HumanAnimator(Rig);
            if (toolVisual) AttachTool();
        }

        public void Push(State s)
        {
            if (count > 0 && s.time <= buf[(head - 1 + 16) % 16].time) return;
            buf[head] = s; head = (head + 1) % 16; count = Mathf.Min(count + 1, 16);
        }

        void AttachTool()
        {
            toolVisual.transform.SetParent(Rig.RightHand, false);
            toolVisual.transform.localPosition = new Vector3(0f, -0.055f, 0.01f);
            toolVisual.transform.localRotation = Quaternion.identity;
            toolVisual.enabled = false;                                   // visual only: remote swings never strike here
        }

        void Update()
        {
            if (count == 0 || !NetSession.Instance) return;
            float t = NetSession.Instance.RenderTime;
            State a = buf[(head - 1 + 16) % 16], b = a; float f = 0f;
            for (int i = 0; i < count - 1; i++)
            {
                var s0 = buf[(head - count + i + 32) % 16]; var s1 = buf[(head - count + i + 1 + 32) % 16];
                if (s0.time <= t && s1.time >= t) { a = s0; b = s1; f = Mathf.InverseLerp(s0.time, s1.time, t); break; }
            }
            var s = b;
            var veh = NetSession.Instance.Vehicle(s.vehicle);
            // placement: parent to the vehicle when seated / inside so interpolation is vehicle-relative
            var parent = (s.seated || s.inside) && veh ? veh.transform : null;
            if (transform.parent != parent) transform.SetParent(parent, false);
            var pos = a.vehicle == b.vehicle && a.inside == b.inside ? Vector3.Lerp(a.pos, b.pos, f) : b.pos;
            float yaw = Mathf.LerpAngle(a.yaw, b.yaw, f);
            if (parent) { transform.localPosition = pos; transform.localRotation = Quaternion.Euler(0, yaw, 0); }
            else transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));

            if (toolIndex != s.tool)
            {
                toolIndex = s.tool;
                if (toolVisual) Destroy(toolVisual.gameObject);
                toolVisual = s.tool < ToolLibrary.Order.Length ? ToolLibrary.Create(ToolLibrary.Order[s.tool], material) : null;
                if (toolVisual) AttachTool();
            }
            anim.Tick(Time.deltaTime, new HumanAnimator.State
            {
                speed = Mathf.Lerp(a.speed, b.speed, f), grounded = s.grounded, verticalSpeed = s.vertical, lookPitch = s.lookPitch,
                sitting = s.seated, steer = veh && s.seated ? veh.steerInput : 0f, carrying = s.carrying,
                tool = toolVisual ? (s.swing > 0f ? toolVisual.Pose(s.swing) : toolVisual.IdlePose) : null,
                twoHanded = toolVisual && toolVisual.TwoHanded
            });
        }
    }
}
