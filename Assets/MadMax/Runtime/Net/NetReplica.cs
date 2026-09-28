using UnityEngine;

namespace MadMax.Net
{
    /// <summary>Snapshot interpolation for a body this peer does not simulate (remote vehicles, loose parts):
    /// keeps a short state buffer, renders ~120 ms in the past, extrapolates briefly on packet loss.
    /// While active the rigidbody is kinematic and moved here.</summary>
    public class NetReplica : MonoBehaviour
    {
        public struct State { public float time; public Vector3 pos, vel; public Quaternion rot; public float steer, throttle; }

        const int Size = 16;
        readonly State[] buffer = new State[Size];
        int count, head;
        public bool active;
        public float Steer { get; private set; }
        public float Throttle { get; private set; }
        public Vector3 Velocity { get; private set; }
        Rigidbody rb;

        void Awake() { rb = GetComponent<Rigidbody>(); }

        public void Push(State s)
        {
            if (count > 0 && s.time <= buffer[(head - 1 + Size) % Size].time) return;   // out of order
            buffer[head] = s;
            head = (head + 1) % Size;
            count = Mathf.Min(count + 1, Size);
        }

        public bool Latest(out State s)
        {
            s = default;
            if (count == 0) return false;
            s = buffer[(head - 1 + Size) % Size];
            return true;
        }

        public void Clear() { count = 0; }

        public void SetActive(bool on)
        {
            active = on;
            if (!rb) rb = GetComponent<Rigidbody>();
            if (on) { if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; } }
            else
            {
                if (rb)
                {
                    rb.isKinematic = false;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    if (Latest(out var s)) rb.linearVelocity = s.vel;
                }
                Clear();
            }
        }

        void Update()
        {
            if (!active || count == 0 || NetSession.Instance == null) return;
            float t = NetSession.Instance.RenderTime;
            // find the pair around t
            State a = default, b = default; bool found = false;
            for (int i = 0; i < count - 1; i++)
            {
                var s0 = buffer[(head - count + i + Size * 2) % Size];
                var s1 = buffer[(head - count + i + 1 + Size * 2) % Size];
                if (s0.time <= t && s1.time >= t) { a = s0; b = s1; found = true; break; }
            }
            Vector3 p; Quaternion r;
            if (found)
            {
                float f = Mathf.InverseLerp(a.time, b.time, t);
                p = Vector3.LerpUnclamped(a.pos, b.pos, f);
                r = Quaternion.Slerp(a.rot, b.rot, f);
                Steer = Mathf.Lerp(a.steer, b.steer, f); Throttle = b.throttle; Velocity = Vector3.Lerp(a.vel, b.vel, f);
            }
            else
            {
                var last = buffer[(head - 1 + Size) % Size];
                if (t < buffer[(head - count + Size) % Size].time) last = buffer[(head - count + Size) % Size];
                float ex = Mathf.Clamp(t - last.time, 0f, 0.25f);                 // short extrapolation only
                p = last.pos + last.vel * ex; r = last.rot;
                Steer = last.steer; Throttle = last.throttle; Velocity = last.vel;
            }
            if (rb) { rb.position = p; rb.rotation = r; }
            transform.SetPositionAndRotation(p, r);
        }
    }
}
