using System;
using System.Collections.Generic;
using System.IO;
using MadMax.Designs;
using MadMax.Rendering;
using MadMax.Vehicles;
using UnityEditor;
using UnityEngine;

namespace MadMax.EditorTools
{
    public static partial class HDVehicleBuilder
    {
        // =========================================================================================== generic parts
        /// <summary>Generic parts (PartLibrary keys) from their HD models, saved under the part key so every vehicle, loot
        /// pile and garage recipe gets them: the parts pack (<c>parts_all</c>, one asset per key, origin = mount point), else
        /// the object a vehicle export carries for it (machine tools and implements, origin = the tool socket, hinged
        /// segments as children named like <see cref="PartDesign.Segment"/>). Parts already made HD by another pass are
        /// kept. Call after the voxel parts are saved, before the vehicles.</summary>
        public static void SaveGenericParts(Dictionary<string, GameObject> parts, Dictionary<string, PartDesign> designs, string partDir)
        {
            designOf = k => k != null && designs.TryGetValue(k, out var x) ? x : null;
            plans.Clear();
            if (ForceVoxel) { Debug.Log("[HD] Force Voxel Vehicles is on: vehicles and parts build from voxels"); return; }
            var embedded = new Dictionary<string, (HDSidecar side, HDObject o)>();
            if (Directory.Exists(HDSidecar.Root))
                foreach (var json in Directory.GetFiles(HDSidecar.Root, "*.hd.json", SearchOption.AllDirectories))
                {
                    var s = HDSidecar.Load(json.Replace('\\', '/'));
                    if (s == null || s.kind != "vehicle" || s.objects == null) continue;
                    foreach (var o in s.objects)
                    {
                        if (o.role != "mesh" || !string.IsNullOrEmpty(o.parent)) continue;
                        string k = o.Prop("part");
                        if (k == null || !designs.TryGetValue(k, out var pd) || embedded.ContainsKey(k)) continue;
                        if (pd.category == PartCategory.Wheel || pd.category == PartCategory.Door || pd.category == PartCategory.Hood
                            || pd.category == PartCategory.FrontBumper || pd.category == PartCategory.RearBumper) continue;   // per-vehicle shapes
                        embedded[k] = (s, o);
                    }
                }
            int fromPack = 0, fromVehicle = 0;
            foreach (var kv in designs)
            {
                string key = kv.Key;
                if (parts.TryGetValue(key, out var cur) && cur && cur.GetComponent<HDModel>()) continue;
                GameObject go = null;
                var asset = HDSidecar.Find(key);
                if (asset != null && asset.kind == "part" && asset.objects != null && AssetDatabase.LoadAssetAtPath<GameObject>(asset.ModelPath))
                {
                    var tops = new List<HDObject>();
                    foreach (var o in asset.objects) if (o.role == "mesh" && string.IsNullOrEmpty(o.parent)) tops.Add(o);
                    go = BuildGeneric(asset, tops, key, kv.Value, Vector3.zero);
                    if (go) fromPack++;
                }
                else if (embedded.TryGetValue(key, out var e) && AssetDatabase.LoadAssetAtPath<GameObject>(e.side.ModelPath))
                {
                    go = BuildGeneric(e.side, new List<HDObject> { e.o }, key, kv.Value, e.o.RootPosition);
                    if (go) fromVehicle++;
                }
                if (!go) continue;
                parts[key] = PrefabUtility.SaveAsPrefabAsset(go, $"{partDir}/{key}.prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log($"[HD] generic parts: {fromPack} from the parts pack, {fromVehicle} from vehicle exports (machine tools, implements)");
        }

        /// <summary>A part prefab from HD objects (tops at <paramref name="origin"/> = the mount point; children nested by
        /// their logical parent, named by their <c>segment</c> prop).</summary>
        static GameObject BuildGeneric(HDSidecar side, List<HDObject> tops, string key, PartDesign pd, Vector3 origin)
        {
            if (tops.Count == 0) return null;
            HDMaterials.Ensure(side);
            var meshes = Meshes(side);
            float size = 0.5f;
            foreach (var t in tops) { var b = t.RootBounds; size = Mathf.Max(size, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z))); }
            var root = new GameObject(key);
            foreach (var t in tops)
            {
                bool onRoot = tops.Count == 1 && (t.RootPosition - origin).sqrMagnitude < 1e-8f && Quaternion.Angle(t.RootRotation, Quaternion.identity) < 0.01f;
                var host = root;
                if (!onRoot)
                {
                    host = new GameObject(SegmentName(t));
                    host.transform.SetParent(root.transform, false);
                    Place(host.transform, t, origin);
                }
                AddTree(side, host, t, meshes, size);
            }
            if (!root.GetComponent<Collider>() && root.GetComponentsInChildren<Collider>().Length == 0) BoxAround(root, root.transform);
            Stats(root, key, pd, pd.radius);
            if (pd.category == PartCategory.Tool)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = MadMax.World.Layers.MachineTool;
            return root;
        }

