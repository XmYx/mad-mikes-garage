using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-18: washing as a moment (getting clean throws a splash off the body, the flies
    /// scatter, and the crew says so; standing in the rain under open sky now really rinses you), and the neighbour who
    /// calls in (after a few ALL WELL? calls on different days, the neighbour walks over from town the next afternoon,
    /// sits by the fire a while and leaves a gift on the ground). The chores are saved by their pieces (latrine cover,
    /// stable bedding, slop bucket); animals bedding down, puddle birds and the slop bucket live in
    /// <c>Animals/Animal.Bedding</c>, <c>AnimalDirector.Puddles</c>, <c>Building/SlopBucket</c>.</summary>
    public partial class WastelandGame
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset1018() { LastWashLine = null; LastCallIn = null; WashMoments = 0; }

        // ------------------------------------------------------------------ washing as a moment

        /// <summary>The crew's word after the player washed (tests).</summary>
        public static string LastWashLine;
        /// <summary>Washes that got rid of the flies this session (tests).</summary>
        public static int WashMoments;
        /// <summary>Hygiene points a minute standing in the rain under open sky (up to <see cref="RainCleanTo"/>).</summary>
        public const float RainWashPerMinute = 6f, RainCleanTo = 70f;
        float washWas = -1f, washTickAt = -1f;
        int crewWashDay = -1;

        static readonly string[] WashLines =
        {
            "BETTER. MUCH BETTER. I CAN BREATHE AGAIN.", "LOOK AT THAT - THE FLIES HAVE GONE AND FOUND SOMEONE ELSE.",
            "THERE'S A FACE UNDER ALL THAT. WHO KNEW.", "NOW YOU SMELL LIKE A PERSON. ALMOST.",
        };

        /// <summary>The wash that brings the player back above the flies' line: splash, flies off, a word from the crew.</summary>
        public void WashMoment()
        {
            if (!Player) return;
            WashMoments++;
            var at = Player.transform.position;
            MadMax.Audio.Sfx.Play("splash", at, 0.5f, 1.15f, 12f);
            var splash = new GameObject("WashSplash");
            splash.transform.position = at;
            var drops = Droplets.Attach(splash, 36, (int i, out Vector3 p, out Vector3 v) =>
            {
                var dir = Quaternion.Euler(0f, i * 47f + Random.Range(-15f, 15f), 0f) * Vector3.forward;
                p = dir * 0.2f + Vector3.up * Random.Range(0.6f, 1.6f);
                v = dir * Random.Range(1.2f, 2.4f) + Vector3.up * Random.Range(0.4f, 1.6f);
            }, new Color(0.72f, 0.86f, 1f), 0.045f, 0.9f);
            drops.emitting = true;
            Destroy(splash, 2.5f);
            StartCoroutine(StopSplash(drops));
            for (int i = 0; i < 9; i++)                                                            // the flies scatter
            {
                var dir = Quaternion.Euler(Random.Range(-20f, 40f), i * 40f, 0f) * Vector3.forward;
                Fx.Streak(at + Vector3.up * 1.5f + dir * 0.3f, dir * Random.Range(2.5f, 4.5f), 0.03f, new Color(0.08f, 0.08f, 0.06f), 0.7f);
            }
            if (crewWashDay == DayNight.Day) return;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !n.companion || Flat(n.transform.position - at) > 15f) continue;
                crewWashDay = DayNight.Day;
                LastWashLine = WashLines[(DayNight.Day + n.Profile.id.Length) % WashLines.Length];
                Remark(n, LastWashLine, 4f);
                if (n.State != null) n.State.disposition = Mathf.Min(100, n.State.disposition + 1);
                break;
            }
        }

        System.Collections.IEnumerator StopSplash(Droplets d)
        {
            yield return new WaitForSeconds(0.45f);
            if (d) d.emitting = false;
        }

        /// <summary>From <c>Wash</c> / <c>Bathe</c>: hygiene went from <paramref name="was"/> to now — the moment if it
        /// crossed the flies' line.</summary>
        void Washed(float was)
        {
            if (was < FlyHygiene && Stats.hygiene >= FlyHygiene) WashMoment();
            washWas = Stats.hygiene;
        }

        /// <summary>Ticked about once a second (the roadside chain): the time since the last tick is the step.</summary>
        void UpdateWashing()
        {
            if (Stats == null || !Player) { washTickAt = -1f; return; }
            float dt = washTickAt < 0f ? 0f : Mathf.Min(5f, Time.time - washTickAt);
            washTickAt = Time.time;
            // rain rinses a player standing out in it (not in a vehicle, not under a roof)
            if (Weather.Raining && !Current && !Sheltered && Stats.hygiene < RainCleanTo && Weather.TemperatureAt(Player.transform.position.z) > 1f)
                Stats.hygiene = Mathf.Min(RainCleanTo, Stats.hygiene + dt * RainWashPerMinute / 60f * Mathf.Max(0.6f, Weather.Wetness));
            float h = Stats.hygiene;
            if (washWas >= 0f && washWas < FlyHygiene && h >= FlyHygiene) WashMoment();
            washWas = h;
        }

        // ------------------------------------------------------------------ the neighbour calls in

        /// <summary>ALL WELL? calls (on different days) after which the neighbour comes over in person.</summary>
        public const int CallInAfter = 3;
        public const float CallInSeconds = 90f, CallInFrom = 13f, CallInTo = 17.5f, CallInHome = 40f, CallInWalk = 32f;
        public class CallIn { public int calls, lastDay = -1, due = -1, came = -1; }
        /// <summary>Neighbour id → their calls and visits.</summary>
        public readonly Dictionary<string, CallIn> CallIns = new Dictionary<string, CallIn>();
        /// <summary>What happened at the last call-in (tests).</summary>
        public static string LastCallIn;
        /// <summary>The neighbour on their way or sitting with the player (null: none).</summary>
        public MadMax.Npc.Npc CallingIn { get; private set; }
        float callInT;

        string ErrandKey(NeighbourErrand e)
        {
            foreach (var kv in NeighbourErrands) if (kv.Value == e) return kv.Key;
            return null;
        }

        /// <summary>From <c>AskAllWell</c>: an answered call; the third day's call has the neighbour promise a visit.</summary>
        public void NoteAllWell(NeighbourErrand w)
        {
            string key = w != null ? ErrandKey(w) : null;
            if (key == null) return;
            if (!CallIns.TryGetValue(key, out var c)) CallIns[key] = c = new CallIn();
            if (c.lastDay == DayNight.Day) return;                                                  // one a day counts
            c.lastDay = DayNight.Day;
            c.calls++;
            if (c.calls < CallInAfter || c.due >= 0 || c.came == DayNight.Day) return;
            c.due = DayNight.Day + 1;
            string who = w.name ?? "YOUR NEIGHBOUR";
            Toast(who + ": \"ALL THIS CALLING - I'LL WALK OVER TOMORROW AFTERNOON AND SEE THE PLACE MYSELF\"");
            Journal.Add("HOME", who + " WILL CALL IN TOMORROW AFTERNOON");
        }

        /// <summary>The spot a visitor heads for at <paramref name="claim"/>: the fire (campfire / fireplace / stove), else a
        /// seat, else beside the flag.</summary>
        public Vector3 CallInSpot(ClaimFlag claim)
        {
            var c = claim.transform.position;
            Placeable best = null; float bd = 25f * 25f;
            foreach (var p in Placeable.All)
            {
                if (!p || (p.id != "campfire" && p.id != "fireplace" && p.id != "stove" && p.id != "kitchen_range")) continue;
                float d = (p.transform.position - c).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best) return best.transform.position;
            foreach (var s in Object.FindObjectsByType<Seat>(FindObjectsSortMode.None))
                if (s && !s.standing && !s.hidden && s.ride == null && (s.transform.position - c).sqrMagnitude < 20f * 20f) return s.transform.position;
            return c + claim.transform.right * 2f;
        }

        void UpdateCallIns()
        {
            if (Time.time < callInT) return;
            callInT = Time.time + 3f;
            if (CallingIn) return;
            CallingIn = null;
            if (CallIns.Count == 0 || !Player || Current || Weather.Raining || DayNight.Hours < CallInFrom || DayNight.Hours > CallInTo) return;
            foreach (var kv in CallIns)
            {
                var c = kv.Value;
                if (c.due < 0 || c.due > DayNight.Day) continue;
                var w = NeighbourErrands.TryGetValue(kv.Key, out var e) ? e : null;
                if (w == null || w.state != 2) { c.due = -1; continue; }
                ClaimFlag home = null;
                foreach (var f in ClaimFlag.All) if (f && f.Owner == Stats.name && Flat(f.transform.position - Player.transform.position) < CallInHome) { home = f; break; }
                if (!home) continue;
                StartCallIn(kv.Key, w, c, home);
                return;
            }
        }

        /// <summary>The neighbour <paramref name="key"/> walks over to <paramref name="claim"/> (tests call it directly);
        /// null when they can't come (dead, unknown).</summary>
        public MadMax.Npc.Npc StartCallIn(string key, NeighbourErrand w, CallIn c, ClaimFlag claim)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("r") || NpcRegistry.IsDead(key) || World == null) { if (c != null) c.due = -1; return null; }
            var profile = NpcProfile.Make(key, NpcRole.Resident, NpcDirector.StableKey(key) ^ World.seed);
            var spot = CallInSpot(claim);
            Vector3 from = spot + Vector3.forward * CallInWalk;
            if (w != null && w.town >= 0 && w.town < World.settlements.Count)
            {
                var t = World.settlements[w.town].pos;
                var dir = new Vector3(t.x - spot.x, 0f, t.y - spot.z);
                if (dir.sqrMagnitude > 1f) from = spot + dir.normalized * CallInWalk;
            }
            var terrain = DeformableTerrain.Instance;
            if (terrain) from.y = terrain.Height(from.x, from.z) + 0.1f;
            var n = MadMax.Npc.Npc.Spawn(profile, from, Quaternion.LookRotation(Flat3(spot - from)).eulerAngles.y, null, propMaterial);
            if (!n) return null;
            CallingIn = n;
            string name = w != null && w.name != null ? w.name : profile.Name;
            n.Visit(spot, CallInSeconds, v =>
            {
                LastCallIn = name + " CAME OVER";
                Remark(v, "SO THIS IS THE PLACE I'VE BEEN WATCHING. NOT BAD, " + Stats.name + ". NOT BAD AT ALL.", 5f);
            }, v => LeaveGift(v, name, c));
            if (c != null) { c.due = -1; c.came = DayNight.Day; c.calls = 0; }
            Toast(name + " IS WALKING UP FROM THE ROAD");
            return n;
        }

        static Vector3 Flat3(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }

        void LeaveGift(MadMax.Npc.Npc n, string name, CallIn c)
        {
            if (!n) return;
            var rnd = new System.Random(Market.Seed(seed, DayNight.Day * 13 + n.Profile.id.Length));
            string gift = VisitFood[rnd.Next(VisitFood.Length)];
            if (FoodLibrary.Get(gift) == null) gift = "food_bread";
            var at = n.transform.position + n.transform.forward * 0.6f + Vector3.up * 0.3f;
            WorldItem.Create(gift, 1, -1f, propMaterial, at, Quaternion.Euler(0f, rnd.Next(360), 0f));
            var s = NpcRegistry.Get(n.Profile);
            s.Set(NpcSave.Met);
            s.disposition = Mathf.Min(100, s.disposition + 5);
            Remark(n, "I'LL LEAVE YOU THIS. KEEP THE LAMP ON, " + Stats.name + ".", 4.5f);
            LastCallIn = name + " LEFT " + ItemIds.Name(gift) + " BY THE FIRE";
            Toast(LastCallIn);
            Journal.Add("HOME", LastCallIn + " AND WALKED HOME");
            Remark(n, "AND COME TO SUPPER SOME EVENING - YOU KNOW WHERE I LIVE.", 4.5f);
            InviteToSupper(n.Profile.id, name);                                                     // the visit goes both ways (Nightly1023)
            CallingIn = null;
        }

        // ------------------------------------------------------------------ tick + save

        void UpdateNightly1018()
        {
            UpdateWashing();
            UpdateCallIns();
            UpdateNightly1023();
        }

        void Save1018(List<string> lines)
        {
            foreach (var kv in CallIns)
                lines.Add("q|" + kv.Key + "|" + kv.Value.calls + "|" + kv.Value.lastDay + "|" + kv.Value.due + "|" + kv.Value.came);
            Save1023(lines);
        }

        void Load1018(string[] f)
        {
            if (f[0] == "v" || f[0] == "y") { Load1023(f); return; }
            if (f[0] != "q" || f.Length < 6) return;
            var c = new CallIn();
            int.TryParse(f[2], out c.calls); int.TryParse(f[3], out c.lastDay); int.TryParse(f[4], out c.due); int.TryParse(f[5], out c.came);
            CallIns[f[1]] = c;
        }

        void Clear1018()
        {
            CallIns.Clear();
            if (CallingIn) Destroy(CallingIn.gameObject);
            CallingIn = null;
            washWas = -1f; crewWashDay = -1;
            Clear1023();
        }
    }
}
