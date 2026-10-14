using MadMax.Rendering;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The railway on the HUD: lines on the minimap and the map page (a dark casing with pale sleeper dashes),
    /// stations as bars along their platforms (named on the map page), and the cab panel while driving a loco —
    /// speed, throttle, brake, reverser, fuel, the next points ahead and how they lie.</summary>
    public partial class PixelHud
    {
        static readonly Color32 RailCase = new Color32(38, 30, 26, 240), RailTie = new Color32(214, 204, 186, 255),
            StationFill = new Color32(150, 190, 220, 255), StationDim = new Color32(110, 130, 150, 255);
        int mapRailVersion = -1;

        void AddRailVectors(WorldGen world, System.Action<Vector2, Vector2, float, byte> add)
        {
            var net = world.rail;
            if (net == null) return;
            foreach (var l in net.lines)
            {
                if (l.dead) continue;
                for (int k = 2; k < l.pts.Length; k += 2)
                    add(new Vector2(l.pts[k - 2].x, l.pts[k - 2].z), new Vector2(l.pts[k].x, l.pts[k].z), 1.6f, 3);
                if (l.pts.Length % 2 == 0) add(new Vector2(l.pts[l.pts.Length - 2].x, l.pts[l.pts.Length - 2].z), new Vector2(l.pts[l.pts.Length - 1].x, l.pts[l.pts.Length - 1].z), 1.6f, 3);
            }
            mapRailVersion = net.Version;
        }

        /// <summary>A railway segment: the dark casing pass, then pale sleeper dashes.</summary>
        void DrawRailSeg(PixelCanvas c, Vector2 wa, Vector2 wb, bool casing, float widthScale)
        {
            int r = c.res;
            var a = ToFine(wa.x, wa.y); var b = ToFine(wb.x, wb.y);
            if (casing) { MapDraw.Capsule(c, a.x, a.y, b.x, b.y, Mathf.Max(0.75f * r * widthScale, 1.6f / vFm), RailCase); return; }
            float len = (b - a).magnitude, dash = 2.2f * r;
            if (len < 0.5f) return;
            int n = Mathf.CeilToInt(len / dash);
            for (int i = 0; i < n; i += 2)
            {
                var p = Vector2.Lerp(a, b, i / (float)n); var q = Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)n));
                MapDraw.Capsule(c, p.x, p.y, q.x, q.y, Mathf.Max(0.32f * r * widthScale, 0.5f / vFm), RailTie);
            }
        }

        /// <summary>Stations on the minimap: a bar along the platform, inked; found ones lit.</summary>
        void DrawRailStations(PixelCanvas c, float fcx, float fcy, float FR)
        {
            var net = game.World != null ? game.World.rail : null;
            if (net == null) return;
            int rs = c.res;
            foreach (var st in net.stations)
            {
                var a = ToFine(st.World(-Mathf.Max(st.half, 8f), 0f)); var b = ToFine(st.World(Mathf.Max(st.half, 8f), 0f));
                var m = (a + b) * 0.5f;
                if ((m - new Vector2(fcx, fcy)).magnitude > FR - 3f * rs) continue;
                bool known = game.Discovered.Contains("rail:" + st.id);
                float w = st.tier == StationTier.Terminal ? 2.1f : st.tier == StationTier.Junction ? 1.2f : 1.6f;
                MapDraw.Capsule(c, a.x, a.y, b.x, b.y, (w + 0.8f) * rs, MapInk);
                MapDraw.Capsule(c, a.x, a.y, b.x, b.y, w * rs, known ? StationFill : StationDim);
            }
        }

        /// <summary>Stations on the map page: bars, names of terminals and stations (all of them zoomed in), hover text.</summary>
        void DrawMapStations(PixelCanvas c, int x, int y, int w, int h, Vector2 center, float mpp, Vector2Int mouse, ref string hovered, ref float hoverD)
        {
            var net = game.World != null ? game.World.rail : null;
            if (net == null) return;
            int r = c.res;
            MapDraw.smooth = r > 1;
            SetView(center.x, center.y, (x + w / 2) * r, (y + h / 2) * r, mpp / r, 0f);
            MapDraw.ClipRect(x * r, y * r, (x + w) * r, (y + h) * r);
            foreach (var st in net.stations)
            {
                var a = ToFine(st.World(-Mathf.Max(st.half, 8f), 0f)); var b = ToFine(st.World(Mathf.Max(st.half, 8f), 0f));
                bool known = game.Discovered.Contains("rail:" + st.id);
                MapDraw.Capsule(c, a.x, a.y, b.x, b.y, 2.4f * r, MapInk);
                MapDraw.Capsule(c, a.x, a.y, b.x, b.y, 1.6f * r, known ? StationFill : StationDim);
                var pi = new Vector2Int(Mathf.RoundToInt((a.x + b.x) * 0.5f / r), Mathf.RoundToInt((a.y + b.y) * 0.5f / r));
                if (pi.x < x + 2 || pi.y < y + 2 || pi.x > x + w - 3 || pi.y > y + h - 3) continue;
                string name = st.Name(game.World);
                bool label = st.tier == StationTier.Terminal || (mpp < 6f && st.tier != StationTier.Junction) || mpp < 2.5f;
                if (label && st.settlement < 0) c.Text(pi.x - PixelCanvas.TextWidth(name) / 2, pi.y + 4, name, known ? StationFill : StationDim);
                float d = Vector2Int.Distance(pi, mouse);
                if (d < hoverD)
                {
                    hoverD = d;
                    string what = st.tier == StationTier.Terminal ? "RAIL TERMINAL: 3 PLATFORMS, YARD, ENGINE SHED" : st.tier == StationTier.Through ? "STATION: 2 PLATFORMS, PASSING LOOP" : st.tier == StationTier.Halt ? "HALT: 1 PLATFORM" : st.tier == StationTier.WaterStop ? "WATER AND FUEL STOP" : "JUNCTION: SIGNAL BOX";
                    hovered = name + "  (" + what + (known ? ")" : ", NOT VISITED)");
                }
            }
            MapDraw.NoClip();
        }

        /// <summary>The cab panel (bottom left) while driving a loco; the map vectors follow new track.</summary>
        void DrawRail()
        {
            var net = game.World != null ? game.World.rail : null;
            if (net != null && net.Version != mapRailVersion && mapWorld != null) BuildMapVectors(game.World);
            var t = game.CabTrain;
            if (t == null || (game.Menus && game.Menus.IsOpen)) return;
            var loco = t.Loco;
            int x = 6, y = canvas.h - 52;
            canvas.Panel(x, y, 190, 46);
            float kmh = GameSettings.Current.SpeedValue(Mathf.Abs(t.speed) * 3.6f);
            canvas.Text(x + 4, y + 4, Mathf.RoundToInt(kmh).ToString("000"), Text, 3);
            canvas.Text(x + 42, y + 4, GameSettings.Current.SpeedUnit, Dim);
            canvas.Text(x + 42, y + 12, t.reverser > 0 ? "FWD" : "REV", Amber);
            canvas.Text(x + 64, y + 4, loco && loco.def.hand ? "PUMP" : "THROTTLE", Dim);
            Bar(x + 64, y + 11, 50, t.throttle, Green);
            canvas.Text(x + 120, y + 4, "BRAKE", Dim);
            Bar(x + 120, y + 11, 50, t.brake, Red);
            if (loco && !loco.def.hand)
            {
                canvas.Text(x + 64, y + 17, "DIESEL " + Mathf.RoundToInt(t.fuel) + " L", t.fuel < t.tank * 0.15f ? Red : Dim);
                Bar(x + 120, y + 19, 50, t.tank > 0f ? t.fuel / t.tank : 0f, Amber);
            }
            canvas.Text(x + 4, y + 26, t.trainName + "  " + t.cars.Count + (t.cars.Count == 1 ? " CAR" : " CARS"), Text);
            int sign = Mathf.Abs(t.speed) > 0.3f ? (t.speed > 0f ? 1 : -1) : t.PullSign;
            int node = t.path.NextFacingSwitch(net, sign > 0 ? t.head : t.head - t.Length, sign, 400f, out float at);
            string pts = "NO POINTS AHEAD";
            if (node >= 0)
            {
                var n = net.nodes[node];
                int div = net.DivergeSide(n);
                pts = "POINTS " + Mathf.RoundToInt(at) + " M: " + (n.state == 0 ? "STRAIGHT" : div < 0 ? "LEFT" : "RIGHT");
            }
            canvas.Text(x + 4, y + 34, pts, Amber);
            float comp = t.Competence;
            if (comp < 0.5f) canvas.Text(x + 104, y + 34, comp <= 0f ? "BEYOND YOU" : "UNTRAINED", Red);
            var st = net.StationNear(t.HeadPoint, 400f);
            if (st != null) canvas.Text(x + 4, y - 8, "NEAR " + st.Name(game.World), Dim);
        }
    }
}
