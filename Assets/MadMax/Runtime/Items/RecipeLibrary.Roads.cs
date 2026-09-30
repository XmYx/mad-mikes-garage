using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for depth stage C (roads). Gravel by hand at the workbench (slow) or at the powered rock
    /// crusher (stone, or rubble with sand besides); gravel feeds the mixer (asphalt with a tar binder, concrete);
    /// the road rake, tamper and line painter are made at the workbench. Pieces (crusher, signs, rails, curbs,
    /// bridges) cost their materials when placed.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> RoadsRecipes()
        {
            var St = ResourceType.Stone; var Gr = ResourceType.Gravel; var Sa = ResourceType.Sand; var Fe = ResourceType.Iron;
            var W = ResourceType.Wood; var S = ResourceType.Scrap; var Rb = ResourceType.Rubber;

            // ---- hand tools
            yield return Itm("rake", "ROAD RAKE", RecipeCategory.Tools, "workbench", "tool_rake", 1, "SPREADS GRAVEL (1 A PATCH); PATCHES POTHOLES WITH ASPHALT", null, (Fe, 2), (W, 2));
            yield return Itm("tamper", "TAMPER", RecipeCategory.Tools, "workbench", "tool_tamper", 1, "SETS COBBLES FROM STONE (2 A PATCH)", null, (Fe, 3), (W, 1));
            yield return Itm("line_painter", "LINE PAINTER", RecipeCategory.Tools, "workbench", "tool_line_painter", 1, "ROAD LINES ON SET ASPHALT OR CONCRETE (WHITE / YELLOW DYE)", null, (Fe, 2), (S, 3), (Rb, 1));

            // ---- gravel: by hand, then the powered crusher
            var hand = Res("gravel_hand", "BREAK STONE X2", RecipeCategory.Refining, "workbench", Gr, 2, "HAMMER AND SWEAT: SLOW", (St, 2));
            hand.seconds = 10f;
            yield return hand;
            var stone = Res("crush_stone", "GRAVEL X6", RecipeCategory.Refining, "rock_crusher", Gr, 6, "JAW CRUSHER (NEEDS POWER)", (St, 4));
            stone.seconds = 5f;
            yield return stone;
            var rubble = Res("crush_rubble", "GRAVEL X4 (RUBBLE)", RecipeCategory.Refining, "rock_crusher", Gr, 4, "SCREENED RUBBLE, SAND BESIDES", (ResourceType.Rubble, 6));
            rubble.byproducts = new[] { (Sa, 2) };
            rubble.seconds = 5f;
            yield return rubble;
            var fine = Res("crush_sand", "SAND X3", RecipeCategory.Refining, "rock_crusher", Sa, 3, "FINE CRUSH", (Gr, 4));
            fine.seconds = 4f;
            yield return fine;

            // ---- gravel as aggregate at the mixer
            yield return Res("asphalt_gravel", "ASPHALT X5 (GRAVEL)", RecipeCategory.Refining, "mixer", ResourceType.Asphalt, 5, "GRAVEL AGGREGATE, TAR BINDER", (Gr, 3), (Sa, 1), (ResourceType.Tar, 1));
            yield return Res("concrete_gravel", "CONCRETE X5 (GRAVEL)", RecipeCategory.Refining, "mixer", ResourceType.Concrete, 5, "GRAVEL AGGREGATE", (Gr, 2), (Sa, 2), (ResourceType.Lime, 1), (ResourceType.Water, 2));
        }
    }
}
