using MadMax.Items;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The seed-oil press at work (2026-10-27): while a pressing runs the bar turns and screws the plate down into
    /// the barrel of seeds (four turns over the job, the frame creaking every quarter turn), golden oil drips from the spout
    /// into a tin cup once the seeds give, and at the end the screw winds back up. The screw, bar and plate are this
    /// component's in the voxel look; with the HD model on they stay the model's and only the oil and sounds play.</summary>
    public class OilPressWork : MonoBehaviour
    {
        const float VS = VoxelMesher.DefaultSize;
        const float RestY = 9f * VS, Travel = 4f * VS, Turns = 4f;
        static readonly Vector3 Spout = new Vector3(0f, 1.6f * VS, 6.6f * VS);
        static readonly Vector3 CupAt = new Vector3(0f, 0f, 6.6f * VS);
        static readonly Color Oil = new Color(0.86f, 0.66f, 0.18f);

        CraftingStation station;
        Transform screw;
        Droplets drip;
        bool built, hdLook;
        float press, angle, lastQuarter;
        CraftingStation.Job job;

        /// <summary>How far the plate is down (0 rest .. 1 full press), bar angle (degrees), oil drops landed (tests).</summary>
        public float Press => press;
        public float BarAngle => angle;
        public int Drops => drip ? drip.Landed : 0;
        public bool Dripping => drip && drip.emitting;
        public bool Working => station && station.Busy && station.Powered;

        void Start() => station = GetComponent<CraftingStation>();

        void Build()
        {
            built = true;
            hdLook = PieceBits.HD(gameObject);
            var mr = GetComponent<MeshRenderer>();
            var mat = mr ? mr.sharedMaterial : null;
            if (!hdLook)
                screw = PieceBits.Shape(transform, "Screw", new Vector3(0f, RestY, 0f), "press_screw", false, g =>
                {
                    g.Mat((byte)ResourceType.Iron);
                    g.Box(-3, 0, -3, 3, 0, 3, Pal.Ramp(Pal.Metal, 1, 1732));                                 // pressing plate
                    g.Box(0, 1, 0, 0, 12, 0, p => (p.y & 1) == 0 ? Pal.Chrome[2] : Pal.Metal[2]);         // threaded screw
                    g.Box(-5, 7, 0, 5, 7, 0, Pal.Ramp(Pal.Metal, 2));                                       // bar
                    foreach (int x in new[] { -5, 5 }) g.Set(x, 7, 0, Pal.Solid(Pal.Wood[1]));               // worn grips
                }, null, mat);
            var cup = PieceBits.Shape(transform, "Cup", CupAt, "press_cup", hdLook, g =>
            {
                g.Mat((byte)ResourceType.Iron);
                g.Box(-1, 0, -1, 1, 1, 1, p => p.y == 1 && p.x == 0 && p.z == 0 ? Pal.Ochre[4] : Pal.Metal[2]);
            }, s =>
            {
                s.Tube(Vector3.zero, new Vector3(0f, 0.11f, 0f), 0.055f, 0.06f, Pal.Metal[2], 12, false);
                s.Tube(new Vector3(0f, 0.005f, 0f), new Vector3(0f, 0.085f, 0f), 0.05f, 0.054f, Pal.Ochre[4], 12, true);
            }, mat);
            var o = new GameObject("OilDrip");
            o.transform.SetParent(transform, false);
            o.transform.localPosition = Spout;
            drip = Droplets.Attach(o, 6, (int i, out Vector3 at, out Vector3 v) =>
            {
                at = Random.insideUnitSphere * 0.004f;
                v = new Vector3(0f, -0.1f, 0.03f);
            }, Oil, 0.022f, 0.5f);
            drip.floorY = CupAt.y + 0.1f - Spout.y;
        }

        void Update()
        {
            bool near = PieceBits.Near(transform.position, 45f);
            if (!near) { if (drip) drip.emitting = false; return; }
            if (!built) Build();
            float dt = Time.deltaTime;
            var cur = station ? station.Current : null;
            float target;
            if (Working && cur != null)
            {
                if (cur != job) { job = cur; }
                target = Mathf.Clamp01(cur.progress);
            }
            else target = 0f;
            float before = press;
            press = target > press ? Mathf.MoveTowards(press, target, dt * 0.5f) : Mathf.MoveTowards(press, target, dt * 0.7f);   // winds back up in ~1.5 s
            angle = press * Turns * 360f;
            if (screw)
            {
                screw.localPosition = new Vector3(0f, RestY - Travel * press, 0f);
                screw.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            float quarter = Mathf.Floor(angle / 90f);
            if (quarter != lastQuarter)
            {
                lastQuarter = quarter;
                bool down = press > before;
                MadMax.Audio.Sfx.Play("creak", transform.position + Vector3.up * 1.2f, down ? 0.3f : 0.2f, down ? Random.Range(0.75f, 0.9f) : 1.2f, 14f, 0.15f);
            }
            if (drip) drip.emitting = Working && press > 0.15f;
        }
    }
}
