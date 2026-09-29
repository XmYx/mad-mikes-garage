using MadMax.Game;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A diving compressor (user additions): on a powered grid, [E] fills the diver's air tank, [T] presses an
    /// O2 bottle (an iron bottle and 2 L of water split by the electrolysis cell) for the submarine's rack or the tank.</summary>
    public class AirCompressor : MonoBehaviour, IInteractable
    {
        UtilityNode node;
        float busyUntil;
        void Awake() { node = GetComponent<UtilityNode>(); if (node) node.demand = 600f; }

        bool Powered => node && node.Powered;

        void Update()
        {
            if (Time.time < busyUntil) MadMax.Audio.Sfx.Loop(this, "generator", 0.35f, 1.4f, 18f);   // the pump thumping
        }

        public string Prompt(WastelandGame g) => !Powered ? "AIR COMPRESSOR: NO POWER"
            : "AIR COMPRESSOR: [E] FILL TANK (" + Mathf.RoundToInt(g.TankAir) + "/" + Mathf.RoundToInt(WastelandGame.TankSize) + " S)  [T] PRESS O2 BOTTLE (1 IRON, 2 L WATER)";

        public void Use(WastelandGame g, bool secondary)
        {
            if (!Powered) { g.Toast("THE COMPRESSOR NEEDS POWER"); return; }
            busyUntil = Time.time + 2.5f;
            if (!secondary)
            {
                if (!g.Wearing("air_tank")) { g.Toast("WEAR AN AIR TANK TO FILL IT"); return; }
                g.FillTank(WastelandGame.TankSize);
                g.Toast("TANK FULL: " + Mathf.RoundToInt(WastelandGame.TankSize) + " S OF AIR");
                return;
            }
            if (g.Inventory.Get(ResourceType.Iron) < 1 || g.Inventory.Get(ResourceType.Water) < 2) { g.Toast("NEED 1 IRON AND 2 L WATER"); return; }
            g.Inventory.TrySpend(ResourceType.Iron, 1); g.Inventory.TrySpend(ResourceType.Water, 2);
            g.Inventory.AddItem(WastelandGame.O2Bottle);
            g.Toast("PRESSED AN O2 BOTTLE");
        }
    }
}
