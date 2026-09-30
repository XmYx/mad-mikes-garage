using MadMax.Animals;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Shears (depth stage E): clip the sheep or goat in front of you (<see cref="Animal.Shear"/>). The same
    /// works with [E] while the shears are in the pack.</summary>
    public class ShearsTool : HandTool
    {
        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (!g) return;
            var a = Nearest(user.transform.position + user.transform.forward * 0.9f, 1.3f);
            if (!a) { g.Toast("SHEARS: STAND BY A SHEEP OR A GOAT"); return; }
            a.Shear(g);
        }

        /// <summary>The living wool animal nearest <paramref name="p"/> within <paramref name="reach"/> m.</summary>
        public static Animal Nearest(Vector3 p, float reach)
        {
            Animal best = null; float bd = reach * reach;
            foreach (var a in Animal.All)
            {
                if (!a || !a.Alive || a.WoolYield == 0) continue;
                var d = a.transform.position - p; d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = a; }
            }
            return best;
        }
    }
}
