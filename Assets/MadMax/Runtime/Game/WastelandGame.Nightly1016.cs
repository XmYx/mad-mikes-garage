using System.Collections.Generic;
using System.Globalization;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-16: flies follow rot (carcasses past their first hours, rotten food on the
    /// ground, a player who hasn't washed), food left out in the open spoils (faster in the heat), and the favour
    /// returned — a neighbour whose errand was done keeps an eye on the player's base and radios when raiders are coming
    /// or a column passes, and turns out with their neighbours when a raid hits while the player is away. Sawdust, bees on
    /// the beds, TV programmes and watchers live in <c>Building/Sawdust</c>, <c>Beehive</c>, <c>TvSet</c>, <c>Npc/Npc.Tv</c>.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ flies follow rot

        float fliesAt, spoilAt = -1f;   // Time.time stamps: this chain ticks at 1 Hz (UpdateRoadside), not per frame
        Swarm playerFlies;
        /// <summary>The flies that follow an unwashed player (tests).</summary>
        public Swarm PlayerFlySwarm => playerFlies;
        /// <summary>Below this hygiene the player draws flies (they thicken toward 0).</summary>
        public const float FlyHygiene = 30f;
        /// <summary>Food on the ground spoils this many times faster than in a pack (sun, ants, damp; ×2 more above 25 °C).</summary>
        public const float OpenSpoil = 1.5f;
        readonly Dictionary<WorldItem, float> groundSpoil = new Dictionary<WorldItem, float>();
        readonly List<WorldItem> spoilDone = new List<WorldItem>();
        /// <summary>Food items that rotted on the ground this session (tests).</summary>
        public static int RottedOnGround;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset1016() { RottedOnGround = 0; LastNeighbourWatch = null; }

        /// <summary>How thick the flies are over rot of <paramref name="ageDays"/> at <paramref name="at"/> (0..1): none in
        /// the cold, fewer at night and in the rain.</summary>
        public static float FlyLevel(float ageDays, Vector3 at)
        {
            if (Weather.TemperatureAt(at.z) < 4f) return 0f;
            float a = Mathf.Clamp01((ageDays - 0.25f) / 1.25f);
            if (a <= 0f) return 0f;
            if (DayNight.Darkness > 0.5f) a *= 0.3f;
            if (Weather.Raining) a *= 0.5f;
            return a;
        }

        /// <summary>Flies over the player's own smell (0..1).</summary>
        public float PlayerFlies()
        {
            if (!Player || Current || Stats == null || Stats.hygiene >= FlyHygiene) return 0f;
            if (Weather.TemperatureAt(Player.transform.position.z) < 4f || Player.HeadUnder) return 0f;
            float a = 0.35f + 0.65f * (FlyHygiene - Stats.hygiene) / FlyHygiene;
            if (DayNight.Darkness > 0.5f) a *= 0.3f;
            if (Weather.Raining) a *= 0.5f;
            return a;
        }

        void UpdateFlies()
        {
            if (Time.time < fliesAt) return;
            fliesAt = Time.time + 2f;
            foreach (var c in Carcass.All)
            {
                if (!c || c.GetComponent<Swarm>()) continue;
                var cc = c; float s = Mathf.Clamp(c.size, 0.4f, 2.5f);
                Swarm.Attach(c.gameObject, Swarm.Kind.Flies, new Vector3(0f, 0.35f * s, 0f), 0.45f * s, Mathf.Clamp(Mathf.RoundToInt(5f * s), 3, 9),
                    () => cc ? FlyLevel(DayNight.TotalDays - cc.diedAt, cc.transform.position) : 0f);
            }
            foreach (var w in WorldItem.All)
            {
                if (!w || w.key != "food_rotten" || w.GetComponent<Swarm>()) continue;
                var ww = w;
                Swarm.Attach(w.gameObject, Swarm.Kind.Flies, new Vector3(0f, 0.18f, 0f), 0.22f, 3, () => ww ? FlyLevel(2f, ww.transform.position) : 0f);
            }
            if (Player && !playerFlies)
            {
                playerFlies = Swarm.Attach(new GameObject("PlayerFlies"), Swarm.Kind.Flies, new Vector3(0f, 1.45f, 0f), 0.42f, 5, PlayerFlies);
                playerFlies.follow = Player.transform;
            }
        }

        /// <summary>Food lying out (a dropped or placed item, not in a container or on a vehicle) spoils like food in the pack,
        /// <see cref="OpenSpoil"/> times faster; the item turns into ROTTEN FOOD where it lies (host only).</summary>
        void UpdateGroundSpoil()
        {
            if (spoilAt < 0f || spoilAt > Time.time) spoilAt = Time.time;
            if (Time.time - spoilAt < 5f) return;
            float dt = Mathf.Min(Time.time - spoilAt, 60f); spoilAt = Time.time;
            if (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) return;
            spoilDone.Clear();
            foreach (var w in WorldItem.All)
            {
                if (!w || w.IsResource || w.OnVehicle || w.key == "food_rotten") continue;
                var f = FoodLibrary.Get(w.key);
                if (f == null || f.spoilMinutes <= 0f || f.spoilsTo != "food_rotten") continue;
                float heat = Weather.TemperatureAt(w.transform.position.z);
                float k = OpenSpoil * (heat > 25f ? 2f : heat < 4f ? 0.25f : 1f);
                groundSpoil.TryGetValue(w, out float acc);
                acc += dt * k / (f.spoilMinutes * 60f);
                groundSpoil[w] = acc;
                if (acc >= 1f) spoilDone.Add(w);
            }
            foreach (var w in spoilDone) RotOnGround(w);
            if (groundSpoil.Count > 64) { var dead = new List<WorldItem>(); foreach (var kv in groundSpoil) if (!kv.Key) dead.Add(kv.Key); foreach (var d in dead) groundSpoil.Remove(d); }
        }

        /// <summary>Turn a food item on the ground into rotten food (same count).</summary>
        public void RotOnGround(WorldItem w)
        {
            if (!w) return;
            groundSpoil.Remove(w);
            w.key = "food_rotten";
            w.Refresh();
            RottedOnGround++;
        }

        // ------------------------------------------------------------------ the favour returned: a neighbour's watch

        /// <summary>A neighbour watches a claim when their town is within this many metres of it.</summary>
        public const float WatchReach = 3000f;
        /// <summary>The warning comes this long (days) before a raid is due — two hours ahead of WasteTalk's.</summary>
        public const float WatchAhead = 3f / 24f;
        public const float WatchColumnReach = 160f, PlayerAwayFrom = 250f;
        /// <summary>Defence a watching neighbour adds when a raid is fought out while the player is away.</summary>
        public const int WatchDefence = 3;
        float watchWarned = -1f, watchAt;
        int watchPassDay = -1;
        /// <summary>The last word a watching neighbour sent (tests).</summary>
        public static string LastNeighbourWatch;

        /// <summary>Neighbours whose favour was done and whose town is close enough to watch <paramref name="claim"/>.</summary>
        public List<NeighbourErrand> Watchers(ClaimFlag claim)
        {
            var list = new List<NeighbourErrand>();
            if (!claim || World == null) return list;
            var c = new Vector2(claim.transform.position.x, claim.transform.position.z);
            foreach (var e in NeighbourErrands.Values)
                if (e.state == 2 && e.town >= 0 && e.town < World.settlements.Count && Vector2.Distance(World.settlements[e.town].pos, c) < WatchReach) list.Add(e);
            return list;
        }

        /// <summary>The player's own claim the raiders would go for (null: none).</summary>
        ClaimFlag WatchedClaim()
        {
            var c = BaseRaid.TargetClaim();
            return c && c.Owner == Stats.name ? c : null;
        }

        void UpdateNeighbourWatch()
        {
            if (Time.time < watchAt) return;
            watchAt = Time.time + 5f;
            if (NeighbourErrands.Count == 0 || !Player || (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient)) return;
            var claim = WatchedClaim();
            if (!claim) return;
            WatchTick(claim, Current ? Current.transform.position : Player.transform.position);
        }

        /// <summary>One look by the watching neighbours at <paramref name="claim"/> with the player at <paramref name="me"/>:
        /// the word sent (null: nothing to say).</summary>
        public string WatchTick(ClaimFlag claim, Vector3 me)
        {
            if (!claim || Vector3.Distance(me, claim.transform.position) < PlayerAwayFrom) return null;
            var who = Watchers(claim);
            if (who.Count == 0) return null;
            var w = who[0];
            var d = NpcDirector.Instance;
            // a raid due within a few hours: the gang's scouts have been seen
            if (BaseRaid.NextDay > 0f && watchWarned != BaseRaid.NextDay && BaseRaid.NextDay - DayNight.TotalDays < WatchAhead && BaseRaid.NextDay > DayNight.TotalDays)
            {
                var gang = d ? d.RaidersByRoad(claim.transform.position) : null;
                watchWarned = BaseRaid.NextDay;
                if (gang != null && gang.SparesBases) return null;
                string g = gang != null ? "THE " + gang.Gang : "RAIDERS";
                NeighbourWord(w, g + " SCOUTS WERE ON THE ROAD BY YOUR PLACE. I'D SAY THEY COME " + (DayNight.Hours > 14f ? "TONIGHT" : "TODAY") + ". I'LL KEEP AN EYE OUT", claim, true);
                return LastNeighbourWatch;
            }
            // a raider column passing close by
            if (d && watchPassDay != DayNight.Day)
            {
                var col = d.NearestRaiders(claim.transform.position, out float dist);
                if (col != null && !col.Friendly && dist < WatchColumnReach)
                {
                    watchPassDay = DayNight.Day;
                    NeighbourWord(w, "A " + col.Gang + " COLUMN JUST WENT PAST YOUR PLACE. DIDN'T STOP. YET", claim, false);
                    return LastNeighbourWatch;
                }
            }
            return null;
        }

        void NeighbourWord(NeighbourErrand w, string line, ClaimFlag claim, bool urgent)
        {
            string msg = "HOME FREQUENCY: " + (w.name ?? "YOUR NEIGHBOUR") + ": \"" + line + "\"";
            LastNeighbourWatch = msg;
            SendHomeCall(msg, claim.transform.position);
            if (urgent) { SetWaypoint(claim.transform.position, "HOME", true); MadMax.Audio.Sfx.Play2D("beep", 0.5f, 0.8f); }
        }

        /// <summary>BaseRaid.NeighbourHelp: watching neighbours turn out when a raid is fought while the player is away.</summary>
        int NeighbourHelp(ClaimFlag claim, out string who)
        {
            who = null;
            var list = Watchers(claim);
            if (list.Count == 0) return 0;
            who = (list[0].name ?? "YOUR NEIGHBOUR") + (list.Count > 1 ? " AND THE NEIGHBOURS" : "");
            return WatchDefence * Mathf.Min(2, list.Count);
        }

        // ------------------------------------------------------------------ tick, save

        void UpdateNightly1016()
        {
            if (BaseRaid.NeighbourHelp == null || !ReferenceEquals(BaseRaid.NeighbourHelp.Target, this)) BaseRaid.NeighbourHelp = NeighbourHelp;
            UpdateFlies();
            UpdateGroundSpoil();
            UpdateNeighbourWatch();
            UpdateNightly1017();
        }

        void Save1016(List<string> lines)
        {
            lines.Add("x|" + watchWarned.ToString("R", CultureInfo.InvariantCulture) + "|" + watchPassDay);
            Save1018(lines);
        }

        void Load1016(string[] f)
        {
            if (f[0] == "x" && f.Length >= 3)
            {
                float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out watchWarned);
                int.TryParse(f[2], out watchPassDay);
            }
            else Load1018(f);
        }

        void Clear1016()
        {
            watchWarned = -1f; watchPassDay = -1; groundSpoil.Clear();
            BaseRaid.NeighbourHelp = NeighbourHelp;
            Clear1018();
        }
    }
}
