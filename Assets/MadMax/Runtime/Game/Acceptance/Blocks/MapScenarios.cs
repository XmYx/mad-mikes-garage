using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The round minimap and the waypoint (smoke test; the integrator checks the look in the editor).</summary>
    public static class MapScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MinimapWaypoint();
        }
    }

    /// <summary>MINIMAP + WAYPOINT: NORTH UP keeps N at the top, HEADING UP puts it at -ViewYaw; a waypoint 300 m out gets
    /// a route and a distance under the minimap, and clears with a toast once the player stands beside it.</summary>
    class MinimapWaypoint : Scenario
    {
        public override string Id => "ui.minimap_waypoint";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var s = GameSettings.Current;
            bool keep = s.minimapNorthUp;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.5f); }
            s.minimapNorthUp = true;
            yield return null; yield return null;
            c.Check(PixelHud.LastMinimapFrame >= Time.frameCount - 2, "the minimap draws");
            c.Check(Mathf.Abs(Mathf.DeltaAngle(PixelHud.LastNorthAngle, 0f)) < 0.5f, $"NORTH UP: N at the top ({PixelHud.LastNorthAngle:0}°)");
            s.minimapNorthUp = false;
            yield return null; yield return null;
            float yaw = g.cameraRig ? g.cameraRig.ViewYaw : 0f;
            c.Check(Mathf.Abs(Mathf.DeltaAngle(PixelHud.LastNorthAngle, -yaw)) < 1f, $"HEADING UP: N at {PixelHud.LastNorthAngle:0}° for view yaw {yaw:0}°");

            var at = g.Player.transform.position;
            var target = at + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 300f;
            g.SetWaypoint(target, "TEST");
            yield return null; yield return null;
            c.Check(g.HasWaypoint && g.Route.Count >= 2, $"the waypoint has a route ({g.Route.Count} points)");
            c.Metric("waypoint_distance", g.WaypointDistance, "m");
            c.Check(g.WaypointDistance >= 290f, $"route distance {g.WaypointDistance:0} m");
            c.Check(PixelHud.LastWaypointText != null, "distance shown under the minimap: " + PixelHud.LastWaypointText);
            c.Screenshot("minimap_waypoint");
            yield return null;

            var wp = g.Waypoint + Vector3.right * 6f;
            var terrain = MadMax.World.DeformableTerrain.Instance;
            if (terrain) wp.y = terrain.Height(wp.x, wp.z) + 0.2f;
            g.Player.Teleport(wp, 0f);
            bool toast = false;
            float t0 = Time.time;
            while (Time.time - t0 < 4f && g.HasWaypoint) { yield return null; }
            for (int i = 0; i < 3; i++) { toast |= g.ToastText != null && g.ToastText.Contains("WAYPOINT REACHED"); yield return null; }
            c.Check(!g.HasWaypoint, "standing beside it clears the waypoint");
            c.Check(toast, "with a WAYPOINT REACHED toast");
            s.minimapNorthUp = keep;
        }
    }
}
