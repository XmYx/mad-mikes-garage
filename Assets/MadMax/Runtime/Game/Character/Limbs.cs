using MadMax.Items;
using MadMax.RPG;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Limbs (player and NPCs): the eight limb zones of <see cref="BodyZone"/> (arms, hands, legs, feet). A heavy
    /// hit on a limb can leave it <see cref="Wound.Mangled"/> (hanging on: bleeds, useless until splinted and dressed,
    /// a further heavy hit takes it off) or sever it outright (<see cref="Roll"/>). Severed limbs are bits of
    /// <see cref="Appearance.lost"/>: ArmX = below the elbow (forearm and hand), HandX = at the wrist, LegX = below the
    /// knee, FootX = at the ankle. The rig hides what is gone (zero-scaled bones, voxel and HD alike) and caps the stump;
    /// a <see cref="ProstheticLibrary"/> piece can take the lost part's place once the stump has healed.</summary>
    public static class Limbs
    {
        public static readonly BodyZone[] All = { BodyZone.ArmL, BodyZone.ArmR, BodyZone.HandL, BodyZone.HandR, BodyZone.LegL, BodyZone.LegR, BodyZone.FootL, BodyZone.FootR };

        public static int Bit(BodyZone z) => 1 << (int)z;
        public static bool IsLimb(BodyZone z) => z >= BodyZone.ArmL;
        public static bool Severed(int lost, BodyZone z) => (lost & Bit(z)) != 0;
        public static bool IsArm(BodyZone z) => z == BodyZone.ArmL || z == BodyZone.ArmR || z == BodyZone.HandL || z == BodyZone.HandR;
        public static bool Left(BodyZone z) => z == BodyZone.ArmL || z == BodyZone.HandL || z == BodyZone.LegL || z == BodyZone.FootL;

        /// <summary>The limb a hand / foot hangs from (null for arms and legs).</summary>
        public static BodyZone? Parent(BodyZone z) => z switch
        {
            BodyZone.HandL => BodyZone.ArmL, BodyZone.HandR => BodyZone.ArmR, BodyZone.FootL => BodyZone.LegL, BodyZone.FootR => BodyZone.LegR, _ => (BodyZone?)null
        };

        /// <summary>The hand / foot below an arm / leg (null otherwise).</summary>
        public static BodyZone? Child(BodyZone z) => z switch
        {
            BodyZone.ArmL => BodyZone.HandL, BodyZone.ArmR => BodyZone.HandR, BodyZone.LegL => BodyZone.FootL, BodyZone.LegR => BodyZone.FootR, _ => (BodyZone?)null
        };

        /// <summary>The zone is gone: severed itself or with the limb above it.</summary>
        public static bool Gone(int lost, BodyZone z) => Severed(lost, z) || (Parent(z) is BodyZone p && Severed(lost, p));

        /// <summary>First bone that comes off with the zone.</summary>
        public static BodyPart CutBone(BodyZone z) => z switch
        {
            BodyZone.ArmL => BodyPart.ForearmL, BodyZone.ArmR => BodyPart.ForearmR, BodyZone.HandL => BodyPart.HandL, BodyZone.HandR => BodyPart.HandR,
            BodyZone.LegL => BodyPart.ShinL, BodyZone.LegR => BodyPart.ShinR, BodyZone.FootL => BodyPart.FootL, _ => BodyPart.FootR
        };

        /// <summary>The bone the stump ends on.</summary>
        public static BodyPart StumpBone(BodyZone z) => z switch
        {
            BodyZone.ArmL => BodyPart.UpperArmL, BodyZone.ArmR => BodyPart.UpperArmR, BodyZone.HandL => BodyPart.ForearmL, BodyZone.HandR => BodyPart.ForearmR,
            BodyZone.LegL => BodyPart.ThighL, BodyZone.LegR => BodyPart.ThighR, BodyZone.FootL => BodyPart.ShinL, _ => BodyPart.ShinR
        };

        /// <summary>A bone is missing (cut off itself or below a cut).</summary>
        public static bool BoneGone(int lost, BodyPart p)
        {
            if (lost == 0) return false;
            switch (p)
            {
                case BodyPart.ForearmL: return Severed(lost, BodyZone.ArmL);
                case BodyPart.ForearmR: return Severed(lost, BodyZone.ArmR);
                case BodyPart.HandL: return Gone(lost, BodyZone.HandL);
                case BodyPart.HandR: return Gone(lost, BodyZone.HandR);
                case BodyPart.ShinL: return Severed(lost, BodyZone.LegL);
                case BodyPart.ShinR: return Severed(lost, BodyZone.LegR);
                case BodyPart.FootL: return Gone(lost, BodyZone.FootL);
                case BodyPart.FootR: return Gone(lost, BodyZone.FootR);
                default: return false;
            }
        }

        public static string Name(BodyZone z) => Injury.ZoneNames[(int)z];

        /// <summary>The item a severed limb becomes on the ground.</summary>
        public static string Item(BodyZone z) => z switch
        {
            BodyZone.ArmL or BodyZone.ArmR => "limb_arm", BodyZone.HandL or BodyZone.HandR => "limb_hand", BodyZone.LegL or BodyZone.LegR => "limb_leg", _ => "limb_foot"
        };

        /// <summary>Cutting tools: they can take a limb off in a fight, and off yourself at need.</summary>
        public static bool Bladed(string toolId) => toolId == "tool_knife" || toolId == ItemIds.Machete || toolId == "tool_axe" || toolId == "tool_leaf_blade"
                                                     || toolId == "tool_spear" || toolId == ItemIds.Cutter;

        /// <summary>A heavy hit on a limb zone: 0 nothing, 1 mangled, 2 severed. <paramref name="mangled"/>: the limb is
        /// already hanging on (a further heavy hit takes it off); <paramref name="protect"/> 0..1 armour on the zone.</summary>
        public static int Roll(BodyZone z, float amount, string cause, bool bladed, bool mangled, System.Random rnd, float protect = 0f)
        {
            if (!IsLimb(z)) return 0;
            float mangle = 0f, sever = 0f;
            switch (cause)
            {
                case "MELEE": if (bladed && amount > 12f) { mangle = 0.22f; sever = 0.35f; } else if (amount > 22f) { mangle = 0.1f; sever = 0.12f; } break;
                case "SHOT": if (amount > 20f) { mangle = 0.15f; sever = 0.15f; } break;
                case "BLAST": if (amount > 25f) { mangle = 0.3f; sever = 0.3f; } break;
                case "CRASH": if (amount > 35f) { mangle = 0.18f; sever = 0.15f; } break;
                case "BITE": if (amount > 12f) { mangle = 0.12f; sever = 0.12f; } break;
            }
            float keep = 1f - Mathf.Clamp01(protect) * 0.8f;
            double r = rnd.NextDouble();
            if (mangled) return r < sever * keep ? 2 : 0;
            if (cause == "BLAST" && amount > 40f && r < 0.08 * keep) return 2;                    // torn clean off
            return r < mangle * keep ? 1 : 0;
        }

        // ------------------------------------------------------------------ meshes
        static Mesh stump;

        /// <summary>The cap on a stump: dressed flesh, ~9 cm across.</summary>
        public static Mesh StumpMesh()
        {
            if (stump) return stump;
            if (MadMax.Rendering.HDBits.On)
            {
                var b = new MadMax.Rendering.HDShapes();
                b.Ellipsoid(Vector3.zero, new Vector3(0.05f, 0.035f, 0.05f), Pal.Flesh);
                b.Ellipsoid(new Vector3(0f, -0.015f, 0f), new Vector3(0.045f, 0.03f, 0.045f), Pal.Dressing, 8, 4, true);
                return stump = b.ToMesh("Stump");
            }
            var g = new VoxelGrid();
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                g.Set(x, 0, z, Pal.Solid((x + z & 1) == 0 ? Pal.Flesh : Pal.FleshLight));
                if (x == 0 || z == 0) g.Set(x, -1, z, Pal.Solid(Pal.Dressing));        // a dressing over the end
            }
            return stump = VoxelMesher.Build(g, "Stump", HumanDesign.S);
        }
    }
}
