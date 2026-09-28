using UnityEngine;

namespace MadMax.Items
{
    public enum ItemCategory { Tool, Weapon, Throwable, Food, Consumable, Seed, Ammo, Clothing, Media, Kit, Crop, Other }

    /// <summary>Display and weight data for any inventory id (tools, clothing, media, kits, ammo).</summary>
    public static class ItemCatalog
    {
        public static ItemCategory Category(string id)
        {
            if (id.StartsWith("tool_pipe") || id == ItemIds.Machete) return ItemCategory.Weapon;
            if (id.StartsWith("tool_")) return ItemCategory.Tool;
            if (id.StartsWith("ammo_")) return ItemCategory.Ammo;
            if (id.StartsWith("throw_")) return ItemCategory.Throwable;
            if (id.StartsWith("food_") || id.StartsWith("drink_")) return ItemCategory.Food;
            if (id.StartsWith("use_") || id.StartsWith("med_") || id.StartsWith("farm_")) return ItemCategory.Consumable;
            if (id.StartsWith("seed_") || id.StartsWith("sapling_")) return ItemCategory.Seed;
            if (id.StartsWith("crop_")) return ItemCategory.Crop;
            if (id.StartsWith("cloth_")) return ItemCategory.Clothing;
            if (id.StartsWith("book_") || id.StartsWith("vhs_")) return ItemCategory.Media;
            if (id.StartsWith("kit_")) return ItemCategory.Kit;
            return ItemCategory.Other;
        }

        public static float Weight(string id) => Category(id) switch
        {
            ItemCategory.Tool => id == ItemIds.Sledgehammer ? 5f : 1.5f,
            ItemCategory.Weapon => 2.5f,
            ItemCategory.Ammo => 0.05f,
            ItemCategory.Throwable => 0.8f,
            ItemCategory.Food => 0.4f,
            ItemCategory.Consumable => 0.3f,
            ItemCategory.Seed => 0.02f,
            ItemCategory.Crop => 0.2f,
            ItemCategory.Clothing => 0.8f,
            ItemCategory.Media => 0.4f,
            ItemCategory.Kit => 6f,
            _ => 1f
        };

        public static float ResourceWeight(ResourceType t) => t switch
        {
            ResourceType.Scrap => 0.6f, ResourceType.Wood => 0.5f, ResourceType.Stone => 1f, ResourceType.Glass => 0.3f,
            ResourceType.Rubber => 0.4f, ResourceType.Cloth => 0.1f, ResourceType.Fuel => 0.75f, ResourceType.Oil => 0.9f, ResourceType.Coolant => 1f,
            ResourceType.Water => 1f, ResourceType.DirtyWater => 1f, ResourceType.Ethanol => 0.8f,
            ResourceType.Sand or ResourceType.Clay or ResourceType.Laterite or ResourceType.Rubble or ResourceType.Slag => 1.2f,
            ResourceType.Iron or ResourceType.Copper or ResourceType.Bronze => 0.8f, ResourceType.Aluminium => 0.3f,
            ResourceType.Asphalt or ResourceType.Concrete => 1.5f, _ => 0.6f
        };

        public static Color32 Color(string id) => Category(id) switch
        {
            ItemCategory.Tool => new Color32(170, 170, 178, 255),
            ItemCategory.Weapon => new Color32(200, 80, 60, 255),
            ItemCategory.Ammo => new Color32(220, 180, 60, 255),
            ItemCategory.Throwable => new Color32(240, 120, 40, 255),
            ItemCategory.Food => FoodLibrary.Get(id) != null ? FoodLibrary.Get(id).color : new Color32(200, 160, 90, 255),
            ItemCategory.Consumable => new Color32(120, 200, 210, 255),
            ItemCategory.Seed => new Color32(140, 180, 70, 255),
            ItemCategory.Crop => new Color32(230, 220, 200, 255),
            ItemCategory.Clothing => new Color32(120, 150, 200, 255),
            ItemCategory.Media => new Color32(200, 120, 220, 255),
            ItemCategory.Kit => new Color32(176, 122, 76, 255),
            _ => new Color32(160, 160, 160, 255)
        };

        public static string Name(string id)
        {
            var m = MadMax.RPG.MediaLibrary.Get(id);
            if (m != null) return m.name;
            if (id.StartsWith("cloth_")) { var c = MadMax.Game.ClothingLibrary.Get(id); if (c != null) return c.name; }
            return ItemIds.Name(id);
        }

        /// <summary>Total carried weight in kg.</summary>
        public static float TotalWeight(Inventory inv)
        {
            float w = 0f;
            for (int t = 1; t < ResourceInfo.Count; t++) w += inv.Get((ResourceType)t) * ResourceWeight((ResourceType)t);
            foreach (var kv in inv.Items) w += kv.Value * Weight(kv.Key);
            return w;
        }
    }
}
