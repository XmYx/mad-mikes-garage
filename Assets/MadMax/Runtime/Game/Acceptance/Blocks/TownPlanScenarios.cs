using System.Collections;
using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Town plans (coherent towns): plan data checks on the start town and a city, then a visit with captures.</summary>
    public static class TownPlanScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new TownPlanSmoke(); }
    }

    /// <summary>TOWN PLAN: every lot fronts a street, the streets form one network, the highways reach the plan's
    /// hub, buildings stand on their lots, street cells sample as paved road with town features; in the start town
    /// and a city the street furniture, lamps and people turn up, a city has its gang hangout. Overhead captures.</summary>
    class TownPlanSmoke : Scenario
    {
        public override string Id => "towns.plan_smoke";
        public override float Timeout => 120f;

        static void Data(ScenarioContext c, WorldGen w, Settlement st)
        {
            var p = st.plan;
            string name = st.kind + " " + st.index;
            if (!c.Check(p != null && p.streets.Count > 0 && p.lots.Count > 0, $"{name}: a plan ({(p != null ? p.streets.Count + " streets, " + p.lots.Count + " lots, " + p.spots.Count + " spots" : "none")})")) return;
            int far = 0;
            foreach (var lot in p.lots)
            {
                var f = lot.FrontMid + lot.front * 0.5f;
                bool near = false;
                for (int k = 0; k < 12 && !near; k++) { var q = f + lot.front * (k * 0.5f); if (p.Query(q, out var h) && (h.feature == TownPlan.FSidewalk || h.feature == TownPlan.FKerb || h.feature == TownPlan.FStreet || h.feature == TownPlan.FPaint || h.road)) near = true; }
                if (!near && !lot.Special) far++;
            }
            c.Check(far == 0, $"{name}: every lot fronts a street ({far} do not)");
            // one network: streets joined by junctions or by an end on another carriageway
            var parent = new int[p.streets.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            for (int i = 0; i < p.streets.Count; i++)
            for (int j = i + 1; j < p.streets.Count; j++)
            {
                var a = p.streets[i]; var b = p.streets[j];
                bool joined = (a.juncA >= 0 && (a.juncA == b.juncA || a.juncA == b.juncB)) || (a.juncB >= 0 && (a.juncB == b.juncA || a.juncB == b.juncB));
                foreach (var e in new[] { a.a, a.b, b.a, b.b })
                {
                    var s = e == a.a || e == a.b ? b : a;
                    var r = e - s.a; float t = Mathf.Clamp(Vector2.Dot(r, s.dir), 0f, s.len);
                    if ((e - (s.a + s.dir * t)).magnitude <= s.half + 0.5f) joined = true;
                }
                if (joined) parent[Find(i)] = Find(j);
            }
            var roots = new HashSet<int>();
            for (int i = 0; i < parent.Length; i++) roots.Add(Find(i));
            c.Check(roots.Count == 1, $"{name}: the streets form one network ({roots.Count} pieces)");
            int roads = 0, reach = 0;
            foreach (var road in w.roads.roads)
            {
                var a = road.points[0]; var b = road.points[road.points.Count - 1];
                bool here = (new Vector2(a.x, a.z) - st.pos).magnitude < p.radius || (new Vector2(b.x, b.z) - st.pos).magnitude < p.radius;
                if (!here) continue;
                roads++;
                foreach (var e in new[] { a, b }) if (p.Query(p.Local(e.x, e.z), out var h) && h.road && h.paved) { reach++; break; }
            }
            c.Check(reach == roads, $"{name}: {reach}/{roads} highways end on the plan's streets");
            int bad = 0, buildings = 0;
            foreach (var s in p.spots)
            {
                if (s.kind != SpotKind.Prop || s.id == null) continue;
                if (!(s.id.StartsWith("Shop") || s.id.StartsWith("BrickHouse") || s.id.StartsWith("Tower") || s.id.StartsWith("Warehouse") || s.id.StartsWith("Farmhouse"))) continue;
                buildings++;
                if (!p.Query(p.Local(s.pos.x, s.pos.y), out var h) || h.lot < 0) bad++;
            }
            c.Check(buildings > 0 && bad == 0, $"{name}: {buildings} buildings, {bad} off their lots");
            var st0 = p.streets[0];
            var mid = p.World(st0.a + st0.dir * (st0.len * 0.5f) + new Vector2(-st0.dir.y, st0.dir.x) * 0.6f);
            var smp = w.Sample(mid.x, mid.y);
            c.Check(smp.road > 0.5f && (smp.paved || !st0.paved) && (smp.feature == TownPlan.FStreet || smp.feature == TownPlan.FPaint || smp.feature == TownPlan.FYellow || smp.feature == 0),
                    $"{name}: a street cell samples as road (road {smp.road:0.0}, paved {smp.paved}, feature {smp.feature})");
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var w = g.World;
            Settlement start = w.settlements.Count > 0 ? w.settlements[0] : null, city = null;
            foreach (var st in w.settlements) if (st.kind == Biome.City && st.plan != null) { city = st; break; }
            if (start == null) { c.Block("no settlements"); yield break; }
            Data(c, w, start);
            if (city != null) { Data(c, w, city); c.Check(city.plan.spots.Exists(s => s.kind == SpotKind.Gang), "the city has a gang hangout"); }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(11f);
            var rig = g.cameraRig;
            foreach (var st in new[] { start, city })
            {
                if (st == null) continue;
                var p = st.plan;
                Vector2 at = p.World(new Vector2(0f, 0f));
                foreach (var lot in p.lots) if (lot.use == LotUse.Plaza || lot.use == LotUse.Market || lot.use == LotUse.Green) { at = p.World(lot.At(lot.Width * 0.3f, 1.2f)); break; }
                g.Player.Teleport(new Vector3(at.x, w.Sample(at.x, at.y).height + 0.3f, at.y), 0f);
                c.Fixture($"teleported to {st.kind} {st.index}");
                if (rig) rig.mode = ViewMode.Overhead;
                yield return new WaitForSeconds(6f);
                var here = new Vector3(at.x, 0f, at.y);
                int benches = 0, lamps = 0, people = 0;
                foreach (var d in Object.FindObjectsByType<DestructibleVoxels>(FindObjectsSortMode.None))
                    if (d && d.TemplateId != null && (d.TemplateId.StartsWith("Bench") || d.TemplateId.StartsWith("Bin")) && Flat(d.transform.position - here) < 80f) benches++;
                foreach (var l in Object.FindObjectsByType<WorldLamp>(FindObjectsSortMode.None)) if (Flat(l.transform.position - here) < 80f) lamps++;
                foreach (var n in MadMax.Npc.Npc.All) if (n && n.Alive && Flat(n.transform.position - here) < 110f) people++;
                int birds = MadMax.Animals.AnimalDirector.Instance ? MadMax.Animals.AnimalDirector.Instance.TownAnimals(here, 120f) : 0;
                c.Metric(st.kind + "_benches_bins", benches, "count"); c.Metric(st.kind + "_lamps", lamps, "count"); c.Metric(st.kind + "_people", people, "count"); c.Metric(st.kind + "_town_animals", birds, "count");
                c.Check(benches > 0 && lamps > 0, $"{st.kind}: street furniture ({benches} benches/bins) and lamps ({lamps}) stand round the square");
                c.Check(people > 0, $"{st.kind}: {people} people about");
                c.Screenshot("town_" + st.kind.ToString().ToLowerInvariant());
                yield return null;
            }
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
