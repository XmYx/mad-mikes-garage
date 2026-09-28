using System;
using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Saved per convoy: which generation is on the road and when the last one was wiped out.</summary>
    [Serializable]
    public class ConvoySave { public string id; public int generation, deadDay = -1; }

    /// <summary>Vehicles travelling a road together: a trader (one truck, pulls over for customers, the trader steps
    /// out) or a raider horde (armoured cars, a boss). Far from the player a convoy is just a distance along its road
    /// (ping-pong); within 140 m it becomes real vehicles with <see cref="AiDriver"/>s, beyond 230 m it folds back.
    /// Raiders that spot the player close in and the boss steps out to demand a toll (parley through
    /// <see cref="Dialogue"/>); refuse, attack or run and they hunt: ramming, shotgun blasts, fire bottles, crews
    /// bailing out of wrecked cars to fight on foot. Towns are safe ground.</summary>
    public class Convoy
    {
        public enum Phase { Travel, Stopped, Confront, Parley, Attack, Leave, Flee, Gone }
        public enum Outcome { Paid, Scared, Fooled, Recruited, Failed }

        public string id, kind;
        public bool raiders;
        public int seed;
        public List<Vector3> route;
        public float travel, speed = 10f;
        public Phase phase = Phase.Travel;
        public ConvoySave save;
        public readonly List<string> designs = new List<string>();
        public readonly List<NpcProfile> crew = new List<NpcProfile>();
        public readonly List<AiDriver> cars = new List<AiDriver>();
        public readonly List<Npc> walkers = new List<Npc>();
        public Npc Boss;                                     // the leader on foot (parley, trader at a stop)
        float timer, truceUntil, farT, fireCd, modeT;
        float routeLen;
        float[] cum;

        public bool Spawned => cars.Count > 0 || walkers.Count > 0;
        public string Gang => crew.Count > 0 ? crew[0].gang : "RAIDERS";
        /// <summary>The boss was won over at a parley: this gang lets the player pass.</summary>
        public bool Friendly => raiders && crew.Count > 0 && NpcRegistry.Get(crew[0]).disposition >= 50;

        public Convoy(string id, bool raiders, string kind, List<Vector3> road, int seed, ConvoySave save)
        {
            this.id = id; this.raiders = raiders; this.kind = kind; this.seed = seed; this.save = save;
            route = road;
            cum = new float[road.Count];
            for (int i = 1; i < road.Count; i++) cum[i] = cum[i - 1] + Vector3.Distance(road[i - 1], road[i]);
            routeLen = Mathf.Max(1f, cum[road.Count - 1]);
            travel = new System.Random(seed).Next(0, 100000) % (int)(routeLen * 2f);
            Crew();
        }

        void Crew()
        {
            designs.Clear(); crew.Clear();
            var r = new System.Random(seed * 13 + save.generation * 7919);
            string cid = id + ":" + save.generation;
            if (raiders)
            {
                designs.Add(r.NextDouble() < 0.35 ? "Hauler" : "Interceptor");
                int n = 2 + r.Next(3);
                string[] pool = { "Interceptor", "Scavenger", "Coupe", "Pickup", "Sedan", "Scavenger" };
                for (int i = 0; i < n; i++) designs.Add(pool[r.Next(pool.Length)]);
                for (int i = 0; i < designs.Count; i++) crew.Add(NpcProfile.Make(cid + ":" + i, i == 0 ? NpcRole.RaiderBoss : NpcRole.Raider, r.Next()));
                speed = 13f;
            }
            else
            {
                designs.Add(kind == "parts" ? "TowTruck" : kind == "food" ? "Wagon" : kind == "fuel" ? "Pickup" : "Sedan");
                crew.Add(NpcProfile.Make(cid + ":0", NpcRole.Trader, r.Next(), kind));
                speed = 9f;
            }
        }

        // ------------------------------------------------------------------ route

        /// <summary>Point and heading at a distance along the ping-pong loop.</summary>
        public Vector3 PointAt(float d, out Vector3 dir)
        {
            float loop = Mathf.Repeat(d, routeLen * 2f);
            bool back = loop > routeLen;
            float s = back ? routeLen * 2f - loop : loop;
            int i = Array.BinarySearch(cum, s);
            if (i < 0) i = ~i;
            i = Mathf.Clamp(i, 1, route.Count - 1);
            float t = Mathf.InverseLerp(cum[i - 1], cum[i], s);
            dir = (route[i] - route[i - 1]).normalized * (back ? -1f : 1f);
            return Vector3.Lerp(route[i - 1], route[i], t);
        }

        /// <summary>Distance along the route of the point nearest to p (for folding a real convoy back to the road).</summary>
        float Project(Vector3 p, Vector3 heading)
        {
            float best = float.MaxValue, at = 0f; int seg = 0;
            for (int i = 1; i < route.Count; i++)
            {
                var a = route[i - 1]; var b = route[i];
                var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; at = cum[i - 1] + t * ab.magnitude; seg = i; }
            }
            bool forward = Vector3.Dot(heading, route[seg] - route[seg - 1]) >= 0f;
            return forward ? at : routeLen * 2f - at;
        }

        // ------------------------------------------------------------------ lifecycle

        public void Tick(WastelandGame g, Vector3 focus, float dt)
        {
            if (phase == Phase.Gone)
            {
                if (save.deadDay >= 0 && DayNight.Day - save.deadDay >= (raiders ? 3 : 2)) { save.generation++; save.deadDay = -1; Crew(); phase = Phase.Travel; }
                return;
            }
            if (!Spawned)
            {
                travel += speed * dt;
                var p = PointAt(travel, out _);
                if (Flat(p - focus).magnitude < 140f) Spawn(g);
                return;
            }
            walkers.RemoveAll(w => !w);
            cars.RemoveAll(c => !c);
            var lead = Leader();
            var leadPos = lead ? lead.transform.position : Boss ? Boss.transform.position : focus;
            bool calm = phase == Phase.Travel || phase == Phase.Leave || phase == Phase.Flee;
            if (calm && Flat(leadPos - focus).magnitude > 230f) { Despawn(); return; }
            if (lead == null && AllDown()) { Wiped(); return; }

            var target = g.Current ? g.Current.transform : g.Player.transform;
            float dist = Flat(target.position - leadPos).magnitude;
            bool inTown = g.World.SettlementAt(target.position.x, target.position.z) != null;
            timer += dt;
            switch (phase)
            {
                case Phase.Travel:
                    if (raiders)
                    {
                        float sight = g.Current && Mathf.Abs(g.Current.ForwardSpeed) > 15f ? 110f : 75f;
                        if (Friendly) { if (dist < 30f && Time.time > truceUntil) { truceUntil = Time.time + 120f; MadMax.Audio.Sfx.Play("horn", leadPos, 0.8f, 1.2f, 120f); g.Toast("THE " + Gang + " HONK A GREETING"); } }
                        else if (Time.time > truceUntil && !inTown && dist < sight) Confront(g);
                    }
                    else if (dist < 22f && (!g.Current || Mathf.Abs(g.Current.ForwardSpeed) < 3f)) Stop(g);
                    break;
                case Phase.Stopped:
                    farT = dist > 35f ? farT + dt : 0f;
                    if (farT > 6f || timer > 240f) Resume();
                    break;
                case Phase.Confront:
                    ConfrontTick(g, target, dist, dt);
                    if (dist > 60f || timer > 25f) Attack(g);
                    else if (Mathf.Repeat(timer, 3f) < dt) g.Toast("THE " + Gang + " BLOCK YOUR WAY - WALK UP AND PARLEY [E]");
                    break;
                case Phase.Parley:
                    if (!g.Menus.IsOpen) { phase = Phase.Confront; timer = Mathf.Max(timer, 18f); }
                    break;
                case Phase.Attack:
                    AttackTick(g, target, dt);
                    if (dist > 220f || inTown) Leave();
                    break;
                case Phase.Flee:
                    if (timer > 60f) { phase = Phase.Travel; foreach (var c in cars) if (c) c.cruise = 12f; }
                    break;
            }
            BailOut(g);
        }

        AiDriver Leader() { foreach (var c in cars) if (c && c.enabled) return c; return null; }

        bool AllDown()
        {
            foreach (var w in walkers) if (w && w.Alive) return false;
            return true;
        }

        void Wiped()
        {
            foreach (var w in walkers) if (w) w.convoy = null;
            walkers.Clear(); cars.Clear(); Boss = null;
            save.deadDay = DayNight.Day;
            phase = Phase.Gone;
            if (raiders) WastelandGame.Instance?.Toast("THE " + Gang + " ARE FINISHED");
        }

        void Spawn(WastelandGame g)
        {
            var terrain = DeformableTerrain.Instance;
            for (int i = 0; i < designs.Count; i++)
            {
                var p = PointAt(travel - i * 13f, out var dir);
                var side = Vector3.Cross(Vector3.up, dir).normalized;
                p += side * (raiders ? (i % 2 == 0 ? 1.8f : -1.8f) : 2.2f);        // keep right, raiders two abreast
                p.y = terrain.Height(p.x, p.z) + 1.2f;
                var v = g.SpawnAiVehicle(designs[i], p, Quaternion.LookRotation(Flat(dir)));
                if (!v) continue;
                if (v.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = sys.fuelCapacity; sys.oil = sys.oilCapacity; sys.coolant = sys.coolantCapacity; }
                if (raiders) Armour(g, v, i);
                var ai = v.gameObject.AddComponent<AiDriver>();
                ai.cruise = speed + 1f;
                if (i == 0 || !raiders) ai.SetPath(route, Vector3.Dot(dir, route[route.Count - 1] - route[0]) >= 0f ? 1 : -1);
                else { ai.goal = AiDriver.Goal.Follow; ai.leader = cars[0]; ai.slot = new Vector3(i % 2 == 0 ? 2.2f : -2.2f, 0f, -11f * ((i + 1) / 2)); }
                cars.Add(ai);
            }
            if (cars.Count == 0) { phase = Phase.Gone; save.deadDay = DayNight.Day; }
        }

        /// <summary>Raider cars get spikes, plates, rams and the odd turret on free sockets.</summary>
        void Armour(WastelandGame g, VehicleDriver v, int i)
        {
            var r = new System.Random(seed + i * 17 + save.generation);
            var chassis = v.GetComponent<VehicleChassis>();
            if (!chassis) return;
            foreach (var s in chassis.Sockets)
            {
                if (!s.IsFree) continue;
                string key = s.accepts switch
                {
                    PartCategory.Armor => r.NextDouble() < 0.5 ? "armor_spikes" : "armor_plate",
                    PartCategory.FrontBumper => r.NextDouble() < 0.6 ? "bumper_ram" : "bumper_bull_bar",
                    PartCategory.Weapon => r.NextDouble() < 0.5 || i == 0 ? "weapon_turret_cannon" : null,
                    _ => null
                };
                if (key == null) continue;
                var part = g.SpawnPart(key, s.transform.position, s.transform.rotation);
                if (part && !s.Attach(part)) UnityEngine.Object.Destroy(part.gameObject);
            }
        }

        void Despawn()
        {
            var lead = Leader();
            if (lead) travel = Project(lead.transform.position, lead.transform.forward);
            foreach (var c in cars) if (c) UnityEngine.Object.Destroy(c.gameObject);
            foreach (var w in walkers) if (w && w.Alive) UnityEngine.Object.Destroy(w.gameObject);
            cars.Clear(); walkers.Clear(); Boss = null;
            if (phase != Phase.Gone) phase = Phase.Travel;
        }

        public void Destroy()
        {
            foreach (var c in cars) if (c) UnityEngine.Object.Destroy(c.gameObject);
            foreach (var w in walkers) if (w) UnityEngine.Object.Destroy(w.gameObject);
            cars.Clear(); walkers.Clear();
        }

        Npc Walker(WastelandGame g, int crewIndex, Vector3 near, bool aggro)
        {
            var terrain = DeformableTerrain.Instance;
            var p = near; p.y = terrain.Height(p.x, p.z) + 0.1f;
            var n = Npc.Spawn(crew[crewIndex], p, 0f, null, g.propMaterial);
            n.convoy = this; n.aggro = aggro;
            n.mode = aggro ? Npc.Mode.Fight : Npc.Mode.Stand;
            walkers.Add(n);
            return n;
        }

        // ------------------------------------------------------------------ traders

        void Stop(WastelandGame g)
        {
            var lead = Leader();
            if (!lead) return;
            phase = Phase.Stopped; timer = 0f; farT = 0f;
            lead.goal = AiDriver.Goal.Park;
            if (NpcRegistry.IsDead(crew[0].id)) return;
            var t = lead.transform;
            Boss = Walker(g, 0, t.position - t.right * 2.2f, false);
            Boss.home = Boss.transform.position; Boss.homeYaw = t.eulerAngles.y - 90f;
            MadMax.Audio.Sfx.Play("car_door", t.position, 0.6f);
        }

        void Resume()
        {
            if (Boss && Boss.Alive) { walkers.Remove(Boss); UnityEngine.Object.Destroy(Boss.gameObject); }
            Boss = null;
            var lead = Leader();
            if (lead) lead.SetPath(route, lead.dir);
            phase = Phase.Travel; timer = 0f;
        }

        // ------------------------------------------------------------------ raiders

        void Confront(WastelandGame g)
        {
            phase = Phase.Confront; timer = 0f;
            var target = g.Current ? g.Current.transform : g.Player.transform;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                c.target = target;
                if (i == 0) { c.goal = AiDriver.Goal.Chase; c.avoidTarget = true; c.chaseSpeed = 16f; }
                else { c.goal = AiDriver.Goal.Circle; c.circleRadius = 20f + i * 3f; }
            }
            MadMax.Audio.Sfx.Play("horn", cars[0].transform.position, 1f, 0.8f, 150f);
        }

        void ConfrontTick(WastelandGame g, Transform target, float dist, float dt)
        {
            var lead = Leader();
            if (!lead) return;
            if (dist < 17f) lead.goal = AiDriver.Goal.Park;
            if (!Boss && lead.Vehicle && Mathf.Abs(lead.Vehicle.ForwardSpeed) < 1f && dist < 30f && !NpcRegistry.IsDead(crew[0].id))
            {
                var t = lead.transform;
                Boss = Walker(g, 0, t.position - t.right * 2.2f, false);
                Boss.home = Boss.transform.position;
                MadMax.Audio.Sfx.Play("car_door", t.position, 0.7f);
            }
        }

        /// <summary>The player opened the parley with the boss.</summary>
        public void BeginParley() { if (phase == Phase.Confront) phase = Phase.Parley; }

        public void OnParley(Outcome o)
        {
            switch (o)
            {
                case Outcome.Failed: Attack(WastelandGame.Instance); break;
                default:
                    if (o == Outcome.Recruited) foreach (var p in crew) NpcRegistry.Get(p).disposition = Mathf.Max(NpcRegistry.Get(p).disposition, 55);
                    Leave();
                    break;
            }
        }

        /// <summary>Someone hit one of them.</summary>
        public void Provoked()
        {
            if (raiders) { if (phase != Phase.Attack) Attack(WastelandGame.Instance); }
            else if (phase != Phase.Flee) { Resume(); phase = Phase.Flee; timer = 0f; foreach (var c in cars) if (c) c.cruise = 24f; }
        }

        public void MemberDied(Npc n)
        {
            if (raiders && n == Boss && phase != Phase.Attack) Attack(WastelandGame.Instance);
        }

        void Attack(WastelandGame g)
        {
            if (!g) return;
            phase = Phase.Attack; timer = 0f; modeT = 0f;
            var target = g.Current ? g.Current.transform : g.Player.transform;
            foreach (var c in cars) { if (!c || !c.enabled) continue; c.target = target; c.goal = AiDriver.Goal.Chase; c.avoidTarget = false; c.chaseSpeed = 28f; }
            foreach (var w in walkers) if (w) w.aggro = true;
            MadMax.Audio.Sfx.Play("horn", cars.Count > 0 && cars[0] ? cars[0].transform.position : target.position, 1f, 0.7f, 150f);
            g.Toast("THE " + Gang + " ATTACK!");
        }

        void AttackTick(WastelandGame g, Transform target, float dt)
        {
            modeT += dt;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c || !c.enabled) continue;
                c.target = target;
                // alternate ramming runs and circling so they don't pile into each other
                if (i > 0) c.goal = ((int)(modeT / 6f) + i) % 3 == 0 ? AiDriver.Goal.Circle : AiDriver.Goal.Chase;
            }
            if ((fireCd -= dt) > 0f) return;
            fireCd = Mathf.Max(0.8f, 3f / Mathf.Max(1, cars.Count));
            var shooter = cars[UnityEngine.Random.Range(0, cars.Count)];
            if (!shooter || !shooter.enabled) return;
            var from = shooter.transform.position + Vector3.up * 1.6f;
            float d = Vector3.Distance(from, target.position);
            if (d < 26f && UnityEngine.Random.value < 0.3f && d > 7f) ThrowBottle(g, from, target);
            else if (d < 22f) Npc.Blast(shooter.gameObject, from, target.position + Vector3.up * 0.8f, d, HasTurret(shooter) ? 1.8f : 1f);
        }

        static bool HasTurret(AiDriver c)
        {
            foreach (var p in c.GetComponentsInChildren<VehiclePart>()) if (p.category == PartCategory.Weapon && p.Socket) return true;
            return false;
        }

        void ThrowBottle(WastelandGame g, Vector3 from, Transform target)
        {
            var rb = target.GetComponentInParent<Rigidbody>();
            var aimAt = target.position + (rb && !rb.isKinematic ? rb.linearVelocity * 0.8f : Vector3.zero);
            float T = Mathf.Clamp(Vector3.Distance(from, aimAt) / 16f, 0.5f, 1.6f);
            var v = (aimAt - from) / T - 0.5f * Physics.gravity * T;
            g.SpawnThrown("throw_molotov", from + v.normalized * 1.2f, v, true);
        }

        void Leave()
        {
            phase = Phase.Leave; timer = 0f;
            truceUntil = Time.time + 600f;
            foreach (var w in walkers) if (w) w.aggro = false;
            if (Boss && Boss.Alive) { walkers.Remove(Boss); UnityEngine.Object.Destroy(Boss.gameObject); Boss = null; }
            var lead = Leader();
            foreach (var c in cars)
            {
                if (!c || !c.enabled) continue;
                if (c == lead) c.SetPath(route, c.dir);
                else { c.goal = AiDriver.Goal.Follow; c.leader = lead; }
                c.target = null;
            }
        }

        /// <summary>Wrecked or flipped cars: the driver climbs out (raiders fight on, traders run).</summary>
        void BailOut(WastelandGame g)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c || !c.enabled || !c.Disabled) continue;
                c.Release();
                int crewIndex = Mathf.Min(i, crew.Count - 1);
                if (NpcRegistry.IsDead(crew[crewIndex].id) || walkers.Exists(w => w && w.Profile.id == crew[crewIndex].id)) continue;
                var w = Walker(g, crewIndex, c.transform.position - c.transform.right * 2f, raiders);
                if (!raiders) w.Scare(20f);
                else if (phase != Phase.Attack) Attack(g);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