        static string SegmentName(HDObject o) { var s = o.Prop("segment"); return string.IsNullOrEmpty(s) ? o.name : s; }

        /// <summary>Mesh (with LODs, glass, lamps) and a box collider on <paramref name="host"/>, then every whole child object
        /// (hinged segments) as a child transform at its pivot.</summary>
        static void AddTree(HDSidecar side, GameObject host, HDObject o, Dictionary<string, Mesh> meshes, float size)
        {
            if (o.type == "MESH")
            {
                AddMesh(host, side, o, meshes, size);
                if (host.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh && !host.GetComponent<Collider>())
                {
                    var c = host.AddComponent<BoxCollider>();
                    c.center = mf.sharedMesh.bounds.center; c.size = Vector3.Max(mf.sharedMesh.bounds.size, Vector3.one * 0.04f);
                }
            }
            foreach (var c in side.Children(o.name))
            {
                if (c.role != "mesh" || IsFurniture(c)) continue;
                var go = new GameObject(SegmentName(c));
                go.transform.SetParent(host.transform, false);
                go.transform.localPosition = c.LocalPosition; go.transform.localRotation = c.LocalRotation;
                var sc = c.LocalScale; go.transform.localScale = new Vector3(Mathf.Abs(sc.x), Mathf.Abs(sc.y), Mathf.Abs(sc.z));
                AddTree(side, go, c, meshes, size);
            }
        }

        /// <summary>VehiclePart (+ engine / wheel stats) like MadMaxBuilder.SavePart, and the HDModel marker.</summary>
        static void Stats(GameObject root, string key, PartDesign pd, float radius, PartCategory? cat = null, HDSidecar side = null)
        {
            var part = root.AddComponent<VehiclePart>();
            part.partId = key;
            part.category = cat ?? pd.category;
            part.sizeClass = pd != null ? pd.sizeClass : 1;
            part.mass = pd != null ? pd.mass : part.category == PartCategory.Wheel ? 20f : 12f;
            part.radius = radius;
            if (pd != null && part.category == PartCategory.Engine)
            {
                var e = root.AddComponent<EngineStats>();
                e.maxTorque = pd.torque; e.maxRpm = pd.maxRpm; e.peakAt = pd.peakAt;
            }
            if (part.category == PartCategory.Wheel)
            {
                var w = root.AddComponent<WheelStats>();
                if (pd != null)
                {
                    w.grip = pd.grip; w.mudGrip = pd.mudGrip; w.width = pd.width;
                    if (pd.wetGrip > 0f) w.wetGrip = pd.wetGrip;
                    if (pd.rolling > 0f) w.rolling = pd.rolling;
                    if (pd.wearRate > 0f) w.wearRate = pd.wearRate;
                    if (pd.footprint > 0f) w.footprint = pd.footprint;
                }
            }
            var hd = root.AddComponent<HDModel>();
            if (side != null) { hd.asset = side.asset; hd.group = side.group; }
            else { hd.asset = key; hd.group = "parts"; }
        }

