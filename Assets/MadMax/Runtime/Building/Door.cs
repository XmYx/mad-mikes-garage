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

        public float hingeX = -0.44f;       // local hinge offset (m)
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

        void Update()
        {
            Capture();
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime * 2.5f);
            float s = t * t * (3f - 2f * t);
            var swing = Quaternion.AngleAxis(-95f * s, Vector3.up);
            var hinge = closedRot * new Vector3(hingeX, 0f, 0f);
            transform.localRotation = closedRot * swing;
            transform.localPosition = closedPos + hinge - closedRot * swing * new Vector3(hingeX, 0f, 0f);
        }

        public string SaveState() => (open ? "1" : "0") + (locked ? "1" : "0");
        public void LoadState(string s) { if (s != null && s.Length >= 2) { open = s[0] == '1'; locked = s[1] == '1'; } }
    }
}
