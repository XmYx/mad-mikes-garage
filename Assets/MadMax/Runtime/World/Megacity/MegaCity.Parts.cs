using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public static partial class MegaCity
    {
        // ------------------------------------------------------------------ towers
        static void TowerKit(CityKit A, TrackedCity.Rng rnd, Params p, Tower t, Built B)
        {
            float x = t.x, y = t.y, w = t.w, d = t.d, z = -2f;
            bool Clear(Vector2 c, float r) => !NearShaft(B, c, c, r + 0.6f, true);
            for (int k = 0; k < 8; k++)                                                          // buttress legs (none through a lift shaft)
            {
                float a = k * Mathf.PI / 4, ox = Mathf.Cos(a), oy = Mathf.Sin(a);
                float ex = Mathf.Abs(ox) > 0.3f ? x + ox * (w / 2 + 10) : x + ox * w * 0.3f;
                float ey = Mathf.Abs(oy) > 0.3f ? y + oy * (d / 2 + 10) : y + oy * d * 0.3f;
                if (NearShaft(B, new Vector2(ex, ey), new Vector2(x + ox * w * 0.42f, y + oy * d * 0.42f), 2.4f, true)) continue;
                A.Beam(CM.Tower, new Vector3(ex, ey, -1), new Vector3(x + ox * w * 0.42f, y + oy * d * 0.42f, 32), 3.5f, 2.4f, true);
            }
            float latticeTo = t.lattice ? rnd.Range(35f, 60f) : -1f;
            while (z < t.h)
            {
                float top = Mathf.Min(z + rnd.Range(20f, 36f), t.h);
                if (z < latticeTo)
                {
                    top = Mathf.Min(top, latticeTo);
                    float cw = w * 0.55f, cd = d * 0.55f;
                    A.Span(CM.TowerB, x - cw / 2, x + cw / 2, y - cd / 2, y + cd / 2, z, top);
                    foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 }) A.Beam(CM.RustDark, new Vector3(x + sx * w / 2, y + sy * d / 2, z), new Vector3(x + sx * w / 2, y + sy * d / 2, top), 2.4f, -1f, true);
                    var cs = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
                    for (int i = 0; i < 4; i++)
                    {
                        var a = new Vector2(x + cs[i].x * w / 2, y + cs[i].y * d / 2); var b = new Vector2(x + cs[(i + 1) % 4].x * w / 2, y + cs[(i + 1) % 4].y * d / 2);
                        A.Beam(CM.RustDark, new Vector3(a.x, a.y, z), new Vector3(b.x, b.y, top), 1f);
                        A.Beam(CM.RustDark, new Vector3(b.x, b.y, z), new Vector3(a.x, a.y, top), 1f);
                        A.Beam(CM.SteelDark, new Vector3(a.x, a.y, top - 1), new Vector3(b.x, b.y, top - 1), 1.3f);
                    }
                    z = top; continue;
                }
                float sw = w * rnd.Range(0.9f, 1.08f), sd = d * rnd.Range(0.9f, 1.08f), ox2 = rnd.Range(-0.06f, 0.06f) * w, oy2 = rnd.Range(-0.06f, 0.06f) * d;
                float cx = x + ox2, cy = y + oy2;
                var mat = rnd.Pick(CM.Tower, CM.Tower, CM.TowerB);
                CutSpan(A, mat, cx - sw / 2, cx + sw / 2, cy - sd / 2, cy + sd / 2, z, top, t);
                CutSpan(A, CM.SteelDark, cx - sw / 2 - 0.6f, cx + sw / 2 + 0.6f, cy - sd / 2 - 0.6f, cy + sd / 2 + 0.6f, top - 1.2f, top, t);
                bool litSeg = rnd.Chance(0.45f);
                foreach (var (s, dd, n, L, ax) in Faces(cx - sw / 2, cx + sw / 2, cy - sd / 2, cy + sd / 2))
                {
                    int np = Mathf.Max(1, (int)(L / rnd.Pick(8f, 12f, 18f, 99f)));
                    for (int i = 1; i < np; i++)
                    {
                        var c = s + dd * (i * L / np) + n * 0.4f;
                        if (InNotch(t, c.x, c.y, (z + top) / 2, 2f) || !Clear(c, 0.9f)) continue;
                        Fbox(A, CM.RustDark, c, ax, 0.9f, 0.8f, top - 1.2f - z, (z + top - 1.2f) / 2);
                    }
                    if (litSeg && rnd.Chance(0.7f))
                    {
                        int nw = Mathf.Max(1, (int)(L / 2.6f));
                        for (float zr = z + 3; zr < top - 3; zr += 4)
                        {
                            if (rnd.Chance(0.35f)) continue;
                            for (int i = 0; i < nw; i++)
                            {
                                if (rnd.Chance(0.45f)) continue;
                                var c = s + dd * ((i + 0.5f) * L / nw) + n * 0.05f;
                                if (InNotch(t, c.x, c.y, zr)) continue;
                                Fbox(A, rnd.Chance(0.35f) ? CM.WindowLit : CM.Window, c, ax, 1.0f, 0.1f, 0.8f, zr);
                            }
                        }
                    }
                    if (rnd.Chance(0.35f) && top - z > 10)
                    {
                        float a2 = rnd.Range(6f, L - 6), mw = rnd.Range(6f, 14f), md = rnd.Range(3f, 7f), mh = rnd.Range(5f, Mathf.Min(14f, top - z - 3));
                        float zc = rnd.Range(z + mh / 2, top - mh / 2 - 1.5f);
                        var c = s + dd * a2 + n * md / 2;
                        if (!InNotch(t, c.x, c.y, zc, Mathf.Max(mw, mh) / 2 + 2) && Clear(c, Mathf.Max(mw, md) / 2))
                        {
                            Fbox(A, rnd.Pick(CM.Rust, CM.RustDark, CM.Machinery, CM.Tower), c, ax, mw, md, mh, zc);
                            if (rnd.Chance(0.5f)) Fbox(A, rnd.Pick(CM.LampAmber, CM.StripCyan), s + dd * a2 + n * (md + 0.04f), ax, mw * 0.7f, 0.06f, 0.25f, zc + mh * 0.3f);
                        }
                    }
                    if (rnd.Chance(0.3f))
                    {
                        float a2 = rnd.Range(3f, L - 3), r = rnd.Range(0.5f, 1.0f);
                        var c = s + dd * a2 + n * (r + 1f);
                        if (!InNotch(t, c.x, c.y, z + 2, 3) && Clear(c, r * 1.2f))
                        {
                            A.Tube(CM.Pipe, new Vector3(c.x, c.y, z), new Vector3(c.x, c.y, top), r, 12);
                            for (float zz = z + 4; zz < top; zz += 8) A.Tube(CM.RustDark, new Vector3(c.x, c.y, zz), new Vector3(c.x, c.y, zz + 0.4f), r * 1.18f, 12);
                        }
                    }
                }
                z = top;
            }
            float tt = t.h;
            if (rnd.Chance(0.5f))                                                                // head block
            {
                float hw = w * rnd.Range(1.12f, 1.35f), hd = d * rnd.Range(1.12f, 1.35f), hh = rnd.Range(18f, 34f);
                foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 }) A.Beam(CM.RustDark, new Vector3(x + sx * w * 0.42f, y + sy * d * 0.42f, tt - 14), new Vector3(x + sx * hw * 0.45f, y + sy * hd * 0.45f, tt + 2), 1.8f);
                A.Span(rnd.Pick(CM.Tower, CM.Rust), x - hw / 2, x + hw / 2, y - hd / 2, y + hd / 2, tt + 2, tt + 2 + hh);
                A.Span(CM.Machinery, x - hw / 2 - 0.5f, x + hw / 2 + 0.5f, y - hd / 2 - 0.5f, y + hd / 2 + 0.5f, tt + 2, tt + 5);
                tt += 2 + hh;
            }
            for (int k = rnd.Range(2, 6); k > 0; k--)                                            // masts + beacons
            {
                float mx = x + rnd.Range(-w * 0.35f, w * 0.35f), my = y + rnd.Range(-d * 0.35f, d * 0.35f), mh = rnd.Range(10f, 34f);
                A.Beam(CM.SteelDark, new Vector3(mx, my, tt), new Vector3(mx, my, tt + mh), rnd.Range(0.35f, 0.8f));
                A.Box(CM.Beacon, new Vector3(0.7f, 0.7f, 0.7f), new Vector3(mx, my, tt + mh + 0.35f));
                for (int s = 0; s < 3; s++) { float a = s * 2.1f; A.Beam(CM.Steel, new Vector3(mx, my, tt + mh * 0.7f), new Vector3(mx + Mathf.Cos(a) * mh * 0.35f, my + Mathf.Sin(a) * mh * 0.35f, tt), 0.06f); }
            }
            t.top = tt;
        }

        // ------------------------------------------------------------------ clusters
        static List<Rect> RectsFor(TrackedCity.Rng rnd, Tower t)
        {
            var m = new[] { rnd.Range(14f, 42f), rnd.Range(14f, 42f), rnd.Range(14f, 42f), rnd.Range(14f, 42f) };
            var r = new Rect(t.x - t.w / 2 - m[0], t.x + t.w / 2 + m[1], t.y - t.d / 2 - m[2], t.y + t.d / 2 + m[3]);
            var list = new List<Rect> { r };
            if (rnd.Chance(0.45f))
            {
                int side = rnd.Range(0, 4); float L = rnd.Range(26f, 50f), D = rnd.Range(18f, 30f);
                if (side == 0) list.Add(new Rect(r.x1, r.x1 + D, r.y0, r.y0 + Mathf.Min(L, r.y1 - r.y0)));
                else if (side == 1) list.Add(new Rect(r.x0 - D, r.x0, r.y1 - Mathf.Min(L, r.y1 - r.y0), r.y1));
                else if (side == 2) list.Add(new Rect(r.x0, r.x0 + Mathf.Min(L, r.x1 - r.x0), r.y1, r.y1 + D));
                else list.Add(new Rect(r.x1 - Mathf.Min(L, r.x1 - r.x0), r.x1, r.y0 - D, r.y0));
            }
            return list;
        }

        /// <summary>A span with rectangular holes cut out (lift shafts through a deck): recursive split.</summary>
        static void HoledSpan(CityKit A, CM m, float x0, float x1, float y0, float y1, float z0, float z1, List<Rect> holes, bool solid = true, int from = 0)
        {
            if (x1 - x0 < 0.05f || y1 - y0 < 0.05f) return;
            for (int i = from; i < holes.Count; i++)
            {
                var h = holes[i];
                if (x1 <= h.x0 || h.x1 <= x0 || y1 <= h.y0 || h.y1 <= y0) continue;
                HoledSpan(A, m, x0, Mathf.Max(x0, h.x0), y0, y1, z0, z1, holes, solid, i + 1);
                HoledSpan(A, m, Mathf.Min(x1, h.x1), x1, y0, y1, z0, z1, holes, solid, i + 1);
                float xa = Mathf.Max(x0, h.x0), xb = Mathf.Min(x1, h.x1);
                HoledSpan(A, m, xa, xb, y0, Mathf.Max(y0, h.y0), z0, z1, holes, solid, i + 1);
                HoledSpan(A, m, xa, xb, Mathf.Min(y1, h.y1), y1, z0, z1, holes, solid, i + 1);
                return;
            }
            A.Span(m, x0, x1, y0, y1, z0, z1, solid);
        }

        static void ClusterKit(CityKit A, TrackedCity.Rng rnd, Params p, Cluster cl, Built B)
        {
            var t = cl.t; float z = cl.z, th = 2.4f, gz = z - th;
            var holes = new List<Rect>();                                                        // shafts through this deck: its own lift's landing, lifts passing up
            foreach (var o in B.clusters)
                if (o.z >= cl.z) foreach (var r in cl.rects) if (o.ShaftRect().Hit(r)) { holes.Add(o.ShaftRect()); break; }
            foreach (var o in B.clusters)
            {
                if (o.z < cl.z || !holes.Exists(h => h.x0 == o.ShaftRect().x0 && h.y0 == o.ShaftRect().y0)) continue;
                var h = o.ShaftRect(0.12f);
                var c = new[] { new Vector2(h.x0, h.y0), new Vector2(h.x1, h.y0), new Vector2(h.x1, h.y1), new Vector2(h.x0, h.y1) };
                if (o != cl)                                                                     // a lift passing up: railed all round
                    A.Railing(new List<Vector2>(c), z, true, 1.1f, 1.6f, null, true);
                else if (o.liftAlongX)                                                           // the landing: rails on the long sides, gates at the ends (MegaCityHost)
                {
                    A.Railing(new List<Vector2> { c[0], c[1] }, z, false, 1.1f, 1.6f, null, true);
                    A.Railing(new List<Vector2> { c[2], c[3] }, z, false, 1.1f, 1.6f, null, true);
                }
                else
                {
                    A.Railing(new List<Vector2> { c[1], c[2] }, z, false, 1.1f, 1.6f, null, true);
                    A.Railing(new List<Vector2> { c[3], c[0] }, z, false, 1.1f, 1.6f, null, true);
                }
                if (o == cl)                                                                     // landing paint + a lamp
                {
                    var pr = o.ShaftRect(1.2f);
                    A.Span(CM.PaintYellow, pr.x0, pr.x1, pr.y0, pr.y1, z, z + 0.01f, false);
                    A.LampAt(new Vector3(o.lift.x, o.lift.y, z + 4f), Warm, 10f);
                }
            }
            foreach (var r in cl.rects)
            {
                HoledSpan(A, rnd.Pick(CM.Tarmac, CM.DeckPlate), r.x0, r.x1, r.y0, r.y1, gz, z, holes);
                HoledSpan(A, CM.RustDark, r.x0 - 0.4f, r.x1 + 0.4f, r.y0 - 0.4f, r.y1 + 0.4f, gz - 0.6f, gz + 0.6f, holes, false);
                A.Railing(new List<Vector2> { new Vector2(r.x0 + 0.3f, r.y0 + 0.3f), new Vector2(r.x1 - 0.3f, r.y0 + 0.3f), new Vector2(r.x1 - 0.3f, r.y1 - 0.3f), new Vector2(r.x0 + 0.3f, r.y1 - 0.3f) }, z, true, 1.1f, 2f, null, false);
                for (float xx = r.x0 + 1; xx < r.x1; xx += 8) HoledSpan(A, CM.SteelDark, xx - 0.6f, xx + 0.6f, r.y0, r.y1, gz - 3f, gz, holes, false);
                for (float yy = r.y0; yy <= r.y1; yy += 8) HoledSpan(A, CM.RustDark, r.x0, r.x1, yy - 0.4f, yy + 0.4f, gz - 1.8f, gz, holes, false);
                foreach (var (s, dd, n, L, ax) in Faces(r.x0, r.x1, r.y0, r.y1))
                {
                    int nbr = Mathf.Max(1, (int)(L / 20));
                    for (int k = 0; k < nbr; k++)                                                // braces back to the tower
                    {
                        var q = s + dd * ((k + 0.5f) * L / nbr);
                        float tx = Mathf.Clamp(q.x, t.x - t.w / 2, t.x + t.w / 2), ty = Mathf.Clamp(q.y, t.y - t.d / 2, t.y + t.d / 2);
                        float dist = Vector2.Distance(q, new Vector2(tx, ty));
                        if (dist < 6 || dist > 70 || NearShaft(B, q, new Vector2(tx, ty), 1.6f)) continue;
                        A.Beam(CM.RustDark, new Vector3(q.x, q.y, gz - 1.5f), new Vector3(tx, ty, gz - 1.5f - dist * 0.75f), 1.4f, 1.1f);
                        A.Beam(CM.SteelDark, new Vector3(q.x, q.y, gz - 1.5f), new Vector3(tx, ty, gz - 1.5f), 0.9f, 1.2f);
                    }
                }
                int mods = (int)((r.x1 - r.x0) * (r.y1 - r.y0) / 260);
                for (int k = 0; k < mods; k++)                                                   // hanging modules + cable loops
                {
                    float mw = rnd.Range(4f, 13f), md = rnd.Range(4f, 11f), mh = rnd.Range(3f, 9f);
                    if (r.x1 - r.x0 < mw + 1 || r.y1 - r.y0 < md + 1) continue;
                    float mx = rnd.Range(r.x0 + mw / 2, r.x1 - mw / 2), my = rnd.Range(r.y0 + md / 2, r.y1 - md / 2);
                    var mr = new Rect(mx - mw / 2, mx + mw / 2, my - md / 2, my + md / 2);
                    if (t.rect.Hit(mr, 1)) continue;
                    bool inShaft = false;
                    foreach (var h in holes) if (mr.Hit(h, 0.5f)) inShaft = true;
                    if (inShaft) continue;
                    A.Span(rnd.Pick(CM.Machinery, CM.Rust, CM.TowerB, CM.RustDark), mx - mw / 2, mx + mw / 2, my - md / 2, my + md / 2, gz - 3 - mh, gz - 3, false);
                    if (rnd.Chance(0.6f)) A.Span(rnd.Pick(CM.LampAmber, CM.StripCyan, CM.WindowLit), mx - mw * 0.35f, mx + mw * 0.35f, my - md / 2 - 0.05f, my - md / 2, gz - 3 - mh * 0.55f, gz - 3 - mh * 0.55f + 0.3f, false);
                    for (int c = rnd.Range(1, 4); c > 0; c--)
                        A.Cable(new Vector3(mx + rnd.Range(-mw, mw) * 0.4f, my - md / 2, gz - 3 - mh * rnd.Range(0.2f, 0.9f)), new Vector3(mx + rnd.Range(-6f, 6f), my + rnd.Range(-8f, 8f), gz - 3), rnd.Range(2f, 6f), 8, 0.12f);
                }
                for (int k = rnd.Range(2, 5); k > 0; k--)
                {
                    float yy = rnd.Range(r.y0 + 2, r.y1 - 2), rr = rnd.Range(0.4f, 0.9f);
                    bool cross = false;
                    foreach (var h in holes) if (yy > h.y0 - rr && yy < h.y1 + rr && h.x1 > r.x0 && h.x0 < r.x1) cross = true;
                    if (!cross) A.Tube(CM.Pipe, new Vector3(r.x0, yy, gz - 3 - rr), new Vector3(r.x1, yy, gz - 3 - rr), rr, 12);
                }
                int nl = (int)((r.x1 - r.x0) * (r.y1 - r.y0) / 180);
                for (int k = 0; k < nl; k++)
                {
                    float lx = rnd.Range(r.x0, r.x1), ly = rnd.Range(r.y0, r.y1);
                    A.Box(rnd.Chance(0.8f) ? CM.LampAmber : CM.LampCyan, new Vector3(0.6f, 0.6f, 0.2f), new Vector3(lx, ly, gz - 3.1f));
                    if (k % 6 == 0) A.LampAt(new Vector3(lx, ly, gz - 4f), Sodium, 14f);
                }
            }
            // blocks on the deck (the rolling cities' storey modules: walkable, rentable)
            var taken = new List<Rect> { new Rect(t.x - t.w / 2 - 4, t.x + t.w / 2 + 4, t.y - t.d / 2 - 4, t.y + t.d / 2 + 4) };
            foreach (var h in holes) taken.Add(new Rect(h.x0 - 3, h.x1 + 3, h.y0 - 3, h.y1 + 3));                // lift landings stay clear
            for (int k = 0; k < (int)(90 * p.blockDensity); k++)
            {
                var r = cl.rects[rnd.Range(0, cl.rects.Count)];
                float w = rnd.Range(14f, 24f), d = rnd.Range(10.5f, 13f);
                if (r.x1 - r.x0 < w + 6 || r.y1 - r.y0 < d + 8) continue;
                float bx = rnd.Range(r.x0 + 3, r.x1 - 3 - w), by = rnd.Range(r.y0 + 5, r.y1 - 3 - d);
                var b = new Rect(bx, bx + w, by - 2.6f, by + d);                                   // the porch on -y
                bool hit = false;
                foreach (var o in taken) if (b.Hit(o, 2.5f)) { hit = true; break; }
                if (hit) continue;
                taken.Add(b);
                int floors = Mathf.Clamp((int)rnd.Range(p.minFloors, p.maxFloors + 1), p.minFloors, p.maxFloors);
                var blk = new TrackedCity.Block { index = B.plan.blocks.Count, cx = bx + w / 2, cy = by + d / 2, w = w, d = d, floors = floors, seed = p.seed * 97 + B.plan.blocks.Count, z0 = z };
                blk.top = z + floors * TrackedCity.Storey + TrackedCity.Slab + 0.1f;
                B.plan.blocks.Add(blk);
                TrackedCity.Rooms(B.plan, blk);
                cl.roofs.Add(new Vector3(blk.cx, blk.cy, blk.top + 1f));
            }
            foreach (var r in cl.rects)                                                          // open deck spots for residents
                for (int k = 0; k < 16; k++)
                {
                    var c = new Vector2(rnd.Range(r.x0 + 3, r.x1 - 3), rnd.Range(r.y0 + 3, r.y1 - 3));
                    bool clear = !t.rect.Hit(new Rect(c.x - 2, c.x + 2, c.y - 2, c.y + 2));
                    foreach (var o in taken) if (o.Hit(new Rect(c.x - 1.5f, c.x + 1.5f, c.y - 1.5f, c.y + 1.5f))) clear = false;
                    if (clear) B.spots.Add(new Vector3(c.x, c.y, z));
                }
            foreach (var (s, dd, n, L, ax) in Faces(t.rect.x0, t.rect.x1, t.rect.y0, t.rect.y1))
            {
                int nn = Mathf.Max(2, (int)(L / 7));
                for (int k = 1; k < nn; k++)
                {
                    var c = s + dd * (k * L / nn) + n * 1.2f;
                    if (p.lights > 0 && rnd.Chance(0.5f)) { A.Box(CM.SteelDark, new Vector3(0.45f, 0.45f, 0.3f), new Vector3(c.x, c.y, z + 3f)); A.Box(CM.Lamp, new Vector3(0.3f, 0.3f, 0.06f), new Vector3(c.x, c.y, z + 2.83f)); A.LampAt(new Vector3(c.x, c.y, z + 2.4f), Warm, 9f); }
                }
            }
        }

        /// <summary>Lift shafts: frame columns from the plaza up beside each tower to its cluster, a winch hut on top.</summary>
        /// <summary>The lift shafts' frames from the plaza up: corner posts, diagonal bracing on the long sides (the short
        /// ends stay open: the platform is boarded there), a cap and a beacon on top.</summary>
        static void Shafts(CityKit A, Built B)
        {
            foreach (var cl in B.clusters)
            {
                var r = cl.ShaftRect(0.35f);
                float top = cl.z + 4.5f;
                foreach (float x in new[] { r.x0, r.x1 }) foreach (float y in new[] { r.y0, r.y1 })
                    A.Span(CM.Rust, x - 0.22f, x + 0.22f, y - 0.22f, y + 0.22f, -0.3f, top, false);
                for (float zz = 0; zz < cl.z - 2f; zz += 4f)
                    foreach (int side in new[] { 0, 1 })
                    {
                        Vector3 a, b;
                        if (cl.liftAlongX) { float y = side == 0 ? r.y0 : r.y1; a = new Vector3(r.x0, y, zz); b = new Vector3(r.x1, y, zz + 4f); }
                        else { float x = side == 0 ? r.x0 : r.x1; a = new Vector3(x, r.y0, zz); b = new Vector3(x, r.y1, zz + 4f); }
                        if (((int)(zz / 4f) & 1) == 1) { var tmp = a.z; a.z = b.z; b.z = tmp; }
                        A.Beam(CM.RustDark, a, b, 0.15f);
                    }
                foreach (float zz in new[] { cl.z + 3.2f, top - 0.2f })                          // headers over the open ends
                {
                    if (cl.liftAlongX) { A.Span(CM.SteelDark, r.x0 - 0.2f, r.x0 + 0.2f, r.y0, r.y1, zz - 0.3f, zz, false); A.Span(CM.SteelDark, r.x1 - 0.2f, r.x1 + 0.2f, r.y0, r.y1, zz - 0.3f, zz, false); }
                    else { A.Span(CM.SteelDark, r.x0, r.x1, r.y0 - 0.2f, r.y0 + 0.2f, zz - 0.3f, zz, false); A.Span(CM.SteelDark, r.x0, r.x1, r.y1 - 0.2f, r.y1 + 0.2f, zz - 0.3f, zz, false); }
                }
                A.Span(CM.Corrugated, r.x0 - 0.6f, r.x1 + 0.6f, r.y0 - 0.6f, r.y1 + 0.6f, top, top + 2.2f);
                A.Box(CM.Machinery, new Vector3(2.4f, 1.6f, 1.2f), new Vector3(cl.lift.x, cl.lift.y, top + 2.8f));
                A.Box(CM.Beacon, new Vector3(0.4f, 0.4f, 0.4f), new Vector3(cl.lift.x, cl.lift.y, top + 3.6f));
            }
        }

        // ------------------------------------------------------------------ bridges, undercroft, plaza, pad
        static void Bridge(CityKit A, TrackedCity.Rng rnd, Vector3 p, Vector3 q)
        {
            var d = q - p; float L = d.magnitude; var u = d / L; var side = new Vector3(-u.y, u.x, 0).normalized;
            float hw = rnd.Range(2.5f, 4f); bool enclosed = rnd.Chance(0.35f);
            int n = Mathf.Max(2, (int)(L / 6)); var top = new Vector3(0, 0, 4.5f);
            for (int i = 0; i < n; i++) A.Beam(CM.DeckPlate, p + d * (i / (float)n), p + d * ((i + 1) / (float)n), 2 * hw, 0.4f, true);
            foreach (int s in new[] { -1, 1 })
            {
                var o = side * s * hw;
                A.Beam(CM.RustDark, p + o - new Vector3(0, 0, 0.6f), q + o - new Vector3(0, 0, 0.6f), 0.7f, 0.9f);
                if (enclosed)
                {
                    A.Beam(CM.Rust, p + o + top * 0.5f, q + o + top * 0.5f, 0.15f, 4.4f, true);
                    for (int i = 0; i < n; i++) { var c = p + d * ((i + 0.5f) / n) + o * 1.02f + top * 0.55f; A.Beam(rnd.Chance(0.4f) ? CM.WindowLit : CM.Window, c - u * 2f, c + u * 2f, 0.08f, 1.2f); }
                }
                else
                {
                    A.Beam(CM.RustDark, p + o + top, q + o + top, 0.5f, 0.6f);
                    for (int i = 0; i <= n; i++)
                    {
                        var c = p + d * (i / (float)n) + o;
                        A.Beam(CM.RustDark, c, c + top, 0.35f);
                        if (i < n) { var c2 = p + d * ((i + 1) / (float)n) + o; A.Beam(CM.SteelDark, i % 2 == 1 ? c : c + top, i % 2 == 1 ? c2 + top : c2, 0.25f); }
                    }
                    var a = p + o * 0.9f; var b = q + o * 0.9f;
                    A.Railing(new List<Vector2> { new Vector2(a.x, a.y), new Vector2(b.x, b.y) }, p.z, false, 1.1f, 2.5f, null, false);
                }
            }
            if (enclosed) A.Beam(CM.Corrugated, p + new Vector3(0, 0, 4.7f), q + new Vector3(0, 0, 4.7f), 2 * hw + 0.6f, 0.3f);
            int nl = Mathf.Max(1, (int)(L / 40));
            for (int i = 0; i < nl; i++) A.LampAt(p + d * ((i + 0.5f) / nl) + new Vector3(0, 0, 3.8f), Warm, 12f);
        }

        static void Undercroft(CityKit A, TrackedCity.Rng rnd, Params p, Built B)
        {
            var cen = new List<Tower>(B.towers);
            cen.Sort((a, b) => new Vector2(a.x, a.y).sqrMagnitude.CompareTo(new Vector2(b.x, b.y).sqrMagnitude));
            cen = cen.GetRange(0, Mathf.Min(2, cen.Count));
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var t in cen) { x0 = Mathf.Min(x0, t.x - t.w); x1 = Mathf.Max(x1, t.x + t.w); y0 = Mathf.Min(y0, t.y - t.d); y1 = Mathf.Max(y1, t.y + t.d); }
            x0 -= 20; x1 += 20; y0 -= 20; y1 += 20;
            const float zt = 34f, cell = 22f;
            int nx = (int)((x1 - x0) / cell), ny = (int)((y1 - y0) / cell);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (rnd.Chance(0.25f)) continue;
                    float cx = x0 + (i + 0.5f) * cell, cy = y0 + (j + 0.5f) * cell, w = cell - rnd.Range(3f, 6f), d = cell - rnd.Range(3f, 6f);
                    var r = new Rect(cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2);
                    bool hit = false;
                    foreach (var t in B.towers) if (r.Hit(t.rect, 2)) hit = true;
                    foreach (var cl in B.clusters) if (r.Hit(cl.ShaftRect(1f))) hit = true;
                    if (hit) continue;
                    float h = rnd.Range(8f, 16f);
                    A.Span(rnd.Pick(CM.Tower, CM.Tower, CM.TowerB, CM.Rust), r.x0, r.x1, r.y0, r.y1, zt - h, zt + 4);
                    A.Span(CM.SteelDark, r.x0 - 0.5f, r.x1 + 0.5f, r.y0 - 0.5f, r.y1 + 0.5f, zt - h - 1f, zt - h, false);
                    foreach (var (s, dd, n, L, ax) in Faces(r.x0, r.x1, r.y0, r.y1))
                        for (float zz = zt - h + 2; zz < zt - 1; zz += 4)
                            if (rnd.Chance(0.5f)) Fbox(A, rnd.Pick(CM.LampAmber, CM.StripCyan, CM.LampCyan, CM.WindowLit), s + dd * rnd.Range(2f, L - 2) + n * 0.05f, ax, rnd.Range(1.5f, 5f), 0.08f, 0.3f, zz);
                    for (int c = rnd.Range(2, 5); c > 0; c--)
                        A.Cable(new Vector3(rnd.Range(r.x0, r.x1), r.y0, zt - h * rnd.Range(0.3f, 1f)), new Vector3(rnd.Range(r.x0, r.x1), r.y0 - rnd.Range(3f, 9f), zt - h * rnd.Range(0.2f, 0.9f)), rnd.Range(2f, 6f), 8, 0.14f);
                    A.LampAt(new Vector3(cx, cy, zt - h - 2), rnd.Chance(0.7f) ? Sodium : Cyan, 18f);
                }
            var holes = ShaftsIn(B, new Rect(x0 - 2, x1 + 2, y0 - 2, y1 + 2), 1.2f);
            for (int i = 0; i <= nx; i++) HoledSpan(A, CM.SteelDark, x0 + i * cell - 1f, x0 + i * cell + 1f, y0, y1, zt, zt + 4, holes, false);
            for (int j = 0; j <= ny; j++) HoledSpan(A, CM.RustDark, x0, x1, y0 + j * cell - 0.8f, y0 + j * cell + 0.8f, zt, zt + 3, holes, false);
            for (int j = 0; j <= ny; j++) for (int k = rnd.Range(2, 4); k > 0; k--)
            {
                float rr = rnd.Range(0.5f, 1.1f), yy = y0 + j * cell + k * 2.6f;
                if (!NearShaft(B, new Vector2(x0, yy), new Vector2(x1, yy), rr + 0.5f)) A.Tube(CM.Pipe, new Vector3(x0, yy, zt - rr - 1), new Vector3(x1, yy, zt - rr - 1), rr, 12);
            }
        }

        static void Plaza(CityKit A, TrackedCity.Rng rnd, Params p, Built B)
        {
            float R = PlazaHalf(p);
            var pits = new List<Rect>();
            foreach (var cl in B.clusters) pits.Add(cl.ShaftRect());
            HoledSpan(A, CM.Tarmac, -R, R, -R, R, -0.6f, 0.05f, pits);
            foreach (var cl in B.clusters)                                                       // the lift pits: floor, kerb paint, rails on the long sides
            {
                var h = cl.ShaftRect(); var h2 = cl.ShaftRect(0.12f); var pr = cl.ShaftRect(1.2f);
                A.Span(CM.ConcreteDark, h.x0, h.x1, h.y0, h.y1, -1.6f, -1.3f);
                A.Span(CM.PaintYellow, pr.x0, pr.x1, pr.y0, pr.y1, 0.05f, 0.06f, false);
                var c = new[] { new Vector2(h2.x0, h2.y0), new Vector2(h2.x1, h2.y0), new Vector2(h2.x1, h2.y1), new Vector2(h2.x0, h2.y1) };
                if (cl.liftAlongX) { A.Railing(new List<Vector2> { c[0], c[1] }, 0.05f, false, 1.1f, 1.6f, null, true); A.Railing(new List<Vector2> { c[2], c[3] }, 0.05f, false, 1.1f, 1.6f, null, true); }
                else { A.Railing(new List<Vector2> { c[1], c[2] }, 0.05f, false, 1.1f, 1.6f, null, true); A.Railing(new List<Vector2> { c[3], c[0] }, 0.05f, false, 1.1f, 1.6f, null, true); }
            }
            for (float x = -R + 8; x < R; x += 16) A.Span(CM.PaintYellow, x, x + 6, -R + 3, -R + 3.3f, 0.05f, 0.06f, false);
            for (int i = 0; i < 26; i++)                                                         // sheds, tanks, stacks on the plaza
            {
                float x = rnd.Range(-R + 10, R - 10), y = rnd.Range(-R + 10, R - 10);
                var r = new Rect(x - 9, x + 9, y - 9, y + 9);
                bool hit = false;
                foreach (var t in B.towers) if (r.Hit(t.rect, 14)) hit = true;
                foreach (var cl in B.clusters) if (r.Hit(cl.ApronRect(2f))) hit = true;
                if (hit) continue;
                float k = rnd.Value;
                if (k < 0.5f)
                {
                    float w = rnd.Range(7f, 16f), d = rnd.Range(5f, 10f), h = rnd.Range(3.5f, 8f);
                    A.Span(rnd.Pick(CM.Corrugated, CM.Rust, CM.ConcreteDark), x - w / 2, x + w / 2, y - d / 2, y + d / 2, 0, h);
                    A.Span(CM.RustDark, x - w / 2 - 0.4f, x + w / 2 + 0.4f, y - d / 2 - 0.4f, y + d / 2 + 0.4f, h, h + 0.4f);
                    A.Box(rnd.Chance(0.5f) ? CM.WindowLit : CM.Window, new Vector3(w * 0.4f, 0.1f, 1.0f), new Vector3(x, y - d / 2 - 0.05f, h * 0.6f));
                }
                else if (k < 0.8f) A.Tube(rnd.Pick(CM.Rust, CM.StackWhite, CM.Machinery), new Vector3(x, y, 0), new Vector3(x, y, rnd.Range(5f, 12f)), rnd.Range(2.5f, 6f), 24, true);
                else A.Tube(rnd.Pick(CM.StackRed, CM.StackWhite), new Vector3(x, y, 0), new Vector3(x, y, rnd.Range(20f, 45f)), rnd.Range(1f, 2f), 16, true);
            }
            foreach (var cl in B.clusters)                                                       // lamps at the foot of the shafts
            {
                var r = cl.ShaftRect(0.6f);
                A.Box(CM.LampAmber, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(r.x1, r.y0, 2.6f));
                A.Box(CM.LampAmber, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(r.x0, r.y1, 2.6f));
                A.LampAt(new Vector3((r.x0 + r.x1) / 2, (r.y0 + r.y1) / 2, 4f), Warm, 10f);
            }
        }

        static Vector3 LandingPad(CityKit A, TrackedCity.Rng rnd, Params p, Tower t, float z, Built B)
        {
            const float W = 44f, D = 38f;
            float x0 = t.x - W / 2, x1 = t.x + W / 2, y1 = t.y - t.d / 2 - 0.5f, y0 = y1 - D;
            B.padRect = new Rect(x0, x1, y0, y1); B.padZ = z;
            A.Span(CM.Tarmac, x0, x1, y0, y1, z - 3f, z);
            A.Span(CM.RustDark, x0 - 0.5f, x1 + 0.5f, y0 - 0.5f, y1, z - 3.6f, z - 2.4f, false);
            A.Railing(new List<Vector2> { new Vector2(x0 + 0.4f, y1), new Vector2(x0 + 0.4f, y0 + 0.4f), new Vector2(x1 - 0.4f, y0 + 0.4f), new Vector2(x1 - 0.4f, y1) }, z, false, 1.1f, 2f, null, false);
            for (int k = 0; k < 7; k++) A.Span(CM.PaintYellow, x0 + 4 + k * 6f, x0 + 7 + k * 6f, y0 + 2f, y0 + 2.5f, z, z + 0.02f, false);
            float cy = (y0 + y1) / 2 - 3;
            for (int a = 0; a < 32; a++) { float ang = a * Mathf.PI / 16; A.Box(CM.PaintYellow, new Vector3(2f, 0.4f, 0.02f), new Vector3(t.x + Mathf.Cos(ang) * 12, cy + Mathf.Sin(ang) * 12, z + 0.01f), new Vector3(0, 0, ang + Mathf.PI / 2)); }
            foreach (float xx in new[] { x0 + 1.5f, x1 - 1.5f }) for (int k = 0; k < 9; k++) A.Box(CM.LampAmber, new Vector3(0.35f, 0.35f, 0.2f), new Vector3(xx, y0 + 2 + k * (D - 4) / 8, z + 0.1f));
            for (int k = 0; k < 6; k++)
            {
                float xx = x0 + 4 + k * (W - 8) / 5;
                A.Beam(CM.RustDark, new Vector3(xx, y0 + 2, z - 3.5f), new Vector3(xx, y1, z - 3.5f - D * 0.7f), 1.3f);
                A.Span(CM.SteelDark, xx - 0.6f, xx + 0.6f, y0, y1, z - 6, z - 3, false);
            }
            for (int k = 0; k < 12; k++) { float mw = rnd.Range(4f, 9f), md = rnd.Range(3f, 7f), mh = rnd.Range(2f, 5f), mx = rnd.Range(x0 + 5, x1 - 5), my = rnd.Range(y0 + 4, y1 - 4); A.Span(rnd.Pick(CM.Machinery, CM.Rust, CM.RustDark), mx - mw / 2, mx + mw / 2, my - md / 2, my + md / 2, z - 6 - mh, z - 6, false); }
            for (int k = 0; k < 14; k++) { var q = new Vector3(rnd.Range(x0, x1), rnd.Range(y0, y0 + 10), z - 6); A.Cable(q, new Vector3(q.x + rnd.Range(-10f, 10f), q.y + rnd.Range(6f, 18f), z - 6), rnd.Range(2f, 7f), 8, 0.14f); }
            HangarSize(t, out float hw, out float hd, out float hh);
            float hx0 = t.x - hw / 2, hx1 = t.x + hw / 2, hy0 = y1 - 0.2f, hy1 = y1 + hd;
            B.hangarRect = new Rect(hx0, hx1, hy0, hy1);
            A.Span(CM.TowerB, hx0 - 2, hx1 + 2, hy0 - 1.2f, hy0, z + hh, z + hh + 5);
            A.Span(CM.SteelDark, hx0, hx1, hy0, hy1, z - 0.4f, z);
            A.Span(CM.Tower, hx0 - 1.2f, hx0, hy0, hy1, z, z + hh);
            A.Span(CM.Tower, hx1, hx1 + 1.2f, hy0, hy1, z, z + hh);
            A.Span(CM.Machinery, hx0, hx1, hy1 - 1.2f, hy1, z, z + hh);
            A.Span(CM.SteelDark, hx0, hx1, hy0, hy1, z + hh, z + hh + 1f);
            for (int k = 0; k < 7; k++)
            {
                float yy = hy0 + 3 + k * (hd - 5) / 6;
                A.Span(k % 2 == 1 ? CM.LampCyan : CM.LampAmber, hx0 + 4, hx1 - 4, yy - 0.15f, yy + 0.15f, z + hh - 0.3f, z + hh, false);
                A.Span(CM.SteelDark, hx0, hx1, yy - 0.4f, yy + 0.4f, z + hh - 1.4f, z + hh - 0.3f, false);
            }
            foreach (int s in new[] { -1, 1 })
            {
                float gx = t.x + s * (hw / 2 - 3);
                A.Span(CM.DeckPlate, gx - 2.5f, gx + 2.5f, hy0 + 2, hy1 - 2, z + 6, z + 6.3f);
                A.Railing(new List<Vector2> { new Vector2(gx - s * 2.4f, hy0 + 2), new Vector2(gx - s * 2.4f, hy1 - 2) }, z + 6.3f, false, 1.1f, 1.5f, null, false);
                A.Ladder(new Vector3(gx + s * 1.5f, hy0 + 3, z), new Vector3(gx + s * 1.5f, hy0 + 3, z + 6.3f), 0.6f);
                A.Span(CM.StripCyan, Mathf.Min(gx - s * 2.6f, gx - s * 2.5f), Mathf.Max(gx - s * 2.6f, gx - s * 2.5f), hy0 + 2, hy1 - 2, z + 5.6f, z + 5.8f, false);
                for (int k = 0; k < 4; k++) A.Span(CM.Machinery, gx + s * 1.2f - 1, gx + s * 1.2f + 1, hy0 + 5 + k * 5, hy0 + 7 + k * 5, z, z + rnd.Range(1.5f, 3.5f));
            }
            for (int k = 0; k < 4; k++) A.LampAt(new Vector3(t.x - 12 + k * 8, hy0 + hd / 2, z + hh - 2), Cyan, 14f);
            foreach (float xx in new[] { x0 + 2, x1 - 2 })
            {
                A.Beam(CM.SteelDark, new Vector3(xx, y0 + 2, z), new Vector3(xx, y0 + 2, z + 12), 0.4f, -1f, true);
                A.Box(CM.SteelDark, new Vector3(0.7f, 0.55f, 0.4f), new Vector3(xx, y0 + 2, z + 12.2f));
                A.LampAt(new Vector3((xx + t.x) / 2, (y0 + y1) / 2, z + 6), Warm, 22f);
            }
            for (int k = 0; k < 8; k++) { float s = rnd.Range(0.8f, 1.3f); A.Box(rnd.Pick(CM.Wood, CM.Rust, CM.HullWhite), new Vector3(s, s, s), new Vector3(rnd.Range(x0 + 3, x0 + 10), rnd.Range(y0 + 6, y1 - 4), z + s / 2), new Vector3(0, 0, rnd.Range(0f, 1.5f)), true); }
            return new Vector3(t.x, cy, z);
        }
    }
}
