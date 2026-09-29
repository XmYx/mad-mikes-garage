using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes added by the roadmap systems (tools, furniture, clothing, attachments, refining, ...),
    /// grouped by system. Same helpers as the core library.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> Roadmap()
        {
            var S = ResourceType.Scrap; var G = ResourceType.Glass; var Rb = ResourceType.Rubber;
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

            // ---- 3. clothing (sewing table)
            var C = ResourceType.Cloth;
            yield return Sew("duster", "LEATHER DUSTER", "KEEPS RAIN OFF (60%), TOUGH", null, (C, 8), (Oil, 1));
            yield return Sew("poncho", "RAIN PONCHO", "KEEPS RAIN OFF (85%)", null, (C, 4), (Rb, 2));
            var hazmat = Sew("hazmat", "HAZMAT SUIT", "BLOCKS 60% RADIATION, WATERPROOF", null, (C, 4), (Rb, 4), (G, 1));
            hazmat.knowledge = "read_book_chemistry";
            yield return hazmat;
            yield return Sew("gasmask", "GAS MASK", "FILTERS DUST AND SMOKE, 30% RADIATION", null, (Rb, 2), (G, 1), (ResourceType.Charcoal, 1));
            yield return Sew("sweater", "WOOL SWEATER", "WARM", null, (C, 5));
            yield return Sew("overalls", "WORK OVERALLS", "TOUGH WORKWEAR", null, (C, 6), (Fe, 1));
            yield return Sew("cowboy", "COWBOY HAT", "SHADE FROM THE SUN", null, (C, 3));
            yield return Sew("bomber", "BOMBER JACKET", "VERY WARM, TOUGH", null, (C, 6), (Rb, 1));
            yield return Sew("shemagh", "SHEMAGH", "FILTERS DUST, KEEPS YOU COOL", null, (C, 2));
            yield return Sew("fingerless", "FINGERLESS GLOVES", "WARM HANDS, FREE FINGERS", null, (C, 1), (Rb, 1));
            yield return Sew("combat_boots", "COMBAT BOOTS", "VERY TOUGH", null, (Rb, 3), (C, 2), (Fe, 1));
            yield return Sew("welding_mask", "WELDING MASK", "WELD WITHOUT THE ARC FLASH", null, (S, 3), (G, 1));
            yield return Sew("skull_mask", "SKULL MASK", "RAIDERS MISTAKE YOU FOR KIN", new[] { ("trophy_skull", 1) }, (C, 1));
            yield return Sew("schoolbag", "SCHOOL BAG", "+8 KG CARRY", null, (C, 4));
            yield return Sew("hikingpack", "HIKING PACK", "+18 KG CARRY", null, (C, 8), (Rb, 1));
            yield return Sew("framepack", "FRAME PACK", "+28 KG CARRY", null, (C, 8), (ResourceType.Aluminium, 2));
            // ---- 4. vehicle attachments: ammunition for mounted weapons
            var mg = Itm("ammo_mg", "MG BELT X2", RecipeCategory.Weapons, "workbench", "ammo_mg", 2, "20 ROUNDS EACH FOR A ROOF MG", null, (Cu, 1), (S, 2));
            mg.knowledge = "read_book_gunsmith";
            yield return mg;
            yield return Itm("ammo_harpoon", "HARPOON BOLTS X2", RecipeCategory.Weapons, "workbench", "ammo_harpoon", 2, "BARBED BOLTS FOR THE HARPOON", null, (Fe, 2));
            yield return Itm("ammo_caltrops", "CALTROP BAG", RecipeCategory.Weapons, "workbench", "ammo_caltrops", 1, "FOR THE REAR DROPPER", null, (S, 3));
            yield return Itm("ammo_smoke", "SMOKE GRENADES X2", RecipeCategory.Weapons, "workbench", "ammo_smoke", 2, "FOR SMOKE DISCHARGERS", null, (ResourceType.Charcoal, 2), (ResourceType.Cloth, 1));

            yield return Itm("sewing_kit", "SEWING KIT", RecipeCategory.Supplies, "sewing", "use_sewing_kit", 1, "MEND A WORN GARMENT (+40%) ANYWHERE", null, (C, 2), (Fe, 1));
        }

        static Recipe Sew(string id, string name, string desc, (string, int)[] items, params (ResourceType, int)[] res) =>
            Itm("c_" + id, name, RecipeCategory.Clothing, "sewing", "cloth_" + id, 1, desc, items, res);

        static Recipe DyeR(string color, string desc, (string, int)[] items, params (ResourceType, int)[] res)
        {
            var r = Itm("dye_" + color, color.ToUpperInvariant() + " DYE", RecipeCategory.Supplies, "stove", "dye_" + color, 2, desc + "; PAINTS BUILT PIECES", items, res);
            r.fuel = ResourceType.Wood; r.fuelAmount = 1;
            return r;
        }
    }
}
