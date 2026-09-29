using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Hand pump on a well: [E] works the handle for a few litres (fewer where the water table is deep),
    /// straight into the water network. Powered wells pump on their own (WaterSource.Well).</summary>
    public class HandPump : MonoBehaviour, IInteractable
    {
        UtilityNode node;
        void Awake() => node = GetComponent<UtilityNode>();

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
            var t = MadMax.World.DeformableTerrain.Instance;
            bool toxic = t && t.BiomeAt(transform.position.x, transform.position.z) == MadMax.World.Biome.Nuclear;
            if (node.Water + litres > node.waterCapacity) litres = Mathf.Max(0f, node.waterCapacity - node.Water);
            if (litres <= 0f) { g.Toast("THE CISTERN IS FULL"); return; }
            if (toxic) node.dirty += litres; else node.clean += litres;
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.6f, 1.2f);
            g.Stats.Practice(MadMax.RPG.Skill.Survival, 0.3f);
            g.Toast("PUMPED " + litres.ToString("0") + " L" + (toxic ? " (TAINTED)" : ""));
            GetComponent<Placeable>()?.Dirty();
        }
    }
}
