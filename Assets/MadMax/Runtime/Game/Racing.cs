using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Races and stunts. Every town board has one event a day ([T]): a road race to the nearest town against
    /// three rival drivers (entry fee, the winner takes the purse), a bike trial through off-road gates around town, an
    /// air race around the nearest airfield, or (cities) the scrap arena: bet, then outlast three armoured wreckers.
    /// Gates show as a marker and the waypoint; best times and the longest hang time off a jump are kept (saved).
    /// Authority-side only; not saved mid-race.</summary>
    public class Racing : MonoBehaviour
    {
        public enum Kind { Road, Trial, Air, Arena }

        public class Event
        {
            public Kind kind; public int town, fee, purse; public string title, record;
            public readonly List<Vector3> gates = new List<Vector3>();
            public readonly List<Vector3> path = new List<Vector3>();        // road races: the whole route for the rivals
            public Vector3 start; public float gateRadius = 14f, limit = 600f;
        }

        public static Racing Instance { get; private set; }
        /// <summary>Best times (s) per event record key, and "hang" = the longest jump (saved).</summary>
        public static readonly Dictionary<string, float> Best = new Dictionary<string, float>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Instance = null; Best.Clear(); }

        public Event Active { get; private set; }
        int gate;
        float startedAt, countdown = -1f, offFor, airT, cleanupAt;
        bool running;
        int beat;                                                    // rivals home before the player
        readonly List<AiDriver> rivals = new List<AiDriver>();
        readonly HashSet<AiDriver> home = new HashSet<AiDriver>();
        WastelandGame game;

        void Awake() { Instance = this; game = GetComponent<WastelandGame>(); }

        static WorldGen World => DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // ------------------------------------------------------------------ offers

        /// <summary>Today's event at a town's board (gates are laid out when it is accepted).</summary>
        public static Event Offer(int town)
        {
            var w = World;
            if (w == null || town < 0 || town >= w.settlements.Count) return null;
            var st = w.settlements[town];
            int day = DayNight.Day;
            var e = new Event { town = town };
            var sites = new List<Site>();
            w.SitesNear(new Vector3(st.pos.x, 0f, st.pos.y), 2500f, sites);
            Site airfield = null;
            foreach (var s in sites) if (s.kind == SiteKind.Airfield) { airfield = s; break; }
            int roll = (town * 7 + day) % 3;
            if (st.kind == Biome.City && day % 2 == 0) { e.kind = Kind.Arena; e.fee = 30; e.purse = 90; e.title = "ARENA: OUTLAST 3 WRECKERS"; e.limit = 300f; }
            else if (roll == 0 && airfield != null) { e.kind = Kind.Air; e.fee = 25; e.purse = 110; e.title = "AIR RACE AT THE AIRFIELD"; e.gateRadius = 26f; }
            else if (roll == 1) { e.kind = Kind.Trial; e.fee = 15; e.purse = 60; e.title = "BIKE TRIAL ROUND " + Market.TownName(st); e.gateRadius = 8f; e.limit = 420f; }
            else { e.kind = Kind.Road; e.fee = 20; e.purse = 80; var sis = TownQuests.Sister(town); e.title = "ROAD RACE TO " + (sis != null ? Market.TownName(sis) : "THE NEXT TOWN"); }
            e.record = e.kind + ":" + town;
            return e;
        }

        public string Prompt(int town)
        {
            if (Active != null) return Active.town == town ? "  [T] QUIT THE RACE" : "";
            var e = Offer(town);
            return e == null ? "" : "  [T] " + e.title + " (FEE " + e.fee + ", PURSE " + e.purse + ")";
        }

        /// <summary>[T] at the board: sign up (pay the fee), or quit the event in progress.</summary>
        public void SignUp(int town)
        {
            if (Active != null) { if (Active.town == town) End("YOU QUIT THE RACE", false); return; }
            var e = Offer(town);
            if (e == null) return;
            if (!Lay(e)) { game.Toast("NO COURSE TODAY"); return; }
            if (!game.Inventory.TrySpend(MadMax.Items.ResourceType.Scrap, e.fee)) { game.Toast("THE FEE IS " + e.fee + " SCRAP"); return; }
            Active = e; gate = 0; running = false; countdown = -1f; beat = 0; home.Clear();
            game.SetWaypoint(e.start, "THE START", true);
            game.Toast(e.title + ": " + (e.kind == Kind.Trial ? "BRING A BIKE" : e.kind == Kind.Air ? "BRING A FLYING MACHINE" : "BRING A CAR") + " TO THE START (WAYPOINT)");
            Journal.Add("RACE", e.title + ", FEE " + e.fee + " PAID");
        }

        /// <summary>Lay out the course.</summary>
        bool Lay(Event e)
        {
            var w = World;
            var st = w.settlements[e.town];
            var centre = new Vector3(st.pos.x, 0f, st.pos.y);
            var t = DeformableTerrain.Instance;
            var r = new System.Random(e.town * 131 + DayNight.Day);
            switch (e.kind)
            {
                case Kind.Road:
                {
                    var sis = TownQuests.Sister(e.town);
                    if (sis == null) return false;
                    var route = new List<Vector3>();
                    RoadRoute.Find(w, centre, new Vector3(sis.pos.x, 0f, sis.pos.y), route);
                    if (route.Count < 3) return false;
                    // line up on the open road out of town, clear of the streets and the start yard
                    int s0 = 1;
                    while (s0 < route.Count - 2 && (Flat(route[s0] - centre) < st.radius * 0.8f || w.YardWeight(route[s0].x, route[s0].z) > 0f)) s0++;
                    e.start = route[s0];
                    for (int i = s0; i < route.Count; i++) e.path.Add(route[i]);
                    float acc = 0f;
                    for (int i = s0 + 1; i < route.Count; i++)
                    {
                        acc += Flat(route[i] - route[i - 1]);
                        if (acc >= 250f || i == route.Count - 1) { e.gates.Add(route[i]); acc = 0f; }
                    }
                    if (e.gates.Count == 0) return false;
                    e.limit = Mathf.Max(240f, e.gates.Count * 250f / 8f);                  // 8 m/s average is a crawl
                    break;
                }
                case Kind.Trial:
                {
                    float a0 = (float)r.NextDouble() * 360f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (a0 + i * 45f) * Mathf.Deg2Rad, rad = st.radius + 90f + (float)r.NextDouble() * 90f;
                        var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                        if (t && t.WaterDepthNoLoad(p.x, p.z) > 0.3f) p = centre + (p - centre) * 0.8f;
                        e.gates.Add(p);
                    }
                    e.gates.Add(e.gates[0]);                                                     // back to the first gate
                    e.start = e.gates[0] + (centre - e.gates[0]).normalized * 20f;
                    break;
                }
                case Kind.Air:
                {
                    var sites = new List<Site>();
                    w.SitesNear(centre, 2500f, sites);
                    Site af = null;
                    foreach (var s in sites) if (s.kind == SiteKind.Airfield) { af = s; break; }
                    if (af == null) return false;
                    var c = new Vector3(af.pos.x, 0f, af.pos.y);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i * 45f + 20f) * Mathf.Deg2Rad, rad = 380f + 60f * Mathf.Sin(i * 1.7f);
                        var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                        p.y = (t ? t.HeightNoLoad(p.x, p.z) : 0f) + 45f + 15f * Mathf.Sin(i * 2.3f);   // in the air
                        e.gates.Add(p);
                    }
                    e.start = c;
                    break;
                }
                case Kind.Arena:
                {
                    float a = (float)r.NextDouble() * Mathf.PI * 2f;
                    e.start = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (st.radius + 70f);
                    break;
                }
            }
            if (t) e.start.y = t.HeightNoLoad(e.start.x, e.start.z);
            return true;
        }

        bool Suits(VehicleDriver v)
        {
            if (!v || Active == null) return false;
            bool bike = v.GetComponent<BikeBalance>(), air = v.GetComponent<FlightModel>();
            return Active.kind == Kind.Trial ? bike : Active.kind == Kind.Air ? air : !air && v.driveable;
        }

        // ------------------------------------------------------------------ running

        void Update()
        {
            if (!game || !game.Player || !game.Ready) return;
            HangTime();
            if (cleanupAt > 0f && Time.time > cleanupAt) Cleanup(false);
            if (Active == null) return;
            var car = game.Current;
            if (!running)
            {
                if (!Suits(car) || Flat(car.transform.position - Active.start) > 30f) { countdown = -1f; return; }
                if (countdown < 0f) { countdown = 3.99f; SpawnField(car); }
                int before = Mathf.CeilToInt(countdown);
                countdown -= Time.deltaTime;
                if (Mathf.CeilToInt(countdown) != before) { game.Toast(countdown > 0f ? Mathf.CeilToInt(countdown).ToString() : "GO!"); MadMax.Audio.Sfx.Play2D("beep", 0.5f, countdown > 0f ? 1f : 1.6f); }
                if (countdown > 0f) return;
                running = true; startedAt = Time.time; offFor = 0f;
                foreach (var ai in rivals) if (ai) ai.enabled = true;
                NextGate();
                return;
            }
            float elapsed = Time.time - startedAt;
            if (elapsed > Active.limit) { End("OUT OF TIME", false); return; }
            if (Active.kind == Kind.Arena) { ArenaTick(car); return; }
            offFor = Suits(car) ? 0f : offFor + Time.deltaTime;
            if (offFor > 12f) { End("YOU LEFT YOUR RIDE: DISQUALIFIED", false); return; }
            // rivals crossing the line
            if (Active.gates.Count > 0)
            {
                var last = Active.gates[Active.gates.Count - 1];
                foreach (var ai in rivals)
                    if (ai && !home.Contains(ai) && Flat(ai.transform.position - last) < 25f && ai.path != null && ai.index >= ai.path.Count - 2)
                    {
                        home.Add(ai); beat++; ai.goal = AiDriver.Goal.Park;
                        game.Toast("A RIVAL CROSSED THE LINE (" + beat + ")");
                    }
            }
            if (!car) return;
            var g = Active.gates[gate];
            float d = Active.kind == Kind.Air ? Vector3.Distance(car.transform.position, g) : Flat(car.transform.position - g);
            if (d > Active.gateRadius) return;
            MadMax.Audio.Sfx.Play2D("beep", 0.4f, 1.3f);
            gate++;
            if (gate >= Active.gates.Count) { Finish(elapsed); return; }
            NextGate();
        }

        void NextGate()
        {
            if (Active.kind == Kind.Arena || gate >= Active.gates.Count) return;
            game.SetWaypoint(Active.gates[gate], null, true);
        }

        /// <summary>Line up the field: rival cars beside the player (road race), wreckers around the arena.</summary>
        void SpawnField(VehicleDriver car)
        {
            Cleanup(true);
            if (Active.kind != Kind.Road && Active.kind != Kind.Arena) return;
            string[] pool = Active.kind == Kind.Arena ? new[] { "Scavenger", "Pickup", "Interceptor" } : new[] { "Interceptor", "Coupe", "Sedan", "Pickup" };
            var r = new System.Random(Active.town * 17 + DayNight.Day);
            var t = DeformableTerrain.Instance;
            var fwd = car.transform.forward; fwd.y = 0f; fwd.Normalize();
            if (Active.kind == Kind.Road && Active.gates.Count > 0) { var to = Active.gates[0] - car.transform.position; to.y = 0f; if (to.sqrMagnitude > 1f) fwd = to.normalized; }
            var side = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p; Quaternion q;
                if (Active.kind == Kind.Road) { p = car.transform.position + side * (i == 0 ? 4f : -4f) - fwd * (i == 2 ? 8f : 0f); q = Quaternion.LookRotation(fwd); }
                else { var dir = Quaternion.Euler(0f, 120f * i + 60f, 0f) * fwd; p = car.transform.position + dir * 45f; q = Quaternion.LookRotation(-dir); }
                // never inside another vehicle or a wall: step back down the road until the spot is clear
                for (int tries = 0; tries < 6; tries++)
                {
                    p.y = (t ? t.HeightNoLoad(p.x, p.z) : p.y) + 1.2f;
                    if (!Physics.CheckBox(p + Vector3.up * 0.3f, new Vector3(1.2f, 0.8f, 2.6f), q, ~(1 << Layers.Terrain), QueryTriggerInteraction.Ignore)) break;
                    p -= (Active.kind == Kind.Road ? fwd : q * Vector3.forward) * 7f;
                }
                var v = game.SpawnAiVehicle(pool[r.Next(pool.Length)], p, q);
                if (!v) continue;
                if (v.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = sys.fuelCapacity; sys.oil = sys.oilCapacity; sys.coolant = sys.coolantCapacity; }
                var ai = v.gameObject.AddComponent<AiDriver>();
                if (Active.kind == Kind.Road)
                {
                    var path = new List<Vector3> { p };
                    path.AddRange(Active.path);                                                  // the road itself, not just the gates
                    ai.SetPath(path, 1);
                    ai.index = 1;                                                               // from the start line (SetPath looks two points ahead)
                    ai.cruise = 19f + (float)r.NextDouble() * 6f;                              // some are quicker than others
                }
                else
                {
                    if (v.TryGetComponent<VehicleArmor>(out var kit)) kit.RandomKit(r.Next(), 0.3f);
                    ai.goal = AiDriver.Goal.Chase; ai.target = car.transform; ai.chaseSpeed = 18f;
                }
                ai.enabled = false;                                                           // held at the line until GO
                rivals.Add(ai);
            }
        }

        void ArenaTick(VehicleDriver car)
        {
            int alive = 0;
            foreach (var ai in rivals) if (ai && !ai.Disabled) { alive++; if (car) ai.target = car.transform; }
            if (alive == 0) { Finish(Time.time - startedAt); return; }
            bool wrecked = !car || car.transform.up.y < 0.3f;
            if (!wrecked)
                foreach (var p in car.GetComponentsInChildren<VehiclePart>())
                    if (p.category == PartCategory.Engine && p.Socket && p.damage >= 0.98f) { wrecked = true; break; }
            offFor = wrecked ? offFor + Time.deltaTime : 0f;
            if (offFor > 6f) End("WRECKED IN THE ARENA", false);
        }

        void Finish(float seconds)
        {
            var e = Active;
            int place = e.kind == Kind.Road ? beat + 1 : 1;
            int pay = place == 1 ? e.purse : place == 2 ? e.fee : 0;
            if (pay > 0) { game.Inventory.Add(MadMax.Items.ResourceType.Scrap, pay); MadMax.Audio.Sfx.Play2D("cash", 0.8f); }
            bool record = !Best.TryGetValue(e.record, out var best) || seconds < best;
            if (place == 1 && record) Best[e.record] = seconds;
            string time = Mathf.FloorToInt(seconds / 60f) + ":" + (seconds % 60f).ToString("00.0");
            string what = e.kind == Kind.Arena ? "ARENA WON" : place == 1 ? "YOU WON" : "PLACE " + place;
            End(what + " IN " + time + (pay > 0 ? " (+" + pay + " SCRAP)" : "") + (place == 1 && record ? " NEW RECORD" : ""), true);
            if (place == 1) { game.Stats.Practice(MadMax.RPG.Skill.Driving, 10f); NpcRegistry.Reputation = Mathf.Min(100, NpcRegistry.Reputation + 1); }
        }

        void End(string message, bool finished)
        {
            game.Toast(message);
            Journal.Add("RACE", (Active != null ? Active.title + ": " : "") + message);
            Active = null; running = false; countdown = -1f;
            game.ClearWaypoint();
            foreach (var ai in rivals) if (ai) { ai.goal = AiDriver.Goal.Park; ai.enabled = true; }
            cleanupAt = Time.time + (finished ? 45f : 20f);
        }

        /// <summary>Rivals and wreckers leave once the player is away from them (or at once, lining up a new race).</summary>
        void Cleanup(bool all)
        {
            var me = game.Current ? game.Current.transform.position : game.Player.transform.position;
            for (int i = rivals.Count - 1; i >= 0; i--)
            {
                var ai = rivals[i];
                if (!ai) { rivals.RemoveAt(i); continue; }
                if (!all && Flat(ai.transform.position - me) < 60f) continue;
                if (game.Current == ai.Vehicle) { rivals.RemoveAt(i); continue; }                // the player took it
                Destroy(ai.gameObject); rivals.RemoveAt(i);
            }
            cleanupAt = rivals.Count > 0 ? Time.time + 10f : 0f;
        }

        // ------------------------------------------------------------------ stunts

        /// <summary>Hang time: all wheels off the ground for more than a second, landed on the wheels.</summary>
        void HangTime()
        {
            var car = game.Current;
            if (!car || car.GetComponent<FlightModel>()) { airT = 0f; return; }
            if (car.WheelsDown == 0 && car.Body && car.Body.linearVelocity.magnitude > 6f) { airT += Time.deltaTime; return; }
            if (airT > 1f && car.transform.up.y > 0.6f)
            {
                bool best = !Best.TryGetValue("hang", out var b) || airT > b;
                if (best) Best["hang"] = airT;
                game.Toast("HANG TIME " + airT.ToString("0.0") + " S" + (best ? " - YOUR BEST!" : "  (BEST " + b.ToString("0.0") + ")"));
                game.Stats.Practice(MadMax.RPG.Skill.Driving, airT);
            }
            airT = 0f;
        }

        // ------------------------------------------------------------------ HUD, save

        /// <summary>The race line for the HUD (null when no event).</summary>
        public string Status
        {
            get
            {
                if (Active == null) return null;
                if (!running) return Active.title + ": " + (countdown > 0f ? Mathf.CeilToInt(countdown).ToString() : "LINE UP AT THE START");
                float s = Time.time - startedAt;
                string time = Mathf.FloorToInt(s / 60f) + ":" + (s % 60f).ToString("00.0");
                if (Active.kind == Kind.Arena) { int alive = 0; foreach (var ai in rivals) if (ai && !ai.Disabled) alive++; return "ARENA  " + alive + " LEFT  " + time; }
                return Active.title + "  GATE " + (gate + 1) + "/" + Active.gates.Count + "  " + time + (Active.kind == Kind.Road ? "  P" + (beat + 1 + Ahead()) : "");
            }
        }

        /// <summary>Rivals nearer the finish than the player right now (by the route index each is heading for).</summary>
        int Ahead()
        {
            var car = game.Current;
            if (!car || Active == null || Active.path.Count == 0) return 0;
            int mine = NearestOnPath(car.transform.position);
            int n = 0;
            foreach (var ai in rivals) if (ai && !home.Contains(ai) && NearestOnPath(ai.transform.position) > mine) n++;
            return n;
        }

        int NearestOnPath(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Active.path.Count; i++) { float d = Flat(Active.path[i] - p); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>Where the next gate is (for the HUD marker).</summary>
        public bool NextGatePos(out Vector3 p)
        {
            p = default;
            if (Active == null || Active.kind == Kind.Arena) return false;
            p = running && gate < Active.gates.Count ? Active.gates[gate] : Active.start;
            return true;
        }

        public static List<string> Save() { var l = new List<string>(); foreach (var kv in Best) l.Add(kv.Key + "=" + kv.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)); return l; }
        public static void Load(List<string> l)
        {
            Best.Clear();
            if (l == null) return;
            foreach (var s in l)
            {
                int i = s.LastIndexOf('=');
                if (i > 0 && float.TryParse(s.Substring(i + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) Best[s.Substring(0, i)] = v;
            }
        }
    }
}
