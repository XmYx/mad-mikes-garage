using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Mirror: [E] opens the character page with hair, hair colour and beard editable (a haircut or a shave).</summary>
    public class Mirror : MonoBehaviour, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g) => "[E] MIRROR (HAIR / BEARD)";
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) g.Menus.OpenMirror(); }
    }
}
