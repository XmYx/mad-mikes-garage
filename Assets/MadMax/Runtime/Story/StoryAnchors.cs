using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    /// <summary>Binds the campaign's places to a generated world, deterministically from its seed (storyline §13): the
    /// convoy wreck and the stranded car in the start yard, Nell's roadside stop down the road, the nearest town, the
    /// garage at the bend, the relay mast nearest that town, the depot (a bunker) and the dispatch city. A place that
    /// cannot be found is reported by <see cref="Validate"/> instead of silently dropping a chapter.</summary>
    public static partial class StoryAnchors
    {
        static readonly Dictionary<string, Vector3> at = new Dictionary<string, Vector3>();
        static readonly Dictionary<string, float> yaw = new Dictionary<string, float>();
        static WorldGen bound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { at.Clear(); yaw.Clear(); bound = null; }

        public static bool Has(string key) => at.ContainsKey(key);
        public static Vector3 Get(string key) => at.TryGetValue(key, out var p) ? p : Vector3.zero;
        public static float Yaw(string key) => yaw.TryGetValue(key, out var y) ? y : 0f;
        public static IEnumerable<KeyValuePair<string, Vector3>> AllBound => at;

        public static void Bind(WorldGen world)
        {
            if (bound == world && at.Count > 0) return;
            bound = world;
            at.Clear(); yaw.Clear();
            world.Yard(out var o, out var along, out var side);
            float h(Vector3 p) => world.Sample(p.x, p.z).height;
            void Set(string k, Vector3 p, float y = 0f) { p.y = h(p); at[k] = p; yaw[k] = y; }
            float roadYaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;

            // the first town: the nearest settlement to the start yard (the start town)
            Settlement town = null; float td = float.MaxValue;
            foreach (var st in world.settlements) { float d = Vector2.Distance(st.pos, new Vector2(o.x, o.z)); if (d < td) { td = d; town = st; } }
            if (town != null) Set("town1", new Vector3(town.pos.x, 0f, town.pos.y));

            // the convoy wreck: out on a road 450-900 m from the town
            if (!Roadside(world, o, 450f, 900f, 14f, p => Clear(town, p, 220f), out var wreck, out var wf)
                && !Roadside(world, o, 350f, 1600f, 20f, p => Clear(town, p, 150f), out wreck, out wf)) { wreck = o + along * 22f - side * 24f; wf = roadYaw; }
            Set("wreck", wreck, wf + 70f);
            var q = Quaternion.Euler(0f, wf, 0f);
            Set("satchel", wreck + q * new Vector3(3.2f, 0f, -1.5f));
            Set("badge", wreck + q * new Vector3(-1.5f, 0f, -3.5f));
            if (Roadside(world, wreck, 16f, 50f, 30f, p => Clear(town, p, 150f), out var car, out var cf)) Set("car", car, cf + 15f);
            var toTown = town != null ? new Vector3(town.pos.x - wreck.x, 0f, town.pos.y - wreck.z).normalized : along;

            // Nell's stop: roadside ground 120-320 m from the wreck, on the way to town
            if (Roadside(world, wreck, 120f, 320f, 16f, p => Vector3.Dot((p - wreck).normalized, toTown) > 0.2f && Clear(town, p, 120f), out var nell, out var ny))
                Set("nell", nell, ny);
            // the garage at the bend: roadside ground 250-700 m out, clear of the town and of Nell's stop
            if (Roadside(world, wreck, 250f, 700f, 22f, p => Clear(town, p, 90f) && (!at.ContainsKey("nell") || Vector3.Distance(p, at["nell"]) > 150f), out var gar, out var gy))
            {
                Set("garage", gar, gy);
                Set("garage_yard", gar + Quaternion.Euler(0f, gy, 0f) * new Vector3(0f, 0f, 7.5f), gy + 180f);   // in front of the doorway
            }

            // side-quest givers at the edge of the first town (open ground just outside its buildings)
            if (town != null)
            {
                int found = 0;
                for (float rr = town.radius + 10f; rr <= town.radius + 90f && found < 2; rr += 10f)
                    for (int k = 0; k < 24 && found < 2; k++)
                    {
                        float ang = (k * 15f + 7f) * Mathf.Deg2Rad;
                        var pp = new Vector3(town.pos.x + Mathf.Sin(ang) * rr, 0f, town.pos.y + Mathf.Cos(ang) * rr);
                        if (!Open(world, pp) || (found == 1 && Vector3.Distance(pp, at["una"]) < 60f)) continue;
                        float h0 = world.Sample(pp.x, pp.z).height, spread = 0f;
                        foreach (var cc in new[] { new Vector3(5f, 0f, 5f), new Vector3(-5f, 0f, 5f), new Vector3(5f, 0f, -5f), new Vector3(-5f, 0f, -5f) })
                            spread = Mathf.Max(spread, Mathf.Abs(world.Sample(pp.x + cc.x, pp.z + cc.z).height - h0));
                        if (spread > 1f) continue;
                        var toC = new Vector3(town.pos.x - pp.x, 0f, town.pos.y - pp.z);
                        Set(found == 0 ? "una" : "gus", pp, Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg);
                        found++;
                    }
            }

            // A2 inside the first town: the fuel pump, a diner (shop), the freight office (a house) and, S03, the chapel
            if (town != null)
            {
                var blds = new List<(string id, Vector2 pos, float yaw)>();
                BiomeProps.Buildings(world, town, blds);
                Vector3 Front((string id, Vector2 pos, float yaw) b, float d)
                {
                    var f = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward;
                    return new Vector3(b.pos.x, 0f, b.pos.y) + f * d;
                }
                var used = new HashSet<int>();
                int Pick(System.Func<string, bool> want)
                {
                    for (int i = 0; i < blds.Count; i++) if (!used.Contains(i) && want(blds[i].id)) { used.Add(i); return i; }
                    return -1;
                }
                int pump = Pick(id => id.StartsWith("GasPump")), diner = Pick(id => id.StartsWith("Shop")),
                    office = Pick(id => id.StartsWith("BrickHouse") || id.StartsWith("Shop") || id.StartsWith("Farmhouse")),
                    chapel = Pick(id => id.StartsWith("BrickHouse") || id.StartsWith("Farmhouse") || id.StartsWith("Shack") || id.StartsWith("Tower"));
                var tc = new Vector3(town.pos.x, 0f, town.pos.y);
                Set("a2_pump", pump >= 0 ? Front(blds[pump], 2.5f) : tc + new Vector3(8f, 0f, 0f));
                if (diner >= 0) Set("a2_diner", Front(blds[diner], 6.5f), blds[diner].yaw + 180f); else Set("a2_diner", tc + new Vector3(-10f, 0f, 6f));
                if (office >= 0) Set("a2_office", Front(blds[office], 6.5f), blds[office].yaw + 180f); else Set("a2_office", tc + new Vector3(6f, 0f, -10f));
                if (chapel >= 0) Set("chapel", Front(blds[chapel], 7.5f), blds[chapel].yaw + 180f); else Set("chapel", tc + new Vector3(-12f, 0f, -8f));
                // Len's repair stall at the town's edge on a road, the tyre marks beside it
                if (Roadside(world, tc, town.radius + 15f, town.radius + 140f, 14f, p => Clear(town, p, 8f) && Away(p, 50f, "una", "gus"), out var stall, out var sy))
                {
                    Set("a2_stall", stall, sy);
                    Set("a2_tracks", stall + Quaternion.Euler(0f, sy, 0f) * new Vector3(3f, 0f, 4f));
                }
                // S03: the hearse seized on the road out of town
                if (Roadside(world, tc, town.radius + 150f, town.radius + 500f, 14f, p => Clear(town, p, 100f) && Away(p, 120f, "wreck", "nell", "garage", "a2_stall"), out var hearse, out var hy))
                    Set("hearse", hearse, hy + 90f);
                // S06: Jo Kettle's farm: level ground for a trench, a garden and an excavator
                for (float rr = town.radius + 60f; rr <= town.radius + 360f && !at.ContainsKey("jo"); rr += 20f)
                    for (int k = 0; k < 36 && !at.ContainsKey("jo"); k++)
                    {
                        float ang = (k * 10f + 3f) * Mathf.Deg2Rad;
                        var pp = new Vector3(town.pos.x + Mathf.Sin(ang) * rr, 0f, town.pos.y + Mathf.Cos(ang) * rr);
                        if (!Open(world, pp) || !Clear(town, pp, 40f) || !Away(pp, 90f, "wreck", "nell", "garage", "hearse", "a2_stall", "una", "gus") || !Level(world, pp, 13f, 1.4f)) continue;
                        bool dry = true;
                        for (float x = -13f; x <= 13f && dry; x += 6.5f) for (float z = -13f; z <= 13f && dry; z += 6.5f) if (!Open(world, pp + new Vector3(x, 0f, z))) dry = false;
                        if (!dry) continue;
                        Set("jo", pp, ang * Mathf.Rad2Deg + 180f);
                    }
                if (at.ContainsKey("jo"))
                {
                    var jq = Quaternion.Euler(0f, yaw["jo"], 0f); var j = at["jo"];
                    Set("jo_t1", j + jq * new Vector3(-6f, 0f, -6f)); Set("jo_t2", j + jq * new Vector3(0f, 0f, -6f)); Set("jo_t3", j + jq * new Vector3(6f, 0f, -6f));
                    Set("jo_garden", j + jq * new Vector3(0f, 0f, 7f));
                    Set("jo_digger", j + jq * new Vector3(-10f, 0f, 3f), yaw["jo"] + 90f);
                }
            }

            // places the individual quests add (Anchors_<id> hooks); they may add clearings
            extraClearings.Clear();
            AnchorsExtra(world, town);

            // clear ground for the scenes (wild props skip these circles)
            foreach (var (k, r) in new[] { ("wreck", 26f), ("car", 14f), ("nell", 18f), ("garage", 16f), ("una", 12f), ("gus", 8f), ("a2_stall", 9f), ("hearse", 12f), ("jo", 22f) })
                if (at.ContainsKey(k)) world.Reserve(new Vector3(at[k].x, at[k].z, r));
            foreach (var (k, r) in extraClearings) if (at.ContainsKey(k)) world.Reserve(new Vector3(at[k].x, at[k].z, r));

            // the relay: the radio mast nearest the first town
            float md = float.MaxValue;
            foreach (var m in BiomeProps.Landmarks(world))
            {
                if (m.kind != BiomeProps.MarkKind.Mast || town == null) continue;
                float d = Vector2.Distance(m.pos, town.pos);
                if (d < md) { md = d; Set("relay", new Vector3(m.pos.x, 0f, m.pos.y), m.yaw); }
            }

            // the relay generator: open ground beside the mast
            if (at.ContainsKey("relay"))
            {
                var rp = at["relay"]; bool gset = false;
                for (int k = 0; k < 16 && !gset; k++)
                {
                    var gp = rp + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * 9f;
                    if (Open(world, gp)) { Set("relay_gen", gp, k * 22.5f + 180f); gset = true; }
                }
                if (!gset) Set("relay_gen", rp + new Vector3(8f, 0f, 0f));
            }

            // the dispatch city: the city settlement farthest from the start (a late destination)
            Settlement city = null; float cd = -1f;
            foreach (var st in world.settlements)
                if (st.kind == Biome.City && st != town) { float d = Vector2.Distance(st.pos, new Vector2(o.x, o.z)); if (d > cd) { cd = d; city = st; } }
            if (city != null) Set("dispatch", new Vector3(city.pos.x, 0f, city.pos.y));

            // the depot: the bunker nearest the midpoint between the start and the dispatch city
            var mid2 = city != null ? (new Vector2(o.x, o.z) + city.pos) * 0.5f : new Vector2(o.x, o.z);
            float bd = float.MaxValue;
            var c0 = new Vector2Int(Mathf.FloorToInt(mid2.x / 320f), Mathf.FloorToInt(mid2.y / 320f));
            for (int dx = -9; dx <= 9; dx++)
            for (int dz = -9; dz <= 9; dz++)
            {
                var site = world.SiteIn(new Vector2Int(c0.x + dx, c0.y + dz));
                if (site == null || site.kind != SiteKind.Bunker) continue;
                float d = Vector2.Distance(site.pos, mid2);
                if (d < bd) { bd = d; Set("depot", new Vector3(site.pos.x, 0f, site.pos.y), site.rot * 90f); }
            }
            // (the depot's gate, service hatch and store are laid out from it after the fallback below)
            // no bunker there: the nearest bunker to the start, else the nearest site of any kind
            for (int pass = 0; pass < 2 && !at.ContainsKey("depot"); pass++)
            {
                var o2 = new Vector2Int(Mathf.FloorToInt(o.x / 320f), Mathf.FloorToInt(o.z / 320f));
                for (int dx = -16; dx <= 16; dx++)
                for (int dz = -16; dz <= 16; dz++)
                {
                    var site = world.SiteIn(new Vector2Int(o2.x + dx, o2.y + dz));
                    if (site == null || (pass == 0 && site.kind != SiteKind.Bunker)) continue;
                    float d = Vector2.Distance(site.pos, new Vector2(o.x, o.z));
                    if (d < bd) { bd = d; Set("depot", new Vector3(site.pos.x, 0f, site.pos.y), site.rot * 90f); }
                }
            }
            if (at.ContainsKey("depot"))
            {
                var dq = Quaternion.Euler(0f, yaw["depot"], 0f); var dp = at["depot"];
                Set("depot_gate", dp + dq * new Vector3(0f, 0f, 16f), yaw["depot"]);
                Set("depot_tunnel", dp + dq * new Vector3(14f, 0f, -6f));
                Set("depot_store", dp, yaw["depot"]);
            }
        }

        static bool Open(WorldGen w, Vector3 p)
        {
            var s = w.Sample(p.x, p.z);
            return s.roadDist >= 6f && float.IsNaN(s.water) && s.feature == 0 && w.SettlementAt(p.x, p.z) == null && w.YardWeight(p.x, p.z) <= 0f
                   && w.SiteAt(p.x, p.z) == null && !w.RiverAt(p.x, p.z, out _, out _, out _);
        }

        static readonly List<(string, float)> extraClearings = new List<(string, float)>();
        /// <summary>Keep wild props off a quest's place (call from an Anchors_&lt;id&gt; hook after setting it).</summary>
        static void Clearing(string key, float radius) => extraClearings.Add((key, radius));

        static bool Away(Vector3 p, float d, params string[] keys)
        {
            foreach (var k in keys) if (at.TryGetValue(k, out var q) && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) < d) return false;
            return true;
        }

        static bool Level(WorldGen w, Vector3 p, float half, float tol)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float x = -half; x <= half; x += half / 2f)
            for (float z = -half; z <= half; z += half / 2f)
            {
                float h = w.Sample(p.x + x, p.z + z).height;
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            return hi - lo <= tol;
        }

        static bool Clear(Settlement town, Vector3 p, float margin) =>
            town == null || Vector2.Distance(new Vector2(p.x, p.z), town.pos) > town.radius + margin;

        /// <summary>The open, fairly level spot beside a road nearest <paramref name="from"/> between <paramref name="min"/>
        /// and <paramref name="max"/> m (deterministic: road points in order of distance), a few metres off the verge
        /// (<paramref name="reach"/> caps the offset); faces the road.</summary>
        static bool Roadside(WorldGen w, Vector3 from, float min, float max, float reach, System.Func<Vector3, bool> ok, out Vector3 spot, out float face)
        {
            var cands = new List<(float d, Vector3 q, Vector3 dir)>();
            foreach (var road in w.roads.roads)
                for (int i = 0; i + 1 < road.points.Count; i++)
                {
                    var a = road.points[i]; var b = road.points[i + 1];
                    var seg = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    float len = seg.magnitude;
                    if (len < 0.01f) continue;
                    for (float t = 0f; t < len; t += 12f)
                    {
                        var q = new Vector3(a.x, 0f, a.z) + seg * (t / len);
                        float d = new Vector2(q.x - from.x, q.z - from.z).magnitude;
                        if (d >= min && d <= max) cands.Add((d, q, seg / len));
                    }
                }
            cands.Sort((x, y) => x.d.CompareTo(y.d));
            foreach (var (d, q, dir) in cands)
            {
                var across = new Vector3(dir.z, 0f, -dir.x);
                foreach (float off in new[] { 10f, -10f, 14f, -14f, 20f, -20f })
                {
                    if (Mathf.Abs(off) > reach) continue;
                    var p = q + across * off;
                    if (!Open(w, p) || !ok(p)) continue;
                    float h0 = w.Sample(p.x, p.z).height, spread = 0f;
                    foreach (var c in new[] { new Vector3(4f, 0f, 4f), new Vector3(-4f, 0f, 4f), new Vector3(4f, 0f, -4f), new Vector3(-4f, 0f, -4f) })
                        spread = Mathf.Max(spread, Mathf.Abs(w.Sample(p.x + c.x, p.z + c.z).height - h0));
                    if (spread > 1.2f) continue;
                    spot = p;
                    var f = q - p;
                    face = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    return true;
                }
            }
            spot = Vector3.zero; face = 0f;
            return false;
        }

        /// <summary>Problems with this world's binding (empty = every anchor the catalogue uses is placed sensibly).</summary>
        public static List<string> Validate()
        {
            var bad = new List<string>();
            foreach (var k in StoryLibrary.Anchors) if (!Has(k)) bad.Add("anchor " + k + " not placed");
            if (Has("wreck") && Has("nell") && Vector3.Distance(Get("wreck"), Get("nell")) > 400f) bad.Add("Nell's stop is more than 400 m from the wreck");
            if (Has("wreck") && Has("car") && Vector3.Distance(Get("wreck"), Get("car")) > 60f) bad.Add("the stranded car is more than 60 m from the wreck");
            if (Has("wreck") && Has("town1") && Vector3.Distance(Get("wreck"), Get("town1")) < 300f) bad.Add("the wreck is inside the first town");
            if (Has("wreck") && Has("town1") && Vector3.Distance(Get("wreck"), Get("town1")) > 3000f) bad.Add("the first town is more than 3 km away");
            return bad;
        }
    }
}
