using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Abandoned vehicles across the map: parts missing, scattered nearby or damaged, bodies dented.
    /// Far-away vehicles and loose parts sleep as kinematic bodies until the player comes close.</summary>
    public partial class WastelandGame
    {
        float sleepCheck;

        void SpawnWrecks(Vector3 spawn)
        {
            var rnd = new System.Random(seed * 31 + 7);
            var prefabs = new List<GameObject>(vehiclePrefabs);
            prefabs.AddRange(trailerPrefabs);
            prefabs.RemoveAll(pf => !pf || pf.GetComponent<FlightModel>());       // aircraft turn up at airfields, not by the road
            var roads = World.roads.roads;
            for (int i = 0; i < wreckCount; i++)
            {
                Vector3 pos, dir;
                var prefab = prefabs[rnd.Next(prefabs.Count)];
                if (i < 3)
                {
                    // a few at the back of the start yard so scavenging begins right away (cold: no fuel to cook off)
                    World.Yard(out var yo, out var ya, out var ys);
                    dir = Quaternion.Euler(0, 40f + i * 55f, 0) * ya;
                    pos = FindClearSpot(yo + ya * (46f + i * 9f) + ys * 30f, ya, HalfExtents(prefab), Quaternion.LookRotation(dir));
                }
                else if (i % 4 == 0)
                {
                    // around a town square, off its streets
                    var t = World.roads.towns[rnd.Next(World.roads.towns.Count)];
                    pos = Vector3.zero; dir = Vector3.forward;
                    for (int tries = 0; tries < 8; tries++)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        pos = new Vector3(t.x, 0, t.y) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (8f + (float)rnd.NextDouble() * 18f);
                        dir = new Vector3(Mathf.Sin(a + 1f), 0, Mathf.Cos(a + 1f));
                        if (World.Sample(pos.x, pos.z).roadDist > 6f) break;
                    }
                }
                else
                {
                    var road = roads[rnd.Next(roads.Count)];
                    int k = rnd.Next(4, road.points.Count - 5);
                    var a = road.points[k]; var b = road.points[k + 1];
                    var along = (b - a); along.y = 0; along.Normalize();
                    var side = Vector3.Cross(Vector3.up, along) * (rnd.NextDouble() < 0.5 ? -1f : 1f);
                    pos = a + side * (road.width * 0.5f + 3f + (float)rnd.NextDouble() * 4f);                 // on the verge
                    dir = Quaternion.Euler(0, (float)rnd.NextDouble() * 70f - 35f, 0) * along;
                }
                if (i >= 3 && (World.YardWeight(pos.x, pos.z) > 0f || World.Sample(pos.x, pos.z).roadDist < 4.5f)) continue;   // never on a lane or in the yard
                if (i >= 3) pos.y = terrain.Height(pos.x, pos.z) + 0.8f;
                var go = Instantiate(prefab, pos, Quaternion.LookRotation(dir));
                go.name = "Wreck " + prefab.name;
                var v = go.GetComponent<VehicleDriver>();
                if (go.TryGetComponent<InteriorSpace>(out var interior)) interior.furnish = rnd.NextDouble() < 0.3;
                Register(v, v.driveable ? wrecks : null);
                Ruin(v, rnd);
                if (go.TryGetComponent<VehicleDamage>(out var settle)) settle.graceUntil = Time.time + 4f;
                if (go.TryGetComponent<VehicleSystems>(out var sys))
                {
                    sys.fuel = i < 3 ? 0f : sys.fuelCapacity * (float)rnd.NextDouble() * 0.35f;
                    sys.oil = sys.oilCapacity * (float)rnd.NextDouble() * 0.8f;
                    sys.coolant = sys.coolantCapacity * (float)rnd.NextDouble() * 0.8f;
                }
                v.Body.isKinematic = true;
            }
            SpawnScrapyardWrecks(rnd, prefabs);
        }

        /// <summary>A few wrecks in every scrapyard lot (landmarks): cars to strip, parts lying about.</summary>
        void SpawnScrapyardWrecks(System.Random rnd, List<GameObject> prefabs)
        {
            var yards = new List<(Vector3 pos, float yaw)>();
            MadMax.World.BiomeProps.Scrapyards(World, yards);
            foreach (var (c, yaw) in yards)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < 4; i++)
                {
                    var prefab = prefabs[rnd.Next(prefabs.Count)];
                    if (!prefab.GetComponent<VehicleDriver>().driveable) continue;
                    var pos = c + q * new Vector3(-6f + (i % 2) * 11f, 0f, -3f + (i / 2) * 9f);
                    pos.y = terrain.Height(pos.x, pos.z) + 0.8f;
                    var go = Instantiate(prefab, pos, q * Quaternion.Euler(0f, 80f + (float)rnd.NextDouble() * 20f, 0f));
                    go.name = "Wreck " + prefab.name;
                    var v = go.GetComponent<VehicleDriver>();
                    if (go.TryGetComponent<InteriorSpace>(out var interior)) interior.furnish = false;
                    Register(v, wrecks);
                    Ruin(v, rnd);
                    if (go.TryGetComponent<VehicleDamage>(out var settle)) settle.graceUntil = Time.time + 4f;
                    if (go.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = 0f; sys.oil *= 0.3f; }
                    v.Body.isKinematic = true;
                }
            }
        }

        void Ruin(VehicleDriver v, System.Random rnd)
        {
            var chassis = v.GetComponent<VehicleChassis>();
            if (rnd.NextDouble() < 0.25 && v.TryGetComponent<VehicleArmor>(out var kit)) kit.RandomKit(rnd.Next(), 0.85f);   // someone's old war rig
            foreach (var s in chassis.Sockets)
            {
                var part = s.Current;
                if (!part) continue;
                double roll = rnd.NextDouble();
                if (roll < 0.28) { var p = s.Detach(false); if (p) Destroy(p.gameObject); }        // stripped
                else if (roll < 0.42)
                {
                    var p = s.Detach(true);                                                          // lying nearby
                    if (!p) continue;
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    var at = v.transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (3f + (float)rnd.NextDouble() * 3f);
                    at.y = terrain.Height(at.x, at.z) + 0.4f;
                    p.transform.SetPositionAndRotation(at, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 90f * rnd.Next(2)));
                    if (p.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
                    p.damage = (float)rnd.NextDouble() * 0.6f;
                }
                else part.damage = (float)(rnd.NextDouble() * (part.category == PartCategory.Engine ? 0.95 : 0.8));
            }
            if (v.TryGetComponent<VehicleDamage>(out var dmg))
            {
                var b = HalfExtents(v.gameObject);
                int dents = 2 + rnd.Next(5);
                for (int i = 0; i < dents; i++)
                {
                    var local = new Vector3(((float)rnd.NextDouble() * 2f - 1f) * b.x, (float)rnd.NextDouble() * b.y * 2f, ((float)rnd.NextDouble() * 2f - 1f) * b.z);
                    var w = v.transform.TransformPoint(local);
                    dmg.DentAt(w, (v.transform.TransformPoint(new Vector3(0, b.y, 0)) - w).normalized, 0.06f + (float)rnd.NextDouble() * 0.14f, 0.4f + (float)rnd.NextDouble() * 0.6f);
                }
                dmg.AddFrameDamage((float)rnd.NextDouble() * 0.6f, rnd.NextDouble() < 0.5 ? -1f : 1f);
            }
        }

        /// <summary>Wake bodies near the player (terrain colliders exist there), freeze distant ones.</summary>
        void UpdateSleepers()
        {
            if (Time.time < sleepCheck) return;
            sleepCheck = Time.time + 0.4f;
            var focus = terrain.focus ? terrain.focus.position : Vector3.zero;
            var net = MadMax.Net.NetSession.Instance;
            float Dist(Vector3 p)
            {
                float d = terrain.focus ? Vector3.Distance(p, focus) : 1e9f;
                foreach (var f in terrain.extraFoci) if (f) d = Mathf.Min(d, Vector3.Distance(p, f.position));
                return d;
            }
            vehicles.RemoveAll(x => !x);
            foreach (var v in vehicles)
            {
                if (v == Current) continue;
                if (net && net.Online && !net.Simulates(v)) continue;          // interpolated replica
                if (v.aiDriven) { if (v.Body.isKinematic) Wake(v.Body); continue; }   // NPC drivers roam beyond the frozen zone
                var tc = v.GetComponent<TowCoupling>();
                if (tc && tc.Tower) { v.Body.isKinematic = false; continue; }
                float d = Dist(v.transform.position);
                if (!v.Body.isKinematic && d > 70f) v.Body.isKinematic = true;
                else if (v.Body.isKinematic && d < 50f) Wake(v.Body);
            }
            foreach (var part in VehiclePart.Registry)
            {
                if (!part || part.Socket || part.transform.parent || !part.TryGetComponent<Rigidbody>(out var rb)) continue;
                if (net && net.IsClient) continue;                            // the server simulates loose parts
                float d = Dist(part.transform.position);
                if (!rb.isKinematic && d > 70f) rb.isKinematic = true;
                else if (rb.isKinematic && d < 50f) Wake(rb);
            }
        }

        void Wake(Rigidbody rb)
        {
            if (rb.TryGetComponent<VehicleDamage>(out var settle)) settle.graceUntil = Time.time + 3f;   // dropping back onto the ground
            var p = rb.position;
            p.y = Mathf.Max(p.y, terrain.Height(p.x, p.z) + 0.5f);
            rb.position = p;
            rb.isKinematic = false;
        }
    }
}
