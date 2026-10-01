using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Mobility suite (roadmap 26 Q3): parts, armour, paint, weapons, towing, winch, crane, transporters,
    /// two-wheelers, aircraft, boats, the submarine and terrain grip. Vehicles with their own control model (bikes,
    /// aircraft) get these instead of the generic <c>vehicle.drive.*</c> test.</summary>
    public static class MobilityScenarios
    {
        public static readonly string[] Suites = { "full", "mobility" };
        static readonly string[] BikeNames = { "DirtBike", "Chopper", "Bicycle", "SidecarOutfit" };
        static readonly string[] AircraftNames = { "Ultralight", "Gyrocopter" };

        public static IEnumerable<Scenario> All()
        {
            yield return new PartsSwap();
            yield return new ArmourPaint();
            yield return new WeaponsTest();
            yield return new TowingTanker();
            yield return new WinchPull();
            yield return new CraneLift();
            yield return new TransporterLoad();
            foreach (var n in Named<BikeBalance>(BikeNames)) yield return new BikeRide(n);
            foreach (var n in Named<FlightModel>(AircraftNames)) { yield return new AircraftGround(n); yield return new AircraftCircuit(n); }
            yield return new BoatTrip("Skiff");
            yield return new SubmarineDive();
            yield return new MudRuts();
            yield return new IceGrip();
            yield return new RiverFord();
        }

        /// <summary>Covered here, not by <c>vehicle.drive.*</c> (own control model).</summary>
        public static bool OwnSuite(string prefabName)
        {
            foreach (var n in Named<BikeBalance>(BikeNames)) if (n == prefabName) return true;
            foreach (var n in Named<FlightModel>(AircraftNames)) if (n == prefabName) return true;
            return false;
        }

        /// <summary>Registered land-fleet prefabs carrying <typeparamref name="T"/> (the fixed list without a game).</summary>
        static IEnumerable<string> Named<T>(string[] fallback) where T : Component
        {
            var g = WastelandGame.Instance;
            if (g == null || g.vehiclePrefabs == null) { foreach (var n in fallback) yield return n; yield break; }
            foreach (var p in g.vehiclePrefabs) if (p && p.GetComponent<T>()) yield return p.name;
        }
    }

    /// <summary>Shared fixtures of the mobility suite: frame-aware waits, placement at exact points (decks, water),
    /// streaming a far spot in, finding open water, airfields and fords near the start.</summary>
    public static class MobilityKit
    {
        static readonly Collider[] hits = new Collider[64];

        /// <summary>Wait until <paramref name="ok"/> holds or <paramref name="seconds"/> of game time pass.</summary>
        public static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        public static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
        public static Vector3 FlatDir(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        public static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        public static string N(Component c) => c ? c.name.Replace("(Clone)", "") : "none";

        /// <summary>Ensure a count of an item in the pack (disclosed).</summary>
        public static void Grant(ScenarioContext c, string item, int n)
        {
            var inv = c.Game.Inventory;
            int have = inv.GetItem(item);
            if (have >= n) return;
            inv.AddItem(item, n - have);
            c.Fixture($"granted {n - have} {item}");
        }

        public static void Grant(ScenarioContext c, ResourceType t, int n)
        {
            var inv = c.Game.Inventory;
            int have = inv.Get(t);
            if (have >= n) return;
            inv.Add(t, n - have);
            c.Fixture($"granted {n - have} {ResourceInfo.Name(t)}");
        }

        /// <summary>A wrench in the pack and in hand.</summary>
        public static void Wrench(ScenarioContext c)
        {
            var g = c.Game;
            Grant(c, ItemIds.Wrench, 1);
            if (!g.Player.Tool || g.Player.Tool.id != ItemIds.Wrench) { g.Player.Equip(ToolLibrary.Create(ItemIds.Wrench, g.propMaterial)); c.Fixture("the wrench in hand"); }
        }

        /// <summary>Put a vehicle at an exact point (a deck, the water) facing <paramref name="forward"/>.</summary>
        public static IEnumerator PlaceAt(ScenarioContext c, VehicleDriver v, Vector3 at, Vector3 forward, float settle, string where)
        {
            v.Body.isKinematic = false;
            v.Body.position = at;
            v.Body.rotation = Quaternion.LookRotation(FlatDir(forward), Vector3.up);
            v.transform.SetPositionAndRotation(at, v.Body.rotation);
            v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
            v.throttleInput = v.brakeInput = v.steerInput = 0f; v.handbrake = true;
            v.Body.WakeUp();
            c.Fixture($"placed {N(v)} {where} at {at.x:0},{at.z:0}");
            if (settle > 0f) yield return new WaitForSeconds(settle);
        }

        /// <summary>Walk the player (teleport) to a spot and wait for the terrain there to stream in (a collider under it).</summary>
        public static IEnumerator Stream(ScenarioContext c, Vector3 at, float seconds = 15f)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return null; }
            float h = t.Height(at.x, at.z);
            g.Player.Teleport(new Vector3(at.x, h + 0.3f, at.z), g.Player.transform.eulerAngles.y);
            c.Fixture($"player moved to {at.x:0},{at.z:0} ({Flat(at, Vector3.zero):0} m from the world origin)");
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                if (Physics.Raycast(new Vector3(at.x, h + 40f, at.z), Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.name.StartsWith("Chunk")) break;
                yield return null;
            }
            c.Metric("stream_in", Time.time - t0, "s");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>No colliders but the terrain within <paramref name="radius"/> of a ground point (low kerbs count).</summary>
        public static bool Clear(Vector3 p, float radius, Transform ignore = null)
        {
            var t = DeformableTerrain.Instance;
            var a = new Vector3(p.x, t.Height(p.x, p.z) + 0.4f + radius, p.z);
            int n = Physics.OverlapCapsuleNonAlloc(a, a + Vector3.up * 3f, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].transform.name.StartsWith("Chunk")) continue;
                if (ignore && hits[i].transform.IsChildOf(ignore)) continue;
                return false;
            }
            return true;
        }

        /// <summary>What blocks a ground point (failure evidence).</summary>
        public static string Blockers(Vector3 p, float radius)
        {
            var t = DeformableTerrain.Instance;
            var a = new Vector3(p.x, t.Height(p.x, p.z) + 0.4f + radius, p.z);
            int n = Physics.OverlapCapsuleNonAlloc(a, a + Vector3.up * 3f, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++) if (!hits[i].transform.name.StartsWith("Chunk")) sb.Append(hits[i].name).Append(" (").Append(hits[i].transform.root.name).Append("); ");
            return sb.Length == 0 ? "nothing" : sb.ToString();
        }

        /// <summary>Open water at least <paramref name="minDepth"/> deep with <paramref name="room"/> m of it all round,
        /// nearest the start yard first; <paramref name="shore"/> = the dry bank towards the yard.</summary>
        public static bool FindWater(float minDepth, float room, float maxRange, out Vector3 at, out Vector3 outward, out Vector3 shore)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            g.World.Yard(out var origin, out _, out _);
            for (float r = 60f; r <= maxRange; r += 30f)
            {
                int n = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * r / 45f));
                for (int i = 0; i < n; i++)
                {
                    float a = i * 2f * Mathf.PI / n;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p = origin + dir * r;
                    if (t.WaterDepthNoLoad(p.x, p.z) < minDepth) continue;
                    bool open = true;
                    for (int k = 0; k < 8 && open; k++)
                    {
                        float b = k * Mathf.PI / 4f;
                        var q = p + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * room;
                        open = t.WaterDepthNoLoad(q.x, q.z) >= minDepth * 0.6f;
                    }
                    if (!open) continue;
                    for (float s = 3f; s < 400f; s += 3f)
                    {
                        var q = p - dir * s;
                        if (t.WaterDepthNoLoad(q.x, q.z) > 0f) continue;
                        at = p; outward = dir; shore = q;
                        at.y = t.WaterDepthNoLoad(p.x, p.z) + t.HeightNoLoad(p.x, p.z);
                        shore.y = t.HeightNoLoad(q.x, q.z);
                        return true;
                    }
                }
            }
            at = outward = shore = default;
            return false;
        }

        /// <summary>The airfield nearest the start yard within <paramref name="range"/>.</summary>
        public static Site Airfield(float range)
        {
            var g = WastelandGame.Instance;
            g.World.Yard(out var origin, out _, out _);
            var list = new List<Site>();
            g.World.SitesNear(origin, range, list);
            Site best = null; float bd = float.MaxValue;
            foreach (var s in list)
            {
                if (s.kind != SiteKind.Airfield) continue;
                float d = Vector2.Distance(s.pos, new Vector2(origin.x, origin.z));
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Where dirt tracks ford rivers (the track dips to ~0.35 m of water), nearest the yard first:
        /// centre (water surface height in y) and the track's direction across.</summary>
        public static List<(Vector3 centre, Vector3 across, float half)> Fords(float range)
        {
            var g = WastelandGame.Instance; var w = g.World; var t = DeformableTerrain.Instance;
            w.Yard(out var origin, out _, out _);
            var found = new List<(Vector3, Vector3, float)>();
            foreach (var road in w.roads.roads)
            {
                if (road.paved) continue;
                for (int i = 0; i + 1 < road.points.Count; i++)
                {
                    var a = road.points[i]; var b = road.points[i + 1];
                    float len = Flat(a, b);
                    for (float s = 0f; s < len; s += 3f)
                    {
                        var p = Vector3.Lerp(a, b, s / Mathf.Max(0.01f, len));
                        if (Flat(p, origin) > range) continue;
                        if (!w.RiverAt(p.x, p.z, out float level, out float half, out float dist) || dist > 1.5f) continue;
                        if (w.NaturalBiome(p.x, p.z) == Biome.Desert) continue;                     // dry wadis
                        if (t.WaterDepthNoLoad(p.x, p.z) < 0.1f) continue;
                        bool dup = false;
                        foreach (var f in found) if (Flat(f.Item1, p) < 60f) dup = true;
                        if (dup) continue;
                        found.Add((new Vector3(p.x, level, p.z), FlatDir(b - a), half));
                    }
                }
            }
            found.Sort((x, y) => Flat(x.Item1, origin).CompareTo(Flat(y.Item1, origin)));
            return found;
        }

        /// <summary>Right-hand bank angle (deg) and nose-up pitch (deg) of a transform.</summary>
        public static void Attitude(Transform tr, out float bankRight, out float pitchUp)
        {
            var fwd = tr.forward;
            var upProj = Vector3.ProjectOnPlane(Vector3.up, fwd);
            bankRight = upProj.sqrMagnitude > 1e-4f ? -Vector3.SignedAngle(upProj.normalized, tr.up, fwd) : 0f;
            pitchUp = -Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd, tr.right);
        }
    }
}
