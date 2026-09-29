using MadMax.Items;
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

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            float w = UtilityGrid.NetWater(node, out float clean);
            string q = w < 0.5f ? "NO WATER" : Mathf.RoundToInt(w) + "L" + (clean < 0.99f ? " DIRTY" : "");
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
                for (int i = 0; i < 20; i++) MadMax.World.Fx.Smoke(transform.position + Vector3.up * (1.2f + Random.value), Vector3.down * 2f + Random.insideUnitSphere * 0.3f, 0.12f, new Color(0.75f, 0.85f, 0.95f, 0.6f), 0.6f);
                return;
            }
            if (!secondary)
            {
                float got = UtilityGrid.Draw(node, 0.5f, out bool clean);
                if (got < 0.1f) { g.Toast("NO WATER"); return; }
                g.Drink(got * 60f, !clean);
                return;
            }
            if (kind == Kind.Sink && g.Stats.hygiene < 95f)
            {
                float got = UtilityGrid.Draw(node, 1f, out bool clean);
                if (got >= 0.9f) { g.Wash(clean ? 30f : 15f, "WASHED HANDS"); return; }
            }
            // fill the canteen (inventory water)
            float fill = UtilityGrid.Draw(node, 5f, out bool c2);
            if (fill < 0.5f) { g.Toast("NO WATER"); return; }
            g.Inventory.Add(c2 ? ResourceType.Water : ResourceType.DirtyWater, Mathf.RoundToInt(fill));
            g.Toast("FILLED " + Mathf.RoundToInt(fill) + "L" + (c2 ? "" : " DIRTY"));
        }
    }
}
