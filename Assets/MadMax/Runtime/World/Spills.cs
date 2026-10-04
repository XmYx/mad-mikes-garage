using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Liquids on the ground. A sparse grid of 0.5 m cells holds what was poured, spilled or leaked: each cell
    /// first soaks into the ground up to the soil's capacity (<see cref="DeformableTerrain.SeepAt"/>: asphalt, concrete
    /// and rock take nothing, sand a lot), the rest stands as a pool that runs downhill to level with its neighbours until
    /// it is a thin film (<see cref="FluidProps.FilmMM"/>), fills dug holes and drains into lakes. Pools evaporate by
    /// game time: water at 1, petrol 12×, diesel slowly, oils never (<see cref="FluidProps.Volatility"/>; volatile parts
    /// of a blend go first), faster when hot, sunny, dry and windy, barely under a roof or in rain; water freezes below
    /// 0 °C. Fuel pools catch fire from flames nearby and burn along a trail, water pools put fires out; soaked water
    /// makes mud (<see cref="WetAt"/>), oil leaves a stain, fuel and chemicals in the soil foul nearby wells, oily or
    /// frozen pools are slick (<see cref="Slick"/>), garden beds drink standing water. Drawn as one transparent mesh per
    /// 8 m region; saved per cell (<see cref="Save"/>); pours are replicated, every peer simulates its own copy.</summary>
    public class Spills : MonoBehaviour
    {
        public const float CellSize = 0.5f, Area = CellSize * CellSize;
        public const int MaxCells = 6000, RegionCells = 16;
        /// <summary>Water evaporation, mm per game hour, at 20 °C under a high clear sun in dry, still air.</summary>
        public const float BaseEvapMM = 0.3f;
        /// <summary>Rain added to pools, mm per game hour.</summary>
        public const float RainMM = 3f;
        /// <summary>Flow and fire are simulated within this distance of the player.</summary>
        public const float ActiveReach = 140f;

        public sealed class Cell
        {
            public int ix, iz;
            public float ground;                                      // terrain height at the centre
            public float pool;                                        // standing litres
            public readonly FluidMix mix = new FluidMix();
            public float soakWater, soakFuel, soakOil, soakToxic;    // litres in the soil by kind
            public float stain;                                       // oily residue 0..1
            public float capacity, rate;                              // soil: litres it can hold, litres/s it takes in
            public float rough = 1f;                                  // hollows the film fills before it runs on (× the liquid's film)
            public float burning;                                     // > 0 while the pool is alight
            public bool frozen, shaded, lined, lake;
            public float drawnPool = -1f, drawnSoak;
            public float Soaked => soakWater + soakFuel + soakOil + soakToxic;
            public float Depth => pool * 0.001f / Area;
            public float Level => ground + Depth;
            public Vector3 Centre => new Vector3((ix + 0.5f) * CellSize, ground, (iz + 0.5f) * CellSize);
            public bool Empty => pool < 0.005f && Soaked < 0.02f && stain < 0.02f && burning <= 0f;
        }

        sealed class Region { public GameObject go; public Mesh mesh; public bool dirty; public float builtAt; }

        public static Spills Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; mat = null; DeformableTerrain.SpillWet = null; }

        readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
        readonly List<Cell> list = new List<Cell>();
        readonly Dictionary<long, Region> regions = new Dictionary<long, Region>();
        static Material mat;
        float flowTick, slowTick, gardenTick, refreshTick, splashTick, pendingHours, fireAt;
        double lastDays = -1;
        Vector3 lastFeet;
        static readonly System.Func<ResourceType, float> volatility = FluidProps.Volatility;
        static readonly System.Func<ResourceType, float> flammable = t => FluidProps.BurnRate(t) > 0f ? 1f : 0f;

        /// <summary>Litres standing on the ground everywhere (tests, debugging).</summary>
        public static float TotalPool { get { if (!Instance) return 0f; float s = 0f; foreach (var c in Instance.list) s += c.pool; return s; } }
        public static float TotalSoaked { get { if (!Instance) return 0f; float s = 0f; foreach (var c in Instance.list) s += c.Soaked; return s; } }
        public static int Count => Instance ? Instance.list.Count : 0;
        public static IReadOnlyList<Cell> Cells => Instance ? Instance.list : (IReadOnlyList<Cell>)System.Array.Empty<Cell>();

        public static Spills Ensure()
        {
            if (Instance) return Instance;
            Instance = new GameObject("Spills").AddComponent<Spills>();
            DeformableTerrain.SpillWet = WetAt;
            return Instance;
        }

        static long Key(int ix, int iz) => ((long)ix << 32) ^ (uint)iz;
        static int Ix(float x) => Mathf.FloorToInt(x / CellSize);

        // ------------------------------------------------------------------ pouring and taking

        /// <summary>Pour <paramref name="litres"/> of <paramref name="mix"/> onto the ground at a point (into a lake it is
        /// simply gone). Returns the litres that left the source.</summary>
        public static float Pour(Vector3 at, FluidMix mix, float litres, bool replicate = true)
        {
            if (mix == null || mix.Empty || litres <= 0f) return 0f;
            var s = Ensure();
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return litres;
            if (replicate && !MadMax.Net.NetSession.Applying) MadMax.Net.NetSession.Instance?.SendSpill(at, mix, litres);
            if (terrain.WaterDepth(at.x, at.z) > 0.05f) return litres;
            var c = s.Get(Ix(at.x), Ix(at.z), true);
            if (c == null) return litres;
            c.mix.Blend(c.pool, mix, litres);
            c.pool += litres;
            s.Touch(c);
            return litres;
        }

        public static float Pour(Vector3 at, ResourceType t, float litres, bool replicate = true) => Pour(at, new FluidMix(t), litres, replicate);

        /// <summary>Scoop up to <paramref name="litres"/> of standing liquid within <paramref name="radius"/> (deepest
        /// cells first) into <paramref name="into"/>. Clean water picked off the ground comes up dirty.</summary>
        public static float Take(Vector3 at, float radius, float litres, FluidMix into, float minCell = 0.05f)
        {
            if (!Instance || litres <= 0f) return 0f;
            var near = Instance.Near(at, radius, minCell);
            near.Sort((a, b) => b.pool.CompareTo(a.pool));
            float got = 0f;
            foreach (var c in near)
            {
                if (got >= litres || c.frozen) continue;
                float t = Mathf.Min(c.pool, litres - got);
                if (t <= 0f) continue;
                into.Blend(got, c.mix, t);
                got += t; c.pool -= t;
                if (c.pool < 0.005f) { c.pool = 0f; c.mix.Clear(); }
                Instance.Touch(c);
            }
            into.Swap(ResourceType.Water, ResourceType.DirtyWater);
            return got;
        }

        /// <summary>Standing litres within <paramref name="radius"/> (not frozen), and what the deepest cell holds.</summary>
        public static float PoolNear(Vector3 at, float radius, out FluidMix mix, float minCell = 0.05f)
        {
            mix = null;
            if (!Instance) return 0f;
            float sum = 0f, best = 0f;
            foreach (var c in Instance.Near(at, radius, minCell))
            {
                if (c.frozen) continue;
                sum += c.pool;
                if (c.pool > best) { best = c.pool; mix = c.mix; }
            }
            return sum;
        }

        public static Cell CellAt(float x, float z) => Instance && Instance.cells.TryGetValue(Key(Ix(x), Ix(z)), out var c) ? c : null;

        /// <summary>Depth (m) of the liquid standing at a point.</summary>
        public static float DepthAt(float x, float z) { var c = CellAt(x, z); return c != null ? c.Depth : 0f; }

        /// <summary>How wet spills make the ground at a point (0..1): standing or soaked water.</summary>
        public static float WetAt(float x, float z)
        {
            var c = CellAt(x, z);
            if (c == null) return 0f;
            float aq = c.pool > 0.1f ? c.mix.Of(FluidFamily.Aqueous) : 0f;
            float soak = c.capacity > 0.01f ? c.soakWater / c.capacity : 0f;
            return Mathf.Clamp01(Mathf.Max(aq * Mathf.Clamp01(c.Depth * 200f), soak));
        }

        /// <summary>An oily, fuel or frozen pool under a wheel: most grip goes.</summary>
        public static bool Slick(Vector3 p)
        {
            var c = CellAt(p.x, p.z);
            if (c == null || c.pool < 0.1f || Mathf.Abs(p.y - c.Level) > 0.6f) return false;
            if (c.frozen) return true;
            return c.mix.Of(FluidFamily.Lube) + c.mix[ResourceType.CrudeOil] + c.mix[ResourceType.Diesel] * 0.6f > 0.4f;
        }

        /// <summary>Dry powder over burning pools (<see cref="MadMax.Game.ExtinguisherTool"/>): puts out every pool alight
        /// within the radius; returns how many.</summary>
        public static int Smother(Vector3 at, float radius)
        {
            if (!Instance) return 0;
            int n = 0;
            foreach (var c in Instance.Near(at, radius, 0f)) if (c.burning > 0f) { c.burning = 0f; Instance.Dirty(c); n++; }
            return n;
        }

        /// <summary>Fuel and oil (litres) and coolant / acid soaked into the ground within <paramref name="radius"/>.</summary>
        public static void SoakedNear(Vector3 p, float radius, out float fuel, out float toxic)
        {
            fuel = 0f; toxic = 0f;
            if (!Instance) return;
            float r2 = radius * radius;
            foreach (var c in Instance.list)
            {
                float dx = (c.ix + 0.5f) * CellSize - p.x, dz = (c.iz + 0.5f) * CellSize - p.z;
                if (dx * dx + dz * dz > r2) continue;
                fuel += c.soakFuel + c.soakOil;
                toxic += c.soakToxic;
            }
        }

        /// <summary>Re-read the ground of the cells around a point (a liner laid or taken up, a hole dug).</summary>
        public static void RefreshAround(Vector3 at, float radius, MadMax.Building.PondLiner gone = null)
        {
            if (!Instance) return;
            int r = Mathf.CeilToInt(radius / CellSize), cx = Ix(at.x), cz = Ix(at.z);
            for (int j = -r; j <= r; j++)
            for (int i = -r; i <= r; i++)
                if (Instance.cells.TryGetValue(Key(cx + i, cz + j), out var c))
                {
                    Instance.Refresh(c, false);
                    if (gone && c.lined && !MadMax.Building.PondLiner.Covers(c.Centre, gone)) { c.lined = false; Instance.Refresh(c, false); }
                }
        }

        public static void Clear()
        {
            if (!Instance) return;
            foreach (var r in Instance.regions.Values) if (r.go) Destroy(r.go);
            Instance.regions.Clear(); Instance.cells.Clear(); Instance.list.Clear();
        }

        // ------------------------------------------------------------------ cells

        List<Cell> Near(Vector3 at, float radius, float minPool)
        {
            var l = new List<Cell>();
            int r = Mathf.CeilToInt(radius / CellSize);
            int cx = Ix(at.x), cz = Ix(at.z);
            for (int j = -r; j <= r; j++)
            for (int i = -r; i <= r; i++)
            {
                if (!cells.TryGetValue(Key(cx + i, cz + j), out var c) || c.pool < minPool) continue;
                float dx = (c.ix + 0.5f) * CellSize - at.x, dz = (c.iz + 0.5f) * CellSize - at.z;
                if (dx * dx + dz * dz <= radius * radius) l.Add(c);
            }
            return l;
        }

        Cell Get(int ix, int iz, bool create)
        {
            long k = Key(ix, iz);
            if (cells.TryGetValue(k, out var c)) return c;
            if (!create) return null;
            if (list.Count >= MaxCells) Trim();
            c = new Cell { ix = ix, iz = iz };
            Refresh(c, true);
            cells[k] = c; list.Add(c);
            return c;
        }

        /// <summary>Ground height, soil, lake, liner and roof of a cell (on creation, then every few seconds near the player).</summary>
        void Refresh(Cell c, bool full)
        {
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return;
            float x = (c.ix + 0.5f) * CellSize, z = (c.iz + 0.5f) * CellSize;
            c.ground = terrain.Height(x, z);
            c.lake = terrain.WaterDepth(x, z) > 0.05f;
            terrain.SeepAt(x, z, out float cap, out float rate);
            c.lined = MadMax.Building.PondLiner.Covers(new Vector3(x, c.ground, z));
            c.capacity = c.lined ? 0f : cap * Area;
            c.rough = cap > 0f ? 4f : c.lined ? 2f : 1f;                         // smooth asphalt, a creased tarp, rough soil
            c.rate = c.lined ? 0f : rate * Area;
            if (full) c.shaded = Physics.Raycast(new Vector3(x, c.ground + 0.3f, z), Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
        }

        void Trim()
        {
            list.Sort((a, b) => (a.pool + a.Soaked).CompareTo(b.pool + b.Soaked));
            int n = list.Count / 10;
            for (int i = 0; i < n; i++) { cells.Remove(Key(list[i].ix, list[i].iz)); Dirty(list[i]); }
            list.RemoveRange(0, n);
        }

        void Touch(Cell c)
        {
            if (Mathf.Abs(c.pool - c.drawnPool) > 0.04f || Mathf.Abs(c.Soaked - c.drawnSoak) > 0.2f) Dirty(c);
        }

        void Dirty(Cell c)
        {
            // a cell's corners are shared with the cells around it, which may sit in the next region
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                long k = Key(Mathf.FloorToInt((float)(c.ix + dx) / RegionCells), Mathf.FloorToInt((float)(c.iz + dz) / RegionCells));
                if (!regions.TryGetValue(k, out var r)) regions[k] = r = new Region();
                r.dirty = true;
            }
            c.drawnPool = c.pool; c.drawnSoak = c.Soaked;
        }

        // ------------------------------------------------------------------ simulation

        void Update()
        {
            if (!DeformableTerrain.Instance) return;
            DeformableTerrain.SpillWet = WetAt;
            double days = DayNight.TotalDays;
            float hours = lastDays < 0 ? 0f : (float)System.Math.Max(0.0, System.Math.Min(48.0, (days - lastDays) * 24.0));
            lastDays = days;
            var focus = Focus();
            float dt = Time.deltaTime;
            if (list.Count > 0) Step(dt, hours, focus);
            SplashUnderfoot(dt);
            Draw();
        }

        static Vector3 Focus()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current) return g.Current.transform.position;
            if (g && g.Player) return g.Player.transform.position;
            return Camera.main ? Camera.main.transform.position : Vector3.zero;
        }

        /// <summary>Run the simulation ahead (tests): <paramref name="gameHours"/> of evaporation and soaking, with
        /// <paramref name="realSeconds"/> of flow in 0.1 s steps.</summary>
        public static void FastForward(float gameHours, float realSeconds)
        {
            var s = Ensure();
            int steps = Mathf.Max(1, Mathf.CeilToInt(realSeconds / 0.1f));
            var focus = Focus();
            for (int i = 0; i < steps; i++) s.Step(0.1f, gameHours / steps, focus);
            s.slowTick = 1f; s.Step(0f, 0f, focus);
        }

        void Step(float dt, float hours, Vector3 focus)
        {
            pendingHours += hours;
            flowTick += dt; slowTick += dt; gardenTick += dt; refreshTick += dt;
            if (flowTick >= 0.0999f) { float f = Mathf.Min(flowTick, 0.5f); Flow(f, focus); Burn(f, focus); flowTick = 0f; }
            if (slowTick >= 0.5f) { Weathering(pendingHours); slowTick = 0f; pendingHours = 0f; }
            if (gardenTick >= 2f) { Gardens(); gardenTick = 0f; }
            if (refreshTick >= 3f)
            {
                refreshTick = 0f;
                float r2 = 60f * 60f;
                foreach (var c in list)
                {
                    float dx = (c.ix + 0.5f) * CellSize - focus.x, dz = (c.iz + 0.5f) * CellSize - focus.z;
                    if (dx * dx + dz * dz < r2) { float g = c.ground; Refresh(c, false); if (Mathf.Abs(g - c.ground) > 0.01f) Dirty(c); }
                }
            }
        }

        static readonly int[] NX = { 1, -1, 0, 0 }, NZ = { 0, 0, 1, -1 };
        readonly Cell[] nb = new Cell[4];
        readonly float[] want = new float[4];
        readonly List<Cell> active = new List<Cell>();

        /// <summary>Soak in, then run downhill towards the neighbour levels until only a film is left.</summary>
        void Flow(float dt, Vector3 focus)
        {
            var terrain = DeformableTerrain.Instance;
            float r2 = ActiveReach * ActiveReach;
            active.Clear();
            foreach (var c in list) if (c.pool > 0.005f) active.Add(c);
            foreach (var c in active)
            {
                if (c.lake) { c.pool = 0f; c.mix.Clear(); Dirty(c); continue; }        // ran into a lake
                if (!c.frozen && c.capacity > 0f && c.Soaked < c.capacity)
                {
                    float take = Mathf.Min(c.pool, Mathf.Min(c.capacity - c.Soaked, c.rate * FluidProps.Fluidity(c.mix) * dt));
                    if (take > 0f)
                    {
                        c.pool -= take;
                        float aq = c.mix.Of(FluidFamily.Aqueous) - c.mix[ResourceType.Coolant];
                        float oil = c.mix.Of(FluidFamily.Lube) + c.mix[ResourceType.CrudeOil];
                        float tox = c.mix[ResourceType.Coolant] + c.mix[ResourceType.Acid];
                        float fuel = Mathf.Max(0f, 1f - aq - oil - tox);
                        c.soakWater += take * aq; c.soakOil += take * oil; c.soakToxic += take * tox; c.soakFuel += take * fuel;
                        if (oil > 0.1f) c.stain = Mathf.Min(1f, c.stain + take * oil * 0.5f);
                        if (c.pool < 0.005f) { c.pool = 0f; c.mix.Clear(); }
                        Touch(c);
                    }
                }
                if (c.pool <= 0.005f || c.frozen) continue;
                float dx = (c.ix + 0.5f) * CellSize - focus.x, dz = (c.iz + 0.5f) * CellSize - focus.z;
                if (dx * dx + dz * dz > r2) continue;
                float level = c.Level, sum = 0f, lowest = c.ground;
                for (int n = 0; n < 4; n++)
                {
                    want[n] = 0f;
                    var o = nb[n] = Get(c.ix + NX[n], c.iz + NZ[n], false);
                    float oGround = o != null ? o.ground : terrain.Height((c.ix + NX[n] + 0.5f) * CellSize, (c.iz + NZ[n] + 0.5f) * CellSize);
                    lowest = Mathf.Min(lowest, oGround);
                    float diff = level - (o != null ? o.Level : oGround);
                    if (diff <= 0.0004f) continue;
                    want[n] = diff * Area * 1000f * 0.25f;
                    sum += want[n];
                }
                if (sum <= 0f) continue;
                // a film stays put on the flat; on a slope it keeps running
                float film = FluidProps.FilmMM(c.mix) * c.rough * Area * (c.ground - lowest > 0.02f ? 0.3f : 1f);
                float movable = Mathf.Max(0f, c.pool - film) * Mathf.Clamp01(FluidProps.Fluidity(c.mix) * dt * 8f);
                if (movable <= 0.002f) continue;
                float scale = Mathf.Min(1f, movable / sum);
                for (int n = 0; n < 4; n++)
                {
                    float m = want[n] * scale;
                    if (m <= 0.0005f) continue;
                    var o = nb[n] ?? Get(c.ix + NX[n], c.iz + NZ[n], true);
                    if (o == null) continue;
                    o.mix.Blend(o.pool, c.mix, m);
                    o.pool += m; c.pool -= m;
                    if (c.burning > 0f && o.burning <= 0f && FluidProps.BurnRate(c.mix) > 0f) o.burning = 0.01f;   // a burning stream carries the flame
                    Touch(o);
                }
                Touch(c);
            }
        }

        /// <summary>Evaporation, rain, freezing, soil drying and stain fading over <paramref name="hours"/> of game time.</summary>
        void Weathering(float hours)
        {
            bool rain = Weather.Raining && !Weather.Snowing;
            float daylight = Mathf.Clamp01(1f - DayNight.Darkness * 1.1f) * Mathf.Clamp01(DayNight.SunDirection.y * 2.5f);
            float cloud = Atmosphere.CloudCover;
            float wind = 1f + 0.12f * new Vector2(Fx.Wind.x, Fx.Wind.z).magnitude;
            var terrain = DeformableTerrain.Instance;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                float x = (c.ix + 0.5f) * CellSize, z = (c.iz + 0.5f) * CellSize;
                float temp = Weather.TemperatureAt(z);
                bool was = c.frozen;
                c.frozen = c.pool > 0.005f && FluidProps.Frozen(c.mix, temp);
                if (was != c.frozen) Dirty(c);
                if (hours > 0f)
                {
                    float e = EvapMM(x, z, temp, daylight, cloud, wind, c.shaded, rain, terrain) * hours;
                    if (c.frozen) e *= 0.05f;
                    if (c.pool > 0f && e > 0f)
                    {
                        c.pool = c.mix.Evaporate(c.pool, e * Area, volatility);
                        if (c.pool < 0.005f) { c.pool = 0f; c.mix.Clear(); }
                    }
                    if (rain && !c.shaded && (c.pool > 0.005f || c.lined))
                    {
                        float add = RainMM * Area * hours * Mathf.Max(0.3f, Weather.Wetness);
                        c.mix.Blend(c.pool, ResourceType.Water, add); c.pool += add;
                    }
                    // the soil dries and drains: water at a quarter of open evaporation plus 3 % an hour deeper,
                    // fuel vapours slowly, oil and chemicals stay (a stain that fades over weeks)
                    if (c.soakWater > 0f) c.soakWater = Mathf.Max(0f, c.soakWater * (1f - 0.03f * hours) - e * Area * 0.25f);
                    if (c.soakFuel > 0f) c.soakFuel = Mathf.Max(0f, c.soakFuel * (1f - 0.05f * hours));
                    if (c.soakToxic > 0f) c.soakToxic = Mathf.Max(0f, c.soakToxic * (1f - 0.004f * hours));
                    if (c.soakOil > 0f) c.soakOil = Mathf.Max(0f, c.soakOil * (1f - 0.002f * hours));
                    if (c.stain > 0f) c.stain = Mathf.Max(0f, c.stain - 0.002f * hours);
                    if (c.pool > 0.005f && c.mix.Of(FluidFamily.Lube) + c.mix[ResourceType.CrudeOil] > 0.3f) c.stain = Mathf.Max(c.stain, 0.3f);
                }
                if (c.Empty) { cells.Remove(Key(c.ix, c.iz)); list.RemoveAt(i); Dirty(c); continue; }
                Touch(c);
            }
            // rain falls on the whole of a lined pond, not only where water already stands
            if (rain && hours > 0f)
                foreach (var l in MadMax.Building.PondLiner.All)
                {
                    if (!l) continue;
                    float litres = RainMM * 4f * l.half.x * l.half.y * hours * Mathf.Max(0.3f, Weather.Wetness);
                    var c = Get(Ix(l.transform.position.x), Ix(l.transform.position.z), true);
                    if (c == null || c.shaded) continue;
                    c.mix.Blend(c.pool, ResourceType.Water, litres); c.pool += litres; Touch(c);
                }
        }

        /// <summary>Evaporation (mm per game hour) of water at a point: temperature, sun (not under a roof), dry air, wind;
        /// almost none in rain. Multiply by <see cref="FluidProps.Volatility"/> per liquid.</summary>
        public static float EvapMM(float x, float z, float temp, float daylight, float cloud, float wind, bool shaded, bool rain, DeformableTerrain terrain)
        {
            float fT = Mathf.Clamp(Mathf.Exp(0.055f * (temp - 20f)), 0.15f, 3f);
            float sun = 0.25f + 0.75f * daylight * (1f - 0.7f * cloud) * (shaded ? 0.2f : 1f);
            float humid = 0.35f + 0.5f * Weather.Wetness + (rain ? 0.25f : 0f);
            if (terrain)
            {
                var b = terrain.BiomeAt(x, z);
                if (b == Biome.Desert) humid -= 0.2f; else if (b == Biome.Tropical) humid += 0.2f;
            }
            float dry = 1f - Mathf.Clamp(humid, 0.05f, 0.98f);
            return BaseEvapMM * fT * sun * dry * 2f * wind * (rain ? 0.1f : 1f);
        }

        /// <summary>Evaporation (mm per game hour) of water at a point with the current sky and air.</summary>
        public static float EvapMM(Vector3 p, bool shaded)
        {
            float daylight = Mathf.Clamp01(1f - DayNight.Darkness * 1.1f) * Mathf.Clamp01(DayNight.SunDirection.y * 2.5f);
            return EvapMM(p.x, p.z, Weather.TemperatureAt(p.z), daylight, Atmosphere.CloudCover, 1f + 0.12f * new Vector2(Fx.Wind.x, Fx.Wind.z).magnitude,
                          shaded, Weather.Raining && !Weather.Snowing, DeformableTerrain.Instance);
        }

        /// <summary>Flames light fuel pools near them; burning pools feed a fire, burn off and light their neighbours;
        /// standing water puts fires in it out.</summary>
        void Burn(float dt, Vector3 focus)
        {
            foreach (var f in Fire.All)
            {
                if (!f || f.fuel <= 0f) continue;
                var p = f.transform.position;
                if ((p - focus).sqrMagnitude > ActiveReach * ActiveReach) continue;
                foreach (var c in Near(p, 1.1f, 0.05f))
                {
                    if (c.frozen) continue;
                    if (FluidProps.BurnRate(c.mix) > 0.1f && c.mix.Of(FluidFamily.Aqueous) < 0.7f)
                    { if (c.burning <= 0f && Mathf.Abs(p.y - c.ground) < 1.5f) c.burning = 0.01f; }
                    else if (c.mix.Of(FluidFamily.Aqueous) > 0.8f && c.pool > 0.15f && Vector3.Distance(p, c.Centre) < 0.6f)
                    { float use = Mathf.Min(c.pool, 2f * dt); Fire.Douse(p, 0.4f, use); c.pool -= use; Touch(c); }
                }
            }
            var net = MadMax.Net.NetSession.Instance;
            bool authority = !net || !net.Online || net.IsServer;
            bool feed = Time.time >= fireAt;
            if (feed) fireAt = Time.time + 0.6f;
            foreach (var c in list)
            {
                if (c.burning <= 0f) continue;
                float rate = FluidProps.BurnRate(c.mix);
                if (c.pool < 0.03f || rate <= 0f || c.frozen || c.mix.Of(FluidFamily.Aqueous) > 0.7f) { c.burning = 0f; Dirty(c); continue; }
                c.burning += dt;
                // only the burning share goes: water in the blend stays behind
                // a burning pool goes down ~4 mm a minute (petrol): a film burns for a quarter of a minute
                c.pool = c.mix.Evaporate(c.pool, rate * dt * 0.02f * (1f + Mathf.Min(3f, c.Depth * 250f)), flammable);
                if (c.pool < 0.03f) { c.burning = 0f; if (c.pool <= 0.005f) { c.pool = 0f; c.mix.Clear(); } }
                if (feed && authority && c.burning > 0.05f)
                    Fire.Ignite(c.Centre + Vector3.up * 0.05f, null, 3f + c.pool * 2f, Mathf.Clamp(0.35f + c.pool * 0.15f, 0.35f, 1f));
                // spreads: petrol runs ahead fast, diesel and oil creep
                if (c.burning > 0.1f)
                    for (int n = 0; n < 4; n++)
                    {
                        var o = Get(c.ix + NX[n], c.iz + NZ[n], false);
                        if (o == null || o.burning > 0f || o.pool < 0.03f || o.frozen) continue;
                        if (Random.value < FluidProps.BurnRate(o.mix) * dt * 25f) o.burning = 0.01f;
                    }
                Touch(c);
            }
        }

        readonly FluidMix scratch = new FluidMix();

        /// <summary>Beds under standing water drink it; fuel, oil, salt or chemicals on them hurt the crop.</summary>
        void Gardens()
        {
            foreach (var p in MadMax.Building.GardenPlot.All)
            {
                if (!p) continue;
                var at = p.transform.position;
                float near = PoolNear(at, 0.9f, out var m, 0.02f);
                var c = CellAt(at.x, at.z);
                if (near < 0.2f && c == null) continue;
                if (near >= 0.3f && m != null && m.Of(FluidFamily.Aqueous) - m[ResourceType.Coolant] > 0.8f && m[ResourceType.SeaWater] < 0.3f)
                {
                    if (p.water < 0.95f) { p.Water(0.15f); Take(at, 0.9f, 0.3f, scratch, 0.02f); }
                }
                else if (c != null && c.capacity > 0f && c.soakWater > c.capacity * 0.3f && p.water < 0.6f) p.Water(0.05f);
                float bad = near >= 0.2f && m != null ? m.Of(FluidFamily.Fuel) + m.Of(FluidFamily.Lube) + m[ResourceType.Coolant] + m[ResourceType.Acid] + m[ResourceType.SeaWater] : 0f;
                if (bad > 0.3f || (c != null && c.soakToxic + c.soakFuel + c.soakOil > 1f)) { p.health = Mathf.Max(0f, p.health - 0.03f); p.GetComponent<MadMax.Building.Placeable>()?.Dirty(); }
            }
        }

        void SplashUnderfoot(float dt)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Player || g.Current || list.Count == 0) return;
            var feet = g.Player.transform.position;
            float speed = (feet - lastFeet).magnitude / Mathf.Max(dt, 1e-4f);
            lastFeet = feet;
            if ((splashTick -= dt) > 0f || speed < 1f || speed > 20f) return;
            var c = CellAt(feet.x, feet.z);
            if (c == null || c.Depth < 0.002f || c.frozen || Mathf.Abs(feet.y - c.Level) > 0.3f) return;
            splashTick = 0.3f;
            var col = FluidProps.Colour(c.mix); col.a = 0.7f;
            for (int i = 0; i < 3; i++) Fx.Smoke(feet + Vector3.up * 0.05f, Random.insideUnitSphere * 0.6f + Vector3.up * 1.2f, 0.04f, col, 0.35f);
            MadMax.Audio.Sfx.Play("splash", feet, Mathf.Clamp(c.Depth * 30f, 0.1f, 0.4f), Random.Range(1.2f, 1.5f), 12f, 0.2f);
        }

        // ------------------------------------------------------------------ drawing

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<int> tris = new List<int>();
        readonly Color[] cellCol = new Color[(RegionCells + 2) * (RegionCells + 2)];
        readonly float[] cellLvl = new float[(RegionCells + 2) * (RegionCells + 2)];
        readonly int[] cornerIx = new int[(RegionCells + 1) * (RegionCells + 1)];
        readonly List<long> drop = new List<long>(), todo = new List<long>();

        void Draw()
        {
            if (regions.Count == 0) return;
            drop.Clear(); todo.Clear();
            foreach (var kv in regions)
                if (kv.Value.dirty && Time.time - kv.Value.builtAt >= 0.15f) { todo.Add(kv.Key); if (todo.Count >= 4) break; }
            foreach (var k in todo)
            {
                var r = regions[k];
                r.dirty = false; r.builtAt = Time.time;
                if (!Build((int)(k >> 32), (int)(uint)k, r)) drop.Add(k);
            }
            foreach (var k in drop) { if (regions[k].go) Destroy(regions[k].go); regions.Remove(k); }
        }

        Color CellColour(Cell c, out float level)
        {
            level = float.NaN;
            if (c == null) return Color.clear;
            if (c.pool > 0.02f)
            {
                level = c.Level;
                var col = FluidProps.Colour(c.mix);
                col.a *= Mathf.Clamp01(0.45f + c.Depth * 1000f * 0.08f);
                if (c.frozen) col = Color.Lerp(col, new Color(0.82f, 0.9f, 0.96f, 0.75f), 0.75f);
                if (c.burning > 0f) col = Color.Lerp(col, new Color(0.25f, 0.12f, 0.05f, 0.85f), 0.5f);
                if (c.mix.Of(FluidFamily.Fuel) > 0.3f && !c.frozen)
                    col = Color.Lerp(col, Color.HSVToRGB(Mathf.Repeat(c.ix * 0.37f + c.iz * 0.21f, 1f), 0.5f, 0.9f), 0.18f);   // a rainbow sheen on fuel
                return col;
            }
            if (c.stain > 0.02f) return new Color(0.07f, 0.06f, 0.05f, Mathf.Clamp01(0.5f * c.stain));
            float wet = c.capacity > 0.01f ? Mathf.Clamp01(c.Soaked / c.capacity) : 0f;
            if (wet > 0.05f) return new Color(0.12f, 0.09f, 0.06f, 0.28f * wet);
            return Color.clear;
        }

        /// <summary>Build a region's mesh: one quad per cell, corners shared, colour and height averaged over the cells
        /// at each corner so pools fade out at their edges. False when nothing is left to draw.</summary>
        bool Build(int rx, int rz, Region r)
        {
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return true;
            const int S = RegionCells + 2, C = RegionCells + 1;
            int x0 = rx * RegionCells, z0 = rz * RegionCells;
            bool any = false;
            for (int j = 0; j < S; j++)
            for (int i = 0; i < S; i++)
            {
                cells.TryGetValue(Key(x0 + i - 1, z0 + j - 1), out var c);
                cellCol[j * S + i] = CellColour(c, out cellLvl[j * S + i]);
                if (cellCol[j * S + i].a > 0.005f) any = true;
            }
            if (!any) return false;
            verts.Clear(); cols.Clear(); tris.Clear();
            var origin = new Vector3(x0 * CellSize, 0f, z0 * CellSize);
            for (int j = 0; j < C; j++)
            for (int i = 0; i < C; i++)
            {
                // corner (i, j) touches region cells (i-1..i, j-1..j) = padded (i..i+1, j..j+1)
                float r0 = 0f, g0 = 0f, b0 = 0f, a = 0f, lvl = float.NegativeInfinity;
                for (int dj = 0; dj < 2; dj++)
                for (int di = 0; di < 2; di++)
                {
                    int k = (j + dj) * S + (i + di);
                    var cc = cellCol[k];
                    r0 += cc.r * cc.a; g0 += cc.g * cc.a; b0 += cc.b * cc.a; a += cc.a;
                    if (!float.IsNaN(cellLvl[k])) lvl = Mathf.Max(lvl, cellLvl[k]);
                }
                if (a < 0.004f) { cornerIx[j * C + i] = -1; continue; }
                float wx = (x0 + i) * CellSize, wz = (z0 + j) * CellSize;
                float ground = terrain.Height(wx, wz);
                float y = (float.IsNegativeInfinity(lvl) ? ground : Mathf.Max(ground, lvl)) + 0.012f;
                cornerIx[j * C + i] = verts.Count;
                verts.Add(new Vector3(wx, y, wz) - origin);
                cols.Add(new Color(r0 / a, g0 / a, b0 / a, 1f).linear * new Color(1f, 1f, 1f, a * 0.25f));
            }
            for (int j = 0; j < RegionCells; j++)
            for (int i = 0; i < RegionCells; i++)
            {
                int v00 = cornerIx[j * C + i], v10 = cornerIx[j * C + i + 1], v01 = cornerIx[(j + 1) * C + i], v11 = cornerIx[(j + 1) * C + i + 1];
                if (v00 < 0 || v10 < 0 || v01 < 0 || v11 < 0) continue;
                tris.Add(v00); tris.Add(v01); tris.Add(v11);
                tris.Add(v00); tris.Add(v11); tris.Add(v10);
            }
            if (tris.Count == 0) return false;
            if (!r.go)
            {
                r.go = new GameObject("Spill", typeof(MeshFilter), typeof(MeshRenderer));
                r.go.transform.SetParent(transform, false);
                r.mesh = new Mesh { name = "Spill" };
                r.mesh.MarkDynamic();
                r.go.GetComponent<MeshFilter>().sharedMesh = r.mesh;
                var mr = r.go.GetComponent<MeshRenderer>();
                if (!mat) { mat = Fx.TransparentMaterial(null); mat.color = Color.white; }
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            r.go.transform.position = origin;
            r.mesh.Clear();
            r.mesh.SetVertices(verts); r.mesh.SetColors(cols); r.mesh.SetTriangles(tris, 0);
            r.mesh.RecalculateBounds();
            return true;
        }

        // ------------------------------------------------------------------ save

        /// <summary>One line per cell holding anything: prefix + "ix,iz,pool,soakW,soakF,soakO,soakT,stain\u001fmix".</summary>
        public static void Save(List<string> into, string prefix)
        {
            if (!Instance) return;
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var c in Instance.list)
            {
                if (c.Empty) continue;
                sb.Clear().Append(prefix).Append(c.ix).Append(',').Append(c.iz).Append(',')
                  .Append(c.pool.ToString("0.###", ci)).Append(',').Append(c.soakWater.ToString("0.##", ci)).Append(',')
                  .Append(c.soakFuel.ToString("0.##", ci)).Append(',').Append(c.soakOil.ToString("0.##", ci)).Append(',')
                  .Append(c.soakToxic.ToString("0.##", ci)).Append(',').Append(c.stain.ToString("0.###", ci))
                  .Append('\u001f').Append(c.mix.Save());
                into.Add(sb.ToString());
            }
        }

        /// <summary>Restore one <see cref="Save"/> line (without its prefix).</summary>
        public static void Load(string line)
        {
            var s = Ensure();
            int sep = line.IndexOf('\u001f');
            var a = (sep >= 0 ? line.Substring(0, sep) : line).Split(',');
            if (a.Length < 8 || !int.TryParse(a[0], out int ix) || !int.TryParse(a[1], out int iz)) return;
            var ci = CultureInfo.InvariantCulture; var st = NumberStyles.Float;
            var c = s.Get(ix, iz, true);
            if (c == null) return;
            float.TryParse(a[2], st, ci, out c.pool); float.TryParse(a[3], st, ci, out c.soakWater); float.TryParse(a[4], st, ci, out c.soakFuel);
            float.TryParse(a[5], st, ci, out c.soakOil); float.TryParse(a[6], st, ci, out c.soakToxic); float.TryParse(a[7], st, ci, out c.stain);
            if (sep >= 0) c.mix.Load(line.Substring(sep + 1));
            if (c.mix.Empty) c.pool = 0f;
            s.Dirty(c);
        }
    }
}