        // =========================================================================================== per-vehicle parts
        /// <summary>HD part prefabs that belong to one vehicle: wheels (<c>&lt;vehicle&gt;_wheel_front</c>: HD mesh and radius,
        /// the design tyre's stats; crawlers: an invisible <c>wheel_track_&lt;vehicle&gt;</c> inside the HD track belts; roller
        /// drums), panels cut from the body (doors and hood keep the voxel cut keys; trunk / tailgate get new sockets) and
        /// the body-shaped bumpers. Generic parts were made by <see cref="SaveGenericParts"/>. Call before the vehicle
        /// prefab is saved.</summary>
        public static void SaveParts(VehicleDesign d, Dictionary<string, GameObject> parts, Func<string, PartDesign> designs, string partDir)
        {
            designOf = designs ?? designOf;
            plans.Remove(d.name);
            var side = For(d);
            if (side == null) return;
            HDMaterials.Ensure(side);
            var p = MakePlan(d, side);
            p.meshes = Meshes(side);
            p.size = Size(side);
            plans[d.name] = p;
            var saved = new HashSet<string>();
            void Save(GameObject go, string key)
            {
                if (!go) return;
                parts[key] = PrefabUtility.SaveAsPrefabAsset(go, $"{partDir}/{key}.prefab");
                UnityEngine.Object.DestroyImmediate(go);
                saved.Add(key);
            }
            foreach (var ds in d.sockets)
            {
                if (ds.mirrored) continue;
                p.sockets.TryGetValue(ds.name, out var o);
                string key = null, leftKey = null;
                switch (ds.accepts)
                {
                    case PartCategory.Wheel:
                    {
                        if (ds.part == null) break;
                        var src = designOf(ds.part);
                        if (p.crawler)
                        {
                            key = "wheel_track_" + p.vlow;                              // VehicleDriver.Tracked: the id starts with wheel_track
                            var at = o != null ? WheelPoint(o, ds) : (Vector3)ds.position * S;
                            p.points[ds.name] = at;
                            p.radii[ds.name] = src != null ? src.radius : 0.344f;
                            if (!saved.Contains(key)) Save(BuildHiddenWheel(side, key, src, p.radii[ds.name]), key);
                        }
                        else if (p.drums.TryGetValue(ds.name, out var drum))
                        {
                            key = p.vlow + "_" + Strip(ds.name);
                            leftKey = key + "_hidden";
                            var db = drum.RootBounds;
                            float r = db.size.y * 0.5f;
                            var at = new Vector3(Mathf.Abs(ds.position.x * S), db.center.y, db.center.z);
                            p.points[ds.name] = at; p.radii[ds.name] = r;
                            Save(BuildWheel(p, drum, key, src, at, r, db.size.x), key);       // the drum spans the axle: it rolls with the right wheel
                            Save(BuildHiddenWheel(side, leftKey, src, r), leftKey);
                        }
                        else if (o != null && o.type == "MESH")
                        {
                            key = p.vlow + "_" + Strip(ds.name);
                            var at = WheelPoint(o, ds);
                            float r = WheelRadius(o, src);
                            p.points[ds.name] = at; p.radii[ds.name] = r;
                            Save(BuildWheel(p, o, key, src, at, r, o.PropFloat("width_m", o.RootBounds.size.x)), key);
                        }
                        break;
                    }
                    case PartCategory.Door:
                    case PartCategory.Hood:
                        if (o == null || o.type != "MESH") break;
                        key = ds.part ?? p.vlow + "_" + Strip(ds.name);
                        Save(BuildPanel(p, o, key, ds.accepts, d.parts.Find(x => x.key == key) ?? designOf(key), o.RootPosition), key);
                        break;
                    case PartCategory.FrontBumper:
                    case PartCategory.RearBumper:
                        if (o == null || o.type != "MESH") break;
                        key = p.vlow + "_" + Strip(ds.name);
                        Save(BuildPanel(p, o, key, ds.accepts, designOf(ds.part) ?? designOf(o.Prop("part")), o.RootPosition), key);
                        break;
                }
                if (key == null) continue;
                p.keys[ds.name] = key;
                if (ds.name.EndsWith("_R"))
                {
                    string l = ds.name.Substring(0, ds.name.Length - 2) + "_L";
                    if (d.sockets.Exists(s => s.name == l)) p.keys[l] = leftKey ?? key;
                }
            }
            foreach (var (socket, o) in p.lids)
            {
                if (o.type != "MESH") continue;
                string key = p.vlow + "_" + socket;
                var at = o.RootBounds.center;
                p.points[socket] = at;
                Save(BuildPanel(p, o, key, PartCategory.Door, null, at), key);
                p.keys[socket] = key;
            }
        }

