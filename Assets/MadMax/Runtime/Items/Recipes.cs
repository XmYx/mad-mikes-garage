using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Items
{
    public enum RecipeCategory { Tools, Weapons, Building, Attachments, Supplies, Clothing, Farming, Media, Cooking, Refining, Smelting, Fuel, Vehicles }
    public enum OutputKind { Item, Part, Resource, Vehicle }

    /// <summary>A crafting recipe. Inputs are resources and/or items; output is an inventory item, a physical
    /// vehicle part spawned at the bench, or a resource (e.g. coolant).</summary>
    public class Recipe
    {
        public string id, name, description;
        public RecipeCategory category;
        public (ResourceType type, int amount)[] resources = new (ResourceType, int)[0];
        public (string item, int amount)[] items = new (string, int)[0];
        public OutputKind kind;
        public string output;            // item id or part id
        public ResourceType outputResource;
        public int amount = 1;
        public (ResourceType type, int amount)[] byproducts;
        public float seconds = 2f;
        public string knowledge;         // must be learned (book, tape or research) before crafting
        public string station = "workbench";   // crafting station type that offers it
        public ResourceType fuel;        // burnt per craft at fire stations (None = no fuel)
        public int fuelAmount;
    }

    /// <summary>Item ids used across the game. Tools start with "tool_", build kits with "kit_", ammo with "ammo_".</summary>
    public static class ItemIds
    {
        public const string Sledgehammer = "tool_sledgehammer", Wrench = "tool_wrench", Cutter = "tool_cutter",
            PipeClub = "tool_pipe_club", Machete = "tool_machete", Shotgun = "tool_pipe_shotgun", Shells = "ammo_shells", ClawHammer = "tool_claw_hammer",
            WallKit = "kit_wall_scrap", BarricadeKit = "kit_barricade", ChestKit = "kit_chest", FloodlightKit = "kit_floodlight", TvKit = "kit_tv", Molotov = "throw_molotov",
            SolarKit = "kit_solar_panel", WindKit = "kit_wind_large", WaterWheelKit = "kit_water_turbine", Coil = "misc_coil", Blade = "misc_blade", SolarCell = "misc_solar_cell",
            Canteen = "use_canteen", Sponge = "use_sponge", Fertilizer = "farm_fertilizer", Pills = "med_pills", Paper = "misc_paper";

        /// <summary>Dye items by index (see <c>Pal.DyeRamp</c>); 0 = none.</summary>
        public static readonly string[] Dyes = { null, "dye_red", "dye_blue", "dye_green", "dye_yellow", "dye_black", "dye_white" };

        static readonly Dictionary<string, string> extraNames = new Dictionary<string, string>
        {
            { "bp_aviation", "BLUEPRINT: FLYING MACHINES" }, { "use_saddle", "SADDLE" }, { "animal_chick", "CHICK" }, { "animal_kid", "GOAT KID" }, { "animal_calf", "CALF" }, { "animal_piglet", "PIGLET" }, { "animal_puppy", "PUPPY" },
            { "misc_bone", "BONE" }, { "misc_feather", "FEATHER" }, { "trophy_tusks", "BOAR TUSKS" }, { "trophy_horns", "ANTELOPE HORNS" }, { "trophy_pelt", "WOLF PELT" },
            { "dye_red", "RED DYE" }, { "dye_blue", "BLUE DYE" }, { "dye_green", "GREEN DYE" }, { "dye_yellow", "YELLOW DYE" }, { "dye_black", "BLACK DYE" }, { "dye_white", "WHITE DYE" },
            { "bp_weapon_mg", "BLUEPRINT: ROOF MG" }, { "bp_weapon_flamer", "BLUEPRINT: FLAMETHROWER" }, { "bp_weapon_harpoon", "BLUEPRINT: HARPOON LAUNCHER" },
            { "bp_cargo_generator", "BLUEPRINT: ONBOARD GENERATOR" }, { "bp_lights_search", "BLUEPRINT: SEARCHLIGHT" }, { "bp_framepack", "BLUEPRINT: FRAME PACK" },
            { "use_battery", "CAR BATTERY" }, { "med_antibiotics", "ANTIBIOTICS" }, { "med_painkillers", "PAINKILLERS" }, { "use_fuel_additive", "FUEL ADDITIVE" }, { "use_sewing_kit", "SEWING KIT" },
            { "throw_dynamite", "DYNAMITE" }, { "throw_pipebomb", "PIPE BOMB" }, { "tool_detector", "METAL DETECTOR" },
            { "ammo_mg", "MG BELT (20)" }, { "ammo_harpoon", "HARPOON BOLT" }, { "ammo_caltrops", "CALTROP BAG" }, { "ammo_smoke", "SMOKE GRENADE" },
            { "bait_worms", "WORMS" }, { "bait_insects", "INSECTS" }, { "bait_meat", "CUT BAIT" }, { "bait_corn", "CORN DOUGH BAIT" }, { "tool_fishing_rod", "FISHING ROD" },
            { "trophy_fish", "MOUNTED FISH" }, { "trophy_fish_mutant", "MOUNTED MUTANT FISH" },
            { "tool_spear", "SPEAR" }, { "tool_nail_bat", "NAIL BAT" }, { "tool_knife", "KNIFE" }, { "tool_leaf_blade", "LEAF-SPRING BLADE" }, { "tool_slingshot", "SLINGSHOT" },
            { "tool_bow", "BOW" }, { "tool_crossbow", "CROSSBOW" }, { "tool_pipe_pistol", "PIPE PISTOL" }, { "tool_revolver", "REVOLVER" }, { "tool_bolt_rifle", "BOLT RIFLE" }, { "tool_flare_gun", "FLARE GUN" }, { "tool_grapple", "GRAPPLING HOOK" },
            { "ammo_arrow", "ARROW" }, { "ammo_bolt", "CROSSBOW BOLT" }, { "ammo_cartridge", "PISTOL ROUND" }, { "ammo_rifle", "RIFLE ROUND" }, { "ammo_flare", "FLARE" },
            { "throw_smoke", "SMOKE BOMB" }, { "throw_rock", "ROCK" },
            { "coin_chit", "GUILD CHIT" }, { "part:cargo_crate", "GUILD CRATE" },
            { "kit_turbo", "TURBO KIT" }, { "kit_supercharger", "SUPERCHARGER KIT" }, { "use_nitrous", "NITROUS BOTTLE" },
            { "use_o2_bottle", "O2 BOTTLE" }, { "bp_submarine", "BLUEPRINT: SUBMARINE" },
            { "trophy_antlers", "DEER ANTLERS" }, { "trophy_bearskin", "BEARSKIN" }, { "misc_shell", "ARMADILLO SHELL" }, { "misc_venom", "VENOM SAC" },
            { "misc_chitin", "CHITIN PLATE" }, { "misc_silk", "SPIDER SILK" }, { "med_antivenom", "ANTIVENOM" },
            { "misc_coil", "GENERATOR COIL" }, { "misc_blade", "TURBINE BLADE" }, { "misc_solar_cell", "SOLAR CELL" },
            { "kit_solar_panel", "SOLAR PANEL KIT" }, { "kit_wind_large", "LARGE WIND TURBINE KIT" }, { "kit_water_turbine", "WATER WHEEL KIT" },
            { "relic_block", "RELIC: V12 BLOCK" }, { "relic_heads", "RELIC: V12 HEADS" }, { "relic_crank", "RELIC: V12 CRANKSHAFT" }, { "relic_blower", "RELIC: TWIN BLOWERS" },
            { "misc_rope", "ROPE" }, { "animal_lamb", "LAMB" }, { "animal_rabbit", "LIVE RABBIT" }, { "vet_salve", "HONEY SALVE (ANIMALS)" },
            { "keepsake_badge", "CONVOY ENAMEL BADGE" }, { "misc_delivery_chit", "DELIVERY CHIT" }, { "misc_relay_module", "RELAY RECORDING MODULE" }, { "misc_receiver", "JUNE'S RECEIVER" }, { "evidence_receipt", "FUEL RECEIPT (EVIDENCE)" }, { "evidence_manifest", "FORGED MANIFEST (EVIDENCE)" }, { "trophy_plate", "LICENCE PLATE" }, { "trophy_ornament", "HOOD ORNAMENT" }, { "trophy_hubcap", "CHROME HUBCAP" }, { "trophy_skull", "BULL SKULL" },
        };

        /// <summary>Names for items added by quests and blocks without editing this table (<see cref="Register"/>).</summary>
        static readonly Dictionary<string, string> registered = new Dictionary<string, string>();
        public static void Register(string id, string name) => registered[id] = name;

        public static string Name(string id)
        {
            var food = FoodLibrary.Get(id);
            if (food != null) return food.name;
            if (extraNames.TryGetValue(id, out var extra)) return extra;
            if (registered.TryGetValue(id, out var reg)) return reg;
            if (id.StartsWith("story_") && MadMax.Story.StoryLibrary.All.Count > 0 && registered.TryGetValue(id, out reg)) return reg;   // quest items register when the catalogue is authored
            var seed = FoodLibrary.SeedName(id);
            if (seed != null) return seed;
            foreach (var r in RecipeLibrary.All) if (r.output == id && r.amount == 1) return r.name;
            if (id == Sledgehammer) return "SLEDGEHAMMER";
            if (id == Canteen) return "CANTEEN";
            if (id == Fertilizer) return "FERTILIZER";
            if (id == Pills) return "PILLS";
            if (id == Paper) return "PAPER";
            int us = id.IndexOf('_');
            return (us >= 0 ? id.Substring(us + 1) : id).Replace('_', ' ').ToUpperInvariant();
        }

        public static bool IsTool(string id) => id.StartsWith("tool_");
    }

    public static partial class RecipeLibrary
    {
        static List<Recipe> all;

        public static IReadOnlyList<Recipe> All => all ??= Build();

        /// <summary>Adds garage recipes for every vehicle part without one (cost from mass and category). Called once the part prefabs are known.</summary>
        public static void RegisterParts(IEnumerable<(string id, MadMax.Vehicles.PartCategory category, float mass, int size)> parts)
        {
            var list = (List<Recipe>)All;
            foreach (var (id, cat, mass, size) in parts)
            {
                bool exists = false;
                foreach (var r in list) if (r.kind == OutputKind.Part && r.output == id) { exists = true; break; }
                if (exists) continue;
                var res = new List<(ResourceType, int)>();
                int metal = Mathf.Max(2, Mathf.RoundToInt(mass / 10f));
                switch (cat)
                {
                    case MadMax.Vehicles.PartCategory.Engine:
                        res.Add((ResourceType.Iron, metal)); res.Add((ResourceType.Aluminium, Mathf.Max(2, metal / 4))); res.Add((ResourceType.Copper, 4)); res.Add((ResourceType.Rubber, 2)); break;
                    case MadMax.Vehicles.PartCategory.Wheel:
                        res.Add((ResourceType.Rubber, Mathf.Max(4, Mathf.RoundToInt(mass / 4f)))); res.Add((ResourceType.Iron, Mathf.Max(2, metal / 2))); break;
                    case MadMax.Vehicles.PartCategory.Radiator:
                        res.Add((ResourceType.Copper, Mathf.Max(3, metal / 2))); res.Add((ResourceType.Scrap, metal)); break;
                    case MadMax.Vehicles.PartCategory.Weapon:
                        res.Add((ResourceType.Iron, metal)); res.Add((ResourceType.Bronze, 4)); break;
                    default:
                        res.Add((ResourceType.Scrap, metal)); res.Add((ResourceType.Iron, Mathf.Max(1, metal / 3))); break;
                }
                // attachments with telling materials
                switch (id)
                {
                    case "cargo_generator": res.Add((ResourceType.Copper, 6)); res.Add((ResourceType.Aluminium, 2)); break;
                    case "cargo_water_tank": res.Clear(); res.Add((ResourceType.Rubber, 6)); res.Add((ResourceType.Scrap, 4)); break;
                    case "lights_bar": case "lights_search": res.Add((ResourceType.Glass, 2)); res.Add((ResourceType.Copper, 2)); break;
                    case "snorkel": res.Add((ResourceType.Rubber, 2)); break;
                    case "weapon_flamer": res.Add((ResourceType.Copper, 3)); break;
                }
                list.Add(new Recipe
                {
                    id = "part_" + id, name = id.Replace('_', ' ').ToUpperInvariant(), category = RecipeCategory.Vehicles, kind = OutputKind.Part,
                    output = id, description = cat.ToString().ToUpperInvariant() + " PART, SIZE " + size, resources = res.ToArray(), station = "garage",
                    knowledge = id == "weapon_mg" || id == "weapon_flamer" || id == "weapon_harpoon" || id == "cargo_generator" || id == "lights_search" ? "bp_" + id : null
                });
            }
        }

        /// <summary>Watercraft (user additions): name, what it is for, knowledge gate and cost; built at a slipway.</summary>
        static readonly Dictionary<string, (string name, string desc, string knowledge, (ResourceType, int)[] res)> Boats = new Dictionary<string, (string, string, string, (ResourceType, int)[])>
        {
            { "Raft", ("OIL-DRUM RAFT", "SIX DRUMS AND A DECK: PADDLE IT, OR CLAMP ON AN OUTBOARD", null, new[] { (ResourceType.Scrap, 30), (ResourceType.Wood, 24), (ResourceType.Cloth, 4) }) },
            { "Skiff", ("SCRAP SKIFF", "ALUMINIUM FISHING BOAT WITH AN OUTBOARD", null, new[] { (ResourceType.Aluminium, 24), (ResourceType.Scrap, 20), (ResourceType.Wood, 6), (ResourceType.Rubber, 2) }) },
            { "Trawler", ("RUST TRAWLER", "WORKBOAT WITH A WHEELHOUSE, FISH HOLD AND TRAWL NET [1]", "k_truck_parts", new[] { (ResourceType.Iron, 120), (ResourceType.Scrap, 90), (ResourceType.Glass, 10), (ResourceType.Cloth, 20), (ResourceType.Copper, 12), (ResourceType.Rubber, 12) }) },
            { "Houseboat", ("SHANTY BOAT", "A FLOATING SHACK TO LIVE ON: BED, STOVE, PORCH", null, new[] { (ResourceType.Scrap, 80), (ResourceType.Wood, 90), (ResourceType.Iron, 30), (ResourceType.Glass, 8) }) },
            { "IronEel", ("IRON EEL SUBMARINE", "DIVES ON BATTERIES, BREATHES THROUGH O2 BOTTLES; A BASE UNDER THE SEA", "bp_submarine", new[] { (ResourceType.Iron, 220), (ResourceType.Scrap, 120), (ResourceType.Copper, 40), (ResourceType.Glass, 16), (ResourceType.Rubber, 24), (ResourceType.Aluminium, 30) }) },
        };

        /// <summary>Garage recipes that build a whole vehicle (bare, fluids empty).</summary>
        public static void RegisterVehicles(IEnumerable<(string design, float mass)> vehicles)
        {
            var list = (List<Recipe>)All;
            foreach (var (design, mass) in vehicles)
            {
                if (list.Exists(r => r.output == design && r.kind == OutputKind.Vehicle)) continue;
                int m = Mathf.RoundToInt(mass / 25f);
                if (Boats.TryGetValue(design, out var boat))
                {
                    list.Add(new Recipe
                    {
                        id = "veh_" + design, name = boat.name, category = RecipeCategory.Vehicles, kind = OutputKind.Vehicle, output = design,
                        description = boat.desc + " (BUILT AT A SLIPWAY)", station = "slipway", knowledge = boat.knowledge, resources = boat.res
                    });
                    continue;
                }
                if (design == "Ultralight" || design == "Gyrocopter")
                {
                    list.Add(new Recipe
                    {
                        id = "veh_" + design, name = design.ToUpperInvariant(), category = RecipeCategory.Vehicles, kind = OutputKind.Vehicle, output = design,
                        description = "FLYING MACHINE, EMPTY TANK (NEEDS A HANGAR)", station = "hangar", knowledge = "bp_aviation",
                        resources = new[] { (ResourceType.Aluminium, 20), (ResourceType.Iron, m / 2), (ResourceType.Cloth, 16), (ResourceType.Rubber, 6), (ResourceType.Copper, 4) }
                    });
                    continue;
                }
                if (design == "Bicycle")
                {
                    list.Add(new Recipe
                    {
                        id = "veh_" + design, name = "BICYCLE", category = RecipeCategory.Vehicles, kind = OutputKind.Vehicle, output = design,
                        description = "PEDAL POWER: SILENT, NO FUEL, COSTS STAMINA", station = "workbench",
                        resources = new[] { (ResourceType.Iron, 6), (ResourceType.Rubber, 2), (ResourceType.Scrap, 4) }
                    });
                    continue;
                }
                list.Add(new Recipe
                {
                    id = "veh_" + design, name = design.ToUpperInvariant(), category = RecipeCategory.Vehicles, kind = OutputKind.Vehicle, output = design,
                    description = "COMPLETE VEHICLE, EMPTY TANKS", station = "garage", knowledge = "k_truck_parts",
                    resources = new[] { (ResourceType.Iron, m), (ResourceType.Scrap, m), (ResourceType.Aluminium, m / 4 + 4), (ResourceType.Rubber, 16), (ResourceType.Glass, 8), (ResourceType.Copper, 10) }
                });
            }
        }

        static Recipe R(string id, string name, RecipeCategory c, OutputKind kind, string output, string desc, params (ResourceType, int)[] res) =>
            new Recipe { id = id, name = name, category = c, kind = kind, output = output, description = desc, resources = res };

        static List<Recipe> Build()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var Rb = ResourceType.Rubber; var C = ResourceType.Cloth;
            var list = new List<Recipe>
            {
                R("claw", "CLAW HAMMER", RecipeCategory.Tools, OutputKind.Item, ItemIds.ClawHammer, "BUILD AND DISMANTLE", (W, 1), (S, 2)),
                R("wrench", "WRENCH", RecipeCategory.Tools, OutputKind.Item, ItemIds.Wrench, "REMOVE AND MOUNT VEHICLE PARTS", (S, 4)),
                R("cutter", "SALVAGE CUTTER", RecipeCategory.Tools, OutputKind.Item, ItemIds.Cutter, "STRIP WRECKS FOR SCRAP", (S, 8), (Rb, 2)),
                R("sledge", "SLEDGEHAMMER", RecipeCategory.Tools, OutputKind.Item, ItemIds.Sledgehammer, "DEMOLISH STRUCTURES", (S, 6), (W, 2)),

                R("club", "PIPE CLUB", RecipeCategory.Weapons, OutputKind.Item, ItemIds.PipeClub, "FAST MELEE", (S, 3)),
                R("machete", "MACHETE", RecipeCategory.Weapons, OutputKind.Item, ItemIds.Machete, "FAST CUTTING MELEE", (S, 5), (W, 1)),
                R("shotgun", "PIPE SHOTGUN", RecipeCategory.Weapons, OutputKind.Item, ItemIds.Shotgun, "8 PELLETS, NEEDS SHELLS", (S, 12), (W, 4)),
                new Recipe { id = "shells", name = "SHELLS X6", category = RecipeCategory.Weapons, kind = OutputKind.Item, output = ItemIds.Shells, amount = 6, description = "PIPE SHOTGUN AMMO", resources = new[] { (S, 2), (C, 1) } },

                R("wall", "SCRAP WALL", RecipeCategory.Building, OutputKind.Item, ItemIds.WallKit, "2 M WALL SECTION, PLACE WITH B", (S, 10)),
                R("barricade", "BARRICADE", RecipeCategory.Building, OutputKind.Item, ItemIds.BarricadeKit, "WOOD AND SPIKE BARRIER", (W, 6), (S, 2)),
                R("chest", "STORAGE CHEST", RecipeCategory.Building, OutputKind.Item, ItemIds.ChestKit, "LOCKABLE STORAGE", (W, 8), (S, 2)),
                R("floodlight", "FLOODLIGHT", RecipeCategory.Building, OutputKind.Item, ItemIds.FloodlightKit, "BRIGHT LAMP", (S, 4), (G, 2)),
                new Recipe { id = "molotov", name = "MOLOTOV", category = RecipeCategory.Weapons, kind = OutputKind.Item, output = ItemIds.Molotov, amount = 1, description = "THROWN FIRE BOMB", resources = new[] { (G, 1), (C, 1), (ResourceType.Fuel, 1) } },
                R("tv", "TV AND VCR", RecipeCategory.Building, OutputKind.Item, ItemIds.TvKit, "PLAYS VHS TAPES", (S, 6), (G, 3)),

                R("bullbar", "BULL BAR", RecipeCategory.Attachments, OutputKind.Part, "bumper_bull_bar", "FRONT BUMPER, SIZE 2", (S, 14)),
                R("carrier", "SPARE CARRIER", RecipeCategory.Attachments, OutputKind.Part, "rear_spare_carrier", "REAR BUMPER WITH SPARE", (S, 10), (Rb, 6)),
                R("rack", "JERRY RACK", RecipeCategory.Attachments, OutputKind.Part, "cargo_jerry_rack", "ROOF/CARGO RACK", (S, 8), (W, 2)),
                R("pipes", "SIDE PIPES", RecipeCategory.Attachments, OutputKind.Part, "exhaust_side_pipes", "EXHAUST", (S, 8)),
                R("stack", "EXHAUST STACK", RecipeCategory.Attachments, OutputKind.Part, "exhaust_stack", "TALL EXHAUST", (S, 10)),
                R("radiator", "RADIATOR", RecipeCategory.Attachments, OutputKind.Part, "radiator_car", "CAR RADIATOR, FIXES LEAKS", (S, 8)),
                R("radiator_t", "TRUCK RADIATOR", RecipeCategory.Attachments, OutputKind.Part, "radiator_truck", "HEAVY RADIATOR", (S, 16)),
                R("street", "STREET WHEEL", RecipeCategory.Attachments, OutputKind.Part, "wheel_street", "ROAD GRIP", (Rb, 6), (S, 3)),
                R("offroad", "OFFROAD WHEEL", RecipeCategory.Attachments, OutputKind.Part, "wheel_offroad", "MUD GRIP", (Rb, 8), (S, 4)),
                R("tanks", "TWIN FUEL TANKS", RecipeCategory.Attachments, OutputKind.Part, "cargo_twin_fuel_tanks", "CARGO", (S, 12)),
                R("turret", "CANNON TURRET", RecipeCategory.Attachments, OutputKind.Part, "weapon_turret_cannon", "ROOF WEAPON", (S, 40), (G, 2)),

                R("c_bandana", "BANDANA", RecipeCategory.Clothing, OutputKind.Item, "cloth_bandana", "DUST MASK", (C, 2)),
                R("c_goggles", "GOGGLES", RecipeCategory.Clothing, OutputKind.Item, "cloth_goggles", "SAND GOGGLES", (G, 1), (Rb, 1)),
                R("c_gloves", "GLOVES", RecipeCategory.Clothing, OutputKind.Item, "cloth_gloves", "WORK GLOVES", (C, 2), (Rb, 1)),
                R("c_helmet", "SCRAP HELMET", RecipeCategory.Clothing, OutputKind.Item, "cloth_helmet", "HEAD ARMOUR", (S, 6)),
                R("c_vest", "SCAV VEST", RecipeCategory.Clothing, OutputKind.Item, "cloth_vest", "POCKETED VEST", (C, 3), (S, 2)),
                R("c_tank", "TANK TOP", RecipeCategory.Clothing, OutputKind.Item, "cloth_tank", "LIGHT TOP", (C, 2)),
                R("c_jeans", "JEANS", RecipeCategory.Clothing, OutputKind.Item, "cloth_jeans", "DENIM", (C, 4)),
                R("c_boots", "BOOTS", RecipeCategory.Clothing, OutputKind.Item, "cloth_boots", "STURDY BOOTS", (Rb, 2), (C, 2)),
                R("c_shoulder", "SHOULDER ARMOUR", RecipeCategory.Clothing, OutputKind.Item, "cloth_shoulder", "LEFT PAULDRON", (S, 5)),
                new Recipe { id = "coolant", name = "COOLANT 5L", category = RecipeCategory.Supplies, kind = OutputKind.Resource, outputResource = ResourceType.Coolant, amount = 5, description = "MIX FROM SCAVENGED BOTTLES", resources = new[] { (G, 1), (C, 1) } },
            };
            list.AddRange(Extra());
            list.AddRange(Roadmap());
            list.AddRange(Wildlife());
            list.AddRange(Kitchen());
            list.AddRange(RoadsRecipes());
            list.AddRange(MetalRecipes());
            list.AddRange(HusbandryRecipes());
            list.AddRange(UtilitiesRecipes());
            list.AddRange(MedMineRecipes());
            list.AddRange(DefenceRecipes());
            list.AddRange(SeasonsRecipes());
            list.AddRange(LightsRecipes());
            list.AddRange(FluidsRecipes());
            list.AddRange(RangeBatches(list));
            foreach (var r in list)
            {
                if (r.category == RecipeCategory.Clothing && (r.station == null || r.station == "workbench")) r.station = "sewing";   // clothes at the sewing table (leather goods: the leather bench)
                // firearms and their ammunition at the gunsmith bench (melee and caltrops stay at the workbench)
                if (r.category == RecipeCategory.Weapons && r.output != null && (r.output == ItemIds.Shotgun || (r.output.StartsWith("ammo_") && r.output != "ammo_caltrops"))) r.station = "gunsmith";
            }
            return list;
        }

        static Recipe Res(string id, string name, RecipeCategory c, string station, ResourceType output, int amount, string desc, params (ResourceType, int)[] res) =>
            new Recipe { id = id, name = name, category = c, station = station, kind = OutputKind.Resource, outputResource = output, amount = amount, description = desc, resources = res };
        static Recipe Itm(string id, string name, RecipeCategory c, string station, string output, int amount, string desc, (string, int)[] items, params (ResourceType, int)[] res) =>
            new Recipe { id = id, name = name, category = c, station = station, kind = OutputKind.Item, output = output, amount = amount, description = desc, items = items ?? new (string, int)[0], resources = res };

        /// <summary>Garments with their own recipe (the rest get a generic "4 cloth" one).</summary>
        static readonly HashSet<string> SewnElsewhere = new HashSet<string>
        {
            "bandana", "goggles", "gloves", "helmet", "vest", "tank", "jeans", "boots", "shoulder", "coat",
            "duster", "poncho", "hazmat", "gasmask", "sweater", "overalls", "cowboy", "bomber", "shemagh", "fingerless", "combat_boots",
            "welding_mask", "skull_mask", "schoolbag", "hikingpack", "framepack",
            "leather_boots", "work_belt", "gun_belt", "leather_cuirass", "leather_chaps", "leather_satchel", "bee_veil",   // depth stage E (RecipeLibrary.Husbandry)
        };

        static IEnumerable<Recipe> Extra()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var Rb = ResourceType.Rubber; var C = ResourceType.Cloth; var St = ResourceType.Stone;
            var Ch = ResourceType.Charcoal; var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Wt = ResourceType.Water; var Dw = ResourceType.DirtyWater;
            // tools & survival
            yield return Itm("canteen", "CANTEEN", RecipeCategory.Tools, "workbench", ItemIds.Canteen, 1, "DRINK CARRIED WATER", null, (S, 2), (C, 1));
            yield return Itm("sponge", "SPONGE", RecipeCategory.Tools, "workbench", ItemIds.Sponge, 1, "SCRUB AWAY BLOOD STAINS (USE FROM THE HOTBAR)", null, (C, 2));
            yield return Itm("torch", "TORCH", RecipeCategory.Tools, null, "tool_torch", 1, "FLICKERING LIGHT; SETS DRY THINGS ALIGHT", null, (W, 1), (C, 1));
            yield return Itm("gas_torch", "GAS TORCH", RecipeCategory.Tools, "workbench", "tool_gas_torch", 1, "BLUE FLAME: LIGHT, CUTS METAL, BURNS FUEL", null, (Fe, 2), (Cu, 1));
            yield return Itm("lantern", "LANTERN", RecipeCategory.Tools, "workbench", "tool_lantern", 1, "STEADY HAND LIGHT", null, (S, 1), (G, 1));
            yield return Itm("shovel", "SHOVEL", RecipeCategory.Tools, "workbench", "tool_shovel", 1, "DIG SOIL, TILL PLOTS", null, (Fe, 2), (W, 2));
            yield return Itm("axe", "AXE", RecipeCategory.Tools, "workbench", "tool_axe", 1, "FELL TREES FAST", null, (Fe, 2), (W, 2));
            yield return Itm("pickaxe", "PICKAXE", RecipeCategory.Tools, "workbench", "tool_pickaxe", 1, "MINE ROCK AND ORE", null, (Fe, 3), (W, 2));
            yield return Itm("pills", "PILLS", RecipeCategory.Supplies, "workbench", ItemIds.Pills, 2, "CURE SICKNESS", new[] { ("food_herbs", 2) }, (G, 1));
            yield return Itm("paper", "PAPER X4", RecipeCategory.Supplies, "workbench", ItemIds.Paper, 4, "FOR BOOKS", null, (W, 2), (Wt, 2));
            yield return Itm("c_coat", "WINTER PARKA", RecipeCategory.Clothing, "workbench", "cloth_coat", 1, "VERY WARM", null, (C, 8), (ResourceType.Rubber, 1));
            yield return Itm("bandage", "BANDAGES X3", RecipeCategory.Supplies, "workbench", "med_bandage", 3, "STOPS BLEEDING", null, (C, 2));
            yield return Itm("splint", "SPLINT", RecipeCategory.Supplies, "workbench", "med_splint", 1, "SET A FRACTURE", null, (W, 2), (C, 1));
            yield return Itm("disinfect", "DISINFECTANT X2", RecipeCategory.Supplies, "workbench", "med_disinfectant", 2, "PREVENTS INFECTION", null, (ResourceType.Ethanol, 1), (G, 1));
            yield return Itm("bottle_water", "BOTTLED WATER", RecipeCategory.Supplies, "workbench", "drink_water", 2, "FILL BOTTLES", null, (G, 1), (Wt, 2));
            foreach (var c in MadMax.Game.ClothingLibrary.All)
            {
                var item = MadMax.Game.ClothingLibrary.ItemId(c);
                bool has = false;
                foreach (var r in all ?? new List<Recipe>()) if (r.output == item) has = true;
                if (!has && !SewnElsewhere.Contains(c.id))
                    yield return Itm("c_" + c.id, c.name, RecipeCategory.Clothing, "workbench", item, 1, "SEW", null, (C, 4));
            }
            yield return Res("filter_water", "FILTER WATER 5L", RecipeCategory.Supplies, "workbench", Wt, 5, "CLOTH AND CHARCOAL FILTER", (Dw, 5), (Ch, 1));
            // media copies: must have studied the original
            foreach (var m in MadMax.RPG.MediaLibrary.All)
            {
                if (m.kind == MadMax.RPG.MediaKind.Book)
                    yield return new Recipe { id = "copy_" + m.id, name = "COPY " + m.name, category = RecipeCategory.Media, station = "workbench", kind = OutputKind.Item, output = m.id, description = "WRITE OUT A BOOK YOU STUDIED", items = new[] { (ItemIds.Paper, 6) }, resources = new (ResourceType, int)[0], knowledge = "read_" + m.id };
                else
                    yield return new Recipe { id = "copy_" + m.id, name = "DUB " + m.name, category = RecipeCategory.Media, station = "workbench", kind = OutputKind.Item, output = m.id, description = "RECORD A TAPE YOU WATCHED", resources = new[] { (S, 2), (ResourceType.Oil, 1) }, knowledge = "read_" + m.id };
            }
            // farming
            yield return Itm("fertilizer_c", "FERTILIZER", RecipeCategory.Farming, "composter", ItemIds.Fertilizer, 2, "FROM ROTTEN FOOD", new[] { ("food_rotten", 3) });
            yield return Itm("fertilizer_m", "MANURE FERTILIZER", RecipeCategory.Farming, "composter", ItemIds.Fertilizer, 3, "FROM THE PEN'S DUNG", new[] { ("farm_manure", 2) });
            yield return Itm("fertilizer_a", "ASH FERTILIZER", RecipeCategory.Farming, "composter", ItemIds.Fertilizer, 1, "FROM CHARCOAL ASH", null, (Ch, 2), (ResourceType.Sand, 2));
            var spin = Res("cloth_cotton", "SPIN CLOTH", RecipeCategory.Supplies, "workbench", C, 2, "COTTON TO CLOTH");
            spin.items = new[] { ("crop_cotton", 3) };
            yield return spin;
            // cooking (stove: wood/charcoal fire, oven: electric)
            foreach (var st in new[] { "stove", "oven" })
            {
                var fuel = st == "stove" ? W : ResourceType.None;
                yield return Cook(st, "bake_potato", "BAKED POTATO", "food_potato_baked", 1, fuel, ("food_potato", 1));
                yield return Cook(st, "roast_corn", "ROAST CORN", "food_corn_roast", 1, fuel, ("food_corn", 1));
                yield return Cook(st, "stew", "VEGETABLE STEW", "food_stew", 2, fuel, ("food_potato", 1), ("food_carrot", 1), ("food_cabbage", 1));
                yield return Cook(st, "soup", "TOMATO SOUP", "food_soup", 2, fuel, ("food_tomato", 2), ("food_herbs", 1));
                yield return Cook(st, "pie", "PUMPKIN PIE", "food_pie", 3, fuel, ("food_pumpkin", 1), ("food_corn", 1));
                yield return Cook(st, "jam", "APPLE JAM", "food_jam", 2, fuel, ("food_apple", 2), ("food_berries", 2));
                yield return Cook(st, "boil", "BOIL WATER 5L", null, 5, fuel);
            }
            // refining soils at the wash plant (needs water)
            yield return Refine("r_sand", "WASH SAND", ResourceType.Sand, (ResourceType.Silica, 4), (ResourceType.IronOre, 1));
            yield return Refine("r_clay", "WASH CLAY", ResourceType.Clay, (ResourceType.CopperOre, 2), (ResourceType.TinOre, 2), (ResourceType.Silica, 1));
            yield return Refine("r_laterite", "WASH LATERITE", ResourceType.Laterite, (ResourceType.Bauxite, 3), (ResourceType.IronOre, 2));
            yield return Refine("r_rubble", "SORT RUBBLE", ResourceType.Rubble, (ResourceType.IronOre, 2), (ResourceType.CopperOre, 1), (St, 3));
            yield return Refine("r_slag", "WASH SLAG", ResourceType.Slag, (ResourceType.CopperOre, 2), (ResourceType.TinOre, 1), (ResourceType.Bauxite, 1));
            // kiln
            yield return Kiln("charcoal", "CHARCOAL", Ch, 3, (W, 4));
            yield return Kiln("lime", "QUICKLIME", ResourceType.Lime, 2, (St, 4), (Ch, 1));
            yield return Kiln("glass_k", "GLASS", G, 2, (ResourceType.Silica, 3), (Ch, 1));
            // furnace (charcoal fired)
            yield return Smelt("iron", "IRON", "furnace", Fe, 2, (ResourceType.IronOre, 3), (Ch, 2));
            yield return Smelt("iron_scrap", "RECYCLE SCRAP", "furnace", Fe, 2, (S, 6), (Ch, 2));
            yield return Smelt("copper", "COPPER", "furnace", Cu, 2, (ResourceType.CopperOre, 3), (Ch, 1));
            yield return Smelt("bronze", "BRONZE", "furnace", ResourceType.Bronze, 3, (Cu, 2), (ResourceType.TinOre, 1), (Ch, 1));
            yield return Smelt("glass_f", "GLASS", "furnace", G, 3, (ResourceType.Silica, 4), (Ch, 1));
            // arc furnace (electric): aluminium
            yield return Smelt("aluminium", "ALUMINIUM", "arc_furnace", ResourceType.Aluminium, 2, (ResourceType.Bauxite, 4));
            yield return Smelt("iron_arc", "IRON", "arc_furnace", Fe, 3, (ResourceType.IronOre, 3));
            // mixer
            yield return Res("concrete", "CONCRETE X4", RecipeCategory.Refining, "mixer", ResourceType.Concrete, 4, "FOR WALLS, FLOORS, ROADS", (St, 2), (ResourceType.Sand, 2), (ResourceType.Lime, 1), (Wt, 2));
            yield return Res("asphalt", "ASPHALT X4", RecipeCategory.Refining, "mixer", ResourceType.Asphalt, 4, "HOT MIX FOR THE PAVER", (St, 3), (ResourceType.Sand, 2), (ResourceType.Oil, 2));
            // still: fuels and oils
            yield return FuelR("biofuel_corn", "CORN BIOFUEL 4L", ResourceType.Fuel, 4, new[] { ("food_corn", 5) }, (Ch, 1), (Wt, 2));
            yield return FuelR("biofuel_potato", "POTATO BIOFUEL 3L", ResourceType.Fuel, 3, new[] { ("food_potato", 5) }, (Ch, 1), (Wt, 2));
            yield return FuelR("biofuel_veg", "VEG BIOFUEL 2L", ResourceType.Fuel, 2, new[] { ("food_pumpkin", 2) }, (Ch, 1));
            yield return FuelR("petrol", "DISTIL PETROL 2L", ResourceType.Fuel, 2, null, (ResourceType.Oil, 3), (Ch, 1));
            yield return FuelR("seed_oil", "PRESS OIL 1L", ResourceType.Oil, 1, new[] { ("food_sunseeds", 4) });
            yield return FuelR("rubber_syn", "SYNTHETIC RUBBER", Rb, 2, null, (ResourceType.Oil, 3), (Ch, 1));
            yield return FuelR("ethanol", "ETHANOL 3L", ResourceType.Ethanol, 3, new[] { ("food_apple", 4) }, (Ch, 1));
            yield return FuelR("coolant_s", "COOLANT 5L", ResourceType.Coolant, 5, null, (ResourceType.Ethanol, 2), (Wt, 3));
        }

        static Recipe Cook(string station, string id, string name, string output, int amount, ResourceType fuel, params (string, int)[] items)
        {
            var r = new Recipe { id = station + "_" + id, name = name, category = RecipeCategory.Cooking, station = station, amount = amount, items = items, description = station == "stove" ? "BURNS 1 WOOD" : "NEEDS POWER" };
            if (output == null) { r.kind = OutputKind.Resource; r.outputResource = ResourceType.Water; r.resources = new[] { (ResourceType.DirtyWater, amount) }; }
            else { r.kind = OutputKind.Item; r.output = output; }
            r.fuel = fuel; r.fuelAmount = fuel == ResourceType.None ? 0 : 1;
            return r;
        }

        static Recipe Refine(string id, string name, ResourceType soil, params (ResourceType type, int amount)[] outs)
        {
            // several outputs: the first is the recipe output, the rest are by-products (Recipe.byproducts)
            var r = Res(id, name, RecipeCategory.Refining, "washplant", outs[0].type, outs[0].amount, "10 SOIL + 5L WATER", (soil, 10), (ResourceType.DirtyWater, 5));
            var extra = new (ResourceType, int)[outs.Length - 1];
            for (int i = 1; i < outs.Length; i++) extra[i - 1] = outs[i];
            r.byproducts = extra;
            return r;
        }

        static Recipe Kiln(string id, string name, ResourceType output, int amount, params (ResourceType, int)[] res) => Res(id, name, RecipeCategory.Smelting, "kiln", output, amount, "KILN", res);
        static Recipe Smelt(string id, string name, string station, ResourceType output, int amount, params (ResourceType, int)[] res) => Res("s_" + station + "_" + id, name, RecipeCategory.Smelting, station, output, amount, station == "arc_furnace" ? "NEEDS POWER" : "CHARCOAL FIRED", res);
        static Recipe FuelR(string id, string name, ResourceType output, int amount, (string, int)[] items, params (ResourceType, int)[] res)
        {
            var r = Res(id, name, RecipeCategory.Fuel, "still", output, amount, "DISTILLERY", res);
            r.items = items ?? new (string, int)[0];
            return r;
        }


        /// <summary>Knowledge gates per recipe id (learned from media or research).</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> Gates = new System.Collections.Generic.Dictionary<string, string>
        {
            { "radiator", "k_radiator" }, { "radiator_t", "k_truck_parts" }, { "stack", "k_truck_parts" }, { "street", "k_tyres" }, { "offroad", "k_tyres" },
            { "shotgun", "k_firearms" }, { "shells", "k_firearms" }, { "turret", "k_weapon_mounts" }, { "wall", "k_walls" }, { "floodlight", "k_electric" },
            { "tv", "k_electric" }, { "coolant", "k_coolant" }, { "molotov", "k_molotov" },
        };

        public static Recipe Get(string id) { foreach (var r in All) if (r.id == id) return r; return null; }

        /// <summary>Working time at a station (seconds at skill 0): explicit per recipe, else by category.</summary>
        public static float Seconds(Recipe r)
        {
            if (r.seconds > 2.01f) return r.seconds;
            switch (r.kind)
            {
                case OutputKind.Vehicle: return 150f;
                case OutputKind.Part: return 45f;
            }
            switch (r.category)
            {
                case RecipeCategory.Tools: return 20f;
                case RecipeCategory.Weapons: return 25f;
                case RecipeCategory.Building: return 15f;
                case RecipeCategory.Clothing: return 20f;
                case RecipeCategory.Cooking: return 12f;
                case RecipeCategory.Farming: return 15f;
                case RecipeCategory.Media: return 30f;
                case RecipeCategory.Refining: return 25f;
                case RecipeCategory.Smelting: return 30f;
                case RecipeCategory.Fuel: return 25f;
                default: return 10f;
            }
        }

        /// <summary>Crafted things whose make matters (crude / sturdy / fine): tools, weapons, clothes, parts.</summary>
        public static bool HasQuality(Recipe r) => r.kind == OutputKind.Part ||
            (r.kind == OutputKind.Item && (r.category == RecipeCategory.Tools || r.category == RecipeCategory.Weapons || r.category == RecipeCategory.Clothing) && !r.output.StartsWith("ammo_"));

        public static string KnowledgeFor(Recipe r) => r.knowledge ?? (Gates.TryGetValue(r.id, out var k) ? k : null);

        /// <summary>Cost multiplier from the crafter's skill (set by the game each frame).</summary>
        public static float CostMult = 1f;
        public static int Amount(int n) => MadMax.RPG.CharacterStats.Cost(n, CostMult);

        public static bool CanCraft(Recipe r, Inventory inv)
        {
            foreach (var (t, n) in r.resources) if (t != ResourceType.None && inv.Get(t) < Amount(n)) return false;
            foreach (var (i, n) in r.items) if (inv.GetItem(i) < n) return false;
            if (r.fuel != ResourceType.None && inv.Get(r.fuel) < r.fuelAmount && !(r.fuel == ResourceType.Wood && inv.Get(ResourceType.Charcoal) >= r.fuelAmount)) return false;
            return true;
        }

        public static void Pay(Recipe r, Inventory inv)
        {
            foreach (var (t, n) in r.resources) if (t != ResourceType.None) inv.TrySpend(t, Amount(n));
            foreach (var (i, n) in r.items) inv.TakeItem(i, n);
            if (r.fuel != ResourceType.None && !inv.TrySpend(r.fuel, r.fuelAmount) && r.fuel == ResourceType.Wood) inv.TrySpend(ResourceType.Charcoal, r.fuelAmount);
        }
    }
}
