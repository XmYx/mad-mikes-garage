using System.Collections;
using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Rendering;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The board's notices (2026-10-27): one poster per job on offer here today (<see cref="Contracts.Offers"/>,
    /// plus the town boss's cull while it runs) — a wanted boss is a crude face with his gang's mark, a cull the gang's
    /// emblem crossed out, pests the animal, a haul a crate stencil and an arrow, a supply job a barrel, a Guild escort
    /// the fuel drop — on tinted paper, tacked up at a slight angle (by the job's id) and lifting in the wind. Taking a
    /// job tears its poster off (a scrap stays under the tack till tomorrow); a payout stamps a PAID stub for a while;
    /// the road news hangs as a headline sheet. Built only within <see cref="PosterReach"/> m of the followed target.</summary>
    public partial class BountyBoard
    {
        public const float PosterReach = 60f, RefreshSeconds = 20f, StampSeconds = 25f;
        const int Slots = 10, MaxScraps = 6;
        const float Pix = 0.08f;                     // one poster pixel before the half-scale child (4 cm on the board)

        sealed class Poster { public string id; public Transform t; public int slot; public Quaternion rest; public float phase; }

        readonly List<Poster> posters = new List<Poster>();
        readonly List<Transform> scraps = new List<Transform>();
        readonly bool[] used = new bool[Slots];
        Poster news;
        Transform posterRoot, stamp;
        Material voxelMat;
        bool hd;
        float stampUntil, refreshAt;
        int seenVersion = -1, seenDay = -1, scrapDay = -1;

        /// <summary>Job posters up (tests): today's offers here plus the town boss's running cull.</summary>
        public int Posters => posters.Count;
        /// <summary>The road news hangs here as a headline sheet (tests).</summary>
        public bool NewsSheet => news != null;
        /// <summary>Posters torn off by taking their job (tests).</summary>
        public int Torn { get; private set; }
        /// <summary>A PAID stub is up (tests).</summary>
        public bool Stamped => stamp;
        /// <summary>Ids of the jobs posted (tests).</summary>
        public bool HasPoster(string id) { foreach (var p in posters) if (p.id == id) return true; return false; }

        static readonly Contract[] none = new Contract[0];

        void Update()
        {
            if (!PieceBits.Near(transform.position, PosterReach))
            {
                if (posterRoot && posterRoot.gameObject.activeSelf) posterRoot.gameObject.SetActive(false);
                return;
            }
            EnsureRoot();
            if (!posterRoot.gameObject.activeSelf) posterRoot.gameObject.SetActive(true);
            if (Contracts.Version != seenVersion || DayNight.Day != seenDay || Time.time >= refreshAt) Refresh();
            if (stamp && Time.time > stampUntil) { Destroy(stamp.gameObject); stamp = null; }
            Flutter();
        }

        void EnsureRoot()
        {
            if (posterRoot) return;
            posterRoot = new GameObject("Posters").transform;
            posterRoot.SetParent(transform, false);
            posterRoot.gameObject.layer = gameObject.layer;
            hd = PieceBits.HD(gameObject);
            var mr = GetComponent<MeshRenderer>();
            voxelMat = mr ? mr.sharedMaterial : null;
        }

        /// <summary>Re-read today's jobs: posters of jobs gone come down, new jobs go up (also the news sheet).</summary>
        public void Refresh()
        {
            EnsureRoot();
            seenVersion = Contracts.Version; seenDay = DayNight.Day; refreshAt = Time.time + RefreshSeconds;
            if (scrapDay != DayNight.Day) { foreach (var s in scraps) if (s) Destroy(s.gameObject); scraps.Clear(); scrapDay = DayNight.Day; }
            var st = Settlement;
            IList<Contract> offers = st != null ? Contracts.Offers(st) : (IList<Contract>)none;
            for (int i = posters.Count - 1; i >= 0; i--)
            {
                var p = posters[i];
                if (Wanted(offers, p.id)) continue;
                if (p.t) Destroy(p.t.gameObject);
                used[p.slot] = false;
                posters.RemoveAt(i);
            }
            foreach (var c in offers) if (!HasPoster(c.id)) Post(c);
            foreach (var c in Contracts.Active)
                if (c.id.StartsWith("Q") && c.town == town && !c.completed && !c.failed && !HasPoster(c.id)) Post(c);
            bool headlines = TownNews.Near(transform.position, 1).Count > 0;
            if (headlines && news == null) news = Hang(null, "news", 0, -1, "NewsSheet");
            else if (!headlines && news != null) { if (news.t) Destroy(news.t.gameObject); used[news.slot] = false; news = null; }
        }

        bool Wanted(IList<Contract> offers, string id)
        {
            foreach (var c in offers) if (c.id == id) return true;
            foreach (var c in Contracts.Active) if (c.id == id && c.id.StartsWith("Q") && c.town == town && !c.completed && !c.failed) return true;
            return false;
        }

        void Post(Contract c)
        {
            string key = Design(c, out int decal);
            var p = Hang(c.id, key, Hash(c.id) >> 9 & 1, decal, "Poster_" + c.id);
            if (p != null) posters.Add(p);
        }

        /// <summary>The poster's design key and the gang / faction emblem it carries (-1 none).</summary>
        static string Design(Contract c, out int decal)
        {
            decal = -1;
            switch (c.kind)
            {
                case 0:
                {
                    int k = c.title != null ? c.title.LastIndexOf("BOSS OF THE ", System.StringComparison.Ordinal) : -1;
                    decal = GangDecal(k >= 0 ? c.title.Substring(k + 12) : null);
                    return "boss";
                }
                case 1: decal = GangDecal(c.target); return c.id.StartsWith("Q") ? "town" : "cull";
                case 2: return "pest_" + (c.target == "boar" || c.target == "wolf" || c.target == "rat" ? c.target : "dog");
                case 3: return "haul";
                case 4: return "supply";
                case 5: decal = 5; return "escort";      // Fuel Guild drop
                default: return "supply";
            }
        }

        static int GangDecal(string gang)
        {
            if (gang == null) return -1;
            for (int i = 0; i < NpcLore.Gangs.Length; i++) if (NpcLore.Gangs[i] == gang) return i + 1;
            return -1;
        }

        /// <summary>A sheet in a free slot (preferred by the id's hash), tilted and nudged by it.</summary>
        Poster Hang(string id, string key, int variant, int decal, string name)
        {
            int h = Hash(id ?? "news:" + town);
            int slot = -1;
            for (int k = 0; k < Slots; k++) { int s = (h % Slots + k) % Slots; if (!used[s]) { slot = s; break; } }
            if (slot < 0) return null;
            used[slot] = true;
            float jx = ((h >> 4 & 15) - 7.5f) * 0.0016f, jy = ((h >> 12 & 15) - 7.5f) * 0.0016f, tilt = ((h >> 16 & 15) - 7.5f) * 0.9f;
            var at = new Vector3((slot % 5 - 2) * 0.32f + jx, (slot < 5 ? 1.98f : 1.52f) + jy, hd ? 0.046f : 0.06f);
            var t = PieceBits.Shape(posterRoot, name, at, "poster_" + key + variant, hd, g => PaintVoxel(g, key, variant), s => PaintHD(s, key, variant), voxelMat);
            t.localScale = Vector3.one * 0.5f;
            var rest = Quaternion.Euler(0f, 0f, tilt);
            t.localRotation = rest;
            if (decal > 0) Emblem(t, decal, key);
            if (key == "cull" || key == "town")
                PieceBits.Shape(t, "Cross", new Vector3(0f, -4f * Pix, 0.075f), "poster_cross", hd, CrossVoxel, CrossHD, voxelMat);
            return new Poster { id = id, t = t, slot = slot, rest = rest, phase = (h >> 20 & 255) / 255f * 6.28f };
        }

        void Emblem(Transform poster, int decal, string key)
        {
            var mesh = MadMax.Vehicles.Decals.Mesh(decal);
            if (!mesh) return;
            var e = new GameObject("Emblem", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            e.gameObject.layer = poster.gameObject.layer;
            e.SetParent(poster, false);
            // boss: a small gang mark under the face; cull: the emblem fills the sheet; escort: the Guild drop over the arrow
            e.localPosition = key == "boss" ? new Vector3(2.1f * Pix, -7.4f * Pix, 0.04f) : key == "escort" ? new Vector3(0f, -2.6f * Pix, 0.04f) : new Vector3(0f, -4f * Pix, 0.04f);
            e.localScale = Vector3.one * (key == "boss" ? 0.24f : key == "escort" ? 0.62f : 0.82f);
            e.GetComponent<MeshFilter>().sharedMesh = mesh;
            e.GetComponent<MeshRenderer>().sharedMaterial = hd ? HDShapes.Solid : voxelMat;
        }

        // ------------------------------------------------------------------ claimed and paid

        void OnTaken(Contract c)
        {
            if (c == null || c.town != town) return;
            for (int i = 0; i < posters.Count; i++)
            {
                if (posters[i].id != c.id) continue;
                var p = posters[i];
                posters.RemoveAt(i);
                used[p.slot] = false;
                Torn++;
                if (p.t)
                {
                    if (posterRoot && posterRoot.gameObject.activeInHierarchy)
                    {
                        if (scraps.Count < MaxScraps)
                        {
                            var sc = PieceBits.Shape(posterRoot, "Scrap", p.t.localPosition, "poster_scrap", hd, ScrapVoxel, ScrapHD, voxelMat);
                            sc.localScale = Vector3.one * 0.5f; sc.localRotation = p.rest;
                            scraps.Add(sc);
                        }
                        StartCoroutine(TearOff(p.t));
                    }
                    else Destroy(p.t.gameObject);
                }
                break;
            }
            seenVersion = Contracts.Version;                                              // the rest still hang
        }

        IEnumerator TearOff(Transform t)
        {
            var at = t.position - t.up * 0.18f;
            var out_ = transform.forward;
            var paper = (Color)Pal.Cream[3];
            for (int i = 0; i < 4; i++) Fx.Smoke(at + Random.insideUnitSphere * 0.08f, out_ * 0.5f + Vector3.up * 0.15f + Random.insideUnitSphere * 0.2f, 0.035f, paper, 0.7f);
            MadMax.Audio.Sfx.Play("scratch", at, 0.45f, 1.7f, 14f);
            t.SetParent(null, true);
            var v = out_ * 1.1f + Vector3.up * 0.6f + Fx.Wind * 0.15f;
            var spin = new Vector3(Random.Range(-260f, 260f), Random.Range(-120f, 120f), Random.Range(-200f, 200f));
            float s0 = t.localScale.x;
            for (float k = 0f; k < 0.5f && t; k += Time.deltaTime)
            {
                v += (Physics.gravity * 0.35f + Fx.Wind * 0.4f) * Time.deltaTime;
                t.position += v * Time.deltaTime;
                t.Rotate(spin * Time.deltaTime, Space.Self);
                t.localScale = Vector3.one * s0 * (1f - k * 1.4f);
                yield return null;
            }
            if (t) Destroy(t.gameObject);
        }

        void OnPaid(Contract c)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Player) return;
            var me = g.Player.transform.position;
            BountyBoard best = null; float bd = 12f * 12f;
            foreach (var b in All) { if (!b) continue; float d = (b.transform.position - me).sqrMagnitude; if (d < bd) { bd = d; best = b; } }
            if (best != this) return;
            EnsureRoot();
            if (stamp) Destroy(stamp.gameObject);
            stamp = PieceBits.Shape(posterRoot, "PaidStub", new Vector3(0.05f, 1.05f, hd ? 0.046f : 0.05f), "poster_paid", hd, PaidVoxel, PaidHD, voxelMat);
            stamp.localScale = Vector3.one * 0.25f;
            stamp.localRotation = Quaternion.Euler(0f, 0f, -8f);
            stampUntil = Time.time + StampSeconds;
            MadMax.Audio.Sfx.Play("hammer", stamp.position, 0.35f, 1.45f, 12f);
        }

        // ------------------------------------------------------------------ wind

        /// <summary>The sheets lift off the board from their tacks with the wind and its gusts (more face-on).</summary>
        void Flutter()
        {
            float w = Fx.Wind.magnitude;
            if (posters.Count == 0 && news == null) return;
            float face = w > 0.01f ? 0.35f + 0.65f * Mathf.Abs(Vector3.Dot(Fx.Wind / w, transform.forward)) : 0f;
            float amp = (1.5f + 13f * Mathf.Clamp01(w / 6f) * (0.35f + 0.65f * WindDust.Gust)) * face;
            float t = Time.time;
            for (int i = 0; i < posters.Count; i++) Lift(posters[i], amp, t);
            if (news != null) Lift(news, amp * 0.6f, t);
        }

        static void Lift(Poster p, float amp, float t)
        {
            if (!p.t) return;
            float f = Mathf.Sin(t * (4.2f + p.phase * 0.4f) + p.phase) * 0.35f + Mathf.Sin(t * 11.3f + p.phase * 3f) * 0.15f + 0.5f;
            p.t.localRotation = p.rest * Quaternion.Euler(-amp * f, 0f, 0f);
        }

        // ------------------------------------------------------------------ art (7 x 9 pixels, row 0 at the tack)

        static string[] Art(string key) => key switch
        {
            "boss" => new[] { "rrrrrrr", ".......", "..hhh..", ".hsssh.", ".sksks.", ".sssss.", ".skkks.", "..sss..", "k.k...." },
            "cull" => new[] { "kkkkkkk", ".......", ".......", ".......", ".......", ".......", ".......", ".......", "k.kk.k." },
            "town" => new[] { "rrrrrrr", ".......", ".......", ".......", ".......", ".......", ".......", ".......", "k.kk.k." },
            "pest_dog" => new[] { "ooooooo", ".......", ".......", "k....k.", ".kkkkkk", ".kkkkk.", ".k..k..", ".......", "k.kk.k." },
            "pest_boar" => new[] { "ooooooo", ".......", "..kkk..", ".kkkkkk", ".kkkkkc", ".kkkkk.", ".k..k..", ".......", "k.kk.k." },
            "pest_wolf" => new[] { "ooooooo", ".......", "....k.k", "....kkk", "kkkkkkk", ".kkkkk.", "k.k.k..", ".......", "k.kk.k." },
            "pest_rat" => new[] { "ooooooo", ".......", ".......", "...kkk.", "..kkkkk", "kkkkkk.", "k..k.k.", ".......", "k.kk.k." },
            "haul" => new[] { "nnnnnnn", ".wwwww.", ".wkwkw.", ".wwwww.", "....r..", "rrrrrr.", "....r..", ".......", "k.kk.k." },
            "supply" => new[] { "vvvvvvv", "..bbb..", ".bkkkb.", ".bbbbb.", ".bkkkb.", ".bbbbb.", "..bbb..", ".......", "k.kk.k." },
            "escort" => new[] { "ooooooo", ".......", ".......", ".......", ".......", "....r..", "rrrrrr.", "....r..", "k.kk.k." },
            "news" => new[] { "kkkkkkk", ".......", "kkk.kkk", ".......", "g.gg.gg", "gg.gg.g", "g.gg.gg", "gg.g.gg", "g.gg.g." },
            _ => new[] { "kkkkkkk", ".......", ".......", ".......", ".......", ".......", ".......", ".......", "......." },
        };

        static Color32[] PaperRamp(string key, out int idx)
        {
            switch (key)
            {
                case "cull": case "escort": idx = 4; return Pal.Ochre;
                case "haul": idx = 4; return Pal.PaleBlue;
                case "pest_dog": case "pest_boar": case "pest_wolf": case "pest_rat": idx = 2; return Pal.Cream;
                case "news": case "supply": idx = 4; return Pal.Cream;
                default: idx = 3; return Pal.Cream;
            }
        }

        static Color32 Ink(char ch) => ch switch
        {
            'r' => Pal.Crimson[3], 'k' => Pal.Black[1], 'g' => Pal.Black[3], 'h' => Pal.Black[2], 's' => Pal.Olive[3], 'o' => Pal.Ochre[2],
            'n' => Pal.Navy[2], 'w' => Pal.Wood[2], 'b' => Pal.Rust[2], 'c' => Pal.Cream[4], 'v' => Pal.Olive[1], _ => Pal.Black[1],
        };

        static void PaintVoxel(VoxelGrid g, string key, int variant)
        {
            var art = Art(key);
            var ramp = PaperRamp(key, out int idx);
            idx = Mathf.Max(1, idx - variant);
            Color32 paper = ramp[idx], foxed = ramp[idx - 1];
            g.Mat((byte)MadMax.Items.ResourceType.Wood);
            for (int row = 0; row < art.Length; row++)
                for (int col = 0; col < 7; col++)
                {
                    char ch = art[row][col];
                    int x = col - 3, y = -row;
                    if (ch != '.') g.Set(x, y, 0, Pal.Solid(Ink(ch)));
                    else g.Set(x, y, 0, p => Pal.Hash(p, 1271) < 0.12f ? foxed : paper);
                }
            g.Set(0, 0, 1, Pal.Solid(Pal.Crimson[3]));                                          // the tack
        }

        static void PaintHD(HDShapes s, string key, int variant)
        {
            var art = Art(key);
            var ramp = PaperRamp(key, out int idx);
            idx = Mathf.Max(1, idx - variant);
            s.Box(new Vector3(0f, -4f * Pix, 0f), new Vector3(7f * Pix, 9f * Pix, 0.012f), ramp[idx], 0.004f);
            for (int row = 0; row < art.Length; row++)
            {
                string r = art[row];
                for (int col = 0; col < 7;)
                {
                    char ch = r[col];
                    int end = col + 1;
                    while (end < 7 && r[end] == ch) end++;
                    if (ch != '.')
                        s.Box(new Vector3(((col + end - 1) * 0.5f - 3f) * Pix, -row * Pix, 0.0075f), new Vector3((end - col) * Pix, Pix, 0.003f), Ink(ch), 0f);
                    col = end;
                }
            }
            s.Ellipsoid(new Vector3(0f, 0f, 0.006f), new Vector3(0.03f, 0.016f, 0.03f), Pal.Crimson[3], 8, 4, true, Quaternion.Euler(90f, 0f, 0f));
        }

        static void CrossVoxel(VoxelGrid g)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Wood);
            for (int i = -3; i <= 3; i++) { g.Set(i, i, 0, Pal.Solid(Pal.Crimson[3])); g.Set(i, -i, 0, Pal.Solid(Pal.Crimson[3])); }
        }

        static void CrossHD(HDShapes s)
        {
            var size = new Vector3(8.6f * Pix, 0.9f * Pix, 0.004f);
            s.Box(Vector3.zero, size, Pal.Crimson[3], 0f, Quaternion.Euler(0f, 0f, 45f));
            s.Box(new Vector3(0f, 0f, 0.001f), size, Pal.Crimson[3], 0f, Quaternion.Euler(0f, 0f, -45f));
        }

        static void ScrapVoxel(VoxelGrid g)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Wood);
            VoxMat paper = Pal.Solid(Pal.Cream[2]);
            g.Set(-1, 0, 0, paper); g.Set(0, 0, 0, paper); g.Set(1, 0, 0, paper); g.Set(0, -1, 0, paper); g.Set(-1, -1, 0, paper);
            g.Set(0, 0, 1, Pal.Solid(Pal.Crimson[3]));
        }

        static void ScrapHD(HDShapes s)
        {
            s.Box(new Vector3(0f, -0.3f * Pix, 0f), new Vector3(2.6f * Pix, 1.4f * Pix, 0.012f), Pal.Cream[2], 0.004f, Quaternion.Euler(0f, 0f, 12f));
            s.Ellipsoid(new Vector3(0f, 0f, 0.006f), new Vector3(0.03f, 0.016f, 0.03f), Pal.Crimson[3], 8, 4, true, Quaternion.Euler(90f, 0f, 0f));
        }

        // PAID in 3x5 letters on a stub: 17 x 7 pixels, centred on its origin
        static readonly string[] PaidRows = { "rr...r..rrr.rr.", "r.r.r.r..r..r.r", "rr..rrr..r..r.r", "r...r.r..r..r.r", "r...r.r.rrr.rr." };

        static void PaidVoxel(VoxelGrid g)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Wood);
            for (int x = -8; x <= 8; x++)
                for (int y = -3; y <= 3; y++)
                {
                    int row = 2 - y, col = x + 7;
                    bool ink = row >= 0 && row < 5 && col >= 0 && col < 15 && PaidRows[row][col] == 'r';
                    g.Set(x, y, 0, Pal.Solid(ink ? Pal.Crimson[3] : Pal.Cream[4]));
                }
        }

        static void PaidHD(HDShapes s)
        {
            s.Box(Vector3.zero, new Vector3(17f * Pix, 7f * Pix, 0.012f), Pal.Cream[4], 0.004f);
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 15; col++)
                    if (PaidRows[row][col] == 'r')
                        s.Box(new Vector3((col - 7) * Pix, (2 - row) * Pix, 0.0075f), new Vector3(Pix, Pix, 0.003f), Pal.Crimson[3], 0f);
        }

        /// <summary>FNV-1a of a string: the same on every machine and run.</summary>
        static int Hash(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return (int)(h & 0x7fffffff);
        }
    }
}
