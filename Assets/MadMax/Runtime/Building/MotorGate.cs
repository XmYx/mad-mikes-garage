using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Motorised sliding gate (fits a gate frame): the leaf runs 4.1 m sideways on its rail. On power it opens
    /// by itself for the owner's vehicles (the car the owner drives, fleet cars with someone at the wheel) coming within
    /// <see cref="Reach"/> m and closes again a few seconds after they have gone (never onto a vehicle or person in the
    /// opening); [E] opens and closes it (cranked slowly without power), [T] switches the automatic off and on (owner).
    /// Anyone else's vehicle finds it shut: the leaf is solid and takes rams (<see cref="GateLeaf"/>) and blows like any
    /// piece until it breaks.</summary>
    public class MotorGate : MonoBehaviour, IInteractable, IPlaceState
    {
        public const float Reach = 14f, Travel = 4.08f;
        public Transform leaf;
        public Mesh driveMesh;
        public float watts = 600f;
        public bool open, auto = true;
        float t, scan, holdUntil;
        bool autoOpened;
        UtilityNode node;
        Placeable piece;
        MeshFilter mf;

        public bool Motor => node && node.Powered;
        /// <summary>0 shut .. 1 fully open.</summary>
        public float Openness => t;
        float Seconds => Motor ? 4f : 14f;

        void Awake()
        {
            node = GetComponent<UtilityNode>(); piece = GetComponent<Placeable>(); mf = GetComponent<MeshFilter>();
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (!g.OwnsPiece(piece)) return "LOCKED GATE - BREAK IT DOWN";
            string verb = !Motor ? (open ? "[E] CRANK SHUT (NO POWER)" : "[E] CRANK OPEN (NO POWER)") : open ? "[E] CLOSE" : "[E] OPEN";
            return "GATE: " + verb + "  [T] AUTO " + (auto ? "ON" : "OFF");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!g.OwnsPiece(piece)) { MadMax.Audio.Sfx.Play("lock", transform.position, 0.6f); return; }
            if (secondary) { auto = !auto; g.Toast(auto ? "GATE OPENS FOR YOUR VEHICLES" : "GATE STAYS AS YOU LEAVE IT"); Dirty(); return; }
            Set(!open, false);
        }

        void Set(bool on, bool automatic)
        {
            if (open == on) return;
            open = on; autoOpened = on && automatic;
            MadMax.Audio.Sfx.Play(Motor ? "hydraulic" : "chain", transform.position + transform.up, 0.7f, 1f, 40f);
            Dirty();
        }

        void Dirty() { if (piece) piece.Dirty(); }

        void Update()
        {
            if (mf && driveMesh && mf.sharedMesh != driveMesh) mf.sharedMesh = driveMesh;               // a dye swapped the whole-gate mesh in
            float target = open ? 1f : 0f;
            bool moving = !Mathf.Approximately(t, target);
            if (node) node.demand = moving && Motor ? watts : 0f;
            if (moving)
            {
                t = Mathf.MoveTowards(t, target, Time.deltaTime / Seconds);
                if (leaf) leaf.localPosition = Vector3.right * (Travel * t);                              // the leaf is built shut at the piece's origin
            }
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;                                                             // the host runs the automatic
            if ((scan -= Time.deltaTime) > 0f) return;
            scan = 0.25f;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !auto || !Motor) return;
            if (FriendNear(g)) { holdUntil = Time.time + 3f; if (!open) Set(true, true); }
            else if (open && autoOpened && Time.time > holdUntil && !Obstructed(g)) Set(false, false);
        }

        /// <summary>One of the owner's vehicles near the gate, in front or behind: the car the owner drives, or a fleet
        /// vehicle with a driver (a companion at the wheel).</summary>
        bool FriendNear(MadMax.Game.WastelandGame g)
        {
            bool mine = g.OwnsPiece(piece);
            foreach (var v in g.AllVehicles)
            {
                if (!v) continue;
                bool friend = (v == g.Current && mine) || (mine && v.Occupied && v != g.Current && Contains(g.Fleet, v));
                if (!friend) continue;
                var lp = transform.InverseTransformPoint(v.transform.position);
                if (Mathf.Abs(lp.x) < 7f && Mathf.Abs(lp.z) < Reach && Mathf.Abs(lp.y) < 4f) return true;
            }
            return false;
        }

        static bool Contains(System.Collections.Generic.IReadOnlyList<VehicleDriver> list, VehicleDriver v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == v) return true;
            return false;
        }

        /// <summary>A vehicle or a person in the opening: the leaf waits.</summary>
        bool Obstructed(MadMax.Game.WastelandGame g)
        {
            foreach (var v in g.AllVehicles)
                if (v && v.Body && InOpening(v.Body.ClosestPointOnBounds(transform.position + transform.up))) return true;
            if (g.Player && g.Player.gameObject.activeSelf && !g.Current && InOpening(g.Player.transform.position + Vector3.up * 0.5f)) return true;
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.Alive && InOpening(n.transform.position + Vector3.up * 0.5f)) return true;
            return false;
        }

        bool InOpening(Vector3 world)
        {
            var lp = transform.InverseTransformPoint(world);
            return Mathf.Abs(lp.x) < 2.2f && Mathf.Abs(lp.z) < 1.4f && lp.y > -0.5f && lp.y < 3f;
        }

        public string SaveState() => (open ? "1" : "0") + (auto ? "1" : "0");

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            open = s[0] == '1';
            if (s.Length > 1) auto = s[1] == '1';
            autoOpened = open && auto;                                                                  // back under the automatic after a load
        }
    }
}
