using UnityEngine;

namespace MadMax.Items
{
    /// <summary>Raw materials (stored per voxel for yield), refined materials and fluids (litres). Stored in the Inventory.
    /// Values are persisted (saves, voxel material bytes): append only.</summary>
    public enum ResourceType : byte
    {
        None, Scrap, Wood, Stone, Glass, Rubber, Cloth, Fuel, Oil, Coolant,
        // soils (dug by excavators; the soil type decides what refining yields)
        Sand, Clay, Laterite, Rubble, Slag,
        // ores
        IronOre, CopperOre, TinOre, Bauxite, Silica,
        // refined
        Iron, Copper, Bronze, Aluminium, Charcoal, Asphalt, Concrete, Lime,
        // fluids
        Water, DirtyWater, Ethanol
    }

    public static class ResourceInfo
    {
        public const int Count = 31;

        static readonly string[] Names =
        {
            "-", "SCRAP", "WOOD", "STONE", "GLASS", "RUBBER", "CLOTH", "FUEL", "OIL", "COOLANT",
            "SAND", "CLAY", "LATERITE", "RUBBLE", "SLAG",
            "IRON ORE", "COPPER ORE", "TIN ORE", "BAUXITE", "SILICA",
            "IRON", "COPPER", "BRONZE", "ALUMINIUM", "CHARCOAL", "ASPHALT", "CONCRETE", "LIME",
            "WATER", "DIRTY WATER", "ETHANOL"
        };
        static readonly Color32[] Colors =
        {
            new Color32(0, 0, 0, 0), new Color32(170, 170, 178, 255), new Color32(176, 122, 76, 255), new Color32(150, 120, 100, 255),
            new Color32(120, 160, 200, 255), new Color32(60, 52, 50, 255), new Color32(200, 170, 120, 255),
            new Color32(220, 180, 60, 255), new Color32(90, 60, 30, 255), new Color32(80, 200, 150, 255),
            new Color32(224, 170, 104, 255), new Color32(168, 110, 80, 255), new Color32(170, 70, 40, 255), new Color32(120, 116, 110, 255), new Color32(96, 104, 64, 255),
            new Color32(130, 80, 60, 255), new Color32(80, 150, 120, 255), new Color32(170, 170, 150, 255), new Color32(190, 110, 80, 255), new Color32(230, 230, 220, 255),
            new Color32(110, 112, 120, 255), new Color32(210, 120, 70, 255), new Color32(190, 140, 70, 255), new Color32(200, 206, 214, 255),
            new Color32(40, 36, 34, 255), new Color32(30, 28, 28, 255), new Color32(160, 158, 150, 255), new Color32(236, 232, 214, 255),
            new Color32(90, 150, 230, 255), new Color32(120, 110, 80, 255), new Color32(200, 220, 120, 255)
        };
        // resistance to carving (radius divides by sqrt(hardness))
        static readonly float[] Hardness =
        {
            1f, 1.7f, 1f, 2.6f, 0.35f, 1.3f, 0.6f, 1f, 1f, 1f,
            0.5f, 0.8f, 0.9f, 1.8f, 1.2f,
            2.4f, 2.4f, 2.4f, 2f, 2f,
            3f, 2f, 2.6f, 1.6f, 0.6f, 1.4f, 3f, 1.2f,
            1f, 1f, 1f
        };
        // pickups produced per destroyed voxel
        static readonly float[] Yield =
        {
            0f, 1f / 30f, 1f / 20f, 1f / 40f, 1f / 15f, 1f / 15f, 1f / 15f, 0f, 0f, 0f,
            1f / 20f, 1f / 20f, 1f / 20f, 1f / 25f, 1f / 25f,
            1f / 30f, 1f / 30f, 1f / 30f, 1f / 30f, 1f / 30f,
            1f / 40f, 1f / 40f, 1f / 40f, 1f / 40f, 1f / 20f, 1f / 30f, 1f / 40f, 1f / 30f,
            0f, 0f, 0f
        };
        static readonly bool[] Fluid =
        {
            false, false, false, false, false, false, false, true, true, true,
            false, false, false, false, false,
            false, false, false, false, false,
            false, false, false, false, false, false, false, false,
            true, true, true
        };
        /// <summary>HUD order: the everyday materials first, fluids last; others only when carried.</summary>
        public static readonly ResourceType[] HudOrder =
        {
            ResourceType.Scrap, ResourceType.Wood, ResourceType.Stone, ResourceType.Glass, ResourceType.Rubber, ResourceType.Cloth,
            ResourceType.Fuel, ResourceType.Oil, ResourceType.Coolant, ResourceType.Water
        };

        public static string Name(ResourceType t) => Names[(int)t];
        public static Color32 Color(ResourceType t) => Colors[(int)t];
        public static float HardnessOf(ResourceType t) => Hardness[(int)t];
        public static float YieldPerVoxel(ResourceType t) => Yield[(int)t];
        public static bool IsFluid(ResourceType t) => Fluid[(int)t];
    }
}
