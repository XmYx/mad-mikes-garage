using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q5 (day/night, seasons, rain/snow, wet ground, fire, decay, overgrowth together): night falls
    /// dark and colder than noon; rain soaks the ground into mud and it dries again when the rain stops; freezing rain
    /// turns to snow that settles; a fire burns its fuel several times faster in the rain; the season turns after its
    /// days; three wet days in the open weather wooden pieces while the same pieces under a roof keep; and the
    /// overgrowth on a town's buildings spreads as the days go by. Weather and the clock are pinned / moved on
    /// (disclosed); nothing changes the physics time scale.</summary>
    class SurvivalWeatherDecay : Scenario
    {
        public override string Id => "survival.weather_decay";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 200f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 10f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var pad = at[0]; var fwd = g.Player.transform.forward; var side = g.Player.transform.right;
            var w = new Waited();

            // ---- day and night
            WeatherPin.Set(false, 2f, 0f);
            DayNight.SetHours(12f); yield return SurvivalKit.GameSeconds(0.5f);
            float noonDark = DayNight.Darkness, noonFelt = g.FeltTemperature;
            DayNight.SetHours(0.5f); yield return SurvivalKit.GameSeconds(0.5f);
            float nightDark = DayNight.Darkness, nightFelt = g.FeltTemperature;
            c.Fixture("clock set to noon, then half past midnight; air held at 2 C");
            c.Check(noonDark < 0.15f && nightDark > 0.6f, $"noon is light, midnight dark (darkness {noonDark:0.00} / {nightDark:0.00})");
            c.Check(nightFelt < noonFelt, $"the night is colder ({noonFelt:0.0} -> {nightFelt:0.0} C felt)");
            DayNight.SetHours(12f);

            // ---- rain on the ground, then drying
            var spot = pad + side * 2f;
            WeatherPin.Set(false, 18f, 0f);
            yield return SurvivalKit.GameSeconds(0.3f);
            var dry = t.SurfaceAt(spot.x, spot.z);
            WeatherPin.Set(true, 18f);
            c.Fixture("rain held at 18 C");
            yield return SurvivalKit.GameSeconds(8f);
            var wet = t.SurfaceAt(spot.x, spot.z);
            c.Metric("ground_wetness_after_8s_rain", Weather.Wetness, "");
            c.Check(Weather.Wetness > 0.15f && wet.wet > dry.wet && wet.mud > dry.mud, $"rain soaks the ground into mud (wet {dry.wet:0.00} -> {wet.wet:0.00}, mud {dry.mud:0.00} -> {wet.mud:0.00})");
            float soaked = Weather.Wetness;
            WeatherPin.Set(false, 18f);
            yield return SurvivalKit.GameSeconds(4f);
            c.Check(Weather.Wetness < soaked, $"and dries when it stops ({soaked:0.00} -> {Weather.Wetness:0.00})");

            // ---- fire in dry weather against rain
            var fire = Fire.Ignite(SurvivalKit.Ground(pad + fwd * 6f) + Vector3.up * 0.1f, null, 120f, 0.8f);
            c.Fixture("a fire lit 6 m away (120 s of fuel)");
            WeatherPin.Set(false, 18f, 0f);
            float f0 = fire ? fire.fuel : 0f, ta = Time.time;
            yield return SurvivalKit.GameSeconds(2f);
            float dryRate = fire ? (f0 - fire.fuel) / (Time.time - ta) : 0f;
            WeatherPin.Set(true, 18f, 1f);
            yield return null;
            f0 = fire ? fire.fuel : 0f; ta = Time.time;
            yield return SurvivalKit.GameSeconds(2f);
            float wetRate = fire ? (f0 - fire.fuel) / (Time.time - ta) : 0f;
            c.Metric("fire_fuel_per_s_dry", dryRate, "s/s"); c.Metric("fire_fuel_per_s_rain", wetRate, "s/s");
            c.Check(fire && wetRate > dryRate * 2.5f, $"rain makes a fire burn out far faster ({dryRate:0.00} -> {wetRate:0.00} fuel/s)");
            if (fire) Object.Destroy(fire.gameObject);

            // ---- snow
            WeatherPin.Set(true, -4f, 0f);
            c.Fixture("freezing rain held at -4 C");
            float snow0 = Weather.Snow;
            yield return SurvivalKit.GameSeconds(4f);
            c.Check(Weather.Snowing && Weather.Snow > snow0, $"below freezing it snows and settles ({snow0:0.000} -> {Weather.Snow:0.000})");

            // ---- the season turns
            if (Weather.DaysPerSeason > 0)
            {
                int season0 = Weather.Season;
                DayNight.SetDay(DayNight.Day + Weather.DaysPerSeason);
                c.Fixture(Weather.DaysPerSeason + " days moved on");
                yield return SurvivalKit.Until(() => Weather.Season != season0, 2f, w);
                c.Check(w.ok, $"the season turns after {Weather.DaysPerSeason} days ({Weather.SeasonNames[season0]} -> {Weather.SeasonNames[Weather.Season]}): {g.ToastText}");
            }
            else c.Note("seasons never change in these rules");

            // ---- decay: wooden posts in the open against posts under a roof, three wet days
            var open = new List<Placeable>(); var covered = new List<Placeable>();
            for (int i = 0; i < 12; i++) open.Add(SurvivalKit.Piece(g, "post", pad - fwd * 4f + side * (i - 5.5f) * 0.7f, fwd));
            for (int k = 0; k < 3; k++)
            {
                var centre = pad - fwd * 7.5f + side * (k - 1) * 2.1f;
                foreach (var o in new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) })
                    covered.Add(SurvivalKit.Piece(g, "post", centre + o, fwd));
                SurvivalKit.Piece(g, "roof_flat", SurvivalKit.Ground(centre) + Vector3.up * 2.6f, fwd, false);
            }
            c.Fixture("12 wooden posts in the open, 12 under three roof panels");
            WeatherPin.Set(true, 12f, 1f);
            yield return SurvivalKit.Frames(3);
            yield return SurvivalKit.GameSeconds(0.5f);
            DayNight.SetDay(DayNight.Day + 3);
            c.Fixture("three rainy days moved on");
            yield return SurvivalKit.GameSeconds(1f, 30);
            int openWorn = open.Count(p => p && p.hits < p.MaxHits), coveredWorn = covered.Count(p => p && p.hits < p.MaxHits);
            c.Metric("posts_weathered_open", openWorn, "of 12"); c.Metric("posts_weathered_roofed", coveredWorn, "of 12");
            c.Check(openWorn >= 2, $"wood left in the rain weathers ({openWorn} of 12 lost a hit)");
            c.Check(coveredWorn == 0, $"under a roof it keeps ({coveredWorn} of 12)");
            c.Check(open.All(p => p && p.hits >= 1), "weathering never breaks a piece outright");

            // ---- overgrowth on a town's buildings
            WeatherPin.Set(false, 22f, 0f);
            var me = new Vector2(pad.x, pad.z);
            var town = g.World.settlements.OrderBy(s => (s.pos - me).sqrMagnitude).FirstOrDefault();
            if (town == null) { c.Block("no settlement"); WeatherPin.Release(); yield break; }
            var tp = SurvivalKit.Ground(new Vector3(town.pos.x, 0f, town.pos.y));
            g.Player.Teleport(tp + Vector3.up * 0.3f, 0f);
            c.Fixture($"teleported to the nearest town ({tp.x:0},{tp.z:0})");
            List<Overgrowth> grown = null;
            yield return SurvivalKit.Until(() => (grown = Object.FindObjectsByType<Overgrowth>(FindObjectsSortMode.None).Where(o => (o.transform.position - tp).sqrMagnitude < 90f * 90f).ToList()).Count > 0 && Cover(grown) > 0, 45f, w);
            if (!c.Check(w.ok, $"town buildings carry overgrowth ({(grown != null ? grown.Count : 0)} buildings)")) { WeatherPin.Release(); yield break; }
            yield return SurvivalKit.GameSeconds(6f);
            int cover0 = Cover(grown);
            DayNight.SetDay(DayNight.Day + 25);
            c.Fixture("25 days moved on");
            yield return SurvivalKit.Until(() => Cover(grown) > cover0, 25f, w);
            c.Metric("overgrowth_vertices_before", cover0, ""); c.Metric("overgrowth_vertices_after", Cover(grown), "");
            c.Check(w.ok, $"overgrowth spreads with the days ({cover0} -> {Cover(grown)} vertices on {grown.Count} buildings)");
            c.Screenshot("overgrowth");
            yield return null;
            WeatherPin.Release();
        }

        static int Cover(List<Overgrowth> list)
        {
            int n = 0;
            foreach (var o in list)
            {
                if (!o) continue;
                var t = o.transform.Find("Overgrowth");
                if (t && t.gameObject.activeSelf && t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) n += mf.sharedMesh.vertexCount;
            }
            return n;
        }
    }
}
