using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes added by the roadmap systems (tools, furniture, clothing, attachments, refining, ...),
    /// grouped by system. Same helpers as the core library.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> Roadmap()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var Rb = ResourceType.Rubber;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Oil = ResourceType.Oil;
            // ---- 1. tools
            yield return Itm("crowbar", "CROWBAR", RecipeCategory.Tools, "workbench", "tool_crowbar", 1, "PRY LOCKS OPEN; SOLID MELEE", null, (Fe, 2));
            yield return Itm("welder", "WELDER", RecipeCategory.Tools, "workbench", "tool_welder", 1, "REPAIR VEHICLES (SCRAP RODS + FUEL)", null, (Fe, 2), (Cu, 2), (Rb, 1));
            yield return Itm("jack", "JACK", RecipeCategory.Tools, "workbench", "tool_jack", 1, "RIGHT FLIPPED CARS; NEEDED FOR BIG WHEELS", null, (Fe, 3), (Oil, 1));
            yield return Itm("binoculars", "BINOCULARS", RecipeCategory.Tools, "workbench", "tool_binoculars", 1, "HOLD RMB TO LOOK FAR", null, (G, 2), (S, 2));
            yield return Itm("geiger", "GEIGER COUNTER", RecipeCategory.Tools, "workbench", "tool_geiger", 1, "CLICKS NEAR RADIATION", null, (Cu, 2), (G, 1), (S, 2));
            yield return Itm("flashlight", "FLASHLIGHT", RecipeCategory.Tools, "workbench", "tool_flashlight", 1, "BRIGHT SPOT BEAM", null, (Cu, 1), (G, 1), (S, 1));
            yield return Itm("repair_kit", "REPAIR KIT", RecipeCategory.Supplies, "workbench", "use_repair_kit", 1, "PATCH A VEHICLE PART (+30%)", null, (S, 4), (Rb, 1), (Oil, 1));
        }
    }
}
