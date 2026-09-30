using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for depth stages G and H. Medicine ladder: herbal poultice and willow-bark tea at the campfire →
    /// the first-aid kit at the workbench → drugs at the chemistry lab (already there) → the clinic pieces. Mining ladder:
    /// the gold pan (by hand) → sluice box and furnace (gold) → the powered stamp mill, whose concentrates smelt to half
    /// again as much metal (gold: twice) on less charcoal; gold rings as a trade good.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> MedMineRecipes()
        {
            var Wa = ResourceType.Water; var C = ResourceType.Cloth; var W = ResourceType.Wood; var Ch = ResourceType.Charcoal;
            var Fe = ResourceType.Iron; var S = ResourceType.Scrap; var Au = ResourceType.Gold;

            // ---- stage G: medicine
            var poultice = Fire("poultice", "HERBAL POULTICE", "med_poultice", 1, new[] { (C, 1), (Wa, 1) }, ("food_herbs", 2));
            poultice.category = RecipeCategory.Supplies;
            poultice.description = "MASHED HERBS IN A CLOTH: BINDS A WOUND, DRAWS IT (1 WOOD)";
            yield return poultice;
            var tea = Fire("bark_tea", "WILLOW-BARK TEA", "drink_bark_tea", 1, new[] { (W, 1), (Wa, 1) }, ("food_herbs", 1));
            tea.category = RecipeCategory.Supplies;
            tea.description = "BITTER BARK BREW: DULLS THE PAIN FOR 90 MINUTES (1 WOOD)";
            yield return tea;
            yield return Itm("firstaid", "FIRST AID KIT", RecipeCategory.Supplies, "workbench", "med_firstaid", 1, "SPLINTS, DISINFECTS AND DRESSES EVERY WOUND AT ONCE",
                new[] { ("med_bandage", 2), ("med_disinfectant", 1), ("med_splint", 1) }, (C, 1));

            // ---- stage H: mining
            yield return Itm("gold_pan", "GOLD PAN", RecipeCategory.Tools, "workbench", "tool_gold_pan", 1, "PAN RIVER GRAVEL FOR GOLD (AND SAND)", null, (Fe, 1), (S, 2));
            yield return Smelt("gold", "GOLD", "furnace", Au, 1, (ResourceType.GoldOre, 3), (Ch, 1));
            yield return Smelt("gold", "GOLD", "arc_furnace", Au, 1, (ResourceType.GoldOre, 3));
            foreach (var (key, ore, metal, name, fromThree) in new[]
            {
                ("iron", ResourceType.IronOre, ResourceType.Iron, "IRON", 3), ("copper", ResourceType.CopperOre, ResourceType.Copper, "COPPER", 3),
                ("gold", ResourceType.GoldOre, Au, "GOLD", 2),
            })
            {
                var stamp = Itm("stamp_" + key, "STAMP " + name + " ORE", RecipeCategory.Refining, "stamp_mill", "misc_" + key + "_concentrate", 3,
                    "CRUSHED AND WASHED: SMELTS RICHER (NEEDS POWER)", null, (ore, 3));
                stamp.seconds = 20f;
                yield return stamp;
                var smelt = Smelt(key + "_conc", name + " FROM CONCENTRATE", "furnace", metal, fromThree, (Ch, 1));
                smelt.items = new[] { ("misc_" + key + "_concentrate", 3) };
                yield return smelt;
            }
            var ring = Itm("gold_ring", "GOLD RING", RecipeCategory.Supplies, "workbench", "misc_gold_ring", 1, "A TRADE GOOD: ANY TRADER PAYS WELL", null, (Au, 1));
            ring.seconds = 25f;
            yield return ring;
        }
    }
}
