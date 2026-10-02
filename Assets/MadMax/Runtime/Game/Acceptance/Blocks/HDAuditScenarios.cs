using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>What still renders in the voxel / pixel look with the HD pack on: every visible renderer near the player
    /// is classed by its shader (HDLit, PixelVoxel, pixel-canvas textures, other) and grouped by object, on foot at the
    /// start and seated in a car in first person (the dashboard), in full-resolution (vector) mode with captures.</summary>
    public static class HDAuditScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new HDAudit(); }

        static readonly Regex Digits = new Regex(@"[\d_\-\.:,]+$|\(Clone\)|\s+\d+");

        static string Group(Renderer r)
        {
            string root = Digits.Replace(r.transform.root.name, "").Trim();
            string self = Digits.Replace(r.name, "").Trim();
            return root == self ? root : root + "/" + self;
        }

        static string Kind(Renderer r)
        {
            var m = r.sharedMaterial;
            if (!m || !m.shader) return "none";
            string s = m.shader.name;
            if (s.Contains("HDLit")) return m.HasProperty("_VertexAlbedo") && m.GetFloat("_VertexAlbedo") > 0.5f && !(m.GetTexture("_BaseMap") is Texture2D t && t.width > 4) ? "hd-voxel" : "hd";
            if (s.Contains("PixelVoxel")) return "voxel";
            var tex = m.mainTexture;
            if (tex && tex.filterMode == FilterMode.Point && tex.width <= 256) return "pixel-tex " + tex.width + "x" + tex.height;
            if (r is ParticleSystemRenderer) return "particles " + s;
            if (r is LineRenderer || r is TrailRenderer) return "lines " + s;
            return "other " + s;
        }

        internal static void Audit(ScenarioContext c, string where, Vector3 at, float radius)
        {
            var count = new Dictionary<string, int>();
            var groups = new Dictionary<string, (int n, float area)>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                if ((r.bounds.center - at).sqrMagnitude > radius * radius) continue;
                string k = Kind(r);
                count.TryGetValue(k, out int n); count[k] = n + 1;
                if (k == "hd") continue;
                string key = k + " | " + Group(r);
                var s = r.bounds.size;
                groups.TryGetValue(key, out var g);
                groups[key] = (g.n + 1, g.area + s.x * s.z + s.x * s.y + s.y * s.z);
            }
            c.Note(where + ": renderers by look: " + string.Join(", ", count.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)));
            foreach (var kv in count) c.Metric(where + "_" + kv.Key.Split(' ')[0], kv.Value, "renderers");
            foreach (var kv in groups.OrderByDescending(kv => kv.Value.area).Take(45))
                c.Note($"  {where} not HD: {kv.Key}  x{kv.Value.n}  ~{kv.Value.area:0} m2");
        }
    }

    class HDAudit : Scenario
    {
        public override string Id => "hd.audit";
        public override string[] Suites => new[] { "full" };
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var s = GameSettings.Current;
            bool vec = s.vector;
            s.vector = true; s.Apply(g);
            c.Fixture("full-resolution (vector) rendering for the captures");
            var rig = g.cameraRig;
            var mode0 = rig ? rig.mode : ViewMode.Isometric;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.5f); }
            yield return new WaitForSeconds(4f);                                                   // streaming settles
            var at = g.Player.transform.position;
            HDAuditScenarios.Audit(c, "foot", at, 90f);
            c.Screenshot("hd_audit_iso");
            yield return null;
            if (rig) { rig.mode = ViewMode.ThirdPerson; yield return new WaitForSeconds(1f); c.Screenshot("hd_audit_third"); yield return null; }

            var cars = new List<VehicleDriver>();
            foreach (var want in new[] { "MonsterTruck", "Sedan", "Pickup", "Interceptor" })
                foreach (var v in g.Fleet) if (v && v.driveable && v.name.StartsWith(want) && cars.Count < 2 && !cars.Contains(v)) { cars.Add(v); break; }
            foreach (var car in cars)
            {
                string cn = car.name.Replace("(Clone)", "");
                g.Enter(car);
                yield return new WaitForSeconds(1f);
                if (rig) rig.mode = ViewMode.FirstPerson;
                yield return new WaitForSeconds(1.2f);
                HDAuditScenarios.Audit(c, "cabin_" + cn, car.transform.position, 6f);
                var dash = car.GetComponent<VehicleDashboard>();
                c.Note(cn + " dashboard: " + (dash ? (dash.IsHD ? "HD geometry" : "pixel canvas") + ", " + dash.offsetFromEye.magnitude.ToString("0.00") + " m from the eye" : "none"));
                c.Screenshot("hd_audit_cabin_" + cn);
                yield return null;
                g.Exit();
                yield return new WaitForSeconds(0.5f);
            }
            if (cars.Count == 0) c.Note("no car in the fleet: cabin skipped");
            if (rig) rig.mode = mode0;
            s.vector = vec; s.Apply(g);
            c.Check(true, "audit done");
        }
    }
}
