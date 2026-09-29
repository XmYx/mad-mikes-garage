using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Roll-up leaf (garage door, window shutter): rolls up into the drum at <see cref="top"/>. A garage door
    /// with a <see cref="UtilityNode"/> runs on its motor when powered, else it is cranked up by hand (slowly).
    /// Lockable by its builder like a <see cref="Door"/>.</summary>
    public class RollerDoor : MonoBehaviour, IPlaceState, IInteractable
    {
        public float top = 2.12f;          // local height of the drum (the leaf's top edge stays there)
        public float watts;                // motor draw while moving (0 = hand operated)
        public bool open, locked;
        float t;
        Vector3 closedPos; Vector3 closedScale; bool init;
        UtilityNode node;

        bool Motor => watts > 0f && node && node.Powered;
        float Seconds => watts <= 0f ? 1.2f : Motor ? 3.5f : 11f;

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            bool mine = g.OwnsPiece(GetComponent<Placeable>());
            if (locked) return mine ? "[T] UNLOCK" : "LOCKED - BREAK IT DOWN";
            string verb = watts > 0f && !Motor ? (open ? "[E] CRANK DOWN" : "[E] CRANK UP (NO POWER)") : open ? "[E] CLOSE" : "[E] OPEN";
            return verb + (mine ? "  [T] LOCK" : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                if (!g.OwnsPiece(GetComponent<Placeable>())) return;
                locked = !locked; if (locked) open = false;
                g.Toast(locked ? "LOCKED" : "UNLOCKED"); GetComponent<Placeable>()?.Dirty();
                return;
            }
            Toggle();
        }

        public void Toggle()
        {
            if (locked) { MadMax.Audio.Sfx.Play("lock", transform.position, 0.6f); return; }
            open = !open;
            MadMax.Audio.Sfx.Play(watts > 0f ? (Motor ? "hydraulic" : "chain") : "door_open", transform.position + transform.up * top, 0.7f);
            GetComponent<Placeable>()?.Dirty();
        }

        void Start() { node = GetComponent<UtilityNode>(); Capture(); }

        void Capture()
        {
            if (init) return;
            init = true;
            closedPos = transform.localPosition; closedScale = transform.localScale;
        }

        void Update()
        {
            Capture();
            float target = open ? 1f : 0f;
            bool moving = !Mathf.Approximately(t, target);
            if (node) node.demand = moving && watts > 0f ? watts : 0f;
            if (!moving) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Seconds);
            // roll up: squash towards the drum, keeping the top edge where it is
            float s = 1f - 0.9f * t;
            transform.localScale = new Vector3(closedScale.x, closedScale.y * s, closedScale.z);
            transform.localPosition = closedPos + transform.localRotation * Vector3.up * (top * (1f - s));
        }

        public string SaveState() => (open ? "1" : "0") + (locked ? "1" : "0");
        public void LoadState(string s) { if (s != null && s.Length >= 2) { open = s[0] == '1'; locked = s[1] == '1'; } }
    }
}
