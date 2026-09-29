using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Rear dropper: [B] scatters caltrops (ammo_caltrops; tyres rolling over them burst) or pours an oil slick
    /// (2 L of oil; wheels on it lose most of their grip) behind the vehicle. Shift+B switches between them.</summary>
    public class RearDropper : VehicleWeapon
    {
        public const string Caltrops = "ammo_caltrops";
        public bool oil;
        float cool;

        public override string Status
        {
            get
            {
                var g = MadMax.Game.WastelandGame.Instance;
                if (!g) return null;
                return (oil ? "OIL " + g.Inventory.Get(ResourceType.Oil) + " L" : "CALTROPS x" + g.Inventory.GetItem(Caltrops)) + "  [B] DROP  [SHIFT+B] SWITCH";
            }
        }

        public override void Operate(in WeaponInput i)
        {
            cool -= i.dt;
            if (!i.drop || cool > 0f) return;
            var g = MadMax.Game.WastelandGame.Instance;
            if (i.alt) { oil = !oil; cool = 0.25f; g.Toast(oil ? "DROPPER: OIL SLICK" : "DROPPER: CALTROPS"); return; }
            cool = 1.2f;
            var at = transform.TransformPoint(new Vector3(0f, 0f, -0.6f));
            if (oil)
            {
                if (!g.Inventory.TrySpend(ResourceType.Oil, 2)) { g.Toast("DROPPER: NEED 2 L OF OIL"); return; }
                RoadHazards.Drop(RoadHazards.Kind.Oil, at, 2.2f);
                MadMax.Audio.Sfx.Play("pour", at, 0.9f, 0.8f);
            }
            else
            {
                if (!g.Inventory.TakeItem(Caltrops)) { g.Toast("DROPPER: NO CALTROPS"); return; }
                RoadHazards.Drop(RoadHazards.Kind.Caltrops, at, 1.6f);
                MadMax.Audio.Sfx.Play("chain", at, 0.8f, 1.3f);
            }
        }
    }
}
