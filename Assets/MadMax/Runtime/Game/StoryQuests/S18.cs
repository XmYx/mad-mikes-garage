using System.Collections.Generic;
using MadMax.Building;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S18 A PERFECTLY LEGAL RACE in the world: Tamsin's start line (flag, sign, her spare bicycle), flags at the
    /// loop's gates, Otto's fruit stall and Peg the marshal at the market crossing. The race runs here while the quest's
    /// "race" step is current: in any vehicle or on a bike within 14 m of the start flag, a count of three, then Tamsin
    /// rides her loop on an AI bicycle (picked up and set back on her line if she crashes or sticks) while the player
    /// passes the flags in order (engines also round the water-tank flag). Anyone struck at the crossing = a foul: no
    /// medal, a marshal's ticket, the stall knocked over. Leaving the ride for 15 s or wandering 900 m off calls it off
    /// (line up again to restart). Not saved mid-race.</summary>
    public partial class WastelandGame
    {
        /// <summary>Tamsin is out on her bike (her standing body is away).</summary>
        public static bool S18RaceOn => Instance && (Instance.s18Phase == 1 || Instance.s18Phase == 2);

        int s18Phase, s18Gate, s18RivalIdx;
        float s18Count, s18Start, s18OffFor, s18RivalProg, s18RivalDown, s18CleanupAt;
        bool s18Bike, s18Foul, s18RivalHome, s18Set, s18Medal;
        AiDriver s18Rival;
        readonly List<Vector3> s18Gates = new List<Vector3>();
        readonly float[] s18Health = new float[2];
        static readonly string[] S18Folk = { "s18_otto", "s18_peg" };
        string s18Ride, s18Square;

        partial void Scene_S18()
        {
            if (!Build || !Build.Structures) return;
            Q2Put("s18_start", "flag", new Vector3(6f, 0f, 0f), 0f);
            Q2Put("s18_g1", "flag", new Vector3(6.5f, 0f, 0f), 0f);
            Q2Put("s18_g1b", "flag", new Vector3(6.5f, 0f, 0f), 0f);
            Q2Put("s18_g1b", "water_tank", new Vector3(10f, 0f, 2f), 0f);
            Q2Put("s18_g2", "flag", Vector3.zero, 0f);
            Q2Put("s18_g3", "flag", new Vector3(-6.5f, 0f, 0f), 0f);
            // Tamsin's corner: her sign, a bench and the spare bike
            Q2Put("tamsin", "sign", new Vector3(1.2f, 0f, -1.6f), 0f);
            Q2Put("tamsin", "bench", new Vector3(-1.6f, 0f, -1.4f), 0f);
            Q2Put("tamsin", "tyres", new Vector3(-3f, 0f, -2.2f), 30f);
            var pf = PrefabFor("Bicycle");
            if (pf && StoryAnchors.Has("tamsin"))
            {
                var r = Quaternion.Euler(0f, StoryAnchors.Yaw("tamsin"), 0f);
                var p = StoryAnchors.Get("tamsin") + r * new Vector3(2.6f, 0f, -1.4f); p.y = terrain.HeightNoLoad(p.x, p.z) + 0.5f;
                var v = Instantiate(pf, p, r * Quaternion.Euler(0f, 90f, 0f)).GetComponent<VehicleDriver>();
                v.name = "Tamsin's Spare Bike";
                Register(v, null);
                StoryTag.Set(v.gameObject, "s18_spare");
            }
            // the market crossing: Otto's fruit stall under an awning, crates, a slow sign
            Q2Put("s18_market", "porch_awning", new Vector3(0f, 0f, -1.4f), 0f);
            Q2Put("s18_market", "table", new Vector3(0f, 0f, -1.1f), 0f);
            Q2Put("s18_market", "crate", new Vector3(1.4f, 0f, -1.6f), 15f);
            Q2Put("s18_market", "crate", new Vector3(-1.4f, 0f, -1.5f), 70f);
            Q2Put("s18_market", "sign", new Vector3(-2.8f, 0f, 1.4f), 0f);
            Journal.Add("PLACE", "TAMSIN'S LOOP: A START FLAG BY TOWN, A FLAG OUT ON THE ROAD, ONE IN THE WASH, THE MARKET CROSSING");
        }

        bool S18Suits(VehicleDriver v) => v && v.driveable && !v.aircraft && !v.GetComponent<FlightModel>() && !v.GetComponent<BoatModel>();

        partial void Tick_S18()
        {
            S18Lines();
            if (!StoryAnchors.Has("s18_start") || !Player) return;
            // the crossing's folk stand where they are (no wandering into the road)
            if (Time.frameCount % 30 == 0) foreach (var k in S18Folk) { var n = CastBody(k); if (n) Q2Pin(n, n.home, n.homeYaw); }
            var start = StoryAnchors.Get("s18_start");
            var car = Current;
            switch (s18Phase)
            {
                case 0:
                {
                    var q = StoryLibrary.Get("S18");
                    var cur = q != null ? Story.Story.Current(q) : null;
                    if (cur == null || cur.id != "race" || !S18Suits(car) || Q2Flat(car.transform.position, start) > 14f) return;
                    s18Bike = car.GetComponent<BikeBalance>();
                    s18Gates.Clear();
                    foreach (var g in s18Bike ? new[] { "s18_g1", "s18_g2", "s18_g3", "s18_start" } : new[] { "s18_g1", "s18_g1b", "s18_g2", "s18_g3", "s18_start" })
                        s18Gates.Add(StoryAnchors.Get(g));
                    s18Phase = 1; s18Count = 3.99f; s18Gate = 0; s18Foul = false; s18RivalHome = false; s18OffFor = 0f;
                    for (int i = 0; i < S18Folk.Length; i++) { var n = CastBody(S18Folk[i]); s18Health[i] = n ? n.Health : 0f; }
                    Toast(s18Bike ? "TAMSIN: BIKE AGAINST BIKE. ON THREE." : "TAMSIN: AN ENGINE? THEN YOU ROUND THE WATER-TANK FLAG TOO. ON THREE.");
                    return;
                }
                case 1:
                {
                    if (!car || Q2Flat(car.transform.position, start) > 22f) { S18Stop("YOU LEFT THE LINE: THE RACE IS OFF", 0f); return; }
                    int before = Mathf.CeilToInt(s18Count);
                    s18Count -= Time.deltaTime;
                    if (Mathf.CeilToInt(s18Count) != before)
                    {
                        Toast(s18Count > 0f ? Mathf.CeilToInt(s18Count).ToString() : "GO!");
                        MadMax.Audio.Sfx.Play2D("beep", 0.5f, s18Count > 0f ? 1f : 1.6f);
                    }
                    if (s18Count > 0f) return;
                    s18Phase = 2; s18Start = Time.time;
                    S18Rival(start);
                    SetWaypoint(s18Gates[0], null, true);
                    return;
                }
                case 2:
                    S18Run(car, start);
                    return;
                case 3:
                    if (Time.time > s18CleanupAt) { S18Clear(); s18Phase = 0; }
                    return;
            }
        }

        /// <summary>Tamsin on her bicycle: start line → first flag → the wash → the crossing → home.</summary>
        void S18Rival(Vector3 start)
        {
            S18Clear();
            var g1 = StoryAnchors.Get("s18_g1");
            var fwd = g1 - start; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1f ? fwd.normalized : Vector3.forward;
            var right = new Vector3(fwd.z, 0f, -fwd.x);
            var p = start - right * 3f; p.y = terrain.Height(p.x, p.z) + 0.6f;
            var v = SpawnAiVehicle("Bicycle", p, Quaternion.LookRotation(fwd));
            if (!v) return;
            v.name = "Tamsin's Bike";
            var path = new List<Vector3>();
            var pts = new[] { p, g1, StoryAnchors.Get("s18_g2"), StoryAnchors.Get("s18_g3"), start };
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Q2Flat(pts[i], pts[i + 1]) / 20f));
                for (int k = 0; k < n; k++) path.Add(Vector3.Lerp(pts[i], pts[i + 1], k / (float)n));
            }
            path.Add(start);
            s18Rival = v.gameObject.AddComponent<AiDriver>();
            s18Rival.SetPath(path, 1);
            s18Rival.index = 1;
            s18Rival.cruise = 8.5f;
            s18RivalIdx = 1; s18RivalProg = Time.time; s18RivalDown = 0f;
        }

        void S18Run(VehicleDriver car, Vector3 start)
        {
            float dt = Time.deltaTime;
            // Tamsin: home when she's on the last stretch at the line; set back on her line if she crashes or sticks
            if (s18Rival && !s18RivalHome && s18Rival.path != null)
            {
                var path = s18Rival.path;
                if (s18Rival.index >= path.Count - 2 && Q2Flat(s18Rival.transform.position, start) < 16f)
                {
                    s18RivalHome = true; s18Rival.goal = AiDriver.Goal.Park;
                    Toast("TAMSIN CROSSES THE LINE");
                }
                else
                {
                    if (s18Rival.index != s18RivalIdx) { s18RivalIdx = s18Rival.index; s18RivalProg = Time.time; }
                    s18RivalDown = s18Rival.Disabled ? s18RivalDown + dt : 0f;
                    if (s18RivalDown > 3f || Time.time - s18RivalProg > 10f)
                    {
                        var v = s18Rival.Vehicle;
                        var at = path[Mathf.Clamp(s18Rival.index, 0, path.Count - 1)];
                        var next = path[Mathf.Clamp(s18Rival.index + 1, 0, path.Count - 1)];
                        var dir = next - at; dir.y = 0f;
                        if (v && v.Body)
                        {
                            v.Body.position = new Vector3(at.x, terrain.Height(at.x, at.z) + 0.6f, at.z);
                            v.Body.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir.normalized : v.transform.forward);
                            v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
                            if (v.TryGetComponent<BikeBalance>(out var bb)) bb.Remount();
                        }
                        s18RivalProg = Time.time; s18RivalDown = 0f;
                    }
                }
            }
            // the player: stay with the ride and near the loop
            s18OffFor = S18Suits(car) ? 0f : s18OffFor + dt;
            if (s18OffFor > 15f) { S18Stop("YOU LEFT YOUR RIDE: THE RACE IS CALLED OFF. LINE UP AGAIN TO RESTART", 8f); return; }
            if (car && Q2Flat(car.transform.position, start) > 900f) { S18Stop("TOO FAR OFF THE LOOP: THE RACE IS CALLED OFF", 8f); return; }
            S18Watch(car);
            if (!car || Q2Flat(car.transform.position, s18Gates[s18Gate]) > 12f) return;
            MadMax.Audio.Sfx.Play2D("beep", 0.4f, 1.3f);
            s18Gate++;
            if (s18Gate < s18Gates.Count) { SetWaypoint(s18Gates[s18Gate], null, true); Toast("FLAG " + s18Gate + "/" + (s18Gates.Count - 1)); return; }
            // home
            float t = Time.time - s18Start;
            bool won = !s18RivalHome;
            string time = Mathf.FloorToInt(t / 60f) + ":" + (t % 60f).ToString("00.0");
            if (won && !s18Foul) Story.Story.Note("s18:medal");
            if (!s18Foul) Story.Story.Note("s18:clean");
            Story.Story.Note(s18Bike ? "s18:done_bike" : "s18:done_car");
            string msg = (won ? "YOU BEAT TAMSIN HOME IN " : "TAMSIN WAS HOME FIRST. YOUR TIME ") + time + (s18Foul ? ": NO MEDAL, SOMEONE GOT HURT" : "");
            Toast(msg);
            Journal.Add("RACE", "TAMSIN'S LOOP: " + msg);
            s18Phase = 3; s18CleanupAt = Time.time + 12f;
            ClearWaypoint();
            if (s18Rival) { s18Rival.goal = AiDriver.Goal.Park; }
        }

        /// <summary>Anyone at the crossing struck by the player's vehicle (or hurt with it close by) is a foul.</summary>
        void S18Watch(VehicleDriver car)
        {
            if (s18Foul || !car || !car.Body) return;
            float speed = car.Body.linearVelocity.magnitude;
            for (int i = 0; i < S18Folk.Length; i++)
            {
                var n = CastBody(S18Folk[i]);
                bool hit = false;
                if (n)
                {
                    float d = Q2Flat(car.transform.position, n.transform.position);
                    if (speed > 3f && d < 6f)
                    {
                        var chest = n.transform.position + Vector3.up * 0.8f;
                        if ((car.Body.ClosestPointOnBounds(chest) - chest).sqrMagnitude < 0.8f) hit = true;
                    }
                    if (d < 8f && (!n.Alive || n.Health < s18Health[i] - 0.5f)) hit = true;
                }
                if (!hit) continue;
                s18Foul = true;
                Story.Story.SetFlag("s18_fouled");
                Inventory.AddItem(StoryLibrary.S18Ticket);
                if (StoryAnchors.Has("s18_market"))
                {
                    var m = StoryAnchors.Get("s18_market");
                    foreach (var p in Placeable.All)
                        if (p && p.id == "table" && IsStoryProp(p) && Q2Flat(p.transform.position, m) < 3f) { p.hits = 1; p.Dirty(); }
                }
                Toast("YOU HIT SOMEONE AT THE CROSSING. NO MEDAL TODAY: PUT IT RIGHT AFTER");
                return;
            }
        }

        void S18Stop(string why, float clearIn)
        {
            Toast(why);
            Journal.Add("RACE", "TAMSIN'S LOOP: " + why);
            ClearWaypoint();
            s18Phase = clearIn > 0f ? 3 : 0;
            s18CleanupAt = Time.time + clearIn;
            if (clearIn <= 0f) S18Clear();
            else if (s18Rival) s18Rival.goal = AiDriver.Goal.Park;
        }

        /// <summary>Tamsin gets off her racing bike (it goes back to her shed; the one the player rides stays).</summary>
        void S18Clear()
        {
            if (s18Rival)
            {
                var v = s18Rival.Vehicle;
                if (v && Current != v) Destroy(v.gameObject);
            }
            s18Rival = null;
        }

        void S18Lines()
        {
            string ride = Story.Story.Route("S18", "race"), sq = Story.Story.Route("S18", "square");
            bool medal = Story.Story.StepDone("S18", "medal");
            if (s18Set && ReferenceEquals(ride, s18Ride) && ReferenceEquals(sq, s18Square) && medal == s18Medal) return;
            s18Set = true; s18Ride = ride; s18Square = sq; s18Medal = medal;
            var q = StoryLibrary.Get("S18"); if (q != null) q.payoff = StoryLibrary.S18Payoff();
        }
    }
}
