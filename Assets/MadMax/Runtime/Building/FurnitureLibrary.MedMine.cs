using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Build pieces for depth stages G and H. Medicine: the clinic bed (heals, sets fractures, nurses others),
    /// the surgery table (cuts out shrapnel, stitches deep wounds) and a wall medicine cabinet that stocks them. Mining:
    /// the sluice box (river gold without power), the powered stamp mill (ore → concentrate), timber mine props and a
    /// tunnel lamp, mine rail and the ore cart that runs on it.</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> MedMinePieces()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var C = ResourceType.Cloth;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper;
            var Fu = BuildCategory.Furniture; var In = BuildCategory.Industry;

            // ---- stage G: the clinic
            yield return D("clinic_bed", "CLINIC BED", Fu, ClinicBedGrid(), 4, false, go =>
            {
                Sit(go, 2.5f, 1f, ClinicBed.Spot);
                go.AddComponent<ClinicBed>();
            }, (Fe, 4), (C, 6), (W, 2));
            yield return D("surgery_table", "SURGERY TABLE", Fu, SurgeryTableGrid(), 6, false, go =>
            {
                Sit(go, 1.2f, 1f, SurgeryTable.Spot);
                go.AddComponent<SurgeryTable>();
            }, (Fe, 6), (G, 2), (Cu, 1), (C, 2));
            yield return D(MedSupply.CabinetId, "MEDICINE CABINET", Fu, MedicineCabinetGrid(), 3, false, go => Box(go, "MEDICINE CABINET", 12f, false), (S, 3), (G, 1));

            // ---- stage H: mining
            yield return D("sluice_box", "SLUICE BOX", In, SluiceGrid(), 5, false, go => go.AddComponent<SluiceBox>(), (W, 8), (S, 2), (C, 2));
            yield return D("stamp_mill", "STAMP MILL", In, StampMillGrid(), 16, false, go =>
            {
                Node(go, UtilityKind.Power, 1.2f);
                var st = Station(go, "stamp_mill", "STAMP MILL (CRUSH ORE)", 1800f);
                st.output = new Vector3(0f, 0.4f, 1.0f);
                go.AddComponent<StampMill>();
            }, (Fe, 12), (W, 8), (Cu, 2));
            yield return D("mine_prop", "MINE PROP", BuildCategory.Structure, MinePropGrid(), 6, true, null, (W, 4));
            yield return D("mine_lamp", "TUNNEL LAMP", Fu, MineLampGrid(), 2, false, go => Glow(go, new Vector3(0.32f, 1.36f, 0f), new Color(1f, 0.78f, 0.45f), 7f, 2.4f, false, 0f), (S, 1), (G, 1), (W, 1));
            yield return D("mine_rail", "MINE RAIL", In, MineRailGrid(), 6, false, go => go.AddComponent<MineRail>(), (Fe, 2), (W, 2));
            yield return D("ore_cart", "ORE CART", In, OreCartGrid(), 8, false, go =>
            {
                Box(go, "ORE CART", 300f, false);
                go.AddComponent<OreCart>();
            }, (Fe, 6), (S, 4));
        }

        /// <summary>Steel-framed clinic bed on casters, the long side (+Z) to get in from: striped mattress, folded blanket,
        /// pillow, head and foot rails with a red-cross plate, a drip stand at the head.</summary>
        static VoxelGrid ClinicBedGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var tube = Pal.Weathered(Pal.Chrome, 0.1f, 4001, 1, 0);
            foreach (int x in new[] { -11, 11 }) foreach (int z in new[] { -4, 4 })
            {
                g.Box(x, 1, z, x, 5, z, tube);
                g.Set(x, 0, z, Pal.Solid(Pal.Black[1]));                                            // casters
            }
            g.Box(-12, 5, -5, 12, 5, 5, p => p.x == -12 || p.x == 12 || p.z == -5 || p.z == 5 ? Pal.Chrome[2] : Pal.Metal[1]);   // spring frame
            g.Box(-11, 2, -4, 11, 2, -4, tube); g.Box(-11, 2, 4, 11, 2, 4, tube);                   // stretchers
            g.Box(-12, 6, -5, -12, 13, 5, p => p.y == 13 || p.y == 6 || p.z == -5 || p.z == 5 ? Pal.Chrome[2] : Pal.Cream[3]);   // headboard
            g.Box(-12, 8, 0, -12, 12, 0, Pal.Solid(Pal.Crimson[3])); g.Box(-12, 10, -2, -12, 10, 2, Pal.Solid(Pal.Crimson[3]));   // red cross
            g.Box(12, 10, -5, 12, 10, 5, Pal.Solid(Pal.Chrome[2]));                                // foot rail
            for (int z = -5; z <= 5; z += 2) g.Box(12, 6, z, 12, 9, z, Pal.Solid(Pal.Chrome[1]));
            g.Mat(Cloth);
            g.Box(-11, 6, -4, 11, 6, 4, p => (p.z & 1) == 0 ? Pal.PaleBlue[3] : Pal.Cream[3]);     // ticking
            g.Box(3, 7, -4, 11, 7, 4, Pal.Ramp(Pal.Navy, 3, 4002));                               // blanket
            g.Box(3, 7, 4, 11, 7, 4, Pal.Solid(Pal.Cream[4]));                                     // turned-down sheet
            g.Box(-11, 7, -3, -8, 8, 3, Pal.Ramp(Pal.Cream, 3, 4003));                             // pillow
            g.Mat(Scrap);
            g.Box(-14, 0, -4, -14, 21, -4, Pal.Ramp(Pal.Chrome, 1, 4004));                          // drip stand
            g.Box(-15, 0, -5, -13, 0, -3, Pal.Solid(Pal.Black[1]));
            g.Box(-15, 21, -4, -13, 21, -4, Pal.Solid(Pal.Chrome[2]));
            g.Mat(Glass);
            g.Box(-15, 17, -4, -15, 20, -4, Pal.Solid(Pal.PaleBlue[4]));                           // saline bag
            g.Set(-15, 18, -4, Pal.Solid(Pal.PaleBlue[2]));
            g.Mat(Cloth);
            g.Box(-15, 8, -3, -15, 16, -3, Pal.Solid(Pal.Cream[2]));                               // drip line
            return g;
        }

        /// <summary>Steel operating table on a pedestal with a surgical lamp on an arm and an instrument trolley.</summary>
        static VoxelGrid SurgeryTableGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            var steel = Pal.Weathered(Pal.Chrome, 0.08f, 4101, 2, 0);
            g.Box(-4, 0, -3, 4, 0, 3, Pal.Ramp(Pal.Metal, 1, 4102));                               // foot plate
            g.Box(-1, 1, -1, 1, 6, 1, Pal.Ramp(Pal.Chrome, 1, 4103));                              // pedestal
            g.Box(-11, 7, -4, 11, 7, 4, Pal.Ramp(Pal.Metal, 2, 4104));
            g.Box(-11, 8, -4, 11, 8, 4, steel);                                                    // top
            g.Mat(Cloth);
            g.Box(-10, 9, -3, -6, 9, 3, Pal.Ramp(Pal.RigGreen, 3, 4105));                          // head pad
            g.Box(-5, 9, -3, 10, 9, 3, p => (p.x & 3) == 0 ? Pal.RigGreen[2] : Pal.RigGreen[3]);  // drape
            g.Mat(Scrap);
            g.Box(-13, 0, -6, -13, 30, -6, Pal.Ramp(Pal.Chrome, 1, 4106));                          // lamp post
            g.Box(-15, 0, -8, -11, 0, -4, Pal.Solid(Pal.Black[1]));
            g.Box(-13, 30, -6, 0, 30, -6, Pal.Ramp(Pal.Chrome, 2)); g.Box(0, 27, -6, 0, 29, -6, Pal.Ramp(Pal.Chrome, 2));   // arm
            g.CylY(0, -3, 3.2f, 25, 26, p => p.y == 26 ? Pal.Cream[3] : Pal.Chrome[1]);            // lamp dish
            g.Mat(Glass);
            g.CylY(0, -3, 2.2f, 24, 24, Pal.Solid(Pal.LightW));
            g.Mat(Scrap);
            foreach (int x in new[] { 9, 13 }) foreach (int z in new[] { -10, -7 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Chrome, 1)); // trolley
            g.Box(9, 10, -10, 13, 10, -7, Pal.Ramp(Pal.Chrome, 2, 4107));
            g.Box(9, 5, -10, 13, 5, -7, Pal.Ramp(Pal.Metal, 2));
            g.Box(10, 11, -9, 11, 11, -9, Pal.Solid(Pal.Chrome[3])); g.Set(12, 11, -8, Pal.Solid(Pal.Chrome[3]));   // instruments
            g.Set(12, 11, -9, Pal.Solid(Pal.Crimson[3]));
            return g;
        }

        /// <summary>A wall cabinet (authored in the XZ plane, +Y out of the wall): white steel box, a door with a red
        /// cross, a handle and a shelf of bottles showing through the glass.</summary>
        static VoxelGrid MedicineCabinetGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-5, 0, 0, 5, 3, 13, Pal.Weathered(Pal.Cream, 0.12f, 4201, 3, -100));
            g.Box(-4, 4, 1, 4, 4, 12, Pal.Ramp(Pal.Cream, 4, 4202));                               // door
            g.Box(-1, 4, 8, 1, 4, 11, p => p.x == 0 || p.z == 9 || p.z == 10 ? Pal.Crimson[3] : Pal.Cream[4]);   // red cross
            g.Box(3, 4, 5, 3, 5, 6, Pal.Solid(Pal.Chrome[2]));                                     // handle
            g.Mat(Glass);
            g.Box(-3, 4, 2, 2, 4, 5, p => (p.x & 1) == 0 ? Pal.Ramp(Pal.Ochre, 2)(p) : Pal.Glass[3]);   // bottles behind glass
            return g;
        }

        /// <summary>Wooden sluice: a 2 m trough that falls from a hopper (+Z, upstream) to the outlet, iron riffles over a
        /// green riffle mat, a grating on the hopper, trestle legs at the head, a glint of colour in the riffles.</summary>
        static VoxelGrid SluiceGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var plank = Pal.Weathered(Pal.Wood, 0.1f, 4301, 2, 0);
            for (int z = -12; z <= 9; z++)
            {
                int f = (z + 12) * 4 / 21;                                                         // the floor falls towards -Z
                g.Box(-3, f, z, 3, f, z, plank);
                foreach (int x in new[] { -4, 4 }) g.Box(x, f, z, x, f + 4, z, p => p.y == f + 4 ? Pal.Wood[3] : Pal.Wood[2]);   // sides
                if (z > -12 && z < 9)
                {
                    g.Mat(Cloth); g.Box(-3, f + 1, z, 3, f + 1, z, Pal.Ramp(Pal.Moss, 3, 4302)); g.Mat(Wood);   // riffle mat
                    if ((z & 3) == 0) { g.Mat(Iron); g.Box(-3, f + 2, z, 3, f + 2, z, Pal.Ramp(Pal.Metal, 2)); g.Mat(Wood); }   // riffles
                }
            }
            g.Mat((byte)ResourceType.Gold); foreach (var (x, z) in new[] { (-2, -8), (1, -4), (2, -8), (-1, 0) }) g.Set(x, 2 + (z + 12) * 4 / 21, z, Pal.Solid(Pal.Ochre[4]));   // colour
            g.Mat(Wood);
            g.Box(-5, 4, 9, 5, 4, 14, plank);                                                      // hopper floor
            foreach (int x in new[] { -5, 5 }) g.Box(x, 5, 9, x, 9, 14, Pal.Ramp(Pal.Wood, 2, 4303));
            g.Box(-5, 5, 14, 5, 9, 14, Pal.Ramp(Pal.Wood, 2, 4304));
            g.Mat(Iron);
            for (int x = -4; x <= 4; x++) for (int z = 10; z <= 13; z++) if (((x + z) & 1) == 0) g.Set(x, 9, z, Pal.Solid(Pal.Metal[2]));   // grating
            g.Mat(Wood);
            foreach (int x in new[] { -5, 5 }) foreach (int z in new[] { 10, 13 }) g.Box(x, 0, z, x, 3, z, Pal.Ramp(Pal.Wood, 1, 4305));   // trestle
            g.Box(-5, 1, 10, 5, 1, 10, Pal.Ramp(Pal.Wood, 1));
            return g;
        }

        /// <summary>Stamp mill frame: timber posts and beams, an iron mortar box, a cam shaft with a belt pulley driven by
        /// an electric motor, an ore hopper behind and a copper apron plate in front. The three stamps are separate meshes
        /// animated by <see cref="StampMill"/>.</summary>
        static VoxelGrid StampMillGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var timber = Pal.Weathered(Pal.Wood, 0.08f, 4401, 1, 0);
            foreach (int z in new[] { -5, 5 }) g.Box(-12, 0, z, 12, 1, z, timber);                  // sills
            foreach (int x in new[] { -11, 11 }) foreach (int z in new[] { -5, 5 }) g.Box(x, 2, z, x, 26, z, Pal.Ramp(Pal.Wood, 2, 4402));   // posts
            foreach (int z in new[] { -5, 5 }) g.Box(-12, 27, z, 12, 27, z, timber);                // cap beams
            foreach (int x in new[] { -11, 11 }) g.Box(x, 27, -5, x, 27, 5, timber);
            foreach (int z in new[] { -2, 2 }) g.Box(-10, 22, z, 10, 22, z, Pal.Ramp(Pal.Wood, 3, 4403));   // stem guides
            g.Mat(Iron);
            g.Box(-8, 0, -3, 8, 9, 3, Pal.Weathered(Pal.Metal, 0.5f, 4404, 2, 0));                 // mortar
            g.ClearBox(-7, 4, -2, 7, 9, 2);
            g.Box(-8, 9, -3, 8, 9, -3, Pal.Ramp(Pal.Rust, 2)); g.Box(-8, 9, 3, 8, 9, 3, Pal.Ramp(Pal.Rust, 2));
            g.CylX(19, -3, 1f, -10, 13, Pal.Ramp(Pal.Metal, 2, 4405));                              // cam shaft
            foreach (int x in new[] { -5, 0, 5 }) g.Box(x - 1, 18, -2, x + 1, 20, -1, Pal.Ramp(Pal.Metal, 3));   // cams
            g.CylX(19, -3, 4f, 14, 15, p => (p.y + p.z) % 3 == 0 ? Pal.Rust[3] : Pal.Metal[2]);      // pulley
            g.Box(14, 5, -7, 14, 15, -7, Pal.Solid(Pal.Black[1]));                                 // belt
            g.Box(12, 0, -10, 17, 5, -5, Pal.Weathered(Pal.Olive, 0.25f, 4406, 2, 0));             // motor
            g.Mat((byte)ResourceType.Copper);
            g.Box(12, 2, -11, 17, 3, -11, p => (p.x & 1) == 0 ? Pal.Bronze[3] : Pal.Bronze[1]);    // windings
            g.Box(-7, 1, 4, 7, 1, 9, p => (p.z & 1) == 0 ? Pal.Bronze[2] : Pal.Bronze[3]);         // amalgam apron
            g.Mat(Wood);
            g.Box(-7, 0, 10, 7, 3, 13, Pal.Ramp(Pal.Wood, 2, 4407)); g.ClearBox(-6, 2, 11, 6, 3, 12);   // concentrate box
            g.Box(-7, 10, -11, 7, 16, -6, Pal.Ramp(Pal.Wood, 2, 4408)); g.ClearBox(-6, 12, -10, 6, 16, -7);   // ore hopper
            g.Box(-3, 9, -5, 3, 10, -4, Pal.Ramp(Pal.Wood, 1));                                    // chute
            g.Mat(Stone);
            g.Box(-5, 12, -10, 5, 13, -7, p => ((p.x * 7 + p.z * 3) & 3) == 0 ? Pal.Sand[1] : Pal.Metal[3]);   // ore in the hopper
            return g;
        }

        /// <summary>A timber mine set: two posts under a cap beam (1.4 m clear between them, 2.3 m high), wedges, iron
        /// straps and a painted set number.</summary>
        static VoxelGrid MinePropGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var post = Pal.Weathered(Pal.Wood, 0.05f, 4501, 1, 0);
            foreach (int x in new[] { -10, 10 }) g.Box(x - 1, 0, -1, x + 1, 28, 1, post);
            g.Box(-13, 29, -1, 13, 31, 1, Pal.Ramp(Pal.Wood, 2, 4502));                             // cap
            foreach (int x in new[] { -8, 8 }) g.Box(x, 28, -1, x, 28, 1, Pal.Ramp(Pal.Wood, 4));   // wedges
            foreach (int x in new[] { -12, 12 }) g.Box(x, 32, -1, x, 32, 1, Pal.Ramp(Pal.Wood, 3)); // lagging stubs
            g.Mat(Iron);
            foreach (int x in new[] { -10, 10 }) g.Box(x - 1, 27, -2, x + 1, 27, 2, Pal.Ramp(Pal.Metal, 1));   // straps
            g.Mat(Wood);
            g.Box(-10, 20, 2, -10, 21, 2, Pal.Solid(Pal.Ochre[4])); g.Set(-10, 22, 2, Pal.Solid(Pal.Ochre[4]));   // set number
            return g;
        }

        /// <summary>A lamp stand for tunnels and pits: a post with a hook arm and a caged lantern.</summary>
        static VoxelGrid MineLampGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-1, 0, -1, 1, 0, 1, Pal.Ramp(Pal.Wood, 1));
            g.Box(0, 1, 0, 0, 22, 0, Pal.Ramp(Pal.Wood, 2, 4601));
            g.Mat(Iron);
            g.Box(0, 22, 0, 4, 22, 0, Pal.Ramp(Pal.Metal, 1)); g.Set(4, 21, 0, Pal.Solid(Pal.Metal[2]));   // hook arm
            g.Box(3, 16, -1, 5, 16, 1, Pal.Ramp(Pal.Metal, 1)); g.Box(3, 20, -1, 5, 20, 1, Pal.Ramp(Pal.Metal, 1));
            foreach (int x in new[] { 3, 5 }) foreach (int z in new[] { -1, 1 }) g.Box(x, 17, z, x, 19, z, Pal.Solid(Pal.Metal[2]));   // cage
            g.Mat(Glass);
            g.Box(4, 17, 0, 4, 19, 0, p => p.y == 18 ? Pal.LightW : Pal.LightY);                   // flame
            return g;
        }

        /// <summary>2 m of mine track: sleepers every 0.32 m and two iron rails at a 0.64 m gauge, heads 0.2 m up.</summary>
        static VoxelGrid MineRailGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            for (int z = -12; z <= 12; z += 4) g.Box(-6, 0, z, 6, 1, z + (z < 12 ? 1 : 0), Pal.Weathered(Pal.Wood, 0.15f, 4701 + z, 1, 0));
            g.Mat(Iron);
            foreach (int x in new[] { -4, 4 }) g.Box(x, 2, -12, x, 2, 12, Pal.Stripe(Pal.Ramp(Pal.Chrome, 1, 4702), Pal.Ramp(Pal.Rust, 2, 4703), 2, 5));
            foreach (int z in new[] { -12, 12 }) foreach (int x in new[] { -5, 5 }) g.Set(x, 2, z, Pal.Solid(Pal.Metal[2]));   // fishplates
            return g;
        }

        /// <summary>A flared steel tub on four flanged wheels set to the rail gauge, a push bar at the back.</summary>
        static VoxelGrid OreCartGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            var tub = Pal.Weathered(Pal.Rust, 0.3f, 4801, 3, 0);
            for (int y = 5; y <= 13; y++)
            {
                int w = 4 + (y - 5) / 4, l = 6 + (y - 5) / 4;
                if (y == 5) g.Box(-w, y, -l, w, y, l, tub);
                else
                {
                    g.Box(-w, y, -l, w, y, -l, tub); g.Box(-w, y, l, w, y, l, tub);
                    g.Box(-w, y, -l, -w, y, l, tub); g.Box(w, y, -l, w, y, l, tub);
                }
            }
            g.Box(-6, 13, -8, 6, 13, -8, Pal.Ramp(Pal.Metal, 2)); g.Box(-6, 13, 8, 6, 13, 8, Pal.Ramp(Pal.Metal, 2));   // rim
            foreach (int x in new[] { -4, 4 }) foreach (int z in new[] { -4, 4 })
            {
                g.CylX(2, z, 2f, x, x, p => (p - new Vector3Int(x, 2, z)).sqrMagnitude <= 1 ? Pal.Chrome[1] : Pal.Metal[1]);   // wheels on the rails
                g.CylX(2, z, 2.6f, x > 0 ? x - 1 : x + 1, x > 0 ? x - 1 : x + 1, Pal.Solid(Pal.Metal[0]));   // inner flanges
            }
            g.Box(-2, 2, -4, 2, 2, -4, Pal.Ramp(Pal.Metal, 0)); g.Box(-2, 2, 4, 2, 2, 4, Pal.Ramp(Pal.Metal, 0));   // axles
            g.Box(-2, 3, -5, 2, 4, 5, Pal.Ramp(Pal.Metal, 1, 4802));                               // frame
            foreach (int x in new[] { -3, 3 }) g.Box(x, 8, -9, x, 11, -9, Pal.Ramp(Pal.Metal, 2));   // push bar
            g.Box(-3, 12, -10, 3, 12, -10, Pal.Ramp(Pal.Chrome, 2));
            g.Mat(Stone);
            g.Box(-4, 6, -6, 4, 7, 6, p => ((p.x * 5 + p.z * 3 + p.y) & 3) == 0 ? Pal.Sand[2] : Pal.Metal[3]);   // ore in the tub
            return g;
        }
    }
}
