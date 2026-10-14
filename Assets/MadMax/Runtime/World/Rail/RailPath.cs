using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A run of whole lines a train stands on, in order, each traversed forward (a→b) or backward, with one
    /// distance coordinate along all of them (<see cref="Start"/> .. <see cref="End"/>). Free trains extend it ahead
    /// through the points as they go (and behind when reversing) and trim what they left; AI services hold their whole
    /// route in one.</summary>
    public sealed class RailPath
    {
        public struct Seg { public RailLine line; public bool fwd; public float start; }
        public readonly List<Seg> segs = new List<Seg>();
        public float Start => segs.Count > 0 ? segs[0].start : 0f;
        public float End => segs.Count > 0 ? segs[segs.Count - 1].start + segs[segs.Count - 1].line.Length : 0f;

        public RailPath Clone() { var p = new RailPath(); p.segs.AddRange(segs); return p; }

        public void Begin(RailLine line, bool fwd, float start) { segs.Clear(); segs.Add(new Seg { line = line, fwd = fwd, start = start }); }

        int Find(float d)
        {
            int lo = 0, hi = segs.Count - 1;
            if (hi < 0) return -1;
            while (lo < hi) { int m = (lo + hi + 1) >> 1; if (segs[m].start <= d) lo = m; else hi = m - 1; }
            return lo;
        }

        /// <summary>Bed point and unit tangent (toward increasing d) at path distance d (clamped to the path).</summary>
        public Vector3 At(float d, out Vector3 tangent)
        {
            int i = Find(d);
            if (i < 0) { tangent = Vector3.forward; return Vector3.zero; }
            var s = segs[i];
            float local = Mathf.Clamp(d - s.start, 0f, s.line.Length);
            var p = s.line.At(s.fwd ? local : s.line.Length - local, out tangent);
            if (!s.fwd) tangent = -tangent;
            if (d > End) p += tangent * (d - End);                                                // past the buffer: keep the line straight on
            else if (d < Start) p += tangent * (d - Start);
            return p;
        }

        public Vector3 At(float d) => At(d, out _);

        /// <summary>Line and its own distance at path distance d.</summary>
        public RailLine LineAt(float d, out float s, out bool fwd)
        {
            int i = Find(d);
            if (i < 0) { s = 0f; fwd = true; return null; }
            var g = segs[i];
            float local = Mathf.Clamp(d - g.start, 0f, g.line.Length);
            s = g.fwd ? local : g.line.Length - local; fwd = g.fwd;
            return g.line;
        }

        /// <summary>The node at the front end of the path and the end of the last line arriving there.</summary>
        public int FrontNode(out int line, out int end)
        {
            var s = segs[segs.Count - 1];
            line = s.line.id; end = s.fwd ? 1 : 0;
            return s.fwd ? s.line.b : s.line.a;
        }

        public int BackNode(out int line, out int end)
        {
            var s = segs[0];
            line = s.line.id; end = s.fwd ? 0 : 1;
            return s.fwd ? s.line.a : s.line.b;
        }

        /// <summary>Append the line beyond the front end through the points (false: a dead end). Trailing points set.</summary>
        public bool ExtendFront(RailNetwork net)
        {
            if (segs.Count == 0) return false;
            int node = FrontNode(out int line, out int end);
            if (node < 0 || !net.Next(node, line, end, out var nx)) return false;
            var l = net.lines[nx.x];
            if (l.dead) return false;
            net.SetFor(node, nx.x, nx.y);
            segs.Add(new Seg { line = l, fwd = nx.y == 0, start = End });
            return true;
        }

        public bool ExtendBack(RailNetwork net)
        {
            if (segs.Count == 0) return false;
            int node = BackNode(out int line, out int end);
            if (node < 0 || !net.Next(node, line, end, out var nx)) return false;
            var l = net.lines[nx.x];
            if (l.dead) return false;
            net.SetFor(node, nx.x, nx.y);
            segs.Insert(0, new Seg { line = l, fwd = nx.y == 1, start = Start - l.Length });
            return true;
        }

        /// <summary>Drop lines lying wholly outside [from, to].</summary>
        public void Trim(float from, float to)
        {
            while (segs.Count > 1 && segs[0].start + segs[0].line.Length < from) segs.RemoveAt(0);
            while (segs.Count > 1 && segs[segs.Count - 1].start > to) segs.RemoveAt(segs.Count - 1);
        }

        /// <summary>A line on the path died (split by new points): swap in its two halves.</summary>
        public bool Repair(RailNetwork net)
        {
            bool changed = false;
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                if (!s.line.dead) continue;
                // the halves share the old line's ends: rebuild from the node chain
                var old = s.line;
                var na = net.nodes[old.a];
                RailLine first = null;
                foreach (var e in na.ends) { var l = net.lines[e.x]; if (!l.dead && e.y == 0 && (l.pts[0] - old.pts[0]).sqrMagnitude < 0.01f && Vector3.Dot(l.Outward(0), old.Outward(0)) > 0.9f) { first = l; break; } }
                if (first == null) return false;
                var chain = new List<RailLine> { first };
                float len = first.Length;
                while (len < old.Length - 0.05f && chain.Count < 16)
                {
                    var nb = net.nodes[chain[chain.Count - 1].b];
                    RailLine nextL = null;
                    foreach (var e in nb.ends) { var l = net.lines[e.x]; if (!l.dead && e.y == 0 && l != chain[chain.Count - 1] && Vector3.Dot(l.Outward(0), -chain[chain.Count - 1].Outward(1)) > 0.9f && l.kind == old.kind) { nextL = l; break; } }
                    if (nextL == null) return false;
                    chain.Add(nextL); len += nextL.Length;
                }
                segs.RemoveAt(i);
                float st = s.start;
                if (!s.fwd) chain.Reverse();
                foreach (var l in chain) { segs.Insert(i++, new Seg { line = l, fwd = s.fwd, start = st }); st += l.Length; }
                i--;
                changed = true;
            }
            return changed;
        }

        /// <summary>Path distance of the point nearest p on the path within <paramref name="maxLateral"/> (false: off it).</summary>
        public bool Project(Vector3 p, float maxLateral, out float d)
        {
            d = 0f; float best = maxLateral;
            bool found = false;
            foreach (var s in segs)
            {
                var b = s.line.bounds; b.Expand(maxLateral * 2f + 1f);
                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                float along = s.line.Nearest(p, out float dist);
                if (dist >= best) continue;
                best = dist; found = true;
                d = s.start + (s.fwd ? along : s.line.Length - along);
            }
            return found;
        }

        /// <summary>The next points ahead of path distance d in the travel direction (sign) within <paramref name="reach"/>:
        /// node id and its distance (-1: none). Only facing points (entered from the toe) count.</summary>
        public int NextFacingSwitch(RailNetwork net, float d, int sign, float reach, out float at)
        {
            at = 0f;
            if (sign >= 0)
                for (int i = 0; i < segs.Count; i++)
                {
                    var s = segs[i];
                    float endD = s.start + s.line.Length;
                    if (endD < d) continue;
                    if (endD - d > reach) break;
                    int node = s.fwd ? s.line.b : s.line.a, end = s.fwd ? 1 : 0;
                    var n = net.nodes[node];
                    if (n.Switch && n.ends[n.toe] == new Vector2Int(s.line.id, end)) { at = endD - d; return node; }
                }
            else
                for (int i = segs.Count - 1; i >= 0; i--)
                {
                    var s = segs[i];
                    if (s.start > d) continue;
                    if (d - s.start > reach) break;
                    int node = s.fwd ? s.line.a : s.line.b, end = s.fwd ? 0 : 1;
                    var n = net.nodes[node];
                    if (n.Switch && n.ends[n.toe] == new Vector2Int(s.line.id, end)) { at = d - s.start; return node; }
                }
            return -1;
        }
    }
}
