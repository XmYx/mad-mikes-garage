using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Free frame (roadmap 29): four posts, cross beams snapped to their tops, an irregular span laid over the
    /// beam ends, a frame ladder leaned against it — the player climbs it and stands on the span; states round-trip.</summary>
    public static class FrameScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new BuildFrame(); }
    }

    class BuildFrame : Scenario
    {
        public override string Id => "build.frame";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no pad"); yield break; }
            var dir = Vector3.forward;
            g.Inventory.Add(ResourceType.Wood, 40); g.Inventory.Add(ResourceType.Scrap, 30); g.Inventory.Add(ResourceType.Iron, 8);   // what the frame costs (a heavy pack would stop the climb)
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            float H = 2.4f;
            var feet = new List<Vector3>();
            foreach (var (u, v) in new[] { (-1.2f, -1.2f), (1.4f, -1.0f), (1.2f, 1.6f), (-1.0f, 1.2f) })   // irregular footprint
            {
                var p = pad + right * u + dir * v;
                p.y = DeformableTerrain.Instance.Height(p.x, p.z);
                feet.Add(p);
            }
            float top = 0f; foreach (var f in feet) top = Mathf.Max(top, f.y);
            top += H;
            var tops = new List<Vector3>();
            int beams = 0;
            foreach (var f in feet)
            {
                var t = new Vector3(f.x, top, f.z);
                if (g.Build.PlaceBeam("beam_wood", f, t)) beams++;
                tops.Add(t);
            }
            yield return null;
            // cross beams start on a snapped post top
            for (int i = 0; i < 4; i++)
            {
                Frame.NearestNode(tops[i] + Vector3.one * 0.1f, Frame.NodeSnap, out var a);
                if (g.Build.PlaceBeam("beam_steel", a, tops[(i + 1) % 4])) beams++;
            }
            c.Check(beams == 8, $"8 beams placed (4 posts, 4 cross beams): {beams}");
            c.Check(Frame.NearestNode(tops[2] + new Vector3(0.2f, 0.1f, -0.1f), Frame.NodeSnap, out var snapped) && (snapped - tops[2]).magnitude < 0.12f,
                    "a click near a post top snaps to the beam end");
            var span = g.Build.PlaceSpan("span_planks", tops);
            c.Check(span && span.GetComponent<FrameSpan>().corners.Count == 4, "a four-cornered span is laid over the beam ends");
            if (!span) yield break;
            yield return new WaitForFixedUpdate();
            var centre = (tops[0] + tops[1] + tops[2] + tops[3]) / 4f;
            float h = -999f;
            bool deck = StructureGround.Top(centre + Vector3.up * 0.5f, ref h, out _);
            c.Check(deck && Mathf.Abs(h - top) < 0.15f, $"the span is a deck at the beam tops ({h:0.00} vs {top:0.00})");
            c.Check(Physics.Raycast(centre + Vector3.up * 1.5f, Vector3.down, out var hit, 3f) && hit.collider.GetComponentInParent<FrameSpan>(), "the span has a walkable collider");
            // state round trip
            var fs = span.GetComponent<FrameSpan>();
            var state = fs.SaveState();
            fs.LoadState(state);
            c.Check(fs.corners.Count == 4 && fs.SaveState() == state, $"span state round-trips ({state})");
            var beam = FrameBeam.All[0];
            float len = beam.length;
            beam.LoadState(beam.SaveState());
            c.Check(Mathf.Abs(beam.length - len) < 0.01f, $"beam length round-trips ({len:0.00} m)");
            // a ladder from the ground to the edge between post 0 and 1, leaning out
            var edgeMid = (tops[0] + tops[1]) * 0.5f;
            var outward = Vector3.ProjectOnPlane(edgeMid - centre, Vector3.up).normalized;
            var foot = edgeMid + outward * 0.8f; foot.y = DeformableTerrain.Instance.Height(foot.x, foot.z);
            var ladderTop = edgeMid + outward * 0.1f + Vector3.up * 0.15f;
            var lad = g.Build.PlaceBeam("ladder_frame", foot, ladderTop);
            c.Check(lad && lad.GetComponent<Ladder>(), "a frame ladder of any length gets a climbable ladder");
            if (!lad) yield break;
            c.Screenshot("frame");
            yield return null;
            var P = g.Player;
            var start = foot + outward * 0.9f;
            start.y = DeformableTerrain.Instance.Height(start.x, start.z) + 0.05f;
            float yaw = Quaternion.LookRotation(-outward).eulerAngles.y;
            P.Teleport(start, yaw);
            P.viewYaw = yaw;
            WastelandGame.ExternalInput = true;
            bool climbed = false, onTop = false;
            float t0 = Time.time;
            while (Time.time - t0 < 14f)
            {
                P.moveInput = new Vector2(0f, 1f);
                if (P.Climbing) climbed = true;
                if (climbed && !P.Climbing && P.transform.position.y > top - 0.3f) { onTop = true; break; }
                yield return null;
            }
            P.moveInput = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            c.Fixture($"player walked into a {lad.GetComponent<FrameBeam>().length:0.0} m frame ladder");
            if (!climbed)
            {
                var L = lad.GetComponent<Ladder>();
                var near = Ladder.At(P.transform.position + Vector3.up * 0.9f, 0.45f);
                c.Note($"player {P.transform.position}, ladder foot {L.Foot} top {L.Top}, axis·up {Vector3.Dot(L.Axis, Vector3.up):0.00}, At {(near ? near.name : "none")}, local {L.transform.InverseTransformPoint(P.transform.position + Vector3.up * 0.9f)}, bounds {L.localBounds}");
            }
            c.Check(climbed, "walking into the ladder takes hold of it");
            c.Check(onTop && P.transform.position.y > top - 0.3f, $"the climber steps off onto the span (feet {P.transform.position.y:0.00}, deck {top:0.00})");
            WastelandGame.ExternalInput = false;
            foreach (var p in new List<Placeable>(Placeable.All)) if (p && (p.GetComponent<FrameBeam>() || p.GetComponent<FrameSpan>())) Object.Destroy(p.gameObject);
            P.Teleport(pad, 0f);
        }
    }
}
