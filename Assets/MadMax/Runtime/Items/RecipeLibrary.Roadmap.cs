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
            var mg = Itm("ammo_mg", "MG BELT X2", RecipeCategory.Weapons, "gunsmith", "ammo_mg", 2, "20 ROUNDS EACH FOR A ROOF MG", null, (Cu, 1), (S, 1), (ResourceType.Gunpowder, 1));
            mg.knowledge = "read_book_gunsmith";
            yield return mg;
            yield return Itm("ammo_harpoon", "HARPOON BOLTS X2", RecipeCategory.Weapons, "workbench", "ammo_harpoon", 2, "BARBED BOLTS FOR THE HARPOON", null, (Fe, 2));
            yield return Itm("ammo_caltrops", "CALTROP BAG", RecipeCategory.Weapons, "workbench", "ammo_caltrops", 1, "FOR THE REAR DROPPER", null, (S, 3));
            yield return Itm("ammo_smoke", "SMOKE GRENADES X2", RecipeCategory.Weapons, "workbench", "ammo_smoke", 2, "FOR SMOKE DISCHARGERS", null, (ResourceType.Charcoal, 2), (ResourceType.Cloth, 1));

            // ---- 5. crafting: chemistry lab, tanning rack, smokehouse, loom
            var Eth = ResourceType.Ethanol; var Ch = ResourceType.Charcoal;
            var gp = Res("gunpowder", "GUNPOWDER X4", RecipeCategory.Supplies, "chemlab", ResourceType.Gunpowder, 4, "CHARCOAL, SULFUR, AND NITRE FROM COMPOST", (Ch, 2), (ResourceType.Sulfur, 1));
            gp.items = new[] { (ItemIds.Fertilizer, 1) };
            yield return gp;
            yield return Itm("antibiotics", "ANTIBIOTICS X2", RecipeCategory.Supplies, "chemlab", "med_antibiotics", 2, "CURES INFECTION AND FEVER", new[] { ("food_herbs", 2) }, (Eth, 1), (G, 1));
            yield return Itm("painkillers", "PAINKILLERS X3", RecipeCategory.Supplies, "chemlab", "med_painkillers", 3, "WOUNDS HAMPER LESS FOR 3 HOURS", new[] { ("food_herbs", 1) }, (Eth, 1));
            yield return Itm("fuel_additive", "FUEL ADDITIVE", RecipeCategory.Supplies, "chemlab", "use_fuel_additive", 1, "A TANK BURNS 25% LEANER", null, (Eth, 2), (Oil, 1));
            yield return Res("acid", "BATTERY ACID 2L", RecipeCategory.Supplies, "chemlab", ResourceType.Acid, 2, "SULFUR IN WATER", (ResourceType.Sulfur, 1), (ResourceType.Water, 2));
            var tan = Res("leather", "LEATHER X2", RecipeCategory.Supplies, "tanning", ResourceType.Leather, 2, "SCRAPED, LIMED AND CURED HIDE", (ResourceType.Hide, 2), (ResourceType.Lime, 1));
            tan.seconds = 60f;
            yield return tan;
            foreach (var (raw, done, name) in new[] { ("food_meat_raw", "food_meat_smoked", "SMOKED MEAT"), ("food_fish_raw", "food_fish_smoked", "SMOKED FISH") })
            {
                var sm = Itm("smoke_" + done, name + " X2", RecipeCategory.Cooking, "smokehouse", done, 2, "KEEPS FOREVER", new[] { (raw, 2) });
                sm.fuel = ResourceType.Wood; sm.fuelAmount = 1; sm.seconds = 45f;
                yield return sm;
            }
            var jerky = Itm("smoke_jerky", "JERKY X4", RecipeCategory.Cooking, "smokehouse", "food_jerky", 4, "DRY, SALTY, KEEPS FOREVER", new[] { ("food_meat_raw", 3) });
            jerky.fuel = ResourceType.Wood; jerky.fuelAmount = 1; jerky.seconds = 60f;
            yield return jerky;
            foreach (var st in new[] { "stove", "oven" })
            {
                var fuel = st == "stove" ? ResourceType.Wood : ResourceType.None;
                yield return Cook(st, "meat", "COOKED MEAT", "food_meat_cooked", 1, fuel, ("food_meat_raw", 1));
                yield return Cook(st, "fish", "GRILLED FISH", "food_fish_cooked", 1, fuel, ("food_fish_raw", 1));
            }
            foreach (var (fibre, name) in new[] { ("crop_cotton", "COTTON"), ("crop_hemp", "HEMP") })
            {
                var weave = Res("loom_" + fibre, "WEAVE CLOTH X3 (" + name + ")", RecipeCategory.Supplies, "loom", ResourceType.Cloth, 3, "FROM " + name + " FIBRE");
                weave.items = new[] { (fibre, 3) };
                yield return weave;
            }

            // ---- 6. refining: crude cuts, seed oil, biodiesel, scrap and lead, batteries, tar
            var Cr = ResourceType.CrudeOil; var Tar = ResourceType.Tar; var Dsl = ResourceType.Diesel; var Fuel = ResourceType.Fuel;
            var cut = Res("crack_petrol", "PETROL CUT 5L", RecipeCategory.Refining, "refinery", Fuel, 5, "10 L CRUDE: PETROL, SOME DIESEL, OIL AND TAR", (Cr, 10));
            cut.byproducts = new[] { (Dsl, 2), (Oil, 1), (Tar, 1) }; cut.fuel = ResourceType.Charcoal; cut.fuelAmount = 1; cut.seconds = 40f;
            yield return cut;
            var dcut = Res("crack_diesel", "DIESEL CUT 5L", RecipeCategory.Refining, "refinery", Dsl, 5, "10 L CRUDE: DIESEL, SOME PETROL, OIL AND TAR", (Cr, 10));
            dcut.byproducts = new[] { (Fuel, 2), (Oil, 1), (Tar, 1) }; dcut.fuel = ResourceType.Charcoal; dcut.fuelAmount = 1; dcut.seconds = 40f;
            yield return dcut;
            var lube = Res("lube_oil", "ENGINE OIL 4L", RecipeCategory.Refining, "refinery", Oil, 4, "HEAVY CUT FOR ENGINES", (Cr, 8));
            lube.byproducts = new[] { (Tar, 2) }; lube.fuel = ResourceType.Charcoal; lube.fuelAmount = 1; lube.seconds = 35f;
            yield return lube;
            foreach (var (seed, name) in new[] { ("food_sunseeds", "SUNFLOWER"), ("food_hempseed", "HEMP") })
            {
                var press = Res("press_" + seed, "SEED OIL 2L (" + name + ")", RecipeCategory.Refining, "press", ResourceType.SeedOil, 2, "PRESSED FROM " + name + " SEEDS");
                press.items = new[] { (seed, 4) };
                yield return press;
            }
            yield return Res("biodiesel", "BIODIESEL 4L", RecipeCategory.Fuel, "chemlab", Dsl, 4, "SEED OIL + ETHANOL: RUNS ANY DIESEL", (ResourceType.SeedOil, 4), (ResourceType.Ethanol, 1));
            yield return Res("asphalt_tar", "ASPHALT X5 (TAR)", RecipeCategory.Refining, "mixer", ResourceType.Asphalt, 5, "TAR BINDS BETTER THAN OIL", (ResourceType.Stone, 3), (ResourceType.Sand, 2), (Tar, 2));
            var scrap = Res("s_furnace_scrap", "IRON X2 (SCRAP)", RecipeCategory.Smelting, "furnace", ResourceType.Iron, 2, "MELT DOWN SCRAP", (S, 6));
            scrap.byproducts = new[] { (ResourceType.Slag, 1) }; scrap.fuel = ResourceType.Charcoal; scrap.fuelAmount = 1;
            yield return scrap;
            yield return Res("s_arc_furnace_scrap", "IRON X2 (SCRAP)", RecipeCategory.Smelting, "arc_furnace", ResourceType.Iron, 2, "MELT DOWN SCRAP (NEEDS POWER)", (S, 4));
            var lead = Res("s_furnace_lead", "LEAD X2", RecipeCategory.Smelting, "furnace", ResourceType.Lead, 2, "GALENA MELTS EASILY", (ResourceType.LeadOre, 2));
            lead.fuel = ResourceType.Charcoal; lead.fuelAmount = 1;
            yield return lead;
            yield return Itm("battery", "CAR BATTERY", RecipeCategory.Supplies, "workbench", "use_battery", 1, "LEAD-ACID: FLASHLIGHTS, BATTERY BANKS", null, (ResourceType.Lead, 2), (ResourceType.Acid, 1), (S, 1));

            // ---- 7. mining: explosives, prospecting
            yield return Itm("dynamite", "DYNAMITE X2", RecipeCategory.Weapons, "chemlab", "throw_dynamite", 2, "4 S FUSE: BREAKS ROCK, BLOWS CRATERS", new[] { (ItemIds.Paper, 1) }, (ResourceType.Gunpowder, 2), (ResourceType.Cloth, 1));
            yield return Itm("pipebomb", "PIPE BOMB", RecipeCategory.Weapons, "gunsmith", "throw_pipebomb", 1, "3 S FUSE: SHRAPNEL", null, (ResourceType.Gunpowder, 1), (S, 2));
            yield return Itm("detector", "METAL DETECTOR", RecipeCategory.Tools, "workbench", "tool_detector", 1, "BEEPS OVER BURIED ORE", null, (Cu, 3), (G, 1), (S, 2));

            // ---- 8-9. gardening and irrigation: garden tools, flour and bread, sugar, beet spirit
            var W = ResourceType.Wood;
            yield return Itm("hoe", "HOE", RecipeCategory.Tools, "workbench", "tool_hoe", 1, "CHOPS WEEDS, LOOSENS THE SOIL", null, (Fe, 1), (W, 2));
            yield return Itm("watering_can", "WATERING CAN", RecipeCategory.Tools, "workbench", "tool_watering_can", 1, "WATERS BEDS, DOUSES FIRES; DIP IN A LAKE", null, (S, 3));
            var flour = Itm("flour", "FLOUR X2", RecipeCategory.Cooking, "workbench", "crop_flour", 2, "WHEAT GROUND ON A HAND QUERN", new[] { ("crop_wheat", 3) });
            flour.seconds = 20f;
            yield return flour;
            yield return FuelR("ethanol_beet", "ETHANOL 4L (BEET)", ResourceType.Ethanol, 4, new[] { ("food_beet", 4) }, (Ch, 1));
            foreach (var st in new[] { "stove", "oven" })
            {
                var fuel = st == "stove" ? ResourceType.Wood : ResourceType.None;
                var bread = Cook(st, "bread", "BREAD X2", "food_bread", 2, fuel, ("crop_flour", 2));
                bread.resources = new[] { (Wa, 1) };
                yield return bread;
                yield return Cook(st, "sugar", "SUGAR X2", "food_sugar", 2, fuel, ("food_beet", 3));
                yield return Cook(st, "mushsoup", "MUSHROOM SOUP X2", "food_mushsoup", 2, fuel, ("food_mushroom", 3), ("food_herbs", 1));
                var porridge = Cook(st, "porridge", "WHEAT PORRIDGE X2", "food_porridge", 2, fuel, ("crop_wheat", 2));
                porridge.resources = new[] { (Wa, 1) };
                yield return porridge;
            }

            // ---- 10. fishing: rod, cut bait, dough balls, grilled glowfish
            yield return Itm("fishing_rod", "FISHING ROD", RecipeCategory.Tools, "workbench", "tool_fishing_rod", 1, "CAST, STRIKE ON A BITE, HOLD TO REEL", null, (W, 2), (S, 1), (C, 1));
            yield return Itm("bait_meat", "CUT BAIT X4", RecipeCategory.Supplies, "workbench", "bait_meat", 4, "CATFISH AND PIKE BITE ON MEAT", new[] { ("food_meat_raw", 1) });
            yield return Itm("bait_maggots", "CUT BAIT X2 (ROTTEN)", RecipeCategory.Supplies, "workbench", "bait_meat", 2, "ROTTEN SCRAPS STILL CATCH FISH", new[] { ("food_rotten", 1) });
            yield return Itm("bait_corn", "CORN DOUGH X4", RecipeCategory.Supplies, "workbench", "bait_corn", 4, "CARP LOVE IT", new[] { ("food_corn", 1) });
            yield return Itm("bait_bread", "CORN DOUGH X4 (BREAD)", RecipeCategory.Supplies, "workbench", "bait_corn", 4, "CARP LOVE IT", new[] { ("food_bread", 1) });
            foreach (var st in new[] { "stove", "oven" })
                yield return Cook(st, "glowfish", "GRILLED GLOWFISH", "food_glowfish_cooked", 1, st == "stove" ? ResourceType.Wood : ResourceType.None, ("food_fish_glow", 1));

            // ---- 12. body armour
            var Le = ResourceType.Leather;
            yield return Armr("vest_scrap", "SCRAP PLATE VEST", "workbench", "STOPS BLADES AND BUCKSHOT; 7 KG, CLANKS", (S, 10), (Le, 2));
            yield return Armr("vest_tyre", "TYRE-RUBBER VEST", "workbench", "SOAKS CRASHES AND BLOWS; QUIET", (Rb, 6), (C, 2));
            yield return Armr("arm_guards", "ARM GUARDS", "sewing", "LEATHER AND STUDS FOR THE ARMS", (Le, 3), (S, 2));
            yield return Armr("gauntlets", "GAUNTLETS", "workbench", "METAL HANDS AGAINST BLADES AND FIRE", (Fe, 2), (Le, 1));
            yield return Armr("shin_guards", "SHIN GUARDS", "workbench", "FOR FALLS AND KICKS", (S, 4), (Le, 1));
            yield return Armr("helmet", "SCRAP HELMET", "workbench", "A POT WITH A STRAP", (S, 4), (C, 1));

            // ---- 13. player weapons, ammunition, throwables
            var Gp = ResourceType.Gunpowder; var Pb = ResourceType.Lead;
            yield return Itm("w_spear", "SPEAR", RecipeCategory.Weapons, "workbench", "tool_spear", 1, "REACH: KEEPS THEM AT ARM'S LENGTH", null, (W, 2), (S, 1));
            yield return Itm("w_nail_bat", "NAIL BAT", RecipeCategory.Weapons, "workbench", "tool_nail_bat", 1, "HEAVY SWING, OPENS WOUNDS", null, (W, 2), (S, 1));
            yield return Itm("w_knife", "KNIFE", RecipeCategory.Weapons, "workbench", "tool_knife", 1, "FAST, BLEEDS THEM", null, (Fe, 1), (W, 1));
            yield return Itm("w_leaf_blade", "LEAF-SPRING BLADE", RecipeCategory.Weapons, "workbench", "tool_leaf_blade", 1, "A CAR SPRING GROUND TO AN EDGE", null, (Fe, 3), (ResourceType.Leather, 1));
            yield return Itm("w_slingshot", "SLINGSHOT", RecipeCategory.Weapons, "workbench", "tool_slingshot", 1, "SILENT; SHOOTS STONES", null, (W, 1), (Rb, 1));
            yield return Itm("w_bow", "BOW", RecipeCategory.Weapons, "workbench", "tool_bow", 1, "SILENT; ARROWS CAN BE PICKED UP", null, (W, 3), (C, 1));
            yield return Itm("w_crossbow", "CROSSBOW", RecipeCategory.Weapons, "workbench", "tool_crossbow", 1, "QUIET, HITS HARD, SLOW TO SPAN", null, (W, 3), (Fe, 2), (C, 1));
            var pistol = Itm("w_pipe_pistol", "PIPE PISTOL", RecipeCategory.Weapons, "gunsmith", "tool_pipe_pistol", 1, "ONE SHOT, JAMS", null, (Fe, 2), (S, 2), (W, 1));
            yield return pistol;
            var revolver = Itm("w_revolver", "REVOLVER", RecipeCategory.Weapons, "gunsmith", "tool_revolver", 1, "SIX ROUNDS, RELIABLE", null, (Fe, 4), (Cu, 1), (W, 1));
            revolver.knowledge = "read_book_gunsmith";
            yield return revolver;
            var rifle = Itm("w_bolt_rifle", "BOLT RIFLE", RecipeCategory.Weapons, "gunsmith", "tool_bolt_rifle", 1, "SCOPED, FIVE ROUNDS, LOUD", null, (Fe, 6), (W, 3), (Cu, 1), (G, 1));
            rifle.knowledge = "read_book_gunsmith";
            yield return rifle;
            yield return Itm("w_flare_gun", "FLARE GUN", RecipeCategory.Weapons, "gunsmith", "tool_flare_gun", 1, "LIGHT, SIGNAL, SETS DRY GRASS ALIGHT", null, (S, 2), (Cu, 1));
            yield return Itm("ammo_arrow", "ARROWS X4", RecipeCategory.Weapons, "workbench", "ammo_arrow", 4, "FOR THE BOW", null, (W, 1), (S, 1));
            yield return Itm("ammo_bolt", "BOLTS X4", RecipeCategory.Weapons, "workbench", "ammo_bolt", 4, "FOR THE CROSSBOW", null, (Fe, 1), (W, 1));
            yield return Itm("ammo_cartridge", "PISTOL ROUNDS X6", RecipeCategory.Weapons, "gunsmith", "ammo_cartridge", 6, "PISTOL AND REVOLVER", null, (Cu, 1), (Pb, 1), (Gp, 1));
            yield return Itm("ammo_rifle", "RIFLE ROUNDS X4", RecipeCategory.Weapons, "gunsmith", "ammo_rifle", 4, "FOR THE BOLT RIFLE", null, (Cu, 1), (Pb, 1), (Gp, 2));
            yield return Itm("ammo_flare", "FLARES X2", RecipeCategory.Weapons, "chemlab", "ammo_flare", 2, "RED LIGHT FOR HALF A MINUTE", null, (Gp, 1), (ResourceType.Sulfur, 1), (C, 1));
            yield return Itm("throw_smoke", "SMOKE BOMBS X2", RecipeCategory.Weapons, "chemlab", "throw_smoke", 2, "A CLOUD TO SLIP AWAY IN", null, (ResourceType.Charcoal, 2), (ResourceType.Sulfur, 1), (S, 1));
            yield return Itm("throw_rock", "THROWING ROCKS X2", RecipeCategory.Weapons, "workbench", "throw_rock", 2, "A KNOCK, AND A NOISE WHERE IT LANDS", null, (ResourceType.Stone, 1));

            yield return Itm("sewing_kit", "SEWING KIT", RecipeCategory.Supplies, "sewing", "use_sewing_kit", 1, "MEND A WORN GARMENT (+40%) ANYWHERE", null, (C, 2), (Fe, 1));
        }

        static Recipe Armr(string id, string name, string station, string desc, params (ResourceType, int)[] res) =>
            Itm("a_" + id, name, RecipeCategory.Clothing, station, "cloth_" + id, 1, desc, null, res);

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
