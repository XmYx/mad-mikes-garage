using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The rolling city's layout, seeded (roadmap 28): the deck either side of the main street is cut into lots
    /// (alleys between them, the gangway plaza on the right kept open) and each lot is built as a prefab panel block
    /// (balconies glazed, caged or sheeted, AC boxes, rust streaks, lift house and mast on the roof), an old downtown
    /// brick or stone block (cornices, a neon sign, shacks hung off the outer wall), a works hall (corrugated, sawtooth
    /// roof, pipes, banded stacks), a market hall or a container yard with the crane. The stern carries the engine works
    /// and its stacks; galleries with huts and lamps hang off the deck sides; cables sag between masts and stacks;
    /// bridges cross the street between facing blocks.</summary>
    public static partial class CityDesign
    {
        public enum LotKind { Panel, Brick, Works, Market, Yard }

        public class Lot
        {
            public LotKind kind;
            public int x0, x1, z0, z1, floors, seed, street, doorZ, index;
            public int shackFloor = -1;
            /// <summary>The wall facing the main street / the deck edge (street = +1: the street is toward +x).</summary>
            public int StreetX => street > 0 ? x1 : x0;
            public int OuterX => street > 0 ? x0 : x1;
            public int Top => floors * Storey;
        }

        public struct StackDef { public int x, z, y0, h, style, owner; public float r; }
        public struct BridgeDef { public int xL, xR, z0, z1, y; }

        public class Plan
        {
            public readonly List<Lot> lots = new List<Lot>();
            public readonly List<StackDef> stacks = new List<StackDef>();
            public readonly List<BridgeDef> bridges = new List<BridgeDef>();
            public readonly List<Vector3Int> anchors = new List<Vector3Int>();
            public bool crane;
        }

        static readonly Color32[] Panel = { Pal.Hex("6e685a"), Pal.Hex("837c6c"), Pal.Hex("989080"), Pal.Hex("aaa290") };

        /// <summary>Smokestack tops (deck-local metres) of the layout for this seed.</summary>
        public static List<Vector3> Stacks(int seed)
        {
            var list = new List<Vector3>();
            foreach (var s in Layout(seed).stacks) list.Add(new Vector3(s.x * V, (s.y0 + s.h + 2) * V, s.z * V));
            return list;
        }

        /// <summary>The seeded layout: lots, stacks, bridges and cable anchors (pure, thread-safe).</summary>
        public static Plan Layout(int seed)
        {
            var plan = new Plan();
            var rnd = new System.Random(seed * 7919 + 28);
            bool market = false;
            foreach (var (x0, x1, street, segs) in new[] { (-68, -20, +1, new[] { (-102, 114) }), (20, 68, -1, new[] { (-102, -26), (26, 114) }) })
            {
                int firstOfSide = plan.lots.Count;
                foreach (var (a, b) in segs)
                {
                    int z = a;
                    while (b - z + 1 >= 36)
                    {
                        int len = 36 + rnd.Next(0, 37);
                        if (b - (z + len + 4) + 1 < 36) len = b - z + 1;
                        var lot = new Lot { x0 = x0, x1 = x1, z0 = z, z1 = z + len - 1, street = street, seed = rnd.Next() & 0x3fffffff, index = plan.lots.Count };
                        double r = rnd.NextDouble();
                        if (street < 0 && lot.z1 >= 100 && !plan.crane && r < 0.45) { lot.kind = LotKind.Yard; plan.crane = true; }
                        else if (!market && r < 0.15) { lot.kind = LotKind.Market; market = true; }
                        else if (r < 0.33) lot.kind = LotKind.Works;
                        else if (r < 0.58) lot.kind = LotKind.Brick;
                        else lot.kind = LotKind.Panel;
                        plan.lots.Add(lot);
                        z += len + 4;
                    }
                }
                bool panel = false;
                for (int i = firstOfSide; i < plan.lots.Count; i++) panel |= plan.lots[i].kind == LotKind.Panel;
                if (!panel)
                {
                    Lot best = null;
                    for (int i = firstOfSide; i < plan.lots.Count; i++) if (plan.lots[i].kind != LotKind.Yard && plan.lots[i].kind != LotKind.Market && (best == null || plan.lots[i].z1 - plan.lots[i].z0 > best.z1 - best.z0)) best = plan.lots[i];
                    if (best != null) best.kind = LotKind.Panel;
                }
            }
            foreach (var lot in plan.lots)
            {
                var r = new System.Random(lot.seed);
                lot.floors = lot.kind switch { LotKind.Panel => 5 + r.Next(0, 8), LotKind.Brick => 3 + r.Next(0, 5), LotKind.Works => 2 + r.Next(0, 2), LotKind.Market => 2, _ => 0 };
                // set back from the street or the deck edge a little
                int back = lot.kind == LotKind.Yard ? 0 : r.Next(0, 5), edge = r.Next(0, 3);
                if (lot.street > 0) { lot.x1 -= back; lot.x0 += edge; } else { lot.x0 += back; lot.x1 -= edge; }
                lot.doorZ = lot.z1 - lot.z0 >= 64 ? (lot.z0 + lot.z1) / 2 : lot.z1 - 9;
                if ((lot.kind == LotKind.Panel || lot.kind == LotKind.Brick) && r.NextDouble() < 0.7) lot.shackFloor = 1 + r.Next(0, Mathf.Max(1, lot.floors - 1));
                if (lot.kind == LotKind.Panel || lot.kind == LotKind.Brick)
                {
                    int mx = (lot.x0 + lot.x1) / 2 - lot.street * 8, mz = lot.z0 + 18 + r.Next(0, Mathf.Max(1, lot.z1 - lot.z0 - 24));
                    plan.anchors.Add(new Vector3Int(mx, lot.Top + 14, mz));
                }
                if (lot.kind == LotKind.Works)
                {
                    int n = lot.z1 - lot.z0 > 56 ? 2 : 1;
                    for (int k = 0; k < n; k++)
                    {
                        var s = new StackDef
                        {
                            x = lot.OuterX + lot.street * 10, z = n == 1 ? (lot.z0 + lot.z1) / 2 : (k == 0 ? lot.z0 + 12 : lot.z1 - 12),
                            y0 = lot.Top + 1, h = 36 + r.Next(0, 40), r = 2.5f + (float)r.NextDouble() * 1.5f, style = r.Next(0, 3), owner = lot.index,
                        };
                        plan.stacks.Add(s);
                        plan.anchors.Add(new Vector3Int(s.x, s.y0 + s.h * 2 / 3, s.z));
                    }
                }
            }
            // the stern works: three or four stacks of different heights
            var xs = new List<int> { -62, -48, -32, 32, 48, 62 };
            int ns = 3 + rnd.Next(0, 2);
            for (int k = 0; k < ns; k++)
            {
                int i = rnd.Next(0, xs.Count); int x = xs[i]; xs.RemoveAt(i);
                var s = new StackDef { x = x, z = -116, y0 = 2 * Storey + 3, h = 50 + rnd.Next(0, 56), r = 4f + (float)rnd.NextDouble() * 2.5f, style = rnd.Next(0, 3), owner = -1 };
                plan.stacks.Add(s);
                plan.anchors.Add(new Vector3Int(s.x, s.y0 + s.h * 2 / 3, s.z));
            }
            // bridges over the street between facing blocks of three floors or more
            foreach (var l in plan.lots)
            {
                if (plan.bridges.Count >= 2 || l.street < 0 || l.floors < 3 || (l.kind != LotKind.Panel && l.kind != LotKind.Brick)) continue;
                foreach (var rl in plan.lots)
                {
                    if (rl.street > 0 || rl.floors < 3 || (rl.kind != LotKind.Panel && rl.kind != LotKind.Brick)) continue;
                    int a = Mathf.Max(l.z0, rl.z0) + 34, b = Mathf.Min(l.z1, rl.z1) - 4;     // clear of the stair bays
                    if (b - a < 12) continue;
                    int z0 = (a + b) / 2 - 6;
                    int f = Mathf.Min(3, Mathf.Min(l.floors, rl.floors) - 1);
                    plan.bridges.Add(new BridgeDef { xL = l.x1, xR = rl.x0, z0 = z0, z1 = z0 + 11, y = f * Storey });
                    break;
                }
            }
            return plan;
        }

        static bool BridgeAt(Plan plan, int z0, int z1, int y)
        {
            foreach (var b in plan.bridges) if (b.y == y && z1 >= b.z0 - 3 && z0 <= b.z1 + 3) return true;
            return false;
        }

        static VoxelGrid LotGrid(Plan plan, Lot lot)
        {
            var g = new VoxelGrid();
            switch (lot.kind)
            {
                case LotKind.Panel: PanelBlock(g, plan, lot); break;
                case LotKind.Brick: BrickBlock(g, plan, lot); break;
                case LotKind.Works: WorksHall(g, lot); break;
                case LotKind.Market: MarketHall(g, lot); break;
                default: Yard(g, lot); break;
            }
            foreach (var b in plan.bridges)                                                      // doors onto the bridges
                if (b.z0 >= lot.z0 && b.z1 <= lot.z1 && (lot.StreetX == b.xL || lot.StreetX == b.xR)) Door(g, b.z0 + 3, b.y + 1, lot.StreetX, false, 6, 9);
            foreach (var s in plan.stacks) if (s.owner == lot.index) Stack(g, s);
            return g;
        }

        // ------------------------------------------------------------------ lot kinds
        /// <summary>A prefab panel block: panel seams every storey and 3 m, a dense window grid, balconies on both long
        /// faces (open rail, sheeted, glazed or caged; some with an AC box), shop signs on the ground floor, a lift house,
        /// a water tank, a mast and a roof garden.</summary>
        static void PanelBlock(VoxelGrid g, Plan plan, Lot lot)
        {
            var r = new System.Random(lot.seed);
            int x0 = lot.x0, x1 = lot.x1, z0 = lot.z0, z1 = lot.z1, F = lot.floors, seed = lot.seed & 0xffff;
            var ramp = r.NextDouble() < 0.6 ? Panel : Conc;
            VoxMat wall = p =>
            {
                if (p.y % Storey == 0) return Conc[0];
                if (((p.x + p.z) % 12 + 12) % 12 == 0) return Conc[1];
                if (Pal.Hash(p.x + p.z * 3, 0, 0, seed) < 0.12f && Pal.Hash(p, seed) < 0.75f) return Pal.Rust[1];
                return Pal.Pick(ramp, p, seed, 2);
            };
            Block(g, x0, z0, x1, z1, F, Stone, wall, Pal.Ramp(Conc, 1, seed), seed, lot.street, lot.doorZ, 8, 3);
            // balconies on the street and outer faces at the window columns (each window opened into a door)
            foreach (int side in new[] { -1, 1 })
            {
                int wx = side < 0 ? x0 : x1;
                bool streetFace = wx == lot.StreetX;
                for (int f = 1; f < F; f++)
                {
                    if (!streetFace && f == lot.shackFloor) continue;
                    for (int z = z0 + 4; z + 3 <= z1 - 3; z += 8)
                    {
                        if (Pal.Hash(z, f, wx, seed) > 0.55f) continue;
                        if (streetFace && BridgeAt(plan, z - 1, z + 3, f * Storey)) continue;
                        Balcony(g, wx, side, f * Storey, z - 1, z + 3, seed + z * 31 + f);
                    }
                }
            }
            // shop signs along the ground floor
            g.Mat(Scrap);
            int sx = lot.StreetX + lot.street;
            for (int z = z0 + 2; z < z1 - 10; z += 14)
                if (Pal.Hash(z, 1, sx, seed) < 0.6f)
                {
                    var c = Pal.Hash(z, 2, sx, seed) < 0.5f ? Pal.Crimson : Pal.Navy;
                    g.Box(sx, 9, z, sx, 10, z + 9, p => (p.z & 1) == 0 && p.y == 10 ? LampGlow : Pal.Pick(c, p, seed, 2));
                }
            if (lot.shackFloor > 0)
            {
                int sz = z0 + 6 + r.Next(0, Mathf.Max(1, z1 - z0 - 24));
                Shack(g, lot.OuterX - lot.street, lot.shackFloor * Storey, sz, 12, 10, 10, -lot.street, seed + 7);
                Door(g, sz + 3, lot.shackFloor * Storey + 1, lot.OuterX, false, 5, 9);
            }
            Roof(g, plan, lot, r, true);
        }

        /// <summary>A balcony off the wall at wx (outward ±1), floor slab at y, z za..zb, 1 m deep.</summary>
        static void Balcony(VoxelGrid g, int wx, int outward, int y, int za, int zb, int seed)
        {
            int xo = wx + outward * 4;
            int lo = Mathf.Min(wx + outward, xo), hi = Mathf.Max(wx + outward, xo);
            for (int z = za + 1; z < zb; z++) for (int j = 1; j <= 8; j++) g.voxels.Remove(new Vector3Int(wx, y + j, z));
            g.Mat(Stone); g.Box(lo, y, za, hi, y, zb, Pal.Ramp(Conc, 1, seed));
            float roll = Pal.Hash(wx, y, za, seed);
            if (roll < 0.3f)
            {
                // open rail
                g.Mat(Scrap);
                VoxMat rail = Pal.Ramp(Pal.Metal, 1, seed);
                g.Box(xo, y + 3, za, xo, y + 3, zb, rail); g.Box(lo, y + 3, za, hi, y + 3, za, rail); g.Box(lo, y + 3, zb, hi, y + 3, zb, rail);
                for (int z = za; z <= zb; z += 2) g.Box(xo, y + 1, z, xo, y + 2, z, rail);
            }
            else
            {
                // parapet: painted sheet or concrete; above it glazing, a cage or open
                var sheet = roll < 0.45f ? Pal.Rust : roll < 0.55f ? Pal.PaleBlue : roll < 0.65f ? Pal.Ochre : Conc;
                g.Mat(sheet == Conc ? Stone : Scrap);
                VoxMat ps = sheet == Conc ? Pal.Ramp(Conc, 2, seed) : Corrugated(sheet, seed);
                g.Box(xo, y + 1, za, xo, y + 3, zb, ps); g.Box(lo, y + 1, za, hi, y + 3, za, ps); g.Box(lo, y + 1, zb, hi, y + 3, zb, ps);
                float up = Pal.Hash(za, y, wx, seed + 5);
                if (up < 0.4f)
                {
                    // glazed in: timber frames, a sheet roof
                    for (int z = za; z <= zb; z++) for (int j = 4; j <= 8; j++)
                    {
                        bool frame = z == za || z == zb || (z - za) % 3 == 0 || j == 8;
                        g.Mat(frame ? Wood : Glass);
                        g.Set(xo, y + j, z, frame ? Pal.Ramp(Pal.Wood, 1, seed) : Pal.Ramp(Pal.Glass, 2, seed));
                    }
                    g.Mat(Scrap); g.Box(lo, y + 9, za, hi, y + 9, zb, Pal.Ramp(Pal.Rust, 1, seed));
                }
                else if (up < 0.6f)
                {
                    // a security cage
                    g.Mat(Scrap);
                    for (int j = 4; j <= 9; j++) for (int z = za; z <= zb; z++) if ((z & 1) == 0 || j == 9) g.Set(xo, y + j, z, Pal.Ramp(Pal.Metal, 0, seed));
                    g.Box(lo, y + 9, za, hi, y + 9, zb, Pal.Ramp(Pal.Metal, 0, seed));
                }
            }
            if (Pal.Hash(zb, y, wx, seed + 9) < 0.3f)
            {
                // an AC box hung outside
                g.Mat(Scrap);
                int ax = xo + outward;
                g.Box(Mathf.Min(ax, ax + outward), y + 4, za + 1, Mathf.Max(ax, ax + outward), y + 5, za + 2, p => p.y == y + 5 && (p.z & 1) == 0 ? Pal.Metal[1] : Pal.Cream[2]);
            }
        }

        /// <summary>Old downtown: brick or stone, cornices each storey, a cap, a neon sign down the street corner, shacks
        /// hung off the outer wall, washing on the roof.</summary>
        static void BrickBlock(VoxelGrid g, Plan plan, Lot lot)
        {
            var r = new System.Random(lot.seed);
            int x0 = lot.x0, x1 = lot.x1, z0 = lot.z0, z1 = lot.z1, F = lot.floors, seed = lot.seed & 0xffff;
            bool stone = r.NextDouble() < 0.3;
            VoxMat wall = stone ? Pal.Ramp(Limestone, 2, seed) : Pal.Stripe(Pal.Ramp(Brick, 2, seed), Pal.Ramp(Brick, 0, seed + 1), 1, 4);
            Block(g, x0, z0, x1, z1, F, Stone, wall, Pal.Ramp(Conc, 1, seed), seed, lot.street, lot.doorZ);
            g.Mat(Stone);
            for (int f = 1; f <= F; f++) foreach (int x in new[] { x0 - 1, x1 + 1 }) g.Box(x, f * Storey, z0, x, f * Storey, z1, Pal.Ramp(Limestone, 2, seed));
            g.Box(x0 - 1, F * Storey + 3, z0 - 1, x1 + 1, F * Storey + 3, z1 + 1, Pal.Ramp(Limestone, 3, seed + 1));
            g.Mat(Scrap);
            int sx = lot.StreetX + lot.street * 2, sz = r.NextDouble() < 0.5 ? z1 - 2 : z0 + 1;
            var c = r.NextDouble() < 0.5 ? Pal.Crimson : Pal.Navy;
            g.Box(Mathf.Min(sx, sx + lot.street), 14, sz, Mathf.Max(sx, sx + lot.street), Mathf.Min(F * Storey - 2, 44), sz + 1, p => (p.y % 6) < 1 ? LampGlow : Pal.Pick(c, p, seed, 2));
            if (lot.shackFloor > 0)
            {
                int shacks = r.Next(1, 4);
                for (int k = 0; k < shacks; k++)
                {
                    int f = 1 + r.Next(0, Mathf.Max(1, F - 1)), w = 10 + r.Next(0, 5);
                    int z = z0 + 4 + (k * (z1 - z0 - 8)) / shacks;
                    if (z + w > z1 - 3) continue;
                    Shack(g, lot.OuterX - lot.street, f * Storey, z, w, 8 + r.Next(0, 5), 9 + r.Next(0, 2), -lot.street, seed + 11 + k);
                    Door(g, z + 2, f * Storey + 1, lot.OuterX, false, 5, 9);
                }
            }
            Roof(g, plan, lot, r, false);
            if (r.NextDouble() < 0.5)
            {
                g.Mat(Cloth);
                int y = F * Storey + 8, wx = (x0 + x1) / 2;
                for (int z = z0 + 6; z < z0 + 30 && z < z1; z++) if ((z & 3) != 0) g.Set(wx, y - (z & 1), z, Pal.Ramp(z % 9 < 3 ? Pal.PaleBlue : z % 9 < 6 ? Pal.Cream : Pal.Crimson, 2));
            }
        }

        /// <summary>A works hall: corrugated walls, wide windows, a sawtooth roof glazed on its steep faces, pipes along the
        /// outer wall, a storage tank; its stacks come from the plan.</summary>
        static void WorksHall(VoxelGrid g, Lot lot)
        {
            var r = new System.Random(lot.seed);
            int x0 = lot.x0, x1 = lot.x1, z0 = lot.z0, z1 = lot.z1, F = lot.floors, seed = lot.seed & 0xffff;
            var ramp = r.NextDouble() < 0.5 ? Pal.Rust : r.NextDouble() < 0.5 ? Pal.Metal : Pal.Olive;
            Block(g, x0, z0, x1, z1, F, Scrap, Corrugated(ramp, seed), Pal.Ramp(Conc, 0, seed), seed, lot.street, lot.doorZ, 16, 6);
            int top = F * Storey;
            g.Mat(Scrap);
            for (int z = z0; z + 8 <= z1; z += 8)
            {
                for (int k = 0; k < 8; k++) g.Box(x0, top + 3 + k * 5 / 8, z + k, x1, top + 3 + k * 5 / 8, z + k, Corrugated(Pal.Rust, seed + z));
                g.Mat(Glass); g.Box(x0 + 1, top + 3, z + 7, x1 - 1, top + 7, z + 7, p => (p.x % 5) == 0 ? Pal.Metal[1] : Pal.Pick(Pal.Glass, p, seed, 2)); g.Mat(Scrap);
            }
            // pipes along the outer wall with drops to the deck
            int px = lot.OuterX - lot.street * 2;
            foreach (int y in new[] { 6, 10 }) g.Tube(new Vector3(px, y, z0 + 2), new Vector3(px, y, z1 - 2), y == 6 ? 1.4f : 0.9f, Pal.Ramp(y == 6 ? Pal.Rust : Pal.Metal, 1, seed + y));
            for (int z = z0 + 6; z < z1 - 4; z += 20) g.Tube(new Vector3(px, 10, z), new Vector3(px, 1, z), 0.8f, Pal.Ramp(Pal.Metal, 1, seed + z));
            if (r.NextDouble() < 0.6 && z1 - z0 > 48)
            {
                int cx = (x0 + x1) / 2 + lot.street * 6, cz = (z0 + z1) / 2;
                g.Box(cx - 5, top + 1, cz - 5, cx + 5, top + 8, cz + 5, Pal.Ramp(Pal.Metal, 1, seed));
                g.CylY(cx, cz, 7f, top + 9, top + 24, p => (p.y % 4) == 0 ? Pal.Rust[1] : Pal.Pick(Pal.Metal, p, seed, 1), 6f);
                g.CylY(cx, cz, 7f, top + 25, top + 25, Pal.Ramp(Pal.Rust, 1, seed));
            }
        }

        /// <summary>The market hall: ground floor open to the street on piers, striped stall awnings, a gallery above.</summary>
        static void MarketHall(VoxelGrid g, Lot lot)
        {
            int x0 = lot.x0, x1 = lot.x1, z0 = lot.z0, z1 = lot.z1, seed = lot.seed & 0xffff, sx = lot.StreetX;
            Block(g, x0, z0, x1, z1, 2, Stone, Pal.Ramp(Brick, 1, seed + 40), Pal.Ramp(Conc, 1, seed), seed + 13, lot.street, lot.doorZ);
            g.ClearBox(sx, 1, z0 + 6, sx, Storey - 2, z1 - 6);
            g.Mat(Stone);
            for (int z = z0 + 6; z <= z1 - 6; z += 12) g.Box(sx, 1, z, sx, Storey - 1, z + 1, Pal.Ramp(Brick, 2, seed + z));
            int ix = sx - lot.street * 6;
            for (int z = z0 + 32; z < z1 - 10; z += 16)
            {
                int a = Mathf.Min(ix, ix - lot.street * 8), b = Mathf.Max(ix, ix - lot.street * 8);
                g.Mat(Wood); g.Box(a, 1, z, b, 3, z + 8, Pal.Ramp(Pal.Wood, 2, seed + z));
                g.Mat(Cloth); g.Box(a - 2, 9, z - 1, b + 2, 9, z + 9, Pal.Stripe(Pal.Solid(Pal.Crimson[3]), Pal.Solid(Pal.Cream[2]), 2, 4, 2));
            }
        }

        /// <summary>A container yard at the bow: stacked boxes in faded paint, a fence along the street (the crane is its
        /// own piece).</summary>
        static void Yard(VoxelGrid g, Lot lot)
        {
            var r = new System.Random(lot.seed);
            int seed = lot.seed & 0xffff;
            var paints = new[] { Pal.Rust, Pal.PaleBlue, Pal.Crimson, Pal.Olive, Pal.Ochre };
            g.Mat(Scrap);
            for (int z = lot.z0 + 2; z + 24 <= lot.z1 - 14; z += 28)
            for (int x = lot.x0 + 4; x + 10 <= lot.x1 - 6; x += 12)
            {
                int high = r.Next(0, 4);
                for (int k = 0; k < high; k++)
                {
                    var paint = paints[r.Next(0, paints.Length)];
                    int y = 1 + k * 10, xa = x, za = z;
                    g.Box(xa, y, za, xa + 9, y + 9, za + 23, p => (p.x == xa || p.x == xa + 9 || p.y == y || p.y == y + 9) && (p.z == za || p.z == za + 23) ? Pal.Metal[1] : Pal.Weathered(paint, 0.3f, seed + k, 2, -40f)(p));
                    g.ClearBox(xa + 1, y + 1, za + 1, xa + 8, y + 8, za + 22);
                    if (k == 0 && r.NextDouble() < 0.5) g.ClearBox(xa + 1, y + 1, za + 23, xa + 8, y + 8, za + 23);       // doors open
                }
            }
            for (int z = lot.z0; z <= lot.z1; z++)
            {
                if ((z & 7) == 0) g.Box(lot.StreetX, 1, z, lot.StreetX, 7, z, Pal.Ramp(Pal.Metal, 1));
                if (z < lot.z0 + 2 || z > lot.z0 + 14) for (int y = 1; y <= 7; y += 2) g.Set(lot.StreetX, y, z, Pal.Ramp(Pal.Chrome, 0));
            }
        }

        // ------------------------------------------------------------------ roofs, stacks, works, galleries, cables
        static void Roof(VoxelGrid g, Plan plan, Lot lot, System.Random r, bool liftHouse)
        {
            int top = lot.Top, seed = lot.seed & 0xffff, cx = (lot.x0 + lot.x1) / 2;
            if (liftHouse)
            {
                g.Mat(Stone);
                g.Box(cx - 5, top + 1, lot.z0 + 4, cx + 5, top + 8, lot.z0 + 14, Pal.Ramp(Conc, 1, seed));
                g.Box(cx - 6, top + 9, lot.z0 + 3, cx + 6, top + 9, lot.z0 + 15, Pal.Ramp(Conc, 0, seed));
            }
            if (r.NextDouble() < 0.5) WaterTank(g, cx + lot.street * 10, top + 1, lot.z1 - 12, 5, seed + 4);
            foreach (var a in plan.anchors)
                if (a.y == top + 14 && a.z >= lot.z0 && a.z <= lot.z1 && a.x >= lot.x0 && a.x <= lot.x1)
                {
                    g.Mat(Scrap);
                    g.Box(a.x, top + 1, a.z, a.x, a.y, a.z, Pal.Ramp(Pal.Chrome, 1));
                    for (int y = top + 6; y <= a.y; y += 4) g.Box(a.x - 2, y, a.z, a.x + 2, y, a.z, Pal.Ramp(Pal.Chrome, 1));
                    g.Mat(Glass); g.Set(a.x, a.y + 1, a.z, Pal.Solid(Pal.Crimson[3]));
                }
            // a roof garden: tubs with a tree or two
            int trees = r.Next(0, 4);
            for (int k = 0; k < trees; k++)
            {
                int tx = lot.x0 + 5 + r.Next(0, Mathf.Max(1, lot.x1 - lot.x0 - 10)), tz = lot.z0 + 20 + r.Next(0, Mathf.Max(1, lot.z1 - lot.z0 - 26));
                Tree(g, tx, top + 1, tz, seed + k);
            }
        }

        static void Tree(VoxelGrid g, int x, int y, int z, int seed)
        {
            g.Mat(Wood);
            g.Box(x - 2, y, z - 2, x + 2, y + 1, z + 2, Pal.Ramp(Pal.Rust, 1, seed));            // tub
            int h = 4 + (int)(Pal.Hash(x, y, z, seed) * 5);
            g.Box(x, y + 2, z, x, y + 2 + h, z, Pal.Ramp(Pal.Wood, 0, seed));
            int cy = y + 3 + h;
            for (int dx = -3; dx <= 3; dx++) for (int dy = -2; dy <= 3; dy++) for (int dz = -3; dz <= 3; dz++)
            {
                if (dx * dx + dy * dy * 2 + dz * dz > 11 || Pal.Hash(x + dx, cy + dy, z + dz, seed) < 0.25f) continue;
                g.Set(x + dx, cy + dy, z + dz, Pal.Ramp(dy > 0 ? Pal.Moss : Pal.Olive, dy > 1 ? 3 : 2, seed));
            }
        }

        /// <summary>A smokestack: hollow; banded red and white near the top, brick with rust rings, or riveted steel; a
        /// gallery ring two thirds up, rungs up its side, a cap.</summary>
        static void Stack(VoxelGrid g, StackDef s)
        {
            int y1 = s.y0 + s.h, x = s.x, z = s.z, seed = s.x * 17 + s.z, y0 = s.y0, h = s.h;
            VoxMat red = Pal.Weathered(Pal.Crimson, 0.25f, seed, 2, -40f), white = Pal.Weathered(Pal.Cream, 0.3f, seed + 1, 1, -40f), steel = Pal.Weathered(Pal.Metal, 0.5f, seed, 2, -40f);
            VoxMat paint = s.style switch
            {
                0 => p => p.y > y0 + h * 0.55f && ((p.y - y0) / 7) % 2 == 0 ? red(p) : white(p),
                1 => p => (p.y % 14) < 2 ? Pal.Rust[2] : Pal.Pick(Brick, p, seed, 1),
                _ => p => (p.y % 10) == 0 ? Pal.Metal[0] : steel(p),
            };
            g.Mat(s.style == 1 ? Stone : Scrap);
            g.CylY(x, z, s.r, y0, y1, paint, s.r - 1.2f);
            g.Mat(Scrap);
            g.CylY(x, z, s.r + 0.6f, y1 + 1, y1 + 2, Pal.Ramp(Pal.Black, 1), s.r - 1.2f);
            int ring = y0 + h * 2 / 3;
            g.CylY(x, z, s.r + 2.6f, ring, ring, Pal.Ramp(Pal.Metal, 1), s.r);
            g.CylY(x, z, s.r + 2.6f, ring + 3, ring + 3, Pal.Ramp(Pal.Rust, 1), s.r + 1.8f);
            int rx = x + Mathf.CeilToInt(s.r) + 1;
            for (int y = y0; y < y1; y++) { g.Set(rx, y, z - 1, Pal.Ramp(Pal.Metal, 1)); g.Set(rx, y, z + 1, Pal.Ramp(Pal.Metal, 1)); if ((y & 1) == 0) g.Set(rx, y, z, Pal.Ramp(Pal.Metal, 2)); }
        }

        /// <summary>The engine works across the stern: two halls with intake grilles and the stacks of the plan, the
        /// city's board over the street and pipes across between them.</summary>
        static VoxelGrid SternWorks(Plan plan, int seed)
        {
            var g = new VoxelGrid();
            int z0 = -126, z1 = -106;
            foreach (var (x0, x1) in new[] { (-70, -20), (20, 70) })
            {
                Block(g, x0, z0, x1, z1, 2, Stone, Pal.Stripe(Pal.Ramp(Brick, 1, seed), Pal.Ramp(Brick, 0, seed + 1), 1, 4), Pal.Ramp(Conc, 0, seed), seed + 50 + x0, x0 < 0 ? +1 : -1, -120);
                g.Mat(Scrap);
                for (int x = x0 + 6; x < x1 - 6; x += 10) g.Box(x, 6, z1 + 1, x + 6, 16, z1 + 1, p => (p.y & 1) == 0 ? Pal.Black[1] : Pal.Metal[2]);
            }
            foreach (var s in plan.stacks) if (s.owner < 0) Stack(g, s);
            g.Mat(Stone);
            g.Box(-20, 2 * Storey, z0, 20, 2 * Storey + 6, z1, Pal.Stripe(Pal.Ramp(Brick, 1, seed), Pal.Ramp(Brick, 0, seed + 1), 1, 4));
            g.Mat(Scrap); g.Box(-16, 2 * Storey + 7, z1, 15, 2 * Storey + 12, z1, p => (p.x & 3) == 0 && p.y > 2 * Storey + 7 && p.y < 2 * Storey + 12 ? LampGlow : Pal.Navy[1]);
            foreach (int y in new[] { 2 * Storey + 14, 2 * Storey + 18 }) g.Tube(new Vector3(-66, y, -110), new Vector3(66, y, -110), 1.3f, Pal.Ramp(Pal.Rust, 1, seed + y));
            return g;
        }

        /// <summary>Bridges across the main street between facing blocks: plank deck, rails, struts to the kerbs.</summary>
        static VoxelGrid Bridges(Plan plan, int seed)
        {
            var g = new VoxelGrid();
            foreach (var b in plan.bridges)
            {
                int by = b.y, xa = b.xL + 1, xb = b.xR - 1;
                g.Mat(Wood); g.Box(xa, by, b.z0, xb, by, b.z1, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed), Pal.Ramp(Pal.Wood, 1, seed + 1), 0, 3));
                g.Mat(Scrap);
                foreach (int z in new[] { b.z0, b.z1 })
                {
                    g.Box(xa, by + 4, z, xb, by + 4, z, Pal.Ramp(Pal.Ochre, 2));
                    for (int x = xa; x <= xb; x += 4) g.Box(x, by + 1, z, x, by + 3, z, Pal.Ramp(Pal.Metal, 1));
                }
                for (int x = xa; x <= xb; x += 6) g.Box(x, by - 2, b.z0 + 1, x, by - 1, b.z1 - 1, Pal.Ramp(Pal.Rust, 0));
                g.Tube(new Vector3(xa + 1, by - 1, b.z0 + 2), new Vector3(-17, 0, b.z0 + 2), 0.8f, Pal.Ramp(Pal.Rust, 0));
                g.Tube(new Vector3(xb - 1, by - 1, b.z1 - 2), new Vector3(17, 0, b.z1 - 2), 0.8f, Pal.Ramp(Pal.Rust, 0));
            }
            return g;
        }

        /// <summary>Galleries hung off both deck sides below the plates (clear of the gangway and the bow): grating, a
        /// rail, knee braces, lamps under the deck edge, huts here and there.</summary>
        static VoxelGrid Galleries(int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            const int y = -10, depth = 12;
            foreach (int side in new[] { -1, 1 })
            {
                int edge = side > 0 ? HX : -HX - 1, outer = edge + side * (depth - 1);
                int lo = Mathf.Min(edge, outer), hi = Mathf.Max(edge, outer);
                for (int z = -HZ + 6; z < HZ - 10; z++)
                {
                    if (side > 0 && z >= -GangHalf - 6 && z <= GangHalf + 5) continue;
                    for (int x = lo; x <= hi; x++) if (((x + z) & 3) != 0 || x == outer) g.Set(x, y, z, Pal.Ramp(Pal.Rust, 1, seed));
                    if ((z & 3) == 0) g.Box(outer, y + 1, z, outer, y + 3, z, Pal.Ramp(Pal.Metal, 1));
                    g.Set(outer, y + 4, z, Pal.Ramp(Pal.Ochre, 2));
                    if ((z & 31) == 0)
                    {
                        g.Tube(new Vector3(outer, y - 1, z), new Vector3(edge, y - 9, z), 0.7f, Pal.Ramp(Pal.Rust, 0));
                        g.Box(edge, y - 9, z, edge, y - 1, z, Pal.Ramp(Pal.Rust, 0));
                    }
                    if ((z & 15) == 8) { g.Mat(Glass); g.Set(edge, -3, z, Pal.Solid(LampGlow)); g.Mat(Scrap); }
                }
                for (int z = -HZ + 12; z < HZ - 30; z += 40)
                {
                    if (side > 0 && z + 18 >= -GangHalf - 6 && z <= GangHalf + 5) continue;
                    if (Pal.Hash(side, z, 0, seed) < 0.45f) continue;
                    int len = 10 + (int)(Pal.Hash(z, side, 1, seed) * 8);
                    var sheet = Corrugated(Pal.Hash(z, side, 2, seed) < 0.5f ? Pal.Rust : Pal.Metal, seed + z);
                    int a = edge + side * 2, b = edge + side * 9, ha = Mathf.Min(a, b), hb = Mathf.Max(a, b);
                    g.Box(ha, y + 1, z, hb, y + 8, z + len, sheet);
                    g.ClearBox(ha + 1, y + 1, z + 1, hb - 1, y + 7, z + len - 1);
                    g.Box(ha - 1, y + 9, z - 1, hb + 1, y + 9, z + len + 1, Pal.Ramp(Pal.Rust, 1, seed + z));
                    g.Mat(Glass); g.Box(b, y + 4, z + 3, b, y + 6, z + 5, p => Pal.Hash(p, seed) < 0.5f ? LampGlow : Pal.Glass[2]); g.Mat(Scrap);
                }
            }
            return g;
        }

        /// <summary>Cables sagging between masts and stacks (each anchor to its one or two nearest neighbours in reach).</summary>
        static VoxelGrid Cables(Plan plan, int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            var a = plan.anchors;
            var done = new HashSet<long>();
            for (int i = 0; i < a.Count; i++)
            {
                for (int pick = 0; pick < 2; pick++)
                {
                    int best = -1; float bd = float.MaxValue;
                    for (int j = 0; j < a.Count; j++)
                    {
                        if (j == i || done.Contains(Mathf.Min(i, j) * 1000L + Mathf.Max(i, j))) continue;
                        float d = Vector3Int.Distance(a[i], a[j]);
                        if (d > 24f && d < 150f && d < bd) { bd = d; best = j; }
                    }
                    if (best < 0) break;
                    done.Add(Mathf.Min(i, best) * 1000L + Mathf.Max(i, best));
                    Vector3 p0 = a[i], p1 = a[best];
                    float sag = bd * 0.09f;
                    int n = Mathf.CeilToInt(bd * 1.6f);
                    for (int k = 0; k <= n; k++)
                    {
                        float t = k / (float)n;
                        g.Set(Vector3Int.RoundToInt(Vector3.Lerp(p0, p1, t) + Vector3.down * (4f * sag * t * (1f - t))), Pal.Ramp(Pal.Black, 1, seed));
                    }
                }
            }
            return g;
        }
    }
}
