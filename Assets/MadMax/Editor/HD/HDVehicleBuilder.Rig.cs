using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MadMax.Designs;
using MadMax.Rendering;
using MadMax.Vehicles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.EditorTools
{
    public static partial class HDVehicleBuilder
    {
        /// <summary>Per-vehicle change rows of this editor session (written to Logs/hd_vehicles.md after every vehicle).</summary>
        static readonly SortedDictionary<string, string> report = new SortedDictionary<string, string>();

        /// <summary>Swap the voxel visuals for the HD model on a vehicle root made by MadMaxBuilder.SaveVehicle (before it
        /// is saved): body + dressing, tracks, decks / ramps, legs, spinners and control surfaces, colliders, sockets with
        /// their HD parts, eye / hitch / coupler points, walk-in interior, boat and aircraft points.</summary>
        public static void ApplyBody(GameObject root, VehicleDesign d, Dictionary<string, GameObject> parts)
        {
            if (!plans.TryGetValue(d.name, out var p)) return;                           // SaveParts found no usable export: voxel
            var side = p.side;
            var body = root.transform.Find("Body");
            var bo = side.Get("Body") ?? side.Get("Hull");
            if (!body || bo == null) { Debug.LogError($"[HD] {d.name}: no Body in the vehicle or the HD export"); return; }
            var map = new BoxMap(d, p);
            var oldSockets = new Dictionary<string, Vector3>();
            var sockets = root.transform.Find("Sockets");
            if (sockets) foreach (var ms in sockets.GetComponentsInChildren<MountSocket>(true)) oldSockets[ms.name] = ms.transform.localPosition;
            var oldRadii = new Dictionary<string, float>();
            if (sockets) foreach (var ms in sockets.GetComponentsInChildren<MountSocket>(true)) if (ms.Current && ms.accepts == PartCategory.Wheel) oldRadii[ms.name] = ms.Current.radius;

            // ---- voxel visuals out, HD body in
            foreach (var n in new[] { "Glass", "Driver", "Roof", "Legs" })
            {
                var t = body.Find(n);
                if (!t || (n == "Legs" && side.Get("Legs") == null)) continue;            // voxel legs stay when the export has none
                Object.DestroyImmediate(t.gameObject);
            }
            foreach (var c in body.GetComponents<Collider>()) Object.DestroyImmediate(c);
            AddMesh(body.gameObject, side, bo, p.meshes, p.size);

            // ---- dressing on the body: glazing, lamps, interior, seats, roof, legs, hull, chains, spare wheels ...
            foreach (var o in side.objects)
            {
                if (o.role != "mesh" || o.type != "MESH" || o == bo || o.name == "Ground" || p.used.Contains(o.name)) continue;
                if (!string.IsNullOrEmpty(o.parent) && o.parent != "Body" && o.parent != bo.name) continue;
                if (IsFurniture(o) || o.Has("segment") || IsMovable(d, o) || IsSpinner(d, o.name) || IsSurface(o.name)) continue;
                var go = new GameObject(o.name);
                go.transform.SetParent(body, false);
                Place(go.transform, o, Vector3.zero);
                AddMesh(go, side, o, p.meshes, p.size);
                if (o.name == "Glass" && go.TryGetComponent<Renderer>(out var gr)) gr.shadowCastingMode = ShadowCastingMode.Off;
                if (o.name == "Legs") BoxAround(go, go.transform);                        // TowCoupling: Body/Legs, telescoping, with a collider
            }
            foreach (var o in p.tracks)                                                    // crawler belts (CrawlerTracks animates them)
            {
                var go = new GameObject(o.name.StartsWith("Track_") ? o.name : o.RootPosition.x < 0f ? "Track_L" : "Track_R");
                go.transform.SetParent(body, false);
                Place(go.transform, o, Vector3.zero);
                AddMesh(go, side, o, p.meshes, p.size);
            }

            // ---- moving sub-bodies (decks, ramps), spinners (props, rotors), control surfaces (wing, rudder)
            foreach (var o in side.objects)
            {
                if (o.role != "mesh" || o.type != "MESH" || !string.IsNullOrEmpty(o.parent)) continue;
                bool movable = IsMovable(d, o), spin = IsSpinner(d, o.name), surface = IsSurface(o.name);
                if (!movable && !spin && !surface) continue;
                var t = root.transform.Find(o.name);
                var go = t ? t.gameObject : new GameObject(o.name);
                go.transform.SetParent(root.transform, false);
                foreach (Transform c in go.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
                foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
                var designEuler = movable ? d.movable.Find(m => m.name == o.name).euler : Vector3.zero;
                Place(go.transform, o, Vector3.zero);
                if (movable && o.Has("rest_euler_x_unity") && Quaternion.Angle(o.RootRotation, Quaternion.identity) < 0.5f)
                    go.transform.localRotation = Quaternion.Euler(designEuler);         // authored flat: the design's rest angle (ramps)
                AddMesh(go, side, o, p.meshes, p.size);
                if (movable) BoxAround(go, go.transform);                                  // decks carry cars: a collider like the voxel ones
                else if (go.TryGetComponent<Renderer>(out var r) && spin) r.shadowCastingMode = ShadowCastingMode.On;
            }

            // ---- colliders
            Colliders(root, d, p, map, bo);

            // ---- sockets at the HD positions, HD parts on them; trunk / tailgate sockets
            if (sockets)
            {
                foreach (var (socket, o) in p.lids)
                {
                    if (!p.keys.ContainsKey(socket) || sockets.Find(socket)) continue;
                    var ms = new GameObject(socket).AddComponent<MountSocket>();
                    ms.transform.SetParent(sockets, false);
                    ms.accepts = PartCategory.Door; ms.maxSizeClass = 1;
                    ms.SetMirrored(false);
                }
                foreach (var ms in sockets.GetComponentsInChildren<MountSocket>(true))
                {
                    ms.transform.localPosition = SocketPosition(p, ms.name, map, oldSockets);
                    if (!p.keys.TryGetValue(ms.name, out var key) || !parts.TryGetValue(key, out var prefab)) continue;
                    var cur = ms.Current;
                    if (cur && cur.partId == key) continue;
                    if (cur) { ms.Detach(false); Object.DestroyImmediate(cur.gameObject); }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (!ms.Attach(inst.GetComponent<VehiclePart>()))
                    {
                        Debug.LogWarning($"[HD] {key} rejected by socket {d.name}/{ms.name}");
                        Object.DestroyImmediate(inst);
                    }
                }
            }

            // ---- points: driver eye over the HD seat, the rest mapped from the voxel design
            var eye = root.transform.Find("DriverEye");
            var seat = side.Get("Seat_Driver");
            var oldEye = eye ? eye.localPosition : Vector3.zero;
            if (eye) eye.localPosition = seat != null ? seat.RootPosition + new Vector3(0f, 0.9f, -0.12f) : map.Point((Vector3)d.eye * S);
            if (eye) FitCabin(root, eye, d.name);
            if (d.hitch.HasValue) Move(root, "Hitch", map.Point((Vector3)d.hitch.Value * S));
            if (d.coupler.HasValue) Move(root, "Coupler", map.Point((Vector3)d.coupler.Value * S));
            var pseat = side.Get("Seat_Passenger");
            if (d.passenger.HasValue) Move(root, "PassengerEye", pseat != null ? pseat.RootPosition + new Vector3(0f, 0.9f, -0.12f) : map.Point((Vector3)d.passenger.Value * S));
            if (root.TryGetComponent<InteriorSpace>(out var space)) { map.Interior(space); Furnish(space, side); }

            // ---- boats and aircraft: hull keel, prop, deck
            var prop = side.Get("Prop");
            if (root.TryGetComponent<BoatModel>(out var boat))
            {
                var hull = side.Get("Hull") ?? bo;
                boat.keelY = hull.RootBounds.min.y;
                boat.propAt = prop != null ? prop.RootPosition : map.Point(boat.propAt);
                boat.deckAt = map.Point(boat.deckAt);
            }
            if (root.TryGetComponent<FlightModel>(out var flight)) flight.propAt = prop != null ? prop.RootPosition : map.Point(flight.propAt);

            // ---- gearing: real (smaller / larger) tyres keep the design's road speed per rpm and tractive force
            float gear = 1f;
            if (root.TryGetComponent<VehicleDriver>(out var drv) && !p.crawler)
            {
                float rNew = 0f, rOld = 0f; int n = 0;
                foreach (var kv in p.radii)
                    if (oldRadii.TryGetValue(kv.Key, out var ro) && ro > 0.05f && kv.Value > 0.05f) { rNew += kv.Value; rOld += ro; n++; }
                if (n > 0 && Mathf.Abs(rNew / rOld - 1f) > 0.02f) { gear = rNew / rOld; drv.finalDrive *= gear; }
            }

            // ---- marker, mass, report
            var hd = (root.TryGetComponent<HDModel>(out var haveHDModel) ? haveHDModel : root.AddComponent<HDModel>());
            hd.asset = side.asset; hd.group = side.group;
            hd.bounds = side.Size;                                                         // the whole model (LOD0), prefab space
            var wn = new List<string>(); var wr = new List<float>();
            foreach (var kv in p.radii) { wn.Add(kv.Key); wr.Add(kv.Value); }
            hd.wheelSockets = wn.ToArray(); hd.wheelRadii = wr.ToArray();
            var chassis = root.GetComponent<VehicleChassis>();
            if (chassis && root.TryGetComponent<Rigidbody>(out var rb)) rb.mass = chassis.TotalMass;
            Report(d, p, map, root, oldSockets, oldRadii, oldEye, gear);
        }

        static void Move(GameObject root, string name, Vector3 at)
        {
            var t = root.transform.Find(name);
            if (t) t.localPosition = at;
        }

        /// <summary>Where a design socket sits on the HD model: fixed by its part (wheels, lids), at the HD object that names
        /// it, mirrored from its right-hand twin, else mapped from the voxel body box.</summary>
        static Vector3 SocketPosition(Plan p, string socket, BoxMap map, Dictionary<string, Vector3> old)
        {
            if (p.points.TryGetValue(socket, out var at)) return at;
            var ds = p.d.sockets.Find(x => x.name == socket);
            if (ds != null && ds.mirrored && socket.EndsWith("_L"))
            {
                string r = socket.Substring(0, socket.Length - 2) + "_R";
                if (p.d.sockets.Exists(x => x.name == r)) { var rp = SocketPosition(p, r, map, old); return new Vector3(-rp.x, rp.y, rp.z); }
            }
            if (p.sockets.TryGetValue(socket, out var o)) return o.RootPosition;
            if (ds != null) return map.Point((Vector3)ds.position * S);
            return old.TryGetValue(socket, out var prev) ? map.Point(prev) : Vector3.zero;
        }

        /// <summary>Colliders: the design's explicit boxes (walk-in shells, decks, bikes, boats) mapped onto the HD model;
        /// otherwise the HD lower body up to the window line plus the glasshouse, kept clear of the ground under the wheels.</summary>
        static void Colliders(GameObject root, VehicleDesign d, Plan p, BoxMap map, HDObject bo)
        {
            var cols = root.transform.Find("Colliders");
            if (d.colliders.Count > 0 && cols)
            {
                foreach (var bc in cols.GetComponentsInChildren<BoxCollider>(true))
                {
                    var b = map.Box(new Bounds(bc.center, bc.size));
                    bc.center = b.center; bc.size = Vector3.Max(b.size, Vector3.one * 0.05f);
                }
                return;
            }
            if (cols) Object.DestroyImmediate(cols.gameObject);
            cols = new GameObject("Colliders").transform;
            cols.SetParent(root.transform, false);
            var bb = bo.RootBounds;
            var hull = p.side.Get("Hull");
            if (hull != null && hull != bo) bb.Encapsulate(hull.RootBounds);
            float floor = float.MaxValue;                                                   // lowest tyre bottom
            foreach (var kv in p.radii) if (p.points.TryGetValue(kv.Key, out var w)) floor = Mathf.Min(floor, w.y - kv.Value);
            float bottom = floor < float.MaxValue ? Mathf.Max(bb.min.y, floor + 0.12f) : bb.min.y;
            var glass = p.side.Get("Glass");
            float belt = glass != null ? glass.RootBounds.min.y : bb.min.y + bb.size.y * 0.55f;
            if (glass == null || belt < bottom + 0.2f || belt > bb.max.y - 0.1f)
            {
                AddBox(cols, "Collider", new Vector3(bb.min.x, bottom, bb.min.z), bb.max);
                return;
            }
            AddBox(cols, "Collider", new Vector3(bb.min.x, bottom, bb.min.z), new Vector3(bb.max.x, belt, bb.max.z));
            var gb = glass.RootBounds;
            AddBox(cols, "Collider", new Vector3(gb.min.x - 0.03f, belt, gb.min.z - 0.03f), new Vector3(gb.max.x + 0.03f, bb.max.y, gb.max.z + 0.03f));
        }

        static void AddBox(Transform parent, string name, Vector3 min, Vector3 max)
        {
            var c = new GameObject(name).AddComponent<BoxCollider>();
            c.transform.SetParent(parent, false);
            c.center = (min + max) * 0.5f;
            c.size = Vector3.Max(max - min, Vector3.one * 0.05f);
        }

        /// <summary>Default furniture of a walk-in interior at the HD export's furniture objects (Furn_&lt;piece&gt;_&lt;n&gt;, origin
        /// = floor mount point), matched by piece id in order; pieces the export does not show keep their mapped spot.</summary>
        static void Furnish(InteriorSpace s, HDSidecar side)
        {
            if (s.furnishings == null) return;
            var spots = new Dictionary<string, List<HDObject>>();
            foreach (var o in side.objects)
            {
                if (o.role != "mesh" || !IsFurniture(o)) continue;
                string id = o.Prop("piece");
                if (string.IsNullOrEmpty(id)) { var n = o.name.Substring(5); int u = n.LastIndexOf('_'); id = u > 0 ? n.Substring(0, u) : n; }
                if (!spots.TryGetValue(id, out var l)) spots[id] = l = new List<HDObject>();
                l.Add(o);
            }
            foreach (var l in spots.Values) l.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var taken = new Dictionary<string, int>();
            for (int i = 0; i < s.furnishings.Length; i++)
            {
                var f = s.furnishings[i];
                if (!spots.TryGetValue(f.id, out var l)) continue;
                taken.TryGetValue(f.id, out int k);
                if (k >= l.Count) continue;
                taken[f.id] = k + 1;
                f.position = l[k].RootPosition;
                s.furnishings[i] = f;
            }
        }

        /// <summary>One row per vehicle: body size and wheel radius voxel → HD, sockets that moved (cm), eye.</summary>
        /// <summary>First person in the HD cabin: the eye stays 18 cm under the outer skin of the roof above it (seat + 0.9 m
        /// put it inside the roof of tall cabs; at most 40 cm lower; no roof or a glass roof: unchanged). The gauge cluster moves to 2 cm in front
        /// of the HD dashboard when the ray towards it meets a front face 15-60 cm out; past the glass or nothing near, it
        /// keeps its default spot. Rays against the body's own triangles (root space; parts, seats, the wheel and the
        /// baked driver ignored).</summary>
        static void FitCabin(GameObject root, Transform eye, string name)
        {
            var tris = new List<Vector3>(); var glass = new List<bool>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.name == "Driver" || mf.name == "Dashboard" || mf.name.Contains("Steer") || mf.name.StartsWith("Seat")) continue;
                bool skip = false;
                for (var x = mf.transform; x && x != root.transform; x = x.parent) if (x.name == "Sockets" || x.name == "Driver") { skip = true; break; }
                if (skip) continue;
                bool isGlass = mf.name.Contains("Glass");
                var m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var vs = mf.sharedMesh.vertices; var ts = mf.sharedMesh.triangles;
                for (int i = 0; i < ts.Length; i++) tris.Add(m.MultiplyPoint3x4(vs[ts[i]]));
                for (int i = 0; i < ts.Length / 3; i++) glass.Add(isGlass);
            }
            if (tris.Count == 0) return;
            // inside the glasshouse: some HD seats sit behind the cab's back wall (MonsterTruck), so the eye goes where the
            // glass is (20 cm in from the rear window, 30 cm behind the windscreen, 10 cm under the top of the glass)
            var e = eye.localPosition;
            var gb = new Bounds(); bool hasGlass = false;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.name != "Glass") continue;
                var b = mf.sharedMesh.bounds;
                var m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
                    if (!hasGlass) { gb = new Bounds(p, Vector3.zero); hasGlass = true; } else gb.Encapsulate(p);
                }
            }
            if (hasGlass && gb.size.z > 0.6f)
            {
                var was = e;
                e.z = Mathf.Clamp(e.z, gb.min.z + 0.2f, Mathf.Max(gb.min.z + 0.2f, gb.max.z - 0.3f));
                e.y = Mathf.Min(e.y, gb.max.y - 0.1f);
                if ((e - was).magnitude > 0.02f) { eye.localPosition = e; Debug.Log($"[HD] {name}: driver eye moved {(e - was).magnitude * 100f:0} cm into the glasshouse"); }
            }
            // the roof's outer skin straight above the eye (a ray down from over the cab): the eye stays 18 cm under it
            var top = new Vector3(e.x, e.y + 3f, e.z);
            float down = Cast(tris, glass, top, Vector3.down, 6f, out _, out bool roofGlass);
            float roofY = top.y - down;
            if (down < 6f && !roofGlass && roofY > e.y - 0.3f && e.y > roofY - 0.18f)       // lower hits are the seat or floor of an open cab
            {
                float y = Mathf.Max(roofY - 0.18f, e.y - 0.4f);
                Debug.Log($"[HD] {name}: driver eye lowered {(e.y - y) * 100f:0} cm under the roof (roof at {roofY:0.00} m)");
                e.y = y; eye.localPosition = e;
            }
            var dash = root.GetComponent<VehicleDashboard>();
            if (!dash) return;
            var dir = new Vector3(0f, dash.offsetFromEye.y, dash.offsetFromEye.z).normalized;
            float hit = Cast(tris, glass, eye.localPosition, dir, 0.6f, out bool backHit, out bool glassHit);
            if (hit >= 0.15f && hit < 0.6f && !backHit && !glassHit)
            {
                dash.offsetFromEye = dir * (hit - 0.02f);
                Debug.Log($"[HD] {name}: gauge cluster {hit - 0.02f:0.00} m from the eye, on the dashboard");
            }
        }

        /// <summary>Distance along <paramref name="dir"/> to the nearest triangle (Möller–Trumbore), or <paramref name="max"/>;
        /// <paramref name="back"/> = the ray met its back face (leaving a solid), <paramref name="isGlass"/> = a glazing triangle.</summary>
        static float Cast(List<Vector3> tris, List<bool> glass, Vector3 o, Vector3 dir, float max, out bool back, out bool isGlass)
        {
            float best = max; back = false; isGlass = false;
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = tris[i], e1 = tris[i + 1] - a, e2 = tris[i + 2] - a;
                var p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1f / det;
                var tv = o - a;
                float u = Vector3.Dot(tv, p) * inv;
                if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(tv, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t > 0.005f && t < best)
                {
                    best = t;
                    back = Vector3.Dot(Vector3.Cross(e1, e2), dir) > 0f;      // Unity winding: clockwise front, normal = e1 x e2 points out
                    isGlass = glass[i / 3];
                }
            }
            return best;
        }

        static void Report(VehicleDesign d, Plan p, BoxMap map, GameObject root, Dictionary<string, Vector3> oldSockets, Dictionary<string, float> oldRadii, Vector3 oldEye, float gear)
        {
            string V(Vector3 v) => $"{v.x:0.00}×{v.y:0.00}×{v.z:0.00}";
            var moved = new List<string>();
            var sockets = root.transform.Find("Sockets");
            if (sockets)
                foreach (var ms in sockets.GetComponentsInChildren<MountSocket>(true))
                {
                    if (ms.name.EndsWith("_L")) continue;
                    if (!oldSockets.TryGetValue(ms.name, out var was)) { moved.Add($"+{ms.name} ({ms.transform.localPosition.x:0.00},{ms.transform.localPosition.y:0.00},{ms.transform.localPosition.z:0.00})"); continue; }
                    var now = ms.transform.localPosition;
                    if ((now - was).magnitude > 0.05f) moved.Add($"{ms.name} {(now - was).magnitude * 100f:0}cm");
                }
            string radii = string.Join(" ", p.radii.Where(kv => !kv.Key.EndsWith("_L")).Select(kv => $"{Strip(kv.Key)} {(oldRadii.TryGetValue(kv.Key, out var o) ? o.ToString("0.00") : "-")}→{kv.Value:0.00}"));
            var eye = root.transform.Find("DriverEye");
            string row = $"| {d.name} | {V(map.vox.size)} | {V(map.hd.size)} | {radii} | {(gear != 1f ? "×" + gear.ToString("0.00") : "-")} | {(eye ? $"{(eye.localPosition - oldEye).magnitude * 100f:0} cm" : "-")} | {p.keys.Count} | {string.Join(", ", moved)} |";
            report[d.name] = row;
            Debug.Log($"[HD] {d.name}: body {V(map.vox.size)} → {V(map.hd.size)} m, wheels {radii}{(gear != 1f ? $", final drive ×{gear:0.00}" : "")}, {p.keys.Count} HD parts, sockets moved: {string.Join(", ", moved)}");
            try
            {
                Directory.CreateDirectory("Logs");
                var sb = new StringBuilder();
                sb.AppendLine("# HD vehicles: changes against the voxel designs (written by HDVehicleBuilder)");
                sb.AppendLine();
                sb.AppendLine("| vehicle | voxel body W×H×L (m) | HD body W×H×L (m) | wheel radius voxel→HD (m) | final drive | eye moved | HD parts | sockets moved > 5 cm / added |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|");
                foreach (var r in report.Values) sb.AppendLine(r);
                File.WriteAllText("Logs/hd_vehicles.md", sb.ToString());
            }
            catch (IOException) { }
        }
    }
}
