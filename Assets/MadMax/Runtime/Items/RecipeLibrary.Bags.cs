using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for bags (<c>Game/Bags</c>): sewn packs and bags, leather belts, and a handcrafted stick-frame
    /// pack made by hand anywhere. The military pack and the suitcase only come from loot and traders.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> BagsRecipes()
        {
            var C = ResourceType.Cloth; var W = ResourceType.Wood; var S = ResourceType.Scrap; var Rb = ResourceType.Rubber;
            var Le = ResourceType.Leather; var Fe = ResourceType.Iron; var Th = ResourceType.Thread;
            var Clo = RecipeCategory.Clothing;
            yield return Itm("bag_backpack", "BACKPACK", Clo, "sewing", "cloth_backpack", 1, "HOLDS 20 KG, PADDED STRAPS", null, (C, 8), (S, 1));
            yield return Itm("bag_shoulder", "SHOULDER BAG", Clo, "sewing", "cloth_shoulder_bag", 1, "HOLDS 12 KG ON ONE SHOULDER", null, (C, 4));
            yield return Itm("bag_sling", "SLING BAG", Clo, "sewing", "cloth_sling_bag", 1, "HOLDS 7 KG ACROSS THE BACK", null, (C, 3), (S, 1));
            yield return Itm("bag_satchel", "CANVAS SATCHEL", Clo, "sewing", "cloth_canvas_satchel", 1, "HOLDS 9 KG AT THE HIP", null, (C, 4));
            yield return Itm("bag_fanny", "FANNY BAG", Clo, "sewing", "cloth_fanny_bag", 1, "HOLDS 3 KG OF SMALL THINGS", null, (C, 2));
            yield return Itm("bag_duffel", "DUFFEL BAG", Clo, "sewing", "cloth_duffel", 1, "HOLDS 35 KG IN ONE HAND: SLOW, [Q] SETS IT DOWN", null, (C, 7), (Rb, 1));
            yield return Itm("bag_toolbelt", "TOOL BELT", Clo, "leather", "cloth_toolbelt", 1, "6 TOOLS AT THE HIP, DRAWN STRAIGHT FROM THE HOTBAR", null, (Le, 3), (Fe, 1));
            yield return Itm("bag_brace", "BACK BRACE", Clo, "leather", "cloth_back_brace", 1, "EASES A HEAVY LOAD, A STRAINED BACK HEALS FASTER", null, (Le, 2), (Th, 1));
            // by hand, anywhere: two sticks, a hide bundle, twine (wears out)
            yield return Itm("bag_craftpack", "HANDCRAFTED BACKPACK", RecipeCategory.Supplies, null, "cloth_craftpack", 1, "HOLDS 15 KG; WEARS OUT WITH USE", null, (W, 2), (C, 3));
        }
    }
}
