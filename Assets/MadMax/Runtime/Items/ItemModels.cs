using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Items
{
    /// <summary>Small voxel models for inventory items that have no world mesh of their own (food, seeds, media,
    /// clothes, supplies). Used for hotbar icons.</summary>
    public static class ItemModels
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>A fish in profile (+X head), lying on +Z <paramref name="z"/>.</summary>
        static void Fish(VoxelGrid g, Color32 c, Color32 dark, int z = 0)
        {
            for (int x = -5; x <= 4; x++)
            {
                int h = x < -3 ? 0 : x > 2 ? 1 : 2;                                                     // half height along the body
                for (int y = 2 - h; y <= 3 + h; y++) g.Set(x, y, z, Pal.Solid(y > 3 ? dark : c));
            }
            g.Box(-7, 1, z, -6, 5, z, Pal.Solid(dark));                                                   // tail
            g.Set(3, 3, z + 1, Pal.Solid(Pal.Black[0]));                                                  // eye
        }

        public static Mesh Get(string id)
        {
            if (cache.TryGetValue(id, out var m) && m) return m;
            var g = new VoxelGrid();
            var c = ItemCatalog.Color(id);
            var dark = new Color32((byte)(c.r * 0.6f), (byte)(c.g * 0.6f), (byte)(c.b * 0.6f), 255);
            switch (ItemCatalog.Category(id))
            {
                case ItemCategory.Food:
                    if (id.StartsWith("drink_") || id == "food_jam" || id.StartsWith("food_jar_") || id.StartsWith("food_pickle") || id == "food_sauerkraut") { g.CylY(0, 0, 2f, 0, 6, Pal.Solid(c)); g.Box(0, 7, 0, 0, 8, 0, Pal.Solid(Pal.Cream[2])); }
                    else if (id.StartsWith("food_can") || id == "food_ration" || id == "food_stew" || id == "food_soup" || id == "food_meat_stew" || id == "food_fish_soup") { g.CylY(0, 0, 2.6f, 0, 5, p => p.y == 0 || p.y == 5 ? Pal.Chrome[2] : c); g.Box(-2, 2, 2, 2, 3, 2, Pal.Solid(Pal.Cream[3])); }
                    else if (id.StartsWith("food_fish") || id == "food_glowfish_cooked") Fish(g, c, dark);
                    else if (id == "food_corn" || id == "food_corn_roast" || id == "food_carrot") { g.Box(0, 0, 0, 0, 6, 0, Pal.Solid(c)); g.Box(-1, 1, 0, 1, 5, 0, Pal.Solid(c)); g.Box(0, 7, 0, 0, 8, 0, Pal.Solid(Pal.Hex("5a8a2a"))); }
                    else
                    {
                        for (int x = -2; x <= 2; x++) for (int y = 0; y <= 4; y++) for (int z = -2; z <= 2; z++)
                            if (new Vector3(x, y - 2, z).sqrMagnitude <= 5f) g.Set(x, y, z, Pal.Solid(y >= 3 ? c : x + z > 0 ? dark : c));
                        g.Set(0, 5, 0, Pal.Solid(Pal.Hex("4a6a20")));
                    }
                    break;
                case ItemCategory.Seed:
                    if (id.StartsWith("sapling_")) { g.Box(-1, 0, -1, 1, 1, 1, Pal.Ramp(Pal.Rust, 1)); g.Box(0, 2, 0, 0, 5, 0, Pal.Ramp(Pal.Wood, 1)); g.Box(-1, 5, 0, 1, 6, 0, Pal.Solid(Pal.Hex("46862c"))); }
                    else { g.Box(-2, 0, -1, 2, 5, 1, Pal.Ramp(Pal.Cream, 1)); g.Box(-1, 2, 2, 1, 3, 2, Pal.Solid(c)); g.Box(-2, 6, 0, 2, 6, 0, Pal.Solid(Pal.Cream[0])); }
                    break;
                case ItemCategory.Media:
                    if (id.StartsWith("vhs_")) { g.Box(-4, 0, -1, 4, 5, 1, Pal.Ramp(Pal.Black, 2)); g.Box(-3, 2, 2, 3, 3, 2, Pal.Solid(c)); }
                    else { g.Box(-3, 0, -1, 3, 7, 1, p => p.x == -3 ? Pal.Cream[2] : c); g.Box(-2, 5, 2, 2, 5, 2, Pal.Solid(Pal.Cream[3])); }
                    break;
                case ItemCategory.Clothing:
                    g.Box(-3, 0, 0, 3, 6, 0, Pal.Solid(c)); g.Box(-5, 4, 0, 5, 6, 0, Pal.Solid(c)); g.Box(-1, 6, 0, 1, 6, 0, Pal.Solid(dark));
                    break;
                case ItemCategory.Throwable:
                    if (id == "throw_dynamite") { for (int x = -2; x <= 2; x += 2) g.CylY(x, 0, 1f, 0, 6, Pal.Ramp(Pal.Crimson, 2)); g.Box(-2, 3, 1, 2, 3, 1, Pal.Solid(Pal.Cream[3])); g.Box(0, 7, 0, 0, 9, 0, Pal.Solid(Pal.Black[2])); }
                    else if (id == "throw_pipebomb") { g.CylY(0, 0, 1.6f, 0, 6, Pal.Ramp(Pal.Metal, 2)); g.CylY(0, 0, 2f, 0, 0, Pal.Ramp(Pal.Metal, 1)); g.CylY(0, 0, 2f, 6, 6, Pal.Ramp(Pal.Metal, 1)); g.Box(0, 7, 0, 0, 8, 0, Pal.Solid(Pal.Black[2])); }
                    else { g.CylY(0, 0, 1.6f, 0, 5, Pal.Ramp(Pal.Glass, 2)); g.Box(0, 6, 0, 0, 8, 0, Pal.Solid(Pal.Cream[2])); g.Set(0, 9, 0, Pal.Solid(Pal.Amber)); }
                    break;
                case ItemCategory.Ammo when id.StartsWith("bait_"):
                    g.CylY(0, 0, 2.4f, 0, 3, p => p.y == 3 ? Pal.Chrome[1] : Pal.Chrome[2]);                  // bait tin
                    var bc = id == "bait_worms" ? Pal.Hex("c87a78") : id == "bait_insects" ? Pal.Hex("3a3020") : id == "bait_meat" ? Pal.Hex("a83a3a") : Pal.Hex("e0c040");
                    g.Box(-1, 4, -1, 1, 4, 1, p => ((p.x + p.z) & 1) == 0 ? bc : Pal.Chrome[2]);
                    if (id == "bait_worms") { g.Set(-2, 5, 0, Pal.Solid(bc)); g.Set(-1, 6, 0, Pal.Solid(bc)); }
                    break;
                case ItemCategory.Ammo:
                    for (int i = -2; i <= 2; i += 2) { g.Box(i, 0, 0, i, 3, 0, Pal.Solid(Pal.Hex("b02818"))); g.Set(i, 4, 0, Pal.Solid(Pal.Bronze[2])); }
                    break;
                case ItemCategory.Crop:
                    for (int x = -2; x <= 2; x++) for (int y = 0; y <= 3; y++) if (Mathf.Abs(x) + Mathf.Abs(y - 2) <= 3) g.Set(x, y, 0, Pal.Solid(c));
                    break;
                default:
                    if (id == ItemIds.Canteen) { g.CylZ(0, 3, 3f, 0, 1, Pal.Ramp(Pal.Olive, 2)); g.Box(0, 6, 0, 0, 7, 0, Pal.Solid(Pal.Black[1])); }
                    else if (id == ItemIds.Sponge) { g.Box(-3, 0, -2, 3, 3, 2, Pal.Solid(Pal.Hex("e8c848"))); g.Box(-3, 3, -2, 3, 3, 2, Pal.Solid(Pal.Hex("5aa050"))); g.Set(-1, 1, 2, Pal.Solid(Pal.Hex("b89830"))); g.Set(2, 2, 2, Pal.Solid(Pal.Hex("b89830"))); }
                    else if (id == ItemIds.Pills) { g.CylY(0, 0, 1.6f, 0, 4, Pal.Solid(Pal.Cream[3])); g.Box(-1, 5, -1, 1, 5, 1, Pal.Solid(Pal.TailR)); }
                    else if (id == ItemIds.Fertilizer) { g.Box(-3, 0, -2, 3, 5, 2, Pal.Ramp(Pal.Sand, 1)); g.Box(-1, 3, 3, 1, 4, 3, Pal.Solid(Pal.Hex("46862c"))); }
                    else if (id.StartsWith("bp_"))
                    {
                        g.CylX(0, 0, 1.6f, -4, 4, p => p.x % 3 == 0 ? Pal.Cream[4] : Pal.Navy[2]);                       // rolled blueprint
                        g.Box(-4, 0, 1, 4, 0, 3, p => (p.x + p.z) % 3 == 0 ? Pal.Cream[4] : Pal.Navy[3]);
                    }
                    else if (id.StartsWith("dye_"))
                    {
                        var ramp = Pal.DyeRamp(System.Array.IndexOf(ItemIds.Dyes, id)) ?? Pal.Cream;
                        g.CylY(0, 0, 2.2f, 0, 4, Pal.Ramp(Pal.Glass, 3)); g.CylY(0, 0, 1.6f, 1, 3, Pal.Ramp(ramp, 2));
                        g.Box(-1, 5, -1, 1, 5, 1, Pal.Ramp(Pal.Wood, 1));
                    }
                    else if (id == "trophy_plate")
                    {
                        g.Box(-6, 0, 0, 6, 5, 0, p => p.y == 0 || p.y == 5 || p.x == -6 || p.x == 6 ? Pal.Chrome[1] : (p.y == 2 || p.y == 3) && p.x % 2 != 0 && System.Math.Abs(p.x) < 5 ? Pal.Black[1] : Pal.Ochre[3]);
                    }
                    else if (id == "trophy_ornament")
                    {
                        g.Box(0, 0, -1, 0, 6, 1, Pal.Ramp(Pal.Chrome, 2));
                        for (int i = 1; i <= 4; i++) { g.Box(-i, 3 + i / 2, -1, -i, 5, -1, Pal.Ramp(Pal.Chrome, 3)); g.Box(i, 3 + i / 2, -1, i, 5, -1, Pal.Ramp(Pal.Chrome, 3)); }
                    }
                    else if (id == "trophy_hubcap") { g.CylZ(0, 4, 4.4f, 0, 0, p => (p.x * p.x + (p.y - 4) * (p.y - 4)) < 3 ? Pal.Chrome[3] : Pal.Chrome[1 + ((p.x + p.y) & 1)]); g.CylZ(0, 4, 1.4f, 1, 1, Pal.Ramp(Pal.Chrome, 2)); }
                    else if (id.StartsWith("trophy_fish"))
                    {
                        g.Box(-7, 0, -1, 7, 5, -1, Pal.Ramp(Pal.Wood, 2, 1093));                                   // plank
                        var fc = id.EndsWith("mutant") ? Pal.Hex("8aff5a") : Pal.Hex("8a9a4a");
                        Fish(g, fc, new Color32((byte)(fc.r * 0.6f), (byte)(fc.g * 0.6f), (byte)(fc.b * 0.6f), 255), 1);
                    }
                    else if (id == "trophy_skull")
                    {
                        g.Box(-2, 0, -1, 2, 5, 1, Pal.Ramp(Pal.Cream, 2)); g.Box(-1, -2, 0, 1, 0, 1, Pal.Ramp(Pal.Cream, 1));
                        g.Set(-1, 3, 2, Pal.Solid(Pal.Black[0])); g.Set(1, 3, 2, Pal.Solid(Pal.Black[0]));
                        g.Tube(new Vector3(-2, 5, 0), new Vector3(-6, 7, 0), 0.5f, Pal.Ramp(Pal.Cream, 3)); g.Tube(new Vector3(2, 5, 0), new Vector3(6, 7, 0), 0.5f, Pal.Ramp(Pal.Cream, 3));
                    }
                    else { g.Box(-3, 0, -2, 3, 1, 2, Pal.Ramp(Pal.Cream, 3)); }
                    break;
            }
            g.Bevel();
            m = VoxelMesher.Build(g, "Item_" + id);
            cache[id] = m;
            return m;
        }
    }
}
