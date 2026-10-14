using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Vehicle decals (roadmap 19): small voxel emblems (4 cm voxels) for the flanks — the four raider gangs,
    /// the factions of the wastes, and plain hot-rod art. A gang's emblem fools that gang's lookouts from a distance
    /// (<see cref="Gang"/>, read by Convoy).</summary>
    public static class Decals
    {
        public static readonly string[] Names = { "NONE", "CHROME JACKALS", "RUSTMEN", "BONE CONVOY", "ASH RIDERS", "FUEL GUILD", "LAST ENGINE", "SALT NOMADS", "BUNKER REMNANTS", "RED CROSS", "FLAMES", "CHECKERS", "SHARK TEETH" };

        /// <summary>Index into NpcLore.Gangs a decal claims, or -1.</summary>
        public static int Gang(int decal) => decal >= 1 && decal <= 4 ? decal - 1 : -1;

        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();

        public static Mesh Mesh(int id)
        {
            if (meshes.TryGetValue(id, out var m) && m) return m;                 // meshes die with play mode
            var g = Grid(id);
            if (g == null || g.Count == 0) return null;
            if (MadMax.Rendering.HDBits.On) return meshes[id] = MadMax.Rendering.HDBits.Decal(g, id);   // HD: painted, not studs
            g.Bevel(0.12f, 0.12f);
            m = VoxelMesher.Build(g, "Decal_" + id, 0.04f);
            meshes[id] = m;
            return m;
        }

        static VoxelGrid Grid(int id)
        {
            var g = new VoxelGrid();
            void Px(int x, int y, Color32 c) => g.Set(x, y, 0, Pal.Solid(c));
            void Disc(float r, Color32 c) { for (int x = -7; x <= 7; x++) for (int y = -7; y <= 7; y++) if (x * x + y * y <= r * r) Px(x, y, c); }
            void Bar(int x0, int y0, int x1, int y1, Color32 c) { int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)); for (int i = 0; i <= n; i++) Px(x0 + (x1 - x0) * i / Mathf.Max(1, n), y0 + (y1 - y0) * i / Mathf.Max(1, n), c); }
            switch (id)
            {
                case 1:                                                                                 // chrome jackal head
                    Disc(6.5f, Pal.Black[1]);
                    for (int y = -1; y <= 2; y++) for (int x = -3; x <= 3; x++) Px(x, y, Pal.Chrome[2]);
                    for (int y = -4; y <= -2; y++) for (int x = -1; x <= 1; x++) Px(x, y, Pal.Chrome[3]);
                    foreach (int s in new[] { -1, 1 }) { Px(s * 3, 3, Pal.Chrome[2]); Px(s * 3, 4, Pal.Chrome[1]); Px(s * 4, 5, Pal.Chrome[1]); Px(s * 2, 3, Pal.Chrome[2]); Px(s * 2, 1, Pal.Amber); }
                    Px(0, -4, Pal.Black[0]);
                    break;
                case 2:                                                                                 // crossed wrenches
                    Disc(6.5f, Pal.Black[2]);
                    Bar(-4, -4, 4, 4, Pal.Rust[3]); Bar(-4, 4, 4, -4, Pal.Rust[4]);
                    foreach (var (x, y) in new[] { (-5, -5), (5, 5), (-5, 5), (5, -5) }) { Px(x, y, Pal.Rust[2]); Px(x + (x > 0 ? -1 : 1), y, Pal.Rust[2]); Px(x, y + (y > 0 ? -1 : 1), Pal.Rust[2]); }
                    break;
                case 3:                                                                                 // skull and crossbones
                    Bar(-6, -6, 6, 6, Pal.Cream[2]); Bar(-6, 6, 6, -6, Pal.Cream[2]);
                    for (int x = -4; x <= 4; x++) for (int y = -1; y <= 5; y++) if (x * x + (y - 2) * (y - 2) <= 16) Px(x, y, Pal.Cream[4]);
                    for (int x = -2; x <= 2; x++) { Px(x, -2, Pal.Cream[3]); if (x % 2 == 0) Px(x, -3, Pal.Cream[3]); }
                    Px(-2, 2, Pal.Black[0]); Px(2, 2, Pal.Black[0]); Px(-2, 1, Pal.Black[0]); Px(2, 1, Pal.Black[0]); Px(0, 0, Pal.Black[1]);
                    break;
                case 4:                                                                                 // ash flame
                    for (int y = -6; y <= 6; y++)
                    {
                        int w = Mathf.RoundToInt(4f - (y + 6) * 0.3f + Mathf.Sin(y * 1.3f) * 0.8f);
                        for (int x = -w; x <= w; x++) Px(x + (y > 2 ? 1 : 0), y, y < -2 ? Pal.Ochre[4] : y < 2 ? (Mathf.Abs(x) < w - 1 ? Pal.Ochre[3] : Pal.Crimson[4]) : Pal.Black[3]);
                    }
                    break;
                case 5:                                                                                 // fuel drop
                    Disc(6.5f, Pal.Black[1]);
                    for (int x = -3; x <= 3; x++) for (int y = -5; y <= 5; y++)
                        if (x * x + (y + 2) * (y + 2) <= 10 || (y >= -1 && Mathf.Abs(x) <= (5 - y) / 2)) Px(x, y, Pal.Ochre[3]);
                    Px(-1, -2, Pal.Cream[4]); Px(-1, -1, Pal.Ochre[4]);
                    break;
                case 6:                                                                                 // cog of the Last Engine
                    for (int x = -7; x <= 7; x++) for (int y = -7; y <= 7; y++)
                    {
                        float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                        bool tooth = r <= 6.6f && r > 4.5f && Mathf.Repeat(a * 8f / (2f * Mathf.PI) + 0.25f, 1f) < 0.5f;
                        if ((r > 2.2f && r <= 4.6f) || tooth) Px(x, y, r > 4.4f ? Pal.Bronze[2] : Pal.Bronze[3]);
                    }
                    for (int i = -1; i <= 1; i++) { Px(i, 0, Pal.Cream[4]); Px(0, i, Pal.Cream[4]); }
                    break;
                case 7:                                                                                 // salt crystal
                    Disc(6.5f, Pal.PaleBlue[1]);
                    for (int x = -4; x <= 4; x++) for (int y = -6; y <= 6; y++) if (Mathf.Abs(x) * 1.5f + Mathf.Abs(y) <= 6f) Px(x, y, x < 0 ? Pal.Cream[4] : Pal.Cream[2]);
                    break;
                case 8:                                                                                 // bunker hatch
                    Disc(6.5f, Pal.Black[1]); Disc(5.2f, Pal.Moss[2]);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.094f + 1.57f;
                        Bar(0, 0, Mathf.RoundToInt(Mathf.Cos(a) * 4.5f), Mathf.RoundToInt(Mathf.Sin(a) * 4.5f), Pal.Ochre[3]);
                    }
                    Px(0, 0, Pal.Black[0]);
                    break;
                case 9:                                                                                 // red cross
                    for (int x = -5; x <= 5; x++) for (int y = -5; y <= 5; y++) Px(x, y, (Mathf.Abs(x) <= 1 && Mathf.Abs(y) <= 4) || (Mathf.Abs(y) <= 1 && Mathf.Abs(x) <= 4) ? Pal.Crimson[3] : Pal.Cream[4]);
                    break;
                case 10:                                                                                // hot-rod flames
                    for (int x = -12; x <= 12; x++)
                    {
                        float top = 2.5f + Mathf.Sin(x * 0.9f) * 1.8f + (x + 12) * 0.08f;
                        for (int y = -3; y <= Mathf.RoundToInt(top); y++) Px(x, y, y > top - 1.5f ? Pal.Crimson[4] : y > 0 ? Pal.Ochre[4] : Pal.Ochre[3]);
                    }
                    break;
                case 11:                                                                                // checker strip
                    for (int x = -12; x <= 11; x++) for (int y = -2; y <= 1; y++) Px(x, y, ((x + 12) / 2 + (y + 2) / 2) % 2 == 0 ? Pal.Black[0] : Pal.Cream[4]);
                    break;
                case 12:                                                                                // shark mouth
                    for (int x = -9; x <= 9; x++) for (int y = -4; y <= 4; y++)
                    {
                        if (Mathf.Abs(y) > 4 - Mathf.Abs(x) / 3) continue;
                        bool tooth = Mathf.Abs(y) >= 1 && (Mathf.Abs(x) % 3) < 3 - (Mathf.Abs(y) - 1);
                        Px(x, y, tooth ? Pal.Cream[4] : Mathf.Abs(y) == 0 ? Pal.Black[0] : Pal.Crimson[3]);
                    }
                    break;
                default: return null;
            }
            return g;
        }
    }
}
