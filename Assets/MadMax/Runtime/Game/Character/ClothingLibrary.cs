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
                { "vest_scrap", (1f, -1.5f) }, { "vest_tyre", (1.5f, -1.5f) }, { "vest_chitin", (1f, -1f) }, { "vest_kevlar", (1f, -1f) }, { "arm_guards", (0.5f, -0.5f) },
                { "gauntlets", (0.5f, -0.3f) }, { "shin_guards", (0.3f, -0.3f) }, { "moto_helmet", (1f, -1.5f) },
                { "dive_helmet", (2f, -2f) }, { "dive_suit", (7f, -3f) }, { "air_tank", (0f, -0.5f) },
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
                Def("helmet", "SCRAP HELMET", ClothingSlot.Head, 1.6f, (v, p, t) => Pal.Hash(v, 811) > 0.85f ? Pal.Chrome[2] : Weave(v, Pal.Metal, 812, 2), (BP.Head, new Vector2(0.68f, 1f))).Gear(durability: 3f, style: "raider").Armor(0.5f, 0.3f, 0.45f, 0.3f, 0.2f, 1.5f, 0.2f, Scrap),
                Def("shoulder", "SHOULDER ARMOUR", ClothingSlot.Back, 2.4f, (v, p, t) => Weave(v, Pal.Chrome, 813, 1), (BP.UpperArmL, new Vector2(0f, 0.3f))).Gear(durability: 3f, style: "raider").Armor(0.35f, 0.2f, 0.2f, 0.1f, 0.2f, 1.5f, 0.25f, Scrap),
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
                // ---- user additions: hard-hat diving (a brass helmet on a bolted collar, a canvas suit, an air tank)
                Def("dive_helmet", "BRASS DIVING HELMET", ClothingSlot.Head, 2.6f, (v, p, t) => Pal.Pick(Pal.Bronze, v, 890, 3), (BP.Head, Full))
                    .Gear(waterproof: 1f, radiation: 0.3f, filter: true, durability: 3f, style: "diver").Prop(BP.Head, a =>
                    {
                        var g = new VoxelGrid();
                        float cy = 3.4f * a.height;
                        for (int x = -6; x <= 6; x++)
                        for (int y = -3; y <= 10; y++)
                        for (int z = -6; z <= 6; z++)
                        {
                            float r = Mathf.Sqrt(x * x + (y - cy) * (y - cy) + z * z);
                            if (r > 6.2f || r < 5f) continue;
                            bool port = (z > 3 && Mathf.Abs(x) <= 2 && Mathf.Abs(y - cy) <= 2) || (Mathf.Abs(x) > 4 && Mathf.Abs(z) <= 1 && Mathf.Abs(y - cy) <= 1);
                            bool rim = (z > 3 && Mathf.Abs(x) <= 3 && Mathf.Abs(y - cy) <= 3) && !port;
                            g.Set(x, y, z, port ? Pal.Solid(Pal.Glass[3]) : rim ? Pal.Solid(Pal.Bronze[1]) : p => Pal.Pick(Pal.Bronze, p, 891, 3));
                        }
                        for (int x = -7; x <= 7; x++) for (int z = -6; z <= 6; z++) if (x * x + z * z <= 42) g.Set(x, -3, z, (x + z) % 4 == 0 ? Pal.Solid(Pal.Chrome[2]) : Pal.Solid(Pal.Bronze[1]));   // bolted collar
                        g.Tube(new Vector3(0, cy + 1f, -6), new Vector3(0, cy - 2f, -9), 0.6f, Pal.Solid(Pal.Black[1]));   // air hose
                        return g;
                    }),
                Def("dive_suit", "CANVAS DIVING SUIT", ClothingSlot.Outer, 2.0f, (v, p, t) => (p == BP.FootL || p == BP.FootR) ? Pal.Black[0] : t > 0.9f && (p == BP.ForearmL || p == BP.ForearmR) ? Pal.Black[1] : Weave(v, Pal.Cream, 892, 1),
                    (BP.Chest, Full), (BP.Pelvis, Full), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, Full), (BP.ForearmR, Full), (BP.HandL, Full), (BP.HandR, Full),
                    (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, Full), (BP.ShinR, Full), (BP.FootL, Full), (BP.FootR, Full)).Gear(waterproof: 1f, radiation: 0.45f, durability: 1.6f, style: "diver"),   // keeps foul water off the skin
                Def("air_tank", "DIVER'S AIR TANK", ClothingSlot.Pack, 0.7f, Straps(Pal.Black), (BP.Chest, new Vector2(0.3f, 0.98f))).Prop(BP.Chest, a =>
                    {
                        var g = new VoxelGrid();
                        foreach (int x in new[] { -2, 2 }) g.CylY(x, -6, 2.2f, 0, 14, p => p.y == 14 ? Pal.Chrome[3] : p.y % 5 == 0 ? Pal.Black[1] : Pal.Crimson[2]);
                        g.Box(-3, 15, -6, 3, 15, -6, Pal.Solid(Pal.Chrome[2]));                        // valve bar
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

                // ---- roadmap 12: armour, layered over the clothes (protection in Armor(): melee, shot, crash, fall, burn)
                Def("vest_scrap", "SCRAP PLATE VEST", ClothingSlot.Vest, 2.6f, (v, p, t) =>
                    {
                        if (p == BP.Chest && t > 0.84f) return (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) ? Pal.Black[1] : default;   // straps over the shoulders
                        int row = Mathf.FloorToInt(t * 5f);
                        if (v.y % 4 == 0 && (v.x & 1) == 0) return Pal.Chrome[1];                                            // rivet rows
                        return (row + (v.x > 0 ? 1 : 0)) % 2 == 0 ? Pal.Pick(Pal.Metal, v, 850 + row, 2) : Pal.Pick(Pal.Rust, v, 855 + row, 2);
                    },
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0f, 0.3f))).Gear(durability: 3f, style: "raider").Armor(0.55f, 0.4f, 0.3f, 0.1f, 0.25f, 7f, 0.6f, Scrap),
                Def("vest_chitin", "CHITIN VEST", ClothingSlot.Vest, 2.3f, (v, p, t) =>
                    {
                        if (p == BP.Chest && t > 0.84f) return (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) ? Pal.Wood[0] : default;   // hide straps
                        int row = Mathf.FloorToInt(t * 6f);
                        if (((v.x + 64) % 4 == 0) || row % 2 == 0 && v.y % 3 == 0) return Pal.Black[1];                   // plate seams
                        return Pal.Pick(row % 2 == 0 ? Pal.Rust : Pal.Ochre, v, 865 + row, 2);
                    },
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0f, 0.3f))).Gear(durability: 2.6f, style: "raider").Armor(0.5f, 0.35f, 0.3f, 0.15f, 0.2f, 3.5f, 0.2f, Leather),
                Def("vest_tyre", "TYRE-RUBBER VEST", ClothingSlot.Vest, 2.4f, (v, p, t) =>
                    {
                        if (p == BP.Chest && t > 0.84f) return (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) ? Pal.Black[0] : default;
                        return ((v.x + v.y) & 3) < 2 ? Pal.Tire[2] : Pal.Tire[1];                                           // tread
                    },
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0f, 0.3f))).Gear(durability: 2f).Armor(0.45f, 0.25f, 0.5f, 0.2f, 0f, 4.5f, 0.1f, Rubber),
                Def("vest_kevlar", "KEVLAR VEST", ClothingSlot.Vest, 2.0f, (v, p, t) =>
                    {
                        if (p == BP.Chest && t > 0.84f) return (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) ? Pal.Navy[0] : default;
                        if (p == BP.Chest && t < 0.4f && v.z > 0 && v.x % 3 != 0) return Pal.Pick(Pal.Olive, v, 857, 1);      // pouches
                        return Pal.Pick(Pal.Navy, v, 858, 1);
                    },
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0f, 0.25f))).Gear(durability: 2.2f).Armor(0.3f, 0.75f, 0.2f, 0.1f, 0.1f, 3.5f, 0.05f, Cloth),
                Def("arm_guards", "ARM GUARDS", ClothingSlot.Arms, 2.1f, (v, p, t) => (v.y % 3 == 0 && ((v.x + v.z) & 1) == 0) ? Pal.Chrome[2] : Pal.Pick(Pal.Wood, v, 859, 1),
                    (BP.UpperArmL, new Vector2(0.35f, 1f)), (BP.UpperArmR, new Vector2(0.35f, 1f)), (BP.ForearmL, new Vector2(0f, 0.85f)), (BP.ForearmR, new Vector2(0f, 0.85f)))
                    .Gear(durability: 2f, style: "raider").Armor(0.45f, 0.15f, 0.3f, 0.2f, 0.3f, 2f, 0.25f, Leather),
                Def("gauntlets", "GAUNTLETS", ClothingSlot.Hands, 1.3f, (v, p, t) => (p == BP.HandL || p == BP.HandR) && v.y % 2 == 0 ? Pal.Chrome[1] : Pal.Pick(Pal.Metal, v, 860, 2),
                    (BP.HandL, Full), (BP.HandR, Full), (BP.ForearmL, new Vector2(0.75f, 1f)), (BP.ForearmR, new Vector2(0.75f, 1f)))
                    .Gear(durability: 3f).Armor(0.5f, 0.1f, 0.35f, 0.3f, 0.45f, 1.2f, 0.2f, Iron),
                Def("shin_guards", "SHIN GUARDS", ClothingSlot.Shins, 1.9f, (v, p, t) => t < 0.12f ? Pal.Pick(Pal.Chrome, v, 861, 1) : (v.y % 4 == 0 ? Pal.Rust[2] : Pal.Pick(Pal.Metal, v, 862, 2)),
                    (BP.ShinL, new Vector2(0f, 0.8f)), (BP.ShinR, new Vector2(0f, 0.8f)), (BP.ThighL, new Vector2(0.9f, 1f)), (BP.ThighR, new Vector2(0.9f, 1f)))
                    .Gear(durability: 2.5f, style: "raider").Armor(0.4f, 0.15f, 0.35f, 0.45f, 0.2f, 2f, 0.3f, Scrap),
                Def("moto_helmet", "MOTORCYCLE HELMET", ClothingSlot.Head, 1.9f, (v, p, t) =>
                    {
                        if (v.z > 1 && t > 0.5f && t < 0.66f && Mathf.Abs(v.x) <= 3) return Pal.Glass[1];                   // visor
                        if (Mathf.Abs(v.x) <= 1 && v.z < 2) return Pal.Cream[3];                                            // racing stripe
                        return Pal.Pick(Pal.Crimson, v, 863, 3);
                    },
                    (BP.Head, new Vector2(0.25f, 1f))).Gear(durability: 2.5f).Armor(0.45f, 0.2f, 0.75f, 0.6f, 0.3f, 1.4f, 0f, Scrap),

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

        const MadMax.Items.ResourceType Scrap = MadMax.Items.ResourceType.Scrap, Rubber = MadMax.Items.ResourceType.Rubber, Leather = MadMax.Items.ResourceType.Leather,
            Iron = MadMax.Items.ResourceType.Iron, Cloth = MadMax.Items.ResourceType.Cloth;

        /// <summary>Armour stats: protection per <see cref="DamageKind"/> on what the garment covers, weight, clank and the
        /// material it is mended with.</summary>
        static ClothingDef Armor(this ClothingDef d, float melee, float shot, float crash, float fall, float burn, float kg, float noise, MadMax.Items.ResourceType mend)
        {
            d.armor = new[] { melee, shot, crash, fall, burn }; d.weight = kg; d.noise = noise; d.mendWith = mend;
            return d;
        }

        /// <summary>Default outfit for a new wastelander.</summary>
        public static readonly string[] Starter = { "tshirt", "jacket", "pants", "boots", "shoulder" };
    }
}
