using UnityEngine;

namespace MadMax.World
{
    /// <summary>What the HD terrain shader needs to draw a town's streets as vectors at one point (see
    /// <see cref="TownPlan.Surface"/>; packed per vertex by DeformableTerrain.FillMeshHD into uv1 / uv4 / uv5).</summary>
    public struct TownSurface
    {
        /// <summary>Signed distance (m) to the nearest carriageway edge, negative on the asphalt (union of the paved
        /// streets and roundabout rings, islands cut out) and to the outer edge of the sidewalks.</summary>
        public float edge, walkEdge;
        /// <summary>Marking frame: street = signed lateral (+ left of a→b) and distance along; ring = radial offset from
        /// the ring's centre line and arc length; car park = lot x across the frontage, y in from the front edge.</summary>
        public float lat, along;
        /// <summary>Street / ring half width; car park: half the lot width.</summary>
        public float half;
        /// <summary>Street: distance past the junction box at the nearer end (99: none); car park: depth of the tarmac.</summary>
        public float end;
        /// <summary><see cref="Kind"/> + 8 zebra + 16 roundabout end + 32 incoming lane on lat &gt; 0 + 64 × crossing
        /// width in quarter metres (integers, exact in floats; HDLit decodes them).</summary>
        public float code;
    }

    public sealed partial class TownPlan
    {
        /// <summary>Marking kinds of <see cref="TownSurface.code"/>.</summary>
        public const int SNone = 0, SArterial = 1, SRing = 2, SLocal = 3, SRoundabout = 4, SParking = 5, SForecourt = 6;

        /// <summary>The street surface fields at a plan point (any thread, read-only). False outside the plan's index.</summary>
        public bool Surface(Vector2 l, out TownSurface f)
        {
            f = new TownSurface { edge = 20f, walkEdge = 20f, end = 99f };
            int c = CellOf(l);
            if (c < 0) return false;
            float edge = 20f, walkEdge = 20f;
            TownStreet best = null, near = null;
            float bestRatio = 99f, bestT = 0f, bestLat = 0f, nearD = 99f, nearT = 0f, nearLat = 0f;
            foreach (int id in cStreets[c])
            {
                var s = streets[id];
                if (!s.paved || s.kind == StreetKind.Pedestrian) continue;           // squares and dirt lanes keep their cells
                float d = Project(s, l, out float t, out float lat);
                edge = Mathf.Min(edge, d - s.half);
                walkEdge = Mathf.Min(walkEdge, d - s.half - s.walk);
                if (d <= s.half && d / s.half < bestRatio) { bestRatio = d / s.half; best = s; bestT = t; bestLat = lat; }
                if (d - s.half < nearD) { nearD = d - s.half; near = s; nearT = t; nearLat = lat; }
            }
            TownJunction onRing = null;
            float ringR = 0f;
            foreach (int id in cJuncs[c])
            {
                var j = junctions[id];
                float r = (l - j.p).magnitude;
                edge = Mathf.Min(edge, Mathf.Max(j.island - r, r - j.ring));
                walkEdge = Mathf.Min(walkEdge, r - j.ring - j.walk);
                if (r >= j.island && r <= j.ring) { onRing = j; ringR = r; }
            }
            foreach (int id in cJuncs[c])                                                   // the islands are no carriageway
            {
                var j = junctions[id];
                float cut = j.island - (l - j.p).magnitude;
                edge = Mathf.Max(edge, cut); walkEdge = Mathf.Max(walkEdge, cut);
            }
            f.edge = edge; f.walkEdge = walkEdge;
            if (onRing != null)
            {
                f.lat = ringR - onRing.Centre;
                f.along = Mathf.Atan2(l.y - onRing.p.y, l.x - onRing.p.x) * onRing.Centre;
                f.half = (onRing.ring - onRing.island) * 0.5f;
                f.code = SRoundabout;
                return true;
            }
            if (best == null && edge > 0f && Parking(l, ref f)) return true;
            var s0 = best ?? near;
            if (s0 == null) return true;
            float t0 = best != null ? bestT : nearT, lat0 = best != null ? bestLat : nearLat;
            f.lat = lat0; f.along = t0; f.half = s0.half;
            int kind = s0.kind == StreetKind.Arterial ? SArterial : s0.kind == StreetKind.Ring ? SRing
                     : s0.kind == StreetKind.Local && (st.kind != Biome.Village || s0.path) ? SLocal : SNone;
            int flags = 0, walk = 0;
            for (int e = 0; e < 2; e++)                                                      // the nearer junction's paint
            {
                int ji = e == 0 ? s0.juncA : s0.juncB;
                if (ji < 0) continue;
                var j = junctions[ji];
                float past = (e == 0 ? t0 : s0.len - t0) - (j.ring > 0f ? j.ring : j.box);
                if (past >= f.end) continue;
                f.end = past;
                flags = (j.zebra ? 1 : 0) + (j.ring > 0f ? 2 : 0) + (e == 0 ? 4 : 0);          // right-hand traffic: lat > 0 comes in at a
                walk = Mathf.RoundToInt(Mathf.Max(2.2f, j.walk) * 4f);
            }
            f.code = kind + 8 * flags + 64 * walk;
            return true;
        }

        /// <summary>Car park / fuel station forecourt frame within half a metre of the lot (so the lot's edge is drawn
        /// from one frame on both sides).</summary>
        bool Parking(Vector2 l, ref TownSurface f)
        {
            int c = CellOf(l);
            foreach (int id in cLots[c])
            {
                var lot = lots[id];
                if (lot.use != LotUse.Parking && lot.use != LotUse.GasStation) continue;
                if (l.x < lot.min.x - 0.5f || l.x > lot.max.x + 0.5f || l.y < lot.min.y - 0.5f || l.y > lot.max.y + 0.5f) continue;
                lot.Frame(l, out float x, out float y);
                f.lat = x; f.along = y; f.half = lot.Width * 0.5f;
                bool park = lot.use == LotUse.Parking;
                f.end = park ? lot.Depth : lot.Depth - 5f;
                f.code = park ? SParking : SForecourt;
                return true;
            }
            return false;
        }
    }
}
