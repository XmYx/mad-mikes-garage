using System.Collections.Generic;
using MadMax.Designs;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Deterministic per-chunk set dressing. Every prop is a DestructibleVoxels built from a shared template:
    /// dead trees (wood), rocks (stone), scrap shacks (metal sheets, wood frame, glass) with furniture, loot crates.</summary>
    public static class PropLibrary
    {
        class Template
        {
            public string id;
            public VoxelGrid grid;
            public Mesh mesh;
            public float size = VoxelMesher.DefaultSize;
            public VoxelMesher.MeshData data;              // made on any thread; the mesh comes from it on the main thread
        }

        static Template[] trees, rocks, shacks;
        static Template crate, table;

        const byte Scrap = (byte)ResourceType.Scrap, Wood = (byte)ResourceType.Wood, Stone = (byte)ResourceType.Stone, Glass = (byte)ResourceType.Glass;

        static Template T(VoxelGrid g, string name, float size = VoxelMesher.DefaultSize)
        {
            g.PruneUnsupported();
            g.Bevel();
            return new Template { id = name, grid = g, data = VoxelMesher.BuildData(g, size), size = size };
        }

        /// <summary>Pristine grid of a template id (for rebuilding saved destruction).</summary>
        public static VoxelGrid TemplateGrid(string id)
        {
            Ensure();
            foreach (var t in trees) if (t.id == id) return t.grid;
            foreach (var t in rocks) if (t.id == id) return t.grid;
            foreach (var t in shacks) if (t.id == id) return t.grid;
            if (crate.id == id) return crate.grid;
            if (table.id == id) return table.grid;
            return BiomeProps.TemplateGrid(id) ?? SiteBuilder.TemplateGrid(id) ?? MadMax.Npc.NpcDirector.StallTemplate(id);
        }

        /// <summary>Voxel size of a template id (salvaged designs mesh it at the same scale).</summary>
        public static float TemplateSize(string id)
        {
            Ensure();
            var l = Legacy(id);
            if (l != null) return l.Value.size;
            float s = BiomeProps.TemplateSize(id);
            return s > 0f ? s : VoxelMesher.DefaultSize;
        }

        static void Ensure()
        {
            // Meshes are destroyed when play mode ends but statics survive (no domain reload): rebuild if stale.
            if (Alive()) return;
            Template[] made;
            var p = prewarm; prewarm = null;
            try { made = p != null ? p.Result : Make(); }                                      // waits when the worker is still at it
            catch (System.Exception e) { Debug.LogException(e); made = Make(); }
            PropRemesher.Prebake(Finish(made));
        }

        static bool Alive() => trees != null && trees[0].mesh && rocks[0].mesh && shacks[0].mesh && crate.mesh && table.mesh;

        static System.Threading.Tasks.Task<Template[]> prewarm;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetPrewarm() => prewarm = null;

        /// <summary>Start the templates' grids and mesh data on a worker (load screen); <see cref="Ensure"/> takes them.</summary>
        public static void Prewarm() { if (prewarm == null && !Alive()) prewarm = System.Threading.Tasks.Task.Run(Make); }
        public static bool PrewarmDone => prewarm == null || prewarm.IsCompleted;
        /// <summary>Load step: meshes for the prewarmed templates (and their colliders cooked) now.</summary>
        public static void EnsureTemplates() => Ensure();

        /// <summary>Every template, in the order of the fields: 4 trees, 4 rocks, 3 shacks, crate, table (pure: any thread).</summary>
        static Template[] Make()
        {
            var l = new List<Template>();
            for (int i = 0; i < 4; i++) { var g = new VoxelGrid().Mat(Wood); Scenery.DeadTree(g, i * 7 + 3, 14 + i * 4); l.Add(T(g, "Tree" + i)); }
            for (int i = 0; i < 4; i++) l.Add(T(Rock(i, 3 + i), "Rock" + i, 0.12f));
            for (int i = 0; i < 3; i++) l.Add(T(Shack(i), "Shack" + i));
            l.Add(T(Crate(), "Crate"));
            l.Add(T(Table(), "Table"));
            return l.ToArray();
        }

        /// <summary>Meshes for made templates and the fields set; returns the meshes whose colliders are cooked.</summary>
        static List<Mesh> Finish(Template[] made)
        {
            foreach (var t in made) { t.mesh = VoxelMesher.ToMesh(t.data, t.id); t.data = null; }
            trees = new[] { made[0], made[1], made[2], made[3] };
            rocks = new[] { made[4], made[5], made[6], made[7] };
            shacks = new[] { made[8], made[9], made[10] };
            crate = made[11]; table = made[12];
            var meshes = new List<Mesh>();
            foreach (var set in new[] { trees, rocks, shacks }) foreach (var t in set) meshes.Add(t.mesh);
            return meshes;
        }

        static VoxelGrid Rock(int seed, int r)
        {
            var g = new VoxelGrid().Mat(Stone);
            for (int x = -r - 1; x <= r + 1; x++)
            for (int y = -1; y <= r + 1; y++)
            for (int z = -r - 1; z <= r + 1; z++)
            {
                var p = new Vector3Int(x, y, z);
                float n = Pal.Noise((Vector3)p * 0.45f, seed) * 1.6f;
                float d = new Vector3(x, y * 1.4f, z).magnitude;
                if (d < r + n - 0.4f) g.Set(p, Pal.Ramp(Pal.Rust, y > r * 0.6f ? 3 : 2, seed));
            }
            return g;
        }

        static VoxelGrid Shack(int seed)
        {
            var g = new VoxelGrid();
            int w = 16 + seed * 3, l = 18 + seed * 2, h = 34;        // 2.7 m walls, 2.1 m door
            var sheet = Pal.Stripe(Pal.Weathered(Pal.Metal, 0.55f, seed + 40, 2, 0), Pal.Ramp(Pal.Rust, 2, seed), seed % 2 == 0 ? 0 : 2, 3);
            var wood = Pal.Ramp(Pal.Wood, 2, seed + 60);
            for (int x = -w; x <= w; x++)
            for (int z = -l; z <= l; z++)
            for (int y = 0; y <= h; y++)
            {
                bool edge = Mathf.Abs(x) == w || Mathf.Abs(z) == l;
                if (!edge) continue;
                bool door = z == -l && Mathf.Abs(x) <= 5 && y <= 26;
                bool window = Mathf.Abs(x) == w && Mathf.Abs(z) <= 4 && y >= 14 && y <= 22;
                bool post = (Mathf.Abs(x) == w && Mathf.Abs(z) == l) || (z == -l && Mathf.Abs(x) == 6 && y <= 27) || (z == -l && y == 27 && Mathf.Abs(x) <= 6);
                bool frame = Mathf.Abs(x) == w && Mathf.Abs(z) <= 5 && (y == 13 || y == 23 || Mathf.Abs(z) == 5) && y >= 13 && y <= 23;
                if (door) continue;
                if (window) { g.Mat(Glass); g.Set(x, y, z, Pal.Ramp(Pal.Glass, 2, seed)); continue; }
                if (post || frame || y == 0) { g.Mat(Wood); g.Set(x, y, z, wood); continue; }
                g.Mat(Scrap); g.Set(x, y, z, sheet);
            }
            g.Mat(Scrap);
            for (int x = -w - 2; x <= w + 2; x++)
            for (int z = -l - 2; z <= l + 2; z++)
            {
                int y = h + 1 + (x + w + 2) / 6;          // slanted tin roof
                g.Set(x, y, z, Pal.Stripe(Pal.Weathered(Pal.Rust, 0.3f, seed + 50, 2, 0), Pal.Solid(Pal.Rust[0]), 2, 4));
            }
            g.Mat(Wood);                                   // roof beams tie the roof to the posts
            foreach (int x in new[] { -w, w })
            foreach (int z in new[] { -l, l })
                for (int y = h; y <= h + 1 + (x + w + 2) / 6; y++) g.Set(x, y, z, wood);
            return g;
        }

        static VoxelGrid Crate()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-3, 0, -3, 3, 6, 3, Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 90), Pal.Ramp(Pal.Wood, 1, 91), 1, 2));
            g.Mat(Scrap);
            foreach (int x in new[] { -3, 3 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 6, z, Pal.Ramp(Pal.Metal, 2, 92));
            g.Box(-1, 3, -3, 1, 3, -3, Pal.Solid(Pal.Amber));
            return g;
        }

        static VoxelGrid Table()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-7, 9, -4, 7, 9, 4, Pal.Ramp(Pal.Wood, 3, 95));
            foreach (int x in new[] { -6, 6 }) foreach (int z in new[] { -3, 3 }) g.Box(x, 0, z, x, 8, z, Pal.Ramp(Pal.Wood, 1, 96));
            g.Mat(Glass); g.Box(2, 10, 0, 2, 11, 0, Pal.Ramp(Pal.Glass, 3));     // bottle
            g.Mat(Scrap); g.Box(-4, 10, -2, -2, 10, 0, Pal.Ramp(Pal.Metal, 2));   // tin plate
            return g;
        }

        static (VoxelGrid grid, Mesh mesh, float size)? Legacy(string id)
        {
            foreach (var t in trees) if (t.id == id) return (t.grid, t.mesh, t.size);
            foreach (var t in rocks) if (t.id == id) return (t.grid, t.mesh, t.size);
            foreach (var t in shacks) if (t.id == id) return (t.grid, t.mesh, t.size);
            if (crate.id == id) return (crate.grid, crate.mesh, crate.size);
            if (table.id == id) return (table.grid, table.mesh, table.size);
            return null;
        }

        public static void Populate(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat)
        {
            if (!mat) return;
            Ensure();
            var world = terrain.World;
            var rnd = new System.Random(c.x * 73856093 ^ c.y * 19349663 ^ world.seed);

            BiomeProps.Populate(terrain, c, parent, mat, rnd, Legacy);
        }

        /// <summary><see cref="Populate"/> a piece at a time (streamed chunks: <see cref="DeformableTerrain"/> steps it
        /// within its frame budget).</summary>
        public static IEnumerator<int> PopulateSteps(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat)
        {
            if (!mat) yield break;
            Ensure();
            var rnd = new System.Random(c.x * 73856093 ^ c.y * 19349663 ^ terrain.World.seed);
            foreach (var s in BiomeProps.PopulateSteps(terrain, c, parent, mat, rnd, Legacy)) yield return s;
        }
    }
}
