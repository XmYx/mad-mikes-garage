using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A wall light switch ([E] flips it, click, the lever shows up = on / down = off). Its circuit, re-read
    /// every second (first rule that finds lights wins):
    /// 1. <see cref="group"/> set (a world building's or a vehicle cabin's own switch): every switchable light in that object.
    /// 2. Cabled: the lights reachable from the switch along power cables without passing a power source (generator,
    ///    battery, panel...) or another light switch. A cabled switch outranks the rules below for the lights it reaches.
    /// 3. On a vehicle: every switchable light on that vehicle (cabin lights, lamps built aboard).
    /// 4. Otherwise its room: lights within <see cref="RoomReach"/> m on the same storey (±<see cref="StoreyReach"/> m)
    ///    with nothing solid between switch and lamp (walls make rooms).
    /// Outdoor lamps (street lamps, masts) and floodlights only answer to rule 2; oil lamps never (lit by hand). A switched
    /// indoor light burns whenever the switch is on and its supply is live. State saves as the piece's state.</summary>
    public class LightSwitch : MonoBehaviour, IPlaceState, IInteractable
    {
        public const float RoomReach = 7f, StoreyReach = 2.4f;
        public static readonly List<LightSwitch> All = new List<LightSwitch>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); adjFrame = -1; adj.Clear(); byId.Clear(); leverMesh = null; }

        public bool on = true;
        /// <summary>World fixtures: the building / vehicle whose lights this switch runs (null: rules 2-4).</summary>
        public Transform group;
        /// <summary>Shown instead of the usual prompt tail when the circuit has no power (a ruin's dead wiring).</summary>
        public string deadNote;
        /// <summary>Someone flipped it ([E], automation): building residents note the player's choice.</summary>
        public System.Action<LightSwitch> flipped;

        readonly List<PoweredLight> circuit = new List<PoweredLight>();
        Transform lever;
        UtilityNode node;
        float poll;
        bool started;

        /// <summary>Lights this switch ran at its last check.</summary>
        public IReadOnlyList<PoweredLight> Circuit => circuit;
        /// <summary>The circuit came from cables (rule 2).</summary>
        public bool Wired { get; private set; }

        void Awake() { node = GetComponent<UtilityNode>(); lever = transform.Find("Lever"); poll = Random.Range(0f, 1f); }
        void OnEnable()
        {
            All.Add(this);
            if (!GetComponent<Placeable>()) MadMax.Vehicles.PartFunctions.Interactables.Add(this);    // world fixtures: on-foot [E]
        }
        void OnDisable() { All.Remove(this); MadMax.Vehicles.PartFunctions.Interactables.Remove(this); }
        void Start() { started = true; Resolve(); ShowLever(); }

        void Update()
        {
            if ((poll -= Time.deltaTime) > 0f) return;
            poll = 1f;
            Resolve();
        }

        // ------------------------------------------------------------------ use

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string s = "LIGHT SWITCH: " + (on ? "ON  [E] SWITCH OFF" : "OFF  [E] SWITCH ON");
            if (circuit.Count == 0) return s + "  (NO LIGHTS)";
            bool live = false;
            foreach (var l in circuit) if (l && l.HasPower) { live = true; break; }
            return s + "  " + circuit.Count + (circuit.Count == 1 ? " LIGHT" : " LIGHTS") + (live ? "" : "  " + (deadNote ?? "NO POWER"));
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) Flip(); }

        /// <summary>Flip the switch (click, lever, the circuit follows at once).</summary>
        public void Flip() => Set(!on, true);

        public void Set(bool value, bool byHand)
        {
            bool changed = value != on;
            on = value;
            if (changed) MadMax.Audio.Sfx.Play("click", transform.position, 0.6f, on ? 1.25f : 1.05f, 12f);
            ShowLever();
            Resolve();
            GetComponent<Placeable>()?.Dirty();
            if (byHand) flipped?.Invoke(this);
        }

        void ShowLever() { if (lever) lever.localRotation = Quaternion.Euler(on ? 35f : -35f, 0f, 0f); }

        public string SaveState() => on ? "1" : "0";
        public void LoadState(string s) { on = s != "0"; ShowLever(); if (started) Resolve(); }

        // ------------------------------------------------------------------ circuit

        /// <summary>Re-read the circuit and claim its lights (also called on every flip).</summary>
        public void Resolve()
        {
            circuit.Clear();
            Wired = false;
            if (group)
            {
                foreach (var l in group.GetComponentsInChildren<PoweredLight>()) if (l.Switchable && !l.outdoor) Take(l, 1);
                return;
            }
            if (node && Trace() > 0) { Wired = true; return; }
            var chassis = GetComponentInParent<MadMax.Vehicles.VehicleChassis>();
            if (chassis)
            {
                foreach (var l in chassis.GetComponentsInChildren<PoweredLight>()) if (l.Switchable && !l.outdoor) Take(l, 1);
                return;
            }
            Room();
        }

        void Take(PoweredLight l, int rank) { if (l.Claim(this, rank)) circuit.Add(l); }

        // undirected cable adjacency, rebuilt at most once per frame for all switches
        static int adjFrame = -1;
        static readonly Dictionary<uint, List<UtilityNode>> adj = new Dictionary<uint, List<UtilityNode>>();
        static readonly Dictionary<uint, UtilityNode> byId = new Dictionary<uint, UtilityNode>();
        static readonly Queue<UtilityNode> queue = new Queue<UtilityNode>();
        static readonly HashSet<UtilityNode> seen = new HashSet<UtilityNode>();

        static void BuildAdjacency()
        {
            if (adjFrame == Time.frameCount) return;
            adjFrame = Time.frameCount;
            foreach (var l in adj.Values) l.Clear();
            byId.Clear();
            foreach (var n in UtilityNode.All) if (n && n.Piece) byId[n.Id] = n;
            foreach (var n in UtilityNode.All)
            {
                if (!n || !n.Piece) continue;
                foreach (var (id, kind) in n.links)
                {
                    if ((kind & UtilityKind.Power) == 0 || !byId.TryGetValue(id, out var m)) continue;
                    Add(n.Id, m); Add(id, n);
                }
            }
        }

        static void Add(uint id, UtilityNode n)
        {
            if (!adj.TryGetValue(id, out var l)) adj[id] = l = new List<UtilityNode>();
            l.Add(n);
        }

        int Trace()
        {
            if (!node.Piece || node.links.Count == 0 && !HasIncoming()) return 0;
            BuildAdjacency();
            seen.Clear(); queue.Clear();
            seen.Add(node); queue.Enqueue(node);
            int found = 0;
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (!adj.TryGetValue(n.Id, out var next)) continue;
                foreach (var m in next)
                {
                    if (!m || !seen.Add(m)) continue;
                    if (m.IsSource || m.GetComponent<LightSwitch>()) continue;              // the supply side, or another switch's circuit
                    var l = m.GetComponent<PoweredLight>();
                    if (l && l.Switchable) { found++; Take(l, 2); }
                    queue.Enqueue(m);
                }
            }
            return found;
        }

        bool HasIncoming()
        {
            uint me = node.Id;
            foreach (var n in UtilityNode.All) if (n && n != node) foreach (var (id, _) in n.links) if (id == me) return true;
            return false;
        }

        static readonly RaycastHit[] hits = new RaycastHit[16];

        void Room()
        {
            var from = transform.position + transform.up * 0.12f;
            foreach (var l in PoweredLight.All)
            {
                if (!l || !l.Switchable || l.outdoor || l.external) continue;
                var at = l.transform.position;
                var lightT = l.GetComponentInChildren<Light>();
                var to = lightT ? lightT.transform.position : at;
                if (Mathf.Abs(to.y - from.y) > StoreyReach + 1.2f || Mathf.Abs(at.y - transform.position.y) > StoreyReach) continue;
                var d = to - from; d.y = 0f;
                if (d.sqrMagnitude > RoomReach * RoomReach) continue;
                if (l.GetComponentInParent<MadMax.Vehicles.VehicleChassis>()) continue;
                if (!Clear(from, to, l.transform)) continue;
                Take(l, 1);
            }
        }

        /// <summary>Nothing solid but the two pieces (and people, loose bodies) between switch and lamp.</summary>
        bool Clear(Vector3 a, Vector3 b, Transform lampRoot)
        {
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(a, dir / len, hits, len - 0.05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (!c || c.transform.IsChildOf(transform) || c.transform.IsChildOf(lampRoot)) continue;
                if (c is CharacterController) continue;
                var rb = c.attachedRigidbody;
                if (rb && !rb.isKinematic) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ visuals

        static Mesh leverMesh;

        /// <summary>The toggle lever (4 cm voxels) on a switch plate, pivoting at the plate's face.</summary>
        public static void FitLever(GameObject go, Material mat)
        {
            if (!leverMesh)
            {
                var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
                g.Box(0, 0, 0, 0, 2, 0, Pal.Ramp(Pal.Cream, 3));
                g.Set(0, 2, 0, Pal.Solid(Pal.Cream[4]));
                leverMesh = VoxelMesher.Build(g, "LightSwitchLever", 0.04f);
            }
            var lv = new GameObject("Lever", typeof(MeshFilter), typeof(MeshRenderer));
            lv.transform.SetParent(go.transform, false);
            lv.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            bool hd = MadMax.Rendering.HDBits.On;
            lv.GetComponent<MeshFilter>().sharedMesh = hd ? MadMax.Rendering.HDBits.Lever() : leverMesh;
            var r = lv.GetComponent<MeshRenderer>();
            r.sharedMaterial = hd ? MadMax.Rendering.HDShapes.Solid : mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
