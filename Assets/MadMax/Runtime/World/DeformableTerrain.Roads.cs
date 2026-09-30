using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Roads (depth stage C) on the paving layer. Pave byte per cell: 1 asphalt and 2 concrete (laid wet, they
    /// cure), 3 gravel and 4 cobbles (firm at once), 5 / 6 asphalt with white / yellow road paint, 7 / 8 concrete with
    /// white / yellow paint, 9 a pothole. Paint goes only on set asphalt or concrete (and the old paved highways); heavy
    /// wheels slowly break set asphalt into potholes, which the rake patches with asphalt. Paved cells are counted per
    /// 4 m map cell so the player's roads show on the minimap and the world map (rebuilt from the saved chunk edits).</summary>
    public partial class DeformableTerrain
    {
        public const byte PaveAsphalt = 1, PaveConcrete = 2, PaveGravel = 3, PaveCobbles = 4, PaveAsphaltWhite = 5, PaveAsphaltYellow = 6,
                          PaveConcreteWhite = 7, PaveConcreteYellow = 8, PavePothole = 9;
        /// <summary>Deepest pothole (m); each cell sinks 60..100 % of it, and a patch lifts it back by the same amount.</summary>
        public const float PotholeDepth = 0.07f;
        /// <summary>Map cell (m) of the player-road overlay: one minimap pixel at the default zoom.</summary>
        public const float MarkCell = 4f;

        static readonly Color32[] GravelRoad = { C(0x55504a), C(0x645d55), C(0x736a60), C(0x857a6d) };
        static readonly Color32[] Cobble = { C(0x4e4a46), C(0x5c5752), C(0x68625b), C(0x78716a) };
        static readonly Color32 GravelChip = C(0x9c9284), CobbleMortar = C(0x2e2b28), WhitePaint = C(0xd4d0c2);

        /// <summary>Asphalt or concrete under the paint (0 for gravel, cobbles, potholes and bare ground).</summary>
        public static byte PaveBase(byte kind) => kind == 1 || kind == 5 || kind == 6 ? PaveAsphalt : kind == 2 || kind == 7 || kind == 8 ? PaveConcrete : (byte)0;
        /// <summary>Road paint on a cell: 0 none, 1 white, 2 yellow.</summary>
        public static byte PaintOf(byte kind) => kind == 5 || kind == 7 ? (byte)1 : kind == 6 || kind == 8 ? (byte)2 : (byte)0;

        /// <summary>Paving at a point (0 none; the generated highways are not paving, see <see cref="HardRoadAt"/>).</summary>
        public byte PaveAt(float x, float z) { var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k); return ch.pave[k]; }
        /// <summary>How far the paving at a point has set (0 wet .. 1).</summary>
        public float CureAt(float x, float z) { var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k); return ch.cure[k]; }

        /// <summary>A hard road already: asphalt, concrete, cobbles or paint, or a generated paved highway.</summary>
        public bool HardRoadAt(float x, float z)
        {
            var ch = Locate(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), out int k);
            byte p = ch.pave[k];
            return p == PaveAsphalt || p == PaveConcrete || (p >= PaveCobbles && p <= PaveConcreteYellow) || (p == 0 && ch.paved[k] && ch.road[k] > 0.5f);
        }

        /// <summary>What a Pave op may lay over what is there: asphalt / concrete on bare ground, a gravel base or a
        /// pothole (the patch); gravel and cobbles on bare ground (not on a paved highway), cobbles on gravel, and
        /// either one into a pothole.</summary>
        static bool CanPaveOver(byte cur, byte kind, bool highway)
        {
            if (kind == PaveAsphalt || kind == PaveConcrete) return cur == 0 || cur == PaveGravel || cur == PavePothole;
            if (kind == PaveGravel) return (cur == 0 && !highway) || cur == PavePothole;
            if (kind == PaveCobbles) return (cur == 0 && !highway) || cur == PaveGravel || cur == PavePothole;
            return false;
        }

        /// <summary>Surface of gravel, cobbles, painted paving and potholes. Gravel grips like a track (no mud, a little
        /// give: heavy wheels leave shallow ruts; dust behind the wheels when dry, see <see cref="RoadTraffic"/>); cobbles
        /// are firm; paint is slick in the rain; a pothole jolts and holds water.</summary>
        static Surface RoadSurface(byte kind, float w)
        {
            switch (kind)
            {
                case PaveGravel: return new Surface { wet = w * 0.6f, road = 0.4f, softness = 0.08f + w * 0.06f, mud = 0f, ice = Weather.Ice * 0.5f };
                case PaveCobbles: return new Surface { wet = w * 0.35f, road = 0.9f, ice = Weather.Ice * 0.8f };
                case PavePothole: return new Surface { wet = Mathf.Min(1f, w * 0.8f), road = 0.6f, softness = 0.06f, mud = 0.15f + w * 0.35f, rut = 0.04f, ice = Weather.Ice * 0.6f };
                default: return new Surface { wet = w * 0.45f, road = 1f, ice = Weather.Ice * 0.85f };
            }
        }

        /// <summary>Colours of gravel (grey-brown chippings), cobbles (a sett per cell, alternating by cell parity, dark
        /// mortar), painted lines (worn flecks show the surface) and potholes (dark crumbs, water after rain).</summary>
        Color32 RoadColor(Chunk ch, int k, float hs, float gx, float gz, int gi, int gj)
        {
            byte kind = ch.pave[k];
            float wet = Weather.Wetness;
            Color32 col;
            switch (kind)
            {
                case PaveGravel:
                {
                    float patch = Mathf.PerlinNoise(gx * 0.45f + 3f, gz * 0.45f + 17f);
                    col = GravelRoad[hs < 0.16f ? 0 : hs > 0.88f ? 3 : patch > 0.52f ? 2 : 1];
                    if (Hash(gi + 3, gj + 11) > 0.94f) col = GravelChip;
                    if (wet > 0.1f) col = Color32.Lerp(col, Crack, Mathf.Round(wet * 3f) / 3f * 0.35f);
                    break;
                }
                case PaveCobbles:
                    col = hs < 0.08f ? CobbleMortar : Cobble[((gi + gj) & 1) * 2 + (hs > 0.55f ? 1 : 0)];
                    if (wet > 0.3f) col = Color32.Lerp(col, Crack, 0.2f);
                    break;
                case PavePothole:
                    col = hs < 0.45f ? Crack : Asphalt[0];
                    if (wet > 0.25f && hs > 0.2f) col = hs > 0.9f ? WaterHi : Color32.Lerp(Water, Crack, 0.4f);
                    break;
                default:
                {
                    bool asphalt = PaveBase(kind) == PaveAsphalt;
                    col = asphalt ? Asphalt[hs < 0.2f ? 0 : hs < 0.85f ? 1 : 2] : Concrete[hs < 0.3f ? 1 : hs < 0.9f ? 2 : 3];
                    if (hs < 0.93f) col = PaintOf(kind) == 1 ? WhitePaint : Line;
                    break;
                }
            }
            if (kind != PavePothole && Puddle(gx, gz)) col = hs > 0.85f ? WaterHi : Color32.Lerp(Water, col, 0.3f);
            float snow = Weather.SnowAt(gz);
            if (snow > 0.3f) col = Color32.Lerp(col, SnowCol, Mathf.Round(snow * 2f) / 3f);
            return col;
        }

        /// <summary>Paint op on one cell: <paramref name="colour"/> 1 white, 2 yellow, 0 strips the paint.</summary>
        float PaintCell(Chunk ch, int k, int ix, int iz, byte colour)
        {
            byte cur = ch.pave[k], bas = PaveBase(cur);
            if (cur == 0 && ch.paved[k] && ch.road[k] > 0.5f) bas = PaveAsphalt;              // the old highways take paint too
            if (bas == 0 || (cur != 0 && ch.cure[k] < 1f) || colour > 2) return 0f;          // gravel, cobbles, bare ground, wet paving
            if (colour == 0 && cur == 0) return 0f;
            byte next = colour == 0 ? bas : (byte)(bas == PaveAsphalt ? 4 + colour : 6 + colour);
            if (next == cur) return 0f;
            SetPave(ix, iz, next, 1f, ch.compact[k]);
            return Cell * Cell;
        }

        /// <summary>Pothole op on one cell: <paramref name="patch"/> 0 breaks set asphalt into a hole, 1 fills a hole
        /// with fresh asphalt (it cures like any paving) and lifts it back level.</summary>
        float PotholeCell(Chunk ch, int k, int ix, int iz, float cur, byte patch)
        {
            byte kind = ch.pave[k];
            float depth = PotholeDepth * (0.6f + 0.4f * Hash(ix + 5, iz + 9));
            if (patch == 0)
            {
                if (PaveBase(kind) != PaveAsphalt || ch.cure[k] < 1f) return 0f;
                SetPave(ix, iz, PavePothole, 1f, 0);
                SetD(ix, iz, cur - depth);
                return Cell * Cell;
            }
            if (kind != PavePothole) return 0f;
            SetPave(ix, iz, PaveAsphalt, 0f, 0);
            SetD(ix, iz, cur + depth);
            return Cell * Cell;
        }

        /// <summary>Test fixture: paving within the radius sets at once (skips the curing wait). Not replicated.</summary>
        public void CureNow(Vector3 p, float radius)
        {
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell), x1 = Mathf.CeilToInt((p.x + radius) / Cell);
            int z0 = Mathf.FloorToInt((p.z - radius) / Cell), z1 = Mathf.CeilToInt((p.z + radius) / Cell);
            for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                float dx = ix * Cell - p.x, dz = iz * Cell - p.z;
                if (dx * dx + dz * dz > radius * radius) continue;
                var ch = Locate(ix, iz, out int k);
                if (ch.pave[k] != 0 && ch.cure[k] < 1f) SetPave(ix, iz, ch.pave[k], 1f, ch.compact[k]);
            }
        }

        // ---- traffic on the paving: dust off dry gravel, and heavy wheels wear set asphalt into potholes, a pass at a time
        struct Wear { public float passes, last; }
        readonly Dictionary<long, Wear> wear = new Dictionary<long, Wear>();
        /// <summary>Wheel load (N) that counts as one full heavy pass; lighter wheels (cars) never wear the road.</summary>
        public float wearLoad = 20000f, wearMinLoad = 8000f;
        static readonly Color GravelDust = new Color(0.62f, 0.58f, 0.52f, 0.4f);

        /// <summary>A rolling wheel on the ground (from <see cref="Deform"/>; replica stamps with dt 0.1 don't count).</summary>
        void RoadTraffic(Vector3 contact, float load, float dt)
        {
            if (dt > 0.05f) return;
            int ix = Mathf.RoundToInt(contact.x / Cell), iz = Mathf.RoundToInt(contact.z / Cell);
            var ch = Locate(ix, iz, out int k);
            byte kind = ch.pave[k];
            if (kind == PaveGravel)
            {
                float dry = 1f - Mathf.Clamp01(Weather.Wetness * 4f);
                if (dry > 0f && !Weather.Raining && Weather.SnowAt(contact.z) < 0.3f && Random.value < dt * 1.2f * dry)
                    Fx.Smoke(contact + Vector3.up * 0.18f, Vector3.up * 0.45f + Fx.Wind * 0.15f, 0.6f, GravelDust, 1.5f);
                return;
            }
            if (load < wearMinLoad || PaveBase(kind) != PaveAsphalt || ch.cure[k] < 1f) return;
            long key = ((long)ix << 32) ^ (uint)iz;
            wear.TryGetValue(key, out var w);
            float now = Time.time;
            if (now - w.last > 0.4f) w.passes += load / wearLoad;                             // a new pass (a wheel resting in place is one)
            w.last = now;
            if (w.passes >= 120f + Hash(ix + 17, iz - 5) * 240f)
            {
                wear.Remove(key);
                var p = new Vector3(ix * Cell, ch.h[k] + ch.d[k], iz * Cell);
                float r = 0.3f + Hash(ix, iz + 3) * 0.25f;
                ApplyTerraform((byte)TerraOp.Pothole, p, r, 0f, 0);
                MadMax.Net.NetSession.Instance?.SendTerraform((byte)TerraOp.Pothole, p, r, 0f, 0);
                return;
            }
            if (wear.Count > 50000) wear.Clear();
            wear[key] = w;
        }

        // ---- the player's roads on the map
        struct RoadMark { public int loose, hard; }
        readonly Dictionary<Vector2Int, RoadMark> roadMarks = new Dictionary<Vector2Int, RoadMark>();

        static Vector2Int MarkOf(int ix, int iz) => new Vector2Int(FloorDiv(ix, 16), FloorDiv(iz, 16));   // 16 cells = 4 m

        void NoteRoad(int ix, int iz, byte old, byte kind)
        {
            var key = MarkOf(ix, iz);
            roadMarks.TryGetValue(key, out var m);
            if (old == PaveGravel) m.loose--; else if (old != 0) m.hard--;
            if (kind == PaveGravel) m.loose++; else if (kind != 0) m.hard++;
            if (m.loose <= 0 && m.hard <= 0) roadMarks.Remove(key); else roadMarks[key] = m;
        }

        /// <summary>Recount the paved cells of saved chunk edits (the chunks stream in later without SetPave).</summary>
        void RoadMarksFrom(List<ChunkEdit> edits)
        {
            roadMarks.Clear();
            if (edits == null) return;
            foreach (var e in edits)
            {
                if (e.rutOnly || string.IsNullOrEmpty(e.pave)) continue;
                var pv = System.Convert.FromBase64String(e.pave);
                for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int k = j * V + i;
                    if (k < pv.Length && pv[k] != 0) NoteRoad(e.x * N + i, e.z * N + j, 0, pv[k]);
                }
            }
        }

        /// <summary>The player's paving covers this 4 m map cell (6+ cells of it); gravel when mostly gravel.</summary>
        public bool PlayerRoadAt(float x, float z, out bool gravel)
        {
            gravel = false;
            if (roadMarks.Count == 0 || !roadMarks.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / MarkCell), Mathf.FloorToInt(z / MarkCell)), out var m)) return false;
            if (m.loose + m.hard < 6) return false;
            gravel = m.loose > m.hard;
            return true;
        }

        /// <summary>Map cells holding any of the player's paving.</summary>
        public int PlayerRoadCount => roadMarks.Count;

        /// <summary>Centres of the map cells with the player's roads (world map page).</summary>
        public IEnumerable<(Vector3 pos, bool gravel)> PlayerRoads()
        {
            foreach (var kv in roadMarks)
                if (kv.Value.loose + kv.Value.hard >= 6)
                    yield return (new Vector3((kv.Key.x + 0.5f) * MarkCell, 0f, (kv.Key.y + 0.5f) * MarkCell), kv.Value.loose > kv.Value.hard);
        }
    }
}
