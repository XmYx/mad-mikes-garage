using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for depth stage E. Feed: hay and grain mixed by hand at the workbench, in bigger batches at the
    /// feed mill (which also grinds flour and chicken mash). Textiles: the spinning wheel spins wool and cotton into
    /// thread, felts wool into cloth and twists hemp (or thread) into rope; the loom weaves thread; the sewing table knits
    /// a wool sweater and a bee veil. Leather: bark tanning at the rack (no lime), then boots, belts, armour, chaps, a
    /// satchel, saddlebags and a saddle at the leather bench. Bees: honey jars at the workbench, honey salve for animals, mead at
    /// the still. Tools: scythe and shears.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> HusbandryRecipes()
        {
            var W = ResourceType.Wood; var Fe = ResourceType.Iron; var C = ResourceType.Cloth; var G = ResourceType.Glass;
            var Hay = ResourceType.Hay; var Feed = ResourceType.Feed; var Wool = ResourceType.Wool; var Th = ResourceType.Thread;
            var Le = ResourceType.Leather; var Hon = ResourceType.Honey; var Wax = ResourceType.Beeswax;
            var Farm = RecipeCategory.Farming; var Sup = RecipeCategory.Supplies; var Clo = RecipeCategory.Clothing;

            // ---- tools
            yield return Itm("scythe", "SCYTHE", RecipeCategory.Tools, "workbench", "tool_scythe", 1, "CUTS GRASS AND RIPE GRAIN INTO HAY", null, (Fe, 2), (W, 3));
            yield return Itm("shears", "SHEARS", RecipeCategory.Tools, "workbench", "tool_shears", 1, "SHEAR SHEEP AND GOATS FOR WOOL", null, (Fe, 2));

            // ---- feed: by hand at the workbench, three times the batch at the feed mill
            foreach (var (grain, name) in new[] { ("crop_wheat", "WHEAT"), ("food_corn", "CORN") })
            {
                var hand = Res("feed_" + grain, "ANIMAL FEED X4 (" + name + ")", Farm, "workbench", Feed, 4, "HAY AND " + name + " MIXED BY HAND: BETTER THAN RAW CROPS IN A TROUGH", (Hay, 3));
                hand.items = new[] { (grain, 2) }; hand.seconds = 20f;
                yield return hand;
                var mill = Res("mill_feed_" + grain, "ANIMAL FEED X12 (" + name + ")", Farm, "feedmill", Feed, 12, "MILLED HAY AND " + name, (Hay, 6));
                mill.items = new[] { (grain, 4) }; mill.seconds = 20f;
                yield return mill;
            }
            var beet = Res("mill_feed_beet", "ANIMAL FEED X9 (BEET)", Farm, "feedmill", Feed, 9, "HAY AND BEET PULP", (Hay, 4));
            beet.items = new[] { ("food_beet", 3) }; beet.seconds = 20f;
            yield return beet;
            var mash = Res("mill_mash", "CHICKEN MASH X5", Farm, "feedmill", Feed, 5, "GROUND GRAIN AND BONE: HENS LAY MORE");
            mash.items = new[] { ("crop_wheat", 2), ("misc_bone", 1) }; mash.seconds = 15f;
            yield return mash;
            var flour = Itm("mill_flour", "FLOUR X4 (MILL)", RecipeCategory.Cooking, "feedmill", "crop_flour", 4, "THE MILL GRINDS FINER AND FASTER THAN A QUERN", new[] { ("crop_wheat", 4) });
            flour.seconds = 12f;
            yield return flour;

            // ---- spinning wheel, loom, sewing
            yield return HusbandryTimed(Res("spin_wool", "SPIN WOOL: THREAD X3", Sup, "spinning", Th, 3, "A FLEECE SPUN INTO YARN", (Wool, 2)), 20f);
            var cotton = Res("spin_cotton", "SPIN COTTON: THREAD X3", Sup, "spinning", Th, 3, "COTTON BOLLS SPUN INTO THREAD");
            cotton.items = new[] { ("crop_cotton", 2) }; cotton.seconds = 20f;
            yield return cotton;
            yield return HusbandryTimed(Itm("spin_rope", "TWIST ROPE X2 (HEMP)", Sup, "spinning", "misc_rope", 2, "HEMP LAID INTO ROPE: SNARES, THE STABLE", new[] { ("crop_hemp", 3) }), 20f);
            yield return HusbandryTimed(Itm("spin_rope_thread", "TWIST ROPE (THREAD)", Sup, "spinning", "misc_rope", 1, "THREAD PLIED INTO A ROPE", null, (Th, 4)), 20f);
            yield return HusbandryTimed(Res("felt", "FELT CLOTH X2", Sup, "spinning", C, 2, "WOOL WETTED AND BEATEN INTO CLOTH", (Wool, 3), (ResourceType.Water, 1)), 25f);
            yield return HusbandryTimed(Res("loom_thread", "WEAVE CLOTH X3 (THREAD)", Sup, "loom", C, 3, "THREAD ON THE LOOM", (Th, 3)), 25f);
            yield return Itm("knit_sweater", "KNIT WOOL SWEATER", Clo, "sewing", "cloth_sweater", 1, "WARM AS A FIRE", null, (Th, 3), (Wool, 2));
            yield return Itm("c_bee_veil", "BEEKEEPER'S VEIL", Clo, "sewing", "cloth_bee_veil", 1, "HAT AND NETTING: THE BEES CAN'T STING", null, (C, 2), (Th, 1));

            // ---- leather: bark tanning, then the leather bench
            yield return HusbandryTimed(Res("leather_bark", "LEATHER X2 (BARK TAN)", Sup, "tanning", Le, 2, "HIDES SOAKED IN BARK: NO LIME, SLOWER", (ResourceType.Hide, 2), (W, 3)), 90f);
            yield return Itm("l_boots", "LEATHER BOOTS", Clo, "leather", "cloth_leather_boots", 1, "TOUGH AND WARM, SOFTEN FALLS", null, (Le, 3), (Th, 1));
            yield return Itm("l_work_belt", "WORK BELT", Clo, "leather", "cloth_work_belt", 1, "POUCHES: +5 KG CARRY", null, (Le, 2), (Fe, 1));
            yield return Itm("l_gun_belt", "GUN BELT AND HOLSTER", Clo, "leather", "cloth_gun_belt", 1, "CARTRIDGE LOOPS AND A HOLSTER: +3 KG", null, (Le, 3), (Fe, 1));
            yield return Itm("l_cuirass", "LEATHER CUIRASS", Clo, "leather", "cloth_leather_cuirass", 1, "BOILED LEATHER: BLADES AND BITES; 3 KG, QUIET", null, (Le, 6), (Th, 2));
            yield return Itm("l_chaps", "RIDING CHAPS", Clo, "leather", "cloth_leather_chaps", 1, "LEGS AGAINST THORNS, TEETH AND FALLS", null, (Le, 4), (Th, 1));
            yield return Itm("l_satchel", "LEATHER SATCHEL", Clo, "leather", "cloth_leather_satchel", 1, "A BAG ON THE BACK: +14 KG", null, (Le, 3), (Th, 1));
            yield return HusbandryTimed(Itm("l_saddlebags", "LEATHER SADDLEBAGS", RecipeCategory.Tools, "leather", "misc_saddlebags", 1, "FIT TO A SADDLED HORSE ([E]): ITS BAGS HOLD 90 KG", null, (Le, 4), (Th, 2), (Fe, 1)), 30f);
            yield return HusbandryTimed(Itm("l_saddle", "SADDLE (LEATHER BENCH)", RecipeCategory.Tools, "leather", "use_saddle", 1, "RIDE A TAMED HORSE ([E] ON IT)", null, (Le, 3), (Th, 2), (Fe, 1)), 30f);

            // ---- bees
            yield return Itm("honey_jar", "JARS OF HONEY X2", RecipeCategory.Cooking, "workbench", "food_honey", 2, "KEEPS FOREVER", null, (Hon, 3), (G, 1));
            yield return Itm("vet_salve", "HONEY SALVE X2", Sup, "workbench", "vet_salve", 2, "FOR ANIMALS: STOPS BLEEDING AND HEALS ([E] ON A HURT ONE)", new[] { ("food_herbs", 1) }, (Hon, 1), (Wax, 1));
            var mead = Itm("brew_mead", "MEAD X3", RecipeCategory.Cooking, "still", "drink_mead", 3, "HONEY WINE, BREWED SLOW", null, (Hon, 4), (ResourceType.Water, 3));
            mead.seconds = 40f;
            yield return mead;
        }

        static Recipe HusbandryTimed(Recipe r, float seconds) { r.seconds = seconds; return r; }
    }
}
