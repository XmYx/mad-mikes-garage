using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S14: Oona's kettle barrel at the inn: [T] pours 10 L of clean water from the pack (20 L keeps the kettles
    /// going until the well clears).</summary>
    public class S14KettleBarrel : MonoBehaviour, IInteractable
    {
        public const int Need = 20;
        static int Poured => MadMax.Story.Story.Flag("s14:pour20") ? 20 : MadMax.Story.Story.Flag("s14:pour10") ? 10 : 0;

        public string Prompt(WastelandGame g)
        {
            if (MadMax.Story.Story.StateOf("S14") != MadMax.Story.Story.State.Active || Poured >= Need) return null;
            return (g.Inventory.Get(ResourceType.Water) >= 10 ? "[T] POUR 10 L OF CLEAN WATER FOR OONA'S KETTLES" : "OONA'S KETTLE BARREL: NEEDS 10 L OF CLEAN WATER") + " (" + Poured + "/" + Need + " L)";
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (!secondary || MadMax.Story.Story.StateOf("S14") != MadMax.Story.Story.State.Active || Poured >= Need) return;
            if (!g.Inventory.TrySpend(ResourceType.Water, 10)) { g.Toast("NEED 10 L OF CLEAN WATER (NOT DIRTY, NOT SALTY)"); return; }
            MadMax.Story.Story.SetFlag(Poured == 0 ? "s14:pour10" : "s14:pour20");
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.6f);
            g.Toast("POURED 10 L INTO THE KETTLE BARREL (" + Poured + "/" + Need + " L)");
            if (Poured >= Need) { MadMax.Story.Story.Note("s14:water_given"); g.Toast("OONA: THAT'LL KEEP THE KETTLES GOING TILL THE WELL COMES RIGHT."); }
        }
    }
}
