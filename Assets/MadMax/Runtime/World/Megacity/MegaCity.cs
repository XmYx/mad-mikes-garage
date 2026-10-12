using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The megacity (port of the Blender generator modelbench/lib/megacity.py, scaled for the game): rusted
    /// megastructure towers in a graded plaza, platform clusters cantilevered off them carrying walkable panel blocks
    /// (the rolling cities' storey modules, so every flat can be rented), machinery undersides with braces, hanging
    /// modules, pipes, cable loops and lamps; truss and enclosed bridges between clusters; a cable web; a lower
    /// layer of hanging modules over the plaza; a landing pad with a lit hangar cut into its tower; a fast lift on a
    /// tower face up to every cluster. Generator space: x east, y north, z up from the plaza (CityKit maps it to the
    /// site's local game axes). Pure and thread-safe.</summary>
    public static partial class MegaCity
    {
        public class Params
        {
            public string name = "THE SPIRES";
            public int seed = 11, towers = 5;
            public float spread = 150f, minHeight = 160f, maxHeight = 280f;
            public float deckLow = 55f, deckHigh = 190f, clusters = 1.6f, blockDensity = 0.8f;
            public int minFloors = 5, maxFloors = 12, bridges = 4, cables = 30;
            public bool undercroft = true, pad = true;
            public float lights = 1f, litWindows = 0.25f;
            public Params With(int s) { var c = (Params)MemberwiseClone(); c.seed = s; return c; }
        }

        public class Rect
        {
            public float x0, x1, y0, y1;
            public Rect(float a, float b, float c, float d) { x0 = a; x1 = b; y0 = c; y1 = d; }
            public bool Contains(float x, float y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
            public bool Hit(Rect o, float pad = 0f) => !(x1 + pad <= o.x0 || o.x1 + pad <= x0 || y1 + pad <= o.y0 || o.y1 + pad <= y0);
            public Vector2 C => new Vector2((x0 + x1) / 2, (y0 + y1) / 2);
            public Vector2 Edge(Vector2 p)
            {
                var c = C; float dx = p.x - c.x, dy = p.y - c.y, hw = (x1 - x0) / 2, hd = (y1 - y0) / 2;
                float k = Mathf.Min(dx != 0 ? hw / Mathf.Abs(dx) : 1e9f, dy != 0 ? hd / Mathf.Abs(dy) : 1e9f);
                return new Vector2(c.x + dx * k, c.y + dy * k);
            }
        }

        public class Tower
        {
            public int i; public float x, y, w, d, h, top; public bool lattice;
            public Rect rect;
            public Vector4 notch;          // hangar hole x0, x1, y0, y1
            public Vector2 notchZ;
            public bool hasNotch;
        }

        public class Cluster
        {
            public Tower t; public float z; public List<Rect> rects = new List<Rect>();
            public readonly List<Vector3> roofs = new List<Vector3>();
            /// <summary>The lift on a tower face: generator-space foot (z = 0); it rises to <see cref="z"/>. The shaft's
            /// long side runs along the face (along x on the north face).</summary>
            public Vector3 lift;
            public bool liftAlongX;
            /// <summary>Generator-space direction from the tower face out to the shaft.</summary>
            public Vector2 liftOut;
            /// <summary>The shaft plus its boarding aprons beyond both open ends (kept clear of tower clutter).</summary>
            public Rect ApronRect(float pad = 0f) => liftAlongX
                ? new Rect(lift.x - ShaftHY - Apron - pad, lift.x + ShaftHY + Apron + pad, lift.y - ShaftHX - pad, lift.y + ShaftHX + pad)
                : new Rect(lift.x - ShaftHX - pad, lift.x + ShaftHX + pad, lift.y - ShaftHY - Apron - pad, lift.y + ShaftHY + Apron + pad);
            public Rect ShaftRect(float pad = 0f) => liftAlongX
                ? new Rect(lift.x - ShaftHY - pad, lift.x + ShaftHY + pad, lift.y - ShaftHX - pad, lift.y + ShaftHX + pad)
                : new Rect(lift.x - ShaftHX - pad, lift.x + ShaftHX + pad, lift.y - ShaftHY - pad, lift.y + ShaftHY + pad);
        }

        public class Piece { public string name; public CityKit kit; public bool cutaway, collide = true; }

        public class Built
        {
            public Params p;
            public TrackedCity.Plan plan;                     // blocks + rooms for quarters and CityFolk
            public readonly List<Tower> towers = new List<Tower>();
            public readonly List<Cluster> clusters = new List<Cluster>();
            public readonly List<Piece> pieces = new List<Piece>();
            public readonly List<TrackedCity.BlockBuild> blocks = new List<TrackedCity.BlockBuild>();
            public readonly List<Vector3> spots = new List<Vector3>();
            public Vector3 padCentre; public bool hasPad;
            public Rect padRect, hangarRect; public float padZ;                               // drivable pad + hangar floor (generator space)
            public float Reach;                                 // plaza radius for grading
        }

        /// <summary>Lift shaft (the hole in the decks) half extents across / along the tower face: the platform
        /// (TrackedCity.LiftPlatform, 3.6 × 5.5 m) with 0.1 m clearance. <see cref="ShaftGap"/> = face to shaft.</summary>
        public const float ShaftHX = TrackedCity.LiftHalfX + 0.1f, ShaftHY = TrackedCity.LiftHalfZ + 0.1f, ShaftGap = 2.6f, Apron = 4.5f;

        /// <summary>Half the side of the square plaza the city stands on.</summary>
        public static float PlazaHalf(Params p) => p.spread + 40f;

        /// <summary>Every shaft hole (padded) that crosses the rect.</summary>
        static List<Rect> ShaftsIn(Built B, Rect r, float pad)
        {
            var list = new List<Rect>();
            foreach (var c in B.clusters) { var h = c.ShaftRect(pad); if (h.Hit(r)) list.Add(h); }
            return list;
        }

        /// <summary>Does the segment's footprint come near a shaft (generator space)?</summary>
        static bool NearShaft(Built B, Vector2 a, Vector2 b, float pad, bool apron = false)
        {
            var r = new Rect(Mathf.Min(a.x, b.x) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.y, b.y) + pad);
            foreach (var c in B.clusters)
            {
                var h = apron ? c.ApronRect(pad) : c.ShaftRect(pad);
                if (!h.Hit(r)) continue;
                // the segment against the padded shaft: sample along it
                for (int i = 0; i <= 16; i++) { var q = Vector2.Lerp(a, b, i / 16f); if (h.Contains(q.x, q.y)) return true; }
            }
            return false;
        }

        static readonly Color Warm = new Color(1f, 0.78f, 0.52f), Sodium = new Color(1f, 0.62f, 0.28f), Cyan = new Color(0.55f, 0.85f, 1f);

        public static Built Build(Params p)
        {
            var B = new Built { p = p, Reach = p.spread + 70f };
            var rnd = new TrackedCity.Rng(p.seed);
            var tp = new TrackedCity.Params { name = p.name, seed = p.seed, minFloors = p.minFloors, maxFloors = p.maxFloors, litWindows = p.litWindows, variants = 3 };
            B.plan = new TrackedCity.Plan { p = tp, rnd = rnd, ZD = 0f, Z0 = 0f, s = 1f, L = 2 * p.spread, Wd = 2 * p.spread };
            for (int k = 0; k < 4000 && B.towers.Count < p.towers; k++)                       // towers: dart throwing with spacing
            {
                float x = rnd.Range(-p.spread, p.spread), y = rnd.Range(-p.spread, p.spread);
                bool ok = true;
                foreach (var o in B.towers) if (new Vector2(x - o.x, y - o.y).magnitude < 95f) { ok = false; break; }
                if (!ok) continue;
                var t = new Tower { i = B.towers.Count, x = x, y = y, w = rnd.Range(22f, 34f), d = rnd.Range(22f, 34f), h = rnd.Range(p.minHeight, p.maxHeight), lattice = rnd.Chance(0.3f) };
                t.rect = new Rect(x - t.w / 2, x + t.w / 2, y - t.d / 2, y + t.d / 2);
                t.top = t.h;
                B.towers.Add(t);
            }
            Tower padT = null; float padZ = 0f;
            if (p.pad && B.towers.Count > 0)
            {
                var sorted = new List<Tower>(B.towers);
                sorted.Sort((a, b) => new Vector2(a.x, a.y).sqrMagnitude.CompareTo(new Vector2(b.x, b.y).sqrMagnitude));
                padT = sorted[Mathf.Min(1, sorted.Count - 1)];
                padZ = Mathf.Round((p.deckLow + p.deckHigh) * 0.45f);
                HangarSize(padT, out float hw, out float hd, out float hh);
                float y1 = padT.y - padT.d / 2 - 0.5f;
                padT.hasNotch = true;
                padT.notch = new Vector4(padT.x - hw / 2 - 0.2f, padT.x + hw / 2 + 0.2f, y1 - 6f, y1 + hd + 0.5f);
                padT.notchZ = new Vector2(padZ - 0.5f, padZ + hh + 1f);
            }
            foreach (var t in B.towers)                                                        // clusters and their lift shafts
            {
                var zs = new List<float>();
                int n = Mathf.Max(1, (int)(p.clusters + rnd.Value));
                for (int c = 0; c < n; c++)
                    for (int tries = 0; tries < 20; tries++)
                    {
                        float z = rnd.Range(p.deckLow, Mathf.Min(p.deckHigh, t.h - 30));
                        bool ok = true;
                        foreach (var q in zs) if (Mathf.Abs(z - q) < 42f) ok = false;
                        if (t == padT && Mathf.Abs(z - padZ) < 45f) ok = false;
                        if (ok) { zs.Add(Mathf.Round(z)); break; }
                    }
                int face = 0;
                foreach (float z in zs)
                {
                    var rects = RectsFor(rnd, t);
                    if (HitsTowers(rects, B.towers, t)) rects = rects.GetRange(0, 1);
                    if (HitsTowers(rects, B.towers, t)) continue;
                    bool bad = false;
                    foreach (var oc in B.clusters) if (Mathf.Abs(oc.z - z) < 40) foreach (var r in rects) foreach (var q in oc.rects) if (r.Hit(q, 6)) bad = true;
                    if (bad) continue;
                    var cl = new Cluster { t = t, z = z, rects = rects };
                    float off = ShaftGap + ShaftHX;                                                 // east, west, north faces of the tower
                    cl.liftOut = face == 0 ? Vector2.right : face == 1 ? Vector2.left : Vector2.up;
                    cl.lift = face == 0 ? new Vector3(t.x + t.w / 2 + off, t.y + t.d * 0.18f, 0f)
                            : face == 1 ? new Vector3(t.x - t.w / 2 - off, t.y - t.d * 0.18f, 0f)
                            : new Vector3(t.x + t.w * 0.18f, t.y + t.d / 2 + off, 0f);
                    cl.liftAlongX = face == 2;
                    bool clash = false;
                    foreach (var o in B.towers) if (o != t && cl.ShaftRect(6f).Hit(o.rect)) clash = true;
                    foreach (var oc in B.clusters) if (cl.ShaftRect(2f).Hit(oc.ShaftRect())) clash = true;
                    if (clash) continue;
                    face++;
                    B.clusters.Add(cl);
                }
            }
            foreach (var t in B.towers)
            {
                var K = new CityKit(Vector3.zero, p.seed + t.i);
                TowerKit(K, rnd, p, t, B);
                B.pieces.Add(new Piece { name = "Tower" + t.i, kit = K, cutaway = true });
            }
            if (padT != null)
            {
                var K = new CityKit(Vector3.zero, p.seed + 500);
                B.padCentre = LandingPad(K, rnd, p, padT, padZ, B);
                B.hasPad = true;
                B.pieces.Add(new Piece { name = "LandingPad", kit = K, cutaway = true });
            }
            for (int i = 0; i < B.clusters.Count; i++)
            {
                var cl = B.clusters[i];
                var K = new CityKit(Vector3.zero, p.seed + 1000 + i);
                ClusterKit(K, rnd, p, cl, B);
                B.pieces.Add(new Piece { name = "Cluster" + i, kit = K, cutaway = true });
            }
            var bk = new CityKit(Vector3.zero, p.seed + 2000);                                 // bridges at similar heights
            var pairs = new List<(float d, Cluster a, Cluster b)>();
            for (int i = 0; i < B.clusters.Count; i++)
                for (int j = i + 1; j < B.clusters.Count; j++)
                {
                    var a = B.clusters[i]; var b = B.clusters[j];
                    if (a.t == b.t || Mathf.Abs(a.z - b.z) > 16) continue;
                    float dd = Vector2.Distance(a.rects[0].C, b.rects[0].C);
                    if (dd < 200) pairs.Add((dd, a, b));
                }
            pairs.Sort((u, v) => u.d.CompareTo(v.d));
            int nb = 0;
            foreach (var (_, a, b) in pairs)
            {
                if (nb >= p.bridges) break;
                var pa = a.rects[0].Edge(b.rects[0].C); var pb = b.rects[0].Edge(a.rects[0].C);
                if (Vector2.Distance(pa, pb) < 8) continue;
                var mid = new Rect(Mathf.Min(pa.x, pb.x) + 2, Mathf.Max(pa.x, pb.x) - 2, Mathf.Min(pa.y, pb.y) + 2, Mathf.Max(pa.y, pb.y) - 2);
                bool blocked = false;
                foreach (var o in B.towers) if (o != a.t && o != b.t && mid.Hit(o.rect, 2)) blocked = true;
                if (NearShaft(B, pa, pb, 4.5f)) blocked = true;
                if (blocked) continue;
                Bridge(bk, rnd, new Vector3(pa.x, pa.y, a.z), new Vector3(pb.x, pb.y, b.z));
                nb++;
            }
            if (nb > 0) B.pieces.Add(new Piece { name = "Bridges", kit = bk, cutaway = true });
            var ck = new CityKit(Vector3.zero, p.seed + 3000);                                 // cable web
            var anchors = new List<Vector3>();
            foreach (var t in B.towers)
                for (int k = 0; k < 5; k++)
                {
                    int s = rnd.Range(0, 4);
                    float ax = t.x + (t.w / 2 + 1) * (s == 0 ? 1 : s == 1 ? -1 : rnd.Range(-0.8f, 0.8f));
                    float ay = t.y + (t.d / 2 + 1) * (s == 2 ? 1 : s == 3 ? -1 : rnd.Range(-0.8f, 0.8f));
                    anchors.Add(new Vector3(ax, ay, rnd.Range(40f, t.h - 10)));
                }
            foreach (var c in B.clusters) { anchors.AddRange(c.roofs); foreach (var r in c.rects) { anchors.Add(new Vector3(r.x0, rnd.Range(r.y0, r.y1), c.z - 3)); anchors.Add(new Vector3(r.x1, rnd.Range(r.y0, r.y1), c.z - 3)); } }
            int nc = 0;
            for (int k = 0; k < p.cables * 6 && nc < p.cables && anchors.Count > 1; k++)
            {
                var a = anchors[rnd.Range(0, anchors.Count)]; var b = anchors[rnd.Range(0, anchors.Count)];
                float dist = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                if (dist < 25 || dist > 200 || Mathf.Abs(a.z - b.z) > 50) continue;
                int strands = rnd.Range(2, 6);
                for (int s = 0; s < strands; s++)
                {
                    var o = new Vector3(rnd.Range(-0.8f, 0.8f), rnd.Range(-0.8f, 0.8f), rnd.Range(-0.5f, 0.5f));
                    ck.Cable(a + o, b + o, dist * rnd.Range(0.04f, 0.12f), Mathf.Max(8, (int)(dist / 10)), rnd.Range(0.07f, 0.16f));
                }
                nc++;
            }
            B.pieces.Add(new Piece { name = "CableWeb", kit = ck, collide = false });
            if (p.undercroft)
            {
                var uk = new CityKit(Vector3.zero, p.seed + 4000);
                Undercroft(uk, rnd, p, B);
                B.pieces.Add(new Piece { name = "Undercroft", kit = uk, cutaway = true });
            }
            var gk = new CityKit(Vector3.zero, p.seed + 5000);
            Plaza(gk, rnd, p, B);
            B.pieces.Add(new Piece { name = "Plaza", kit = gk, cutaway = true });
            var sk = new CityKit(Vector3.zero, p.seed + 6000);                                 // lift shafts: their own piece so the cutaway clips them
            Shafts(sk, B);
            B.pieces.Add(new Piece { name = "Shafts", kit = sk, cutaway = true });
            foreach (var b in B.plan.blocks) B.blocks.Add(TrackedCity.BuildBlock(B.plan, b));
            return B;
        }

        /// <summary>A lift gate (generator space, along x, centred on the origin, standing on z = 0): posts and a striped
        /// double bar.</summary>
        public static CityKit Gate(float width, int seed)
        {
            var K = new CityKit(Vector3.zero, seed);
            foreach (float x in new[] { -width / 2 + 0.08f, width / 2 - 0.08f }) K.Box(CM.SteelDark, new Vector3(0.14f, 0.14f, 1.25f), new Vector3(x, 0f, 0.625f));
            int n = Mathf.Max(2, Mathf.RoundToInt(width / 0.6f));
            for (int i = 0; i < n; i++)
            {
                float x = -width / 2 + (i + 0.5f) * width / n;
                var m = (i & 1) == 0 ? CM.PaintYellow : CM.RustDark;
                K.Box(m, new Vector3(width / n, 0.1f, 0.12f), new Vector3(x, 0f, 1.1f));
                K.Box(m, new Vector3(width / n, 0.08f, 0.1f), new Vector3(x, 0f, 0.55f));
            }
            return K;
        }

        static bool HitsTowers(List<Rect> rects, List<Tower> towers, Tower own)
        {
            foreach (var r in rects) foreach (var o in towers) if (o != own && r.Hit(o.rect, 8)) return true;
            return false;
        }

        static void HangarSize(Tower t, out float hw, out float hd, out float hh) { hw = Mathf.Min(34f, t.w * 0.8f); hd = Mathf.Min(28f, t.d * 0.8f); hh = 14f; }

        static void Fbox(CityKit A, CM m, Vector2 c, bool alongX, float along, float o, float h, float zc) =>
            A.Box(m, alongX ? new Vector3(along, o, h) : new Vector3(o, along, h), new Vector3(c.x, c.y, zc));

        static IEnumerable<(Vector2 s, Vector2 d, Vector2 n, float L, bool ax)> Faces(float x0, float x1, float y0, float y1)
        {
            yield return (new Vector2(x0, y0), new Vector2(1, 0), new Vector2(0, -1), x1 - x0, true);
            yield return (new Vector2(x1, y1), new Vector2(-1, 0), new Vector2(0, 1), x1 - x0, true);
            yield return (new Vector2(x1, y0), new Vector2(0, 1), new Vector2(1, 0), y1 - y0, false);
            yield return (new Vector2(x0, y1), new Vector2(0, -1), new Vector2(-1, 0), y1 - y0, false);
        }

        static bool InNotch(Tower t, float x, float y, float z, float pad = 1f) =>
            t.hasNotch && t.notch.x - pad <= x && x <= t.notch.y + pad && t.notch.z - pad <= y && y <= t.notch.w + pad && t.notchZ.x - pad <= z && z <= t.notchZ.y + pad;

        static void CutSpan(CityKit A, CM m, float x0, float x1, float y0, float y1, float z0, float z1, Tower t)
        {
            var n = t.notch; var nz = t.notchZ;
            if (!t.hasNotch || x1 <= n.x || n.y <= x0 || y1 <= n.z || n.w <= y0 || z1 <= nz.x || nz.y <= z0) { A.Span(m, x0, x1, y0, y1, z0, z1); return; }
            A.Span(m, x0, x1, y0, y1, z0, Mathf.Max(z0, nz.x));
            A.Span(m, x0, x1, y0, y1, Mathf.Min(z1, nz.y), z1);
            float za = Mathf.Max(z0, nz.x), zb = Mathf.Min(z1, nz.y);
            A.Span(m, x0, Mathf.Max(x0, n.x), y0, y1, za, zb);
            A.Span(m, Mathf.Min(x1, n.y), x1, y0, y1, za, zb);
            float xa = Mathf.Max(x0, n.x), xb = Mathf.Min(x1, n.y);
            A.Span(m, xa, xb, y0, Mathf.Max(y0, n.z), za, zb);
            A.Span(m, xa, xb, Mathf.Min(y1, n.w), y1, za, zb);
        }
    }
}
