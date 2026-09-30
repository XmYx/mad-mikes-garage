using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;
using BP = MadMax.Game.BodyPart;

namespace MadMax.Game
{
    /// <summary>Leather goods and bee gear (depth stage E), made at the leather bench (<c>RecipeLibrary.Husbandry</c>):
    /// boots, a work belt and a gun belt with holster (small carry), a leather cuirass and riding chaps (armour), a
    /// satchel, and the beekeeper's veil (sewn; keeps the bees off at the hive).</summary>
    public static partial class ClothingLibrary
    {
        static List<ClothingDef> LeatherGoods(List<ClothingDef> l)
        {
            var tan = Pal.Wood; var dark = Pal.Black;
            l.Add(WithWarmth(Def("leather_boots", "LEATHER BOOTS", ClothingSlot.Feet, 1.25f, (v, p, t) =>
                    (p == BP.FootL || p == BP.FootR) && v.y <= -2 ? dark[0] : (p == BP.ShinL || p == BP.ShinR) && t < 0.8f && ((v.y + 64) % 3 == 0) && v.z > 0 ? Pal.Chrome[1] : Weave(v, tan, 3701, 2),
                    (BP.FootL, Full), (BP.FootR, Full), (BP.ShinL, new Vector2(0.6f, 1f)), (BP.ShinR, new Vector2(0.6f, 1f)))
                .Gear(durability: 2.4f, waterproof: 0.1f, style: "drifter").Armor(0.15f, 0.05f, 0.1f, 0.3f, 0.15f, 1.2f, 0f, Leather), 2f, -0.5f));
            l.Add(Def("work_belt", "WORK BELT", ClothingSlot.Belt, 1.6f, (v, p, t) =>
                    v.x == 0 && v.z > 0 ? Pal.Chrome[2] : (v.x > 2 || v.x < -3) && v.z >= 0 && t < 0.6f ? Weave(v, Pal.Olive, 3702, 1) : Weave(v, tan, 3703, 1),   // buckle, pouches
                    (BP.Pelvis, new Vector2(0.35f, 0.8f)))
                .Gear(carry: 5f, durability: 2f));
            l.Add(Def("gun_belt", "GUN BELT AND HOLSTER", ClothingSlot.Belt, 1.6f, (v, p, t) =>
                    v.x == 0 && v.z > 0 ? Pal.Chrome[2] : (v.x & 1) == 0 && v.z < 0 && t > 0.5f ? Pal.Bronze[3] : Weave(v, dark, 3704, 2),                         // buckle, cartridge loops
                    (BP.Pelvis, new Vector2(0.35f, 0.8f)))
                .Gear(carry: 3f, durability: 2f, style: "drifter").Prop(BP.Pelvis, a =>
                    {
                        var g = new VoxelGrid();
                        float w = 5.6f * a.build;
                        g.Box(Mathf.RoundToInt(w), -5, -1, Mathf.RoundToInt(w) + 1, 1, 1, p => Pal.Pick(Pal.Black, p, 3705, 2));   // holster on the right hip
                        g.Box(Mathf.RoundToInt(w), 2, 0, Mathf.RoundToInt(w) + 1, 3, 0, p => Pal.Pick(Pal.Wood, p, 3706, 1));      // the grip
                        return g;
                    }));
            l.Add(WithWarmth(Def("leather_cuirass", "LEATHER CUIRASS", ClothingSlot.Vest, 2.2f, (v, p, t) =>
                    {
                        if (p == BP.Chest && t > 0.84f) return (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) ? dark[1] : default;                               // shoulder straps
                        if (v.y % 5 == 0 && (v.x & 1) == 0) return Pal.Chrome[1];                                                                         // rivet rows
                        if ((v.x + 64) % 6 == 0) return tan[0];                                                                                            // seams
                        return Weave(v, tan, 3707, 2);
                    },
                    (BP.Chest, Full), (BP.Pelvis, new Vector2(0f, 0.35f)))
                .Gear(durability: 2.6f).Armor(0.4f, 0.15f, 0.3f, 0.15f, 0.2f, 3f, 0.03f, Leather), 1.5f, -1f));
            l.Add(WithWarmth(Def("leather_chaps", "RIDING CHAPS", ClothingSlot.Shins, 1.9f, (v, p, t) =>
                    (v.x + v.y) % 7 == 0 && v.z > 0 ? tan[4] : Weave(v, tan, 3708, 1),                                                                   // fringe stitching
                    (BP.ThighL, new Vector2(0.1f, 1f)), (BP.ThighR, new Vector2(0.1f, 1f)), (BP.ShinL, new Vector2(0f, 0.85f)), (BP.ShinR, new Vector2(0f, 0.85f)))
                .Gear(durability: 2.2f, waterproof: 0.2f, style: "drifter").Armor(0.25f, 0.05f, 0.15f, 0.2f, 0.15f, 1.8f, 0f, Leather), 2f, -0.5f));
            l.Add(Def("leather_satchel", "LEATHER SATCHEL", ClothingSlot.Pack, 0.7f, Straps(Pal.Wood), (BP.Chest, new Vector2(0.3f, 0.98f))).Gear(carry: 14f, durability: 2f).Prop(BP.Chest, a =>
                {
                    var g = new VoxelGrid();
                    g.Box(-4, 2, -7, 4, 10, -4, p => Pal.Pick(Pal.Wood, p, 3709, 2));                                                                       // the bag
                    g.Box(-4, 8, -8, 4, 10, -8, p => Pal.Pick(Pal.Wood, p, 3710, 1));                                                                       // flap
                    g.Box(-1, 6, -8, 1, 7, -8, Pal.Solid(Pal.Bronze[3]));                                                                                  // clasp
                    g.CylX(12, -5, 1.4f, -4, 4, p => Pal.Pick(Pal.Olive, p, 3711, 2));                                                                     // rolled blanket
                    return g;
                }));
            l.Add(WithWarmth(Def("bee_veil", "BEEKEEPER'S VEIL", ClothingSlot.Head, 2.4f, (v, p, t) =>
                    t > 0.82f ? Weave(v, Pal.Cream, 3712, 3) : ((v.x + v.y) & 1) == 0 ? dark[1] : default,                                               // hat crown, netting
                    (BP.Head, new Vector2(0.25f, 1f)))
                .Gear().Prop(BP.Head, a =>
                    {
                        var g = new VoxelGrid();
                        int y = Mathf.RoundToInt(7f * a.height);
                        for (int x = -6; x <= 6; x++)
                        for (int z = -6; z <= 6; z++)
                        {
                            float r = Mathf.Sqrt(x * x + (z - 0.5f) * (z - 0.5f));
                            if (r <= 5.8f && r >= 2.5f) g.Set(x, y, z, p => Pal.Pick(Pal.Cream, p, 3713, 3));                                          // wide brim
                        }
                        return g;
                    }), 0.5f, 1f));
            return l;
        }

        static ClothingDef WithWarmth(ClothingDef d, float warmth, float cooling) { d.warmth = warmth; d.cooling = cooling; return d; }
    }
}
