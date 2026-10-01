using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    public enum ArmorZone : byte { Front, Left, Right, Rear, Roof, Wheels, Windows }
    public enum ArmorMat : byte { None, Scrap, Steel, Composite }

    /// <summary>Welded-on armour (roadmap 11), fitted to any vehicle: the body's voxel shell (body, roof, doors, hood) is
    /// recovered from its mesh once per vehicle type, and each zone — front, sides, rear, roof, wheel guards over the arches,
    /// window grilles over the glass — becomes a one-voxel plate that follows the body. Materials: scrap sheet (cheap),
    /// steel plate (stops bullets, heavy), tyre-rubber composite (soaks crashes, burns). Crashes, gunfire and tool hits
    /// on a zone go through its armour first (VehicleDamage); the armour wears and is torn off at zero. Grilles keep
    /// shots off the driver. The weight goes into the chassis mass (handling, fuel).</summary>
    public class VehicleArmor : MonoBehaviour
    {
        public const int Zones = 7;
        public static readonly string[] ZoneNames = { "FRONT", "LEFT SIDE", "RIGHT SIDE", "REAR", "ROOF", "WHEEL GUARDS", "WINDOW GRILLES" };
        public static readonly string[] MatNames = { "NONE", "SCRAP SHEET", "STEEL PLATE", "TYRE COMPOSITE" };

        /// <summary>crash / bullet: share soaked at full condition; fire: heat intake factor; kg per voxel face; wear:
        /// condition lost per unit soaked; cost: one <c>res</c> per <c>per</c> voxels (plus <c>res2</c> per <c>per2</c>).</summary>
        public struct MatStats { public float crash, bullet, fire, kg, wear; public ResourceType res, res2; public int per, per2; }
        public static readonly MatStats[] Mats =
        {
            default,
            new MatStats { crash = 0.35f, bullet = 0.45f, fire = 0.95f, kg = 0.12f, wear = 0.09f, res = ResourceType.Scrap, per = 12 },
            new MatStats { crash = 0.55f, bullet = 0.8f, fire = 0.75f, kg = 0.3f, wear = 0.04f, res = ResourceType.Iron, per = 10 },
            new MatStats { crash = 0.6f, bullet = 0.35f, fire = 1.35f, kg = 0.16f, wear = 0.05f, res = ResourceType.Rubber, per = 10, res2 = ResourceType.Scrap, per2 = 24 },
        };

        public readonly ArmorMat[] mat = new ArmorMat[Zones];
        public readonly float[] condition = new float[Zones];
        readonly GameObject[] shown = new GameObject[Zones];
        Transform body;
        VehicleChassis chassis;
        VehicleDriver driver;
        Shape shape;

        const float S = VoxelMesher.DefaultSize;
        class Shape
        {
            public readonly List<Vector3Int>[] zone = new List<Vector3Int>[Zones];
            public Vector3Int min, max;
            public int belt;
            public Bounds glass; public bool hasGlass;
        }
        static readonly Dictionary<string, Shape> shapes = new Dictionary<string, Shape>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { shapes.Clear(); meshes.Clear(); }

        void Awake()
        {
            body = transform.Find("Body");
            chassis = GetComponent<VehicleChassis>();
            driver = GetComponent<VehicleDriver>();
        }

        public bool Any { get { foreach (var m in mat) if (m != ArmorMat.None) return true; return false; } }
        public int Voxels(ArmorZone z) { Ensure(); return shape.zone[(int)z].Count; }

        /// <summary>Materials to weld a zone in <paramref name="m"/> (a repair costs the missing share).</summary>
        public void Cost(ArmorZone z, ArmorMat m, float share, out ResourceType r1, out int n1, out ResourceType r2, out int n2)
        {
            var st = Mats[(int)m]; int vox = Voxels(z);
            r1 = st.res; n1 = m == ArmorMat.None ? 0 : Mathf.Max(1, Mathf.CeilToInt(vox * share / st.per));
            r2 = st.res2; n2 = st.per2 > 0 ? Mathf.Max(1, Mathf.CeilToInt(vox * share / st.per2)) : 0;
        }

        public float Kg(ArmorZone z) => mat[(int)z] == ArmorMat.None ? 0f : Voxels(z) * Mats[(int)mat[(int)z]].kg;
        public float TotalKg { get { float k = 0f; for (int i = 0; i < Zones; i++) k += Kg((ArmorZone)i); return k; } }

        /// <summary>Heat taken from fire around the vehicle (rubber burns, steel shields).</summary>
        public float FireFactor
        {
            get { float f = 1f; for (int i = 0; i < Zones; i++) if (mat[i] != ArmorMat.None) f += (Mats[(int)mat[i]].fire - 1f) * 0.25f * condition[i]; return Mathf.Max(0.3f, f); }
        }

        /// <summary>How much of the shots at the driver the window grilles stop.</summary>
        public float GrilleCover => mat[(int)ArmorZone.Windows] == ArmorMat.None ? 0f : 0.75f * (0.4f + 0.6f * condition[(int)ArmorZone.Windows]);

        // ------------------------------------------------------------------ install / damage
        public void Set(ArmorZone z, ArmorMat m, float cond)
        {
            int i = (int)z;
            mat[i] = m; condition[i] = m == ArmorMat.None ? 0f : Mathf.Clamp01(cond);
            Show(z);
            if (chassis) { chassis.extraMass = TotalKg; chassis.NotifyChanged(); }
        }

        /// <summary>The zone a world point on the vehicle belongs to.</summary>
        public ArmorZone ZoneAt(Vector3 world)
        {
            Ensure();
            var p = body.InverseTransformPoint(world) / S;
            if (shape.hasGlass && shape.glass.SqrDistance(p) < 1.5f * 1.5f && p.y > shape.belt) return ArmorZone.Windows;
            foreach (var s in chassis.Sockets)
            {
                var w = s.Current;
                if (!w || w.category != PartCategory.Wheel) continue;
                if (Vector3.Distance(body.InverseTransformPoint(w.transform.position) / S, p) < w.radius / S + 4f) return ArmorZone.Wheels;
            }
            if (p.y > shape.max.y - 3) return ArmorZone.Roof;
            Vector3 c = (Vector3)(shape.min + shape.max) * 0.5f, half = (Vector3)(shape.max - shape.min) * 0.5f + Vector3.one;
            float fx = (p.x - c.x) / half.x, fz = (p.z - c.z) / half.z;
            if (Mathf.Abs(fz) > Mathf.Abs(fx)) return fz > 0f ? ArmorZone.Front : ArmorZone.Rear;
            return fx > 0f ? ArmorZone.Right : ArmorZone.Left;
        }

        /// <summary>Let the zone's armour soak a hit. Returns the share it stopped (0 = bare).</summary>
        public float Soak(Vector3 world, float amount, bool bullet)
        {
            if (!Any) return 0f;
            var z = ZoneAt(world);
            int i = (int)z;
            if (mat[i] == ArmorMat.None) return 0f;
            var st = Mats[(int)mat[i]];
            float share = (bullet ? st.bullet : st.crash) * (0.3f + 0.7f * condition[i]);
            condition[i] -= amount * share * st.wear;
            if (condition[i] <= 0f) TearOff(z);
            return share;
        }

        void TearOff(ArmorZone z)
        {
            int i = (int)z;
            var fx = MadMax.World.DebrisSystem.Instance;
            if (fx && shown[i] && shown[i].TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
            {
                var v = mf.sharedMesh.vertices; var c = mf.sharedMesh.colors32;
                var chunks = new List<MadMax.World.DebrisSystem.Chunk>();
                for (int k = 0; k < 40 && v.Length > 0; k++) { int r = Random.Range(0, v.Length); chunks.Add(new MadMax.World.DebrisSystem.Chunk { position = mf.transform.TransformPoint(v[r]), color = c[r] }); }
                fx.Emit(chunks, 0.07f, Vector3.up * 2f);
            }
            MadMax.Audio.Sfx.Play("crash_small", transform.position, 0.8f, 0.7f);
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current == driver) g.Toast(ZoneNames[i] + " ARMOUR TORN OFF");
            Set(z, ArmorMat.None, 0f);
        }

        // ------------------------------------------------------------------ save
        public string SaveState()
        {
            if (!Any) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Zones; i++) sb.Append((int)mat[i]).Append(',').Append(condition[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            return sb.ToString();
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(';');
            for (int i = 0; i < Zones && i < parts.Length; i++)
            {
                var kv = parts[i].Split(',');
                if (kv.Length < 2 || !int.TryParse(kv[0], out int m)) continue;
                float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float c);
                Set((ArmorZone)i, (ArmorMat)Mathf.Clamp(m, 0, 3), c);
            }
        }

        /// <summary>Raider / wreck kit: a random welded-on set (deterministic from the seed).</summary>
        public void RandomKit(int seed, float wear)
        {
            var r = new System.Random(seed);
            for (int i = 0; i < Zones; i++)
            {
                if (r.NextDouble() > (i == (int)ArmorZone.Windows || i == (int)ArmorZone.Front ? 0.7 : 0.45)) continue;
                var m = r.NextDouble() < 0.6 ? ArmorMat.Scrap : r.NextDouble() < 0.5 ? ArmorMat.Composite : ArmorMat.Steel;
                Set((ArmorZone)i, m, 1f - wear * (float)r.NextDouble());
            }
        }

        // ------------------------------------------------------------------ shape
        void Ensure()
        {
            if (shape != null) return;
            if (!body) body = transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            string key = name.Replace("(Clone)", "").Replace("Wreck ", "").Trim() + "|" + (mf && mf.sharedMesh ? mf.sharedMesh.vertexCount : 0);
            if (!shapes.TryGetValue(key, out shape)) shapes[key] = shape = BuildShape();
        }

        Shape BuildShape()
        {
            var sh = new Shape();
            for (int i = 0; i < Zones; i++) sh.zone[i] = new List<Vector3Int>();
            var shell = new HashSet<Vector3Int>();
            if (!body) return sh;
            AddMesh(body, shell, null);
            var roof = body.Find("Roof");
            if (roof) AddMesh(roof, shell, null);
            foreach (var s in chassis.Sockets)
                if (s.Current && (s.Current.category == PartCategory.Door || s.Current.category == PartCategory.Hood)) AddMesh(s.Current.transform, shell, null);
            if (shell.Count == 0) return sh;
            var glassFaces = new List<(Vector3Int p, Vector3Int n)>();
            var glassT = body.Find("Glass");
            if (glassT) AddMesh(glassT, null, glassFaces);
            foreach (var s in chassis.Sockets)                                                              // HD doors carry their windows
                if (s.Current && s.Current.category == PartCategory.Door && s.Current.transform.Find("Glass") is Transform dg) AddMesh(dg, null, glassFaces);

            Vector3Int mn = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), mx = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var p in shell) { mn = Vector3Int.Min(mn, p); mx = Vector3Int.Max(mx, p); }
            sh.min = mn; sh.max = mx;
            int glassLow = int.MaxValue;
            if (glassFaces.Count > 0)
            {
                sh.hasGlass = true;
                sh.glass = new Bounds(glassFaces[0].p, Vector3.zero);
                foreach (var f in glassFaces) { sh.glass.Encapsulate(f.p); glassLow = Mathf.Min(glassLow, f.p.y); }
            }
            sh.belt = glassLow != int.MaxValue ? glassLow : mn.y + Mathf.RoundToInt((mx.y - mn.y) * 0.55f);

            // wheels (voxel space): centre, radius, x span
            var wheels = new List<(Vector3 c, float r, float xa, float xb)>();
            foreach (var s in chassis.Sockets)
            {
                var w = s.Current;
                if (!w || w.category != PartCategory.Wheel) continue;
                var c = body.InverseTransformPoint(w.transform.position) / S;
                float width = (w.TryGetComponent<WheelStats>(out var ws) ? ws.width : 0.3f) / S;
                float side = c.x >= (mn.x + mx.x) * 0.5f ? 1f : -1f;
                wheels.Add((c, w.radius / S, c.x, c.x + side * width));
            }
            bool NearWheel(int y, int z, float extra)
            {
                foreach (var w in wheels) if (new Vector2(y - w.c.y, z - w.c.z).magnitude < w.r + extra) return true;
                return false;
            }

            var rowMin = new Dictionary<Vector2Int, int>(); var rowMax = new Dictionary<Vector2Int, int>();   // (y, z) → x
            var colMin = new Dictionary<Vector2Int, int>(); var colMax = new Dictionary<Vector2Int, int>();   // (x, y) → z
            var top = new Dictionary<Vector2Int, int>();                                                      // (x, z) → y
            foreach (var p in shell)
            {
                var yz = new Vector2Int(p.y, p.z); var xy = new Vector2Int(p.x, p.y); var xz = new Vector2Int(p.x, p.z);
                rowMin[yz] = rowMin.TryGetValue(yz, out int a) ? Mathf.Min(a, p.x) : p.x;
                rowMax[yz] = rowMax.TryGetValue(yz, out int b) ? Mathf.Max(b, p.x) : p.x;
                colMin[xy] = colMin.TryGetValue(xy, out int c) ? Mathf.Min(c, p.z) : p.z;
                colMax[xy] = colMax.TryGetValue(xy, out int d) ? Mathf.Max(d, p.z) : p.z;
                top[xz] = top.TryGetValue(xz, out int e) ? Mathf.Max(e, p.y) : p.y;
            }
            int y0 = mn.y + 1;
            // sides: from the sill up to the window line, following the body but never sunk into door gaps
            for (int y = y0; y < sh.belt; y++)
            for (int z = mn.z; z <= mx.z; z++)
            {
                var k = new Vector2Int(y, z);
                if (!rowMax.TryGetValue(k, out int xr) || NearWheel(y, z, 1.5f)) continue;
                sh.zone[(int)ArmorZone.Right].Add(new Vector3Int(Mathf.Clamp(xr + 1, mx.x - 2, mx.x + 1), y, z));
                sh.zone[(int)ArmorZone.Left].Add(new Vector3Int(Mathf.Clamp(rowMin[k] - 1, mn.x - 1, mn.x + 2), y, z));
            }
            // front and rear faces up to the window line
            for (int x = mn.x; x <= mx.x; x++)
            for (int y = y0; y < sh.belt; y++)
            {
                var k = new Vector2Int(x, y);
                if (!colMax.TryGetValue(k, out int zf)) continue;
                sh.zone[(int)ArmorZone.Front].Add(new Vector3Int(x, y, Mathf.Clamp(zf + 1, mx.z - 3, mx.z + 1)));
                sh.zone[(int)ArmorZone.Rear].Add(new Vector3Int(x, y, Mathf.Clamp(colMin[k] - 1, mn.z - 1, mn.z + 3)));
            }
            // roof over the cabin (columns that rise above the windows)
            foreach (var kv in top)
                if (kv.Value >= sh.belt + 3) sh.zone[(int)ArmorZone.Roof].Add(new Vector3Int(kv.Key.x, kv.Value + 1, kv.Key.y));
            // wheel guards: arcs over the top of each tyre
            var guard = new HashSet<Vector3Int>();
            foreach (var w in wheels)
            {
                float R = w.r + 2f;
                int x0 = Mathf.FloorToInt(Mathf.Min(w.xa, w.xb)) - 1, x1 = Mathf.CeilToInt(Mathf.Max(w.xa, w.xb)) + 1;
                for (float a = 15f; a <= 165f; a += 40f / R)
                {
                    int y = Mathf.RoundToInt(w.c.y + R * Mathf.Sin(a * Mathf.Deg2Rad)), z = Mathf.RoundToInt(w.c.z + R * Mathf.Cos(a * Mathf.Deg2Rad));
                    for (int x = x0; x <= x1; x++) { var p = new Vector3Int(x, y, z); if (!shell.Contains(p)) guard.Add(p); }
                }
            }
            sh.zone[(int)ArmorZone.Wheels].AddRange(guard);
            // grilles: bars one voxel out from the outside faces of the glass
            var centre = (Vector3)(mn + mx) * 0.5f;
            var bars = new HashSet<Vector3Int>();
            foreach (var (p, n) in glassFaces)
            {
                if (n.y < 0 || Vector3.Dot(n, (Vector3)p - centre) <= 0f) continue;                         // inside faces
                var q = p + n;
                if (shell.Contains(q)) continue;
                // vertical bars every 5 voxels and a rail every 10: the view out stays mostly clear
                int across = n.x != 0 ? q.z : q.x, up = n.y != 0 ? q.z : q.y;
                if (Mod(across, 5) == 0 || Mod(up, 10) == 0) bars.Add(q);
            }
            sh.zone[(int)ArmorZone.Windows].AddRange(bars);
            return sh;
        }

        /// <summary>Surface voxels of a mesh built by <see cref="VoxelMesher"/> (4 vertices per face), in body voxel space.</summary>
        void AddMesh(Transform t, HashSet<Vector3Int> shell, List<(Vector3Int, Vector3Int)> faces)
        {
            if (!t.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return;
            var mesh = mf.sharedMesh;
            var v = t.TryGetComponent<DeformableMesh>(out var dm) && dm.Pristine != null ? dm.Pristine : mesh.vertices;   // before any dents
            var nrm = mesh.normals;
            if (nrm.Length != v.Length) return;
            var m = body.worldToLocalMatrix * t.localToWorldMatrix;
            if (MadMax.Rendering.HDModel.IsHDMesh(mesh)) { AddTriangles(mesh, v, m, shell, faces); return; }
            for (int i = 0; i + 3 < v.Length; i += 4)
            {
                var c = m.MultiplyPoint3x4((v[i] + v[i + 1] + v[i + 2] + v[i + 3]) * 0.25f) / S;
                var n = m.MultiplyVector(nrm[i]).normalized;
                var p = Vector3Int.RoundToInt(c - n * 0.5f);
                shell?.Add(p);
                faces?.Add((p, Vector3Int.RoundToInt(n)));
            }
        }

        /// <summary>HD meshes: the triangles are sampled every half voxel; each sample marks the voxel just inside the
        /// surface (faces get the dominant axis of the triangle normal).</summary>
        static void AddTriangles(Mesh mesh, Vector3[] v, Matrix4x4 m, HashSet<Vector3Int> shell, List<(Vector3Int, Vector3Int)> faces)
        {
            var tris = mesh.triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[tris[i]]) / S, b = m.MultiplyPoint3x4(v[tris[i + 1]]) / S, c = m.MultiplyPoint3x4(v[tris[i + 2]]) / S;
                var nrm = Vector3.Cross(b - a, c - a);
                if (nrm.sqrMagnitude < 1e-10f) continue;
                nrm.Normalize();
                var ax = Mathf.Abs(nrm.x) >= Mathf.Abs(nrm.y) && Mathf.Abs(nrm.x) >= Mathf.Abs(nrm.z) ? new Vector3Int((int)Mathf.Sign(nrm.x), 0, 0)
                    : Mathf.Abs(nrm.y) >= Mathf.Abs(nrm.z) ? new Vector3Int(0, (int)Mathf.Sign(nrm.y), 0) : new Vector3Int(0, 0, (int)Mathf.Sign(nrm.z));
                int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude)) * 2f), 1, 64);
                for (int u = 0; u <= n; u++)
                for (int w = 0; w <= n - u; w++)
                {
                    var p = a + (b - a) * (u / (float)n) + (c - a) * (w / (float)n);
                    var q = Vector3Int.RoundToInt(p - nrm * 0.5f);
                    shell?.Add(q);
                    faces?.Add((q, ax));
                }
            }
        }

        // ------------------------------------------------------------------ visuals
        void Show(ArmorZone z)
        {
            int i = (int)z;
            if (shown[i]) Destroy(shown[i]);
            shown[i] = null;
            if (mat[i] == ArmorMat.None || !body) return;
            Ensure();
            var list = shape.zone[i];
            if (list.Count == 0) return;
            string key = shape.min + "|" + shape.max + "|" + list.Count + "|" + i + "|" + (int)mat[i];
            if (!meshes.TryGetValue(key, out var mesh) || !mesh)
            {
                var g = new VoxelGrid();
                var m = mat[i];
                g.Mat((byte)(m == ArmorMat.Steel ? ResourceType.Iron : m == ArmorMat.Composite ? ResourceType.Rubber : ResourceType.Scrap));
                foreach (var p in list) g.Set(p, Paint(m, z));
                g.Bevel();
                meshes[key] = mesh = VoxelMesher.Build(g, "Armor_" + ZoneNames[i] + "_" + MatNames[(int)m]);
            }
            var go = new GameObject("Armor_" + z, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(z == ArmorZone.Windows ? transform : body, false);                    // grilles stay visible from the cab
            if (z == ArmorZone.Windows) { go.transform.localPosition = body.localPosition; go.transform.localRotation = body.localRotation; go.transform.localScale = body.localScale; }
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var br = body.GetComponent<MeshRenderer>();
            if (br) go.GetComponent<MeshRenderer>().sharedMaterial = MadMax.Rendering.HDModel.VoxelMaterialFor(br);   // voxel plates: HDLit with vertex colours on HD bodies
            shown[i] = go;
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;
        static int Div(int a, int m) => a >= 0 ? a / m : -((-a + m - 1) / m);

        static VoxMat Paint(ArmorMat m, ArmorZone z)
        {
            if (z == ArmorZone.Windows)
                return m == ArmorMat.Steel ? Pal.Ramp(Pal.Metal, 1, 1201) : m == ArmorMat.Composite ? Pal.Ramp(Pal.Tire, 2, 1202) : Pal.Ramp(Pal.Rust, 2, 1203);
            // seams, patches and tread run in the plate's own plane, whichever way it faces
            System.Func<Vector3Int, Vector2Int> uv = z == ArmorZone.Left || z == ArmorZone.Right ? p => new Vector2Int(p.z, p.y)
                : z == ArmorZone.Front || z == ArmorZone.Rear ? p => new Vector2Int(p.x, p.y) : (System.Func<Vector3Int, Vector2Int>)(p => new Vector2Int(p.x, p.z));
            switch (m)
            {
                case ArmorMat.Steel:
                    return p =>
                    {
                        var q = uv(p);
                        bool seam = Mod(q.x, 12) == 0 || Mod(q.y, 9) == 0;
                        return seam ? (Mod(q.x + q.y, 3) == 0 ? Pal.Chrome[1] : Pal.Metal[0]) : Pal.Pick(Pal.Metal, p, 1211, 2);
                    };
                case ArmorMat.Composite:
                    return p => { var q = uv(p); return Mod(q.x, 10) == 0 ? Pal.Metal[2] : Mod(q.x + q.y, 4) < 2 ? Pal.Tire[2] : Pal.Tire[1]; };
                default:
                    return p =>
                    {
                        // patchwork of scavenged sheets, riveted at the edges
                        var q = uv(p);
                        float h = Pal.Hash(Div(q.x, 7), Div(q.y, 5), (int)z, 1221);
                        if ((Mod(q.x, 7) == 0 || Mod(q.y, 5) == 0) && Mod(q.x + q.y, 2) == 0) return Pal.Chrome[1];
                        return h < 0.35f ? Pal.Pick(Pal.Rust, p, 1222, 2) : h < 0.7f ? Pal.Pick(Pal.Metal, p, 1223, 2) : Pal.Pick(Pal.Olive, p, 1224, 1);
                    };
            }
        }
    }
}
