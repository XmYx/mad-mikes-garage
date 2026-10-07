using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Scheduled update 2026-10-04: the fire extinguisher (workbench; refilled with sand like a repair), the
    /// crutch (by hand) and the handheld radio (workbench).</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> SafetyRecipes()
        {
            yield return Itm("extinguisher", "FIRE EXTINGUISHER", RecipeCategory.Tools, "workbench", "tool_extinguisher", 1,
                "EIGHT BURSTS OF DRY POWDER: PUTS OUT AN ENGINE FIRE BEFORE THE TANK GOES", null, (ResourceType.Iron, 2), (ResourceType.Sand, 4), (ResourceType.Rubber, 1));
            yield return Itm("crutch", "CRUTCH", RecipeCategory.Supplies, "workbench", "tool_crutch", 1,
                "HELD IN HAND: HOP ON ONE LEG OR A BROKEN ONE NEARLY AT A WALK", null, (ResourceType.Wood, 3), (ResourceType.Cloth, 1));
            yield return Itm("hand_radio", "HANDHELD RADIO", RecipeCategory.Tools, "workbench", "tool_radio", 1,
                "MUSIC ON FOOT, AND THE HOME FREQUENCY EVEN IN THE PACK", null, (ResourceType.Copper, 2), (ResourceType.Scrap, 3), (ResourceType.Glass, 1));
            yield return Itm("key_blank", "KEY BLANK", RecipeCategory.Supplies, "workbench", "misc_key_blank", 2,
                "USE A CAR KEY AT A WORKBENCH WITH A BLANK: A SPARE FOR THE GLOVEBOX OR A COMPANION", null, (ResourceType.Copper, 1), (ResourceType.Scrap, 1));
        }
    }
}
