using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MadMax.Rendering;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The HD vehicle roster (tools/blender/hd/PIPELINE.md, Editor/HD/HDVehicleBuilder): every registered vehicle,
    /// trailer and boat is spawned and checked for its HD model.</summary>
    public static class HDVehicleScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new HDVehicles();
        }
    }

    /// <summary>Spawns every vehicle prefab in turn on a level pad: HD body renderers present, mounted wheel radii equal
    /// the sidecar's (real-world) radii, the body colliders lie inside the HD model's box, then drives a few metres (land
    /// vehicles and aircraft taxiing), trailers rest on their wheels, boats keep their keel at the HD hull. Prefabs built
    /// on the voxel fallback (no HD export at build time) are listed, not failed.</summary>
    class HDVehicles : Scenario
    {
        public override string Id => "hd.vehicles";
        public override string[] Suites => new[] { "full", "vehicles", "hd" };
        public override float Timeout => 900f;

        [System.Serializable] class MiniSidecar { public MiniObject[] objects; }
        [System.Serializable] class MiniObject { public string name, type, parent; public MiniProp[] props; public float[] boundsMin, boundsMax; }
        [System.Serializable] class MiniProp { public string k, s; public float n; }

        /// <summary>Wheel radii the export declares (radius_m, else half the wheel / drum mesh height); null without the sidecar.</summary>
        static List<float> SidecarRadii(HDModel m)
        {
            if (!Application.isEditor || string.IsNullOrEmpty(m.asset) || string.IsNullOrEmpty(m.group)) return null;
            string path = Path.Combine(Application.dataPath, "MadMax/Models/HD", m.group, m.asset, m.asset + ".hd.json");
            if (!File.Exists(path)) return null;
            var s = JsonUtility.FromJson<MiniSidecar>(File.ReadAllText(path));
            if (s?.objects == null) return null;
            var r = new List<float>();
            foreach (var o in s.objects)
            {
                if (o.type != "MESH" || !string.IsNullOrEmpty(o.parent) || !(o.name.StartsWith("Wheel_") || o.name.StartsWith("Drum_"))) continue;
                float rad = 0f;
                if (o.props != null)
                    foreach (var p in o.props)
                        if (p.k == "radius_m") rad = p.n != 0f ? p.n : float.TryParse(p.s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f;
                if (rad <= 0f && o.boundsMin != null && o.boundsMax != null && o.boundsMin.Length >= 3) rad = Mathf.Max(o.boundsMax[1] - o.boundsMin[1], o.boundsMax[2] - o.boundsMin[2]) * 0.5f;
                if (rad > 0.05f) r.Add(rad);
            }
            return r;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var prefabs = (g.vehiclePrefabs ?? new GameObject[0]).Concat(g.trailerPrefabs ?? new GameObject[0]).Concat(g.boatPrefabs ?? new GameObject[0])
                .Where(p => p).GroupBy(p => p.name).Select(x => x.First()).ToList();
            if (!TestWorld.Pad(10f, 30f, out var pad, out var fwd)) { c.Block("no level test pad with a clear 30 m lane"); yield break; }
            int hd = 0, voxel = 0;
            foreach (var prefab in prefabs)
            {
                string name = prefab.name;
                var model = prefab.GetComponent<HDModel>();
                if (!model) { voxel++; c.Note(name + ": voxel model (no HD export when it was built)"); continue; }
                hd++;
                var v = g.SpawnFound(name, pad + Vector3.up * 1.5f, Quaternion.LookRotation(fwd));
                if (!c.Check(v, name + ": spawns")) continue;
                yield return null;

                // HD renderers
                var body = v.transform.Find("Body");
                c.Check(body && HDModel.IsHD(body.GetComponent<Renderer>()), name + ": the body is drawn with the HD material");
                int hdRenderers = v.GetComponentsInChildren<Renderer>().Count(HDModel.IsHD);
                c.Check(hdRenderers >= 3, $"{name}: {hdRenderers} HD renderers (body, glazing / dressing, parts)");

                // wheel radii against the sidecar
                var ch = v.GetComponent<VehicleChassis>();
                var declared = SidecarRadii(model);
                for (int i = 0; model.wheelSockets != null && i < model.wheelSockets.Length; i++)
                {
                    var s = ch ? ch.FindSocket(model.wheelSockets[i]) : null;
                    if (!s || !s.Current) { c.Check(false, $"{name}: a wheel on {model.wheelSockets[i]}"); continue; }
                    if (s.Current.partId.StartsWith("wheel_track")) continue;                    // crawler wheels sit inside the HD belts
                    float r = s.Current.radius;
                    bool ok = Mathf.Abs(r - model.wheelRadii[i]) < 0.002f && (declared == null || declared.Count == 0 || declared.Any(d => Mathf.Abs(d - r) < 0.01f));
                    c.Check(ok, $"{name}: {model.wheelSockets[i]} radius {r:0.000} m{(declared != null && declared.Count > 0 ? " (sidecar " + string.Join("/", declared.Distinct().Select(d => d.ToString("0.000"))) + ")" : "")}");
                }

                // body colliders inside the HD body box (parts and tools stick out by design: not checked)
                var box = model.bounds;
                box.Expand(0.2f);
                int outside = 0;
                foreach (var col in v.GetComponentsInChildren<Collider>())
                {
                    if (col.isTrigger || col.GetComponentInParent<VehiclePart>() || col.GetComponentInParent<MadMax.Building.Placeable>()) continue;
                    bool inside = true;
                    if (col is BoxCollider bc)
                        for (int k = 0; k < 8; k++)
                        {
                            var corner = bc.center + Vector3.Scale(bc.size * 0.5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                            inside &= box.Contains(v.transform.InverseTransformPoint(bc.transform.TransformPoint(corner)));
                        }
                    else inside = box.Contains(v.transform.InverseTransformPoint(col.bounds.center));
                    if (!inside) { outside++; c.Note($"{name}: collider {col.name} reaches outside the HD box {model.bounds}"); }
                }
                c.Check(outside == 0, $"{name}: body colliders lie within the HD model's box");

                // drive / rest
                bool boat = v.GetComponent<BoatModel>();
                yield return TestWorld.Place(c, v, pad, fwd, 1.2f);
                if (boat)
                {
                    var bm = v.GetComponent<BoatModel>();
                    c.Check(bm.keelY >= model.bounds.min.y - 0.3f && bm.keelY <= model.bounds.min.y + 0.6f, $"{name}: keel at the HD hull ({bm.keelY:0.00} m)");
                    c.Note(name + ": boat — afloat checks are in mobility.* (needs water)");
                }
                else if (!v.driveable)
                {
                    c.Check(v.GroundedWheels >= 2, $"{name}: rests on its wheels ({v.GroundedWheels} grounded)");
                }
                else
                {
                    g.Enter(v);
                    yield return new WaitForSeconds(0.3f);
                    if (c.Check(g.Current == v, "entered " + name))
                    {
                        yield return TestWorld.StartEngine(c, v);
                        var p0 = v.transform.position;
                        var fm = v.GetComponent<FlightModel>();
                        float t0 = Time.time;
                        v.handbrake = false; v.brakeInput = 0f;
                        while (Time.time - t0 < 4f)
                        {
                            if (fm) { fm.throttleAxis = 1f; fm.rollInput = 0f; fm.pitchInput = 0f; }
                            else v.throttleInput = 1f;
                            yield return null;
                        }
                        if (fm) fm.throttleAxis = -1f;
                        v.throttleInput = 0f; v.brakeInput = 1f;
                        float moved = Vector3.Distance(p0, v.transform.position);
                        c.Metric(name + "_moved_4s", moved, "m");
                        c.Check(moved > 2f, $"{name}: drives a few metres ({moved:0.0} m in 4 s)");
                        yield return new WaitForSeconds(0.8f);
                        if (fm) fm.throttleAxis = 0f;
                        v.brakeInput = 0f; v.handbrake = true;
                        g.Exit();
                        yield return new WaitForSeconds(0.3f);
                    }
                }
                Object.Destroy(v.gameObject);
                yield return null;
            }
            c.Metric("hd_vehicles", hd, "");
            c.Metric("voxel_fallback", voxel, "");
            c.Check(hd > 0, $"vehicles built from their HD models ({hd} HD, {voxel} voxel fallback)");
        }
    }
}
