using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public static partial class TrackedCity
    {
        // ------------------------------------------------------------------ crawler unit (modelbench/lib/crawler.py)
        public const float TrackR = 6f, TrackW = 11f, ShoePitch = 1.6f, TrackLift = 0.65f;

        /// <summary>Belt pin line (x, z) round the unit: ground run rear → front, idler, sagging top run, sprocket.</summary>
        static List<Vector2> TrackPath(float L, float R, float sag = 0.35f, int n = 720)
        {
            float xi = L / 2 - R, xs = -(L / 2 - R);
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if (t < 0.3f) pts.Add(new Vector2(xs + (xi - xs) * (t / 0.3f), 0f));
                else if (t < 0.5f) { float a = -Mathf.PI / 2 + Mathf.PI * (t - 0.3f) / 0.2f; pts.Add(new Vector2(xi + R * Mathf.Cos(a), R + R * Mathf.Sin(a))); }
                else if (t < 0.8f) { float u = (t - 0.5f) / 0.3f; pts.Add(new Vector2(xi + (xs - xi) * u, 2 * R - sag * Mathf.Sin(Mathf.PI * u))); }
                else { float a = Mathf.PI / 2 + Mathf.PI * (t - 0.8f) / 0.2f; pts.Add(new Vector2(xs + R * Mathf.Cos(a), R + R * Mathf.Sin(a))); }
            }
            return pts;
        }

        static void CylY(CityKit K, CM m, float r, float depth, Vector3 c, int seg = 20) => K.Tube(m, c - new Vector3(0, depth / 2, 0), c + new Vector3(0, depth / 2, 0), r, seg);
        static void CylX(CityKit K, CM m, float r, float depth, Vector3 c, int seg = 20) => K.Tube(m, c - new Vector3(depth / 2, 0, 0), c + new Vector3(depth / 2, 0, 0), r, seg);
        static void CylZ(CityKit K, CM m, float r, float depth, Vector3 c, int seg = 20) => K.Tube(m, c - new Vector3(0, 0, depth / 2), c + new Vector3(0, 0, depth / 2), r, seg);

        /// <summary>One cast shoe on the pin line at (x, z), turned by the belt direction (Blender rotation about y).</summary>
        static void Shoe(CityKit K, float x, float z, float ang)
        {
            float p = ShoePitch, w = TrackW, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            var e = new Vector3(0, -ang, 0);
            Vector3 L(float lx, float lz, float y = 0f) => new Vector3(x + lx * cs - lz * sn, y, z + lx * sn + lz * cs);
            K.Box(CM.Track, new Vector3(p * 0.94f, w, 0.32f), L(0, -0.16f), e);                    // base plate
            K.Box(CM.Track, new Vector3(0.28f, w * 0.98f, 0.34f), L(p * 0.18f, -0.48f), e);       // grouser bar
            K.Box(CM.Track, new Vector3(0.18f, w * 0.9f, 0.2f), L(-p * 0.25f, -0.42f), e);        // secondary rib
            foreach (int sy in new[] { -1, 1 })
            {
                K.Box(CM.Track, new Vector3(p * 0.5f, 0.22f, 0.42f), L(0, 0.2f, sy * w * 0.17f), e);  // guide horns
                foreach (float sx in new[] { -0.5f, 0.5f }) CylY(K, CM.SteelDark, 0.2f, w * 0.18f, L(sx * p * 0.94f, 0.05f, sy * w * 0.32f), 8);   // pin bosses
            }
            K.Box(CM.SteelDark, new Vector3(p * 0.3f, 0.6f, 0.12f), L(0, 0.05f), e);               // sprocket tooth pad
        }

        /// <summary>The belt of shoes at one of four phases (a quarter pitch apart: the meshes take turns as it rolls).</summary>
        public static CityKit Tread(Plan plan, int phase)
        {
            var K = new CityKit(Vector3.zero, plan.p.seed + 17);
            var pts = TrackPath(plan.Lu, TrackR);
            var seg = new float[pts.Count + 1];
            for (int i = 0; i < pts.Count; i++) seg[i + 1] = seg[i] + Vector2.Distance(pts[i], pts[(i + 1) % pts.Count]);
            int count = (int)(seg[pts.Count] / ShoePitch);
            float step = seg[pts.Count] / count;
            int j = 0;
            for (int k = 0; k < count; k++)
            {
                float s = (k + phase * 0.25f) * step;
                while (j < pts.Count - 1 && seg[j + 1] < s) j++;
                Vector2 a = pts[j], b = pts[(j + 1) % pts.Count];
                float f = (s - seg[j]) / Mathf.Max(1e-6f, seg[j + 1] - seg[j]);
                Shoe(K, a.x + (b.x - a.x) * f, a.y + (b.y - a.y) * f + TrackLift, Mathf.Atan2(b.y - a.y, b.x - a.x));
            }
            return K;
        }

        static void Wheel(CityKit K, Vector3 c, float r, float width, int kind)
        {
            if (kind == 0)                                                                          // sprocket + final drive
            {
                CylY(K, CM.Steel, r * 0.86f, width * 0.35f, c, 40);
                for (int k = 0; k < 14; k++)
                {
                    float a = 2 * Mathf.PI * k / 14;
                    K.Box(CM.Steel, new Vector3(0.55f, width * 0.3f, 0.7f), c + new Vector3(r * 0.92f * Mathf.Cos(a), 0, r * 0.92f * Mathf.Sin(a)), new Vector3(0, Mathf.PI / 2 - a, 0));
                }
                for (int k = 0; k < 6; k++)
                {
                    float a = 2 * Mathf.PI * k / 6;
                    CylY(K, CM.SteelDark, r * 0.16f, width * 0.37f, c + new Vector3(r * 0.52f * Mathf.Cos(a), 0, r * 0.52f * Mathf.Sin(a)), 12);
                }
                CylY(K, CM.RustDark, r * 0.34f, width * 0.9f, c, 24);
                CylY(K, CM.SteelDark, r * 0.55f, width * 0.5f, c + new Vector3(0, width * 0.55f, 0), 28);
                for (int k = 0; k < 16; k++)
                {
                    float a = 2 * Mathf.PI * k / 16;
                    K.Box(CM.SteelDark, new Vector3(0.12f, width * 0.5f, r * 0.12f), c + new Vector3(r * 0.58f * Mathf.Cos(a), width * 0.55f, r * 0.58f * Mathf.Sin(a)), new Vector3(0, -a, 0));
                    CylY(K, CM.Steel, 0.07f, 0.12f, c + new Vector3(r * 0.28f * Mathf.Cos(a), width * 0.82f, r * 0.28f * Mathf.Sin(a)), 6);
                }
            }
            else if (kind == 1)                                                                     // idler
            {
                foreach (int sy in new[] { -1, 1 }) CylY(K, CM.Steel, r, width * 0.12f, c + new Vector3(0, sy * width * 0.12f, 0), 40);
                CylY(K, CM.SteelDark, r * 0.9f, width * 0.3f, c, 40);
                for (int k = 0; k < 8; k++)
                {
                    float a = 2 * Mathf.PI * k / 8;
                    K.Box(CM.SteelDark, new Vector3(0.35f, width * 0.42f, r * 0.7f), c + new Vector3(r * 0.45f * Mathf.Cos(a), 0, r * 0.45f * Mathf.Sin(a)), new Vector3(0, -a, 0));
                }
                CylY(K, CM.RustDark, r * 0.3f, width * 0.8f, c, 20);
            }
            else                                                                                    // road wheel / roller
            {
                foreach (int sy in new[] { -1, 1 })
                {
                    CylY(K, CM.Steel, r, width * 0.16f, c + new Vector3(0, sy * width * 0.3f, 0), 24);
                    CylY(K, CM.SteelDark, r * 0.78f, width * 0.06f, c + new Vector3(0, sy * width * 0.4f, 0), 24);
                }
                CylY(K, CM.SteelDark, r * 0.45f, width * 0.9f, c, 16);
                for (int k = 0; k < 5; k++)
                {
                    float a = 2 * Mathf.PI * k / 5;
                    CylY(K, CM.RustDark, r * 0.12f, width * 0.95f, c + new Vector3(r * 0.6f * Mathf.Cos(a), 0, r * 0.6f * Mathf.Sin(a)), 8);
                }
            }
        }

        /// <summary>A crawler unit without its belt (unit-local generator space: +x along the track, z = 0 the ground,
        /// the truck on +y): sprocket, idler, road wheels, rollers, frame, truck, trestle, slewing ring and chain drive.
        /// Instanced per bogie, mirrored with the scale signs.</summary>
        public static CityKit Unit(Plan plan)
        {
            var K = new CityKit(Vector3.zero, plan.p.seed + 23);
            float L = plan.Lu, R = TrackR, W = TrackW, lift = TrackLift;
            float xi = L / 2 - R, xs = -(L / 2 - R);
            Wheel(K, new Vector3(xs, 0, R + lift), R * 0.98f, W, 0);
            Wheel(K, new Vector3(xi, 0, R + lift), R * 0.9f, W, 1);
            float x0 = xs + R * 0.9f, x1 = xi - R * 0.9f;
            var wx = new float[8];
            for (int i = 0; i < 8; i++) { wx[i] = x0 + i * (x1 - x0) / 7; Wheel(K, new Vector3(wx[i], 0, lift + 0.25f + 1.25f), 1.25f, W * 0.7f, 2); }
            var rx = new[] { xs + R + 3, 0f, xi - R - 3 };
            foreach (float x in rx) Wheel(K, new Vector3(x, 0, 2 * R + lift - 0.95f), 0.75f, W * 0.5f, 2);
            // frame: box girder, side plates with bolt rows, equalisers, roller brackets, recoil tensioner
            float fx0 = xs + R * 0.4f, fx1 = xi - R * 0.3f;
            K.Box(CM.Rust, new Vector3(fx1 - fx0, W * 0.34f, 3.2f), new Vector3((fx0 + fx1) / 2, 0, 4.9f));
            foreach (int sy in new[] { -1, 1 })
            {
                K.Box(CM.RustDark, new Vector3(fx1 - fx0 - 2, 0.12f, 2.6f), new Vector3((fx0 + fx1) / 2, sy * W * 0.18f, 4.9f));
                for (int i = 0; i < (int)((fx1 - fx0 - 2) / 1.8f); i++)
                    foreach (float z in new[] { 3.8f, 6.0f }) K.Box(CM.Steel, 0.12f, 0.1f, 0.12f, fx0 + 1 + i * 1.8f, sy * W * 0.245f, z);
            }
            for (int i = 0; i < 8; i += 2)
            {
                float cx = (wx[i] + wx[i + 1]) / 2;
                K.Box(CM.SteelDark, new Vector3(wx[i + 1] - wx[i] + 1.2f, W * 0.22f, 0.9f), new Vector3(cx, 0, 2.6f));
                CylY(K, CM.RustDark, 0.6f, W * 0.4f, new Vector3(cx, 0, 3.3f), 16);
            }
            foreach (float x in rx) K.Box(CM.SteelDark, new Vector3(0.8f, W * 0.25f, 2 * R - 7.2f), new Vector3(x, 0, (2 * R + 6.5f - 0.95f) / 2 + lift / 2));
            CylX(K, CM.Steel, 0.45f, 6.0f, new Vector3(xi - 4.2f, 0, R + lift), 16);
            CylX(K, CM.SteelDark, 0.7f, 2.4f, new Vector3(xi - 6.4f, 0, R + lift), 16);
            K.Box(CM.Rust, new Vector3(3.0f, W * 0.5f, 1.4f), new Vector3(xi - 1.6f, 0, R + lift));
            Truck(K, xs, R, lift, plan.Z0 / plan.s, L / 2 - 3f);
            return K;
        }

        static void Gear(CityKit K, Vector2 c, float r, int teeth, float width, float y)
        {
            CylY(K, CM.Steel, r * 0.88f, width, new Vector3(c.x, y, c.y), Mathf.Max(24, teeth * 2));
            for (int k = 0; k < teeth; k++)
            {
                float a = 2 * Mathf.PI * k / teeth;
                K.Box(CM.Steel, new Vector3(r * 0.22f, width, r * 0.24f), new Vector3(c.x + r * 0.94f * Mathf.Cos(a), y, c.y + r * 0.94f * Mathf.Sin(a)), new Vector3(0, Mathf.PI / 2 - a, 0));
            }
            CylY(K, CM.RustDark, r * 0.3f, width * 1.6f, new Vector3(c.x, y, c.y), 16);
            for (int k = 0; k < 6; k++)
            {
                float a = 2 * Mathf.PI * k / 6 + 0.3f;
                CylY(K, CM.SteelDark, r * 0.14f, width * 1.05f, new Vector3(c.x + r * 0.58f * Mathf.Cos(a), y, c.y + r * 0.58f * Mathf.Sin(a)), 10);
            }
        }

        static List<Vector2> ConvexHull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            List<Vector2> Half(IEnumerable<Vector2> seq)
            {
                var h = new List<Vector2>();
                foreach (var p in seq)
                {
                    while (h.Count >= 2 && (h[h.Count - 1].x - h[h.Count - 2].x) * (p.y - h[h.Count - 2].y) - (h[h.Count - 1].y - h[h.Count - 2].y) * (p.x - h[h.Count - 2].x) <= 0) h.RemoveAt(h.Count - 1);
                    h.Add(p);
                }
                return h;
            }
            var lo = Half(pts);
            var rev = new List<Vector2>(pts); rev.Reverse();
            var up = Half(rev);
            lo.RemoveAt(lo.Count - 1); up.RemoveAt(up.Count - 1);
            lo.AddRange(up);
            return lo;
        }

        /// <summary>The bogie truck on the inboard side: transverse axle beams through the belt loop, a riveted carrier
        /// girder, a braced trestle up to the slewing ring under the hull, levelling cylinders and the chain drive (big
        /// cog on the final drive shaft, roller chain, pinion, gearbox + motor on a column, conduit to the deck).</summary>
        static void Truck(CityKit K, float xs, float R, float lift, float deckZ, float half)
        {
            float zc = R + lift;
            foreach (float x in new[] { -8f, 0f, 8f })
            {
                K.Box(CM.RustDark, new Vector3(1.6f, 8.0f, 2.4f), new Vector3(x, 5.2f, 4.9f));
                CylY(K, CM.SteelDark, 1.0f, 1.0f, new Vector3(x, 9.0f, 4.9f), 20);
            }
            K.Box(CM.Rust, new Vector3(2 * half, 2.4f, 6.0f), new Vector3(0, 10.4f, 6.0f));
            foreach (float z in new[] { 2.85f, 9.15f })
            {
                K.Box(CM.RustDark, new Vector3(2 * half + 0.4f, 3.4f, 0.4f), new Vector3(0, 10.4f, z));
                for (int i = 0; i < (int)(2 * half / 1.2f); i++)
                    foreach (float y in new[] { 9.0f, 11.8f }) K.Box(CM.Steel, 0.12f, 0.12f, 0.1f, -half + 0.6f + i * 1.2f, y, z + 0.25f);
            }
            for (int i = 0; i <= (int)(2 * half / 3); i++)
                foreach (float y in new[] { 9.12f, 11.68f }) K.Box(CM.RustDark, 0.3f, 0.3f, 5.9f, -half + i * 3, y, 6.0f);
            float zt0 = 9.35f, zt1 = deckZ - 2.4f;
            var cols = new[] { new Vector2(-4.5f, 9.6f), new Vector2(4.5f, 9.6f), new Vector2(-4.5f, 13.4f), new Vector2(4.5f, 13.4f) };
            foreach (var c in cols)
            {
                K.Box(CM.Rust, new Vector3(1.2f, 1.2f, zt1 - zt0), new Vector3(c.x, c.y, (zt0 + zt1) / 2));
                K.Box(CM.SteelDark, new Vector3(2.0f, 2.0f, 0.4f), new Vector3(c.x, c.y, zt0 + 0.2f));
            }
            for (float z0 = zt0; z0 < zt1 - 0.5f; z0 += 5f)
            {
                float z1 = Mathf.Min(z0 + 5, zt1);
                foreach (var (a, b) in new[] { (cols[0], cols[1]), (cols[2], cols[3]), (cols[0], cols[2]), (cols[1], cols[3]) })
                {
                    K.Beam(CM.RustDark, new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z1), 0.35f);
                    K.Beam(CM.RustDark, new Vector3(b.x, b.y, z0), new Vector3(a.x, a.y, z1), 0.35f);
                    K.Beam(CM.Rust, new Vector3(a.x, a.y, z1), new Vector3(b.x, b.y, z1), 0.5f);
                }
            }
            K.Box(CM.RustDark, new Vector3(11, 6.2f, 0.6f), new Vector3(0, 11.5f, zt1 + 0.3f));
            CylZ(K, CM.SteelDark, 4.0f, 0.8f, new Vector3(0, 11.5f, zt1 + 1.0f), 40);
            for (int k = 0; k < 28; k++)
            {
                float a = 2 * Mathf.PI * k / 28;
                K.Box(CM.Steel, 0.18f, 0.18f, 0.25f, 3.7f * Mathf.Cos(a), 11.5f + 3.7f * Mathf.Sin(a), zt1 + 1.5f);
            }
            CylZ(K, CM.RustDark, 4.4f, 1.0f, new Vector3(0, 11.5f, zt1 + 1.9f), 40);
            foreach (int sx in new[] { -1, 1 })
            {
                var a = new Vector3(sx * Mathf.Min(13, half - 1), 10.4f, 9.35f); var b = new Vector3(sx * 5.6f, 11.5f, zt1 - 0.6f);
                var m = Vector3.Lerp(a, b, 0.55f);
                K.Beam(CM.SteelDark, a, m, 0.9f);
                K.Beam(CM.Steel, m, b, 0.45f);
                K.Box(CM.RustDark, new Vector3(1.2f, 1.4f, 1.0f), a);
                K.Box(CM.RustDark, new Vector3(1.2f, 1.4f, 1.0f), b);
            }
            // chain drive
            float pz = Mathf.Min(19.6f, deckZ - 4f);
            var big = new Vector2(xs, zc); var pin = new Vector2(xs + 0.6f, pz);
            const float yc = 12.9f;
            CylY(K, CM.SteelDark, 0.6f, 4.4f, new Vector3(xs, 10.8f, zc), 16);
            K.Box(CM.SteelDark, new Vector3(2.0f, 2.6f, 2.0f), new Vector3(xs, 10.4f, zc));
            Gear(K, big, 3.4f, 22, 0.5f, yc);
            Gear(K, pin, 1.3f, 10, 0.5f, yc);
            var ring = new List<Vector2>();
            foreach (var (c, r) in new[] { (big, 3.55f), (pin, 1.45f) })
                for (int k = 0; k < 90; k++) { float a = 2 * Mathf.PI * k / 90; ring.Add(new Vector2(c.x + r * Mathf.Cos(a), c.y + r * Mathf.Sin(a))); }
            var path = ConvexHull(ring);
            var seg = new float[path.Count + 1];
            for (int i = 0; i < path.Count; i++) seg[i + 1] = seg[i] + Vector2.Distance(path[i], path[(i + 1) % path.Count]);
            const float pitch = 0.55f;
            int n = (int)(seg[path.Count] / pitch), j = 0;
            for (int k = 0; k < n; k++)
            {
                float s = k * seg[path.Count] / n;
                while (j < path.Count - 1 && seg[j + 1] < s) j++;
                Vector2 a = path[j], b = path[(j + 1) % path.Count];
                float f = (s - seg[j]) / Mathf.Max(1e-6f, seg[j + 1] - seg[j]);
                float x = a.x + (b.x - a.x) * f, z = a.y + (b.y - a.y) * f, ang = Mathf.Atan2(b.y - a.y, b.x - a.x);
                float off = k % 2 == 1 ? 0.34f : 0.26f;
                foreach (int sy in new[] { -1, 1 }) K.Box(CM.SteelDark, new Vector3(pitch + 0.08f, 0.06f, 0.32f), new Vector3(x, yc + sy * off, z), new Vector3(0, -ang, 0));
                CylY(K, CM.Steel, 0.12f, 0.7f, new Vector3(x, yc, z), 6);
            }
            K.Box(CM.Rust, new Vector3(1.4f, 1.4f, pz - 1.4f - 9.35f), new Vector3(xs + 0.6f, 10.6f, (9.35f + pz - 1.4f) / 2));
            K.Box(CM.SteelDark, new Vector3(3.0f, 2.4f, 2.8f), new Vector3(xs + 0.6f, 10.6f, pz));
            CylY(K, CM.SteelDark, 0.35f, 1.4f, new Vector3(xs + 0.6f, 12.2f, pz), 12);
            CylX(K, CM.RustDark, 1.0f, 3.4f, new Vector3(xs + 0.6f, 10.6f, pz + 2.3f), 24);
            for (int i = 0; i < 9; i++) CylX(K, CM.SteelDark, 1.12f, 0.08f, new Vector3(xs + 0.6f - 1.4f + i * 0.35f, 10.6f, pz + 2.3f), 24);
            K.Box(CM.Steel, new Vector3(0.8f, 0.8f, 0.6f), new Vector3(xs + 0.6f, 10.6f, pz + 3.5f));
            K.Beam(CM.SteelDark, new Vector3(xs + 0.6f, 10.6f, pz + 3.8f), new Vector3(xs + 0.6f, 10.6f, deckZ), 0.25f);
            foreach (int sx in new[] { -1, 1 })                                                      // floods watching the belt
            {
                K.Box(CM.SteelDark, new Vector3(0.7f, 0.55f, 0.4f), new Vector3(sx * 4.5f, 9.0f, 16.0f));
                K.Box(CM.LampCyan, new Vector3(0.6f, 0.45f, 0.04f), new Vector3(sx * 4.5f + sx * 0.2f, 8.7f, 15.8f), new Vector3(0.6f, 0, 0));
            }
        }
    }
}
