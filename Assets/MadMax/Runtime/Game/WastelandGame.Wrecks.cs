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
                else if (i % 4 == 0 && World.settlements.Count > 0 && World.settlements[0].plan == null)
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
                if (i >= 3 && World.InTown(pos.x, pos.z)) continue;                                   // towns park their own (PlanTownWrecks)
                if (World.Reserved(pos.x, pos.z)) continue;                                              // campaign scenes stay clear
                wreckPlans.Add(new WreckPlan { prefab = pf, pos = pos, dir = dir, seed = wseed, cold = i < 3 });
            }
            PlanTownWrecks(prefabs, cars);
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

        /// <summary>New game: plan the wrecks, place the yard ones now (one a frame); the rest spawn as the player comes near.</summary>
        System.Collections.IEnumerator SpawnWrecks(Vector3 spawn)
        {
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.Wrecks");
            PlanWrecks();
            wrecksPending.Clear();
            UnityEngine.Profiling.Profiler.EndSample();
            for (int i = 0; i < wreckPlans.Count; i++)
            {
                if (i < 3 && wreckPlans[i].cold && i < wreckCount)
                {
                    yield return null;
                    UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.Wrecks");
                    SpawnWreck(i);
                    UnityEngine.Profiling.Profiler.EndSample();
                }
                else wrecksPending.Add(i);
            }
        }

        /// <summary>Spawn the next pending wreck within 220 m of the player (one every quarter second). A streamed wreck
        /// is built over several frames (<see cref="SpawnWreckSteps"/>): copied under an inactive holder, woken and
        /// ruined the next frame, its dents worked off within <see cref="WreckDentBudgetMs"/> a frame after that.</summary>
        void UpdateWreckStreaming()
        {
            if (wreckJob != null) { StepWreckJob(); return; }
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
                wreckJob = SpawnWreckSteps(idx, true);
                StepWreckJob();
                return;
            }
        }

        /// <summary>Main-thread time a streamed wreck's dents may take per frame.</summary>
        const float WreckDentBudgetMs = 2f;
        System.Collections.IEnumerator wreckJob;
        Transform wreckHold;                                                                     // inactive: Instantiate copies there without Awake

        void StepWreckJob()
        {
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Game.Wreck.Step");
            if (!wreckJob.MoveNext()) wreckJob = null;
            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>A streamed wreck still being built is finished at once (a save must see it whole).</summary>
        void FinishWreckSpawns()
        {
            while (wreckJob != null) if (!wreckJob.MoveNext()) wreckJob = null;
        }

        void SpawnWreck(int index)
        {
            var it = SpawnWreckSteps(index, false);
            while (it.MoveNext()) { }
        }

        /// <summary>Dents a streamed wreck applies after its spawn frame: the meshes <see cref="VehicleDamage.DentAt"/>
        /// would dent (taken at the ruin, in its order) and each dent in vehicle space (the same rolls, the same result).</summary>
        class WreckDents
        {
            public readonly List<MeshFilter> meshes = new List<MeshFilter>();
            public readonly List<(Vector3 local, Vector3 top, float depth, float radius)> dents = new List<(Vector3, Vector3, float, float)>();
        }

        System.Collections.IEnumerator SpawnWreckSteps(int index, bool staged)
        {
            if (index < 0 || index >= wreckPlans.Count) yield break;
            var plan = wreckPlans[index];
            var prefabs = WreckPrefabs();
            if (plan.prefab >= prefabs.Count) yield break;
            var prefab = prefabs[plan.prefab];
            var rnd = new System.Random(plan.seed);
            var pos = plan.pos;
            var rot = Quaternion.LookRotation(plan.dir.sqrMagnitude > 0.01f ? plan.dir : Vector3.forward);
            if (index < 3 && plan.cold && index < wreckCount) pos = FindClearSpot(pos, plan.dir, HalfExtents(prefab), rot);
            else pos.y = terrain.HeightNoLoad(pos.x, pos.z) + 0.8f;                              // settled below; no chunk generated for it
            GameObject go;
            if (staged)
            {
                if (!wreckHold) { var h = new GameObject("Wreck Spawn"); h.SetActive(false); wreckHold = h.transform; }
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Game.Wreck.Instantiate");
                go = Instantiate(prefab, pos, rot, wreckHold);                                   // copied, not awake yet
                UnityEngine.Profiling.Profiler.EndSample();
                yield return null;
                if (!go) yield break;
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Game.Wreck.Wake");
                go.transform.SetParent(null, true);                                              // Awake / OnEnable now
                UnityEngine.Profiling.Profiler.EndSample();
            }
            else go = Instantiate(prefab, pos, rot);
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Game.Wreck.Ruin");
            go.name = "Wreck " + prefab.name;
            var v = go.GetComponent<VehicleDriver>();
            if (go.TryGetComponent<InteriorSpace>(out var interior)) interior.furnish = rnd.NextDouble() < 0.3;
            Register(v, v.driveable ? wrecks : null);
            var later = staged ? new WreckDents() : null;
            Ruin(v, rnd, later);
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
            SettleOnGround(v);
            UnityEngine.Profiling.Profiler.EndSample();
            if (later == null || later.dents.Count == 0 || !settle) yield break;
            // the dents: one mesh after another (each copied, dented, uploaded once), a couple of ms a frame
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int m = 0; m < later.meshes.Count; m++)
            {
                if (sw.Elapsed.TotalMilliseconds > WreckDentBudgetMs) { yield return null; sw.Restart(); }
                if (!v || !settle) yield break;
                var mf = later.meshes[m];
                if (!mf) continue;
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Game.Wreck.Dent");
                var t = v.transform;
                foreach (var d in later.dents)
                {
                    var w = t.TransformPoint(d.local);
                    settle.DentMesh(mf, w, (t.TransformPoint(d.top) - w).normalized, d.depth, d.radius);
                }
                UnityEngine.Profiling.Profiler.EndSample();
            }
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
            SettleOnGround(v);
            return v;
        }

        /// <summary>Strip, scatter and damage a fresh wreck. With <paramref name="later"/> the dents are only rolled (same
        /// rolls, same order) and left to the caller, unless it burns (the char works on the dented meshes).</summary>
        void Ruin(VehicleDriver v, System.Random rnd, WreckDents later = null)
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
                    at.y = terrain.HeightNoLoad(at.x, at.z) + 0.4f;                                  // far off: no chunk generated for it
                    p.transform.SetPositionAndRotation(at, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 90f * rnd.Next(2)));
                    if (p.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
                    p.damage = (float)rnd.NextDouble() * 0.6f;
                }
                else part.damage = (float)(rnd.NextDouble() * (part.category == PartCategory.Engine ? 0.95 : 0.8));
            }
            int stash; bool burned;
            if (v.TryGetComponent<VehicleDamage>(out var dmg))
            {
                var b = HalfExtents(v.gameObject);
                var top = new Vector3(0, b.y, 0);
                later?.dents.Clear();
                var rolled = later != null ? later.dents : new List<(Vector3 local, Vector3 top, float depth, float radius)>();
                int dents = 2 + rnd.Next(5);
                for (int i = 0; i < dents; i++)
                {
                    var local = new Vector3(((float)rnd.NextDouble() * 2f - 1f) * b.x, (float)rnd.NextDouble() * b.y * 2f, ((float)rnd.NextDouble() * 2f - 1f) * b.z);
                    float depth = 0.06f + (float)rnd.NextDouble() * 0.14f;
                    rolled.Add((local, top, depth, 0.4f + (float)rnd.NextDouble() * 0.6f));
                }
                dmg.AddFrameDamage((float)rnd.NextDouble() * 0.6f, rnd.NextDouble() < 0.5 ? -1f : 1f);
                stash = rnd.Next();
                burned = rnd.NextDouble() < 0.12;                                                    // burned out where it stopped
                if (later != null && !burned) dmg.DentTargets(later.meshes);                         // dented by the caller, a mesh at a time
                else
                {
                    foreach (var d in rolled)
                    {
                        var w = v.transform.TransformPoint(d.local);
                        dmg.DentAt(w, (v.transform.TransformPoint(d.top) - w).normalized, d.depth, d.radius);
                    }
                    later?.dents.Clear();
                }
            }
            else { stash = rnd.Next(); burned = rnd.NextDouble() < 0.12; }
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
                if (v.transform.parent && v.GetComponentInParent<MadMax.World.RollingCity>()) continue;   // strapped on the rolling city's deck
                if (RigVehicle.OnStands(v)) continue;                                              // a moving base being built: held on its stands
                var tc = v.GetComponent<TowCoupling>();
                if (tc && tc.Tower) { v.Body.isKinematic = false; continue; }
                float d = Dist(v.transform.position);
                if (!v.Body.isKinematic && d > 70f) { v.Body.isKinematic = true; SettleOnGround(v); }
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

        /// <summary>Set a vehicle that is about to sleep kinematic down on the ground under it: resting on its springs
        /// (on its belly when the wheels are gone), tilted to the slope, the heading kept. Left alone when it lies on its
        /// side or roof, stands on a deck, or floats. Without this, cars frozen mid-drop (spawned wrecks, a car still
        /// settling when the player drove 70 m off) hovered until the player came within 50 m.</summary>
        public void SettleOnGround(VehicleDriver v)
        {
            if (!v || !terrain) return;
            var t = v.transform;
            if (t.up.y < 0.6f || v.GetComponentInParent<MadMax.World.RollingCity>() || v.transform.parent) return;
            var p = v.Body ? v.Body.position : t.position;
            float probe = p.y + 1.5f;
            if (MadMax.Building.StructureGround.Count > 0 && MadMax.Building.StructureGround.Top(new Vector3(p.x, p.y + 1.5f, p.z), ref probe, out _)) return;
            var fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) return;
            var right = Vector3.Cross(Vector3.up, fwd);
            float H(Vector3 at) => terrain.HeightNoLoad(at.x, at.z);
            float hf = H(p + fwd * 1.4f), hb = H(p - fwd * 1.4f), hr = H(p + right * 0.75f), hl = H(p - right * 0.75f);
            float ground = (hf + hb + hr + hl) * 0.25f;
            if (!float.IsNaN(terrain.WaterLevel(p.x, p.z)) && terrain.WaterLevel(p.x, p.z) > ground + 0.3f) return;   // afloat or sunk
            var n = Vector3.Cross(fwd * 2.8f + Vector3.up * (hf - hb), right * 1.5f + Vector3.up * (hr - hl)).normalized;
            if (n.y < 0f) n = -n;
            float rest = v.HasWheels ? v.RestHeight : BellyHeight(v);
            var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, n).normalized, n);
            var pos = new Vector3(p.x, ground, p.z) + n * rest;
            if (v.Body) { v.Body.position = pos; v.Body.rotation = rot; }
            t.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Origin height above the ground for a vehicle lying on its body (lowest collider point, local).</summary>
        static float BellyHeight(VehicleDriver v)
        {
            float min = float.MaxValue;
            var inv = v.transform.worldToLocalMatrix;
            foreach (var c in v.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger) continue;
                var b = c.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    min = Mathf.Min(min, inv.MultiplyPoint3x4(corner).y);
                }
            }
            return min == float.MaxValue ? 0.4f : Mathf.Max(0.05f, -min);
        }

        void Wake(Rigidbody rb)
        {
            if (rb.TryGetComponent<VehicleDamage>(out var settle)) settle.graceUntil = Time.time + 3f;
            // put down where it rests instead of dropping it (a lift to 0.5 m over the ground made everything that woke up
            // fall, bounce and rock in front of the player), still and asleep until something touches it
            if (rb.TryGetComponent<VehicleDriver>(out var v) && !v.transform.parent && v.transform.up.y >= 0.6f && v.HasWheels) SettleOnGround(v);
            else GroundByColliders(rb);
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        /// <summary>Move a (kinematic) body so its lowest collider just touches what is under it: out of the terrain if it
        /// sank in, then down onto the ground, a rock or a deck below (within a metre).</summary>
        void GroundByColliders(Rigidbody rb)
        {
            float sink = 0f;
            foreach (var c in rb.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger) continue;
                var b = c.bounds;
                float g = Mathf.Max(Mathf.Max(terrain.Height(b.min.x, b.min.z), terrain.Height(b.max.x, b.min.z)), Mathf.Max(terrain.Height(b.min.x, b.max.z), terrain.Height(b.max.x, b.max.z)));
                g = Mathf.Max(g, terrain.Height(b.center.x, b.center.z));
                sink = Mathf.Max(sink, g - b.min.y);
            }
            var p = rb.position;
            if (sink > 0f) { p.y += sink + 0.01f; rb.position = p; rb.transform.position = p; }
            if (rb.SweepTest(Vector3.down, out var hit, 1f, QueryTriggerInteraction.Ignore) && hit.distance > 0.02f)
            { p.y -= hit.distance - 0.01f; rb.position = p; rb.transform.position = p; }
        }

        /// <summary>Behind the loading fade: wake everything near the player onto the ground and let it come to rest
        /// (physics at 3x, at most <see cref="SettleStepsPerFrame"/> steps a frame), then send it to sleep, so nothing
        /// settles, rocks or slides in view once the game starts.</summary>
        const int SettleStepsPerFrame = 6;
        /// <summary>The step cap to restore if the scene goes away mid-settle (-1: not settling).</summary>
        float settleMaxDt = -1f;

        System.Collections.IEnumerator SettleWorld()
        {
            if (!Player || !terrain) yield break;
            var at = Current ? Current.transform.position : Player.transform.position;
            yield return terrain.BuildAround(at, LoadBudgetMs, f => ScreenFader.Progress(0.88f + 0.06f * f));   // what the settings' view radius added
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.Settle");
            sleepCheck = 0f;
            UpdateSleepers();
            var settling = new List<Rigidbody>();
            foreach (var v in vehicles) if (v && v != Current && !v.aiDriven && v.Body && !v.Body.isKinematic) settling.Add(v.Body);
            foreach (var part in VehiclePart.Registry)
                if (part && !part.Socket && !part.transform.parent && part.TryGetComponent<Rigidbody>(out var prb) && !prb.isKinematic) settling.Add(prb);
            float scale = Time.timeScale, maxDt = Time.maximumDeltaTime, t0 = Time.realtimeSinceStartup;
            UnityEngine.Profiling.Profiler.EndSample();
            Time.timeScale = 3f;
            settleMaxDt = maxDt;
            Time.maximumDeltaTime = Time.fixedDeltaTime * SettleStepsPerFrame;                   // a slow frame must not run dozens of steps
            // kept awake the whole time: Unity would put a slowly tipping heavy body to sleep half way over
            int still = 0;
            while (Time.realtimeSinceStartup - t0 < 3f && still < 20)
            {
                bool moving = false;
                foreach (var rb in settling) if (rb) { rb.WakeUp(); if (rb.linearVelocity.sqrMagnitude > 1e-4f || rb.angularVelocity.sqrMagnitude > 1e-4f) moving = true; }
                still = moving || Time.realtimeSinceStartup - t0 < 0.8f ? 0 : still + 1;
                yield return null;
            }
            Time.timeScale = scale;
            Time.maximumDeltaTime = maxDt;
            settleMaxDt = -1f;
            foreach (var v in vehicles)
                if (v && v != Current && !v.aiDriven && v.Body && !v.Body.isKinematic) { v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero; v.Body.Sleep(); }
            foreach (var part in VehiclePart.Registry)
                if (part && !part.Socket && !part.transform.parent && part.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.Sleep(); }
        }
    }
}
