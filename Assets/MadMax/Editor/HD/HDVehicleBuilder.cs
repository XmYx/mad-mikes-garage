using System;
using System.Collections.Generic;
using MadMax.Designs;
using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.EditorTools
{
    /// <summary>Builds every vehicle prefab from its HD model (Models/HD/&lt;group&gt;/&lt;Name&gt;/, see tools/blender/hd/PIPELINE.md)
    /// on top of what <see cref="MadMaxBuilder"/> makes from the voxel design: the HD body, glazing, lamps, interior and
    /// seats replace the voxel meshes; doors, hood, trunk / tailgate, bumpers and wheels become HD part prefabs (real
    /// radii, the design's tyre stats); generic parts come from the parts pack; sockets, colliders, eye points, interiors,
    /// tracks, decks, spinners and boat hulls move to the HD geometry (real-world sizes). Everything else (mass, drive,
    /// systems, damage) keeps the design's data. A design whose HD files are missing (or every design, with
    /// <b>MadMax/HD/Force Voxel Vehicles</b>) builds from voxels as before.
    /// Partial: <c>.Parts</c> (part prefabs), <c>.Rig</c> (body, sockets, colliders, special rigs).</summary>
    public static partial class HDVehicleBuilder
    {
        const float S = VoxelMesher.DefaultSize;
        const string ForcePref = "MadMax.HD.ForceVoxelVehicles", ForceMenu = "MadMax/HD/Force Voxel Vehicles";

        /// <summary>Designs kept on the voxel path by hand (comparison, a broken export).</summary>
        public static readonly HashSet<string> Disabled = new HashSet<string>();

        /// <summary>Editor toggle: build every vehicle (and generic part) from voxels, for comparison.</summary>
        public static bool ForceVoxel
        {
            get => EditorPrefs.GetBool(ForcePref, false);
            set => EditorPrefs.SetBool(ForcePref, value);
        }

        [MenuItem(ForceMenu, false, 40)]
        static void ToggleForceVoxel()
        {
            ForceVoxel = !ForceVoxel;
            Debug.Log("[HD] vehicles build from " + (ForceVoxel ? "VOXELS" : "HD models") + " — run MadMax/Build Parts + Vehicles (or Build Game Scene) to apply");
        }

        [MenuItem(ForceMenu, true)]
        static bool ToggleForceVoxelCheck() { Menu.SetChecked(ForceMenu, ForceVoxel); return true; }

        /// <summary>Per-design build plan, made by <see cref="SaveParts"/> and used by <see cref="ApplyBody"/> (same build).</summary>
        static readonly Dictionary<string, Plan> plans = new Dictionary<string, Plan>();
        static Func<string, PartDesign> designOf = k => null;

        /// <summary>The design's HD export when it can be used, else null (voxel path, with the reason in the console).</summary>
        public static HDSidecar For(VehicleDesign d)
        {
            if (d == null || ForceVoxel || Disabled.Contains(d.name)) return null;
            var s = HDSidecar.Find(d.name);
            string why = null;
            if (s == null) why = $"no HD export in {HDSidecar.Root} (run tools/blender/hd/export/run_export.py {d.name})";
            else if (s.objects == null || s.objects.Length == 0) why = "the sidecar lists no objects";
            else if (s.kind != "vehicle") why = $"the export is kind '{s.kind}', not vehicle";
            else if (s.Get("Body") == null && s.Get("Hull") == null) why = "the export has no Body / Hull object";
            else if (!AssetDatabase.LoadAssetAtPath<GameObject>(s.ModelPath)) why = "the model is not imported yet: " + s.ModelPath;
            if (why == null) return s;
            Debug.LogWarning($"[HD] {d.name}: {why}; voxel model kept");
            return null;
        }

        // =========================================================================================== plan
        /// <summary>Which HD object stands for which design socket, what is consumed by sockets / rigs (not body dressing).</summary>
        class Plan
        {
            public VehicleDesign d;
            public HDSidecar side;
            public string vlow;
            public Dictionary<string, Mesh> meshes;
            public float size;
            public bool crawler;
            public readonly Dictionary<string, HDObject> sockets = new Dictionary<string, HDObject>();   // design socket (right / centre) → HD object
            public readonly HashSet<string> used = new HashSet<string>();                                // HD objects that are no body dressing
            public readonly Dictionary<string, string> keys = new Dictionary<string, string>();         // design socket → HD part key to mount
            public readonly Dictionary<string, Vector3> points = new Dictionary<string, Vector3>();     // socket → position fixed by a part build (wheels, lids)
            public readonly Dictionary<string, float> radii = new Dictionary<string, float>();          // wheel socket → HD radius
            public readonly List<HDObject> tracks = new List<HDObject>();
            public readonly Dictionary<string, HDObject> drums = new Dictionary<string, HDObject>();     // wheel socket → roller drum
            public readonly List<(string socket, HDObject o)> lids = new List<(string, HDObject)>();     // trunk / tailgate: sockets the design lacks
        }

        static string Strip(string s) => s.EndsWith("_L") || s.EndsWith("_R") ? s.Substring(0, s.Length - 2) : s;

        /// <summary>The design socket an HD socket base name stands for: the right one of a pair, or the single one.</summary>
        static SocketDesign DesignSocket(VehicleDesign d, string b) => d.sockets.Find(s => s.name == b + "_R") ?? d.sockets.Find(s => s.name == b && !s.mirrored);

        static bool IsSpinner(VehicleDesign d, string n) => n == "Prop" || n == "Rotor" || d.spinners.Exists(sp => sp.name == n);
        static bool IsSurface(string n) => n == "Wing" || n == "Rudder";
        static bool IsMovable(VehicleDesign d, HDObject o) => !string.IsNullOrEmpty(o.Prop("movable")) || d.movable.Exists(m => m.name == o.name);
        static bool IsFurniture(HDObject o) => o.name.StartsWith("Furn_") || !string.IsNullOrEmpty(o.Prop("piece"));

        static Plan MakePlan(VehicleDesign d, HDSidecar side)
        {
            var p = new Plan { d = d, side = side, vlow = d.name.ToLowerInvariant() };
            foreach (var o in side.objects)
            {
                if (o.role != "mesh" || o.name == "Ground" || !string.IsNullOrEmpty(o.parent)) continue;
                if (o.type != "MESH" && o.type != "EMPTY") continue;
                string sock = o.Prop("socket"), b = null;
                bool left = false, drum = false;
                if (!string.IsNullOrEmpty(sock))
                {
                    if (sock == "tracks") { p.tracks.Add(o); p.used.Add(o.name); continue; }
                    left = sock.EndsWith("_L") || o.Prop("mirrored") == "true";
                    b = Strip(sock);
                }
                else if (o.name.StartsWith("Wheel_"))                                   // misc exports: Wheel_Front, Wheel_Main_L, Wheel_Side ...
                {
                    string rest = o.name.Substring(6).ToLowerInvariant();
                    left = rest.EndsWith("_l");
                    if (rest.EndsWith("_l") || rest.EndsWith("_r")) rest = rest.Substring(0, rest.Length - 2);
                    b = "wheel_" + rest;
                }
                else if (o.name.StartsWith("Drum_")) { b = "wheel_" + o.name.Substring(5).ToLowerInvariant(); drum = true; }   // roller drums ride on the wheel sockets
                else if (o.name == "Trunk" || o.name == "Tailgate") { p.lids.Add((o.name.ToLowerInvariant(), o)); p.used.Add(o.name); continue; }
                else
                {
                    var pd = designOf(o.name) ?? d.parts.Find(x => x.key == o.name);      // an object named like a part key (misc: engine_vtwin, weapon_mg)
                    if (pd != null)
                    {
                        var ds0 = d.sockets.Find(s => !s.mirrored && s.part == o.name) ?? d.sockets.Find(s => !s.mirrored && s.accepts == pd.category);
                        if (ds0 != null) b = ds0.name.EndsWith("_R") ? Strip(ds0.name) : ds0.name;
                        else p.used.Add(o.name);                                            // a part the design has no socket for: not body dressing
                    }
                }
                if (b == null) continue;
                p.used.Add(o.name);
                if (left) continue;                                                         // left sides mirror the right part
                var ds = DesignSocket(d, b);
                if (ds == null) { Debug.LogWarning($"[HD] {d.name}: HD object {o.name} names socket '{b}' the design does not have"); continue; }
                if (drum) p.drums[ds.name] = o;
                if (!p.sockets.ContainsKey(ds.name)) p.sockets[ds.name] = o;
            }
            p.crawler = d.crawler || p.tracks.Count > 0;
            return p;
        }

        // =========================================================================================== meshes
        /// <summary>Object name -> imported mesh (from the model prefab, so names match the sidecar).</summary>
        static Dictionary<string, Mesh> Meshes(HDSidecar side)
        {
            var map = new Dictionary<string, Mesh>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(side.ModelPath);
            if (!model) { Debug.LogError("[HD] model not imported: " + side.ModelPath); return map; }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) map[mf.name] = mf.sharedMesh;
            foreach (var sm in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sm.sharedMesh) map[sm.name] = sm.sharedMesh;
            return map;
        }

        static float Size(HDSidecar side)
        {
            var b = side.Size;
            return Mathf.Max(1f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        }

        /// <summary>Give <paramref name="host"/> the object's mesh and atlas material; its glazing / lamps become children
        /// named Glass, Lamp_Head ... and its LODs children LOD1 / LOD2 under one LOD group sized like the whole vehicle
        /// (so every part switches together).</summary>
        static void AddMesh(GameObject host, HDSidecar side, HDObject o, Dictionary<string, Mesh> meshes, float size)
        {
            if (o.type != "MESH") return;
            var mat = HDMaterials.For(side, o);
            var r0 = SetMesh(host, o, meshes, mat);
            var l1 = new List<Renderer>(); var l2 = new List<Renderer>();
            foreach (var c in side.Children(o.name))
            {
                if (c.type != "MESH" || c.role == "mesh") continue;            // whole objects parented here are placed by the caller
                string n = c.role == "lod1" ? "LOD1" : c.role == "lod2" ? "LOD2" : c.role == "glass" ? "Glass" : RoleName(c.role);
                var go = new GameObject(n);
                go.transform.SetParent(host.transform, false);
                go.transform.localPosition = c.LocalPosition; go.transform.localRotation = c.LocalRotation; go.transform.localScale = c.LocalScale;
                var r = SetMesh(go, c, meshes, HDMaterials.For(side, c) ?? mat);
                if (!r) continue;
                if (c.role == "lod1") l1.Add(r);
                else if (c.role == "lod2") l2.Add(r);
                else r.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (r0 && l1.Count > 0)
            {
                var lg = (host.TryGetComponent<LODGroup>(out var haveLODGroup) ? haveLODGroup : host.AddComponent<LODGroup>());
                lg.SetLODs(HDLod.Levels(new List<Renderer> { r0 }, l1, l2));
                lg.size = size;
                lg.localReferencePoint = r0.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh ? mf.sharedMesh.bounds.center : Vector3.zero;
            }
        }

        static string RoleName(string role)
        {
            if (role.StartsWith("lamp_")) return "Lamp_" + char.ToUpperInvariant(role[5]) + role.Substring(6);
            return string.IsNullOrEmpty(role) ? "Mesh" : char.ToUpperInvariant(role[0]) + role.Substring(1);
        }

        static Renderer SetMesh(GameObject go, HDObject o, Dictionary<string, Mesh> meshes, Material mat)
        {
            if (!meshes.TryGetValue(o.name, out var mesh)) { Debug.LogWarning("[HD] mesh not found in the model: " + o.name); return null; }
            var mf = (go.TryGetComponent<MeshFilter>(out var haveMeshFilter) ? haveMeshFilter : go.AddComponent<MeshFilter>());
            mf.sharedMesh = mesh;
            var mr = (go.TryGetComponent<MeshRenderer>(out var haveMeshRenderer) ? haveMeshRenderer : go.AddComponent<MeshRenderer>());
            mr.sharedMaterials = HDMaterials.Slots(mat, mesh.subMeshCount);
            return mr;
        }

        /// <summary>Put a root-level (or Body-level) HD object's transform on <paramref name="t"/>, relative to <paramref name="origin"/>.</summary>
        static void Place(Transform t, HDObject o, Vector3 origin)
        {
            t.localPosition = o.RootPosition - origin;
            t.localRotation = o.RootRotation;
            var sc = o.LocalScale;
            t.localScale = new Vector3(Mathf.Abs(sc.x), Mathf.Abs(sc.y), Mathf.Abs(sc.z));
        }

        /// <summary>Mesh bounds of a host and its non-LOD children in the coordinates of <paramref name="space"/>.</summary>
        static bool LocalBounds(Transform host, Transform space, out Bounds b)
        {
            b = default; bool any = false;
            foreach (var mf in host.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || HDModel.IsLod(mf.transform)) continue;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(new Vector3((i & 1) != 0 ? mb.max.x : mb.min.x, (i & 2) != 0 ? mb.max.y : mb.min.y, (i & 4) != 0 ? mb.max.z : mb.min.z));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                }
            }
            return any;
        }

        static BoxCollider BoxAround(GameObject go, Transform host)
        {
            if (!LocalBounds(host, go.transform, out var b)) return null;
            var c = go.AddComponent<BoxCollider>();
            c.center = b.center; c.size = Vector3.Max(b.size, Vector3.one * 0.04f);
            return c;
        }

        /// <summary>Maps design (voxel) positions onto the HD model: the voxel body box (body, glass, roof) stretched onto
        /// the HD body box (body, glass, hull, roof, wing and the panels cut from the body), per axis.</summary>
        class BoxMap
        {
            public readonly Bounds vox, hd;

            public BoxMap(VehicleDesign d, Plan p)
            {
                bool any = false;
                Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
                foreach (var g in new[] { d.body, d.glass, d.roof })
                {
                    if (g == null) continue;
                    foreach (var k in g.voxels.Keys) { mn = Vector3.Min(mn, k); mx = Vector3.Max(mx, k); any = true; }
                }
                vox = any ? new Bounds(((mn + mx) * 0.5f) * S, (mx - mn + Vector3.one) * S) : new Bounds(Vector3.zero, Vector3.one);
                bool first = true;
                var b = new Bounds();
                foreach (var o in p.side.objects)
                {
                    if (o.role != "mesh" || o.type != "MESH") continue;
                    bool shell = o.name == "Body" || o.name == "Glass" || o.name == "Hull" || o.name == "Roof" || o.name == "Wing" || o.name == "Trunk" || o.name == "Tailgate";
                    if (!shell)
                    {
                        string sock = o.Prop("socket");
                        shell = !string.IsNullOrEmpty(sock) && (sock.StartsWith("door") || sock == "hood");
                    }
                    if (!shell || (!string.IsNullOrEmpty(o.parent) && o.parent != "Body")) continue;
                    var rb = o.RootBounds;
                    if (first) { b = rb; first = false; } else b.Encapsulate(rb);
                }
                hd = first ? p.side.Size : b;
            }

            public Vector3 Point(Vector3 v)
            {
                var t = new Vector3(Ratio(v.x, vox.min.x, vox.size.x), Ratio(v.y, vox.min.y, vox.size.y), Ratio(v.z, vox.min.z, vox.size.z));
                return hd.min + Vector3.Scale(t, hd.size);
            }

            static float Ratio(float v, float min, float size) => size > 1e-4f ? (v - min) / size : 0.5f;

            public Vector3 Scale => new Vector3(Ratio1(hd.size.x, vox.size.x), Ratio1(hd.size.y, vox.size.y), Ratio1(hd.size.z, vox.size.z));
            static float Ratio1(float a, float b) => b > 1e-4f ? a / b : 1f;

            /// <summary>A box (centre, size) mapped corner to corner.</summary>
            public Bounds Box(Bounds b)
            {
                var a = Point(b.min); var c = Point(b.max);
                return new Bounds((a + c) * 0.5f, new Vector3(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y), Mathf.Abs(c.z - a.z)));
            }

            /// <summary>Walk-in interiors keep their layout, stretched like everything else.</summary>
            public void Interior(InteriorSpace s)
            {
                s.floorY = Point(new Vector3(0, s.floorY, 0)).y; s.ceilingY = Point(new Vector3(0, s.ceilingY, 0)).y;
                var a = Point(new Vector3(s.min.x, 0, s.min.y)); var b = Point(new Vector3(s.max.x, 0, s.max.y));
                s.min = new Vector2(a.x, a.z); s.max = new Vector2(b.x, b.z);
                s.seat = Point(s.seat); s.stand = Point(s.stand);
                if (s.doors != null) for (int i = 0; i < s.doors.Length; i++) { s.doors[i].inside = Point(s.doors[i].inside); s.doors[i].outside = Point(s.doors[i].outside); }
                if (s.obstacles != null) for (int i = 0; i < s.obstacles.Length; i++) s.obstacles[i] = Box(s.obstacles[i]);
                if (s.furnishings != null) for (int i = 0; i < s.furnishings.Length; i++) s.furnishings[i].position = Point(s.furnishings[i].position);
            }
        }

        /// <summary>Menu: rebuild the HD atlas materials (after a re-export, without a full build).</summary>
        [MenuItem("MadMax/HD/Refresh HD Materials")]
        public static void RefreshMaterials()
        {
            int n = 0;
            if (System.IO.Directory.Exists(HDSidecar.Root))
                foreach (var json in System.IO.Directory.GetFiles(HDSidecar.Root, "*.hd.json", System.IO.SearchOption.AllDirectories))
                {
                    var s = HDSidecar.Load(json.Replace('\\', '/'));
                    if (s == null) continue;
                    HDMaterials.Ensure(s); n++;
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HD] materials refreshed for {n} assets");
        }
    }
}
