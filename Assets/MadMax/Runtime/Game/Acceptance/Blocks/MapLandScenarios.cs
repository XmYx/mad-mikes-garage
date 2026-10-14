using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The HD map land layer (<see cref="MapLand"/>): smoke test; the integrator judges the look from the captures.</summary>
    public static class MapLandScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MapLandTiles();
        }
    }

    /// <summary>MAP LAND: on foot the minimap under the player comes from an HD tile within seconds; the map page (paused)
    /// covers the player's spot from its own tiles too.</summary>
    class MapLandTiles : Scenario
    {
        public override string Id => "ui.map_land";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var hud = PixelHud.Instance;
            if (!hud || hud.Land == null) { c.Block("no HUD / map land"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.5f); }
            float t0 = Time.realtimeSinceStartup;
            while (!hud.LandCovered(g.Player.transform.position, false) && Time.realtimeSinceStartup - t0 < 15f) yield return null;
            float mini = Time.realtimeSinceStartup - t0;
            c.Metric("minimap_tile_seconds", mini, "s");
            c.Check(hud.LandCovered(g.Player.transform.position, false), $"the minimap draws HD land under the player ({mini:0.0} s, {hud.Land.Finished} tiles built)");
            c.Screenshot("minimap_land");
            yield return null;
            g.Menus.Open(MenuSystem.Page.Map);
            t0 = Time.realtimeSinceStartup;
            while (!hud.LandCovered(g.Player.transform.position, true) && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            float page = Time.realtimeSinceStartup - t0;
            c.Metric("map_tile_seconds", page, "s");
            c.Check(hud.LandCovered(g.Player.transform.position, true), $"the map page draws HD land at the player ({page:0.0} s)");
            yield return new WaitForSecondsRealtime(3f);                                              // let more of the view fill in
            c.Metric("map_tiles_cached", hud.Land.Cached, "");
            c.Screenshot("map_land");
            yield return null;
            g.Menus.Close();
        }
    }
}
