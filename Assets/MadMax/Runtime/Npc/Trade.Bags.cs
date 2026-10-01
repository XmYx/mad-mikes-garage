using System.Collections.Generic;
using MadMax.Game;

namespace MadMax.Npc
{
    /// <summary>Bags at the vendors (<see cref="BagLibrary.Stock"/>): pack traders carry everyday bags, luggage and a
    /// back brace, salvage traders the military pack, sling bags and tool belts, parts dealers tool belts. Prices from
    /// <see cref="BagLibrary.Value"/>; a bag with something in it is not for sale (<see cref="Sell"/>).</summary>
    public static partial class Trade
    {
        static Trade()
        {
            foreach (var (kind, id, min, max) in BagLibrary.Stock)
            {
                if (!sells.TryGetValue(kind, out var have)) continue;
                var l = new List<(string, int, int)>(have);
                if (l.Exists(e => e.Item1 == id)) continue;
                l.Add((id, min, max));
                sells[kind] = l.ToArray();
            }
        }
    }
}
