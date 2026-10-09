using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A prosthetic for a lost limb (item "pros_*"). <see cref="arm"/> pieces fit arms, the rest legs;
    /// <see cref="whole"/> pieces replace below the elbow / knee (fit ArmX / LegX), the others a hand / foot (fit HandX /
    /// FootX). Arms: <see cref="grip"/> (0 none .. 1 a hand: tools need 0.4, two-handed both sides 0.5),
    /// <see cref="climbs"/> (holds ledges), <see cref="tool"/> (a tool mount: that tool is always at hand on the
    /// hotbar, carried on the mount). Legs: <see cref="limp"/> left in the gait, <see cref="speed"/>. Any piece:
    /// <see cref="stamina"/> drain multiplier, <see cref="carry"/> kg, <see cref="noise"/>. Model: 4 cm voxels, origin at
    /// the joint, hanging along -Y like the bone it replaces; a "grip" point carries a held or mounted tool.</summary>
    public class ProstheticDef
    {
        public string id, name, desc;
        public bool arm, whole;
        public float grip, limp, speed = 1f, stamina = 1f, carry, noise, kg = 1f;
        public bool climbs;
        /// <summary>Pre-war salvage (the lead's cyber arm): found or worn from the start, never made at a bench.</summary>
        public bool salvage;
        public string tool;
        public System.Action<VoxelGrid> model;
        public Vector3Int gripAt = new Vector3Int(0, -6, 0);

        public bool Fits(BodyZone z) => Limbs.IsLimb(z) && Limbs.IsArm(z) == arm && (z == BodyZone.ArmL || z == BodyZone.ArmR || z == BodyZone.LegL || z == BodyZone.LegR) == whole;
    }

    /// <summary>Every prosthetic (<see cref="ProstheticDef"/>); crafted at the workbench, forge and machine shop
    /// (<c>RecipeLibrary.Prosthetics</c>), found in clinics, bunkers and on raiders.</summary>
    public static class ProstheticLibrary
    {
        static List<ProstheticDef> all;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => meshes.Clear();

        public static IReadOnlyList<ProstheticDef> All => all ??= Build();

        public static ProstheticDef Get(string id) { if (id == null) return null; foreach (var d in All) if (d.id == id) return d; return null; }

        public static bool Is(string id) => id != null && id.StartsWith("pros_");

        static VoxMat Leather => Pal.Ramp(Pal.Wood, 1);
        static VoxMat Steel => Pal.Ramp(Pal.Chrome, 1);
        static VoxMat Bright => Pal.Ramp(Pal.Chrome, 3);
        static VoxMat Timber => Pal.Ramp(Pal.Wood, 3);

        /// <summary>A leather cuff laced round the stump (every piece starts with one).</summary>
        static void Cuff(VoxelGrid g, int len) { g.CylY(0, 0, 1.6f, -len, 0, Leather); g.Box(-2, -1, 0, -2, -1, 0, Pal.Solid(Pal.Ochre[3])); }

        static List<ProstheticDef> Build() => new List<ProstheticDef>
        {
            new ProstheticDef { id = "pros_hook", name = "HOOK HAND", arm = true, grip = 0.5f, climbs = true, kg = 0.4f, gripAt = new Vector3Int(0, -5, 1),
                desc = "HOLDS ONE-HANDED TOOLS AND HELPS WITH TWO, HANGS ON TO LEDGES",
                model = g => { Cuff(g, 2); g.Box(0, -6, 0, 0, -3, 0, Steel); g.Box(0, -7, 1, 0, -7, 2, Bright); g.Set(0, -6, 3, Bright); g.Set(0, -5, 3, Bright); } },
            new ProstheticDef { id = "pros_claw", name = "SPRUNG CLAW", arm = true, grip = 0.65f, climbs = true, kg = 0.8f, noise = 0.1f, gripAt = new Vector3Int(0, -6, 0),
                desc = "TWO SPRUNG PRONGS: GRIPS MOST TOOLS, STIFFLY",
                model = g => { Cuff(g, 2); g.Box(-1, -4, -1, 1, -3, 1, Steel); g.Box(-1, -8, 0, -1, -5, 0, Bright); g.Box(1, -8, 0, 1, -5, 0, Bright); g.Set(0, -8, 0, Steel); } },
            new ProstheticDef { id = "pros_wood_hand", name = "CARVED HAND", arm = true, grip = 0.25f, kg = 0.3f, gripAt = new Vector3Int(0, -5, 0),
                desc = "LOOKS THE PART AND HOLDS NEXT TO NOTHING",
                model = g => { Cuff(g, 1); g.Box(-1, -5, -1, 1, -2, 0, Timber); g.Box(-1, -7, 0, 1, -6, 0, Pal.Ramp(Pal.Wood, 2)); g.Set(2, -3, 0, Timber); } },
            new ProstheticDef { id = "pros_mech_hand", name = "MACHINIST'S HAND", arm = true, grip = 0.9f, climbs = true, kg = 1.2f, noise = 0.15f, gripAt = new Vector3Int(0, -6, 0),
                desc = "CABLES AND SPRINGS FROM THE MACHINE SHOP: NEARLY A HAND",
                model = g => { Cuff(g, 2); g.Box(-1, -5, -1, 1, -3, 1, Steel); for (int f = -1; f <= 1; f++) g.Box(f, -8, 0, f, -6, 0, Bright); g.Box(2, -5, 0, 2, -4, 1, Bright); } },
            new ProstheticDef { id = "pros_blade_arm", name = "BLADE ARM", arm = true, whole = true, tool = ItemIds.Machete, kg = 1.6f, gripAt = new Vector3Int(0, -9, 0),
                desc = "A MOUNT IN PLACE OF THE FOREARM: A MACHETE ALWAYS AT HAND",
                model = g => { Cuff(g, 4); g.Box(-1, -8, -1, 1, -5, 1, Steel); g.Set(0, -9, 0, Bright); } },
            new ProstheticDef { id = "pros_torch_arm", name = "TORCH ARM", arm = true, whole = true, tool = "tool_gas_torch", kg = 2.2f, noise = 0.05f, gripAt = new Vector3Int(0, -9, 0),
                desc = "A GAS-TORCH MOUNT: CUTS AND BURNS (RUNS ON FUEL)",
                model = g => { Cuff(g, 4); g.CylY(0, 0, 1.2f, -8, -5, Steel); g.Box(1, -7, 1, 1, -5, 1, Pal.Ramp(Pal.Rust, 2)); } },
            new ProstheticDef { id = "pros_shotgun_arm", name = "SHOTGUN ARM", arm = true, whole = true, tool = ItemIds.Shotgun, kg = 3f, noise = 0.1f, gripAt = new Vector3Int(0, -9, 0),
                desc = "A PIPE-SHOTGUN MOUNT ON THE STUMP (TAKES SHELLS)",
                model = g => { Cuff(g, 4); g.Box(-1, -8, -1, 1, -5, 1, Pal.Ramp(Pal.Black, 2)); g.Set(0, -9, 0, Steel); } },
            new ProstheticDef { id = "pros_hydraulic_arm", name = "HYDRAULIC ARM", arm = true, whole = true, grip = 1f, climbs = true, carry = 15f, stamina = 1.1f, kg = 4f, noise = 0.25f, gripAt = new Vector3Int(0, -12, 0),
                desc = "PISTONS AND A STEEL GRAB: A STRONG ARM (+15 KG CARRIED), HEAVY",
                model = g => { Cuff(g, 3); g.Box(-1, -10, -1, 1, -4, 1, Steel); g.Box(2, -9, 0, 2, -4, 0, Bright); g.Box(-2, -9, 0, -2, -4, 0, Pal.Ramp(Pal.Ochre, 2)); g.Box(-1, -13, 0, 1, -11, 0, Bright); } },
            new ProstheticDef { id = "pros_cyber_arm", name = "SALVAGED CYBER ARM", salvage = true, arm = true, whole = true, grip = 1f, climbs = true, kg = 1.6f, noise = 0.08f, gripAt = new Vector3Int(0, -13, 0),
                desc = "PRE-WAR PLATING, BLACK JOINTS, CABLES OUT IN THE OPEN: A FULL HAND",
                model = g =>
                {
                    VoxMat plate = p => Pal.Hash(p, 870) < 0.12f ? Pal.Sand[1] : Pal.Pick(Pal.Cream, p, 871, 1), joint = Pal.Ramp(Pal.Black, 1);
                    g.CylY(0, 0, 1.7f, -1, 0, Steel);                                                       // elbow socket
                    g.CylY(0, 0, 1.6f, -9, -2, plate);                                                     // forearm shell
                    g.Box(-2, -4, -1, -2, -3, 1, joint); g.Box(-2, -8, -1, -2, -7, 1, joint);              // seams
                    g.Set(1, -5, -2, Pal.Solid(Pal.Ochre[3]));                                             // status lamp
                    for (int y = -9; y <= -1; y++) { g.Set(-1, y, -2, Pal.Solid(y % 3 == 0 ? Pal.Crimson[2] : Pal.Black[0])); g.Set(1, y + (y & 1), -2, Pal.Solid(Pal.Black[1])); }   // cables
                    g.Box(-1, -10, -1, 1, -10, 1, joint);                                                  // wrist
                    g.Box(-1, -12, -1, 1, -11, 1, plate);                                                  // palm
                    for (int f = -1; f <= 1; f++) { g.Set(f, -13, 0, joint); g.Box(f, -15, 0, f, -14, 0, plate); }
                    g.Set(2, -11, 0, joint); g.Box(2, -13, 0, 2, -12, 1, plate);                          // thumb
                } },
            new ProstheticDef { id = "pros_peg_leg", name = "PEG LEG", whole = true, limp = 0.45f, speed = 0.8f, stamina = 1.15f, kg = 1.5f, noise = 0.1f,
                desc = "A TURNED WOODEN PEG: WALKS, SLOWLY",
                model = g => { Cuff(g, 3); g.CylY(0, 0, 1.4f, -7, -4, Timber); g.Box(0, -12, 0, 0, -8, 0, Timber); g.Set(0, -13, 0, Pal.Solid(Pal.Tire[1])); } },
            new ProstheticDef { id = "pros_strut_leg", name = "STEEL STRUT", whole = true, limp = 0.2f, speed = 0.92f, stamina = 1.05f, kg = 3f, noise = 0.2f,
                desc = "A WELDED STRUT WITH A RUBBER FOOT: STEADY, NOISY",
                model = g => { Cuff(g, 3); g.Box(0, -12, 0, 0, -4, 0, Steel); g.Box(-1, -13, -1, 1, -13, 2, Pal.Ramp(Pal.Tire, 1)); } },
            new ProstheticDef { id = "pros_spring_leg", name = "BLADE SPRING", whole = true, limp = 0.1f, speed = 1.06f, stamina = 1.2f, kg = 2f, noise = 0.05f,
                desc = "A CURVED LEAF-SPRING BLADE: QUICK ON FLAT GROUND, TIRING",
                model = g => { Cuff(g, 3); g.Box(0, -7, 0, 0, -4, 0, Steel); g.Box(0, -10, 1, 0, -8, 1, Bright); g.Box(0, -12, 0, 0, -11, 0, Bright); g.Box(0, -13, -1, 0, -13, 1, Pal.Ramp(Pal.Tire, 1)); } },
            new ProstheticDef { id = "pros_wood_foot", name = "WOODEN FOOT", limp = 0.3f, speed = 0.9f, stamina = 1.05f, kg = 0.6f,
                desc = "A CARVED FOOT IN AN OLD BOOT",
                model = g => { Cuff(g, 1); g.Box(-1, -2, -1, 1, -1, 4, Timber); g.Box(-1, -3, -1, 1, -3, 4, Pal.Ramp(Pal.Tire, 1)); } },
        };

        /// <summary>The piece's mesh (cached; built again when play mode killed it).</summary>
        public static Mesh MeshFor(ProstheticDef d)
        {
            if (d == null) return null;
            if (meshes.TryGetValue(d.id, out var m) && m) return m;
            var g = new VoxelGrid();
            d.model?.Invoke(g);
            g.Bevel();
            return meshes[d.id] = VoxelMesher.Build(g, "Prosthetic_" + d.id, HumanDesign.S);
        }

        // ------------------------------------------------------------------ the fitted set ("zone=id;zone=id" on Appearance)
        public static string FittedOn(Appearance a, BodyZone z)
        {
            if (a == null || string.IsNullOrEmpty(a.prosthetics)) return null;
            string key = ((int)z).ToString() + "=";
            foreach (var e in a.prosthetics.Split(';')) if (e.StartsWith(key)) return e.Substring(key.Length);
            return null;
        }

        public static void SetFitted(Appearance a, BodyZone z, string id)
        {
            var parts = new List<string>();
            string key = ((int)z).ToString() + "=";
            if (!string.IsNullOrEmpty(a.prosthetics)) foreach (var e in a.prosthetics.Split(';')) if (e.Length > 0 && !e.StartsWith(key)) parts.Add(e);
            if (!string.IsNullOrEmpty(id)) parts.Add(key + id);
            a.prosthetics = string.Join(";", parts);
        }

        /// <summary>Every fitted piece with its zone.</summary>
        public static IEnumerable<(BodyZone zone, ProstheticDef def)> Fitted(Appearance a)
        {
            if (a == null || string.IsNullOrEmpty(a.prosthetics)) yield break;
            foreach (var e in a.prosthetics.Split(';'))
            {
                int eq = e.IndexOf('=');
                if (eq <= 0 || !int.TryParse(e.Substring(0, eq), out int z)) continue;
                var d = Get(e.Substring(eq + 1));
                if (d != null) yield return ((BodyZone)z, d);
            }
        }
    }
}
