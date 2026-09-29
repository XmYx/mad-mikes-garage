using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Animals
{
    public static partial class AnimalModels
    {
        // ------------------------------------------------------------------ arthropods

        /// <summary>Scorpions, spiders, roaches and beetles (user additions): a front body and an abdomen (round and
        /// hairy on spiders, plated on scorpions and roaches, a domed shell on beetles), eight or six jointed legs that
        /// arch out sideways (right side; the left ones are mirrored), pincers, fangs or antennae on the head, and the
        /// scorpion's segmented tail curling over the back to the stinger.</summary>
        static AnimalMeshes Arthropod(AnimalDef d)
        {
            float s = VoxelMesher.DefaultSize * d.scale;
            int L = d.len, D = d.depth, W = d.width, Lg = Mathf.Max(1, d.leg);
            var coat = d.Coat.Mat(3400 + L); var belly = d.Belly.Mat(3401 + L); var accent = d.Accent.Mat(3402 + L);
            bool spider = !d.Has("legs6") && !d.Has("pincers");
            int seed = d.id.GetHashCode();
            float cy = Lg + D * 0.5f;
            var b = new VoxelGrid();
            // front body (cephalothorax)
            Ellipsoid(b, new Vector3(0f, cy, L * 0.2f), new Vector3(W * 0.36f, D * 0.45f, L * 0.22f), 2.2f, p => p.y < cy - D * 0.25f ? belly(p) : coat(p));
            if (spider)
                Ellipsoid(b, new Vector3(0f, cy + D * 0.2f, -L * 0.25f), new Vector3(W * 0.46f, D * 0.62f, L * 0.34f), 2f, p =>
                    d.Has("hairy") && Pal.Hash(p.x, p.y, p.z, seed) > 0.7f ? accent(p) : p.y < cy - D * 0.1f ? belly(p) : coat(p));
            else if (d.Has("shell"))
                Ellipsoid(b, new Vector3(0f, cy + D * 0.15f, -L * 0.15f), new Vector3(W * 0.5f, D * 0.6f, L * 0.4f), 2f, p => p.x == 0 && p.y > cy ? accent(p) : p.y < cy - D * 0.2f ? belly(p) : coat(p));   // wing-case seam
            else
                for (int z = -L / 2; z <= 0; z++)                                                           // plated abdomen, a seam every other voxel
                {
                    float w = W * 0.45f * (1f - Mathf.Abs(z + L * 0.2f) / (L * 0.45f)) + 1f;
                    int hw = Mathf.Max(1, Mathf.RoundToInt(w)), zz = z;
                    b.Box(-hw, Mathf.RoundToInt(cy - D * 0.4f), z, hw, Mathf.RoundToInt(cy + D * 0.3f), z, p => (zz & 1) == 0 && p.y > cy ? accent(p) : p.y < cy - D * 0.2f ? belly(p) : coat(p));
                }
            var m = new AnimalMeshes { body = Build(b, "Animal_" + d.id, s), sprawl = true };

            // head: eyes, fangs / pincers / antennae (pivot at the front of the body)
            var h = new VoxelGrid();
            Ellipsoid(h, new Vector3(0f, 0f, 1f), new Vector3(W * 0.22f + 0.5f, D * 0.3f + 0.5f, 1.2f), 2f, p => coat(p));
            var eye = d.Has("glow") ? Pal.Solid(Pal.LightY) : Pal.Solid(Pal.Black[0]);
            h.Set(-1, 1, 2, eye); h.Set(1, 1, 2, eye);
            if (spider) { h.Set(0, 1, 2, eye); h.Box(-1, -1, 2, -1, -1, 3, accent); h.Box(1, -1, 2, 1, -1, 3, accent); }                    // eye cluster, fangs
            if (d.Has("pincers"))
                foreach (int sx in new[] { -1, 1 })
                {
                    var a0 = new Vector3(sx * 1f, 0f, 1f); var a1 = new Vector3(sx * (W * 0.55f + 1f), 0f, 2.5f); var a2 = new Vector3(sx * (W * 0.45f + 1f), 0f, 3.5f + L * 0.2f);
                    h.Tube(a0, a1, 0.55f, coat); h.Tube(a1, a2, 0.6f, coat);
                    Ellipsoid(h, a2 + new Vector3(0f, 0f, 1.2f), new Vector3(1.3f, 0.9f, 1.8f), 2f, p => accent(p));                          // claw
                    h.Set(Mathf.RoundToInt(a2.x - sx), 0, Mathf.RoundToInt(a2.z + 3f), accent);
                }
            if (d.Has("antennae"))
                foreach (int sx in new[] { -1, 1 }) h.Tube(new Vector3(sx * 0.5f, 0.5f, 2f), new Vector3(sx * 2.5f, 2f, 2f + L * 0.8f), 0.35f, accent);
            m.head = Build(h, "AnimalHead_" + d.id, s);
            m.neck = new Vector3(0f, cy, L * 0.42f) * s;

            // one right leg: up and out to the knee, down to the foot
            int n = d.Has("legs6") ? 6 : 8;
            float reach = W * 0.55f + Lg * (spider ? 1.1f : 0.8f);
            var lg = new VoxelGrid();
            var knee = new Vector3(reach * 0.55f, spider ? Lg * 0.9f + 1f : Lg * 0.4f + 0.5f, 0f);
            var foot = new Vector3(reach, -Lg, 0f);
            float lr = Mathf.Max(0.45f, W * 0.07f);
            lg.Tube(Vector3.zero, knee, lr, coat);
            lg.Tube(knee, foot, lr * 0.85f, d.Has("hairy") ? accent : coat);
            m.leg = Build(lg, "AnimalLeg_" + d.id, s);
            m.legRoots = new Vector3[n]; m.legFan = new float[n];
            int pairs = n / 2;
            for (int i = 0; i < n; i++)
            {
                int pair = i / 2; bool left = (i & 1) == 0;
                float z = L * 0.32f - pair * (L * 0.32f / Mathf.Max(1, pairs - 1)) * (spider ? 0.9f : 1.2f);
                m.legRoots[i] = new Vector3((left ? -1f : 1f) * W * 0.3f, Lg, z) * s;
                m.legFan[i] = Mathf.Lerp(45f, -40f, pair / (float)Mathf.Max(1, pairs - 1));                   // front legs reach forward, back ones back
            }
            m.legLen = Lg * s;

            // scorpion tail: segments curling up and forward over the back, ending in the stinger
            var t = new VoxelGrid();
            if (d.Has("stinger"))
            {
                int T = Mathf.Max(4, d.tail);
                var pts = new[] { Vector3.zero, new Vector3(0f, T * 0.25f, -T * 0.35f), new Vector3(0f, T * 0.7f, -T * 0.3f), new Vector3(0f, T * 0.95f, T * 0.05f), new Vector3(0f, T * 0.85f, T * 0.3f) };
                for (int i = 0; i < pts.Length - 1; i++) t.Tube(pts[i], pts[i + 1], Mathf.Lerp(W * 0.18f + 0.6f, 0.6f, i / 3f), i % 2 == 0 ? coat : accent);
                var tip = pts[pts.Length - 1];
                var sting = d.Has("glow") ? Pal.Solid(Pal.LightY) : accent;
                Ellipsoid(t, tip, new Vector3(0.9f, 1.1f, 1.1f), 2f, p => sting(p));
                t.Tube(tip, tip + new Vector3(0f, -1.5f, 1.2f), 0.4f, Pal.Solid(Pal.Black[0]));
            }
            else t.Set(0, 0, 0, belly);                                                                         // a stub (spinnerets, roach cerci)
            m.tail = Build(t, "AnimalTail_" + d.id, s);
            m.tailRoot = new Vector3(0f, cy, spider ? -L * 0.55f : -L * 0.5f) * s;
            return m;
        }
    }
}
