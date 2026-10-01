using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;
using BP = MadMax.Game.BodyPart;

namespace MadMax.Game
{
    /// <summary>Bags you wear or lug (storage in <see cref="BagLibrary"/>): a daypack, the military MOLLE pack, a
    /// handcrafted stick-frame pack, a tool belt, a fanny bag, one-shoulder bags (shoulder bag, sling bag, canvas
    /// satchel), hand luggage (duffel, suitcase) and a back brace. The older packs and belts become bags too (their
    /// <c>carry</c> bonus is now the bag's own capacity).</summary>
    public static partial class ClothingLibrary
    {
        static List<ClothingDef> Bags(List<ClothingDef> l)
        {
            l.Add(WithWarmth(Def("backpack", "BACKPACK", ClothingSlot.Pack, 0.7f, Straps(Pal.Ochre), (BP.Chest, new Vector2(0.25f, 0.98f))).Prop(BP.Chest, a =>
                {
                    var g = new VoxelGrid();
                    g.Box(-4, 2, -8, 4, 12, -4, p => Pal.Pick(Pal.Ochre, p, 3801, 2));
                    g.Box(-3, 3, -9, 3, 7, -9, p => Pal.Pick(Pal.Ochre, p, 3802, 3));                       // front pocket
                    g.Box(-3, 8, -9, 3, 8, -9, Pal.Solid(Pal.Black[1]));                                       // zip
                    g.Box(-4, 12, -8, 4, 12, -8, Pal.Solid(Pal.Black[1]));                                     // top seam
                    g.Box(5, 4, -7, 5, 8, -5, p => Pal.Pick(Pal.Navy, p, 3803, 2));                            // side bottle pocket
                    g.Set(0, 13, -6, Pal.Solid(Pal.Black[0]));                                                 // grab loop
                    return g;
                }), 0f, -0.5f));
            l.Add(WithWarmth(Def("milpack", "MILITARY BACKPACK", ClothingSlot.Pack, 0.7f, (v, p, t) =>
                    (Mathf.Abs(v.x) == 2 || Mathf.Abs(v.x) == 3) && (v.z > 0 || t > 0.85f) ? Pal.Pick(Pal.Olive, v, 3804, 1)
                    : p == BP.Chest && t < 0.08f ? Pal.Pick(Pal.Olive, v, 3805, 0) : default,                       // padded straps and the hip belt
                    (BP.Chest, new Vector2(0f, 0.98f))).Prop(BP.Chest, a =>
                {
                    var g = new VoxelGrid();
                    g.Box(-5, -1, -11, 5, 14, -4, p => (p.y % 3 == 0 && p.z == -11) ? Pal.Black[1] : Pal.Pick(Pal.Olive, p, 3806, 2));   // MOLLE webbing rows
                    foreach (int x in new[] { -6, 6 }) g.Box(x, 1, -9, x, 7, -6, p => p.y % 3 == 0 ? Pal.Black[1] : Pal.Pick(Pal.Olive, p, 3807, 1));   // side pouches
                    g.Box(-3, 2, -12, 3, 6, -12, p => Pal.Pick(Pal.Moss, p, 3808, 2));                        // front admin pouch
                    g.Box(-1, 5, -13, 1, 5, -13, Pal.Solid(Pal.Chrome[1]));                                   // buckle
                    g.CylX(15, -7, 1.8f, -5, 5, p => Pal.Pick(Pal.Moss, p, 3809, 2));                        // rolled mat on top
                    foreach (int x in new[] { -4, 4 }) g.Box(x, 15, -9, x, 16, -5, Pal.Solid(Pal.Black[1]));   // compression straps
                    return g;
                }).Gear(durability: 3f, style: "raider"), 0.5f, -1.2f));
            l.Add(WithWarmth(Def("craftpack", "HANDCRAFTED BACKPACK", ClothingSlot.Pack, 0.7f, Straps(Pal.Sand), (BP.Chest, new Vector2(0.25f, 0.98f))).Prop(BP.Chest, a =>
                {
                    var g = new VoxelGrid();
                    foreach (int x in new[] { -4, 4 }) g.Box(x, 0, -5, x, 14, -5, p => Pal.Pick(Pal.Wood, p, 3810, 1));   // two sticks
                    foreach (int y in new[] { 1, 12 }) g.Box(-4, y, -5, 4, y, -5, p => Pal.Pick(Pal.Wood, p, 3811, 2));
                    g.Box(-3, 2, -9, 3, 11, -6, p => Pal.Pick(Pal.Sand, p, 3812, 2));                          // hide bundle
                    g.Box(-3, 5, -10, 3, 5, -6, Pal.Solid(Pal.Wood[0]));                                       // twine
                    g.Box(-3, 9, -10, 3, 9, -6, Pal.Solid(Pal.Wood[0]));
                    return g;
                }).Gear(durability: 0.6f), 0.5f, -0.5f));
            l.Add(Def("toolbelt", "TOOL BELT", ClothingSlot.Belt, 1.6f, (v, p, t) =>
                    v.x == 0 && v.z > 0 ? Pal.Chrome[2] : Weave(v, Pal.Wood, 3813, 1), (BP.Pelvis, new Vector2(0.35f, 0.8f)))
                .Gear(durability: 2f).Prop(BP.Pelvis, a =>
                {
                    var g = new VoxelGrid();
                    int w = Mathf.RoundToInt(5.4f * a.build);
                    g.Box(w, -5, -2, w + 1, 1, 2, p => p.y == 1 ? Pal.Wood[0] : Pal.Pick(Pal.Wood, p, 3814, 2));    // nail pouch, right hip
                    g.Box(w + 1, 1, -1, w + 1, 3, -1, Pal.Solid(Pal.Metal[2]));                                // screwdriver handles
                    g.Box(w + 1, 1, 1, w + 1, 2, 1, Pal.Solid(Pal.Crimson[1]));
                    g.Box(-w - 1, -9, 0, -w - 1, 0, 0, p => Pal.Pick(Pal.Wood, p, 3815, 1));                   // hammer in its loop, left hip
                    g.Box(-w - 1, -10, -2, -w - 1, -9, 2, Pal.Solid(Pal.Metal[1]));
                    return g;
                }));
            l.Add(Def("fanny_bag", "FANNY BAG", ClothingSlot.Belt, 1.6f, (v, p, t) =>
                    t > 0.45f && t < 0.62f ? Weave(v, Pal.Black, 3816, 1) : default, (BP.Pelvis, new Vector2(0.35f, 0.8f)))
                .Prop(BP.Pelvis, a =>
                {
                    var g = new VoxelGrid();
                    g.Box(-3, -2, 4, 3, 1, 5, p => Pal.Pick(Pal.Crimson, p, 3817, 2));
                    g.Box(-3, 1, 6, 3, 1, 6, Pal.Solid(Pal.Black[1]));                                        // zip
                    g.Set(3, 0, 6, Pal.Solid(Pal.Chrome[2]));
                    return g;
                }));
            l.Add(Def("shoulder_bag", "SHOULDER BAG", ClothingSlot.Shoulder, 1.5f, Diagonal(Pal.Navy, false), (BP.Chest, Full))
                .Prop(BP.Chest, a => SideBag(a, -1, Pal.Navy, 3818, 5, 7)));
            l.Add(Def("sling_bag", "SLING BAG", ClothingSlot.Shoulder, 1.5f, Diagonal(Pal.Black, true), (BP.Chest, Full))
                .Prop(BP.Chest, a =>
                {
                    var g = new VoxelGrid();
                    g.Box(0, 4, -8, 4, 10, -5, p => Pal.Pick(Pal.Metal, p, 3819, 2));                        // across the shoulder blades
                    g.Box(1, 5, -9, 3, 7, -9, p => Pal.Pick(Pal.Crimson, p, 3820, 1));
                    g.Box(0, 10, -8, 4, 10, -8, Pal.Solid(Pal.Black[1]));
                    return g;
                }));
            l.Add(Def("canvas_satchel", "CANVAS SATCHEL", ClothingSlot.Shoulder, 1.5f, Diagonal(Pal.Wood, false), (BP.Chest, Full))
                .Prop(BP.Chest, a => SideBag(a, 1, Pal.Sand, 3821, 4, 6)));
            l.Add(Def("duffel", "DUFFEL BAG", ClothingSlot.Hand, 0.5f, Grip, (BP.HandR, Full)).Gear(durability: 1.4f).Prop(BP.HandR, a =>
                {
                    var g = new VoxelGrid();
                    g.CylZ(0, -9, 3.6f, -9, 9, p => Mathf.Abs(p.z) == 9 ? Pal.Black[1] : p.y == -6 ? Pal.Black[1] : Pal.Pick(Pal.Olive, p, 3822, 2));   // the roll, zip on top
                    g.Box(0, -5, -2, 0, -2, -2, Pal.Solid(Pal.Black[0]));                                     // carry handles
                    g.Box(0, -5, 2, 0, -2, 2, Pal.Solid(Pal.Black[0]));
                    g.Box(0, -2, -2, 0, -2, 2, Pal.Solid(Pal.Black[0]));
                    return g;
                }));
            l.Add(Def("suitcase", "SUITCASE", ClothingSlot.Hand, 0.5f, Grip, (BP.HandR, Full)).Gear(durability: 1.8f).Prop(BP.HandR, a =>
                {
                    var g = new VoxelGrid();
                    g.Box(-2, -17, -8, 1, -4, 8, p =>
                        (Mathf.Abs(p.z) == 8 && (p.y == -17 || p.y == -4)) ? Pal.Chrome[2]                                   // corner caps
                        : p.y == -10 ? Pal.Black[1]                                                                          // the seam
                        : (p.x == 1 && p.y < -12 && p.y > -16 && p.z > 1 && p.z < 6) ? Pal.Cream[2]                        // a travel sticker
                        : Pal.Pick(Pal.Crimson, p, 3823, 2));
                    g.Box(-1, -3, -2, 0, -3, 2, Pal.Solid(Pal.Black[0]));                                     // handle
                    g.Box(-1, -2, -2, 0, -2, -2, Pal.Solid(Pal.Black[0])); g.Box(-1, -2, 2, 0, -2, 2, Pal.Solid(Pal.Black[0]));
                    return g;
                }));
            l.Add(WithWarmth(Def(BagLibrary.Brace, "BACK BRACE", ClothingSlot.Belt, 1.3f, (v, p, t) =>
                    p == BP.Chest ? (t < 0.22f ? ((v.y & 1) == 0 && v.z < 0 ? Pal.Black[1] : Weave(v, Pal.Cream, 3824, 2)) : default)
                    : ((v.x + 64) % 4 == 0 && v.z < 0 ? Pal.Black[1] : Weave(v, Pal.Cream, 3825, 2)),
                    (BP.Pelvis, new Vector2(0.3f, 1f)), (BP.Chest, new Vector2(0f, 0.22f))).Gear(durability: 2f), 0.5f, -0.5f));

            // the older packs and belts are bags now: their carry bonus is their capacity, they weigh something
            foreach (var d in l)
            {
                var spec = BagLibrary.Get(d.id);
                if (spec == null) continue;
                d.carry = 0f;
                if (d.weight <= 0f) d.weight = spec.weight;
                if (d.id == "framepack") { d.name = "TOURIST FRAME PACK"; d.noise = 0.04f; }
                if (d.id == "milpack") d.noise = 0.06f;
                if (d.id == "toolbelt") d.noise = 0.08f;                                               // the tools clink
            }
            return l;
        }

