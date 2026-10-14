using System.Collections.Generic;
using MadMax.Rendering;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The round minimap (top right, at the HUD's physical detail, anti-aliased on HD HUD), the map page's
    /// vector layers (water, highways, dirt tracks, the route), the waypoint readout and its on-screen marker.
    /// Orientation (setting MINIMAP): NORTH UP keeps the map fixed and turns the arrow; HEADING UP (default) turns the map
    /// with the camera's view (<see cref="CameraRig.ViewYaw"/>), so up on the minimap is up on the screen.</summary>
    public partial class PixelHud
    {
        // ------------------------------------------------------------------ test hooks
        /// <summary>Where the N marker sat on the minimap rim last frame (degrees clockwise from up).</summary>
        public static float LastNorthAngle { get; private set; }
        /// <summary>The world heading pointing up on the minimap (0 = north).</summary>
        public static float LastMapUp { get; private set; }
        /// <summary>The player arrow on the minimap (degrees clockwise from up).</summary>
        public static float LastArrowAngle { get; private set; }
        /// <summary>The minimap's scale (metres per HUD pixel), smoothed with speed.</summary>
        public static float LastMinimapMpp { get; private set; }
        /// <summary>The waypoint distance drawn under the minimap (null: none).</summary>
        public static string LastWaypointText { get; private set; }
        public static int LastMinimapFrame { get; private set; } = -1;
        /// <summary>The on-screen waypoint marker: 0 none, 1 in the view, 2 an arrow at the screen edge.</summary>
        public static int LastMarker { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMinimapHooks()
        {
            LastNorthAngle = LastMapUp = LastArrowAngle = LastMinimapMpp = 0f;
            LastWaypointText = null; LastMinimapFrame = -1; LastMarker = 0;
        }

        static readonly Color32 MapWater = new Color32(54, 96, 118, 255), MapToxic = new Color32(92, 116, 58, 255),
            HighwayFill = new Color32(222, 210, 184, 255), HighwayEdge = new Color32(34, 26, 20, 230), TrackFill = new Color32(172, 128, 84, 255),
            OwnPaved = new Color32(196, 188, 172, 255), OwnGravel = new Color32(150, 134, 108, 255),
            RouteFill = new Color32(120, 220, 255, 255), RouteEdge = new Color32(12, 34, 48, 220), MapInk = new Color32(16, 11, 8, 255),
            TownFill = new Color32(230, 112, 50, 255), TownDim = new Color32(160, 92, 54, 255), MapArrow = new Color32(250, 246, 236, 255);

        // ------------------------------------------------------------------ vector layers
        struct MapSeg { public Vector2 a, b; public float half; public byte kind; }   // kind 0 river, 1 dirt track, 2 highway, 3 railway
        const float SegCell = 256f;
        readonly List<MapSeg> mapSegs = new List<MapSeg>();
        readonly Dictionary<Vector2Int, List<int>> segGrid = new Dictionary<Vector2Int, List<int>>();
        readonly List<int> segNear = new List<int>();
        int[] segSeen; int segStamp;
        readonly List<Lake> allLakes = new List<Lake>(), nearLakes = new List<Lake>();
        WorldGen mapWorld;

        // view used by the vector layers: world origin → physical pixel centre, scale and turn
        float vOx, vOz, vCx, vCy, vFm, vRx = 1f, vRz, vUx, vUz = 1f;

        void SetView(float ox, float oz, float fcx, float fcy, float fm, float upYaw)
        {
            vOx = ox; vOz = oz; vCx = fcx; vCy = fcy; vFm = fm;
            float a = upYaw * Mathf.Deg2Rad;
            vUx = Mathf.Sin(a); vUz = Mathf.Cos(a); vRx = vUz; vRz = -vUx;
        }

        Vector2 ToFine(float wx, float wz)
        {
            float dx = wx - vOx, dz = wz - vOz;
            return new Vector2(vCx + (dx * vRx + dz * vRz) / vFm, vCy - (dx * vUx + dz * vUz) / vFm);
        }

        Vector2 ToFine(Vector3 w) => ToFine(w.x, w.z);

        /// <summary>Lakes and wet rivers into the base map (once, at load).</summary>
        void BakeWater(WorldGen world, Color32[] pix, int size, float mpp)
        {
            mapWorld = world;
            world.AllLakes(allLakes);
            foreach (var lake in allLakes)
            {
                var m = ToMap(new Vector3(lake.pos.x, 0f, lake.pos.y));
                int rr = Mathf.CeilToInt(lake.radius * 1.3f / mpp) + 1;
                for (int py = (int)m.y - rr; py <= (int)m.y + rr; py++)
                for (int px = (int)m.x - rr; px <= (int)m.x + rr; px++)
                {
                    if (px < 0 || py < 0 || px >= size || py >= size) continue;
                    float wx = -mapHalf + (px + 0.5f) * mpp, wz = mapCz - mapHalf + (py + 0.5f) * mpp;
                    if (world.LakeShape(lake, wx, wz) < 1.05f) pix[py * size + px] = lake.toxic ? MapToxic : MapWater;
                }
            }
            foreach (var rv in world.rivers)
                for (int k = 1; k < rv.pts.Length; k++)
                {
                    var mid = (rv.pts[k - 1] + rv.pts[k]) * 0.5f;
                    if (world.NaturalBiome(mid.x, mid.y) == Biome.Desert) continue;               // dry wadis
                    var a = ToMap(new Vector3(rv.pts[k - 1].x, 0f, rv.pts[k - 1].y)); var b = ToMap(new Vector3(rv.pts[k].x, 0f, rv.pts[k].y));
                    int n = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
                    for (int s = 0; s <= n; s++)
                    {
                        var p = Vector2.Lerp(a, b, s / (float)n);
                        int px = (int)p.x, py = (int)p.y;
                        if (px >= 0 && py >= 0 && px < size && py < size) pix[py * size + px] = MapWater;
                    }
                }
        }

        /// <summary>Roads and wet rivers as segments in a coarse grid (drawn as smooth lines over the bare land).</summary>
        void BuildMapVectors(WorldGen world)
        {
            mapSegs.Clear(); segGrid.Clear();
            void Add(Vector2 a, Vector2 b, float half, byte kind)
            {
                int i = mapSegs.Count;
                mapSegs.Add(new MapSeg { a = a, b = b, half = half, kind = kind });
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / SegCell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / SegCell);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / SegCell), z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / SegCell);
                for (int cz = z0; cz <= z1; cz++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    var key = new Vector2Int(cx, cz);
                    if (!segGrid.TryGetValue(key, out var list)) segGrid[key] = list = new List<int>();
                    list.Add(i);
                }
            }
            foreach (var rv in world.rivers)
                for (int k = 1; k < rv.pts.Length; k++)
                {
                    var mid = (rv.pts[k - 1] + rv.pts[k]) * 0.5f;
                    if (world.NaturalBiome(mid.x, mid.y) == Biome.Desert) continue;
                    Add(rv.pts[k - 1], rv.pts[k], (rv.half[k - 1] + rv.half[k]) * 0.5f, 0);
                }
            foreach (var road in world.roads.roads)
                for (int k = 1; k < road.points.Count; k++)
                {
                    var a = road.points[k - 1]; var b = road.points[k];
                    Add(new Vector2(a.x, a.z), new Vector2(b.x, b.z), Mathf.Max(1.5f, road.width * 0.5f), road.paved ? (byte)2 : (byte)1);
                }
            AddRailVectors(world, Add);                                                           // railway lines (PixelHud.Rail)
            segSeen = new int[mapSegs.Count];
        }

        void GatherSegs(float cx, float cz, float radius)
        {
            segNear.Clear();
            if (segSeen == null) return;
            segStamp++;
            int x0 = Mathf.FloorToInt((cx - radius) / SegCell), x1 = Mathf.FloorToInt((cx + radius) / SegCell);
            int z0 = Mathf.FloorToInt((cz - radius) / SegCell), z1 = Mathf.FloorToInt((cz + radius) / SegCell);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!segGrid.TryGetValue(new Vector2Int(x, z), out var list)) continue;
                foreach (int i in list) if (segSeen[i] != segStamp) { segSeen[i] = segStamp; segNear.Add(i); }
            }
        }

        /// <summary>Rivers, dirt tracks and highways (dark casing under a pale fill) at the current view, widths at least
        /// a readable line and at most the real road.</summary>
        void DrawVectors(PixelCanvas c, float widthScale = 1f)
        {
            int r = c.res;
            for (int pass = 0; pass < 6; pass++)
                foreach (int i in segNear)
                {
                    var s = mapSegs[i];
                    if (pass >= 4) { if (s.kind == 3) DrawRailSeg(c, s.a, s.b, pass == 4, widthScale); continue; }
                    if (pass == 0 && s.kind != 0 || pass == 1 && s.kind != 1 || pass >= 2 && s.kind != 2) continue;
                    var a = ToFine(s.a.x, s.a.y); var b = ToFine(s.b.x, s.b.y);
                    float real = s.half / vFm;
                    switch (pass)
                    {
                        case 0: MapDraw.Capsule(c, a.x, a.y, b.x, b.y, Mathf.Max(0.5f * r, real), MapWater); break;
                        case 1: MapDraw.Capsule(c, a.x, a.y, b.x, b.y, Mathf.Max(0.55f * r * widthScale, real * 0.9f), TrackFill); break;
                        case 2: MapDraw.Capsule(c, a.x, a.y, b.x, b.y, Mathf.Max(1.1f * r * widthScale, real) + 0.65f * r, HighwayEdge); break;
                        default: MapDraw.Capsule(c, a.x, a.y, b.x, b.y, Mathf.Max(1.1f * r * widthScale, real), HighwayFill); break;
                    }
                }
        }

        /// <summary>Lakes at full detail into a north-up land buffer (the map page's cache).</summary>
        void PaintLakes(Color32[] buf, int fw, int fh, Vector2 center, float fm, bool onlyFallback = false)
        {
            if (mapWorld == null) return;
            var world = mapWorld;
            float halfW = fw * 0.5f * fm, halfH = fh * 0.5f * fm;
            foreach (var lake in allLakes)
            {
                float reach = lake.radius * 1.3f;
                if (Mathf.Abs(lake.pos.x - center.x) > halfW + reach || Mathf.Abs(lake.pos.y - center.y) > halfH + reach) continue;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((lake.pos.x - reach - center.x) / fm + fw / 2)), i1 = Mathf.Min(fw - 1, Mathf.CeilToInt((lake.pos.x + reach - center.x) / fm + fw / 2));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(-(lake.pos.y + reach - center.y) / fm + fh / 2)), j1 = Mathf.Min(fh - 1, Mathf.CeilToInt(-(lake.pos.y - reach - center.y) / fm + fh / 2));
                if (i1 < i0 || j1 < j0) continue;
                var col = lake.toxic ? MapToxic : MapWater;
                var lk = lake;
                System.Threading.Tasks.Parallel.For(j0, j1 + 1, j =>
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        if (onlyFallback && buf[j * fw + i].a == 255) continue;                     // an HD tile drew it (water included)
                        float t = world.LakeShape(lk, center.x + (i - fw / 2) * fm, center.y - (j - fh / 2) * fm);
                        if (t < 1.05f) buf[j * fw + i] = t > 0.97f ? Color32.Lerp(col, new Color32(150, 170, 160, 255), 0.35f) : col;
                    }
                });
            }
        }

        // ------------------------------------------------------------------ the round minimap
        float miniMpp;
        Color32[] miniBuf;
        System.Action<int> miniLand;
        (int x0, int y0, int n, float fcx, float fcy, float FR, float fm, Vector3 me, DeformableTerrain roads) miniArgs;

        /// <summary>One row of the minimap's land (bare land or HD tiles, lakes, the player's roads, darkened to the rim).</summary>
        void MinimapRow(int j)
        {
            var a = miniArgs;
            int rs = canvas.res;
            int fy = a.y0 + j;
            float dy = fy + 0.5f - a.fcy;
            float reach = a.FR + 0.5f;                                                            // coverage ends half a pixel out
            if (dy * dy >= reach * reach) return;
            float hx = Mathf.Sqrt(reach * reach - dy * dy);
            int i0 = Mathf.Max(0, Mathf.FloorToInt(a.fcx - hx) - 1 - a.x0), i1 = Mathf.Min(a.n - 1, Mathf.CeilToInt(a.fcx + hx) - a.x0);
            for (int i = i0; i <= i1; i++)
            {
                int fx = a.x0 + i;
                float dx = fx + 0.5f - a.fcx;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float cov = MapDraw.Cov(a.FR - d);
                if (cov <= 0f) continue;
                float wx = a.me.x + (dx * vRx - dy * vUx) * a.fm, wz = a.me.z + (dx * vRz - dy * vUz) * a.fm;
                bool fine = false;
                var col = mapLand != null ? mapLand.At(miniView, wx, wz, rs > 1, out fine) : default;    // HD tiles (MapLand), else the base map
                if (!fine)
                {
                    col = MapAt(wx, wz, rs > 1, new Color32(30, 18, 12, 255), true);
                    foreach (var lake in nearLakes)
                    {
                        float t = mapWorld.LakeShape(lake, wx, wz);
                        if (t < 1.05f) { col = t > 0.97f ? Color32.Lerp(lake.toxic ? MapToxic : MapWater, new Color32(150, 170, 160, 255), 0.35f) : lake.toxic ? MapToxic : MapWater; break; }
                    }
                }
                if (a.roads && a.roads.PlayerRoadAt(wx, wz, out bool gravel)) col = gravel ? OwnGravel : OwnPaved;
                float edge = Mathf.SmoothStep(0f, 1f, (d / a.FR - 0.72f) / 0.28f);
                col = Color32.Lerp(col, new Color32(20, 13, 9, 255), 0.12f + edge * 0.3f);
                col.a = 255;
                MapDraw.Plot(canvas, fx, fy, col, cov);
            }
        }

        void DrawRoundMinimap(int x, int y, int size)
        {
            if (map == null || !game.Player) return;
            if (game.Menus && game.Menus.Current == MenuSystem.Page.Map) return;                  // the page draws the whole map
            var car = game.Current;
            var focus = car ? car.transform : game.Player.transform;
            int rs = canvas.res;
            MapDraw.smooth = rs > 1; MapDraw.NoClip();
            // zoom: close on foot, wider with speed (and in the air), eased
            float speed = car ? Mathf.Abs(car.ForwardSpeed) : 0f;
            float want = car ? minimapMetresPerPixel * Mathf.Lerp(1f, 2.6f, Mathf.SmoothStep(0f, 1f, speed / 30f)) * (car.aircraft ? 2f : 1f)
                             : minimapMetresPerPixel * 0.6f;
            miniMpp = miniMpp <= 0f ? want : Mathf.Lerp(miniMpp, want, 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime));
            float fm = miniMpp / rs;
            // orientation
            bool northUp = GameSettings.Current.minimapNorthUp;
            float up = northUp ? 0f : rig ? rig.ViewYaw : focus.eulerAngles.y;
            var fwd = focus.forward;
            float heading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            LastMapUp = Mathf.Repeat(up, 360f); LastNorthAngle = Mathf.Repeat(-up, 360f); LastArrowAngle = Mathf.Repeat(heading - up, 360f);
            LastMinimapMpp = miniMpp; LastMinimapFrame = Time.frameCount;

            float R = size * 0.5f - 2f;
            float cxL = x + size * 0.5f, cyL = y + size * 0.5f - 1f;
            float fcx = cxL * rs, fcy = cyL * rs, FR = R * rs;
            var me = focus.position;
            SetView(me.x, me.z, fcx, fcy, fm, up);
            float viewR = FR * fm;

            // drop shadow, then the terrain (bare land, lakes, own roads), darkening toward the rim
            MapDraw.Disc(canvas, fcx + rs, fcy + rs, FR + 1.5f * rs, new Color32(8, 6, 4, 110));
            nearLakes.Clear();
            foreach (var lake in allLakes) if ((lake.pos - new Vector2(me.x, me.z)).magnitude < viewR + lake.radius * 1.3f) nearLakes.Add(lake);
            var roads = RoadOverlay();
            float pad = viewR + 2f * fm;
            if (mapLand != null) mapLand.Prepare(miniView, me.x - pad, me.z - pad, me.x + pad, me.z + pad, fm, new Vector2(me.x, me.z), MapWorkers(false));
            int x0 = Mathf.FloorToInt(fcx - FR - 1f), y0 = Mathf.FloorToInt(fcy - FR - 1f), n = Mathf.CeilToInt(FR * 2f + 2f);
            // rows in parallel (each writes only its own pixels; the sources are read-only here), each row only over the
            // face's chord
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Hud.MinimapLand");
            miniLand ??= MinimapRow;
            miniArgs = (x0, y0, n, fcx, fcy, FR, fm, me, roads);
            if (n >= 48) System.Threading.Tasks.Parallel.For(0, n, miniLand);
            else for (int j = 0; j < n; j++) MinimapRow(j);
            miniArgs.roads = null;
            UnityEngine.Profiling.Profiler.EndSample();

            MapDraw.ClipCircle(fcx, fcy, FR - 0.5f * rs);
            GatherSegs(me.x, me.z, viewR + 20f);
            DrawVectors(canvas);
            // towns: a district tint, the centre marker comes later
            var world = game.World;
            foreach (var st in world.settlements)
            {
                var p = ToFine(st.pos.x, st.pos.y);
                float sr = st.radius / fm;
                if ((p - new Vector2(fcx, fcy)).magnitude > FR + sr) continue;
                byte ta = DistrictAlpha(miniMpp, 34);
                if (ta > 0) MapDraw.Disc(canvas, p.x, p.y, sr, new Color32(230, 112, 50, ta));
            }
            // the route along the roads (from where you are)
            if (game.HasWaypoint && game.Route.Count > 1)
                for (int pass = 0; pass < 2; pass++)
                {
                    var prev = ToFine(me);
                    for (int i = 1; i < game.Route.Count; i++)
                    {
                        var b = ToFine(game.Route[i]);
                        if (Mathf.Min((prev - new Vector2(fcx, fcy)).magnitude, (b - new Vector2(fcx, fcy)).magnitude) < FR + (b - prev).magnitude)
                            MapDraw.Capsule(canvas, prev.x, prev.y, b.x, b.y, pass == 0 ? 1.55f * rs : 0.85f * rs, pass == 0 ? RouteEdge : RouteFill);
                        prev = b;
                    }
                }
            foreach (var st in world.settlements)
            {
                var p = ToFine(st.pos.x, st.pos.y);
                if ((p - new Vector2(fcx, fcy)).magnitude > FR) continue;
                bool known = game.Discovered.Contains(TownKey(st.index));
                MapDraw.Disc(canvas, p.x, p.y, 2.6f * rs, MapInk);
                MapDraw.Disc(canvas, p.x, p.y, 1.8f * rs, known ? TownFill : TownDim);
            }
            DrawRailStations(canvas, fcx, fcy, FR);                                               // stations (PixelHud.Rail)
            world.SitesNear(me, viewR, mapSites);
            foreach (var site in mapSites)
            {
                if (!game.Discovered.Contains(site.Key)) continue;
                var p = ToFine(site.pos.x, site.pos.y);
                if ((p - new Vector2(fcx, fcy)).magnitude > FR - 5f * rs) continue;
                MapDraw.Disc(canvas, p.x, p.y, 3.4f * rs, new Color32(20, 30, 40, 235));
                string letter = site.kind == SiteKind.Bunker ? "B" : site.kind == SiteKind.Airfield ? "A" : "T";
                canvas.Text(Mathf.RoundToInt(p.x / rs) - 1, Mathf.RoundToInt(p.y / rs) - 2, letter, new Color32(150, 200, 255, 255), 1, false);
            }
            foreach (var w in game.Wrecks) if (w) Dot(w.transform.position, 1.1f, new Color32(150, 140, 130, 220));
            foreach (var t in game.Trailers) if (t) Dot(t.transform.position, 1.4f, new Color32(110, 160, 220, 255));
            foreach (var fv in game.Fleet) if (fv && fv != game.Current) Dot(fv.transform.position, 1.6f, Amber);
            void Dot(Vector3 wp, float r, Color32 col)
            {
                var p = ToFine(wp);
                if ((p - new Vector2(fcx, fcy)).magnitude > FR - 2f * rs) return;
                MapDraw.Disc(canvas, p.x, p.y, (r + 0.8f) * rs, MapInk);
                MapDraw.Disc(canvas, p.x, p.y, r * rs, col);
            }
            // pins and the waypoint: in range a diamond, out of range an arrowhead on the rim pointing at it
            MapDraw.NoClip();
            game.JobPins(pins);
            foreach (var pin in pins)
            {
                var p = ToFine(pin.pos);
                if (pin.radius > 0f)
                {
                    float pr = pin.radius / fm;
                    if (pr < FR * 1.5f && (p - new Vector2(fcx, fcy)).magnitude < FR + pr)
                    {
                        MapDraw.ClipCircle(fcx, fcy, FR - 0.5f * rs);
                        var pc = pin.color; pc.a = 150;
                        MapDraw.Ring(canvas, p.x, p.y, pr, 0.45f * rs, pc);
                        MapDraw.NoClip();
                    }
                    continue;
                }
                MiniMark(p, fcx, fcy, FR, 2.4f * rs, pin.color, false);
            }
            if (game.HasWaypoint) MiniMark(ToFine(game.Waypoint), fcx, fcy, FR, 3.3f * rs, RouteFill, true);
            // you
            MapDraw.Arrow(canvas, fcx, fcy, LastArrowAngle, 8.5f * rs, MapArrow, MapInk, 1.1f * rs);
            if (HD) return;                                                                      // HD HUD: the vector rim (PixelHud.HD), every frame
            // rim, compass points, health (left) and stamina (right) arcs
            MapDraw.Ring(canvas, fcx, fcy, FR, 0.9f * rs, new Color32(22, 15, 11, 255));
            MapDraw.Ring(canvas, fcx, fcy, FR - 1.1f * rs, 0.35f * rs, new Color32(236, 206, 156, 80));
            for (int k = 1; k < 4; k++)
            {
                float a = (LastNorthAngle + k * 90f) * Mathf.Deg2Rad;
                MapDraw.Disc(canvas, fcx + Mathf.Sin(a) * FR, fcy - Mathf.Cos(a) * FR, 0.9f * rs, new Color32(200, 180, 150, 220));
            }
            var stats = game.Stats;
            float hp = stats != null ? Mathf.Clamp01(stats.health / Mathf.Max(1f, stats.MaxHealth)) : 1f;
            float sp = stats != null ? Mathf.Clamp01(stats.stamina / Mathf.Max(1f, stats.MaxStamina)) : 1f;
            float ringR = FR + 2.5f * rs, ringW = 0.95f * rs;
            MapDraw.Ring(canvas, fcx, fcy, ringR, ringW + 0.5f * rs, new Color32(14, 10, 8, 190), 182f, 176f);
            MapDraw.Ring(canvas, fcx, fcy, ringR, ringW + 0.5f * rs, new Color32(14, 10, 8, 190), 2f, 176f);
            bool low = hp < 0.25f && (Time.unscaledTime * 2f) % 1f > 0.4f;
            MapDraw.Ring(canvas, fcx, fcy, ringR, ringW, low ? Red : Green, 182f, 176f * hp);
            MapDraw.Ring(canvas, fcx, fcy, ringR, ringW, Amber, 178f - 176f * sp, 176f * sp);
            // N on the rim
            float na = LastNorthAngle * Mathf.Deg2Rad;
            float nx = fcx + Mathf.Sin(na) * FR, ny = fcy - Mathf.Cos(na) * FR;
            MapDraw.Disc(canvas, nx, ny, 3.9f * rs, new Color32(22, 15, 11, 245));
            MapDraw.Ring(canvas, nx, ny, 3.9f * rs, 0.45f * rs, Amber);
            canvas.Text(Mathf.RoundToInt(nx / rs) - 1, Mathf.RoundToInt(ny / rs) - 2, "N", Text, 1, false);
            // distance to the waypoint, a tab under the rim
            LastWaypointText = null;
            if (game.HasWaypoint)
            {
                string dist = FormatDistance(game.WaypointDistance);
                LastWaypointText = dist;
                int tw = PixelCanvas.TextWidth(dist) + 8, tx = Mathf.RoundToInt(cxL) - tw / 2, ty = y + size - 7;
                canvas.Rect(tx, ty, tw, 9, new Color32(12, 9, 7, 235));
                canvas.Frame(tx, ty, tw, 9, RouteFill);
                canvas.Text(tx + 4, ty + 2, dist, RouteFill);
            }
        }

        static readonly List<string> townKeys = new List<string>();
        /// <summary>"town:i" without a new string every compose.</summary>
        static string TownKey(int i)
        {
            while (townKeys.Count <= i) townKeys.Add("town:" + townKeys.Count);
            return townKeys[i];
        }

        /// <summary>A minimap marker: a diamond inside the face, an arrowhead just inside the rim pointing toward it when
        /// it is out of range.</summary>
        void MiniMark(Vector2 p, float fcx, float fcy, float FR, float r, Color32 col, bool big)
        {
            int rs = canvas.res;
            var d = p - new Vector2(fcx, fcy);
            float dist = d.magnitude;
            if (dist < FR - r - 1.5f * rs)
            {
                MapDraw.Diamond(canvas, p.x, p.y, r + 1.1f * rs, MapInk);
                MapDraw.Diamond(canvas, p.x, p.y, r, col);
                if (big) MapDraw.Diamond(canvas, p.x, p.y, r * 0.38f, MapInk);
                return;
            }
            if (dist < 1e-3f) return;
            float ang = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
            var at = new Vector2(fcx, fcy) + d / dist * (FR - (big ? 4.5f : 3.6f) * rs);
            MapDraw.Arrow(canvas, at.x, at.y, ang, (big ? 7.5f : 5.5f) * rs, col, MapInk, 0.9f * rs);
        }

        /// <summary>A distance for display: "850 M" / "1.2 KM", or yards / miles with imperial units.</summary>
        public static string FormatDistance(float m)
        {
            if (GameSettings.Current.metric)
                return m < 995f ? Mathf.Max(0, Mathf.RoundToInt(m / 10f) * 10) + " M" : (m / 1000f).ToString(m < 9950f ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + " KM";
            float mi = m / 1609.344f;
            return mi < 0.1f ? Mathf.RoundToInt(m * 1.0936f / 10f) * 10 + " YD" : mi.ToString(mi < 9.95f ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + " MI";
        }

        // ------------------------------------------------------------------ the waypoint in the view
        Camera markerCam;

        /// <summary>The waypoint in the 3D view: a diamond with the distance where it is on screen; off screen in the views
        /// looking down, an arrowhead at the screen edge pointing at it (crosshair views have the compass strip).</summary>
        void DrawWaypointMarker()
        {
            LastMarker = 0;
            if (!game.HasWaypoint || (game.Menus && game.Menus.IsOpen) || !rig || !rig.pixel) return;
            if (!markerCam) markerCam = rig.pixel.GetComponent<Camera>();
            if (!markerCam) return;
            int rs = canvas.res;
            MapDraw.smooth = rs > 1; MapDraw.NoClip();
            var vp = markerCam.WorldToViewportPoint(game.Waypoint + Vector3.up * 1.5f);
            string dist = FormatDistance(game.WaypointDistance);
            if (vp.z > 0f && vp.x > 0.03f && vp.x < 0.97f && vp.y > 0.05f && vp.y < 0.95f)
            {
                float fx = vp.x * canvas.pw, fy = (1f - vp.y) * canvas.ph;
                float bob = Mathf.Sin(Time.unscaledTime * 3f) * 0.8f * rs;
                MapDraw.Capsule(canvas, fx, fy - 4f * rs + bob, fx, fy + 4f * rs, 0.45f * rs, new Color32(120, 220, 255, 140));
                MapDraw.Diamond(canvas, fx, fy - 7f * rs + bob, 5f * rs, MapInk);
                MapDraw.Diamond(canvas, fx, fy - 7f * rs + bob, 3.9f * rs, RouteFill);
                MapDraw.Diamond(canvas, fx, fy - 7f * rs + bob, 1.5f * rs, MapInk);
                int lx = Mathf.RoundToInt(fx / rs), ly = Mathf.RoundToInt(fy / rs);
                canvas.Text(lx - PixelCanvas.TextWidth(dist) / 2, ly + 3, dist, RouteFill);
                LastMarker = 1;
                return;
            }
            if (!rig.TopDownView) return;
            if (vp.z < 0f) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
            var d = new Vector2((vp.x - 0.5f) * canvas.w, (0.5f - vp.y) * canvas.h);
            if (d.sqrMagnitude < 1f) return;
            float hw = canvas.w * 0.5f - 16f, hh = canvas.h * 0.5f - 16f;
            float k = Mathf.Min(hw / Mathf.Max(1e-3f, Mathf.Abs(d.x)), hh / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
            var at = new Vector2(canvas.w * 0.5f, canvas.h * 0.5f) + d * k;
            float ang = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;                                  // clockwise from up (y down)
            MapDraw.Arrow(canvas, at.x * rs, at.y * rs, ang, 10f * rs, RouteFill, MapInk, 1.1f * rs);
            var tp = at - d.normalized * 11f;
            canvas.Text(Mathf.RoundToInt(tp.x) - PixelCanvas.TextWidth(dist) / 2, Mathf.RoundToInt(tp.y) - 2, dist, RouteFill);
            LastMarker = 2;
        }

        // ------------------------------------------------------------------ the map page (north up)
        /// <summary>The map page's base: bare land (+ lakes), rivers, roads as smooth lines, the player's own roads.</summary>
        void DrawMapBase(PixelCanvas c, int x, int y, int w, int h, Vector2 center, float mpp)
        {
            DrawMapLand(c, x, y, w, h, center, mpp, true);
            int r = c.res;
            MapDraw.smooth = r > 1;
            SetView(center.x, center.y, (x + w / 2) * r, (y + h / 2) * r, mpp / r, 0f);
            MapDraw.ClipRect(x * r, y * r, (x + w) * r, (y + h) * r);
            GatherSegs(center.x, center.y, 0.5f * Mathf.Sqrt(w * w + h * h) * mpp + 20f);
            DrawVectors(c, Mathf.Clamp(6f / mpp, 0.6f, 1f));
            var world = game.World;
            byte da = DistrictAlpha(mpp, 40);
            if (da > 0)
                foreach (var st in world.settlements)
                {
                    var p = ToFine(st.pos.x, st.pos.y);
                    MapDraw.Disc(c, p.x, p.y, st.radius / vFm, new Color32(230, 112, 50, da));     // districts (fade out as the plan shows)
                }
            MapDraw.NoClip();
            DrawPlayerRoads(c, x, y, w, h, center, mpp);
        }

        /// <summary>The map page's route (cased line along the roads) and the waypoint diamond.</summary>
        void DrawMapRoute(PixelCanvas c, int x, int y, int w, int h, Vector2 center, float mpp)
        {
            int r = c.res;
            MapDraw.smooth = r > 1;
            SetView(center.x, center.y, (x + w / 2) * r, (y + h / 2) * r, mpp / r, 0f);
            MapDraw.ClipRect(x * r, y * r, (x + w) * r, (y + h) * r);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < game.Route.Count; i++)
                {
                    var a = ToFine(game.Route[i - 1]); var b = ToFine(game.Route[i]);
                    MapDraw.Capsule(c, a.x, a.y, b.x, b.y, pass == 0 ? 1.6f * r : 0.85f * r, pass == 0 ? RouteEdge : RouteFill);
                }
            var wp = ToFine(game.Waypoint);
            MapDraw.Diamond(c, wp.x, wp.y, 5f * r, MapInk);
            MapDraw.Diamond(c, wp.x, wp.y, 3.9f * r, RouteFill);
            MapDraw.Diamond(c, wp.x, wp.y, 1.5f * r, MapInk);
            MapDraw.NoClip();
        }

        /// <summary>The player on the map page: the camera's view cone and the heading arrow.</summary>
        void DrawMapYou(PixelCanvas c, int x, int y, int w, int h, Vector2 center, float mpp, Transform me)
        {
            int r = c.res;
            MapDraw.smooth = r > 1;
            SetView(center.x, center.y, (x + w / 2) * r, (y + h / 2) * r, mpp / r, 0f);
            MapDraw.ClipRect(x * r, y * r, (x + w) * r, (y + h) * r);
            var p = ToFine(me.position);
            float view = (rig ? rig.ViewYaw : me.eulerAngles.y) * Mathf.Deg2Rad, len = 26f * r, half = 32f * Mathf.Deg2Rad;
            var a = p + new Vector2(Mathf.Sin(view - half), -Mathf.Cos(view - half)) * len;
            var b = p + new Vector2(Mathf.Sin(view + half), -Mathf.Cos(view + half)) * len;
            MapDraw.Triangle(c, p, a, b, new Color32(255, 240, 210, 46));
            var f = me.forward;
            MapDraw.Arrow(c, p.x, p.y, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 10f * r, MapArrow, MapInk, 1.2f * r);
            MapDraw.NoClip();
        }

        /// <summary>The map page's legend (two columns, bottom left of the map).</summary>
        public void DrawMapLegend(PixelCanvas c, int x, int y)
        {
            int r = c.res;
            MapDraw.smooth = r > 1; MapDraw.NoClip();
            const int rowH = 8, colW = 58, rows = 6;
            c.Rect(x, y, colW * 2 + 6, rows * rowH + 6, new Color32(12, 8, 6, 215));
            c.Frame(x, y, colW * 2 + 6, rows * rowH + 6, MadMax.Voxel.Pal.PanelEdge);
            for (int k = 0; k < 12; k++)
            {
                int lx = x + 4 + (k / rows) * colW, ly = y + 4 + (k % rows) * rowH;
                float cx = (lx + 3.5f) * r, cy = (ly + 2.5f) * r;
                string label;
                switch (k)
                {
                    case 0: MapDraw.Arrow(c, cx, cy, 0f, 6.5f * r, MapArrow, MapInk, 0.8f * r); label = "YOU"; break;
                    case 1: MapDraw.Diamond(c, cx, cy, 3.2f * r, MapInk); MapDraw.Diamond(c, cx, cy, 2.4f * r, RouteFill); label = "WAYPOINT"; break;
                    case 2: MapDraw.Capsule(c, cx - 3f * r, cy, cx + 3f * r, cy, 1.4f * r, RouteEdge); MapDraw.Capsule(c, cx - 3f * r, cy, cx + 3f * r, cy, 0.8f * r, RouteFill); label = "ROUTE"; break;
                    case 3: MapDraw.Capsule(c, cx - 3f * r, cy, cx + 3f * r, cy, 1.6f * r, HighwayEdge); MapDraw.Capsule(c, cx - 3f * r, cy, cx + 3f * r, cy, 1.0f * r, HighwayFill); label = "HIGHWAY"; break;
                    case 4: MapDraw.Capsule(c, cx - 3f * r, cy, cx + 3f * r, cy, 0.6f * r, TrackFill); label = "DIRT TRACK"; break;
                    case 5: MapDraw.Disc(c, cx, cy, 2.6f * r, MapWater); label = "WATER"; break;
                    case 6: MapDraw.Disc(c, cx, cy, 2.4f * r, MapInk); MapDraw.Disc(c, cx, cy, 1.7f * r, TownFill); label = "TOWN"; break;
                    case 7: MapDraw.Diamond(c, cx, cy, 2.8f * r, MapInk); MapDraw.Diamond(c, cx, cy, 2.0f * r, new Color32(255, 200, 60, 255)); label = "JOB"; break;
                    case 8: MapDraw.Disc(c, cx, cy, 2.2f * r, MapInk); MapDraw.Disc(c, cx, cy, 1.5f * r, Amber); label = "YOUR VEHICLE"; break;
                    case 9: c.Rect(lx + 2, ly, 1, 5, Text); c.Rect(lx + 3, ly, 3, 2, Green); label = "CLAIM"; break;
                    case 10: c.Rect(lx + 1, ly, 5, 5, Empty); c.Set(lx + 3, ly + 2, Green); label = "WORKSHOP"; break;
                    default: MapDraw.Ring(c, cx, cy, 2.6f * r, 0.4f * r, new Color32(255, 200, 60, 200)); label = "SEARCH AREA"; break;
                }
                c.Text(lx + 9, ly, label, Dim, 1, false);
            }
        }
    }
}
