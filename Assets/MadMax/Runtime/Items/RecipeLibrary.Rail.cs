using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Rolling stock kits (put on the nearest track from the pack): a hand draisine at the workbench, wagons
    /// and a makeshift diesel loco at the garage (the loco needs the iron road's know-how: the Railwayman's Handbook or
    /// the workbench research).</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> RailRecipes()
        {
            var V = RecipeCategory.Vehicles;
            yield return Itm("rail_cart", "DRAISINE KIT", V, "workbench", "kit_rail_cart", 1,
                "A HAND-PUMPED RAIL CART FOR TWO: USE IT BESIDE A TRACK. NO FUEL, JUST ARMS", null, (ResourceType.Iron, 10), (ResourceType.Wood, 10), (ResourceType.Scrap, 8));
            yield return Itm("rail_flatcar", "FLATCAR KIT", V, "garage", "kit_rail_flatcar", 1,
                "A TIMBER-DECKED FLATCAR: CARS DRIVE ON FROM A LOADING RAMP AND RIDE STRAPPED. COUPLE IT TO A LOCO", null, (ResourceType.Iron, 24), (ResourceType.Wood, 24), (ResourceType.Scrap, 20));
            yield return Itm("rail_hopper", "HOPPER WAGON KIT", V, "garage", "kit_rail_hopper", 1,
                "AN OPEN STEEL WAGON FOR SCRAP AND ORE", null, (ResourceType.Iron, 30), (ResourceType.Scrap, 30));
            yield return Itm("rail_shack", "SHACK WAGON KIT", V, "garage", "kit_rail_shack", 1,
                "A CORRUGATED HOME ON A FLATCAR, WITH A PORCH AND A STOVEPIPE", null, (ResourceType.Iron, 20), (ResourceType.Wood, 40), (ResourceType.Scrap, 30), (ResourceType.Glass, 4));
            var loco = Itm("rail_loco", "MAKESHIFT LOCO KIT", V, "garage", "kit_rail_loco", 1,
                "A TRUCK DIESEL ON A RAIL FRAME UNDER PATCHED PANELS: PULLS A TRAIN. RUNS ON DIESEL (300 L TANK)", null,
                (ResourceType.Iron, 70), (ResourceType.Steel, 20), (ResourceType.Scrap, 80), (ResourceType.Copper, 12), (ResourceType.Rubber, 6), (ResourceType.Glass, 4));
            loco.knowledge = "k_rail";
            yield return loco;
        }
    }
}
