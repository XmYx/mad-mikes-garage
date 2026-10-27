using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A lens flash seen (2026-10-26): someone hostile who caught the sun off the player's binoculars knows
    /// where they are for <see cref="GlintMemory"/> s and comes for them from up to <see cref="GlintSpotRange"/> m
    /// (instead of the usual 60 m sight). Set by <see cref="MadMax.Game.WastelandGame.BinocularTick"/>.</summary>
    public partial class Npc
    {
        public const float GlintMemory = 25f, GlintSpotRange = 400f;
        float glintUntil = -1f;
        /// <summary>Where the flash came from.</summary>
        public Vector3 GlintFrom { get; private set; }
        /// <summary>Still after the flash they saw.</summary>
        public bool GlintAlert => Time.time < glintUntil;

        public void SeeGlint(Vector3 at)
        {
            glintUntil = Time.time + GlintMemory;
            GlintFrom = at;
            Attend(at);
        }
    }
}
