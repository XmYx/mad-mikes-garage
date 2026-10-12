using System.Collections;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Starts on the second platform, climbs its real stair core, exits onto an elevated street and walks
    /// a bridge. Probes both promenade levels for floors/headroom and exercises the maintenance stair and dock fit.</summary>
    class CityIndustrialAccess : Scenario
    {
        public override string Id => "city.industrial_access";
        public override float Timeout => 260f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, start = 2, startingKit = 0, survival = false, damage = 0f, weather = 0 };

        static IEnumerator Walk(ScenarioContext c, RollingCity city, Vector3 local, float seconds = 25f)
        {
            var p = c.Game.Player;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                var d = city.DeckPoint(local) - p.transform.position; d.y = 0;
                if (d.magnitude < .3f) break;
                p.AutoWalk = d.normalized;
                yield return null;
            }
            p.AutoWalk = null;
            var at = city.transform.InverseTransformPoint(p.transform.position);
            var delta = at - local;
            c.Check(new Vector2(delta.x, delta.z).magnitude < .65f && Mathf.Abs(delta.y) < .6f,
                $"walked to {local} (actual {at})");
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!c.Check(g.Cities.Count == 2 && g.Cities[1].Ready, "both moving cities are built")) yield break;
            var city = g.Cities[1]; var p = g.Player;
            if (city.Tracked != null)                                                                // the generated city replaced the voxel profile
            {
                c.Note("the second circuit carries the generated tracked city (" + city.Name + "): access is covered by city.living");
                c.Check(city.Plan != null && city.Plan.blocks.Count > 0 && city.Plan.rooms.Count > 0, $"the generated second city has blocks and rooms ({city.Plan.blocks.Count}, {city.Plan.rooms.Count})");
                yield break;
            }
            yield return new WaitForSeconds(.5f);
            c.Check(g.StartingCityIndex == 1 && !g.Current && city.OnDeck(p.transform.position, 4f), "industrial start places the player aboard the second city");
            c.Check(city.Dock == 0 && city.GangwayDown, "industrial start is docked with its gangway lowered");
            c.Check(city.Route.halfWidth == 24f && city.Route.halfLength == 56f, "industrial deck is 48 by 112 metres");
            c.Check(CityDesign.BogieCentres(true).Length == 8, "eight tracked bogies support the larger deck");
            var plan = CityDesign.Layout(g.seed + 31, true);
            c.Check(plan.lots.Count >= 10 && plan.bridges.Count >= 12, $"seeded city has {plan.lots.Count} lots and {plan.bridges.Count} floor connections");
            DayNight.SetHours(12f);
            city.Clock = 12; city.Snap(); city.enabled = false;
            c.Fixture("timetable held at dock 0 during traversal; walking uses the player controller, no flight");
            int clear = 0, samples = 0;
            foreach (float y in new[] { 9.25f, 18.25f })
            foreach (float x in new[] { -4.5f, 4.5f })
            for (float z = -44f; z <= 46f; z += 6f)
            {
                var foot = city.DeckPoint(new Vector3(x, y, z));
                bool floor = Physics.Raycast(foot + Vector3.up * .5f, Vector3.down, out var hit, .8f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<RollingCity>() == city;
                bool room = !Physics.CheckCapsule(foot + Vector3.up * .36f, foot + Vector3.up * 1.55f, .27f, ~0, QueryTriggerInteraction.Ignore);
                if (floor && room) clear++;
                else c.Note($"blocked promenade sample {x}, {y}, {z}: floor {floor}, headroom {room}");
                samples++;
            }
            c.Check(clear == samples, $"both elevated streets have continuous floors and standing headroom ({clear}/{samples})");
            CityDesign.Lot lot = plan.lots.Find(l => l.kind == CityDesign.LotKind.Panel && l.street > 0);
            if (!c.Check(lot != null, "a residential stair core is available")) { city.enabled = true; yield break; }
            float centre = (lot.x0 + lot.x1 + 1) * .125f;
            p.Teleport(city.DeckPoint(new Vector3((lot.x0 + 6f) * .25f, .6f, (lot.z0 + 4f) * .25f)), city.transform.eulerAngles.y);
            c.Fixture("player set at the first stair's bottom riser");
            for (int f = 0; f < 3 && !c.Failed; f++)
            {
                float x = (f % 2 == 0 ? lot.x0 + 6f : lot.x1 - 5f) * .25f;
                if (f > 0)
                {
                    yield return Walk(c, city, new Vector3(centre, f * 3f + .25f, (lot.z0 + 32f) * .25f));
                    yield return Walk(c, city, new Vector3(centre, f * 3f + .25f, (lot.z0 + 4f) * .25f));
                    yield return Walk(c, city, new Vector3(x, f * 3f + .5f, (lot.z0 + 4f) * .25f));
                }
                yield return Walk(c, city, new Vector3(x, (f + 1) * 3f + .25f, (lot.z0 + 32f) * .25f));
            }
            if (!c.Failed)
            {
                float doorZ = (lot.doorZ + 3f) * .25f;
                yield return Walk(c, city, new Vector3(centre, 9.25f, doorZ));
                yield return Walk(c, city, new Vector3(-4.5f, 9.25f, doorZ));
                c.Screenshot("onto_the_sky_street");
                // Same connected level, set near a crossing to keep the test bounded.
                p.Teleport(city.DeckPoint(new Vector3(-4.5f, 9.4f, 0f)), city.transform.eulerAngles.y + 90);
                yield return Walk(c, city, new Vector3(4.5f, 9.25f, 0f));
            }
            float sx = (CityDesign.IndustrialHX + 6f) * .25f, sz = CityDesign.IndustrialHZ - 44;
            p.Teleport(city.DeckPoint(new Vector3(sx, -2.4f, (sz + 1) * .25f)), city.transform.eulerAngles.y);
            yield return Walk(c, city, new Vector3(sx, .25f, (sz + 30) * .25f));
            yield return Walk(c, city, new Vector3(23f, .1f, (sz + 30) * .25f));
            CityDock.Frame(city.Route, 0, out var dock, out _);
            var tip = city.DeckPoint(new Vector3(city.Route.halfWidth + CityDesign.PierGap, 0, 0));
            c.Check(Vector3.Distance(tip, dock) < .8f, "the wider city's gangway still aligns with its own dock");
            // Reference views: the entire moving platform and the machinery at human scale.
            p.Teleport(city.DeckPoint(new Vector3(9f, .15f, 0)), 0);
            var rig = g.cameraRig;
            if (rig && rig.pixel)
            {
                bool wasVector = GameSettings.Current.vector;
                GameSettings.Current.vector = true; GameSettings.Current.Apply(g);
                rig.enabled = false;
                var cam = rig.pixel.transform;
                var lens = cam.GetComponent<Camera>();
                bool wasOrtho = lens.orthographic; float wasFov = lens.fieldOfView;
                lens.orthographic = false; lens.fieldOfView = 55f;
                c.Fixture("overview camera in perspective, full-resolution rendering for detail review");
                cam.position = city.DeckPoint(new Vector3(110, 25, 95));
                cam.LookAt(city.DeckPoint(new Vector3(0, 20, 0)));
                yield return new WaitForSeconds(.5f);
                c.Screenshot("industrial_city_overview");
                yield return null;
                cam.position = city.DeckPoint(new Vector3(34, -2, 43));
                cam.LookAt(city.DeckPoint(new Vector3(18, 10, 0)));
                yield return new WaitForSeconds(.5f);
                c.Screenshot("industrial_tracks_and_districts");
                yield return null;
                lens.orthographic = wasOrtho; lens.fieldOfView = wasFov;
                GameSettings.Current.vector = wasVector; GameSettings.Current.Apply(g);
                rig.enabled = true;
            }
            city.enabled = true;
        }
    }
}
