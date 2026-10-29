using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-29 — the rag and bottle economy: a bottled drink leaves its empty bottle
    /// (glass in the pack), a garment that falls apart leaves rags (cloth: 2 off a coat or trousers, 1 off the rest; armour
    /// mended with other stuff leaves none), so a MOLOTOV (glass + cloth + a litre of fuel, made by hand) comes out of the
    /// drink, clothes and fuel loops. The rest of this pass lives on its pieces: <c>World/Windsock</c>, <c>World/ColdBreath</c>
    /// (<c>HumanAnimator.Breath</c>, <c>Animal.Prints</c>), <c>PlayerCharacter.Contact</c>, <c>Npc/RaiderBottle</c>.</summary>
    public partial class WastelandGame
    {
        static readonly string[] Bottled = { "drink_soda", "drink_water", "drink_beer", "drink_cider", "drink_mead" };
        /// <summary>Empty bottles and rags kept (tests).</summary>
        public static int BottlesKept, RagsKept;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset1029() { BottlesKept = 0; RagsKept = 0; }

        public static bool IsBottled(string id) => System.Array.IndexOf(Bottled, id) >= 0;

        void Ate1029(string id)
        {
            if (!IsBottled(id)) return;
            using var feed = Inventory.Source("EMPTY BOTTLE");
            Inventory.Add(ResourceType.Glass, 1);
            BottlesKept++;
        }

        /// <summary>Rags off a garment that fell apart (from <c>FallApart</c>): the toast's suffix.</summary>
        string Rags1029(ClothingDef d)
        {
            if (d == null || d.mendWith != ResourceType.Cloth || d.slot == ClothingSlot.Pack || d.slot == ClothingSlot.Belt || d.slot == ClothingSlot.Hand) return "";
            int n = d.slot == ClothingSlot.Torso || d.slot == ClothingSlot.Outer || d.slot == ClothingSlot.Legs ? 2 : 1;
            using var feed = Inventory.Source("RAGS");
            Inventory.Add(ResourceType.Cloth, n);
            RagsKept += n;
            return " - KEPT THE RAGS";
        }
    }
}
