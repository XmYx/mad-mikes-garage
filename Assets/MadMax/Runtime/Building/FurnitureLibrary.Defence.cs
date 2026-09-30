using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Depth stage I — building materials and defence. Materials: the saw bench (hand saw: wood → planks, slow)
    /// and the powered sawmill (fast, more planks per log), the brick mould (clay + water → green bricks, fired in the
    /// kiln), prefab concrete panels cast at the mixer; plank, fired-brick and prefab-panel walls outlast the raw-material
    /// ones and upgrade plank → fired brick → panel. Defence: sandbag walls, the watchtower (climb up, see far), tilt-rod
    /// landmines, tripwires (flare + alarm bell), a gate frame with a motorised sliding gate (opens for the owner's
    /// vehicles) and the sandbagged MG nest (fired by a gunner) that upgrades to the powered auto turret.</summary>
    public static partial class FurnitureLibrary
    {
        const byte DfPlank = (byte)ResourceType.Plank, DfBrick = (byte)ResourceType.Brick, DfSand = (byte)ResourceType.Sand;
        public const string ConcretePanelKit = "kit_concrete_panel", LandmineKit = "kit_landmine";

        static IEnumerable<FurnitureDef> DefencePieces()
        {
            var B = BuildCategory.Structure; var Df = BuildCategory.Defence; var In = BuildCategory.Industry;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var C = ResourceType.Cloth; var Rb = ResourceType.Rubber;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Co = ResourceType.Concrete; var Li = ResourceType.Lime;
            var Pl = ResourceType.Plank; var Br = ResourceType.Brick; var Sa = ResourceType.Sand;

            // ---- material stations: hand saw → sawmill, brick mould (the kiln fires the bricks, the mixer casts panels)
            yield return D("saw_bench", "SAW BENCH", In, DfSawBench(), 5, false, go => Station(go, "saw_bench", "SAW PLANKS (HAND SAW)", 0f).output = new Vector3(1f, 0.4f, 0f), (W, 6), (Fe, 1));
            yield return D("sawmill", "SAWMILL", In, DfSawmill(), 12, false, go =>
            {
                Node(go, UtilityKind.Power, 0.6f);
                var st = Station(go, "sawmill", "SAWMILL (CIRCULAR SAW)", 1500f);
                st.tier = 0.5f; st.output = new Vector3(1f, 1f, 0f);
            }, (Fe, 10), (Cu, 4), (W, 6), (S, 4));
            yield return D("brick_mould", "BRICK MOULD", In, DfBrickMould(), 4, false, go => Station(go, "brick_mould", "MOULD BRICKS", 0f).output = new Vector3(1f, 0.3f, -0.2f), (W, 5), (Fe, 1));

            // ---- walls of processed materials: more hits than wood (8), brick (20) and poured concrete (40)
            yield return D("wall_plank", "PLANK WALL", B, DfPlankWall(0, 3701), 14, true, null, (Pl, 6)).DfUp("wall_brick_fired");
            yield return D("wall_plank_window", "PLANK WINDOW WALL", B, DfPlankWall(1, 3702), 14, true, null, (Pl, 5), (G, 2)).DfUp("wall_brick_fired_window");
            yield return D("doorway_plank", "PLANK DOORWAY", B, DfPlankWall(2, 3703), 14, true, null, (Pl, 5)).DfUp("doorway_brick_fired");
            yield return D("floor_plank", "PLANK FLOOR", B, DfPlankFloor(), 14, true, null, (Pl, 4)).Deck(1f, 1f, 0.12f, 0.12f).DfUp("floor_concrete");
            yield return D("wall_brick_fired", "FIRED BRICK WALL", B, DfBrickWall(0, 3711), 32, true, null, (Br, 12), (Li, 1)).DfUp("wall_panel");
            yield return D("wall_brick_fired_window", "FIRED BRICK WINDOW WALL", B, DfBrickWall(1, 3712), 32, true, null, (Br, 10), (Li, 1), (G, 2));
            yield return D("doorway_brick_fired", "FIRED BRICK DOORWAY", B, DfBrickWall(2, 3713), 32, true, null, (Br, 10), (Li, 1)).DfUp("doorway_panel");
            yield return DfKitPiece("wall_panel", "PREFAB CONCRETE WALL", DfPanelWall(0), ConcretePanelKit, B, 60, true);
            yield return DfKitPiece("doorway_panel", "PREFAB CONCRETE DOORWAY", DfPanelWall(2), ConcretePanelKit, B, 60, true);

            // ---- defence
            yield return D("sandbag_wall", "SANDBAG WALL", Df, DfSandbags(), 16, false, null, (Sa, 10), (C, 2));
            yield return D("watchtower", "WATCHTOWER", Df, DfWatchtower(), 24, true, go => go.AddComponent<Watchtower>(), (Pl, 18), (W, 10), (S, 6), (Fe, 2));
            var mine = DfKitPiece("landmine", "LANDMINE", DfMine(), LandmineKit, Df, 1, false);
            mine.setup = go => { DfCollider(go, new Vector3(0f, 0.04f, 0f), new Vector3(0.52f, 0.16f, 0.52f)); go.AddComponent<Landmine>(); };
            yield return mine;
            yield return D("tripwire", "TRIPWIRE AND FLARE", Df, DfTripwire(), 2, false, go =>
            {
                DfCollider(go, new Vector3(0.96f, 0.3f, 0.04f), new Vector3(0.24f, 0.6f, 0.24f));                  // only the flare stake is solid: the wire trips, it doesn't block
                go.AddComponent<Tripwire>();
            }, (S, 2), (Fe, 1), (ResourceType.Gunpowder, 1));
            yield return D("gate_frame", "GATE FRAME", B, DfGateFrame(), 40, true, null, (Co, 10), (Fe, 2));
            yield return D("motorised_gate", "MOTORISED GATE", Df, DfGate(0), 36, false, DfGateSetup, (Fe, 10), (S, 8), (Cu, 3), (Rb, 2)).Snap("gate_frame");
            yield return D("mg_nest", "MG NEST", Df, DfNest(), 18, true, go =>
            {
                Box(go, "NEST AMMO (MG BELTS)", 12f, false);
                go.AddComponent<GunNest>();
                var t = go.AddComponent<AutoTurret>();
                t.head = DfNestGun(go); t.range = 34f; t.watts = 0f;                                               // no power: it fires only with a gunner
            }, (Sa, 14), (C, 3), (Fe, 8), (Cu, 1)).DfUp("auto_turret");
        }

        static FurnitureDef DfUp(this FurnitureDef d, string to) { d.upgrade = to; return d; }

        static FurnitureDef DfKitPiece(string id, string name, VoxelGrid g, string kit, BuildCategory cat, int hits, bool meshCollider)
        {
            var d = Kit(id, name, g, kit, cat);
            d.hits = hits; d.meshCollider = meshCollider;
            return d;
        }

        /// <summary>Resize the piece's box collider (set up by <see cref="Spawn"/>) to the part that should be solid.</summary>
        static void DfCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var b = go.GetComponent<BoxCollider>();
            if (b) { b.center = center; b.size = size; }
        }

        // ------------------------------------------------------------------ material stations

        /// <summary>Saw bench: a plank top on two A-frame trestles, a log half cut with a red bow saw standing in the kerf,
        /// sawn planks stacked beside it and sawdust underneath.</summary>
        static VoxelGrid DfSawBench()
        {
            var g = new VoxelGrid().Mat(Wood);
            var frame = Pal.Ramp(Pal.Wood, 1, 3741);
            foreach (int x in new[] { -8, 8 })
            {
                g.Tube(new Vector3(x, 0, -4), new Vector3(x, 9, 0), 0.5f, frame);
                g.Tube(new Vector3(x, 0, 4), new Vector3(x, 9, 0), 0.5f, frame);
                g.Box(x, 4, -2, x, 4, 2, frame);
            }
            g.Box(-10, 10, -2, 10, 10, 2, PlankMat(3742));                                                        // bench top
            g.CylX(13, 0, 2.6f, -9, 9, p => Mathf.Abs(p.x) == 9 ? Pal.Wood[4] : Pal.Pick(Pal.Wood, p, 3743, 1));   // the log, pale cut ends
            g.ClearBox(2, 12, -3, 2, 16, 3);                                                                     // the kerf
            g.Mat(Iron);
            g.Box(2, 12, -4, 2, 12, 4, Pal.Solid(Pal.Chrome[2]));                                                  // blade
            var bow = Pal.Ramp(Pal.Crimson, 3, 3744);
            g.Tube(new Vector3(2, 12, -4), new Vector3(2, 19, -2), 0.5f, bow);
            g.Tube(new Vector3(2, 19, -2), new Vector3(2, 19, 2), 0.5f, bow);
            g.Tube(new Vector3(2, 19, 2), new Vector3(2, 12, 4), 0.5f, bow);
            g.Mat(DfPlank);
            g.Box(11, 0, -3, 17, 3, 3, p => p.y % 2 == 1 ? Pal.Wood[2] : Pal.Pick(Pal.Wood, new Vector3Int(p.z / 2, p.y, 0), 3745, 3));   // sawn planks
            g.Mat(Wood);
            for (int x = -7; x <= 7; x++) for (int z = -4; z <= 4; z++) if (Pal.Hash(x, 0, z, 3746) > 0.55f) g.Set(x, 0, z, Pal.Solid(Pal.Wood[4]));   // sawdust
            return g;
        }

        /// <summary>Sawmill: a steel table 2.8 m long on legs with carriage rails, a big circular blade under a yellow
        /// guard, the electric motor and belt below, a log on the in-feed and sawn planks stacked on the out-feed.</summary>
        static VoxelGrid DfSawmill()
        {
            var g = new VoxelGrid().Mat(Iron);
            foreach (int x in new[] { -16, -5, 5, 16 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Metal, 1, 3751));
            g.Box(-17, 10, -5, 17, 10, 5, Pal.Weathered(Pal.Metal, 0.3f, 3752, 2, 0));                              // table
            foreach (int z in new[] { -5, 5 }) g.Box(-17, 11, z, 17, 11, z, Pal.Ramp(Pal.Chrome, 1, 3753));          // carriage rails
            for (int x = -6; x <= 6; x++)                                                                         // the blade, teeth bright
            for (int y = 5; y <= 17; y++)
            {
                float r = Mathf.Sqrt(x * x + (y - 11) * (y - 11));
                if (r > 6.2f) continue;
                bool tooth = r > 5.2f && (Mathf.RoundToInt(Mathf.Atan2(y - 11, x) * 4f) & 1) == 0;
                g.Set(x, y, 0, Pal.Solid(r < 1.5f ? Pal.Metal[0] : tooth ? Pal.Chrome[3] : Pal.Chrome[1]));
            }
            for (int x = -7; x <= 7; x++) g.Box(x, 16 + Mathf.RoundToInt(Mathf.Sqrt(49 - x * x) * 0.35f), -1, x, 18 + Mathf.RoundToInt(Mathf.Sqrt(49 - x * x) * 0.35f), 1, Pal.Ramp(Pal.Ochre, 3, 3754));   // guard
            g.Box(-4, 2, -3, 4, 7, 3, Pal.Weathered(Pal.RigGreen, 0.25f, 3755, 3, 0));                              // motor
            g.CylZ(3, 5, 1.2f, 4, 4, Pal.Ramp(Pal.Chrome, 1));                                                    // pulley
            g.Tube(new Vector3(3, 5, 4), new Vector3(0, 10, 1), 0.5f, Pal.Ramp(Pal.Black, 1));                   // belt
            g.Box(16, 5, 5, 16, 7, 6, Pal.Ramp(Pal.Black, 2)); g.Set(16, 6, 7, Pal.Solid(Pal.Crimson[4]));          // switch box
            g.Mat(Wood);
            g.CylX(14, 0, 2.8f, -16, -8, p => p.x == -8 || p.x == -16 ? Pal.Wood[4] : Pal.Pick(Pal.Wood, p, 3756, 1));   // log on the in-feed
            g.Mat(DfPlank);
            g.Box(9, 11, -3, 16, 13, 3, p => p.y == 12 ? Pal.Wood[2] : Pal.Pick(Pal.Wood, new Vector3Int(p.z / 2, p.y, 0), 3757, 3));   // planks on the out-feed
            g.Mat(Wood);
            for (int x = -5; x <= 5; x++) for (int z = -3; z <= 3; z++) if (Pal.Hash(x, 0, z, 3758) > 0.4f) g.Set(x, 0, z, Pal.Solid(Pal.Wood[4]));   // sawdust
            return g;
        }

        /// <summary>Brick moulding table: a low bench with a three-brick wooden mould full of wet clay, a tub of clay and a
        /// water bucket beside it, and green bricks drying on a pallet.</summary>
        static VoxelGrid DfBrickMould()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -8, 8 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 6, z, Pal.Ramp(Pal.Wood, 1, 3761));
            g.Box(-9, 7, -5, 9, 7, 5, PlankMat(3762));
            g.Box(-7, 8, -2, 7, 9, 2, Pal.Ramp(Pal.Wood, 2, 3763));                                                // the mould
            var clay = Pal.Ramp(Pal.Olive, 2, 3764);
            foreach (int c in new[] { -4, 0, 4 }) g.Repaint(c - 1, 9, -1, c + 1, 9, 1, clay);
            g.Mat((byte)ResourceType.Clay);
            g.Box(-5, 8, 3, -2, 8, 4, clay);                                                                     // a lump waiting
            g.Mat(Iron);
            g.CylY(-13, 0, 2.6f, 0, 4, Pal.Weathered(Pal.Metal, 0.4f, 3765, 2, 0), 1.6f);                        // clay tub
            g.Mat((byte)ResourceType.Clay); g.CylY(-13, 0, 1.6f, 0, 3, clay);
            g.Mat(Iron); g.CylY(13, 3, 1.5f, 0, 3, Pal.Ramp(Pal.Metal, 2, 3766), 0.8f);                           // water bucket
            g.Set(13, 3, 3, Pal.Solid(Pal.PaleBlue[1]));
            g.Mat(Wood); g.Box(11, 0, -5, 17, 0, -1, Pal.Ramp(Pal.Wood, 0, 3767));                               // pallet
            g.Mat((byte)ResourceType.Clay);
            for (int x = 11; x <= 16; x += 3) for (int z = -5; z <= -2; z += 2) g.Box(x, 1, z, x + 1, 2, z, Pal.Ramp(Pal.Olive, 3, 3768 + x));   // green bricks drying
            return g;
        }

        // ------------------------------------------------------------------ walls and floors

        /// <summary>2 × 2.4 m wall of sawn planks: staggered horizontal boards with shadow gaps and nail heads outside,
        /// a stud frame inside; hole 0 none, 1 window, 2 doorway (the same openings as the other walls).</summary>
        static VoxelGrid DfPlankWall(int hole, int seed)
        {
            var g = new VoxelGrid().Mat(DfPlank);
            VoxMat board = p =>
            {
                int row = p.y / 3, run = p.x + 12 + (row & 1) * 6;
                if (p.y % 3 == 2 || run % 12 == 0) return Pal.Wood[1];                                          // shadow gap, butt joints
                return Pal.Hash(p, seed + 1) > 0.9f ? Pal.Wood[2] : Pal.Pick(Pal.Wood, new Vector3Int(run / 12, row, 0), seed, 3);
            };
            VoxMat stud = Pal.Ramp(Pal.Wood, 2, seed + 2);
            for (int x = -12; x <= 12; x++)
            for (int y = 0; y <= 29; y++)
            {
                bool window = hole == 1 && Mathf.Abs(x) <= 5 && y >= 12 && y <= 22;
                bool door = hole == 2 && Mathf.Abs(x) <= 6 && y <= 27;
                if (door) continue;
                if (window)
                {
                    if (y == 17 || x == 0) { g.Set(x, y, 1, Pal.Ramp(Pal.Wood, 3)); continue; }                   // mullions
                    g.Mat(Glass); g.Set(x, y, 1, Pal.Ramp(Pal.Glass, 2, seed)); g.Mat(DfPlank); continue;
                }
                bool nail = (x == -12 || x == 12 || x == -9 || x == 9) && y % 3 == 1;
                g.Set(x, y, 1, nail ? Pal.Solid(Pal.Metal[3]) : board);
                bool frame = x == -12 || x == 12 || x == -9 || x == 9 || y == 0 || y == 29;
                g.Set(x, y, 0, frame ? stud : board);
            }
            if (hole == 1) g.Box(-6, 11, 2, 6, 11, 2, Pal.Ramp(Pal.Wood, 3, seed + 3));                          // sill
            if (hole == 2) { g.Box(-7, 0, 2, -7, 28, 2, Pal.Ramp(Pal.Wood, 2)); g.Box(7, 0, 2, 7, 28, 2, Pal.Ramp(Pal.Wood, 2)); g.Box(-7, 28, 2, 7, 28, 2, Pal.Ramp(Pal.Wood, 2)); }
            return g;
        }

        /// <summary>2 × 2 m floor of sawn boards on joists, nailed at the seams.</summary>
        static VoxelGrid DfPlankFloor()
        {
            var g = new VoxelGrid().Mat(DfPlank);
            g.Box(-12, 0, -12, 12, 0, 12, p => (p.x + 12) % 6 == 0 ? Pal.Wood[0] : Pal.Wood[1]);                   // joists
            g.Box(-12, 1, -12, 12, 1, 12, p =>
            {
                int lane = (p.x + 12) / 4, run = p.z + 12 + (lane & 1) * 8;
                if ((p.x + 12) % 4 == 3 || run % 16 == 0) return Pal.Wood[2];
                if ((p.z + 12) % 6 == 0 && (p.x + 12) % 4 == 1) return Pal.Metal[3];                             // nail heads
                return Pal.Pick(Pal.Wood, new Vector3Int(lane, run / 16, 0), 3722, 3);
            });
            return g;
        }

        /// <summary>2 × 2.4 m wall of kiln-fired bricks in stretcher bond with pale mortar joints, a soldier course on top,
        /// a concrete sill under the window and a steel lintel over the doorway.</summary>
        static VoxelGrid DfBrickWall(int hole, int seed)
        {
            var g = new VoxelGrid().Mat(DfBrick);
            VoxMat brick = p =>
            {
                int course = p.y / 3;
                if (p.y % 3 == 2) return Pal.Cream[1];                                                          // bed joint
                bool soldier = p.y >= 27;
                int run = soldier ? p.x + 12 : p.x + 12 + (course & 1) * 3;
                if (run % (soldier ? 3 : 6) == 0) return Pal.Cream[1];                                          // head joint
                var b = new Vector3Int(run / 6, course, p.z);
                float h = Pal.Hash(b, seed);
                return h < 0.1f ? Pal.Crimson[3] : h < 0.2f ? Pal.Rust[2] : Pal.Pick(Pal.Rust, b, seed + 1, 3);   // fired red, a few dark clinkers
            };
            for (int x = -12; x <= 12; x++)
            for (int y = 0; y <= 29; y++)
            {
                bool window = hole == 1 && Mathf.Abs(x) <= 5 && y >= 12 && y <= 22;
                bool door = hole == 2 && Mathf.Abs(x) <= 6 && y <= 27;
                if (door) continue;
                if (window)
                {
                    if (y == 17 || x == 0) { g.Mat(Wood); g.Set(x, y, 1, Pal.Ramp(Pal.Wood, 1)); g.Mat(DfBrick); continue; }
                    g.Mat(Glass); g.Set(x, y, 1, Pal.Ramp(Pal.Glass, 2, seed)); g.Mat(DfBrick); continue;
                }
                g.Set(x, y, 0, brick); g.Set(x, y, 1, brick);
            }
            if (hole == 1) { g.Mat(Concrete); g.Box(-6, 11, 0, 6, 11, 2, Pal.Ramp(ConcRamp, 2, seed + 2)); }
            if (hole == 2) { g.Mat(Iron); g.Box(-7, 28, 0, 7, 28, 1, Pal.Weathered(Pal.Metal, 0.4f, seed + 3, 2, 0)); }
            return g;
        }

        /// <summary>Prefab concrete panel (2 × 2.4 m): form-face concrete with tie-rod holes, a steel edge frame and two
        /// lifting anchors in the top edge; the doorway panel has a steel lintel and jambs.</summary>
        static VoxelGrid DfPanelWall(int hole)
        {
            var g = new VoxelGrid().Mat(Concrete);
            VoxMat face = p => (p.x + 12) % 8 == 4 && p.y % 8 == 4 ? Pal.Metal[2] : Pal.Pick(ConcRamp, p, 3731, p.y < 3 ? 1 : 2);
            VoxMat steel = Pal.Weathered(Pal.Metal, 0.3f, 3732, 2, 0);
            for (int x = -12; x <= 12; x++)
            for (int y = 0; y <= 29; y++)
            {
                if (hole == 2 && Mathf.Abs(x) <= 6 && y <= 27) continue;
                bool edge = x == -12 || x == 12 || y == 29 || (hole == 2 && (Mathf.Abs(x) == 7 && y <= 28 || y == 28 && Mathf.Abs(x) <= 7));
                g.Mat(edge ? Iron : Concrete);
                g.Set(x, y, 0, edge ? steel : face); g.Set(x, y, 1, edge ? steel : face);
            }
            g.Mat(Iron);
            foreach (int x in new[] { -7, 7 }) g.Box(x, 29, 0, x, 29, 1, Pal.Solid(Pal.Chrome[2]));            // cast-in lifting anchors, flush with the top
            return g;
        }

        // ------------------------------------------------------------------ defences

        /// <summary>2 m of sandbags, 1.25 m high: eight staggered courses of burlap bags with tied ends, two bags deep at
        /// the foot, one at the top.</summary>
        static VoxelGrid DfSandbags()
        {
            var g = new VoxelGrid().Mat(DfSand);
            for (int c = 0; c < 8; c++)
            {
                int y0 = c * 2, off = (c & 1) * 4;
                foreach (int z0 in c < 4 ? new[] { -4, 0 } : new[] { -2 })
                    for (int x0 = -12 - off; x0 <= 12; x0 += 8)
                        DfBag(g, Mathf.Max(-12, x0), Mathf.Min(12, x0 + 7), y0, z0, c * 31 + x0 * 7 + z0 + 40);
            }
            return g;
        }

        static void DfBag(VoxelGrid g, int x0, int x1, int y0, int z0, int seed)
        {
            var cloth = Pal.Ramp(Pal.Olive, Pal.Hash(seed, 0, 0, 3771) > 0.5f ? 3 : 2, seed);
            var tie = Pal.Ramp(Pal.Olive, 1, seed);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y0 + 1; y++)
            for (int z = z0; z <= z0 + 3; z++)
            {
                bool end = x == x0 || x == x1, side = z == z0 || z == z0 + 3;
                if (end && side && y == y0 + 1) continue;                                                         // rounded corners
                g.Set(x, y, z, end ? tie : cloth);
            }
        }

        /// <summary>Timber watchtower: four posts with X-bracing, a plank platform 4.4 m up behind a waist-high parapet
        /// and rail, a tin roof, a ladder up the front, a searchlight on the rail and an ammo crate up top.</summary>
        static VoxelGrid DfWatchtower()
        {
            var g = new VoxelGrid().Mat(Wood);
            const int F = 55, T = 86;                                                                           // platform deck, roof eaves
            var post = Pal.Ramp(Pal.Wood, 1, 3781);
            foreach (int x in new[] { -14, 14 }) foreach (int z in new[] { -14, 14 }) g.Box(x - 1, 0, z - 1, x + 1, T - 1, z + 1, post);
            var brace = Pal.Ramp(Pal.Wood, 2, 3782);
            for (int bay = 0; bay < 2; bay++)
            {
                int y0 = 3 + bay * 26, y1 = y0 + 24;
                foreach (int s in new[] { -14, 14 })
                {
                    g.Tube(new Vector3(-13, y0, s), new Vector3(13, y1, s), 0.6f, brace); g.Tube(new Vector3(13, y0, s), new Vector3(-13, y1, s), 0.6f, brace);
                    g.Tube(new Vector3(s, y0, -13), new Vector3(s, y1, 13), 0.6f, brace); g.Tube(new Vector3(s, y0, 13), new Vector3(s, y1, -13), 0.6f, brace);
                }
            }
            for (int z = -16; z <= 16; z += 4) g.Box(-16, F - 1, z, 16, F - 1, z, Pal.Ramp(Pal.Wood, 0, 3783));   // joists
            g.Mat(DfPlank);
            g.Box(-16, F, -16, 16, F, 16, p => (p.x + 16) % 4 == 3 ? Pal.Wood[2] : Pal.Pick(Pal.Wood, new Vector3Int(p.x / 4, 0, 0), 3784, 3));   // deck
            var parapet = Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 3785), Pal.Ramp(Pal.Wood, 1, 3786), 1, 3);
            for (int x = -16; x <= 16; x++)
            for (int z = -16; z <= 16; z++)
            {
                if (Mathf.Abs(x) != 16 && Mathf.Abs(z) != 16) continue;
                if (z == 16 && Mathf.Abs(x) <= 4) continue;                                                       // the ladder gap
                g.Box(x, F + 1, z, x, F + 8, z, parapet);
                g.Set(x, F + 13, z, Pal.Ramp(Pal.Wood, 1, 3787));                                                 // top rail
                if (Mathf.Abs(x) == 16 ? (z + 16) % 8 == 0 : (x + 16) % 8 == 0)
                    g.Box(x, F + 9, z, x, F + 12, z, Pal.Ramp(Pal.Wood, 1, 3788));                               // rail uprights
            }
            g.Mat(Scrap);
            for (int z = -18; z <= 18; z++)
            {
                int y = T + (18 - Mathf.Abs(z)) / 3;
                g.Box(-18, y, z, 18, y, z, p => (p.x & 3) == 0 ? Pal.Rust[1] : Pal.Pick(Pal.Rust, p, 3789, 2));   // tin roof
            }
            g.Mat(Wood);
            foreach (int x in new[] { -4, 4 }) g.Box(x, 0, 17, x, F + 12, 17, Pal.Ramp(Pal.Wood, 1, 3790));       // ladder stiles
            for (int y = 3; y <= F; y += 4) g.Box(-3, y, 17, 3, y, 17, Pal.Ramp(Pal.Wood, 2, 3791));             // rungs
            g.Mat(Iron);
            g.Box(11, F + 14, 15, 13, F + 16, 17, Pal.Weathered(Pal.Metal, 0.3f, 3792, 2, 0));                     // searchlight
            g.Mat(Glass); g.Box(11, F + 14, 18, 13, F + 16, 18, Pal.Solid(Pal.LightW));
            g.Mat(Wood); g.Box(-14, F + 1, -14, -10, F + 4, -11, Pal.Stripe(Pal.Ramp(Pal.RigGreen, 3, 3793), Pal.Solid(Pal.Cream[2]), 1, 4));   // ammo crate
            return g;
        }

        /// <summary>Tilt-rod mine laid flush: a dirt-caked steel pan, a pressure plate and a thin rod standing in the
        /// grass; a red arming tag.</summary>
        static VoxelGrid DfMine()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.CylY(0, 0, 3.2f, 0, 0, p => Pal.Hash(p, 3795) > 0.45f ? Pal.Sand[0] : Pal.Metal[1]);
            g.CylY(0, 0, 1.6f, 1, 1, Pal.Weathered(Pal.Olive, 0.35f, 3796, 1, 0));
            g.Box(0, 2, 0, 0, 4, 0, Pal.Ramp(Pal.Metal, 0));
            g.Set(2, 1, 0, Pal.Solid(Pal.Crimson[4]));
            return g;
        }

        /// <summary>Tripwire: two stakes 1.9 m apart, a wire at shin height, a flare canister and its pin on the right stake.</summary>
        static VoxelGrid DfTripwire()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -12, 12 }) g.Box(x, 0, 0, x, 4, 0, Pal.Ramp(Pal.Wood, 1, 3801 + x));
            g.Mat(Iron);
            for (int x = -11; x <= 11; x++) g.Set(x, 2, 0, Pal.Ramp(Pal.Chrome, 2, 3802));
            g.CylY(12, 1, 0.9f, 3, 7, p => p.y == 7 ? Pal.Ochre[3] : Pal.Crimson[3]);
            g.Set(11, 3, 0, Pal.Ramp(Pal.Chrome, 3));
            return g;
        }

        /// <summary>Gate frame: two concrete posts 4.3 m apart (a 3.9 m opening) with hazard paint on the inner faces, a
        /// guide roller for the gate leaf on the left post and a warning lamp on the right.</summary>
        static VoxelGrid DfGateFrame()
        {
            var g = new VoxelGrid().Mat(Concrete);
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 25, 0, -2, s * 28, 35, 2, ConcMat(3811));
                g.Box(s * 24, 36, -3, s * 29, 37, 3, Pal.Ramp(ConcRamp, 2, 3812));
                g.Repaint(s * 25, 2, -2, s * 25, 26, 2, p => (p.y / 3 & 1) == 0 ? Pal.Ochre[3] : Pal.Black[1]);
            }
            g.Mat(Iron);
            g.Box(-25, 20, 3, -25, 26, 6, Pal.Ramp(Pal.Metal, 2, 3813));                                         // guide bracket, clear of the leaf
            g.CylY(-24, 5.5f, 0.8f, 21, 25, Pal.Ramp(Pal.Chrome, 1));
            g.Mat(Glass); g.Box(26, 38, 0, 27, 39, 1, Pal.Solid(Pal.Amber));
            return g;
        }

        /// <summary>Motorised sliding gate: part 1 = the drive (motor housing on its pad beside the right post, the rail
        /// along the ground), 2 = the leaf (steel bars in a frame, hazard-striped bottom rail, spikes on top), 0 = motor
        /// and leaf (the build ghost). The leaf runs 4.1 m to the right, in front of the posts.</summary>
        static VoxelGrid DfGate(int part)
        {
            var g = new VoxelGrid().Mat(Iron);
            if (part != 2)
            {
                g.Mat(Concrete); g.Box(28, 0, 5, 35, 0, 10, Pal.Ramp(ConcRamp, 1, 3821));
                g.Mat(Iron);
                g.Box(29, 1, 6, 34, 7, 9, Pal.Weathered(Pal.RigGreen, 0.3f, 3822, 3, 0));
                g.Repaint(29, 1, 9, 34, 2, 9, p => ((p.x + p.y) & 1) == 0 ? Pal.Ochre[3] : Pal.Black[1]);
                g.Set(31, 5, 10, Pal.Solid(Pal.Crimson[4])); g.Set(33, 5, 10, Pal.Solid(Pal.Moss[4]));             // status lamps
                if (part == 1) g.Box(-25, 0, 3, 76, 0, 4, p => (p.x & 1) == 0 ? Pal.Metal[2] : Pal.Metal[1]);    // rail (left off the ghost: it needs no room)
            }
            if (part != 1)
            {
                var bar = Pal.Ramp(Pal.Metal, 2, 3823);
                g.Box(-24, 1, 3, 24, 3, 4, p => ((p.x + 24) / 3 & 1) == 0 ? Pal.Ochre[3] : Pal.Black[1]);
                g.Box(-24, 22, 3, 24, 23, 4, Pal.Weathered(Pal.Metal, 0.35f, 3824, 2, 0));
                for (int x = -21; x <= 21; x += 3) g.Box(x, 4, 3, x, 21, 3, bar);
                foreach (int x in new[] { -24, 24 }) g.Box(x, 4, 3, x, 21, 4, bar);
                g.Tube(new Vector3(-23, 4, 4), new Vector3(23, 21, 4), 0.5f, Pal.Ramp(Pal.Metal, 1, 3825));
                for (int x = -23; x <= 23; x += 3) { g.Set(x, 24, 3, Pal.Ramp(Pal.Chrome, 1)); g.Set(x, 25, 3, Pal.Ramp(Pal.Chrome, 2)); }
            }
            return g;
        }

        static Mesh dfGateDrive, dfGateLeaf;

        /// <summary>The gate piece keeps the drive as its own mesh and box (the motor housing: what raiders batter), and
        /// gets a sliding child leaf (kinematic body, box collider) that blocks people and vehicles while shut.</summary>
        static void DfGateSetup(GameObject go)
        {
            if (!dfGateLeaf || !dfGateDrive)
            {
                var a = DfGate(1); a.Bevel(); dfGateDrive = VoxelMesher.Build(a, "Furniture_motor_gate_drive");
                var b = DfGate(2); b.Bevel(); dfGateLeaf = VoxelMesher.Build(b, "Furniture_motor_gate_leaf");
            }
            if (go.TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = dfGateDrive;
            DfCollider(go, new Vector3(2.54f, 0.32f, 0.6f), new Vector3(0.5f, 0.6f, 0.34f));
            var leaf = new GameObject("Leaf", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            leaf.transform.SetParent(go.transform, false);
            leaf.GetComponent<MeshFilter>().sharedMesh = dfGateLeaf;
            if (go.TryGetComponent<MeshRenderer>(out var mr)) leaf.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;
            var lb = leaf.GetComponent<BoxCollider>(); lb.center = dfGateLeaf.bounds.center; lb.size = dfGateLeaf.bounds.size;
            var rb = leaf.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            leaf.AddComponent<GateLeaf>();
            var n = Node(go, UtilityKind.Power, 0.6f);
            n.port = new Vector3(2.54f, 0.62f, 0.6f);
            var gate = go.AddComponent<MotorGate>();
            gate.leaf = leaf.transform; gate.driveMesh = dfGateDrive;
        }

        /// <summary>MG nest: a horseshoe of sandbags 0.9 m high, open at the back, a steel pintle on a tripod and an ammo
        /// crate for the gunner to sit on, spare ammo cans.</summary>
        static VoxelGrid DfNest()
        {
            var g = new VoxelGrid().Mat(DfSand);
            for (int x = -15; x <= 15; x++)
            for (int z = -15; z <= 15; z++)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                if (r < 10.5f || r > 15f) continue;
                float a = Mathf.Atan2(x, z) * Mathf.Rad2Deg;                                                     // 0 = front (+Z)
                if (Mathf.Abs(a) > 125f) continue;                                                                // open at the back
                for (int y = 0; y <= 11; y++)
                {
                    int course = y / 2;
                    if (course >= 4 && r > 13.6f) continue;                                                       // narrower at the top
                    float along = a * Mathf.Deg2Rad * 13f + (course & 1) * 4f;
                    int bag = Mathf.FloorToInt(along / 8f);
                    bool seam = Mathf.Repeat(along, 8f) < 1f;
                    g.Set(x, y, z, seam ? Pal.Solid(Pal.Olive[1]) : Pal.Solid(Pal.Pick(Pal.Olive, new Vector3Int(bag, course, 0), 3831, Pal.Hash(bag, course, 0, 3832) > 0.5f ? 3 : 2)));
                }
            }
            g.Mat(Iron);
            g.Box(0, 0, 3, 0, 9, 3, Pal.Ramp(Pal.Metal, 1, 3833));                                                // pintle
            foreach (var foot in new[] { new Vector3(-4, 0, 0), new Vector3(4, 0, 0), new Vector3(0, 0, 7) })
                g.Tube(foot, new Vector3(0, 6, 3), 0.5f, Pal.Ramp(Pal.Metal, 0, 3834));
            g.Mat(Wood);
            g.Box(-3, 0, -9, 3, 5, -6, Pal.Stripe(Pal.Ramp(Pal.RigGreen, 3, 3835), Pal.Solid(Pal.Cream[2]), 1, 5)); // ammo crate seat
            g.Mat(Iron);
            g.Box(8, 0, -6, 10, 2, -5, Pal.Weathered(Pal.Olive, 0.25f, 3836, 2, 0));                              // spare ammo cans
            g.Box(6, 0, -9, 8, 2, -8, Pal.Weathered(Pal.Olive, 0.25f, 3837, 2, 0));
            return g;
        }

        static Mesh dfNestGunMesh;

        /// <summary>The nest's machine gun on its cradle (turns as the <see cref="AutoTurret"/> head).</summary>
        static Transform DfNestGun(GameObject go)
        {
            if (!dfNestGunMesh)
            {
                var g = new VoxelGrid().Mat(Iron);
                g.Box(-1, 0, -4, 1, 2, 3, Pal.Weathered(Pal.Black, 0.2f, 3841, 2, 0));                                  // receiver
                g.CylZ(0, 1, 1f, 4, 16, p => (p.z & 1) == 0 ? Pal.Black[0] : Pal.Black[2]);                            // perforated jacket
                g.CylZ(0, 1, 0.5f, 17, 18, Pal.Solid(Pal.Black[0]));                                                  // muzzle
                g.Box(-3, -1, -1, -2, 1, 1, Pal.Weathered(Pal.Olive, 0.2f, 3842, 2, 0));                                // ammo box
                g.Box(-1, -2, -1, 1, -1, 1, Pal.Ramp(Pal.Metal, 1));                                                    // cradle
                foreach (int x in new[] { -2, 2 }) g.Box(x, 0, -7, x, 1, -5, Pal.Ramp(Pal.Metal, 2));                  // spade grips
                g.Set(0, 3, -1, Pal.Solid(Pal.Chrome[2]));                                                             // rear sight
                g.Bevel();
                dfNestGunMesh = VoxelMesher.Build(g, "Furniture_mg_nest_gun");
            }
            var h = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer));
            h.transform.SetParent(go.transform, false);
            h.transform.localPosition = new Vector3(0f, 0.86f, 0.24f);
            h.GetComponent<MeshFilter>().sharedMesh = dfNestGunMesh;
            if (go.TryGetComponent<MeshRenderer>(out var mr)) h.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;
            return h.transform;
        }
    }
}
