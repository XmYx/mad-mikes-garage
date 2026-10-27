using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Mirror: [E] steps up to the glass and faces it (<see cref="MadMax.Game.WastelandGame.UseMirror"/>), then opens
    /// the character page with hair, hair colour and beard editable (a haircut or a shave): every change snips and drops a
    /// puff of trimmings.</summary>
    public class Mirror : MonoBehaviour, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g) => "[E] MIRROR (HAIR / BEARD)";

        /// <summary>Out of the glass (the piece's +Y: wall pieces are authored in the XZ plane).</summary>
        public Vector3 Normal => transform.up;
        /// <summary>The middle of the glass.</summary>
        public Vector3 Centre => transform.position + transform.up * 0.08f;
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) g.UseMirror(this); }
    }
}
