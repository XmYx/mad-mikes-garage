using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Build pieces for depth stage D, the metalworking ladder: the forge and anvil (charcoal or coal, by hand:
    /// steel, tools, nails, bolts, horseshoes, leaf springs, crude armour) and the powered machine shop (lathe and mill,
    /// tier 1.5 = finer make: gearboxes, brake, suspension and tank kits, forged engines, exhausts, radiators).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> MetalPieces()
        {
            var In = BuildCategory.Industry;
            var W = ResourceType.Wood; var St = ResourceType.Stone; var Fe = ResourceType.Iron; var Cu = ResourceType.Copper;
            yield return D("forge", "FORGE AND ANVIL", In, ForgeGrid(), 20, false, go =>
            {
                var st = Station(go, "forge", "SMITH (FORGE AND ANVIL)", 0f);
                st.tier = 0.5f; st.output = new Vector3(0.56f, 0.95f, 0.1f);
                Glow(go, new Vector3(-0.52f, 0.85f, 0f), new Color(1f, 0.5f, 0.2f), 4f, 1.8f, false, 0f);
                go.AddComponent<MetalShopFx>().forge = true;
            }, (St, 16), (ResourceType.Clay, 4), (Fe, 6), (W, 4));
            yield return D("machine_shop", "MACHINE SHOP", In, MachineShopGrid(), 16, false, go =>
            {
                Node(go, UtilityKind.Power, 1.0f);
                var st = Station(go, "machine_shop", "MACHINE SHOP (LATHE AND MILL)", 2000f);
                st.tier = 1.5f; st.output = new Vector3(0f, 0.35f, 0.75f);
                go.AddComponent<MetalShopFx>();
            }, (Fe, 14), (ResourceType.Steel, 6), (Cu, 6), (ResourceType.Aluminium, 2));
        }

        static VoxMat ForgeBricks(int seed) => p =>
        {
            bool mortar = p.y % 3 == 0 || ((p.x + p.z + (p.y / 3 % 2) * 2) % 4 + 4) % 4 == 0;
            return mortar ? Pal.Cream[0] : Pal.Pick(Pal.Crimson, p, seed, 2);
        };

        /// <summary>Brick hearth with a coal bed, hood and chimney, bellows behind it; the anvil on an oak stump, tongs, a
        /// glowing billet and a quench bucket. Hearth to the left (-X), anvil to the right.</summary>
        static VoxelGrid ForgeGrid()
        {
            var g = new VoxelGrid().Mat(Stone);
            var bricks = ForgeBricks(4301);
            g.Box(-13, 0, -5, -1, 9, 5, bricks);                                                                  // hearth
            g.ClearBox(-10, 8, -3, -4, 9, 3);
            g.Box(-10, 8, -3, -4, 8, 3, p => ((p.x + p.z) & 1) == 0 ? Pal.Amber : Pal.Pick(Pal.Black, p, 4302, 1));   // coal bed, glowing
            g.Set(-7, 9, 0, Pal.Solid(Pal.LightY)); g.Set(-6, 9, 1, Pal.Solid(Pal.Amber));
            foreach (int x in new[] { -13, -1 }) g.Box(x, 10, -5, x, 17, -3, bricks);                            // hood posts
            for (int y = 18; y <= 22; y++)
            {
                int i = y - 18;
                g.Box(-13 + i, y, -5, -1 - i, y, 3 - i, bricks);                                                  // hood
            }
            g.Box(-9, 23, -4, -5, 36, -1, bricks);                                                               // chimney
            g.Box(-9, 37, -4, -5, 37, -1, Pal.Ramp(Pal.Black, 1, 4303));                                           // sooty top
            g.Mat(Wood);
            g.Box(-12, 3, -9, -6, 7, -6, p => p.y == 5 ? Pal.Wood[1] : Pal.Pick(Pal.Olive, p, 4304, 1));           // bellows
            g.Box(-9, 8, -9, -9, 12, -9, Pal.Ramp(Pal.Wood, 2, 4305)); g.Box(-9, 12, -9, -9, 12, -12, Pal.Ramp(Pal.Wood, 2));   // lever
            g.Box(-8, 5, -5, -7, 5, -5, Pal.Ramp(Pal.Black, 1));                                                  // nozzle into the hearth
            g.CylY(7, 0, 3f, 0, 6, p => p.y == 6 ? Pal.Wood[3] : Pal.Pick(Pal.Wood, p, 4306, 1));                // oak stump
            g.Mat(Iron);
            var anvil = Pal.Ramp(Pal.Black, 2, 4307);
            g.Box(5, 7, -2, 9, 7, 2, anvil);                                                                     // foot
            g.Box(6, 8, -1, 8, 8, 1, anvil);                                                                     // waist
            g.Box(3, 9, -2, 11, 10, 2, p => p.y == 10 ? Pal.Metal[3] : Pal.Black[2]);                            // face
            g.Box(12, 9, -1, 13, 10, 1, anvil); g.Set(14, 10, 0, anvil); g.Set(2, 10, 0, anvil);                  // horn and heel
            g.Box(5, 11, 0, 7, 11, 0, p => p.x == 7 ? Pal.LightY : Pal.Amber);                                   // hot billet
            g.Box(8, 11, -1, 12, 11, -1, Pal.Solid(Pal.Metal[2])); g.Set(12, 11, 0, Pal.Solid(Pal.Metal[2]));     // tongs
            g.Box(9, 11, 1, 10, 12, 1, Pal.Ramp(Pal.Metal, 3)); g.Box(10, 11, 2, 10, 11, 4, Pal.Ramp(Pal.Wood, 2)); // hammer
            g.Mat(Wood);
            g.CylY(12, 6, 2.2f, 0, 5, Pal.Ramp(Pal.Wood, 1, 4308), 1.4f);                                        // quench bucket
            g.CylY(12, 6, 1.4f, 0, 4, p => p.y == 4 ? Pal.PaleBlue[1] : Pal.Navy[1]);                             // water
            g.Mat(Iron);
            foreach (int y in new[] { 1, 4 }) g.CylY(12, 6, 2.4f, y, y, Pal.Ramp(Pal.Metal, 1), 1.9f);            // hoops
            return g;
        }

        /// <summary>Machine shop: a lathe (headstock, chuck, a bar in the work, carriage, tailstock) on cabinet legs,
        /// a knee mill (column, head, spindle, T-slot table) and a tool board, all on a striped floor plate.</summary>
        static VoxelGrid MachineShopGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            var paint = Pal.Weathered(Pal.Moss, 0.15f, 4321, 2, 0);
            var cast = Pal.Ramp(Pal.Metal, 1, 4322);
            g.Box(-16, 0, -6, 15, 0, 6, p => Mathf.Abs(p.z) == 6 || p.x == -16 || p.x == 15 ? (((p.x + p.z) / 2 & 1) == 0 ? Pal.Ochre[3] : Pal.Black[1]) : Pal.Pick(Pal.Metal, p, 4323, 2));   // floor plate
            // lathe
            g.Box(-15, 1, -2, -12, 7, 2, paint); g.Box(-4, 1, -2, -1, 7, 2, paint);                              // cabinet legs
            g.Box(-15, 8, -2, -1, 9, 1, cast);                                                                   // bed
            g.Box(-15, 10, -3, -11, 15, 2, paint);                                                               // headstock
            g.CylX(13, 0, 2.2f, -10, -9, Pal.Ramp(Pal.Chrome, 2, 4324));                                          // chuck
            g.CylX(13, 0, 0.8f, -8, -4, Pal.Solid(Pal.Chrome[3]));                                               // bar in the work
            g.Box(-3, 10, -2, -1, 14, 1, paint); g.CylX(13, 0, 0.6f, -4, -4, Pal.Solid(Pal.Chrome[2]));           // tailstock and centre
            g.Box(-8, 10, -1, -6, 10, 3, cast); g.Box(-7, 11, 1, -6, 11, 2, Pal.Solid(Pal.Chrome[2]));            // carriage and toolpost
            g.CylZ(-12, 12, 1.4f, 3, 3, Pal.Solid(Pal.Chrome[1])); g.CylZ(-7, 9, 1.2f, 3, 3, Pal.Solid(Pal.Chrome[1]));   // handwheels
            g.Box(-14, 14, 3, -13, 14, 3, Pal.Solid(Pal.TailR));                                                 // stop button
            // knee mill
            g.Box(6, 1, -5, 10, 24, -2, paint);                                                                  // column
            g.Box(6, 1, -1, 10, 10, 3, paint);                                                                   // knee
            g.Box(3, 11, -1, 13, 12, 4, p => p.y == 12 && (p.z & 1) == 0 ? Pal.Black[1] : Pal.Metal[2]);          // T-slot table
            g.Box(5, 18, -2, 11, 24, 3, paint);                                                                  // head
            g.CylY(8, 1.5f, 1.1f, 14, 17, Pal.Ramp(Pal.Chrome, 2, 4325)); g.Set(8, 13, 1, Pal.Solid(Pal.Chrome[3]));   // spindle and cutter
            g.Box(6, 12, 1, 7, 13, 2, Pal.Ramp(Pal.Black, 2));                                                   // vice
            g.CylX(11, 1, 1.4f, 14, 14, Pal.Solid(Pal.Chrome[1]));                                               // table handwheel
            g.Box(9, 25, -4, 11, 27, -2, Pal.Ramp(Pal.Black, 1, 4326));                                           // motor
            // tool board and a bench grinder between the machines
            g.Mat(Wood);
            g.Box(-10, 17, -6, 3, 26, -6, p => ((p.x + p.y) % 3 == 0) ? Pal.Wood[1] : Pal.Wood[2]);              // pegboard
            g.Box(-10, 1, -6, -10, 16, -6, Pal.Ramp(Pal.Wood, 1)); g.Box(3, 1, -6, 3, 16, -6, Pal.Ramp(Pal.Wood, 1));   // its posts
            g.Mat(Iron);
            for (int x = -8; x <= 1; x += 3) g.Box(x, 19, -5, x, 23, -5, Pal.Solid(Pal.Chrome[x % 2 == 0 ? 2 : 1]));   // spanners
            g.Box(1, 1, -4, 3, 8, -2, cast); g.CylX(10, -3, 1.6f, 0, 0, Pal.Ramp(Pal.Chrome, 1)); g.CylX(10, -3, 1.6f, 4, 4, Pal.Ramp(Pal.Chrome, 1));   // grinder
            g.Box(1, 9, -4, 3, 10, -2, Pal.Ramp(Pal.Moss, 2));
            return g;
        }
    }
}
