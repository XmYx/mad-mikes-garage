using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for depth stage D, the metalworking ladder. Forge and anvil (charcoal, coal stands in; slow, by
    /// hand): blister steel, nails, bolts, horseshoes, forged blades and tools, leaf-spring packs, crude armour. Furnace:
    /// steel (slow), sand-cast blanks and engine blocks. Arc furnace: steel fast. Machine shop (powered, finer make):
    /// gearbox, transfer case, brake, suspension and tank kits for the tuning bench, the forged V8, exhausts,
    /// radiators, sloped plate. Garage: the new lamps and the ducktail. Parts with a recipe here are made only where it
    /// says (the garage's generated part recipes skip them).</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> MetalRecipes()
        {
            const string Nails = MetalItems.Nails, Bolts = MetalItems.Bolts, Castings = MetalItems.Castings, EngineBlock = MetalItems.EngineBlock, Horseshoes = MetalItems.Horseshoes;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var Fe = ResourceType.Iron; var Stl = ResourceType.Steel; var Ch = ResourceType.Charcoal;
            var Cu = ResourceType.Copper; var Al = ResourceType.Aluminium; var Rb = ResourceType.Rubber; var G = ResourceType.Glass; var Oil = ResourceType.Oil;
            var Sand = ResourceType.Sand; var T = RecipeCategory.Tools; var Wp = RecipeCategory.Weapons; var Su = RecipeCategory.Supplies; var At = RecipeCategory.Attachments;

            // ---- forge and anvil: by hand, one charcoal (or coal) per heat
            var blister = Res("forge_steel", "BLISTER STEEL", RecipeCategory.Smelting, "forge", Stl, 1, "IRON PACKED IN CHARCOAL, HAMMERED OUT: SLOW", (Fe, 2), (Ch, 2));
            yield return MetalHot(blister, 60f);
            yield return MetalHot(Itm("forge_nails", "NAILS X12", Su, "forge", Nails, 12, "CUT AND HEADED ON THE ANVIL", null, (Fe, 1)), 20f);
            yield return MetalHot(Itm("forge_bolts", "BOLTS X6", Su, "forge", Bolts, 6, "HAND-CUT THREADS (THE MACHINE SHOP MAKES 12 FROM STEEL)", null, (Fe, 2)), 30f);
            yield return MetalHot(Itm("forge_horseshoes", "HORSESHOES", Su, "forge", Horseshoes, 1, "A SET OF FOUR: USE BESIDE YOUR HORSE (GALLOPS LONGER)", new[] { (Nails, 8) }, (Fe, 2)), 35f);
            yield return MetalHot(Itm("forge_knife", "FORGED KNIFE", Wp, "forge", "tool_knife", 1, "STEEL BLADE, OAK GRIP", null, (Stl, 1), (W, 1)), 30f);
            yield return MetalHot(Itm("forge_machete", "FORGED MACHETE", Wp, "forge", "tool_machete", 1, "STEEL, DRAWN OUT AND GROUND", null, (Stl, 2), (W, 1)), 40f);
            yield return MetalHot(Itm("forge_leaf_blade", "LEAF-SPRING BLADE", Wp, "forge", "tool_leaf_blade", 1, "AN OLD SPRING LEAF, REFORGED", null, (Stl, 2), (S, 2), (W, 1)), 50f);
            yield return MetalHot(Itm("forge_spear", "FORGED SPEAR", Wp, "forge", "tool_spear", 1, "STEEL HEAD ON A LONG SHAFT", null, (Stl, 1), (W, 3)), 30f);
            yield return MetalHot(Itm("forge_axe", "FORGED AXE", T, "forge", "tool_axe", 1, "STEEL BIT: HOLDS AN EDGE", null, (Stl, 2), (W, 2)), 40f);
            yield return MetalHot(Itm("forge_pickaxe", "FORGED PICKAXE", T, "forge", "tool_pickaxe", 1, "STEEL POINTS FOR ROCK AND ORE", null, (Stl, 3), (W, 2)), 45f);
            yield return MetalHot(Itm("forge_shovel", "FORGED SHOVEL", T, "forge", "tool_shovel", 1, "STEEL BLADE, IRON SOCKET", null, (Stl, 1), (Fe, 1), (W, 2)), 35f);
            yield return MetalHot(Itm("forge_hammer", "CLAW HAMMER", T, "forge", ItemIds.ClawHammer, 1, "BUILD AND DISMANTLE", null, (Fe, 1), (W, 1)), 25f);
            yield return MetalHot(Itm("forge_crowbar", "CROWBAR", T, "forge", "tool_crowbar", 1, "PRY DOORS, CRATES AND PANELS", null, (Stl, 2)), 30f);
            yield return MetalHot(Itm("forge_hd_springs", "LEAF-SPRING PACKS", At, "forge", "kit_suspension_hd", 1, "HEAVY-DUTY SUSPENSION KIT: FIT AT A TUNING BENCH (MECH 2)", new[] { (Bolts, 4) }, (Stl, 6), (Fe, 2)), 90f);
            yield return MetalHot(MetalPart("forge_window_mesh", "MESH WINDOW GUARDS", "armor_window_mesh", "forge", "ARMOUR: WELDED MESH OVER THE SIDE WINDOWS", 60f, null, new[] { (Bolts, 4) }, (Fe, 6), (S, 4)), 0f);
            yield return MetalHot(MetalPart("forge_spiked_skirts", "SPIKED SIDE SKIRTS", "armor_skirt_spiked", "forge", "ARMOUR: SILL SKIRTS BRISTLING WITH SPIKES", 75f, null, new[] { (Bolts, 6) }, (Fe, 8), (Stl, 2), (S, 6)), 0f);

            // ---- furnace (slow, charcoal fired) and arc furnace (fast, power)
            var steel = Res("s_furnace_steel", "STEEL", RecipeCategory.Smelting, "furnace", Stl, 2, "IRON AND CHARCOAL: SLOW (COAL FIRES IT TOO)", (Fe, 3), (Ch, 1));
            steel.fuel = Ch; steel.fuelAmount = 2; steel.seconds = 75f;
            yield return steel;
            var coke = Res("s_furnace_steel_coal", "STEEL (COKE)", RecipeCategory.Smelting, "furnace", Stl, 2, "IRON AND COAL: SLOW", (Fe, 3), (ResourceType.Coal, 1));
            coke.fuel = Ch; coke.fuelAmount = 2; coke.seconds = 75f;
            yield return coke;
            var cast = Itm("s_furnace_castings", "ROUGH CASTINGS X2", Su, "furnace", Castings, 2, "SAND-CAST BLANKS FOR THE MACHINE SHOP", null, (Stl, 2), (Sand, 2));
            cast.category = RecipeCategory.Smelting; cast.fuel = Ch; cast.fuelAmount = 1; cast.seconds = 60f;
            yield return cast;
            var block = Itm("s_furnace_engine_block", "CAST ENGINE BLOCK", Su, "furnace", EngineBlock, 1, "A V8 BLOCK IN SAND: THE MACHINE SHOP BORES IT", null, (Stl, 8), (Al, 4), (Sand, 4));
            block.category = RecipeCategory.Smelting; block.fuel = Ch; block.fuelAmount = 3; block.seconds = 120f;
            yield return block;
            var arc = Res("s_arc_furnace_steel", "STEEL", RecipeCategory.Smelting, "arc_furnace", Stl, 3, "NEEDS POWER: FAST", (Fe, 3), (Ch, 1));
            arc.seconds = 30f;
            yield return arc;
            var arcScrap = Res("s_arc_furnace_steel_scrap", "SCRAP TO STEEL", RecipeCategory.Smelting, "arc_furnace", Stl, 2, "NEEDS POWER: MELTS DOWN SCRAP", (S, 8), (Ch, 1));
            arcScrap.seconds = 35f;
            yield return arcScrap;

            // ---- machine shop: kits for the tuning bench, precise tools, the parts it alone makes
            yield return MetalTimed(Itm("ms_bolts", "BOLTS X12", Su, "machine_shop", Bolts, 12, "THREADED ON THE LATHE", null, (Stl, 1)), 15f);
            yield return MetalTimed(Itm("ms_gearbox_close", "CLOSE-RATIO GEARBOX", At, "machine_shop", "kit_gearbox_close", 1, "GAPS CLOSED: EVERY GEAR PULLS HARDER, TOP SPEED LOWER. TUNING BENCH (MECH 3)", new[] { (Castings, 2), (Bolts, 6) }, (Stl, 4), (Oil, 1)), 120f);
            yield return MetalTimed(Itm("ms_gearbox_wide", "WIDE-RATIO GEARBOX", At, "machine_shop", "kit_gearbox_wide", 1, "CRAWLER FIRST, OVERDRIVE TOP: TOWS AND CRUISES. TUNING BENCH (MECH 3)", new[] { (Castings, 2), (Bolts, 6) }, (Stl, 4), (Oil, 1)), 120f);
            var transfer = Itm("ms_transfer", "TRANSFER CASE (4X4)", At, "machine_shop", "kit_transfer_case", 1, "SELECTABLE 4WD FOR A TWO-WHEEL-DRIVE VEHICLE. TUNING BENCH (MECH 4)", new[] { (Castings, 3), (Bolts, 8) }, (Stl, 6), (Oil, 2));
            transfer.knowledge = "k_truck_parts";
            yield return MetalTimed(transfer, 150f);
            yield return MetalTimed(Itm("ms_brakes", "HEAVY-DUTY BRAKE KIT", At, "machine_shop", "kit_brakes_hd", 1, "BIG DISCS, FOUR-POT CALIPERS: +35% STOPPING. TUNING BENCH (MECH 2)", new[] { (Castings, 2) }, (Stl, 3), (Cu, 2), (Rb, 1)), 90f);
            yield return MetalTimed(Itm("ms_lift", "LIFT KIT", At, "machine_shop", "kit_lift", 1, "+10 CM, LONG-TRAVEL COILS FOR ROCKS AND MUD. TUNING BENCH (MECH 2)", new[] { (Bolts, 8) }, (Stl, 6), (Rb, 2)), 90f);
            yield return MetalTimed(Itm("ms_lowering", "LOWERING KIT", At, "machine_shop", "kit_lowering", 1, "-5 CM, SHORT STIFF SPRINGS FOR THE ROAD. TUNING BENCH (MECH 2)", new[] { (Bolts, 6) }, (Stl, 4), (Al, 2)), 75f);
            yield return MetalTimed(Itm("ms_tank", "LONG-RANGE TANK", At, "machine_shop", "kit_long_range_tank", 1, "HALF AS MUCH FUEL AGAIN (30 L AT LEAST). TUNING BENCH (MECH 1)", null, (Al, 8), (Stl, 2), (Rb, 2)), 90f);
            yield return MetalTimed(Itm("ms_wrench", "PRECISION WRENCH", T, "machine_shop", ItemIds.Wrench, 1, "MILLED FROM STEEL BAR", null, (Stl, 2)), 25f);
            yield return MetalTimed(Itm("ms_cutter", "SALVAGE CUTTER", T, "machine_shop", ItemIds.Cutter, 1, "STRIP WRECKS FOR SCRAP", null, (Stl, 4), (Cu, 2), (Rb, 2)), 45f);
            yield return MetalPart("ms_engine_v8_forged", "FORGED V8", "engine_v8_forged", "machine_shop", "ENGINE: BORED BLOCK, FORGED CRANK, EIGHT STACKS. REVS TO 7800", 240f, "k_truck_parts", new[] { (EngineBlock, 1), (Bolts, 12) }, (Stl, 10), (Al, 4), (Cu, 6), (Rb, 2));
            yield return MetalPart("ms_twin_chrome", "TWIN CHROME TAILPIPES", "exhaust_twin_chrome", "machine_shop", "EXHAUST: MUFFLER AND TWO POLISHED PIPES", 60f, null, new[] { (Bolts, 2) }, (Stl, 4), (S, 4));
            yield return MetalPart("ms_flame_stack", "FLAME-SPITTER STACK", "exhaust_flame_stack", "machine_shop", "EXHAUST: POPS FLAMES ON THE OVERRUN AND UNDER NITROUS", 60f, null, new[] { (Bolts, 2) }, (Stl, 5), (Cu, 1));
            yield return MetalPart("ms_radiator_bigcore", "BIG-CORE RADIATOR", "radiator_bigcore", "machine_shop", "RADIATOR: TWO ROWS, TWIN FANS, +50% COOLING", 75f, "k_radiator", null, (Cu, 8), (Al, 4), (Stl, 2));
            yield return MetalPart("ms_radiator_oil", "RADIATOR + OIL COOLER", "radiator_oil_cooler", "machine_shop", "RADIATOR: +30% COOLING, THE OIL LASTS NEARLY TWICE AS LONG", 75f, "k_radiator", null, (Cu, 6), (Al, 3), (Stl, 2), (Rb, 1));
            yield return MetalPart("ms_sloped_plate", "SLOPED STEEL PLATE", "armor_sloped", "machine_shop", "ARMOUR: ANGLED PLATE, SOAKS THE MOST", 90f, null, new[] { (Bolts, 8) }, (Stl, 12));

            // ---- garage: lamps and the ducktail
            yield return MetalPart("g_led_bar", "LED ROOF BAR", "lights_led_bar", "garage", "LIGHTS: FOUR WIDE FLOODS", 45f, null, null, (Al, 2), (G, 2), (Cu, 3), (Stl, 1));
            yield return MetalPart("g_fog_pods", "FOG LAMP PODS", "lights_fog", "garage", "LIGHTS: AMBER, WIDE AND LOW FOR FOG AND DUST", 40f, null, null, (G, 3), (Cu, 2), (Stl, 1));
            yield return MetalPart("g_search_pod", "SEARCHLIGHT POD", "lights_search_pod", "garage", "LIGHTS: ONE LONG BEAM THAT FOLLOWS YOUR AIM", 60f, null, null, (Stl, 3), (G, 3), (Cu, 3), (Al, 1));
            yield return MetalPart("g_ducktail", "DUCKTAIL SPOILER", "spoiler_ducktail", "garage", "SPOILER: A KICKED-UP LIP ON THE BOOT", 30f, null, null, (Al, 3), (S, 2));
        }

        /// <summary>A forge job: one charcoal per heat (wood won't do; coal stands in).</summary>
        static Recipe MetalHot(Recipe r, float seconds)
        {
            r.fuel = ResourceType.Charcoal; r.fuelAmount = 1;
            if (seconds > 0f) r.seconds = seconds;
            return r;
        }

        static Recipe MetalTimed(Recipe r, float seconds) { r.seconds = seconds; return r; }

        static Recipe MetalPart(string id, string name, string part, string station, string desc, float seconds, string knowledge, (string, int)[] items, params (ResourceType, int)[] res) =>
            new Recipe
            {
                id = id, name = name, category = RecipeCategory.Vehicles, kind = OutputKind.Part, output = part, station = station, description = desc,
                seconds = seconds, knowledge = knowledge, items = items ?? new (string, int)[0], resources = res
            };
    }

    /// <summary>Item ids of the metalworking ladder.</summary>
    public static class MetalItems
    {
        public const string Nails = "misc_nails", Bolts = "misc_bolts", Castings = "misc_castings", EngineBlock = "misc_engine_block", Horseshoes = "use_horseshoes";

        /// <summary>Carried weight (kg) of these items; 0 = not one of them.</summary>
        public static float Weight(string id) => id == Nails ? 0.02f : id == Bolts ? 0.05f : id == Castings ? 3f : id == EngineBlock ? 45f : id == Horseshoes ? 1.6f : 0f;
    }
}
