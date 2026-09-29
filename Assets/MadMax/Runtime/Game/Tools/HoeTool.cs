using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Garden hoe: chops the weeds out of the bed in front and loosens the soil (a little fertility back).
    /// Quicker and cheaper on stamina than pulling weeds by hand.</summary>
    public class HoeTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (!g) return;
            var plot = Nearest(user.transform.position + user.transform.forward * 0.9f, 1.4f);
            if (!plot) { g.Toast("HOE: NO GARDEN BED IN FRONT"); return; }
            if (!g.Vitals.Spend(3f)) return;
            bool weedy = plot.weeds > 0.05f;
            plot.Weed();
            plot.fertility = Mathf.Min(1f, plot.fertility + 0.02f);
            MadMax.World.Fx.Smoke(plot.transform.position + plot.soil, Vector3.up * 0.5f, 0.2f, new Color(0.35f, 0.25f, 0.15f, 0.8f), 0.6f);
            MadMax.Audio.Sfx.Play("dig", plot.transform.position, 0.5f, 1.2f, 15f);
            g.Stats.Practice(MadMax.RPG.Skill.Farming, weedy ? 1f : 0.2f);
            g.WearTool(id, 0.01f);
            g.Toast(weedy ? "HOED THE WEEDS OUT" : "LOOSENED THE SOIL");
        }

        public static GardenPlot Nearest(Vector3 p, float reach)
        {
            GardenPlot best = null; float bd = reach;
            foreach (var plot in FindObjectsByType<GardenPlot>())
            {
                float d = Vector3.Distance(plot.transform.position, p);
                if (d < bd) { bd = d; best = plot; }
            }
            return best;
        }
    }
}
