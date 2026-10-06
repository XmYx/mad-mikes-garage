using System.Collections.Generic;
using MadMax.Net;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Abandoned vehicles across the map: parts missing, scattered nearby or damaged, bodies dented.
    /// Far-away vehicles and loose parts sleep as kinematic bodies until the player comes close.</summary>
    public partial class WastelandGame
    {
        float sleepCheck;

        // ---- wrecks are planned up front (deterministic from the seed) and spawned as the player comes near them
        struct WreckPlan { public int prefab; public Vector3 pos, dir; public int seed; public bool cold; }
        readonly List<WreckPlan> wreckPlans = new List<WreckPlan>();
        /// <summary>Plan indices not spawned yet (saved: a load keeps spawning them lazily).</summary>
        readonly List<int> wrecksPending = new List<int>();
        bool wrecksPlanned;
        float wreckCheck;

        List<GameObject> WreckPrefabs()
        {
            var prefabs = new List<GameObject>(vehiclePrefabs);
            prefabs.AddRange(trailerPrefabs);
            prefabs.RemoveAll(pf => !pf || pf.GetComponent<FlightModel>());       // aircraft turn up at airfields, not by the road
            return prefabs;
        }

        /// <summary>Where every wreck of this world lies (no objects yet): a few at the back of the start yard, some
        /// around town squares, the rest on the verges, and four in each scrapyard.</summary>
        void PlanWrecks()
        {
            wreckPlans.Clear();
            wrecksPlanned = true;
            var rnd = new System.Random(seed * 31 + 7);
            var prefabs = WreckPrefabs();
            var roads = World.roads.roads;
            // the yard wrecks are plain cars (lights, bumpers, exhausts to take off), not machines or rigs
            var cars = new List<int>();
            for (int k = 0; k < prefabs.Count; k++)
            {
                var pfb = prefabs[k];
                if (!pfb.GetComponent<VehicleDriver>().driveable || pfb.GetComponent<Machine>() || pfb.GetComponent<BikeBalance>() || !pfb.TryGetComponent<VehicleChassis>(out var ch)) continue;
                foreach (var s in ch.GetComponentsInChildren<MountSocket>(true)) if (s.accepts == PartCategory.Lights) { cars.Add(k); break; }
            }
            for (int i = 0; i < wreckCount; i++)
            {
                Vector3 pos, dir;
                int pf = rnd.Next(prefabs.Count);
                if (i < 3 && cars.Count > 0) pf = cars[pf % cars.Count];
                if (i < 3)
                {
                    // a few at the back of the start yard so scavenging begins right away (cold: no fuel to cook off)
                    World.Yard(out var yo, out var ya, out var ys);
                    dir = Quaternion.Euler(0, 40f + i * 55f, 0) * ya;
                    pos = yo + ya * (46f + i * 9f) + ys * 30f;
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
                int wseed = rnd.Next();
                if (i >= 3 && (World.YardWeight(pos.x, pos.z) > 0f || World.Sample(pos.x, pos.z).roadDist < 4.5f)) continue;   // never on a lane or in the yard
                if (World.Reserved(pos.x, pos.z)) continue;                                              // campaign scenes stay clear
                wreckPlans.Add(new WreckPlan { prefab = pf, pos = pos, dir = dir, seed = wseed, cold = i < 3 });
            }
            // four in each scrapyard lot
            var yards = new List<(Vector3 pos, float yaw)>();
            MadMax.World.BiomeProps.Scrapyards(World, yards);
            foreach (var (c, yaw) in yards)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < 4; i++)
                {
                    int pf = rnd.Next(prefabs.Count);
                    if (!prefabs[pf].GetComponent<VehicleDriver>().driveable) continue;
                    var pos = c + q * new Vector3(-6f + (i % 2) * 11f, 0f, -3f + (i / 2) * 9f);
                    var dir = q * Quaternion.Euler(0f, 80f + (float)rnd.NextDouble() * 20f, 0f) * Vector3.forward;
                    wreckPlans.Add(new WreckPlan { prefab = pf, pos = pos, dir = dir, seed = rnd.Next(), cold = true });
                }
            }
        }

        /// <summary>New game: plan the wrecks, place the yard ones now; the rest spawn as the player comes near.</summary>
        void SpawnWrecks(Vector3 spawn)
        {
            PlanWrecks();
            wrecksPending.Clear();
            for (int i = 0; i < wreckPlans.Count; i++) { if (i < 3 && wreckPlans[i].cold && i < wreckCount) SpawnWreck(i); else wrecksPending.Add(i); }
        }

        /// <summary>Spawn the next pending wreck within 220 m of the player (one every quarter second).</summary>
        void UpdateWreckStreaming()
        {
            if (wrecksPending.Count == 0 || Time.time < wreckCheck || (NetSession.Instance && NetSession.Instance.IsClient)) return;
            wreckCheck = Time.time + 0.25f;
            bool Near(Vector3 p, Vector3 f) { float dx = p.x - f.x, dz = p.z - f.z; return dx * dx + dz * dz < 220f * 220f; }
            for (int k = 0; k < wrecksPending.Count; k++)
            {
                var plan = wreckPlans[wrecksPending[k]];
                bool near = !Dedicated && Near(plan.pos, FocusPos);
                if (!near && terrain) foreach (var f in terrain.extraFoci) if (f && Near(plan.pos, f.position)) { near = true; break; }   // other players
                if (!near) continue;
                int idx = wrecksPending[k];
                wrecksPending.RemoveAt(k);
                SpawnWreck(idx);
                return;
            }
        }

        void SpawnWreck(int index)
        {
            if (index < 0 || index >= wreckPlans.Count) return;
            var plan = wreckPlans[index];
            var prefabs = WreckPrefabs();
            if (plan.prefab >= prefabs.Count) return;
            var prefab = prefabs[plan.prefab];
            var rnd = new System.Random(plan.seed);
            var pos = plan.pos;
            var rot = Quaternion.LookRotation(plan.dir.sqrMagnitude > 0.01f ? plan.dir : Vector3.forward);
            if (index < 3 && plan.cold && index < wreckCount) pos = FindClearSpot(pos, plan.dir, HalfExtents(prefab), rot);
            else pos.y = terrain.Height(pos.x, pos.z) + 0.8f;
            var go = Instantiate(prefab, pos, rot);
            go.name = "Wreck " + prefab.name;
            var v = go.GetComponent<VehicleDriver>();
            if (go.TryGetComponent<InteriorSpace>(out var interior)) interior.furnish = rnd.NextDouble() < 0.3;
            Register(v, v.driveable ? wrecks : null);
            Ruin(v, rnd);
            if (index == 0 && plan.cold) TutorialPart(v);
            if (go.TryGetComponent<VehicleDamage>(out var settle)) settle.graceUntil = Time.time + 4f;
            if (go.TryGetComponent<VehicleSystems>(out var sys))
            {
                sys.fuel = plan.cold ? 0f : sys.fuelCapacity * (float)rnd.NextDouble() * 0.35f;
                sys.oil = sys.oilCapacity * (float)rnd.NextDouble() * 0.8f;
                sys.coolant = sys.coolantCapacity * (float)rnd.NextDouble() * 0.8f;
                if (FuelFrozen(MadMax.World.Weather.Season) && MadMax.World.Weather.Temperature < 0f) sys.IceTank();
            }
            v.Body.isKinematic = true;
        }

        /// <summary>The first yard wreck keeps a light bar on its roof: something to take off with the wrench and bolt onto
        /// your own car (FIRST STEPS).</summary>
        void TutorialPart(VehicleDriver v)
        {
            foreach (var s in v.GetComponent<VehicleChassis>().Sockets)
            {
                if (s.Current || s.accepts != PartCategory.Lights) continue;
                var part = SpawnPart("lights_bar", s.transform.position, s.transform.rotation);
                if (part && !s.Attach(part)) Destroy(part.gameObject);
                return;
            }
        }

        void SaveWreckPlan(SaveData d) { d.wrecksPlanned = wrecksPlanned; d.wrecksPending = new List<int>(wrecksPending); }

        void LoadWreckPlan(SaveData d)
        {
            wrecksPending.Clear();
            if (!d.wrecksPlanned) return;                                                        // older saves spawned every wreck up front
            PlanWrecks();
            if (d.wrecksPending != null) wrecksPending.AddRange(d.wrecksPending);
        }

        /// <summary>A wreck left by a fight on the road (off-screen skirmishes): ruined, dry, asleep until the player
        /// comes; saved like any wreck.</summary>
        public VehicleDriver SpawnRoadWreck(string design, Vector3 pos, Quaternion rot, int seed)
        {
            var prefab = PrefabFor(design);
            if (!prefab) return null;
            pos.y = terrain.HeightNoLoad(pos.x, pos.z) + 0.8f;
            var go = Instantiate(prefab, pos, rot);
            go.name = "Wreck " + prefab.name;
            var v = go.GetComponent<VehicleDriver>();
            if (go.TryGetComponent<InteriorSpace>(out var interior)) interior.furnish = false;
            Register(v, v.driveable ? wrecks : null);
            Ruin(v, new System.Random(seed));
            if (go.TryGetComponent<VehicleDamage>(out var settle)) { settle.graceUntil = Time.time + 4f; settle.AddFrameDamage(0.5f, 1f); }
            if (go.TryGetComponent<VehicleSystems>(out var sys) && !(go.TryGetComponent<VehicleBurn>(out var vb) && vb.charred)) { sys.fuel = sys.fuelCapacity * 0.05f; }
            v.Body.isKinematic = true;
            return v;
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
                float strip = Rules.StripChance;                                                    // LOOT: how picked-over wrecks are
                if (roll < strip) { var p = s.Detach(false); if (p) Destroy(p.gameObject); }        // stripped
                else if (roll < strip + 0.14)
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
            int stash = rnd.Next();
            bool burned = rnd.NextDouble() < 0.12;                                                   // burned out where it stopped
            if (v.TryGetComponent<VehicleStorage>(out var storage) && !burned) storage.FillWreck(stash);   // a searchable trunk and glovebox
            if (burned && v.TryGetComponent<VehicleBurn>(out var vb)) vb.Char(false);
            if (v.TryGetComponent<VehicleIgnition>(out var ign)) ign.RollWreck(new System.Random(stash ^ 0x6b1d), storage);   // after the stash: the glovebox may hold the key
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
                if (v.transform.parent && v.GetComponentInParent<TrailerDeck>()) continue;   // strapped on a transporter: stays kinematic, rides along
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
