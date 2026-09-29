using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Watering can: one litre from the pack (clean or dirty water) waters every bed within reach of the spout,
    /// or knocks down a small fire. Dipped into a lake or channel it fills the pack with 5 L of dirty water.</summary>
    public class WateringCanTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (!g) return;
            var at = user.transform.position + user.transform.forward * 1.0f;
            var terrain = MadMax.World.DeformableTerrain.Instance;
            var kind = g.Inventory.Get(ResourceType.DirtyWater) > 0 ? ResourceType.DirtyWater : ResourceType.Water;   // water plants with the dirty stuff first
            bool have = g.Inventory.Get(kind) > 0;

            if (have && MadMax.World.Fire.Douse(at + Vector3.up * 0.3f, 1.8f, 0f) > 0)
            {
                g.Inventory.TrySpend(kind, 1);
                MadMax.World.Fire.Douse(at + Vector3.up * 0.3f, 1.8f, 1f);
                MadMax.Audio.Sfx.Play("sizzle", at, 0.8f, 1f, 20f);
                Spray(at, 10);
                g.Toast("DOUSED THE FLAMES");
                return;
            }
            int watered = 0;
            if (have)
                foreach (var plot in FindObjectsByType<MadMax.Building.GardenPlot>())
                    if (Vector3.Distance(plot.transform.position, at) < 1.6f && plot.water < 0.95f) { plot.Water(1f); watered++; }
            if (watered > 0)
            {
                g.Inventory.TrySpend(kind, 1);
                MadMax.Audio.Sfx.Play("pour", at, 0.6f, 1.1f, 15f);
                Spray(at, 6);
                g.Stats.Practice(MadMax.RPG.Skill.Farming, 0.3f * watered);
                g.Toast("WATERED " + watered + (watered == 1 ? " BED" : " BEDS"));
                return;
            }
            if (terrain && terrain.WaterDepth(at.x, at.z) > 0.08f)
            {
                g.Inventory.Add(ResourceType.DirtyWater, 5);
                MadMax.Audio.Sfx.Play("splash", at, 0.6f, 1.2f, 15f);
                g.Toast("FILLED THE CAN: +5 L DIRTY WATER");
                return;
            }
            g.Toast(have ? "NOTHING TO WATER HERE" : "THE CAN IS DRY: DIP IT IN WATER");
        }

        static void Spray(Vector3 at, int n)
        {
            for (int i = 0; i < n; i++)
                MadMax.World.Fx.Smoke(at + Vector3.up * 0.5f, Random.insideUnitSphere * 0.6f + Vector3.down * 0.5f, 0.1f, new Color(0.7f, 0.8f, 0.95f, 0.6f), 0.4f);
        }
    }
}
