using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>HD stand-ins for the small meshes the game builds at runtime (with the HD pack on): debris chunks and
    /// puffs, resource pickups, glass and lamp shards, switch levers, smooth bulbs. Cached per colour / kind; drawn with
    /// <see cref="HDShapes.Solid"/>.</summary>
    public static class HDBits
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => cache.Clear();

        /// <summary>The HD pack is on and its vertex-colour material is available.</summary>
        public static bool On => HDAssets.Enabled && HDShapes.Solid;

        static bool Cached(string key, out Mesh m) => cache.TryGetValue(key, out m) && m;

        /// <summary>A bevelled 1 m chunk (scaled by the caller like the voxel cube).</summary>
        public static Mesh Chunk(Color32 c)
        {
            string key = "chunk" + c.r + "," + c.g + "," + c.b;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            b.Box(Vector3.zero, Vector3.one, c, 0.2f);
            return cache[key] = b.ToMesh("DebrisHD");
        }

        /// <summary>A soft 1 m puff (smoke, steam, drips).</summary>
        public static Mesh Puff(Color32 c)
        {
            string key = "puff" + c.r + "," + c.g + "," + c.b;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            b.Ellipsoid(Vector3.zero, Vector3.one * 0.5f, c, 9, 6);
            for (int i = 0; i < b.c.Count; i++) b.c[i] = c;                                         // no underside shading on smoke
            return cache[key] = b.ToMesh("PuffHD");
        }

        /// <summary>A resource lying on the ground (origin on the ground, about the voxel pickup's size).</summary>
        public static Mesh Pickup(ResourceType t)
        {
            string key = "pick" + (int)t;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            var r = new System.Random((int)t * 77 + 5);
            float R() => (float)r.NextDouble();
            switch (t)
            {
                case ResourceType.Scrap:
                    b.Plate(new Vector3(0f, 0.012f, 0f), new Vector3(0.34f, 0.012f, 0.24f), Pal.Chrome[1], Quaternion.Euler(4f, 20f, -6f), 6, Pal.Chrome[2]);
                    b.Plate(new Vector3(0.03f, 0.05f, -0.02f), new Vector3(0.22f, 0.012f, 0.14f), Pal.Rust[2], Quaternion.Euler(-14f, -35f, 10f), 4, Pal.Chrome[1]);
                    b.Tube(new Vector3(-0.12f, 0.03f, 0.08f), new Vector3(0.1f, 0.06f, 0.1f), 0.014f, 0.014f, Pal.Rust[3], 6, true);
                    break;
                case ResourceType.Wood:
                    for (int k = 0; k < 3; k++)
                        b.Box(new Vector3(0f, 0.025f + (k == 2 ? 0.045f : 0f), -0.05f + (k % 2) * 0.1f), new Vector3(0.5f - k * 0.06f, 0.045f, 0.09f), Pal.Wood[2 + (k & 1)], 0.008f, Quaternion.Euler(0f, (R() - 0.5f) * 14f, 0f));
                    break;
                case ResourceType.Stone:
                    for (int k = 0; k < 3; k++)
                        b.Ellipsoid(new Vector3((R() - 0.5f) * 0.14f, 0.05f, (R() - 0.5f) * 0.14f), new Vector3(0.07f + R() * 0.04f, 0.05f + R() * 0.03f, 0.06f + R() * 0.04f), Pal.Sand[1 + k % 2], 6, 4, false, Quaternion.Euler(R() * 40f, R() * 360f, R() * 40f));
                    break;
                case ResourceType.Glass:
                    for (int k = 0; k < 4; k++)
                        b.Plate(new Vector3((R() - 0.5f) * 0.16f, 0.01f + k * 0.006f, (R() - 0.5f) * 0.16f), new Vector3(0.08f + R() * 0.06f, 0.006f, 0.05f + R() * 0.05f), Pal.Glass[2 + k % 2], Quaternion.Euler((R() - 0.5f) * 20f, R() * 360f, (R() - 0.5f) * 20f));
                    break;
                case ResourceType.Rubber:
                    for (int k = 0; k < 12; k++)
                    {
                        float a0 = k * Mathf.PI / 6f, a1 = (k + 1) * Mathf.PI / 6f;
                        b.Tube(new Vector3(Mathf.Cos(a0) * 0.12f, 0.035f, Mathf.Sin(a0) * 0.12f), new Vector3(Mathf.Cos(a1) * 0.12f, 0.035f, Mathf.Sin(a1) * 0.12f), 0.035f, 0.035f, Pal.Tire[1], 7);
                    }
                    break;
                default:
                    b.Ellipsoid(new Vector3(0f, 0.07f, 0f), new Vector3(0.11f, 0.075f, 0.09f), Pal.Sand[3], 8, 5);
                    b.Ellipsoid(new Vector3(0f, 0.15f, 0f), new Vector3(0.03f, 0.02f, 0.03f), Pal.Sand[2], 6, 3);
                    break;
            }
            return cache[key] = b.ToMesh("PickupHD_" + t);
        }

        /// <summary>Broken glass / lamp lenses scattered on the ground (~0.4 m across).</summary>
        public static Mesh Shards(Color32[] ramp, int count, float spread, int seed)
        {
            string key = "shards" + seed + "," + count;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            var r = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Pow((float)r.NextDouble(), 1.6f) * spread;
                var at = new Vector3(Mathf.Cos(a) * d, 0.004f, Mathf.Sin(a) * d);
                float len = 0.015f + (float)r.NextDouble() * 0.03f, rot = (float)r.NextDouble() * 6.283f;
                b.Leaf(at, at + new Vector3(Mathf.Cos(rot) * len, 0.003f, Mathf.Sin(rot) * len), len * 0.7f, ramp[r.Next(ramp.Length)]);
            }
            return cache[key] = b.ToMesh("ShardsHD");
        }

        /// <summary>A light switch's toggle lever (stem and knob, pivot at the plate).</summary>
        public static Mesh Lever()
        {
            if (Cached("lever", out var m)) return m;
            var b = new HDShapes();
            b.Tube(Vector3.zero, new Vector3(0f, 0.09f, 0f), 0.009f, 0.007f, Pal.Cream[3], 8, true);
            b.Ellipsoid(new Vector3(0f, 0.095f, 0f), new Vector3(0.014f, 0.012f, 0.014f), Pal.Cream[4], 8, 5);
            return cache["lever"] = b.ToMesh("LeverHD");
        }

        /// <summary>A smooth glass bulb / globe filling the bounds of a voxel bulb, in its average colour.</summary>
        public static Mesh Bulb(Mesh voxel)
        {
            if (!voxel) return null;
            string key = "bulb" + voxel.GetEntityId();
            if (Cached(key, out var m)) return m;
            var bd = voxel.bounds;
            var cols = voxel.colors32;
            long rr = 0, gg = 0, bb = 0;
            foreach (var c in cols) { rr += c.r; gg += c.g; bb += c.b; }
            int n = Mathf.Max(1, cols.Length);
            var col = new Color32((byte)(rr / n), (byte)(gg / n), (byte)(bb / n), 255);
            var b = new HDShapes();
            b.Ellipsoid(bd.center, bd.extents * 1.02f, col, 12, 8);
            for (int i = 0; i < b.c.Count; i++) b.c[i] = col;                                         // even glow, no underside shade
            return cache[key] = b.ToMesh("BulbHD");
        }

        const float S = VoxelMesher.DefaultSize;

        /// <summary>A jerry can held for pouring (grip at the origin, body hanging below, spout +X).</summary>
        public static Mesh JerryCan()
        {
            if (Cached("jerry", out var m)) return m;
            var b = new HDShapes();
            b.Box(new Vector3(0f, -3.5f, 0f) * S, new Vector3(5f, 8f, 3f) * S, Pal.Olive[2], 0.5f * S);
            b.Box(new Vector3(0f, -3.5f, 1.55f) * S, new Vector3(3.6f, 6f, 0.2f) * S, Pal.Olive[3], 0.1f * S);   // the X-pressed panel
            b.Tube(new Vector3(-1.2f, 1f, 0f) * S, new Vector3(1.2f, 1f, 0f) * S, 0.35f * S, 0.35f * S, Pal.Black[1], 6, true);
            b.Tube(new Vector3(2.2f, 0.2f, 0f) * S, new Vector3(3.6f, 2.2f, 0f) * S, 0.45f * S, 0.32f * S, Pal.Metal[2], 8, true);
            return cache["jerry"] = b.ToMesh("JerryCanHD");
        }

        /// <summary>A plastic oil jug (grip at the origin).</summary>
        public static Mesh OilJug()
        {
            if (Cached("oiljug", out var m)) return m;
            var b = new HDShapes();
            b.Box(new Vector3(0f, -3.5f, 0f) * S, new Vector3(3f, 6f, 3f) * S, Pal.Ochre[3], 0.7f * S);
            b.Box(new Vector3(0f, -3.6f, 1.52f) * S, new Vector3(2.4f, 2.2f, 0.1f) * S, Pal.Cream[2], 0.05f * S);
            b.Tube(new Vector3(-1f, 0f, 0f) * S, new Vector3(1f, 0f, 0f) * S, 0.3f * S, 0.3f * S, Pal.Black[1], 6, true);
            b.Tube(new Vector3(1f, -0.4f, 0f) * S, new Vector3(1.2f, 1.2f, 0f) * S, 0.4f * S, 0.35f * S, Pal.Black[2], 8, true);
            return cache["oiljug"] = b.ToMesh("OilJugHD");
        }

        /// <summary>A float: white under, red cap, black tip (origin at the waterline).</summary>
        public static Mesh Bobber()
        {
            if (Cached("bobber", out var m)) return m;
            var b = new HDShapes();
            b.Ellipsoid(new Vector3(0f, 0.6f, 0f) * S, new Vector3(1.4f, 1f, 1.4f) * S, Pal.Cream[3], 10, 6);
            b.Ellipsoid(new Vector3(0f, 1.4f, 0f) * S, new Vector3(1.3f, 1.1f, 1.3f) * S, Pal.Crimson[3], 10, 6, true);
            b.Tube(new Vector3(0f, 2.2f, 0f) * S, new Vector3(0f, 3.6f, 0f) * S, 0.18f * S, 0.12f * S, Pal.Black[1], 6, true);
            return cache["bobber"] = b.ToMesh("BobberHD");
        }

        /// <summary>A projectile in flight along +Z (0 arrow, 1 bolt, 2 stone, else a molotov-style bottle), 4 cm voxel units.</summary>
        public static Mesh Projectile(int kind)
        {
            string key = "proj" + kind;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            const float v = 0.04f;
            switch (kind)
            {
                case 0:
                    b.Tube(new Vector3(0, 0, -5) * v, new Vector3(0, 0, 4.5f) * v, 0.18f * v, 0.18f * v, Pal.Wood[3], 6);
                    b.Tube(new Vector3(0, 0, 4.5f) * v, new Vector3(0, 0, 5.8f) * v, 0.45f * v, 0.02f * v, Pal.Chrome[2], 6);
                    b.Leaf(new Vector3(0, 0, -4.8f) * v, new Vector3(0, 0, -3.2f) * v, 1.6f * v, Pal.Crimson[3], 0f, Vector3.right);
                    b.Leaf(new Vector3(0, 0, -4.8f) * v, new Vector3(0, 0, -3.2f) * v, 1.6f * v, Pal.Crimson[3], 0f, Vector3.up);
                    break;
                case 1:
                    b.Tube(new Vector3(0, 0, -3) * v, new Vector3(0, 0, 3.5f) * v, 0.3f * v, 0.3f * v, Pal.Metal[2], 6);
                    b.Tube(new Vector3(0, 0, 3.5f) * v, new Vector3(0, 0, 4.6f) * v, 0.5f * v, 0.02f * v, Pal.Chrome[3], 6);
                    break;
                case 2:
                    b.Ellipsoid(Vector3.zero, new Vector3(0.6f, 0.5f, 0.55f) * v, Pal.Metal[3], 6, 4);
                    break;
                default:
                    b.Tube(new Vector3(0, 0, -1.4f) * v, new Vector3(0, 0, 1f) * v, 0.6f * v, 0.6f * v, Pal.Crimson[4], 8, true);
                    b.Tube(new Vector3(0, 0, 1f) * v, new Vector3(0, 0, 2.2f) * v, 0.3f * v, 0.25f * v, Pal.Crimson[4], 8, true);
                    b.Ellipsoid(new Vector3(0, 0, 2.4f) * v, Vector3.one * 0.35f * v, Pal.LightY, 6, 4);
                    break;
            }
            return cache[key] = b.ToMesh("ProjectileHD" + kind);
        }

        /// <summary>A runway edge lamp: short post, amber lens on top (voxel units as the old lamp).</summary>
        public static Mesh RunwayLamp()
        {
            if (Cached("runway", out var m)) return m;
            var b = new HDShapes();
            b.Tube(new Vector3(0f, -0.5f, 0f) * S, new Vector3(0f, 2.6f, 0f) * S, 0.45f * S, 0.4f * S, Pal.Metal[2], 8, true);
            b.Ellipsoid(new Vector3(0f, 3.1f, 0f) * S, new Vector3(1.4f, 0.8f, 1.4f) * S, Pal.Ochre[3], 10, 6);
            return cache["runway"] = b.ToMesh("RunwayLampHD");
        }

        /// <summary>A switch / mill stamp: shoe, stem and tappet (origin at the shoe's foot, voxel units).</summary>
        public static Mesh Stamp()
        {
            if (Cached("stamp", out var m)) return m;
            var b = new HDShapes();
            b.Box(new Vector3(0f, 1f, 0f) * S, new Vector3(3f, 3f, 3f) * S, Pal.Metal[3], 0.4f * S);
            b.Tube(new Vector3(0f, 2.5f, 0f) * S, new Vector3(0f, 17.5f, 0f) * S, 0.5f * S, 0.5f * S, Pal.Chrome[1], 8, true);
            b.Box(new Vector3(0f, 13.5f, 0f) * S, new Vector3(3f, 2f, 3f) * S, Pal.Rust[2], 0.3f * S);
            return cache["stamp"] = b.ToMesh("StampHD");
        }

        /// <summary>A clock hand along +Z in the face plane.</summary>
        public static Mesh ClockHand(int length)
        {
            string key = "hand" + length;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            b.Leaf(new Vector3(0f, 0f, -0.6f) * S, new Vector3(0f, 0f, length + 0.4f) * S, 0.9f * S, Pal.Black[1], 0f, Vector3.right, Vector3.up);
            b.Ellipsoid(Vector3.zero, new Vector3(0.6f, 0.3f, 0.6f) * S, Pal.Black[2], 8, 4);
            return cache[key] = b.ToMesh("ClockHandHD" + length);
        }

        /// <summary>A crow on the ground (beak +Z), voxel units as the old one.</summary>
        public static Mesh Crow()
        {
            if (Cached("crow", out var m)) return m;
            var b = new HDShapes();
            b.Ellipsoid(new Vector3(0f, 1.8f, -0.4f) * S, new Vector3(1.3f, 1.1f, 2f) * S, Pal.Black[1], 10, 6);
            b.Ellipsoid(new Vector3(0f, 3f, 1.4f) * S, new Vector3(0.8f, 0.8f, 0.85f) * S, Pal.Black[2], 8, 5);
            b.Tube(new Vector3(0f, 2.9f, 2.1f) * S, new Vector3(0f, 2.7f, 3.4f) * S, 0.3f * S, 0.04f * S, Pal.Ochre[2], 6);
            b.Leaf(new Vector3(0f, 2f, -2f) * S, new Vector3(0f, 1.6f, -4.3f) * S, 1.6f * S, Pal.Black[0]);
            foreach (float x in new[] { -0.6f, 0.6f }) b.Tube(new Vector3(x, 1f, 0f) * S, new Vector3(x, -0.4f, 0.1f) * S, 0.12f * S, 0.1f * S, Pal.Ochre[1], 5);
            return cache["crow"] = b.ToMesh("CrowHD");
        }

        /// <summary>A soil load (bucket / blade mound, or a flat crowned bed load) of half-size w × h × d voxels.</summary>
        public static Mesh SoilHeap(Vector3Int size, bool bed, Color32 c, int seed)
        {
            string key = "soil" + size + bed + c.r + "," + c.g + "," + c.b;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            var r = new System.Random(seed);
            float w = size.x + 0.5f, h = size.y, d = size.z + 0.5f;
            if (bed)
            {
                b.Box(new Vector3(0f, h * 0.38f, 0f) * S, new Vector3(w * 2f, h * 0.76f, d * 2f) * S, HDShapes.Tone(c, 0.9f), 0.6f * S);
                b.Ellipsoid(new Vector3(0f, h * 0.76f, 0f) * S, new Vector3(w * 0.95f, h * 0.3f, d * 0.95f) * S, c, 14, 6, true);
            }
            else b.Ellipsoid(new Vector3(0f, -h * 0.25f, 0f) * S, new Vector3(w, h * 1.2f, d) * S, c, 14, 7, true);
            for (int k = 0; k < 7; k++)                                                              // clods on its skin
            {
                float a = (float)r.NextDouble() * 6.283f, rr = (float)r.NextDouble() * 0.75f;
                var at = new Vector3(Mathf.Cos(a) * rr * w, 0f, Mathf.Sin(a) * rr * d);
                at.y = bed ? h * 0.95f : h * Mathf.Sqrt(Mathf.Max(0f, 1f - rr * rr)) * 0.95f;
                b.Ellipsoid(at * S, Vector3.one * (0.6f + (float)r.NextDouble() * 0.6f) * S, HDShapes.Tone(c, 0.8f + (float)r.NextDouble() * 0.35f), 6, 4);
            }
            return cache[key] = b.ToMesh("SoilHeapHD");
        }

        /// <summary>A painted emblem: each pixel of the decal grid a flat 1 mm tile (paint, not studs), 4 cm grid.</summary>
        public static Mesh Decal(VoxelGrid g, int id)
        {
            string key = "decal" + id;
            if (Cached(key, out var m)) return m;
            var b = new HDShapes();
            const float v = 0.04f;
            foreach (var kv in g.voxels)
            {
                var p = kv.Key;
                b.Box(new Vector3(p.x, p.y, p.z) * v, new Vector3(v * 1.02f, v * 1.02f, 0.002f), kv.Value.color, 0f);
            }
            return cache[key] = b.ToMesh("DecalHD" + id);
        }
    }
}
