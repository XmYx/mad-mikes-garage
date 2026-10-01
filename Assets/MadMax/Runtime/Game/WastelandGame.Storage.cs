using System.Collections.Generic;
using MadMax.Building;

namespace MadMax.Game
{
    /// <summary>Storage the player carries (worn bags, belts): shown with the pack in the loot window and counted in the
    /// carry weight. Vehicle compartments and placed storage are ordinary <see cref="Container"/>s found through
    /// <see cref="Container.All"/> and reached at <see cref="Container.AccessAt"/>.</summary>
    public partial class WastelandGame
    {
        public readonly List<Container> WornStorage = new List<Container>();
    }
}
