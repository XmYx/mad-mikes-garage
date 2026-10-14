using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Laying track (RAIL TRACK / SCRAP TRACK). First click: an open track end (the new track carries straight
    /// on from it), the side of any track (new points: the branch leaves tangent to it and a lever stand appears) or bare
    /// ground. Then the cursor gives the other end: a circular curve tangent to the start (no tighter than 25 m, up to
    /// 60 m a click), or a joined curve when it ends on another track end / track. The track follows the ground
    /// (6 % at most), joins the network (trains run on it, it is saved) and chains on from its end; [RMB] stops,
    /// [X] on built track takes it up (half the materials back).</summary>
    public partial class BuildMode
    {
        public const float RailMinRadius = 25f, RailMaxRun = 60f, RailMaxGrade = 0.06f;
        Vector3? railStart;
        Vector2 railHeading;
        bool railHeadingFixed;
        Mesh railPreview;
        Vector3 railPreviewKey;
        readonly List<Vector3> railPts = new List<Vector3>();
        /// <summary>The track run under the cursor (tests).</summary>
        public IReadOnlyList<Vector3> RailPreviewPoints => railPts;

        bool RailTick(FurnitureDef def, RaycastHit hit, Keyboard kb, Mouse mouse, Gamepad pad)
        {
            if (!FurnitureLibrary.IsTrack(def)) { railStart = null; return false; }
            var net = game.World != null ? game.World.rail : null;
            if (net == null) { ghost.SetActive(false); Valid = false; Status = "NO RAILWAY IN THIS WORLD"; return true; }
            bool scrap = def.id == FurnitureLibrary.RailTrackScrap;
            bool click = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            bool stop = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (stop) railStart = null;
            var point = hit.point;
            if (Controls.Down(Controls.Act.BuildDismantle))
            {
                var l = net.NearestTrack(point, 2.5f, out _, out _);
                if (l != null && l.built)
                {
                    var refund = FurnitureLibrary.FrameCost(def, l.Length);
                    using (Inventory.Source("TAKEN UP", null)) foreach (var (t, c) in refund) game.Inventory.Add(t, Mathf.Max(1, c / 2));
                    net.RemoveBuilt(l);
                    MadMax.Audio.Sfx.Play("hammer", point, 0.7f);
                    game.Toast("TRACK TAKEN UP");
                }
                else game.Toast(l != null ? "ONLY TRACK YOU LAID COMES UP" : "AIM AT TRACK YOU LAID");
                return true;
            }
            if (railStart == null)
            {
                ghost.SetActive(false);
                var end = net.NodeNear(point, 4f, true);
                if (end != null)
                {
                    var e = end.ends[0];
                    var o = -net.lines[e.x].Outward(e.y);
                    Valid = true; Status = "[LMB] CARRY THE TRACK ON FROM THIS END";
                    if (click) { railStart = end.pos; railHeading = new Vector2(o.x, o.z).normalized; railHeadingFixed = true; }
                    return true;
                }
                var line = net.NearestTrack(point, 3f, out float s, out _);
                if (line != null)
                {
                    var p = line.At(s, out var tan);
                    bool nearNode = net.NodeNear(p, 6f) != null;
                    Valid = !nearNode;
                    Status = nearNode ? "TOO CLOSE TO OTHER POINTS - BRANCH OFF FURTHER ALONG" : "[LMB] BRANCH OFF HERE (NEW POINTS)";
                    if (click && !nearNode) { railStart = p; railHeading = new Vector2(tan.x, tan.z).normalized; railHeadingFixed = false; }
                    return true;
                }
                Valid = true; Status = "[LMB] START NEW TRACK HERE";
                if (click) { var t = DeformableTerrain.Instance; railStart = new Vector3(point.x, t ? t.Height(point.x, point.z) : point.y, point.z); railHeadingFixed = false; railHeading = Vector2.zero; }
                return true;
            }

            // the run from the start to the cursor
            var S = railStart.Value;
            string why = PlanRun(net, S, point, out bool snapped);
            float len = 0f; for (int i = 1; i < railPts.Count; i++) len += Vector3.Distance(railPts[i - 1], railPts[i]);
            var cost = FurnitureLibrary.FrameCost(def, len);
            bool afford = CanAfford(cost);
            bool ok = why == null && afford && railPts.Count >= 2;
            if (railPts.Count >= 2)
            {
                var key = new Vector3(railPts[railPts.Count - 1].x, railPts[railPts.Count - 1].z, railPts.Count);
                if (!railPreview || (key - railPreviewKey).sqrMagnitude > 0.04f)
                {
                    if (railPreview) Destroy(railPreview);
                    railPreview = MadMax.Voxel.VoxelMesher.ToMesh(RailTrackBuilder.Preview(railPts, scrap).mesh, "RailPreview");
                    railPreviewKey = key;
                }
                ghost.SetActive(true);
                ghostMesh.sharedMesh = railPreview;
                ghost.transform.SetPositionAndRotation(railPts[0], Quaternion.identity);
                Tint(ok);
            }
            else ghost.SetActive(false);
            Valid = ok;
            Status = why ?? (!afford ? "NOT ENOUGH MATERIALS FOR " + len.ToString("0") + " M" : "[LMB] LAY " + len.ToString("0") + " M" + (snapped ? " TO THE TRACK" : "") + "  [RMB] STOP");
            if (!click || !ok) return true;
            using (Inventory.Source(null, "BUILT"))
                foreach (var (t, c) in cost) game.Inventory.TrySpend(t, Cost(c));
            var laid = net.AddBuilt(railPts, scrap);
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 3f);
            game.Stats.Practice(MadMax.RPG.Skill.Rail, 2f);
            MadMax.Audio.Sfx.Play("hammer", railPts[railPts.Count - 1], 0.8f);
            MadMax.Audio.Sfx.Play("hit_metal", railPts[railPts.Count / 2], 0.5f, 0.7f, 30f);
            if (snapped) { railStart = null; return true; }
            var endT = laid.Outward(1);
            railStart = laid.pts[laid.pts.Length - 1]; railHeading = -new Vector2(endT.x, endT.z).normalized; railHeadingFixed = true;
            return true;
        }

        /// <summary>Points of the run S → cursor into <see cref="railPts"/>; returns why it can't be laid (null: fine).</summary>
        string PlanRun(RailNetwork net, Vector3 S, Vector3 cursor, out bool snapped)
        {
            railPts.Clear();
            snapped = false;
            var s2 = new Vector2(S.x, S.z);
            var c = new Vector2(cursor.x, cursor.z) - s2;
            if (c.magnitude < 3f) return "PICK WHERE IT GOES  [RMB] STOP";
            if (c.magnitude > RailMaxRun) c = c.normalized * RailMaxRun;
            var h = railHeading;
            if (h.sqrMagnitude < 0.5f) h = c.normalized;                                          // free start: straight at the cursor
            else if (!railHeadingFixed && Vector2.Dot(h, c) < 0f) h = -h;                          // a branch leaves either way
            var E = s2 + c;
            // ending on track: an open end or the side of a line
            Vector2? arrive = null;
            var endNode = net.NodeNear(new Vector3(E.x, 0f, E.y), 3f, true);
            if (endNode != null && (endNode.pos - S).sqrMagnitude > 1f)
            {
                var o = net.lines[endNode.ends[0].x].Outward(endNode.ends[0].y);
                arrive = -new Vector2(o.x, o.z).normalized;
                E = new Vector2(endNode.pos.x, endNode.pos.z);
            }
            else
            {
                var l = net.NearestTrack(new Vector3(E.x, 0f, E.y), 2f, out float sl, out _);
                if (l != null && (l.At(sl) - S).sqrMagnitude > 9f)
                {
                    var p = l.At(sl, out var t);
                    E = new Vector2(p.x, p.z);
                    var tt = new Vector2(t.x, t.z).normalized;
                    arrive = Vector2.Dot(tt, E - s2) >= 0f ? tt : -tt;
                }
            }
            var plan = new List<Vector2>();
            if (arrive.HasValue)
            {
                snapped = true;
                if (Vector2.Dot(arrive.Value, (E - s2).normalized) < 0.25f || Vector2.Dot(h, (E - s2).normalized) < 0.25f) return "THE ENDS DON'T LINE UP";
                float L = (E - s2).magnitude;
                int n = Mathf.Max(2, Mathf.CeilToInt(L / 2f));
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n, t2 = t * t, t3 = t2 * t;
                    plan.Add((2f * t3 - 3f * t2 + 1f) * s2 + (t3 - 2f * t2 + t) * L * h + (-2f * t3 + 3f * t2) * E + (t3 - t2) * L * arrive.Value);
                }
            }
            else
            {
                float cross = h.x * c.y - h.y * c.x, theta = Mathf.Atan2(cross, Vector2.Dot(h, c));
                float chord = c.magnitude;
                if (Mathf.Abs(theta) < 0.003f)
                {
                    int n = Mathf.Max(2, Mathf.CeilToInt(chord / 2f));
                    for (int i = 0; i <= n; i++) plan.Add(s2 + c * (i / (float)n));
                }
                else
                {
                    if (Mathf.Abs(theta) > Mathf.PI * 0.45f) return "TOO SHARP A TURN";
                    float R = chord / (2f * Mathf.Sin(Mathf.Abs(theta)));
                    if (R < RailMinRadius) return "TOO SHARP: CURVES NO TIGHTER THAN " + RailMinRadius.ToString("0") + " M";
                    float arc = 2f * Mathf.Abs(theta) * R, sg = Mathf.Sign(theta);
                    var nrm = new Vector2(-h.y, h.x) * sg;
                    int n = Mathf.Max(2, Mathf.CeilToInt(arc / 2f));
                    for (int i = 0; i <= n; i++) { float phi = (arc * i / n) / R; plan.Add(s2 + h * (R * Mathf.Sin(phi)) + nrm * (R * (1f - Mathf.Cos(phi)))); }
                }
            }
            // heights: the ground, eased from the start (and onto a joined end), a little smoothed
            var terr = DeformableTerrain.Instance;
            float endY = snapped ? (endNode != null ? endNode.pos.y : net.NearestTrack(new Vector3(E.x, 0f, E.y), 2.5f, out float se, out _) is RailLine el ? el.At(se).y : S.y) : float.NaN;
            var hs = new float[plan.Count];
            for (int i = 0; i < plan.Count; i++) hs[i] = terr ? terr.Height(plan[i].x, plan[i].y) : S.y;
            var sm = new float[plan.Count];
            for (int i = 0; i < plan.Count; i++) { float sum = 0f; int k = 0; for (int j = Mathf.Max(0, i - 2); j <= Mathf.Min(plan.Count - 1, i + 2); j++) { sum += hs[j]; k++; } sm[i] = sum / k; }
            float total = 0f; var along = new float[plan.Count];
            for (int i = 1; i < plan.Count; i++) { total += (plan[i] - plan[i - 1]).magnitude; along[i] = total; }
            for (int i = 0; i < plan.Count; i++)
            {
                float y = sm[i];
                y = Mathf.Lerp(S.y, y, Mathf.Clamp01(along[i] / 8f));
                if (!float.IsNaN(endY)) y = Mathf.Lerp(endY, y, Mathf.Clamp01((total - along[i]) / 8f));
                railPts.Add(new Vector3(plan[i].x, y, plan[i].y));
            }
            for (int i = 1; i < railPts.Count; i++)
            {
                float d = (new Vector2(railPts[i].x - railPts[i - 1].x, railPts[i].z - railPts[i - 1].z)).magnitude;
                if (d > 0.01f && Mathf.Abs(railPts[i].y - railPts[i - 1].y) / d > RailMaxGrade) return "TOO STEEP FOR TRACK - LEVEL THE GROUND FIRST";
            }
            foreach (var p in railPts) if (!float.IsNaN(game.World.WaterLevel(p.x, p.z)) && game.World.WaterLevel(p.x, p.z) > p.y) return "NOT THROUGH WATER";
            return null;
        }

        /// <summary>Automation: lay a run from a to b (costs paid) as the clicks would; null when it can't be laid.</summary>
        public RailLine PlaceTrack(string id, Vector3 a, Vector3 b, Vector2 heading)
        {
            var def = FurnitureLibrary.Get(id);
            var net = game.World != null ? game.World.rail : null;
            if (!FurnitureLibrary.IsTrack(def) || net == null) return null;
            railHeading = heading; railHeadingFixed = heading.sqrMagnitude > 0.5f;
            if (PlanRun(net, a, b, out _) != null || railPts.Count < 2) return null;
            float len = 0f; for (int i = 1; i < railPts.Count; i++) len += Vector3.Distance(railPts[i - 1], railPts[i]);
            var cost = FurnitureLibrary.FrameCost(def, len);
            if (!CanAfford(cost)) return null;
            foreach (var (t, c) in cost) game.Inventory.TrySpend(t, Cost(c));
            return net.AddBuilt(railPts, def.id == FurnitureLibrary.RailTrackScrap);
        }
    }
}
