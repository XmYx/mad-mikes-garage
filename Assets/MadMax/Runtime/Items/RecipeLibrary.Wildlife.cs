using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>What the new wildlife gives the workshop (user additions): antivenom from venom sacs, the chitin vest,
    /// spider silk woven into cloth, armadillo-shell shin guards, a bearskin coat and fried bug skewers.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> Wildlife()
        {
            yield return Itm("antivenom", "ANTIVENOM X2", RecipeCategory.Supplies, "chemlab", "med_antivenom", 2, "WHAT BIT YOU CURES YOU: CLEARS VENOM SICKNESS", new[] { ("misc_venom", 1) }, (ResourceType.Ethanol, 1));
            yield return Itm("a_vest_chitin", "CHITIN VEST", RecipeCategory.Clothing, "workbench", "cloth_vest_chitin", 1, "BUG PLATES ON HIDE: LIGHT, TOUGH, QUIET", new[] { ("misc_chitin", 5) }, (ResourceType.Leather, 2));
            yield return Itm("a_shin_guards_shell", "SHIN GUARDS (SHELL)", RecipeCategory.Clothing, "workbench", "cloth_shin_guards", 1, "ARMADILLO SHELL, STRAPPED ON", new[] { ("misc_shell", 2) }, (ResourceType.Leather, 1));
            yield return Itm("c_coat_bear", "BEARSKIN COAT", RecipeCategory.Clothing, "sewing", "cloth_coat", 1, "THE WARMEST THING IN THE WASTES", new[] { ("trophy_bearskin", 1) }, (ResourceType.Cloth, 1));
            var silk = Res("loom_silk", "WEAVE CLOTH X3 (SILK)", RecipeCategory.Supplies, "loom", ResourceType.Cloth, 3, "SPIDER SILK: STRONGER THAN IT LOOKS");
            silk.items = new[] { ("misc_silk", 3) };
            yield return silk;
            // hard-hat diving (user additions)
            yield return Itm("a_dive_helmet", "BRASS DIVING HELMET", RecipeCategory.Clothing, "workbench", "cloth_dive_helmet", 1, "SEE AND BREATHE UNDER WATER (WITH AN AIR TANK)", null, (ResourceType.Copper, 10), (ResourceType.Glass, 2), (ResourceType.Iron, 2));
            yield return Itm("c_dive_suit", "CANVAS DIVING SUIT", RecipeCategory.Clothing, "sewing", "cloth_dive_suit", 1, "KEEPS THE COLD SEA OUT", null, (ResourceType.Rubber, 6), (ResourceType.Cloth, 8));
            yield return Itm("a_air_tank", "DIVER'S AIR TANK", RecipeCategory.Clothing, "workbench", "cloth_air_tank", 1, "3 MINUTES OF AIR: FILL AT A COMPRESSOR", null, (ResourceType.Iron, 6), (ResourceType.Rubber, 1), (ResourceType.Copper, 1));
            foreach (var st in new[] { "stove", "oven" })
                yield return Cook(st, "bugs", "FRIED BUG SKEWER", "food_bug_skewer", 1, st == "stove" ? ResourceType.Wood : ResourceType.None, ("food_bugmeat", 2));
        }
    }
}
