using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Build pieces for depth stage E: the feed mill (hay and grain into mixed feed), spinning wheel (wool and
    /// cotton into thread, hemp into rope), leather bench (leather goods), beehive (honey and wax) and beeswax candles,
    /// a stable with a manger (horses rest), snares and cage traps (small game over time) and a butchering table.</summary>
    public static partial class FurnitureLibrary
    {
        const byte LeatherMat = (byte)ResourceType.Leather, HideMat = (byte)ResourceType.Hide, HayMat = (byte)ResourceType.Hay,
            WoolMat = (byte)ResourceType.Wool, WaxMat = (byte)ResourceType.Beeswax;

        static IEnumerable<FurnitureDef> HusbandryPieces()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var St = ResourceType.Stone; var C = ResourceType.Cloth; var Fe = ResourceType.Iron;
            var Ga = BuildCategory.Garden; var In = BuildCategory.Industry;
            yield return D("feed_mill", "FEED MILL", In, FeedMillGrid(), 8, false, go => Station(go, "feedmill", "MILL FEED (FEED MILL)", 0f).output = new Vector3(0f, 0.6f, 0.7f), (W, 10), (Fe, 6), (St, 4));
            yield return D("spinning_wheel", "SPINNING WHEEL", In, SpinningWheelGrid(), 3, false, go => Station(go, "spinning", "SPIN (SPINNING WHEEL)", 0f), (W, 8), (Fe, 1));
            yield return D("leather_bench", "LEATHER BENCH", In, LeatherBenchGrid(), 4, false, go => Station(go, "leather", "LEATHERWORK (BENCH)", 0f).tier = 0.5f, (W, 6), (Fe, 2), (ResourceType.Leather, 1));
            yield return D("butcher_table", "BUTCHERING TABLE", In, ButcherTableGrid(), 6, false, go => go.AddComponent<ButcherTable>(), (W, 8), (Fe, 3));
            yield return D("beehive", "BEEHIVE", Ga, BeehiveGrid(), 3, false, go => go.AddComponent<Beehive>(), (W, 8), (S, 1));
            yield return D("stable", "STABLE", Ga, StableGrid(), 16, true, go =>
            {
                go.AddComponent<MadMax.Animals.Trough>();                                          // the manger
                go.AddComponent<Stable>();
            }, (W, 30), (S, 8)).Needs("misc_rope");
            yield return D("snare", "SNARE", Ga, SnareGrid(), 1, false, go => go.AddComponent<SnareTrap>(), (W, 1)).Needs("misc_rope");
            yield return D("cage_trap", "CAGE TRAP", Ga, CageTrapGrid(), 3, false, go => go.AddComponent<SnareTrap>().cage = true, (S, 3), (W, 1));
            yield return D("candles", "BEESWAX CANDLES", BuildCategory.Decor, CandlesGrid(), 1, false,
                go => Glow(go, new Vector3(0f, 0.5f, 0f), new Color(1f, 0.78f, 0.45f), 3.5f, 1.3f, false, 0f), (ResourceType.Beeswax, 2), (C, 1));
        }

        /// <summary>Hand-cranked hammer mill: a plank hopper on a frame over an iron grinder, a flywheel with a crank,
        /// a chute and feed sacks.</summary>
        static VoxelGrid FeedMillGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var oak = Pal.Ramp(Pal.Wood, 2, 3801);
            foreach (int x in new[] { -5, 5 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 12, z, oak);
            g.Box(-5, 4, -4, 5, 4, 4, Pal.Ramp(Pal.Wood, 1, 3802));                                   // brace
            for (int y = 13; y <= 20; y++)                                                             // hopper, wider at the top
            {
                int w = 2 + (y - 13) / 2;
                for (int x = -w; x <= w; x++)
                for (int z = -w; z <= w; z++)
                    if (Mathf.Abs(x) == w || Mathf.Abs(z) == w || y == 13) g.Set(x, y, z, Pal.Ramp(Pal.Wood, 2, 3803));
            }
            g.Mat(HayMat);
            g.Box(-4, 20, -4, 4, 20, 4, p => Pal.Hash(p.x, p.y, p.z, 3804) > 0.35f ? Pal.Pick(Pal.Ochre, p, 3805, 3) : Pal.Pick(Pal.Ochre, p, 3806, 2));   // hay in the hopper
            g.Mat(Iron);
            g.CylZ(0, 9, 3.2f, -3, 3, Pal.Weathered(Pal.Metal, 0.3f, 3807, 2, 0));                   // grinder drum
            g.CylX(9, 0, 5.6f, 7, 7, Pal.Ramp(Pal.Metal, 1, 3808), 4.6f);                            // flywheel rim
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                g.Tube(new Vector3(7, 9, 0), new Vector3(7, 9 + Mathf.Sin(a) * 4.6f, Mathf.Cos(a) * 4.6f), 0.3f, Pal.Ramp(Pal.Metal, 2));
            }
            g.Box(6, 9, 0, 8, 9, 0, Pal.Ramp(Pal.Chrome, 1)); g.Box(8, 9, 0, 8, 13, 0, Pal.Ramp(Pal.Rust, 2)); g.Box(9, 13, 0, 10, 13, 0, Pal.Ramp(Pal.Wood, 3));   // crank
            g.Box(-1, 3, 4, 1, 6, 8, Pal.Ramp(Pal.Rust, 2, 3809));                                   // chute
            g.Mat(Cloth);
            foreach (int x in new[] { -9, -6 }) { g.Box(x, 0, 5, x + 2, 5, 8, Pal.Ramp(Pal.Cream, 1, 3810 + x)); g.Box(x + 1, 6, 6, x + 1, 6, 7, Pal.Ramp(Pal.Cream, 0)); }   // feed sacks, tied
            return g;
        }

        /// <summary>Saxony spinning wheel: a slanted bench on three legs, the drive wheel on two uprights, a treadle and
        /// footman, the flyer with a bobbin of yarn, and a distaff dressed with wool.</summary>
        static VoxelGrid SpinningWheelGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var oak = Pal.Ramp(Pal.Wood, 2, 3821);
            g.Box(-2, 5, -7, 2, 5, 6, Pal.Ramp(Pal.Wood, 3, 3822));                                   // bench
            g.Tube(new Vector3(-2, 0, -7), new Vector3(-1, 5, -6), 0.5f, oak);
            g.Tube(new Vector3(2, 0, -7), new Vector3(1, 5, -6), 0.5f, oak);
            g.Tube(new Vector3(0, 0, 7), new Vector3(0, 5, 5), 0.5f, oak);
            foreach (int x in new[] { -2, 2 }) g.Box(x, 6, 2, x, 15, 2, oak);                          // uprights
            g.CylX(14, 2, 6.3f, 0, 0, Pal.Ramp(Pal.Wood, 3, 3823), 5.2f);                           // drive wheel rim
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                g.Tube(new Vector3(0, 14, 2), new Vector3(0, 14 + Mathf.Sin(a) * 5.2f, 2 + Mathf.Cos(a) * 5.2f), 0.3f, Pal.Ramp(Pal.Wood, 1, 3824));
            }
            g.Box(-2, 14, 2, 2, 14, 2, Pal.Ramp(Pal.Metal, 2));                                       // axle
            g.Box(-2, 1, -1, 2, 1, 3, Pal.Ramp(Pal.Wood, 1, 3825));                                   // treadle
            g.Tube(new Vector3(1, 1, 1), new Vector3(1, 11, 2), 0.3f, Pal.Ramp(Pal.Wood, 0));        // footman
            g.Box(-1, 7, -7, 1, 9, -4, oak);                                                         // mother-of-all
            g.Mat(WoolMat);
            g.CylZ(0, 10, 1.3f, -7, -5, Pal.Ramp(Pal.Cream, 3, 3826));                               // bobbin of yarn
            g.Mat(Wood); g.Box(2, 6, -2, 2, 18, -2, oak);                                            // distaff
            g.Mat(WoolMat); g.CylY(2, -2, 1.6f, 16, 20, p => Pal.Pick(Pal.Cream, p, 3827, 4));        // dressed with wool
            return g;
        }

        /// <summary>Leather bench: a heavy table with a hide being cut, awl, mallet, knife, a spool of thread, a
        /// stitching clamp and leather strips hanging behind.</summary>
        static VoxelGrid LeatherBenchGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var oak = Pal.Ramp(Pal.Wood, 1, 3841);
            foreach (int x in new[] { -9, 9 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 8, z, oak);
            g.Box(-9, 3, -4, 9, 3, 4, Pal.Ramp(Pal.Wood, 0, 3842));                                  // shelf
            g.Box(-9, 9, -4, 9, 10, 4, Pal.Ramp(Pal.Wood, 3, 3843));                                 // top
            g.Mat(LeatherMat);
            for (int x = -8; x <= 2; x++)
            for (int z = -3; z <= 3; z++)
                if (Mathf.Abs(x + 3) + Mathf.Abs(z) <= 7 || Pal.Hash(x, 0, z, 3844) < 0.4f) g.Set(x, 11, z, Pal.Ramp(Pal.Wood, 3, 3845));   // a hide, cut to a pattern
            for (int i = 0; i < 4; i++) g.Box(-7 + i * 3, 12, -4, -7 + i * 3, 20, -4, Pal.Ramp(i % 2 == 0 ? Pal.Wood : Pal.Black, 2, 3846 + i));   // strips on the rack
            g.Mat(Wood);
            g.Box(-9, 11, -5, 9, 21, -5, Pal.Ramp(Pal.Wood, 2, 3850));                               // backboard
            g.Box(6, 11, -1, 6, 16, 0, oak); g.Box(8, 11, -1, 8, 16, 0, oak);                        // stitching clamp
            g.Box(4, 11, 2, 4, 11, 3, Pal.Ramp(Pal.Wood, 3)); g.Box(4, 12, 1, 5, 13, 3, Pal.Ramp(Pal.Wood, 2));   // mallet
            g.Mat(Iron);
            g.Box(-1, 11, 2, 1, 11, 2, Pal.Solid(Pal.Chrome[3])); g.Box(-3, 11, 2, -2, 11, 2, Pal.Ramp(Pal.Wood, 1));   // round knife
            g.Set(2, 11, -2, Pal.Solid(Pal.Chrome[2])); g.Set(2, 12, -2, Pal.Ramp(Pal.Wood, 2));    // awl
            g.Mat(Cloth); g.CylY(-6, 3, 1f, 11, 12, Pal.Ramp(Pal.Cream, 2, 3851));                    // spool of waxed thread
            return g;
        }

        /// <summary>Butchering table: a thick end-grain block on stout legs, a cleaver, a hook rail on two posts with a
        /// side hanging, and a bucket.</summary>
        static VoxelGrid ButcherTableGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var oak = Pal.Ramp(Pal.Wood, 1, 3861);
            foreach (int x in new[] { -8, 7 }) foreach (int z in new[] { -4, 3 }) g.Box(x, 0, z, x + 1, 7, z + 1, oak);
            g.Box(-9, 8, -5, 9, 10, 5, Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 3862), Pal.Ramp(Pal.Wood, 2, 3863), 0, 3));   // end-grain block
            g.Box(-9, 10, -5, 9, 10, 5, p => Pal.Hash(p.x, 0, p.z, 3864) > 0.9f ? Pal.Crimson[1] : Pal.Pick(Pal.Wood, p, 3865, 3));   // stained top
            g.Mat(Iron);
            foreach (int x in new[] { -9, 9 }) g.Box(x, 11, -6, x, 26, -6, Pal.Ramp(Pal.Metal, 1, 3866));
            g.Box(-9, 26, -6, 9, 26, -6, Pal.Ramp(Pal.Metal, 2, 3867));                              // hook rail
            foreach (int x in new[] { -5, 0, 5 }) g.Box(x, 23, -6, x, 25, -6, Pal.Solid(Pal.Chrome[2]));
            g.Mat(HideMat);
            g.Box(-1, 16, -7, 1, 22, -5, p => p.y == 22 || Mathf.Abs(p.x) == 1 && p.y > 19 ? Pal.Pick(Pal.Cream, p, 3868, 2) : Pal.Pick(Pal.Crimson, p, 3869, 2));   // a side of meat
            g.Mat(Iron);
            g.Box(1, 11, 1, 5, 11, 2, p => p.x == 5 ? Pal.Chrome[3] : Pal.Chrome[1]); g.Box(-2, 11, 1, 0, 11, 1, Pal.Ramp(Pal.Wood, 0));   // cleaver
            g.CylY(-6, 8, 1.8f, 0, 4, Pal.Weathered(Pal.Metal, 0.4f, 3870, 2, 0), 1f);               // bucket
            return g;
        }

        /// <summary>Box hive: three painted supers on a stand with a landing board, a tin lid, bees at the entrance and
        /// a drip of honey.</summary>
        static VoxelGrid BeehiveGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var oak = Pal.Ramp(Pal.Wood, 1, 3881);
            foreach (int x in new[] { -5, 5 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 4, z, oak);
            g.Box(-6, 5, -5, 6, 5, 7, Pal.Ramp(Pal.Wood, 2, 3882));                                  // stand and landing board
            for (int b = 0; b < 3; b++)
            {
                int y0 = 6 + b * 5;
                var paint = b % 2 == 0 ? Pal.Weathered(Pal.Cream, 0.15f, 3883 + b, 2, 0) : Pal.Weathered(Pal.Ochre, 0.15f, 3883 + b, 3, 0);
                g.Box(-5, y0, -4, 5, y0 + 4, 4, p => p.y == y0 + 4 ? Pal.Wood[1] : paint(p));
                foreach (int x in new[] { -6, 6 }) g.Box(x, y0 + 2, -1, x, y0 + 2, 1, Pal.Ramp(Pal.Wood, 0));   // hand holds
            }
            g.Box(-3, 6, 4, 3, 6, 4, Pal.Solid(Pal.Black[0]));                                        // entrance
            g.Mat(Scrap);
            g.Box(-6, 21, -5, 6, 22, 5, p => p.y == 22 && (p.x & 1) == 0 ? Pal.Metal[3] : Pal.Pick(Pal.Metal, p, 3887, 2));   // tin lid
            g.Mat(Stone); g.Box(-2, 23, -1, 1, 24, 1, Pal.Ramp(Pal.Fur, 2, 3888));                   // a stone on the lid
            g.Mat(WaxMat);
            g.Set(4, 7, 5, Pal.Solid(Pal.Amber)); g.Set(4, 6, 5, Pal.Solid(Pal.Ochre[4]));            // a drip of honey
            foreach (var (x, z) in new[] { (-2, 5), (0, 6), (2, 5), (-1, 7) }) g.Set(x, 6, z, (x + z) % 2 == 0 ? Pal.Solid(Pal.LightY) : Pal.Solid(Pal.Black[1]));   // bees
            return g;
        }

        /// <summary>Three beeswax candles of different heights on a tin dish.</summary>
        static VoxelGrid CandlesGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 2.6f, 0, 0, Pal.Ramp(Pal.Metal, 2, 3891));
            g.Mat(WaxMat);
            foreach (var (x, z, h) in new[] { (-1, -1, 5), (1, 0, 3), (0, 1, 4) })
            {
                g.Box(x, 1, z, x, h, z, Pal.Ramp(Pal.Cream, 3, 3892 + h));
                g.Set(x, h + 1, z, Pal.Solid(Pal.Black[1]));                                         // wick
                g.Set(x, h + 2, z, Pal.Solid(Pal.LightY));                                           // flame
            }
            return g;
        }

        /// <summary>Stable: an open-fronted plank shed (4.2 x 3 m, 2.5 m to the eaves) with a tin roof, two stalls
        /// split by a low partition, a manger of hay along the back and a heap of straw in the corner.</summary>
        static VoxelGrid StableGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            var post = Pal.Ramp(Pal.Wood, 1, 3901);
            var plank = Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 3902), Pal.Ramp(Pal.Wood, 1, 3903), 1, 4);
            int Roof(int z) => 31 + (z + 20) * 4 / 41;
            foreach (int x in new[] { -26, 26 }) foreach (int z in new[] { -19, 19 }) g.Box(x, 0, z, x, Roof(z) - 1, z, post);
            g.Box(-26, 0, -19, 26, 30, -19, plank);                                                   // back wall
            foreach (int x in new[] { -26, 26 }) g.Box(x, 0, -18, x, 20, 18, plank);                  // side walls
            g.Box(-26, Roof(19) - 2, 19, 26, Roof(19) - 1, 19, post);                                 // header beam over the open front
            g.Box(0, 0, -18, 0, 14, 2, plank);                                                        // stall partition
            g.Box(0, 0, 2, 0, 16, 2, post);
            g.Box(-25, 7, -18, 25, 8, -15, Pal.Ramp(Pal.Wood, 1, 3904));                             // manger
            g.Box(-25, 9, -15, 25, 11, -15, Pal.Ramp(Pal.Wood, 2, 3905));
            g.Mat(HayMat);
            for (int x = -24; x <= 24; x++)
            for (int z = -18; z <= -16; z++)
                g.Box(x, 9, z, x, Pal.Hash(x, 0, z, 3906) > 0.6f ? 10 : 9, z, Pal.Ramp(Pal.Ochre, 3, 3907));   // hay in the manger
            g.CylY(21, 13, 3.6f, 0, 1, p => Pal.Pick(Pal.Ochre, p, 3908, 3)); g.CylY(21, 13, 2.4f, 2, 3, p => Pal.Pick(Pal.Ochre, p, 3909, 2));   // straw heap
            g.Mat(Scrap);
            for (int z = -21; z <= 22; z++)
                g.Box(-28, Roof(z), z, 28, Roof(z), z, p => (p.x & 3) == 0 ? Pal.Metal[1] : Pal.Hash(p.x / 4, 0, p.z / 3, 3910) > 0.8f ? Pal.Rust[2] : Pal.Metal[2]);   // corrugated tin
            g.Box(-25, 22, -18, -23, 22, -18, Pal.Solid(Pal.Chrome[1]));                              // bridle hook
            g.Mat(LeatherMat); g.Box(-24, 17, -18, -24, 21, -18, Pal.Ramp(Pal.Wood, 0, 3911));        // a bridle hanging
            return g;
        }

        /// <summary>Snare: a stake, a bent sapling as the spring, and a wire noose over the run.</summary>
        static VoxelGrid SnareGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(0, 0, -3, 0, 4, -3, Pal.Ramp(Pal.Wood, 2, 3921));
            g.Tube(new Vector3(-2, 0, -5), new Vector3(0, 8, -3), 0.4f, Pal.Ramp(Pal.Moss, 3, 3922));   // green sapling
            g.Mat(Scrap);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * 2f), 3 + Mathf.RoundToInt(Mathf.Sin(a) * 2f), 1, Pal.Ramp(Pal.Chrome, 2, 3923));
            }
            g.Box(0, 5, -3, 0, 5, 0, Pal.Ramp(Pal.Chrome, 1)); g.Box(0, 6, -3, 0, 8, -3, Pal.Ramp(Pal.Chrome, 1));   // the line to the spring
            return g;
        }

        /// <summary>Cage trap: a wire-mesh box with a drop door and a carry handle.</summary>
        static VoxelGrid CageTrapGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            for (int x = -3; x <= 3; x++)
            for (int y = 0; y <= 4; y++)
            for (int z = -5; z <= 5; z++)
            {
                int edges = (x == -3 || x == 3 ? 1 : 0) + (y == 0 || y == 4 ? 1 : 0) + (z == -5 || z == 5 ? 1 : 0);
                if (edges == 0) continue;
                if (edges >= 2 || y == 0) g.Set(x, y, z, Pal.Ramp(Pal.Metal, 2, 3931));                  // frame and floor
                else if (((x + y + z) & 1) == 0) g.Set(x, y, z, Pal.Ramp(Pal.Chrome, 1, 3932));           // mesh
            }
            g.Box(-1, 5, 0, 1, 5, 0, Pal.Ramp(Pal.Metal, 1)); g.Set(-1, 5, -1, Pal.Ramp(Pal.Metal, 1)); g.Set(1, 5, -1, Pal.Ramp(Pal.Metal, 1));   // handle
            g.Box(-2, 5, 4, 2, 5, 5, Pal.Ramp(Pal.Rust, 2, 3933));                                    // door latch
            return g;
        }
    }
}
