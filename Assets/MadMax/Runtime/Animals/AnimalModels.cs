using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Meshes and joint positions of one species (shared by every animal of it).</summary>
    public class AnimalMeshes
    {
        public Mesh body, head, leg, tail, wing, saddle, segment, snakeHead, rattle;
        public Vector3 neck, tailRoot, wingRoot, seat;         // local pivots (m)
        public Vector3[] legRoots;                              // FL FR BL BR (birds: L R)
        public float legLen, segLen;
        public int segments;
    }

    /// <summary>Procedural voxel animals (roadmap 23): a quadruped generator driven by the species' proportions
    /// (barrel torso with belly shading and coat patterns, neck + head with snout, ears, horns, tusks, mane or beard,
    /// four legs with hooves or paws, tail), birds (plump or big-winged, combs, bald heads) and snakes (patterned
    /// segments with a rattle). Pivots come back in metres for the gait animation.</summary>
    public static class AnimalModels
    {
        static readonly Dictionary<string, AnimalMeshes> cache = new Dictionary<string, AnimalMeshes>();

        public static AnimalMeshes For(AnimalDef d)
        {
            if (cache.TryGetValue(d.id, out var m) && m.body) return m;       // meshes die with play mode
            m = d.plan == BodyPlan.Bird ? Bird(d) : d.plan == BodyPlan.Snake ? Snake(d) : Quadruped(d);
            cache[d.id] = m;
            return m;
        }

        static void Ellipsoid(VoxelGrid g, Vector3 c, Vector3 r, float power, System.Func<Vector3Int, Color32> paint)
        {
            for (int x = Mathf.FloorToInt(c.x - r.x); x <= Mathf.CeilToInt(c.x + r.x); x++)
            for (int y = Mathf.FloorToInt(c.y - r.y); y <= Mathf.CeilToInt(c.y + r.y); y++)
            for (int z = Mathf.FloorToInt(c.z - r.z); z <= Mathf.CeilToInt(c.z + r.z); z++)
            {
                float v = Mathf.Pow(Mathf.Abs((x - c.x) / Mathf.Max(0.5f, r.x)), power) + Mathf.Pow(Mathf.Abs((y - c.y) / Mathf.Max(0.5f, r.y)), power) + Mathf.Pow(Mathf.Abs((z - c.z) / Mathf.Max(0.5f, r.z)), power);
                if (v <= 1f) g.Set(x, y, z, p => paint(p));
            }
        }

        static Mesh Build(VoxelGrid g, string name, float s) { g.Bevel(); return VoxelMesher.Build(g, name, s); }

        // ------------------------------------------------------------------ four legs

        static AnimalMeshes Quadruped(AnimalDef d)
        {
            float s = VoxelMesher.DefaultSize * d.scale;
            int L = d.len, D = d.depth, W = d.width, Lg = d.leg;
            var coat = d.Coat.Mat(3100 + L); var belly = d.Belly.Mat(3101 + L); var accent = d.Accent.Mat(3102 + L);
            bool paws = d.nature == Nature.Predator || d.guard || d.id == "rat";
            var hoof = Pal.Ramp(paws ? Pal.Black : Pal.Metal, 0, 3103);
            float cy = Lg + D * 0.5f;
            int seed = d.id.GetHashCode();
            // torso: a slightly boxy barrel, chest deeper than the rump, belly lighter; spots, a bristly ridge on boars
            var b = new VoxelGrid();
            Ellipsoid(b, new Vector3(0f, cy, 0f), new Vector3(W * 0.5f, D * 0.5f, L * 0.5f), 2.4f, p =>
            {
                if (d.Has("spots") && Pal.Hash(p.x / 3, p.y / 3, p.z / 3, seed) > 0.62f) return d.Accent.ramp[Mathf.Min(d.Accent.ramp.Length - 1, 1)];
                if (p.y < cy - D * 0.2f) return belly(p);
                if (d.id.Contains("boar") && p.x == 0 && p.y >= cy + D * 0.3f) return accent(p);
                return coat(p);
            });
            Ellipsoid(b, new Vector3(0f, cy + 0.3f, L * 0.3f), new Vector3(W * 0.52f, D * 0.55f, L * 0.22f), 2.2f, p => p.y < cy - D * 0.2f ? belly(p) : coat(p));    // chest
            if (d.Has("udder")) b.Box(-1, Lg - 1, -L / 2 + 3, 1, Lg, -L / 2 + 5, Pal.Ramp(Pal.Pink, 3, 3104));
            if (d.id == "horse") b.Box(-W / 2 + 1, Lg + D - 1, -L / 2 + 1, W / 2 - 1, Lg + D, -L / 2 + 4, coat);                          // rump
            var m = new AnimalMeshes { body = Build(b, "Animal_" + d.id, s) };

            // neck + head (pivot at the front top of the torso)
            var neckRoot = new Vector3(0f, Lg + D * 0.72f, L * 0.5f - 1.5f);
            var h = new VoxelGrid();
            float nr = Mathf.Max(1f, Mathf.Min(W, D) * 0.34f);
            var ne = d.neck <= 1 ? new Vector3(0f, 0f, 1.5f) : new Vector3(0f, d.neck * 0.78f, d.neck * 0.62f);
            h.Tube(Vector3.zero, ne, nr, coat);
            float hs = d.head;
            var hc = ne + new Vector3(0f, hs * 0.15f, hs * 0.35f);
            Ellipsoid(h, hc, new Vector3(hs * 0.42f, hs * 0.48f, hs * 0.55f), 2.2f, p => coat(p));                                        // skull
            int sz0 = Mathf.RoundToInt(hc.z + hs * 0.35f), sz1 = sz0 + d.snout;
            int sy = Mathf.RoundToInt(hc.y - hs * 0.2f);
            int sw = Mathf.Max(1, Mathf.RoundToInt(hs * 0.28f));
            h.Box(-sw, sy - 1, sz0, sw, sy + 1, sz1, coat);                                                                             // snout
            h.Box(-sw, sy - 1, sz1, sw, sy, sz1, d.id.Contains("boar") || d.id == "pig" ? Pal.Ramp(Pal.Pink, 1, 3105) : Pal.Ramp(Pal.Black, 1, 3105));   // nose
            int ey = Mathf.RoundToInt(hc.y + hs * 0.12f), ez = Mathf.RoundToInt(hc.z + hs * 0.15f), ex = Mathf.RoundToInt(hs * 0.42f);
            var eye = d.Has("glow") ? Pal.Solid(Pal.LightY) : Pal.Solid(Pal.Black[0]);
            h.Set(-ex, ey, ez, eye); h.Set(ex, ey, ez, eye);
            int top = Mathf.RoundToInt(hc.y + hs * 0.45f), ez0 = Mathf.RoundToInt(hc.z - hs * 0.2f);
            foreach (int sx in new[] { -1, 1 })
            {
                int x = sx * Mathf.Max(1, Mathf.RoundToInt(hs * 0.3f));
                if (d.Has("ears_long")) h.Box(x + sx, top - 2, ez0, x + sx * 2, top - 1, ez0 + 1, coat);                            // floppy ears
                else if (d.Has("ears_up")) h.Box(x, top + 1, ez0, x, top + 2, ez0, coat);
                if (d.Has("horns"))
                {
                    var hr = Pal.Ramp(d.id == "cow" ? Pal.Cream : Pal.Black, 2, 3106);
                    if (d.id == "antelope") h.Tube(new Vector3(x, top, ez0 + 1), new Vector3(x * 1.3f, top + 6, ez0 - 3), 0.5f, hr);           // long ringed horns
                    else if (d.id == "goat") { h.Tube(new Vector3(x, top, ez0 + 1), new Vector3(x, top + 3, ez0 - 1), 0.6f, hr); h.Tube(new Vector3(x, top + 3, ez0 - 1), new Vector3(x * 1.4f, top + 1, ez0 - 3), 0.5f, hr); }
                    else h.Tube(new Vector3(x, top - 1, ez0 + 1), new Vector3(x * 2.4f, top + 1, ez0 + 1), 0.6f, hr);
                }
                if (d.Has("tusks"))
                {
                    var tr = Pal.Ramp(d.Has("glow") ? Pal.Moss : Pal.Cream, d.Has("glow") ? 4 : 3, 3107);
                    h.Tube(new Vector3(sx * (sw + 0.5f), sy - 1, sz1 - 1), new Vector3(sx * (sw + 1.5f), sy + 2, sz1 - 2), 0.5f, tr);
                }
            }
            if (d.Has("mane"))
            {
                for (int i = 0; i <= d.neck; i++)                                                                                             // mane along the neck to the forelock
                {
                    var p = Vector3.Lerp(new Vector3(0f, nr + 0.5f, -0.5f), ne + new Vector3(0f, nr + 0.5f, -0.5f), i / (float)d.neck);
                    h.Box(0, Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z), 0, Mathf.RoundToInt(p.y) + 1, Mathf.RoundToInt(p.z), accent);
                }
                h.Box(0, top, Mathf.RoundToInt(hc.z), 0, top + 1, Mathf.RoundToInt(hc.z) + 1, accent);
            }
            if (d.Has("beard")) h.Box(0, sy - 3, sz0, 0, sy - 2, sz0 + 1, accent);
            m.head = Build(h, "AnimalHead_" + d.id, s);
            m.neck = neckRoot * s;

            // legs: thick upper, thin lower, hoof or paw (top hidden inside the torso)
            int up = Mathf.RoundToInt(D * 0.35f);
            var lg = new VoxelGrid();
            int th = W >= 6 ? 1 : 0;
            lg.Box(-th, -Lg / 2, -th, th, up, th, coat);
            lg.Box(0, -Lg + 1, 0, 0, -Lg / 2, 0, coat);
            lg.Box(-th, -Lg, -th, th, -Lg, th + (paws ? 1 : 0), hoof);
            m.leg = Build(lg, "AnimalLeg_" + d.id, s);
            float lx = Mathf.Max(1f, W * 0.5f - 1f), lzf = L * 0.5f - Mathf.Max(2f, L * 0.18f), lzb = -L * 0.5f + Mathf.Max(2f, L * 0.18f);
            m.legRoots = new[] { new Vector3(-lx, Lg, lzf) * s, new Vector3(lx, Lg, lzf) * s, new Vector3(-lx, Lg, lzb) * s, new Vector3(lx, Lg, lzb) * s };
            m.legLen = Lg * s;

            // tail
            var t = new VoxelGrid();
            if (d.id == "horse") t.Tube(Vector3.zero, new Vector3(0f, -d.tail * 0.85f, -d.tail * 0.35f), 1.1f, accent);
            else if (d.Has("curl")) { t.Set(0, 0, 0, coat); t.Set(0, 1, -1, coat); t.Set(0, 0, -2, coat); t.Set(0, -1, -1, coat); }
            else if (d.id == "rat") t.Tube(Vector3.zero, new Vector3(0f, -1f, -d.tail), 0.4f, Pal.Ramp(Pal.Pink, 1, 3108));
            else if (paws) t.Tube(Vector3.zero, new Vector3(0f, d.tail * 0.3f, -d.tail * 0.9f), 0.7f, coat);
            else if (d.id == "cow") { t.Tube(Vector3.zero, new Vector3(0f, -d.tail * 0.9f, -1f), 0.4f, coat); t.Box(0, -d.tail - 1, -1, 0, -d.tail + 1, -1, accent); }
            else t.Tube(Vector3.zero, new Vector3(0f, -d.tail * 0.6f, -d.tail * 0.5f), 0.6f, coat);
            m.tail = Build(t, "AnimalTail_" + d.id, s);
            m.tailRoot = new Vector3(0f, Lg + D * 0.7f, -L * 0.5f + 0.5f) * s;

            if (d.rideable)
            {
                // saddle: blanket, leather seat with cantle and horn, stirrups on straps
                var sd = new VoxelGrid();
                sd.Box(-W / 2 - 1, 0, -4, W / 2 + 1, 0, 3, Pal.Ramp(Pal.Crimson, 2, 3109));
                sd.Box(-W / 2, 1, -3, W / 2, 1, 2, Pal.Ramp(Pal.Olive, 1, 3110));
                sd.Box(-W / 2 + 1, 2, -3, W / 2 - 1, 2, -3, Pal.Ramp(Pal.Olive, 1, 3110));
                sd.Box(0, 2, 2, 0, 3, 2, Pal.Ramp(Pal.Olive, 0, 3110));
                foreach (int sx in new[] { -1, 1 }) { sd.Box(sx * (W / 2 + 1), -5, 0, sx * (W / 2 + 1), -1, 0, Pal.Ramp(Pal.Olive, 0, 3111)); sd.Box(sx * (W / 2 + 1), -6, -1, sx * (W / 2 + 1), -6, 1, Pal.Ramp(Pal.Metal, 2, 3112)); }
                m.saddle = Build(sd, "AnimalSaddle_" + d.id, s);
                m.seat = new Vector3(0f, Lg + D + 2.5f, -1f) * s;
            }
            return m;
        }

        // ------------------------------------------------------------------ birds

        static AnimalMeshes Bird(AnimalDef d)
        {
            float s = VoxelMesher.DefaultSize * d.scale;
            int L = d.len, D = d.depth, W = d.width, Lg = d.leg;
            var coat = d.Coat.Mat(3200 + L); var belly = d.Belly.Mat(3201 + L); var accent = d.Accent.Mat(3202);
            float cy = Lg + D * 0.5f;
            var b = new VoxelGrid();
            Ellipsoid(b, new Vector3(0f, cy, 0f), new Vector3(W * 0.5f, D * 0.5f, L * 0.5f), 2f, p => p.y < cy - 1 ? belly(p) : coat(p));
            // tail feathers: a raised fan (hen) or a flat wedge (vulture)
            if (d.flies) b.Box(-1, Mathf.RoundToInt(cy), -L / 2 - d.tail, 1, Mathf.RoundToInt(cy), -L / 2, coat);
            else b.Box(0, Mathf.RoundToInt(cy), -L / 2 - 1, 0, Mathf.RoundToInt(cy) + d.tail, -L / 2, Pal.Ramp(d.Coat.ramp, 2, 3203));
            var m = new AnimalMeshes { body = Build(b, "Animal_" + d.id, s) };
            var h = new VoxelGrid();
            if (d.Has("bald")) h.Box(-1, -1, -1, 1, 0, 1, Pal.Ramp(Pal.Cream, 2, 3204));                                              // neck ruff
            h.Tube(Vector3.zero, new Vector3(0f, d.neck, 0.5f), 0.7f, d.Has("bald") ? accent : coat);
            var hc = new Vector3(0f, d.neck + 1f, 1f);
            Ellipsoid(h, hc, new Vector3(d.head * 0.6f, d.head * 0.6f, d.head * 0.7f), 2f, p => d.Has("bald") ? accent(p) : coat(p));
            var beak = Pal.Ramp(d.flies ? Pal.Metal : Pal.Ochre, 3, 3205);
            int hy = Mathf.RoundToInt(hc.y), hz = Mathf.RoundToInt(hc.z);
            h.Box(0, hy, hz + d.head, 0, hy, hz + d.head + d.snout, beak);
            if (d.flies) h.Set(0, hy - 1, hz + d.head + d.snout, beak);                                                                 // hooked tip
            if (d.Has("comb")) h.Box(0, hy + d.head, hz - 1, 0, hy + d.head + 1, hz + 1, accent);
            if (d.Has("wattle")) h.Box(0, hy - 2, hz + 1, 0, hy - 1, hz + 1, accent);
            h.Set(-d.head / 2, hy + 1, hz, Pal.Solid(Pal.Black[0]));
            h.Set(d.head / 2, hy + 1, hz, Pal.Solid(Pal.Black[0]));
            m.head = Build(h, "AnimalHead_" + d.id, s);
            m.neck = new Vector3(0f, cy + D * 0.3f, L * 0.5f - 1f) * s;
            var lg = new VoxelGrid();
            var legC = Pal.Ramp(d.flies ? Pal.Fur : Pal.Ochre, 3, 3206);
            lg.Box(0, -Lg, 0, 0, 1, 0, legC);
            lg.Box(0, -Lg, 1, 0, -Lg, 1, legC); lg.Box(-1, -Lg, 1, -1, -Lg, 1, legC); lg.Box(1, -Lg, 1, 1, -Lg, 1, legC);            // toes
            m.leg = Build(lg, "AnimalLeg_" + d.id, s);
            m.legRoots = new[] { new Vector3(-W * 0.25f, Lg, 0f) * s, new Vector3(W * 0.25f, Lg, 0f) * s };
            m.legLen = Lg * s;
            // right wing (the left is mirrored): folded along the body for a hen, a broad fingered plank for a vulture
            var w = new VoxelGrid();
            int span = d.flies ? 12 : 1, chord = d.flies ? 5 : L - 2;
            for (int x = 0; x <= span; x++)
            {
                int c = d.flies ? chord - Mathf.Max(0, x - span + 3) : chord;
                w.Box(x, 0, -c + 2, x, 0, 1, x > span - 3 && d.flies ? Pal.Ramp(Pal.Black, 0, 3207) : coat);
                if (d.flies && x > span - 3) for (int f = -c + 2; f <= 1; f += 2) w.Set(x + 1, 0, f, Pal.Ramp(Pal.Black, 0, 3207));  // primaries
            }
            if (!d.flies) w.Box(0, -2, -chord + 2, 0, 0, 1, coat);
            m.wing = Build(w, "AnimalWing_" + d.id, s);
            m.wingRoot = new Vector3(W * 0.5f - (d.flies ? 0.5f : 0f), cy + D * 0.2f, L * 0.15f) * s;
            return m;
        }

        // ------------------------------------------------------------------ snake

        static AnimalMeshes Snake(AnimalDef d)
        {
            float s = VoxelMesher.DefaultSize * d.scale;
            var coat = d.Coat.Mat(3300); var belly = d.Belly.Mat(3301); var accent = d.Accent.Mat(3302);
            var seg = new VoxelGrid();
            // diamond-backed segment: 2 voxels long, dark diamonds on the back
            seg.Box(-1, 0, -1, 0, 1, 0, p => p.y == 0 ? belly(p) : ((p.x + p.z) & 1) == 0 ? accent(p) : coat(p));
            var m = new AnimalMeshes { segment = Build(seg, "AnimalSeg_" + d.id, s), segLen = 2f * s, segments = Mathf.Max(4, d.len / 2) };
            var h = new VoxelGrid();
            h.Box(-1, 0, 0, 1, 1, 2, coat);
            h.Box(-1, 0, 3, 0, 1, 3, coat);
            h.Set(-1, 1, 2, Pal.Solid(Pal.Black[0])); h.Set(1, 1, 2, Pal.Solid(Pal.Black[0]));
            m.snakeHead = Build(h, "AnimalHead_" + d.id, s);
            var r = new VoxelGrid();
            r.Box(0, 0, -3, 0, 1, 0, p => (p.z & 1) == 0 ? Pal.Cream[3] : Pal.Cream[1]);
            m.rattle = Build(r, "AnimalRattle_" + d.id, s);
            m.body = m.segment;
            return m;
        }
    }
}
