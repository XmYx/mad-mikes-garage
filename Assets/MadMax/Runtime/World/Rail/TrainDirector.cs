using System.Collections.Generic;
using System.Globalization;
using MadMax.Game;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Runs the railway's traffic: makeshift trader services between stations and armoured patrols on long
    /// runs (deterministic per world, clocks saved), spawned as trains within ~600 m of the player and folded away
    /// beyond ~750 m; people and a trader on the platform while a makeshift train stands; crews bailing out of a
    /// knocked-out train; derelict wagons, locos and draisines standing in station yards (taken once, then saved as
    /// free trains); stations found on the journey; free (player / derelict) trains saved with their cars and place.</summary>
    public class TrainDirector : MonoBehaviour
    {
        public static TrainDirector Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Instance = null;

        public const float SpawnReach = 600f, FoldReach = 760f;
        static readonly string[] Names = { "RUST BUCKET", "DUST QUEEN", "OLD SMOKY", "IRON MULE", "LONG HAUL", "WIDOWMAKER", "SALT EXPRESS", "THE CRAWLER", "GRAVEL GERTIE", "TIN LIZZIE" };
        static readonly string[] TraderKinds = { "parts", "food", "salvage", "fuel" };

        WastelandGame game;
        public RailNetwork Net { get; private set; }
        Material mat, glow;
        public readonly List<TrainService> services = new List<TrainService>();
        public readonly HashSet<int> yards = new HashSet<int>();
        readonly HashSet<int> routeLines = new HashSet<int>();
        readonly Dictionary<TrainService, List<MadMax.Npc.Npc>> folk = new Dictionary<TrainService, List<MadMax.Npc.Npc>>();
        readonly List<MadMax.Npc.Npc> loose = new List<MadMax.Npc.Npc>();
        readonly Dictionary<int, MadMax.Npc.Npc> masters = new Dictionary<int, MadMax.Npc.Npc>();
        readonly HashSet<string> notedTheft = new HashSet<string>();
        float scanAt;
        public Material Mat => mat;
        public Material Glow => glow;

        public static TrainDirector Create(WastelandGame g, RailNetwork net)
        {
            var d = g.gameObject.AddComponent<TrainDirector>();
            Instance = d;
            d.game = g; d.Net = net;
            d.mat = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : g.propMaterial;
            d.glow = RollingCity.GlowMaterial(g.propMaterial);
            DefineLoot();
            d.BuildServices();
            return d;
        }

        static void DefineLoot()
        {
            LootTables.Define("rail_office", ("book_railways", 1, 1, 0.5f), ("misc_paper", 1, 4, 2f), ("coin_chit", 1, 3, 1f), ("tool_lantern", 1, 1, 0.3f), ("food_can", 1, 2, 1.5f), ("drink_water", 1, 2, 1.5f), ("res:1", 2, 6, 2f), ("kit_rail_cart", 1, 1, 0.08f));
            LootTables.Define("rail_signal", ("book_railways", 1, 1, 1.2f), ("tool_wrench", 1, 1, 0.4f), ("misc_coil", 1, 1, 0.4f), ("res:21", 1, 4, 1.5f), ("res:20", 1, 3, 1f), ("tool_flashlight", 1, 1, 0.3f), ("res:8", 1, 3, 1f));
            LootTables.Define("rail_depot", ("res:20", 2, 8, 2f), ("res:45", 1, 4, 1f), ("res:8", 2, 6, 2f), ("res:36", 5, 20, 1.5f), ("tool_wrench", 1, 1, 0.4f), ("kit_rail_flatcar", 1, 1, 0.12f), ("kit_rail_cart", 1, 1, 0.15f), ("book_railways", 1, 1, 0.3f), ("res:1", 4, 12, 2f));
            LootTables.Define("rail_hopper", ("res:1", 6, 20, 3f), ("res:15", 4, 12, 1.5f), ("res:39", 4, 12, 1.5f), ("res:3", 4, 12, 1f), ("res:44", 4, 10, 1f));
            LootTables.Define("rail_wreck", ("res:1", 4, 12, 3f), ("res:5", 2, 6, 2f), ("res:4", 1, 3, 1f), ("part:wheel_street", 1, 1, 0.4f), ("part:radiator_car", 1, 1, 0.25f), ("use_battery", 1, 1, 0.4f));
            LootTables.Define("rail_armoury", ("ammo_shells", 4, 12, 2f), ("ammo_mg", 1, 2, 1f), ("ammo_rifle", 3, 8, 1f), ("throw_dynamite", 1, 2, 0.5f), ("tool_bolt_rifle", 1, 1, 0.15f), ("med_bandage", 1, 3, 1f));
        }

        // ------------------------------------------------------------------ services

        void BuildServices()
        {
            var net = Net;
            var rnd = new System.Random(game.World.seed * 4049 + 11);
            var pass = new List<RailStation>();
            foreach (var st in net.stations) if (st.Passenger && st.platformLines.Count > 0) pass.Add(st);
            if (pass.Count < 2) return;
            int makeshift = Mathf.Clamp(pass.Count / 3, 2, 6);
            var usedEnds = new HashSet<int>();
            for (int k = 0; k < makeshift + 2; k++)
            {
                bool armoured = k >= makeshift;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var a = pass[rnd.Next(pass.Count)];
                    if (!armoured && usedEnds.Contains(a.id) && attempt < 5) continue;
                    var dist = Dijkstra(a, out var prev);
                    RailStation best = null; int bestState = -1; float bestScore = float.MaxValue;
                    foreach (var b in pass)
                    {
                        if (b == a) continue;
                        int s = BestState(b, dist, out float d);
                        if (s < 0) continue;
                        float score = armoured ? -d : Mathf.Abs(d - 2600f) + (usedEnds.Contains(b.id) ? 1500f : 0f);
                        if ((armoured && d < 1800f) || (!armoured && (d < 700f || d > 6500f))) continue;
                        if (score < bestScore) { bestScore = score; best = b; bestState = s; }
                    }
                    if (best == null) continue;
                    var svc = MakeService(services.Count, armoured, a, best, Rebuild(bestState, prev), rnd);
                    if (svc == null) continue;
                    services.Add(svc);
                    usedEnds.Add(a.id); usedEnds.Add(best.id);
                    break;
                }
            }
        }

        static int S(int line, bool fwd) => line * 2 + (fwd ? 1 : 0);

        Dictionary<int, float> Dijkstra(RailStation from, out Dictionary<int, int> prev)
        {
            var net = Net;
            var dist = new Dictionary<int, float>(); prev = new Dictionary<int, int>();
            var heap = new RailNetwork.MinHeap();
            foreach (int l in from.platformLines) foreach (bool f in new[] { true, false }) { int s = S(l, f); dist[s] = 0f; prev[s] = -1; heap.Push(s, 0f); }
            var buf = new List<Vector2Int>();
            int guard = 0;
            while (heap.Count > 0 && guard++ < 20000)
            {
                int s = heap.Pop(out float d);
                if (dist.TryGetValue(s, out float known) && d > known + 1e-3f) continue;
                var line = net.lines[s >> 1]; bool fwd = (s & 1) == 1;
                if (line.dead) continue;
                int node = fwd ? line.b : line.a;
                net.Exits(node, line.id, fwd ? 1 : 0, buf);
                foreach (var e in buf)
                {
                    var nl = net.lines[e.x];
                    if (nl.dead || nl.kind == TrackKind.Siding) continue;
                    int ns = S(e.x, e.y == 0);
                    float nd = d + nl.Length;
                    if (dist.TryGetValue(ns, out float old) && old <= nd) continue;
                    dist[ns] = nd; prev[ns] = s;
                    heap.Push(ns, nd);
                }
            }
            return dist;
        }

        static int BestState(RailStation b, Dictionary<int, float> dist, out float d)
        {
            d = float.MaxValue; int best = -1;
            foreach (int l in b.platformLines) foreach (bool f in new[] { true, false }) { int s = S(l, f); if (dist.TryGetValue(s, out float v) && v < d) { d = v; best = s; } }
            return best;
        }

        RailPath Rebuild(int state, Dictionary<int, int> prev)
        {
            var chain = new List<int>();
            for (int s = state; s >= 0 && chain.Count < 4000; s = prev.TryGetValue(s, out int p) ? p : -1) chain.Add(s);
            chain.Reverse();
            var path = new RailPath();
            float at = 0f;
            foreach (int s in chain) { var l = Net.lines[s >> 1]; path.segs.Add(new RailPath.Seg { line = l, fwd = (s & 1) == 1, start = at }); at += l.Length; }
            return path;
        }

        TrainService MakeService(int id, bool armoured, RailStation a, RailStation b, RailPath route, System.Random rnd)
        {
            if (route.segs.Count == 0) return null;
            var consist = armoured ? TrainDesigns.Armoured(rnd.Next()) : TrainDesigns.Makeshift(rnd.Next());
            float len = 0f; foreach (var k in consist) len += TrainDesigns.Get(k).length + 0.5f;
            // lead lines either end so the tail fits at the end stops
            route.ExtendFront(Net); route.ExtendBack(Net);
            var svc = new TrainService
            {
                id = id, armoured = armoured, route = route, consist = consist, trainLength = len, seed = rnd.Next(),
                vmax = armoured ? 10.5f : 13.5f, dwell = armoured ? 40f : 75f,
                faction = armoured ? (id % 2 == 0 ? Faction.Rustmen : Faction.Remnants) : Faction.FuelGuild,
            };
            svc.name = armoured ? (svc.faction == Faction.Remnants ? "REMNANT IRONCLAD" : "RUSTMEN WAR TRAIN") : "THE " + Names[rnd.Next(Names.Length)];
            // stops: every station the route runs through (ends first)
            var found = new List<(float d, int st)>();
            foreach (var st in Net.stations)
            {
                if (!(st.Passenger || st.tier == StationTier.WaterStop)) continue;
                if (armoured && st != a && st != b) continue;
                if (!route.Project(st.World(0f, 0f), 8f, out float d)) continue;
                float h = Mathf.Clamp(d + len * 0.5f, route.Start + len + 1f, route.End - 1f);
                bool dup = false;
                foreach (var f in found) if (Mathf.Abs(f.d - h) < 60f) dup = true;
                if (!dup) found.Add((h, st.id));
            }
            if (found.Count < 2) return null;
            found.Sort((x, y) => x.d.CompareTo(y.d));
            foreach (var f in found) { svc.stops.Add(f.d); svc.stopStation.Add(f.st); }
            svc.Timetable();
            svc.clock = rnd.NextDouble() * svc.Cycle;
            foreach (var s in route.segs) routeLines.Add(s.line.id);
            return svc;
        }

        // ------------------------------------------------------------------ the tick

        void Update()
        {
            if (!game || !game.Ready || Net == null) return;
            float dt = Time.deltaTime;
            foreach (var s in services) if (!s.live && !s.ended) s.clock += dt;
            if (Time.time < scanAt) return;
            scanAt = Time.time + 0.5f;
            var focus = game.FocusPos;
            foreach (var s in services)
            {
                if (s.ended)
                {
                    if (DayNight.Day >= s.endDay + 3 && (s.WorldPoint() - focus).sqrMagnitude > FoldReach * FoldReach) s.ended = false;
                    continue;
                }
                float d2 = (s.WorldPoint() - focus).sqrMagnitude;
                if (!s.live && d2 < SpawnReach * SpawnReach) Spawn(s);
                else if (s.live && d2 > FoldReach * FoldReach) Fold(s);
            }
            Yards(focus);
            Discover();
            Theft();
            for (int i = loose.Count - 1; i >= 0; i--) if (!loose[i] || (loose[i].transform.position - focus).sqrMagnitude > 250f * 250f) { if (loose[i]) Destroy(loose[i].gameObject); loose.RemoveAt(i); }
        }

        void Spawn(TrainService s)
        {
            var flips = new bool[s.consist.Length];
            float h = s.Evaluate(s.clock, out _, out _, out _, out _);
            var t = Train.Create(Net, s.consist, flips, s.route, h, mat, glow, s.seed, s.name);
            t.service = s; s.live = t;
            t.KeyLoot("rail:" + s.id + ":" + DayNight.Day);
            t.Snap();
        }

        void Fold(TrainService s)
        {
            ClearPlatform(s);
            if (s.live) Destroy(s.live.gameObject);
            s.live = null;
        }

        /// <summary>Arrival (stop ≥ 0): the bell, people step down; departure (stop -1): they get back on.</summary>
        public void StopEvent(TrainService s, Train t, int stop, int last)
        {
            if (stop < 0) { ClearPlatform(s); return; }
            var st = Net.stations[s.stopStation[stop]];
            var at = st.World(0f, 0f);
            MadMax.Audio.Sfx.Play("bell", at + Vector3.up * 3f, 0.6f, 1.1f, 120f, 0.5f);
            if (s.armoured || !st.Passenger || st.platforms.Count == 0 || (at - game.FocusPos).sqrMagnitude > 230f * 230f) return;
            var list = new List<MadMax.Npc.Npc>();
            var pf = st.platforms[0];
            var r = new System.Random(s.seed ^ DayNight.Day * 31 ^ stop);
            for (int k = 0; k < 3; k++)
            {
                bool trader = k == 0;
                var role = trader ? NpcRole.Trader : NpcRole.Wanderer;
                var prof = NpcProfile.Make("rail:" + s.id + ":" + DayNight.Day + ":" + stop + ":" + k, role, game.World.seed * 7 + s.seed + k * 131 + stop * 17, trader ? TraderKinds[r.Next(TraderKinds.Length)] : null);
                float u = Mathf.Lerp(pf.x + 4f, pf.y - 4f, (float)r.NextDouble());
                var p = st.World(u, (pf.z + pf.w) * 0.5f, RailNetwork.PlatformTop + 0.05f);
                var n = MadMax.Npc.Npc.Spawn(prof, p, st.Yaw + 90f, null, game.propMaterial);
                n.homeRadius = trader ? 1.5f : 6f;
                n.mode = trader ? MadMax.Npc.Npc.Mode.Stand : MadMax.Npc.Npc.Mode.Wander;
                list.Add(n);
            }
            SpawnStationmaster(s, t, st, pf);
            folk[s] = list;
        }

        /// <summary>The stationmaster for this stop: on the platform beside the loco, turned to the train; lamp by night.</summary>
        /// <summary>Stays on the platform between trains (folds away with distance like the loose folk).</summary>
        void SpawnStationmaster(TrainService s, Train t, RailStation st, Vector4 pf)
        {
            var loco = t ? t.Loco : null;
            if (!loco) return;
            float v = (pf.z + pf.w) * 0.5f, bestU = pf.x + 3f, bd = float.MaxValue;
            for (float u = pf.x + 2f; u <= pf.y - 2f; u += 1.5f)
            {
                float d = (st.World(u, v) - loco.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; bestU = u; }
            }
            var p = st.World(bestU, v, RailNetwork.PlatformTop + 0.05f);
            var to = loco.transform.position - p; to.y = 0f;
            float yaw = to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : st.Yaw;
            bool night = DayNight.Hours < 6.5f || DayNight.Hours > 19.5f;
            if (masters.TryGetValue(st.id, out var old) && old && old.Alive)
            {
                var sm = old.GetComponent<MadMax.World.Stationmaster>();
                if (sm) sm.service = s;
                old.home = p; old.homeYaw = yaw;
                return;
            }
            var prof = NpcProfile.Make("railsm:" + st.id, NpcRole.Shopkeeper, game.World.seed * 3 + st.id * 7919);
            prof.title = "STATIONMASTER";
            prof.tool = night ? "tool_lantern" : null;
            var n = MadMax.Npc.Npc.Spawn(prof, p, yaw, null, game.propMaterial);
            n.homeRadius = 0.6f;
            n.mode = MadMax.Npc.Npc.Mode.Stand;
            MadMax.World.Stationmaster.Attach(n, s, mat, night);
            masters[st.id] = n;
            loose.Add(n);
        }

        /// <summary>The people a service put on the platform get back on (or walk off into town if the player is
        /// talking to them / close: they are left to fold away with distance).</summary>
        public void ClearPlatform(TrainService s)
        {
            if (!folk.TryGetValue(s, out var list)) return;
            foreach (var n in list)
            {
                if (!n) continue;
                bool near = game && game.Player && (n.transform.position - game.Player.transform.position).sqrMagnitude < 36f;
                if (near || !n.Alive || n.Hostile) loose.Add(n); else Destroy(n.gameObject);
            }
            folk.Remove(s);
        }

        /// <summary>A knocked-out train's crew: raiders out of an armoured train come out fighting, a trader crew runs.</summary>
        public void Crew(TrainService s, Train t, bool armoured)
        {
            var loco = t.Loco;
            var at = loco ? loco.transform.position : t.HeadPoint;
            var side = loco ? loco.transform.right : Vector3.right;
            var fwd = loco ? loco.transform.forward : Vector3.forward;
            for (int k = 0; k < (armoured ? 3 : 2); k++)
            {
                var prof = NpcProfile.Make("railcrew:" + s.id + ":" + DayNight.Day + ":" + k, armoured ? NpcRole.Raider : NpcRole.Wanderer, game.World.seed + s.seed + k * 977);
                var p = at + side * (2.6f + k * 0.8f) * (k % 2 == 0 ? 1f : -1f) + fwd * (k - 1) * 2f;
                var t2 = DeformableTerrain.Instance;
                if (t2) p.y = t2.Height(p.x, p.z) + 0.05f;
                var n = MadMax.Npc.Npc.Spawn(prof, p, 0f, null, game.propMaterial);
                if (armoured) { n.aggro = true; n.mode = MadMax.Npc.Npc.Mode.Fight; }
                else n.Scare(30f);
                loose.Add(n);
            }
            if (armoured) TownNews.Post("THE " + s.name + " WAS STOPPED ON THE LINE. ITS CREW TOOK TO THE SAND", at);
        }

        // ------------------------------------------------------------------ yards, discovery, theft

        void Yards(Vector3 focus)
        {
            foreach (var st in Net.stations)
            {
                if (yards.Contains(st.id) || (st.pos - new Vector2(focus.x, focus.z)).sqrMagnitude > 450f * 450f) continue;
                yards.Add(st.id);
                var r = new System.Random(game.World.seed * 13 + st.id * 7919);
                if (st.tier == StationTier.Terminal)
                {
                    var sidings = new List<RailLine>();
                    foreach (var l in Net.lines) if (!l.dead && l.kind == TrackKind.Siding && (new Vector2(l.pts[0].x, l.pts[0].z) - st.pos).magnitude < st.half + 80f) sidings.Add(l);
                    sidings.Sort((x, y) => x.id.CompareTo(y.id));
                    if (sidings.Count > 0) SpawnFree(new[] { CarKind.Flatcar, r.Next(2) == 0 ? CarKind.Hopper : CarKind.ShackWagon }, sidings[0], sidings[0].Length - 18f, true, "YARD WAGONS", false, 0f);
                    if (sidings.Count > 1 && r.NextDouble() < 0.6) SpawnFree(new[] { CarKind.MakeshiftLoco }, sidings[1], sidings[1].Length - 9f, true, "SHED LOCO", false, 0f);
                }
                else if (st.tier == StationTier.Halt && r.NextDouble() < 0.4)
                {
                    foreach (int li in st.platformLines)
                    {
                        var l = Net.lines[li];
                        if (l.dead || routeLines.Contains(l.id)) continue;
                        SpawnFree(new[] { CarKind.Draisine }, l, 6f, true, "DRAISINE", false, 0f);
                        break;
                    }
                }
                else if (st.tier == StationTier.WaterStop && r.NextDouble() < 0.25)
                {
                    var l = Net.NearestTrack(st.World(0f, 0f), 6f, out float s, out _);
                    if (l != null && !routeLines.Contains(l.id)) SpawnFree(new[] { CarKind.Draisine }, l, s, true, "DRAISINE", false, 0f);
                }
            }
        }

        void Discover()
        {
            if (!game.Player) return;
            var p = game.FocusPos;
            foreach (var st in Net.stations)
            {
                if ((st.pos - new Vector2(p.x, p.z)).sqrMagnitude > 90f * 90f) continue;
                string key = "rail:" + st.id;
                if (!game.Discovered.Add(key)) continue;
                string name = st.Name(game.World);
                game.Toast(name);
                Journal.Add("FOUND", name + (st.tier == StationTier.Terminal ? " (RAIL TERMINAL)" : st.tier == StationTier.Junction ? " (RAIL JUNCTION)" : st.tier == StationTier.WaterStop ? " (WATER STOP)" : ""));
            }
        }

        void Theft()
        {
            foreach (var s in services)
            {
                if (!s.live) continue;
                foreach (var c in s.live.cars)
                {
                    if (!c || !c.Loot || c.Loot.key == null || !Lootable.Searched.Contains(c.Loot.key) || !notedTheft.Add(c.Loot.key)) continue;
                    Factions.Shift(s.faction, -3);
                    game.Toast(s.armoured ? "THE CREW SEES YOU AT THE LOCKER" : "THE GUILD CREW SAW THAT");
                    if (s.armoured) s.aggroUntil = Time.time + 300f;
                }
            }
        }

        // ------------------------------------------------------------------ free trains

        /// <summary>A free train of <paramref name="kinds"/> centred at distance <paramref name="s"/> on a line (kept off
        /// its ends); null when it doesn't fit.</summary>
        public Train SpawnFree(IList<CarKind> kinds, RailLine line, float s, bool facingFwd, string name, bool owned, float fuel)
        {
            if (line == null || line.dead) return null;
            var path = new RailPath();
            path.Begin(line, true, 0f);
            float len = 0f; foreach (var k in kinds) len += TrainDesigns.Get(k).length + 0.5f;
            var flips = new bool[kinds.Count];
            for (int i = 0; i < flips.Length; i++) flips[i] = !facingFwd;
            var t = Train.Create(Net, kinds, flips, path, s + len * 0.5f, mat, glow, line.id * 31 + (int)s, name);
            t.owned = owned; t.fuel = fuel;
            t.Advance(0f);
            float hi = t.path.End - 0.6f, lo = t.path.Start + t.Length + 0.6f;
            if (lo > hi) { Destroy(t.gameObject); return null; }
            t.head = Mathf.Clamp(t.head, lo, hi);
            t.Snap();
            var hp = t.HeadPoint;
            t.KeyLoot("rail:free:" + Mathf.RoundToInt(hp.x) + "," + Mathf.RoundToInt(hp.z));
            return t;
        }

        /// <summary>Who stands on the track near a point (placing a new car).</summary>
        public static bool Occupied(Vector3 p, float r)
        {
            foreach (var t in Train.All) foreach (var c in t.cars) if (c && (c.transform.position - p).sqrMagnitude < (r + c.def.Half) * (r + c.def.Half)) return true;
            return false;
        }

        // ------------------------------------------------------------------ save

        public void Save(List<string> o)
        {
            var ci = CultureInfo.InvariantCulture;
            string V(Vector3 v) => v.x.ToString("0.###", ci) + "," + v.y.ToString("0.###", ci) + "," + v.z.ToString("0.###", ci);
            foreach (var l in Net.lines)
            {
                if (l.dead || !l.built) continue;
                var sb = new System.Text.StringBuilder("b|").Append(l.scrap ? 1 : 0).Append('|');
                for (int i = 0; i < l.pts.Length; i++) { if (i > 0) sb.Append(';'); sb.Append(V(l.pts[i])); }
                o.Add(sb.ToString());
            }
            foreach (var n in Net.nodes) if (n.Switch) o.Add("w|" + V(n.pos) + "|" + n.state);
            foreach (var s in services) o.Add("c|" + s.id + "|" + s.clock.ToString("R", ci) + "|" + (s.ended ? 1 : 0) + "|" + s.endDay);
            foreach (int y in yards) o.Add("y|" + y);
            foreach (var t in Train.All)
            {
                if (t.service != null || t.cars.Count == 0) continue;
                var kinds = new System.Text.StringBuilder(); var flips = new System.Text.StringBuilder(); var hp = new System.Text.StringBuilder();
                foreach (var c in t.cars) { if (!c) continue; if (kinds.Length > 0) { kinds.Append(','); flips.Append(','); hp.Append(','); } kinds.Append((int)c.def.kind); flips.Append(c.flipped ? 1 : 0); hp.Append(Mathf.RoundToInt(c.hp)); }
                var hpnt = t.path.At(t.head - 0.5f, out var tan);
                o.Add("t|" + kinds + "|" + flips + "|" + V(hpnt) + "|" + V(tan) + "|" + t.fuel.ToString("0.#", ci) + "|" + (t.owned ? 1 : 0) + "|" + hp + "|" + t.trainName + (t.Derailed ? "|1" : ""));
            }
        }

        /// <summary>Rebuild player track (in the order it was laid), points, clocks, yards and free trains.</summary>
        public void Load(List<string> lines)
        {
            if (lines == null) return;
            var ci = CultureInfo.InvariantCulture;
            Vector3 P(string s) { var p = s.Split(','); return new Vector3(float.Parse(p[0], ci), float.Parse(p[1], ci), float.Parse(p[2], ci)); }
            foreach (var line in lines)
            {
                var f = line.Split('|');
                try
                {
                    switch (f[0])
                    {
                        case "b":
                        {
                            var pts = new List<Vector3>();
                            foreach (var q in f[2].Split(';')) pts.Add(P(q));
                            if (pts.Count >= 2) Net.AddBuilt(pts, f[1] == "1");
                            break;
                        }
                        case "w": { var n = Net.NodeNear(P(f[1]), 0.6f); if (n != null && n.Switch) n.state = int.Parse(f[2]); break; }
                        case "c":
                        {
                            int id = int.Parse(f[1]);
                            if (id < 0 || id >= services.Count) break;
                            var s = services[id];
                            s.clock = double.Parse(f[2], ci); s.ended = f[3] == "1"; s.endDay = int.Parse(f[4]);
                            if (s.live) { Fold(s); }
                            break;
                        }
                        case "y": yards.Add(int.Parse(f[1])); break;
                        case "t":
                        {
                            var kinds = new List<CarKind>(); var flips = new List<bool>();
                            foreach (var k in f[1].Split(',')) kinds.Add((CarKind)int.Parse(k));
                            foreach (var k in f[2].Split(',')) flips.Add(k == "1");
                            var hpnt = P(f[3]); var tan = P(f[4]);
                            var l = Net.NearestTrack(hpnt, 4f, out float s, out _);
                            if (l == null) break;
                            l.At(s, out var lt);
                            bool fwd = Vector3.Dot(lt, tan) >= 0f;
                            var path = new RailPath();
                            path.Begin(l, fwd, 0f);
                            float head = (fwd ? s : l.Length - s) + 0.5f;
                            var t = Train.Create(Net, kinds, flips, path, head, mat, glow, l.id * 31, f.Length > 8 ? f[8] : "TRAIN");
                            t.fuel = float.Parse(f[5], ci); t.owned = f[6] == "1";
                            var hps = f[7].Split(',');
                            for (int i = 0; i < t.cars.Count && i < hps.Length; i++) t.cars[i].hp = float.Parse(hps[i], ci);
                            t.Advance(0f);
                            if (f.Length > 9 && f[9] == "1") t.LoadDerailed(Mathf.RoundToInt(hpnt.x * 7f + hpnt.z));
                            t.Snap();
                            t.KeyLoot("rail:free:" + Mathf.RoundToInt(hpnt.x) + "," + Mathf.RoundToInt(hpnt.z));
                            break;
                        }
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("[rail] bad save line: " + line + " (" + e.Message + ")"); }
            }
        }

        /// <summary>Remove every free train (new game / load replaces them).</summary>
        public void ClearFree()
        {
            for (int i = Train.All.Count - 1; i >= 0; i--) { var t = Train.All[i]; if (t && t.service == null) Destroy(t.gameObject); }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