        /// <summary>The handle wrapped in the fist.</summary>
        static Color32 Grip(Vector3Int v, BP p, float t) => t > 0.45f && t < 0.75f && Mathf.Abs(v.z) <= 1 ? Pal.Black[0] : default;

        /// <summary>A strap from one shoulder across the chest and back to the other hip (<paramref name="mirror"/>: the
        /// other way); everything else bare.</summary>
        static System.Func<Vector3Int, BP, float, Color32> Diagonal(Color32[] ramp, bool mirror) => (v, p, t) =>
        {
            float x = (mirror ? 1f : -1f) * (-5f + 9f * t);
            return Mathf.Abs(v.x - x) <= 0.9f ? Pal.Pick(ramp, v, 3826, 1) : default;
        };

        /// <summary>A bag hanging at one hip (side −1 left, +1 right) on the chest bone.</summary>
        static VoxelGrid SideBag(Appearance a, int side, Color32[] ramp, int seed, int half, int tall)
        {
            var g = new VoxelGrid();
            int x0 = Mathf.RoundToInt(5.6f * a.build) * side, x1 = x0 + 2 * side;
            int lo = Mathf.Min(x0, x1), hi = Mathf.Max(x0, x1);
            g.Box(lo, -tall + 1, -half, hi, 1, half, p => Pal.Pick(ramp, p, seed, 2));
            g.Box(side > 0 ? hi + 1 : lo - 1, -2, -half, side > 0 ? hi + 1 : lo - 1, 1, half, p => Pal.Pick(ramp, p, seed + 1, 1));   // flap
            g.Set(side > 0 ? hi + 1 : lo - 1, -1, 0, Pal.Solid(Pal.Bronze[2]));                         // clasp
            g.Box(x0, 2, 0, x0, 4, 0, Pal.Solid(Pal.Black[1]));                                          // strap end
            return g;
        }
    }
}
