using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A hand liquid container in hand (fluids block): LMB dips it in open water, drinks clean water from it, or
    /// douses a fire; at tanks K / G open the siphon / pour choice (<see cref="WastelandGame"/> fluids partial). Carried
    /// down by the hand like the jerry can of the pour pose.</summary>
    public class FluidCanTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (g) g.CanStrike(this, user);
        }
    }
}
