using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public static partial class TrackedCity
    {
        // storey module metrics (modelbench/lib/walkable.py, doors at the game's 1.04 × 2.24 m)
        public const float Bay = 3.2f, Wall = 0.30f, Slab = 0.22f, Inner = 0.12f;
        public const float Sill = 0.9f, WinH = 1.45f, WinW = 1.5f, DoorW = 1.04f, DoorH = 2.24f;

        static void Bays(float w, out List<float> bays, out float core)
        {
            int nb = Mathf.Max(2, (int)((w - 1f) / Bay));
            bays = new List<float>();
            for (int i = 0; i < nb; i++) bays.Add(-(nb - 1) * Bay / 2 + i * Bay);
            core = bays[nb / 2];
        }

        static List<float> Partitions(float w, List<float> bays)
        {
            var xs = new List<float> { -w / 2 + Wall };
            for (int i = 1; i < bays.Count - 1; i += 2) xs.Add(bays[i] + Bay / 2);
            xs.Add(w / 2 - Wall);
            return xs;
        }

        /// <summary>One storey of a w × d block in module space (origin at the footprint centre on the slab's underside;
        /// x along the long facades, ±y the long facades): walls 0.3 m with real openings, slab with the stair hole,
        /// corridor along x with a door per flat, the stair core's two flights, balconies (bars / solid / glazed loggia,
        /// awnings, pots, laundry), AC units, dishes and cable runs; the ground storey has the entrance, lobby, shop
        /// counters and window grilles. Structure and facade go into <paramref name="shell"/>, furnishings into
        /// <paramref name="inside"/> (hidden at a distance).</summary>
        public static void Module(CityKit shell, CityKit inside, float w, float d, int seed, float lit, bool ground, CM facade, List<Vector3> lights)
        {
            var rnd = new Rng(seed);
            var M = shell; var I = inside;
            float hw = w / 2, hd = d / 2, fz = Slab;
            Bays(w, out var bays, out float core);
            float cw = Bay - Inner, cx0 = core - cw / 2, cx1 = core + cw / 2;
            float cy0 = 0.9f + Inner, cy1 = hd - Wall;
            // slab with the stair well (open over both flights and the mid landing below)
            M.Span(CM.ConcreteDark, -hw, cx0, -hd, hd, 0, fz);
            M.Span(CM.ConcreteDark, cx1, hw, -hd, hd, 0, fz);
            M.Span(CM.ConcreteDark, cx0, cx1, -hd, cy0 + 0.3f, 0, fz);                                  // arrival strip at the corridor end
            M.Span(CM.ConcreteDark, cx0, cx1, cy1, hd, 0, fz);                                           // the well stays open over the landing (headroom)
            I.Span(CM.Floor, -hw + Wall, cx0, -hd + Wall, hd - Wall, fz, fz + 0.02f, false);
            I.Span(CM.Floor, cx1, hw - Wall, -hd + Wall, hd - Wall, fz, fz + 0.02f, false);
            // long facades: windows per bay, balcony doors in some
            var balconies = new List<(float bx, int sign)>();
            var ops = new List<(float c, float w, float sill, float h, bool door)>();
            foreach (int sign in new[] { -1, 1 })
            {
                ops.Clear();
                foreach (float bx in bays)
                {
                    if (sign > 0 && Mathf.Abs(bx - core) < 0.1f) ops.Add((bx, 0.9f, 1.2f, 0.9f, false));
                    else if (ground && sign < 0 && Mathf.Abs(bx - core) < 0.1f) ops.Add((bx, 1.6f, 0f, 2.4f, true));     // main entrance
                    else if (!ground && rnd.Value < 0.3f)
                    {
                        ops.Add((bx - 0.55f, DoorW, 0.05f, DoorH, true));
                        ops.Add((bx + 0.65f, 0.8f, Sill, WinH, false));
                        balconies.Add((bx, sign));
                    }
                    else ops.Add((bx, WinW, Sill, WinH, false));
                }
                FacadeX(M, w, sign * hd, sign, ops, fz, facade);
                foreach (float bx in bays)
                    if (rnd.Value < 0.18f)
                    {
                        M.Box(CM.Machinery, 0.75f, 0.45f, 0.55f, bx + 1.15f, sign * (hd + 0.25f), fz + 0.6f);               // AC unit
                        M.Box(CM.SteelDark, 0.5f, 0.02f, 0.4f, bx + 1.15f, sign * (hd + 0.48f), fz + 0.6f);
                    }
            }
            int nby = Mathf.Max(1, (int)((d - 2.4f) / Bay));
            var ends = new List<float>();
            for (int i = 0; i < nby; i++) ends.Add(-(nby - 1) * Bay / 2 + i * Bay);
            foreach (int sign in new[] { -1, 1 }) FacadeY(M, d - 2 * Wall, sign * hw, sign, ends, fz, facade);
            foreach (var (bx, sign) in balconies) Balcony(M, rnd, bx, sign, hd, fz, facade);
            foreach (float bx in bays)                                                                                 // dishes, cable runs
            {
                if (rnd.Value < 0.12f)
                {
                    int sg = rnd.Pick(-1, 1);
                    M.Box(CM.SteelDark, 0.04f, 0.3f, 0.04f, bx + 1.2f, sg * (hd + 0.15f), fz + 1.9f);
                    M.Box(CM.Dish, 0.6f, 0.05f, 0.6f, bx + 1.2f, sg * (hd + 0.35f), fz + 1.95f);
                    M.Box(CM.SteelDark, 0.04f, 0.25f, 0.04f, bx + 1.2f, sg * (hd + 0.5f), fz + 1.95f);
                }
                if (rnd.Value < 0.15f)
                {
                    int sg = rnd.Pick(-1, 1);
                    M.Box(CM.SteelDark, 0.04f, 0.04f, Storey, bx + 1.45f, sg * (hd + 0.06f), Storey / 2 + fz / 2);
                }
            }
            if (ground)                                                                                                // window grilles
                foreach (int sign in new[] { -1, 1 })
                    foreach (float bx in bays)
                    {
                        if (sign < 0 && Mathf.Abs(bx - core) < 0.1f) continue;
                        for (int k = 0; k < 7; k++) M.Box(CM.SteelDark, 0.025f, 0.025f, WinH, bx - 0.7f + k * 0.233f, sign * (hd + 0.05f), fz + Sill + WinH / 2);
                        M.Box(CM.SteelDark, 1.5f, 0.025f, 0.025f, bx, sign * (hd + 0.05f), fz + Sill + WinH / 2);
                    }
            // corridor walls with a door per flat; partitions every two bays
            var xs = Partitions(w, bays);
            var apts = new List<(int kind, float a, float b, int side)>();          // kind 0 flat, 1 core, 2 lobby
            foreach (int side in new[] { -1, 1 })
            {
                float y0 = side < 0 ? -0.9f - Inner : 0.9f, y1 = side < 0 ? -0.9f : 0.9f + Inner;
                var doors = new List<(float c, float w)>();
                for (int i = 0; i + 1 < xs.Count; i++)
                {
                    float a = xs[i], b = xs[i + 1];
                    if (side > 0 && a < cx1 && b > cx0) { apts.Add((1, a, b, side)); doors.Add((core, 1.4f)); }
                    else if (ground && side < 0 && a < core && core < b) { apts.Add((2, a, b, side)); doors.Add((core, 2.0f)); }
                    else { apts.Add((0, a, b, side)); doors.Add(((a + b) / 2 - 0.8f, DoorW)); }
                }
                doors.Sort((p, q) => p.c.CompareTo(q.c));
                float x = -hw + Wall;
                foreach (var (dc, dw) in doors)
                {
                    M.Span(CM.Plaster, x, dc - dw / 2, y0, y1, fz, Storey);
                    M.Span(CM.Plaster, dc - dw / 2, dc + dw / 2, y0, y1, fz + DoorH, Storey);
                    x = dc + dw / 2;
                }
                M.Span(CM.Plaster, x, hw - Wall, y0, y1, fz, Storey);
                float ya = side < 0 ? -hd + Wall : 0.9f + Inner, yb = side < 0 ? -0.9f - Inner : hd - Wall;
                for (int i = 1; i + 1 < xs.Count; i++) M.Span(CM.Plaster, xs[i] - Inner / 2, xs[i] + Inner / 2, ya, yb, fz, Storey);
            }
            for (float x = Mathf.Floor(-hw + 2); x < hw - 1; x += 6)                                                       // corridor strip lights
                M.Box(CM.Lamp, 1.2f, 0.1f, 0.05f, x, 0, Storey - 0.04f);
            I.Box(CM.Frame, 0.6f, 0.1f, 0.8f, cx0 - 1.2f, -0.97f, fz + 1.5f);                                               // electrical cabinet
            I.Box(CM.Switch, 0.08f, 0.02f, 0.12f, cx0 - 1.2f, -0.91f, fz + 1.5f);
            // stair core: two flights of 8 risers (up toward the facade, back toward the corridor)
            int steps = 8; float rise = Storey / 16f, run = (cy1 - 1.2f - (cy0 + 0.3f)) / steps, half = (cw - 0.2f) / 2;
            // treads 0.45 m deep under their top (a soffit, not a solid wedge): the flight a storey up leaves headroom
            for (int i = 0; i < steps; i++) { float top = fz + (i + 1) * rise; M.Span(CM.ConcreteDark, cx0 + 0.05f, cx0 + 0.05f + half, cy0 + 0.3f + i * run, cy0 + 0.3f + (i + 1) * run, Mathf.Max(fz, top - 0.45f), top); }
            M.Span(CM.ConcreteDark, cx0 + 0.05f, cx1 - 0.05f, cy1 - 1.2f, cy1, fz + steps * rise - 0.15f, fz + steps * rise);
            for (int i = 0; i < steps; i++)
            {
                float ye = cy1 - 1.2f - i * run;
                float top = fz + (steps + i + 1) * rise;
                M.Span(CM.ConcreteDark, cx1 - 0.05f - half, cx1 - 0.05f, ye - run, ye, top - 0.45f, top);
            }
            M.Span(CM.Frame, cx0 + 0.05f + half - 0.03f, cx0 + 0.05f + half + 0.03f, cy0 + 0.3f, cy1 - 1.2f, fz, fz + 1.0f);  // balustrade wall
            M.Beam(CM.Steel, new Vector3(cx0 + 0.05f + half, cy0 + 0.3f, fz + 1.0f), new Vector3(cx0 + 0.05f + half, cy1 - 1.2f, fz + Storey / 2 + 1f), 0.05f);
            // flats
            foreach (var (kind, a, b, side) in apts)
            {
                if (kind == 1) continue;
                if (kind == 2)                                                                                         // mailboxes, bench, bulb
                {
                    float la = -hd + Wall, lb = -0.9f - Inner;
                    I.Box(CM.Frame, 0.25f, 1.6f, 0.9f, a + 0.2f, (la + lb) / 2, fz + 1.2f);
                    for (int k = 0; k < 4; k++) I.Box(CM.SteelDark, 0.02f, 0.3f, 0.18f, a + 0.33f, (la + lb) / 2 - 0.6f + k * 0.4f, fz + 1.35f);
                    I.Box(CM.Wood, 0.45f, 1.6f, 0.06f, b - 0.4f, (la + lb) / 2, fz + 0.45f);
                    M.Box(CM.Lamp, 0.25f, 0.25f, 0.25f, (a + b) / 2, (la + lb) / 2, Storey - 0.15f);
                    lights.Add(new Vector3((a + b) / 2, (la + lb) / 2, Storey - 0.5f));
                    continue;
                }
                Flat(M, I, rnd, a, b, side, hd, fz, bays, lit, lights, ground);
            }
        }

        static void Flat(CityKit M, CityKit I, Rng rnd, float a, float b, int side, float hd, float fz, List<float> bays, float lit, List<Vector3> lights, bool shop)
        {
            float ya = side < 0 ? -hd + Wall : 0.9f + Inner, yb = side < 0 ? -0.9f - Inner : hd - Wall;
            float cxm = (a + b) / 2, cym = (ya + yb) / 2;
            float fy = side < 0 ? yb : ya;            // corridor side
            float oy = side < 0 ? ya : yb;            // facade side
            int inward = -side;                       // from the facade towards the corridor
            bool on = shop || rnd.Value < lit;
            I.Span(CM.Plaster, a + Inner / 2, b - Inner / 2, Mathf.Min(ya, yb), Mathf.Max(ya, yb), Storey - 0.015f, Storey, false);   // ceiling
            I.Box(CM.SteelDark, 0.02f, 0.02f, 0.6f, cxm, cym, Storey - 0.3f);                                          // pendant cord
            (on ? M : I).Box(on ? CM.Lamp : CM.LampOff, 0.42f, 0.42f, 0.22f, cxm, cym, Storey - 0.7f);
            if (on) lights.Add(new Vector3(cxm, cym, Storey - 0.9f));
            foreach (float bx in bays)                                                                                 // radiators under windows
                if (a + 0.8f < bx && bx < b - 0.8f)
                {
                    for (int k = 0; k < 9; k++) I.Box(CM.Radiator, 0.06f, 0.09f, 0.55f, bx - 0.5f + k * 0.125f, oy + inward * 0.12f, fz + 0.5f);
                    I.Box(CM.Frame, 0.03f, 0.03f, 0.7f, bx + 0.6f, oy + inward * 0.08f, fz + 0.45f);
                }
            float dxs = (a + b) / 2 - 0.8f;
            I.Box(CM.Switch, 0.08f, 0.02f, 0.12f, dxs + 0.75f, fy - inward * 0.015f, fz + 1.0f);
            I.Box(CM.Switch, 0.07f, 0.02f, 0.07f, a + 0.6f, fy - inward * 0.015f, fz + 0.3f);
            I.Box(CM.Switch, 0.07f, 0.02f, 0.07f, b - 2.2f, oy + inward * 0.33f, fz + 0.3f);
            if (shop)
            {
                // a shop unit: counter across the room, shelves on the facade wall, crates, the lamp over the counter
                float cy = (fy + oy) / 2;
                I.Span(CM.Wood, a + 0.6f, b - 0.6f, cy - 0.3f, cy + 0.3f, fz, fz + 1.0f, true);
                I.Span(CM.Wood, a + 0.5f, b - 0.5f, cy - 0.35f, cy + 0.35f, fz + 1.0f, fz + 1.06f, false);
                for (int k = 0; k < 3; k++)
                    I.Span(CM.Wood, a + 0.3f, b - 0.3f, oy + inward * 0.1f, oy + inward * 0.5f, fz + 0.5f + k * 0.6f, fz + 0.54f + k * 0.6f, false);
                for (int k = 0; k < 6; k++)
                    I.Box(rnd.Pick(CM.Wood, CM.Canvas, CM.Rust, CM.Pot), 0.35f, 0.3f, 0.3f, a + 0.6f + k * (b - a - 1.2f) / 5f, oy + inward * 0.3f, fz + 0.72f + (k % 3) * 0.6f);
                I.Box(CM.Wood, 0.7f, 0.7f, 0.7f, b - 0.7f, fy - inward * 0.6f, fz + 0.35f, true);
                return;
            }
            float kx = b - 0.06f, ky = fy - inward * 1.3f;                                                            // kitchen on the partition
            I.Box(CM.Tiles, 0.02f, 2.0f, 0.6f, kx, ky, fz + 1.2f);
            I.Box(CM.Switch, 0.5f, 0.5f, 0.04f, b - 0.35f, ky - 0.6f, fz + 0.92f);
            I.Box(CM.SteelDark, 0.16f, 0.16f, 0.01f, b - 0.47f, ky - 0.72f, fz + 0.945f);
            I.Box(CM.SteelDark, 0.16f, 0.16f, 0.01f, b - 0.23f, ky - 0.48f, fz + 0.945f);
            I.Box(CM.Frame, 0.45f, 0.4f, 0.04f, b - 0.35f, ky + 0.5f, fz + 0.89f);
            I.Box(CM.Switch, 0.6f, 0.6f, 1.7f, b - 0.35f, ky + 1.35f, fz + 0.85f, true);                     // fridge
            I.Box(CM.Plaster, 0.6f, 2.0f, 0.9f, b - 0.35f, ky, fz + 0.45f, true);                             // counter
            I.Box(CM.Frame, 0.35f, 2.0f, 0.6f, b - 0.2f, ky, fz + 1.6f);                                               // wall cupboards
            if (rnd.Value < 0.7f)                                                                                     // picture
            {
                float px = a + (b - a) * rnd.Range(0.3f, 0.6f);
                I.Box(CM.Frame, 0.6f, 0.03f, 0.45f, px, fy - inward * 0.02f, fz + 1.6f);
                I.Box(CM.Canvas, 0.52f, 0.01f, 0.37f, px, fy - inward * 0.035f, fz + 1.6f);
            }
            I.Box(CM.Rug, 2.2f, 1.6f, 0.01f, cxm + 0.8f, cym, fz + 0.03f);
            I.Box(CM.Wood, 1.5f, 2.0f, 0.3f, a + 1.1f, oy + inward * 1.2f, fz + 0.25f, true);                 // bed
            I.Box(CM.Fabric, 1.4f, 1.9f, 0.14f, a + 1.1f, oy + inward * 1.2f, fz + 0.45f);
            I.Box(CM.Cloth, 0.5f, 0.3f, 0.12f, a + 1.1f, oy + inward * 0.4f, fz + 0.56f);                             // pillow
            I.Box(CM.Wood, 0.6f, 0.6f, 2.0f, a + 0.45f, fy - inward * 0.4f, fz + 1.0f, true);                 // wardrobe
            I.Box(CM.Fabric, 2.0f, 0.9f, 0.5f, b - 1.4f, oy + inward * 0.6f, fz + 0.4f, true);                // sofa
            I.Box(CM.Fabric, 2.0f, 0.2f, 0.6f, b - 1.4f, oy + inward * 0.2f, fz + 0.7f);
            I.Box(CM.Wood, 1.2f, 0.8f, 0.05f, b - 1.4f, cym, fz + 0.72f);                                              // table + legs
            foreach (float lx in new[] { -0.45f, 0.45f }) foreach (float ly in new[] { -0.3f, 0.3f }) I.Box(CM.Wood, 0.05f, 0.05f, 0.72f, b - 1.4f + lx, cym + ly, fz + 0.36f);
            if (rnd.Value < 0.6f)                                                                                     // curtains
                foreach (float bx in bays)
                    if (a < bx && bx < b) foreach (float c in new[] { -0.85f, 0.85f }) I.Box(CM.Fabric, 0.35f, 0.04f, 1.8f, bx + c, oy + inward * 0.12f, fz + Sill + 0.8f);
            float dx = (a + b) / 2 - 0.8f;                                                                            // half-open door leaf
            I.Box(CM.Door, 0.04f, 0.95f, DoorH, dx - DoorW / 2 + 0.02f, fy - inward * 0.5f, fz + DoorH / 2);
        }

        static void FacadeX(CityKit M, float length, float yWall, int sign, List<(float c, float w, float sill, float h, bool door)> ops, float fz, CM facade)
        {
            float yi = sign > 0 ? yWall - Wall : yWall, yo = sign > 0 ? yWall : yWall + Wall, ym = (yi + yo) / 2;
            float x = -length / 2;
            ops.Sort((p, q) => p.c.CompareTo(q.c));
            foreach (var (c, w, sill0, h, door) in ops)
            {
                float a = c - w / 2, b = c + w / 2, sill = sill0 + fz;
                M.Span(facade, x, a, yi, yo, 0, Storey);
                M.Span(facade, a, b, yi, yo, 0, sill);
                M.Span(facade, a, b, yi, yo, sill + h, Storey);
                if (!door) M.Span(CM.Glass, a, b, ym - 0.01f, ym + 0.01f, sill, sill + h);
                Frame(M, true, a, b, ym, sill, sill + h, !door);
                x = b;
            }
            M.Span(facade, x, length / 2, yi, yo, 0, Storey);
            float outer = sign > 0 ? yo : yi;                                                                         // panel seam
            M.Span(CM.ConcreteDark, -length / 2, length / 2, outer - 0.03f, outer + 0.03f, Storey - 0.1f, Storey, false);
        }

        static void FacadeY(CityKit M, float length, float xWall, int sign, List<float> centres, float fz, CM facade)
        {
            float xi = sign > 0 ? xWall - Wall : xWall, xo = sign > 0 ? xWall : xWall + Wall, xm = (xi + xo) / 2;
            float y = -length / 2;
            foreach (float c in centres)
            {
                float a = c - WinW * 0.4f, b = c + WinW * 0.4f, sill = Sill + fz;
                M.Span(facade, xi, xo, y, a, 0, Storey);
                M.Span(facade, xi, xo, a, b, 0, sill);
                M.Span(facade, xi, xo, a, b, sill + WinH, Storey);
                M.Span(CM.Glass, xm - 0.01f, xm + 0.01f, a, b, sill, sill + WinH);
                Frame(M, false, a, b, xm, sill, sill + WinH, false);
                y = b;
            }
            M.Span(facade, xi, xo, y, length / 2, 0, Storey);
        }

        static void Frame(CityKit M, bool alongX, float a, float b, float mid, float lo, float hi, bool mullion)
        {
            const float f = 0.06f;
            if (alongX)
            {
                M.Span(CM.Frame, a, b, mid - 0.04f, mid + 0.04f, lo, lo + f, false);
                M.Span(CM.Frame, a, b, mid - 0.04f, mid + 0.04f, hi - f, hi, false);
                M.Span(CM.Frame, a, a + f, mid - 0.04f, mid + 0.04f, lo, hi, false);
                M.Span(CM.Frame, b - f, b, mid - 0.04f, mid + 0.04f, lo, hi, false);
                if (mullion) M.Span(CM.Frame, (a + b) / 2 - f / 2, (a + b) / 2 + f / 2, mid - 0.04f, mid + 0.04f, lo, hi, false);
                M.Span(CM.ConcreteDark, a - 0.05f, b + 0.05f, mid - 0.25f, mid + 0.25f, lo - 0.06f, lo, false);       // sill
            }
            else
            {
                M.Span(CM.Frame, mid - 0.04f, mid + 0.04f, a, b, lo, lo + f, false);
                M.Span(CM.Frame, mid - 0.04f, mid + 0.04f, a, b, hi - f, hi, false);
                M.Span(CM.Frame, mid - 0.04f, mid + 0.04f, a, a + f, lo, hi, false);
                M.Span(CM.Frame, mid - 0.04f, mid + 0.04f, b - f, b, lo, hi, false);
            }
        }

        static void Balcony(CityKit M, Rng rnd, float bx, int sign, float hd, float fz, CM facade)
        {
            float yOut = sign * (hd + 1.2f), yo = yOut - sign * 0.04f;
            M.Span(CM.ConcreteDark, bx - 1.5f, bx + 1.5f, sign * hd, yOut, fz - 0.18f, fz);
            int style = rnd.Pick(0, 0, 1, 2);                       // bars, bars, solid, loggia
            if (style == 1) M.Box(rnd.Pick(facade, CM.ConcreteDark), new Vector3(3.0f, 0.08f, 1.0f), new Vector3(bx, yo, fz + 0.5f), default, true);
            else
            {
                M.Box(CM.Frame, 3.0f, 0.05f, 0.05f, bx, yo, fz + 1.0f);
                M.Box(CM.Frame, 3.0f, 0.04f, 0.04f, bx, yo, fz + 0.12f);
                for (int k = 0; k < 25; k++) M.Box(CM.Frame, 0.02f, 0.02f, 0.88f, bx - 1.44f + k * 0.12f, yo, fz + 0.56f);
                M.SolidWall(new Vector3(bx - 1.5f, yo, 0), new Vector3(bx + 1.5f, yo, 0), fz, fz + 1.05f);
            }
            foreach (int sx in new[] { -1, 1 })
            {
                M.Box(CM.Frame, 0.06f, 1.2f, 1.0f, bx + sx * 1.46f, sign * (hd + 0.6f), fz + 0.5f);
                M.SolidWall(new Vector3(bx + sx * 1.46f, sign * hd, 0), new Vector3(bx + sx * 1.46f, yOut, 0), fz, fz + 1.05f);
            }
            if (style == 2)                                                                                            // glazed loggia
            {
                M.Box(CM.Glass, 3.0f, 0.03f, 1.4f, bx, yo, fz + 1.8f);
                for (int k = 0; k < 5; k++) M.Box(CM.Wood, 0.06f, 0.06f, 1.4f, bx - 1.5f + k * 0.75f, yo, fz + 1.8f);
                M.Box(CM.Wood, 3.0f, 0.08f, 0.08f, bx, yo, fz + 1.08f);
                M.Box(CM.Wood, 3.0f, 0.1f, 0.1f, bx, yo, Storey - 0.12f);
                M.Span(CM.ConcreteDark, bx - 1.5f, bx + 1.5f, sign * hd, yOut, Storey - 0.08f, Storey);
            }
            else if (rnd.Value < 0.45f)                                                                                // awning
            {
                M.Box(CM.Awning, new Vector3(3.1f, 1.5f, 0.04f), new Vector3(bx, sign * (hd + 0.75f), fz + 2.45f), new Vector3(sign * 0.12f, 0, 0));
                M.Box(CM.Frame, 0.03f, 0.03f, 0.5f, bx - 1.4f, sign * (hd + 1.35f), fz + 2.2f);
                M.Box(CM.Frame, 0.03f, 0.03f, 0.5f, bx + 1.4f, sign * (hd + 1.35f), fz + 2.2f);
            }
            int pots = rnd.Range(0, 5);
            for (int k = 0; k < pots; k++)                                                                             // flower pots
            {
                float px = bx - 1.2f + k * 0.6f;
                M.Box(CM.Pot, 0.3f, 0.3f, 0.3f, px, yOut - sign * 0.3f, fz + 0.15f);
                M.Box(CM.Foliage, 0.4f, 0.35f, 0.35f, px, yOut - sign * 0.3f, fz + 0.45f);
            }
            if (style != 2 && rnd.Value < 0.4f)                                                                       // washing line
            {
                M.Box(CM.Frame, 2.6f, 0.01f, 0.01f, bx, sign * (hd + 0.9f), fz + 1.95f);
                int n = rnd.Range(2, 6);
                for (int k = 0; k < n; k++)
                    M.Box(rnd.Pick(CM.Fabric, CM.Cloth, CM.Plaster, CM.Rug), 0.34f, 0.02f, rnd.Range(0.35f, 0.6f), bx - 1.0f + k * 0.45f, sign * (hd + 0.9f), fz + 1.65f);
            }
        }

        /// <summary>Porch (canopy + steps at the -y entrance) and roof slab with parapet and stair hut, block-local.</summary>
        public static void BlockExtras(CityKit M, Block b)
        {
            Bays(b.w, out _, out float core);
            M.Span(CM.ConcreteDark, core - 1.6f, core + 1.6f, -b.d / 2 - 2.2f, -b.d / 2, 2.75f, 2.95f);
            for (int k = 0; k < 3; k++) M.Span(CM.ConcreteDark, core - 1.2f, core + 1.2f, -b.d / 2 - 0.9f - k * 0.3f, -b.d / 2, 0, 0.22f - k * 0.07f);
            float top = b.floors * Storey, rt = top + Slab + 0.1f;
            M.Span(CM.ConcreteDark, -b.w / 2, b.w / 2, -b.d / 2, b.d / 2, top, rt);
            M.Span(CM.ConcreteDark, -b.w / 2 - 0.2f, b.w / 2 + 0.2f, -b.d / 2 - 0.2f, -b.d / 2 + 0.05f, rt, rt + 0.8f);
            M.Span(CM.ConcreteDark, -b.w / 2 - 0.2f, b.w / 2 + 0.2f, b.d / 2 - 0.05f, b.d / 2 + 0.2f, rt, rt + 0.8f);
            M.Span(CM.ConcreteDark, -b.w / 2 - 0.2f, -b.w / 2 + 0.05f, -b.d / 2, b.d / 2, rt, rt + 0.8f);
            M.Span(CM.ConcreteDark, b.w / 2 - 0.05f, b.w / 2 + 0.2f, -b.d / 2, b.d / 2, rt, rt + 0.8f);
            M.Span(CM.ConcreteDark, b.w * 0.025f, b.w * 0.275f, -b.d * 0.2f, b.d * 0.2f, rt, rt + 3.0f);
        }
    }
}
