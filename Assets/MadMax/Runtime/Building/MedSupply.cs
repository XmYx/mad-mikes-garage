using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Medical supplies for the clinic (depth stage G): the pack first, then medicine cabinets
    /// (<c>medicine_cabinet</c> pieces) within reach of the bed or table.</summary>
    public static class MedSupply
    {
        public const string CabinetId = "medicine_cabinet";
        public const float Reach = 5f;
        static readonly List<Container> near = new List<Container>();

        /// <summary>Medicine cabinets within <paramref name="r"/> m of <paramref name="at"/>.</summary>
        public static List<Container> Cabinets(Vector3 at, float r)
        {
            Container.Near(at, r, near);
            for (int i = near.Count - 1; i >= 0; i--)
                if (!near[i].TryGetComponent<Placeable>(out var p) || p.id != CabinetId) near.RemoveAt(i);
            return near;
        }

        /// <summary>How many of <paramref name="id"/> the cabinets near <paramref name="at"/> hold.</summary>
        public static int InCabinets(Vector3 at, string id)
        {
            int n = 0;
            foreach (var c in Cabinets(at, Reach)) n += c.inventory.GetItem(id);
            return n;
        }

        /// <summary>Pack plus nearby cabinets.</summary>
        public static int Count(MadMax.Game.WastelandGame g, Vector3 at, string id) => g.Inventory.GetItem(id) + InCabinets(at, id);

        /// <summary>Take one from a cabinet near <paramref name="at"/> (the clinic's own stock).</summary>
        public static bool TakeFromCabinet(Vector3 at, string id)
        {
            foreach (var c in Cabinets(at, Reach)) if (c.inventory.TakeItem(id)) return true;
            return false;
        }

        /// <summary>Take one from the pack, else from a cabinet near <paramref name="at"/>.</summary>
        public static bool Take(MadMax.Game.WastelandGame g, Vector3 at, string id) => g.Inventory.TakeItem(id) || TakeFromCabinet(at, id);
    }
}
