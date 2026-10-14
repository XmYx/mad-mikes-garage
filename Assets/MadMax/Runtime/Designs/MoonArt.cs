using System.Collections.Generic;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.Designs
{
    /// <summary>The title's lunar set (user additions): a cratered regolith plain round a flat stage, Mad Mike's
    /// crash-landed scrap rocket with the car's tyre tracks leading away from it, an old-world descent stage with a
    /// bleached flag, and the planet itself — the world generator's continents, seas, deserts, forests, fallout zones,
    /// towns and polar ice under a cloud shell. Local set space: the neon sign stands at the origin facing −Z, the car
    /// parks at z −6.5, the camera looks along +Z from about z −20.</summary>
    public static class MoonArt
    {
        public const float Radius = 72f, Cell = 0.45f;
        /// <summary>The flat stage the car and the sign stand on (local x / z extents).</summary>
        public const float StageX = 10f, StageZ0 = -30f, StageZ1 = 6f;
        /// <summary>The rocket's tail (it stands nose-down in its crater, see <see cref="RocketTilt"/>) and the lander.</summary>
        public static readonly Vector3 RocketAt = new Vector3(14f, 4.6f, 13f), LanderAt = new Vector3(-19f, 0f, 30f);
        public static readonly Quaternion RocketTilt = Quaternion.Euler(48f, 35f, 8f);
        static readonly Vector2 Impact = new Vector2(16.4f, 16.4f);
        static readonly Vector2 TrackA = new Vector2(17f, 13.5f), TrackB = new Vector2(17f, -6.5f), TrackC = new Vector2(2.2f, -6.5f);

        struct Crater { public float x, z, r, depth, rim; public bool fresh; }
        static Crater[] craters;

        /// <summary>Call on the main thread before <see cref="BuildSurface"/> runs on a worker.</summary>
        public static void Prepare() => Craters();

        static Crater[] Craters()
        {
            if (craters != null) return craters;
            var list = new List<Crater>();
            var r = new System.Random(1969);
            for (int i = 0; i < 46; i++)
            {
                float rad = 1f + Mathf.Pow((float)r.NextDouble(), 2.4f) * 13f;
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)r.NextDouble()) * (Radius - 4f);
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                list.Add(new Crater { x = x, z = z, r = rad, depth = rad * (0.2f + (float)r.NextDouble() * 0.12f), rim = rad * 0.07f });
            }
            // the fresh crater the rocket dug on landing
            list.Add(new Crater { x = Impact.x, z = Impact.y, r = 5.5f, depth = 1.2f, rim = 0.55f, fresh = true });
            return craters = list.ToArray();
        }

        /// <summary>0 on the stage, 1 away from it (smooth).</summary>
        static float Wild(float x, float z)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x) - StageX), dz = Mathf.Max(0f, Mathf.Max(StageZ0 - z, z - StageZ1));
            float k = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / 6f);
            return k * k * (3f - 2f * k);
        }

        /// <summary>Ground height of the set (local metres; the stage is exactly 0).</summary>
        public static float Height(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.045f + 3.1f, z * 0.045f + 7.7f) - 0.5f) * 1.8f
                    + (Mathf.PerlinNoise(x * 0.21f + 11f, z * 0.21f - 4f) - 0.5f) * 0.3f;
            foreach (var c in Craters())
            {
                float dx = x - c.x, dz = z - c.z, dd = dx * dx + dz * dz;
                if (dd > 4f * c.r * c.r) continue;
                float d = Mathf.Sqrt(dd) / c.r;
                if (d < 1f) h += (d * d - 1f) * c.depth;                                        // the bowl
                float rim = (d - 1f) / 0.28f;
                h += c.rim * Mathf.Exp(-rim * rim);                                             // the raised lip
            }
            // far hills break the horizon line
            float far = Mathf.Clamp01((new Vector2(x, z).magnitude - 36f) / 26f);
            h += far * far * Mathf.PerlinNoise(x * 0.03f + 40f, z * 0.03f) * 7f;
            return h * Wild(x, z);                                                              // the stage stays level
        }

        static float TrackDist(float x, float z)
        {
            float best = 1e9f;
            var prev = TrackA;
            for (int i = 1; i <= 40; i++)
            {
                float t = i / 40f, u = 1f - t;
                var p = u * u * TrackA + 2f * u * t * TrackB + t * t * TrackC;
                var ab = p - prev; var ap = new Vector2(x, z) - prev;
                float k = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                best = Mathf.Min(best, (ap - ab * k).magnitude);
                prev = p;
            }
            return best;
        }

        static Color32 Regolith(float x, float z, int ix, int iz)
        {
            float n = Mathf.PerlinNoise(x * 0.12f + 5f, z * 0.12f + 9f), grain = Pal.Hash(ix, 3, iz, 71);
            int shade = n > 0.62f ? 4 : n > 0.4f ? 3 : 2;
            if (grain > 0.93f) shade = Mathf.Max(1, shade - 1); else if (grain < 0.05f) shade = Mathf.Min(4, shade + 1);
            var col = Pal.Fur[shade];
            foreach (var c in Craters())
            {
                float dx = x - c.x, dz = z - c.z, dd = dx * dx + dz * dz;
                if (dd > 13f * c.r * c.r) continue;
                float d = Mathf.Sqrt(dd) / c.r;
                if (d < 0.85f) col = Pal.Fur[Mathf.Max(1, shade - 1)];                            // shaded bowls
                else if (d < 1.25f && grain > 0.35f) col = Pal.Fur[4];                             // bright lips
                if (c.fresh && d > 1f && d < 3.6f)
                {
                    // ejecta rays of the fresh crater
                    float ang = Mathf.Atan2(dz, dx);
                    if (Mathf.Sin(ang * 7f + 1.3f) > 0.55f - (3.6f - d) * 0.12f && grain > 0.2f) col = Pal.Cream[d < 1.8f ? 1 : 0];
                }
            }
            // the tyre tracks from the rocket to the stage: two dark ribbons, tread speckle
            if (x < 0f || x > 20f || z < -9f || z > 16f) return col;
            float td = TrackDist(x, z);
            if (td > 0.55f && td < 1.05f && grain > 0.12f) col = Pal.Fur[(ix + iz) % 3 == 0 ? 0 : 1];
            return col;
        }

        /// <summary>Vertex data of the regolith plain (pure: runs on a worker thread after <see cref="Prepare"/>).</summary>
        public sealed class SurfaceData { public Vector3[] verts, norms; public Color32[] cols; public int[] tris; }

        /// <summary>The regolith plain as a mesh (main thread).</summary>
        public static Mesh Surface(SurfaceData d = null)
        {
            d ??= BuildSurface();
            var m = new Mesh { name = "MoonSurface", indexFormat = d.verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.vertices = d.verts; m.normals = d.norms; m.colors32 = d.cols; m.triangles = d.tris;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The regolith plain: a heightfield at <see cref="Cell"/> spacing, one flat colour per cell (like the
        /// terrain) over smooth normals.</summary>
        /// <summary>HD look for the Moon set (set by the title sequence on the main thread before the worker builds).</summary>
        public static bool hd;

        public static SurfaceData BuildSurface()
        {
            int n = Mathf.RoundToInt(Radius * 2f / Cell);
            int row = n + 1;
            var hs = new float[row * row];
            var ns = new Vector3[row * row];
            for (int j = 0; j < row; j++)
            for (int i = 0; i < row; i++)
                hs[j * row + i] = Height(-Radius + i * Cell, -Radius + j * Cell);
            for (int j = 0; j < row; j++)
            for (int i = 0; i < row; i++)
            {
                float hl = hs[j * row + Mathf.Max(0, i - 1)], hr = hs[j * row + Mathf.Min(n, i + 1)];
                float hd = hs[Mathf.Max(0, j - 1) * row + i], hu = hs[Mathf.Min(n, j + 1) * row + i];
                ns[j * row + i] = new Vector3(hl - hr, 2f * Cell, hd - hu).normalized;
            }
            var verts = new Vector3[n * n * 4];
            var norms = new Vector3[n * n * 4];
            var cols = new Color32[n * n * 4];
            var tris = new int[n * n * 6];
            int v = 0, t = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var col = Regolith(-Radius + (i + 0.5f) * Cell, -Radius + (j + 0.5f) * Cell, i, j);
                for (int q = 0; q < 4; q++)
                {
                    int ii = i + (q & 1), jj = j + (q >> 1), k = jj * row + ii;
                    verts[v + q] = new Vector3(-Radius + ii * Cell, hs[k], -Radius + jj * Cell);
                    norms[v + q] = ns[k];
                    cols[v + q] = hd ? Regolith(-Radius + ii * Cell, -Radius + jj * Cell, ii, jj) : col;   // HD: smooth, not per cell
                }
                tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
                tris[t++] = v + 1; tris[t++] = v + 2; tris[t++] = v + 3;
                v += 4;
            }
            return new SurfaceData { verts = verts, norms = norms, cols = cols, tris = tris };
        }

        /// <summary>Boulders scattered off the stage, seated on the ground (0.16 m voxels, one mesh in set space).</summary>
        public static Mesh Rocks()
        {
            if (MadMax.Rendering.HDBits.On) return RocksHD();
            const float s = 0.16f;
            var g = new VoxelGrid();
            var r = new System.Random(4242);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 7f + Mathf.Sqrt((float)r.NextDouble()) * (Radius - 12f);
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                if (Wild(x, z) < 0.9f) continue;
                float rad = 1.2f + Mathf.Pow((float)r.NextDouble(), 2f) * 5f;
                var c = new Vector3(x / s, Height(x, z) / s + rad * 0.3f, z / s);
                int R = Mathf.CeilToInt(rad) + 1;
                int seed = i * 17;
                for (int vx = -R; vx <= R; vx++)
                for (int vy = -R; vy <= R; vy++)
                for (int vz = -R; vz <= R; vz++)
                {
                    float lump = rad * (0.8f + Pal.Hash(vx >> 1, vy >> 1, vz >> 1, seed) * 0.35f);
                    if (vx * vx + vy * vy * 1.6f + vz * vz > lump * lump) continue;
                    var p = new Vector3Int(Mathf.RoundToInt(c.x) + vx, Mathf.RoundToInt(c.y) + vy, Mathf.RoundToInt(c.z) + vz);
                    int sh = vy > rad * 0.3f ? 3 : vy > -rad * 0.2f ? 2 : 1;
                    g.Set(p, Pal.Solid(Pal.Fur[Mathf.Clamp(sh + (Pal.Hash(p, 5) > 0.8f ? 1 : 0), 0, 4)]));
                }
            }
            g.Bevel();
            return VoxelMesher.Build(g, "MoonRocks", s);
        }

        /// <summary>Mad Mike's scrap rocket, lying where it came down: a riveted drum of patched rust and bare metal,
        /// four tail fins, a scorched bell, a crumpled nose with a hazard band and the side hatch hanging open as a
        /// ramp. Authored along +Z (nose forward), origin at the hull's lowest point, 0.12 m voxels.</summary>
        public static Mesh Rocket()
        {
            if (MadMax.Rendering.HDBits.On) return RocketHD();
            var g = new VoxelGrid();
            const int L = 72, R = 8;
            VoxMat hull = p =>
            {
                int band = (p.z + 200) / 18;
                float h = Pal.Hash(p, 31);
                if (p.z % 18 == 0) return Pal.Metal[1];                                                      // ring seams
                if (p.z % 18 == 2 && (p.x + p.y) % 3 == 0) return Pal.Chrome[3];                             // rivet rows
                if (p.z > 6 && p.z < 12) return (p.x + p.y + p.z) / 3 % 2 == 0 ? Pal.Ochre[4] : Pal.Black[1];  // hazard band at the tail
                if (Pal.Hash(band, (p.x > 0 ? 1 : 0) + (p.y > R ? 2 : 0), 0, 9) > 0.6f) return Pal.Rust[h > 0.85f ? 4 : h > 0.3f ? 3 : 2];   // patched plates
                return Pal.Chrome[h > 0.8f ? 3 : h > 0.25f ? 2 : 1];
            };
            g.CylZ(0f, R, R, 0, L, hull, R - 1.5f);
            // hazard band and the painted nose
            for (int z = L + 1; z <= L + 22; z++)
            {
                float k = (z - L) / 22f;
                float rr = R * (1f - k * k * 0.92f);
                int zz = z;
                g.CylZ(Mathf.Sin(k * 3f) * 0.8f, R - k * 1.5f, rr, zz, zz, p => ((p.x + p.y + zz) / 3 & 1) == 0 && k < 0.25f ? Pal.Ochre[4] : k < 0.25f ? Pal.Black[1] : Pal.Crimson[Pal.Hash(p, 3) > 0.6f ? 3 : 2]);
            }
            // the bell: scorched chrome flaring out behind
            for (int z = -8; z < 0; z++)
            {
                float rr = 3f + (-z) * 0.55f;
                g.CylZ(0f, R, rr, z, z, p => Pal.Hash(p, 8) > 0.5f ? Pal.Black[2] : Pal.Chrome[0], rr - 1.2f);
            }
            // fins: four plates at the tail
            for (int f = 0; f < 4; f++)
            {
                for (int z = 0; z < 16; z++)
                for (int e = 0; e < 7 - z / 3 + 3; e++)
                {
                    int ox = f == 0 ? R + e : f == 1 ? -(R + e) : 0, oy = f == 2 ? R + R + e : f == 3 ? -e : R;
                    g.Set(ox, oy, z, Pal.Solid(e == 0 ? Pal.Metal[1] : Pal.Crimson[2 + (z & 1)]));
                }
            }
            // the side hatch: a dark opening, its door hanging down as a ramp to the ground
            g.ClearBox(R - 2, 3, 30, R + 1, 11, 42);
            g.Box(-5, 2, 30, -5, 14, 42, Pal.Solid(Pal.Black[0]));                              // the dark hold behind it
            for (int s = 0; s < 12; s++) g.Box(R + 1 + s, 3 - s / 2, 30, R + 1 + s, 3 - s / 2, 42, p => (p.z & 1) == 0 ? Pal.Metal[2] : Pal.Rust[2]);
            // welded-on scrap: a patch plate and a spare tyre strapped to the hull
            g.Box(-R - 1, 6, 48, -R - 1, 12, 56, p => Pal.Rust[1 + (p.y + p.z) % 3]);
            g.CylX(R + R + 1, 20f, 3.5f, -2, 2, p => Pal.Tire[p.x == 0 ? 2 : 1], 1.8f);
            g.Bevel();
            return VoxelMesher.Build(g, "ScrapRocket", 0.12f);
        }

        /// <summary>An old-world descent stage: crinkled gold foil, four splayed legs with pads, a ladder, and a flag
        /// bleached almost white. Origin on the ground under its centre, 0.14 m voxels.</summary>
        public static Mesh Lander()
        {
            if (MadMax.Rendering.HDBits.On) return LanderHD();
            var g = new VoxelGrid();
            VoxMat foil = p => Pal.Hash(p.x >> 1, p.y, p.z >> 1, 12) > 0.6f ? Pal.Ochre[4] : Pal.Hash(p, 13) > 0.5f ? Pal.Ochre[3] : Pal.Bronze[3];
            for (int y = 12; y <= 24; y++)
            for (int x = -10; x <= 10; x++)
            for (int z = -10; z <= 10; z++)
                if (Mathf.Abs(x) + Mathf.Abs(z) <= 15) g.Set(x, y, z, Mathf.Abs(x) + Mathf.Abs(z) >= 14 || Mathf.Abs(x) == 10 || Mathf.Abs(z) == 10 ? foil : Pal.Solid(Pal.Metal[2]));
            g.Box(-7, 25, -7, 7, 26, 7, Pal.Ramp(Pal.Chrome));                                  // the deck the ascent stage left
            for (int k = 0; k < 4; k++)
            {
                int sx = (k & 1) == 0 ? 1 : -1, sz = k < 2 ? 1 : -1;
                g.Tube(new Vector3(sx * 8, 16, sz * 8), new Vector3(sx * 17, 1, sz * 17), 1.1f, Pal.Ramp(Pal.Chrome));
                g.Tube(new Vector3(sx * 9, 13, sz * 9), new Vector3(sx * 13, 6, sz * 13), 0.6f, Pal.Ramp(Pal.Chrome));
                g.CylY(sx * 17, sz * 17, 3f, 0, 0, Pal.Ramp(Pal.Chrome));
            }
            for (int y = 2; y < 16; y += 3) g.Box(13, y, -2, 14, y, 2, Pal.Solid(Pal.Chrome[2]));        // ladder rungs
            g.Box(13, 1, -3, 13, 16, -3, Pal.Solid(Pal.Chrome[1])); g.Box(13, 1, 3, 13, 16, 3, Pal.Solid(Pal.Chrome[1]));
            // the flag, planted a few steps off: a sun-bleached old-world banner on a wire frame
            g.Box(-22, 0, 6, -22, 23, 6, Pal.Solid(Pal.Chrome[3]));
            g.Box(-21, 23, 6, -12, 23, 6, Pal.Solid(Pal.Chrome[3]));
            g.Box(-21, 17, 6, -12, 22, 6, p => p.x < -17 && p.y > 19 ? Pal.PaleBlue[p.y % 2 == 0 ? 3 : 4] : (p.y & 1) == 0 ? Pal.Pink[4] : Pal.Cream[4]);
            g.Bevel();
            return VoxelMesher.Build(g, "OldLander", 0.14f);
        }

        // ------------------------------------------------------------------ the planet

        static Vector3 Dir(float lat, float lon)
        {
            float a = lat * Mathf.Deg2Rad, b = lon * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * Mathf.Sin(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Cos(b));
        }

        static Color32 Ground(WorldGen w, float lat, float lon, int i, int j)
        {
            float h = Pal.Hash(i, j, 0, 77);
            if (Mathf.Abs(lat) > 83f) return Pal.Cream[h > 0.5f ? 4 : 3];                        // polar caps
            float x = lon / 360f * WorldGen.Circumference, z = WorldGen.ZOfLatitude(lat);
            float c = w.ContinentNoise(x, z);
            if (c < 0.5f)
            {
                if (Mathf.Abs(lat) > 72f && h > 0.45f) return Pal.Cream[2];                       // pack ice
                float deep = Mathf.Clamp01((0.5f - c) / 0.2f);
                return deep > 0.55f ? Pal.Navy[1] : deep > 0.2f ? Pal.Navy[2] : c > 0.485f ? Pal.PaleBlue[0] : Pal.Navy[3];
            }
            if (w.SettlementAt(x, z) != null) return Pal.Metal[3];                               // rust-grey towns
            switch (w.NaturalBiome(x, z))
            {
                case Biome.Tundra: return Pal.Cream[h > 0.5f ? 1 : 0];
                case Biome.Forest: return Pal.Moss[h > 0.6f ? 3 : 2];
                case Biome.Tropical: return Pal.Moss[h > 0.5f ? 2 : 1];
                case Biome.Nuclear: return h > 0.5f ? Pal.Ochre[4] : Pal.RigGreen[4];                // fallout scars
                default: return c < 0.53f ? Pal.Sand[4] : h > 0.55f ? Pal.Sand[3] : Pal.Olive[3];
            }
        }

        static float CloudAt(Vector3 d, float lat)
        {
            float n = Pal.Noise(d * 5f + new Vector3(3f, 7f, 1f), 11) * 0.65f + Pal.Noise(d * 13f, 12) * 0.35f;
            float a = Mathf.Abs(lat);
            float belt = a < 10f ? 0.12f : a < 32f ? -0.12f : a < 65f ? 0.1f : 0f;              // wet equator, dry subtropics, storm tracks
            return n + belt;
        }

        /// <summary>The planet as a unit sphere (north = +Y, longitude 0 = +Z, east = +X): one flat-coloured quad per
        /// 2.8° × 2.8° cell sampled from the world generator, and a separate cloud shell slightly above it.</summary>
        public static Mesh Earth(WorldGen w, out Mesh clouds)
        {
            const int NLon = 128, NLat = 64;
            var gv = new List<Vector3>(); var gn = new List<Vector3>(); var gc = new List<Color32>(); var gt = new List<int>();
            var cv = new List<Vector3>(); var cn = new List<Vector3>(); var cc = new List<Color32>(); var ct = new List<int>();
            for (int j = 0; j < NLat; j++)
            for (int i = 0; i < NLon; i++)
            {
                float lat0 = -90f + 180f * j / NLat, lat1 = -90f + 180f * (j + 1) / NLat;
                float lon0 = -180f + 360f * i / NLon, lon1 = -180f + 360f * (i + 1) / NLon;
                float latC = (lat0 + lat1) * 0.5f, lonC = (lon0 + lon1) * 0.5f;
                var col = w != null ? Ground(w, latC, lonC, i, j) : Pal.Navy[2];
                Quad(gv, gn, gc, gt, Dir(lat0, lon0), Dir(lat0, lon1), Dir(lat1, lon1), Dir(lat1, lon0), col, 1f);
                if (MadMax.Rendering.HDBits.On && w != null)
                {
                    // HD: each corner its own sample, so coasts and biomes blend instead of stepping; the clouds are
                    // painted into the corners by density (an opaque shell of quads reads as white tiles)
                    int k0 = gc.Count - 4;
                    gc[k0] = Cloudy(Ground(w, lat0, lon0, i, j), lat0, lon0); gc[k0 + 1] = Cloudy(Ground(w, lat0, lon1, i + 1, j), lat0, lon1);
                    gc[k0 + 2] = Cloudy(Ground(w, lat1, lon1, i + 1, j + 1), lat1, lon1); gc[k0 + 3] = Cloudy(Ground(w, lat1, lon0, i, j + 1), lat1, lon0);
                    continue;
                }
                float cl = CloudAt(Dir(latC, lonC), latC);
                if (cl > 0.66f) Quad(cv, cn, cc, ct, Dir(lat0, lon0), Dir(lat0, lon1), Dir(lat1, lon1), Dir(lat1, lon0), cl > 0.72f ? Pal.Cream[4] : Pal.Cream[3], 1.025f);
            }
            clouds = Build("EarthClouds", cv, cn, cc, ct);
            return Build("Earth", gv, gn, gc, gt);
        }

        static Color32 Cloudy(Color32 ground, float lat, float lon)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.76f, CloudAt(Dir(lat, lon), lat)));
            return Color32.Lerp(ground, Pal.Cream[4], t * 0.92f);
        }

        static void Quad(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color32 col, float r)
        {
            int k = v.Count;
            v.Add(a * r); v.Add(b * r); v.Add(d * r); v.Add(e * r);
            n.Add(a); n.Add(b); n.Add(d); n.Add(e);
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            // outward winding: a front face's Cross(b - a, c - a) points at the viewer
            var centre = (a + b + d + e) * 0.25f;
            bool outward = Vector3.Dot(Vector3.Cross(b - a, d - a), centre) > 0f;
            if (outward) { t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3); }
            else { t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2); }
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ HD (HD pack on)
        static Mesh RocksHD()
        {
            var b = new MadMax.Rendering.HDShapes();
            var r = new System.Random(4242);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 7f + Mathf.Sqrt((float)r.NextDouble()) * (Radius - 12f);
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                if (Wild(x, z) < 0.9f) continue;
                float rad = (1.2f + Mathf.Pow((float)r.NextDouble(), 2f) * 5f) * 0.16f;
                var c = new Vector3(x, Height(x, z) + rad * 0.3f, z);
                int lumps = 2 + (int)(r.NextDouble() * 3);
                for (int k = 0; k < lumps; k++)
                {
                    var o = c + new Vector3((float)(r.NextDouble() - 0.5) * rad, (float)(r.NextDouble() - 0.5) * rad * 0.4f, (float)(r.NextDouble() - 0.5) * rad);
                    var rr = new Vector3(rad * (0.7f + (float)r.NextDouble() * 0.4f), rad * (0.45f + (float)r.NextDouble() * 0.3f), rad * (0.7f + (float)r.NextDouble() * 0.4f));
                    b.Ellipsoid(o, rr, Pal.Fur[2 + k % 2], 9, 6, false, Quaternion.Euler((float)r.NextDouble() * 30f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 30f));
                }
            }
            return b.ToMesh("MoonRocksHD");
        }

        /// <summary>HD scrap rocket: the same layout as the voxel one (along +Z, origin at the hull bottom) from tubes and plates.</summary>
        static Mesh RocketHD()
        {
            const float s = 0.12f, L = 72f, R = 8f;
            var b = new MadMax.Rendering.HDShapes();
            var axis = new Vector3(0f, R * s, 0f);
            // hull: 18-voxel drums of patched rust / bare metal, seam rings, rivet bands
            for (int k = 0; k < 4; k++)
            {
                float z0 = k * 18f, z1 = Mathf.Min(L, z0 + 18f);
                var col = Pal.Hash(k, 1, 0, 9) > 0.55f ? Pal.Rust[3] : Pal.Chrome[2];
                b.Tube(axis + Vector3.forward * z0 * s, axis + Vector3.forward * z1 * s, R * s, R * s, col, 18);
                b.Tube(axis + Vector3.forward * (z0 - 0.4f) * s, axis + Vector3.forward * (z0 + 0.4f) * s, (R + 0.25f) * s, (R + 0.25f) * s, Pal.Metal[1], 18);
            }
            b.Tube(axis + Vector3.forward * 6f * s, axis + Vector3.forward * 12f * s, (R + 0.12f) * s, (R + 0.12f) * s, Pal.Ochre[4], 18);   // hazard band
            // nose: tapering rings, crumpled crimson
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f, c = (k + 1) / 8f;
                float ra = R * (1f - a * a * 0.92f), rc = R * (1f - c * c * 0.92f);
                b.Tube(axis + new Vector3(Mathf.Sin(a * 3f) * 0.8f * s, 0f, (L + a * 22f) * s), axis + new Vector3(Mathf.Sin(c * 3f) * 0.8f * s, 0f, (L + c * 22f) * s), ra * s, rc * s, k < 2 ? Pal.Ochre[4] : Pal.Crimson[2 + (k & 1)], 18);
            }
            b.Ellipsoid(axis + new Vector3(Mathf.Sin(3f) * 0.8f * s, 0f, (L + 22f) * s), Vector3.one * R * 0.09f * s, Pal.Crimson[2], 8, 5);
            // the bell, scorched
            b.Tube(axis, axis - Vector3.forward * 8f * s, 3f * s, 7.4f * s, Pal.Black[2], 16, false);
            // fins
            for (int f = 0; f < 4; f++)
            {
                float ang = f * 90f;
                var dir = Quaternion.Euler(0f, 0f, ang) * Vector3.right;
                var o = axis + dir * (R + 4f) * s + Vector3.forward * 7f * s;
                b.Plate(o, new Vector3(8f * s, 0.8f * s, 15f * s), Pal.Crimson[2], Quaternion.LookRotation(Vector3.forward, Quaternion.Euler(0f, 0f, 90f) * dir), 6, Pal.Metal[1]);
            }
            // the hatch ramp down to the ground, the dark opening
            b.Box(new Vector3((R - 0.2f) * s, 7f * s, 36f * s), new Vector3(0.6f * s, 8f * s, 12f * s), Pal.Black[0], 0.1f * s);
            b.Plate(new Vector3((R + 6f) * s, 0.5f * s, 36f * s), new Vector3(12f * s, 0.6f * s, 12f * s), Pal.Metal[2], Quaternion.Euler(0f, 0f, -25f), 8, Pal.Rust[2]);
            // welded scrap: a patch plate and the spare tyre
            b.Plate(new Vector3(-(R + 0.6f) * s, 9f * s, 52f * s), new Vector3(7f * s, 0.6f * s, 9f * s), Pal.Rust[2], Quaternion.Euler(0f, 0f, 90f), 6, Pal.Chrome[1]);
            var tyre = new Vector3(0f, (R + R + 1f) * s, 20f * s);
            for (int k = 0; k < 14; k++)
            {
                float a0 = k * Mathf.PI * 2f / 14f, a1 = (k + 1) * Mathf.PI * 2f / 14f;
                b.Tube(tyre + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * 2.7f * s, tyre + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * 2.7f * s, 1.1f * s, 1.1f * s, Pal.Tire[1], 8);
            }
            return b.ToMesh("ScrapRocketHD");
        }

        static Mesh LanderHD()
        {
            const float s = 0.14f;
            var b = new MadMax.Rendering.HDShapes();
            var body = new Vector3(0f, 18f * s, 0f);
            b.Box(body, new Vector3(20f, 13f, 20f) * s, Pal.Ochre[4], 1.2f * s, Quaternion.Euler(0f, 45f, 0f));
            b.Box(body, new Vector3(20f, 12.6f, 20f) * s, Pal.Bronze[3], 1.2f * s);
            b.Box(new Vector3(0f, 25.5f * s, 0f), new Vector3(15f, 1.6f, 15f) * s, Pal.Chrome[2], 0.4f * s);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? 1f : -1f, sz = k < 2 ? 1f : -1f;
                b.Tube(new Vector3(sx * 8f, 16f, sz * 8f) * s, new Vector3(sx * 17f, 1f, sz * 17f) * s, 1.1f * s, 0.9f * s, Pal.Chrome[2], 8);
                b.Tube(new Vector3(sx * 9f, 13f, sz * 9f) * s, new Vector3(sx * 13f, 6f, sz * 13f) * s, 0.6f * s, 0.6f * s, Pal.Chrome[1], 6);
                b.Ellipsoid(new Vector3(sx * 17f, 0.4f, sz * 17f) * s, new Vector3(3f, 0.6f, 3f) * s, Pal.Chrome[2], 12, 4, true);
            }
            for (float y = 2f; y < 16f; y += 3f) b.Tube(new Vector3(13.5f, y, -2.5f) * s, new Vector3(13.5f, y, 2.5f) * s, 0.35f * s, 0.35f * s, Pal.Chrome[2], 6);
            b.Tube(new Vector3(13.5f, 1f, -3f) * s, new Vector3(13.5f, 16f, -3f) * s, 0.4f * s, 0.4f * s, Pal.Chrome[1], 6);
            b.Tube(new Vector3(13.5f, 1f, 3f) * s, new Vector3(13.5f, 16f, 3f) * s, 0.4f * s, 0.4f * s, Pal.Chrome[1], 6);
            b.Tube(new Vector3(-22f, 0f, 6f) * s, new Vector3(-22f, 23f, 6f) * s, 0.35f * s, 0.3f * s, Pal.Chrome[3], 6);
            b.Tube(new Vector3(-22f, 23f, 6f) * s, new Vector3(-12f, 23f, 6f) * s, 0.25f * s, 0.25f * s, Pal.Chrome[3], 6);
            b.Box(new Vector3(-16.5f, 19.5f, 6f) * s, new Vector3(9.5f, 5.5f, 0.2f) * s, Pal.Cream[4], 0.05f * s);
            b.Box(new Vector3(-19.2f, 21f, 5.85f) * s, new Vector3(4f, 2.6f, 0.15f) * s, Pal.PaleBlue[3], 0.03f * s);
            for (int k = 0; k < 3; k++) b.Box(new Vector3(-16.5f, 17.6f + k * 1.8f, 5.85f) * s, new Vector3(9.4f, 0.8f, 0.12f) * s, Pal.Pink[4], 0.02f * s);
            return b.ToMesh("OldLanderHD");
        }
    }
}
