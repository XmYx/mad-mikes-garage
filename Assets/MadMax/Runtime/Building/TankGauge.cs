using MadMax.Rendering;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>How full a water store is, seen from the ground (2026-10-26). The water tower carries a float gauge: a
    /// graduated board on the tank side with a red target that rides up and down with the water (the float inside
    /// settles slowly, overshooting a little); a rain barrel a gauge glass whose blue column stands as high as the water.
    /// Full with water still coming in, the overflow runs (a stream off the tower's overflow pipe, over the barrel's lip);
    /// a staved tank more than a third full weeps drips from its bottom ring. Reads its <see cref="UtilityNode"/>'s share of
    /// the network (every node holds the same fraction).</summary>
    public class TankGauge : MonoBehaviour
    {
        public enum Style { Board, Glass }
        public Style style;
        /// <summary>Gauge bottom (local), height of its travel (m), overflow outlet and its outward direction (local).</summary>
        public Vector3 bottom, overflowAt, overflowDir = Vector3.right;
        /// <summary>The way the gauge faces (local, out of the tank).</summary>
        public Vector3 outward = Vector3.back;
        public float height = 1.5f;
        /// <summary>Staved timber tank: drips off the edge of its deck (half size <see cref="weepR"/>, height <see cref="weepY"/>).</summary>
        public bool weeps;
        public float weepY, weepR;

        const float VS = VoxelMesher.DefaultSize;
        UtilityNode node;
        Transform marker;
        Droplets overflow, weep;
        float shown = -1f, vel, inflowAt, startFrame;
        Vector3 waterBase;
        bool inflow, built;

        /// <summary>Fill the gauge shows now (0..1; lags the real level like a float).</summary>
        public float Level => Mathf.Max(0f, shown);
        /// <summary>Real fill of the network (0..1).</summary>
        public float Fill => node && node.waterCapacity > 0f ? Mathf.Clamp01(node.Water / node.waterCapacity) : 0f;
        /// <summary>Marker (target / water column top) height above the gauge bottom, metres (tests).</summary>
        public float MarkerHeight => marker ? (style == Style.Board ? marker.localPosition.y - bottom.y : marker.localScale.y * VS) : -1f;
        public bool Overflowing => overflow && overflow.emitting;
        public bool Weeping => weep && weep.emitting;

        void Awake() { node = GetComponent<UtilityNode>(); startFrame = Time.frameCount; }

        void Build()
        {
            built = true;
            bool hd = PieceBits.HD(gameObject);
            var face = Quaternion.LookRotation(-outward.normalized);                                   // the gauge's -Z looks out
            var mr = GetComponent<MeshRenderer>();
            var mat = mr ? mr.sharedMaterial : null;
            Transform board;
            if (style == Style.Board)
            {
                int n = Mathf.RoundToInt(height / VS);
                board = PieceBits.Shape(transform, "GaugeBoard", bottom, "gauge_board" + n, hd, g =>
                {
                    g.Mat((byte)MadMax.Items.ResourceType.Wood);
                    g.Box(0, 0, 0, 1, n, 0, p => p.y % 4 == 0 && p.x == 1 ? Pal.Black[1] : p.y >= n - 1 ? Pal.Crimson[3] : Pal.Cream[2]);   // graduated board, red top
                    g.Box(-1, n + 1, 1, 2, n + 1, 1, Pal.Ramp(Pal.Metal, 1));                                                               // pulley bar
                }, s =>
                {
                    s.Box(new Vector3(0.04f, height * 0.5f, 0f), new Vector3(0.14f, height + 0.08f, 0.022f), Pal.Cream[2], 0.006f);
                    for (int k = 0; k <= 8; k++) s.Box(new Vector3(0.07f, height * k / 8f, -0.013f), new Vector3(k % 2 == 0 ? 0.07f : 0.04f, 0.012f, 0.006f), Pal.Black[1], 0.002f);
                    s.Box(new Vector3(0.04f, height + 0.02f, -0.012f), new Vector3(0.12f, 0.05f, 0.006f), Pal.Crimson[3], 0.003f);
                    s.Tube(new Vector3(-0.04f, height + 0.1f, 0.03f), new Vector3(0.12f, height + 0.1f, 0.03f), 0.03f, 0.03f, Pal.Metal[2], 10, true);
                }, mat);
                marker = PieceBits.Shape(transform, "GaugeTarget", bottom, "gauge_target", hd, g =>
                {
                    g.Mat((byte)MadMax.Items.ResourceType.Iron);
                    g.Box(-1, 0, -1, 2, 0, -1, p => p.x == 2 ? Pal.Black[0] : Pal.Crimson[3]);                                                // target with a pointer tip
                    g.Box(0, 1, -1, 0, 2, -1, Pal.Ramp(Pal.Chrome, 1));                                                                     // cable clip
                }, s =>
                {
                    s.Box(new Vector3(0.04f, 0f, -0.03f), new Vector3(0.22f, 0.07f, 0.03f), Pal.Crimson[3], 0.008f);
                    s.Box(new Vector3(0.16f, 0f, -0.035f), new Vector3(0.04f, 0.02f, 0.02f), Pal.Black[0], 0.004f);
                    s.Tube(new Vector3(0.04f, 0.035f, -0.03f), new Vector3(0.04f, 0.4f, -0.03f), 0.004f, 0.004f, Pal.Chrome[1], 4);
                }, mat);
            }
            else
            {
                int n = Mathf.RoundToInt(height / VS);
                board = PieceBits.Shape(transform, "GaugeGlass", bottom, "gauge_glass" + n, hd, g =>
                {
                    g.Mat((byte)MadMax.Items.ResourceType.Glass);
                    g.Box(0, 0, 0, 0, n, 0, Pal.Ramp(Pal.Black, 1));                                                                        // empty glass, dark
                    g.Mat((byte)MadMax.Items.ResourceType.Iron);
                    g.Box(0, -1, -1, 0, -1, 1, Pal.Ramp(Pal.Chrome, 1)); g.Box(0, n + 1, -1, 0, n + 1, 1, Pal.Ramp(Pal.Chrome, 1));         // taps into the barrel
                }, s =>
                {
                    s.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, height, 0f), 0.022f, 0.022f, Pal.Glass[1], 10, true);
                    s.Box(new Vector3(0f, -0.02f, 0.03f), new Vector3(0.05f, 0.04f, 0.08f), Pal.Chrome[1], 0.006f);
                    s.Box(new Vector3(0f, height + 0.02f, 0.03f), new Vector3(0.05f, 0.04f, 0.08f), Pal.Chrome[1], 0.006f);
                }, mat);
                waterBase = bottom + face * new Vector3(0f, 0f, hd ? -0.022f : -VS) - (hd ? Vector3.zero : Vector3.up * (VS * 0.5f));   // the glass's foot
                marker = PieceBits.Shape(transform, "GaugeWater", waterBase, "gauge_water", hd, g =>
                {
                    g.Mat((byte)MadMax.Items.ResourceType.Glass);
                    g.Set(0, 0, 0, Pal.Solid(Pal.PaleBlue[1]));
                }, s => s.Box(Vector3.zero, new Vector3(0.034f, VS, 0.034f), Pal.PaleBlue[1], 0.002f), mat);         // one cell, centred: scaled up from the foot
            }
            board.localRotation = marker.localRotation = face;
            if (style == Style.Board)                                                                  // the overflow pipe
            {
                var pipe = PieceBits.Shape(transform, "OverflowPipe", overflowAt - overflowDir.normalized * 0.06f, "overflow_pipe", hd, g =>
                {
                    g.Mat((byte)MadMax.Items.ResourceType.Iron);
                    g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Rust, 2)); g.Set(1, -1, 0, Pal.Solid(Pal.Rust[1]));
                }, s => s.Tube(new Vector3(-0.1f, 0f, 0f), new Vector3(0.08f, 0f, 0f), 0.03f, 0.03f, Pal.Rust[2], 10, true), mat);
                pipe.localRotation = Quaternion.FromToRotation(Vector3.right, overflowDir);
            }
            var o = new GameObject("Overflow");
            o.transform.SetParent(transform, false);
            o.transform.localPosition = overflowAt;
            var dir = overflowDir.normalized;
            overflow = Droplets.Attach(o, 16, (int i, out Vector3 at, out Vector3 v) =>
            {
                at = Random.insideUnitSphere * 0.02f;
                v = dir * Random.Range(0.35f, 0.6f) + Vector3.up * Random.Range(0f, 0.15f) + Random.insideUnitSphere * 0.05f;
            }, new Color(0.72f, 0.86f, 1f), 0.045f, 1.1f);
            overflow.floorY = -overflowAt.y + 0.02f;
            if (weeps)
            {
                var w = new GameObject("Weep");
                w.transform.SetParent(transform, false);
                w.transform.localPosition = new Vector3(0f, weepY, 0f);
                float r = weepR;
                weep = Droplets.Attach(w, 3, (int i, out Vector3 at, out Vector3 v) =>
                {
                    float t = Random.Range(-r, r);                                                     // off the deck's edge
                    int side = Random.Range(0, 4);
                    at = side == 0 ? new Vector3(r, 0f, t) : side == 1 ? new Vector3(-r, 0f, t) : side == 2 ? new Vector3(t, 0f, r) : new Vector3(t, 0f, -r);
                    v = Vector3.down * 0.1f;
                }, new Color(0.72f, 0.86f, 1f), 0.04f, 2.2f);
                weep.floorY = -weepY + 0.02f;
            }
        }

        /// <summary>Water is coming into this node's network right now (a source on it supplying).</summary>
        bool Inflow()
        {
            if (!node) return false;
            if (node.sourceClean > 0f || node.sourceDirty > 0f) return true;
            if (node.waterNet < 0) return false;
            foreach (var n in UtilityNode.All)
                if (n && n.waterNet == node.waterNet && (n.sourceClean > 0f || n.sourceDirty > 0f)) return true;
            return false;
        }

        void Update()
        {
            if (!node) return;
            float fill = Fill;
            float dt = Time.deltaTime;
            if (shown < 0f) shown = fill;
            // a float on a cable: eases towards the water, a little overshoot
            vel += ((fill - shown) * 3f - vel * 2.4f) * dt;
            shown = Mathf.Clamp01(shown + vel * dt);
            if (!PieceBits.Near(transform.position, 60f)) { if (overflow) overflow.emitting = false; if (weep) weep.emitting = false; return; }
            if (!built) { if (Time.frameCount < startFrame + 3) return; Build(); }
            if (Time.time >= inflowAt) { inflowAt = Time.time + 1f; inflow = Inflow(); }
            if (style == Style.Board) marker.localPosition = bottom + Vector3.up * (shown * height);
            else
            {
                float h = Mathf.Max(0.001f, shown * height / VS);
                marker.localScale = new Vector3(1f, h, 1f);
                marker.localPosition = waterBase + Vector3.up * (h * VS * 0.5f);
            }
            bool over = fill >= 0.985f && inflow;
            if (overflow.emitting != over) overflow.emitting = over;
            if (weep && weep.emitting != (fill > 0.35f)) weep.emitting = fill > 0.35f;
            if (over) MadMax.Audio.Sfx.Loop(this, "pour", 0.25f, 0.8f, 16f);
            else MadMax.Audio.Sfx.Loop(this, "pour", 0f, 0.8f, 16f);
        }
    }
}
