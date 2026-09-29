using MadMax.Game;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A rack of O2 bottles (user additions; the submarine carries one): [E] cracks a bottle from the pack into
    /// the cabin air (in a submarine) or the diver's tank.</summary>
    public class O2Rack : MonoBehaviour, IInteractable
    {
        public string Prompt(WastelandGame g) => "O2 RACK: [E] CRACK A BOTTLE (" + g.Inventory.GetItem(WastelandGame.O2Bottle) + " IN THE PACK)";
        public void Use(WastelandGame g, bool secondary) { if (!secondary) g.UseItem(WastelandGame.O2Bottle); }
    }
}
