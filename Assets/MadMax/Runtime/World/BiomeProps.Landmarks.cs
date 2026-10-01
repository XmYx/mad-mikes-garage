using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Landmarks between the towns (gaps list: variety for driving): radio masts outside towns and cities (the
    /// radio's signal fades with the distance to the nearest one), Remnant checkpoints across the highways (honk or
    /// [E] at the hut to pay the toll; ram the boom and they remember), scrapyards beside some towns (fenced lots of
    /// crushed cars, a parts locker and wrecks to strip), and one refinery on the richest oil field (tank farm, column,
    /// a diesel pump and a store of fuel). All deterministic from the world; props are ordinary destructible voxels
    /// keyed "m{landmark},{piece}".</summary>
    public static partial class BiomeProps
    {
        public enum MarkKind { Mast, Checkpoint, Scrapyard, Refinery }
        public struct Mark { public MarkKind kind; public Vector2 pos; public float yaw; public int index; public string Name => kind == MarkKind.Mast ? "RADIO MAST" : kind == MarkKind.Checkpoint ? "CHECKPOINT" : kind == MarkKind.Scrapyard ? "SCRAPYARD" : "REFINERY"; }
        struct Piece { public string id; public Vector2 pos; public float yaw; public string table; }

        static readonly object markLock = new object();
        static WorldGen markWorld;
        static List<Mark> marks;
        static List<List<Piece>> markPieces;

        static readonly Color32[] Tank = { Pal.Hex("5c5a50"), Pal.Hex("767262"), Pal.Hex("908a76"), Pal.Hex("aaa28a") };

        /// <summary>All landmarks of this world.</summary>
        public static List<Mark> Landmarks(WorldGen world)
        {
            lock (markLock)
            {
                if (markWorld == world && marks != null) return marks;
                markWorld = world;
                marks = new List<Mark>(); markPieces = new List<List<Piece>>();
                Plan(world);
                return marks;
            }
        }

        /// <summary>0..1 radio reception at a point: full within 300 m of a mast, fading to a quarter 1.8 km out.</summary>
        public static float Signal(WorldGen world, Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var m in Landmarks(world)) if (m.kind == MarkKind.Mast) best = Mathf.Min(best, Vector2.Distance(m.pos, new Vector2(p.x, p.z)));
            return best == float.MaxValue ? 1f : Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((best - 300f) / 1500f));
        }

        /// <summary>Scrapyard lots (centre, yaw): the wreck spawner parks a few wrecks in each.</summary>
        public static void Scrapyards(WorldGen world, List<(Vector3 pos, float yaw)> into)
        {
            into.Clear();
            foreach (var m in Landmarks(world)) if (m.kind == MarkKind.Scrapyard) into.Add((new Vector3(m.pos.x, 0f, m.pos.y), m.yaw));
        }

        static bool Open(WorldGen w, Vector2 p, float road)
        {
            var s = w.Sample(p.x, p.y);
            return s.roadDist > road && float.IsNaN(s.water) && s.feature == 0 && w.SettlementAt(p.x, p.y) == null && w.YardWeight(p.x, p.y) <= 0f
                   && w.SiteAt(p.x, p.y) == null && !w.RiverAt(p.x, p.y, out _, out _, out _);
        }

        static void Plan(WorldGen w)
        {
            var r = new System.Random(w.seed * 131 + 17);
            void AddMark(MarkKind k, Vector2 p, float yaw, List<Piece> pieces) { marks.Add(new Mark { kind = k, pos = p, yaw = yaw, index = marks.Count }); markPieces.Add(pieces); }
            // radio masts outside every town and city
            foreach (var st in w.settlements)
            {
                if (st.kind == Biome.Village) continue;
                float a0 = (float)r.NextDouble() * 360f;
                for (int k = 0; k < 8; k++)
                {
                    float a = (a0 + k * 45f) * Mathf.Deg2Rad;
                    var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (st.radius + 38f);
                    if (!Open(w, p, 14f)) continue;
                    AddMark(MarkKind.Mast, p, 0f, new List<Piece> { new Piece { id = "Mast0", pos = p, yaw = r.Next(4) * 90f } });
                    break;
                }
            }
            // checkpoints across the middle of the long highways, away from towns
            foreach (var road in w.roads.roads)
            {
                if (!road.paved || road.points.Count < 12) continue;
                int k = road.points.Count / 2;
                var c3 = road.points[k]; var n3 = road.points[k + 1];
                var c = new Vector2(c3.x, c3.z);
                bool nearTown = false;
                foreach (var st in w.settlements) if ((st.pos - c).magnitude < st.radius + 180f) nearTown = true;
                if (nearTown || w.YardWeight(c.x, c.y) > 0f || w.RiverAt(c.x, c.y, out _, out _, out _)) continue;
                var along = new Vector2(n3.x - c3.x, n3.z - c3.z).normalized;
                var side = new Vector2(along.y, -along.x);
                float yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
                float half = road.width * 0.5f;
                var pieces = new List<Piece>
                {
                    new Piece { id = "GuardHut0", pos = c + side * (half + 4.5f), yaw = yaw + 90f, table = "hut" },
                    new Piece { id = "Sandbags0", pos = c + side * (half + 1.5f) + along * 3.5f, yaw = yaw + 90f },
                    new Piece { id = "Sandbags0", pos = c - side * (half + 1.5f) + along * 3.5f, yaw = yaw + 90f },
                    new Piece { id = "Sandbags0", pos = c - side * (half + 1.5f) - along * 3.5f, yaw = yaw + 90f },
                };
                AddMark(MarkKind.Checkpoint, c, yaw, pieces);
            }
            // scrapyards beside every third town or city
            foreach (var st in w.settlements)
            {
                if (st.kind == Biome.Village || st.index % 3 != 1) continue;
                float a0 = (float)r.NextDouble() * 360f;
                for (int k = 0; k < 8; k++)
                {
                    float a = (a0 + k * 45f) * Mathf.Deg2Rad;
                    var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (st.radius + 62f);
                    bool clear = true;
                    for (int q = 0; q < 9 && clear; q++) { var o = q == 8 ? Vector2.zero : new Vector2(Mathf.Cos(q * 0.785f), Mathf.Sin(q * 0.785f)) * 20f; clear = Open(w, p + o, 10f); }
                    if (!clear) continue;
                    float yaw = r.Next(4) * 90f;
                    var q0 = Quaternion.Euler(0f, yaw, 0f);
                    var pieces = new List<Piece>();
                    Vector2 L(float x, float z) { var v = q0 * new Vector3(x, 0f, z); return p + new Vector2(v.x, v.z); }
                    for (float x = -16f; x <= 16f; x += 4f) { pieces.Add(new Piece { id = "Fence0", pos = L(x, -14f), yaw = yaw }); if (Mathf.Abs(x) > 5f) pieces.Add(new Piece { id = "Fence0", pos = L(x, 14f), yaw = yaw }); }   // a gap for the gate
                    for (float z = -12f; z <= 12f; z += 4f) { pieces.Add(new Piece { id = "Fence0", pos = L(-18f, z), yaw = yaw + 90f }); pieces.Add(new Piece { id = "Fence0", pos = L(18f, z), yaw = yaw + 90f }); }
                    pieces.Add(new Piece { id = "CarStack0", pos = L(-12f, -9f), yaw = yaw + 5f });
                    pieces.Add(new Piece { id = "CarStack1", pos = L(-12f, -2f), yaw = yaw - 8f });
                    pieces.Add(new Piece { id = "CarStack0", pos = L(12f, -9f), yaw = yaw + 90f });
                    pieces.Add(new Piece { id = "CarStack1", pos = L(12f, 8f), yaw = yaw + 84f });
                    pieces.Add(new Piece { id = "Loot", pos = L(0f, -11f), yaw = yaw, table = "garage" });
                    AddMark(MarkKind.Scrapyard, p, yaw, pieces);
                    break;
                }
            }
            // one refinery on the richest oil near a road
            Vector2 best = default; float bestOil = 0.35f;
            for (float x = -WorldGen.HalfX + WorldGen.Meridian + 300f; x < WorldGen.HalfX - WorldGen.Meridian - 300f; x += 96f)
            for (float z = WorldGen.ZOfLatitude(-62f); z < WorldGen.ZOfLatitude(62f); z += 96f)
            {
                float oil = w.OilAt(x, z);
                if (oil <= bestOil || !w.Habitable(x, z, 0.6f)) continue;
                if (oil <= bestOil) continue;
                float rd = RoadDistance(w, x, z);
                if (rd < 25f || rd > 200f || !Open(w, new Vector2(x, z), 25f)) continue;
                bool nearTown = false;
                foreach (var st in w.settlements) if ((st.pos - new Vector2(x, z)).magnitude < st.radius + 150f) nearTown = true;
                if (nearTown) continue;
                bestOil = oil; best = new Vector2(x, z);
            }
            if (bestOil > 0.35f)
            {
                float yaw = r.Next(4) * 90f;
                var q0 = Quaternion.Euler(0f, yaw, 0f);
                Vector2 L(float x, float z) { var v = q0 * new Vector3(x, 0f, z); return best + new Vector2(v.x, v.z); }
                var pieces = new List<Piece>
                {
                    new Piece { id = "Column0", pos = L(0f, 0f), yaw = yaw },
                    new Piece { id = "Tank0", pos = L(-9f, 6f), yaw = yaw },
                    new Piece { id = "Tank0", pos = L(-9f, -6f), yaw = yaw + 90f },
                    new Piece { id = "Tank0", pos = L(9f, 7f), yaw = yaw + 180f },
                    new Piece { id = "GasPump0", pos = L(8f, -8f), yaw = yaw + 180f, table = "pump" },
                    new Piece { id = "Loot", pos = L(4f, 3f), yaw = yaw, table = "refinery" },
                };
                AddMark(MarkKind.Refinery, best, yaw, pieces);
            }
        }

        /// <summary>Flat distance to the nearest road point (coarse: planning only).</summary>
        static float RoadDistance(WorldGen w, float x, float z)
        {
            float best = float.MaxValue;
            foreach (var road in w.roads.roads) foreach (var p in road.points) { float dx = p.x - x, dz = p.z - z; best = Mathf.Min(best, dx * dx + dz * dz); }
            return Mathf.Sqrt(best);
        }

        static void PopulateLandmarks(DeformableTerrain terrain, Vector2Int c, Transform parent, Material mat, Lookup legacy)
        {
            var world = terrain.World;
            var list = Landmarks(world);
            for (int m = 0; m < list.Count; m++)
            {
                var pieces = markPieces[m];
                for (int j = 0; j < pieces.Count; j++)
                {
                    var pc = pieces[j];
                    if (DeformableTerrain.ChunkOf(new Vector3(pc.pos.x, 0f, pc.pos.y)) != c) continue;
                    string key = "m" + m + "," + j;
                    float y = terrain.Height(pc.pos.x, pc.pos.y) - 0.1f;
                    if (pc.id == "Loot") { SpawnLandmarkLoot(pc, key, new Vector3(pc.pos.x, y + 0.1f, pc.pos.y), parent, mat); continue; }
                    var d = SpawnById(terrain, pc.id, pc.id.TrimEnd('0', '1'), parent, mat, new Vector3(pc.pos.x, y, pc.pos.y), pc.yaw, false, terrain.DestructionState, key, legacy);
                    if (!d) continue;
                    if (pc.table == "pump")
                    {
                        var pump = d.gameObject.AddComponent<GasPump>();
                        pump.key = "R" + m; pump.stock = 900f; pump.capacity = 900f;
                        pump.nozzle = new Vector3(0.5f, 0.8f, 0.15f);
                        pump.SetKind(ResourceType.Diesel);
                    }
                    if (pc.table == "hut") { var cp = d.gameObject.AddComponent<Checkpoint>(); cp.Setup(list[m].pos, list[m].yaw, mat); }
                    if (pc.id == "Mast0") d.gameObject.AddComponent<MastLight>();
                }
            }
        }

        static void SpawnLandmarkLoot(Piece pc, string key, Vector3 pos, Transform parent, Material mat)
        {
            var def = MadMax.Building.FurnitureLibrary.Get("locker");
            if (def == null) return;
            var lg = new GameObject("LootSpot", typeof(MeshFilter), typeof(MeshRenderer));
            lg.transform.SetParent(parent, true);
            lg.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, pc.yaw, 0));
            lg.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            lg.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var box = lg.AddComponent<BoxCollider>(); box.center = def.mesh.bounds.center; box.size = def.mesh.bounds.size;
            MadMax.Building.FurnitureLibrary.DressHD(lg, def.id, HDProp.FlagsOf(mat));
            var loot = lg.AddComponent<Lootable>();
            loot.key = "M" + key; loot.table = pc.table;
            loot.locked = Lootable.RollLocked(loot.key, "locker");
            loot.title = pc.table == "refinery" ? "FUEL STORE" : "PARTS LOCKER";
        }

        // ------------------------------------------------------------------ templates

        static void AddLandmarkTemplates(System.Action<Template> add)
        {
            add(T("Mast0", RadioMast(), 0.16f));
            add(T("GuardHut0", GuardHut(), 0.12f));
            add(T("Sandbags0", Sandbags(), 0.1f));
            for (int i = 0; i < 2; i++) add(T("CarStack" + i, CarStack(i), 0.12f));
            add(T("Tank0", StorageTank(), 0.2f));
            add(T("Column0", Column(), 0.2f));
        }

        /// <summary>A lattice radio mast, ~22 m: four legs leaning in, cross bracing, dishes, a red lamp on top.</summary>
        static VoxelGrid RadioMast()
        {
            var g = new VoxelGrid().Mat(Scrap);
            const int H = 136;
            var steel = Pal.Weathered(Pal.Metal, 0.45f, 2101, 2, 0);
            for (int y = 0; y <= H; y++)
            {
                int w = Mathf.RoundToInt(Mathf.Lerp(7f, 1f, y / (float)H));
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) g.Set(sx * w, y, sz * w, steel);
                if (y % 12 == 0)                                                                      // bracing rings and diagonals
                {
                    g.Box(-w, y, -w, w, y, -w, steel); g.Box(-w, y, w, w, y, w, steel);
                    g.Box(-w, y, -w, -w, y, w, steel); g.Box(w, y, -w, w, y, w, steel);
                }
                if (y % 12 < 11 && w > 1) { int d = Mathf.RoundToInt(Mathf.Lerp(-w, w, (y % 12) / 11f)); g.Set(d, y, -w, steel); g.Set(-d, y, w, steel); g.Set(-w, y, d, steel); g.Set(w, y, -d, steel); }
            }
            g.Box(0, H, 0, 0, H + 14, 0, Pal.Ramp(Pal.Chrome, 1));                                  // whip antenna
            g.Box(-1, H + 15, -1, 1, H + 16, 1, Pal.Solid(Pal.Crimson[4]));                          // the lamp
            foreach (int y in new[] { 80, 104 })                                                      // dishes
            {
                g.CylZ(0, y, 3.2f, -9, -8, Pal.Ramp(Pal.Cream, 2, 2102));
                g.Box(0, y, -7, 0, y, -4, steel);
            }
            g.Mat(Stone); g.Box(-9, -2, -9, 9, 0, 9, Pal.Ramp(Conc, 1, 2103));                      // footing
            g.Mat(Wood); g.Box(9, 1, -3, 13, 9, 3, Pal.Ramp(Pal.Wood, 1, 2104)); g.Box(9, 10, -4, 14, 10, 4, Pal.Ramp(Pal.Rust, 2, 2105));   // equipment shed
            return g;
        }

        /// <summary>A Remnant guard hut: plank walls, a slit window, a tin roof, a red-and-white sign.</summary>
        static VoxelGrid GuardHut()
        {
            var g = new VoxelGrid().Mat(Wood);
            var plank = Pal.Stripe(Pal.Ramp(Pal.Wood, 1, 2111), Pal.Ramp(Pal.Wood, 2, 2112), 1, 3);
            g.Box(-10, 0, -10, 10, 20, 10, plank);
            g.ClearBox(-9, 1, -9, 9, 19, 9);
            g.Box(-9, 0, -9, 9, 0, 9, Pal.Ramp(Pal.Wood, 0, 2113));                                   // floor
            g.ClearBox(-4, 1, 10, 3, 16, 10);                                                         // doorway (front)
            g.Mat(Glass); g.Box(-8, 12, -10, 8, 14, -10, Pal.Ramp(Pal.Glass, 2, 2114));             // slit window (road side)
            g.Mat(Scrap); g.Box(-12, 21, -12, 12, 22, 12, Pal.Stripe(Pal.Ramp(Pal.Rust, 2, 2115), Pal.Ramp(Pal.Metal, 2, 2116), 0, 4));   // tin roof
            g.Box(-6, 23, 0, 6, 28, 0, Pal.Stripe(Pal.Solid(Pal.Crimson[3]), Pal.Solid(Pal.Cream[3]), 0, 3));   // sign
            g.Box(-5, 22, 0, -5, 23, 0, Pal.Ramp(Pal.Metal, 1)); g.Box(5, 22, 0, 5, 23, 0, Pal.Ramp(Pal.Metal, 1));
            return g;
        }

        /// <summary>A sandbag wall, 2.4 m long and chest high.</summary>
        static VoxelGrid Sandbags()
        {
            var g = new VoxelGrid().Mat(Cloth);
            for (int row = 0; row < 5; row++)
                for (int b = -3; b <= 3; b++)
                {
                    int x0 = b * 4 - 2 + (row % 2) * 2;
                    if (x0 < -12 || x0 + 3 > 12) continue;
                    g.Box(x0, row * 3, -2, x0 + 3, row * 3 + 2, 2, Pal.Ramp(Pal.Sand, 1 + (row + b + 10) % 3, 2120 + row * 7 + b));
                }
            return g;
        }

        /// <summary>Crushed cars, three high: flattened slabs in faded paint with a wheel showing.</summary>
        static VoxelGrid CarStack(int v)
        {
            var g = new VoxelGrid().Mat(Scrap);
            Color32[][] paints = { Pal.Rust, Pal.Navy, Pal.Crimson, Pal.Olive, Pal.Moss, Pal.Cream };
            for (int i = 0; i < 3; i++)
            {
                var paint = paints[(v * 2 + i * 3) % paints.Length];
                int y0 = i * 6, off = (i + v) % 2 == 0 ? 1 : -2;
                g.Box(-18 + off, y0, -7, 18 + off, y0 + 4, 7, Pal.Weathered(paint, 0.55f, 2130 + v * 11 + i, 2, 0));
                g.Box(-10 + off, y0 + 4, -5, 6 + off, y0 + 5, 5, Pal.Weathered(paint, 0.6f, 2140 + v * 11 + i, 1, 0));   // the crushed roof
                g.Mat(Glass); g.Box(-9 + off, y0 + 3, -7, 5 + off, y0 + 3, -7, Pal.Ramp(Pal.Glass, 1)); g.Mat(Scrap);
                g.CylX(y0 + 2, 7, 2.2f, 12 + off, 13 + off, Pal.Ramp(Pal.Tire, 1));                  // a wheel still on
            }
            return g;
        }

        /// <summary>A refinery storage tank: 5 m across, 4 m tall, a ladder and a stencil band.</summary>
        static VoxelGrid StorageTank()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 12.5f, 0, 18, Pal.Weathered(Tank, 0.35f, 2150, 2, 0));
            g.Remove(p => p.y >= 1 && p.y <= 17 && p.x * p.x + p.z * p.z < 11.5f * 11.5f);           // hollow
            g.CylY(0, 0, 12.5f, 19, 19, Pal.Ramp(Tank, 1, 2151));
            g.CylY(0, 0, 12.6f, 9, 10, Pal.Solid(Pal.Ochre[3]));                                     // hazard band
            for (int y = 0; y <= 19; y += 2) g.Box(-1, y, -13, 1, y, -13, Pal.Ramp(Pal.Metal, 1));   // ladder rungs
            g.Box(-2, 0, -13, -2, 20, -13, Pal.Ramp(Pal.Metal, 1)); g.Box(2, 0, -13, 2, 20, -13, Pal.Ramp(Pal.Metal, 1));
            return g;
        }

        /// <summary>The distillation column: 16 m, platforms every few metres, a flare stack and pipes.</summary>
        static VoxelGrid Column()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 4f, 0, 80, Pal.Weathered(Pal.Chrome, 0.5f, 2160, 1, 0));
            for (int y = 16; y <= 72; y += 18)
            {
                g.CylY(0, 0, 7f, y, y, Pal.Stripe(Pal.Ramp(Pal.Metal, 2), Pal.Ramp(Pal.Rust, 2), 0, 2));   // platform grating
            }
            g.Box(6, 0, -1, 7, 60, 1, Pal.Ramp(Pal.Rust, 2, 2161));                                    // riser pipe
            g.Box(-14, 0, 3, -13, 90, 4, Pal.Ramp(Pal.Metal, 2, 2162));                                // flare stack
            g.Box(-15, 91, 2, -12, 92, 5, Pal.Solid(Pal.Black[2]));
            g.Mat(Stone); g.Box(-8, -2, -8, 8, 0, 8, Pal.Ramp(Conc, 1, 2163));
            return g;
        }
    }
}
