using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Garden hoe: chops the weeds out of the bed in front and loosens the soil (a little fertility back);
    /// on open ground it tills a 2 m field bed (<see cref="Fields"/>). Quicker than pulling weeds by hand.</summary>
    public class HoeTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (!g) return;
            var front = user.transform.position + user.transform.forward * 1.2f;
            var plot = Nearest(user.transform.position + user.transform.forward * 0.9f, 1.4f);
            if (!plot)
            {
                // open ground: till a 2 m field bed (depth stage B)
                if (!Fields.CanTill(front, out var why)) { g.Toast("HOE: CAN'T TILL HERE (" + why + ")"); return; }
                if (!g.Vitals.Spend(10f)) return;
                var bed = Fields.Till(g, front);
                if (!bed) return;
                MadMax.World.Fx.Smoke(bed.transform.position + Vector3.up * 0.2f, Vector3.up * 0.5f, 0.4f, new Color(0.35f, 0.25f, 0.15f, 0.8f), 0.8f);
                MadMax.Audio.Sfx.Play("dig", bed.transform.position, 0.6f, 1.0f, 15f);
                g.Stats.Practice(MadMax.RPG.Skill.Farming, 2f);
                g.WearTool(id, 0.02f);
                g.Toast("TILLED A FIELD BED (2 X 2 M): [E] TO SOW");
                return;
            }
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
