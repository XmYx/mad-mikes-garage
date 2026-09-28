using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hydraulic jack: swing it at a vehicle to put a flipped one back on its wheels (on foot, without the
    /// recovery cheat). Carrying one lets you change heavy wheels (size 3+).</summary>
    public class JackTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            VehicleDriver v = null; float best = 3.5f;
            foreach (var c in Physics.OverlapSphere(user.transform.position + user.transform.forward * 1.2f + Vector3.up * 0.5f, 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var d = c.GetComponentInParent<VehicleDriver>();
                if (!d) continue;
                float dist = Vector3.Distance(d.transform.position, user.transform.position);
                if (dist < best) { best = dist; v = d; }
            }
            if (!v) { g?.Toast("JACK: NO VEHICLE IN REACH"); return; }
            if (v.transform.up.y > 0.7f) { g?.Toast("IT IS ON ITS WHEELS"); return; }
            if (v.Body.mass > 6000f && g && g.Stats.Attribute(MadMax.RPG.Attr.Strength) < 7) { g.Toast("TOO HEAVY TO JACK OVER (STRENGTH 7)"); return; }
            MadMax.Audio.Sfx.Play("ratchet", v.transform.position, 0.9f, 0.7f);
            v.Recover();
            g?.Stats.Practice(MadMax.RPG.Skill.Mechanics, 4f);
            g?.WearTool(id, 0.05f);
        }
    }
}
