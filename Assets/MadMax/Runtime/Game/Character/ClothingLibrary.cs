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
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0, 0.35f)), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.92f)), (BP.ForearmR, new Vector2(0, 0.92f))),
                Def("vest", "SCAV VEST", ClothingSlot.Outer, 1.6f, (v, p, t) => (v.y % 5 == 0) ? Pal.Rust[1] : Weave(v, dust, 804, 1), (BP.Chest, new Vector2(0.05f, 0.9f))),
                Def("pants", "CARGO PANTS", ClothingSlot.Legs, 0.7f, (v, p, t) => (p == BP.ThighL || p == BP.ThighR) && t > 0.45f && t < 0.6f ? Weave(v, khaki, 805, 0) : Weave(v, khaki, 806, 2),
                    (BP.Pelvis, Full), (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, new Vector2(0, 0.85f)), (BP.ShinR, new Vector2(0, 0.85f))),
                Def("jeans", "JEANS", ClothingSlot.Legs, 0.7f, (v, p, t) => Weave(v, denim, 807, 1),
                    (BP.Pelvis, Full), (BP.ThighL, Full), (BP.ThighR, Full), (BP.ShinL, new Vector2(0, 0.88f)), (BP.ShinR, new Vector2(0, 0.88f))),
                Def("boots", "BOOTS", ClothingSlot.Feet, 1.2f, (v, p, t) => (p == BP.FootL || p == BP.FootR) && v.y <= -2 ? Pal.Black[0] : Weave(v, Pal.Wood, 808, 1),
                    (BP.FootL, Full), (BP.FootR, Full), (BP.ShinL, new Vector2(0.72f, 1f)), (BP.ShinR, new Vector2(0.72f, 1f))),
                Def("gloves", "GLOVES", ClothingSlot.Hands, 0.8f, (v, p, t) => Weave(v, leather, 809, 1),
                    (BP.HandL, Full), (BP.HandR, Full), (BP.ForearmL, new Vector2(0.85f, 1f)), (BP.ForearmR, new Vector2(0.85f, 1f))),
                Def("goggles", "GOGGLES", ClothingSlot.Face, 0.9f, (v, p, t) => v.z > 1 && Mathf.Abs(v.x) >= 1 ? Pal.Hex("d08a30") : Pal.Black[0],
                    (BP.Head, new Vector2(0.58f, 0.66f))),
                Def("bandana", "BANDANA", ClothingSlot.Face, 0.9f, (v, p, t) => v.z > -1 ? Weave(v, red, 810, 1) : Pal.Black[1], (BP.Head, new Vector2(0.28f, 0.47f))),
                Def("helmet", "SCRAP HELMET", ClothingSlot.Head, 1.6f, (v, p, t) => Pal.Hash(v, 811) > 0.85f ? Pal.Chrome[2] : Weave(v, Pal.Metal, 812, 2), (BP.Head, new Vector2(0.68f, 1f))),
                Def("shoulder", "SHOULDER ARMOUR", ClothingSlot.Back, 2.4f, (v, p, t) => Weave(v, Pal.Chrome, 813, 1), (BP.UpperArmL, new Vector2(0f, 0.3f))),
                Def("coat", "WINTER PARKA", ClothingSlot.Outer, 2.2f, (v, p, t) => p == BP.Chest && v.y % 6 == 0 ? Pal.Olive[0] : Weave(v, Pal.Olive, 814, 2),
                    (BP.Chest, Full), (BP.Pelvis, Full), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, Full), (BP.ForearmR, Full), (BP.ThighL, new Vector2(0, 0.35f)), (BP.ThighR, new Vector2(0, 0.35f))),
                Def("hoodie", "HOODIE", ClothingSlot.Torso, 0.9f, (v, p, t) => Weave(v, Pal.Metal, 815, 2),
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0, 0.4f)), (BP.UpperArmL, Full), (BP.UpperArmR, Full), (BP.ForearmL, new Vector2(0, 0.9f)), (BP.ForearmR, new Vector2(0, 0.9f))),
                Def("beanie", "BEANIE", ClothingSlot.Head, 1.2f, (v, p, t) => v.y % 2 == 0 ? red[1] : red[2], (BP.Head, new Vector2(0.72f, 1f))),
                Def("sunhat", "SUN HAT", ClothingSlot.Head, 2.6f, (v, p, t) => Weave(v, dust, 816, 2), (BP.Head, new Vector2(0.8f, 1f))),
                Def("scarf", "SCARF", ClothingSlot.Face, 1.3f, (v, p, t) => (v.x + v.y) % 3 == 0 ? Pal.Cream[2] : red[0], (BP.Head, new Vector2(0.0f, 0.22f)), (BP.Chest, new Vector2(0.9f, 1f))),
                Def("shorts", "SHORTS", ClothingSlot.Legs, 0.7f, (v, p, t) => Weave(v, khaki, 817, 1), (BP.Pelvis, Full), (BP.ThighL, new Vector2(0, 0.55f)), (BP.ThighR, new Vector2(0, 0.55f))),
            };
        }

        /// <summary>Default outfit for a new wastelander.</summary>
        public static readonly string[] Starter = { "tshirt", "jacket", "pants", "boots", "shoulder" };
    }
}