        /// <summary>Where a wheel socket sits: the inner tyre face on the axle (right-hand wheels), or the left face of a
        /// centre-line wheel (bikes, nose wheels), as the voxel sockets do.</summary>
        static Vector3 WheelPoint(HDObject o, SocketDesign ds)
        {
            if (o.type != "MESH") { var e = o.RootPosition; return new Vector3(Mathf.Abs(e.x), e.y, e.z); }
            var b = o.RootBounds;
            bool centre = b.min.x < -0.005f && b.max.x > 0.005f && !ds.name.EndsWith("_R");
            float x = centre ? b.min.x : InnerX(o);
            return new Vector3(x, b.center.y, b.center.z);
        }

        /// <summary>|x| of the inner tyre face (the wheel socket) for a right-hand wheel object.</summary>
        static float InnerX(HDObject o)
        {
            if (o.Has("socket_inner_x_vox")) return Mathf.Abs(o.PropFloat("socket_inner_x_vox", 0f)) * S;
            var b = o.RootBounds;
            return Mathf.Min(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
        }

        static float WheelRadius(HDObject o, PartDesign src)
        {
            float r = o.PropFloat("radius_m", 0f);
            if (r > 0.05f) return r;
            if (o.type == "MESH") { var b = o.RootBounds; return Mathf.Max(b.size.y, b.size.z) * 0.5f; }
            return src != null ? src.radius : 0.3f;
        }

        /// <summary>A wheel part: pivot = the socket point (inner face on the axle), the HD mesh in a child "Wheel".</summary>
        static GameObject BuildWheel(Plan p, HDObject o, string key, PartDesign src, Vector3 at, float radius, float width)
        {
            var root = new GameObject(key);
            var host = new GameObject("Wheel");
            host.transform.SetParent(root.transform, false);
            Place(host.transform, o, at);
            AddMesh(host, p.side, o, p.meshes, p.size);
            BoxAround(root, host.transform);
            Stats(root, key, src, radius, PartCategory.Wheel, p.side);
            if (root.TryGetComponent<WheelStats>(out var w) && width > 0.02f) w.width = width;
            return root;
        }

        /// <summary>A wheel with no mesh of its own (crawler wheels inside the HD track belts, the far end of a roller drum).</summary>
        static GameObject BuildHiddenWheel(HDSidecar side, string key, PartDesign src, float radius)
        {
            var root = new GameObject(key);
            var c = root.AddComponent<BoxCollider>();
            float w = src != null && src.width > 0f ? src.width : 0.3f;
            c.center = new Vector3(w * 0.5f, 0f, 0f); c.size = new Vector3(w, radius * 2f, radius * 2f);
            Stats(root, key, src, radius, PartCategory.Wheel, side);
            return root;
        }

        /// <summary>A panel / bumper part: pivot at <paramref name="at"/> (the hinge for doors and hood), the HD mesh on the
        /// root when it shares the pivot, else in a child.</summary>
        static GameObject BuildPanel(Plan p, HDObject o, string key, PartCategory cat, PartDesign src, Vector3 at)
        {
            var root = new GameObject(key);
            var host = root;
            if ((o.RootPosition - at).sqrMagnitude > 1e-8f || Quaternion.Angle(o.RootRotation, Quaternion.identity) > 0.01f)
            {
                host = new GameObject("Panel");
                host.transform.SetParent(root.transform, false);
                Place(host.transform, o, at);
            }
            AddMesh(host, p.side, o, p.meshes, p.size);
            BoxAround(root, host.transform);
            Stats(root, key, src, src != null ? src.radius : 0f, cat, p.side);
            if (src == null) { var vp = root.GetComponent<VehiclePart>(); vp.mass = cat == PartCategory.Door ? 14f : 15f; vp.sizeClass = 1; }
            return root;
        }
    }
}
