using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Liquid handling pieces: makeshift tarp pool, clay pond, storage drum (any liquid; <see cref="FluidStore"/>),
    /// pond liner (<see cref="PondLiner"/>: line a dug hole), standpipe tap (<see cref="WaterTap"/>), transfer pump
    /// (<see cref="TransferPump"/>) and the one-way pipe (a check valve between two water networks).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> FluidsPieces()
        {
            var U = BuildCategory.Utility;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var C = ResourceType.Cloth; var Rb = ResourceType.Rubber;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper;

            var oneWay = new FurnitureDef { id = "pipe_oneway", name = "ONE-WAY PIPE", category = U, cost = new[] { (Cu, 1) }, link = UtilityKind.Water | UtilityKind.OneWay, mesh = Spool(false) };
            oneWay.desc = "A PIPE WITH A CHECK VALVE: WATER RUNS FROM THE FIRST PIECE TO THE SECOND, NEVER BACK. DOWNHILL BY ITSELF, UPHILL ONLY FROM A PUMPED NETWORK.";
            yield return oneWay;

            var pool = D("pool_tarp", "MAKESHIFT POOL", U, FlPoolGrid(), 4, false, go =>
            {
                Node(go, UtilityKind.Water, 0.5f);
                var s = go.AddComponent<FluidStore>();
                s.title = "POOL"; s.capacity = 1500f; s.open = true;
                s.surfaceMin = new Vector3(-1.04f, 0.1f, -1.04f); s.surfaceMax = new Vector3(1.04f, 0.5f, 1.04f); s.drain = new Vector3(0f, 0.06f, 1.3f);
            }, (W, 8), (C, 6), (Rb, 2));
            pool.desc = "A TIMBER FRAME LINED WITH TARP: HOLDS 1500 L OF ANY ONE LIQUID. WATER IN IT JOINS ITS PIPES; IT CATCHES RAIN, EVAPORATES IN THE SUN AND FREEZES OVER. OPEN THE DRAIN TO EMPTY IT.";
            yield return pool;

            var pond = D("clay_pond", "CLAY POND", U, FlPondGrid(), 8, false, go =>
            {
                Node(go, UtilityKind.Water, 0.3f);
                var s = go.AddComponent<FluidStore>();
                s.title = "POND"; s.capacity = 4000f; s.open = true; s.seepPerDay = 0.02f; s.muddy = true;
                s.surfaceMin = new Vector3(-1.36f, 0.06f, -1.36f); s.surfaceMax = new Vector3(1.36f, 0.36f, 1.36f); s.drain = new Vector3(0f, 0.04f, 1.75f);
            }, (ResourceType.Clay, 20), (ResourceType.Stone, 8));
            pond.desc = "PUDDLED CLAY BANKS: 4000 L, CHEAP AND MUDDY. SEEPS A LITTLE EVERY DAY; THE WATER NEEDS FILTERING.";
            yield return pond;

            var drum = D("storage_drum", "STORAGE DRUM", U, FlDrumGrid(), 4, false, go =>
            {
                Node(go, UtilityKind.Water, 0.6f);
                var s = go.AddComponent<FluidStore>();
                s.title = "DRUM"; s.capacity = 200f; s.open = false; s.drainRate = 0.6f;
                s.surfaceMin = new Vector3(-0.26f, 0.06f, -0.26f); s.surfaceMax = new Vector3(0.26f, 0.84f, 0.26f); s.drain = new Vector3(0f, 0.12f, 0.42f);
            }, (S, 6), (Fe, 2));
            drum.desc = "A SEALED 200 L DRUM WITH A TAP: FUEL, OIL, COOLANT OR WATER. NO RAIN GETS IN, NOTHING EVAPORATES. OPEN THE TAP TO RUN IT OUT.";
            yield return drum;

            var liner = D("pond_liner", "POND LINER", U, FlLinerGrid(), 2, false, go => go.AddComponent<PondLiner>(), (C, 4), (Rb, 3));
            liner.desc = "A 3 X 3 M TARP: DIG A HOLE, LAY IT IN AND NOTHING SOAKS AWAY. WHAT YOU POUR, PUMP OR RAIN INTO IT STAYS AS A POND.";
            yield return liner;

            var tap = D("water_tap", "STANDPIPE TAP", U, FlTapGrid(), 3, false, go =>
            {
                Node(go, UtilityKind.Water, 0.3f).waterCapacity = 2f;
                go.AddComponent<WaterTap>();
            }, (Fe, 1), (S, 2));
            tap.desc = "A TAP ON THE WATER PIPES: FILL A CONTAINER OR DRINK; LEFT RUNNING IT FILLS WHAT IS UNDER IT, WATERS A BED OR FLOODS A FURROW.";
            yield return tap;

            var pump = D("transfer_pump", "TRANSFER PUMP", U, FlPumpGrid(), 4, false, go =>
            {
                Node(go, UtilityKind.Power | UtilityKind.Water, 0.5f).waterCapacity = 10f;
                go.AddComponent<TransferPump>();
            }, (Fe, 4), (Cu, 2), (Rb, 2));
            pump.desc = "A PUMP WITH A HOSE: IN FROM A POND, PUDDLE, LAKE OR ANOTHER STORE INTO ITS PIPES, OR OUT ONTO THE GROUND. 1 L/S ON POWER, OR CRANK IT BY HAND.";
            yield return pump;
        }

        /// <summary>Makeshift pool: a 2.4 m square of timber walls, corner posts and a blue tarp lining folded over the rim.</summary>
        static VoxelGrid FlPoolGrid()
        {
            const int h = 14, wall = 6;
            var g = new VoxelGrid().Mat(Wood);
            for (int x = -h; x <= h; x++)
            for (int z = -h; z <= h; z++)
                if (Mathf.Abs(x) == h || Mathf.Abs(z) == h) g.Box(x, 0, z, x, wall, z, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 4301), Pal.Ramp(Pal.Wood, 1, 4302), 1, 2));
            foreach (int x in new[] { -h, h }) foreach (int z in new[] { -h, h }) g.Box(x, 0, z, x, wall + 1, z, Pal.Ramp(Pal.Wood, 0, 4303));   // posts
            g.Mat(Cloth);
            g.Box(-h + 1, 0, -h + 1, h - 1, 0, h - 1, Pal.Ramp(Pal.Navy, 1, 4304));                                               // tarp floor
            for (int x = -h + 1; x <= h - 1; x++) foreach (int z in new[] { -h + 1, h - 1 }) g.Box(x, 1, z, x, wall, z, Pal.Ramp(Pal.Navy, 2, 4305));
            for (int z = -h + 1; z <= h - 1; z++) foreach (int x in new[] { -h + 1, h - 1 }) g.Box(x, 1, z, x, wall, z, Pal.Ramp(Pal.Navy, 2, 4305));
            for (int x = -h; x <= h; x++) foreach (int z in new[] { -h, h }) g.Set(x, wall + 1, z, Pal.Ramp(Pal.Navy, 3, 4306));    // folded over the rim
            for (int z = -h; z <= h; z++) foreach (int x in new[] { -h, h }) g.Set(x, wall + 1, z, Pal.Ramp(Pal.Navy, 3, 4306));
            g.Mat(Scrap);
            g.Box(0, 0, h + 1, 0, 1, h + 2, Pal.Ramp(Pal.Chrome, 1, 4307)); g.Set(0, 1, h + 3, Pal.Solid(Pal.Crimson[2]));             // drain spigot, red valve
            for (int i = -h; i <= h; i += 7) g.Set(i, wall - 1, h + 1, Pal.Solid(Pal.Chrome[2]));                                    // rope ties
            return g;
        }

        /// <summary>Clay pond: a ring of puddled clay banks around a muddy bed, stones on the banks, a plank outlet.</summary>
        static VoxelGrid FlPondGrid()
        {
            const int h = 19;
            var g = new VoxelGrid().Mat((byte)ResourceType.Clay);
            for (int x = -h; x <= h; x++)
            for (int z = -h; z <= h; z++)
            {
                int ring = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (ring <= h - 3) { g.Set(x, 0, z, Pal.Ramp(Pal.Ochre, 0, 4311)); continue; }                                   // bed
                int top = ring == h ? 3 : 4;
                g.Box(x, 0, z, x, top, z, Pal.Ramp(Pal.Ochre, ring == h ? 1 : 2, 4312));
            }
            g.Mat(Stone);
            var rng = new System.Random(4313);
            for (int i = 0; i < 18; i++)
            {
                int side = rng.Next(4), t = rng.Next(-h + 2, h - 1);
                int x = side == 0 ? -h + 1 : side == 1 ? h - 1 : t, z = side == 2 ? -h + 1 : side == 3 ? h - 1 : t;
                g.Set(x, 5, z, Pal.Ramp(Pal.Sand, 1, 4314 + i));
            }
            g.Mat(Wood);
            g.Box(-1, 0, h - 2, 1, 1, h + 3, Pal.Ramp(Pal.Wood, 2, 4315));                                                       // outlet trough
            g.Box(-2, 2, h, 2, 4, h, Pal.Ramp(Pal.Wood, 1, 4316));                                                              // sluice board
            return g;
        }

        /// <summary>Storage drum: a ribbed steel barrel on a pallet with a brass tap near the bottom and bungs on top.</summary>
        static VoxelGrid FlDrumGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-4, 0, -4, 4, 0, 4, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 4321), Pal.Ramp(Pal.Wood, 1, 4322), 0, 3));            // pallet
            g.Mat(Scrap);
            g.CylY(0, 0, 3.6f, 1, 11, Pal.Weathered(Pal.Olive, 0.45f, 4323, 2, 2f));
            foreach (int y in new[] { 1, 4, 8, 11 }) g.CylY(0, 0, 3.9f, y, y, Pal.Ramp(Pal.Metal, 2, 4324));                  // ribs and rims
            g.Set(1, 12, 1, Pal.Solid(Pal.Chrome[1])); g.Set(-2, 12, 0, Pal.Solid(Pal.Chrome[2]));                              // bungs
            g.Box(0, 1, 4, 0, 2, 5, Pal.Solid(Pal.Bronze[2])); g.Set(0, 3, 5, Pal.Solid(Pal.Crimson[2]));                        // tap, red handle
            g.Box(-2, 6, 4, 2, 7, 4, Pal.Solid(Pal.LightY));                                                                    // hazard label
            return g;
        }

        /// <summary>Pond liner: a flat tarp (it drapes itself over the dug ground when laid).</summary>
        static VoxelGrid FlLinerGrid()
        {
            const int h = 18;
            var g = new VoxelGrid().Mat(Cloth);
            g.Box(-h, 0, -h, h, 0, h, p => (Mathf.Abs(p.x) == h || Mathf.Abs(p.z) == h) ? Pal.Ochre[1] : ((p.x + p.z) & 7) == 0 ? Pal.Navy[2] : Pal.Navy[1]);
            return g;
        }

        /// <summary>Standpipe: a steel riser on a concrete foot, a cross-handle valve and a spout.</summary>
        static VoxelGrid FlTapGrid()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.Box(-2, 0, -2, 2, 1, 2, Pal.Ramp(Pal.Sand, 1, 4331));
            g.Mat(Scrap);
            g.Box(0, 2, 0, 0, 8, 0, Pal.Ramp(Pal.Chrome, 1, 4332));
            g.Box(0, 8, 0, 0, 8, 3, Pal.Ramp(Pal.Chrome, 1, 4333)); g.Set(0, 7, 3, Pal.Ramp(Pal.Chrome, 2));                     // spout
            g.Box(-1, 9, 0, 1, 9, 0, Pal.Solid(Pal.Crimson[2])); g.Set(0, 9, -1, Pal.Solid(Pal.Crimson[2])); g.Set(0, 9, 1, Pal.Solid(Pal.Crimson[2]));   // handle
            g.Set(0, 1, -3, Pal.Ramp(Pal.Chrome, 0));                                                                            // pipe in
            return g;
        }

        /// <summary>Transfer pump: a pump body on a skid, motor, a pressure gauge and a hose coiled beside it and run forward.</summary>
        static VoxelGrid FlPumpGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-4, 0, -3, 4, 0, 3, Pal.Ramp(Pal.Metal, 1, 4341));                                                           // skid
            g.CylX(3, 0, 2.6f, -3, 1, Pal.Weathered(Pal.Crimson, 0.35f, 4342, 2, 0));                                          // volute
            g.Box(2, 1, -2, 4, 5, 2, Pal.Weathered(Pal.Olive, 0.3f, 4343, 2, 0));                                              // motor
            g.Set(3, 6, 0, Pal.Solid(Pal.Amber));
            g.Mat(Glass); g.Set(-1, 6, 0, Pal.Solid(Pal.Cream[4])); g.Set(-1, 6, 1, Pal.Solid(Pal.Black[0]));                  // gauge
            g.Mat((byte)ResourceType.Rubber);
            g.CylY(-1, -5, 2.4f, 1, 2, Pal.Ramp(Pal.Tire, 1, 4344), 1.4f);                                                       // coiled hose
            g.Box(0, 1, 3, 0, 1, 11, Pal.Ramp(Pal.Tire, 1, 4345));                                                              // hose to the intake
            g.Mat(Scrap); g.Box(-1, 0, 11, 1, 1, 12, Pal.Ramp(Pal.Chrome, 1));                                                 // strainer
            return g;
        }
    }
}
