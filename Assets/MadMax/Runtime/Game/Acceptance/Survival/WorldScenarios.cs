using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q5 (the planet, biomes, towns): the seed-7 world wraps east-west every 9.6 km (the same ground
    /// on both sides of the seam), the date line is open sea, the start lies near 31° N, the poles are much colder and
    /// carry tundra, the habitable land holds several distinct biomes, and every settlement is a village, town or city
    /// reached by the road network. Pure queries on the world generator: deterministic, nothing travels.</summary>
    class WorldPlanet : Scenario
    {
        public override string Id => "world.planet";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var w = c.Game.World;
            if (!c.Check(w != null, "the world generator exists")) yield break;
            float lat0 = WorldGen.Latitude(0f);
            c.Metric("start_latitude", lat0, "deg");
            c.Check(lat0 > 25f && lat0 < 36f, $"the start lies near 31 N ({lat0:0.0})");
            int same = 0, n = 0;
            for (int i = 0; i < 24; i++)
            {
                float x = -2000f + i * 173f, z = -900f + i * 97f;
                n++;
                if (Mathf.Abs(w.ContinentNoise(x, z) - w.ContinentNoise(x + WorldGen.Circumference, z)) < 1e-4f && w.Ocean(x, z) == w.Ocean(x - WorldGen.Circumference, z)) same++;
            }
            c.Check(same == n, $"land and sea repeat one circumference east and west ({same}/{n} samples; the date-line crossing moves you across the open-sea band)");
            c.Check(w.Ocean(WorldGen.HalfX, 0f) && w.Ocean(-WorldGen.HalfX + 50f, 400f), "the date line is open sea");
            float polar = WorldGen.ClimateOffset(WorldGen.ZOfLatitude(80f)), tropic = WorldGen.ClimateOffset(WorldGen.ZOfLatitude(2f));
            c.Metric("climate_offset_80N", polar, "C");
            c.Check(polar < -15f && tropic > 0f, $"the far north is much colder, the equator warmer ({polar:0} / +{tropic:0} C)");
            int tundra = 0;
            for (int i = 0; i < 40; i++) { float x = -3500f + i * 180f; float z = WorldGen.ZOfLatitude(78f); if (!w.Ocean(x, z) && w.BiomeAt(x, z) == Biome.Tundra) tundra++; }
            c.Check(tundra > 0, $"tundra on land near the pole ({tundra} of 40 samples)");
            var biomes = new HashSet<Biome>();
            for (float x = -4000f; x <= 4000f; x += 160f)
                for (float z = WorldGen.ZOfLatitude(-50f); z <= WorldGen.ZOfLatitude(60f); z += 160f)
                    if (w.Habitable(x, z)) biomes.Add(w.NaturalBiome(x, z));
            c.Metric("natural_biomes", biomes.Count, "");
            c.Check(biomes.Count >= 4, "the land holds several biomes: " + string.Join(", ", biomes));
            var setl = w.settlements;
            c.Metric("settlements", setl.Count, "");
            c.Check(setl.Count >= 3, $"settlements exist ({setl.Count})");
            c.Check(setl.All(s => s.kind == Biome.Village || s.kind == Biome.Town || s.kind == Biome.City), "each is a village, town or city: " + string.Join(", ", setl.Select(s => s.kind).Distinct()));
            // the road network never crosses the sea: a town on an island is reached by boat (RoadNetwork.Dry)
            int linked = 0, islands = 0;
            var on = new List<Settlement>(); var off = new List<Settlement>();
            foreach (var s in setl)
            {
                bool near = false;
                foreach (var r in w.roads.roads) { foreach (var p in r.points) if ((new Vector2(p.x, p.z) - s.pos).sqrMagnitude < (s.radius + 60f) * (s.radius + 60f)) { near = true; break; } if (near) break; }
                (near ? on : off).Add(s);
            }
            linked = on.Count;
            foreach (var s in off)
            {
                var other = on.OrderBy(o => (o.pos - s.pos).sqrMagnitude).FirstOrDefault();
                bool sea = false;
                if (other != null) for (int k = 1; k < 64 && !sea; k++) { var q = Vector2.Lerp(s.pos, other.pos, k / 64f); sea = w.Ocean(q.x, q.y); }
                if (sea) islands++;
                c.Note($"settlement {s.index} ({s.kind}) at {s.pos.x:0},{s.pos.y:0} has no road; nearest road town {(other != null ? other.index.ToString() : "-")} at {(other != null ? (other.pos - s.pos).magnitude : 0f):0} m, sea between: {sea}");
            }
            c.Metric("island_settlements", islands, "");
            c.Check(linked + islands == setl.Count, $"roads reach every settlement on the mainland; the rest lie across the sea ({linked} by road, {islands} islands, of {setl.Count})");
            yield return null;
        }
    }

    /// <summary>Roadmap 26 Q5 (sites): the nearest bunker, rock tunnel and airfield of the seed-7 world bind to their 320 m
    /// cells deterministically and, when the player arrives (a disclosed teleport), their pieces stream in: the bunker
    /// is underground (cutaway roof) with loot spots, the tunnel's rock pieces stand, and the airfield's hangar spawns
    /// with its one found aircraft.</summary>
    class WorldSites : Scenario
    {
        public override string Id => "world.sites";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 220f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var w = g.World;
            if (g.Current) { g.Exit(); yield return null; }
            var start = g.Player.transform.position;
            var near = new List<Site>();
            foreach (SiteKind kind in new[] { SiteKind.Bunker, SiteKind.Outcrop, SiteKind.Airfield })
            {
                Site best = null;
                for (float r = 800f; r <= 9600f && best == null; r += 800f)
                {
                    w.SitesNear(start, r, near);
                    best = near.Where(s => s.kind == kind).OrderBy(s => (s.pos - new Vector2(start.x, start.z)).sqrMagnitude).FirstOrDefault();
                }
                if (best == null) { c.Block($"no {kind} within 9.6 km of the start on this seed"); continue; }
                c.Check(w.SiteIn(best.cell) == best && w.SiteAt(best.pos.x, best.pos.y) == best, $"{kind} {best.Key}: bound to its cell, found again at its position");
                string label = kind == SiteKind.Bunker ? "Bunker" : kind == SiteKind.Airfield ? "Hangar" : "RockTunnel";
                var to = new Vector3(best.pos.x + best.reach + 6f, 0f, best.pos.y);
                to.y = DeformableTerrain.Instance.Height(to.x, to.z) + 0.4f;
                g.Player.Teleport(to, -90f);
                c.Fixture($"teleported beside the {kind} at {best.pos.x:0},{best.pos.y:0} ({(best.pos - new Vector2(start.x, start.z)).magnitude:0} m out)");
                var wt = new Waited();
                List<DestructibleVoxels> pieces = null;
                string prefix = "site:" + best.Key + ":";
                yield return SurvivalKit.Until(() => (pieces = DestructibleVoxels.All.Where(d => d && d.TemplateId != null && d.TemplateId.StartsWith(prefix)).ToList()).Count > 0, 60f, wt);
                c.Metric(kind + "_spawn_s", wt.seconds, "s");
                if (!c.Check(wt.ok, $"the {kind}'s pieces stream in ({(pieces != null ? pieces.Count : 0)})")) continue;
                c.Check(pieces.All(p => p.name.StartsWith(label)), $"they are {label} pieces");
                yield return SurvivalKit.GameSeconds(2f);
                if (kind == SiteKind.Bunker)
                {
                    c.Check(pieces.Any(p => p.GetComponent<Subterranean>()), "the bunker is underground (cutaway roof)");
                    var loot = Object.FindObjectsByType<Lootable>(FindObjectsSortMode.None).Count(l => l.table == "bunker" && (l.transform.position - pieces[0].transform.position).sqrMagnitude < 60f * 60f);
                    c.Check(loot > 0, $"with loot spots ({loot})");
                }
                if (kind == SiteKind.Airfield)
                {
                    yield return SurvivalKit.Until(() => g.FoundAircraft.Contains(best.Key), 20f, wt);
                    c.Check(wt.ok, "the hangar's aircraft is placed once");
                    var plane = Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None).FirstOrDefault(v => v.aircraft && (v.transform.position - pieces[0].transform.position).sqrMagnitude < 120f * 120f);
                    c.Check(plane, "an aircraft stands at the airfield" + (plane ? ": " + plane.name : ""));
                }
                c.Screenshot(kind.ToString().ToLowerInvariant());
                yield return null;
            }
        }
    }
}
