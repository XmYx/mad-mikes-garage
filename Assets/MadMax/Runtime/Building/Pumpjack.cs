using System.Globalization;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Pumpjack: over an oil field (tar-stained ground, WorldGen.OilAt) and on power it nods its walking beam and
    /// fills its tank with crude (up to 0.5 L/s on the richest ground). [E] fills a container from the tank (a siphon source
    /// for any can in hand too). The polished rod hangs from the horse head on its bridle and strokes in and out of the
    /// stuffing box with the beam (a squeak at the top of every stroke, a shine of crude on the rod); a sight glass on the
    /// gearbox shows the crude in the tank. Starting and stopping ease the beam (it winds up and nods to rest level).</summary>
    public class Pumpjack : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly System.Collections.Generic.List<Pumpjack> All = new System.Collections.Generic.List<Pumpjack>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        const float VS = VoxelMesher.DefaultSize;
        static readonly Vector3 Pivot = new Vector3(0f, 22f * VS, 0f);
        /// <summary>Where the carrier bar hangs in beam space (below the horse head, over the wellhead).</summary>
        static readonly Vector3 Carrier = new Vector3(0f, -7.5f * VS, 13f * VS);
        static readonly Vector3 GlassAt = new Vector3(3.6f * VS, 2.5f * VS, -10f * VS);
        const float GlassH = 0.4f;
        static readonly Color32 Crude = Pal.Hex("2a1d12");

        public float stored, capacity = 300f;
        const float Rate = 0.5f;
        float field = -1f, phase, run;
        UtilityNode node;
        Transform beam, rod, column;
        bool built;
        int strokes;

        /// <summary>Beam angle now (degrees, + = head down), how far the rod has risen from rest (m), strokes counted (tests).</summary>
        public float BeamAngle { get; private set; }
        public float RodLift => rod ? rod.localPosition.y - (Pivot.y + Carrier.y) : 0f;
        public int Strokes => strokes;
        /// <summary>Height of the crude column in the sight glass (m, tests).</summary>
        public float GlassLevel => column ? column.localScale.y : 0f;

        void Start()
        {
            node = GetComponent<UtilityNode>();
            beam = transform.Find("Beam");
            var t = MadMax.World.DeformableTerrain.Instance;
            field = t && t.World != null ? t.World.OilAt(transform.position.x, transform.position.z) : 0f;
        }

        /// <summary>Automation: runs without a power net; the field's richness under it (0..1).</summary>
        public bool forcePower;
        public float Field { get => field; set => field = value; }
        public bool Working => (forcePower || node && node.Powered) && field > 0.05f && stored < capacity;

        void Build()
        {
            built = true;
            bool hd = PieceBits.HD(gameObject);
            var mr = GetComponent<MeshRenderer>();
            var mat = mr ? mr.sharedMaterial : null;
            rod = PieceBits.Shape(transform, "Rod", Pivot + Carrier, "pumpjack_rod", hd, g =>
            {
                g.Mat((byte)ResourceType.Iron);
                g.Box(-2, 0, 0, 2, 0, 0, Pal.Ramp(Pal.Metal, 0));                                            // carrier bar
                g.Box(0, 1, 0, 0, 1, 0, Pal.Ramp(Pal.Chrome, 1));                                            // rod clamp
                g.Box(0, -15, 0, 0, -1, 0, p => p.y > -6 && (p.y & 1) == 0 ? Pal.Black[1] : Pal.Chrome[3]);  // polished rod, crude film
                foreach (int x in new[] { -2, 2 }) g.Box(x, 1, 0, x, 8, 0, Pal.Ramp(Pal.Black, 2));          // bridle cables
            }, s =>
            {
                s.Box(new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.05f, 0.07f), Pal.Metal[0], 0.008f);
                s.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.06f, 0.05f, 0.06f), Pal.Chrome[1], 0.006f);
                s.Tube(new Vector3(0f, -1.2f, 0f), new Vector3(0f, 0.12f, 0f), 0.02f, 0.02f, Pal.Chrome[3], 8);
                s.Tube(new Vector3(0f, -0.48f, 0f), new Vector3(0f, -0.08f, 0f), 0.023f, 0.023f, Pal.Black[1], 8);
                foreach (float x in new[] { -0.16f, 0.16f }) s.Tube(new Vector3(x, 0f, 0f), new Vector3(x, 0.66f, 0f), 0.008f, 0.008f, Pal.Black[2], 4);
            }, mat);
            int n = Mathf.RoundToInt(GlassH / VS);
            PieceBits.Shape(transform, "CrudeGlass", GlassAt, "crude_glass" + n, hd, g =>
            {
                g.Mat((byte)ResourceType.Iron);
                g.Box(0, -1, -1, 0, n, -1, Pal.Ramp(Pal.Metal, 1)); g.Box(0, -1, 1, 0, n, 1, Pal.Ramp(Pal.Metal, 1));
                g.Box(0, -1, 0, 0, -1, 0, Pal.Ramp(Pal.Chrome, 2)); g.Box(0, n, 0, 0, n, 0, Pal.Ramp(Pal.Chrome, 2));
            }, s =>
            {
                s.Box(new Vector3(0f, GlassH * 0.5f - 0.04f, -0.07f), new Vector3(0.03f, GlassH + 0.1f, 0.025f), Pal.Metal[1], 0.004f);
                s.Box(new Vector3(0f, GlassH * 0.5f - 0.04f, 0.07f), new Vector3(0.03f, GlassH + 0.1f, 0.025f), Pal.Metal[1], 0.004f);
                s.Tube(new Vector3(0f, -0.05f, 0f), new Vector3(0f, GlassH + 0.02f, 0f), 0.028f, 0.028f, Pal.Glass[3], 10, true);
            }, mat);
            column = PieceBits.Glow(transform, "Crude", GlassAt + new Vector3(0.01f, -0.04f, 0f), new Vector3(0.05f, 0.001f, 0.05f), Crude);
        }

        void Update()
        {
            if (node) node.demand = field > 0.05f && stored < capacity ? 900f : 0f;
            bool work = Working;
            float dt = Time.deltaTime;
            run = Mathf.MoveTowards(run, work ? 1f : 0f, dt * (work ? 0.35f : 0.25f));
            MadMax.Audio.Sfx.Loop(this, "hydraulic", 0.25f * run, 0.6f, 25f);
            if (work) stored = Mathf.Min(capacity, stored + Rate * field * dt);
            if (run > 0f)
            {
                float c0 = Mathf.Cos(phase);
                phase += dt * 1.4f * Mathf.Lerp(0.3f, 1f, run);
                if (c0 > 0f && Mathf.Cos(phase) <= 0f)                                                 // horse head at the bottom: rod down
                    strokes++;
                else if (c0 < 0f && Mathf.Cos(phase) >= 0f && PieceBits.Near(transform.position, 30f))  // rod at the top: the stuffing box squeaks
                    MadMax.Audio.Sfx.Play("creak", transform.TransformPoint(Pivot + Carrier), 0.18f * run, Random.Range(1.5f, 1.8f), 18f, 0.5f);
            }
            BeamAngle = Mathf.Sin(phase) * 16f * Mathf.SmoothStep(0f, 1f, run);
            if (!PieceBits.Near(transform.position, 70f)) return;
            if (!built) Build();
            var rot = Quaternion.Euler(BeamAngle, 0f, 0f);
            if (beam) beam.localRotation = rot;
            if (rod) rod.localPosition = new Vector3(0f, Pivot.y + (rot * Carrier).y, Carrier.z);
            if (column)
            {
                float h = Mathf.Max(0.001f, GlassH * stored / capacity);
                column.localScale = new Vector3(0.05f, h, 0.05f);
                column.localPosition = GlassAt + new Vector3(0.01f, h * 0.5f - 0.04f, 0f);
            }
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (field >= 0f && field <= 0.05f) return "PUMPJACK: NO OIL UNDER HERE";
            return "[E] FILL A CAN WITH CRUDE " + Mathf.FloorToInt(stored) + "/" + capacity + " L" + (stored >= capacity ? "  TANK FULL, STOPPED" : field > 0.6f ? "  THE FIELD RUNS STRONG" : field > 0.25f ? "  THE FIELD RUNS STEADY" : "  THE FIELD RUNS THIN") + (node && !node.Powered ? "  NO POWER" : "");
        }

        /// <summary>Crude leaves the tank only into a container (the siphon choice opens with the best one in the pack).</summary>
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            if (stored < 1f) { g.Toast("THE TANK IS EMPTY"); return; }
            g.UseCanAt(null, true, FluidFamily.Fuel);
        }

        public string SaveState() => stored.ToString("0.#", CultureInfo.InvariantCulture);
        public void LoadState(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out stored);
    }
}
