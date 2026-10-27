using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Hinged door or gate: swings 90° about its left edge. Lockable by its builder; locked doors must be broken.</summary>
    public class Door : MonoBehaviour, IPlaceState, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g)
        {
            bool mine = g.OwnsPiece(GetComponent<Placeable>());
            if (locked) return mine ? "[T] UNLOCK" : "LOCKED - BREAK IT DOWN";
            return (open ? "[E] CLOSE" : "[E] OPEN") + (mine ? "  [T] LOCK" : "");
        }
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { if (g.OwnsPiece(GetComponent<Placeable>())) { SetLocked(!locked); g.Toast(locked ? "LOCKED" : "UNLOCKED"); } return; }
            Toggle();
        }

        public float hingeX = -0.52f;       // local hinge offset (m): door leaf 1.04 m wide
        public bool open, locked;
        Vector3 closedPos; Quaternion closedRot; bool init;
        float t;

        void Start() { Capture(); }

        void Capture()
        {
            if (init) return;
            init = true;
            closedPos = transform.localPosition; closedRot = transform.localRotation;
        }

        public void ClosedWorld(out Vector3 pos, out Quaternion rot)
        {
            Capture();
            var parent = transform.parent;
            pos = parent ? parent.TransformPoint(closedPos) : closedPos;
            rot = parent ? parent.rotation * closedRot : closedRot;
        }

        public void Toggle() { if (locked) { MadMax.Audio.Sfx.Play("lock", transform.position, 0.6f); return; } open = !open; MadMax.Audio.Sfx.Play(open ? "door_open" : "door_close", transform.position, 0.7f); GetComponent<Placeable>()?.Dirty(); }
        public void SetLocked(bool l) { locked = l; if (l) open = false; GetComponent<Placeable>()?.Dirty(); }

        // a knock on the leaf (crowbar work, see DoorPry): degrees of extra swing on a damped spring, + = further open
        float jolt, joltVel, burst;
        /// <summary>The extra swing the spring holds now (degrees; tests).</summary>
        public float JoltAngle => jolt;
        /// <summary>The swing furthest from rest during the last frame (signed degrees; tests at any frame rate).</summary>
        public float JoltPeak { get; private set; }
        const float SpringStep = 1f / 120f;
        /// <summary>Kick the leaf by <paramref name="degPerSecond"/> (+ = swinging open): it rattles against the frame and settles.</summary>
        public void Jolt(float degPerSecond) => joltVel += degPerSecond;
        /// <summary>The lock gave: unlocked, the leaf flies open past its stop and settles back.</summary>
        public void BurstOpen()
        {
            locked = false; open = true; burst = 1f;
            joltVel += 240f;
            GetComponent<Placeable>()?.Dirty();
        }

        void Update()
        {
            Capture();
            float target = open ? 1f : 0f;
            bool springing = Mathf.Abs(jolt) > 0.05f || Mathf.Abs(joltVel) > 0.5f;
            if (Mathf.Approximately(t, target) && !springing) { JoltPeak = 0f; return; }
            float dt = Time.deltaTime;
            t = Mathf.MoveTowards(t, target, dt * (burst > 0f ? 7f : 2.5f));
            if (Mathf.Approximately(t, target)) burst = 0f;
            // a stiff, damped hinge, stepped finely: in one long frame (low fps) the spring would blow up
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / SpringStep), 1, 40);
            float h = dt / n, peak = 0f;
            for (int i = 0; i < n; i++)
            {
                joltVel += (-90f * jolt - 7f * joltVel) * h;
                jolt += joltVel * h;
                if (t <= 0f && jolt < 0f) { jolt = 0f; joltVel = Mathf.Abs(joltVel) * 0.4f; }      // shut: the frame stops it
                if (Mathf.Abs(jolt) > Mathf.Abs(peak)) peak = jolt;
            }
            JoltPeak = peak;
            if (!springing && Mathf.Abs(jolt) < 0.05f) { jolt = 0f; joltVel = 0f; }
            float s = t * t * (3f - 2f * t);
            var swing = Quaternion.AngleAxis(-95f * s - jolt, Vector3.up);
            var hinge = closedRot * new Vector3(hingeX, 0f, 0f);
            transform.localRotation = closedRot * swing;
            transform.localPosition = closedPos + hinge - closedRot * swing * new Vector3(hingeX, 0f, 0f);
        }

        public string SaveState() => (open ? "1" : "0") + (locked ? "1" : "0");
        public void LoadState(string s) { if (s != null && s.Length >= 2) { open = s[0] == '1'; locked = s[1] == '1'; } }
    }
}
