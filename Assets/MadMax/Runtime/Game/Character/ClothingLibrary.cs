using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;
using BP = MadMax.Game.BodyPart;

namespace MadMax.Game
{
    /// <summary>Wearable garments. Item id in the Inventory = "cloth_" + def id. Layer order is the inflate value.</summary>
    public static class ClothingLibrary
    {
        static List<ClothingDef> all;
        public static IReadOnlyList<ClothingDef> All => all ??= Thermal(Build());

        /// <summary>Cold protection (warmth, °C) and heat relief (cooling; negative traps heat) per garment. They stack.</summary>
        static List<ClothingDef> Thermal(List<ClothingDef> l)
        {
            var t = new Dictionary<string, (float w, float c)>
            {
                { "tshirt", (1f, 1f) }, { "tank", (0.5f, 2f) }, { "jacket", (5f, -1.5f) }, { "vest", (2f, -0.5f) }, { "pants", (2f, 0f) }, { "jeans", (2.5f, -0.5f) },
                { "boots", (1.5f, -0.5f) }, { "gloves", (1.5f, 0f) }, { "goggles", (0.5f, 0.5f) }, { "bandana", (1f, 0.5f) }, { "helmet", (1f, -1f) }, { "shoulder", (0.5f, -1f) },
                { "coat", (10f, -4f) }, { "hoodie", (4f, -1f) }, { "beanie", (3f, -1f) }, { "sunhat", (0f, 3f) }, { "scarf", (3f, -0.5f) }, { "shorts", (0f, 2f) },
                { "duster", (6f, -2f) }, { "poncho", (2f, 0.5f) }, { "hazmat", (3f, -4f) }, { "gasmask", (0.5f, -1f) }, { "sweater", (6f, -2f) }, { "overalls", (2.5f, -0.5f) },
                { "cowboy", (0.5f, 3f) }, { "bomber", (7f, -2f) }, { "shemagh", (1f, 2f) }, { "fingerless", (0.8f, 0f) }, { "combat_boots", (2f, -0.5f) },
                { "welding_mask", (0.5f, -1f) }, { "skull_mask", (0.5f, -0.5f) }, { "schoolbag", (0f, -0.3f) }, { "hikingpack", (0.5f, -0.8f) }, { "framepack", (0.5f, -1f) },
            };
            foreach (var d in l) if (t.TryGetValue(d.id, out var v)) { d.warmth = v.w; d.cooling = v.c; }
            return l;
        }
        public static string ItemId(ClothingDef d) => "cloth_" + d.id;

