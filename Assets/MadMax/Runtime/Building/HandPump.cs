using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Hand pump on a well: [E] works the handle for a few litres (fewer where the water table is deep),
    /// straight into the water network. Powered wells pump on their own (WaterSource.Well).
    /// 2026-10-26: each stroke gushes from the spout in pulses with the handle (a fuller gush where the water table is
    /// near, brown where the ground water is fouled) and splashes down the shaft; a powered pump down the well keeps a
    /// thin trickle running while it fills the cistern.</summary>
    public class HandPump : MonoBehaviour, IInteractable
    {
        UtilityNode node;
        MadMax.World.Droplets gush;
        float gushUntil, gushStart, gushRate, splashT;
        const float VS = MadMax.Voxel.VoxelMesher.DefaultSize;
        /// <summary>Spout mouth (local, from the voxel and HD models) and the shaft's water below it.</summary>
        static readonly Vector3 Spout = new Vector3(0f, 14f * VS - 0.02f, 1.4f * VS);
        const float WaterY = 0.06f;
        void Awake() => node = GetComponent<UtilityNode>();

        /// <summary>Water is running from the spout now.</summary>
        public bool Gushing => gush && gush.emitting;
        /// <summary>Drops in the air from the spout (tests).</summary>
        public int GushDrops => gush ? gush.Airborne : 0;
        /// <summary>Time.time of the last hand stroke (tests).</summary>
        public float LastStroke { get; private set; } = -1f;

        /// <summary>Start a gush of <paramref name="seconds"/> s at <paramref name="rate"/> (0..1, the stroke's litres).</summary>
        public void Gush(float seconds, float rate)
        {
            if (!gush)
            {
                var o = new GameObject("Spout");
                o.transform.SetParent(transform, false);
                o.transform.localPosition = Spout;
                bool foul = WaterQuality.GroundTaint(transform.position) != WaterTaint.None;
                gush = MadMax.World.Droplets.Attach(o, 18, (int i, out Vector3 at, out Vector3 v) =>
                {
                    at = Random.insideUnitSphere * 0.012f;
                    v = new Vector3(Random.Range(-0.04f, 0.04f), -0.2f, Random.Range(0.05f, 0.18f) + 0.35f * gushRate);
                }, foul ? new Color(0.55f, 0.48f, 0.36f) : new Color(0.72f, 0.86f, 1f), 0.04f, 0.6f);
                gush.floorY = WaterY - Spout.y;
            }
            gushStart = Time.time;
            gushUntil = Mathf.Max(gushUntil, Time.time + seconds);
            gushRate = Mathf.Clamp01(rate);
            if (seconds > 0f && PieceBits.Near(transform.position, 40f)) gush.emitting = true;
        }

        void Update()
        {
            if (!gush && !(node && node.Powered && node.sourceClean + node.sourceDirty > 0f)) return;
            float t = Time.time;
            bool hand = t < gushUntil && (Mathf.Sin((t - gushStart) * 8.2f) > -0.35f);          // pulses with the handle's down-strokes
            bool motor = node && node.Powered && node.sourceClean + node.sourceDirty > 0f && node.Water < node.waterCapacity - 0.5f;
            if (motor && !gush) Gush(0f, 0.1f);
            if (!hand && motor) gushRate = 0.05f;
            bool on = (hand || motor) && PieceBits.Near(transform.position, 40f);
            if (gush.emitting != on) gush.emitting = on;
            if (on && hand && (splashT -= Time.deltaTime) <= 0f)
            {
                splashT = 0.25f;
                MadMax.World.Fx.Foam(transform.TransformPoint(new Vector3(0f, WaterY + 0.02f, Spout.z + 0.12f)), Vector3.up * 0.15f, 0.08f, new Color(0.85f, 0.9f, 0.95f, 0.5f), 0.8f);
            }
        }

        float Depth
        {
            get
            {
                var t = MadMax.World.DeformableTerrain.Instance;
                return t && t.World != null ? t.World.WaterTable(transform.position.x, transform.position.z) : 10f;
            }
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            float w = UtilityGrid.NetWater(node, out _);
            return "[E] PUMP  WELL " + Mathf.RoundToInt(Depth) + " M DEEP  " + Mathf.RoundToInt(w) + " L" + (node && node.Powered ? "  (ELECTRIC PUMP ON)" : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary || !node) return;
            if (!g.Vitals.Spend(6f)) { g.Toast("TOO TIRED TO PUMP"); return; }
            float litres = Mathf.Clamp(40f / Depth, 1f, 6f);
            var taint = WaterQuality.GroundTaint(transform.position);
            if (node.Water + litres > node.waterCapacity) litres = Mathf.Max(0f, node.waterCapacity - node.Water);
            if (litres <= 0f) { g.Toast("THE CISTERN IS FULL"); return; }
            if (taint != WaterTaint.None) { node.dirty += litres; node.taint |= taint; } else node.clean += litres;
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.6f, 1.2f);
            PieceMotion.Use(gameObject, "pump", 1.6f);                                              // the handle strokes
            Gush(1.5f, litres / 6f);
            LastStroke = Time.time;
            g.Stats.Practice(MadMax.RPG.Skill.Survival, 0.3f);
            g.Toast("PUMPED " + litres.ToString("0") + " L" + (taint != WaterTaint.None ? " (" + WaterQuality.Word(taint) + ")" : ""));
            GetComponent<Placeable>()?.Dirty();
        }
    }
}
