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

            // ---- 2. furniture: dyes (boiled on a wood stove) and cold dishes at the kitchen counter
            var Wa = ResourceType.Water;
            yield return DyeR("red", "BOILED BERRIES", new[] { ("food_berries", 2) }, (Wa, 1));
            yield return DyeR("blue", "COPPER SALTS", null, (Cu, 1), (Wa, 1));
            yield return DyeR("green", "BOILED HERBS", new[] { ("food_herbs", 2) }, (Wa, 1));
            yield return DyeR("yellow", "PUMPKIN AND SUNFLOWER", new[] { ("food_pumpkin", 1) }, (Wa, 1));
            yield return DyeR("black", "CHARCOAL AND OIL", null, (ResourceType.Charcoal, 2), (Oil, 1));
            yield return DyeR("white", "SLAKED LIME", null, (ResourceType.Lime, 2), (Wa, 1));
            yield return Itm("counter_salad", "GARDEN SALAD", RecipeCategory.Cooking, "counter", "food_salad", 1, "NO FIRE NEEDED", new[] { ("food_tomato", 1), ("food_cabbage", 1), ("food_carrot", 1) });
            yield return Itm("counter_fruit", "FRUIT BOWL", RecipeCategory.Cooking, "counter", "food_fruit", 1, "NO FIRE NEEDED", new[] { ("food_apple", 1), ("food_berries", 2) });
            yield return Itm("counter_trailmix", "TRAIL MIX", RecipeCategory.Cooking, "counter", "food_trailmix", 2, "KEEPS FOREVER", new[] { ("food_sunseeds", 2), ("food_berries", 1) });
        }

        static Recipe DyeR(string color, string desc, (string, int)[] items, params (ResourceType, int)[] res)
        {
            var r = Itm("dye_" + color, color.ToUpperInvariant() + " DYE", RecipeCategory.Supplies, "stove", "dye_" + color, 2, desc + "; PAINTS BUILT PIECES", items, res);
            r.fuel = ResourceType.Wood; r.fuelAmount = 1;
            return r;
        }
    }
}
