using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    public enum BuildCategory { Structure, Furniture, Utility, Garden, Industry, Decor, Hidden, Defence }

    public class FurnitureDef
    {
        public string id, name;
        public BuildCategory category;
        public VoxelGrid grid;
        public (ResourceType type, int amount)[] cost;
        public string kit;               // crafted item consumed instead of raw cost (null = raw cost)
        public Mesh mesh;
        public int hits = 3;
        public bool meshCollider;        // walls, stairs, floors: exact voxel collision (holes, steps)
        public UtilityKind link;         // pseudo pieces: cable / pipe tools (no mesh placed)
        public System.Action<GameObject> setup;
        public string needsItem;         // also consumes one of this item (e.g. a flower for the pot)
        public string snapTo;            // hangs in a piece whose id contains this (door → doorway, shutter → window wall)
        public string upgrade;           // upgrade in place to this piece (wood → brick → concrete)
        public bool foundation;          // stands level on the ground (legs reach down), tiles with its neighbours
        public Vector4 deck;             // drivable top: half width, half length, top at the back / front edge (m, local)
        public int plan = -1;            // structure plan pseudo piece: index into StructurePlans (-2 = the capture tool)
        public float voxel = VoxelMesher.DefaultSize;   // big coarse pieces (the hangar) use larger voxels
    }

    /// <summary>Placeable furniture and building pieces. Origin = mounting point on the surface, +Y = away from the
    /// surface (floor: up, wall: out of the wall, ceiling: down), +Z = front.</summary>
    public static partial class FurnitureLibrary
    {
        static List<FurnitureDef> defs;
        static readonly Dictionary<string, FurnitureDef> byId = new Dictionary<string, FurnitureDef>();

        const byte Scrap = (byte)ResourceType.Scrap, Wood = (byte)ResourceType.Wood, Cloth = (byte)ResourceType.Cloth, Glass = (byte)ResourceType.Glass, Stone = (byte)ResourceType.Stone;

        public static IReadOnlyList<FurnitureDef> All { get { Ensure(); return defs; } }

        public static FurnitureDef Get(string id)
        {
            Ensure();
            if (byId.Count == 0) foreach (var d in defs) byId[d.id] = d;
            return id != null && byId.TryGetValue(id, out var def) ? def : null;
        }

        static void Ensure()
        {
            // meshes die with play mode while statics survive (no domain reload)
            if (defs != null && defs[0].mesh) return;    // (rotorMesh is created lazily by its own check)
            byId.Clear();
            var B = BuildCategory.Structure; var Fu = BuildCategory.Furniture; var U = BuildCategory.Utility; var Ga = BuildCategory.Garden; var In = BuildCategory.Industry; var De = BuildCategory.Decor;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var St = ResourceType.Stone; var C = ResourceType.Cloth;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Co = ResourceType.Concrete; var Al = ResourceType.Aluminium;
            var WS = BuildPieces.WallStyle.Wood; var BS = BuildPieces.WallStyle.Brick; var CS = BuildPieces.WallStyle.Concrete;
            defs = new List<FurnitureDef>
            {
                // structure
                D("wall_wood", "WOOD WALL", B, BuildPieces.Wall(WS, 0, 701), 8, true, null, (W, 6)),
                D("wall_wood_window", "WOOD WINDOW WALL", B, BuildPieces.Wall(WS, 1, 702), 8, true, null, (W, 5), (G, 2)),
                D("doorway_wood", "WOOD DOORWAY", B, BuildPieces.Wall(WS, 2, 703), 8, true, null, (W, 5)),
                D("wall_brick", "BRICK WALL", B, BuildPieces.Wall(BS, 0, 704), 20, true, null, (St, 10), (ResourceType.Lime, 1)),
                D("wall_brick_window", "BRICK WINDOW WALL", B, BuildPieces.Wall(BS, 1, 705), 20, true, null, (St, 8), (ResourceType.Lime, 1), (G, 2)),
                D("doorway_brick", "BRICK DOORWAY", B, BuildPieces.Wall(BS, 2, 706), 20, true, null, (St, 8), (ResourceType.Lime, 1)),
                D("wall_concrete", "CONCRETE WALL", B, BuildPieces.Wall(CS, 0, 707), 40, true, null, (Co, 6), (Fe, 1)),
                D("doorway_concrete", "CONCRETE DOORWAY", B, BuildPieces.Wall(CS, 2, 708), 40, true, null, (Co, 5), (Fe, 1)),
                D("door_wood", "WOOD DOOR", B, BuildPieces.Door(false), 6, false, go => go.AddComponent<Door>(), (W, 4), (S, 1)).Snap("doorway"),
                D("door_metal", "METAL DOOR", B, BuildPieces.Door(true), 25, false, go => go.AddComponent<Door>(), (Fe, 4), (S, 2)).Snap("doorway"),
                D("floor_wood", "WOOD FLOOR", B, BuildPieces.Floor(WS, 710), 8, true, null, (W, 4)).Deck(1f, 1f, 0.12f, 0.12f),
                D("floor_concrete", "CONCRETE FLOOR", B, BuildPieces.Floor(CS, 711), 40, true, null, (Co, 4)).Deck(1f, 1f, 0.12f, 0.12f),
                D("jump_ramp", "JUMP RAMP", B, JumpRamp(), 12, true, null, (W, 12), (S, 6)).Deck(1.4f, 2.4f, 0.04f, 1.28f),
                D("stairs", "STAIRS", B, BuildPieces.Stairs(), 8, true, null, (W, 8)),
                D("ladder", "LADDER", B, BuildPieces.Ladder(), 4, false, go => { var l = go.AddComponent<Ladder>(); l.localBounds = go.GetComponent<MeshFilter>().sharedMesh.bounds; }, (W, 3)),
                D("roof_flat", "ROOF", B, BuildPieces.Roof(false), 8, true, null, (S, 4)),
                D("roof_slope", "SLOPED ROOF", B, BuildPieces.Roof(true), 8, true, null, (S, 5)),
                D("fence_wood", "FENCE", B, BuildPieces.Fence(false), 5, false, null, (W, 3)),
                D("fence_wire", "WIRE FENCE", B, BuildPieces.Fence(true), 8, false, null, (S, 3), (W, 1)),
                D("gate", "GATE", B, BuildPieces.Gate(), 6, false, go => go.AddComponent<Door>().hingeX = -0.96f, (W, 4), (S, 1)),
                D("post", "POST", B, BuildPieces.Post(), 5, false, null, (W, 2)),
                Kit("wall_scrap", "SCRAP WALL", Wall(), ItemIds.WallKit, B),
                Kit("barricade", "BARRICADE", Barricade(), ItemIds.BarricadeKit, B),
                Def("plate_scrap", "SCRAP PLATE", Plate(Scrap), (ResourceType.Scrap, 3)).In(B),
                Def("plate_wood", "WOOD PANEL", Plate(Wood), (ResourceType.Wood, 3)).In(B),

                // furniture
                D("bed", "BED", Fu, Bed(), 3, false, go => go.AddComponent<Bed>(), (W, 6), (C, 4)),
                D("fridge", "FRIDGE", Fu, Fridge(), 5, false, go => { Node(go, UtilityKind.Power, 0.9f); Box(go, "FRIDGE", 60f, true); }, (S, 8), (Cu, 1)),
                D("workbench", "WORKBENCH", Fu, Workbench(), 4, false, go => Station(go, "workbench", "WORKBENCH", 0f), (W, 5), (S, 4)),
                D("locker", "LOCKER", Fu, Locker(), 5, false, go => { Box(go, "LOCKER", 60f, false); go.AddComponent<Door>().enabled = false; }, (S, 6)),
                D("shelf", "SHELF", Fu, Shelf(), 3, false, go => Box(go, "SHELF", 30f, false), (W, 3)),
                D("crate", "CRATE", Fu, Crate(), 3, false, go => Box(go, "CRATE", 40f, false), (W, 4)),
                Kit("chest", "CHEST", Chest(), ItemIds.ChestKit, Fu).With(go => { Box(go, "CHEST", 120f, false); go.AddComponent<Door>().enabled = false; }),
                D("stove", "WOOD STOVE", Fu, Stove(), 4, false, go => { Station(go, "stove", "COOK (WOOD STOVE)", 0f).output = new Vector3(0, 0.9f, 0); Glow(go, new Vector3(0, 0.35f, 0.35f), new Color(1f, 0.55f, 0.25f), 2.5f, 1.4f, false, 0f); var cl = go.AddComponent<Climate>(); cl.burnsWood = true; cl.heat = 12f; cl.on = false; }, (S, 6), (St, 2)),
                D("oven", "ELECTRIC OVEN", Fu, Oven(), 5, false, go => { Node(go, UtilityKind.Power, 0.6f); Station(go, "oven", "COOK (ELECTRIC OVEN)", 2000f); }, (Fe, 4), (Cu, 2), (G, 1)),
                D("chair", "CHAIR", Fu, BuildPieces.Chair(), 2, false, go => Sit(go, 1.4f, 1.1f, new Vector3(0f, 0.6f, -0.04f)), (W, 2)),
                D("table", "TABLE", Fu, BuildPieces.Table(), 3, false, go => go.AddComponent<DiningTable>().hours = 3f, (W, 4)),
                D("sofa", "SOFA", Fu, BuildPieces.Sofa(), 3, false, go => Sit(go, 2f, 1.3f, new Vector3(-0.44f, 0.6f, 0f), new Vector3(0.44f, 0.6f, 0f)), (W, 3), (C, 5)),
                Kit("tv", "TV", Tv(), ItemIds.TvKit, Fu).With(go => { Node(go, UtilityKind.Power, 0.4f); go.AddComponent<TvSet>(); }),
                D("radio", "RADIO", Fu, Radio(), 2, false, go => go.AddComponent<RadioSet>(), (S, 3), (Cu, 2), (G, 1)),
                D("lamp", "OIL LAMP", Fu, Lamp(), 2, false, go => Glow(go, new Vector3(0, 0.4f, 0), new Color(1f, 0.82f, 0.55f), 6f, 2.2f, false, 0f), (S, 2), (G, 1)),
                D("light_ceiling", "CEILING LIGHT", Fu, BuildPieces.CeilingLight(), 2, false, go => { Node(go, UtilityKind.Power, 0.05f); Glow(go, new Vector3(0, -0.3f, 0), new Color(1f, 0.9f, 0.75f), 8f, 7f, true, 40f); }, (S, 1), (G, 1), (Cu, 1)),
                Kit("floodlight", "FLOODLIGHT", Floodlight(), ItemIds.FloodlightKit, Fu).With(go => { Node(go, UtilityKind.Power, 1.6f); Glow(go, new Vector3(0, 1.72f, 0.3f), new Color(1f, 0.95f, 0.85f), 18f, 40f, true, 200f); go.GetComponent<PoweredLight>().canSense = true; }),
                D("lamppost", "LAMP POST", Fu, BuildPieces.LampPost(), 10, false, go => { Node(go, UtilityKind.Power, 3.6f); Glow(go, new Vector3(0, 3.6f, 0.64f), new Color(1f, 0.8f, 0.5f), 16f, 30f, true, 120f); }, (Fe, 3), (G, 1), (Cu, 1)),
                D("sink", "SINK", Fu, BuildPieces.Sink(), 4, false, go => { Node(go, UtilityKind.Water, 1.0f).waterCapacity = 5f; go.AddComponent<WaterOutlet>().kind = WaterOutlet.Kind.Sink; }, (S, 4), (Fe, 1)),
                D("shower", "SHOWER", Fu, BuildPieces.Shower(), 4, false, go => { Node(go, UtilityKind.Water, 2.3f).waterCapacity = 5f; go.AddComponent<WaterOutlet>().kind = WaterOutlet.Kind.Shower; }, (S, 6), (Fe, 1), (C, 2)),

                // utility
                new FurnitureDef { id = "cable", name = "POWER CABLE", category = U, cost = new[] { (Cu, 1) }, link = UtilityKind.Power, mesh = Spool(true) },
                new FurnitureDef { id = "pipe", name = "WATER PIPE", category = U, cost = new[] { (S, 1) }, link = UtilityKind.Water, mesh = Spool(false) },
                D("generator", "GENERATOR", U, BuildPieces.Generator(), 8, false, go => { Node(go, UtilityKind.Power, 0.8f); go.AddComponent<Generator>(); }, (Fe, 6), (Cu, 4), (Al, 2)),
                D("windmill", "WIND TURBINE", U, BuildPieces.WindmillTower(), 12, false, go => { Node(go, UtilityKind.Power, 0.4f); Rotor(go); }, (Fe, 8), (Cu, 4), (Al, 4)),
                D("battery", "BATTERY BANK", U, BuildPieces.Battery(), 5, false, go => { var n = Node(go, UtilityKind.Power, 0.7f); n.batteryWh = 3000f; go.AddComponent<BatteryRack>(); }, (Cu, 4), (S, 4), (ResourceType.Oil, 2)),
                D("power_pole", "POWER POLE", U, BuildPieces.PowerPole(), 6, false, go => Node(go, UtilityKind.Power, 4.7f), (W, 4), (G, 1)),
                D("rain_collector", "RAIN COLLECTOR", U, BuildPieces.RainCollector(), 5, false, go => { Node(go, UtilityKind.Water, 0.3f).waterCapacity = 200f; go.AddComponent<WaterSource>().mode = WaterSource.Mode.Rain; go.AddComponent<WaterOutlet>().kind = WaterOutlet.Kind.Barrel; }, (S, 6)),
                D("water_tank", "WATER TANK", U, BuildPieces.WaterTank(), 10, false, go => Node(go, UtilityKind.Water, 1.3f).waterCapacity = 1000f, (S, 12), (Fe, 4)),
                D("filter", "WATER FILTER", U, BuildPieces.Filter(), 4, false, go => { Node(go, UtilityKind.Water, 0.6f).waterCapacity = 20f; go.AddComponent<WaterSource>().mode = WaterSource.Mode.Filter; }, (S, 3), (ResourceType.Charcoal, 4), (C, 2), (ResourceType.Sand, 4)),
                D("pump", "WATER PUMP", U, BuildPieces.Pump(), 6, false, go => { Node(go, UtilityKind.Power | UtilityKind.Water, 0.5f).waterCapacity = 20f; go.AddComponent<WaterSource>().mode = WaterSource.Mode.Pump; }, (Fe, 4), (Cu, 2)),
                D("fuel_pump", "FUEL PUMP", U, MadMax.World.BiomeProps.GasPumpGrid(), 8, false, go => { var gp = go.AddComponent<MadMax.World.GasPump>(); gp.stock = 0f; gp.nozzle = new Vector3(0.5f, 0.8f, 0.15f); }, (Fe, 6), (S, 4), (G, 1)),
                D("heater", "ELECTRIC HEATER", U, BuildPieces.Heater(false), 3, false, go => { Node(go, UtilityKind.Power, 0.4f); go.AddComponent<Climate>().heat = 16f; }, (Fe, 2), (Cu, 3)),
                D("aircon", "AIR CONDITIONER", U, BuildPieces.Heater(true), 3, false, go => { Node(go, UtilityKind.Power, 0.4f); go.AddComponent<Climate>().heat = -14f; }, (Fe, 3), (Cu, 4), (Al, 2)),
                D("fireplace", "FIREPLACE", Fu, BuildPieces.Fireplace(), 10, false, go => { go.AddComponent<Climate>().heat = 20f; go.GetComponent<Climate>().burnsWood = true; Glow(go, new Vector3(0, 0.3f, 0.3f), new Color(1f, 0.5f, 0.2f), 5f, 3f, false, 0f); }, (St, 12)),
                D("sprinkler", "SPRINKLER", U, BuildPieces.Sprinkler(), 3, false, go => { Node(go, UtilityKind.Water, 0.2f).waterCapacity = 2f; go.AddComponent<Sprinkler>(); }, (S, 2), (Fe, 1)),
                D("drip_line", "DRIP LINE", U, BuildPieces.DripLine(), 2, false, go => { Node(go, UtilityKind.Water, 0.1f).waterCapacity = 1f; var d = go.AddComponent<Sprinkler>(); d.reach = 1.6f; d.drip = true; }, (ResourceType.Rubber, 1), (S, 1)),
                D("well", "WELL", U, BuildPieces.Well(), 20, false, go => { Node(go, UtilityKind.Water | UtilityKind.Power, 0.4f).waterCapacity = 60f; go.AddComponent<WaterSource>().mode = WaterSource.Mode.Well; go.AddComponent<HandPump>(); go.AddComponent<WaterOutlet>().kind = WaterOutlet.Kind.Well; }, (St, 16), (Fe, 3), (W, 2)),
                D("water_tower", "WATER TOWER", U, BuildPieces.WaterTower(), 16, false, go => Node(go, UtilityKind.Water, 0.4f).waterCapacity = 3000f, (W, 24), (Fe, 4), (S, 4)),

                // garden
                D("garden_plot", "GARDEN PLOT", Ga, BuildPieces.GardenBed(11, 6), 4, false, go => go.AddComponent<GardenPlot>(), (W, 3), (ResourceType.Clay, 4)),
                D("planter", "PLANTER", Ga, BuildPieces.GardenBed(4, 4), 3, false, go => go.AddComponent<GardenPlot>(), (W, 2), (ResourceType.Clay, 2)),
                D("composter", "COMPOSTER", Ga, BuildPieces.Composter(), 4, false, go => Station(go, "composter", "COMPOSTER", 0f), (W, 6)),
                D("greenhouse", "GREENHOUSE", Ga, BuildPieces.Greenhouse(), 10, true, null, (W, 12), (G, 16)),
                D("scarecrow", "SCARECROW", Ga, BuildPieces.Scarecrow(), 2, false, go => go.AddComponent<Scarecrow>(), (W, 3), (C, 3)),
                D("trough", "TROUGH", Ga, BuildPieces.Trough(), 4, false, go => go.AddComponent<MadMax.Animals.Trough>(), (W, 8), (S, 1)),
                D("nest_box", "NEST BOX", Ga, BuildPieces.NestBox(), 3, false, go => Box(go, "NEST BOX", 12f, false), (W, 5), (S, 1)),
                D("fish_trap", "FISH TRAP", Ga, BuildPieces.FishTrap(), 3, false, go => go.AddComponent<FishTrap>(), (Fe, 2), (S, 2), (C, 1)),

                // industry
                D("furnace", "FURNACE", In, BuildPieces.Furnace(false), 20, false, go => { Station(go, "furnace", "SMELT (FURNACE)", 0f).output = new Vector3(0, 0.6f, 0.8f); Glow(go, new Vector3(0, 0.3f, 0.6f), new Color(1f, 0.5f, 0.2f), 4f, 2f, false, 0f); }, (St, 20), (ResourceType.Clay, 6)),
                D("arc_furnace", "ARC FURNACE", In, BuildPieces.Furnace(true), 20, false, go => { Node(go, UtilityKind.Power, 1.8f); Station(go, "arc_furnace", "SMELT (ARC FURNACE)", 3000f).output = new Vector3(0, 0.6f, 0.8f); }, (Fe, 12), (Cu, 8), (Co, 4)),
                D("kiln", "KILN", In, BuildPieces.Kiln(), 15, false, go => Station(go, "kiln", "KILN", 0f).output = new Vector3(0, 0.5f, 0.8f), (St, 14), (ResourceType.Clay, 4)),
                D("washplant", "WASH PLANT", In, BuildPieces.WashPlant(), 12, false, go => Station(go, "washplant", "REFINE SOIL (WASH PLANT)", 0f), (Fe, 6), (S, 8)),
                D("mixer", "CEMENT MIXER", In, BuildPieces.Mixer(), 10, false, go => Station(go, "mixer", "MIX (CEMENT MIXER)", 0f), (Fe, 6), (S, 4)),
                D("still", "DISTILLERY", In, BuildPieces.Still(), 8, false, go => Station(go, "still", "DISTIL (STILL)", 0f), (Cu, 8), (G, 2)),
                D("garage", "GARAGE", In, BuildPieces.Garage(), 30, true, go => { var st = Station(go, "garage", "GARAGE", 0f); st.output = new Vector3(0, 0.6f, 0); st.tier = 1f; go.AddComponent<TuningBench>(); }, (Co, 20), (Fe, 16), (S, 20)).Deck(2f, 3f, 0.04f, 0.04f),
                D("tuning_bench", "TUNING BENCH", In, BuildPieces.TuningBench(), 8, false, go => go.AddComponent<TuningBench>(), (Fe, 6), (Cu, 2), (G, 1)),
                D("paint_booth", "PAINT STATION", In, BuildPieces.PaintBooth(), 6, false, go => go.AddComponent<PaintBooth>(), (Fe, 4), (Cu, 2), (S, 4), (ResourceType.Rubber, 1)),
                D("player_stall", "MARKET STALL", Fu, MarketStall(), 6, false, go => { Box(go, "STALL GOODS", 80f, false); go.AddComponent<PlayerStall>(); }, (W, 8), (C, 4)),

                // decor
                D("rug", "RUG", De, BuildPieces.Rug(), 1, false, null, (C, 3)),
                D("painting", "PAINTING", De, BuildPieces.Painting(), 1, false, null, (W, 1), (C, 1)),
                D("flag", "FLAG", De, BuildPieces.Flag(), 2, false, null, (S, 2), (C, 2)),
                D("tyres", "TYRE STACK", De, BuildPieces.Tyres(), 4, false, null, (ResourceType.Rubber, 6)),
                D("barrel", "BARREL", De, BuildPieces.Barrel(), 3, false, go => Box(go, "BARREL", 30f, false), (S, 4)),
                D("sign", "KEEP OUT SIGN", De, BuildPieces.Sign(), 2, false, null, (S, 2), (W, 1)),
                D("skull_pole", "SKULL POLE", De, BuildPieces.SkullPole(), 2, false, null, (W, 1), (St, 1)),
                D("flower_pot", "FLOWER POT", De, BuildPieces.FlowerPot(), 1, false, null, (St, 1)).Needs("crop_flower"),

                // planted trees (placed with saplings, not from the build menu)
                new FurnitureDef { id = "tree_planted", name = "TREE", category = BuildCategory.Hidden, cost = new[] { (W, 10) }, hits = 6, mesh = Spool(false), setup = go => go.AddComponent<PlantedTree>() },
            };
            defs.AddRange(Home());
            defs.AddRange(Workshops());
            defs.AddRange(Refining());
            defs.AddRange(BaseDefs());
            Upgrades();
        }

        static FurnitureDef D(string id, string name, BuildCategory cat, VoxelGrid g, int hits, bool meshCollider, System.Action<GameObject> setup, params (ResourceType, int)[] cost)
        {
            var d = Def(id, name, g, cost);
            d.category = cat; d.hits = hits; d.meshCollider = meshCollider; d.setup = setup;
            return d;
        }

        static FurnitureDef In(this FurnitureDef d, BuildCategory c) { d.category = c; return d; }
        static FurnitureDef With(this FurnitureDef d, System.Action<GameObject> s) { d.setup = s; d.hits = 4; return d; }
        static FurnitureDef Needs(this FurnitureDef d, string item) { d.needsItem = item; return d; }
        static FurnitureDef Snap(this FurnitureDef d, string into) { d.snapTo = into; return d; }
        static FurnitureDef Deck(this FurnitureDef d, float halfX, float halfZ, float back, float front) { d.deck = new Vector4(halfX, halfZ, back, front); return d; }

        static UtilityNode Node(GameObject go, UtilityKind k, float portY)
        {
            var n = go.AddComponent<UtilityNode>();
            n.kinds = k; n.port = new Vector3(0, portY, 0);
            return n;
        }

        static void Box(GameObject go, string title, float cap, bool fridge)
        {
            var c = go.AddComponent<Container>();
            c.title = title; c.capacity = cap; c.fridge = fridge;
        }

        static CraftingStation Station(GameObject go, string type, string title, float watts)
        {
            var s = go.AddComponent<CraftingStation>();
            s.type = type; s.title = title; s.watts = watts;
            return s;
        }

        static void Glow(GameObject go, Vector3 at, Color c, float range, float intensity, bool powered, float watts)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = at;
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            var pl = go.AddComponent<PoweredLight>();
            pl.needsPower = powered; pl.watts = watts;
        }

        static Mesh rotorMesh;
        static void Rotor(GameObject go)
        {
            if (!rotorMesh) { var g = BuildPieces.WindmillRotor(); g.Bevel(); rotorMesh = VoxelMesher.Build(g, "Furniture_rotor"); }
            var r = new GameObject("Rotor", typeof(MeshFilter), typeof(MeshRenderer));
            r.transform.SetParent(go.transform, false);
            r.transform.localPosition = new Vector3(0, 5.76f, 0.3f);
            r.GetComponent<MeshFilter>().sharedMesh = rotorMesh;
            r.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
            go.AddComponent<Windmill>().rotor = r.transform;
        }

        static Mesh Spool(bool cable)
        {
            var g = new VoxelGrid().Mat(cable ? (byte)ResourceType.Copper : Scrap);
            g.CylX(3, 0, 3f, -2, 2, cable ? Pal.Ramp(Pal.Bronze, 2) : Pal.Ramp(Pal.Metal, 2));
            g.Bevel();
            return VoxelMesher.Build(g, cable ? "Furniture_cable" : "Furniture_pipe");
        }

        /// <summary>Market stall: a plank counter under a striped awning on four poles, goods crates underneath.</summary>
        static VoxelGrid MarketStall()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -12, 12 }) foreach (int z in new[] { -6, 6 }) g.Box(x, 0, z, x, 26, z, Pal.Ramp(Pal.Wood, 1, 1611));
            g.Box(-12, 10, 4, 12, 11, 7, Pal.Ramp(Pal.Wood, 2, 1612));                                                     // counter
            g.Box(-12, 0, 5, 12, 9, 7, p => p.x % 4 == 0 ? Pal.Wood[1] : Pal.Wood[2]);                                     // front boards
            g.Mat(Cloth);
            for (int z = -8; z <= 8; z++) g.Box(-13, 27 - Mathf.Abs(z) / 4, z, 13, 27 - Mathf.Abs(z) / 4, z, p => (p.x / 3 & 1) == 0 ? Pal.Crimson[3] : Pal.Cream[3]);   // awning
            g.Mat(Wood);
            g.Box(-9, 1, -4, -4, 5, 1, Pal.Ramp(Pal.Wood, 3, 1613)); g.Box(3, 1, -4, 9, 6, 1, Pal.Ramp(Pal.Wood, 2, 1614)); // crates
            g.Box(-3, 12, 5, -1, 13, 6, Pal.Solid(Pal.Ochre[3])); g.Box(2, 12, 5, 4, 12, 6, Pal.Ramp(Pal.Crimson, 2));   // wares on the counter
            return g;
        }

        static VoxelGrid Oven()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-5, 0, -4, 5, 11, 3, Pal.Weathered(Pal.Cream, 0.2f, 1030, 2, 0));
            g.Mat(Glass); g.Box(-3, 2, 4, 3, 6, 4, Pal.Ramp(Pal.Black, 2));
            g.Mat(Scrap); for (int x = -3; x <= 3; x += 2) g.Set(x, 9, 4, Pal.Solid(Pal.Black[0]));
            g.Box(-4, 12, -3, 4, 12, 2, Pal.Ramp(Pal.Black, 1));
            return g;
        }

        static FurnitureDef Def(string id, string name, VoxelGrid g, params (ResourceType, int)[] cost)
        {
            g.Bevel();
            return new FurnitureDef { id = id, name = name, grid = g, cost = cost, mesh = VoxelMesher.Build(g, "Furniture_" + id) };
        }

        static FurnitureDef Kit(string id, string name, VoxelGrid g, string kit, BuildCategory cat)
        {
            var d = Def(id, name, g);
            d.kit = kit; d.category = cat;
            return d;
        }

        /// <summary>Buildable pieces of a category (menu order).</summary>
        public static List<FurnitureDef> InCategory(BuildCategory c)
        {
            var l = new List<FurnitureDef>();
            foreach (var d in All) if (d.category == c) l.Add(d);
            if (c == BuildCategory.Structure) StructurePlans.AddDefs(l);      // saved plans + the capture tool
            return l;
        }

        static VoxelGrid Wall()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-12, 0, 0, 12, 24, 0, Pal.Stripe(Pal.Weathered(Pal.Metal, 0.55f, 681, 2, 0), Pal.Ramp(Pal.Rust, 2, 682), 0, 4));
            g.Mat(Wood);
            foreach (int x in new[] { -12, 12 }) g.Box(x, 0, 1, x, 24, 1, Pal.Ramp(Pal.Wood, 2, 683));
            g.Box(-12, 12, 1, 12, 12, 1, Pal.Ramp(Pal.Wood, 2, 684));
            return g;
        }

        static VoxelGrid Barricade()
        {
            var g = new VoxelGrid().Mat(Wood);
            var w = Pal.Ramp(Pal.Wood, 2, 691);
            g.Tube(new Vector3(-9, 0, 0), new Vector3(9, 12, 0), 0.6f, w);
            g.Tube(new Vector3(9, 0, 0), new Vector3(-9, 12, 0), 0.6f, w);
            g.Tube(new Vector3(-10, 6, 1), new Vector3(10, 6, 1), 0.6f, w);
            g.Mat(Scrap);
            foreach (int x in new[] { -8, -3, 3, 8 }) g.Tube(new Vector3(x, 8, 1), new Vector3(x, 12, 5), 0f, Pal.Ramp(Pal.Chrome, 1));
            return g;
        }

        static VoxelGrid Chest()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-6, 0, -4, 6, 7, 4, Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 695), Pal.Ramp(Pal.Wood, 1, 696), 1, 3));
            g.Mat(Scrap);
            foreach (int x in new[] { -4, 4 }) g.Box(x, 0, -4, x, 8, 4, Pal.Ramp(Pal.Metal, 1, 697));
            g.Box(-1, 5, 5, 1, 6, 5, Pal.Solid(Pal.Chrome[2]));
            return g;
        }

        static VoxelGrid Floodlight()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-2, 0, -2, 2, 0, 2, Pal.Ramp(Pal.Metal, 0, 698));
            g.Box(0, 1, 0, 0, 20, 0, Pal.Ramp(Pal.Metal, 1, 699));
            g.Box(-2, 20, -1, 2, 23, 1, Pal.Ramp(Pal.Metal, 2));
            g.Mat(Glass); g.Box(-2, 20, 2, 2, 23, 2, Pal.Solid(Pal.LightW));
            return g;
        }

        static VoxelGrid Bed()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-5, 0, -12, 5, 2, 11, Pal.Ramp(Pal.Wood, 2, 601));
            g.Box(-5, 0, 11, 5, 8, 11, Pal.Ramp(Pal.Wood, 3, 602));
            g.Mat(Cloth);
            g.Box(-4, 3, -11, 4, 4, 10, Pal.Ramp(Pal.Cream, 1, 603));
            g.Box(-4, 5, -11, 4, 5, 3, Pal.Weathered(Pal.Olive, 0.2f, 604, 2, 0));
            g.Box(-3, 5, 7, 3, 6, 10, Pal.Ramp(Pal.Cream, 3, 605));
            return g;
        }

        static VoxelGrid Fridge()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-4, 0, -4, 4, 22, 3, Pal.Weathered(Pal.Cream, 0.18f, 611, 2, 0));
            g.Box(-4, 14, 3, 4, 14, 3, Pal.Solid(Pal.Metal[0]));
            g.Box(3, 8, 4, 3, 12, 4, Pal.Solid(Pal.Chrome[2])); g.Box(3, 16, 4, 3, 20, 4, Pal.Solid(Pal.Chrome[2]));
            g.Box(-3, 1, 3, 3, 2, 3, p => p.x % 2 == 0 ? Pal.Void : Pal.Metal[1]);
            return g;
        }

        static VoxelGrid Workbench()
        {
            var g = new VoxelGrid().Mat(Scrap);
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -4, 3 }) g.Box(x, 0, z, x, 8, z, Pal.Ramp(Pal.Metal, 1, 621));
            g.Box(-8, 3, -4, 8, 3, 3, Pal.Ramp(Pal.Metal, 0, 622));
            g.Mat(Wood); g.Box(-8, 9, -4, 8, 10, 3, Pal.Ramp(Pal.Wood, 3, 623));
            g.Mat(Scrap);
            g.Box(6, 11, 1, 7, 12, 3, Pal.Ramp(Pal.Metal, 2, 624));                           // vise
            g.Box(-5, 11, -2, -1, 11, -2, Pal.Solid(Pal.Chrome[2]));                          // wrench
            g.Box(-3, 11, 0, -3, 11, 2, Pal.Solid(Pal.Wood[1]));
            return g;
        }

        static VoxelGrid Locker()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-3, 0, -3, 3, 20, 2, Pal.Weathered(Pal.Olive, 0.35f, 631, 2, 0));
            g.Box(0, 1, 2, 0, 19, 2, Pal.Solid(Pal.Black[1]));
            for (int y = 15; y <= 18; y += 2) { g.Box(-2, y, 2, -1, y, 2, Pal.Solid(Pal.Void)); g.Box(1, y, 2, 2, y, 2, Pal.Solid(Pal.Void)); }
            return g;
        }

        static VoxelGrid Shelf()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-7, 0, -3, 7, 1, 3, Pal.Ramp(Pal.Wood, 3, 641));
            g.Mat(Glass);
            g.Box(-5, 2, -1, -5, 4, -1, Pal.Ramp(Pal.Glass, 3)); g.Box(-3, 2, 0, -3, 3, 0, Pal.Ramp(Pal.Glass, 2));
            g.Mat(Scrap); g.Box(2, 2, -2, 4, 3, 0, Pal.Ramp(Pal.Rust, 2)); g.Box(5, 2, 1, 6, 4, 2, Pal.Ramp(Pal.Metal, 2));
            return g;
        }

        /// <summary>A plank jump ramp: 2.9 m wide, 4.8 m long, rising to 1.3 m at the lip (+Z), on scrap trestles.</summary>
        static VoxelGrid JumpRamp()
        {
            var g = new VoxelGrid();
            g.Mat((byte)ResourceType.Wood);
            for (int z = -30; z <= 30; z++)
            {
                int top = Mathf.RoundToInt((z + 30) / 60f * 16f);
                g.Box(-18, top, z, 18, top, z, (z & 3) == 0 ? Pal.Ramp(Pal.Wood, 1, 1701) : Pal.Ramp(Pal.Wood, 2, 1702));   // planks
                if ((z + 30) % 15 == 0 && top > 1)
                {
                    g.Mat((byte)ResourceType.Scrap);
                    foreach (int x in new[] { -17, 17 }) g.Box(x, 0, z, x, top - 1, z, Pal.Weathered(Pal.Metal, 0.5f, 1703 + z, 1, 0));   // trestle legs
                    g.Box(-17, top - 1, z, 17, top - 1, z, Pal.Ramp(Pal.Rust, 1, 1704));
                    g.Mat((byte)ResourceType.Wood);
                }
            }
            g.Box(-18, 16, 30, 18, 16, 30, Pal.Stripe(Pal.Solid(Pal.Ochre[3]), Pal.Solid(Pal.Black[1]), 0, 3));   // hazard-striped lip
            return g;
        }

        static VoxelGrid Lamp()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(0, 0, 0, 0, 2, 0, Pal.Solid(Pal.Black[0]));                                 // cord
            g.Box(-2, 3, -2, 2, 3, 2, Pal.Ramp(Pal.Metal, 2)); g.Box(-1, 2, -1, 1, 2, 1, Pal.Ramp(Pal.Metal, 1));
            g.Mat(Glass); g.Set(0, 4, 0, Pal.Solid(Pal.LightW)); g.Box(-1, 4, -1, 1, 4, 1, Pal.Solid(Pal.LightY));
            g.Set(0, 4, 0, Pal.Solid(Pal.LightW));
            return g;
        }

        static VoxelGrid Radio()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-5, 0, -2, 5, 5, 1, Pal.Weathered(Pal.Metal, 0.35f, 701, 2, 0));                    // case
            g.Box(-5, 0, -2, 5, 0, 1, Pal.Ramp(Pal.Black, 1, 702));
            foreach (int sx in new[] { -3, 3 })                                                          // speakers
            {
                g.Box(sx - 1, 1, 2, sx + 1, 3, 2, Pal.Solid(Pal.Black[0]));
                g.Set(sx, 2, 2, Pal.Solid(Pal.Chrome[1]));
            }
            g.Mat(Glass); g.Box(-1, 3, 2, 1, 4, 2, Pal.Solid(Pal.Amber));                             // tuning dial
            g.Mat(Scrap);
            g.Set(0, 2, 2, Pal.Solid(Pal.Chrome[0])); g.Set(-1, 1, 2, Pal.Solid(Pal.Chrome[1])); g.Set(1, 1, 2, Pal.Solid(Pal.Chrome[1])); // knobs
            g.Box(-3, 7, 0, 3, 7, 0, Pal.Solid(Pal.Black[0])); g.Box(-3, 6, 0, -3, 6, 0, Pal.Solid(Pal.Black[0])); g.Box(3, 6, 0, 3, 6, 0, Pal.Solid(Pal.Black[0])); // handle
            g.Box(4, 6, -1, 4, 11, -1, Pal.Solid(Pal.Chrome[0]));                                     // antenna
            return g;
        }

        static VoxelGrid Tv()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-5, 0, -3, 5, 1, 3, Pal.Ramp(Pal.Black, 2, 691));                                   // VCR
            g.Box(-4, 1, 3, -1, 1, 3, Pal.Solid(Pal.Black[0])); g.Set(3, 1, 3, Pal.Solid(new Color32(230, 40, 30, 255)));
            g.Box(-5, 2, -3, 5, 10, 3, Pal.Weathered(Pal.Metal, 0.3f, 692, 2, 0));                    // cabinet
            g.Mat(Glass);
            g.Box(-4, 3, 3, 3, 9, 3, Pal.Solid(new Color32(40, 60, 64, 255)));                        // screen
            g.Mat(Scrap);
            g.Set(4, 8, 3, Pal.Solid(Pal.Chrome[1])); g.Set(4, 6, 3, Pal.Solid(Pal.Chrome[1]));       // knobs
            g.Box(-2, 11, 0, -2, 14, 0, Pal.Solid(Pal.Chrome[0])); g.Box(2, 11, 0, 3, 13, 0, Pal.Solid(Pal.Chrome[0])); // antenna
            return g;
        }

        static VoxelGrid Crate()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-3, 0, -3, 3, 6, 3, Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 651), Pal.Ramp(Pal.Wood, 1, 652), 1, 2));
            g.Mat(Scrap);
            foreach (int x in new[] { -3, 3 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 6, z, Pal.Ramp(Pal.Metal, 2, 653));
            return g;
        }

        static VoxelGrid Stove()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-4, 0, -3, 4, 8, 3, Pal.Weathered(Pal.Metal, 0.4f, 661, 1, 0));
            g.Box(-2, 3, 3, 2, 5, 3, Pal.Solid(Pal.Amber)); g.Set(0, 4, 3, Pal.Solid(Pal.LightY));   // fire door glow
            g.CylY(-2, 0, 1.4f, 9, 11, Pal.Ramp(Pal.Metal, 2));                                 // pot
            g.CylY(2, -2, 1f, 9, 22, Pal.Ramp(Pal.Rust, 1));                                    // flue
            g.Mat(Stone); g.Box(-4, 0, -3, 4, 0, 3, Pal.Ramp(Pal.Sand, 1));
            return g;
        }

        static VoxelGrid Plate(byte mat)
        {
            var g = new VoxelGrid().Mat(mat);
            var m = mat == Wood ? Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 671), Pal.Ramp(Pal.Wood, 1, 672), 0, 3)
                                : Pal.Stripe(Pal.Weathered(Pal.Metal, 0.5f, 673, 2, 0), Pal.Ramp(Pal.Rust, 2, 674), 2, 3);
            g.Box(-6, 0, -6, 6, 0, 6, m);
            if (mat == Scrap) foreach (int x in new[] { -5, 5 }) foreach (int z in new[] { -5, 5 }) g.Set(x, 1, z, Pal.Solid(Pal.Chrome[1])); // rivets
            return g;
        }

        public static Placeable Spawn(string id, Transform parent, Vector3 localPos, Quaternion localRot, Material mat)
        {
            var def = Get(id);
            if (def == null || def.link != UtilityKind.None) return null;
            var go = new GameObject("Placed_" + id, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            bool dynamicParent = parent && parent.GetComponentInParent<Rigidbody>();
            if (def.meshCollider && !dynamicParent) go.AddComponent<MeshCollider>().sharedMesh = def.mesh;
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = def.mesh.bounds.center; box.size = def.mesh.bounds.size;
            }
            var p = go.AddComponent<Placeable>();
            p.id = id;
            p.hits = def.hits;
            def.setup?.Invoke(go);
            // pieces standing on the ground keep the grass out from under them
            var t = MadMax.World.DeformableTerrain.Instance;
            if (!dynamicParent && t && t.World != null)
            {
                var b = go.GetComponent<Renderer>().bounds;
                if (b.min.y < t.Height(b.center.x, b.center.z) + 0.3f) MadMax.World.FloraBlocker.Add(go, 0.02f);
            }
            return p;
        }
    }
}
