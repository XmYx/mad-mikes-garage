using System;
using System.Collections.Generic;
using System.Linq;
using MadMax.Designs;
using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.EditorTools
{
    /// <summary>Builds a vehicle prefab from its HD model (Models/HD/&lt;group&gt;/&lt;Name&gt;/, see tools/blender/hd/PIPELINE.md)
    /// on top of what <see cref="MadMaxBuilder"/> makes from the voxel design: the HD body, glazing, lamps, interior and
    /// seats replace the voxel meshes; doors, hood, bumpers and wheels become HD part prefabs; sockets move to the HD
    /// positions (real-world sizes); colliders come from the HD body. Everything else (driver, systems, damage, sockets
    /// without an HD object, interiors) keeps the design's data, mapped from the voxel body box onto the HD body box.</summary>
    public static class HDVehicleBuilder
    {
        /// <summary>Vehicles built from their HD model. Integrators add names as their exports land.</summary>
        public static readonly HashSet<string> Enabled = new HashSet<string> { "Sedan" };

        const float S = VoxelMesher.DefaultSize;
        static readonly Dictionary<string, Dictionary<string, string>> overrides = new Dictionary<string, Dictionary<string, string>>();

        public static HDSidecar For(VehicleDesign d)
        {
            if (d == null || !Enabled.Contains(d.name)) return null;
            var s = HDSidecar.Find(d.name);
            if (s == null) Debug.LogWarning($"[HD] {d.name}: no HD export in {HDSidecar.Root} (run tools/blender/hd/export/run_export.py {d.name}); voxel model kept");
            return s;
        }

        // =========================================================================================== parts
        /// <summary>HD part prefabs for the vehicle-specific panels (doors, hood: same keys as the voxel cuts) and new keys
        /// for its HD wheels and bumpers (<c>&lt;vehicle&gt;_wheel_front</c>, <c>&lt;vehicle&gt;_bumper_front</c>, stats copied from
        /// the design's default part). Call before the vehicle prefab is saved.</summary>
        public static void SaveParts(VehicleDesign d, Dictionary<string, GameObject> parts, Func<string, PartDesign> designOf, string partDir)
        {
            var side = For(d);
            if (side == null) return;
            HDMaterials.Ensure(side);
            var meshes = Meshes(side);
            var ov = new Dictionary<string, string>();
            overrides[d.name] = ov;
            float size = Size(side);
            foreach (var o in side.objects)
            {
                if (o.role != "mesh" || o.type != "MESH") continue;
                string hs = o.Prop("socket");
                if (string.IsNullOrEmpty(hs) || hs.EndsWith("_L") || o.Prop("mirrored") == "true") continue;   // left sides mirror the right part
                var ds = DesignSocket(d, hs);
                if (ds == null) { Debug.LogWarning($"[HD] {d.name}: HD object {o.name} names socket '{hs}' the design does not have"); continue; }
                string key;
                PartDesign src;
                switch (ds.accepts)
                {
                    case PartCategory.Door:
                    case PartCategory.Hood:
                        key = ds.part ?? d.name.ToLowerInvariant() + "_" + hs;
                        src = d.parts.Find(p => p.key == key);
                        break;
                    case PartCategory.Wheel:
                    case PartCategory.FrontBumper:
                    case PartCategory.RearBumper:
                        key = d.name.ToLowerInvariant() + "_" + hs;
                        src = ds.part != null ? designOf(ds.part) : null;
                        break;
                    default:
                        continue;                       // engines, radiators, tools: generic parts (HD versions come with the parts pack)
                }
                var go = BuildPart(side, o, meshes, key, ds.accepts, src, size);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{partDir}/{key}.prefab");
                UnityEngine.Object.DestroyImmediate(go);
                parts[key] = prefab;
                ov[ds.name] = key;
                if (ds.name.EndsWith("_R")) ov[ds.name.Substring(0, ds.name.Length - 2) + "_L"] = key;
            }
        }

        static SocketDesign DesignSocket(VehicleDesign d, string hdSocket) =>
            d.sockets.Find(s => s.name == hdSocket + "_R") ?? d.sockets.Find(s => s.name == hdSocket);

        static GameObject BuildPart(HDSidecar side, HDObject o, Dictionary<string, Mesh> meshes, string key, PartCategory cat, PartDesign src, float size)
        {
            var root = new GameObject(key);
            var host = root;
            if (cat == PartCategory.Wheel)
            {
                // pivot = the inner tyre face on the axle (socket convention); the mesh sits outward (+X, right-hand part)
                host = new GameObject("Wheel");
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3(Mathf.Abs(o.RootPosition.x) - InnerX(o), 0f, 0f);
            }
            AddMesh(host, side, o, meshes, size);
            if (cat != PartCategory.Wheel && host.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
            {
                var col = root.AddComponent<BoxCollider>();
                col.center = mf.sharedMesh.bounds.center; col.size = mf.sharedMesh.bounds.size;
            }
            var part = root.AddComponent<VehiclePart>();
            part.partId = key; part.category = cat;
            part.sizeClass = src != null ? src.sizeClass : 1;
            part.mass = src != null ? src.mass : cat == PartCategory.Wheel ? 20f : 12f;
            part.radius = cat == PartCategory.Wheel ? o.PropFloat("radius_m", src != null ? src.radius : 0.3f) : src != null ? src.radius : 0f;
            if (cat == PartCategory.Wheel)
            {
                var w = root.AddComponent<WheelStats>();
                if (src != null)
                {
                    w.grip = src.grip; w.mudGrip = src.mudGrip;
                    if (src.wetGrip > 0f) w.wetGrip = src.wetGrip;
                    if (src.rolling > 0f) w.rolling = src.rolling;
                    if (src.wearRate > 0f) w.wearRate = src.wearRate;
                    if (src.footprint > 0f) w.footprint = src.footprint;
                }
                w.width = o.PropFloat("width_m", src != null ? src.width : 0.24f);
            }
            var hd = root.AddComponent<HDModel>();
            hd.asset = side.asset; hd.group = side.group;
            return root;
        }

        /// <summary>|x| of the inner tyre face (the wheel socket) for a wheel object.</summary>
        static float InnerX(HDObject o)
        {
            if (o.Has("socket_inner_x_vox")) return Mathf.Abs(o.PropFloat("socket_inner_x_vox", 0f)) * S;
            var b = o.RootBounds;
            return Mathf.Min(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
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
                var lg = host.AddComponent<LODGroup>();
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
            var mf = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = HDMaterials.Slots(mat, mesh.subMeshCount);
            return mr;
        }

        // =========================================================================================== body
        /// <summary>Swap the voxel body for the HD one on a vehicle root made by MadMaxBuilder.SaveVehicle (before it is saved).</summary>
        public static void ApplyBody(GameObject root, VehicleDesign d, Dictionary<string, GameObject> parts)
        {
            var side = For(d);
            if (side == null) return;
            HDMaterials.Ensure(side);
            var meshes = Meshes(side);
            float size = Size(side);
            var body = root.transform.Find("Body");
            var bo = side.Get("Body");
            if (!body || bo == null) { Debug.LogError($"[HD] {d.name}: no Body in the vehicle or the HD export"); return; }
            var map = new BoxMap(d, side);

            // ---- voxel visuals out, HD body in
            foreach (var n in new[] { "Glass", "Driver", "Roof" })
            {
                var t = body.Find(n);
                if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            foreach (var c in body.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            AddMesh(body.gameObject, side, bo, meshes, size);
            // everything else without a socket rides on the body: glazing, lights, trunk lid, interior, seats
            foreach (var o in side.objects)
            {
                if (o.role != "mesh" || o.type != "MESH" || o.name == "Body" || !string.IsNullOrEmpty(o.Prop("socket"))) continue;
                if (!string.IsNullOrEmpty(o.parent) && o.parent != "Body") continue;
                var go = new GameObject(o.name);
                go.transform.SetParent(body, false);
                go.transform.localPosition = o.RootPosition;
                go.transform.localRotation = o.LocalRotation; go.transform.localScale = o.LocalScale;
                AddMesh(go, side, o, meshes, size);
                if (o.name == "Glass" && go.TryGetComponent<Renderer>(out var gr)) gr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // ---- colliders: the lower body up to the window line, the glasshouse above it
            var oldCols = root.transform.Find("Colliders");
            if (oldCols) UnityEngine.Object.DestroyImmediate(oldCols.gameObject);
            var cols = new GameObject("Colliders").transform;
            cols.SetParent(root.transform, false);
            var bb = bo.RootBounds;
            var glass = side.Get("Glass");
            float belt = glass != null ? glass.RootBounds.min.y : bb.min.y + bb.size.y * 0.55f;
            AddBox(cols, "Collider", new Vector3(bb.min.x, bb.min.y, bb.min.z), new Vector3(bb.max.x, belt, bb.max.z));
            if (glass != null)
            {
                var gb = glass.RootBounds;
                AddBox(cols, "Collider", new Vector3(gb.min.x - 0.03f, belt, gb.min.z - 0.03f), new Vector3(gb.max.x + 0.03f, bb.max.y, gb.max.z + 0.03f));
            }

            // ---- sockets at the HD positions, HD parts on them
            overrides.TryGetValue(d.name, out var ov);
            var sockets = root.transform.Find("Sockets");
            if (sockets)
            {
                foreach (var ms in sockets.GetComponentsInChildren<MountSocket>(true))
                {
                    var ds = d.sockets.Find(x => x.name == ms.name);
                    ms.transform.localPosition = SocketPosition(side, ms.name, ds, map);
                    if (ov == null || !ov.TryGetValue(ms.name, out var key) || !parts.TryGetValue(key, out var prefab)) continue;
                    var cur = ms.Current;
                    if (cur && cur.partId == key) continue;
                    if (cur) { ms.Detach(false); UnityEngine.Object.DestroyImmediate(cur.gameObject); }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (!ms.Attach(inst.GetComponent<VehiclePart>()))
                    {
                        Debug.LogWarning($"[HD] {key} rejected by socket {d.name}/{ms.name}");
                        UnityEngine.Object.DestroyImmediate(inst);
                    }
                }
            }

            // ---- points: driver eye over the HD seat, the rest mapped from the voxel design
            var eye = root.transform.Find("DriverEye");
            var seat = side.Get("Seat_Driver");
            if (eye) eye.localPosition = seat != null ? seat.RootPosition + new Vector3(0f, 0.9f, -0.12f) : map.Point((Vector3)d.eye * S);
            if (d.hitch.HasValue) Move(root, "Hitch", map.Point((Vector3)d.hitch.Value * S));
            if (d.coupler.HasValue) Move(root, "Coupler", map.Point((Vector3)d.coupler.Value * S));
            if (d.passenger.HasValue) Move(root, "PassengerEye", map.Point((Vector3)d.passenger.Value * S));
            if (root.TryGetComponent<InteriorSpace>(out var space)) map.Interior(space);

            var hd = root.GetComponent<HDModel>() ?? root.AddComponent<HDModel>();
            hd.asset = side.asset; hd.group = side.group;
            var chassis = root.GetComponent<VehicleChassis>();
            if (chassis && root.TryGetComponent<Rigidbody>(out var rb)) rb.mass = chassis.TotalMass;
            Debug.Log($"[HD] {d.name}: HD body, {(ov != null ? ov.Count : 0)} HD parts, {side.tris?[0] ?? 0} tris");
        }

        static void AddBox(Transform parent, string name, Vector3 min, Vector3 max)
        {
            var c = new GameObject(name).AddComponent<BoxCollider>();
            c.transform.SetParent(parent, false);
            c.center = (min + max) * 0.5f;
            c.size = Vector3.Max(max - min, Vector3.one * 0.05f);
        }

        static void Move(GameObject root, string name, Vector3 p)
        {
            var t = root.transform.Find(name);
            if (t) t.localPosition = p;
        }

        /// <summary>Where a design socket sits on the HD model: at the HD object that names it (wheels: the inner tyre face
        /// on the axle), else mapped from the voxel body box.</summary>
        static Vector3 SocketPosition(HDSidecar side, string socket, SocketDesign ds, BoxMap map)
        {
            string hd = socket.EndsWith("_R") ? socket.Substring(0, socket.Length - 2) : socket;
            var o = side.WithProp("socket", hd) ?? side.WithProp("socket", socket);
            if (socket.EndsWith("_R") && o != null && o.Prop("mirrored") == "true") o = null;
            if (o != null)
            {
                var p = o.RootPosition;
                if (ds != null && ds.accepts == PartCategory.Wheel) p.x = Mathf.Sign(p.x == 0f ? (ds.mirrored ? -1f : 1f) : p.x) * InnerX(o);
                return p;
            }
            return ds != null ? map.Point((Vector3)ds.position * S) : Vector3.zero;
        }

        /// <summary>Maps design (voxel) positions onto the HD model: the voxel body box (body + glass) stretched onto the HD
        /// body box (body, glass, panels), per axis.</summary>
        class BoxMap
        {
            readonly Bounds vox, hd;

            public BoxMap(VehicleDesign d, HDSidecar side)
            {
                bool any = false;
                Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
                foreach (var g in new[] { d.body, d.glass })
                {
                    if (g == null) continue;
                    foreach (var k in g.voxels.Keys) { mn = Vector3.Min(mn, k); mx = Vector3.Max(mx, k); any = true; }
                }
                vox = any ? new Bounds(((mn + mx) * 0.5f) * S, (mx - mn + Vector3.one) * S) : new Bounds(Vector3.zero, Vector3.one);
                bool first = true;
                var b = new Bounds();
                foreach (var o in side.objects)
                {
                    if (o.role != "mesh" || o.type != "MESH") continue;
                    var n = o.name;
                    if (n.StartsWith("Wheel") || n.StartsWith("Bumper") || n.StartsWith("Engine") || n.StartsWith("Seat") || n.StartsWith("Interior") || n.StartsWith("Lights")) continue;
                    var rb = o.RootBounds;
                    if (first) { b = rb; first = false; } else b.Encapsulate(rb);
                }
                hd = first ? side.Size : b;
            }

            public Vector3 Point(Vector3 p)
            {
                var t = new Vector3(Ratio(p.x, vox.min.x, vox.size.x), Ratio(p.y, vox.min.y, vox.size.y), Ratio(p.z, vox.min.z, vox.size.z));
                return hd.min + Vector3.Scale(t, hd.size);
            }

            static float Ratio(float v, float min, float size) => size > 1e-4f ? (v - min) / size : 0.5f;

            Vector3 Scale => new Vector3(Ratio1(hd.size.x, vox.size.x), Ratio1(hd.size.y, vox.size.y), Ratio1(hd.size.z, vox.size.z));
            static float Ratio1(float a, float b) => b > 1e-4f ? a / b : 1f;

            /// <summary>Walk-in interiors keep their layout, stretched like everything else.</summary>
            public void Interior(InteriorSpace s)
            {
                var k = Scale;
                s.floorY = Point(new Vector3(0, s.floorY, 0)).y; s.ceilingY = Point(new Vector3(0, s.ceilingY, 0)).y;
                var a = Point(new Vector3(s.min.x, 0, s.min.y)); var b = Point(new Vector3(s.max.x, 0, s.max.y));
                s.min = new Vector2(a.x, a.z); s.max = new Vector2(b.x, b.z);
                s.seat = Point(s.seat); s.stand = Point(s.stand);
                if (s.doors != null) for (int i = 0; i < s.doors.Length; i++) { s.doors[i].inside = Point(s.doors[i].inside); s.doors[i].outside = Point(s.doors[i].outside); }
                if (s.obstacles != null) for (int i = 0; i < s.obstacles.Length; i++) s.obstacles[i] = new Bounds(Point(s.obstacles[i].center), Vector3.Scale(s.obstacles[i].size, k));
                if (s.furnishings != null) for (int i = 0; i < s.furnishings.Length; i++) s.furnishings[i].position = Point(s.furnishings[i].position);
            }
        }

        /// <summary>Menu: rebuild only the HD-enabled vehicles' materials (after a re-export, without a full build).</summary>
        [MenuItem("MadMax/HD/Refresh HD Materials")]
        public static void RefreshMaterials()
        {
            int n = 0;
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