        public static ClothingDef Get(string id)
        {
            if (id != null && id.StartsWith("cloth_")) id = id.Substring(6);
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        static readonly Vector2 Full = new Vector2(0f, 1f);

        static ClothingDef Def(string id, string name, ClothingSlot slot, float inflate, System.Func<Vector3Int, BP, float, Color32> paint, params (BP part, Vector2 range)[] cover)
        {
            var d = new ClothingDef { id = id, name = name, slot = slot, inflate = inflate, paint = paint };
            foreach (var (p, r) in cover) d.coverage[p] = r;
            return d;
        }

        static Color32 Weave(Vector3Int v, Color32[] ramp, int seed, int bias = 1) => Pal.Pick(ramp, v, seed, bias);

        static List<ClothingDef> Build()
        {
            var denim = new[] { Pal.Hex("1e2a3a"), Pal.Hex("2c3c52"), Pal.Hex("3e5270"), Pal.Hex("56709a") };
            var leather = Pal.Black;
            var khaki = Pal.Olive;
            var dust = new[] { Pal.Hex("8a7a64"), Pal.Hex("a8967a"), Pal.Hex("c4b294") };
            var red = new[] { Pal.Hex("5a1a14"), Pal.Hex("7e261c"), Pal.Hex("a43426") };
            return new List<ClothingDef>
            {
                Def("tshirt", "T-SHIRT", ClothingSlot.Torso, 0.6f, (v, p, t) => Weave(v, Pal.Cream, 801, 1),
                    (BP.Chest, Full), (BP.UpperArmL, new Vector2(0, 0.45f)), (BP.UpperArmR, new Vector2(0, 0.45f)), (BP.Pelvis, new Vector2(0, 0.3f))),
                Def("tank", "TANK TOP", ClothingSlot.Torso, 0.6f, (v, p, t) => Weave(v, khaki, 802, 2), (BP.Chest, new Vector2(0, 0.92f)), (BP.Pelvis, new Vector2(0, 0.3f))),
                Def("jacket", "LEATHER JACKET", ClothingSlot.Outer, 1.4f, (v, p, t) =>
                    p == BP.Chest && v.x == 0 && v.z > 0 ? Pal.Chrome[2] : Weave(v, leather, 803, 2),
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0, 0.35f)), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.92f)), (BP.ForearmR, new Vector2(0, 0.92f))).Gear(waterproof: 0.3f, durability: 1.6f),
                Def("vest", "SCAV VEST", ClothingSlot.Outer, 1.6f, (v, p, t) => (v.y % 5 == 0) ? Pal.Rust[1] : Weave(v, dust, 804, 1), (BP.Chest, new Vector2(0.05f, 0.9f))),
                Def("pants", "CARGO PANTS", ClothingSlot.Legs, 0.7f, (v, p, t) => (p == BP.ThighL || p == BP.ThighR) && t > 0.45f && t < 0.6f ? Weave(v, khaki, 805, 0) : Weave(v, khaki, 806, 2),
                    (BP.Pelvis, Full), (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, new Vector2(0, 0.85f)), (BP.ShinR, new Vector2(0, 0.85f))),
                Def("jeans", "JEANS", ClothingSlot.Legs, 0.7f, (v, p, t) => Weave(v, denim, 807, 1),
                    (BP.Pelvis, Full), (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, new Vector2(0, 0.88f)), (BP.ShinR, new Vector2(0, 0.88f))),
                Def("boots", "BOOTS", ClothingSlot.Feet, 1.2f, (v, p, t) => (p == BP.FootL || p == BP.FootR) && v.y <= -2 ? Pal.Black[0] : Weave(v, Pal.Wood, 808, 1),
                    (BP.FootL, Full), (BP.FootR, Full), (BP.ShinL, new Vector2(0.72f, 1f)), (BP.ShinR, new Vector2(0.72f, 1f))).Gear(durability: 1.5f),
                Def("gloves", "GLOVES", ClothingSlot.Hands, 0.8f, (v, p, t) => Weave(v, leather, 809, 1),
                    (BP.HandL, Full), (BP.HandR, Full), (BP.ForearmL, new Vector2(0.85f, 1f)), (BP.ForearmR, new Vector2(0.85f, 1f))),
                Def("goggles", "GOGGLES", ClothingSlot.Face, 0.9f, (v, p, t) => v.z > 1 && Mathf.Abs(v.x) >= 1 ? Pal.Hex("d08a30") : Pal.Black[0],
                    (BP.Head, new Vector2(0.58f, 0.66f))),
                Def("bandana", "BANDANA", ClothingSlot.Face, 0.9f, (v, p, t) => v.z > -1 ? Weave(v, red, 810, 1) : Pal.Black[1], (BP.Head, new Vector2(0.28f, 0.47f))).Gear(filter: true),
                Def("helmet", "SCRAP HELMET", ClothingSlot.Head, 1.6f, (v, p, t) => Pal.Hash(v, 811) > 0.85f ? Pal.Chrome[2] : Weave(v, Pal.Metal, 812, 2), (BP.Head, new Vector2(0.68f, 1f))).Gear(durability: 3f, style: "raider"),
                Def("shoulder", "SHOULDER ARMOUR", ClothingSlot.Back, 2.4f, (v, p, t) => Weave(v, Pal.Chrome, 813, 1), (BP.UpperArmL, new Vector2(0f, 0.3f))).Gear(durability: 3f, style: "raider"),
                Def("coat", "WINTER PARKA", ClothingSlot.Outer, 2.2f, (v, p, t) => p == BP.Chest && v.y % 6 == 0 ? Pal.Olive[0] : Weave(v, Pal.Olive, 814, 2),
                    (BP.Chest, Full), (BP.Pelvis, Full), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, Full), (BP.ForearmR, Full), (BP.ThighL, new Vector2(0, 0.35f)), (BP.ThighR, new Vector2(0, 0.35f))).Gear(waterproof: 0.3f),
                Def("hoodie", "HOODIE", ClothingSlot.Torso, 0.9f, (v, p, t) => Weave(v, Pal.Metal, 815, 2),
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0, 0.4f)), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.9f)), (BP.ForearmR, new Vector2(0, 0.9f))),
                Def("beanie", "BEANIE", ClothingSlot.Head, 1.2f, (v, p, t) => v.y % 2 == 0 ? red[1] : red[2], (BP.Head, new Vector2(0.72f, 1f))),
                Def("sunhat", "SUN HAT", ClothingSlot.Head, 2.6f, (v, p, t) => Weave(v, dust, 816, 2), (BP.Head, new Vector2(0.8f, 1f))),
                Def("scarf", "SCARF", ClothingSlot.Face, 1.3f, (v, p, t) => (v.x + v.y) % 3 == 0 ? Pal.Cream[2] : red[0], (BP.Head, new Vector2(0.0f, 0.22f)), (BP.Chest, new Vector2(0.9f, 1f))).Gear(filter: true),
                Def("shorts", "SHORTS", ClothingSlot.Legs, 0.7f, (v, p, t) => Weave(v, khaki, 817, 1), (BP.Pelvis, Full), (BP.ThighL, new Vector2(0, 0.55f)), (BP.ThighR, new Vector2(0, 0.55f))),

                // ---- roadmap 3: weather, hazard and style gear
                Def("duster", "LEATHER DUSTER", ClothingSlot.Outer, 1.8f, (v, p, t) => p == BP.Chest && t > 0.9f ? Pal.Wood[0] : Weave(v, Pal.Wood, 818, 1),
                    (BP.Chest, Full), (BP.Pelvis, Full), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.9f)), (BP.ForearmR, new Vector2(0, 0.9f)),
                    (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, new Vector2(0, 0.55f)), (BP.ShinR, new Vector2(0, 0.55f))).Gear(waterproof: 0.6f, durability: 1.6f, style: "drifter"),
                Def("poncho", "RAIN PONCHO", ClothingSlot.Outer, 2.4f, (v, p, t) => v.y % 4 == 0 ? Pal.Rust[3] : v.y % 4 == 1 ? Pal.Sand[2] : Weave(v, Pal.Olive, 819, 2),
                    (BP.Chest, Full), (BP.UpperArmL, new Vector2(0, 0.6f)), (BP.UpperArmR, new Vector2(0, 0.6f)), (BP.Pelvis, new Vector2(0, 0.6f))).Gear(waterproof: 0.85f),
                Def("hazmat", "HAZMAT SUIT", ClothingSlot.Outer, 2.0f, (v, p, t) => t < 0.06f || t > 0.94f ? Pal.Black[1] : Weave(v, Pal.Ochre, 820, 3),
                    (BP.Chest, Full), (BP.Pelvis, Full), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, Full), (BP.ForearmR, Full), (BP.HandL, Full), (BP.HandR, Full),
                    (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, Full), (BP.ShinR, Full), (BP.FootL, Full), (BP.FootR, Full)).Gear(waterproof: 0.9f, radiation: 0.6f, durability: 0.7f, style: "hazmat"),
                Def("gasmask", "GAS MASK", ClothingSlot.Face, 1.1f, (v, p, t) => v.z > 1 && v.y >= 4 && v.y <= 5 && Mathf.Abs(v.x) >= 1 && Mathf.Abs(v.x) <= 2 ? Pal.Glass[3] : Weave(v, Pal.Black, 821, 2),
                    (BP.Head, new Vector2(0.26f, 0.66f))).Gear(radiation: 0.3f, filter: true, style: "hazmat").Prop(BP.Head, a =>
                    {
                        var g = new VoxelGrid();
                        float y = 3.2f * a.height;
                        g.CylZ(0, y, 1.4f, 4, 6, p => p.z == 6 ? Pal.Black[0] : Pal.Pick(Pal.Olive, p, 822, 1));
                        return g;
                    }),
                Def("sweater", "WOOL SWEATER", ClothingSlot.Torso, 0.9f, (v, p, t) => p == BP.Chest && t > 0.55f && t < 0.7f && ((v.x + v.y) & 1) == 0 ? Pal.Cream[3] : Weave(v, Pal.Moss, 823, 2),
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0, 0.4f)), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.95f)), (BP.ForearmR, new Vector2(0, 0.95f))),
                Def("overalls", "WORK OVERALLS", ClothingSlot.Legs, 0.8f, (v, p, t) =>
                    {
                        if (p != BP.Chest) return Weave(v, Pal.Navy, 824, 2);
                        bool strap = Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3;
                        if (t > 0.3f && v.z < 0 && !strap) return default;                   // open back: two straps
                        if (t > 0.72f && !strap) return default;
                        return v.z > 0 && t > 0.6f && t < 0.66f && strap ? Pal.Chrome[2] : Weave(v, Pal.Navy, 825, 2);
                    },
                    (BP.Chest, new Vector2(0, 0.9f)), (BP.Pelvis, Full), (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, new Vector2(0, 0.9f)), (BP.ShinR, new Vector2(0, 0.9f))).Gear(durability: 1.4f),
                Def("cowboy", "COWBOY HAT", ClothingSlot.Head, 1.5f, (v, p, t) => t < 0.84f ? Pal.Black[1] : Weave(v, Pal.Wood, 826, 2), (BP.Head, new Vector2(0.78f, 1f)))
                    .Gear(style: "drifter").Prop(BP.Head, a =>
                    {
                        var g = new VoxelGrid();
                        int y = Mathf.RoundToInt(7f * a.height);
                        for (int x = -6; x <= 6; x++)
                        for (int z = -6; z <= 6; z++)
                        {
                            float r = Mathf.Sqrt(x * x + (z - 0.5f) * (z - 0.5f));
                            if (r > 5.6f || r < 2.5f) continue;
                            g.Set(x, y + (Mathf.Abs(x) >= 5 ? 1 : 0), z, p => Pal.Pick(Pal.Wood, p, 827, 2));    // brim, curled at the sides
                        }
                        return g;
                    }),
                Def("bomber", "BOMBER JACKET", ClothingSlot.Outer, 1.4f, (v, p, t) =>
                        p == BP.Chest && t > 0.88f ? Pal.Pick(Pal.Cream, v, 828, 2) : p == BP.Chest && v.x == 0 && v.z > 0 ? Pal.Chrome[2] : t < 0.1f ? Pal.RigGreen[1] : Weave(v, Pal.RigGreen, 829, 3),
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0, 0.3f)), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.95f)), (BP.ForearmR, new Vector2(0, 0.95f))).Gear(durability: 1.3f),
                Def("shemagh", "SHEMAGH", ClothingSlot.Face, 1.3f, (v, p, t) => ((v.x + v.y) & 1) == 0 && ((v.x - v.y) & 3) == 0 ? Pal.Black[1] : Pal.Cream[2],
                    (BP.Head, new Vector2(0f, 0.5f)), (BP.Chest, new Vector2(0.88f, 1f))).Gear(filter: true),
                Def("fingerless", "FINGERLESS GLOVES", ClothingSlot.Hands, 0.8f, (v, p, t) => Weave(v, Pal.Black, 830, 2),
                    (BP.HandL, new Vector2(0, 0.55f)), (BP.HandR, new Vector2(0, 0.55f)), (BP.ForearmL, new Vector2(0.88f, 1f)), (BP.ForearmR, new Vector2(0.88f, 1f))),
                Def("combat_boots", "COMBAT BOOTS", ClothingSlot.Feet, 1.3f, (v, p, t) => (p == BP.FootL || p == BP.FootR) && v.y <= -2 ? Pal.Black[0] : v.x == 0 && v.z > 0 ? Pal.Metal[2] : Weave(v, Pal.Black, 831, 2),
                    (BP.FootL, Full), (BP.FootR, Full), (BP.ShinL, new Vector2(0.6f, 1f)), (BP.ShinR, new Vector2(0.6f, 1f))).Gear(durability: 2f),
                Def("welding_mask", "WELDING MASK", ClothingSlot.Face, 1.6f, (v, p, t) =>
                        v.z < 0 ? (t > 0.7f && t < 0.76f ? Pal.Black[0] : default)                                  // only a strap round the back
                        : v.z > 1 && v.y >= 4 && v.y <= 5 && Mathf.Abs(v.x) <= 2 ? Pal.RigGreen[0] : Weave(v, Pal.Metal, 832, 2),
                    (BP.Head, new Vector2(0.2f, 0.85f))).Gear(durability: 2f),
                Def("skull_mask", "SKULL MASK", ClothingSlot.Face, 1.2f, (v, p, t) =>
                    {
                        if (v.z < 0) return t > 0.62f && t < 0.68f ? Pal.Black[1] : default;
                        int ax = Mathf.Abs(v.x);
                        if (v.z > 1 && v.y >= 4 && v.y <= 5 && ax >= 1 && ax <= 2) return Pal.Black[0];              // eye sockets
                        if (v.z > 2 && v.y == 3 && ax == 0) return Pal.Black[0];                                   // nose
                        if (v.z > 1 && v.y <= 1) return (v.x & 1) == 0 ? Pal.Cream[4] : Pal.Black[1];                // teeth
                        return Pal.Pick(Pal.Cream, v, 833, 2);
                    },
                    (BP.Head, new Vector2(0.22f, 0.8f))).Gear(style: "raider"),

                // ---- backpacks: straps on the chest, the bag as a rigid prop on the back
                Def("schoolbag", "SCHOOL BAG", ClothingSlot.Pack, 0.7f, Straps(Pal.Navy), (BP.Chest, new Vector2(0.3f, 0.98f))).Gear(carry: 8f).Prop(BP.Chest, a =>
                    {
                        var g = new VoxelGrid();
                        g.Box(-3, 3, -6, 3, 9, -4, p => Pal.Pick(Pal.Navy, p, 834, 2));
                        g.Box(-2, 3, -7, 2, 5, -7, p => Pal.Pick(Pal.Navy, p, 835, 3));
                        g.Box(-2, 6, -7, 2, 6, -7, Pal.Solid(Pal.Chrome[2]));
                        return g;
                    }),
                Def("hikingpack", "HIKING PACK", ClothingSlot.Pack, 0.7f, Straps(Pal.Black), (BP.Chest, new Vector2(0.2f, 0.98f))).Gear(carry: 18f).Prop(BP.Chest, a =>
                    {
                        var g = new VoxelGrid();
                        g.Box(-4, 1, -8, 4, 12, -4, p => Pal.Pick(Pal.Crimson, p, 836, 2));
                        g.Box(-4, 13, -8, 4, 13, -4, p => Pal.Pick(Pal.Crimson, p, 837, 1));
                        g.Box(-2, 3, -9, 2, 7, -9, p => Pal.Pick(Pal.Crimson, p, 838, 3));
                        g.CylX(15, -6, 1.6f, -5, 5, p => Pal.Pick(Pal.Olive, p, 839, 2));                         // bedroll
                        g.Box(5, 3, -6, 5, 6, -6, Pal.Ramp(Pal.Glass, 3));                                          // bottle
                        return g;
                    }),
                Def("framepack", "FRAME PACK", ClothingSlot.Pack, 0.7f, Straps(Pal.Olive), (BP.Chest, new Vector2(0.1f, 0.98f))).Gear(carry: 28f).Prop(BP.Chest, a =>
                    {
                        var g = new VoxelGrid();
                        foreach (int x in new[] { -5, 5 }) g.Box(x, -3, -5, x, 16, -5, Pal.Ramp(Pal.Chrome, 2));
                        foreach (int y in new[] { -3, 6, 16 }) g.Box(-5, y, -5, 5, y, -5, Pal.Ramp(Pal.Chrome, 1));
                        g.Box(-4, 2, -10, 4, 14, -6, p => Pal.Pick(Pal.Olive, p, 840, 2));
                        g.Box(-4, 8, -11, 4, 8, -6, Pal.Solid(Pal.Black[1]));                                        // cinch strap
                        g.CylX(-1, -8, 2f, -4, 4, p => Pal.Pick(Pal.Navy, p, 841, 2));                             // sleeping bag
                        return g;
                    }),
            };
        }

        /// <summary>Backpack straps over the shoulders and down the front; everything else stays bare.</summary>
        static System.Func<Vector3Int, BP, float, Color32> Straps(Color32[] ramp) => (v, p, t) =>
            (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) && (v.z > 0 || t > 0.85f) ? Pal.Pick(ramp, v, 842, 1) : default;

        static ClothingDef Gear(this ClothingDef d, float carry = 0f, float waterproof = 0f, float radiation = 0f, bool filter = false, float durability = 1f, string style = null)
        {
            d.carry = carry; d.waterproof = waterproof; d.radiation = radiation; d.filter = filter; d.durability = durability; d.style = style;
            return d;
        }

        static ClothingDef Prop(this ClothingDef d, BP bone, System.Func<Appearance, VoxelGrid> make) { d.prop = make; d.propBone = bone; return d; }

        /// <summary>Default outfit for a new wastelander.</summary>
        public static readonly string[] Starter = { "tshirt", "jacket", "pants", "boots", "shoulder" };
    }
}
