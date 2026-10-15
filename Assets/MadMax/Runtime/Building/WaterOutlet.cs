using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Sink / shower / rain barrel tap: drink, wash, shower or fill the canteen from the water network
    /// (or the piece's own storage). Dirty water can make you sick.</summary>
    public class WaterOutlet : MonoBehaviour, IInteractable
    {
        public enum Kind { Sink, Shower, Barrel, Bath, Well }
        public Kind kind;
        UtilityNode node;
        void Awake() => node = GetComponent<UtilityNode>();

        /// <summary>Shower: seconds the rose sprays after use, the head height (piece space) and the spray (2026-10-15:
        /// used to be one burst of puffs).</summary>
        public const float ShowerSeconds = 5f;
        static readonly Vector3 Rose = new Vector3(0f, 2.2f, 0f);
        float sprayUntil, dropT;
        public bool Spraying => Time.time < sprayUntil;
        public void Spray(float seconds) => sprayUntil = Time.time + seconds;

        Droplets spray;

        void Update()
        {
            if (spray) spray.emitting = Spraying;
            if (!Spraying) return;
            if (!spray)
                spray = Droplets.Attach(gameObject, 36, (int i, out Vector3 at, out Vector3 v) =>
                {
                    var off = Random.insideUnitCircle * 0.11f;
                    at = Rose + new Vector3(off.x, -0.03f, off.y);
                    v = new Vector3(off.x * 3f, -Random.Range(1.2f, 2.2f), off.y * 3f);
                }, 0.045f, 0.75f);
            float left = sprayUntil - Time.time;
            MadMax.Audio.Sfx.Loop(this, "pour", Mathf.Clamp01(left / 0.6f) * 0.45f, 1.25f, 10f);
            if ((dropT -= Time.deltaTime) > 0f) return;
            dropT = 0.25f;                                                                         // steam off a hot shower
            MadMax.World.Fx.Smoke(transform.TransformPoint(new Vector3(0f, 0.3f, 0f)) + Random.insideUnitSphere * 0.25f, Vector3.up * 0.3f, 0.2f, new Color(0.9f, 0.92f, 0.95f, 0.3f), 1.4f);
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            float w = UtilityGrid.NetWater(node, out float clean);
            string q = w < 0.5f ? "NO WATER" : Mathf.RoundToInt(w) + "L" + (clean < 0.99f ? " " + WaterQuality.Word(WaterQuality.TaintOf(node)) : "");
            return kind switch
            {
                Kind.Shower => w < 10f ? "SHOWER: " + q : "[E] SHOWER  " + q,
                Kind.Bath => w < 40f ? "BATHTUB (NEEDS 40L): " + q : "[E] TAKE A BATH  " + q,
                Kind.Sink => w < 0.5f ? "SINK: " + q : "[E] DRINK  [T] WASH HANDS / FILL  " + q,
                Kind.Well => w < 0.5f ? null : "[T] FILL CANTEEN",                          // [E] belongs to the hand pump
                _ => w < 0.5f ? "BARREL: " + q : "[E] DRINK  [T] FILL CANTEEN  " + q,
            };
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            PieceMotion.Use(gameObject, "turn", 1.5f);                                             // the tap handle turns
            if (kind == Kind.Bath)
            {
                if (secondary) return;
                float got = UtilityGrid.Draw(node, 40f, out bool clean);
                if (got < 39.9f) { g.Toast("NOT ENOUGH WATER (40L)"); return; }
                MadMax.Audio.Sfx.Play("splash", transform.position, 0.8f, 0.8f);
                MadMax.Game.ScreenFader.FadeThrough(() => g.Bathe(clean), 1f);
                return;
            }
            if (kind == Kind.Shower)
            {
                float got = UtilityGrid.Draw(node, 10f, out bool clean);
                if (got < 9.9f) { g.Toast("NOT ENOUGH WATER"); return; }
                g.Wash(clean ? 100f : 60f, "SHOWERED");
                sprayUntil = Time.time + ShowerSeconds;                                          // the rose runs a while (Update)
                PieceMotion.Use(gameObject, "turn", ShowerSeconds);
                return;
            }
            if (!secondary)
            {
                var taint = WaterQuality.TaintOf(node);
                float got = UtilityGrid.Draw(node, 0.5f, out bool clean);
                if (got < 0.1f) { g.Toast("NO WATER"); return; }
                g.DrinkTainted(got * 60f, clean ? WaterTaint.None : taint);
                return;
            }
            if (kind == Kind.Sink && g.Stats.hygiene < 95f)
            {
                float got = UtilityGrid.Draw(node, 1f, out bool clean);
                if (got >= 0.9f) { g.Wash(clean ? 30f : 15f, "WASHED HANDS"); return; }
            }
            // fill the canteen (inventory water)
            var carried = WaterQuality.Carried(node);
            string word = WaterQuality.Word(WaterQuality.TaintOf(node));
            float fill = UtilityGrid.Draw(node, 5f, out bool c2);
            if (fill < 0.5f) { g.Toast("NO WATER"); return; }
            g.Inventory.Add(c2 ? ResourceType.Water : carried, Mathf.RoundToInt(fill));
            g.Toast("FILLED " + Mathf.RoundToInt(fill) + "L" + (c2 ? "" : " " + word));
        }
    }
}
