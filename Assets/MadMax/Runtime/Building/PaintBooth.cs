using MadMax.Game;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Paint station (roadmap 19): park a vehicle beside it and [E] opens the paint shop — a new body colour
    /// (dyes, or scrap and oil for the mixed shades) and a decal for the flanks.</summary>
    public class PaintBooth : MonoBehaviour, IInteractable
    {
        public float reach = 8f;

        public string Prompt(WastelandGame g)
        {
            var v = Nearest(g);
            return v ? "[E] PAINT THE " + WastelandGame.Name(v) : "PAINT STATION: PARK A VEHICLE HERE";
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary) return;
            var v = Nearest(g);
            if (!v) { g.Toast("PARK A VEHICLE BY THE PAINT STATION"); return; }
            g.Menus.OpenPaint(VehiclePaint.Of(v));
        }

        VehicleDriver Nearest(WastelandGame g)
        {
            VehicleDriver best = null; float bd = reach * reach;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.driveable || v.aiDriven) continue;
                float d = (v.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }
    }
}
