using System.Collections;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Shared fixture helpers for scenarios: find fleet vehicles, flat clear test pads and slopes, place and
    /// start vehicles. Every placement is disclosed through <see cref="ScenarioContext.Fixture"/>.</summary>
    public static class TestWorld
    {
        static readonly Collider[] hits = new Collider[64];

        public static VehicleDriver Vehicle(string prefabName)
        {
            foreach (var d in Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None))
                if (d.name == prefabName + "(Clone)" || d.name == prefabName) return d;
            return null;
        }

        /// <summary>A dry, level (&lt; 3°) spot clear of anything but terrain within <paramref name="radius"/>, near the start.</summary>
        public static bool Pad(float radius, out Vector3 pad) => Pad(radius, 0f, out pad, out _);

        /// <summary>A level pad clear within <paramref name="radius"/> and along a lane of <paramref name="lane"/> m in
        /// <paramref name="dir"/>. The start road through the yard comes first (long, straight, kept clear of the parked
        /// fleet); then spots near the player, where the streamed terrain has its props and colliders.</summary>
        public static bool Pad(float radius, float lane, out Vector3 pad, out Vector3 dir)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            g.World.Yard(out var origin, out var along, out _);
            var focus = g.Player ? g.Player.transform.position : origin;
            if (lane > 0f)                                                                  // driving tests: the road (no digging there)
            foreach (float sign in new[] { 1f, -1f })
                for (float k = 0f; k < 40f; k += 6f)
                {
                    var p = origin + along * (sign * k);
                    if (Usable(p, radius, lane, along * sign)) { pad = new Vector3(p.x, t.Height(p.x, p.z), p.z); dir = along * sign; return true; }
                }
            for (int k = 0; k < 300; k++)
            {
                float a = k * 2.39996f, r = 12f + k * 0.25f;
                var p = focus + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                foreach (var d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                    if (Usable(p, radius, lane, d)) { pad = new Vector3(p.x, t.Height(p.x, p.z), p.z); dir = d; return true; }
            }
            pad = dir = default; return false;
        }

        static bool Usable(Vector3 p, float radius, float lane, Vector3 dir)
        {
            if (!Level(p, radius, 3f) || !Clear(p, radius)) return false;
            for (float d = radius; d < lane; d += 6f) if (!Clear(p + dir * d, 3f) || !Level(p + dir * d, 3f, 8f)) return false;
            return true;
        }

        /// <summary>A slope of <paramref name="min"/>..<paramref name="max"/> degrees, even over 8 m, dry and clear.</summary>
        public static bool Slope(float min, float max, out Vector3 at, out Vector3 uphill)
        {
            var t = DeformableTerrain.Instance;
            var g = WastelandGame.Instance;
            g.World.Yard(out var origin, out _, out _);
            for (int k = 0; k < 900; k++)
            {
                float a = k * 2.39996f, r = 30f + k * 0.6f;
                var p = origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                var n = t.Normal(p.x, p.z);
                float ang = Vector3.Angle(n, Vector3.up);
                if (ang < min || ang > max) continue;
                bool even = true;
                for (int i = 0; i < 6 && even; i++)
                {
                    var q = p + new Vector3(Mathf.Cos(i), 0f, Mathf.Sin(i)) * 4f;
                    even = Vector3.Angle(t.Normal(q.x, q.z), n) < 4f;
                }
                if (!even || t.WaterDepth(p.x, p.z) > 0f || !Clear(p, 5f)) continue;
                at = new Vector3(p.x, t.Height(p.x, p.z), p.z);
                uphill = new Vector3(-n.x, 0f, -n.z).normalized;
                return true;
            }
            at = uphill = default; return false;
        }

        static bool Level(Vector3 p, float radius, float maxDeg)
        {
            var t = DeformableTerrain.Instance;
            if (t.WaterDepth(p.x, p.z) > 0f || t.SurfaceAt(p.x, p.z).mud > 0.3f) return false;
            float h0 = t.Height(p.x, p.z);
            for (int i = 0; i < 8; i++)
            {
                var q = p + new Vector3(Mathf.Cos(i * 0.785f), 0f, Mathf.Sin(i * 0.785f)) * radius;
                if (t.WaterDepth(q.x, q.z) > 0f) return false;
                if (Mathf.Abs(t.Height(q.x, q.z) - h0) > Mathf.Tan(maxDeg * Mathf.Deg2Rad) * radius) return false;
                if (Vector3.Angle(t.Normal(q.x, q.z), Vector3.up) > maxDeg + 2f) return false;
            }
            return true;
        }

        static bool Clear(Vector3 p, float radius)
        {
            var t = DeformableTerrain.Instance;
            var c = new Vector3(p.x, t.Height(p.x, p.z) + 0.4f + radius, p.z);              // low rocks, fences and kerbs count too
            int n = Physics.OverlapCapsuleNonAlloc(c, c + Vector3.up * 3f, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!hits[i].transform.name.StartsWith("Chunk")) return false;
            return true;
        }

        /// <summary>Engine, fluids, faults and gearbox of a vehicle (failure evidence).</summary>
        public static string State(VehicleDriver v)
        {
            var s = v.GetComponent<VehicleSystems>();
            return $"{v.name}: started {(s ? s.Started : false)}, fuel {(s ? s.fuel : 0):0.0} L, faults '{(s ? s.FaultText() : "")}', rev {v.Reversing}, gear {v.Gear}, rpm {v.Rpm:0}, v {v.ForwardSpeed:0.00}, wheels on ground {v.GroundedWheels}, hb {v.handbrake}, sleeping {v.Body.IsSleeping()}, kinematic {v.Body.isKinematic}";
        }

        /// <summary>What a vehicle's colliders are touching (failure evidence).</summary>
        public static string Contacts(VehicleDriver v)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var col in v.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled || col.isTrigger) continue;
                var b = col.bounds;
                int n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.03f, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var h = hits[i];
                    if (h.transform.IsChildOf(v.transform) || Physics.GetIgnoreLayerCollision(col.gameObject.layer, h.gameObject.layer)) continue;
                    if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, h, h.transform.position, h.transform.rotation, out var dir, out var dist))
                        sb.Append(col.name).Append(" x ").Append(h.name).Append(" (").Append(h.transform.root.name).Append(") ").Append(dist.ToString("0.00")).Append(" m ").Append(dir.ToString("0.0")).Append("; ");
                }
            }
            return sb.Length == 0 ? "no contacts" : sb.ToString();
        }

        /// <summary>Put a vehicle on the ground at <paramref name="at"/> facing <paramref name="forward"/> (resting, handbrake on).</summary>
        public static IEnumerator Place(ScenarioContext c, VehicleDriver v, Vector3 at, Vector3 forward, float settle = 1.5f)
        {
            var t = DeformableTerrain.Instance;
            var n = t.Normal(at.x, at.z);
            v.Body.position = new Vector3(at.x, t.Height(at.x, at.z) + 0.6f, at.z);
            v.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, n), n);
            v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
            v.throttleInput = v.brakeInput = v.steerInput = 0f; v.handbrake = true;
            v.Body.WakeUp();
            c.Fixture($"placed {v.name.Replace("(Clone)", "")} at {at.x:0},{at.z:0}");
            yield return new WaitForSeconds(settle);
        }

        /// <summary>Hold the throttle with the handbrake on until the engine runs (cranking included).</summary>
        public static IEnumerator StartEngine(ScenarioContext c, VehicleDriver v, float within = 12f)
        {
            var sys = v.GetComponent<VehicleSystems>();
            if (!sys) yield break;
            float t0 = Time.time;
            while (!sys.Started && Time.time - t0 < within)
            {
                v.handbrake = true; v.throttleInput = 0.4f;
                yield return new WaitForSeconds(0.25f);
                if (!sys.Started && !sys.Cranking) { v.throttleInput = 0f; yield return new WaitForSeconds(0.3f); }   // turn the key again
            }
            v.throttleInput = 0f;
            c.Check(sys.Started, $"engine starts within {within:0} s ({sys.StartChance * 100f:0}% start chance)");
            c.Metric("engine_start", Time.time - t0, "s");
        }
    }
}
