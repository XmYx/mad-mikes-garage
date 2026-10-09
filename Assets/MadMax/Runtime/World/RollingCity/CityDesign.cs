using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The rolling city's voxel design (roadmap 28), pure and thread-safe: a 36 × 64 m deck on four tracked
    /// bogies carrying a block of old downtown — a brick hotel, a concrete office tower, a stone bank, a market hall —
    /// patched and grown over with the wasteland's additions: shacks hung off the facades, a scaffold, a bridge over the
    /// main street, a water tank, a windmill, an engine house with two stacks, a crane at the bow. Buildings have real
    /// storeys (3 m), a flight of stairs every floor, doorways onto the street and roof access. Deck-local voxels of
    /// 0.25 m: +X right, +Y up, +Z forward; the deck surface is y = 0 (the pieces sit half a voxel up).</summary>
    public static partial class CityDesign
    {
        public const float V = 0.25f;
        /// <summary>Deck half extents in voxels (18 × 32 m) and the storey height (3 m).</summary>
        public const int HX = 72, HZ = 128, Storey = 12;
        /// <summary>Bogie centres (metres, deck-local; y = their top below the deck surface).</summary>
        public static readonly Vector3[] Bogies = { new Vector3(-13.5f, -1.75f, 21f), new Vector3(13.5f, -1.75f, 21f), new Vector3(-13.5f, -1.75f, -21f), new Vector3(13.5f, -1.75f, -21f) };
        /// <summary>The gangway: hinge on the right deck edge, 6 m wide along z, 7 m long — it rests a metre onto the
        /// pier, which stands <see cref="PierGap"/> out from the deck edge (the deck sits up to 0.7 m off the bed's
        /// centre line on the gentle curves at the docks).</summary>
        public const int GangHalf = 12, GangLen = 28;
        public const float PierGap = 6f;

        const byte Scrap = (byte)ResourceType.Scrap, Wood = (byte)ResourceType.Wood, Stone = (byte)ResourceType.Stone, Glass = (byte)ResourceType.Glass, Cloth = (byte)ResourceType.Cloth;
        static readonly Color32[] Brick = { Pal.Hex("5a2a1c"), Pal.Hex("6e3422"), Pal.Hex("823e28"), Pal.Hex("964a30") };
        static readonly Color32[] Conc = { Pal.Hex("4a4846"), Pal.Hex("5c5a56"), Pal.Hex("6e6b66"), Pal.Hex("807c76") };
        static readonly Color32[] Limestone = { Pal.Hex("7a7262"), Pal.Hex("8e8674"), Pal.Hex("a29a86"), Pal.Hex("b6ae98") };
        static readonly Color32 Hazard = Pal.Hex("d4b020"), LampGlow = Pal.Hex("ffd15a");

        public enum Collide { None, Mesh, Box }

        public class Piece
        {
            public string name;
            public VoxelGrid grid;
            public Collide collide = Collide.Mesh;
        }

        /// <summary>The city's fixed pieces: the deck, every lot of the seeded layout (<see cref="Layout"/>), the stern
        /// works, the galleries hung off the deck sides and the cables strung between roofs. Bogies, treads and the
        /// gangway are built separately (<see cref="Bogie"/>, <see cref="Tread"/>, <see cref="Gangway"/>).</summary>
        public static List<Piece> City(int seed)
        {
            var plan = Layout(seed);
            var list = new List<Piece> { new Piece { name = "Deck", grid = Deck(seed), collide = Collide.Box } };
            for (int i = 0; i < plan.lots.Count; i++) list.Add(new Piece { name = "Lot" + i + "_" + plan.lots[i].kind, grid = LotGrid(plan, plan.lots[i]) });
            list.Add(new Piece { name = "Works", grid = SternWorks(plan, seed) });
            list.Add(new Piece { name = "Bridges", grid = Bridges(plan, seed) });
            list.Add(new Piece { name = "Galleries", grid = Galleries(seed) });
            list.Add(new Piece { name = "Cables", grid = Cables(plan, seed), collide = Collide.None });
            if (plan.crane) list.Add(new Piece { name = "Crane", grid = Crane(seed), collide = Collide.None });
            foreach (var p in list) p.grid.Bevel();
            return list;
        }

        // ------------------------------------------------------------------ helpers
        static VoxMat Plate(int seed) => p => (p.x & 15) == 0 || (p.z & 15) == 0 ? Pal.Metal[0] : Pal.Pick(Pal.Metal, p, seed, 2);
        static VoxMat Corrugated(Color32[] ramp, int seed) => p => ((p.x + p.z) & 1) == 0 ? Pal.Pick(ramp, p, seed, 2) : Pal.Pick(ramp, p, seed + 1, 1);

        static void Slab(VoxelGrid g, int x0, int z0, int x1, int z1, int y, byte mat, VoxMat paint) { g.Mat(mat); g.Box(x0, y, z0, x1, y, z1, paint); }

        /// <summary>Four walls one voxel thick from y0 to y1 with window openings every storey (glass in some, boarded
        /// in others, the rest open).</summary>
        static void Walls(VoxelGrid g, int x0, int z0, int x1, int z1, int y0, int y1, byte mat, VoxMat paint, int seed, int pitch = 12, int winW = 4, int sill = 3, int winH = 6)
        {
            g.Mat(mat);
            g.Box(x0, y0, z0, x1, y1, z0, paint); g.Box(x0, y0, z1, x1, y1, z1, paint);
            g.Box(x0, y0, z0, x0, y1, z1, paint); g.Box(x1, y0, z0, x1, y1, z1, paint);
            for (int y = y0 - 1; y + sill + winH <= y1 + 1; y += Storey)
            {
                for (int x = x0 + 4; x + winW <= x1 - 3; x += pitch) { Window(g, x, y + sill, z0, winW, winH, true, seed); Window(g, x, y + sill, z1, winW, winH, true, seed + 1); }
                for (int z = z0 + 4; z + winW <= z1 - 3; z += pitch) { Window(g, x0, y + sill, z, winW, winH, false, seed + 2); Window(g, x1, y + sill, z, winW, winH, false, seed + 3); }
            }
        }

        static void Window(VoxelGrid g, int a, int y, int wall, int w, int h, bool alongX, int seed)
        {
            float roll = Pal.Hash(a, y, wall, seed);
            for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
            {
                var p = alongX ? new Vector3Int(a + i, y + j, wall) : new Vector3Int(wall, y + j, a + i);
                g.voxels.Remove(p);
                if (roll < 0.45f) { g.Mat(Glass); g.Set(p, Pal.Ramp(Pal.Glass, 2, seed)); }
                else if (roll < 0.6f && j < h / 2) { g.Mat(Wood); g.Set(p, Pal.Ramp(Pal.Wood, 1, seed)); }      // boarded up halfway
            }
        }

        static void Door(VoxelGrid g, int a, int y, int wall, bool alongX, int w = 5, int h = 9)
        {
            for (int i = 0; i < w; i++) for (int j = 0; j < h; j++)
                g.voxels.Remove(alongX ? new Vector3Int(a + i, y + j, wall) : new Vector3Int(wall, y + j, a + i));
        }

        /// <summary>A straight flight from the slab at floorY up one storey (one voxel up per two along, 27°), along +z
        /// from z0, the slab above opened where the head needs room.</summary>
        static void Flight(VoxelGrid g, int x0, int x1, int z0, int floorY, byte mat, VoxMat paint)
        {
            g.Mat(mat);
            for (int k = 0; k < Storey; k++) g.Box(x0, floorY + 1, z0 + 2 * k, x1, floorY + 1 + k, z0 + 2 * k + 1, paint);
            g.ClearBox(x0, floorY + Storey, z0 + 6, x1, floorY + Storey, z0 + 2 * Storey + 1);
        }

        /// <summary>A building of storeys: a slab per floor, walls with windows, flights alternating between two stair
        /// bays (so the openings never stack), a doorway onto the street (side -1 = the x0 wall, +1 = x1), roof parapet.</summary>
        static void Block(VoxelGrid g, int x0, int z0, int x1, int z1, int floors, byte mat, VoxMat wall, VoxMat floor, int seed, int streetSide, int doorZ, int pitch = 12, int winW = 4)
        {
            int top = floors * Storey;
            for (int f = 0; f <= floors; f++) Slab(g, x0, z0, x1, z1, f * Storey, Stone, floor);
            Walls(g, x0, z0, x1, z1, 1, top - 1, mat, wall, seed, pitch, winW);
            g.Mat(mat);
            g.Box(x0, top + 1, z0, x1, top + 2, z0, wall); g.Box(x0, top + 1, z1, x1, top + 2, z1, wall);
            g.Box(x0, top + 1, z0, x0, top + 2, z1, wall); g.Box(x1, top + 1, z0, x1, top + 2, z1, wall);
            for (int f = 0; f < floors; f++)
            {
                int fx0 = f % 2 == 0 ? x0 + 3 : x1 - 6;
                Flight(g, fx0, fx0 + 3, z0 + 3, f * Storey, Stone, Pal.Ramp(Conc, 2, seed + f));
            }
            Door(g, doorZ, 1, streetSide < 0 ? x0 : x1, false);
        }

        /// <summary>A scrap box cantilevered off a facade at (x0, y0, z0..z0+w-1), d deep outward (±1), h high: plank
        /// floor, corrugated walls, a slanting roof, a window, struts back to the wall below.</summary>
        static void Shack(VoxelGrid g, int x0, int y0, int z0, int w, int d, int h, int outward, int seed)
        {
            int x1 = x0 + outward * (d - 1);
            int lo = Mathf.Min(x0, x1), hi = Mathf.Max(x0, x1);
            g.Mat(Wood); g.Box(lo, y0, z0, hi, y0, z0 + w - 1, Pal.Ramp(Pal.Wood, 1, seed));
            g.Mat(Scrap);
            var sheet = Corrugated(seed % 2 == 0 ? Pal.Rust : Pal.Metal, seed);
            g.Box(lo, y0 + 1, z0, hi, y0 + h, z0, sheet); g.Box(lo, y0 + 1, z0 + w - 1, hi, y0 + h, z0 + w - 1, sheet);
            int outer = outward > 0 ? hi : lo;
            g.Box(outer, y0 + 1, z0, outer, y0 + h, z0 + w - 1, sheet);
            for (int i = 0; i < d; i++) { int x = x0 + outward * i; g.Box(x, y0 + h + 1 - i / 4, z0 - 1, x, y0 + h + 1 - i / 4, z0 + w, Pal.Ramp(Pal.Rust, 1, seed + 3)); }
            g.Mat(Glass); g.Box(outer, y0 + 4, z0 + w / 2 - 1, outer, y0 + 6, z0 + w / 2, Pal.Ramp(Pal.Glass, 3));
            g.Mat(Scrap);
            g.Tube(new Vector3(outer, y0 - 1, z0 + 1), new Vector3(x0 - outward, y0 - d, z0 + 1), 0.6f, Pal.Ramp(Pal.Rust, 0));
            g.Tube(new Vector3(outer, y0 - 1, z0 + w - 2), new Vector3(x0 - outward, y0 - d, z0 + w - 2), 0.6f, Pal.Ramp(Pal.Rust, 0));
        }

        /// <summary>Pipe scaffold with plank walks every storey on a facade (the wall at wallX, z0..z1), outward ±1.</summary>
        static void Scaffold(VoxelGrid g, int wallX, int z0, int z1, int floors, int outward, int seed)
        {
            g.Mat(Scrap);
            int xo = wallX + outward * 5;
            for (int z = z0; z <= z1; z += 8) g.Box(xo, 0, z, xo, floors * Storey + 2, z, Pal.Ramp(Pal.Chrome, 0, seed));
            for (int f = 1; f <= floors; f++)
            {
                int y = f * Storey;
                g.Mat(Scrap); g.Box(xo, y + 4, z0, xo, y + 4, z1, Pal.Ramp(Pal.Chrome, 0, seed));
                g.Mat(Wood); g.Box(Mathf.Min(wallX + outward, xo), y, z0, Mathf.Max(wallX + outward, xo), y, z1, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed + f), Pal.Ramp(Pal.Wood, 1, seed), 2, 4));
            }
        }

        static void WaterTank(VoxelGrid g, int cx, int y0, int cz, int r, int seed)
        {
            g.Mat(Scrap);
            foreach (var (dx, dz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) g.Box(cx + dx * (r - 1), y0, cz + dz * (r - 1), cx + dx * (r - 1), y0 + 8, cz + dz * (r - 1), Pal.Ramp(Pal.Metal, 1));
            g.Mat(Wood); g.CylY(cx, cz, r, y0 + 9, y0 + 22, p => (p.y % 5) == 0 ? Pal.Metal[1] : Pal.Pick(Pal.Wood, p, seed, 2));
            g.Mat(Scrap); for (int i = 0; i <= r; i++) g.CylY(cx, cz, r - i + 0.5f, y0 + 23 + i / 2, y0 + 23 + i / 2, Pal.Ramp(Pal.Rust, 1, seed + 1));
        }

        // ------------------------------------------------------------------ the deck
        static VoxelGrid Deck(int seed)
        {
            var g = new VoxelGrid();
            g.Mat(Scrap);
            // two plate layers; the main street paved down the middle with a dashed centre line, hazard stripes at the edges
            g.Box(-HX, -2, -HZ, HX - 1, -1, HZ - 1, p =>
            {
                if (p.y == -1 && p.x >= -16 && p.x < 16) return (p.x == -1 || p.x == 0) && (p.z & 15) < 8 ? Pal.Cream[2] : Pal.Pick(Pal.Black, p, seed, 2);
                if (p.y == -1 && (p.x < -HX + 3 || p.x > HX - 4) && ((p.z + p.x) & 7) < 4) return Hazard;
                return Plate(seed)(p);
            });
            // girders under it, cross beams, armour skirts down the sides over the tracks (riveted)
            for (int x = -HX; x < HX; x += 24) g.Box(x, -6, -HZ, x + 1, -3, HZ - 1, Pal.Ramp(Pal.Rust, 1, seed + 1));
            for (int z = -HZ; z < HZ; z += 32) g.Box(-HX, -5, z, HX - 1, -3, z + 1, Pal.Ramp(Pal.Rust, 1, seed + 2));
            foreach (int x in new[] { -HX, HX - 1 })
                g.Box(x, -11, -HZ + 4, x, -3, HZ - 5, p => (p.y == -10 || p.y == -4) && (p.z & 3) == 0 ? Pal.Chrome[1] : Pal.Weathered(Pal.Olive, 0.35f, seed + 3, 1, -40f)(p));
            // rails along the sides (an opening on the right for the gangway) and across the bow
            for (int z = -HZ; z < HZ; z++)
            foreach (int x in new[] { -HX, HX - 1 })
            {
                if (x > 0 && z >= -GangHalf - 1 && z <= GangHalf) continue;
                if ((z & 7) == 0) g.Box(x, 0, z, x, 3, z, Pal.Ramp(Pal.Metal, 1));
                g.Set(x, 4, z, Pal.Ramp(Pal.Ochre, 2)); g.Set(x, 2, z, Pal.Ramp(Pal.Metal, 2));
            }
            for (int x = -HX; x < HX; x++)
            {
                if ((x & 7) == 0) g.Box(x, 0, HZ - 1, x, 3, HZ - 1, Pal.Ramp(Pal.Metal, 1));
                g.Set(x, 4, HZ - 1, Pal.Ramp(Pal.Ochre, 2)); g.Set(x, 2, HZ - 1, Pal.Ramp(Pal.Metal, 2));
            }
            // the bow: a ram plough from the deck edge down to the ground line with teeth, two searchlights
            for (int y = -24; y <= -3; y++)
            {
                int reach = HZ + (y + 24) / 3;
                g.Box(-HX + 8, y, reach - 1, HX - 9, y, reach, p => (p.x & 7) < 2 && p.y < -18 ? Pal.Chrome[2] : Pal.Weathered(Pal.Olive, 0.5f, seed + 4, 1, -40f)(p));
            }
            foreach (int x in new[] { -60, 59 })
            {
                g.Box(x, 0, HZ - 4, x + 1, 6, HZ - 3, Pal.Ramp(Pal.Metal, 1));
                g.Mat(Glass); g.Box(x - 1, 7, HZ - 4, x + 2, 9, HZ - 2, p => p.z == HZ - 2 ? LampGlow : Pal.Metal[1]); g.Mat(Scrap);
            }
            // bollards along the kerbs of the main street (cars park against them)
            for (int z = -HZ + 40; z < HZ - 24; z += 24) foreach (int x in new[] { -18, 17 }) g.Box(x, 0, z, x, 2, z, Pal.Ramp(Pal.Ochre, 2));
            return g;
        }

        /// <summary>A tracked bogie (frame, road wheels, idlers, the pivot drum up to the deck): bogie-local voxels, top
        /// at y = 0, the ground at y = -18; 7 m wide (x), 18 m long (z). The belt is <see cref="Tread"/>.</summary>
        public static VoxelGrid Bogie(int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-11, -14, -26, 10, -4, 25, Pal.Weathered(Pal.Olive, 0.3f, seed, 1, -40f));
            foreach (int z in new[] { -22, -13, -4, 4, 13, 22 }) foreach (int x in new[] { -13, 12 })
            {
                int zc = z;
                g.CylX(-13, zc, 4.2f, x, x + 1, p => (p.y + 13) * (p.y + 13) + (p.z - zc) * (p.z - zc) < 4 ? Pal.Chrome[1] : Pal.Pick(Pal.Metal, p, seed, 1));
            }
            foreach (int z in new[] { -12, 12 }) g.CylX(-2, z, 2.2f, -12, 11, Pal.Ramp(Pal.Metal, 1, seed + 3));              // return rollers
            // sprocket and idler: hub, spokes, rim
            foreach (int z in new[] { -27, 26 })
            {
                int zc = z;
                g.CylX(-9, zc, 7f, -12, 11, p =>
                {
                    float dy = p.y + 9, dz = p.z - zc, rr = Mathf.Sqrt(dy * dy + dz * dz);
                    if (rr < 2.2f) return Pal.Chrome[1];
                    if (rr > 5.6f) return ((p.y + p.z) & 3) == 0 ? Pal.Rust[2] : Pal.Pick(Pal.Metal, p, seed + 1, 2);
                    return Mathf.Abs(Mathf.Sin(3f * Mathf.Atan2(dy, dz))) < 0.4f ? Pal.Rust[1] : Pal.Black[0];
                });
            }
            g.CylY(0, 0, 9f, -4, 0, Pal.Ramp(Pal.Rust, 1, seed + 2));                                // pivot drum
            g.Bevel();
            return g;
        }

        /// <summary>The belt round a bogie with its grouser plates shifted by <paramref name="phase"/> voxels (0-3): the
        /// four meshes take turns as the city rolls, so the plates crawl at the speed of travel.</summary>
        public static VoxelGrid Tread(int phase)
        {
            var g = new VoxelGrid().Mat(Scrap);
            const float zEnd = 26.5f, r = 8.6f, cy = -9f;
            float straight = zEnd * 2f, arc = Mathf.PI * r;
            float len = 2f * straight + 2f * arc;
            for (float u = 0f; u < len; u += 0.35f)
            {
                Vector2 pnt, nrm;      // (z, y) and the outward normal
                if (u < straight) { pnt = new Vector2(-zEnd + u, cy + r); nrm = Vector2.up; }
                else if (u < straight + arc) { float a = (u - straight) / r; pnt = new Vector2(zEnd + Mathf.Sin(a) * r, cy + Mathf.Cos(a) * r); nrm = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); }
                else if (u < 2f * straight + arc) { pnt = new Vector2(zEnd - (u - straight - arc), cy - r); nrm = Vector2.down; }
                else { float a = (u - 2f * straight - arc) / r; pnt = new Vector2(-zEnd - Mathf.Sin(a) * r, cy - Mathf.Cos(a) * r); nrm = new Vector2(-Mathf.Sin(a), -Mathf.Cos(a)); }
                bool grouser = ((int)(u + phase) & 3) == 0;
                for (int x = -14; x <= 13; x++)
                {
                    var tire = Pal.Tire[(x & 1) + 1];
                    g.Set(new Vector3Int(x, Mathf.RoundToInt(pnt.y), Mathf.RoundToInt(pnt.x)), _ => tire);
                    if (grouser && x > -14 && x < 13)
                    {
                        var steel = Pal.Metal[(x & 3) == 0 ? 1 : 2];
                        g.Set(new Vector3Int(x, Mathf.RoundToInt(pnt.y + nrm.y), Mathf.RoundToInt(pnt.x + nrm.x)), _ => steel);
                    }
                }
            }
            g.Bevel();
            return g;
        }

        /// <summary>The gangway: hinge at the origin on the right deck edge, reaching out along +x, 6 m wide; planks, rails.</summary>
        public static VoxelGrid Gangway(int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(0, -2, -GangHalf, GangLen - 1, -2, GangHalf - 1, Pal.Ramp(Pal.Rust, 1, seed));
            g.Mat(Wood); g.Box(0, -1, -GangHalf, GangLen - 1, -1, GangHalf - 1, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed), Pal.Ramp(Pal.Wood, 1, seed + 1), 0, 3));
            g.Mat(Scrap);
            foreach (int z in new[] { -GangHalf, GangHalf - 1 })
            {
                for (int x = 0; x < GangLen; x += 6) g.Box(x, 0, z, x, 3, z, Pal.Ramp(Pal.Metal, 1));
                g.Box(0, 4, z, GangLen - 1, 4, z, Pal.Ramp(Pal.Ochre, 2));
            }
            g.Bevel();
            return g;
        }

        /// <summary>A lattice crane at the bow on the right, its jib swung in over the deck.</summary>
        static VoxelGrid Crane(int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            int cx = 60, cz = 118, top = 64;
            for (int y = 0; y < top; y++)
                foreach (var (dx, dz) in new[] { (-2, -2), (2, -2), (-2, 2), (2, 2) })
                {
                    g.Set(cx + dx, y, cz + dz, Pal.Ramp(Pal.Ochre, 2, seed));
                    if (y % 6 == 0) { g.Box(cx - 2, y, cz + dz, cx + 2, y, cz + dz, Pal.Ramp(Pal.Ochre, 1)); g.Box(cx + dx, y, cz - 2, cx + dx, y, cz + 2, Pal.Ramp(Pal.Ochre, 1)); }
                }
            g.Box(cx - 3, top, cz - 3, cx + 3, top + 3, cz + 3, Pal.Ramp(Pal.Ochre, 2, seed + 1));
            g.Mat(Glass); g.Box(cx - 3, top + 1, cz + 3, cx + 3, top + 2, cz + 3, Pal.Ramp(Pal.Glass, 3)); g.Mat(Scrap);
            for (int x = cx - 44; x < cx; x++) { g.Set(x, top + 4, cz, Pal.Ramp(Pal.Ochre, 2)); if ((x & 3) == 0) g.Box(x, top + 4, cz - 1, x, top + 6, cz + 1, Pal.Ramp(Pal.Ochre, 1)); }
            g.Box(cx + 1, top + 4, cz - 2, cx + 8, top + 7, cz + 2, Pal.Ramp(Conc, 1));
            g.Box(cx - 40, top - 20, cz, cx - 40, top + 3, cz, Pal.Ramp(Pal.Black, 1));
            g.Box(cx - 41, top - 22, cz - 1, cx - 39, top - 21, cz + 1, Pal.Ramp(Pal.Metal, 2));
            return g;
        }

        // ------------------------------------------------------------------ the docks
        public const int PierW = 32, PierHalf = 120, RampLen = 160, RampHalf = 16, DockGround = -26;

        /// <summary>A dock in dock-local voxels (+x away from the route, +z along it; y = 0 the pier top at deck height,
        /// the yard ground at y = -26): a 60 m pier on concrete piers with a rail on the far side, a 40 m ramp from the
        /// far side down to the yard for vehicles and walkers, lamps and the dock's board over the ramp's head.</summary>
        public static VoxelGrid Dock(int seed)
        {
            var g = new VoxelGrid().Mat(Scrap);
            int px1 = PierW - 1;
            g.Box(0, -2, -PierHalf, px1, -1, PierHalf - 1, Plate(seed));
            g.Mat(Stone);
            for (int z = -PierHalf; z < PierHalf; z += 24) foreach (int x in new[] { 2, px1 - 2 }) g.Box(x - 1, DockGround, z, x + 1, -3, z + 2, Pal.Ramp(Conc, 1, seed + z));
            g.Mat(Scrap);
            for (int z = -PierHalf; z < PierHalf; z++)
            {
                if (z >= -RampHalf && z < RampHalf) continue;
                if ((z & 7) == 0) g.Box(px1, 0, z, px1, 3, z, Pal.Ramp(Pal.Metal, 1));
                g.Set(px1, 4, z, Pal.Ramp(Pal.Ochre, 2));
            }
            for (int z = -PierHalf; z < PierHalf; z += 2) g.Set(0, -1, z, (z & 4) == 0 ? Pal.Solid(Hazard) : Pal.Ramp(Pal.Black, 1));
            for (int i = 0; i < RampLen; i++)
            {
                int y = -1 - (i * (-DockGround - 1)) / RampLen;
                g.Mat(Scrap); g.Box(px1 + 1 + i, y - 1, -RampHalf, px1 + 1 + i, y, RampHalf - 1, (i & 3) == 0 ? Pal.Ramp(Pal.Metal, 0) : Pal.Ramp(Pal.Metal, 2, seed + i));
                if ((i & 15) == 0) foreach (int z in new[] { -RampHalf, RampHalf - 1 }) g.Box(px1 + 1 + i, y + 1, z, px1 + 1 + i, y + 4, z, Pal.Ramp(Pal.Metal, 1));
                if (y - 2 > DockGround && (i & 31) == 8) { g.Mat(Stone); foreach (int z in new[] { -RampHalf + 2, RampHalf - 3 }) g.Box(px1 + i, DockGround, z, px1 + i + 1, y - 2, z + 1, Pal.Ramp(Conc, 1)); }
            }
            g.Mat(Scrap);
            foreach (int z in new[] { -100, -40, 40, 100 })
            {
                g.Box(px1 - 3, 0, z, px1 - 3, 16, z, Pal.Ramp(Pal.Metal, 2));
                g.Mat(Glass); g.Box(px1 - 5, 16, z - 1, px1 - 3, 17, z + 1, Pal.Solid(LampGlow)); g.Mat(Scrap);
            }
            g.Box(px1 + 2, 0, -RampHalf - 6, px1 + 2, 22, -RampHalf - 5, Pal.Ramp(Pal.Metal, 1)); g.Box(px1 + 2, 0, RampHalf + 4, px1 + 2, 22, RampHalf + 5, Pal.Ramp(Pal.Metal, 1));
            g.Box(px1 + 2, 16, -RampHalf - 5, px1 + 2, 22, RampHalf + 4, p => (p.z & 3) == 0 && p.y > 16 && p.y < 22 ? Pal.Cream[3] : Pal.Navy[1]);
            g.Bevel();
            return g;
        }
    }
}
