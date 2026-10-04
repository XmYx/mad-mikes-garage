using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Gold pan (depth stage H, the hand rung of mining): standing at a river or lake shore, each swirl washes a
    /// scoop of the bed — sand now and then, and the stretch's gold fines into the vial; a full vial is a flake of GOLD
    /// ORE (<see cref="WastelandGame.PanGold"/>). Rivers pay best, lakes and the sea a third, dry wadis nothing.</summary>
    public class GoldPanTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !t || g.Current) return;
            var feet = user.transform.position;
            var at = feet + user.transform.forward * 0.9f;
            if (t.WaterDepth(at.x, at.z) < 0.04f) at = feet;
            if (t.WaterDepth(at.x, at.z) < 0.04f) { g.Toast("GOLD PAN: WORK IT IN THE SHALLOWS OF A RIVER OR LAKE"); return; }
            if (user.Swimming) { g.Toast("TOO DEEP TO PAN: STAND IN THE SHALLOWS"); return; }
            if (!g.Vitals.Spend(5f)) return;
            float rich = g.GoldRichness(at);
            int flakes = g.PanGold(at);
            for (int i = 0; i < 5; i++)
                Fx.Smoke(at + Vector3.up * 0.2f, Random.insideUnitSphere * 0.5f + Vector3.up * 0.3f, 0.08f, new Color(0.62f, 0.55f, 0.42f, 0.6f), 0.5f);
            MadMax.Audio.Sfx.Play("splash", at, 0.45f, 1.3f, 15f);
            g.Stats.Practice(Skill.Survival, 0.6f);
            g.WearTool(id, 0.004f);
            if (flakes > 0) { g.Toast("A FLAKE OF GOLD IN THE PAN: +1 GOLD ORE"); MadMax.Audio.Sfx.Play2D("ding", 0.4f, 1.4f); }
            else g.Toast((rich < 0.2f ? "BARE GRAVEL: HARDLY A SPECK HERE" : rich < 0.6f ? "A FEW SPECKS OF COLOUR" : "GOOD COLOUR IN THE PAN") + "  (THE VIAL: " + Words.Amount(g.GoldFines) + ")");
        }
    }
}
