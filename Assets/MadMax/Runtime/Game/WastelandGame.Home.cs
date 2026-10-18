using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-03: the base's voice on the radio (the player's stations away from them report
    /// a stall, power back and a finished queue on the home frequency), one HOME ledger (workshop, pantry, power, water,
    /// wear per claim) and road-news wrecks that other scavengers pick over when the player is late.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ workshop alerts on the radio

        /// <summary>0 idle, 1 working, 2 stalled for power, 3 goods on the tray — per station, last seen state.</summary>
        readonly Dictionary<CraftingStation, int> stationState = new Dictionary<CraftingStation, int>();

        /// <summary>The last home-frequency message (tests, HUD).</summary>
        public string LastHomeCall { get; private set; }

        /// <summary>Home calls heard on the handheld radio in the pack (tests).</summary>
        public int HandRadioCalls { get; private set; }

        public static int StationState(CraftingStation st) => st.Busy ? (st.Powered ? 1 : 2) : st.TrayCount > 0 ? 3 : 0;

        void UpdateHomeRadio()
        {
            var me = FocusPos;
            foreach (var st in CraftingStation.All)
            {
                if (!st) continue;
                var p = st.GetComponent<Placeable>();
                if (!p || p.owner != Stats.name) continue;
                int now = StationState(st);
                if (!stationState.TryGetValue(st, out int was)) { stationState[st] = now; continue; }
                if (now == was) continue;
                stationState[st] = now;
                if (Flat(st.transform.position - me) < 40f) continue;                              // in earshot: the player sees it
                string msg = HomeCall(st, was, now);
                if (msg == null) continue;
                var voice = HomeVoice(st.transform.position);                                      // a companion on post says it
                if (voice) MadMax.Npc.Companions.Relay(this, voice, msg.Replace("HOME FREQUENCY: ", ""));
                else SendHomeCall(msg, st.transform.position);
            }
            if (stationState.Count > 64)
            {
                var dead = new List<CraftingStation>();
                foreach (var k in stationState.Keys) if (!k) dead.Add(k);
                foreach (var k in dead) stationState.Remove(k);
            }
        }

        /// <summary>The line a station sends when its state changes (null: nothing worth a call).</summary>
        public static string HomeCall(CraftingStation st, int was, int now)
        {
            string what = "THE " + st.title;
            if (now == 2) return "HOME FREQUENCY: " + what + " HAS LOST POWER. THE QUEUE IS STALLED";
            if (was == 2 && now == 1) return "HOME FREQUENCY: POWER IS BACK, " + what + " IS WORKING AGAIN";
            if (was == 1 && now == 3) return "HOME FREQUENCY: " + what + " IS DONE. " + st.TrayCount + " ON THE TRAY";
            return null;
        }

        public void SendHomeCall(string msg, Vector3 at)
        {
            LastHomeCall = msg;
            Journal.Add("HOME", msg.Replace("HOME FREQUENCY: ", ""));
            MadMax.Audio.RadioNetwork.Flash(msg, 30f, at, false);
            if (Inventory.GetItem(SafetyTools.HandRadio) > 0 && !MadMax.Audio.RadioNetwork.Heard())
            {
                // the handheld in the pack crackles with the home frequency
                MadMax.Audio.Sfx.Play2D("beep", 0.3f, 1.3f);
                Toast(msg);
                HandRadioCalls++;
            }
        }

        // ------------------------------------------------------------------ the home ledger

        /// <summary>One "how is my base" summary per claim flag the player owns (plus the pantry), for the journal's HOME
        /// section: workshop jobs, days of food, power produced / drawn and stored, water held, pieces worn.</summary>
        public void HomeLedger(List<string> into) => HomeLedger(into, null);

        /// <summary>What ENTER on a HOME line does: a waypoint (<see cref="at"/>, named <see cref="what"/>) and a longer
        /// note (<see cref="hint"/>: the worn pieces with their repair cost, what rots first).</summary>
        public struct LedgerAction { public bool on; public Vector3 at; public string what, hint; }

        /// <summary>The ledger with an action per line (<paramref name="acts"/> parallel to <paramref name="into"/>): a
        /// stalled station → waypoint to it; WEATHERED → the pieces and their repair cost, waypoint to the worst;
        /// PANTRY → what rots first, waypoint to the nearest preserving station (smokehouse, cannery, root cellar…).</summary>
        public void HomeLedger(List<string> into, List<LedgerAction> acts)
        {
            into.Clear(); acts?.Clear();
            void Act(Vector3 at, string what, string hint) { if (acts == null) return; while (acts.Count < into.Count - 1) acts.Add(default); acts.Add(new LedgerAction { on = true, at = at, what = what, hint = hint }); }
            var me = FocusPos;
            foreach (var claim in ClaimFlag.All)
            {
                if (!claim || claim.Owner != Stats.name) continue;
                var c = claim.transform.position;
                float prod = 0f, use = 0f, wh = 0f, whMax = 0f, water = 0f, waterMax = 0f; int stalled = 0, shed = 0;
                var counted = new HashSet<int>();
                foreach (var n in UtilityNode.All)
                {
                    if (!n || !claim.Inside(n.transform.position)) continue;
                    if ((n.kinds & UtilityKind.Power) != 0)
                    {
                        prod += n.produce; use += n.demand;
                        wh += n.batteryCharge; whMax += n.batteryWh;
                        if (n.Shed || n.Overloaded) shed++;
                    }
                    if ((n.kinds & UtilityKind.Water) != 0 && counted.Add(n.waterNet < 0 ? -1 - UtilityNode.All.IndexOf(n) : n.waterNet))
                    {
                        water += UtilityGrid.NetWater(n, out _); waterMax += UtilityGrid.NetCapacity(n);
                    }
                }
                int working = 0, ready = 0;
                CraftingStation stall = null, done = null;
                foreach (var st in CraftingStation.All)
                {
                    if (!st || !claim.Inside(st.transform.position)) continue;
                    int s = StationState(st);
                    if (s == 1) working++; else if (s == 2) { stalled++; stall = st; } else if (s == 3) { ready++; done = st; }
                }
                int worn = BaseUpkeep.WornIn(claim);
                into.Add("HOME " + Mathf.RoundToInt(Flat(c - me)) + " M: WORKSHOP " + working + " WORKING" + (stalled > 0 ? ", " + stalled + " STALLED" : "") + (ready > 0 ? ", " + ready + " READY" : ""));
                if (stall) Act(stall.transform.position, stall.title + " (STALLED)", "ENTER: WAYPOINT TO THE STALLED " + stall.title + " - IT NEEDS POWER OR FUEL");
                else if (done) Act(done.transform.position, done.title + " (READY)", "ENTER: WAYPOINT TO THE " + done.title + " - COLLECT THE TRAY WITH " + Controls.Name(Controls.Act.Second));
                else Act(c, "HOME", "ENTER: SET A WAYPOINT HOME");
                if (prod > 0f || use > 0f || whMax > 0f)
                    into.Add("  POWER " + Mathf.RoundToInt(prod) + " W IN / " + Mathf.RoundToInt(use) + " W OUT" + (whMax > 0f ? ", BATTERY " + Mathf.RoundToInt(100f * wh / whMax) + "%" : "") + (shed > 0 ? ", " + shed + " SHED" : ""));
                if (waterMax > 0f) into.Add("  WATER " + Mathf.RoundToInt(water) + " / " + Mathf.RoundToInt(waterMax) + " L");
                if (NeighbourLedgerLine(claim) is string nl) into.Add(nl);                                // who keeps an eye on it (Nightly1017)
                into.Add("  " + (worn > 0 ? worn + (worn == 1 ? " PIECE" : " PIECES") + " WEATHERED (REPAIR IN BUILD MODE)" : "EVERY PIECE SOUND"));
                if (worn > 0) WornAction(claim, Act);
            }
            Pantry(Mathf.Max(1, Weather.DaysPerSeason), out float keeps, out float rots);
            into.Add("PANTRY " + keeps.ToString("0.#") + " DAYS THAT KEEP" + (rots >= 0.5f ? ", " + rots.ToString("0.#") + " THAT ROT FIRST" : ""));
            if (acts != null) PantryAction(Act);
            if (acts != null) while (acts.Count < into.Count) acts.Add(default);
        }

        /// <summary>WEATHERED: the worn pieces (worst first) with what a repair takes; waypoint to the worst.</summary>
        void WornAction(ClaimFlag claim, System.Action<Vector3, string, string> act)
        {
            var worn = new List<Placeable>();
            foreach (var p in Placeable.All) if (p && p.hits < p.MaxHits && claim.Inside(p.transform.position) && !p.GetComponentInParent<Rigidbody>()) worn.Add(p);
            if (worn.Count == 0) return;
            worn.Sort((a, b) => (a.hits / (float)a.MaxHits).CompareTo(b.hits / (float)b.MaxHits));
            var sb = new System.Text.StringBuilder("WORN: ");
            for (int i = 0; i < worn.Count && i < 6; i++)
            {
                var def = FurnitureLibrary.Get(worn[i].id);
                if (def == null) continue;
                float missing = 1f - worn[i].hits / (float)def.hits;
                if (i > 0) sb.Append("; ");
                sb.Append(def.name).Append(' ').Append(Words.Condition(worn[i].hits / (float)def.hits)).Append(" (");
                bool first = true;
                foreach (var (t, n) in def.cost) { if (!first) sb.Append(", "); first = false; sb.Append(Mathf.Max(1, Mathf.CeilToInt(n * missing * 0.5f))).Append(' ').Append(ResourceInfo.Name(t)); }
                sb.Append(')');
            }
            if (worn.Count > 6) sb.Append("; +").Append(worn.Count - 6).Append(" MORE");
            sb.Append(". ENTER: WAYPOINT TO THE WORST, ").Append(Controls.Name(Controls.Act.BuildRepair)).Append(" IN BUILD MODE REPAIRS");
            act(worn[0].transform.position, "WORN " + (FurnitureLibrary.Get(worn[0].id)?.name ?? "PIECE"), sb.ToString());
        }

        static readonly string[] Preserving = { "smokehouse", "cannery", "drying_rack", "crock" };

        /// <summary>PANTRY: what rots first in the pack and the player's stores, and the nearest owned way to keep it
        /// (a preserving station or a root cellar / cold store); waypoint there.</summary>
        void PantryAction(System.Action<Vector3, string, string> act)
        {
            var rot = new Dictionary<string, int>();
            void Count(Inventory inv, float spoil)
            {
                foreach (var kv in inv.Items)
                {
                    var f = FoodLibrary.Get(kv.Key);
                    if (f == null || kv.Value <= 0 || f.spoilMinutes <= 0f || spoil <= 0f || kv.Key == "food_rotten") continue;
                    if (f.spoilMinutes / spoil >= Mathf.Max(1, Weather.DaysPerSeason) * DayNight.DayMinutes) continue;
                    rot.TryGetValue(kv.Key, out int n); rot[kv.Key] = n + kv.Value;
                }
            }
            Count(Inventory, 1f);
            foreach (var c in Container.All) { if (!c) continue; var pl = c.GetComponent<Placeable>(); if (pl && pl.owner == Stats.name) Count(c.inventory, c.SpoilFactor); }
            var me = FocusPos;
            Vector3 best = Vector3.zero; string what = null; float bd = float.MaxValue;
            foreach (var st in CraftingStation.All)
            {
                if (!st || System.Array.IndexOf(Preserving, st.type) < 0) continue;
                var pl = st.GetComponent<Placeable>();
                if (!pl || pl.owner != Stats.name) continue;
                float d = (st.transform.position - me).sqrMagnitude;
                if (d < bd) { bd = d; best = st.transform.position; what = st.title; }
            }
            foreach (var c in Container.All)
            {
                if (!c || c.SpoilFactor >= 0.6f) continue;
                var pl = c.GetComponent<Placeable>();
                if (!pl || pl.owner != Stats.name) continue;
                float d = (c.transform.position - me).sqrMagnitude;
                if (d < bd) { bd = d; best = c.transform.position; what = c.title; }
            }
            var sb = new System.Text.StringBuilder();
            if (rot.Count == 0) sb.Append("NOTHING ROTS BEFORE THE SEASON TURNS.");
            else
            {
                sb.Append("ROTS FIRST: ");
                int i = 0;
                foreach (var kv in rot) { if (i++ >= 6) { sb.Append(", ..."); break; } if (i > 1) sb.Append(", "); sb.Append(ItemCatalog.Name(kv.Key)).Append(" X").Append(kv.Value); }
                sb.Append(". SMOKE IT, TIN IT, DRY IT OR PUT IT IN A ROOT CELLAR.");
            }
            if (what != null) { sb.Append(" ENTER: WAYPOINT TO YOUR ").Append(what); act(best, what, sb.ToString()); }
            else { sb.Append(" BUILD A SMOKEHOUSE, CANNERY OR ROOT CELLAR."); act(Vector3.zero, null, sb.ToString()); }
        }

        // ------------------------------------------------------------------ wrecks from the news get picked over

        /// <summary>Wrecks left by off-screen skirmishes: x, y, z and the world day they were left (w); once the player
        /// has come near, w = −1000 − the day it was found (<see cref="Found"/>). Saved as <c>SaveData.roadWrecks</c>.</summary>
        public readonly List<Vector4> RoadWrecks = new List<Vector4>();
        /// <summary>Days before others start stripping a news wreck, and when they are done with it.</summary>
        public const float ScavengeFrom = 1.5f, ScavengeDone = 4f;
        const float FoundMark = -1000f;
        public static bool Found(Vector4 w) => w.w <= FoundMark;

        public void NoteRoadWreck(Vector3 at)
        {
            RoadWrecks.Add(new Vector4(at.x, at.y, at.z, DayNight.TotalDays));
            if (RoadWrecks.Count > 24) RoadWrecks.RemoveAt(0);
        }

        /// <summary>Share of what the wreck still had that is gone after <paramref name="age"/> days.</summary>
        public static float ScavengedShare(float age) => Mathf.Clamp01((age - ScavengeFrom) / (ScavengeDone - ScavengeFrom)) * 0.85f;

        /// <summary>How fast others get to a wreck by season (0 summer … 3 spring): summer roads are busy, nobody
        /// travels in winter, autumn and spring in between.</summary>
        public static float SeasonPace(int season) => season switch { 0 => 1.35f, 2 => 0.4f, _ => 0.85f };

        /// <summary><see cref="ScavengedShare(float)"/> on the season's clock: the wreck ages faster for scavengers in
        /// summer and hardly at all in winter.</summary>
        public static float ScavengedShare(float age, int season) => ScavengedShare(ScavengeFrom + Mathf.Max(0f, age - ScavengeFrom) * SeasonPace(season));

        /// <summary>... and by road traffic (<see cref="TrafficPace"/>): a wreck on a paved highway is stripped fastest.</summary>
        public static float ScavengedShare(float age, int season, float traffic) => ScavengedShare(ScavengeFrom + Mathf.Max(0f, age - ScavengeFrom) * SeasonPace(season) * traffic);

        /// <summary>How busy the road past a wreck is: a paved highway 1.3, a dirt track 1, off the road 0.7.</summary>
        public float TrafficPace(Vector3 at)
        {
            if (World == null) return 1f;
            var s = World.Sample(at.x, at.z);
            return s.roadDist < 8f ? (s.paved ? 1.3f : 1f) : 0.7f;
        }

        /// <summary>Winter: the tank is frozen solid with the wreck (nobody drains it).</summary>
        public static bool FuelFrozen(int season) => season == 2;

        void UpdateRoadWrecks()
        {
            if (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) return;
            var me = FocusPos;
            var dir = MadMax.Npc.NpcDirector.Instance;
            for (int i = RoadWrecks.Count - 1; i >= 0; i--)
            {
                var w = RoadWrecks[i];
                var at = new Vector3(w.x, w.y, w.z);
                string scav = ScavengerId(at);
                if (Found(w))                                                                       // found: the scavenger stays a day
                {
                    if (DayNight.TotalDays > FoundMark - w.w + 1f) { RoadWrecks.RemoveAt(i); if (dir) dir.Scavengers.Remove(scav); HaulOff(scav, at); }
                    continue;
                }
                float age = DayNight.TotalDays - w.w;
                if (age > 12f) { RoadWrecks.RemoveAt(i); continue; }
                if (Flat(at - me) > 90f) continue;
                RoadWrecks[i] = new Vector4(w.x, w.y, w.z, FoundMark - DayNight.TotalDays);
                bool onSite = !BurnedNear(at) && age >= ScavengeFrom && age < ScavengeFrom + (ScavengeDone - ScavengeFrom) / (SeasonPace(MadMax.World.Weather.Season) * TrafficPace(at)) && dir;
                int taken = Scavenge(at, age, Mathf.RoundToInt(w.x * 31f + w.z * 7f), onSite ? scav : null);
                if (onSite)
                {
                    dir.Scavengers[scav] = at + new Vector3(3f, 0f, 2f);
                    Toast(MadMax.Npc.Trade.HaulCount(scav) > 0 ? "SOMEONE IS ALREADY PICKING AT THE WRECK - THEY MIGHT SELL WHAT THEY TOOK" : "SOMEONE IS ALREADY PICKING AT THE WRECK");
                }
                else if (taken > 0) Toast("SCAVENGERS GOT HERE FIRST - THE WRECK IS PICKED OVER");
                if (taken > 0) Journal.Add("NEWS", "THE WRECK AT " + Mathf.RoundToInt(w.x) + "," + Mathf.RoundToInt(w.z) + " WAS PICKED OVER (" + taken + " PARTS GONE)");
            }
        }

        /// <summary>The scavenger leaves with what nobody bought: it turns up on the salvage stalls of the town nearest
        /// the wreck the next day (<c>Trade.HaulToMarket</c>), and the news goes round.</summary>
        public int HaulOff(string scav, Vector3 at)
        {
            MadMax.World.Settlement town = null; float td = float.MaxValue;
            if (World != null) foreach (var st in World.settlements) { float d = Vector2.Distance(st.pos, new Vector2(at.x, at.z)); if (d < td) { td = d; town = st; } }
            int n = MadMax.Npc.Trade.HaulToMarket(scav, town, at);
            if (n > 0 && town != null) Journal.Add("NEWS", "SALVAGE OFF THE WRECK AT " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + " IS GOING TO " + MadMax.Npc.Market.TownName(town) + " MARKET");
            return n;
        }

        public static string ScavengerId(Vector3 at) => "scav:" + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z);

        /// <summary>Strip the wreck nearest <paramref name="at"/> by its age: mounted and loose parts, its storage and
        /// fuel, paced by the season (<see cref="ScavengedShare(float, int)"/>). With a <paramref name="haul"/> id the
        /// scavenger still on site keeps what was taken as trade stock (<c>Trade.Hauls</c>); otherwise it is gone.
        /// Returns the parts taken.</summary>
        public int Scavenge(Vector3 at, float age, int seed, string haul = null)
        {
            int season = MadMax.World.Weather.Season;
            float share = ScavengedShare(age, season, TrafficPace(at));
            if (share <= 0f) return 0;
            VehicleDriver v = null; float bd = 12f;
            foreach (var x in wrecks) if (x) { float d = Flat(x.transform.position - at); if (d < bd) { bd = d; v = x; } }
            if (v && BurnedNear(v.transform.position)) return 0;                                    // burned out this week: nobody wants it
            var rnd = new System.Random(seed);
            int taken = 0;
            if (v && v.TryGetComponent<VehicleChassis>(out var chassis))
                foreach (var s in chassis.Sockets)
                {
                    var part = s.Current;
                    if (!part) continue;
                    float k = part.category == PartCategory.Engine ? share * 0.6f : share;           // engines are heavy work
                    if (rnd.NextDouble() < k) { var p = s.Detach(false); if (p) { Keep(haul, "part:" + p.partId, 1); Destroy(p.gameObject); taken++; } }
                }
            var loose = new List<VehiclePart>();
            foreach (var p in VehiclePart.Registry) if (p && !p.IsMounted && Flat(p.transform.position - at) < 10f) loose.Add(p);
            foreach (var p in loose) if (rnd.NextDouble() < share) { Keep(haul, "part:" + p.partId, 1); Destroy(p.gameObject); taken++; }
            if (v)
            {
                var storage = VehicleStorage.For(v);
                if (storage)
                    foreach (var c in storage.compartments)
                    {
                        if (!c || !c.container) continue;
                        var inv = c.container.inventory;
                        var res = inv.ResourceArray;
                        for (int r = 0; r < res.Length; r++) { int left = Mathf.FloorToInt(res[r] * (1f - share)); Keep(haul, "res:" + r, res[r] - left); res[r] = left; }
                        var items = new List<KeyValuePair<string, int>>();
                        foreach (var kv in inv.Items) { int n = Mathf.FloorToInt(kv.Value * (1f - share)); Keep(haul, kv.Key, kv.Value - n); if (n > 0) items.Add(new KeyValuePair<string, int>(kv.Key, n)); }
                        inv.Restore(res, items);
                    }
                if (FuelFrozen(season) && v.TryGetComponent<VehicleSystems>(out var ice)) ice.IceTank();
                else if (v.TryGetComponent<VehicleSystems>(out var sys))
                {
                    float gone = sys.fuel * share;
                    sys.fuel -= gone;
                    if (sys.tankKind != ResourceType.None) Keep(haul, "res:" + (int)sys.tankKind, Mathf.FloorToInt(gone));
                }
            }
            return taken;
        }

        /// <summary>A stripped good goes to the scavenger's haul when one is on site (keys and story items never).</summary>
        static void Keep(string haul, string id, int n)
        {
            if (haul == null || n <= 0 || ItemIds.IsCarKey(id) || id == "res:" + (int)ResourceType.Scrap) return;   // scrap is their money
            MadMax.Npc.Trade.AddHaul(haul, id, n);
        }

        void SaveHome(SaveData d)
        {
            d.roadWrecks = new List<Vector4>(RoadWrecks);
            d.scavHauls = MadMax.Npc.Trade.SaveHauls();
            SaveRoadside(d);
            SaveNightly1013(d);
        }

        void LoadHome(SaveData d)
        {
            RoadWrecks.Clear();
            if (d.roadWrecks != null) RoadWrecks.AddRange(d.roadWrecks);
            MadMax.Npc.Trade.LoadHauls(d.scavHauls);
            LoadRoadside(d);
            LoadNightly1013(d);
        }
    }
}
