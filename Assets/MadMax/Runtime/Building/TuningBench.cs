using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Tuning bench (garage and the tuning bench piece): [T] opens the TUNING page for the vehicle parked closest
    /// (within 9 m): stat card, dyno graph and every setting of <see cref="VehicleTuning"/>. Needs a wrench in the pack.</summary>
    public class TuningBench : MonoBehaviour, IInteractable
    {
        public const float Reach = 9f;

        public VehicleDriver Nearest(MadMax.Game.WastelandGame g)
        {
            VehicleDriver best = null; float bd = Reach;
            foreach (var v in g.AllVehicles)
            {
                if (!v || v.aiDriven || !v.GetComponent<VehicleTuning>()) continue;
                float d = Vector3.Distance(v.transform.position, transform.position);
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var v = Nearest(g);
            return v ? "[T] TUNE " + MadMax.Game.WastelandGame.Name(v) : "TUNING: PARK A VEHICLE BY THE BENCH";
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary) return;
            var v = Nearest(g);
            if (!v) return;
            if (g.Inventory.GetItem(MadMax.Items.ItemIds.Wrench) <= 0) { g.Toast("TUNING NEEDS A WRENCH"); return; }
            g.Menus.OpenTuning(v.GetComponent<VehicleTuning>());
        }
    }
}
